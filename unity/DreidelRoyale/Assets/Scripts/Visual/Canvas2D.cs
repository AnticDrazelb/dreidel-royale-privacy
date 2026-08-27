using System;
using System.Collections.Generic;
using UnityEngine;

namespace DreidelRoyale.Visual
{
    /// <summary>
    /// A small CPU raster surface with the slice of the HTML canvas 2D API the artwork
    /// actually uses: source-over compositing, linear and radial gradients, rounded-rect
    /// paths as clips or strokes, arcs and ellipses. Every texture in the web build is
    /// painted with these calls, so porting the drawing code one-for-one keeps the art
    /// identical instead of approximate.
    ///
    /// Y runs DOWNWARD, as in canvas. ToTexture flips into Unity's bottom-up convention.
    /// </summary>
    public class Canvas2D
    {
        public readonly int W, H;
        readonly Color[] _px;
        float[] _clip;              // null = unclipped; else per-pixel coverage 0..1

        public Canvas2D(int w, int h)
        {
            W = w; H = h;
            _px = new Color[w * h];   // starts fully transparent, like a fresh canvas
        }

        // ---------- compositing ----------
        public void Blend(int x, int y, Color src)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            float cov = _clip == null ? 1f : _clip[y * W + x];
            if (cov <= 0f) return;
            float a = src.a * cov;
            if (a <= 0f) return;
            int i = y * W + x;
            var dst = _px[i];
            float outA = a + dst.a * (1f - a);
            if (outA <= 0f) { _px[i] = Color.clear; return; }
            _px[i] = new Color(
                (src.r * a + dst.r * dst.a * (1f - a)) / outA,
                (src.g * a + dst.g * dst.a * (1f - a)) / outA,
                (src.b * a + dst.b * dst.a * (1f - a)) / outA,
                outA);
        }

