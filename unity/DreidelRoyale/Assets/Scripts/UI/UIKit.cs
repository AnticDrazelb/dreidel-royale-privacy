using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DreidelRoyale.UI
{
    /// <summary>
    /// Builders for the widgets the stylesheet defines — the gold button, its ghost and text
    /// variants, chips, panels, inputs — so screens can be assembled declaratively instead of
    /// hand-wiring RectTransforms.
    /// </summary>
    public static class UIKit
    {
        public static RectTransform Rect(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            return rt != null ? rt : go.AddComponent<RectTransform>();
        }

        public static GameObject Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>A full-bleed child that tracks its parent's rect.</summary>
        public static RectTransform Stretch(GameObject go, float pad = 0f)
        {
            var rt = Rect(go);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad);
            return rt;
        }

        public static Image Panel(Transform parent, Color color, float radius = Theme.RLg, string name = "panel")
        {
            var go = Node(name, parent);
            var img = go.AddComponent<Image>();
            img.sprite = Theme.Rounded(radius);
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        /// <summary>A hairline ring drawn over a panel, matching the stylesheet's borders.</summary>
        public static Image Border(Transform parent, Color color, float radius, float width = 1f)
        {
            var go = Node("border", parent);
            Stretch(go);
            var img = go.AddComponent<Image>();
            img.sprite = Theme.RoundedOutline(radius, width);
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
                                 TextAnchor anchor = TextAnchor.MiddleCenter, bool display = false,
                                 FontStyle style = FontStyle.Normal)
        {
            var go = Node("label", parent);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = display ? Theme.Display : Theme.Body;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        public enum BtnKind { Primary, Ghost, Text, Danger }

        /// <summary>
        /// The stylesheet's button: gold by default, a translucent "ghost" for secondary
        /// actions, a bare text link, and a red destructive variant.
        /// </summary>
        public static Button Btn(Transform parent, string text, BtnKind kind, Action onClick,
                                 float minWidth = 210f, float height = 50f, int fontSize = 20)
        {
            var go = Node("button", parent);
            var rt = Rect(go);
            rt.sizeDelta = new Vector2(minWidth, height);

            var img = go.AddComponent<Image>();
            img.sprite = Theme.Rounded(Theme.RLg);
            img.type = Image.Type.Sliced;

            Color bg, fg;
            switch (kind)
            {
                case BtnKind.Ghost: bg = Theme.Surface2; fg = Theme.Text; break;
                case BtnKind.Text: bg = new Color(0, 0, 0, 0); fg = Theme.Sub; break;
                case BtnKind.Danger: bg = new Color(0, 0, 0, 0); fg = Theme.Danger; break;
                default: bg = Theme.Gold; fg = Theme.ButtonText; break;
            }
            img.color = bg;

            var lbl = Label(go.transform, text, fontSize, fg, TextAnchor.MiddleCenter, false,
                            kind == BtnKind.Primary ? FontStyle.Bold : FontStyle.Normal);
            Stretch(lbl.gameObject, 6f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
            colors.pressedColor = new Color(0.94f, 0.94f, 0.94f, 1f);   // filter: brightness(0.94)
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);   // grayscale + dim
            colors.fadeDuration = 0.12f;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            go.AddComponent<PressScale>();      // :active { transform: scale(0.97) }

            if (kind == BtnKind.Danger)
                Border(go.transform, new Color(Theme.Danger.r, Theme.Danger.g, Theme.Danger.b, 0.5f), Theme.RLg);

            return btn;
        }

        /// <summary>A selectable chip, the shape used for counts, difficulty, rules and antes.</summary>
        public static Button Chip(Transform parent, string text, bool selected, Action onClick,
                                  float width = 54f, float height = 46f, int fontSize = 18)
        {
            var go = Node("chip", parent);
            Rect(go).sizeDelta = new Vector2(width, height);
            var img = go.AddComponent<Image>();
            img.sprite = Theme.Rounded(Theme.RSm);
            img.type = Image.Type.Sliced;
            img.color = selected ? new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.18f) : Theme.Surface2;

            var lbl = Label(go.transform, text, fontSize, selected ? Theme.Gold : Theme.Text,
                            TextAnchor.MiddleCenter, false, FontStyle.Bold);
            Stretch(lbl.gameObject, 4f);

            if (selected)
                Border(go.transform, new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.5f), Theme.RSm, 1.5f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            go.AddComponent<PressScale>();
            return btn;
        }

        public static InputField Input(Transform parent, string placeholder, int maxLength,
                                       float width = 280f, float height = 54f)
        {
            var go = Node("input", parent);
            Rect(go).sizeDelta = new Vector2(width, height);
            var img = go.AddComponent<Image>();
            img.sprite = Theme.Rounded(Theme.RMd);
            img.type = Image.Type.Sliced;
            img.color = Theme.Surface1;
            Border(go.transform, Theme.Hairline, Theme.RMd);

            var textGo = Node("text", go.transform);
            Stretch(textGo, 14f);
            var txt = textGo.AddComponent<Text>();
            txt.font = Theme.Body; txt.fontSize = 20; txt.color = Theme.Text;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.supportRichText = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;

            var phGo = Node("placeholder", go.transform);
            Stretch(phGo, 14f);
            var ph = phGo.AddComponent<Text>();
            ph.font = Theme.Body; ph.fontSize = 20;
            ph.color = new Color(Theme.Sub.r, Theme.Sub.g, Theme.Sub.b, 0.6f);
            ph.alignment = TextAnchor.MiddleCenter;
            ph.text = placeholder;

            var field = go.AddComponent<InputField>();
            field.textComponent = txt;
            field.placeholder = ph;
            field.characterLimit = maxLength;
            field.targetGraphic = img;
            return field;
        }

        /// <summary>A labelled on/off switch, as used in the pause menu.</summary>
        public static Toggle Switch(Transform parent, string label, bool on, Action<bool> onChange,
                                    float width = 300f)
        {
            var row = Node("switchRow", parent);
            Rect(row).sizeDelta = new Vector2(width, 46f);
            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.padding = new RectOffset(6, 6, 0, 0);

            var lbl = Label(row.transform, label, 17, Theme.Text, TextAnchor.MiddleLeft);
            var le = lbl.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;

            var track = Node("track", row.transform);
            Rect(track).sizeDelta = new Vector2(52f, 28f);
            track.AddComponent<LayoutElement>().preferredWidth = 52f;
            var trackImg = track.AddComponent<Image>();
            trackImg.sprite = Theme.Rounded(14f);
            trackImg.type = Image.Type.Sliced;
            trackImg.color = on ? Theme.Gold : Theme.Surface3;

            var knob = Node("knob", track.transform);
            var krt = Rect(knob);
            krt.anchorMin = new Vector2(0, 0.5f); krt.anchorMax = new Vector2(0, 0.5f);
            krt.pivot = new Vector2(0.5f, 0.5f);
            krt.sizeDelta = new Vector2(22f, 22f);
            krt.anchoredPosition = new Vector2(on ? 37f : 15f, 0f);
            var knobImg = knob.AddComponent<Image>();
            knobImg.sprite = Theme.Circle();
            knobImg.color = on ? Theme.Night : Theme.Sub;

            var tog = row.AddComponent<Toggle>();
            tog.targetGraphic = trackImg;
            tog.isOn = on;
            tog.onValueChanged.AddListener(v =>
            {
                trackImg.color = v ? Theme.Gold : Theme.Surface3;
                knobImg.color = v ? Theme.Night : Theme.Sub;
                krt.anchoredPosition = new Vector2(v ? 37f : 15f, 0f);
                if (onChange != null) onChange(v);
            });
            return tog;
        }

        /// <summary>A vertical stack with the stylesheet's default gaps.</summary>
        public static VerticalLayoutGroup Column(Transform parent, float spacing = 8f,
                                                 TextAnchor align = TextAnchor.UpperCenter)
        {
            var v = parent.gameObject.GetComponent<VerticalLayoutGroup>()
                    ?? parent.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = align;
            v.childForceExpandWidth = false;
            v.childForceExpandHeight = false;
            v.childControlWidth = false;
            v.childControlHeight = false;
            return v;
        }

        public static GameObject Row(Transform parent, float spacing = 8f, float height = 46f,
                                     TextAnchor align = TextAnchor.MiddleCenter)
        {
            var go = Node("row", parent);
            Rect(go).sizeDelta = new Vector2(0, height);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.childControlWidth = false;
            h.childControlHeight = false;
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }

        /// <summary>A wrapping grid, for the table and dreidel pickers.</summary>
        public static GameObject Grid(Transform parent, Vector2 cell, float spacing = 8f, float width = 340f)
        {
            var go = Node("grid", parent);
            Rect(go).sizeDelta = new Vector2(width, cell.y);
            var g = go.AddComponent<GridLayoutGroup>();
            g.cellSize = cell;
            g.spacing = new Vector2(spacing, spacing);
            g.childAlignment = TextAnchor.MiddleCenter;
            g.constraint = GridLayoutGroup.Constraint.Flexible;
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }

        public static GameObject Spacer(Transform parent, float h)
        {
            var go = Node("spacer", parent);
            Rect(go).sizeDelta = new Vector2(1, h);
            return go;
        }

        /// <summary>The "small caps, wide tracking" section label the screens use.</summary>
        public static Text SectionLabel(Transform parent, string text)
        {
            var t = Label(parent, text.ToUpper(), 12, Theme.Sub, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            Rect(t.gameObject).sizeDelta = new Vector2(320, 20);
            return t;
        }

        public static void SetSize(Component c, float w, float h)
        {
            Rect(c.gameObject).sizeDelta = new Vector2(w, h);
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// A paged setup screen. The web build splits setup across two pages with step dots rather
    /// than one long scroll, so the first decision is made before the second is even visible -
    /// which is what stops a phone-sized screen turning into a settings form.
    /// </summary>
    public class Pager : MonoBehaviour
    {
        readonly List<GameObject> _pages = new List<GameObject>();
        readonly List<Image> _dots = new List<Image>();
        RectTransform _dotRow;
        int _page;

        public int Page { get { return _page; } }

        public void Init(Transform dotParent)
        {
            var row = UIKit.Node("step-dots", dotParent);
            _dotRow = UIKit.Rect(row);
            _dotRow.sizeDelta = new Vector2(80, 14);
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 7f; h.childAlignment = TextAnchor.MiddleCenter;
            h.childForceExpandWidth = false; h.childControlWidth = false;
            h.childForceExpandHeight = false; h.childControlHeight = false;
        }

        public GameObject AddPage(Transform parent)
        {
            var go = UIKit.Node("page" + _pages.Count, parent);
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8f; v.childAlignment = TextAnchor.UpperCenter;
            v.childForceExpandWidth = false; v.childForceExpandHeight = false;
            v.childControlWidth = false; v.childControlHeight = false;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _pages.Add(go);

            var dot = UIKit.Node("dot", _dotRow);
            UIKit.Rect(dot).sizeDelta = new Vector2(8, 8);
            var img = dot.AddComponent<Image>();
            img.sprite = Theme.Circle();
            img.raycastTarget = false;
            _dots.Add(img);

            Show(0);
            return go;
        }

        public void Show(int index)
        {
            _page = Mathf.Clamp(index, 0, Mathf.Max(0, _pages.Count - 1));
            for (int i = 0; i < _pages.Count; i++) _pages[i].SetActive(i == _page);
            for (int i = 0; i < _dots.Count; i++)
                _dots[i].color = i == _page ? Theme.Gold : Theme.Surface3;
        }

        public void Next() { Show(_page + 1); }
        public void Back() { Show(_page - 1); }
    }

    /// <summary>A steady rotation, for the waiting spinners.</summary>
    public class Spin : MonoBehaviour
    {
        public float DegreesPerSecond = -400f;
        void Update() { transform.Rotate(0f, 0f, DegreesPerSecond * Time.deltaTime); }
    }

    /// <summary>The stylesheet's `:active { transform: scale(0.97) }` on every button.</summary>
    public class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public float Scale = 0.97f;
        Vector3 _base = Vector3.one;
        bool _down;

        void OnEnable() { transform.localScale = _base; _down = false; }

        public void OnPointerDown(PointerEventData e)
        {
            if (_down) return;
            _down = true;
            _base = transform.localScale;
            transform.localScale = _base * Scale;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_down) return;
            _down = false;
            transform.localScale = _base;
        }
    }
}
