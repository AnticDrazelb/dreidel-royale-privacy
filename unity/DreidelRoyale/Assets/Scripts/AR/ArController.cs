using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;
using DreidelRoyale.Core;
using DreidelRoyale.Visual;

namespace DreidelRoyale.AR
{
    /// <summary>
    /// Puts the whole diorama on a real table.
    ///
    /// The game world is built at "dreidel = 1.6 units" scale. AR keeps every object,
    /// animation and game rule exactly as-is and simply shrinks the `world` group onto a
    /// surface the phone has found. Four things do NOT survive a naive scale-down and are
    /// corrected here: the shadow distance, point-light falloff, the near plane, and the
    /// particle sizes — everything measured in world units rather than local ones.
    /// </summary>
    public class ArController : MonoBehaviour
    {
        public const float ArMin = 0.022f;   // about an 18cm board
        public const float ArMax = 0.20f;    // about a 1.6m board
        const float DefaultScale = 0.055f;

        public DreidelView View;
        public Camera Cam;

        /// <summary>(on, placed) — the UI mirrors this.</summary>
        public Action<bool, bool> OnChange;

        public bool IsOn { get; private set; }
        public bool IsPlaced { get; private set; }
        public bool HasSurface { get { return _hitValid; } }
        public float Scale { get { return _scale; } }
        public string TableMode { get { return _tableMode; } }

        ARSession _session;
        XROrigin _origin;
        ARRaycastManager _raycasts;
        ARPlaneManager _planes;
        ARCameraManager _camManager;
        ARCameraBackground _camBackground;
        Behaviour _poseDriver;

        readonly ArProps _props = new ArProps();
        readonly List<ARRaycastHit> _hits = new List<ARRaycastHit>();

        float _scale = DefaultScale;
        bool _hitValid;
        Vector3 _hitPos;
        Quaternion _hitRot = Quaternion.identity;
        string _tableMode = "shadow";
        float _tGlobal;

        // saved so exiting AR puts the flat-screen game back exactly as it was
        bool _savedFog;
        float _savedNear, _savedShadowDistance;
        Transform _savedCamParent;
        readonly List<KeyValuePair<Light, float>> _savedRanges = new List<KeyValuePair<Light, float>>();

        SceneRig Rig { get { return View.Rig; } }

        // ---------------------------------------------------------------
        //  availability
        // ---------------------------------------------------------------
        public bool Available { get; private set; }
        public string WhyNot { get; private set; }

        /// <summary>
        /// Ask the platform whether AR can run at all, and — when it can't — say the true
        /// thing about why, and what to do about it.
        /// </summary>
        public IEnumerator CheckAvailability()
        {
            EnsureRig();

            if ((ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability))
                yield return ARSession.CheckAvailability();

            switch (ARSession.state)
            {
                case ARSessionState.Unsupported:
                    Available = false;
                    WhyNot = Application.isMobilePlatform
                        ? "This phone doesn't support AR. It needs ARCore (Android) or ARKit (iOS)."
                        : "AR needs a phone with a camera and motion tracking. On a desktop there's nothing to point at the table.";
                    break;
                case ARSessionState.NeedsInstall:
                    Available = false;
                    WhyNot = "AR needs Google Play Services for AR — install it and the button turns on.";
                    // Offer the install rather than just reporting it.
                    yield return ARSession.Install();
                    if (ARSession.state == ARSessionState.Ready || ARSession.state == ARSessionState.Installing)
                    {
                        Available = true; WhyNot = null;
                    }
                    break;
                case ARSessionState.Ready:
                case ARSessionState.SessionInitializing:
                case ARSessionState.SessionTracking:
                    Available = true; WhyNot = null;
                    break;
                default:
                    Available = false;
                    WhyNot = "AR isn't available on this device right now.";
                    break;
            }
        }

