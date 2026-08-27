using System.Collections.Generic;
using UnityEngine;
using DreidelRoyale.Core;

namespace DreidelRoyale.Visual
{
    /// <summary>
    /// Per-table prop kits, all procedural. Each one dresses the space around the play area
    /// in the table's own idiom: a starfield, cellar barrels, ice shards, chips and dice,
    /// voxel trees and torches, or flat storybook toys.
    /// </summary>
    public static class EnvKits
    {
        public static void Apply(SceneRig rig, EnvDef env)
        {
            Clear(rig);
            switch (env.Kit)
            {
                case "midnight": Midnight(rig); break;
                case "den": Den(rig); break;
                case "frost": Frost(rig); break;
                case "felt": Felt(rig); break;
                case "blocky": Blocky(rig); break;
                case "backyard": Backyard(rig); break;
            }
        }

        static void Clear(SceneRig rig)
        {
            for (int i = rig.EnvProps.childCount - 1; i >= 0; i--)
                Object.Destroy(rig.EnvProps.GetChild(i).gameObject);
            if (rig.StarField != null) { Object.Destroy(rig.StarField.gameObject); rig.StarField = null; }
        }

        static Transform Mk(SceneRig rig, string name, Mesh mesh, Material mat, Vector3 pos,
                            Vector3? euler = null, bool shadow = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rig.EnvProps, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On
                                          : UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = shadow;
            go.transform.localPosition = pos;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            return go.transform;
        }

        static Mesh Cube(float s) { return Geo.Box(s, s, s); }              // one material
        static Mesh Cube6(float s) { return Geo.BoxSixMaterials(s, s, s); }  // six-faced, for grass blocks

