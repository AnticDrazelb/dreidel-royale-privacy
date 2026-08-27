using UnityEngine;

namespace DreidelRoyale.Visual
{
    /// <summary>
    /// The Oil Miracle's liquid, simulated rather than faked.
    ///
    /// A shallow-water height field over the vessel's floor: each cell carries a surface
    /// height and a vertical velocity, and the wave equation moves energy between neighbours.
    /// Three real forces drive it —
    ///
    ///   * <b>gravity and tilt</b>, which set the equilibrium plane the surface relaxes toward;
    ///   * <b>linear acceleration</b>, measured from the vessel's own motion, so a hard launch
    ///     throws the oil to the back wall and it sloshes forward as the spin settles;
    ///   * <b>rotation</b>, which pushes the surface into the parabola a spinning liquid
    ///     actually forms — climbing the walls, dipping in the middle, deeper the faster it goes.
    ///
    /// Volume is conserved every step, so the oil never quietly gains or loses itself.
    ///
    /// The honest limit: a height field cannot pour. When the dreidel topples the equilibrium
    /// plane is clamped so the surface stays inside the glass, which reads as thick oil
    /// straining toward the ground. Actually emptying it out would need a volume method, and
    /// nothing in the game ever tips the vessel far enough for it to matter.
    /// </summary>
    public class OilFluid
    {
        public const int N = 20;                 // cells per side
        const float Half = 0.52f;                // vessel interior half-width
        const float BaseY = -0.67f;              // vessel floor, in spinner-local space
        const float RestY = -0.01f;              // surface at rest

        // Tuned so a spin reads as thick oil rather than water: waves travel, but slowly, and
        // they die out over about a second.
        const float WaveSpeed = 34f;
        const float Restore = 9f;
        const float Damping = 3.2f;
        const float MaxDeviation = 0.16f;        // how far a wave may climb before it is capped
        const float MaxSlope = 0.28f;            // clamped so the rim can never reach the glass

        /// <summary>
        /// The integrator is explicit, so its step is bounded by the wave speed: too long a
        /// step and the surface does not slosh, it detonates. Rather than slow the waves to
        /// suit the frame rate, the sim takes its own fixed steps and the frame takes as many
        /// as it needs. That also makes the oil behave identically at 30fps and 120fps, which
        /// a frame-rate-coupled version does not.
        /// </summary>
        const float FixedStep = 1f / 120f;
        const int MaxSubSteps = 4;

        /// <summary>
        /// Oil wets glass, so its edge climbs the wall instead of meeting it flat. It is a
        /// couple of millimetres on a real vessel and it is one of the few cues that says
        /// "liquid in a container" rather than "surface in a box".
        /// </summary>
        const float Meniscus = 0.022f;

        /// <summary>Depth at which the oil reads fully saturated, for the thickness shading.</summary>
        const float FullDepth = 0.5f;

        readonly float[,] _h = new float[N + 1, N + 1];
        readonly float[,] _v = new float[N + 1, N + 1];
        readonly float[,] _target = new float[N + 1, N + 1];

        /// <summary>
        /// How strongly each cell feels the wall it is touching — 1 in the corner, falling to
        /// 0 a couple of cells in. Fixed for the life of the vessel, so it is built once.
        /// </summary>
        static readonly float[,] _wet = BuildWetting();

        static float[,] BuildWetting()
        {
            var w = new float[N + 1, N + 1];
            for (int i = 0; i <= N; i++)
                for (int j = 0; j <= N; j++)
                {
                    float dx = Mathf.Min(j, N - j) / (float)N;
                    float dz = Mathf.Min(i, N - i) / (float)N;
                    float d = Mathf.Min(dx, dz);                  // distance to the nearest wall
                    w[i, j] = Mathf.Clamp01(1f - d / 0.14f);
                    w[i, j] *= w[i, j];                           // tight to the glass, not a dome
                }
            return w;
        }

        Mesh _mesh;
        Vector3[] _verts;
        Vector3[] _norms;
        Vector2[] _uvs;
        MeshRenderer _renderer;
        Transform _t;

        // motion history, for deriving the accelerations that drive the sim
        Vector3 _lastWorldPos;
        Vector3 _lastVel;
        bool _primed;

        public Transform Transform { get { return _t; } }
        public MeshRenderer Renderer { get { return _renderer; } }

