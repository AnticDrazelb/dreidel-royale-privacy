using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DreidelRoyale.Audio;
using DreidelRoyale.Core;

namespace DreidelRoyale.UI
{
    /// <summary>
    /// The screen-space effects layer: confetti, dust puffs, and gelt flying between the pot
    /// and a player's row. Drawn straight into one mesh so a jackpot's worth of particles
    /// costs a single draw call.
    /// </summary>
    public class FxLayer : MaskableGraphic
    {
        class Particle
        {
            public Vector2 P, V;
            public float Grav, Fric, Life, Decay, Size, Rot, VRot;
            public Color Col;
            public bool Dust;
        }

        class Coin
        {
            public Vector2 A, B;
            public float T, Speed, Arc;
            public RectTransform Target;
        }

        readonly List<Particle> _parts = new List<Particle>();
        readonly List<Coin> _coins = new List<Coin>();

        static readonly Color[] ConfettiColors =
        {
            new Color(242/255f,193/255f,78/255f), new Color(75/255f,102/255f,230/255f),
            Color.white, new Color(255/255f,157/255f,69/255f)
        };

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        /// <summary>A soft round dot, so confetti flecks and dust read as motes, not blocks.</summary>
        public override Texture mainTexture
        {
            get { return Theme.Dot() != null ? Theme.Dot().texture : s_WhiteTexture; }
        }

        // ---------------------------------------------------------------
        public void Confetti(float x = -1f, float y = -1f, int n = 110, float spread = 20f)
        {
            if (x < 0f) x = Screen.width / 2f;
            if (y < 0f) y = Screen.height / 2f;
            for (int i = 0; i < n; i++)
                _parts.Add(new Particle
                {
                    P = new Vector2(x, y),
                    V = new Vector2((Random.value - 0.5f) * spread, (Random.value + 0.4f) * spread),
                    Grav = -0.45f, Fric = 0.99f, Life = 1f, Decay = 0.012f + Random.value * 0.008f,
                    Size = 6f + Random.value * 6f, Rot = Random.value * 6f, VRot = (Random.value - 0.5f) * 0.4f,
                    Col = ConfettiColors[Random.Range(0, ConfettiColors.Length)]
                });
            SetVerticesDirty();
        }

        public void DustPuff(float x, float y, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float ang = Random.value * Mathf.PI * 2f, sp = Random.value * 4f + 1f;
                _parts.Add(new Particle
                {
                    P = new Vector2(x, y),
                    V = new Vector2(Mathf.Cos(ang) * sp, -(Mathf.Sin(ang) * sp * 0.35f - 0.5f)),
                    Grav = -0.02f, Fric = 0.94f, Life = 0.9f, Decay = 0.02f + Random.value * 0.015f,
                    Size = 3f + Random.value * 4f,
                    Col = new Color(200 / 255f, 180 / 255f, 140 / 255f, 0.5f), Dust = true
                });
            }
            SetVerticesDirty();
        }

        /// <summary>A fountain of gelt out of the pot — the GIMEL flourish.</summary>
        public void FountainGelt(RectTransform from, int n)
        {
            if (from == null) return;
            var c = ScreenCentre(from);
            StartCoroutine(FountainRoutine(c, Mathf.Min(n, 14)));
        }

        IEnumerator FountainRoutine(Vector2 c, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float ang = Mathf.PI * (0.15f + Random.value * 0.7f);
                _coins.Add(new Coin
                {
                    A = c,
                    B = c + new Vector2(Mathf.Cos(ang) * (80f + Random.value * 160f) * (Random.value < 0.5f ? -1f : 1f),
                                        -(120f + Random.value * 220f)),
                    T = 0f, Speed = 0.03f + Random.value * 0.012f, Arc = 100f + Random.value * 120f
                });
                yield return new WaitForSeconds(0.045f);
            }
        }

        /// <summary>Coins hopping between two HUD elements, staggered so they read as a stream.</summary>
        public void FlyGelt(RectTransform from, RectTransform to, int count, float stagger = 0.07f)
        {
            if (from == null || to == null || count <= 0) return;
            StartCoroutine(FlyRoutine(from, to, Mathf.Min(count, 12), stagger));
        }

        IEnumerator FlyRoutine(RectTransform from, RectTransform to, int count, float stagger)
        {
            for (int i = 0; i < count; i++)
            {
                if (from == null || to == null) yield break;
                _coins.Add(new Coin
                {
                    A = ScreenCentre(from) + new Vector2((Random.value - 0.5f) * 20f, 0f),
                    B = ScreenCentre(to) + new Vector2((Random.value - 0.5f) * 14f, 0f),
                    T = 0f, Speed = 0.035f + Random.value * 0.012f, Arc = 40f + Random.value * 50f,
                    Target = to
                });
                yield return new WaitForSeconds(stagger);
            }
        }

        Vector2 ScreenCentre(RectTransform rt)
        {
            var world = rt.TransformPoint(rt.rect.center);
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
                return RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, world);
            return world;
        }

        // ---------------------------------------------------------------
        void Update()
        {
            bool any = false;

            for (int i = _parts.Count - 1; i >= 0; i--)
            {
                var p = _parts[i];
                p.V = new Vector2(p.V.x * p.Fric, (p.V.y + p.Grav) * p.Fric);
                p.P += p.V;
                p.Rot += p.VRot;
                p.Life -= p.Decay;
                if (p.Life <= 0f || p.P.y < -40f) _parts.RemoveAt(i);
                else any = true;
            }

            for (int i = _coins.Count - 1; i >= 0; i--)
            {
                var c = _coins[i];
                c.T += c.Speed * (Time.deltaTime * 60f);
                if (c.T >= 1f)
                {
                    _coins.RemoveAt(i);
                    if (c.Target != null) Sfx.Play("coin");
                }
                else any = true;
            }

            if (any || _dirtyOnce) { SetVerticesDirty(); _dirtyOnce = any; }
        }
        bool _dirtyOnce;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            var half = new Vector2(Screen.width, Screen.height) * 0.5f / scale;

            foreach (var p in _parts)
            {
                var pos = p.P / scale - half;
                var col = new Color(p.Col.r, p.Col.g, p.Col.b, p.Col.a * Mathf.Clamp01(p.Life));
                if (p.Dust) AddQuad(vh, pos, p.Size / scale, p.Size / scale, 0f, col);
                else AddQuad(vh, pos, p.Size / scale, p.Size / scale * 0.6f, p.Rot, col);
            }

            foreach (var c in _coins)
            {
                float k = Mathf.Clamp01(c.T);
                var pos = Vector2.Lerp(c.A, c.B, k);
                pos.y += Mathf.Sin(k * Mathf.PI) * c.Arc;      // the hop
                AddQuad(vh, pos / scale - half, 9f / scale, 9f / scale, 0f, Theme.Gold);
            }
        }

        static void AddQuad(VertexHelper vh, Vector2 c, float w, float h, float rot, Color col)
        {
            int i = vh.currentVertCount;
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            var corners = new[]
            {
                new Vector2(-w, -h), new Vector2(w, -h), new Vector2(w, h), new Vector2(-w, h)
            };
            var uvs = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            for (int k = 0; k < 4; k++)
            {
                var p = new Vector2(corners[k].x * cs - corners[k].y * sn,
                                    corners[k].x * sn + corners[k].y * cs) + c;
                vh.AddVert(p, col, uvs[k]);
            }
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
