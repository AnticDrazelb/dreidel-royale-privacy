using System;
using System.Collections.Generic;
using UnityEngine;
using DreidelRoyale.Core;
using Random = UnityEngine.Random;

namespace DreidelRoyale.Visual
{
    /// <summary>
    /// The 3D layer: real geometry, real light, real tumble. Owns the scene, the dreidel and
    /// the animation state machine (idle -> spin -> tumble -> rest -> recover), and exposes
    /// the same surface the web build's `D` module does so the game layer reads the same.
    /// </summary>
    public class DreidelView : MonoBehaviour
    {
        // ---- rig ----
        public SceneRig Rig;
        public DreidelRig Dreidel;
        public GeltSystem Gelt;
        public ScuffMap Scuff;
        public Camera Cam;

        PointCloud _dust, _skinBurst, _embers3d;

        /// <summary>
        /// Set by the AR layer once it exists. Everything below asks it two questions: is the
        /// diorama on a real table, and is the phone the camera?
        /// </summary>
        public System.Func<bool> ArIsOn = () => false;
        public System.Func<bool> ArIsPlaced = () => false;
        public System.Func<string> ArTableMode = () => "shadow";

        bool Ar { get { return ArIsOn(); } }

        const float DustSize = 0.24f, BurstSize = 0.16f, EmberSize = 0.09f;

        /// <summary>
        /// Point sizes are ABSOLUTE world units and do not inherit the world group's scale.
        /// Positions shrink with the AR diorama; the motes themselves don't, so they have to
        /// be refitted — the same lesson as the shadow distance and the light falloff.
        /// </summary>
        public void SetParticleScale(float s)
        {
            _dust.Size = DustSize * s;
            _skinBurst.Size = BurstSize * s;
            _embers3d.Size = EmberSize * s;
        }

        // ---- camera ----
        class CamSpec
        {
            public Vector3 P, T; public float K;
            public CamSpec(Vector3 p, Vector3 t, float k) { P = p; T = t; K = k; }
        }
        static readonly Dictionary<string, CamSpec> CAMS = new Dictionary<string, CamSpec>
        {
            { "default", new CamSpec(new Vector3(0, 3.4f, 8.2f), new Vector3(0, 1.7f, 0), 0.05f) },
            { "charge",  new CamSpec(new Vector3(0, 3.0f, 7.4f), new Vector3(0, 1.6f, 0), 0.05f) },
            { "crane",   new CamSpec(new Vector3(0, 9.6f, 4.6f), new Vector3(0, 0.5f, 0), 0.028f) }
        };
        string _camMode = "default";
        float _distScale = 1f;
        Vector3 _camPos, _camTgt;

        // ---- animation state ----
        string _mode = "idle";
        float _tGlobal;

        class SpinState
        {
            public float From, To, Dur, Power, Wobble, T, Start, PrecPhase, StartY, WanderMax;
            public Quaternion BlendQ;
        }
        class TumbleState
        {
            public float T, Dur, Start, Wobble, FinalYaw, Power, FromX, FromZ;
            public bool Fake, Impacted;
            public List<Vector2> Knocks;    // x = normalised time, y = strength
            public int KnockI;
        }
        class RecoverState { public float T, Dur, Start, FromY; public Quaternion FromQ; }

        SpinState _spin;
        TumbleState _tumble;
        RecoverState _recover;

        bool _chargeOn;
        float _chargeP, _chargeEnergy, _chargeRingT, _chargeRingLife, _chargeRingPeak;

        // walk: the contact point precesses, so the whole top wanders a slow rosette
        Vector3 _wander, _wanderVel;
        float _spinWind, _streakEnergy, _haloTarget, _waxAmt;
        bool _drama;

        // ---- callbacks into the game layer ----
        public Action<float> OnChargePulse;
        public Action<float, float> OnSpinAudio;     // speed01, power
        public Action<float> OnKnock;                // strength
        public Action<float> OnImpact;               // power, at the true contact frame

        // ---------------------------------------------------------------
        public void Init(Camera cam)
        {
            Cam = cam;
            Rig = new SceneRig();
            Rig.Build(transform, cam);

            Dreidel = new DreidelRig();
            Dreidel.Build(Rig.World);

            Gelt = new GeltSystem(Rig);
            Scuff = new ScuffMap();
            Scuff.Build(Rig.World);

            _dust = new PointCloud();
            _dust.Build(Rig.World, cam, 64, DustSize, new Color(0.78f, 0.71f, 0.55f, 0.55f), "dust3d");
            _dust.Gravity = 4.5f; _dust.Drag = 1.6f; _dust.FloorY = 0.02f;

            _skinBurst = new PointCloud();
            _skinBurst.Build(Rig.World, cam, 48, BurstSize, Color.white, "skinBurst");
            _skinBurst.Gravity = 5.2f; _skinBurst.Drag = 1.4f; _skinBurst.FloorY = 0.04f; _skinBurst.Bounce = 0.3f;

            _embers3d = new PointCloud();
            _embers3d.Build(Rig.World, cam, 32, EmberSize, new Color(1f, 0.72f, 0.37f, 0.8f), "embers3d");
            _embers3d.Gravity = -0.35f;   // embers rise
            _embers3d.Drag = 0.2f; _embers3d.FloorY = -99f;

            var spec = CAMS["default"];
            _camPos = spec.P; _camTgt = spec.T;
            cam.transform.position = _camPos;
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
            cam.transform.LookAt(_camTgt);

            foreach (var b in Rig.World.GetComponentsInChildren<Billboard>(true)) b.Cam = cam;

            OnResize();
        }

        public void OnResize()
        {
            float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            // pull back on narrow screens so the dreidel isn't wall-to-wall
            _distScale = Mathf.Min(Mathf.Max(0.62f / aspect, 1f), 1.5f);
        }

