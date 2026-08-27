// Minimal AR Foundation surface, for type-checking outside the editor.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine
{
    public struct Pose { public Vector3 position; public Quaternion rotation; }
    public struct Touch { public int fingerId; public Vector2 position; public TouchPhase phase; }
    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }
    public enum Space { World, Self }
}

namespace UnityEngine.XR.ARSubsystems
{
    [Flags]
    public enum TrackableType
    {
        None = 0, PlaneWithinPolygon = 8, PlaneWithinBounds = 16,
        PlaneWithinInfinity = 4, PlaneEstimated = 32, Planes = 60, FeaturePoint = 2, All = 62
    }
}

namespace Unity.XR.CoreUtils
{
    /// XROrigin replaced ARSessionOrigin in AR Foundation 5.
    public class XROrigin : UnityEngine.MonoBehaviour
    {
        public enum TrackingOriginMode { NotSpecified, Device, Floor, Unbounded }
        public UnityEngine.Camera Camera { get; set; }
        public UnityEngine.GameObject CameraFloorOffsetObject { get; set; }
        public TrackingOriginMode RequestedTrackingOriginMode { get; set; }
        public float CameraYOffset { get; set; }
    }
}

namespace UnityEngine.InputSystem
{
    public enum InputActionType { Value, Button, PassThrough }
    public class InputAction
    {
        public InputAction(string name = null, InputActionType type = InputActionType.Value,
                           string binding = null, string interactions = null,
                           string processors = null, string expectedControlType = null) { }
        public void Enable() { }
        public void Disable() { }
    }
    public struct InputActionProperty
    {
        public InputActionProperty(InputAction action) { }
    }
}

namespace UnityEngine.InputSystem.XR
{
    public class TrackedPoseDriver : UnityEngine.MonoBehaviour
    {
        public enum TrackingType { RotationAndPosition, RotationOnly, PositionOnly }
        public enum UpdateType { UpdateAndBeforeRender, Update, BeforeRender }
        public TrackingType trackingType { get; set; }
        public UpdateType updateType { get; set; }
        public InputActionProperty positionInput { get; set; }
        public InputActionProperty rotationInput { get; set; }
        public InputActionProperty trackingStateInput { get; set; }
    }
}

namespace UnityEngine.XR.ARFoundation
{
    public enum ARSessionState
    {
        None, Unsupported, CheckingAvailability, NeedsInstall, Installing,
        Ready, SessionInitializing, SessionTracking
    }

    public class ARSession : MonoBehaviour
    {
        public static ARSessionState state;
        public static IEnumerator CheckAvailability() { return null; }
        public static IEnumerator Install() { return null; }
    }

    public class ARInputManager : MonoBehaviour {}

    public class ARSessionOrigin : MonoBehaviour { public Camera camera; }

    public struct ARRaycastHit { public Pose pose; public float distance; }

    public class ARRaycastManager : MonoBehaviour
    {
        public bool Raycast(Vector2 screenPoint, List<ARRaycastHit> hits,
                            UnityEngine.XR.ARSubsystems.TrackableType types) { return false; }
    }

    public class ARPlaneManager : MonoBehaviour { public GameObject planePrefab; }
    public class ARCameraManager : MonoBehaviour {}
    public class ARCameraBackground : MonoBehaviour {}
    public class ARPoseDriver : MonoBehaviour {}
}
