using System.Collections.Generic;
using UnityEngine;
using DreidelRoyale.Core;
using Stop = DreidelRoyale.Visual.Canvas2D.Stop;

namespace DreidelRoyale.Visual
{
    /// <summary>
    /// Shared in-world flourishes: the soft additive sprites that stand in for bloom, and
    /// the camera-facing billboards the flames use.
    /// </summary>
    public static class Fx
    {
        static readonly Dictionary<string, Texture2D> RadialCache = new Dictionary<string, Texture2D>();

        public static Texture2D RadialTex(string innerRgba)
        {
            Texture2D t;
            if (RadialCache.TryGetValue(innerRgba, out t) && t != null) return t;
            var c = Hex.To(innerRgba);
            t = Tex.Radial(new Stop(0f, c), new Stop(1f, new Color(c.r, c.g, c.b, 0f)));
            RadialCache[innerRgba] = t;
            return t;
        }

        /// <summary>A flat additive quad that always faces the camera — a fake bloom point.</summary>
        public static Transform GlowSprite(Transform parent, string colorRgba, float size, float opacity)
        {
            var go = new GameObject("glow");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Geo.Quad(size, size);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MatUtil.Glow(new Color(1, 1, 1, opacity), RadialTex(colorRgba));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.AddComponent<Billboard>();
            SetGlow(go.transform, opacity);
            return go.transform;
        }

        public static void SetGlow(Transform sprite, float opacity)
        {
            if (sprite == null) return;
            var mr = sprite.GetComponent<MeshRenderer>();
            if (mr == null) return;
            MatUtil.SetAlpha(mr.material, opacity);
        }

        public static float GetGlow(Transform sprite)
        {
            if (sprite == null) return 0f;
            var mr = sprite.GetComponent<MeshRenderer>();
            return mr == null ? 0f : MatUtil.GetTint(mr.material).a;
        }
    }

    /// <summary>Turns to face the active camera each frame, like a three.js sprite.</summary>
    public class Billboard : MonoBehaviour
    {
        public Camera Cam;
        /// <summary>Extra local turn applied after the billboard, for the leaning flames.</summary>
        public Quaternion Extra = Quaternion.identity;

        void LateUpdate()
        {
            var c = Cam ?? Camera.main;
            if (c == null) return;
            transform.rotation = Extra * c.transform.rotation;
        }
    }
}
