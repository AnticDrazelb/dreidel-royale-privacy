using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DreidelRoyale.AR;
using DreidelRoyale.Audio;
using DreidelRoyale.Core;
using DreidelRoyale.Net;
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
        ArController _ar;
        NetManager _net;
        NetUI _netUi;
        UI.ChatPanel _chat;

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            LoadPrefs();
            // The tier owns shadows, resolution and the ambient layer, so it is applied
            // before anything reads them rather than after.
            GfxSettings.Load();
            BuildCamera();
            BuildAudio();
            BuildWorld();
            BuildUI();
            BuildAr();
            Wire();

            ValidateSavedChoices();
            _gc.ApplyEnv(_gc.HostEnvChoice);
            _view.SetSkin(_gc.MySkinChoice);

            // Each plaque is rasterised on first use, and the gold crown swaps in the moment
            // someone takes the lead. Building those four textures mid-spin is a visible hitch,
            // so the two skins that are certain to appear are baked now, during the menu.
            Prewarm(_gc.MySkinChoice);
            Prewarm("gold");

            // The table was applied before the dreidel was chosen, so the pairing that decides
            // the chrome is only knowable once both are settled.
            _ui.RefreshChrome();

            // The native store's landing pad has to be a component on a named object, so it
            // rides on Bootstrap - the one object the scene is guaranteed to have.
            gameObject.AddComponent<IapCallbacks>();
            Iap.OnEntitled += OnEntitled;

            // An invite tapped in a message opens the app straight into the join flow.
            Application.deepLinkActivated += OnDeepLink;
            if (!string.IsNullOrEmpty(Application.absoluteURL)) OnDeepLink(Application.absoluteURL);

            StartCoroutine(WatchConnectivity());

            // first run: show the rules before the first spin, exactly once
            if (Store.Get("drdl-seen") != "1") _ui.ShowHowTo();
        }

        void OnDestroy()
        {
            Iap.OnEntitled -= OnEntitled;
            Application.deepLinkActivated -= OnDeepLink;
        }

        /// <summary>
        /// An entitlement landing is the same moment whether it came from a purchase or a
        /// debug grant, so the celebration lives here rather than at each call site.
        /// </summary>
        void OnEntitled(bool celebrate)
        {
            _ui.RefreshPickers();
            if (!celebrate) { _ui.Toast("Purchases restored"); return; }
            _ui.Toast("Full Collection unlocked - thank you!");
            Sfx.Play("coin");
            Sfx.Buzz(40, 60, 40);
            try { _ui.Fx.Confetti(Screen.width / 2f, Screen.height * 0.5f, 90, 16); } catch { }
        }

        void OnDeepLink(string url) { _ui.HandleDeepLink(url); }

        /// <summary>
        /// Losing the network mid-game is worth saying out loud: the alternative is a table
        /// that just stops, with nothing on screen explaining why.
        /// </summary>
        IEnumerator WatchConnectivity()
        {
            var wait = new WaitForSeconds(2f);
            bool wasOnline = Application.internetReachability != NetworkReachability.NotReachable;
            while (true)
            {
                yield return wait;
                bool online = Application.internetReachability != NetworkReachability.NotReachable;
                if (wasOnline && !online && _gc.Net != null && _gc.Net.Active)
                    _ui.Toast("Network lost - you may drop from the game", true);
                wasOnline = online;
            }
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

        /// <summary>
        /// A stored choice can go stale: a save restored onto a fresh install names a dreidel
        /// that has not been earned here, and a table that has not been unlocked. Falling back
        /// is quieter than showing a locked piece on the table.
        /// </summary>
        void ValidateSavedChoices()
        {
            var S = Stats.Load();
            var skin = Unlocks.Skins.Find(d => d.Id == _gc.MySkinChoice);
            if (skin == null || !Unlocks.SkinUnlocked(skin, S)) _gc.MySkinChoice = "wood";
            if (!EnvDefs.All.ContainsKey(_gc.HostEnvChoice) || !Unlocks.EnvUnlocked(_gc.HostEnvChoice, S))
                _gc.HostEnvChoice = "midnight";
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

            // FindFirstObjectByType, not FindObjectOfType: the old one is deprecated in Unity 6,
            // and its replacement is explicit about not promising a sort order it never had.
            if (FindFirstObjectByType<EventSystem>() == null)
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

            _net = mgr.AddComponent<NetManager>();
            _netUi = mgr.AddComponent<NetUI>();
            _net.GC = _gc; _net.UI = _ui; _net.Screens = _netUi;
            _netUi.UI = _ui; _netUi.GC = _gc; _netUi.Net = _net;
            _gc.Net = _net;
            _ui.NetScreens = _netUi;

            _chat = mgr.AddComponent<UI.ChatPanel>();
            _chat.UI = _ui; _chat.Net = _net;
            _ui.Chat = _chat;

            _ui.Build(canvas);
            _netUi.Build(_ui.Root);
            _chat.Build(_ui.Root);
        }

        void BuildAr()
        {
            var go = new GameObject("AR");
            _ar = go.AddComponent<ArController>();
            _ar.View = _view;
            _ar.Cam = _cam;
            go.AddComponent<ArGestures>().Ar = _ar;

            _ui.Ar = _ar;
            _ar.OnChange = _ui.OnArChanged;

            // The view asks the AR layer what it should be doing, rather than the AR layer
            // reaching in to change it: one direction of dependency, and the flat-screen game
            // behaves identically whether or not AR was ever compiled in.
            _view.ArIsOn = () => _ar.IsOn;
            _view.ArIsPlaced = () => _ar.IsPlaced;
            _view.ArTableMode = () => _ar.TableMode;
            _view.Gelt.EdgeRadius = () =>
                _ar.IsOn && _ar.TableMode == "board" ? ArProps.TableRadius : 19f;

            _gc.OnEnvApplied = env => _ar.NoteEnv(env);

            // The capability check talks to ARCore/ARKit and can take a moment, so it runs
            // behind the menu rather than holding up the first frame.
            StartCoroutine(CheckArThenRefresh());
        }

        System.Collections.IEnumerator CheckArThenRefresh()
        {
            yield return _ar.CheckAvailability();
            _ui.OnArChanged(false, false);
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

            // In AR the screen-space transfer slides coins across the phone glass while
            // everything else sits on the table - the one illusion-breaker left. Placed, the
            // pot's legs go through the world instead: real coins leap off the stacks and arc
            // toward the phone, and the HUD only takes delivery.
            _ui.Fx.WorldFlyOut = count =>
                (_ar != null && _ar.IsOn && _ar.IsPlaced) ? _view.Gelt.FlyOut(count) : 0;
            _view.Gelt.OnFlightGone = worldPos => _ui.Fx.DeliverFlight(_cam, worldPos);
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
