using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DreidelRoyale.Core;

namespace DreidelRoyale.UI
{
    /// <summary>
    /// Each table dresses the screen edge, and the dressing is alive: stars twinkle, frost
    /// breathes and turns, the felt's stitches march anticlockwise, the Den's brass catches a
    /// travelling glint.
    ///
    /// The web build paints this to a canvas — a static layer blitted once, with the handful of
    /// moving pieces redrawn on top. Here it is one procedural mesh instead. Vertex colours give
    /// the edge fades for free, thin quads keep the rules crisp at any density, and the whole
    /// frame costs a single draw call rather than a full-screen CPU repaint on every resize.
    /// </summary>
    public class EnvFrameLayer : MaskableGraphic
    {
        class Element
        {
            public float X, Y, R, Speed, Phase, Rot, RotSpeed, Alpha, Drift, Size;
            public Color Col;
        }

        readonly List<Element> _els = new List<Element>();
        string _env = "";
        float _band = 32f;
        float _t;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public override Texture mainTexture
        {
            get { return Theme.Dot() != null ? Theme.Dot().texture : s_WhiteTexture; }
        }

        public void SetEnv(EnvDef env)
        {
            _env = env.Id;
            Rebuild();
        }

        void OnRectTransformDimensionsChange() { Rebuild(); }

        void Rebuild()
        {
            var r = rectTransform.rect;
            // Band width tracks the smaller screen dimension, so the dressing stays a border
            // rather than becoming a picture frame on a tablet.
            _band = Mathf.Clamp(Mathf.Min(r.width, r.height) * 0.05f, 24f, 44f);
            SpawnElements(r);
            SetVerticesDirty();
        }

        void SpawnElements(Rect r)
        {
            _els.Clear();
            if (string.IsNullOrEmpty(_env)) return;
            var rnd = new System.Random(_env.GetHashCode());   // same table, same stars, every launch
            System.Func<float> next = () => (float)rnd.NextDouble();

            int count = _env == "frost" ? 22 : _env == "felt" ? 0 : _env == "den" ? 0 : 34;
            for (int i = 0; i < count; i++)
            {
                // scattered inside the band, on whichever edge
                int side = (int)(next() * 4) % 4;
                float t = next(), d = next() * _band;
                float x = side == 0 ? t * r.width : side == 1 ? r.width - d : side == 2 ? t * r.width : d;
                float y = side == 0 ? d : side == 1 ? t * r.height : side == 2 ? r.height - d : t * r.height;

                _els.Add(new Element
                {
                    X = x - r.width * 0.5f,
                    Y = y - r.height * 0.5f,
                    R = 0.8f + next() * (_env == "frost" ? 5f : 1.6f),
                    Size = 3f + Mathf.Floor(next() * 3f) * 3f,     // blocky motes live on a 3px grid
                    Speed = 0.6f + next() * 2.2f,
                    Phase = next() * 6.28f,
                    Rot = next() * 6.28f,
                    RotSpeed = (next() - 0.5f) * 0.5f,
                    Alpha = 0.35f + next() * 0.55f,
                    Drift = 2f + next() * 5f,
                    Col = ElementColour(_env, next())
                });
            }
        }

        static Color ElementColour(string env, float roll)
        {
            switch (env)
            {
                case "frost": return Hex.To("#dff0ff");
                case "blocky": return roll < 0.5f ? Hex.To("#fff3b0") : Hex.To("#a8e07a");
                case "backyard": return roll < 0.33f ? Hex.To("#ffd27a")
                                      : roll < 0.66f ? Hex.To("#8fd0ff") : Hex.To("#ffa0c0");
                default: return roll < 0.75f ? Hex.To("#fff4d6") : Hex.To("#bcd0ff");
            }
        }

        void Update()
        {
            if (_els.Count > 0 || _env == "felt" || _env == "den")
            {
                _t += Time.deltaTime;
                SetVerticesDirty();
            }
        }

        // ---------------------------------------------------------------
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (string.IsNullOrEmpty(_env)) return;

            var r = rectTransform.rect;
            float w = r.width, h = r.height;
            float bx = -w * 0.5f, by = -h * 0.5f;

