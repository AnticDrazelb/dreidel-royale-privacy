using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DreidelRoyale.Core;

namespace DreidelRoyale.UI
{
    /// <summary>
    /// Ambient weather over the whole screen: warm motes rising on most tables, snow falling
    /// on Silver Frost, with the occasional four-point sparkle glint. Colour, count, size and
    /// speed all come from the table's own ember config.
    /// </summary>
    public class EmberLayer : MaskableGraphic
    {
        class Ember { public float X, Y, R, S, A, Drift, Ph; public bool Warm; }
        class Sparkle { public float X, Y, R, Life; }

        readonly List<Ember> _embers = new List<Ember>();
        readonly List<Sparkle> _sparkles = new List<Sparkle>();

        Color[] _colors = { Hex.To("#f2c14e"), Hex.To("#7f96ff") };
        EmberCfg _cfg = new EmberCfg("rise", 26, 1.8f, 0.3f);
        int _sparkleT;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            Spawn();
        }

        public override Texture mainTexture
        {
            get { return Theme.Dot() != null ? Theme.Dot().texture : s_WhiteTexture; }
        }

        public void SetEnv(EnvDef env)
        {
            _colors = env.Embers;
            _cfg = env.Ember;
            Spawn();
        }

        void Spawn()
        {
            _embers.Clear();
            bool snow = _cfg.Mode == "snow";
            for (int i = 0; i < _cfg.Count; i++)
                _embers.Add(new Ember
                {
                    X = Random.value * Screen.width,
                    Y = Random.value * Screen.height,
                    R = Random.value * _cfg.Size + 0.6f,
                    S = (Random.value * 0.6f + 0.4f) * _cfg.Speed * 60f,
                    A = Random.value * 0.5f + 0.3f,
                    Drift = (Random.value - 0.5f) * (snow ? 0.6f : 0.25f) * 60f,
                    Ph = Random.value * 6.28f,
                    Warm = Random.value < 0.6f
                });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool snow = _cfg.Mode == "snow";
            foreach (var e in _embers)
            {
                if (snow)
                {
                    e.Y -= e.S * dt;
                    e.X += (Mathf.Sin(e.Y * 0.012f + e.Ph) * 0.7f + e.Drift * 0.4f) * dt;
                    if (e.Y < -10f) { e.Y = Screen.height + 10f; e.X = Random.value * Screen.width; }
                }
                else
                {
                    e.Y += e.S * dt;
                    e.X += (e.Drift + Mathf.Sin(e.Y * 0.01f) * 0.15f) * dt;
                    if (e.Y > Screen.height + 10f) { e.Y = -10f; e.X = Random.value * Screen.width; }
                }
            }

            // occasional four-point sparkle glints
            _sparkleT++;
            if (_sparkleT % 22 == 0 && _sparkles.Count < 10)
                _sparkles.Add(new Sparkle
                {
                    X = Random.value * Screen.width,
                    Y = Random.value * Screen.height * 0.8f + Screen.height * 0.2f,
                    R = Random.value * 3f + 2f, Life = 1f
                });
            for (int i = _sparkles.Count - 1; i >= 0; i--)
            {
                _sparkles[i].Life -= 0.03f;
                if (_sparkles[i].Life <= 0f) _sparkles.RemoveAt(i);
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            var half = new Vector2(Screen.width, Screen.height) * 0.5f / scale;
            bool snow = _cfg.Mode == "snow";

            foreach (var e in _embers)
            {
                var c = _colors[e.Warm ? 0 : Mathf.Min(1, _colors.Length - 1)];
                float a = e.A * (snow ? 0.9f : (0.6f + 0.4f * Mathf.Sin(e.Y * 0.05f)));
                AddQuad(vh, new Vector2(e.X, e.Y) / scale - half, e.R / scale,
                        new Color(c.r, c.g, c.b, a));
            }

            foreach (var s in _sparkles)
            {
                float a = Mathf.Max(s.Life, 0f);
                float rad = s.R * (1.4f - s.Life);
                var col = new Color(_colors[0].r, _colors[0].g, _colors[0].b, a);
                var p = new Vector2(s.X, s.Y) / scale - half;
                AddBar(vh, p, rad / scale, 0.7f / scale, col);
                AddBar(vh, p, 0.7f / scale, rad / scale, col);
            }
        }

        static void AddQuad(VertexHelper vh, Vector2 c, float r, Color col)
        {
            AddBar(vh, c, r, r, col);
        }

        static void AddBar(VertexHelper vh, Vector2 c, float w, float h, Color col)
        {
            int i = vh.currentVertCount;
            vh.AddVert(c + new Vector2(-w, -h), col, new Vector2(0, 0));
            vh.AddVert(c + new Vector2(w, -h), col, new Vector2(1, 0));
            vh.AddVert(c + new Vector2(w, h), col, new Vector2(1, 1));
            vh.AddVert(c + new Vector2(-w, h), col, new Vector2(0, 1));
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
