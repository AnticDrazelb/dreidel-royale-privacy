using System.Collections.Generic;
using UnityEngine;
using DreidelRoyale.Core;
using Stop = DreidelRoyale.Visual.Canvas2D.Stop;

namespace DreidelRoyale.Visual
{
    /// <summary>One candle: its flame billboard, its point light and its halo.</summary>
    public class Flame
    {
        public Transform Group, Plane, Halo;
        public Light L;
        public Billboard Bill;
        public float Seed, Lean, Gust, BaseX, BaseY;
        public Vector3 LeanAxis;
        public Transform Drip, Pool;
    }

    /// <summary>
    /// The table and everything on it: lights, ground, sky, candles, the pot's gelt, and
    /// the flourishes that ride the dreidel (aura, halo, spin rings, burst).
    /// </summary>
    public class SceneRig
    {
        public Transform World;            // everything diegetic lives here
        public Camera Cam;
        public Light AmbFill, KeyLight, RimLight, GlowLight, TopFill, BurstLight, NerLight;
        public Transform Ground, SkyDome, FloorGlow, Aura, ChargeRing, GoldHalo, BurstSprite;
        public Transform EnvProps, StarField;
        public Transform NtFlame, NtFlameMesh, NtHalo;

        public readonly List<Flame> Flames = new List<Flame>();
        public readonly List<Transform> CandleGroups = new List<Transform>();
        public readonly List<Transform> PotCoins = new List<Transform>();
        public readonly List<Transform> LooseCoins = new List<Transform>();
        public readonly List<Transform> CoinGlows = new List<Transform>();
        public readonly List<Transform> FlightPool = new List<Transform>();

        public Material GroundMat, SkyMat;
        public Material GeltMat;
        public Mesh GeltMesh;

        public Color EnvRim = Hex.FromInt(0x4f7cff);

        /// <summary>Pot coins modelled on the table before the number carries it.</summary>
        public const int GELT_MAX = 32;

        public readonly List<SpinRing> SpinRings = new List<SpinRing>();

        public class SpinRing
        {
            public Transform T;
            public Material M;
            public float R, Tube, Tilt, BaseOp, Wob, WobAmt, Spin;
        }

        public void Build(Transform parent, Camera cam)
        {
            Cam = cam;
            World = new GameObject("world").transform;
            World.SetParent(parent, false);

            BuildLights();
            BuildSky();
            BuildGround();
            BuildGelt();
            BuildCandles();
            BuildAura();
            BuildHeroFlourishes();

            EnvProps = new GameObject("envProps").transform;
            EnvProps.SetParent(World, false);
        }

        void BuildLights()
        {
            // Unity has no ambient light object; the scene's ambient colour plays that role,
            // and a soft downward fill keeps the underside of the dreidel from going black.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Hex.FromInt(0x2e3c6e);

            KeyLight = MkLight("key", LightType.Directional, Hex.FromInt(0xfff0d2), 1.0f);
            KeyLight.transform.localPosition = new Vector3(3, 6, 4);
            KeyLight.transform.rotation = Quaternion.LookRotation(-KeyLight.transform.localPosition.normalized);
            KeyLight.shadows = LightShadows.Soft;
            KeyLight.shadowStrength = 0.75f;
            // The whole scene is about four units across. Unity's default 150-unit shadow
            // distance spends the entire cascade on empty space, which is why an untuned
            // mobile build gets a blocky, crawling shadow under the dreidel; pulling the far
            // plane in to the table itself is the same texels over a fortieth of the area.
            KeyLight.shadowBias = 0.008f;
            KeyLight.shadowNormalBias = 0.25f;
            KeyLight.shadowNearPlane = 0.15f;
            QualitySettings.shadowDistance = 22f;
            QualitySettings.shadowProjection = ShadowProjection.CloseFit;

            RimLight = MkLight("rim", LightType.Directional, Hex.FromInt(0x4f7cff), 0.7f);
            RimLight.transform.localPosition = new Vector3(-4, 3, -5);
            RimLight.transform.rotation = Quaternion.LookRotation(-RimLight.transform.localPosition.normalized);
            RimLight.shadows = LightShadows.None;

            GlowLight = MkLight("glow", LightType.Point, Hex.FromInt(0xffb45e), 0.55f);
            GlowLight.transform.localPosition = new Vector3(0, 1.2f, 2.6f);
            GlowLight.range = 12f;

            TopFill = MkLight("topFill", LightType.Directional, Hex.FromInt(0xbfd4ff), 0.35f);
            TopFill.transform.localPosition = new Vector3(0.5f, 8, 1);
            TopFill.transform.rotation = Quaternion.LookRotation(-TopFill.transform.localPosition.normalized);
            TopFill.shadows = LightShadows.None;
        }