        // ---------------------------------------------------------------
        public void Build(Transform parent, Material surface, Material sides, Material bottom)
        {
            for (int i = 0; i <= N; i++)
                for (int j = 0; j <= N; j++) { _h[i, j] = RestY; _target[i, j] = RestY; }

            var go = new GameObject("oilFluid");
            go.transform.SetParent(parent, false);
            _t = go.transform;

            BuildMesh();
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterials = new[] { sides, surface, bottom };
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            go.SetActive(false);
        }

        int _skirtStart, _floorStart;

        void BuildMesh()
        {
            int grid = (N + 1) * (N + 1);
            int skirt = 4 * (N + 1) * 2;
            int floor = 4;
            _verts = new Vector3[grid + skirt + floor];
            _norms = new Vector3[_verts.Length];
            _uvs = new Vector2[_verts.Length];
            var uvs = _uvs;

            _skirtStart = grid;
            _floorStart = grid + skirt;

            // ---- surface grid ----
            var surfaceTris = new int[N * N * 6];
            int t = 0;
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                {
                    int a = i * (N + 1) + j, b = a + 1, c = a + (N + 1), d = c + 1;
                    surfaceTris[t++] = a; surfaceTris[t++] = c; surfaceTris[t++] = b;
                    surfaceTris[t++] = b; surfaceTris[t++] = c; surfaceTris[t++] = d;
                }
            // The surface's UV is not a texture coordinate in the usual sense: u carries how
            // deep the oil is under that vertex, and the material's albedo is a one-dimensional
            // ramp. Thin oil over the floor reads pale and the deep middle reads saturated,
            // which is what a real absorbing liquid does and what a single flat colour cannot.
            for (int i = 0; i <= N; i++)
                for (int j = 0; j <= N; j++)
                    uvs[i * (N + 1) + j] = new Vector2(0f, 0.5f);

            // ---- four skirts, surface edge down to the floor ----
            var sideTris = new int[4 * N * 6];
            t = 0;
            for (int e = 0; e < 4; e++)
            {
                int baseV = _skirtStart + e * (N + 1) * 2;
                for (int k = 0; k <= N; k++)
                {
                    uvs[baseV + k * 2] = new Vector2(k / (float)N, 1f);      // at the surface
                    uvs[baseV + k * 2 + 1] = new Vector2(k / (float)N, 0f);  // at the floor
                }
                for (int k = 0; k < N; k++)
                {
                    int a = baseV + k * 2, b = a + 1, c = a + 2, d = a + 3;
                    sideTris[t++] = a; sideTris[t++] = b; sideTris[t++] = c;
                    sideTris[t++] = b; sideTris[t++] = d; sideTris[t++] = c;
                }
            }

            // ---- floor ----
            _verts[_floorStart] = new Vector3(-Half, BaseY, -Half);
            _verts[_floorStart + 1] = new Vector3(Half, BaseY, -Half);
            _verts[_floorStart + 2] = new Vector3(Half, BaseY, Half);
            _verts[_floorStart + 3] = new Vector3(-Half, BaseY, Half);
            for (int k = 0; k < 4; k++) _norms[_floorStart + k] = Vector3.down;
            var floorTris = new[]
            {
                _floorStart, _floorStart + 1, _floorStart + 2,
                _floorStart, _floorStart + 2, _floorStart + 3
            };

            _mesh = new Mesh { name = "OilFluid" };
            _mesh.MarkDynamic();
            _mesh.vertices = _verts;
            _mesh.normals = _norms;
            _mesh.uv = uvs;
            _mesh.subMeshCount = 3;
            _mesh.SetTriangles(sideTris, 0);
            _mesh.SetTriangles(surfaceTris, 1);
            _mesh.SetTriangles(floorTris, 2);
            _mesh.bounds = new Bounds(new Vector3(0, (BaseY + RestY) * 0.5f, 0),
                                      new Vector3(Half * 2.2f, 1.2f, Half * 2.2f));
            WriteVertices();
        }

        public void SetActive(bool on)
        {
            if (_t == null) return;
            _t.gameObject.SetActive(on);
            if (on && !_primed) Reset();
        }

        public bool Active { get { return _t != null && _t.gameObject.activeSelf; } }

