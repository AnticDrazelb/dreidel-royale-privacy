using UnityEngine;

namespace DreidelRoyale.Visual
{
    /// <summary>
    /// Material helpers that translate the three.js material vocabulary the source is
    /// written in — metalness / roughness / emissive+intensity / opacity — into built-in
    /// pipeline Standard-shader terms, so the port can keep quoting the original numbers.
    /// </summary>
    public static class MatUtil
    {
        static Shader _standard, _unlit, _additive, _unlitTransparent;

        public static Shader Standard
        {
            get { return _standard ?? (_standard = Shader.Find("Standard")); }
        }

        public static Shader Unlit
        {
            get { return _unlit ?? (_unlit = Shader.Find("Unlit/Texture")); }
        }

        public static Shader UnlitTransparent
        {
            get { return _unlitTransparent ?? (_unlitTransparent = Find("Sprites/Default", "Unlit/Transparent")); }
        }

        /// <summary>Additive, unlit, depth-write off — every halo, aura, ring and flame.</summary>
        public static Shader Additive
        {
            get
            {
                return _additive ?? (_additive = Find(
                    "Legacy Shaders/Particles/Additive",
                    "Mobile/Particles/Additive",
                    "Particles/Standard Unlit",
                    "Sprites/Default"));
            }
        }

        static Shader Find(params string[] names)
        {
            foreach (var n in names) { var s = Shader.Find(n); if (s != null) return s; }
            return Shader.Find("Sprites/Default");
        }

        /// <summary>Opaque PBR surface. `rough` and `metal` use the three.js sense.</summary>
        public static Material Pbr(Color color, float metal, float rough, Color? emissive = null,
                                   float emissiveIntensity = 1f, Texture map = null)
        {
            var m = new Material(Standard);
            m.color = color;
            m.SetFloat("_Metallic", Mathf.Clamp01(metal));
            m.SetFloat("_Glossiness", Mathf.Clamp01(1f - rough));
            if (map != null) m.mainTexture = map;
            if (emissive.HasValue && emissiveIntensity > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", emissive.Value * emissiveIntensity);
            }
            return m;
        }

        /// <summary>
        /// Translucent PBR, for the gems. three.js reaches for MeshPhysicalMaterial with
        /// clearcoat; the Standard shader's nearest honest equivalent is a transparent
        /// surface with very high smoothness, which reads the same at this art scale.
        /// </summary>
        public static Material Gem(Color color, Color emissive, float opacity, float rough,
                                   float emissiveIntensity = 0.4f)
        {
            var m = new Material(Standard);
            SetTransparent(m);
            var c = color; c.a = Mathf.Clamp01(opacity);
            m.color = c;
            m.SetFloat("_Metallic", 0f);
            // clearcoat 1.0 with roughness 0.06 means the surface always keeps a hard
            // specular; clamp smoothness up so a matte-ish gem still catches the candles.
            m.SetFloat("_Glossiness", Mathf.Clamp01(Mathf.Max(1f - rough, 0.86f)));
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", emissive * emissiveIntensity);
            return m;
        }

        public static void SetTransparent(Material m)
        {
            m.SetFloat("_Mode", 3f);                       // Transparent
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 1);                        // convex body: self-sorting works,
            m.DisableKeyword("_ALPHATEST_ON");             // and you see the stem THROUGH the cube,
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");       // which is the whole point
            m.DisableKeyword("_ALPHABLEND_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        /// <summary>Cutout-style alpha, for the plaques: transparent corners, solid panel.</summary>
        public static Material Plaque(Texture tex, Color emissive)
        {
            var m = new Material(Standard);
            m.mainTexture = tex;
            m.SetFloat("_Mode", 2f);                       // Fade
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 1);
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry + 50;
            m.SetFloat("_Metallic", 0.05f);
            m.SetFloat("_Glossiness", 0.25f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetTexture("_EmissionMap", tex);
            m.SetColor("_EmissionColor", emissive);
            return m;
        }

        public static Material Glow(Color tint, Texture tex = null)
        {
            var m = new Material(Additive);
            if (tex != null && m.HasProperty("_MainTex")) m.mainTexture = tex;
            Tint(m, tint);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
            return m;
        }

        /// <summary>
        /// Set a material's tint through whichever property it actually exposes. The legacy
        /// particle shaders use _TintColor and have no _Color at all, and assigning
        /// Material.color to one of those logs an error every frame.
        /// </summary>
        public static void Tint(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public static Color GetTint(Material m)
        {
            if (m == null) return Color.clear;
            if (m.HasProperty("_TintColor")) return m.GetColor("_TintColor");
            if (m.HasProperty("_Color")) return m.GetColor("_Color");
            return Color.white;
        }

        public static Material UnlitTex(Texture tex, Color tint)
        {
            var m = new Material(UnlitTransparent);
            if (tex != null && m.HasProperty("_MainTex")) m.mainTexture = tex;
            Tint(m, tint);
            return m;
        }

        public static Material UnlitColor(Color c)
        {
            var m = new Material(Find("Unlit/Color", "Sprites/Default"));
            Tint(m, c);
            return m;
        }

        public static void SetAlpha(Material m, float a)
        {
            if (m == null) return;
            var c = GetTint(m);
            c.a = a;
            Tint(m, c);
        }
    }
}