        public Color Get(int x, int y)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return Color.clear;
            return _px[y * W + x];
        }

        // ---------- clipping ----------
        public void Save() { _clipStack.Push(_clip == null ? null : (float[])_clip.Clone()); }
        public void Restore() { if (_clipStack.Count > 0) _clip = _clipStack.Pop(); }
        readonly Stack<float[]> _clipStack = new Stack<float[]>();

        /// <summary>Intersect the clip with a rounded rectangle (antialiased edge).</summary>
        public void ClipRoundRect(float x, float y, float w, float h, float r)
        {
            var mask = new float[W * H];
            for (int py = 0; py < H; py++)
                for (int px = 0; px < W; px++)
                    mask[py * W + px] = RoundRectCoverage(px + 0.5f, py + 0.5f, x, y, w, h, r);
            if (_clip == null) _clip = mask;
            else for (int i = 0; i < mask.Length; i++) _clip[i] *= mask[i];
        }

        public void ClipNone() { _clip = null; }

        static float RoundRectCoverage(float px, float py, float x, float y, float w, float h, float r)
        {
            // signed distance to a rounded box, turned into a 1px antialiased edge
            float cx = x + w * 0.5f, cy = y + h * 0.5f;
            float qx = Mathf.Abs(px - cx) - (w * 0.5f - r);
            float qy = Mathf.Abs(py - cy) - (h * 0.5f - r);
            float d = Mathf.Min(Mathf.Max(qx, qy), 0f)
                    + new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude - r;
            return Mathf.Clamp01(0.5f - d);
        }

        // ---------- fills ----------
        public void FillRect(float x, float y, float w, float h, Color c)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            int x1 = Mathf.CeilToInt(x + w), y1 = Mathf.CeilToInt(y + h);
            for (int py = y0; py < y1; py++)
                for (int px = x0; px < x1; px++)
                    Blend(px, py, c);
        }

        public void FillAll(Color c) { FillRect(0, 0, W, H, c); }

        public delegate Color Shader(float x, float y);

        public void FillRectShaded(float x, float y, float w, float h, Shader s)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(x)), y0 = Mathf.Max(0, Mathf.FloorToInt(y));
            int x1 = Mathf.Min(W, Mathf.CeilToInt(x + w)), y1 = Mathf.Min(H, Mathf.CeilToInt(y + h));
            for (int py = y0; py < y1; py++)
                for (int px = x0; px < x1; px++)
                    Blend(px, py, s(px + 0.5f, py + 0.5f));
        }

        public void FillCircle(float cx, float cy, float r, Color c)
        {
            int x0 = Mathf.FloorToInt(cx - r) - 1, x1 = Mathf.CeilToInt(cx + r) + 1;
            int y0 = Mathf.FloorToInt(cy - r) - 1, y1 = Mathf.CeilToInt(cy + r) + 1;
            for (int py = y0; py < y1; py++)
                for (int px = x0; px < x1; px++)
                {
                    float d = Mathf.Sqrt((px + 0.5f - cx) * (px + 0.5f - cx) + (py + 0.5f - cy) * (py + 0.5f - cy));
                    float cov = Mathf.Clamp01(r - d + 0.5f);
                    if (cov > 0f) Blend(px, py, new Color(c.r, c.g, c.b, c.a * cov));
                }
        }

        public void FillEllipse(float cx, float cy, float rx, float ry, float rot, Color c)
        {
            float m = Mathf.Max(rx, ry) + 2f;
            float cs = Mathf.Cos(-rot), sn = Mathf.Sin(-rot);
            for (int py = Mathf.FloorToInt(cy - m); py < Mathf.CeilToInt(cy + m); py++)
                for (int px = Mathf.FloorToInt(cx - m); px < Mathf.CeilToInt(cx + m); px++)
                {
                    float dx = px + 0.5f - cx, dy = py + 0.5f - cy;
                    float u = (dx * cs - dy * sn) / rx, v = (dx * sn + dy * cs) / ry;
                    float d = Mathf.Sqrt(u * u + v * v);
                    float cov = Mathf.Clamp01((1f - d) * Mathf.Min(rx, ry) + 0.5f);
                    if (cov > 0f) Blend(px, py, new Color(c.r, c.g, c.b, c.a * cov));
                }
        }

        // ---------- strokes ----------
        public void StrokeRoundRect(float x, float y, float w, float h, float r, float lineWidth, Color c)
        {
            float half = lineWidth * 0.5f;
            float cx = x + w * 0.5f, cy = y + h * 0.5f;
            for (int py = 0; py < H; py++)
                for (int px = 0; px < W; px++)
                {
                    float qx = Mathf.Abs(px + 0.5f - cx) - (w * 0.5f - r);
                    float qy = Mathf.Abs(py + 0.5f - cy) - (h * 0.5f - r);
                    float d = Mathf.Min(Mathf.Max(qx, qy), 0f)
                            + new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude - r;
                    float cov = Mathf.Clamp01(half - Mathf.Abs(d) + 0.5f);
                    if (cov > 0f) Blend(px, py, new Color(c.r, c.g, c.b, c.a * cov));
                }
        }

        public void StrokeLine(float x0, float y0, float x1, float y1, float lineWidth, Color c)
        {
            float half = Mathf.Max(lineWidth * 0.5f, 0.5f);
            var a = new Vector2(x0, y0); var b = new Vector2(x1, y1);
            var ab = b - a; float len2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
            int mx0 = Mathf.FloorToInt(Mathf.Min(x0, x1) - half - 1), mx1 = Mathf.CeilToInt(Mathf.Max(x0, x1) + half + 1);
            int my0 = Mathf.FloorToInt(Mathf.Min(y0, y1) - half - 1), my1 = Mathf.CeilToInt(Mathf.Max(y0, y1) + half + 1);
            for (int py = my0; py < my1; py++)
                for (int px = mx0; px < mx1; px++)
                {
                    var p = new Vector2(px + 0.5f, py + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    float d = (p - (a + ab * t)).magnitude;
                    float cov = Mathf.Clamp01(half - d + 0.5f);
                    if (cov > 0f) Blend(px, py, new Color(c.r, c.g, c.b, c.a * cov));
                }
        }

        /// <summary>Polyline through the given points, as canvas moveTo/lineTo/stroke does.</summary>
        public void StrokePath(IList<Vector2> pts, float lineWidth, Color c)
        {
            for (int i = 1; i < pts.Count; i++)
                StrokeLine(pts[i - 1].x, pts[i - 1].y, pts[i].x, pts[i].y, lineWidth, c);
        }

        public void StrokeEllipse(float cx, float cy, float rx, float ry, float rot, float lineWidth, Color c, int segs = 48)
        {
            var pts = new List<Vector2>(segs + 1);
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            for (int i = 0; i <= segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                float x = Mathf.Cos(a) * rx, y = Mathf.Sin(a) * ry;
                pts.Add(new Vector2(cx + x * cs - y * sn, cy + x * sn + y * cs));
            }
            StrokePath(pts, lineWidth, c);
        }

        // ---------- gradients ----------
        public struct Stop { public float T; public Color C; public Stop(float t, Color c) { T = t; C = c; } }

        public static Color Sample(IList<Stop> stops, float t)
        {
            if (stops.Count == 0) return Color.clear;
            if (t <= stops[0].T) return stops[0].C;
            for (int i = 1; i < stops.Count; i++)
            {
                if (t <= stops[i].T)
                {
                    float span = stops[i].T - stops[i - 1].T;
                    float k = span <= 1e-6f ? 1f : (t - stops[i - 1].T) / span;
                    return Color.Lerp(stops[i - 1].C, stops[i].C, k);
                }
            }
            return stops[stops.Count - 1].C;
        }

        /// <summary>canvas createRadialGradient(x0,y0,r0,x1,y1,r1) semantics, simplified to concentric.</summary>
        public static Shader RadialGradient(float x0, float y0, float r0, float x1, float y1, float r1, IList<Stop> stops)
        {
            return (px, py) =>
            {
                float d = Mathf.Sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));
                float t = (r1 - r0) <= 1e-6f ? 1f : Mathf.Clamp01((d - r0) / (r1 - r0));
                return Sample(stops, t);
            };
        }

        public static Shader LinearGradient(float x0, float y0, float x1, float y1, IList<Stop> stops)
        {
            var a = new Vector2(x0, y0); var ab = new Vector2(x1 - x0, y1 - y0);
            float len2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
            return (px, py) =>
            {
                float t = Mathf.Clamp01(Vector2.Dot(new Vector2(px, py) - a, ab) / len2);
                return Sample(stops, t);
            };
        }

        // ---------- output ----------
        public Texture2D ToTexture(bool point = false, bool mips = true, string name = "tex")
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, mips, false) { name = name };
            var flipped = new Color[W * H];
            for (int y = 0; y < H; y++)
                Array.Copy(_px, y * W, flipped, (H - 1 - y) * W, W);   // canvas Y-down -> Unity Y-up
            t.SetPixels(flipped);
            t.filterMode = point ? FilterMode.Point : FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Repeat;
            t.anisoLevel = point ? 0 : 4;
            t.Apply(mips);
            return t;
        }
    }
}
