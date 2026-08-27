using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DreidelRoyale.Audio;
using DreidelRoyale.Core;
using DreidelRoyale.Visual;

namespace DreidelRoyale.UI
{
    /// <summary>
    /// Screens, overlays and the menu flow. One canvas, one screen visible at a time, with
    /// the stylesheet's transitions: screens fade and settle, toasts queue, the chant flashes
    /// over the table.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager I;

        public Canvas Canvas;
        public RectTransform Root;
        public GameController GC;
        public DreidelView View;
        public Hud Hud;
        public FxLayer Fx;
        public EmberLayer Embers;
        public EnvFrameLayer Frame;
        public FlatStage Flat;

        readonly Dictionary<string, RectTransform> _screens = new Dictionary<string, RectTransform>();

        /// <summary>Which screen is showing, or "game" during play.</summary>
        public string Current { get; private set; }

        Image _backdrop, _dim;
        Text _chant, _countdown, _showdown;
        RectTransform _countdownGo, _showdownGo, _toastZone;
        RectTransform _pause, _howto, _winner;

        // live widgets the screens re-render into
        Transform _cpuCountPicker, _cpuDiffPicker, _rulesPickerCpu, _antePickerCpu;
        Transform _envPickerCpu, _skinPickerCpu;
        Transform _rulesPickerLocal, _antePickerLocal, _envPickerLocal, _skinPickerLocal, _localList;
        Transform _envPickerCustom, _skinPickerCustom, _skinPickerChange, _envPickerChange;
        /// <summary>Where the store listings point for the policy both platforms require.</summary>
        public const string PrivacyUrl = "https://anticdrazelb.github.io/dreidel-royale-privacy/";

        Transform _recordsStats, _winStats, _gfxPicker;
        Text _winnerName, _winnerLine, _winLifetime, _collectionLine, _buyLabel;
        Button _buyBtn;
        InputField _cpuName, _localName;
        readonly InputField[] _customFaces = new InputField[4];
        Button _resumeBtn;
        Text _resumeLabel;
        Pager _cpuPager, _localPager;

        readonly Queue<KeyValuePair<string, bool>> _toastQ = new Queue<KeyValuePair<string, bool>>();
        bool _toastShowing;

        /// <summary>The networking screens, when multiplayer is present.</summary>
        public DreidelRoyale.Net.NetUI NetScreens;

        /// <summary>Table talk. Only reachable at a networked table.</summary>
        public ChatPanel Chat;

        // ---- AR ----
        public DreidelRoyale.AR.ArController Ar;
        Text _arLabel, _arSub, _arWhy, _arBoardLabel, _arHint;
        RectTransform _arRow, _arHintBox;
        Button _arLaunch;

        void Awake() { I = this; }

        // ---------------------------------------------------------------
        public void Build(Canvas canvas)
        {
            Canvas = canvas;
            Root = canvas.GetComponent<RectTransform>();

            BuildBackdrop();

            // The flat stage stands in for the diorama, so it sits behind everything else.
            Flat = MakeGraphic<FlatStage>("flat-stage");
            Flat.View = View; Flat.GC = GC;
            Flat.Build();

            Embers = MakeGraphic<EmberLayer>("embers");
            Frame = MakeGraphic<EnvFrameLayer>("env-frame");
            Fx = MakeGraphic<FxLayer>("fx");

            BuildScreen("landing", BuildLanding);
            BuildScreen("cpu", BuildCpuSetup);
            BuildScreen("local", BuildLocalSetup);
            BuildScreen("custom", BuildCustom);
            BuildScreen("records", BuildRecords);
            BuildScreen("change", BuildChangeDreidel);

            Hud.Build(Root);

            BuildOverlays();

            // Confetti and flying gelt read as being in front of the table AND the HUD, so the
            // effects layer is lifted above everything once the rest of the tree exists.
            Fx.transform.SetAsLastSibling();

            Show("landing");
        }

        T MakeGraphic<T>(string name) where T : MaskableGraphic
        {
            var go = UIKit.Node(name, Root);
            UIKit.Stretch(go);
            var g = go.AddComponent<T>();
            g.raycastTarget = false;
            return g;
        }

        void BuildBackdrop()
        {
            var go = UIKit.Node("backdrop", Root);
            UIKit.Stretch(go);
            _backdrop = go.AddComponent<Image>();
            _backdrop.color = Theme.Night;
            _backdrop.raycastTarget = false;
            go.transform.SetAsFirstSibling();
            // The 3D camera paints the table; the backdrop only tints the menus, so it stays
            // off during play.
            _backdrop.enabled = false;
        }

        /// <summary>
        /// Register a screen built by someone else - the networking screens live with the
        /// networking code, but they are the same kind of screen and share its transitions.
        /// </summary>
        public RectTransform MakeScreen(string id, Action<Transform> build)
        {
            BuildScreen(id, build);
            return _screens[id];
        }

        void BuildScreen(string id, Action<Transform> build)
        {
            var go = UIKit.Node("screen-" + id, Root);
            var rt = UIKit.Stretch(go);

            // a scrim so menu text stays legible over the live table behind it
            var scrim = UIKit.Node("scrim", go.transform);
            UIKit.Stretch(scrim);
            var simg = scrim.AddComponent<Image>();
            simg.color = new Color(Theme.Night.r, Theme.Night.g, Theme.Night.b, 0.82f);

            var content = UIKit.Node("content", go.transform);
            var srt = UIKit.Rect(content);
            srt.anchorMin = new Vector2(0.5f, 0.5f); srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(380, 100);
            var v = content.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8f; v.childAlignment = TextAnchor.MiddleCenter;
            v.childForceExpandWidth = false; v.childForceExpandHeight = false;
            v.childControlWidth = false; v.childControlHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            go.AddComponent<CanvasGroup>();
            build(content.transform);
            _screens[id] = rt;
            go.SetActive(false);
        }

        // ---------------------------------------------------------------
        //  screens
        // ---------------------------------------------------------------
        void BuildLanding(Transform c)
        {
            var h = UIKit.Label(c, "Dreidel\nRoyale", 58, Hex.To("#f4f6ff"), TextAnchor.MiddleCenter, true);
            UIKit.SetSize(h, 360, 140);
            var tag = UIKit.Label(c, "Last player holding <color=#f2c14e><b>gelt</b></color> wins", 16, Theme.Sub);
            UIKit.SetSize(tag, 340, 26);
            UIKit.Spacer(c, 10f);

            _resumeBtn = UIKit.Btn(c, "Resume Game", UIKit.BtnKind.Primary, () =>
            {
                Sfx.Play("tick");
                if (!GC.ResumeCpuGame()) Toast("That game could not be restored", true);
            });
            _resumeLabel = _resumeBtn.GetComponentInChildren<Text>();
            _resumeBtn.gameObject.SetActive(false);

            UIKit.Btn(c, "Single Player", UIKit.BtnKind.Primary, () => { Sfx.Play("tick"); Show("cpu"); });
            UIKit.Btn(c, "Quick Match", UIKit.BtnKind.Ghost,
                      () => { Sfx.Play("tick"); if (NetScreens != null) NetScreens.BeginQuickMatch(); });
            UIKit.Btn(c, "Decision Dreidel", UIKit.BtnKind.Ghost, () => { Sfx.Play("tick"); OpenCustom(); });

            UIKit.Spacer(c, 12f);
            UIKit.SectionLabel(c, "Play with friends");
            var netRow = UIKit.Row(c, 8f, 46f);
            UIKit.Btn(netRow.transform, "Host", UIKit.BtnKind.Ghost,
                      () => { Sfx.Play("tick"); if (NetScreens != null) NetScreens.BeginHost(); }, 96f, 46f, 15);
            UIKit.Btn(netRow.transform, "Join", UIKit.BtnKind.Ghost,
                      () => { Sfx.Play("tick"); if (NetScreens != null) NetScreens.BeginJoin(); }, 96f, 46f, 15);
            UIKit.Btn(netRow.transform, "Pass & Play", UIKit.BtnKind.Ghost,
                      () => { Sfx.Play("tick"); Show("local"); }, 130f, 46f, 15);

            UIKit.Spacer(c, 10f);
            var row = UIKit.Row(c, 10f, 40f);
            UIKit.Btn(row.transform, "Records", UIKit.BtnKind.Text, () => { Sfx.Play("tick"); Show("records"); }, 120f, 40f, 15);
            UIKit.Btn(row.transform, "How to Play", UIKit.BtnKind.Text, ShowHowTo, 140f, 40f, 15);

            UIKit.Spacer(c, 6f);
            var verRow = UIKit.Row(c, 6f, 18f);
            var ver = UIKit.Label(verRow.transform, "GELT EDITION - V1.0 -", 10,
                                  new Color(Theme.Sub.r, Theme.Sub.g, Theme.Sub.b, 0.7f));
            UIKit.SetSize(ver, 150, 16);
            // A reachable privacy policy is a store requirement on both platforms, so the
            // link is a real control rather than the flat text a screenshot would give.
            UIKit.Btn(verRow.transform, "Privacy", UIKit.BtnKind.Text,
                      () => Application.OpenURL(PrivacyUrl), 56f, 18f, 10);
        }

