using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DreidelRoyale.Audio;
using DreidelRoyale.Core;
using DreidelRoyale.Net;

namespace DreidelRoyale.UI
{
    /// <summary>
    /// Table talk. A slide-up sheet with the scrollback and an input, plus a row of quick
    /// phrases — because typing mid-turn on a phone is a chore, and most of what anyone wants
    /// to say at a dreidel table is one of eight things.
    ///
    /// When the sheet is closed, arriving lines still flash briefly over the HUD, so you can
    /// follow the table without opening anything.
    /// </summary>
    public class ChatPanel : MonoBehaviour
    {
        public UIManager UI;
        public NetManager Net;

        RectTransform _sheet, _feed, _toastStack;
        InputField _input;
        Button _openBtn;
        Text _badge;
        ScrollRect _scroll;
        int _unread;

        public bool IsOpen { get { return _sheet != null && _sheet.gameObject.activeSelf; } }

        // ---------------------------------------------------------------
        public void Build(RectTransform root)
        {
            BuildToastStack(root);
            BuildSheet(root);
            BuildOpenButton(root);
            Net.Chat.OnLine += OnLine;
            Show(false);
        }

        void OnDestroy() { if (Net != null) Net.Chat.OnLine -= OnLine; }

        /// <summary>Chat only exists at a networked table; single-player has the bots instead.</summary>
        public void SetAvailable(bool on)
        {
            if (_openBtn != null) _openBtn.gameObject.SetActive(on);
            if (!on) { Close(); ClearToasts(); }
        }

        void BuildOpenButton(RectTransform root)
        {
            _openBtn = UIKit.Btn(root, "Chat", UIKit.BtnKind.Ghost, Toggle, 74f, 40f, 13);
            var rt = UIKit.Rect(_openBtn.gameObject);
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-14, -68);

            var badgeGo = UIKit.Node("badge", _openBtn.transform);
            var brt = UIKit.Rect(badgeGo);
            brt.anchorMin = brt.anchorMax = new Vector2(1, 1);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(-2, -2);
            brt.sizeDelta = new Vector2(18, 18);
            var bimg = badgeGo.AddComponent<Image>();
            bimg.sprite = Theme.Circle();
            bimg.color = Theme.Danger;
            bimg.raycastTarget = false;
            _badge = UIKit.Label(badgeGo.transform, "", 10, Color.white,
                                 TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIKit.Stretch(_badge.gameObject);
            badgeGo.SetActive(false);
            _openBtn.gameObject.SetActive(false);
        }

        void BuildSheet(RectTransform root)
        {
            var go = UIKit.Node("chat-sheet", root);
            _sheet = UIKit.Rect(go);
            _sheet.anchorMin = new Vector2(0.5f, 0);
            _sheet.anchorMax = new Vector2(0.5f, 0);
            _sheet.pivot = new Vector2(0.5f, 0);
            _sheet.anchoredPosition = new Vector2(0, 0);
            _sheet.sizeDelta = new Vector2(400, 380);

            var bg = go.AddComponent<Image>();
            bg.sprite = Theme.Rounded(Theme.RXl);
            bg.type = Image.Type.Sliced;
            bg.color = Theme.Card;
            UIKit.Border(go.transform, new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.3f), Theme.RXl);

            // ---- header ----
            var head = UIKit.Node("head", go.transform);
            var hrt = UIKit.Rect(head);
            hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.offsetMin = new Vector2(14, 0); hrt.offsetMax = new Vector2(-14, 0);
            hrt.anchoredPosition = new Vector2(0, -10);
            hrt.sizeDelta = new Vector2(hrt.sizeDelta.x, 30);
            var title = UIKit.Label(head.transform, "TABLE TALK", 12, Theme.Sub,
                                    TextAnchor.MiddleLeft, false, FontStyle.Bold);
            UIKit.Stretch(title.gameObject);
            var close = UIKit.Btn(head.transform, "Close", UIKit.BtnKind.Text, Close, 62f, 28f, 12);
            var crt = UIKit.Rect(close.gameObject);
            crt.anchorMin = crt.anchorMax = new Vector2(1, 0.5f);
            crt.pivot = new Vector2(1, 0.5f);
            crt.anchoredPosition = Vector2.zero;

