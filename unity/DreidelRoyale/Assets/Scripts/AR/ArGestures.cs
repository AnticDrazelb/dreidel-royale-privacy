using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using DreidelRoyale.Audio;

namespace DreidelRoyale.AR
{
    /// <summary>
    /// Gestures on the transparent catcher behind the HUD: tap to set the board down, drag to
    /// turn it, pinch to resize. Touches that land on a HUD control are ignored, so a tap on
    /// the spin button is never also a placement.
    /// </summary>
    public class ArGestures : MonoBehaviour
    {
        public ArController Ar;

        readonly Dictionary<int, Vector2> _points = new Dictionary<int, Vector2>();
        string _mode;
        float _lastX, _pinchD0, _scale0, _moved;

        const float TapSlop = 12f;      // pixels of travel before a tap becomes a drag

        void Update()
        {
            if (Ar == null || !Ar.IsOn) { _points.Clear(); _mode = null; return; }

            if (Input.touchCount > 0) HandleTouches();
            else HandleMouse();
        }

        void HandleTouches()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                switch (t.phase)
                {
                    case TouchPhase.Began: Down(t.fingerId, t.position); break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary: Move(t.fingerId, t.position); break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled: Up(t.fingerId); break;
                }
            }
        }

        void HandleMouse()
        {
            const int id = -1;
            if (Input.GetMouseButtonDown(0)) Down(id, Input.mousePosition);
            else if (Input.GetMouseButton(0)) Move(id, Input.mousePosition);
            else if (Input.GetMouseButtonUp(0)) Up(id);
        }

        void Down(int id, Vector2 pos)
        {
            // A tap on the HUD must not also count as a placement. Filtering here means every
            // AR interaction below is a plain pointer event - one input path, no races.
            if (OverUI(id, pos)) return;

            _points[id] = pos;
            if (_points.Count == 1) { _mode = "tap"; _lastX = pos.x; _moved = 0f; }
            else if (_points.Count == 2) { _mode = "pinch"; _pinchD0 = PinchDistance(); _scale0 = Ar.Scale; }
        }

        void Move(int id, Vector2 pos)
        {
            Vector2 prev;
            if (!_points.TryGetValue(id, out prev)) return;
            _points[id] = pos;

            if (_mode == "pinch" && _points.Count == 2)
            {
                float d = PinchDistance();
                if (_pinchD0 > 8f) Ar.ApplyScale(_scale0 * d / _pinchD0);
                return;
            }

            _moved += Mathf.Abs(pos.x - prev.x) + Mathf.Abs(pos.y - prev.y);
            if (_moved > TapSlop && _mode == "tap") _mode = Ar.IsPlaced ? "turn" : "tap";
            if (_mode == "turn")
            {
                Ar.Yaw((pos.x - _lastX) * 0.011f);   // a screen-width drag is about a half turn
                _lastX = pos.x;
            }
        }

        void Up(int id)
        {
            if (_mode == "tap" && _moved < TapSlop && _points.ContainsKey(id))
            {
                if (Ar.Place()) Sfx.Play("go");
            }
            _points.Remove(id);
            if (_points.Count == 0) _mode = null;
            else if (_points.Count == 1)
            {
                _mode = "turn";
                foreach (var p in _points.Values) { _lastX = p.x; break; }
            }
        }

        float PinchDistance()
        {
            Vector2 a = Vector2.zero, b = Vector2.zero;
            int i = 0;
            foreach (var p in _points.Values)
            {
                if (i == 0) a = p; else if (i == 1) b = p;
                i++;
            }
            return Vector2.Distance(a, b);
        }

        static bool OverUI(int id, Vector2 pos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (id >= 0 && es.IsPointerOverGameObject(id)) return true;
            return es.IsPointerOverGameObject();
        }
    }
}