            EdgeFades(vh, bx, by, w, h);
            Rules(vh, bx, by, w, h);
            Animated(vh, bx, by, w, h);
        }

        /// <summary>The four bands, opaque at the edge and fading inward.</summary>
        void EdgeFades(VertexHelper vh, float bx, float by, float w, float h)
        {
            var edge = FadeColour(_env);
            var clear = new Color(edge.r, edge.g, edge.b, 0f);
            float f = _band * 1.6f;

            // bottom, top, left, right
            Gradient(vh, bx, by, w, f, edge, edge, clear, clear);
            Gradient(vh, bx, by + h - f, w, f, clear, clear, edge, edge);
            GradientH(vh, bx, by, f, h, edge, clear);
            GradientH(vh, bx + w - f, by, f, h, clear, edge);
        }

        static Color FadeColour(string env)
        {
            switch (env)
            {
                case "den": return new Color(46 / 255f, 26 / 255f, 10 / 255f, 0.92f);
                case "frost": return new Color(12 / 255f, 30 / 255f, 48 / 255f, 0.85f);
                case "felt": return new Color(3 / 255f, 26 / 255f, 14 / 255f, 0.9f);
                case "blocky": return new Color(28 / 255f, 52 / 255f, 80 / 255f, 0.55f);
                case "backyard": return new Color(180 / 255f, 116 / 255f, 62 / 255f, 0.45f);
                default: return new Color(2 / 255f, 4 / 255f, 16 / 255f, 0.92f);
            }
        }

        /// <summary>
        /// The inner rules. Felt runs a marching stitch, so its rails are drawn as dashes whose
        /// offset walks each frame; everything else gets a plain hairline.
        /// </summary>
        void Rules(VertexHelper vh, float bx, float by, float w, float h)
        {
            if (_env == "felt")
            {
                DashedRect(vh, bx + _band * 0.55f, by + _band * 0.55f, w - _band * 1.1f, h - _band * 1.1f,
                           7f, 6f, _t * 9f, 1.6f, new Color(238 / 255f, 224 / 255f, 180 / 255f, 0.5f));
                DashedRect(vh, bx + _band * 0.8f, by + _band * 0.8f, w - _band * 1.6f, h - _band * 1.6f,
                           4f, 8f, _t * 6f, 1.2f, new Color(238 / 255f, 224 / 255f, 180 / 255f, 0.28f));
                return;
            }

            var rule = RuleColour(_env);
            if (rule.a <= 0f) return;
            float inset = _band * 0.85f;
            StrokeRect(vh, bx + inset, by + inset, w - inset * 2f, h - inset * 2f, 1.2f, rule);
        }

        static Color RuleColour(string env)
        {
            switch (env)
            {
                case "den": return new Color(242 / 255f, 193 / 255f, 78 / 255f, 0.35f);
                case "frost": return new Color(200 / 255f, 232 / 255f, 255 / 255f, 0.22f);
                case "blocky": return new Color(1f, 1f, 1f, 0.14f);
                case "backyard": return new Color(1f, 1f, 1f, 0.2f);
                default: return new Color(242 / 255f, 193 / 255f, 78 / 255f, 0.18f);
            }
        }