        // ---------- helpers, verbatim from the source ----------
        static float EaseOutCubic(float t) { return 1f - Mathf.Pow(1f - t, 3f); }
        static float EaseSpin(float t) { return 1f - Mathf.Pow(1f - t, 3.4f); }   // fast launch, long tail
        static float Clamp01(float t) { return Mathf.Clamp01(t); }

        public float GetRotDeg() { return _rotDeg; }
        float _rotDeg;                     // authoritative yaw in degrees, unwrapped

        void ApplyYaw(float deg) { _rotDeg = deg; Dreidel.Spinner.localRotation = Quaternion.Euler(0, deg, 0); }

        public bool IsLying { get { return _mode == "rest"; } }
        public string CurrentSkin { get { return Dreidel.CurrentSkin; } }

        // ---------- mode transitions ----------
        public void ChargeStart()
        {
            _chargeOn = true; _chargeP = 0f;
            if (_mode == "rest") StartRecover(0.28f);
        }

        public void ChargeSet(float p) { _chargeP = p; }

        public void ChargeEnd()
        {
            _chargeOn = false;
            Fx.SetGlow(Rig.Aura, 0f);
            SetAuraOpacity(0f);
            if (Rig.ChargeRing) Rig.ChargeRing.gameObject.SetActive(false);
            _chargeRingT = 0f;
            // coins ease home from wherever the tremble left them
            foreach (var m in Rig.LooseCoins)
            {
                var h = m.GetComponent<CoinHome>();
                if (h != null) h.GoHome();
            }
        }

        /// <summary>Land exactly on finalDeg (mod 360) after roughly `delta` of travel.</summary>
        public void StartSpin(float finalDeg, float delta, float dur, float power, float wobble)
        {
            float from = _rotDeg;
            float d = from - finalDeg;
            d += 360f * Mathf.Round((delta - d) / 360f);

            _spin = new SpinState
            {
                From = from, To = from - d, Dur = dur, Power = power, Wobble = wobble,
                T = 0f, Start = Time.time, PrecPhase = Random.value * 6.28f,
                BlendQ = Dreidel.Root.localRotation, StartY = Dreidel.Root.localPosition.y
            };

            // The walk: a real top's contact point precesses, so it traces a slow rosette.
            // Direction is random each spin; the cap keeps it near the play area.
            _wander = new Vector3(Dreidel.Root.localPosition.x, 0, Dreidel.Root.localPosition.z);
            float wAng = Random.value * Mathf.PI * 2f;
            float wSp = 0.10f + power * 0.16f;
            _wanderVel = new Vector3(Mathf.Cos(wAng) * wSp, 0, Mathf.Sin(wAng) * wSp);
            if (Ar)
            {
                // toward the player's edge, so the last seconds carry real is-it-going-over
                // tension; the cap keeps it inside the brass rim
                _wanderVel.z = Mathf.Abs(_wanderVel.z) * 0.6f + 0.16f + power * 0.1f;
            }
            _spin.WanderMax = Ar ? 2.35f : 0.9f;

            _tumble = null; _recover = null;
            _mode = "spin";
            Dreidel.Oil.Disturb(0.5f + power * 0.8f);   // the launch throws it too
        }

        public void StartRecover(float dur = 0.5f)
        {
            _present = null;    // recover slerps from wherever the AR beat left it - no snap
            _recover = new RecoverState
            {
                T = 0f, Dur = dur, Start = Time.time,
                FromQ = Dreidel.Root.localRotation, FromY = Dreidel.Root.localPosition.y
            };
            _mode = "recover";
            _camMode = "default";        // never leave the crane parked between turns
        }

        public void SetCam(string m) { _camMode = CAMS.ContainsKey(m ?? "") ? m : "default"; }
        public void SetDrama(bool v) { _drama = v; }
        public void SetSkin(string k) { Dreidel.SetSkin(k); ApplySkinFlourishes(k); }
        public void SetCustomFaces(bool on, string[] labels) { Dreidel.SetCustomFaces(on, labels); }
        public void RebuildLetters() { Dreidel.RebuildLetters(); }
        public void SetPotCoins(int n) { Gelt.SetPotCoins(n); }
        public int PotMax { get { return SceneRig.GELT_MAX; } }

        public void SetEnv(EnvDef env)
        {
            Rig.SetEnv(env);
            // flat storybook tables paint no shading at all, so a soft dark smudge would read
            // as dirt on the rug rather than as evidence of a spin
            Scuff.Show(!env.Room);
        }

        void ApplySkinFlourishes(string k)
        {
            bool ner = k == "nertamid";
            if (Rig.NerLight) Rig.NerLight.gameObject.SetActive(ner);
            if (Rig.NtFlame) Rig.NtFlame.gameObject.SetActive(ner);
            _haloTarget = (k == "gold" || k == "founder" || k == "goldpup") ? 0.35f : 0f;
        }

        /// <summary>The GIMEL flash: a flood of warm light off the dreidel.</summary>
        public void Burst()
        {
            if (Rig.BurstLight) Rig.BurstLight.intensity = Ar ? 9f : 6f;
            if (Rig.BurstSprite)
            {
                Fx.SetGlow(Rig.BurstSprite, 1f);
                Rig.BurstSprite.localScale = Vector3.one * 3f;
            }
            // In AR the win also floods the surface: a broad warm plane flashed flat on the
            // table, so for a beat the jackpot visibly lights the room around the board.
            if (Ar && Rig.ChargeRing)
            {
                Rig.ChargeRing.gameObject.SetActive(true);
                _chargeRingLife = 0f;
                _chargeRingPeak = 1.2f;
            }
        }

