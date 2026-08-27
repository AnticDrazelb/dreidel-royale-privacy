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