        // ---------------------------------------------------------------
        //  rig
        // ---------------------------------------------------------------
        void EnsureRig()
        {
            if (_session != null) return;

            var sessionGo = new GameObject("ARSession");
            sessionGo.transform.SetParent(transform, false);
            _session = sessionGo.AddComponent<ARSession>();
            _session.enabled = false;

            // XROrigin replaced ARSessionOrigin in AR Foundation 5. Its shape is fixed and
            // not optional: origin -> floor-offset object -> camera. The offset object is
            // what the tracking origin mode moves, so the camera must be under it rather
            // than under the origin directly, or a device-origin session puts the camera at
            // the floor instead of at eye height.
            var originGo = new GameObject("XROrigin");
            originGo.transform.SetParent(transform, false);
            _origin = originGo.AddComponent<XROrigin>();

            _camOffset = new GameObject("CameraOffset");
            _camOffset.transform.SetParent(originGo.transform, false);
            _origin.CameraFloorOffsetObject = _camOffset;
            _origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;

            _raycasts = originGo.AddComponent<ARRaycastManager>();
            _planes = originGo.AddComponent<ARPlaneManager>();
            _planes.planePrefab = null;          // detection without the tell-tale grid overlay
            _planes.enabled = false;
            _origin.gameObject.SetActive(false);
        }

        GameObject _camOffset;

        void AttachCamera()
        {
            // One camera, not two: the game's camera BECOMES the AR camera, so billboards,
            // projection and every reference to it keep working untouched.
            _savedCamParent = Cam.transform.parent;
            Cam.transform.SetParent(_camOffset.transform, false);
            Cam.transform.localPosition = Vector3.zero;
            Cam.transform.localRotation = Quaternion.identity;
            _origin.Camera = Cam;

            if (_camManager == null) _camManager = Cam.gameObject.AddComponent<ARCameraManager>();
            if (_camBackground == null) _camBackground = Cam.gameObject.AddComponent<ARCameraBackground>();
            if (_poseDriver == null) _poseDriver = ArPose.Attach(Cam.gameObject);
            _camManager.enabled = true;
            _camBackground.enabled = true;
            if (_poseDriver != null) _poseDriver.enabled = true;
        }

        void DetachCamera()
        {
            if (_camManager != null) _camManager.enabled = false;
            if (_camBackground != null) _camBackground.enabled = false;
            if (_poseDriver != null) _poseDriver.enabled = false;
            Cam.transform.SetParent(_savedCamParent, false);
        }

        // ---------------------------------------------------------------
        //  enter / exit
        // ---------------------------------------------------------------
        public bool Enter()
        {
            if (IsOn) return false;
            if (!Available) return false;

            EnsureRig();
            _props.Build(Rig, transform);

            // remember what the flat-screen game needs back
            _savedFog = RenderSettings.fog;
            _savedNear = Cam.nearClipPlane;
            _savedShadowDistance = QualitySettings.shadowDistance;
            _savedRanges.Clear();
            foreach (var l in Rig.World.GetComponentsInChildren<Light>(true))
                if (l.type == LightType.Point) _savedRanges.Add(new KeyValuePair<Light, float>(l, l.range));

            RenderSettings.fog = false;      // fog.near is 10 units — meaningless at 1/18th scale
            Cam.nearClipPlane = 0.02f;       // let players put their nose on the dreidel

            _origin.gameObject.SetActive(true);
            _session.enabled = true;
            _planes.enabled = true;
            AttachCamera();

            IsOn = true;
            IsPlaced = false;
            _hitValid = false;
            ApplyVisibility();
            ApplyScale(_scale);
            Rig.World.gameObject.SetActive(false);   // hidden until the player picks a surface

            if (OnChange != null) OnChange(true, false);
            return true;
        }

        public void Exit()
        {
            if (!IsOn) return;
            IsOn = false;
            IsPlaced = false;
            _hitValid = false;

            DetachCamera();
            _planes.enabled = false;
            _session.enabled = false;
            _origin.gameObject.SetActive(false);
            if (_props.Reticle != null) _props.Reticle.gameObject.SetActive(false);

            // put every world-unit measurement back
            foreach (var kv in _savedRanges) if (kv.Key != null) kv.Key.range = kv.Value;
            _savedRanges.Clear();
            RenderSettings.fog = _savedFog;
            Cam.nearClipPlane = _savedNear;
            QualitySettings.shadowDistance = _savedShadowDistance;
            View.SetParticleScale(1f);

            Rig.World.localPosition = Vector3.zero;
            Rig.World.localRotation = Quaternion.identity;
            Rig.World.localScale = Vector3.one;
            Rig.World.gameObject.SetActive(true);
            ApplyVisibility();

            if (OnChange != null) OnChange(false, false);
        }