        public void Reset()
        {
            for (int i = 0; i <= N; i++)
                for (int j = 0; j <= N; j++) { _h[i, j] = RestY; _v[i, j] = 0f; }
            _primed = false;
        }

        // ---------------------------------------------------------------
        /// <summary>Step the fluid from the vessel's real motion this frame.</summary>
        public void Step(float dt, Transform vessel, float spinRadiansPerSec)
        {
            if (_t == null || !_t.gameObject.activeSelf || dt <= 0f) return;
            // The sub-step budget covers a 30fps frame exactly (4 x 1/120). Beyond that the
            // sim deliberately runs a little slow rather than skipping ahead, which is the
            // safer failure: oil that lags for one frame is invisible, oil that teleports
            // is not.
            dt = Mathf.Min(dt, 1f / 30f);

            // ---- what the fluid actually feels ----
            var worldPos = vessel.position;
            Vector3 accelWorld = Vector3.zero;
            if (_primed)
            {
                var vel = (worldPos - _lastWorldPos) / dt;
                accelWorld = (vel - _lastVel) / dt;
                _lastVel = vel;
            }
            else
            {
                _lastVel = Vector3.zero;
                _primed = true;
            }
            _lastWorldPos = worldPos;

            // Effective gravity is gravity minus the vessel's acceleration: the same reason a
            // drink leans back when the tray is pushed forward.
            var gWorld = Vector3.down * 9.81f - Vector3.ClampMagnitude(accelWorld, 40f);
            var gLocal = Quaternion.Inverse(vessel.rotation) * gWorld;

            // The equilibrium surface sits perpendicular to that. With gLocal.y toward zero the
            // slope runs away, so it is clamped - which is also what keeps the oil in the glass
            // when the dreidel is lying on its side.
            float gy = Mathf.Max(Mathf.Abs(gLocal.y), 1.5f);
            float slopeX = Mathf.Clamp(-gLocal.x / gy, -MaxSlope, MaxSlope);
            float slopeZ = Mathf.Clamp(-gLocal.z / gy, -MaxSlope, MaxSlope);

            // A spinning liquid climbs its walls and dips in the middle: h = w^2 r^2 / 2g. Real,
            // and the single most convincing thing the vessel does while it whirls.
            float w = Mathf.Abs(spinRadiansPerSec);
            float parabola = Mathf.Min(w * w / (2f * 9.81f), 0.9f);

            float meanTarget = 0f;
            for (int i = 0; i <= N; i++)
                for (int j = 0; j <= N; j++)
                {
                    float x = (j / (float)N - 0.5f) * 2f * Half;
                    float z = (i / (float)N - 0.5f) * 2f * Half;
                    float r2 = x * x + z * z;
                    float tgt = RestY + slopeX * x + slopeZ * z
                              + parabola * (r2 - Half * Half * 0.5f) * 0.12f
                              + Meniscus * _wet[i, j];
                    _target[i, j] = tgt;
                    meanTarget += tgt;
                }
            meanTarget /= (N + 1) * (N + 1);

            // ---- shallow water, in fixed sub-steps ----
            _carry += dt;
            int steps = Mathf.Min(MaxSubSteps, Mathf.FloorToInt(_carry / FixedStep));
            _carry -= steps * FixedStep;
            if (steps >= MaxSubSteps) _carry = 0f;      // a stall must not build up a debt

            for (int step = 0; step < steps; step++) Integrate(FixedStep, meanTarget);
            if (steps > 0) WriteVertices();
        }

        float _carry;

        void Integrate(float h_, float meanTarget)
        {
            for (int i = 0; i <= N; i++)
                for (int j = 0; j <= N; j++)
                {
                    float c = _h[i, j];
                    float lap = Sample(i - 1, j) + Sample(i + 1, j)
                              + Sample(i, j - 1) + Sample(i, j + 1) - 4f * c;
                    float a = lap * WaveSpeed - (c - _target[i, j]) * Restore;
                    _v[i, j] = (_v[i, j] + a * h_) * Mathf.Exp(-Damping * h_);
                }

            float mean = 0f;
            for (int i = 0; i <= N; i++)
                for (int j = 0; j <= N; j++)
                {
                    _h[i, j] += _v[i, j] * h_;
                    mean += _h[i, j];
                }
            mean /= (N + 1) * (N + 1);

            // Conserve volume: whatever the forcing did to the average, put it back. Without
            // this the oil slowly drains or overflows over a long game.
            float correction = meanTarget - mean;
            for (int i = 0; i <= N; i++)
                for (int j = 0; j <= N; j++)
                {
                    float h = _h[i, j] + correction;
                    // never let a wave poke through the glass, or dig below the floor
                    _h[i, j] = Mathf.Clamp(h, BaseY + 0.03f, RestY + MaxDeviation);
                }
        }