        void BuildCpuSetup(Transform c)
        {
            var h = UIKit.Label(c, "Single Player", 34, Hex.To("#f4f6ff"), TextAnchor.MiddleCenter, true);
            UIKit.SetSize(h, 360, 46);
            var tag = UIKit.Label(c, "Play against the house - everyone starts with 10 gelt", 14, Theme.Sub);
            UIKit.SetSize(tag, 340, 34);

            var pager = c.gameObject.AddComponent<Pager>();
            pager.Init(c);
            _cpuPager = pager;

            // page 1: the game
            var p1 = pager.AddPage(c).transform;
            _cpuName = UIKit.Input(p1, "YOUR NAME", 10);
            _cpuName.text = "You";
            UIKit.SectionLabel(p1, "Opponents");
            _cpuCountPicker = UIKit.Row(p1, 8f, 46f).transform;
            UIKit.SectionLabel(p1, "Difficulty");
            _cpuDiffPicker = UIKit.Row(p1, 8f, 46f).transform;
            UIKit.SectionLabel(p1, "Game style");
            _rulesPickerCpu = UIKit.Row(p1, 8f, 46f).transform;
            UIKit.SectionLabel(p1, "Starting ante");
            _antePickerCpu = UIKit.Row(p1, 8f, 46f).transform;
            UIKit.Spacer(p1, 6f);
            UIKit.Btn(p1, "Next", UIKit.BtnKind.Primary, () => { Sfx.Play("tick"); pager.Next(); });
            UIKit.Btn(p1, "Back", UIKit.BtnKind.Ghost, () => Show("landing"));

            // page 2: the look, and the start
            var p2 = pager.AddPage(c).transform;
            UIKit.SectionLabel(p2, "Choose your table");
            _envPickerCpu = UIKit.Grid(p2, new Vector2(96, 92)).transform;
            UIKit.SectionLabel(p2, "Your dreidel - earned through play");
            _skinPickerCpu = UIKit.Grid(p2, new Vector2(96, 92)).transform;
            UIKit.Spacer(p2, 6f);
            UIKit.Btn(p2, "Start", UIKit.BtnKind.Primary, () =>
            {
                Sfx.Play("tick");
                if (CheckTestUnlock(_cpuName.text)) { _cpuName.text = "You"; RefreshCpuSetup(); return; }
                GC.StartCpuGame(_cpuName.text.Trim());
            });
            UIKit.Btn(p2, "Back", UIKit.BtnKind.Ghost, () => { Sfx.Play("tick"); pager.Back(); });
        }

        void BuildLocalSetup(Transform c)
        {
            var h = UIKit.Label(c, "The Table", 34, Hex.To("#f4f6ff"), TextAnchor.MiddleCenter, true);
            UIKit.SetSize(h, 360, 46);
            var tag = UIKit.Label(c, "Everyone starts with 10 gelt", 14, Theme.Sub);
            UIKit.SetSize(tag, 340, 24);

            var localPager = c.gameObject.AddComponent<Pager>();
            localPager.Init(c);
            _localPager = localPager;
            var p1 = localPager.AddPage(c).transform;

            var listGo = UIKit.Node("local-list", p1);
            UIKit.Rect(listGo).sizeDelta = new Vector2(320, 40);
            var limg = listGo.AddComponent<Image>();
            limg.sprite = Theme.Rounded(Theme.RMd); limg.type = Image.Type.Sliced;
            limg.color = Theme.Surface1; limg.raycastTarget = false;
            var lv = listGo.AddComponent<VerticalLayoutGroup>();
            lv.spacing = 2f; lv.padding = new RectOffset(8, 8, 8, 8);
            lv.childForceExpandWidth = true; lv.childControlWidth = true;
            lv.childForceExpandHeight = false; lv.childControlHeight = false;
            listGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _localList = listGo.transform;


            _localName = UIKit.Input(p1, "PLAYER NAME", 10);
            UIKit.Btn(p1, "+ Add Player", UIKit.BtnKind.Ghost, AddLocalPlayer, 180f, 44f, 16);
            UIKit.SectionLabel(p1, "Game style");
            _rulesPickerLocal = UIKit.Row(p1, 8f, 46f).transform;
            UIKit.SectionLabel(p1, "Starting ante");
            _antePickerLocal = UIKit.Row(p1, 8f, 46f).transform;
            UIKit.Spacer(p1, 6f);
            UIKit.Btn(p1, "Next", UIKit.BtnKind.Primary, () =>
            {
                if (GC.G.Players.Count < 2) { Toast("Add at least 2 players", true); return; }
                Sfx.Play("tick");
                localPager.Next();
            });
            UIKit.Btn(p1, "Back", UIKit.BtnKind.Ghost, () => Show("landing"));

            var p2 = localPager.AddPage(c).transform;
            UIKit.SectionLabel(p2, "Choose your table");
            _envPickerLocal = UIKit.Grid(p2, new Vector2(96, 92)).transform;
            UIKit.SectionLabel(p2, "Table's dreidel - earned through play");
            _skinPickerLocal = UIKit.Grid(p2, new Vector2(96, 92)).transform;
            UIKit.Spacer(p2, 6f);
            UIKit.Btn(p2, "Start", UIKit.BtnKind.Primary, () =>
            {
                Sfx.Play("tick");
                if (GC.G.Players.Count < 2) { Toast("Need at least 2 players", true); return; }
                GC.StartLocalGame();
            });
            UIKit.Btn(p2, "Back", UIKit.BtnKind.Ghost, () => { Sfx.Play("tick"); localPager.Back(); });
        }

        void BuildCustom(Transform c)
        {
            var h = UIKit.Label(c, "Decision Dreidel", 32, Hex.To("#f4f6ff"), TextAnchor.MiddleCenter, true);
            UIKit.SetSize(h, 360, 44);
            var blurb = UIKit.Label(c,
                "Label the four faces - dares, chores, dinner, who goes first - then spin and let it settle. "
                + "<color=#f2c14e><b>No gelt, no pot.</b></color>", 13, Theme.Sub);
            UIKit.SetSize(blurb, 340, 54);

            string[] placeholders = { "Pizza", "Curry", "Sushi", "Chef's choice" };
            for (int i = 0; i < 4; i++)
            {
                var row = UIKit.Row(c, 10f, 54f);
                var plaque = UIKit.Node("plaque", row.transform);
                UIKit.Rect(plaque).sizeDelta = new Vector2(46, 46);
                var pimg = plaque.AddComponent<Image>();
                pimg.sprite = Theme.Rounded(10f); pimg.type = Image.Type.Sliced;
                pimg.color = Theme.Surface2;
                var glyph = UIKit.Label(plaque.transform, Consts.Sides[i].Char, 22, Theme.Gold,
                                        TextAnchor.MiddleCenter, true);
                UIKit.Stretch(glyph.gameObject);
                _customFaces[i] = UIKit.Input(row.transform, placeholders[i], 24, 250f, 50f);
            }

            UIKit.SectionLabel(c, "Choose your table");
            _envPickerCustom = UIKit.Grid(c, new Vector2(96, 92)).transform;
            UIKit.SectionLabel(c, "Your dreidel");
            _skinPickerCustom = UIKit.Grid(c, new Vector2(96, 92)).transform;

            UIKit.Spacer(c, 6f);
            UIKit.Btn(c, "Play", UIKit.BtnKind.Primary, () =>
            {
                Sfx.Play("tick");
                var labels = new string[4];
                for (int i = 0; i < 4; i++)
                {
                    var v = _customFaces[i].text.Trim();
                    labels[i] = string.IsNullOrEmpty(v) ? Consts.Sides[i].Phon : v;
                }
                Store.Set("drdl-custom", string.Join("\n", labels));
                GC.StartCustomGame(labels);
            });
            UIKit.Btn(c, "Back", UIKit.BtnKind.Ghost,
                      () => { GC.CustomMode = false; View.SetCustomFaces(false, null); Show("landing"); });
        }