            // ---- scrollback ----
            var viewport = UIKit.Node("viewport", go.transform);
            var vrt = UIKit.Rect(viewport);
            vrt.anchorMin = new Vector2(0, 0); vrt.anchorMax = new Vector2(1, 1);
            vrt.offsetMin = new Vector2(12, 96); vrt.offsetMax = new Vector2(-12, -44);
            var vimg = viewport.AddComponent<Image>();
            vimg.sprite = Theme.Rounded(Theme.RSm);
            vimg.type = Image.Type.Sliced;
            vimg.color = Theme.Surface1;
            viewport.AddComponent<Mask>().showMaskGraphic = true;

            var content = UIKit.Node("content", viewport.transform);
            _feed = UIKit.Rect(content);
            _feed.anchorMin = new Vector2(0, 1); _feed.anchorMax = new Vector2(1, 1);
            _feed.pivot = new Vector2(0.5f, 1f);
            var cv = content.AddComponent<VerticalLayoutGroup>();
            cv.spacing = 3f; cv.padding = new RectOffset(8, 8, 8, 8);
            cv.childAlignment = TextAnchor.UpperLeft;
            cv.childForceExpandWidth = true; cv.childControlWidth = true;
            cv.childForceExpandHeight = false; cv.childControlHeight = true;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll = go.AddComponent<ScrollRect>();
            _scroll.content = _feed;
            _scroll.viewport = vrt;
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Elastic;
            _scroll.scrollSensitivity = 24f;

            // ---- quick phrases ----
            var quick = UIKit.Node("quick", go.transform);
            var qrt = UIKit.Rect(quick);
            qrt.anchorMin = new Vector2(0, 0); qrt.anchorMax = new Vector2(1, 0);
            qrt.pivot = new Vector2(0.5f, 0);
            qrt.offsetMin = new Vector2(10, 52); qrt.offsetMax = new Vector2(-10, 52);
            qrt.sizeDelta = new Vector2(qrt.sizeDelta.x, 38);
            var qg = quick.AddComponent<GridLayoutGroup>();
            qg.cellSize = new Vector2(88, 30);
            qg.spacing = new Vector2(5, 4);
            qg.childAlignment = TextAnchor.MiddleCenter;
            foreach (var phrase in ChatSystem.QuickPhrases)
            {
                var p = phrase;
                UIKit.Chip(quick.transform, p, false, () => Say(p), 88f, 30f, 11);
            }

            // ---- input ----
            var row = UIKit.Node("input-row", go.transform);
            var irt = UIKit.Rect(row);
            irt.anchorMin = new Vector2(0, 0); irt.anchorMax = new Vector2(1, 0);
            irt.pivot = new Vector2(0.5f, 0);
            irt.offsetMin = new Vector2(12, 10); irt.offsetMax = new Vector2(-12, 10);
            irt.sizeDelta = new Vector2(irt.sizeDelta.x, 40);
            var ih = row.AddComponent<HorizontalLayoutGroup>();
            ih.spacing = 6f; ih.childAlignment = TextAnchor.MiddleCenter;
            ih.childForceExpandWidth = false; ih.childControlWidth = false;
            ih.childForceExpandHeight = false; ih.childControlHeight = false;

            _input = UIKit.Input(row.transform, "Say something…", ChatSystem.MaxLength, 288f, 40f);
            _input.textComponent.alignment = TextAnchor.MiddleLeft;
            if (_input.placeholder is Text) ((Text)_input.placeholder).alignment = TextAnchor.MiddleLeft;
            _input.onEndEdit.AddListener(_ =>
            {
                // On a phone the on-screen keyboard's Done is the natural send.
                if (Input.GetKeyDown(KeyCode.Return) || Application.isMobilePlatform) SendTyped();
            });
            UIKit.Btn(row.transform, "Send", UIKit.BtnKind.Primary, SendTyped, 74f, 40f, 13);

            go.SetActive(false);
        }

