using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DreidelRoyale.Visual
{
    /// <summary>
    /// What the oil looks like, driven by what the oil is doing.
    ///
    /// The simulation underneath is real, and this exists so that reads. Every element here
    /// is a function of a measured quantity rather than a timer: foam brightens with the
    /// steepest slope on the surface, because a breaking wave is a steep one; bubbles rise
    /// and are born at a rate set by agitation; droplets are thrown from actual surface
    /// points, only when the surface is actually violent. Nothing plays on a schedule, so
    /// nothing can play when the liquid is still.
    ///
    /// One mesh, rebuilt per frame, drawn additively. Particle systems would be the obvious
    /// reach and the wrong one - these all live inside a spinning vessel a few centimetres
    /// across, and parenting a system there fights the transform every frame.
    /// </summary>
    public class OilDressing
    {
        const int MaxBubbles = 18;
        const int MaxDroplets = 14;

        class Bubble { public Vector3 P; public float R, Rise, Life; }
        class Droplet { public Vector3 P, V; public float Life, R; }

        readonly List<Bubble> _bubbles = new List<Bubble>();
        readonly List<Droplet> _drops = new List<Droplet>();

        Transform _t;
        Mesh _mesh;
        MeshRenderer _mr;
        Vector3[] _verts;
        Vector2[] _uvs;
        Color[] _cols;
        int[] _tris;
        float _spawnCarry;

        public void Build(Transform parent, Material additive)
        {
            var go = new GameObject("oilDressing");
            go.transform.SetParent(parent, false);
            _t = go.transform;

            int quads = MaxBubbles + MaxDroplets;
            _verts = new Vector3[quads * 4];
            _uvs = new Vector2[quads * 4];
            _cols = new Color[quads * 4];
            _tris = new int[quads * 6];
            for (int q = 0; q < quads; q++)
            {
                int v = q * 4, t = q * 6;
                _tris[t] = v; _tris[t + 1] = v + 2; _tris[t + 2] = v + 1;
                _tris[t + 3] = v + 1; _tris[t + 4] = v + 2; _tris[t + 5] = v + 3;
                _uvs[v] = new Vector2(0, 0); _uvs[v + 1] = new Vector2(1, 0);
                _uvs[v + 2] = new Vector2(0, 1); _uvs[v + 3] = new Vector2(1, 1);
            }

            _mesh = new Mesh { name = "OilDressing" };
            _mesh.MarkDynamic();
            _mesh.vertices = _verts;
            _mesh.uv = _uvs;
            _mesh.colors = _cols;
            _mesh.triangles = _tris;
            // Generous and fixed: the contents move every frame and recalculating bounds
            // from a mesh that is mostly degenerate quads is both wasteful and wrong.
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2.4f);

            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _mr = go.AddComponent<MeshRenderer>();
            _mr.sharedMaterial = additive;
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;
            go.SetActive(false);
        }

        public void SetActive(bool on)
        {
            if (_t == null) return;
            _t.gameObject.SetActive(on);
            if (!on) { _bubbles.Clear(); _drops.Clear(); }
        }

        /// <summary>Splash: throw droplets off the surface, at a strength the caller measured.</summary>
        public void Splash(OilFluid oil, float strength)
        {
            if (_t == null || !_t.gameObject.activeSelf || oil == null) return;
            int n = Mathf.Clamp(Mathf.RoundToInt(strength * 9f), 0, MaxDroplets - _drops.Count);
            for (int k = 0; k < n; k++)
            {
                var p = oil.RandomSurfacePoint();
                _drops.Add(new Droplet
                {
                    P = p,
                    // Outward and up: a droplet leaving a sloshing vessel goes over the wall,
                    // not straight into the air.
                    V = (new Vector3(p.x, 0f, p.z).normalized * Random.Range(0.15f, 0.5f)
                         + Vector3.up * Random.Range(0.5f, 1.3f)) * (0.4f + strength),
                    Life = Random.Range(0.35f, 0.7f),
                    R = Random.Range(0.012f, 0.030f)
                });
            }
        }

        public void Step(float dt, OilFluid oil, Camera cam)
        {
            if (_t == null || !_t.gameObject.activeSelf || oil == null || cam == null) return;

            float agitation = oil.Agitation;

            // Bubbles are born at a rate the liquid sets, not a timer. A still surface makes
            // none at all, which is the whole point of driving this from the sim.
            _spawnCarry += agitation * 26f * dt;
            while (_spawnCarry >= 1f && _bubbles.Count < MaxBubbles)
            {
                _spawnCarry -= 1f;
                var p = oil.RandomSurfacePoint();
                _bubbles.Add(new Bubble
                {
                    P = new Vector3(p.x, Random.Range(-0.6f, p.y), p.z),
                    R = Random.Range(0.010f, 0.026f),
                    Rise = Random.Range(0.18f, 0.42f),
                    Life = 1f
                });
            }

            for (int i = _bubbles.Count - 1; i >= 0; i--)
            {
                var b = _bubbles[i];
                b.P.y += b.Rise * dt;
                // It pops at the surface it was rising toward, not at a fixed height, so a
                // tilted or spun-up surface still consumes its own bubbles correctly.
                if (b.P.y >= oil.SurfacePeak().y - 0.01f) b.Life -= dt * 6f;
                if (b.Life <= 0f) _bubbles.RemoveAt(i);
            }

            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                var d = _drops[i];
                d.V += Vector3.down * 9.81f * 0.35f * dt;   // local units, not metres
                d.P += d.V * dt;
                d.Life -= dt;
                if (d.Life <= 0f) _drops.RemoveAt(i);
            }

            WriteQuads(cam, agitation, oil.PeakSlope);
        }

        void WriteQuads(Camera cam, float agitation, float slope)
        {
            // Billboarded in the vessel's local space: the dreidel spins, and a quad built in
            // world space would shear as its parent turns.
            var camRight = _t.InverseTransformDirection(cam.transform.right);
            var camUp = _t.InverseTransformDirection(cam.transform.up);

            // Foam brightens with the steepest wave on the surface. A breaking wave is a
            // steep one, so this tracks the thing that actually produces foam.
            float foam = Mathf.Clamp01(slope * 7f);

            int q = 0;
            foreach (var b in _bubbles)
            {
                var c = new Color(1f, 0.93f, 0.72f, 0.30f + foam * 0.45f) * b.Life;
                Quad(q++, b.P, b.R, camRight, camUp, c);
            }
            foreach (var d in _drops)
            {
                var c = new Color(1f, 0.86f, 0.55f, 0.75f) * Mathf.Clamp01(d.Life * 2.2f);
                Quad(q++, d.P, d.R, camRight, camUp, c);
            }
            // Everything unused collapses to a point, which costs one degenerate triangle
            // rather than a mesh rebuild.
            for (; q < MaxBubbles + MaxDroplets; q++) Quad(q, Vector3.zero, 0f, camRight, camUp, Color.clear);

            _mesh.vertices = _verts;
            _mesh.colors = _cols;
        }

        void Quad(int q, Vector3 centre, float r, Vector3 right, Vector3 up, Color c)
        {
            int v = q * 4;
            var x = right * r;
            var y = up * r;
            _verts[v] = centre - x - y;
            _verts[v + 1] = centre + x - y;
            _verts[v + 2] = centre - x + y;
            _verts[v + 3] = centre + x + y;
            _cols[v] = _cols[v + 1] = _cols[v + 2] = _cols[v + 3] = c;
        }
    }
}
