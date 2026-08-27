using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DreidelRoyale.Audio;
using DreidelRoyale.Core;
using DreidelRoyale.Visual;

namespace DreidelRoyale.UI
{
    /// <summary>
    /// The in-game overlay: the turn badge, the seats down the left, the pot with its pip
    /// stack, the result card, and the charge ring around the spin coin.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public RectTransform Root;
        public GameController GC;
        public DreidelView View;
        public UIManager UI;

        Text _turnBadge, _potVal, _potLbl;
        RectTransform _playerList, _potStack;
        public RectTransform PotBox;

        RectTransform _resultCard;
        Text _resChar, _resPhon, _resText, _resOutcome;
        CanvasGroup _resultGroup;

        Image _ringFill, _ringSweet;
        Text _tierFlash;
        CanvasGroup _tierGroup;
        Button _spinBtn;
        Text _spinLabel;

        readonly Dictionary<int, RectTransform> _rows = new Dictionary<int, RectTransform>();
        readonly Dictionary<string, int> _prevCoins = new Dictionary<string, int>();
        readonly Dictionary<int, string> _activeQuips = new Dictionary<int, string>();
        readonly Dictionary<int, float> _quipUntil = new Dictionary<int, float>();

        int? _potShown;
        Coroutine _potTween;

        // ---- charge state ----
        bool _isCharging;
        float _chargePower, _chargeStart;

        public void Build(RectTransform parent)
        {
            Root = UIKit.Rect(UIKit.Node("game-ui", parent));
            UIKit.Stretch(Root.gameObject);

            BuildHamburger();
            BuildTurnBadge();
            BuildTop();
            BuildBottom();
            if (UI != null && UI.Fx != null) UI.Fx.PotSource = PotBox;
            Root.gameObject.SetActive(false);
        }

