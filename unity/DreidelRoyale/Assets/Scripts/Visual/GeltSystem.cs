using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DreidelRoyale.Visual
{
    /// <summary>
    /// The pot as physical objects. Coins take an impulse radiating from the dreidel, bounce
    /// on the play surface, and once past its edge they simply keep falling. Each face gets
    /// its own verb: GIMEL scatters the pot, HEI cleaves it toward the player, SHIN pays coins
    /// in from the player's side.
    /// </summary>
    public class GeltSystem
    {
        public const float GELT_G = 26f;        // gravity, world units/s^2 — tuned to the 1.6-unit dreidel
        public const float GELT_REST = 0.42f;   // bounce
        public const float GELT_FLOOR = -14f;   // out of sight, on the floor

        class Body
        {
            public Transform M;
            public Vector3 V, W;
            public bool Rest, Edge, Incoming, Euler;
            public float Tilt, Prec;
        }

        readonly SceneRig _rig;
        readonly List<Body> _bodies = new List<Body>();
        readonly List<Body> _flight = new List<Body>();

        string _mode = "idle";
        float _t;
        int? _pending;

        public Action<float> OnClink;                 // coin-on-coin, 0..1 strength
        public Action<string, float> OnCoinLand;      // surface ("wood"/"floor"), strength
        public Action<float> OnEuler;                 // an Euler's-disk spin-down began, with its duration
        public Action<Vector3> OnFlightGone;          // a coin reached the viewer

        public GeltSystem(SceneRig rig) { _rig = rig; }

        public bool Idle { get { return _mode == "idle"; } }

        static void GoHome(Transform m)
        {
            var h = m.GetComponent<CoinHome>();
            if (h != null) h.GoHome();
            m.localScale = Vector3.one;
        }

        public void SetPotCoins(int n)
        {
            if (_mode != "idle") { _pending = n; return; }   // don't yank coins out of mid-air
            _rig.SetPotCoinsVisible(n);
        }

        /// <summary>GIMEL. A harder slam throws the pot further.</summary>
        public bool Scatter(float power = 0.5f)
        {
            if (_mode != "idle") return false;
            var live = _rig.PotCoins.Where(m => m.gameObject.activeSelf).ToList();
            if (live.Count == 0) return false;
            _bodies.Clear();
            foreach (var m in live)
            {
                var dir = new Vector3(m.localPosition.x, 0, m.localPosition.z);
                if (dir.sqrMagnitude < 1e-4f) dir = new Vector3(Random.value - 0.5f, 0, Random.value - 0.5f);
                dir.Normalize();
                // higher coins in a stack get more of the kick — the tower topples outward
                float lift = 0.6f + m.localPosition.y * 0.9f;
                float sp = (2.8f + Random.value * 3.0f + power * 3.2f) * lift;
                _bodies.Add(new Body
                {
                    M = m,
                    V = new Vector3(dir.x * sp, (4.2f + Random.value * 3.6f + power * 2.8f) * lift, dir.z * sp),
                    W = new Vector3((Random.value - 0.5f) * 24f, (Random.value - 0.5f) * 16f, (Random.value - 0.5f) * 24f),
                    Rest = false,
                    Edge = Random.value < 0.25f        // candidate for the Euler's-disk spin-down
                });
            }
            _mode = "flying"; _t = 0f;
            return true;
        }

        /// <summary>
        /// HEI. The pot visibly cleaves: `count` coins hop toward the player (+Z is the front,
        /// where the default camera lives). The rest stay put.
        /// </summary>
        public bool Cleave(int count, float power = 0.5f)
        {
            if (_mode != "idle") return false;
            var live = _rig.PotCoins.Where(m => m.gameObject.activeSelf).ToList();
            if (live.Count == 0 || count <= 0) return false;
            // take the coins nearest the player so the split reads spatially
            var taken = live.OrderByDescending(m => m.localPosition.z).Take(Mathf.Min(count, live.Count));
            _bodies.Clear();
            foreach (var m in taken)
            {
                float sp = 1.6f + Random.value * 1.2f + power * 1.2f;
                _bodies.Add(new Body
                {
                    M = m,
                    V = new Vector3((Random.value - 0.5f) * 1.4f, 2.6f + Random.value * 1.6f, sp),
                    W = new Vector3((Random.value - 0.5f) * 16f, (Random.value - 0.5f) * 10f, (Random.value - 0.5f) * 16f),
                    Rest = false
                });
            }
            _mode = "flying"; _t = 0f;
            return true;
        }

        /// <summary>
        /// SHIN/PEI. The pot grows by an object arriving: coins arc in from the player's side,
        /// land by the stacks, bounce, and settle where the next pot update will want them.
        /// </summary>
        public bool PayIn(int count)
        {
            if (_mode != "idle" || count <= 0) return false;
            var hidden = _rig.PotCoins.Where(m => !m.gameObject.activeSelf).ToList();
            if (hidden.Count == 0) return false;
            _bodies.Clear();
            int n = Mathf.Min(count, hidden.Count);
            for (int i = 0; i < n; i++)
            {
                var m = hidden[i];
                var home = m.GetComponent<CoinHome>();
                var tgt = home != null ? home.P : Vector3.zero;
                m.gameObject.SetActive(true);
                m.localScale = Vector3.one;
                m.localPosition = new Vector3(tgt.x + (Random.value - 0.5f) * 0.4f, 1.6f + i * 0.28f, 5.6f + i * 0.5f);
                // ballistic arc solved for the home spot: v = (target - start - 1/2 g t^2)/t
                float tf = 0.62f + i * 0.07f;
                var v = (tgt - m.localPosition) / tf;
                v.y += 0.5f * GELT_G * tf;
                _bodies.Add(new Body
                {
                    M = m, V = v,
                    W = new Vector3((Random.value - 0.5f) * 14f, 0, (Random.value - 0.5f) * 14f),
                    Rest = false, Incoming = true
                });
            }
            _mode = "flying"; _t = 0f;
            return true;
        }

        /// <summary>
        /// Real coins that leap off the pot and arc toward the camera, growing slightly as
        /// they come, then vanish "into your hand". The HUD is pinged on each arrival.
        /// </summary>
        public int FlyOut(int count)
        {
            var free = _rig.FlightPool.Where(m => !m.gameObject.activeSelf).ToList();
            int n = Mathf.Min(count, free.Count);
            if (n == 0) return 0;
            var camLocal = _rig.World.InverseTransformPoint(_rig.Cam.transform.position);
            for (int i = 0; i < n; i++)
            {
                var m = free[i];
                var srcCoin = _rig.PotCoins[Random.Range(0, _rig.PotCoins.Count)].GetComponent<CoinHome>();
                var src = srcCoin != null ? srcCoin.P : Vector3.zero;
                m.gameObject.SetActive(true);
                m.localScale = Vector3.one;
                m.localPosition = new Vector3(src.x + (Random.value - 0.5f) * 0.3f, 0.3f, src.z + (Random.value - 0.5f) * 0.3f);
                float tf = 0.55f + i * 0.09f;
                var v = (camLocal - m.localPosition) / tf;
                v.y += 0.5f * GELT_G * tf * 0.35f;      // a shallower arc — a toss, not a mortar
                _flight.Add(new Body
                {
                    M = m, V = v, Tilt = 0f, Prec = tf,   // Tilt reused as elapsed, Prec as flight time
                    W = new Vector3((Random.value - 0.5f) * 18f, 0, (Random.value - 0.5f) * 18f)
                });
            }
            return n;
        }

        public void FlightStep(float dt)
        {
            for (int i = _flight.Count - 1; i >= 0; i--)
            {
                var b = _flight[i];
                b.Tilt += dt;
                b.V = new Vector3(b.V.x, b.V.y - GELT_G * 0.35f * dt, b.V.z);
                b.M.localPosition += b.V * dt;
                b.M.localRotation *= Quaternion.Euler(b.W.x * dt * Mathf.Rad2Deg, 0, b.W.z * dt * Mathf.Rad2Deg);
                float k = b.Tilt / b.Prec;
                b.M.localScale = Vector3.one * (1f + k * 0.6f);   // grows as it nears the eye
                if (k >= 0.88f)
                {
                    b.M.gameObject.SetActive(false);
                    _flight.RemoveAt(i);
                    if (OnFlightGone != null) OnFlightGone(b.M.position);
                }
            }
        }

        public void Step(float dt)
        {
            FlightStep(dt);
            if (_mode == "idle") return;
            _t += dt;

            if (_mode == "flying")
            {
                const float R = 19f - 0.32f;
                int moving = 0;
                foreach (var b in _bodies)
                {
                    if (!b.M.gameObject.activeSelf || b.Rest) continue;
                    b.V = new Vector3(b.V.x, b.V.y - GELT_G * dt, b.V.z);
                    b.M.localPosition += b.V * dt;
                    b.M.localRotation *= Quaternion.Euler(b.W.x * dt * Mathf.Rad2Deg,
                                                          b.W.y * dt * Mathf.Rad2Deg,
                                                          b.W.z * dt * Mathf.Rad2Deg);

                    // Euler's-disk spin-down: an on-edge coin whose wobble accelerates as it
                    // flattens — tilt decays while the precession races, then it flops flat.
                    if (b.Euler)
                    {
                        b.Tilt *= (1f - 1.7f * dt);
                        b.Prec += dt * (5f + 34f * (1f - b.Tilt / 1.35f));
                        b.M.localRotation = Quaternion.Euler(0, b.Prec * Mathf.Rad2Deg, 0)
                                          * Quaternion.Euler(b.Tilt * Mathf.Rad2Deg, 0, 0);
                        var p = b.M.localPosition;
                        b.M.localPosition = new Vector3(p.x, 0.045f + Mathf.Sin(b.Tilt) * 0.28f, p.z);
                        if (b.Tilt < 0.11f)
                        {
                            b.Euler = false;
                            p = b.M.localPosition;
                            b.M.localPosition = new Vector3(p.x, 0.045f, p.z);
                            b.M.localRotation = Quaternion.Euler(0, b.Prec * Mathf.Rad2Deg, 0);
                            b.V = Vector3.zero; b.W = Vector3.zero;
                            if (OnCoinLand != null) OnCoinLand("wood", 0.5f);
                            b.Rest = true; continue;
                        }
                        moving++; continue;
                    }

                    float r = Mathf.Sqrt(b.M.localPosition.x * b.M.localPosition.x
                                       + b.M.localPosition.z * b.M.localPosition.z);
                    if (r < R)
                    {
                        if (b.M.localPosition.y < 0.045f && b.V.y < 0f)      // land on the surface
                        {
                            float hitV = -b.V.y;
                            var p = b.M.localPosition;
                            b.M.localPosition = new Vector3(p.x, 0.045f, p.z);
                            b.V = new Vector3(b.V.x * 0.74f, b.V.y * -GELT_REST, b.V.z * 0.74f);
                            b.W *= 0.55f;
                            if (hitV > 1.6f && OnCoinLand != null) OnCoinLand("wood", Mathf.Min(1f, hitV / 8f));

                            // an edge candidate with the right energy stands up instead of settling
                            if (b.Edge && !b.Incoming && hitV > 1.2f && hitV < 6f && _bodies.Count(x => x.Euler) < 4)
                            {
                                b.Edge = false; b.Euler = true;
                                b.Tilt = 1.35f; b.Prec = Random.value * 6.28f;
                                // exponential decay: tilt(t) = 1.35*e^(-1.7t) -> t_flop = ln(1.35/0.11)/1.7
                                float eDur = Mathf.Log(1.35f / 0.11f) / 1.7f;
                                if (OnEuler != null) OnEuler(Mathf.Min(1.6f, eDur));
                                moving++; continue;
                            }
                            if (Mathf.Abs(b.V.y) < 0.7f)                     // settled: lie flat and stop
                            {
                                b.V = Vector3.zero; b.W = Vector3.zero;
                                var e = b.M.localEulerAngles;
                                b.M.localRotation = Quaternion.Euler(0, e.y, 0);
                                if (b.Incoming) GoHome(b.M);                 // snap the last few mm home
                                b.Rest = true; continue;
                            }
                        }
                    }
                    else if (b.M.localPosition.y < GELT_FLOOR)               // over the edge and gone
                    {
                        b.M.gameObject.SetActive(false);
                        if (OnCoinLand != null) OnCoinLand("floor", 0.5f + Random.value * 0.5f);
                        continue;
                    }
                    moving++;
                }

                CoinOnCoin();

                // Hand over once the pot's been paid out and the throw has had its moment.
                if ((_pending.HasValue && _t > 1.9f) || moving == 0 || _t > 4.5f) { _mode = "collect"; _t = 0f; }
            }
            else if (_mode == "collect")
            {
                // The winner rakes it in: shrink out rather than popping. Only the coins that
                // were actually thrown — a cleave leaves the remaining pot standing; incoming
                // shin coins land and simply BECOME the pot, with no shrink at all.
                bool anyOut = _bodies.Any(b => !b.Incoming);
                float k = anyOut ? Mathf.Max(0f, 1f - _t / 0.28f) : 0f;
                if (anyOut)
                    foreach (var b in _bodies)
                        if (!b.Incoming && b.M.gameObject.activeSelf) b.M.localScale = Vector3.one * k;
                if (k <= 0f)
                {
                    _mode = "idle";
                    foreach (var b in _bodies) if (!b.Incoming) GoHome(b.M);
                    _bodies.Clear();
                    if (_pending.HasValue) { int n = _pending.Value; _pending = null; SetPotCoins(n); }
                }
            }
        }

        /// <summary>
        /// Crude sphere pushes turn a scatter into a cascade. Only near the surface — mid-air
        /// crossings read fine without it — and clinks are reported so the game layer can
        /// rate-limit the sound.
        /// </summary>
        void CoinOnCoin()
        {
            for (int i = 0; i < _bodies.Count; i++)
            {
                var A = _bodies[i];
                if (!A.M.gameObject.activeSelf || A.Euler) continue;
                for (int j = i + 1; j < _bodies.Count; j++)
                {
                    var B = _bodies[j];
                    if (!B.M.gameObject.activeSelf || B.Euler) continue;
                    if (A.M.localPosition.y > 0.5f || B.M.localPosition.y > 0.5f) continue;

                    var n = B.M.localPosition - A.M.localPosition;
                    n.y *= 0.3f;
                    float d = n.magnitude;
                    if (d <= 1e-5f || d >= 0.62f) continue;
                    n /= d;
                    float push = (0.62f - d) * 0.5f;
                    if (!A.Rest) A.M.localPosition -= n * push;
                    if (!B.Rest) B.M.localPosition += n * push;
                    float rel = Vector3.Dot(B.V - A.V, n);
                    if (rel < 0f)
                    {
                        float imp = -rel * 0.55f;
                        if (!A.Rest) A.V -= n * imp;
                        if (!B.Rest) B.V += n * imp;
                        if (A.Rest && Mathf.Abs(imp) > 0.4f) { A.Rest = false; A.V -= n * (imp * 0.6f); }
                        if (imp > 0.5f && OnClink != null) OnClink(Mathf.Min(1f, imp / 4f));
                    }
                }
            }
        }
    }
}