        void Animated(VertexHelper vh, float bx, float by, float w, float h)
        {
            if (_env == "den")
            {
                // brass breathes with the candlelight...
                float warm = 0.5f + 0.5f * Mathf.Sin(_t * 0.9f);
                var glow = new Color(1f, 214 / 255f, 120 / 255f, 0.10f + 0.10f * warm);
                float s = _band * 1.2f;
                foreach (var c in new[]
                {
                    new Vector2(bx, by), new Vector2(bx + w - s, by),
                    new Vector2(bx + w - s, by + h - s), new Vector2(bx, by + h - s)
                })
                    Quad(vh, c.x, c.y, s, s, glow);

                // ...and a glint travels the inner rule, anticlockwise
                float inset = _band * 0.85f;
                Vector2 p = RulePoint((_t * 0.045f) % 1f, bx + inset, by + inset,
                                      w - inset * 2f, h - inset * 2f);
                Quad(vh, p.x - 10f, p.y - 10f, 20f, 20f,
                     new Color(1f, 238 / 255f, 190 / 255f, 0.7f));
                return;
            }

            foreach (var e in _els)
            {
                float wv = 0.5f + 0.5f * Mathf.Sin(_t * e.Speed + e.Phase);

                if (_env == "frost")
                {
                    // grow and shrink, fade nearly out and bloom back, with a slow personal turn
                    float rad = e.R * (0.7f + 0.4f * wv);
                    float a = e.Alpha * (0.15f + 0.85f * wv);
                    Flake(vh, e.X, e.Y, rad, e.Rot + _t * e.RotSpeed,
                          new Color(e.Col.r, e.Col.g, e.Col.b, a));
                }
                else if (_env == "blocky")
                {
                    // pixel motes bob on a 3px grid and blink - square everything, no arcs
                    float bob = Mathf.Round(Mathf.Sin(_t * e.Speed * 0.7f + e.Phase) * 2f) * 3f;
                    Quad(vh, e.X, e.Y + bob, e.Size, e.Size,
                         new Color(e.Col.r, e.Col.g, e.Col.b, 0.15f + 0.6f * wv));
                    if (wv > 0.96f)
                    {
                        float q = Mathf.Max(3f, e.Size - 4f);
                        Quad(vh, e.X + (e.Size - q) * 0.5f, e.Y + bob + (e.Size - q) * 0.5f, q, q,
                             new Color(1f, 1f, 235 / 255f, (wv - 0.96f) / 0.04f * 0.85f));
                    }
                }
                else if (_env == "backyard")
                {
                    float dy = Mathf.Sin(_t * e.Speed * 0.6f + e.Phase) * e.Drift;
                    float rad = e.R * (0.8f + 0.35f * wv);
                    Quad(vh, e.X - rad, e.Y + dy - rad, rad * 2f, rad * 2f,
                         new Color(e.Col.r, e.Col.g, e.Col.b, 0.2f + 0.6f * wv));
                }
                else
                {
                    // midnight: stars twinkle, and now and then one throws a four-ray glint
                    float rad = e.R * (0.85f + 0.3f * wv);
                    Quad(vh, e.X - rad, e.Y - rad, rad * 2f, rad * 2f,
                         new Color(e.Col.r, e.Col.g, e.Col.b, 0.25f + 0.7f * wv));
                    if (wv > 0.985f)
                    {
                        float a = (wv - 0.985f) / 0.015f * 0.8f;
                        float L = e.R * 4.5f;
                        var c = new Color(1f, 250 / 255f, 230 / 255f, a);
                        Quad(vh, e.X - L, e.Y - 0.4f, L * 2f, 0.8f, c);
                        Quad(vh, e.X - 0.4f, e.Y - L, 0.8f, L * 2f, c);
                    }
                }
            }
        }

        /// <summary>A point at parameter u anticlockwise around a rectangle's perimeter.</summary>
        static Vector2 RulePoint(float u, float x, float y, float w, float h)
        {
            float per = 2f * (w + h);
            float d = (1f - Mathf.Repeat(u, 1f)) * per;       // anticlockwise
            if (d < w) return new Vector2(x + d, y);
            d -= w;
            if (d < h) return new Vector2(x + w, y + d);
            d -= h;
            if (d < w) return new Vector2(x + w - d, y + h);
            d -= w;
            return new Vector2(x, y + h - d);
        }

        // ---------------------------------------------------------------
        //  mesh helpers
        // ---------------------------------------------------------------
        static void Quad(VertexHelper vh, float x, float y, float w, float h, Color c)
        {
            if (c.a <= 0.002f) return;
            Gradient(vh, x, y, w, h, c, c, c, c);
        }