        Light MkLight(string name, LightType type, Color c, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(World, false);
            var l = go.AddComponent<Light>();
            l.type = type; l.color = c; l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        void BuildSky()
        {
            // atmospheric depth behind the table; the texture is set by SetEnv
            var go = new GameObject("skyDome");
            go.transform.SetParent(World, false);
            go.AddComponent<MeshFilter>().sharedMesh = Geo.InvertedSphere(40f, 24, 16);
            var mr = go.AddComponent<MeshRenderer>();
            SkyMat = new Material(MatUtil.Unlit);
            mr.sharedMaterial = SkyMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            SkyDome = go.transform;
        }

        void BuildGround()
        {
            var go = new GameObject("ground");
            go.transform.SetParent(World, false);
            go.AddComponent<MeshFilter>().sharedMesh = Geo.Circle(20f, 48);
            var mr = go.AddComponent<MeshRenderer>();
            GroundMat = MatUtil.Pbr(Color.white, 0.15f, 0.55f);
            mr.sharedMaterial = GroundMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
            go.transform.localRotation = Quaternion.Euler(-90f, 0, 0);
            Ground = go.transform;

            // polished-floor reflection glow under the dreidel, tinted by skin
            var fg = new GameObject("floorGlow");
            fg.transform.SetParent(World, false);
            fg.AddComponent<MeshFilter>().sharedMesh = Geo.Circle(1.7f, 32);
            var fmr = fg.AddComponent<MeshRenderer>();
            fmr.sharedMaterial = MatUtil.Glow(new Color(1, 1, 1, 0.5f), Fx.RadialTex("rgba(242,193,78,0.55)"));
            fmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fmr.receiveShadows = false;
            fg.transform.localRotation = Quaternion.Euler(-90f, 0, 0);
            fg.transform.localPosition = new Vector3(0, 0.015f, 0);
            FloorGlow = fg.transform;
        }

        /// <summary>
        /// Pot gelt: four stacks eight high around the dreidel, height = the actual pot.
        /// Two stacks capped the visible pot at 14 while the rules routinely push it past 20.
        /// </summary>
        void BuildGelt()
        {
            GeltMat = MatUtil.Pbr(Hex.FromInt(0xe8b23c), 0.8f, 0.3f, Hex.FromInt(0x3a2a08), 1f);
            GeltMesh = Geo.Cylinder(0.32f, 0.32f, 0.09f, 20);

            var stackSpots = new[]
            {
                new Vector2(-1.12f, 0.55f), new Vector2(1.12f, 0.45f),
                new Vector2(-1.05f, -0.78f), new Vector2(1.05f, -0.88f)
            };

            for (int i = 0; i < GELT_MAX; i++)
            {
                var s = stackSpots[i % 4];
                int level = i / 4;
                var t = MkCoin("potCoin" + i);
                t.localPosition = new Vector3(s.x + (level % 2 == 1 ? 0.04f : -0.03f),
                                              0.045f + level * 0.091f,
                                              s.y + (level % 2 == 1 ? -0.03f : 0.04f));
                t.localRotation = Quaternion.Euler(0, Random.value * 180f, 0);
                t.gameObject.SetActive(false);
                t.gameObject.AddComponent<CoinHome>().Set(t);
                PotCoins.Add(t);
            }

            // a few loose coins by the stacks, always visible
            var loose = new[]
            {
                new Vector3(-0.85f, 0.95f, -0.2f), new Vector3(0.88f, 0.9f, 0.4f),
                new Vector3(-1.5f, -1.6f, 0.7f), new Vector3(1.55f, -1.5f, -0.9f)
            };
            foreach (var l in loose)
            {
                var t = MkCoin("looseCoin");
                t.localPosition = new Vector3(l.x, 0.045f, l.y);
                t.localRotation = Quaternion.Euler(0, l.z * Mathf.Rad2Deg, 0);
                t.gameObject.AddComponent<CoinHome>().Set(t);
                LooseCoins.Add(t);
            }

            // flight pool: pot->player transfers as real coins
            for (int i = 0; i < 10; i++)
            {
                var t = MkCoin("flightCoin");
                t.gameObject.SetActive(false);
                FlightPool.Add(t);
            }

            // soft glow over each pot stack (fake bloom; opacity tracks the pot)
            foreach (var s in stackSpots)
            {
                var g = Fx.GlowSprite(World, "rgba(255,210,120,0.9)", 1.3f, 0f);
                g.localPosition = new Vector3(s.x, 0.35f, s.y);
                CoinGlows.Add(g);
            }
        }

        Transform MkCoin(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(World, false);
            go.AddComponent<MeshFilter>().sharedMesh = GeltMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GeltMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            return go.transform;
        }

        void BuildCandles()
        {
            var waxMat = MatUtil.Pbr(Hex.FromInt(0xf3ead2), 0f, 0.6f);
            var flameTex = Tex.Flame();

            foreach (var pos in new[] { new Vector2(-1.75f, -3.4f), new Vector2(1.75f, -3.4f) })
            {
                var grp = new GameObject("candle").transform;
                grp.SetParent(World, false);
                grp.localPosition = new Vector3(pos.x, 0, pos.y);

                var wax = MkMesh("wax", grp, Geo.Cylinder(0.16f, 0.19f, 1.5f, 16), waxMat);
                wax.localPosition = new Vector3(0, 0.75f, 0);

                // wax buildup: a drip collar at the lip and a pool at the base, both grown
                // per spin — a long game leaves melted evidence
                var drip = MkMesh("drip", grp, Geo.Sphere(0.11f, 10, 8), waxMat);
                drip.localScale = new Vector3(1, 0.55f, 1);
                drip.localPosition = new Vector3(0.1f, 1.42f, 0.05f);
                drip.gameObject.SetActive(false);

                var pool = MkMesh("pool", grp, Geo.Cylinder(0.24f, 0.27f, 0.05f, 14), waxMat);
                pool.localPosition = new Vector3(0, 0.025f, 0);
                pool.gameObject.SetActive(false);

                var flameGo = new GameObject("flame");
                flameGo.transform.SetParent(grp, false);
                flameGo.AddComponent<MeshFilter>().sharedMesh = Geo.Quad(0.5f, 0.75f);
                var fmr = flameGo.AddComponent<MeshRenderer>();
                fmr.sharedMaterial = MatUtil.Glow(Color.white, flameTex);
                fmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                fmr.receiveShadows = false;
                flameGo.transform.localPosition = new Vector3(0, 1.85f, 0);
                var bill = flameGo.AddComponent<Billboard>();
                bill.Cam = Cam;

                var lGo = new GameObject("candleLight");
                lGo.transform.SetParent(grp, false);
                lGo.transform.localPosition = new Vector3(0, 1.9f, 0);
                var fl = lGo.AddComponent<Light>();
                fl.type = LightType.Point; fl.color = Hex.FromInt(0xffa64d);
                fl.intensity = 0.5f; fl.range = 9f; fl.shadows = LightShadows.None;

                var halo = Fx.GlowSprite(grp, "rgba(255,170,70,0.8)", 1.6f, 0.7f);
                halo.localPosition = new Vector3(0, 1.9f, 0);

                CandleGroups.Add(grp);

                // Lean axis: the horizontal axis to turn the flame about so its tip tips
                // toward the dreidel at the local origin. Precomputed once — the candles are
                // fixed in the diorama's local frame, which is where the lean is applied.
                var toDreidel = new Vector3(-pos.x, 0, -pos.y).normalized;
                var leanAxis = Vector3.Cross(Vector3.up, toDreidel).normalized;

                Flames.Add(new Flame
                {
                    Group = grp, Plane = flameGo.transform, Halo = halo, L = fl, Bill = bill,
                    Seed = Random.value * 10f, LeanAxis = leanAxis,
                    BaseX = flameGo.transform.localPosition.x, BaseY = flameGo.transform.localPosition.y,
                    Drip = drip, Pool = pool
                });
            }
        }

        Transform MkMesh(string name, Transform parent, Mesh mesh, Material mat)
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

        void BuildAura()
        {
            var go = new GameObject("aura");
            go.transform.SetParent(World, false);
            go.AddComponent<MeshFilter>().sharedMesh = Geo.Circle(2.4f, 40);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MatUtil.Glow(new Color(1, 1, 1, 0f), Tex.Aura());
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.transform.localRotation = Quaternion.Euler(-90f, 0, 0);
            go.transform.localPosition = new Vector3(0, 0.02f, 0);
            Aura = go.transform;

            // Pressure ring — a shock of energy that pushes outward across the surface as the
            // wind-up peaks. Emits repeated expanding rings while charge is high.
            var cr = new GameObject("chargeRing");
            cr.transform.SetParent(World, false);
            cr.AddComponent<MeshFilter>().sharedMesh = Geo.Ring(0.86f, 1.0f, 48, true);
            var cmr = cr.AddComponent<MeshRenderer>();
            cmr.sharedMaterial = MatUtil.Glow(new Color(1, 0.851f, 0.541f, 0f));
            cmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cmr.receiveShadows = false;
            cr.transform.localRotation = Quaternion.Euler(-90f, 0, 0);
            cr.transform.localPosition = new Vector3(0, 0.025f, 0);
            cr.SetActive(false);
            ChargeRing = cr.transform;
        }

        void BuildHeroFlourishes()
        {
            GoldHalo = Fx.GlowSprite(World, "rgba(255,205,110,0.9)", 4.2f, 0f);

            // spin blur: a stack of rings at slightly different radii, tilts and opacities.
            // One flat hoop reads as a static halo; several offset ones read as motion.
            var specs = new[]
            {
                new SpinRing { R=1.14f, Tube=0.055f, Tilt=0.00f, BaseOp=0.70f, Wob=1.7f, WobAmt=0.10f, Spin= 8.0f },
                new SpinRing { R=1.24f, Tube=0.040f, Tilt=0.22f, BaseOp=0.42f, Wob=2.3f, WobAmt=0.16f, Spin=-6.5f },
                new SpinRing { R=1.05f, Tube=0.035f, Tilt=-0.28f,BaseOp=0.30f, Wob=1.3f, WobAmt=0.20f, Spin=10.5f }
            };
            var cols = new[] { Hex.FromInt(0xffd27a), Hex.FromInt(0xffe0a0), Hex.FromInt(0xfff0c8) };
            for (int i = 0; i < specs.Length; i++)
            {
                var go = new GameObject("spinRing" + i);
                go.transform.SetParent(World, false);
                go.AddComponent<MeshFilter>().sharedMesh = Geo.Torus(specs[i].R, specs[i].Tube, 8, 44);
                var mr = go.AddComponent<MeshRenderer>();
                var m = MatUtil.Glow(new Color(cols[i].r, cols[i].g, cols[i].b, 0f));
                mr.sharedMaterial = m;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                go.transform.localRotation = Quaternion.Euler(90f, 0, 0);   // lie flat at the waist
                specs[i].T = go.transform;
                specs[i].M = mr.material;
                SpinRings.Add(specs[i]);
            }

            BurstLight = MkLight("burst", LightType.Point, Hex.FromInt(0xffe6a0), 0f);
            BurstLight.transform.localPosition = new Vector3(0, 1.4f, 0);
            BurstLight.range = 16f;

            BurstSprite = Fx.GlowSprite(World, "rgba(255,225,150,0.95)", 6f, 0f);
            BurstSprite.localPosition = new Vector3(0, 1.4f, 0);

            // Ner Tamid's eternal flame — warm candlelight that follows the dreidel.
            NerLight = MkLight("nerLight", LightType.Point, Hex.FromInt(0xffb84a), 0f);
            NerLight.transform.localPosition = new Vector3(0, 1.2f, 0);
            NerLight.range = 9f;
            NerLight.gameObject.SetActive(false);

            // ...and a REAL flame riding the handle tip: a living fire that stays upright
            // even as the vessel spins and topples beneath it, like a lamp that never goes out.
            NtFlame = new GameObject("ntFlame").transform;
            NtFlame.SetParent(World, false);
            var fm = new GameObject("ntFlameMesh");
            fm.transform.SetParent(NtFlame, false);
            fm.AddComponent<MeshFilter>().sharedMesh = Geo.Quad(0.42f, 0.62f);
            var fmr2 = fm.AddComponent<MeshRenderer>();
            fmr2.sharedMaterial = MatUtil.Glow(Color.white, Tex.Flame());
            fmr2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fmr2.receiveShadows = false;
            fm.AddComponent<Billboard>().Cam = Cam;
            NtFlameMesh = fm.transform;
            NtHalo = Fx.GlowSprite(NtFlame, "rgba(255,166,60,0.85)", 1.3f, 0.6f);
            NtFlame.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------
        /// <summary>Dress the table: fog, floor, sky, light colours, candles and prop kit.</summary>
        public void SetEnv(EnvDef env)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = env.Fog;
            // flat rooms: fog would dissolve the crisp 2D backdrop into haze — push it past
            // everything; every other table keeps its atmospheric 10..26 falloff
            RenderSettings.fogStartDistance = env.Room ? 80f : 10f;
            RenderSettings.fogEndDistance = env.Room ? 120f : 26f;

            GroundMat.mainTexture = Tex.Ground(env);
            KeyLight.color = env.Key;
            EnvRim = env.Rim;
            ApplyEnvironmentLighting(env);

            foreach (var g in CandleGroups) g.gameObject.SetActive(env.Candles);

            SkyMat.mainTexture = Tex.Sky(env);
            Fx.SetGlow(FloorGlow, 0.5f);
            var fgMr = FloorGlow.GetComponent<MeshRenderer>();
            if (fgMr != null)
            {
                var c = env.Glow; c.a = 0.5f;
                MatUtil.Tint(fgMr.material, c);
            }

            // flat rooms: no warm light-pool or under-glow — gradients would shade the 2D floor
            GlowLight.intensity = env.Room ? 0f : 0.55f;
            FloorGlow.gameObject.SetActive(!env.Room);
            if (StarField != null) StarField.gameObject.SetActive(env.Stars);

            EnvKits.Apply(this, env);
        }

        /// <summary>
        /// Image-based lighting from the table's own environment map.
        ///
        /// This is the half of the original's look that a straight port of the light objects
        /// misses. three.js had `scene.environment = cube`, which feeds every standard
        /// material's reflection AND its ambient term; Unity splits those into two settings,
        /// and without them a metallic surface has literally nothing to reflect, so gold,
        /// gems and glass render as flat dark plastic no matter how many lights point at them.
        ///
        /// Ambient becomes three-band rather than flat for the same reason: a single ambient
        /// colour lights the underside of the dreidel exactly as brightly as its top, which
        /// removes the shading that tells the eye where the floor is.
        /// </summary>
        void ApplyEnvironmentLighting(EnvDef env)
        {
            var cube = Tex.EnvCube(env);

            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
            // customReflectionTexture, not customReflection: Unity 6 widened the field from
            // Cubemap to Texture so a 2D reflection can be assigned, and deprecated the old one.
            RenderSettings.customReflectionTexture = cube;
            // three.js set envMapIntensity per material (1.2 on brass up to 2.2 on the gem);
            // Unity's Standard shader has no per-material equivalent and the global is a
            // 0..1 slider, so the metals get the full weight and the flat 2D rooms - where a
            // reflecting dreidel would fight the hand-drawn backdrop - get most of it taken
            // back off.
            RenderSettings.reflectionIntensity = env.Room ? 0.55f : 1f;
            RenderSettings.reflectionBounces = 1;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(env.Ambient, env.CubeHi, 0.55f);
            RenderSettings.ambientEquatorColor = env.Ambient;
            RenderSettings.ambientGroundColor = Color.Lerp(env.Ambient, env.CubeLo, 0.7f);
            RenderSettings.ambientIntensity = 1f;
        }

        public void SetPotCoinsVisible(int n)
        {
            for (int i = 0; i < PotCoins.Count; i++)
                PotCoins[i].gameObject.SetActive(i < Mathf.Min(n, PotCoins.Count));
        }
    }

    /// <summary>Where a coin goes back to after it has been thrown across the room.</summary>
    public class CoinHome : MonoBehaviour
    {
        public Vector3 P;
        public Quaternion R;
        public void Set(Transform t) { P = t.localPosition; R = t.localRotation; }
        public void GoHome() { transform.localPosition = P; transform.localRotation = R; }
    }
}
