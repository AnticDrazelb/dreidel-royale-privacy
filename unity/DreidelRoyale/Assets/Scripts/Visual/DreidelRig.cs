using System.Collections.Generic;
using UnityEngine;
using DreidelRoyale.Core;
using Stop = DreidelRoyale.Visual.Canvas2D.Stop;

namespace DreidelRoyale.Visual
{
    /// <summary>
    /// Builds the dreidel: rounded body, four lettered plaques and a brass cap, the square
    /// tip tucked underneath, the handle and knob, and the per-skin extras (the Menorah's
    /// branches, the Diamond's brilliant-cut gem, the Oil Miracle's liquid fill).
    ///
    /// Every constant is the web build's, in world units where the body edge is 1.6.
    /// </summary>
    public class DreidelRig
    {
        // geometry constants (world units)
        public const float BODY = 1.6f;                  // cube edge
        public const float HALF = BODY / 2f;
        public const float TIP_H = 1.15f;                // pyramid height
        public const float STAND_Y = HALF + TIP_H;       // body-centre height standing on the tip
        public const float LIE_Y = HALF;                 // body-centre height lying on a face

        public Transform Root;      // pose: lean, hop, walk
        public Transform Spinner;   // yaw only — the face that comes up is read off this

        public MeshRenderer Core, Tip, Handle, Knob;
        public MeshRenderer[] PlaqueRends = new MeshRenderer[4];
        public Material[] PlaqueMats = new Material[4];
        public Material TopMat;
        public Transform MenorahGroup, DiamondGem, FounderMark, OilRing;
        readonly List<MeshRenderer> _letterMeshes = new List<MeshRenderer>();
        public OilFluid Oil = new OilFluid();
        public OilDressing OilFoam = new OilDressing();
        public Transform OilGlint;

        string _currentSkin = "";
        bool _customMode;
        string[] _customLabels = { "", "", "", "" };

        public string CurrentSkin { get { return _currentSkin; } }

        public void Build(Transform parent)
        {
            SkinLibrary.Build();

            Root = new GameObject("dreidelRoot").transform;
            Root.SetParent(parent, false);
            Root.localPosition = new Vector3(0, STAND_Y, 0);

            Spinner = new GameObject("spinner").transform;
            Spinner.SetParent(Root, false);

            var wood = SkinLibrary.Get("wood");

            // ---- body core: rounded-edge cube ----
            // three.js extrude bevel EXPANDS the outline by bevelSize, so the shape is shrunk
            // first: hw + bevelSize lands back on exactly +/-HALF at the waist.
            const float BV = 0.12f, R = 0.16f;
            float hw = HALF - BV;
            var coreGo = Mk("core", Spinner,
                Geo.RoundedExtrudeBody(hw, R, BODY - 2f * BV, BV, BV, 4, 8), wood.Body);
            Core = coreGo.GetComponent<MeshRenderer>();
            Core.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            BuildOilFill();
            BuildPlaques();
            BuildLetters();

            // ---- tip: 4-sided pyramid tucked under the rounded body ----
            var tipGo = Mk("tip", Spinner, Geo.Cone(1.02f, TIP_H, 4), wood.Tip);
            tipGo.transform.localRotation = Quaternion.Euler(180f, 45f, 0f);   // point down, square to the cube
            tipGo.transform.localPosition = new Vector3(0, -(HALF + TIP_H / 2f) + 0.06f, 0);
            Tip = tipGo.GetComponent<MeshRenderer>();

            // ---- handle + knob ----
            var hGo = Mk("handle", Spinner, Geo.Cylinder(0.11f, 0.14f, 0.95f, 20), wood.Handle);
            hGo.transform.localPosition = new Vector3(0, HALF + 0.475f, 0);
            Handle = hGo.GetComponent<MeshRenderer>();

            var kGo = Mk("knob", Spinner, Geo.Sphere(0.2f, 20, 16), wood.Handle);
            kGo.transform.localPosition = new Vector3(0, HALF + 0.95f + 0.12f, 0);
            Knob = kGo.GetComponent<MeshRenderer>();

            BuildMenorah();
            BuildDiamondGem();
            BuildFounderMark();
            BuildOilRing();

            SetSkin("wood", true);
        }