        public void SkinBurst(Color color, int n = 36, float power = 0.7f)
        {
            _skinBurst.SetTint(color);
            var p = Dreidel.Root.localPosition;
            _skinBurst.Burst(new Vector3(p.x, 0.1f, p.z), n, 0.25f, 0.6f * 0.8f, 1.4f * 0.8f,
                             1.6f + power * 1.2f, 1.8f, 0.6f, 0.5f);
        }

        public void DustBurst(int n, float power)
        {
            var p = Dreidel.Root.localPosition;
            _dust.Burst(new Vector3(p.x, 0.05f, p.z), n, 0.2f, 0.5f + power * 0.9f, 1.1f,
                        0.7f + power * 0.7f, 1.1f, 0.55f, 0.35f);
        }

        /// <summary>The nun anti-fanfare: the flames sigh and pull back.</summary>
        public void FlameSigh() { foreach (var F in Rig.Flames) F.Gust = -1.6f; }

        /// <summary>
        /// Wax buildup — one notch per spin, saturating over a long game. The candle shortens,
        /// a drip collar swells at the lip, a pool spreads at the base, and the flame (plus its
        /// light and halo) rides down with the shrinking candle.
        /// </summary>
        public void AddWax()
        {
            _waxAmt = Mathf.Min(1f, _waxAmt + 0.045f);
            foreach (var F in Rig.Flames)
            {
                var body = F.Group.Find("wax");
                if (body != null)
                {
                    float sy = 1f - _waxAmt * 0.22f;
                    body.localScale = new Vector3(1, sy, 1);
                    body.localPosition = new Vector3(0, 0.75f * sy, 0);
                    if (F.Drip)
                    {
                        F.Drip.gameObject.SetActive(_waxAmt > 0.1f);
                        F.Drip.localScale = new Vector3(1 + _waxAmt * 0.7f, 0.55f + _waxAmt * 0.9f, 1 + _waxAmt * 0.7f);
                        F.Drip.localPosition = new Vector3(0.1f, 1.42f * sy, 0.05f);
                    }
                    if (F.Pool)
                    {
                        F.Pool.gameObject.SetActive(_waxAmt > 0.25f);
                        F.Pool.localScale = Vector3.one * (0.4f + _waxAmt * 0.8f);
                    }
                }
                float dy = F.BaseY * (1f - 0.22f * _waxAmt) - F.BaseY;
                F.Plane.localPosition = new Vector3(F.Plane.localPosition.x, F.BaseY + dy, F.Plane.localPosition.z);
                F.L.transform.localPosition = new Vector3(0, 1.9f + dy, 0);
                if (F.Halo) F.Halo.localPosition = new Vector3(0, 1.9f + dy, 0);
            }
        }

        // ---------------------------------------------------------------
        int _lastW, _lastH;

        void Update()
        {
            if (Screen.width != _lastW || Screen.height != _lastH)
            {
                _lastW = Screen.width; _lastH = Screen.height;
                OnResize();
            }

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            _tGlobal += dt;

            if (!Ar) CameraGlide();     // in AR the phone IS the camera, and nothing may move it
            Candles(dt);
            PressureRing(dt);
            HeroFlourishes(dt);

            EmberEmit(dt);
            Gelt.Step(dt);
            _dust.Step(dt);
            _skinBurst.Step(dt);
            _embers3d.Step(dt);
            Scuff.Heal(dt);
            _spinWind *= (1f - 1.7f * dt);            // airflow decays once the whirl stops

            PotGlow();
            NerTamid();
            OilSlosh(dt);
            SpinRings(dt);
            BurstDecay(dt);
            RimDrama();

            StateMachine(dt);
        }

        void CameraGlide()
        {
            var C = CAMS[_camMode];
            // pull back on narrow screens so the dreidel isn't wall-to-wall
            var t = C.T;
            var p = (C.P - t) * _distScale + t;
            _camPos = Vector3.Lerp(_camPos, p, C.K);
            _camTgt = Vector3.Lerp(_camTgt, t, C.K);
            Cam.transform.position = _camPos;
            Cam.transform.LookAt(_camTgt);
            // sky dome tracks the camera so it reads as infinite
            if (Rig.SkyDome) Rig.SkyDome.position = Cam.transform.position;
        }

        void Candles(float dt)
        {
            // Charge energy: smoothed so the pull on the room eases in and out rather than snapping.
            float chargeTarget = _chargeOn ? _chargeP : 0f;
            _chargeEnergy += (chargeTarget - _chargeEnergy) * (_chargeOn ? 0.14f : 0.08f);
            float windBase = Mathf.Min(1f, _spinWind);

            var rootPos = Dreidel.Root.localPosition;
            foreach (var F in Rig.Flames)
            {
                // The flames feel the wind-up AND the whirl; the whirl's effect falls off with
                // distance from the (walking) dreidel.
                float dx = F.Group.localPosition.x - rootPos.x, dz = F.Group.localPosition.z - rootPos.z;
                float prox = 1f / (1f + (dx * dx + dz * dz) * 0.16f);
                float gustE = _chargeEnergy + windBase * prox * 1.4f;
                float agit = 1f + gustE * 2.4f;
                float f = 0.85f + Mathf.Sin(_tGlobal * 11f * agit + F.Seed) * 0.08f * agit
                                + Mathf.Sin(_tGlobal * 23f + F.Seed * 2f) * 0.06f * agit;

                // Lean toward the dreidel, with a living gust on top so it isn't a rigid tilt.
                F.Gust += (Random.value - 0.5f) * (0.5f + windBase * prox * 0.9f);
                F.Gust *= 0.82f;
                float targetLean = _chargeEnergy * 0.7f + F.Gust * (_chargeEnergy + windBase * prox * 0.5f) * 0.4f;
                F.Lean += (targetLean - F.Lean) * 0.2f;

                // The leaning flame also stretches and drifts its tip, the way a guttering flame does.
                F.Plane.localScale = new Vector3(f, f * (1f + Mathf.Sin(_tGlobal * 17f + F.Seed) * 0.08f + F.Lean * 0.5f), 1f);
                F.Plane.localPosition = new Vector3(F.BaseX + F.Lean * 0.12f, F.Plane.localPosition.y, F.Plane.localPosition.z);
                F.L.intensity = 0.42f * f + (_drama ? 0.15f : 0f) + _chargeEnergy * 0.35f + windBase * prox * 0.2f;

                // Flames are billboards; the lean is applied on top, about a precomputed axis
                // in the diorama's local frame.
                // The lean axis was precomputed in the diorama's local frame; Billboard writes
                // a world rotation, so the axis is lifted into world space. Identity outside
                // AR, correct once the board has been turned to face the player.
                F.Bill.Extra = Mathf.Abs(F.Lean) > 0.0015f
                    ? Quaternion.AngleAxis(F.Lean * Mathf.Rad2Deg, Rig.World.rotation * F.LeanAxis)
                    : Quaternion.identity;

                if (F.Halo) Fx.SetGlow(F.Halo, 0.55f * f);
            }

            // Candlelight drives the scene: the key light breathes with the summed flame
            // flicker, so the shadows on the table genuinely dance.
            if (Rig.KeyLight && Rig.Flames.Count > 0)
            {
                float fsum = 0f;
                foreach (var F in Rig.Flames) fsum += F.Plane.localScale.x;
                float breathe = fsum / Rig.Flames.Count;      // ~0.85..1.0, wilder when agitated
                Rig.KeyLight.intensity = 1.0f * (0.90f + breathe * 0.11f);
            }
        }