        void BuildRecords(Transform c)
        {
            var h = UIKit.Label(c, "Records", 34, Hex.To("#f4f6ff"), TextAnchor.MiddleCenter, true);
            UIKit.SetSize(h, 360, 46);
            var tag = UIKit.Label(c, "Lifetime - this device", 14, Theme.Sub);
            UIKit.SetSize(tag, 340, 24);

            _recordsStats = UIKit.Grid(c, new Vector2(100, 74), 8f, 330f).transform;

            _collectionLine = UIKit.Label(c, "", 12, Theme.Sub);
            UIKit.SetSize(_collectionLine, 340, 24);

            UIKit.Spacer(c, 8f);
            _buyBtn = UIKit.Btn(c, "Unlock Full Collection", UIKit.BtnKind.Primary, () =>
            {
                Sfx.Play("tick");
                if (Unlocks.OwnsFullCollection()) return;
                // The store decides, not this button. A build with no billing bridge says so
                // rather than granting a paid unlock on tap.
                if (!Iap.RequestFullCollection())
                    Toast("Purchases are available in the store version of the game", true);
            });
            _buyLabel = _buyBtn.GetComponentInChildren<Text>();
            UIKit.Btn(c, "Restore Purchase", UIKit.BtnKind.Text, () =>
            {
                Sfx.Play("tick");
                if (Iap.RequestRestore()) return;         // the answer arrives as a callback
                Toast(Unlocks.OwnsFullCollection() ? "Full Collection already unlocked"
                                                   : "Nothing to restore yet",
                      !Unlocks.OwnsFullCollection());
            }, 200f, 40f, 15);
            UIKit.Btn(c, "Back", UIKit.BtnKind.Ghost, () => Show("landing"));
        }

        void BuildChangeDreidel(Transform c)
        {
            var h = UIKit.Label(c, "Your Dreidel", 34, Hex.To("#f4f6ff"), TextAnchor.MiddleCenter, true);
            UIKit.SetSize(h, 360, 46);
            var tag = UIKit.Label(c, "Pick a new dreidel - you'll drop straight back into the game", 14, Theme.Sub);
            UIKit.SetSize(tag, 340, 34);

            UIKit.SectionLabel(c, "Dreidel");
            _skinPickerChange = UIKit.Grid(c, new Vector2(96, 92)).transform;
            UIKit.SectionLabel(c, "Table - can't change mid-game");
            _envPickerChange = UIKit.Grid(c, new Vector2(96, 92)).transform;

            UIKit.Spacer(c, 6f);
            UIKit.Btn(c, "Back to Game", UIKit.BtnKind.Primary, () =>
            {
                Sfx.Play("tick");
                ShowGame();
                Hud.Refresh();
            });
        }

        // ---------------------------------------------------------------
        //  overlays
        // ---------------------------------------------------------------
        void BuildOverlays()
        {
            // dim + chant, over the table
            var dimGo = UIKit.Node("dim", Root);
            UIKit.Stretch(dimGo);
            _dim = dimGo.AddComponent<Image>();
            _dim.color = new Color(0, 0, 0, 0);
            _dim.raycastTarget = false;

            var chantGo = UIKit.Node("chant", Root);
            UIKit.Stretch(chantGo);
            _chant = UIKit.Label(chantGo.transform, "", 76, Theme.GoldHot, TextAnchor.MiddleCenter, true);
            UIKit.Stretch(_chant.gameObject);
            _chant.canvasRenderer.SetAlpha(0f);

            // countdown
            var cd = UIKit.Node("countdown", Root);
            _countdownGo = UIKit.Stretch(cd);
            var cdBg = cd.AddComponent<Image>();
            cdBg.color = new Color(Theme.Night.r, Theme.Night.g, Theme.Night.b, 0.55f);
            _countdown = UIKit.Label(cd.transform, "3", 110, Theme.Gold, TextAnchor.MiddleCenter, true);
            UIKit.Stretch(_countdown.gameObject);
            cd.SetActive(false);

            // showdown banner
            var sb = UIKit.Node("showdown", Root);
            _showdownGo = UIKit.Rect(sb);
            _showdownGo.anchorMin = _showdownGo.anchorMax = new Vector2(0.5f, 0.5f);
            _showdownGo.sizeDelta = new Vector2(420, 80);
            _showdown = UIKit.Label(sb.transform, "Final Showdown", 40, Theme.Danger, TextAnchor.MiddleCenter, true);
            UIKit.Stretch(_showdown.gameObject);
            sb.SetActive(false);

            BuildPause();
            BuildHowTo();
            BuildWinner();
            BuildArHint();

            var tz = UIKit.Node("toast-zone", Root);
            _toastZone = UIKit.Rect(tz);
            _toastZone.anchorMin = new Vector2(0.5f, 0); _toastZone.anchorMax = new Vector2(0.5f, 0);
            _toastZone.pivot = new Vector2(0.5f, 0);
            _toastZone.anchoredPosition = new Vector2(0, 210);
            _toastZone.sizeDelta = new Vector2(360, 50);
        }

        void BuildPause()
        {
            var go = UIKit.Node("pause", Root);
            _pause = UIKit.Stretch(go);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(Theme.Night.r, Theme.Night.g, Theme.Night.b, 0.92f);

            var col = UIKit.Node("col", go.transform);
            var crt = UIKit.Rect(col);
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(340, 100);
            var v = col.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8f; v.childAlignment = TextAnchor.MiddleCenter;
            v.childForceExpandWidth = false; v.childForceExpandHeight = false;
            v.childControlWidth = false; v.childControlHeight = false;
            col.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var h = UIKit.Label(col.transform, "Paused", 34, Hex.To("#f4f6ff"), TextAnchor.MiddleCenter, true);
            UIKit.SetSize(h, 320, 46);

            UIKit.Switch(col.transform, "Music", Sfx.MusicOn, v2 =>
            {
                Sfx.MusicOn = v2; Store.Set("drdl-music", v2 ? "1" : "0");
                if (Synth.I != null) Synth.I.MusicGain = v2 ? Synth.MusicVol : 0f;
            });
            UIKit.Switch(col.transform, "Sound FX", Sfx.SfxOn, v2 =>
            {
                Sfx.SfxOn = v2; Store.Set("drdl-sfx", v2 ? "1" : "0");
                if (!v2) Sfx.StopRumble();
                if (Synth.I != null) Synth.I.SfxGain = v2 ? 1f : 0f;
            });
            UIKit.Switch(col.transform, "Vibration", Sfx.HapticsOn, v2 =>
            {
                Sfx.HapticsOn = v2; Store.Set("drdl-haptics", v2 ? "1" : "0");
            });
            UIKit.Switch(col.transform, "Israel dreidel  <color=#8b93b8><size=10>(\u05E4 Po - else \u05E9 Sham)</size></color>",
                         Consts.IsraelMode, v2 =>
            {
                Consts.IsraelMode = v2;
                Store.Set("drdl-israel", v2 ? "1" : "0");
                Consts.RefreshSides();
                View.RebuildLetters();
                Sfx.Play("tick");
            });

            UIKit.Spacer(col.transform, 6f);
            BuildArControls(col.transform);
            UIKit.Btn(col.transform, "How to Play", UIKit.BtnKind.Ghost, ShowHowTo);
            UIKit.Btn(col.transform, "Change Dreidel", UIKit.BtnKind.Ghost, () =>
            {
                _pause.gameObject.SetActive(false);
                Hud.Show(false);
                Show("change");
            });

            UIKit.SectionLabel(col.transform, "Graphics");
            _gfxPicker = UIKit.Row(col.transform, 6f, 44f).transform;
            RenderGfxPicker();

            UIKit.Btn(col.transform, "Return to Game", UIKit.BtnKind.Primary, TogglePause);
            UIKit.Btn(col.transform, "Main Menu", UIKit.BtnKind.Danger, QuitToMenu);

            go.SetActive(false);
        }