        static GameObject Mk(string name, Transform parent, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            return go;
        }

        // ---- inset plaques with letters ----
        void BuildPlaques()
        {
            const float P = 0.812f, PS = 1.42f;
            var quad = Geo.PlaqueQuad(PS, PS);
            var st = FaceStyles.Get("wood");

            var spec = new[]
            {
                new { pos = new Vector3(0, 0,  P), rot = new Vector3(0,    0, 0), letter = Consts.Sides[0].Char },   // +z NUN   (0)
                new { pos = new Vector3( P, 0, 0), rot = new Vector3(0,   90, 0), letter = Consts.Sides[1].Char },   // +x GIMEL (-90)
                new { pos = new Vector3(0, 0, -P), rot = new Vector3(0,  180, 0), letter = Consts.Sides[2].Char },   // -z HEI   (-180)
                new { pos = new Vector3(-P, 0, 0), rot = new Vector3(0,  -90, 0), letter = Consts.Fourth().Char }    // -x SHIN  (-270)
            };

            for (int i = 0; i < 4; i++)
            {
                var mat = MatUtil.Plaque(Tex.Face(spec[i].letter, "wood"), st.Emissive);
                var go = Mk("plaque" + i, Spinner, quad, mat);
                go.transform.localPosition = spec[i].pos;
                go.transform.localRotation = Quaternion.Euler(spec[i].rot);
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                PlaqueRends[i] = go.GetComponent<MeshRenderer>();
                PlaqueMats[i] = mat;
            }

            // +y brass cap
            TopMat = MatUtil.Plaque(Tex.Top(), FaceStyles.Get("wood").Emissive);
            var top = Mk("plaqueTop", Spinner, quad, TopMat);
            top.transform.localPosition = new Vector3(0, P, 0);
            top.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            top.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// Raised glyphs: a plane just in front of each face, so the letter floats about
        /// 0.09 units proud of the plaque and catches its own light. In Decision Dreidel mode
        /// the custom text is painted flat on the plaques and these are skipped entirely —
        /// the Hebrew would float over the labels and clash.
        /// </summary>
        void BuildLetters()
        {
            foreach (var m in _letterMeshes) if (m != null) Object.Destroy(m.gameObject);
            _letterMeshes.Clear();
            if (_customMode) return;

            const float P = 0.905f;   // the face sits at ~0.812, so this floats ~0.09 proud
            const float SZ = 1.32f;
            var quad = Geo.PlaqueQuad(SZ, SZ);
            var skin = string.IsNullOrEmpty(_currentSkin) ? "wood" : _currentSkin;

            var spec = new[]
            {
                new { pos = new Vector3(0, 0,  P), rot = new Vector3(0,   0, 0), c = Consts.Sides[0].Char },
                new { pos = new Vector3( P, 0, 0), rot = new Vector3(0,  90, 0), c = Consts.Sides[1].Char },
                new { pos = new Vector3(0, 0, -P), rot = new Vector3(0, 180, 0), c = Consts.Sides[2].Char },
                new { pos = new Vector3(-P, 0, 0), rot = new Vector3(0, -90, 0), c = Consts.Fourth().Char }
            };

            foreach (var f in spec)
            {
                var mat = MatUtil.UnlitTex(Tex.Letter(f.c, skin), Color.white);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 20;
                var go = Mk("letter", Spinner, quad, mat);
                go.transform.localPosition = f.pos;
                go.transform.localRotation = Quaternion.Euler(f.rot);
                var mr = go.GetComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                _letterMeshes.Add(mr);
            }
        }

        /// <summary>
        /// The Oil Miracle's liquid. A child of the spinner, so it inherits the vessel's yaw
        /// exactly and its corners stay aligned with the glass walls; the surface itself is a
        /// simulated height field rather than a flat cap. The sides keep the authored gradient
        /// that darkens toward the bottom, so the fill's edges dissolve into the vessel
        /// instead of reading as a block.
        /// </summary>
        void BuildOilFill()
        {
            var sideMat = MatUtil.UnlitTex(Tex.OilSide(), Color.white);
            // White albedo, because the colour comes from the depth ramp the sim writes into
            // the surface's u - a flat tint here would multiply the thickness away again.
            var surfMat = MatUtil.Pbr(Color.white, 0.15f, 0.18f, Hex.FromInt(0x2a1804), 0.35f);
            surfMat.mainTexture = Tex.OilDepth();
            var botMat = MatUtil.UnlitColor(Hex.FromInt(0x050300));

            // Lit, unlike the sides: a moving surface only reads as liquid if the light rolls
            // across it, and the sim writes real normals for exactly that.
            Oil.Build(Spinner, surfMat, sideMat, botMat);

            // Foam, bubbles and thrown droplets, additive over the surface. A child of the
            // spinner like the fluid itself, so it turns with the vessel rather than smearing.
            OilFoam.Build(Spinner, MatUtil.Glow(Color.white, Tex.Radial(
                new Stop(0f, Hex.To("rgba(255,246,214,1)")),
                new Stop(0.45f, Hex.To("rgba(255,214,130,0.55)")),
                new Stop(1f, Hex.To("rgba(255,190,90,0)")))));

            OilGlint = Fx.GlowSprite(Spinner, "rgba(255,205,90,0.8)", 0.55f, 0f);
            OilGlint.gameObject.SetActive(false);
        }

        /// <summary>
        /// Menorah branches: four arm pairs curving up each side of the handle (the central
        /// stem is the shamash), each tipped with a gold cup and ember, so the Menorah
        /// dreidel's handle reads as an actual menorah.
        /// </summary>
        void BuildMenorah()
        {
            var g = new GameObject("menorah").transform;
            g.SetParent(Spinner, false);
            MenorahGroup = g;

            var armMat = MatUtil.Pbr(Hex.FromInt(0xf0b830), 0.9f, 0.3f, Hex.FromInt(0x3a2404), 0.25f);
            var cupMat = MatUtil.Pbr(Hex.FromInt(0xffd36a), 1.0f, 0.2f, Hex.FromInt(0x5a3c08), 0.35f);
            var emberMat = MatUtil.UnlitColor(Hex.FromInt(0xffd873));

            float baseY = HALF + 0.15f;      // where arms leave the stem

            // a thick gold collar where all the arms meet the stem
            var collar = Mk("collar", g, Geo.Torus(0.12f, 0.05f, 12, 24), armMat);
            collar.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            collar.transform.localPosition = new Vector3(0, baseY, 0);
            var collar2 = Mk("collar2", g, Geo.Cylinder(0.09f, 0.13f, 0.08f, 20), armMat);
            collar2.transform.localPosition = new Vector3(0, baseY + 0.09f, 0);

            float[] reaches = { 0.30f, 0.46f, 0.62f, 0.78f };
            float topY = HALF + 0.92f;       // all candle cups sit level, like a real menorah
            for (int i = 0; i < reaches.Length; i++)
                foreach (int side in new[] { -1, 1 })
                {
                    float x = side * reaches[i];
                    float dx = x, dy = topY - baseY;
                    float len = Mathf.Sqrt(dx * dx + dy * dy);
                    var arm = Mk("arm", g, Geo.Cylinder(0.03f, 0.03f, len, 8), armMat);
                    arm.transform.localPosition = new Vector3(x / 2f, (baseY + topY) / 2f, 0);
                    arm.transform.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(dx, dy) * Mathf.Rad2Deg);
                    var cup = Mk("cup", g, Geo.Cylinder(0.055f, 0.032f, 0.06f, 10), cupMat);
                    cup.transform.localPosition = new Vector3(x, topY, 0);
                    var ember = Mk("ember", g, Geo.Sphere(0.032f, 8, 6), emberMat);
                    ember.transform.localPosition = new Vector3(x, topY + 0.055f, 0);
                }

            // shamash cup on the central stem, slightly higher, where the knob is
            var scup = Mk("shamashCup", g, Geo.Cylinder(0.06f, 0.035f, 0.07f, 10), cupMat);
            scup.transform.localPosition = new Vector3(0, HALF + 1.02f, 0);
            var sember = Mk("shamashEmber", g, Geo.Sphere(0.036f, 8, 6), emberMat);
            sember.transform.localPosition = new Vector3(0, HALF + 1.09f, 0);

            g.gameObject.SetActive(false);
        }

