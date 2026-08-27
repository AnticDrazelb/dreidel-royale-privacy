using System.Collections.Generic;
using UnityEngine;
using DreidelRoyale.Core;
using DreidelRoyale.Visual;

namespace DreidelRoyale.AR
{
    /// <summary>
    /// The objects that only exist once the diorama is standing on a real table: the brass
    /// gelt board, the shadow catcher that replaces it by default, and the reticle you aim
    /// with. Built lazily, the first time AR is entered.
    /// </summary>
    public class ArProps
    {
        public const float TableRadius = 4.3f;   // world units — comfortably holds candles and gelt

        public Transform Table, ShadowCatcher, Reticle;
        Transform _reticleRing;
        Material _reticleRingMat;

        bool _built;

        public void Build(SceneRig rig, Transform sceneRoot)
        {
            if (_built) return;
            _built = true;

            // ---- the table: a real object you set down, not a floating shadow ----
            Table = new GameObject("arTable").transform;
            Table.SetParent(rig.World, false);

            var top = Mk("top", Table, Geo.Cylinder(TableRadius, TableRadius * 0.97f, 0.34f, 64),
                         rig.GroundMat);                    // shares the table's surface
            top.localPosition = new Vector3(0, -0.17f, 0);   // play surface sits exactly on y=0
            top.GetComponent<MeshRenderer>().shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            // brass rim — reads as a gelt board, and visually catches coins at the edge
            var rim = Mk("rim", Table, Geo.Torus(TableRadius, 0.14f, 10, 72),
                         MatUtil.Pbr(Hex.FromInt(0xa06b1a), 0.9f, 0.28f, Hex.FromInt(0x2a1a04), 0.4f));
            rim.localRotation = Quaternion.Euler(90f, 0, 0);

            var under = Mk("underside", Table,
                           Geo.Cylinder(TableRadius * 0.97f, TableRadius * 0.9f, 0.12f, 48),
                           MatUtil.Pbr(Hex.FromInt(0x120c05), 0.05f, 0.9f));
            under.localPosition = new Vector3(0, -0.39f, 0);

            Table.gameObject.SetActive(false);

            // ---- shadow catcher: the default mode, with the brass board one tap away ----
            var sc = new GameObject("shadowCatcher");
            sc.transform.SetParent(rig.World, false);
            sc.AddComponent<MeshFilter>().sharedMesh = Geo.Circle(TableRadius * 1.15f, 48);
            var scMr = sc.AddComponent<MeshRenderer>();
            var shader = Shader.Find("DreidelRoyale/ShadowCatcher");
            scMr.sharedMaterial = shader != null
                ? new Material(shader)
                : MatUtil.UnlitTex(null, new Color(0, 0, 0, 0.25f));
            scMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            scMr.receiveShadows = true;
            sc.transform.localRotation = Quaternion.Euler(-90f, 0, 0);
            sc.transform.localPosition = new Vector3(0, 0.001f, 0);
            sc.SetActive(false);
            ShadowCatcher = sc.transform;

            // ---- reticle: a spinning gold ring, not the default white donut ----
            // It lives in room space, NOT in the world group — it is what you use to decide
            // where the world goes.
            Reticle = new GameObject("arReticle").transform;
            Reticle.SetParent(sceneRoot, false);

            var ring = Mk("ring", Reticle, Geo.Ring(0.052f, 0.068f, 40, true),
                          MatUtil.Glow(new Color(1f, 0.757f, 0.306f, 0.95f)));
            ring.localRotation = Quaternion.Euler(-90f, 0, 0);
            _reticleRing = ring;
            _reticleRingMat = ring.GetComponent<MeshRenderer>().material;

            var inner = Mk("inner", Reticle, Geo.Ring(0.014f, 0.021f, 24, true),
                           MatUtil.Glow(new Color(1f, 0.914f, 0.659f, 0.8f)));
            inner.localRotation = Quaternion.Euler(-90f, 0, 0);

            var pool = Mk("pool", Reticle, Geo.Circle(0.05f, 32),
                          MatUtil.Glow(Color.white, Fx.RadialTex("rgba(242,193,78,0.5)")));
            pool.localRotation = Quaternion.Euler(-90f, 0, 0);

            foreach (var mr in Reticle.GetComponentsInChildren<MeshRenderer>(true))
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                // the reticle reads through anything it lands behind
                mr.material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;
            }
            Reticle.gameObject.SetActive(false);
        }

        static Transform Mk(string name, Transform parent, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            return go.transform;
        }

        /// <summary>The one flourish, and it is the dreidel's own gesture: a slow spin.</summary>
        public void SpinReticle(float dt, float tGlobal)
        {
            if (Reticle == null || !Reticle.gameObject.activeSelf || _reticleRing == null) return;
            _reticleRing.Rotate(0f, 0f, dt * 0.9f * Mathf.Rad2Deg, Space.Self);
            MatUtil.SetAlpha(_reticleRingMat, 0.75f + Mathf.Sin(tGlobal * 4f) * 0.2f);
        }
    }
}