        float Sample(int i, int j)
        {
            // A closed vessel: the walls reflect, which is what makes the slosh ring back.
            i = Mathf.Clamp(i, 0, N);
            j = Mathf.Clamp(j, 0, N);
            return _h[i, j];
        }

        /// <summary>Splash the surface where something struck it — the landing slam.</summary>
        public void Disturb(float strength)
        {
            if (_t == null || !_t.gameObject.activeSelf) return;
            for (int i = 0; i <= N; i++)
                for (int j = 0; j <= N; j++)
                {
                    float x = j / (float)N - 0.5f, z = i / (float)N - 0.5f;
                    float r = Mathf.Sqrt(x * x + z * z);
                    _v[i, j] += Mathf.Cos(Mathf.Min(r * 6f, Mathf.PI * 0.5f)) * strength;
                }
        }

        // ---------------------------------------------------------------
        void WriteVertices()
        {
            // surface
            for (int i = 0; i <= N; i++)
                for (int j = 0; j <= N; j++)
                {
                    float x = (j / (float)N - 0.5f) * 2f * Half;
                    float z = (i / (float)N - 0.5f) * 2f * Half;
                    _verts[i * (N + 1) + j] = new Vector3(x, _h[i, j], z);
                }

            // surface normals from the height gradient, so the light rolls with the waves
            for (int i = 0; i <= N; i++)
                for (int j = 0; j <= N; j++)
                {
                    float dhx = Sample(i, j + 1) - Sample(i, j - 1);
                    float dhz = Sample(i + 1, j) - Sample(i - 1, j);
                    float step = 2f * Half / N * 2f;
                    _norms[i * (N + 1) + j] = new Vector3(-dhx, step, -dhz).normalized;

                    // How far the light has to travel through the oil to get back out. A
                    // trough is thin and pale; the bulk is deep and rich.
                    float depth = (_h[i, j] - BaseY) / FullDepth;
                    _uvs[i * (N + 1) + j] = new Vector2(Mathf.Clamp01(depth), 0.5f);
                }

            // skirts: each edge follows the surface down to the floor
            for (int e = 0; e < 4; e++)
            {
                int baseV = _skirtStart + e * (N + 1) * 2;
                var outward = e == 0 ? Vector3.back : e == 1 ? Vector3.right
                            : e == 2 ? Vector3.forward : Vector3.left;
                for (int k = 0; k <= N; k++)
                {
                    int gi, gj;
                    switch (e)
                    {
                        case 0: gi = 0; gj = k; break;          // -z edge
                        case 1: gi = k; gj = N; break;          // +x edge
                        case 2: gi = N; gj = N - k; break;      // +z edge
                        default: gi = N - k; gj = 0; break;     // -x edge
                    }
                    float x = (gj / (float)N - 0.5f) * 2f * Half;
                    float z = (gi / (float)N - 0.5f) * 2f * Half;
                    _verts[baseV + k * 2] = new Vector3(x, _h[gi, gj], z);
                    _verts[baseV + k * 2 + 1] = new Vector3(x, BaseY, z);
                    _norms[baseV + k * 2] = outward;
                    _norms[baseV + k * 2 + 1] = outward;
                }
            }

            _mesh.vertices = _verts;
            _mesh.normals = _norms;
            _mesh.uv = _uvs;                 // the depth channel moves with the waves
        }

        /// <summary>Where the glint should ride: the highest point of the surface.</summary>
        public Vector3 SurfacePeak()
        {
            float best = float.MinValue;
            int bi = 0, bj = 0;
            for (int i = 0; i <= N; i++)
                for (int j = 0; j <= N; j++)
                    if (_h[i, j] > best) { best = _h[i, j]; bi = i; bj = j; }
            return new Vector3((bj / (float)N - 0.5f) * 2f * Half, best,
                               (bi / (float)N - 0.5f) * 2f * Half);
        }
    }
}