        /// <summary>
        /// The Diamond's brilliant-cut gem, crowning the handle in place of the ball knob:
        /// a table ring, a girdle and a single culet point, with the facets between rings
        /// offset so they zig-zag like a cut stone.
        /// </summary>
        void BuildDiamondGem()
        {
            var g = new GameObject("diamondGem").transform;
            g.SetParent(Spinner, false);
            DiamondGem = g;

            var gemMat = MatUtil.Gem(Hex.FromInt(0xeaf6ff), Hex.FromInt(0x9fc8ff), 0.78f, 0.04f, 0.14f);

            const int N = 12;                 // facets around
            float cy = HALF + 1.06f;          // vertical centre of the gem
            // POINT UP: the flat table sits at the BOTTOM near the handle, the sharp culet on top.
            float tableY = cy - 0.16f, girdleY = cy - 0.04f, culetY = cy + 0.26f;
            float tableR = 0.085f, girdleR = 0.17f;

            var verts = new List<Vector3>();
            var tris = new List<int>();

            int tableStart = verts.Count;
            for (int i = 0; i < N; i++)
            {
                float a = i / (float)N * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * tableR, tableY, Mathf.Sin(a) * tableR));
            }
            int girdleStart = verts.Count;
            for (int i = 0; i < N; i++)
            {
                float a = (i + 0.5f) / N * Mathf.PI * 2f;   // offset half a step: the zig-zag
                verts.Add(new Vector3(Mathf.Cos(a) * girdleR, girdleY, Mathf.Sin(a) * girdleR));
            }
            int culet = verts.Count;
            verts.Add(new Vector3(0, culetY, 0));
            int tableCentre = verts.Count;
            verts.Add(new Vector3(0, tableY, 0));