        /// <summary>Corner colours run bottom-left, bottom-right, top-right, top-left.</summary>
        static void Gradient(VertexHelper vh, float x, float y, float w, float h,
                             Color bl, Color br, Color tr, Color tl)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector2(x, y), bl, new Vector2(0.5f, 0.5f));
            vh.AddVert(new Vector2(x + w, y), br, new Vector2(0.5f, 0.5f));
            vh.AddVert(new Vector2(x + w, y + h), tr, new Vector2(0.5f, 0.5f));
            vh.AddVert(new Vector2(x, y + h), tl, new Vector2(0.5f, 0.5f));
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        static void GradientH(VertexHelper vh, float x, float y, float w, float h, Color left, Color right)
        {
            Gradient(vh, x, y, w, h, left, right, right, left);
        }

        static void StrokeRect(VertexHelper vh, float x, float y, float w, float h, float t, Color c)
        {
            Quad(vh, x, y, w, t, c);
            Quad(vh, x, y + h - t, w, t, c);
            Quad(vh, x, y, t, h, c);
            Quad(vh, x + w - t, y, t, h, c);
        }

        /// <summary>
        /// A running stitch. Increasing the offset walks the dash pattern backwards along the
        /// path, which is what makes the felt's stitching march anticlockwise.
        /// </summary>
        static void DashedRect(VertexHelper vh, float x, float y, float w, float h,
                               float dash, float gap, float offset, float thick, Color c)
        {
            float per = 2f * (w + h);
            float step = dash + gap;
            float start = -Mathf.Repeat(offset, step);
            for (float d = start; d < per; d += step)
            {
                float a = Mathf.Max(d, 0f), b = Mathf.Min(d + dash, per);
                if (b <= a) continue;
                DashSegment(vh, a, b, x, y, w, h, thick, c);
            }
        }

        static void DashSegment(VertexHelper vh, float a, float b, float x, float y,
                                float w, float h, float t, Color c)
        {
            // clip the run against each side in turn, so a dash that straddles a corner draws
            // as two pieces rather than cutting across it
            Side(vh, a, b, 0f, w, x, y, w, t, true, false, c, t);
            Side(vh, a, b, w, h, x, y, h, t, false, true, c, t);
            Side(vh, a, b, w + h, w, x, y, w, t, true, true, c, t);
            Side(vh, a, b, 2f * w + h, h, x, y, h, t, false, false, c, t);
        }

        static void Side(VertexHelper vh, float a, float b, float from, float len,
                         float x, float y, float w, float h, bool horizontal, bool far,
                         Color c, float t)
        {
            float s = Mathf.Max(a, from), e = Mathf.Min(b, from + len);
            if (e <= s) return;
            float u0 = s - from, u1 = e - from;
            if (horizontal)
            {
                float yy = far ? y + h - t : y;
                float px = far ? x + w - u1 : x + u0;
                Quad(vh, px, yy, u1 - u0, t, c);
            }
            else
            {
                float xx = far ? x : x + w - t;   // +x side first, then -x on the way back
                float py = far ? y + h - u1 : y + u0;
                Quad(vh, xx, py, t, u1 - u0, c);
            }
        }

        /// <summary>A six-armed flake: three crossed bars, turned about its own centre.</summary>
        static void Flake(VertexHelper vh, float cx, float cy, float r, float rot, Color c)
        {
            if (c.a <= 0.004f) return;
            for (int arm = 0; arm < 3; arm++)
            {
                float a = rot + arm * Mathf.PI / 3f;
                float dx = Mathf.Cos(a) * r, dy = Mathf.Sin(a) * r;
                Bar(vh, cx - dx, cy - dy, cx + dx, cy + dy, 0.7f, c);
            }
        }

        static void Bar(VertexHelper vh, float x0, float y0, float x1, float y1, float t, Color c)
        {
            var d = new Vector2(x1 - x0, y1 - y0);
            float len = d.magnitude;
            if (len < 0.001f) return;
            var n = new Vector2(-d.y, d.x) / len * (t * 0.5f);
            int i = vh.currentVertCount;
            vh.AddVert(new Vector2(x0 - n.x, y0 - n.y), c, new Vector2(0.5f, 0.5f));
            vh.AddVert(new Vector2(x1 - n.x, y1 - n.y), c, new Vector2(0.5f, 0.5f));
            vh.AddVert(new Vector2(x1 + n.x, y1 + n.y), c, new Vector2(0.5f, 0.5f));
            vh.AddVert(new Vector2(x0 + n.x, y0 + n.y), c, new Vector2(0.5f, 0.5f));
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
