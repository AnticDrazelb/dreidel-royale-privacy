using System;
using UnityEngine;

namespace DreidelRoyale.AR
{
    /// <summary>
    /// Drives the AR camera's pose from device tracking.
    ///
    /// This is one line of intent and a surprising amount of history. AR Foundation 4 shipped
    /// `ARPoseDriver`, which needed nothing else. AR Foundation 5 deprecated it and 6 removed
    /// it: the camera's pose now comes from the Input System's `TrackedPoseDriver`, bound to
    /// the XR HMD's centre-eye position and rotation.
    ///
    /// It lives alone in this file for one reason: it is the single piece of the AR rig whose
    /// exact API has moved twice in three versions. If a future Unity moves it again, this is
    /// the file to change, and nothing else in the AR code needs to know.
    ///
    /// The binding is built in code rather than from an .inputactions asset, to stay with the
    /// port's rule that nothing ships as an asset.
    /// </summary>
    public static class ArPose
    {
        /// <summary>
        /// Attach a pose driver to the AR camera, or return null if this build has no
        /// Input System. A null return is reported, not thrown: the caller degrades to a
        /// clear "AR can't start here" rather than a crash.
        /// </summary>
        public static Behaviour Attach(GameObject cameraGo)
        {
#if ENABLE_INPUT_SYSTEM
            var driver = cameraGo.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
            if (driver == null)
            {
                driver = cameraGo.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();

                var position = new UnityEngine.InputSystem.InputAction(
                    "AR Camera Position", UnityEngine.InputSystem.InputActionType.Value,
                    "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
                var rotation = new UnityEngine.InputSystem.InputAction(
                    "AR Camera Rotation", UnityEngine.InputSystem.InputActionType.Value,
                    "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
                // trackingState, not isTracked. TrackedPoseDriver reads this action as
                // `(TrackingStates)context.ReadValue<int>()` — a flags int where Position is
                // bit 0 and Rotation is bit 1. A Button would read back 1, which the driver
                // would take to mean "position tracked, rotation not", and the AR camera
                // would refuse to rotate. The driver's own default uses "Integer".
                var tracked = new UnityEngine.InputSystem.InputAction(
                    "AR Camera Tracking State", UnityEngine.InputSystem.InputActionType.Value,
                    "<XRHMD>/trackingState", expectedControlType: "Integer");

                driver.positionInput = new UnityEngine.InputSystem.InputActionProperty(position);
                driver.rotationInput = new UnityEngine.InputSystem.InputActionProperty(rotation);
                driver.trackingStateInput = new UnityEngine.InputSystem.InputActionProperty(tracked);

                driver.trackingType =
                    UnityEngine.InputSystem.XR.TrackedPoseDriver.TrackingType.RotationAndPosition;
                // Sampling again just before render is what keeps the camera from swimming
                // against the video feed - a frame of latency here is very visible in AR.
                driver.updateType =
                    UnityEngine.InputSystem.XR.TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            }
            return driver;
#else
            Debug.LogWarning("[AR] No Input System in this build, so the camera cannot be tracked. "
                             + "Enable it under Project Settings > Player > Active Input Handling.");
            return null;
#endif
        }
    }
}
