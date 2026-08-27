using System.Collections.Generic;
using UnityEngine;

namespace DreidelRoyale.Visual
{
    /// <summary>
    /// Rasterises text into a coverage mask so the plaque art can layer it the way the
    /// canvas original does: an engraved shadow, a heavy outline, the fill, then a sheen —
    /// four offset draws of the same glyph, not one flat label.
    ///
    /// The web build asks for 'Secular One' and falls back through Arial Hebrew and Noto
    /// Sans Hebrew; the same chain is requested from the OS here. Rendering goes through a
    /// throwaway orthographic camera, which is the one reliable way to get real glyph
    /// outlines onto the CPU at runtime.
    /// </summary>
    public static class GlyphRaster
    {
        static Font _font;

        /// <summary>
        /// A layer of its own for the offscreen text rig. Without it the glyph camera's
        /// culling mask takes in the whole scene, and every plaque comes out with the table
        /// baked into it.
        /// </summary>
        const int GlyphLayer = 31;
        static readonly string[] FontChain =
        {
            "Secular One", "Arial Hebrew", "Noto Sans Hebrew", "Noto Sans Hebrew UI",
            "David", "Times New Roman", "Arial Unicode MS", "Arial", "DejaVu Sans", "Liberation Sans"
        };

        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    try { _font = UnityEngine.Font.CreateDynamicFontFromOSFont(FontChain, 128); }
                    catch { _font = null; }
                    if (_font == null)
                    {
                        try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
                    }
                }
                return _font;
            }
        }

        /// <summary>Advance width of a string at a given pixel size, for greedy word wrapping.</summary>
        public static float Measure(string s, int px)
        {
            var f = Font;
            if (f == null || string.IsNullOrEmpty(s)) return 0f;
            f.RequestCharactersInTexture(s, px, FontStyle.Bold);
            float w = 0f;
            foreach (var ch in s)
            {
                CharacterInfo ci;
                if (f.GetCharacterInfo(ch, out ci, px, FontStyle.Bold)) w += ci.advance;
                else w += px * 0.5f;
            }
            return w;
        }

        /// <summary>
        /// Render `lines` centred in a square of `size` pixels and return per-pixel coverage
        /// (0..1, canvas Y-down). `pxSize` is the glyph size in those same pixels.
        /// </summary>
        public static float[] Mask(IList<string> lines, int size, int pxSize, float lineHeight, out bool ok)
        {
            ok = false;
            var mask = new float[size * size];
            if (lines == null || lines.Count == 0) return mask;

            var f = Font;
            if (f == null) return mask;

            GameObject rig = null;
            RenderTexture rt = null;
            RenderTexture prev = RenderTexture.active;
            try
            {
                rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                rt.antiAliasing = 1;

                // parked far from the diorama, and on its own layer, so nothing else can
                // wander into frame
                rig = new GameObject("~glyphRig") { hideFlags = HideFlags.HideAndDontSave };
                rig.layer = GlyphLayer;
                rig.transform.position = new Vector3(0f, -10000f, 0f);
                var camGo = new GameObject("cam") { hideFlags = HideFlags.HideAndDontSave };
                camGo.transform.SetParent(rig.transform, false);
                camGo.layer = GlyphLayer;
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = size * 0.5f;      // 1 world unit == 1 pixel
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
                cam.cullingMask = 1 << GlyphLayer;
                cam.targetTexture = rt;
                cam.transform.localPosition = new Vector3(0, 0, -10f);
                cam.allowMSAA = false;
                cam.allowHDR = false;

                // The layer stack is drawn once in pure white; colour and offset are applied
                // later on the CPU, so a single render serves all four passes.
                float total = (lines.Count - 1) * lineHeight;
                for (int i = 0; i < lines.Count; i++)
                {
                    var go = new GameObject("t" + i) { hideFlags = HideFlags.HideAndDontSave };
                    go.layer = GlyphLayer;
                    go.transform.SetParent(rig.transform, false);
                    var tm = go.AddComponent<TextMesh>();
                    tm.text = lines[i];
                    tm.font = f;
                    tm.fontSize = pxSize;
                    tm.fontStyle = FontStyle.Bold;
                    tm.characterSize = 1f;
                    tm.anchor = TextAnchor.MiddleCenter;
                    tm.alignment = TextAlignment.Center;
                    tm.color = Color.white;
                    tm.richText = false;
                    var mr = go.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = f.material;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    // TextMesh lays out in font units; characterSize 1 with fontSize N means
                    // one unit per pixel, so a plain Y offset centres the block.
                    go.transform.localPosition = new Vector3(0, total * 0.5f - i * lineHeight, 0);
                }

                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                tex.Apply(false);
                var px = tex.GetPixels();
                Object.DestroyImmediate(tex);

                float sum = 0f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        // ReadPixels is bottom-up; the mask is canvas-style top-down
                        float a = px[(size - 1 - y) * size + x].a;
                        mask[y * size + x] = a;
                        sum += a;
                    }
                ok = sum > 1f;

                // TextMesh's world size depends on font metrics and Unity's own character-size
                // convention, which varies by font and version. Rather than trust it, the
                // rendered block is measured and rescaled to the size the artwork asks for —
                // so a missing "Secular One" falling back to Arial still fills the plaque.
                if (ok)
                {
                    float targetH = pxSize * (lines.Count > 1 ? lines.Count * (lineHeight / pxSize) * 0.78f : 0.78f);
                    mask = Normalize(mask, size, size * 0.78f, Mathf.Min(targetH, size * 0.86f));
                }
            }
            catch
            {
                ok = false;
            }
            finally
            {
                RenderTexture.active = prev;
                if (rig != null) Object.DestroyImmediate(rig);
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
            }
            return mask;
        }

        /// <summary>
        /// Rescale and centre a mask so its ink fits a target box. Keeps the aspect ratio, so
        /// a wide two-line label and a single tall glyph both land where the plaque expects.
        /// </summary>
        static float[] Normalize(float[] mask, int size, float targetW, float targetH)
        {
            int minX = size, minY = size, maxX = -1, maxY = -1;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    if (mask[y * size + x] > 0.02f)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
            if (maxX < minX || maxY < minY) return mask;

            float w = maxX - minX + 1, h = maxY - minY + 1;
            float scale = Mathf.Min(targetW / w, targetH / h);
            if (scale <= 0f || float.IsInfinity(scale)) return mask;
            // already about right: leave it rather than resampling for nothing
            if (Mathf.Abs(scale - 1f) < 0.04f && Mathf.Abs((minX + maxX) * 0.5f - size * 0.5f) < 2f
                && Mathf.Abs((minY + maxY) * 0.5f - size * 0.5f) < 2f) return mask;

            float cx = (minX + maxX) * 0.5f, cy = (minY + maxY) * 0.5f;
            var outp = new float[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // sample the source at the position this destination pixel maps back to
                    float sx = (x + 0.5f - half) / scale + cx;
                    float sy = (y + 0.5f - half) / scale + cy;
                    outp[y * size + x] = Bilinear(mask, size, sx - 0.5f, sy - 0.5f);
                }
            return outp;
        }

        static float Bilinear(float[] m, int size, float x, float y)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            return Sample(m, size, x0, y0) * (1 - fx) * (1 - fy)
                 + Sample(m, size, x0 + 1, y0) * fx * (1 - fy)
                 + Sample(m, size, x0, y0 + 1) * (1 - fx) * fy
                 + Sample(m, size, x0 + 1, y0 + 1) * fx * fy;
        }

        static float Sample(float[] m, int size, int x, int y)
        {
            if (x < 0 || y < 0 || x >= size || y >= size) return 0f;
            return m[y * size + x];
        }

        /// <summary>Composite a coverage mask onto a canvas at an offset, in one colour.</summary>
        public static void Draw(Canvas2D cv, float[] mask, int size, float dx, float dy, Color col, float alpha = 1f)
        {
            int ix = Mathf.RoundToInt(dx), iy = Mathf.RoundToInt(dy);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = mask[y * size + x];
                    if (a <= 0.002f) continue;
                    cv.Blend(x + ix, y + iy, new Color(col.r, col.g, col.b, col.a * a * alpha));
                }
        }

        /// <summary>
        /// Dilate a mask by `radius` pixels — the CPU stand-in for canvas strokeText, whose
        /// lineWidth W paints a band of W/2 either side of the outline.
        /// </summary>
        public static float[] Dilate(float[] mask, int size, float radius)
        {
            var outp = new float[size * size];
            int r = Mathf.CeilToInt(radius);
            var offsets = new List<Vector2Int>();
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (dx * dx + dy * dy <= radius * radius) offsets.Add(new Vector2Int(dx, dy));

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float best = 0f;
                    foreach (var o in offsets)
                    {
                        int sx = x + o.x, sy = y + o.y;
                        if (sx < 0 || sy < 0 || sx >= size || sy >= size) continue;
                        float v = mask[sy * size + sx];
                        if (v > best) { best = v; if (best >= 0.999f) break; }
                    }
                    outp[y * size + x] = best;
                }
            return outp;
        }
    }
}
