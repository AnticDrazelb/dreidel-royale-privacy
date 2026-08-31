using UnityEngine;
using DreidelRoyale.Core;

namespace DreidelRoyale.Visual
{
    /// <summary>
    /// The screen treatment, and the place the game's punches land.
    ///
    /// It replaces the plain tone mapper because tone mapping was never going to be the only
    /// full-screen work: once there is one blit there should only ever be one, and everything
    /// else folds into it. Bloom is the exception — a blur cannot be one tap — so it runs at
    /// quarter resolution and costs a sixteenth of the bandwidth.
    ///
    /// The transient effects are deliberately not ambient. A radial blur that is always on is
    /// a smeared game; one that arrives on release and decays over the spin is speed. The same
    /// for aberration, which only exists in the frames around an impact. Nothing here runs
    /// while the table is idle except the tone curve and a whisper of vignette.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PostFx : MonoBehaviour
    {
        public static PostFx I;

        /// <summary>Matches three.js's toneMappingExposure in the original.</summary>
        public float Exposure = 0.95f;

        /// <summary>How much of the frame is dimmed at the corners, always on and always small.</summary>
        public float Vignette = 0.28f;

        const float BloomThreshold = 0.85f;
        const float BloomIntensity = 1.15f;

        Material _mat;
        Camera _cam;
        bool _savedHdr;

        // live, decaying state
        float _blur, _blurDecay, _ab, _abDecay, _flash, _flashDecay;
        Color _flashColor = Color.white;

        void OnEnable()
        {
            I = this;
            _cam = GetComponent<Camera>();
            _savedHdr = _cam.allowHDR;
            _cam.allowHDR = true;

            var shader = Shader.Find("DreidelRoyale/PostFx");
            if (shader == null)
            {
                // Rendering ungraded is bad; rendering black is worse. This is also exactly
                // what the Always Included Shaders list exists to stop happening on device.
                Debug.LogWarning("[Dreidel Royale] PostFx shader missing - no tone mapping.");
                enabled = false;
                return;
            }
            _mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        void OnDisable()
        {
            if (_cam != null) _cam.allowHDR = _savedHdr;
            if (_mat != null) { Destroy(_mat); _mat = null; }
            if (I == this) I = null;
        }

        // ---------------------------------------------------------------
        //  what the game asks for
        // ---------------------------------------------------------------
        /// <summary>Speed. Rises on release and bleeds off across the spin.</summary>
        public void Speed(float amount, float seconds)
        {
            _blur = Mathf.Max(_blur, amount);
            _blurDecay = amount / Mathf.Max(0.05f, seconds);
        }

        /// <summary>A hit. A frame or two of fringing, gone before it registers as an effect.</summary>
        public void Impact(float amount)
        {
            _ab = Mathf.Max(_ab, amount);
            _abDecay = amount / 0.22f;
        }

        /// <summary>The whole frame lifts - a gimel, a win. Colour is the table's, not white.</summary>
        public void Flash(Color c, float amount, float seconds)
        {
            _flashColor = c;
            _flash = Mathf.Max(_flash, amount);
            _flashDecay = amount / Mathf.Max(0.05f, seconds);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;      // must outlive a hit-stop
            _blur = Mathf.Max(0f, _blur - _blurDecay * dt);
            _ab = Mathf.Max(0f, _ab - _abDecay * dt);
            _flash = Mathf.Max(0f, _flash - _flashDecay * dt);
        }

        // ---------------------------------------------------------------
        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (_mat == null) { Graphics.Blit(src, dst); return; }

            // Potato skips bloom entirely: three extra targets is the wrong trade on a phone
            // that is already dropping resolution to keep up.
            bool bloom = GfxSettings.Tier != GfxSettings.Low;

            RenderTexture a = null, b = null;
            if (bloom)
            {
                int w = Mathf.Max(1, src.width / 4), h = Mathf.Max(1, src.height / 4);
                a = RenderTexture.GetTemporary(w, h, 0, src.format);
                b = RenderTexture.GetTemporary(w, h, 0, src.format);

                _mat.SetFloat("_Threshold", BloomThreshold);
                _mat.SetFloat("_BloomIntensity", BloomIntensity);
                Graphics.Blit(src, a, _mat, 0);

                _mat.SetVector("_BlurDir", new Vector4(1f, 0f, 0f, 0f));
                Graphics.Blit(a, b, _mat, 1);
                _mat.SetVector("_BlurDir", new Vector4(0f, 1f, 0f, 0f));
                Graphics.Blit(b, a, _mat, 1);

                _mat.SetTexture("_BloomTex", a);
            }
            else _mat.SetTexture("_BloomTex", Texture2D.blackTexture);

            _mat.SetFloat("_Exposure", Exposure);
            _mat.SetFloat("_Vignette", Vignette);
            _mat.SetFloat("_RadialBlur", _blur);
            _mat.SetFloat("_Aberration", _ab);
            _mat.SetFloat("_Flash", _flash);
            _mat.SetColor("_FlashColor", _flashColor);
            Graphics.Blit(src, dst, _mat, 2);

            if (a != null) RenderTexture.ReleaseTemporary(a);
            if (b != null) RenderTexture.ReleaseTemporary(b);
        }
    }
}
