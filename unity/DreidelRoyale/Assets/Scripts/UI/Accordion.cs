using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DreidelRoyale.Audio;

namespace DreidelRoyale.UI
{
    /// <summary>
    /// The stylesheet's `.setup-acc`: a titled panel that collapses to a single line.
    ///
    /// The screens are centred vertical stacks with no scroll view, so a lobby that lists
    /// players, offers six tables, three rule styles and five ante steps simply runs off a
    /// phone — and the Start button runs off with it. Collapsing the stakes behind their own
    /// summary ("Rising Stakes · ante 1") is what keeps the whole lobby on one screen, which is why
    /// the web build put it there.
    ///
    /// One panel open at a time per screen, matching the web build: opening one closes its
    /// siblings rather than pushing the layout twice as far.
    /// </summary>
    public class Accordion : MonoBehaviour
    {
        public Transform Body { get; private set; }

        Text _summary, _chev;
        GameObject _bodyGo;
        RectTransform _chevRt;
        Coroutine _spin;
        bool _open;

        public bool IsOpen { get { return _open; } }

        internal void Wire(Transform body, GameObject bodyGo, Text summary, Text chev)
        {
            Body = body; _bodyGo = bodyGo; _summary = summary; _chev = chev;
            _chevRt = chev != null ? chev.rectTransform : null;
            _bodyGo.SetActive(false);
        }

        /// <summary>The one-line readout on the head, shown whether open or closed.</summary>
        public void SetSummary(string text)
        {
            if (_summary != null) _summary.text = text ?? "";
        }

        public void Toggle()
        {
            Sfx.Play("tick"); Sfx.Buzz(10);
            SetOpen(!_open);
        }

        public void SetOpen(bool open, bool animate = true)
        {
            if (open == _open && _bodyGo.activeSelf == open) return;
            _open = open;
            _bodyGo.SetActive(open);

            if (open) CloseSiblings();

            if (_chevRt == null) return;
            float target = open ? 180f : 0f;
            if (_spin != null) StopCoroutine(_spin);
            if (!animate || !gameObject.activeInHierarchy)
            {
                _chevRt.localRotation = Quaternion.Euler(0, 0, target);
                return;
            }
            _spin = StartCoroutine(SpinChevron(target));
        }

        /// <summary>`transition: transform .25s` — unscaled, so a hit-stop cannot stall it.</summary>
        IEnumerator SpinChevron(float target)
        {
            float from = _chevRt.localEulerAngles.z;
            if (from > 180.5f) from -= 360f;
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.25f;
                _chevRt.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(from, target, k * k * (3f - 2f * k)));
                yield return null;
            }
            _chevRt.localRotation = Quaternion.Euler(0, 0, target);
            _spin = null;
        }

        void CloseSiblings()
        {
            var scope = ScreenRoot();
            if (scope == null) return;
            foreach (var a in scope.GetComponentsInChildren<Accordion>(true))
                if (a != this) a.SetOpen(false);
        }

        /// <summary>
        /// The screen this panel belongs to, so "one at a time" cannot reach across screens.
        /// Falls back to the immediate parent when the panel is used outside a built screen.
        /// </summary>
        Transform ScreenRoot()
        {
            for (var t = transform.parent; t != null; t = t.parent)
                if (t.name.StartsWith("screen-")) return t;
            return transform.parent;
        }
    }
}
