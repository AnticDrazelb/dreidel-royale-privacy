using System.Collections.Generic;
using UnityEngine;
using DreidelRoyale.Core;

namespace DreidelRoyale.UI
{
    /// <summary>
    /// The stylesheet's design tokens — surfaces, hairlines, radii, elevation — carried
    /// across so the UI keeps the same palette and weight as the web build.
    /// </summary>
    public static class Theme
    {
        public static readonly Color Night     = Hex.To("#05081a");
        public static readonly Color Night2    = Hex.To("#0d1430");
        public static readonly Color Night3    = Hex.To("#182254");
        public static readonly Color Gold      = Hex.To("#f2c14e");
        public static readonly Color GoldHot   = Hex.To("#ffe9a8");
        public static readonly Color GoldDeep  = Hex.To("#a06b1a");
        public static readonly Color BrassHi   = Hex.To("#ffdf8e");
        public static readonly Color Flame     = Hex.To("#ff9d45");
        public static readonly Color Text      = Hex.To("#f4f1e6");
        public static readonly Color Sub       = Hex.To("#9aa3c7");
        public static readonly Color Danger    = Hex.To("#ff5470");
        public static readonly Color Ok        = Hex.To("#57e6a8");

        public static readonly Color Card      = new Color(9 / 255f, 13 / 255f, 34 / 255f, 0.92f);
        public static readonly Color CardGlass = new Color(9 / 255f, 13 / 255f, 34 / 255f, 0.78f);

        public static readonly Color Surface1  = new Color(1, 1, 1, 0.04f);   // recessed groups
        public static readonly Color Surface2  = new Color(1, 1, 1, 0.06f);   // interactive resting
        public static readonly Color Surface3  = new Color(1, 1, 1, 0.09f);   // interactive hover/raised
        public static readonly Color Hairline  = new Color(1, 1, 1, 0.10f);
        public static readonly Color Hairline2 = new Color(1, 1, 1, 0.14f);

        public const float RSm = 13f, RMd = 14f, RLg = 16f, RXl = 22f;

        public static readonly Color ButtonText = Hex.To("#1c1204");
        public static readonly Color SpinText   = Hex.To("#4e3305");

        // ---- fonts ----
        static Font _display, _body;

        /// <summary>Secular One stands in for headings; Rubik for everything else.</summary>
        public static Font Display
        {
            get { return _display ?? (_display = Load("Secular One", "Rubik", "Arial Rounded MT Bold", "Trebuchet MS")); }
        }

        public static Font Body
        {
            get { return _body ?? (_body = Load("Rubik", "Segoe UI", "Helvetica Neue", "Arial")); }
        }

        static Font Load(params string[] names)
        {
            try
            {
                var f = Font.CreateDynamicFontFromOSFont(names, 32);
                if (f != null) return f;
            }
            catch { }
            // LegacyRuntime.ttf, not Arial.ttf. Unity removed Arial from its builtin
            // resources in 2022.2 and renamed the fallback; asking for the old name on
            // Unity 6 returns null, and a null font means every Text in the game draws
            // nothing at all — menus, HUD and result card alike, with no error.
            foreach (var builtin in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try
                {
                    var f = Resources.GetBuiltinResource<Font>(builtin);
                    if (f != null) return f;
                }
                catch { }
            }
            Debug.LogError("[Dreidel Royale] No font could be loaded. All text will be invisible.");
            return null;
        }

        // ---- sprites ----
        static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