        /// <summary>
        /// The entry point is also the exit: "Exit AR" lives in the menu, not over the room.
        /// </summary>
        void BuildArControls(Transform parent)
        {
            _arLaunch = UIKit.Btn(parent, "", UIKit.BtnKind.Ghost, () => StartCoroutine(ArToggle()), 300f, 60f, 16);
            // the button carries two lines, so its stock label is replaced by a stack
            foreach (var t in _arLaunch.GetComponentsInChildren<Text>()) Destroy(t.gameObject);
            _arLabel = UIKit.Label(_arLaunch.transform, "Play on your table", 16, Theme.Text,
                                   TextAnchor.MiddleCenter, false, FontStyle.Bold);
            var lrt = _arLabel.rectTransform;
            lrt.anchorMin = new Vector2(0, 0.5f); lrt.anchorMax = new Vector2(1, 1);
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            _arSub = UIKit.Label(_arLaunch.transform, "Put the board in the room with you", 11, Theme.Sub);
            var srt = _arSub.rectTransform;
            srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 0.5f);
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;

            _arWhy = UIKit.Label(parent, "", 11, new Color(Theme.Sub.r, Theme.Sub.g, Theme.Sub.b, 0.9f));
            UIKit.SetSize(_arWhy, 300, 34);
            _arWhy.gameObject.SetActive(false);