        /// <summary>Where lines flash when the sheet is shut.</summary>
        void BuildToastStack(RectTransform root)
        {
            var go = UIKit.Node("chat-flash", root);
            _toastStack = UIKit.Rect(go);
            _toastStack.anchorMin = new Vector2(0, 0);
            _toastStack.anchorMax = new Vector2(0, 0);
            _toastStack.pivot = new Vector2(0, 0);
            _toastStack.anchoredPosition = new Vector2(12, 210);
            _toastStack.sizeDelta = new Vector2(240, 10);
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = 3f;
            v.childAlignment = TextAnchor.LowerLeft;
            v.childForceExpandWidth = false; v.childControlWidth = false;
            v.childForceExpandHeight = false; v.childControlHeight = false;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // ---------------------------------------------------------------
        void Toggle() { Show(!IsOpen); }
        void Close() { Show(false); }

        void Show(bool on)
        {
            if (_sheet == null) return;
            _sheet.gameObject.SetActive(on);
            if (!on) return;
            Sfx.Play("tick");
            _unread = 0;
            if (_badge != null) _badge.transform.parent.gameObject.SetActive(false);
            ClearToasts();
            Rebuild();
        }

        void SendTyped()
        {
            var text = _input.text;
            _input.text = "";
            if (string.IsNullOrEmpty(text.Trim())) return;
            Say(text);
            _input.ActivateInputField();
        }

        void Say(string text)
        {
            if (Net == null || !Net.Active) return;
            Sfx.Play("tick");
            Net.SayChat(text);
        }

        // ---------------------------------------------------------------
        void OnLine(ChatLine line)
        {
            if (IsOpen) { AppendRow(line); ScrollToEnd(); return; }
            Flash(line);
            _unread++;
            if (_badge != null)
            {
                _badge.transform.parent.gameObject.SetActive(true);
                _badge.text = _unread > 9 ? "9+" : _unread.ToString();
            }
        }

        void Rebuild()
        {
            UIKit.Clear(_feed);
            foreach (var l in Net.Chat.Lines) AppendRow(l);
            ScrollToEnd();
        }

        void ScrollToEnd()
        {
            if (_scroll != null) StartCoroutine(ScrollNextFrame());
        }

        IEnumerator ScrollNextFrame()
        {
            yield return null;              // let the layout settle before jumping to the end
            _scroll.verticalNormalizedPosition = 0f;
        }

        void AppendRow(ChatLine line)
        {
            var go = UIKit.Node("line", _feed);
            var t = go.AddComponent<Text>();
            t.font = Theme.Body;
            t.fontSize = 13;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;

            if (line.IsNotice)
            {
                t.color = new Color(Theme.Sub.r, Theme.Sub.g, Theme.Sub.b, 0.8f);
                t.fontStyle = FontStyle.Italic;
                t.text = line.Text;
            }
            else
            {
                t.color = Theme.Text;
                var hue = line.Seat >= 0 ? Consts.HueColor(line.Seat) : Theme.Sub;
                t.text = "<color=#" + ColorUtility.ToHtmlStringRGB(hue) + "><b>"
                       + line.Name + "</b></color>  " + line.Text;
            }
            go.AddComponent<LayoutElement>().minHeight = 18;
        }

        // ---- flashes over the HUD when the sheet is shut ----
        readonly List<GameObject> _toasts = new List<GameObject>();

        void Flash(ChatLine line)
        {
            var go = UIKit.Node("flash", _toastStack);
            UIKit.Rect(go).sizeDelta = new Vector2(240, 26);
            var img = go.AddComponent<Image>();
            img.sprite = Theme.Rounded(9f);
            img.type = Image.Type.Sliced;
            img.color = new Color(Theme.Night.r, Theme.Night.g, Theme.Night.b, 0.82f);
            img.raycastTarget = false;

            var hue = line.Seat >= 0 ? Consts.HueColor(line.Seat) : Theme.Sub;
            var body = line.IsNotice
                ? "<i>" + line.Text + "</i>"
                : "<color=#" + ColorUtility.ToHtmlStringRGB(hue) + "><b>" + line.Name + "</b></color> " + line.Text;
            var t = UIKit.Label(go.transform, body, 11, Theme.Text, TextAnchor.MiddleLeft);
            UIKit.Stretch(t.gameObject, 8f);

            _toasts.Add(go);
            if (_toasts.Count > 4) { Destroy(_toasts[0]); _toasts.RemoveAt(0); }
            StartCoroutine(FadeFlash(go));
        }

        IEnumerator FadeFlash(GameObject go)
        {
            var cg = go.AddComponent<CanvasGroup>();
            yield return new WaitForSeconds(4.5f);
            float t = 0f;
            while (t < 0.4f && go != null)
            {
                t += Time.deltaTime;
                cg.alpha = 1f - t / 0.4f;
                yield return null;
            }
            _toasts.Remove(go);
            if (go != null) Destroy(go);
        }

        void ClearToasts()
        {
            foreach (var g in _toasts) if (g != null) Destroy(g);
            _toasts.Clear();
        }
    }
}
