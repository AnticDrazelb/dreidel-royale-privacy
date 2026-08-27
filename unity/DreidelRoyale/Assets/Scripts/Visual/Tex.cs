using System.Collections.Generic;
using UnityEngine;
using DreidelRoyale.Core;
using Stop = DreidelRoyale.Visual.Canvas2D.Stop;

namespace DreidelRoyale.Visual
{
    /// <summary>
    /// Every texture in the game, painted in code — same sizes, same passes and the same
    /// magic numbers as the canvas originals, so the tables and dreidels look like
    /// themselves rather than like a re-interpretation.
    /// </summary>
    public static class Tex
    {
        static readonly Dictionary<string, Texture2D> FaceCache = new Dictionary<string, Texture2D>();

        // ---------------- face plaques ----------------
        /// <summary>
        /// A transparent-cornered plaque: optionally translucent panel, gold double frame,
        /// then the letter in four layered passes. A single Hebrew glyph draws big (150px);
        /// custom Decision Dreidel labels are word-wrapped and auto-sized to fit.
        /// </summary>
        public static Texture2D Face(string letter, string skin)
        {
            if (!FaceStyles.All.ContainsKey(skin ?? "")) skin = "wood";
            string key = skin + ":" + (string.IsNullOrEmpty(letter) ? "blank" : letter);
            Texture2D cached;
            if (FaceCache.TryGetValue(key, out cached) && cached != null) return cached;

            var st = FaceStyles.Get(skin);
            var cv = new Canvas2D(256, 256);

            // transparent outside the plaque; the panel itself may be translucent glass
            cv.Save();
            cv.ClipRoundRect(8, 8, 240, 240, 34);
            var g = Canvas2D.RadialGradient(96, 84, 30, 128, 128, 190, new[]
            {
                new Stop(0f, st.Grad[0]), new Stop(0.55f, st.Grad[1]), new Stop(1f, st.Grad[2])
            });
            cv.FillRectShaded(0, 0, 256, 256, g);

            if (skin == "blocky")
            {
                // plank-pixel dither: the plaque reads as crafted wood
                for (int i = 0; i < 180; i++)
                {
                    var c = Random.value < 0.5f ? new Color(0, 0, 0, 0.10f) : new Color(1f, 235 / 255f, 200 / 255f, 0.07f);
                    cv.FillRect(Mathf.Floor(Random.value * 32) * 8, Mathf.Floor(Random.value * 32) * 8, 8, 8, c);
                }
            }
            cv.Restore();

            // gold frame — the one constant that ties every skin to the set
            cv.StrokeRoundRect(14, 14, 228, 228, 28, 12, Hex.To("#c08a24"));
            cv.StrokeRoundRect(21, 21, 214, 214, 22, 4, Hex.To("#f6d582"));

            if (!string.IsNullOrEmpty(letter))
            {
                bool isCustom = letter.Length > 2 || System.Text.RegularExpressions.Regex.IsMatch(letter, "[a-zA-Z0-9]");
                if (isCustom)
                {
                    var words = letter.ToUpper().Split(new[] { ' ', '\t', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
                    int size = 88;
                    List<string> lines = WrapFit(words, size, 196f);
                    while ((lines.Count > 3 || AnyTooWide(lines, size, 200f)) && size > 28)
                    {
                        size -= 6;
                        lines = WrapFit(words, size, 196f);
                    }
                    float lh = size * 1.05f;
                    bool ok;
                    var mask = GlyphRaster.Mask(lines, 256, size, lh, out ok);
                    if (ok)
                    {
                        var outline = GlyphRaster.Dilate(mask, 256, 4f);
                        GlyphRaster.Draw(cv, mask, 256, 2, 3, st.Shadow);
                        GlyphRaster.Draw(cv, outline, 256, 0, 0, st.Outline);
                        GlyphRaster.Draw(cv, mask, 256, 0, 0, st.Fill);
                    }
                }
                else
                {
                    bool ok;
                    var lines = new List<string> { letter };
                    var mask = GlyphRaster.Mask(lines, 256, 150, 150f, out ok);
                    if (ok)
                    {
                        // Canvas draws the glyph baseline-anchored at y=138 with the shadow at
                        // 146; TextMesh centres it, so the same 8px separation is applied here.
                        var outline = GlyphRaster.Dilate(mask, 256, 5f);
                        GlyphRaster.Draw(cv, mask, 256, 3, 8, st.Shadow);       // engraved shadow
                        GlyphRaster.Draw(cv, outline, 256, 0, 0, st.Outline);
                        GlyphRaster.Draw(cv, mask, 256, 0, 0, st.Fill);
                        GlyphRaster.Draw(cv, mask, 256, -2, -4, st.Sheen);
                    }
                }
            }

            var tex = cv.ToTexture(false, true, "face:" + key);
            FaceCache[key] = tex;
            return tex;
        }

        static List<string> WrapFit(string[] words, int size, float maxW)
        {
            var lines = new List<string>();
            string cur = "";
            foreach (var w in words)
            {
                string t = cur.Length > 0 ? cur + " " + w : w;
                if (GlyphRaster.Measure(t, size) > maxW && cur.Length > 0) { lines.Add(cur); cur = w; }
                else cur = t;
            }
            if (cur.Length > 0) lines.Add(cur);
            if (lines.Count == 0) lines.Add("");
            return lines;
        }

        static bool AnyTooWide(List<string> lines, int size, float maxW)
        {
            foreach (var l in lines) if (GlyphRaster.Measure(l, size) > maxW) return true;
            return false;
        }

        static readonly Dictionary<string, Texture2D> LetterCache = new Dictionary<string, Texture2D>();

        /// <summary>
        /// The letter alone, on a transparent ground, for the glyph that floats proud of the
        /// face. It shares its palette with the plaque beneath it but runs slightly brighter —
        /// it hovers above the face and catches more light.
        /// </summary>
        public static Texture2D Letter(string letter, string skin)
        {
            if (!FaceStyles.All.ContainsKey(skin ?? "")) skin = "wood";
            string key = skin + ":" + letter;
            Texture2D cached;
            if (LetterCache.TryGetValue(key, out cached) && cached != null) return cached;

            var st = FaceStyles.Get(skin);
            bool isIvory = skin == "wood" || skin == "gold";
            var cv = new Canvas2D(256, 256);

            bool ok;
            var mask = GlyphRaster.Mask(new List<string> { letter }, 256, 168, 168f, out ok);
            if (ok)
            {
                var outline = GlyphRaster.Dilate(mask, 256, 6f);
                GlyphRaster.Draw(cv, mask, 256, 4, 10, st.Shadow);      // a soft drop, so it reads as hovering
                GlyphRaster.Draw(cv, outline, 256, 0, 0, isIvory ? Hex.To("#12224f") : st.Outline);
                GlyphRaster.Draw(cv, mask, 256, 0, 0, isIvory ? Hex.To("#2f52d8") : st.Fill);
                GlyphRaster.Draw(cv, mask, 256, -2, -4, st.Sheen);
            }

            cached = cv.ToTexture(false, true, "letter:" + key);
            LetterCache[key] = cached;
            return cached;
        }

        /// <summary>The brass cap on the +Y face.</summary>
        public static Texture2D Top()
        {
            var cv = new Canvas2D(256, 256);
            cv.Save();
            cv.ClipRoundRect(8, 8, 240, 240, 34);
            cv.FillRectShaded(0, 0, 256, 256, Canvas2D.RadialGradient(90, 80, 20, 128, 128, 180, new[]
            {
                new Stop(0f, Hex.To("#ffe2a0")), new Stop(0.5f, Hex.To("#e5ae45")), new Stop(1f, Hex.To("#8f5c14"))
            }));
            cv.Restore();
            cv.StrokeRoundRect(14, 14, 228, 228, 28, 8, Hex.To("#6f4610"));
            return cv.ToTexture(false, true, "topCap");
        }

        // ---------------- ground ----------------
        public static Texture2D Ground(EnvDef env)
        {
            var cv = new Canvas2D(1024, 1024);
            cv.FillAll(env.Ground);

            if (env.Planks)
            {
                // wooden boards with grain
                for (int row = 0; row < 8; row++)
                {
                    float y = row * 128;
                    cv.FillRect(0, y, 1024, 128, row % 2 == 1 ? new Color(0, 0, 0, 0.10f) : new Color(1, 1, 1, 0.03f));
                    cv.StrokeLine(0, y, 1024, y, 3, new Color(0, 0, 0, 0.35f));
                    for (int gi = 0; gi < 7; gi++)
                    {
                        float gy = y + 14 + Random.value * 100;
                        var pts = new List<Vector2>();
                        for (int px = 0; px <= 1024; px += 64)
                            pts.Add(new Vector2(px, gy + Mathf.Sin(px * 0.01f + row) * 4 + Random.value * 2));
                        cv.StrokePath(pts, 1.5f, new Color(0, 0, 0, 0.12f));
                    }
                    float seam = (row * 397) % 1024;
                    cv.StrokeLine(seam, y, seam, y + 128, 2, new Color(0, 0, 0, 0.3f));
                }
            }
            else if (env.Room)
            {
                // flat playroom floor: solid plank rows, hard seams, zero shading — and the
                // big round rug the game happens on, dashes and all
                var planks = new[] { Hex.To("#b4743e"), Hex.To("#ac6c38"), Hex.To("#ba7a44"), Hex.To("#a86836") };
                for (int row = 0; row < 10; row++)
                {
                    cv.FillRect(0, row * 103, 1024, 103, planks[row % planks.Length]);
                    cv.FillRect(0, row * 103, 1024, 4, Hex.To("rgba(90,50,20,0.55)"));
                    float seam = ((row * 367) % 900) + 60;
                    cv.FillRect(seam, row * 103, 4, 103, Hex.To("rgba(90,50,20,0.55)"));
                }
                cv.FillCircle(512, 512, 300, Hex.To("#9ccd6a"));
                cv.FillCircle(512, 512, 215, Hex.To("#f2eecb"));
                for (int d = 0; d < 12; d++)
                {
                    float a = d / 12f * Mathf.PI * 2f;
                    float cx = 512 + Mathf.Cos(a) * 178, cy = 512 + Mathf.Sin(a) * 178;
                    cv.FillEllipse(cx, cy, 16, 7, a, Hex.To("#4a4438"));
                }
            }
            else if (env.Lawn)
            {
                var gs = new[] { new Color(1,1,1,0.07f), new Color(60/255f,120/255f,40/255f,0.10f), new Color(120/255f,190/255f,80/255f,0.12f) };
                for (int i = 0; i < 90; i++)
                {
                    float bx = Random.value * 1024, by = Random.value * 1024, br = 30 + Random.value * 70;
                    var col = gs[Random.Range(0, gs.Length)];
                    cv.FillRectShaded(bx - br, by - br, br * 2, br * 2, Canvas2D.RadialGradient(bx, by, 4, bx, by, br,
                        new[] { new Stop(0f, col), new Stop(1f, new Color(0, 0, 0, 0)) }));
                }
                for (int s = 0; s < 8; s++) if (s % 2 == 1) cv.FillRect(0, s * 128, 1024, 128, new Color(1, 1, 1, 0.045f));
                for (int d = 0; d < 26; d++)
                {
                    float dx = Random.value * 1024, dy = Random.value * 1024;
                    for (int p = 0; p < 5; p++)
                    {
                        float a = p / 5f * 6.283f;
                        cv.FillEllipse(dx + Mathf.Cos(a) * 5, dy + Mathf.Sin(a) * 5, 3.4f, 2.2f, a, Hex.To("rgba(255,252,240,0.85)"));
                    }
                    cv.FillCircle(dx, dy, 3, Hex.To("rgba(255,206,84,0.95)"));
                }
            }
            else if (env.Blocks)
            {
                // voxel turf: an 8x8 grid of grass blocks, each dithered with 4x4 pixel noise
                var greens = new[] { "#5d9440", "#549038", "#67a047", "#4f8a34", "#6ba84c", "#588f3c" };
                for (int by = 0; by < 8; by++)
                    for (int bx = 0; bx < 8; bx++)
                    {
                        int ox = bx * 128, oy = by * 128;
                        cv.FillRect(ox, oy, 128, 128, Hex.To(greens[(bx * 7 + by * 13) % greens.Length]));
                        for (int py = 0; py < 4; py++)
                            for (int px = 0; px < 4; px++)
                                if (Random.value < 0.6f)
                                    cv.FillRect(ox + px * 32, oy + py * 32, 32, 32, Hex.To(greens[Random.Range(0, greens.Length)]));
                        if (Random.value < 0.10f)
                            cv.FillRect(ox + 32 * Random.Range(0, 4), oy + 32 * Random.Range(0, 4), 32, 32, Hex.To("#7a5a34"));
                        if (Random.value < 0.08f)
                            cv.FillRect(ox + 8 + 32 * Random.Range(0, 4), oy + 8 + 32 * Random.Range(0, 4), 14, 14,
                                        Hex.To(Random.value < 0.5f ? "#f2e05a" : "#e86a6a"));
                    }
                for (int i = 0; i <= 8; i++)
                {
                    float p = i * 128;
                    cv.StrokeLine(p, 0, p, 1024, 3, Hex.To("rgba(20,40,12,0.35)"));
                    cv.StrokeLine(0, p, 1024, p, 3, Hex.To("rgba(20,40,12,0.35)"));
                }
            }
            else
            {
                for (int i = 0; i <= 16; i++)
                {
                    float p = i * 64;
                    cv.StrokeLine(p, 0, p, 1024, 2, env.Grid);
                    cv.StrokeLine(0, p, 1024, p, 2, env.Grid);
                }
            }

            // centre pool (skipped for flat rooms — gradients would break the 2D look)
            if (!env.Room)
            {
                cv.FillRectShaded(0, 0, 1024, 1024, Canvas2D.RadialGradient(512, 512, 60, 512, 512, 512, new[]
                {
                    new Stop(0f, env.Pool),
                    new Stop(0.4f, Hex.To("rgba(60,70,150,0.06)")),
                    new Stop(1f, new Color(5/255f, 8/255f, 26/255f, 0f))
                }));
            }

            return cv.ToTexture(env.Blocks, true, "ground:" + env.Id);
        }

        // ---------------- dreidel body textures ----------------
        public static Texture2D Wood()
        {
            var cv = new Canvas2D(256, 256);
            cv.FillRectShaded(0, 0, 256, 256, Canvas2D.LinearGradient(0, 0, 256, 0, new[]
            {
                new Stop(0f, Hex.To("#9a6631")), new Stop(0.5f, Hex.To("#8a5a2b")), new Stop(1f, Hex.To("#7c4f24"))
            }));
            for (int i = 0; i < 26; i++)
            {
                float gx = Random.value * 256;
                var col = new Color(60 / 255f, 35 / 255f, 12 / 255f, 0.10f + Random.value * 0.18f);
                var pts = new List<Vector2>();
                for (int y = 0; y <= 256; y += 32)
                    pts.Add(new Vector2(gx + Mathf.Sin(y * 0.05f + i) * 6 + Random.value * 3, y));
                cv.StrokePath(pts, 1f + Random.value * 2.2f, col);
            }
            for (int k = 0; k < 2; k++)
            {
                float kx = 60 + Random.value * 140, ky = 50 + Random.value * 160;
                for (float r = 9; r > 1; r -= 2)
                    cv.StrokeEllipse(kx, ky, r * 1.5f, r, 0.3f, 1.6f, new Color(55 / 255f, 32 / 255f, 10 / 255f, 0.28f - r * 0.02f));
            }
            return cv.ToTexture(false, true, "wood");
        }

        /// <summary>16x16 speckled voxel texture; point-filtered so the pixels stay square.</summary>
        public static Texture2D Pixel(string baseHex, string[] speckles, float density = 0.55f)
        {
            var cv = new Canvas2D(64, 64);
            cv.FillAll(Hex.To(baseHex));
            for (int py = 0; py < 16; py++)
                for (int px = 0; px < 16; px++)
                    if (Random.value < density)
                        cv.FillRect(px * 4, py * 4, 4, 4, Hex.To(speckles[Random.Range(0, speckles.Length)]));
            var t = cv.ToTexture(true, false, "pixel");
            t.wrapMode = TextureWrapMode.Repeat;
            return t;
        }

        /// <summary>Grass-block side: dirt with a ragged green fringe over the top edge.</summary>
        public static Texture2D GrassSide()
        {
            var cv = new Canvas2D(64, 64);
            cv.FillAll(Hex.To("#7a5533"));
            var dirts = new[] { "#6e4b2c", "#86603a", "#5f4226", "#7a5533", "#8a6540" };
            for (int py = 0; py < 16; py++)
                for (int px = 0; px < 16; px++)
                    if (Random.value < 0.55f) cv.FillRect(px * 4, py * 4, 4, 4, Hex.To(dirts[Random.Range(0, dirts.Length)]));
            var greens = new[] { "#5d9440", "#6ba84c", "#4f8a34" };
            for (int px = 0; px < 16; px++)
            {
                int depth = 1 + Random.Range(0, 3);
                for (int py = 0; py < depth; py++)
                    cv.FillRect(px * 4, py * 4, 4, 4, Hex.To(greens[Random.Range(0, greens.Length)]));
            }
            var t = cv.ToTexture(true, false, "grassSide");
            t.wrapMode = TextureWrapMode.Repeat;
            return t;
        }

        /// <summary>
        /// Heeler coat: soft two-tone blue with darker patches and one tan spot, gently
        /// mottled so it reads as fur-adjacent rather than plastic.
        /// </summary>
        public static Texture2D Heeler()
        {
            var cv = new Canvas2D(256, 256);
            cv.FillAll(Hex.To("#6aaede"));
            var patches = new[] { new Vector3(40, 60, 58), new Vector3(210, 50, 48), new Vector3(190, 200, 62), new Vector3(60, 205, 44) };
            foreach (var p in patches)
            {
                cv.FillRectShaded(p.x - p.z, p.y - p.z, p.z * 2, p.z * 2,
                    Canvas2D.RadialGradient(p.x, p.y, p.z * 0.2f, p.x, p.y, p.z, new[]
                    {
                        new Stop(0f, Hex.To("#3f719f")), new Stop(0.75f, Hex.To("#4a80b2")),
                        new Stop(1f, new Color(74/255f,128/255f,178/255f,0f))
                    }));
            }
            cv.FillRectShaded(128 - 42, 150 - 42, 84, 84,
                Canvas2D.RadialGradient(128, 150, 8, 128, 150, 42, new[]
                {
                    new Stop(0f, Hex.To("#dcaa6c")), new Stop(0.7f, Hex.To("#d8a86a")),
                    new Stop(1f, new Color(216/255f,168/255f,106/255f,0f))
                }));
            for (int i = 0; i < 160; i++)
                cv.FillCircle(Random.value * 256, Random.value * 256, 2 + Random.value * 4,
                    Random.value < 0.5f ? new Color(1, 1, 1, 0.05f) : new Color(30 / 255f, 60 / 255f, 100 / 255f, 0.05f));
            return cv.ToTexture(false, true, "heeler");
        }

        // ---------------- glows and skies ----------------
        public static Texture2D Aura()
        {
            var cv = new Canvas2D(256, 256);
            cv.FillRectShaded(0, 0, 256, 256, Canvas2D.RadialGradient(128, 128, 10, 128, 128, 128, new[]
            {
                new Stop(0f, Hex.To("rgba(255,225,150,0.9)")),
                new Stop(0.5f, Hex.To("rgba(242,193,78,0.35)")),
                new Stop(1f, Hex.To("rgba(242,193,78,0)"))
            }));
            return cv.ToTexture(false, true, "aura");
        }

        /// <summary>Soft radial sprite used for every halo, pool and bloom stand-in.</summary>
        public static Texture2D Radial(params Stop[] stops)
        {
            var cv = new Canvas2D(128, 128);
            cv.FillRectShaded(0, 0, 128, 128, Canvas2D.RadialGradient(64, 64, 0, 64, 64, 64, stops));
            return cv.ToTexture(false, true, "radial");
        }

        public static Texture2D Flame()
        {
            var cv = new Canvas2D(64, 96);
            cv.FillRectShaded(0, 0, 64, 96, Canvas2D.RadialGradient(32, 60, 4, 32, 52, 44, new[]
            {
                new Stop(0f, Hex.To("rgba(255,250,220,1)")),
                new Stop(0.35f, Hex.To("rgba(255,190,80,0.9)")),
                new Stop(0.7f, Hex.To("rgba(255,120,30,0.35)")),
                new Stop(1f, Hex.To("rgba(255,90,20,0)"))
            }));
            return cv.ToTexture(false, true, "flame");
        }

        /// <summary>
        /// The oil's thickness ramp: pale where the film is thin, saturated and near-black
        /// where it is deep. The fluid writes a normalised depth into the surface mesh's u,
        /// so sampling this is Beer–Lambert absorption done by the texture unit — no custom
        /// shader, and it works on the stock Standard material every other piece uses.
        /// </summary>
        public static Texture2D OilDepth()
        {
            if (_oilDepth != null) return _oilDepth;
            const int W = 64;
            var cv = new Canvas2D(W, 4);
            cv.FillRectShaded(0, 0, W, 4, Canvas2D.LinearGradient(0, 0, W, 0, new[]
            {
                new Stop(0f,    Hex.To("#d8a154")),   // a thin film, lit right through
                new Stop(0.35f, Hex.To("#a4661f")),
                new Stop(0.7f,  Hex.To("#6b3c0b")),
                new Stop(1f,    Hex.To("#3a1d02"))    // deep oil, almost none of the light returns
            }));
            _oilDepth = cv.ToTexture(false, false, "oilDepth");
            _oilDepth.wrapMode = TextureWrapMode.Clamp;
            return _oilDepth;
        }

        static Texture2D _oilDepth;

        /// <summary>
        /// The table's environment map — the thing every metal, gem and pane of glass in the
        /// scene is actually reflecting.
        ///
        /// A bright radial top, a flat floor, and four side faces carrying a vertical gradient
        /// with a bright horizontal strip across them: a studio light-bar. That strip is the
        /// whole point. A metal with nothing to reflect renders as a flat dark colour no matter
        /// how good its shading is, and the moving glint of a light-bar sliding across a
        /// spinning gold body is most of what reads as "metal" to the eye.
        ///
        /// Cached per table, because six 128px faces is not something to rebuild on a spin.
        /// </summary>
        public static Cubemap EnvCube(EnvDef env)
        {
            Cubemap cached;
            if (_envCubes.TryGetValue(env.Name, out cached) && cached != null) return cached;

            const int S = 128;
            var cube = new Cubemap(S, TextureFormat.RGBA32, true) { name = "envCube-" + env.Name };
            cube.filterMode = FilterMode.Bilinear;

            var top = new Canvas2D(S, S);
            top.FillRectShaded(0, 0, S, S, Canvas2D.RadialGradient(S / 2f, S / 2f, 4f, S / 2f, S / 2f, S * 0.7f,
                new[] { new Stop(0f, env.CubeHi), new Stop(1f, env.CubeMid) }));

            var bottom = new Canvas2D(S, S);
            bottom.FillRect(0, 0, S, S, env.CubeLo);

            var side = new Canvas2D(S, S);
            side.FillRectShaded(0, 0, S, S, Canvas2D.LinearGradient(0, 0, 0, S, new[]
            {
                new Stop(0f,    env.CubeMid),
                new Stop(0.42f, env.CubeHi),
                new Stop(0.52f, env.CubeMid),
                new Stop(1f,    env.CubeLo)
            }));
            side.FillRect(0, S * 0.39f, S, S * 0.08f, new Color(1f, 1f, 1f, 0.30f));

            SetFace(cube, CubemapFace.PositiveX, side);
            SetFace(cube, CubemapFace.NegativeX, side);
            SetFace(cube, CubemapFace.PositiveZ, side);
            SetFace(cube, CubemapFace.NegativeZ, side);
            SetFace(cube, CubemapFace.PositiveY, top);
            SetFace(cube, CubemapFace.NegativeY, bottom);
            cube.Apply(true);

            _envCubes[env.Name] = cube;
            return cube;
        }

        static readonly Dictionary<string, Cubemap> _envCubes = new Dictionary<string, Cubemap>();

        static void SetFace(Cubemap cube, CubemapFace face, Canvas2D cv)
        {
            // Canvas2D is Y-down and a cube face is read Y-up, so the rows go back the way
            // ToTexture flips them - done here rather than allocating a Texture2D per face.
            var px = new Color[cv.W * cv.H];
            for (int y = 0; y < cv.H; y++)
                for (int x = 0; x < cv.W; x++)
                    px[(cv.H - 1 - y) * cv.W + x] = cv.Get(x, y);
            cube.SetPixels(px, face);
        }

        /// <summary>Sky dome: the table's vertical gradient plus its nebula blobs.</summary>
        public static Texture2D Sky(EnvDef env)
        {
            var cv = new Canvas2D(256, 256);
            var stops = new List<Stop>();
            foreach (var s in env.Sky) stops.Add(new Stop(s.T, s.C));
            cv.FillRectShaded(0, 0, 256, 256, Canvas2D.LinearGradient(0, 0, 0, 256, stops));
            foreach (var n in env.Nebs)
                cv.FillRectShaded(n.X - n.R, n.Y - n.R, n.R * 2, n.R * 2,
                    Canvas2D.RadialGradient(n.X, n.Y, 0, n.X, n.Y, n.R,
                        new[] { new Stop(0f, n.C), new Stop(1f, new Color(n.C.r, n.C.g, n.C.b, 0f)) }));
            return cv.ToTexture(false, true, "sky:" + env.Id);
        }

        /// <summary>
        /// The Oil Miracle's fill gradient: bright amber right at the surface, darkening
        /// steeply to near-black by the bottom, so there is no floating box and no hard band.
        /// </summary>
        public static Texture2D OilSide()
        {
            var cv = new Canvas2D(4, 128);
            cv.FillRectShaded(0, 0, 4, 128, Canvas2D.LinearGradient(0, 128, 0, 0, new[]
            {
                new Stop(0f,    Hex.To("#050300")),   // bottom: near-black
                new Stop(0.35f, Hex.To("#0d0701")),
                new Stop(0.70f, Hex.To("#1d1002")),
                new Stop(0.92f, Hex.To("#3a2204")),
                new Stop(1f,    Hex.To("#7a4a0a"))    // just below the surface: amber
            }));
            return cv.ToTexture(false, false, "oilSide");
        }

        /// <summary>Bump map for the wood grain — luminance of a re-rolled grain pass.</summary>
        public static Texture2D WoodBump()
        {
            var cv = new Canvas2D(256, 256);
            cv.FillAll(new Color(0.5f, 0.5f, 0.5f, 1f));
            for (int i = 0; i < 30; i++)
            {
                float gx = Random.value * 256;
                var pts = new List<Vector2>();
                for (int y = 0; y <= 256; y += 24)
                    pts.Add(new Vector2(gx + Mathf.Sin(y * 0.05f + i) * 6 + Random.value * 3, y));
                cv.StrokePath(pts, 1f + Random.value * 2f, new Color(0.25f, 0.25f, 0.25f, 0.5f));
            }
            var t = cv.ToTexture(false, true, "woodBump");
            return t;
        }

        public static void ClearFaceCache()
        {
            foreach (var kv in FaceCache) if (kv.Value != null) Object.Destroy(kv.Value);
            FaceCache.Clear();
            foreach (var kv in LetterCache) if (kv.Value != null) Object.Destroy(kv.Value);
            LetterCache.Clear();
        }
    }
}