        /// <summary>A nine-sliced rounded rectangle, in the pixel radii the stylesheet uses.</summary>
        public static Sprite Rounded(float radius)
        {
            string key = "r" + radius;
            Sprite s;
            if (SpriteCache.TryGetValue(key, out s) && s != null) return s;

            int r = Mathf.Max(2, Mathf.RoundToInt(radius));
            int size = r * 2 + 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedDist(x + 0.5f, y + 0.5f, size, size, r);
                    px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - d));
                }
            tex.SetPixels(px);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();

            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                              SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
            SpriteCache[key] = s;
            RadiusOf[s] = radius;
            return s;
        }

        /// <summary>A rounded outline — the hairline and inset-ring borders the panels wear.</summary>
        public static Sprite RoundedOutline(float radius, float lineWidth)
        {
            string key = "o" + radius + ":" + lineWidth;
            Sprite s;
            if (SpriteCache.TryGetValue(key, out s) && s != null) return s;

            int r = Mathf.Max(2, Mathf.RoundToInt(radius));
            int size = r * 2 + 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            float half = Mathf.Max(lineWidth, 1f) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedDist(x + 0.5f, y + 0.5f, size, size, r);
                    px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(half - Mathf.Abs(d + half) + 0.5f));
                }
            tex.SetPixels(px);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();

            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                              SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
            SpriteCache[key] = s;
            return s;
        }

        // Every rounded sprite remembers the radius it was cut at, so a square twin can be
        // found for it - and found again on the way back.
        static readonly Dictionary<Sprite, float> RadiusOf = new Dictionary<Sprite, float>();
        static readonly Dictionary<Sprite, Sprite> SquareOf = new Dictionary<Sprite, Sprite>();
        static readonly Dictionary<Sprite, Sprite> RoundOf = new Dictionary<Sprite, Sprite>();

        /// <summary>
        /// The voxel-chrome swap: hand it a rounded sprite and get the square one back, or the
        /// other way round. Anything it does not recognise comes back null and is left alone.
        /// </summary>
        public static Sprite Blockify(Sprite sprite, bool square)
        {
            if (sprite == null) return null;
            if (square)
            {
                Sprite found;
                if (SquareOf.TryGetValue(sprite, out found)) return found;
                float radius;
                if (!RadiusOf.TryGetValue(sprite, out radius)) return null;
                var hard = Rounded(2f);          // a two-pixel corner still antialiases cleanly
                if (hard == sprite) return null;
                SquareOf[sprite] = hard;
                RoundOf[hard] = sprite;
                return hard;
            }
            Sprite back;
            return RoundOf.TryGetValue(sprite, out back) ? back : null;
        }

        static float RoundedDist(float px, float py, float w, float h, float r)
        {
            float qx = Mathf.Abs(px - w * 0.5f) - (w * 0.5f - r);
            float qy = Mathf.Abs(py - h * 0.5f) - (h * 0.5f - r);
            return Mathf.Min(Mathf.Max(qx, qy), 0f)
                 + new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude - r;
        }

        public static Sprite Circle()
        {
            Sprite s;
            if (SpriteCache.TryGetValue("circle", out s) && s != null) return s;
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N / 2f, N / 2f));
                    px[y * N + x] = new Color(1, 1, 1, Mathf.Clamp01(N / 2f - d));
                }
            tex.SetPixels(px); tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
            SpriteCache["circle"] = s;
            return s;
        }

        /// <summary>A soft radial dot, for screen-space glows and particles.</summary>
        public static Sprite Dot()
        {
            Sprite s;
            if (SpriteCache.TryGetValue("dot", out s) && s != null) return s;
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N / 2f, N / 2f)) / (N / 2f);
                    px[y * N + x] = new Color(1, 1, 1, Mathf.Clamp01(1f - d));
                }
            tex.SetPixels(px); tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
            SpriteCache["dot"] = s;
            return s;
        }

        /// <summary>
        /// The spin button's face: a procedurally minted gelt coin — banded shading, a milled
        /// rim and a glint, struck at 4x and upscaled, so the band-boundary dither survives as
        /// a fine metal grain instead of reading as chunky pixels.
        /// </summary>
        public static Sprite SpinCoin()
        {
            Sprite s;
            if (SpriteCache.TryGetValue("spinCoin", out s) && s != null) return s;

            const int S = 384;
            float R = S / 2f;
            var tones = new[]
            {
                new Color(255/255f,237/255f,176/255f), new Color(249/255f,217/255f,126/255f),
                new Color(242/255f,193/255f,78/255f),  new Color(224/255f,171/255f,54/255f),
                new Color(192/255f,141/255f,34/255f),  new Color(148/255f,108/255f,20/255f)
            };
            float lx = S * 0.40f, ly = S * 0.36f;             // light sits upper-left
            float maxD = Mathf.Sqrt(S * 0.62f * (S * 0.62f) + S * 0.66f * (S * 0.66f));
            int[,] bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true);
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x + 0.5f - R, dy = y + 0.5f - R;
                    float rad = Mathf.Sqrt(dx * dx + dy * dy);
                    if (rad > R) { px[y * S + x] = Color.clear; continue; }

                    float d = Mathf.Sqrt((x - lx) * (x - lx) + (y - ly) * (y - ly)) / maxD;
                    float band = d * (tones.Length - 1);
                    int bi = Mathf.FloorToInt(band);
                    float frac = band - bi;
                    // ordered dither across the band boundary: fine grain, no OLED banding
                    if (frac > (bayer[y & 3, x & 3] + 0.5f) / 16f) bi++;
                    bi = Mathf.Clamp(bi, 0, tones.Length - 1);
                    Color c = tones[bi];

                    // milled rim
                    float rimT = rad / R;
                    if (rimT > 0.90f)
                    {
                        float a = Mathf.Atan2(dy, dx);
                        bool mill = Mathf.Repeat(a * 48f / (Mathf.PI * 2f), 1f) < 0.5f;
                        c = Color.Lerp(c, mill ? tones[1] : tones[4], 0.55f);
                    }
                    if (rimT > 0.965f) c = Color.Lerp(c, tones[5], 0.6f);

                    // glint: a soft specular streak toward the light
                    float g = Mathf.Clamp01(1f - Mathf.Sqrt((x - lx) * (x - lx) * 1.6f + (y - ly) * (y - ly)) / (S * 0.30f));
                    c = Color.Lerp(c, Color.white, g * g * 0.35f);

                    px[y * S + x] = new Color(c.r, c.g, c.b, Mathf.Clamp01(R - rad));
                }
            tex.SetPixels(px);
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply(true);
            s = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            SpriteCache["spinCoin"] = s;
            return s;
        }

        /// <summary>
        /// An annulus, for the power ring. The stylesheet draws it as a 132px rect with a
        /// 66px corner radius — which is a circle — so a radial-filled Image reproduces the
        /// sweep exactly, starting at top centre with no rotation needed.
        /// </summary>
        public static Sprite Ring(int size, float stroke)
        {
            string key = "ring" + size + ":" + stroke;
            Sprite s;
            if (SpriteCache.TryGetValue(key, out s) && s != null) return s;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            float R = size * 0.5f, mid = R - stroke * 0.5f, half = stroke * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(R, R));
                    px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(half - Mathf.Abs(d - mid) + 0.5f));
                }
            tex.SetPixels(px);
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            SpriteCache[key] = s;
            return s;
        }

        /// <summary>A vertical two-stop gradient sprite, for the menu swatches.</summary>
        public static Sprite Gradient(Color a, Color b, float angleDeg = 160f)
        {
            string key = "g" + ColorUtility.ToHtmlStringRGB(a) + ColorUtility.ToHtmlStringRGB(b) + angleDeg;
            Sprite cached;
            if (SpriteCache.TryGetValue(key, out cached) && cached != null) return cached;

            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color[N * N];
            float rad = angleDeg * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad));
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float t = Mathf.Clamp01(Vector2.Dot(new Vector2(x / (float)N - 0.5f, y / (float)N - 0.5f), dir) + 0.5f);
                    px[y * N + x] = Color.Lerp(a, b, t);
                }
            tex.SetPixels(px); tex.filterMode = FilterMode.Bilinear; tex.Apply();
            cached = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
            SpriteCache[key] = cached;
            return cached;
        }
    }
}