            var row = UIKit.Row(parent, 8f, 44f);
            _arRow = UIKit.Rect(row);
            UIKit.Btn(row.transform, "Reposition", UIKit.BtnKind.Ghost, () =>
            {
                Sfx.Play("tick");
                if (_pause.gameObject.activeSelf) TogglePause();   // placement needs the room in view
                if (Ar != null) Ar.Unplace();
            }, 138f, 42f, 14);
            var boardBtn = UIKit.Btn(row.transform, "Board: Off", UIKit.BtnKind.Ghost, () =>
            {
                Sfx.Play("tick");
                if (Ar == null) return;
                var m = Ar.SetTableMode(Ar.TableMode == "shadow" ? "board" : "shadow");
                if (_arBoardLabel != null) _arBoardLabel.text = m == "board" ? "Board: On" : "Board: Off";
            }, 138f, 42f, 14);
            _arBoardLabel = boardBtn.GetComponentInChildren<Text>();
            _arRow.gameObject.SetActive(false);
        }

        IEnumerator ArToggle()
        {
            Sfx.Play("tick");
            if (Ar == null) yield break;
            if (Ar.IsOn) { Ar.Exit(); yield break; }        // same button, both directions

            if (!Ar.Available) yield return StartCoroutine(Ar.CheckAvailability());
            if (!Ar.Available) { RefreshArButton(); Toast(Ar.WhyNot ?? "AR isn't available here", true); yield break; }

            if (!Ar.Enter()) { Toast("AR could not start", true); yield break; }
            // the pause sheet would sit on top of the room
            if (_pause.gameObject.activeSelf) TogglePause();
        }

        /// <summary>
        /// Mirrors the AR layer's state onto the menu and the hint. Called on every change,
        /// and once at boot after the capability check settles.
        /// </summary>
        public void OnArChanged(bool on, bool placed)
        {
            RefreshArButton();
            if (_arRow != null) _arRow.gameObject.SetActive(on);
            if (Embers != null) Embers.gameObject.SetActive(!on);   // the overlay is on the glass
            _shake = 0f;    // a charge left mid-flight would freeze the board off-centre

            if (_arHintBox != null) _arHintBox.gameObject.SetActive(on && !placed);
            if (on && placed) Toast("Drag to turn - pinch to resize");
            if (_arBoardLabel != null && Ar != null)
                _arBoardLabel.text = Ar.TableMode == "board" ? "Board: On" : "Board: Off";
        }

        void RefreshArButton()
        {
            if (_arLabel == null || Ar == null) return;
            bool on = Ar.IsOn;
            _arLabel.text = on ? "Exit AR" : "Play on your table";
            _arSub.text = on ? "Back to the full-screen board"
                             : Ar.Available ? "Full tracking - walk around the board"
                                            : "Put the board in the room with you";
            if (_arWhy != null)
            {
                bool show = !Ar.Available && !string.IsNullOrEmpty(Ar.WhyNot);
                _arWhy.gameObject.SetActive(show);
                if (show) _arWhy.text = Ar.WhyNot;
            }
            if (_arLaunch != null) _arLaunch.interactable = Ar.Available || on;
        }

        void BuildArHint()
        {
            var go = UIKit.Node("ar-hint", Root);
            _arHintBox = UIKit.Rect(go);
            _arHintBox.anchorMin = _arHintBox.anchorMax = new Vector2(0.5f, 1f);
            _arHintBox.pivot = new Vector2(0.5f, 1f);
            _arHintBox.anchoredPosition = new Vector2(0, -60);
            _arHintBox.sizeDelta = new Vector2(300, 40);
            var img = go.AddComponent<Image>();
            img.sprite = Theme.Rounded(20f); img.type = Image.Type.Sliced;
            img.color = new Color(Theme.Night.r, Theme.Night.g, Theme.Night.b, 0.82f);
            img.raycastTarget = false;
            UIKit.Border(go.transform, new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.4f), 20f);
            _arHint = UIKit.Label(go.transform, "Looking for a flat surface...", 13, Theme.GoldHot);
            UIKit.Stretch(_arHint.gameObject, 10f);
            go.SetActive(false);
        }

        /// <summary>
        /// While the board is unplaced the hint tracks the tracker: it really is looking for a
        /// plane, and it says so until it finds one.
        /// </summary>
        void UpdateArHint()
        {
            if (Ar == null || _arHintBox == null) return;
            bool want = Ar.IsOn && !Ar.IsPlaced;
            if (_arHintBox.gameObject.activeSelf != want) _arHintBox.gameObject.SetActive(want);
            if (!want) return;
            _arHint.text = Ar.HasSurface ? "Tap to set the board down" : "Looking for a flat surface...";
            _arHint.color = Ar.HasSurface ? Theme.GoldHot : Theme.Sub;
        }

        Text _howtoAcro;
        Text _howtoFourthGlyph, _howtoFourthName, _howtoFourthDesc;

        void BuildHowTo()
        {
            var go = UIKit.Node("howto", Root);
            _howto = UIKit.Stretch(go);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.72f);

            var card = UIKit.Panel(go.transform, Theme.Card, Theme.RXl, "howto-card");
            var crt = UIKit.Rect(card.gameObject);
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(360, 100);
            UIKit.Border(card.transform, new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.35f), Theme.RXl);
            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6f; v.padding = new RectOffset(20, 20, 18, 18);
            v.childAlignment = TextAnchor.UpperCenter;
            v.childForceExpandWidth = false; v.childForceExpandHeight = false;
            v.childControlWidth = false; v.childControlHeight = false;
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var t = UIKit.Label(card.transform, "How to Play", 26, Theme.Gold, TextAnchor.MiddleCenter, true);
            UIKit.SetSize(t, 320, 34);
            var sub = UIKit.Label(card.transform, "Antes rise as the game goes on - hold to charge, release to spin",
                                  12, Theme.Sub);
            UIKit.SetSize(sub, 320, 32);

            AddHowToRow(card.transform, Consts.Sides[0].Char, "NUN", "<i>nisht</i> - nothing happens.");
            AddHowToRow(card.transform, Consts.Sides[1].Char, "GIMEL", "<i>gants</i> - sweep the whole pot.");
            AddHowToRow(card.transform, Consts.Sides[2].Char, "HEI", "<i>halb</i> - take half the pot.");
            var fourth = AddHowToRow(card.transform, Consts.Fourth().Char, Consts.Fourth().Name,
                                     "<i>shtel</i> - put one in the pot.");
            _howtoFourthGlyph = fourth[0]; _howtoFourthName = fourth[1]; _howtoFourthDesc = fourth[2];

            _howtoAcro = UIKit.Label(card.transform,
                "The letters spell <b>Nes Gadol Haya Sham</b> - \"a great miracle happened there.\"",
                12, Theme.Sub);
            UIKit.SetSize(_howtoAcro, 320, 34);
            var note = UIKit.Label(card.transform,
                "Play actions come from the Yiddish: nisht / gants / halb / shtel. "
                + "Run out of gelt and you're out - last player holding gelt wins.", 12,
                new Color(Theme.Sub.r, Theme.Sub.g, Theme.Sub.b, 0.85f));
            UIKit.SetSize(note, 320, 44);

            UIKit.Spacer(card.transform, 6f);
            UIKit.Btn(card.transform, "Got It", UIKit.BtnKind.Primary, () =>
            {
                _howto.gameObject.SetActive(false);
                Store.Set("drdl-seen", "1");
            }, 160f, 46f, 17);

            go.SetActive(false);
        }

        Text[] AddHowToRow(Transform parent, string glyph, string name, string desc)
        {
            var row = UIKit.Row(parent, 10f, 34f, TextAnchor.MiddleLeft);
            UIKit.Rect(row).sizeDelta = new Vector2(320, 34);
            var g = UIKit.Label(row.transform, glyph, 24, Theme.Gold, TextAnchor.MiddleCenter, true);
            UIKit.SetSize(g, 32, 32);
            var n = UIKit.Label(row.transform, name, 13, Theme.Text, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            UIKit.SetSize(n, 64, 32);
            var d = UIKit.Label(row.transform, desc, 12, Theme.Sub, TextAnchor.MiddleLeft);
            UIKit.SetSize(d, 200, 32);
            return new[] { g, n, d };
        }

        void BuildWinner()
        {
            var go = UIKit.Node("winner", Root);
            _winner = UIKit.Stretch(go);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(Theme.Night.r, Theme.Night.g, Theme.Night.b, 0.86f);

            var col = UIKit.Node("col", go.transform);
            var crt = UIKit.Rect(col);
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(360, 100);
            var v = col.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6f; v.childAlignment = TextAnchor.MiddleCenter;
            v.childForceExpandWidth = false; v.childForceExpandHeight = false;
            v.childControlWidth = false; v.childControlHeight = false;
            col.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sub = UIKit.Label(col.transform, "Nes Gadol Haya Sham", 13, Theme.Sub);
            UIKit.SetSize(sub, 340, 22);
            var h = UIKit.Label(col.transform, "Winner", 44, Hex.To("#f4f6ff"), TextAnchor.MiddleCenter, true);
            UIKit.SetSize(h, 340, 56);
            _winnerName = UIKit.Label(col.transform, "Player", 32, Theme.Gold, TextAnchor.MiddleCenter, true);
            UIKit.SetSize(_winnerName, 340, 44);
            _winnerLine = UIKit.Label(col.transform, "walks away with all the gelt", 14, Theme.Sub);
            UIKit.SetSize(_winnerLine, 340, 24);

            UIKit.Spacer(col.transform, 8f);
            _winStats = UIKit.Grid(col.transform, new Vector2(78, 62), 6f, 340f).transform;
            _winLifetime = UIKit.Label(col.transform, "", 11, Theme.Sub);
            UIKit.SetSize(_winLifetime, 340, 20);

            UIKit.Spacer(col.transform, 8f);
            UIKit.Btn(col.transform, "Share Result", UIKit.BtnKind.Ghost, ShareResult, 200f, 46f, 15);
            UIKit.Btn(col.transform, "Rematch", UIKit.BtnKind.Primary, () => { Sfx.Play("tick"); GC.Rematch(); });
            UIKit.Btn(col.transform, "Main Menu", UIKit.BtnKind.Ghost, QuitToMenu);

            go.SetActive(false);
        }

        // ---------------------------------------------------------------
        //  navigation
        // ---------------------------------------------------------------
        public void Show(string id)
        {
            foreach (var kv in _screens) kv.Value.gameObject.SetActive(kv.Key == id);
            Hud.Show(false);
            Current = id;

            if (id == "cpu") { if (_cpuPager != null) _cpuPager.Show(0); RefreshCpuSetup(); }
            else if (id == "local") { if (_localPager != null) _localPager.Show(0); RefreshLocalSetup(); }
            else if (id == "custom") RefreshCustom();
            else if (id == "records") RefreshRecords();
            else if (id == "change") RefreshChange();
            else if (id == "landing") RefreshResumeButton();
            else if (id == "net-name" && NetScreens != null) NetScreens.OnNameScreenShown();
            else if (id == "net-code" && NetScreens != null) NetScreens.OnCodeScreenShown();

            if (Chat != null) Chat.SetAvailable(id == "net-lobby" && GC.Net != null && GC.Net.Active);

            RectTransform rt;
            if (_screens.TryGetValue(id, out rt)) StartCoroutine(ScreenIn(rt));
        }

        void RefreshResumeButton()
        {
            int round = GC.SavedRound();
            _resumeBtn.gameObject.SetActive(round > 0);
            if (round > 0 && _resumeLabel != null) _resumeLabel.text = "Resume Game - Round " + round;
        }

        IEnumerator ScreenIn(RectTransform rt)
        {
            var cg = rt.GetComponent<CanvasGroup>();
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.4f);
                cg.alpha = k;
                rt.localScale = Vector3.one * Mathf.Lerp(0.995f, 1f, k);
                yield return null;
            }
            cg.alpha = 1f; rt.localScale = Vector3.one;
        }

        public void ShowGame()
        {
            foreach (var kv in _screens) kv.Value.gameObject.SetActive(false);
            _pause.gameObject.SetActive(false);
            _winner.gameObject.SetActive(false);
            _howto.gameObject.SetActive(false);
            if (NetScreens != null) NetScreens.HideReconnect();
            Current = "game";
            Hud.Show(true);
            if (Chat != null) Chat.SetAvailable(GC.Net != null && GC.Net.Active);
        }

        public void TogglePause()
        {
            _pause.gameObject.SetActive(!_pause.gameObject.activeSelf);
            Sfx.Play("tick");
        }

        /// <summary>Back to the landing screen, whatever was showing.</summary>
        public void BackToLanding()
        {
            _pause.gameObject.SetActive(false);
            _winner.gameObject.SetActive(false);
            _howto.gameObject.SetActive(false);
            if (NetScreens != null) NetScreens.ShowObserverChip(false);
            View.SetDrama(false);
            View.SetPotCoins(0);
            Show("landing");
        }

        void QuitToMenu()
        {
            Sfx.Play("tick");
            GC.StopDangerBeat();
            if (GC.Net != null) GC.Net.LeaveEverything();     // tell the table before walking away
            GC.IsLocalGame = true;
            GC.G.Status = GameStatus.Lobby;
            GC.CustomMode = false;
            View.SetCustomFaces(false, null);
            GC.Music.SetIntensity(0);
            BackToLanding();
        }

        public void ShowHowTo()
        {
            RefreshHowTo();
            _howto.gameObject.SetActive(true);
        }

        void RefreshHowTo()
        {
            var f = Consts.Fourth();
            _howtoFourthGlyph.text = f.Char;
            _howtoFourthName.text = f.Name;
            _howtoFourthDesc.text = Consts.IsraelMode
                ? "<i>pei</i> - put one in the pot."
                : "<i>shtel</i> - put one in the pot.";
            _howtoAcro.text = Consts.IsraelMode
                ? "The letters spell <b>Nes Gadol Haya Po</b> - \"a great miracle happened here.\""
                : "The letters spell <b>Nes Gadol Haya Sham</b> - \"a great miracle happened there.\"";
        }

        void OpenCustom()
        {
            try
            {
                var saved = Store.Get("drdl-custom");
                if (!string.IsNullOrEmpty(saved))
                {
                    var parts = saved.Split('\n');
                    for (int i = 0; i < 4 && i < parts.Length; i++) _customFaces[i].text = parts[i];
                }
            }
            catch { }
            Show("custom");
        }

        // ---------------------------------------------------------------
        //  screen refreshes
        // ---------------------------------------------------------------
        void RefreshCpuSetup()
        {
            UIKit.Clear(_cpuCountPicker);
            for (int n = 1; n <= 3; n++)
            {
                int cn = n;
                UIKit.Chip(_cpuCountPicker, n.ToString(), GC.CpuCount == n,
                           () => { GC.CpuCount = cn; Sfx.Play("tick"); Sfx.Buzz(10); RefreshCpuSetup(); });
            }
            UIKit.Clear(_cpuDiffPicker);
            foreach (var d in CpuBrain.Diffs)
            {
                var cd = d;
                UIKit.Chip(_cpuDiffPicker, d.Label, GC.CpuDiff == d.Id,
                           () => { GC.CpuDiff = cd.Id; Store.Set("drdl-cpudiff", cd.Id); Sfx.Play("tick"); RefreshCpuSetup(); },
                           92f);
            }
            RenderRules(_rulesPickerCpu, RefreshCpuSetup);
            RenderAnte(_antePickerCpu, RefreshCpuSetup);
            Pickers.RenderEnv(_envPickerCpu, GC.HostEnvChoice, id => { PickEnv(id); RefreshCpuSetup(); });
            Pickers.RenderSkin(_skinPickerCpu, GC.MySkinChoice, id => { PickSkin(id); RefreshCpuSetup(); });
        }

        void RefreshLocalSetup()
        {
            UIKit.Clear(_localList);
            if (GC.G.Players.Count == 0)
            {
                var t = UIKit.Label(_localList, "Add at least 2 players", 13, new Color(0.32f, 0.36f, 0.55f));
                UIKit.SetSize(t, 300, 26);
                t.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
            }
            for (int i = 0; i < GC.G.Players.Count; i++)
            {
                var p = GC.G.Players[i];
                int idx = i;
                var row = UIKit.Row(_localList, 8f, 34f, TextAnchor.MiddleLeft);
                UIKit.Rect(row).sizeDelta = new Vector2(296, 34);
                row.AddComponent<LayoutElement>().preferredHeight = 34;

                var av = UIKit.Node("avatar", row.transform);
                UIKit.Rect(av).sizeDelta = new Vector2(24, 24);
                var avImg = av.AddComponent<Image>();
                avImg.sprite = Theme.Circle();
                avImg.color = Consts.HueColor(i);
                var init = UIKit.Label(av.transform, p.Name.Substring(0, 1).ToUpper(), 11, Color.white,
                                       TextAnchor.MiddleCenter, false, FontStyle.Bold);
                UIKit.Stretch(init.gameObject);

                var n = UIKit.Label(row.transform, p.Name, 15, Theme.Text, TextAnchor.MiddleLeft);
                UIKit.SetSize(n, 156, 30);
                var coins = UIKit.Label(row.transform, p.Coins.ToString(), 14, Theme.Gold,
                                        TextAnchor.MiddleRight, false, FontStyle.Bold);
                UIKit.SetSize(coins, 34, 30);
                var goneName = p.Name;
                UIKit.Btn(row.transform, "X", UIKit.BtnKind.Danger, () =>
                {
                    GC.G.Players.RemoveAt(idx);
                    Sfx.Play("tick");
                    Toast(goneName + " removed");
                    RefreshLocalSetup();
                }, 40f, 30f, 14);
            }

            RenderRules(_rulesPickerLocal, RefreshLocalSetup);
            RenderAnte(_antePickerLocal, RefreshLocalSetup);
            Pickers.RenderEnv(_envPickerLocal, GC.HostEnvChoice, id => { PickEnv(id); RefreshLocalSetup(); });
            Pickers.RenderSkin(_skinPickerLocal, GC.MySkinChoice, id => { PickSkin(id); RefreshLocalSetup(); });
        }

        void AddLocalPlayer()
        {
            var raw = _localName.text.Trim();
            if (string.IsNullOrEmpty(raw)) { Toast("Enter a name first", true); return; }
            if (GC.G.Players.Count >= 8) { Toast("The table is full", true); return; }
            var name = UniqueName(GameController.Trim(raw, 10));
            GC.G.Players.Add(new Player("L" + GC.G.Players.Count, name, Consts.StartCoins));
            _localName.text = "";
            Sfx.Play("tick"); Sfx.Buzz(10);
            RefreshLocalSetup();
        }

        string UniqueName(string basename)
        {
            var taken = new HashSet<string>(GC.G.Players.Select(p => p.Name));
            if (!taken.Contains(basename)) return basename;
            for (int i = 2; i < 40; i++)
            {
                var t = basename + " " + i;
                if (!taken.Contains(t)) return t;
            }
            return basename;
        }

        void RefreshCustom()
        {
            for (int i = 0; i < 4; i++)
            {
                if (_customFaces[i] == null) continue;
                var plaque = _customFaces[i].transform.parent.GetChild(0);
                var g = plaque.GetComponentInChildren<Text>();
                if (g != null) g.text = Consts.Sides[i].Char;
            }
            Pickers.RenderEnv(_envPickerCustom, GC.HostEnvChoice, id => { PickEnv(id); RefreshCustom(); });
            Pickers.RenderSkin(_skinPickerCustom, GC.MySkinChoice, id => { PickSkin(id); RefreshCustom(); });
        }

        void RefreshChange()
        {
            Pickers.RenderSkin(_skinPickerChange, GC.MySkinChoice,
                               id => { PickSkin(id, false); RefreshChange(); }, false);
            // the table is locked mid-game: the pot and props are already on it
            Pickers.RenderEnv(_envPickerChange, GC.G.Env, _ => Toast("Table can't change mid-game", true), true);
        }

        void RefreshRecords()
        {
            var S = Stats.Load();
            UIKit.Clear(_recordsStats);

            if (S.games == 0)
            {
                // An empty grid of zeroes reads as a bug. One line reads as an invitation.
                var empty = UIKit.Label(_recordsStats, "No games yet - your story starts with a spin.",
                                        13, Theme.Sub, TextAnchor.MiddleCenter);
                UIKit.SetSize(empty, 320, 60);
            }
            else
            {
                int played = S.wins + S.losses;
                string rate = played > 0 ? Mathf.RoundToInt(S.wins / (float)played * 100f) + "%" : "-";
                AddStat(_recordsStats, S.games.ToString(),      "Games");
                AddStat(_recordsStats, S.wins.ToString(),       "Wins");
                AddStat(_recordsStats, rate,                    "Win Rate");
                AddStat(_recordsStats, S.bestStreak.ToString(), "Best Streak");
                AddStat(_recordsStats, S.spins.ToString(),      "Spins");
                AddStat(_recordsStats, S.gimels.ToString(),     "Gimels");
                AddStat(_recordsStats, S.bestSweep.ToString(),  "Best Sweep");
                AddStat(_recordsStats, S.streak.ToString(),     "Streak");
            }

            if (_collectionLine != null)
            {
                int skins = Unlocks.Skins.Count(d => Unlocks.SkinUnlocked(d, S));
                int envs = EnvDefs.All.Count(kv => Unlocks.EnvUnlocked(kv.Key, S));
                _collectionLine.text = string.Format("Collection - {0}/{1} dreidels - {2}/{3} tables",
                                                     skins, Unlocks.Skins.Count, envs, EnvDefs.All.Count);
                _collectionLine.gameObject.SetActive(S.games > 0);
            }

            RefreshIapRow();
        }

        /// <summary>The buy row states what is already owned rather than offering it again.</summary>
        void RefreshIapRow()
        {
            if (_buyBtn == null) return;
            bool owned = Unlocks.OwnsFullCollection();
            if (_buyLabel != null)
                _buyLabel.text = owned ? "Full Collection owned" : "Unlock Full Collection";
            _buyBtn.interactable = !owned;
        }

        void AddStat(Transform parent, string value, string label)
        {
            var go = UIKit.Node("stat", parent);
            var img = go.AddComponent<Image>();
            img.sprite = Theme.Rounded(Theme.RSm); img.type = Image.Type.Sliced;
            img.color = Theme.Surface1; img.raycastTarget = false;
            var v = UIKit.Label(go.transform, value, 22, Theme.Gold, TextAnchor.MiddleCenter, true);
            var vrt = v.rectTransform;
            vrt.anchorMin = new Vector2(0, 0.42f); vrt.anchorMax = new Vector2(1, 1);
            vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
            var l = UIKit.Label(go.transform, label.ToUpper(), 9, Theme.Sub, TextAnchor.UpperCenter, false, FontStyle.Bold);
            var lrt = l.rectTransform;
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 0.42f);
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        }

        void RenderRules(Transform container, Action after)
        {
            UIKit.Clear(container);
            foreach (var r in Rules.Defs)
            {
                var cr = r;
                UIKit.Chip(container, r.Label, GC.RulesMode == r.Id, () =>
                {
                    GC.RulesMode = cr.Id;
                    Store.Set("drdl-rules", cr.Id);
                    Sfx.Play("tick"); Sfx.Buzz(10);
                    after();
                }, 108f);
            }
        }

        void RenderAnte(Transform container, Action after)
        {
            UIKit.Clear(container);
            for (int n = 1; n <= 3; n++)
            {
                int cn = n;
                UIKit.Chip(container, n.ToString(), GC.AnteAmount == n, () =>
                {
                    GC.AnteAmount = cn;
                    Store.Set("drdl-ante", cn.ToString());
                    Sfx.Play("tick"); Sfx.Buzz(10);
                    after();
                });
            }
        }

        void PickEnv(string id)
        {
            GC.HostEnvChoice = id;
            Store.Set("drdl-env", id);
            GC.ApplyEnv(id);
        }

        /// <summary>
        /// `pairTable` is false only on the Change Dreidel screen, where the table is locked
        /// mid-game and must not switch out from under the players.
        /// </summary>
        void PickSkin(string id, bool pairTable = true)
        {
            GC.MySkinChoice = id;
            Store.Set("drdl-skin", id);

            // Mid-game the dreidel on the table belongs to whoever's turn it is, so a change
            // from the pause menu only shows immediately when that dreidel is mine - and
            // never mid-spin, which would swap the piece in the air.
            bool showNow = GC.G == null || GC.G.Status != GameStatus.Playing;
            if (!showNow && !GC.IsSpinning)
            {
                var cur = GC.G.Current;
                showNow = GC.IsLocalGame || GC.CustomMode
                          || (cur != null && GC.Net != null && GC.Net.IsMySeat(cur));
            }
            if (showNow) View.SetSkin(id);

            // Announce it so the rest of the table sees the new dreidel on my next turn.
            if (GC.Net != null && GC.Net.Active) GC.Net.AnnounceSkin(id);

            RefreshChrome();       // the table and the dreidel together decide the chrome

            // The Blue Pup and the Backyard only travel together, in both directions.
            if (pairTable && !_pairLock)
            {
                _pairLock = true;
                try
                {
                    if (id == "heeler" && GC.HostEnvChoice != "backyard")
                    {
                        PickEnv("backyard");
                        Toast("Blue Pup brings the Backyard along");
                    }
                    else if (id != "heeler" && GC.HostEnvChoice == "backyard")
                    {
                        PickEnv("midnight");
                    }
                }
                finally { _pairLock = false; }
            }
        }

        bool _pairLock;

        /// <summary>
        /// The secret test unlock, typed into a name field. Kept for the same reason the web
        /// build keeps it: so the premium pieces can be checked without a store round-trip.
        /// </summary>
        public bool CheckTestUnlock(string name)
        {
            // Debug builds only, exactly as the web build gated it on window.DR_DEBUG. In a
            // release build the code does nothing, so the purchase can't be typed past.
            if (!Debug.isDebugBuild && !Application.isEditor) return false;
            if (name == null || name.Trim().ToUpper() != Unlocks.TestUnlockCode) return false;
            Iap.GrantEntitlement(true);
            return true;
        }

        // ---------------------------------------------------------------
        //  overlay controls
        // ---------------------------------------------------------------
        public void ApplyEnvBackdrop(EnvDef env)
        {
            if (Embers != null) Embers.SetEnv(env);
            if (Frame != null) Frame.SetEnv(env);
            RefreshChrome();
        }

        /// <summary>
        /// Two pairings change the whole look, and only when BOTH halves are chosen - the table
        /// and the dreidel have to agree before the game commits to a different world.
        ///
        ///   Backyard + Blue Pup    -> the diorama gives way to the flat 2D playroom
        ///   Blocky Biome + Grass   -> the UI squares off into voxel chrome
        /// </summary>
        public void RefreshChrome()
        {
            var env = GC.AppliedEnv ?? "";
            var skin = GC.MySkinChoice ?? "";

            bool flat = env == "backyard" && skin == "heeler";
            if (Flat != null) Flat.SetActive(flat);

            bool blocky = env == "blocky" && skin == "blocky";
            SetBlockChrome(blocky);
        }

        bool _blockChrome;

        /// <summary>
        /// Voxel chrome: every rounded corner in the UI squares off. The panels were built with
        /// nine-sliced sprites, so this is a sprite swap over the tree rather than a rebuild -
        /// which also means it can be turned straight back off.
        /// </summary>
        void SetBlockChrome(bool on)
        {
            if (_blockChrome == on) return;
            _blockChrome = on;
            foreach (var img in Root.GetComponentsInChildren<Image>(true))
            {
                if (img.type != Image.Type.Sliced || img.sprite == null) continue;
                var swapped = Theme.Blockify(img.sprite, on);
                if (swapped != null) img.sprite = swapped;
            }
        }

        public void SetDim(float a) { _dim.color = new Color(0, 0, 0, a); }

        public void Chant(string word, float power)
        {
            _chant.text = word;
            if (_chantRoutine != null) StopCoroutine(_chantRoutine);
            _chantRoutine = StartCoroutine(ChantRoutine(power));
        }

        Coroutine _chantRoutine;

        IEnumerator ChantRoutine(float power)
        {
            float t = 0f;
            const float dur = 0.55f;
            var rt = _chant.rectTransform;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                _chant.canvasRenderer.SetAlpha(k < 0.25f ? k / 0.25f : 1f - (k - 0.25f) / 0.75f);
                rt.localScale = Vector3.one * (0.7f + (0.5f + power * 0.5f) * Mathf.Min(k * 3f, 1f));
                yield return null;
            }
            _chant.canvasRenderer.SetAlpha(0f);
        }

        public void ShowCountdown(string s)
        {
            _countdownGo.gameObject.SetActive(true);
            _countdown.text = s;
            StartCoroutine(CountdownPop());
        }

        IEnumerator CountdownPop()
        {
            var rt = _countdown.rectTransform;
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                rt.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.Clamp01(t / 0.35f));
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        public void HideCountdown() { _countdownGo.gameObject.SetActive(false); }

        public void ShowShowdownBanner() { StartCoroutine(ShowdownRoutine()); }

        IEnumerator ShowdownRoutine()
        {
            _showdownGo.gameObject.SetActive(true);
            float t = 0f;
            while (t < 2.2f)
            {
                t += Time.deltaTime;
                float k = t / 2.2f;
                _showdown.canvasRenderer.SetAlpha(k < 0.15f ? k / 0.15f : k > 0.8f ? (1f - k) / 0.2f : 1f);
                _showdownGo.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.12f);
                yield return null;
            }
            _showdownGo.gameObject.SetActive(false);
        }

        public void ShowWinner(string name, bool humanLost, int rounds, int spins, int gimels,
                               int bestSweep, LifetimeStats S)
        {
            _winnerName.text = name;
            _winnerLine.text = humanLost ? "The house takes it this time" : "walks away with all the gelt";
            UIKit.Clear(_winStats);
            AddStat(_winStats, rounds.ToString(), "Rounds");
            AddStat(_winStats, spins.ToString(), "Spins");
            AddStat(_winStats, gimels.ToString(), "Gimels");
            AddStat(_winStats, bestSweep.ToString(), "Best Sweep");
            _winLifetime.text = string.Format("{0} played - {1}W - {2}L - best sweep {3}",
                                              S.games, S.wins, S.losses, S.bestSweep);
            _winner.gameObject.SetActive(true);
        }

        public void HideWinner() { _winner.gameObject.SetActive(false); }

        // ---------------------------------------------------------------
        //  graphics tier
        // ---------------------------------------------------------------
        void RenderGfxPicker()
        {
            if (_gfxPicker == null) return;
            UIKit.Clear(_gfxPicker);
            foreach (var kv in GfxSettings.Labels)
            {
                var id = kv.Key;
                UIKit.Chip(_gfxPicker, kv.Value, GfxSettings.Mode == id, () =>
                {
                    Sfx.Play("tick"); Sfx.Buzz(10);
                    GfxSettings.Pick(id);
                    RenderGfxPicker();
                    // Auto names the tier it landed on, because "Auto" alone doesn't tell
                    // anyone whether their phone was judged fast or slow.
                    Toast("Graphics: " + GfxSettings.LabelFor(id)
                          + (id == GfxSettings.Auto ? " (" + GfxSettings.LabelFor(GfxSettings.Tier) + ")" : ""));
                }, 74f, 40f, 13);
            }
        }

        /// <summary>Every visible dreidel/table picker, re-rendered after an entitlement lands.</summary>
        public void RefreshPickers()
        {
            if (Current == "records") RefreshRecords();
            else if (Current == "cpu") RefreshCpuSetup();
            else if (Current == "local") RefreshLocalSetup();
            else if (Current == "custom") RefreshCustom();
            else if (Current == "change") RefreshChange();
            if (NetScreens != null) NetScreens.RefreshSkinPicker();
        }

        /// <summary>
        /// An invite tapped elsewhere on the phone. Only a code is honoured, and only from a
        /// standing start: dropping someone out of a live game because a message arrived
        /// would be worse than making them finish it.
        /// </summary>
        public void HandleDeepLink(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            var code = ExtractJoinCode(url);
            if (string.IsNullOrEmpty(code)) return;

            if (GC.G != null && GC.G.Status == GameStatus.Playing
                || (GC.Net != null && GC.Net.Active))
            {
                Toast("Finish this game first - invite code was " + code, true);
                return;
            }
            if (NetScreens != null) NetScreens.BeginJoinWithCode(code);
        }

        static string ExtractJoinCode(string url)
        {
            int q = url.IndexOf("join=", StringComparison.OrdinalIgnoreCase);
            if (q < 0) return null;
            var rest = url.Substring(q + 5);
            int end = rest.IndexOfAny(new[] { '&', '#', '/', '?' });
            if (end >= 0) rest = rest.Substring(0, end);
            rest = DreidelRoyale.Net.RoomCode.Clean(rest);
            return DreidelRoyale.Net.RoomCode.IsValid(rest) ? rest : null;
        }

        /// <summary>
        /// The result as a story rather than a scoreline: the spin log runs as emoji, so
        /// someone reading it can see the game turn.
        /// </summary>
        void ShareResult()
        {
            Sfx.Play("tick");
            var st = GC.G.Stats;
            var body = "Dreidel Royale \U0001FA99\n"
                     + string.Format("\U0001F3C6 {0} wins in {1} rounds - best sweep {2} gelt\n",
                                     _winnerName.text, GC.G.Round, st.BiggestSweep)
                     + NativeShare.HistoryEmoji(st.History);
            if (!NativeShare.Share("Dreidel Royale", body))
                Toast("Result copied to the clipboard");
        }

        /// <summary>The wind-up shakes the whole view - a nudge, not a lurch.</summary>
        public void ShakeScreen(float power) { _shake = power; }
        float _shake;

        /// <summary>
        /// The slam. A short decaying jolt whose magnitude scales with the spin's power, on
        /// top of whatever the wind-up is already doing.
        /// </summary>
        public void ImpactShake(float magnitudePx)
        {
            if (_impactRoutine != null) StopCoroutine(_impactRoutine);
            _impactRoutine = StartCoroutine(ImpactShakeRoutine(magnitudePx));
        }

        Coroutine _impactRoutine;
        float _impact;

        IEnumerator ImpactShakeRoutine(float mag)
        {
            float t = 0f;
            const float dur = 0.52f;
            while (t < dur)
            {
                t += Time.deltaTime;
                _impact = mag * (1f - t / dur);
                yield return null;
            }
            _impact = 0f;
            _impactRoutine = null;
        }

        void Update() { UpdateArHint(); }

        void LateUpdate()
        {
            // Screen shake tears the board off the table in AR, where the camera is the phone
            // and the world is supposed to be standing still in the room.
            if (Ar != null && Ar.IsOn) return;
            float amount = _shake * 0.05f + _impact * 0.012f;
            if (amount > 0f && View != null && View.Cam != null)
            {
                var c = View.Cam.transform;
                c.position += new Vector3((UnityEngine.Random.value - 0.5f) * amount,
                                          (UnityEngine.Random.value - 0.5f) * amount, 0f);
            }
        }

        // ---- toasts ----
        public void Toast(string msg, bool bad = false)
        {
            _toastQ.Enqueue(new KeyValuePair<string, bool>(msg, bad));
            if (!_toastShowing) StartCoroutine(ToastPump());
        }

        IEnumerator ToastPump()
        {
            _toastShowing = true;
            while (_toastQ.Count > 0)
            {
                var item = _toastQ.Dequeue();
                var go = UIKit.Node("toast", _toastZone);
                var rt = UIKit.Rect(go);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
                rt.sizeDelta = new Vector2(340, 44);
                var img = go.AddComponent<Image>();
                img.sprite = Theme.Rounded(Theme.RMd); img.type = Image.Type.Sliced;
                img.color = item.Value ? new Color(0.35f, 0.08f, 0.16f, 0.94f) : new Color(0.06f, 0.09f, 0.22f, 0.94f);
                UIKit.Border(go.transform, item.Value ? Theme.Danger : Theme.Gold, Theme.RMd);
                var t = UIKit.Label(go.transform, item.Key, 14, item.Value ? Theme.Danger : Theme.GoldHot);
                UIKit.Stretch(t.gameObject, 12f);

                var cg = go.AddComponent<CanvasGroup>();
                float e = 0f;
                while (e < 0.25f)
                {
                    e += Time.deltaTime;
                    cg.alpha = e / 0.25f;
                    rt.anchoredPosition = new Vector2(0, e / 0.25f * 10f);
                    yield return null;
                }
                cg.alpha = 1f;
                yield return new WaitForSeconds(2.0f);
                e = 0f;
                while (e < 0.3f) { e += Time.deltaTime; cg.alpha = 1f - e / 0.3f; yield return null; }
                Destroy(go);
            }
            _toastShowing = false;
        }
    }
}
