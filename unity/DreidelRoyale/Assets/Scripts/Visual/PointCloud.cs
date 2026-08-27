using UnityEngine;
using Stop = DreidelRoyale.Visual.Canvas2D.Stop;

namespace DreidelRoyale.Visual
{
    /// <summary>
    /// A pool of soft, camera-facing motes rebuilt into one dynamic mesh each frame — the
    /// stand-in for three.js Points with size attenuation. Used for the dust the tip kicks
    /// up, the coloured splash a special dreidel throws when it lands, and the embers that
    /// rise in the candles' glow. These live in the scene rather than being pasted on the
    /// lens, so they sit on the table with everything else.
    /// </summary>
    public class PointCloud
    {
        public struct Body { public Vector3 P, V; public float Life, Max; }

        public Body[] Bodies;
        public float Size = 0.24f;
        public float Gravity = 4.5f;
        public float Drag = 1.6f;
        public float FloorY = 0.02f;
        public float Bounce;              // 0 = stop dead at the floor, else restitution

        Mesh _mesh;
        MeshRenderer _mr;
        Transform _t;
        Camera _cam;
        Vector3[] _verts;
        Vector2[] _uvs;
        Color[] _cols;
        int[] _tris;

        public bool Visible { get { return _t != null && _t.gameObject.activeSelf; } }

        public void Build(Transform parent, Camera cam, int count, float size, Color tint, string name)
        {
            _cam = cam; Size = size;
            Bodies = new Body[count];

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            _t = go.transform;

            _mesh = new Mesh { name = name };
            _mesh.MarkDynamic();
            _verts = new Vector3[count * 4];
            _uvs = new Vector2[count * 4];
            _cols = new Color[count * 4];
            _tris = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                int v = i * 4, t = i * 6;
                _uvs[v] = new Vector2(0, 0); _uvs[v + 1] = new Vector2(1, 0);
                _uvs[v + 2] = new Vector2(1, 1); _uvs[v + 3] = new Vector2(0, 1);
                _tris[t] = v; _tris[t + 1] = v + 1; _tris[t + 2] = v + 2;
                _tris[t + 3] = v; _tris[t + 4] = v + 2; _tris[t + 5] = v + 3;
            }
            _mesh.vertices = _verts; _mesh.uv = _uvs; _mesh.colors = _cols; _mesh.triangles = _tris;
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 40f);

            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _mr = go.AddComponent<MeshRenderer>();
            var tex = Tex.Radial(new Stop(0f, Color.white), new Stop(0.45f, new Color(1, 1, 1, 0.55f)),
                                 new Stop(1f, new Color(1, 1, 1, 0f)));
            _mr.sharedMaterial = MatUtil.Glow(tint, tex);
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;
            go.SetActive(false);
        }

        public void SetTint(Color c)
        {
            if (_mr == null) return;
            MatUtil.Tint(_mr.material, c);
        }

        /// <summary>Spawn up to `n` motes from a point, with the burst's own speed profile.</summary>
        public void Burst(Vector3 origin, int n, float spread, float speedBase, float speedRand,
                          float lift, float liftRand, float lifeBase, float lifeRand)
        {
            int spawned = 0;
            for (int i = 0; i < Bodies.Length && spawned < n; i++)
            {
                if (Bodies[i].Life > 0f) continue;
                float a = Random.value * Mathf.PI * 2f;
                float sp = speedBase + Random.value * speedRand;
                Bodies[i].P = origin + new Vector3((Random.value - 0.5f) * spread, 0f, (Random.value - 0.5f) * spread);
                Bodies[i].V = new Vector3(Mathf.Cos(a) * sp, lift + Random.value * liftRand, Mathf.Sin(a) * sp);
                Bodies[i].Life = Bodies[i].Max = lifeBase + Random.value * lifeRand;
                spawned++;
            }
            if (spawned > 0 && _t != null) _t.gameObject.SetActive(true);
        }

        public void Step(float dt)
        {
            if (_t == null || !_t.gameObject.activeSelf) return;
            var cam = _cam ?? Camera.main;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            Vector3 up = cam != null ? cam.transform.up : Vector3.up;

            int alive = 0;
            for (int i = 0; i < Bodies.Length; i++)
            {
                int v = i * 4;
                if (Bodies[i].Life > 0f)
                {
                    Bodies[i].Life -= dt;
                    Bodies[i].V = new Vector3(Bodies[i].V.x, Bodies[i].V.y - Gravity * dt, Bodies[i].V.z);
                    Bodies[i].V *= (1f - Drag * dt);          // air drag
                    Bodies[i].P += Bodies[i].V * dt;
                    if (Bodies[i].P.y < FloorY)
                    {
                        Bodies[i].P = new Vector3(Bodies[i].P.x, FloorY, Bodies[i].P.z);
                        if (Bounce > 0f)
                        {
                            Bodies[i].V = new Vector3(Bodies[i].V.x * 0.6f, Bodies[i].V.y * -Bounce, Bodies[i].V.z * 0.6f);
                        }
                        else Bodies[i].V = new Vector3(Bodies[i].V.x, 0f, Bodies[i].V.z);
                    }
                    alive++;

                    float h = Size * 0.5f;
                    var p = Bodies[i].P;
                    _verts[v] = p - right * h - up * h;
                    _verts[v + 1] = p + right * h - up * h;
                    _verts[v + 2] = p + right * h + up * h;
                    _verts[v + 3] = p - right * h + up * h;
                    float a = Mathf.Clamp01(Bodies[i].Max > 0f ? Bodies[i].Life / Bodies[i].Max : 0f);
                    var col = new Color(1, 1, 1, a);
                    _cols[v] = _cols[v + 1] = _cols[v + 2] = _cols[v + 3] = col;
                }
                else
                {
                    // park dead motes at a degenerate point so they cost nothing to draw
                    _verts[v] = _verts[v + 1] = _verts[v + 2] = _verts[v + 3] = new Vector3(0, -99f, 0);
                }
            }

            _mesh.vertices = _verts;
            _mesh.colors = _cols;
            if (alive == 0) _t.gameObject.SetActive(false);
        }
    }
}