        // ---------------------------------------------------------------
        //  placement
        // ---------------------------------------------------------------
        public bool Place()
        {
            if (!IsOn || !_hitValid) return false;

            Rig.World.position = _hitPos;
            // Face the player: the diorama's "front" is +Z, which is where the default
            // camera sits, so the board turns to meet whoever set it down.
            var d = Cam.transform.position - _hitPos;
            if (d.x * d.x + d.z * d.z > 1e-6f)
                Rig.World.rotation = Quaternion.Euler(0f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);

            Rig.World.gameObject.SetActive(true);
            IsPlaced = true;
            if (_props.Reticle != null) _props.Reticle.gameObject.SetActive(false);
            if (OnChange != null) OnChange(true, true);
            return true;
        }

        public void Unplace()
        {
            if (!IsOn) return;
            IsPlaced = false;
            Rig.World.gameObject.SetActive(false);
            if (OnChange != null) OnChange(true, false);
        }

        /// <summary>A screen-width drag is about a half turn.</summary>
        public void Yaw(float deltaRadians)
        {
            if (!IsPlaced) return;
            Rig.World.Rotate(0f, deltaRadians * Mathf.Rad2Deg, 0f, Space.World);
        }

        public float ApplyScale(float s)
        {
            _scale = Mathf.Clamp(s, ArMin, ArMax);
            Rig.World.localScale = Vector3.one * _scale;

            // Unity's shadow distance is a world-space camera range; at 1/18th scale the
            // default pushes the cascade far past anything that casts.
            QualitySettings.shadowDistance = Mathf.Max(1f, 40f * _scale);

            // Legacy falloff is a world-space radius, and Unity does NOT scale a light's range
            // with its transform — shrink the world without shrinking `range` and every candle
            // turns into a floodlight.
            foreach (var kv in _savedRanges) if (kv.Key != null) kv.Key.range = kv.Value * _scale;

            // Particles are sized in world units too, and would otherwise render as ghost
            // blobs over the candles and 24cm dust clouds.
            View.SetParticleScale(_scale);
            return _scale;
        }

        public string SetTableMode(string mode)
        {
            _tableMode = mode == "board" ? "board" : "shadow";
            if (IsOn) ApplyVisibility();
            return _tableMode;
        }

        /// <summary>The room replaces the backdrop: no sky, no stars, no 40-unit floor.</summary>
        void ApplyVisibility()
        {
            if (Rig.Ground) Rig.Ground.gameObject.SetActive(!IsOn);
            if (Rig.SkyDome) Rig.SkyDome.gameObject.SetActive(!IsOn);
            if (Rig.StarField) Rig.StarField.gameObject.SetActive(!IsOn && _starsWanted);
            if (Rig.FloorGlow) Rig.FloorGlow.gameObject.SetActive(!IsOn && _floorGlowWanted);
            if (_props.Table) _props.Table.gameObject.SetActive(IsOn && _tableMode == "board");
            if (_props.ShadowCatcher) _props.ShadowCatcher.gameObject.SetActive(IsOn && _tableMode == "shadow");
        }

        bool _starsWanted = true, _floorGlowWanted = true;

        /// <summary>
        /// A table swap must not resurrect the sky inside someone's kitchen, so the env's own
        /// wishes are recorded and only honoured while AR is off.
        /// </summary>
        public void NoteEnv(EnvDef env)
        {
            _starsWanted = env.Stars;
            _floorGlowWanted = !env.Room;
            if (IsOn) ApplyVisibility();
        }

        // ---------------------------------------------------------------
        void Update()
        {
            if (!IsOn) return;
            _tGlobal += Time.deltaTime;

            if (!IsPlaced)
            {
                // hit-test straight down the middle of the screen: you aim with the phone
                var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                if (_raycasts.Raycast(centre, _hits, TrackableType.PlaneWithinPolygon))
                {
                    _hitValid = true;
                    _hitPos = _hits[0].pose.position;
                    _hitRot = _hits[0].pose.rotation;
                    if (_props.Reticle != null)
                    {
                        _props.Reticle.gameObject.SetActive(true);
                        _props.Reticle.SetPositionAndRotation(_hitPos, _hitRot);
                    }
                }
                else
                {
                    _hitValid = false;
                    if (_props.Reticle != null) _props.Reticle.gameObject.SetActive(false);
                }
            }

            _props.SpinReticle(Time.deltaTime, _tGlobal);
        }
    }
}
