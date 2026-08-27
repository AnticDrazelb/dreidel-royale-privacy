using UnityEngine;
using Stop = DreidelRoyale.Visual.Canvas2D.Stop;

namespace DreidelRoyale.Visual
{
    /// <summary>
    /// The dreidel leaves evidence. A canvas the tip draws into as it walks: it darkens
    /// where the top has spun, and slowly heals — the mark lingers about twenty seconds,
    /// then the table forgives.
    /// </summary>
    public class ScuffMap
    {
        public const float SCUFF_AREA = 6f;    // world units covered by the canvas (+/-3)
        const int SIZE = 256;

        readonly Color[] _px = new Color[SIZE * SIZE];
        Texture2D _tex;
        Transform _mesh;   // kept so the decal can be hidden per table
        bool _dirty, _texDirty;
        float _heal;

        public void Build(Transform world)
        {
            _tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false) { name = "scuff" };
            for (int i = 0; i < _px.Length; i++) _px[i] = Color.clear;
            _tex.SetPixels(_px); _tex.Apply(false);

            var go = new GameObject("scuff");
            go.transform.SetParent(world, false);
            go.AddComponent<MeshFilter>().sharedMesh = Geo.Quad(SCUFF_AREA, SCUFF_AREA);
            var mr = go.AddComponent<MeshRenderer>();
            var mat = MatUtil.UnlitTex(_tex, new Color(1, 1, 1, 0.55f));
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.transform.localRotation = Quaternion.Euler(-90f, 0, 0);
            go.transform.localPosition = new Vector3(0, 0.012f, 0);   // under the aura, over the surface
            _mesh = go.transform;
        }

        /// <summary>Flat storybook tables paint no shading, so the scuff would read as dirt.</summary>
        public void Show(bool on) { if (_mesh != null) _mesh.gameObject.SetActive(on); }

        /// <summary>Darken a soft dot where the tip is touching down.</summary>
        public void Stamp(float x, float z, float r)
        {
            if (_tex == null) return;
            if (Mathf.Abs(x) > SCUFF_AREA / 2f || Mathf.Abs(z) > SCUFF_AREA / 2f) return;
            float px = (x / SCUFF_AREA + 0.5f) * SIZE;
            float pz = (z / SCUFF_AREA + 0.5f) * SIZE;
            float pr = Mathf.Max(1.6f, r / SCUFF_AREA * SIZE);
            var ink = new Color(38 / 255f, 22 / 255f, 8 / 255f, 0.14f);

            int x0 = Mathf.Max(0, Mathf.FloorToInt(px - pr)), x1 = Mathf.Min(SIZE, Mathf.CeilToInt(px + pr));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(pz - pr)), y1 = Mathf.Min(SIZE, Mathf.CeilToInt(pz + pr));
            for (int y = y0; y < y1; y++)
                for (int xx = x0; xx < x1; xx++)
                {
                    float d = Mathf.Sqrt((xx + 0.5f - px) * (xx + 0.5f - px) + (y + 0.5f - pz) * (y + 0.5f - pz));
                    if (d >= pr) continue;
                    float a = ink.a * (1f - d / pr);
                    // canvas Y-down -> Unity Y-up
                    int i = (SIZE - 1 - y) * SIZE + xx;
                    var dst = _px[i];
                    float outA = a + dst.a * (1f - a);
                    if (outA <= 0f) continue;
                    _px[i] = new Color(
                        (ink.r * a + dst.r * dst.a * (1f - a)) / outA,
                        (ink.g * a + dst.g * dst.a * (1f - a)) / outA,
                        (ink.b * a + dst.b * dst.a * (1f - a)) / outA,
                        outA);
                }
            _dirty = true; _texDirty = true;
        }

        /// <summary>Fade the whole mark slowly, in quarter-second steps to keep the cost down.</summary>
        public void Heal(float dt)
        {
            if (_tex == null || !_dirty) return;
            _heal += dt;
            if (_heal >= 0.25f)
            {
                _heal = 0f;
                bool any = false;
                for (int i = 0; i < _px.Length; i++)
                {
                    if (_px[i].a <= 0f) continue;
                    var c = _px[i];
                    c.a *= 0.95f;                         // destination-out at 0.05 alpha
                    if (c.a < 0.002f) c.a = 0f; else any = true;
                    _px[i] = c;
                }
                _dirty = any;
                _texDirty = true;
            }
            if (_texDirty) { _tex.SetPixels(_px); _tex.Apply(false); _texDirty = false; }
        }
    }
}
