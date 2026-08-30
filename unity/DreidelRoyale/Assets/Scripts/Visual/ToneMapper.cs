using UnityEngine;

namespace DreidelRoyale.Visual
{
    /// <summary>
    /// ACES filmic tone mapping, because the original has it and the built-in pipeline does not.
    ///
    /// three.js sets `renderer.toneMapping = ACESFilmicToneMapping` with exposure 0.95, and
    /// every colour in the game was authored against that curve. Unity's built-in render
    /// pipeline ships no tone mapper, so the port was clipping at 1.0: the gold, the candle
    /// flames and the emissive gems all blew out to flat white where the web build rolls them
    /// off. It is one full-screen pass, and it is the difference between the two builds
    /// looking like the same game and merely looking like the same shapes.
    ///
    /// HDR has to be on for it to have anything to work with — tone mapping an already-clamped
    /// image only darkens it — so this turns it on and restores whatever it found on the way out.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ToneMapper : MonoBehaviour
    {
        /// <summary>Matches three.js's toneMappingExposure in the original.</summary>
        public float Exposure = 0.95f;

        Material _mat;
        Camera _cam;
        bool _savedHdr;

        void OnEnable()
        {
            _cam = GetComponent<Camera>();
            _savedHdr = _cam.allowHDR;
            _cam.allowHDR = true;

            var shader = Shader.Find("DreidelRoyale/AcesToneMap");
            if (shader == null)
            {
                // Better to render untone-mapped than to render black. This is also what the
                // Always Included Shaders list exists to prevent in a device build.
                Debug.LogWarning("[Dreidel Royale] ACES tone-map shader missing; "
                                 + "highlights will clip.");
                enabled = false;
                return;
            }
            _mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        void OnDisable()
        {
            if (_cam != null) _cam.allowHDR = _savedHdr;
            if (_mat != null) { Destroy(_mat); _mat = null; }
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (_mat == null) { Graphics.Blit(src, dst); return; }
            _mat.SetFloat("_Exposure", Exposure);
            Graphics.Blit(src, dst, _mat);
        }
    }
}
