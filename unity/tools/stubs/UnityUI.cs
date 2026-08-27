using System;
using UnityEngine;
using UnityEngine.Events;

namespace UnityEngine.Events
{
    public class UnityEvent { public void AddListener(UnityAction a){} public void RemoveAllListeners(){} }
    public class UnityEvent<T> { public void AddListener(UnityAction<T> a){} public void RemoveAllListeners(){} }
    public delegate void UnityAction();
    public delegate void UnityAction<T>(T arg);
}

namespace UnityEngine.EventSystems
{
    public class EventSystem : UnityEngine.Behaviour {
        public static EventSystem current;
        public bool IsPointerOverGameObject(){return false;}
        public bool IsPointerOverGameObject(int pointerId){return false;}
    }
    public class StandaloneInputModule : UnityEngine.Behaviour {}
    public class PointerEventData { public Vector2 position; public int pointerId; }
    public interface IPointerDownHandler { void OnPointerDown(PointerEventData e); }
    public interface IPointerUpHandler { void OnPointerUp(PointerEventData e); }
    public interface IPointerClickHandler { void OnPointerClick(PointerEventData e); }
    public interface IPointerExitHandler { void OnPointerExit(PointerEventData e); }
    public interface IPointerEnterHandler { void OnPointerEnter(PointerEventData e); }
    public class UIBehaviour : UnityEngine.MonoBehaviour {
        protected virtual void Awake(){} protected virtual void OnEnable(){} protected virtual void OnDisable(){}
        protected virtual void Start(){} protected virtual void OnDestroy(){}
    }
}

namespace UnityEngine.UI
{
    public class VertexHelper {
        public int currentVertCount { get { return 0; } }
        public void Clear(){}
        public void AddVert(Vector2 p, Color c, Vector2 uv){}
        public void AddVert(Vector3 p, Color c, Vector2 uv){}
        public void AddTriangle(int a,int b,int c){}
    }
    public class Graphic : UnityEngine.EventSystems.UIBehaviour {
        public Color color; public bool raycastTarget;
        public Material material;
        public RectTransform rectTransform;
        public CanvasRenderer canvasRenderer;
        public virtual Texture mainTexture { get { return null; } }
        protected static Texture2D s_WhiteTexture;
        public void SetVerticesDirty(){} public void SetMaterialDirty(){} public void SetAllDirty(){}
        protected virtual void OnPopulateMesh(VertexHelper vh){}
    }
    public class MaskableGraphic : Graphic {}
    public class Image : MaskableGraphic {
        public Sprite sprite; public Type type; public FillMethod fillMethod;
        public int fillOrigin; public float fillAmount; public bool fillClockwise;
        public bool preserveAspect;
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
        public enum Origin360 { Bottom, Right, Top, Left }
    }
    public class Text : MaskableGraphic {
        public string text; public Font font; public int fontSize; public FontStyle fontStyle;
        public TextAnchor alignment; public bool supportRichText;
        public HorizontalWrapMode horizontalOverflow; public VerticalWrapMode verticalOverflow;
        public float lineSpacing; public bool resizeTextForBestFit;
    }
    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }
    public class Selectable : UnityEngine.EventSystems.UIBehaviour {
        public bool interactable; public Graphic targetGraphic;
        public ColorBlock colors; public Transition transition;
        public enum Transition { None, ColorTint, SpriteSwap, Animation }
    }
    public struct ColorBlock {
        public Color normalColor, highlightedColor, pressedColor, selectedColor, disabledColor;
        public float colorMultiplier, fadeDuration;
    }
    public class Button : Selectable { public UnityEngine.Events.UnityEvent onClick; }
    public class Toggle : Selectable {
        public bool isOn; public Graphic graphic;
        public UnityEngine.Events.UnityEvent<bool> onValueChanged;
    }
    public class InputField : Selectable {
        public string text; public Text textComponent; public Graphic placeholder;
        public int characterLimit;
        public UnityEngine.Events.UnityEvent<string> onValueChanged, onEndEdit;
    }
    public class Canvas : UnityEngine.Behaviour {
        public RenderMode renderMode; public bool pixelPerfect; public float scaleFactor;
        public Camera worldCamera; public int sortingOrder;
    }
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }
    public class CanvasScaler : UnityEngine.Behaviour {
        public ScaleMode uiScaleMode; public Vector2 referenceResolution;
        public ScreenMatchMode screenMatchMode; public float matchWidthOrHeight;
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
    }
    public class GraphicRaycaster : UnityEngine.Behaviour {}
    public class LayoutGroup : UnityEngine.EventSystems.UIBehaviour {
        public RectOffset padding; public TextAnchor childAlignment;
    }
    public class HorizontalOrVerticalLayoutGroup : LayoutGroup {
        public float spacing;
        public bool childForceExpandWidth, childForceExpandHeight, childControlWidth, childControlHeight;
        public bool reverseArrangement;
    }
    public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup {}
    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup {}
    public class GridLayoutGroup : LayoutGroup {
        public Vector2 cellSize, spacing; public Constraint constraint; public int constraintCount;
        public enum Constraint { Flexible, FixedColumnCount, FixedRowCount }
    }
    public class ContentSizeFitter : UnityEngine.EventSystems.UIBehaviour {
        public FitMode horizontalFit, verticalFit;
        public enum FitMode { Unconstrained, MinSize, PreferredSize }
    }
    public class LayoutElement : UnityEngine.EventSystems.UIBehaviour {
        public float minWidth, minHeight, preferredWidth, preferredHeight, flexibleWidth, flexibleHeight;
    }
    public static class RectTransformUtility {
        public static Vector2 WorldToScreenPoint(Camera c, Vector3 w){return Vector2.zero;}
    }
}