        void PressureRing(float dt)
        {
            if (!Rig.ChargeRing) return;
            // Emit an expanding shock while the wind-up is past the halfway mark, faster and
            // brighter the closer to full charge.
            if (_chargeOn && _chargeP > 0.42f)
            {
                _chargeRingT += dt * (1.1f + _chargeP * 2.2f);
                if (_chargeRingT >= 1f)
                {
                    _chargeRingT -= 1f;
                    Rig.ChargeRing.gameObject.SetActive(true);
                    _chargeRingLife = 0f;
                    _chargeRingPeak = _chargeP;
                    if (OnChargePulse != null) OnChargePulse(_chargeP);
                }
            }
            if (Rig.ChargeRing.gameObject.activeSelf)
            {
                _chargeRingLife += dt * 2.6f;
                float L = _chargeRingLife;
                if (L >= 1f) Rig.ChargeRing.gameObject.SetActive(false);
                else
                {
                    Rig.ChargeRing.localScale = Vector3.one * (0.5f + L * 2.6f);
                    var mr = Rig.ChargeRing.GetComponent<MeshRenderer>();
                    MatUtil.SetAlpha(mr.material, (1f - L) * 0.5f * _chargeRingPeak);
                }
            }
        }

        void HeroFlourishes(float dt)
        {
            if (Rig.GoldHalo)
            {
                float cur = Fx.GetGlow(Rig.GoldHalo);
                float want = _haloTarget * (0.8f + 0.2f * Mathf.Sin(_tGlobal * 2.5f));
                Fx.SetGlow(Rig.GoldHalo, cur + (want - cur) * 0.06f);
            }
            // polished floor glow tracks the dreidel and pulses subtly
            if (Rig.FloorGlow)
            {
                var p = Dreidel.Root.localPosition;
                Rig.FloorGlow.localPosition = new Vector3(p.x, 0.015f, p.z);
                var mr = Rig.FloorGlow.GetComponent<MeshRenderer>();
                MatUtil.SetAlpha(mr.material,
                    0.35f + 0.12f * Mathf.Sin(_tGlobal * 1.7f) + (Dreidel.CurrentSkin == "gold" ? 0.2f : 0f));
            }
        }

        /// <summary>
        /// A handful of motes rising in each candle's glow, living in the scene rather than
        /// pasted on the lens — so they sit on the table with everything else.
        /// </summary>
        float _emberT;

        void EmberEmit(float dt)
        {
            if (Rig.Flames.Count == 0) return;
            _emberT += dt;
            if (_emberT < 0.22f) return;
            _emberT = 0f;
            foreach (var F in Rig.Flames)
            {
                if (!F.Group.gameObject.activeSelf) continue;
                var p = F.Group.localPosition;
                _embers3d.Burst(new Vector3(p.x, 1.85f, p.z), 1, 0.14f, 0.05f, 0.12f, 0.25f, 0.35f, 1.6f, 1.2f);
            }
        }

        void PotGlow()
        {
            int potN = 0;
            foreach (var m in Rig.PotCoins) if (m.gameObject.activeSelf) potN++;
            foreach (var s in Rig.CoinGlows)
            {
                float cur = Fx.GetGlow(s);
                Fx.SetGlow(s, cur + (Mathf.Min(potN / 16f, 1f) * 0.7f - cur) * 0.1f);
            }
        }

        void NerTamid()
        {
            if (Rig.NerLight && Rig.NerLight.gameObject.activeSelf)
            {
                var p = Dreidel.Root.localPosition;
                Rig.NerLight.transform.localPosition = new Vector3(p.x, p.y + 0.2f, p.z);
                float flick = 0.7f + Mathf.Sin(_tGlobal * 11f) * 0.09f
                            + Mathf.Sin(_tGlobal * 23f + 1.3f) * 0.05f + Random.value * 0.04f;
                Rig.NerLight.intensity = 0.85f * flick;
            }
            // The riding flame: pinned to the knob's WORLD position, always burning upward —
            // it clings to the tip through spins and topples alike.
            if (Rig.NtFlame && Rig.NtFlame.gameObject.activeSelf && Dreidel.Knob)
            {
                var wp = Dreidel.Knob.transform.position;
                Rig.NtFlame.position = new Vector3(wp.x, wp.y + 0.26f, wp.z);
                float ff = 0.85f + Mathf.Sin(_tGlobal * 13f) * 0.1f + Mathf.Sin(_tGlobal * 29f + 0.7f) * 0.06f;
                Rig.NtFlameMesh.localScale = new Vector3(ff, ff * (1f + Mathf.Sin(_tGlobal * 17f) * 0.1f), 1f);
                if (Rig.NtHalo) Fx.SetGlow(Rig.NtHalo, 0.45f + 0.15f * Mathf.Sin(_tGlobal * 7f));
                if (Rig.NerLight) Rig.NerLight.transform.position = Rig.NtFlame.position;
            }
        }