            for (int i = 0; i < N; i++)
            {
                int j = (i + 1) % N;
                // pavilion band, table ring to girdle ring
                tris.Add(tableStart + i); tris.Add(girdleStart + i); tris.Add(tableStart + j);
                tris.Add(tableStart + j); tris.Add(girdleStart + i); tris.Add(girdleStart + j);
                // crown: girdle up to the culet point
                tris.Add(girdleStart + i); tris.Add(culet); tris.Add(girdleStart + j);
                // flat table underneath
                tris.Add(tableCentre); tris.Add(tableStart + j); tris.Add(tableStart + i);
            }

            var mesh = new Mesh { name = "BrilliantCut" };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var faceted = Geo.Faceted(mesh);          // flat-shaded: every facet catches its own light

            Mk("gem", g, faceted, gemMat);
            g.gameObject.SetActive(false);
        }

        /// <summary>Founder's engraved supporter mark, on one face below the plaque.</summary>
        void BuildFounderMark()
        {
            var g = new GameObject("founderMark").transform;
            g.SetParent(Spinner, false);
            FounderMark = g;

            var cv = new Canvas2D(128, 128);
            cv.StrokeRoundRect(20, 40, 88, 48, 10, 4, Hex.To("rgba(255,240,190,0.85)"));
            bool ok;
            var mask = GlyphRaster.Mask(new List<string> { "FOUNDER" }, 128, 22, 22f, out ok);
            if (ok) GlyphRaster.Draw(cv, mask, 128, 0, 0, Hex.To("rgba(255,248,216,0.95)"));

            var mat = MatUtil.Glow(Color.white, cv.ToTexture(false, true, "founderMark"));
            var go = Mk("mark", g, Geo.PlaqueQuad(0.5f, 0.5f), mat);
            go.transform.localPosition = new Vector3(0, -0.5f, 0.816f);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.gameObject.SetActive(false);
        }

        /// <summary>The Oil Miracle's gold band around the vessel's waist.</summary>
        void BuildOilRing()
        {
            var g = new GameObject("oilRing").transform;
            g.SetParent(Spinner, false);
            OilRing = g;
            var mat = MatUtil.Pbr(Hex.FromInt(0xc9962c), 1f, 0.22f, Hex.FromInt(0x4a3208), 0.3f);
            var go = Mk("band", g, Geo.Torus(0.86f, 0.055f, 10, 40), mat);
            go.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            go.transform.localPosition = new Vector3(0, -0.02f, 0);
            g.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------
        /// <summary>
        /// Dress the dreidel. Body, tip and handle swap material; the faces and the brass cap
        /// re-dress to match, from a cache, so the swap is map-only.
        /// </summary>
        public void SetSkin(string kind, bool force = false)
        {
            if (!force && kind == _currentSkin) return;
            if (!SkinLibrary.Skins.ContainsKey(kind ?? "")) return;
            _currentSkin = kind;
            var s = SkinLibrary.Get(kind);

            Core.sharedMaterial = s.Body;
            Tip.sharedMaterial = s.Tip;

            // Oil uses dedicated handle/knob glass with its own gradient and shows its gold
            // ring; every other skin uses its single handle material and hides the ring.
            if (kind == "oil")
            {
                Handle.sharedMaterial = SkinLibrary.OilHandle;
                Knob.sharedMaterial = SkinLibrary.OilKnob;
            }
            else
            {
                Handle.sharedMaterial = s.Handle;
                Knob.sharedMaterial = s.Handle;
            }

            if (OilRing) OilRing.gameObject.SetActive(kind == "oil");
            Oil.SetActive(kind == "oil");
            OilFoam.SetActive(kind == "oil");
            if (OilGlint) OilGlint.gameObject.SetActive(kind == "oil");
            if (FounderMark) FounderMark.gameObject.SetActive(kind == "founder");
            if (MenorahGroup) MenorahGroup.gameObject.SetActive(kind == "streaker");
            if (DiamondGem) DiamondGem.gameObject.SetActive(kind == "diamond");
            // for the Diamond skin the gem crowns the handle in place of the ball knob
            if (Knob) Knob.gameObject.SetActive(kind != "diamond");

            var st = FaceStyles.Get(kind);
            // In Decision Dreidel mode the four faces show the user's labels; SIDES order is
            // NUN, GIMEL, HEI, SHIN -> customLabels[0..3].
            var letters = _customMode
                ? _customLabels
                : new[] { Consts.Sides[0].Char, Consts.Sides[1].Char, Consts.Sides[2].Char, Consts.Fourth().Char };

            for (int i = 0; i < 4; i++)
            {
                var t = Tex.Face(letters[i], kind);
                PlaqueMats[i].mainTexture = t;
                PlaqueMats[i].SetTexture("_EmissionMap", t);
                PlaqueMats[i].SetColor("_EmissionColor", st.Emissive);
            }

            BuildLetters();   // the floating glyphs re-dress to match the body
        }

        /// <summary>Swap the four faces for Decision Dreidel labels, or back to the letters.</summary>
        public void SetCustomFaces(bool on, string[] labels)
        {
            _customMode = on;
            if (labels != null)
                for (int i = 0; i < 4 && i < labels.Length; i++) _customLabels[i] = labels[i];
            var k = string.IsNullOrEmpty(_currentSkin) ? "wood" : _currentSkin;
            SetSkin(k, true);
        }

        /// <summary>Repaint the fourth face and its glyph after an Israel/diaspora toggle.</summary>
        public void RebuildLetters()
        {
            var k = string.IsNullOrEmpty(_currentSkin) ? "wood" : _currentSkin;
            SetSkin(k, true);
        }

        /// <summary>Spinner yaw in degrees, the value the landing maths is expressed in.</summary>
        public float RotDeg
        {
            get { return Spinner.localEulerAngles.y; }
            set { Spinner.localRotation = Quaternion.Euler(0, value, 0); }
        }

        /// <summary>
        /// Which face currently points most nearly upward. Cosmetic only — the result of a
        /// spin is decided by the landing yaw in Rules.ResolveFace, never read back here.
        /// </summary>
        public string UpFace()
        {
            var q = Spinner.rotation;
            var axes = new Dictionary<string, Vector3>
            {
                { "NUN",   new Vector3(0, 0, 1) },
                { "GIMEL", new Vector3(1, 0, 0) },
                { "HEI",   new Vector3(0, 0, -1) },
                { "TOP",   new Vector3(0, 1, 0) },
                { "TIP",   new Vector3(0, -1, 0) },
                { Consts.Fourth().Name, new Vector3(-1, 0, 0) }
            };
            string best = null; float bestY = -2f;
            foreach (var kv in axes)
            {
                float y = (q * kv.Value).y;
                if (y > bestY) { bestY = y; best = kv.Key; }
            }
            return best;
        }
    }
}