        // ---------------- Midnight: a starfield dome ----------------
        static void Midnight(SceneRig rig)
        {
            const int N = 170;
            var verts = new Vector3[N];
            var idx = new int[N];
            var cols = new Color[N];
            for (int i = 0; i < N; i++)
            {
                float th = Random.value * Mathf.PI * 2f, ph = Random.value * Mathf.PI * 0.45f;
                const float r = 21f;
                verts[i] = new Vector3(r * Mathf.Sin(ph) * Mathf.Cos(th),
                                       r * Mathf.Cos(ph) + 1f,
                                       r * Mathf.Sin(ph) * Mathf.Sin(th));
                idx[i] = i;
                cols[i] = new Color(1f, 244 / 255f, 214 / 255f, 0.85f);
            }
            var mesh = new Mesh { name = "starfield" };
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.SetIndices(idx, MeshTopology.Points, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 60f);

            var go = new GameObject("starField");
            go.transform.SetParent(rig.World, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            // Point topology renders as single pixels; a small additive quad per star would
            // cost more than it buys at this size, and the web build's points look the same.
            mr.sharedMaterial = MatUtil.Glow(new Color(1f, 244 / 255f, 214 / 255f, 0.85f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            rig.StarField = go.transform;
        }

        // ---------------- Maple Den: cellar barrels ----------------
        static void Den(SceneRig rig)
        {
            var bm = MatUtil.Pbr(Color.white, 0f, 0.8f, null, 0f, BarrelTexture());
            var barrel = Geo.Cylinder(0.5f, 0.56f, 1.05f, 14);
            Mk(rig, "barrel", barrel, bm, new Vector3(-1.85f, 0.525f, -3.8f), new Vector3(0, 0.5f * Mathf.Rad2Deg, 0));
            Mk(rig, "barrel", barrel, bm, new Vector3(1.9f, 0.525f, -4.3f), new Vector3(0, -0.3f * Mathf.Rad2Deg, 0));
            // one tipped barrel
            Mk(rig, "barrelTipped", barrel, bm, new Vector3(-1.6f, 0.53f, -1.8f),
               new Vector3(0, 0.7f * Mathf.Rad2Deg, 90f));
        }

        static Texture2D BarrelTexture()
        {
            var cv = new Canvas2D(128, 128);
            cv.FillAll(Hex.To("#6b4526"));
            for (int i = 0; i < 10; i++)
            {
                float x = i * 13f;
                cv.FillRect(x, 0, 10, 128, new Color(0, 0, 0, i % 2 == 0 ? 0.10f : 0.04f));
                cv.StrokeLine(x, 0, x, 128, 1.5f, new Color(0, 0, 0, 0.28f));
            }
            foreach (float band in new[] { 16f, 60f, 104f })
            {
                cv.FillRect(0, band, 128, 12, Hex.To("#3a3a42"));
                cv.FillRect(0, band, 128, 3, new Color(1, 1, 1, 0.18f));
            }
            return cv.ToTexture(false, true, "barrel");
        }

        // ---------------- Silver Frost: ice shards ----------------
        static void Frost(SceneRig rig)
        {
            var im = MatUtil.Gem(Hex.FromInt(0xbfe6ff), Hex.FromInt(0x1c3450), 0.82f, 0.12f, 0.4f);
            var spec = new[]
            {
                new Vector4(-1.8f, -3.2f, 1.05f, 1.1f), new Vector4(1.85f, -3.6f, 1.2f, 1.5f),
                new Vector4(1.5f, -1.4f, 0.6f, 2.2f),   new Vector4(-1.45f, -0.9f, 0.42f, 0.4f),
                new Vector4(1.5f, -1.05f, 0.38f, 0.8f)
            };
            foreach (var s in spec)
            {
                var mesh = Geo.Faceted(Geo.Sphere(s.z, 5, 4));    // low-poly, hard facets: an ice shard
                Mk(rig, "shard", mesh, im, new Vector3(s.x, s.z * 0.55f, s.y),
                   new Vector3(Random.value * 0.6f * Mathf.Rad2Deg, s.w * Mathf.Rad2Deg, Random.value * 0.5f * Mathf.Rad2Deg));
            }
        }

        // ---------------- Casino Felt: chip stacks and dice ----------------
        static void Felt(SceneRig rig)
        {
            var chipColors = new[] { 0xc0392b, 0x2455a4, 0x1a1a22, 0xf4f1ea };
            var chipMesh = Geo.Cylinder(0.3f, 0.3f, 0.065f, 18);
            var spots = new[] { new Vector2(-1.5f, -1.2f), new Vector2(1.55f, -1.4f) };
            for (int si = 0; si < spots.Length; si++)
            {
                int n = 5 + si * 2;
                for (int i = 0; i < n; i++)
                {
                    var c = Hex.FromInt(chipColors[(i + si) % 4]);
                    var m = MatUtil.Pbr(c, 0f, 0.5f, c, 0.22f);
                    Mk(rig, "chip", chipMesh, m, new Vector3(
                        spots[si].x + (i % 2 == 1 ? 0.025f : -0.02f),
                        0.0325f + i * 0.067f,
                        spots[si].y + (i % 2 == 1 ? -0.02f : 0.025f)));
                }
            }

            // pair of dice, front of the dreidel. Box face order is +x,-x,+y,-y,+z,-z, so
            // opposite faces get 1/6, 2/5 and 3/4 the way real dice are numbered.
            var pips = new[] { 1, 6, 2, 5, 3, 4 };
            var mats = new Material[6];
            for (int i = 0; i < 6; i++) mats[i] = MatUtil.Pbr(Color.white, 0f, 0.35f, null, 0f, DiceTexture(pips[i]));
            var dieMesh = Geo.BoxSixMaterials(0.36f, 0.36f, 0.36f);
            foreach (var d in new[] { new Vector3(1.3f, -0.15f, 0.6f), new Vector3(1.12f, 0.45f, 1.9f) })
            {
                var go = new GameObject("die");
                go.transform.SetParent(rig.EnvProps, false);
                go.AddComponent<MeshFilter>().sharedMesh = dieMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = mats;
                go.transform.localPosition = new Vector3(d.x, 0.18f, d.y);
                go.transform.localRotation = Quaternion.Euler(0, d.z * Mathf.Rad2Deg, 0);
            }
        }

        static Texture2D DiceTexture(int pips)
        {
            var cv = new Canvas2D(64, 64);
            cv.FillAll(Hex.To("#f4f1ea"));
            var layouts = new Dictionary<int, Vector2[]>
            {
                { 1, new[]{ new Vector2(32,32) } },
                { 2, new[]{ new Vector2(18,18), new Vector2(46,46) } },
                { 3, new[]{ new Vector2(16,16), new Vector2(32,32), new Vector2(48,48) } },
                { 4, new[]{ new Vector2(18,18), new Vector2(46,18), new Vector2(18,46), new Vector2(46,46) } },
                { 5, new[]{ new Vector2(16,16), new Vector2(48,16), new Vector2(32,32), new Vector2(16,48), new Vector2(48,48) } },
                { 6, new[]{ new Vector2(18,14), new Vector2(46,14), new Vector2(18,32), new Vector2(46,32), new Vector2(18,50), new Vector2(46,50) } }
            };
            foreach (var p in layouts[pips]) cv.FillCircle(p.x, p.y, 6, Hex.To("#1a1a22"));
            return cv.ToTexture(false, true, "dice" + pips);
        }

        // ---------------- Blocky Biome: cube trees, blocks and torches ----------------
        static void Blocky(SceneRig rig)
        {
            var leafM = MatUtil.Pbr(Color.white, 0f, 0.9f, null, 0f,
                Tex.Pixel("#3f8a2c", new[] { "#357a24", "#4c9c36", "#2e6e1e", "#58aa42" }, 0.7f));
            var logM = MatUtil.Pbr(Color.white, 0f, 0.9f, null, 0f,
                Tex.Pixel("#6e4b2c", new[] { "#5f4226", "#7c5a34", "#54381e" }, 0.5f));
            var dirtM = MatUtil.Pbr(Color.white, 0f, 0.95f, null, 0f,
                Tex.Pixel("#7a5533", new[] { "#6e4b2c", "#86603a", "#5f4226" }, 0.55f));
            var grassTopM = MatUtil.Pbr(Color.white, 0f, 0.9f, null, 0f,
                Tex.Pixel("#5d9440", new[] { "#549038", "#67a047", "#4f8a34" }, 0.6f));
            var grassSideM = MatUtil.Pbr(Color.white, 0f, 0.9f, null, 0f, Tex.GrassSide());
            var stoneM = MatUtil.Pbr(Color.white, 0f, 0.95f, null, 0f,
                Tex.Pixel("#8a8a8a", new[] { "#767676", "#9a9a9a", "#6a6a6a" }, 0.6f));
            var grassBlockMats = new[] { grassSideM, grassSideM, grassTopM, dirtM, grassSideM, grassSideM };

            // two cube trees flanking the back of the table
            var trees = new[] { new Vector2(-2.2f, -4.0f), new Vector2(2.3f, -4.4f) };
            for (int i = 0; i < trees.Length; i++)
            {
                float trunkH = 1.4f + i * 0.3f;
                Mk(rig, "trunk", Geo.Box(0.42f, trunkH, 0.42f), logM,
                   new Vector3(trees[i].x, trunkH / 2f, trees[i].y));
                // canopy: a fat cube with two smaller cubes breaking the silhouette
                Mk(rig, "canopy", Cube(1.35f), leafM, new Vector3(trees[i].x, trunkH + 0.55f, trees[i].y));
                Mk(rig, "canopy2", Cube(0.7f), leafM, new Vector3(trees[i].x + 0.75f, trunkH + 0.25f, trees[i].y + 0.2f));
                Mk(rig, "canopy3", Cube(0.6f), leafM, new Vector3(trees[i].x - 0.35f, trunkH + 1.35f, trees[i].y - 0.3f));
            }

            // scattered voxel blocks near the play area
            MkMulti(rig, Cube6(0.44f), grassBlockMats, new Vector3(-1.55f, 0.22f, -1.35f));
            MkMulti(rig, Cube6(0.40f), grassBlockMats, new Vector3(1.6f, 0.20f, -1.1f));
            Mk(rig, "stone", Cube(0.34f), stoneM, new Vector3(1.35f, 0.17f, -1.5f));
            Mk(rig, "dirt", Cube(0.26f), dirtM, new Vector3(-1.3f, 0.13f, -0.6f));

            // torches where the candles would stand: pixel stick, glowing coal head
            var torches = new[] { new Vector2(-1.75f, -3.4f), new Vector2(1.75f, -3.4f) };
            var headM = MatUtil.Pbr(Hex.FromInt(0xffb845), 0f, 0.6f, Hex.FromInt(0xff8a20), 1.4f);
            for (int i = 0; i < torches.Length; i++)
            {
                Mk(rig, "stick", Geo.Box(0.14f, 1.0f, 0.14f), logM,
                   new Vector3(torches[i].x, 0.5f, torches[i].y));
                Mk(rig, "coal", Cube(0.2f), headM, new Vector3(torches[i].x, 1.08f, torches[i].y), null, false);
                var halo = Fx.GlowSprite(rig.EnvProps, "rgba(255,180,80,0.85)", 1.2f, 0.6f);
                halo.localPosition = new Vector3(torches[i].x, 1.15f, torches[i].y);
                if (i == 0)
                {
                    // one shared light keeps the budget where the candle pair left it
                    var lGo = new GameObject("torchLight");
                    lGo.transform.SetParent(rig.EnvProps, false);
                    lGo.transform.localPosition = new Vector3(0, 1.3f, torches[i].y);
                    var l = lGo.AddComponent<Light>();
                    l.type = LightType.Point; l.color = Hex.FromInt(0xffa64d);
                    l.intensity = 0.55f; l.range = 10f; l.shadows = LightShadows.None;
                }
            }
        }

        static void MkMulti(SceneRig rig, Mesh mesh, Material[] mats, Vector3 pos)
        {
            var go = new GameObject("block");
            go.transform.SetParent(rig.EnvProps, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            go.transform.localPosition = pos;
        }

        // ---------------- Backyard Games: flat storybook toys ----------------
        static void Backyard(SceneRig rig)
        {
            // FLAT kit: every prop is unlit, so there is no 3D shading at all. Depth comes
            // only from silhouette and hand-placed two-tone colour, which is exactly how the
            // storybook look works.
            System.Func<int, Material> flat = c => MatUtil.UnlitColor(Hex.FromInt(c));

            var trees = new[] { new Vector3(-2.3f, -4.2f, 1.0f), new Vector3(2.4f, -4.6f, 1.25f) };
            for (int i = 0; i < trees.Length; i++)
            {
                float s = trees[i].z;
                Mk(rig, "trunk", Geo.Cylinder(0.16f, 0.22f, 1.3f, 10), flat(0xb08a58),
                   new Vector3(trees[i].x, 0.65f, trees[i].y), null, false);
                var can = Mk(rig, "canopy", Geo.Sphere(s, 16, 12), flat(i == 1 ? 0x93cc72 : 0x7dbb5e),
                   new Vector3(trees[i].x, 1.3f + s * 0.7f, trees[i].y), null, false);
                can.localScale = new Vector3(1, 0.85f, 1);
                Mk(rig, "puff", Geo.Sphere(s * 0.55f, 12, 10), flat(i == 1 ? 0x7dbb5e : 0x93cc72),
                   new Vector3(trees[i].x + s * 0.6f, 1.15f + s * 0.5f, trees[i].y + 0.2f), null, false);
            }

            // the red ball — flat disc of colour with a flat white glint sticker
            Mk(rig, "ball", Geo.Sphere(0.42f, 18, 14), flat(0xe8503c), new Vector3(1.65f, 0.42f, -1.2f));
            var glint = Mk(rig, "glint", Geo.Circle(0.09f, 12), flat(0xffffff),
                           new Vector3(1.5f, 0.62f, -0.9f), null, false);
            glint.rotation = Quaternion.LookRotation(new Vector3(0, 3, 8) - glint.position);

            // keepy-uppy balloon on its string
            var balloon = Mk(rig, "balloon", Geo.Sphere(0.34f, 16, 12), flat(0xffd24a),
                             new Vector3(-1.7f, 2.4f, -2.2f), null, false);
            balloon.localScale = new Vector3(1, 1.15f, 1);
            Mk(rig, "string", Geo.Cylinder(0.012f, 0.012f, 0.9f, 6), flat(0xf2ead2),
               new Vector3(-1.7f, 1.55f, -2.2f), null, false);

            // toy blocks instead of cushions — solid pastel
            var blocks = new[]
            {
                new Vector4(-1.5f, -0.9f, 0x8fb8e8, 0.4f),
                new Vector4(1.35f, -0.5f, 0xf2a8c0, 0.34f),
                new Vector4(-1.15f, -1.5f, 0xffd24a, 0.26f)
            };
            foreach (var b in blocks)
            {
                float s = b.w;
                Mk(rig, "toyBlock", Geo.Box(s * 1.6f, s * 1.6f, s * 1.6f), flat((int)b.z),
                   new Vector3(b.x, s * 0.8f, b.y), new Vector3(0, Random.value * 0.8f * Mathf.Rad2Deg, 0));
            }
        }
    }
}