        /// <summary>
        /// Step the Oil Miracle's fluid. It is handed the vessel itself and works out what the
        /// liquid feels from there: the tilt sets the plane it relaxes toward, the vessel's own
        /// acceleration throws it at the walls, and the spin rate raises the parabola a
        /// rotating liquid actually forms.
        /// </summary>
        void OilSlosh(float dt)
        {
            if (!Dreidel.Oil.Active) return;

            // Angular rate straight off the yaw the spin is driving, rather than a stand-in.
            float spinRate = dt > 0f ? Mathf.Abs(_rotDeg - _lastRotDeg) * Mathf.Deg2Rad / dt : 0f;
            _lastRotDeg = _rotDeg;

            Dreidel.Oil.Step(dt, Dreidel.Spinner, spinRate);

            if (Dreidel.OilGlint)
            {
                // the glint rides the highest point of the surface, wherever the slosh put it
                Dreidel.OilGlint.localPosition = Dreidel.Oil.SurfacePeak() + Vector3.up * 0.04f;
                Fx.SetGlow(Dreidel.OilGlint, 0.18f + Mathf.Min(spinRate / 30f, 1f) * 0.35f);
            }
        }
        float _lastRotDeg;

        /// <summary>
        /// Spin energy rings — a wobbling stack that reads as a motion blur. One flat hoop
        /// reads as a static halo; several offset ones read as motion.
        /// </summary>
        void SpinRings(float dt)
        {
            if (Rig.SpinRings.Count == 0) return;
            _streakEnergy *= 0.9f;
            var rp = Dreidel.Root.localPosition;
            for (int i = 0; i < Rig.SpinRings.Count; i++)
            {
                var s = Rig.SpinRings[i];
                MatUtil.SetAlpha(s.M, _streakEnergy * s.BaseOp);
                float wob = Mathf.Sin(_tGlobal * s.Wob + i * 2.1f) * s.WobAmt * _streakEnergy;
                s.T.localScale = Vector3.one * (1f + _streakEnergy * 0.22f + wob * 0.3f);
                s.T.localPosition = new Vector3(rp.x, rp.y - DreidelRig.STAND_Y, rp.z);
                _ringSpin[i] += dt * _streakEnergy * s.Spin;
                s.T.localRotation = Quaternion.Euler(
                    (Mathf.PI / 2f + s.Tilt + wob) * Mathf.Rad2Deg,
                    Mathf.Sin(_tGlobal * s.Wob * 0.7f + i) * s.WobAmt * 0.8f * _streakEnergy * Mathf.Rad2Deg,
                    _ringSpin[i] * Mathf.Rad2Deg);
            }
        }
        readonly float[] _ringSpin = new float[3];

        void BurstDecay(float dt)
        {
            if (Rig.BurstLight && Rig.BurstLight.intensity > 0.01f)
            {
                Rig.BurstLight.intensity *= 0.90f;
                Fx.SetGlow(Rig.BurstSprite, Fx.GetGlow(Rig.BurstSprite) * 0.90f);
                Rig.BurstSprite.localScale += Vector3.one * (dt * 10f);
            }
        }

        void RimDrama()
        {
            // showdown drama: rim light warms to red
            var target = _drama ? Hex.FromInt(0xff5470) : Rig.EnvRim;
            Rig.RimLight.color = Color.Lerp(Rig.RimLight.color, target, 0.04f);
            Rig.RimLight.intensity += ((_drama ? 1.0f : 0.7f) - Rig.RimLight.intensity) * 0.04f;
        }

        void SetAuraOpacity(float a)
        {
            var mr = Rig.Aura.GetComponent<MeshRenderer>();
            MatUtil.SetAlpha(mr.material, a);
        }

        float AuraOpacity()
        {
            return MatUtil.GetTint(Rig.Aura.GetComponent<MeshRenderer>().material).a;
        }

