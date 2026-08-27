using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DreidelRoyale.Audio;
using DreidelRoyale.Core;
using DreidelRoyale.UI;
using DreidelRoyale.Visual;

namespace DreidelRoyale
{
    /// <summary>
    /// Builds the whole game at runtime — camera, table, dreidel, audio graph and UI.
    ///
    /// The web build generates every mesh, texture and sound in code rather than shipping
    /// assets, and this port keeps that: the scene file holds one object with this component
    /// on it, and everything else is constructed here. Nothing to re-import, nothing to drift
    /// out of sync with the code that describes it.
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        public static Bootstrap I;

        Camera _cam;
        DreidelView _view;
        GameController _gc;
        UIManager _ui;
        Hud _hud;
        MusicEngine _music;

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.Medium;

            LoadPrefs();
            BuildCamera();
            BuildAudio();
            BuildWorld();
            BuildUI();
            Wire();

            _gc.ApplyEnv(_gc.HostEnvChoice);
            _view.SetSkin(_gc.MySkinChoice);

            // Each plaque is rasterised on first use, and the gold crown swaps in the moment
            // someone takes the lead. Building those four textures mid-spin is a visible hitch,
            // so the two skins that are certain to appear are baked now, during the menu.
            Prewarm(_gc.MySkinChoice);
            Prewarm("gold");

            // first run: show the rules before the first spin, exactly once
            if (Store.Get("drdl-seen") != "1") _ui.ShowHowTo();
        }

        static void Prewarm(string skin)
        {
            try
            {
                Visual.Tex.Face(Consts.Sides[0].Char, skin);
                Visual.Tex.Face(Consts.Sides[1].Char, skin);
                Visual.Tex.Face(Consts.Sides[2].Char, skin);
                Visual.Tex.Face(Consts.Fourth().Char, skin);
            }
            catch { /* a plaque that fails to bake just draws blank; never fatal */ }
        }

        static void LoadPrefs()
        {
            Sfx.MusicOn = Store.Get("drdl-music") != "0";
            Sfx.SfxOn = Store.Get("drdl-sfx") != "0";
            Sfx.HapticsOn = Store.Get("drdl-haptics") != "0";
            Consts.IsraelMode = Store.Get("drdl-israel") == "1";
            Consts.RefreshSides();
        }

        void BuildCamera()
        {
            var go = new GameObject("MainCamera");
            go.tag = "MainCamera";
            _cam = go.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Theme.Night;
            _cam.fieldOfView = 38f;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 60f;
            _cam.allowHDR = false;
            go.AddComponent<AudioListener>();
        }

        void BuildAudio()
        {
            _cam.gameObject.AddComponent<Synth>();
            _music = _cam.gameObject.AddComponent<MusicEngine>();
            if (Synth.I != null)
            {
                Synth.I.SfxGain = Sfx.SfxOn ? 1f : 0f;
                Synth.I.MusicGain = Sfx.MusicOn ? Synth.MusicVol : 0f;
            }
        }

        void BuildWorld()
        {
            var go = new GameObject("World");
            _view = go.AddComponent<DreidelView>();
            _view.Init(_cam);
        }

        void BuildUI()
        {
            var canvasGo = new GameObject("UI");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(420, 860);   // a phone in portrait
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.65f;                    // favour width, so nothing crops
            canvasGo.AddComponent<GraphicRaycaster>();

            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            var mgr = new GameObject("UIManager");
            _ui = mgr.AddComponent<UIManager>();
            _hud = mgr.AddComponent<Hud>();

            var gcGo = new GameObject("GameController");
            _gc = gcGo.AddComponent<GameController>();

            _ui.GC = _gc; _ui.View = _view; _ui.Hud = _hud;
            _hud.GC = _gc; _hud.View = _view; _hud.UI = _ui;
            _gc.View = _view; _gc.Hud = _hud; _gc.UI = _ui; _gc.Music = _music;

            _ui.Build(canvas);
        }

        void Wire()
        {
            // The 3D layer drives the sounds that have to land on the exact frame the geometry
            // says they do — the scrape's RPM, each wooden knock, and the slam that fires the
            // spin's consequences.
            _view.OnSpinAudio = (speed01, power) => Sfx.SetScrape(speed01, power);
            _view.OnChargePulse = p => Sfx.Play("chargePulse", p);
            _view.OnKnock = v =>
            {
                Sfx.Play("knock", v);
                if (v > 0.4f) Sfx.Buzz(Mathf.RoundToInt(8 + v * 26));
            };

            // The impact moment: the body actually meets the surface, and the 3D layer knows
            // when - with the fake-out it varies. Everything that sells the slam fires here,
            // in sync with the loud knock.
            _view.OnImpact = power =>
            {
                Sfx.StopScrape();
                _view.AddWax();                  // every spin melts the candles a notch further
                Sfx.Play("land", power);
                Sfx.Buzz(Mathf.RoundToInt(30 + power * 60), 40, 20);
                _ui.ImpactShake(3f + power * 9f);
                _gc.FireImpact(power);
            };

            _view.Gelt.OnClink = v => Clink("clinkWood", v, true);
            _view.Gelt.OnCoinLand = (surface, v) =>
            {
                Clink(surface == "floor" ? "clinkFloor" : "clinkWood", v, false);
                if (surface == "floor") Sfx.Buzz(8);
            };
            // The Euler's-disk rattle: a run of ticks that accelerates as the coin flattens -
            // the signature wrrrrRRRR. Tick times follow the same decay the visual uses.
            _view.Gelt.OnEuler = dur => StartCoroutine(EulerRattle(dur));
        }

        // Coins land in cascades; without a floor on the interval a scatter reads as static.
        // Coin-on-coin and coin-on-surface get their own throttles, as in the source.
        float _lastClink, _lastLand;

        void Clink(string fx, float v, bool coinOnCoin)
        {
            float gap = coinOnCoin ? 0.07f : 0.09f;
            float last = coinOnCoin ? _lastClink : _lastLand;
            if (Time.time - last < gap) return;
            if (coinOnCoin) _lastClink = Time.time; else _lastLand = Time.time;
            Sfx.Play(fx, v);
        }

        System.Collections.IEnumerator EulerRattle(float dur)
        {
            const int n = 16;
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)(n - 1);
                float t = dur * (1f - Mathf.Pow(1f - u, 2.2f));   // ticks bunch up toward the end
                float wait = t - prev;
                prev = t;
                if (wait > 0f) yield return new WaitForSeconds(wait);
                Sfx.Play("clinkWood", 0.25f + u * 0.5f);
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Sfx.StopRumble();
        }
    }
}