        void BuildHamburger()
        {
            var b = UIKit.Btn(Root, "≡", UIKit.BtnKind.Ghost, () => UI.TogglePause(), 46f, 46f, 24);
            var rt = UIKit.Rect(b.gameObject);
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-14, -14);
        }

        void BuildTurnBadge()
        {
            var go = UIKit.Node("turn-badge", Root);
            var rt = UIKit.Rect(go);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -12);
            rt.sizeDelta = new Vector2(300, 38);
            var img = go.AddComponent<Image>();
            img.sprite = Theme.Rounded(19f);
            img.type = Image.Type.Sliced;
            img.color = new Color(28 / 255f, 36 / 255f, 74 / 255f, 0.85f);
            img.raycastTarget = false;
            _turnBadge = UIKit.Label(go.transform, "…", 15, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIKit.Stretch(_turnBadge.gameObject, 8f);
        }

        void BuildTop()
        {
            // seats down the left
            var list = UIKit.Node("player-list", Root);
            _playerList = UIKit.Rect(list);
            _playerList.anchorMin = new Vector2(0, 1); _playerList.anchorMax = new Vector2(0, 1);
            _playerList.pivot = new Vector2(0, 1);
            _playerList.anchoredPosition = new Vector2(12, -62);
            _playerList.sizeDelta = new Vector2(210, 10);
            var panel = list.AddComponent<Image>();
            panel.sprite = Theme.Rounded(Theme.RLg);
            panel.type = Image.Type.Sliced;
            panel.color = Theme.CardGlass;
            panel.raycastTarget = false;
            var v = list.AddComponent<VerticalLayoutGroup>();
            v.spacing = 2f; v.padding = new RectOffset(8, 8, 8, 8);
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            v.childControlWidth = true; v.childControlHeight = false;
            list.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // the pot, top right
            var pot = UIKit.Node("pot-box", Root);
            PotBox = UIKit.Rect(pot);
            PotBox.anchorMin = PotBox.anchorMax = new Vector2(1, 1);
            PotBox.pivot = new Vector2(1, 1);            // growth goes inward/down, never off-screen
            PotBox.anchoredPosition = new Vector2(-14, -70);
            PotBox.sizeDelta = new Vector2(140, 108);
            var pimg = pot.AddComponent<Image>();
            pimg.sprite = Theme.Rounded(Theme.RLg);
            pimg.type = Image.Type.Sliced;
            pimg.color = Theme.CardGlass;
            pimg.raycastTarget = false;
            UIKit.Border(pot.transform, new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.18f), Theme.RLg);

            var pv = pot.AddComponent<VerticalLayoutGroup>();
            pv.padding = new RectOffset(8, 8, 8, 10);
            pv.spacing = 1f;
            pv.childAlignment = TextAnchor.UpperCenter;
            pv.childForceExpandWidth = true; pv.childForceExpandHeight = false;
            pv.childControlWidth = true; pv.childControlHeight = true;

            _potLbl = UIKit.Label(pot.transform, "THE POT", 11, Theme.Sub, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            _potLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 14;
            _potVal = UIKit.Label(pot.transform, "0", 36, Theme.Gold, TextAnchor.MiddleCenter, true);
            _potVal.gameObject.AddComponent<LayoutElement>().preferredHeight = 42;

            var stack = UIKit.Node("pot-stack", pot.transform);
            _potStack = UIKit.Rect(stack);
            var sv = stack.AddComponent<VerticalLayoutGroup>();
            sv.spacing = 1f; sv.childAlignment = TextAnchor.LowerCenter; sv.reverseArrangement = true;
            sv.childForceExpandWidth = false; sv.childForceExpandHeight = false;
            sv.childControlWidth = false; sv.childControlHeight = false;
            stack.AddComponent<LayoutElement>().preferredHeight = 34;
        }

        void BuildBottom()
        {
            var stack = UIKit.Node("bottom-stack", Root);
            var srt = UIKit.Rect(stack);
            srt.anchorMin = new Vector2(0.5f, 0); srt.anchorMax = new Vector2(0.5f, 0);
            srt.pivot = new Vector2(0.5f, 0);
            srt.anchoredPosition = new Vector2(0, 14);
            srt.sizeDelta = new Vector2(420, 320);
            var sv = stack.AddComponent<VerticalLayoutGroup>();
            sv.spacing = 12f; sv.childAlignment = TextAnchor.LowerCenter;
            sv.childForceExpandWidth = false; sv.childForceExpandHeight = false;
            sv.childControlWidth = false; sv.childControlHeight = false;

            BuildResultCard(stack.transform);
            BuildSpinContainer(stack.transform);
        }

        void BuildResultCard(Transform parent)
        {
            var go = UIKit.Node("result-card", parent);
            _resultCard = UIKit.Rect(go);
            _resultCard.sizeDelta = new Vector2(330, 150);
            var img = go.AddComponent<Image>();
            img.sprite = Theme.Rounded(Theme.RXl);
            img.type = Image.Type.Sliced;
            img.color = Theme.CardGlass;
            img.raycastTarget = false;
            UIKit.Border(go.transform, new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.55f), Theme.RXl);
            _resultGroup = go.AddComponent<CanvasGroup>();
            _resultGroup.alpha = 0f;
            _resultGroup.blocksRaycasts = false;

            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(20, 20, 10, 10);
            v.spacing = 2f; v.childAlignment = TextAnchor.UpperCenter;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            v.childControlWidth = true; v.childControlHeight = true;

            _resChar = UIKit.Label(go.transform, "ג", 56, Theme.Gold, TextAnchor.MiddleCenter, true);
            _resChar.gameObject.AddComponent<LayoutElement>().preferredHeight = 62;
            _resPhon = UIKit.Label(go.transform, "Ready", 20, Theme.Text, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            _resPhon.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            _resText = UIKit.Label(go.transform, "Hold the button to charge your spin", 14, Theme.Sub);
            _resText.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
            _resOutcome = UIKit.Label(go.transform, "", 15, Theme.GoldHot, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            _resOutcome.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;
        }

        void BuildSpinContainer(Transform parent)
        {
            var go = UIKit.Node("spin-container", parent);
            var rt = UIKit.Rect(go);
            rt.sizeDelta = new Vector2(148, 148);

            // the ring: background, sweet-spot marker, then the fill that sweeps with power
            var bg = UIKit.Node("ring-bg", go.transform);
            UIKit.Stretch(bg);
            var bgImg = bg.AddComponent<Image>();
            bgImg.sprite = Theme.Ring(148, 7f);
            bgImg.color = new Color(160 / 255f, 107 / 255f, 26 / 255f, 0.22f);
            bgImg.raycastTarget = false;

            var sweet = UIKit.Node("ring-sweet", go.transform);
            UIKit.Stretch(sweet);
            _ringSweet = sweet.AddComponent<Image>();
            _ringSweet.sprite = Theme.Ring(148, 7f);
            _ringSweet.color = new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.28f);
            _ringSweet.type = Image.Type.Filled;
            _ringSweet.fillMethod = Image.FillMethod.Radial360;
            _ringSweet.fillOrigin = (int)Image.Origin360.Top;
            _ringSweet.fillClockwise = true;
            _ringSweet.fillAmount = 1f - Consts.SweetSpot;   // the last stretch, where PERFECT lives
            _ringSweet.raycastTarget = false;
            var srt = UIKit.Rect(sweet);
            srt.localRotation = Quaternion.Euler(0, 0, (1f - Consts.SweetSpot) * 360f);

            var fill = UIKit.Node("ring-fill", go.transform);
            UIKit.Stretch(fill);
            _ringFill = fill.AddComponent<Image>();
            _ringFill.sprite = Theme.Ring(148, 7f);
            _ringFill.color = Theme.Gold;
            _ringFill.type = Image.Type.Filled;
            _ringFill.fillMethod = Image.FillMethod.Radial360;
            _ringFill.fillOrigin = (int)Image.Origin360.Top;
            _ringFill.fillClockwise = true;
            _ringFill.fillAmount = 0f;
            _ringFill.raycastTarget = false;

            // the tier flash — WEAK / GOOD / STRONG / PERFECT
            var flash = UIKit.Node("tier-flash", go.transform);
            var frt = UIKit.Rect(flash);
            frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 1f);
            frt.pivot = new Vector2(0.5f, 0f);
            frt.anchoredPosition = new Vector2(0, 6);
            frt.sizeDelta = new Vector2(240, 28);
            _tierFlash = UIKit.Label(flash.transform, "", 17, Theme.Gold, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIKit.Stretch(_tierFlash.gameObject);
            _tierGroup = flash.AddComponent<CanvasGroup>();
            _tierGroup.alpha = 0f;

            // the spin coin itself
            var btnGo = UIKit.Node("spin-btn", go.transform);
            var brt = UIKit.Rect(btnGo);
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(14, 14); brt.offsetMax = new Vector2(-14, -14);
            var bimg = btnGo.AddComponent<Image>();
            bimg.sprite = Theme.SpinCoin();
            bimg.color = Color.white;
            _spinLabel = UIKit.Label(btnGo.transform, "HOLD TO\nSPIN", 13, Theme.SpinText,
                                     TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIKit.Stretch(_spinLabel.gameObject, 8f);
            _spinBtn = btnGo.AddComponent<Button>();
            _spinBtn.targetGraphic = bimg;
            _spinBtn.transition = Selectable.Transition.None;

            var hold = btnGo.AddComponent<HoldButton>();
            hold.OnDown = StartCharge;
            hold.OnUp = ReleaseCharge;
        }

        // ---------------------------------------------------------------
        //  charge
        // ---------------------------------------------------------------
        void StartCharge()
        {
            if (GC.IsSpinning || _isCharging || GC.G.Status != GameStatus.Playing) return;
            if (!GC.CustomMode && !GC.MyTurn()) return;

            _isCharging = true; GC.IsCharging = true;
            _chargePower = 0f; _chargeStart = Time.time;
            _spinLabel.text = "CHARGING";
            View.ChargeStart();
            View.SetCam("charge");
            Sfx.StartRumble();
            StartCoroutine(ChargeLoop());
        }

        IEnumerator ChargeLoop()
        {
            float lastTick = 0f;
            while (_isCharging)
            {
                _chargePower = Mathf.Min((Time.time - _chargeStart) / Consts.ChargeTime, 1f);
                SetPowerRing(_chargePower);
                View.ChargeSet(_chargePower);
                UI.ShakeScreen(_chargePower);
                Sfx.SetRumble(_chargePower);

                if (Time.time - lastTick > (0.2f - _chargePower * 0.13f))
                {
                    lastTick = Time.time;
                    Sfx.Play("chargeTick", _chargePower);
                    Sfx.Buzz(Mathf.RoundToInt(8 + _chargePower * 22));
                }
                _spinLabel.text = _chargePower >= Consts.SweetSpot ? "★\nPERFECT"
                                : _chargePower > 0.6f ? "STRONG"
                                : _chargePower > 0.3f ? "GOOD" : "CHARGING";
                yield return null;
            }
        }

        void ReleaseCharge()
        {
            if (!_isCharging) return;
            _isCharging = false; GC.IsCharging = false;
            Sfx.StopRumble();
            UI.ShakeScreen(0f);
            SetPowerRing(0f);
            View.ChargeEnd();
            View.SetCam("default");

            float power = Mathf.Max(_chargePower, 0.15f);
            bool perfect = power >= Consts.SweetSpot;
            FlashTier(perfect ? "★ PERFECT ★" : power > 0.6f ? "STRONG!" : power > 0.3f ? "GOOD" : "WEAK…",
                      perfect ? Theme.GoldHot : power > 0.6f ? Theme.Gold : power > 0.3f ? Theme.Sub : Hex.To("#5a6390"));
            if (perfect) { Sfx.Play("perfect"); Sfx.Buzz(30, 40, 60); }
            _spinLabel.text = "…";
            GC.UserTriggerSpin(power);
        }

        public void SetPowerRing(float p) { if (_ringFill) _ringFill.fillAmount = Mathf.Clamp01(p); }

        void FlashTier(string text, Color color)
        {
            _tierFlash.text = text;
            _tierFlash.color = color;
            if (_tierRoutine != null) StopCoroutine(_tierRoutine);
            _tierRoutine = StartCoroutine(TierFlashRoutine());
        }

        Coroutine _tierRoutine, _revealRoutine;

        IEnumerator TierFlashRoutine()
        {
            var rt = UIKit.Rect(_tierGroup.gameObject);
            float t = 0f;
            while (t < 0.9f)
            {
                t += Time.deltaTime;
                float k = t / 0.9f;
                _tierGroup.alpha = k < 0.15f ? k / 0.15f : Mathf.Clamp01(1f - (k - 0.15f) / 0.85f);
                rt.anchoredPosition = new Vector2(0, 6 + k * 26f);
                rt.localScale = Vector3.one * (0.8f + Mathf.Min(k * 4f, 1f) * 0.2f);
                yield return null;
            }
            _tierGroup.alpha = 0f;
        }

        // ---------------------------------------------------------------
        //  the board
        // ---------------------------------------------------------------
        public void ResetMotion() { _prevCoins.Clear(); _potShown = null; }

        public void SetSpinButton(bool enabled, string label)
        {
            if (_spinBtn) _spinBtn.interactable = enabled;
            if (_spinLabel)
            {
                _spinLabel.text = label;
                _spinLabel.color = enabled ? Theme.SpinText : new Color(Theme.SpinText.r, Theme.SpinText.g, Theme.SpinText.b, 0.55f);
            }
        }

        public void ShowResult(string ch, string phon, string text, string outcome)
        {
            _resChar.text = ch;
            _resPhon.text = phon;
            _resText.text = text;
            _resOutcome.text = outcome;
        }

        public void SetOutcome(string s) { _resOutcome.text = s; }

        public void HideResultCard()
        {
            if (_revealRoutine != null) { StopCoroutine(_revealRoutine); _revealRoutine = null; }
            _resultGroup.alpha = 0f;
            _resOutcome.text = "";
            _resultCard.localScale = Vector3.one * 0.85f;
            _resultCard.anchoredPosition = new Vector2(_resultCard.anchoredPosition.x, -90f);
        }

        public void RevealResultCard()
        {
            if (_revealRoutine != null) StopCoroutine(_revealRoutine);
            _revealRoutine = StartCoroutine(RevealRoutine());
        }

        IEnumerator RevealRoutine()
        {
            // the card springs up on the stylesheet's overshoot curve
            float t = 0f;
            const float dur = 0.55f;
            float y0 = _resultCard.anchoredPosition.y;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                float e = Overshoot(k);
                _resultGroup.alpha = Mathf.Clamp01(k * 2.5f);
                _resultCard.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, e);
                _resultCard.anchoredPosition = new Vector2(_resultCard.anchoredPosition.x, Mathf.Lerp(y0, 0f, e));
                yield return null;
            }
            _resultGroup.alpha = 1f;
            _resultCard.localScale = Vector3.one;
            _resultCard.anchoredPosition = new Vector2(_resultCard.anchoredPosition.x, 0f);
        }

        /// <summary>cubic-bezier(0.34, 1.56, 0.64, 1) — the stylesheet's spring.</summary>
        static float Overshoot(float k)
        {
            const float c = 1.70158f, c3 = c + 1f;
            return 1f + c3 * Mathf.Pow(k - 1f, 3f) + c * Mathf.Pow(k - 1f, 2f);
        }

        public void SetPotDisplay(int v)
        {
            if (_potShown == null || _potShown == v) { _potShown = v; _potVal.text = v.ToString(); return; }
            if (_potTween != null) StopCoroutine(_potTween);
            _potTween = StartCoroutine(PotTween(_potShown.Value, v));
        }

        IEnumerator PotTween(int from, int to)
        {
            float t0 = Time.time;
            const float dur = 0.38f;
            while (true)
            {
                float k = Mathf.Min(1f, (Time.time - t0) / dur);
                float e = 1f - Mathf.Pow(1f - k, 3f);
                _potShown = Mathf.RoundToInt(from + (to - from) * e);
                _potVal.text = _potShown.ToString();
                if (k >= 1f) break;
                yield return null;
            }
        }

        public RectTransform RowFor(int idx)
        {
            RectTransform r;
            return _rows.TryGetValue(idx, out r) ? r : null;
        }

        /// <summary>Rebuild the seats, the pot and the turn state — the web build's updateGameUI.</summary>
        public void Refresh()
        {
            var G = GC.G;
            GC.SaveCpuGame();
            SetPotDisplay(G.Pot);
            View.SetPotCoins(G.Pot);
            float scale = 1f + Mathf.Min(G.Pot * 0.008f, 0.10f);
            PotBox.localScale = Vector3.one * scale;

            BuildPotPips(G.Pot);
            BuildRows(G);

            int anteN = Mathf.Max(1, G.Ante);
            _potLbl.text = anteN > 1 ? "POT · ANTE " + anteN : "THE POT";

            var activeP = G.Current;
            if (activeP != null && G.Status == GameStatus.Playing)
            {
                // The gold leader-crown NEVER replaces a dreidel the player unlocked or bought —
                // any chosen skin stays (the crown on the name marks the leader). Gold only fills
                // in for the DEFAULT wood dreidel.
                string leaderId = G.LeaderId();
                bool mineTurn = !activeP.Cpu && GC.IsLocalGame;
                string theirSkin = Unlocks.ValidSkin(activeP.Skin) ? activeP.Skin : "wood";
                string chosen = mineTurn ? GC.MySkinChoice : theirSkin;
                bool useGold = activeP.Id == leaderId && chosen == "wood";
                View.SetSkin(useGold ? "gold" : chosen);
            }

            // a new person's turn -> fade the previous player's result off screen
            if (G.Status == GameStatus.Playing && G.TurnIndex != GC.LastTurnIndex)
            {
                if (GC.LastTurnIndex != -1) HideResultCard();
                GC.LastTurnIndex = G.TurnIndex;
            }

            if (activeP != null)
            {
                bool mine = GC.MyTurn();
                string R = G.Round > 1 ? "R" + G.Round + " · " : "";
                string label = activeP.Cpu ? activeP.Name + " spinning…"
                             : mine ? activeP.Name + " — Hold to Spin"
                             : activeP.Name + "'s Turn";
                _turnBadge.text = (R + label).ToUpper();
                var badgeImg = _turnBadge.transform.parent.GetComponent<Image>();
                badgeImg.color = mine ? Theme.Gold : new Color(28 / 255f, 36 / 255f, 74 / 255f, 0.85f);
                _turnBadge.color = mine ? Hex.To("#331f04") : Color.white;
                StartCoroutine(BadgePop());

                if (!GC.IsSpinning && !_isCharging)
                {
                    SetSpinButton(mine, mine ? "HOLD TO\nSPIN" : (activeP.Cpu ? "CPU…" : "WAITING"));
                    // stand the dreidel back up for the next spinner
                    if (G.Status == GameStatus.Playing && View.IsLying)
                    {
                        View.StartRecover(0.55f);
                        UI.Fx.DustPuff(Screen.width / 2f, Screen.height * 0.45f, 6);
                    }
                }

                var me = GC.IsLocalGame ? activeP : null;
                bool inDanger = me != null && !me.Eliminated && me.Coins <= 1 && G.Status == GameStatus.Playing;
                if (inDanger) GC.StartDangerBeat(); else GC.StopDangerBeat();
            }
        }

        IEnumerator BadgePop()
        {
            var rt = UIKit.Rect(_turnBadge.transform.parent.gameObject);
            float t = 0f;
            while (t < 0.45f)
            {
                t += Time.deltaTime;
                rt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, Overshoot(Mathf.Clamp01(t / 0.45f)));
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        /// <summary>
        /// Coin pips: rows of 5 that pile upward — 7 gelt is a full row with 2 on top, 20 gelt
        /// is a stack 4 rows high (capped at 6 rows; the number carries the rest).
        /// </summary>
        void BuildPotPips(int pot)
        {
            UIKit.Clear(_potStack);
            int n = Mathf.Min(pot, 30);
            for (int i = 0; i < n; i += 5)
            {
                var row = UIKit.Node("ps-row", _potStack);
                UIKit.Rect(row).sizeDelta = new Vector2(60, 5);
                var h = row.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 2f; h.childAlignment = TextAnchor.MiddleCenter;
                h.childForceExpandWidth = false; h.childForceExpandHeight = false;
                h.childControlWidth = false; h.childControlHeight = false;
                int count = Mathf.Min(5, n - i);
                for (int k = 0; k < count; k++)
                {
                    var pip = UIKit.Node("pip", row.transform);
                    UIKit.Rect(pip).sizeDelta = new Vector2(8, 4);
                    var img = pip.AddComponent<Image>();
                    img.sprite = Theme.Rounded(2f);
                    img.type = Image.Type.Sliced;
                    img.color = Theme.Gold;
                    img.raycastTarget = false;
                }
            }
        }

        void BuildRows(GameState G)
        {
            UIKit.Clear(_playerList);
            _rows.Clear();
            string leaderId = G.LeaderId();

            for (int i = 0; i < G.Players.Count; i++)
            {
                var p = G.Players[i];
                if (p.Forfeited) continue;
                bool isTurn = i == G.TurnIndex && G.Status == GameStatus.Playing;
                bool isDanger = !p.Eliminated && p.Coins <= 1;

                var row = UIKit.Node("p-row", _playerList);
                var rrt = UIKit.Rect(row);
                rrt.sizeDelta = new Vector2(194, 38);
                row.AddComponent<LayoutElement>().preferredHeight = 38;
                var bg = row.AddComponent<Image>();
                bg.sprite = Theme.Rounded(10f);
                bg.type = Image.Type.Sliced;
                bg.color = isTurn ? new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.12f) : new Color(0, 0, 0, 0);
                bg.raycastTarget = false;
                if (isTurn) UIKit.Border(row.transform, Theme.Gold, 10f);

                var h = row.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 8f; h.padding = new RectOffset(6, 8, 0, 0);
                h.childAlignment = TextAnchor.MiddleLeft;
                h.childForceExpandWidth = false; h.childForceExpandHeight = false;
                h.childControlWidth = false; h.childControlHeight = false;

                // avatar: the seat's hue, with the player's initial
                var av = UIKit.Node("avatar", row.transform);
                UIKit.Rect(av).sizeDelta = new Vector2(26, 26);
                av.AddComponent<LayoutElement>().preferredWidth = 26;
                var avImg = av.AddComponent<Image>();
                avImg.sprite = Theme.Circle();
                avImg.color = Consts.HueColor(i);
                avImg.raycastTarget = false;
                var init = UIKit.Label(av.transform, string.IsNullOrEmpty(p.Name) ? "?" : p.Name.Substring(0, 1).ToUpper(),
                                       12, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
                UIKit.Stretch(init.gameObject);

                string crown = p.Id == leaderId ? " <color=#f2c14e>♛</color>" : "";
                var nameT = UIKit.Label(row.transform, p.Name + crown, 15,
                                        p.Eliminated ? new Color(Theme.Text.r, Theme.Text.g, Theme.Text.b, 0.38f) : Theme.Text,
                                        TextAnchor.MiddleLeft, false, FontStyle.Bold);
                UIKit.Rect(nameT.gameObject).sizeDelta = new Vector2(104, 26);
                nameT.gameObject.AddComponent<LayoutElement>().preferredWidth = 104;

                var coinsT = UIKit.Label(row.transform, p.Coins.ToString(), 16,
                                         isDanger ? Theme.Danger : Theme.Gold, TextAnchor.MiddleRight, false, FontStyle.Bold);
                UIKit.Rect(coinsT.gameObject).sizeDelta = new Vector2(38, 26);
                coinsT.gameObject.AddComponent<LayoutElement>().preferredWidth = 38;

                int prev;
                if (_prevCoins.TryGetValue(p.Id, out prev) && prev != p.Coins)
                    StartCoroutine(BumpCoins(coinsT.rectTransform));
                _prevCoins[p.Id] = p.Coins;

                if (p.Eliminated) row.AddComponent<CanvasGroup>().alpha = 0.38f;
                _rows[i] = rrt;

                // CPU table-talk: an inline message row beneath the speaker's name
                string quip;
                float until;
                if (_activeQuips.TryGetValue(i, out quip) && _quipUntil.TryGetValue(i, out until) && Time.time < until)
                {
                    var qrow = UIKit.Node("quip-row", _playerList);
                    UIKit.Rect(qrow).sizeDelta = new Vector2(194, 26);
                    qrow.AddComponent<LayoutElement>().preferredHeight = 26;
                    var qimg = qrow.AddComponent<Image>();
                    qimg.sprite = Theme.Rounded(8f);
                    qimg.type = Image.Type.Sliced;
                    qimg.color = Theme.Surface1;
                    qimg.raycastTarget = false;
                    var qt = UIKit.Label(qrow.transform, quip, 12, Theme.Sub, TextAnchor.MiddleLeft);
                    UIKit.Stretch(qt.gameObject, 8f);
                }
            }
        }

        IEnumerator BumpCoins(RectTransform rt)
        {
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                rt.localScale = Vector3.one * (1f + Mathf.Sin(Mathf.Clamp01(t / 0.35f) * Mathf.PI) * 0.35f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        public void PopDelta(int idx, string text, bool gain)
        {
            var row = RowFor(idx);
            if (row == null) return;
            StartCoroutine(DeltaRoutine(row, text, gain));
        }

        IEnumerator DeltaRoutine(RectTransform row, string text, bool gain)
        {
            var t = UIKit.Label(row, text, 16, gain ? Theme.Ok : Theme.Danger, TextAnchor.MiddleRight, false, FontStyle.Bold);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.sizeDelta = new Vector2(70, 24);
            float e = 0f;
            while (e < 1.15f)
            {
                e += Time.deltaTime;
                float k = e / 1.15f;
                rt.anchoredPosition = new Vector2(-6, k * 28f);
                t.color = new Color(t.color.r, t.color.g, t.color.b, 1f - k);
                yield return null;
            }
            Destroy(t.gameObject);
        }

        public void SetCpuThinking(bool on)
        {
            if (!on) return;
            var p = GC.G.Current;
            if (p != null) _turnBadge.text = (p.Name + " · · ·").ToUpper();
        }

        /// <summary>
        /// A quip expands beneath the speaker, holds 4s, then retracts. Active quips live in a
        /// map so HUD rebuilds re-insert them intact.
        /// </summary>
        public void ShowQuip(int idx, string line)
        {
            _activeQuips[idx] = line;
            _quipUntil[idx] = Time.time + 4f;
            Refresh();
            StartCoroutine(CloseQuip(idx));
        }

        IEnumerator CloseQuip(int idx)
        {
            yield return new WaitForSeconds(4.05f);
            _activeQuips.Remove(idx);
            _quipUntil.Remove(idx);
            if (GC.G.Status == GameStatus.Playing) Refresh();
        }

        public void Show(bool on) { Root.gameObject.SetActive(on); }
    }

    /// <summary>Press-and-hold, the spin button's whole interaction.</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public System.Action OnDown, OnUp;
        bool _held;

        public void OnPointerDown(PointerEventData e)
        {
            var b = GetComponent<Button>();
            if (b != null && !b.interactable) return;
            _held = true;
            if (OnDown != null) OnDown();
        }

        public void OnPointerUp(PointerEventData e) { Release(); }
        public void OnPointerExit(PointerEventData e) { Release(); }

        void Release()
        {
            if (!_held) return;
            _held = false;
            if (OnUp != null) OnUp();
        }

        void Update()
        {
            // the keyboard shortcut: space charges, release spins
            if (Input.GetKeyDown(KeyCode.Space) && !_held)
            {
                var b = GetComponent<Button>();
                if (b == null || b.interactable) { _held = true; if (OnDown != null) OnDown(); }
            }
            else if (Input.GetKeyUp(KeyCode.Space)) Release();
        }
    }
}