        // ---------------------------------------------------------------
        void StateMachine(float dt)
        {
            var root = Dreidel.Root;

            switch (_mode)
            {
                case "idle":
                {
                    ApplyYaw(_rotDeg - dt * 0.35f * Mathf.Rad2Deg);
                    root.localPosition = new Vector3(root.localPosition.x,
                        DreidelRig.STAND_Y + Mathf.Sin(_tGlobal * 1.6f) * 0.05f, root.localPosition.z);
                    float rz = Mathf.Sin(_tGlobal * 0.9f) * 0.02f;
                    float rx = Mathf.Cos(_tGlobal * 0.7f) * 0.015f;

                    if (_chargeOn)
                    {
                        // wind-up: tilt back, tremble, glow
                        float p = _chargeP;
                        rx = -0.05f - p * 0.15f + (Random.value - 0.5f) * p * 0.05f;
                        rz = (Random.value - 0.5f) * p * 0.06f;
                        root.localPosition = new Vector3((Random.value - 0.5f) * p * 0.06f,
                            root.localPosition.y, root.localPosition.z);
                        ApplyYaw(_rotDeg + dt * p * 1.2f * Mathf.Rad2Deg);   // slow reverse wind
                        SetAuraOpacity(0.15f + p * 0.75f);
                        Rig.Aura.localScale = Vector3.one * (0.5f + p * 0.8f);
                        Rig.GlowLight.intensity = 0.55f + p * 1.15f;
                        // Spring-loading: the body compresses as energy builds, so the launch
                        // can spring out of it (the release stretch lives in the 'spin' state).
                        root.localScale = new Vector3(1f + p * 0.03f, 1f - p * 0.06f, 1f + p * 0.03f);

                        // The loose gelt gets nervous — a fine tremble that grows with the charge.
                        float jit = p * p * 0.05f;
                        for (int i = 0; i < Rig.LooseCoins.Count; i++)
                        {
                            var m = Rig.LooseCoins[i];
                            var h = m.GetComponent<CoinHome>();
                            if (h == null) continue;
                            float s = _tGlobal * 34f + i * 1.7f;
                            m.localPosition = new Vector3(
                                h.P.x + Mathf.Sin(s) * jit,
                                h.P.y + Mathf.Abs(Mathf.Sin(s * 1.7f)) * jit * 0.6f,
                                h.P.z + Mathf.Cos(s * 1.3f) * jit);
                            m.localRotation = h.R * Quaternion.Euler(0, 0, Mathf.Sin(s * 0.8f) * jit * 1.2f * Mathf.Rad2Deg);
                        }
                    }
                    else
                    {
                        root.localPosition = new Vector3(root.localPosition.x * 0.9f,
                            root.localPosition.y, root.localPosition.z);
                        SetAuraOpacity(AuraOpacity() * 0.9f);
                        Rig.GlowLight.intensity += (0.55f - Rig.GlowLight.intensity) * 0.1f;
                        root.localScale = Vector3.Lerp(root.localScale, Vector3.one, 0.2f);
                        // settle the coins back home
                        foreach (var m in Rig.LooseCoins)
                        {
                            var h = m.GetComponent<CoinHome>();
                            if (h == null) continue;
                            m.localPosition = Vector3.Lerp(m.localPosition, h.P, 0.25f);
                            m.localRotation = Quaternion.Slerp(m.localRotation, h.R, 0.25f);
                        }
                    }
                    root.localRotation = Quaternion.Euler(rx * Mathf.Rad2Deg, 0, rz * Mathf.Rad2Deg);
                    break;
                }

                case "spin":
                {
                    _spin.T = Time.time - _spin.Start;
                    float T = Clamp01(_spin.T / _spin.Dur);
                    float e = EaseSpin(T);
                    ApplyYaw(_spin.From + (_spin.To - _spin.From) * e);

                    // Release spring: the compressed body springs out — squash, then overshoot
                    // to a stretch, then settle. Roughly volume-preserving (xz counter-scales y).
                    const float SQ = 0.26f;
                    if (_spin.T < SQ)
                    {
                        float u = _spin.T / SQ;
                        float amt = (0.16f + _spin.Power * 0.14f) * -Mathf.Sin(u * Mathf.PI * 2f);
                        root.localScale = new Vector3(1f - amt * 0.5f, 1f + amt, 1f - amt * 0.5f);
                    }
                    else if (root.localScale.y != 1f) root.localScale = Vector3.one;

                    // scoop upright from wherever it was (masked by the launch hop)
                    float blend = EaseOutCubic(Clamp01(_spin.T / 0.4f));
                    float baseY = _spin.StartY + (DreidelRig.STAND_Y - _spin.StartY) * blend;

                    // launch hop, scaled by power
                    float hopT = Clamp01(_spin.T / 0.55f);
                    float hop = (0.35f + _spin.Power * 1.15f) * Mathf.Sin(Mathf.PI * hopT);

                    if (_spin.T < 0.4f)
                    {
                        root.localRotation = Quaternion.Slerp(_spin.BlendQ, Quaternion.identity, blend);
                    }
                    else
                    {
                        // precession wobble grows as it slows
                        float lean = Mathf.Pow(Clamp01((T - 0.45f) / 0.55f), 2f) * (0.16f + _spin.Power * 0.12f);
                        _spin.PrecPhase += dt * (10f - 6f * T);
                        root.localRotation = Quaternion.Euler(
                            Mathf.Cos(_spin.PrecPhase) * lean * Mathf.Rad2Deg, 0,
                            Mathf.Sin(_spin.PrecPhase) * lean * Mathf.Rad2Deg);
                    }

                    // ---- the walk ----
                    // The rosette: steady drift plus a precession-locked orbit whose radius
                    // grows as it slows (the wobble carries the contact point wider). It decays
                    // near the end so the top isn't sliding as it dies.
                    float speed01 = 1f - T;
                    float walkFade = Clamp01(speed01 / 0.22f);
                    _wander.x += _wanderVel.x * dt * walkFade;
                    _wander.z += _wanderVel.z * dt * walkFade;
                    float orbitR = (1f - speed01) * 0.16f * (0.7f + _spin.Power * 0.6f);
                    float wx = _wander.x + Mathf.Cos(_spin.PrecPhase) * orbitR;
                    float wz = _wander.z + Mathf.Sin(_spin.PrecPhase) * orbitR;
                    float wr = Mathf.Sqrt(wx * wx + wz * wz);
                    if (wr > _spin.WanderMax) { wx *= _spin.WanderMax / wr; wz *= _spin.WanderMax / wr; }
                    root.localPosition = new Vector3(wx, baseY + hop * (1f - T * 0.3f), wz);

                    // scuff: the tip leaves a mark where it's been
                    Scuff.Stamp(wx, wz, 0.02f + speed01 * 0.028f);

                    // airflow: flames gutter harder the faster it whirls
                    _spinWind = Mathf.Max(_spinWind, speed01 * _spin.Power);

                    // scrape audio tracks RPM every frame
                    if (OnSpinAudio != null) OnSpinAudio(speed01, _spin.Power);

                    // ground halo tied to speed, riding under the walking top
                    SetAuraOpacity(speed01 * 0.5f * _spin.Power + 0.05f);
                    Rig.Aura.localScale = Vector3.one * (0.7f + speed01 * 0.5f);
                    Rig.Aura.localPosition = new Vector3(wx, 0.02f, wz);
                    Rig.GlowLight.intensity = 0.55f + speed01 * _spin.Power * 0.9f;
                    _streakEnergy = Mathf.Max(_streakEnergy, speed01 * _spin.Power);

                    if (T >= 1f) BeginTumble();
                    break;
                }

                case "tumble":
                {
                    _tumble.T = Time.time - _tumble.Start;
                    float T = Clamp01(_tumble.T / _tumble.Dur);
                    // fall away from the camera so the resolved letter faces UP, with a bounce
                    float fallK, dropK;
                    if (_tumble.Fake)
                    {
                        // will-it-stand: dip to ~55% down, claw back to ~22%, then lose it
                        if (T < 0.28f) fallK = EaseOutCubic(T / 0.28f) * 0.55f;
                        else if (T < 0.46f) fallK = 0.55f - EaseOutCubic((T - 0.28f) / 0.18f) * 0.33f;
                        else fallK = 0.22f + EaseOutCubic(Clamp01((T - 0.46f) / 0.34f)) * 0.78f;
                        dropK = Clamp01((T - 0.34f) / 0.46f);
                    }
                    else
                    {
                        fallK = EaseOutCubic(Clamp01(T / 0.6f));
                        dropK = Clamp01(T / 0.55f);
                    }
                    float over = Mathf.Sin(Clamp01((T - (_tumble.Fake ? 0.72f : 0.55f)) / 0.45f) * Mathf.PI) * 0.12f;
                    float rx = _tumble.FromX - fallK * (Mathf.PI / 2f) - over;
                    float rz = _tumble.FromZ + EaseOutCubic(T) * _tumble.Wobble * Mathf.Deg2Rad;
                    root.localRotation = Quaternion.Euler(rx * Mathf.Rad2Deg, 0, rz * Mathf.Rad2Deg);

                    // body drops to rest on its side, with one small hop
                    float drop = DreidelRig.STAND_Y + (DreidelRig.LIE_Y - DreidelRig.STAND_Y) * EaseOutCubic(dropK);
                    float bounce = Mathf.Max(0f,
                        Mathf.Sin(Clamp01((T - (_tumble.Fake ? 0.72f : 0.55f)) / 0.3f) * Mathf.PI)) * 0.14f;
                    root.localPosition = new Vector3(root.localPosition.x, drop + bounce, root.localPosition.z);

                    // wooden clatter and the impact moment, exactly when the body meets the surface
                    while (_tumble.KnockI < _tumble.Knocks.Count && T >= _tumble.Knocks[_tumble.KnockI].x)
                    {
                        var k = _tumble.Knocks[_tumble.KnockI++];
                        if (OnKnock != null) OnKnock(k.y * (0.5f + _tumble.Power * 0.5f));
                        if (!_tumble.Impacted && k.y >= 0.85f)
                        {
                            _tumble.Impacted = true;
                            Dreidel.Oil.Disturb(-0.9f - _tumble.Power * 1.4f);   // the oil feels the landing
                            DustBurst(Mathf.RoundToInt(8 + _tumble.Power * 14), _tumble.Power);
                            if (OnImpact != null) OnImpact(_tumble.Power);
                        }
                    }
                    // scrape audio dies through the topple
                    if (OnSpinAudio != null) OnSpinAudio(Mathf.Max(0f, 0.18f * (1f - T)), _tumble.Power);
                    SetAuraOpacity(AuraOpacity() * 0.92f);
                    Rig.GlowLight.intensity += (0.55f - Rig.GlowLight.intensity) * 0.08f;
                    // dying jitter around the final yaw — settles at exactly square
                    ApplyYaw(_tumble.FinalYaw + Mathf.Pow(1f - T, 2f) * Mathf.Sin(T * 20f) * 0.1f * Mathf.Rad2Deg);

                    if (T >= 1f)
                    {
                        root.localRotation = Quaternion.Euler((_tumble.FromX - Mathf.PI / 2f) * Mathf.Rad2Deg, 0, rz * Mathf.Rad2Deg);
                        root.localPosition = new Vector3(root.localPosition.x, DreidelRig.LIE_Y, root.localPosition.z);
                        ApplyYaw(_tumble.FinalYaw);
                        _mode = "rest";
                        StartPresent();          // AR's stand-in for the crane shot
                    }
                    break;
                }

                case "rest":
                    // stillness; faint breathing of the candle light
                    Rig.GlowLight.intensity = 0.5f + Mathf.Sin(_tGlobal * 2.2f) * 0.06f;
                    if (_present != null) PresentStep(dt);
                    break;

                case "recover":
                {
                    _recover.T = Time.time - _recover.Start;
                    float T = Clamp01(_recover.T / _recover.Dur);
                    float e = EaseOutCubic(T);
                    root.localRotation = Quaternion.Slerp(_recover.FromQ, Quaternion.identity, e);
                    // hop up to standing — and back to centre, wherever the walk left it
                    float arc = Mathf.Sin(Mathf.PI * T) * 0.5f;
                    var p = root.localPosition;
                    root.localPosition = new Vector3(p.x * (1f - e),
                        _recover.FromY + (DreidelRig.STAND_Y - _recover.FromY) * e + arc, p.z * (1f - e));
                    Rig.Aura.localPosition = new Vector3(root.localPosition.x, 0.02f, root.localPosition.z);
                    if (T >= 1f)
                    {
                        root.localRotation = Quaternion.identity;
                        root.localPosition = new Vector3(0, DreidelRig.STAND_Y, 0);
                        Rig.Aura.localPosition = new Vector3(0, 0.02f, 0);
                        _mode = "idle";
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// In normal play the camera cranes overhead when the dreidel falls, so you can read
        /// the face that came up. In AR the phone IS the camera and nothing may move it, so
        /// the result comes to the viewer instead: after a beat to let the clatter finish, the
        /// dreidel rises off the table, turns its winning face toward the lens, holds, and
        /// settles back exactly where it fell.
        ///
        /// Purely cosmetic — the result is decided by the landing yaw, never read off the
        /// geometry here.
        /// </summary>
        class PresentState
        {
            public string Phase = "wait";
            public float T, Y0;
            public Quaternion From, To;
        }

        PresentState _present;

        const float PresentWait = 0.35f, PresentUp = 0.55f, PresentHold = 1.15f,
                    PresentDown = 0.5f, PresentLift = 0.5f, PresentLean = 0.55f;

        void StartPresent()
        {
            if (Ar && ArIsPlaced()) _present = new PresentState();
        }

        /// <summary>
        /// Aim is recomputed when the lift starts, not when the dreidel lands, so a phone that
        /// moved during the clatter still gets the face turned to where it is now.
        /// </summary>
        bool PresentAim(out Quaternion aimLocal)
        {
            aimLocal = Quaternion.identity;
            var here = Dreidel.Root.position;
            var toLens = Cam.transform.position - here;
            if (toLens.sqrMagnitude < 1e-6f) return false;

            var aim = Vector3.Lerp(Vector3.up, toLens.normalized, PresentLean);
            aim.y = Mathf.Max(aim.y, 0.35f);      // never lean so far it rolls onto its face
            var lean = Quaternion.FromToRotation(Vector3.up, aim.normalized);

            // that lean is a world-space turn; bring it back into whatever space root sits in
            var parentW = Dreidel.Root.parent != null ? Dreidel.Root.parent.rotation : Quaternion.identity;
            aimLocal = Quaternion.Inverse(parentW) * lean * Dreidel.Root.rotation;
            return true;
        }

        void PresentStep(float dt)
        {
            if (!Ar || Cam == null)
            {
                // AR exited mid-beat - put it back down
                if (_present != null && _present.Phase != "wait")
                {
                    Dreidel.Root.localRotation = _present.From;
                    Dreidel.Root.localPosition = new Vector3(Dreidel.Root.localPosition.x, _present.Y0,
                                                             Dreidel.Root.localPosition.z);
                }
                _present = null;
                return;
            }

            _present.T += dt;
            var root = Dreidel.Root;

            if (_present.Phase == "wait")
            {
                if (_present.T < PresentWait) return;
                Quaternion to;
                if (!PresentAim(out to)) { _present = null; return; }
                _present = new PresentState
                {
                    Phase = "up", T = 0f, From = root.localRotation, To = to, Y0 = root.localPosition.y
                };
            }
            else if (_present.Phase == "up")
            {
                float e = EaseOutCubic(Clamp01(_present.T / PresentUp));
                root.localRotation = Quaternion.Slerp(_present.From, _present.To, e);
                root.localPosition = new Vector3(root.localPosition.x, _present.Y0 + PresentLift * e,
                                                 root.localPosition.z);
                if (_present.T >= PresentUp) { _present.Phase = "hold"; _present.T = 0f; }
            }
            else if (_present.Phase == "hold")
            {
                if (_present.T >= PresentHold) { _present.Phase = "down"; _present.T = 0f; }
            }
            else
            {
                float e = EaseOutCubic(Clamp01(_present.T / PresentDown));
                root.localRotation = Quaternion.Slerp(_present.To, _present.From, e);
                root.localPosition = new Vector3(root.localPosition.x,
                                                 _present.Y0 + PresentLift * (1f - e), root.localPosition.z);
                if (_present.T >= PresentDown)
                {
                    root.localRotation = _present.From;
                    root.localPosition = new Vector3(root.localPosition.x, _present.Y0, root.localPosition.z);
                    _present = null;
                }
            }
        }

        void BeginTumble()
        {
            var root = Dreidel.Root;
            ApplyYaw(_spin.To);                       // guaranteed square landing
            var e = root.localEulerAngles;
            float fromX = Mathf.DeltaAngle(0f, e.x) * Mathf.Deg2Rad;
            float fromZ = Mathf.DeltaAngle(0f, e.z) * Mathf.Deg2Rad;

            // The fake-out: sometimes the top nearly catches itself — rises back toward
            // vertical for a beat — before losing it for real. Drama lives in the near-miss.
            bool fake = Random.value < 0.22f;
            float dur = fake ? 1.25f : 0.75f;

            // irregular wooden clatter: first body contact, then 2-3 dying knocks
            var knocks = fake
                ? new List<Vector2> {
                    new Vector2(0.30f, 0.5f), new Vector2(0.62f, 0.9f),
                    new Vector2(0.62f + 0.13f + Random.value * 0.05f, 0.5f),
                    new Vector2(0.62f + 0.24f + Random.value * 0.06f, 0.28f),
                    new Vector2(0.95f, 0.14f) }
                : new List<Vector2> {
                    new Vector2(0.55f, 0.9f),
                    new Vector2(0.55f + 0.16f + Random.value * 0.06f, 0.5f),
                    new Vector2(0.55f + 0.30f + Random.value * 0.08f, 0.26f),
                    new Vector2(0.93f, 0.13f) };

            _tumble = new TumbleState
            {
                T = 0f, Dur = dur, Start = Time.time, Wobble = _spin.Wobble,
                FinalYaw = _spin.To, Fake = fake, Power = _spin.Power,
                FromX = fromX, FromZ = fromZ, Knocks = knocks, KnockI = 0, Impacted = false
            };
            _mode = "tumble";
        }
    }
}
