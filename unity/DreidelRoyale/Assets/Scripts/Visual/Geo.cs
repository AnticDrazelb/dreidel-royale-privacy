using System.Collections.Generic;
using UnityEngine;

namespace DreidelRoyale.Visual
{
    /// <summary>
    /// Procedural mesh builders. The web build generates every piece of the dreidel in
    /// code rather than shipping models, and so does this port — same construction, same
    /// constants, so the silhouette matches rather than merely resembling.
    /// </summary>
    public static class Geo
    {
        // ---------------------------------------------------------------
        // The body: a rounded square profile swept along Z with a bevelled cap.
        //
        // three.js ExtrudeGeometry's bevel EXPANDS the outline outward, which is why the
        // source shrinks the shape first (hw = HALF - bevelSize). The swept solid is:
        //   |z| <= depth/2                      outline offset by bevelSize
        //   beyond, over a quarter-circle arc   offset falls to 0 as z reaches the cap
        // Offsetting a rounded square outward by d is exactly a rounded square with the
        // same corner centres and radius R + d, which makes the sweep a clean one-parameter
        // family and lets every normal be computed analytically instead of averaged.
        // ---------------------------------------------------------------
        public static Mesh RoundedExtrudeBody(float hw, float cornerR, float depth,
            float bevelSize, float bevelThickness, int bevelSegments = 4, int curveSegments = 8)
        {
            float cc = hw - cornerR;              // corner-arc centre offset, constant under offsetting
            int perCorner = curveSegments + 1;    // inclusive of both ends, so edge normals stay exact
            int ring = 4 * perCorner;

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Ring plan: back cap rim -> back bevel -> side wall (2 rings) -> front bevel -> front cap rim.
            var offs = new List<float>();
            var zs = new List<float>();
            var nrs = new List<float>();          // radial component of the surface normal
            var nzs = new List<float>();          // axial component

            float halfD = depth * 0.5f;

            // back bevel: theta pi/2 -> 0  (cap rim to side wall)
            for (int i = bevelSegments; i >= 0; i--)
            {
                float th = (i / (float)bevelSegments) * Mathf.PI * 0.5f;
                offs.Add(bevelSize * Mathf.Cos(th));
                zs.Add(-(halfD + bevelThickness * Mathf.Sin(th)));
                var n = new Vector2(bevelThickness * Mathf.Cos(th), -bevelSize * Mathf.Sin(th)).normalized;
                if (n.sqrMagnitude < 1e-8f) n = new Vector2(1f, 0f);
                nrs.Add(n.x); nzs.Add(n.y);
            }
            // side wall (duplicate ring at the far end so the wall is its own quad band)
            offs.Add(bevelSize); zs.Add(halfD); nrs.Add(1f); nzs.Add(0f);
            // front bevel: theta 0 -> pi/2
            for (int i = 0; i <= bevelSegments; i++)
            {
                float th = (i / (float)bevelSegments) * Mathf.PI * 0.5f;
                offs.Add(bevelSize * Mathf.Cos(th));
                zs.Add(halfD + bevelThickness * Mathf.Sin(th));
                var n = new Vector2(bevelThickness * Mathf.Cos(th), bevelSize * Mathf.Sin(th)).normalized;
                if (n.sqrMagnitude < 1e-8f) n = new Vector2(1f, 0f);
                nrs.Add(n.x); nzs.Add(n.y);
            }

            int ringCount = offs.Count;

            // corner arc start angles, walking the profile anticlockwise from +x/+y
            float[] a0 = { 0f, 90f, 180f, 270f };
            Vector2[] centres = {
                new Vector2( cc,  cc), new Vector2(-cc,  cc),
                new Vector2(-cc, -cc), new Vector2( cc, -cc)
            };

            for (int r = 0; r < ringCount; r++)
            {
                float rad = cornerR + offs[r];
                for (int c = 0; c < 4; c++)
                {
                    for (int s = 0; s <= curveSegments; s++)
                    {
                        float a = Mathf.Deg2Rad * (a0[c] + 90f * s / curveSegments);
                        var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        var p = centres[c] + dir * rad;
                        verts.Add(new Vector3(p.x, p.y, zs[r]));
                        norms.Add(new Vector3(dir.x * nrs[r], dir.y * nrs[r], nzs[r]).normalized);
                        float u = (c * (curveSegments + 1) + s) / (float)(ring - 1);
                        uvs.Add(new Vector2(u, (zs[r] + halfD + bevelThickness) / (depth + 2f * bevelThickness)));
                    }
                }
            }

            for (int r = 0; r < ringCount - 1; r++)
            {
                int b0 = r * ring, b1 = (r + 1) * ring;
                for (int i = 0; i < ring; i++)
                {
                    int j = (i + 1) % ring;
                    tris.Add(b0 + i); tris.Add(b1 + j); tris.Add(b1 + i);
                    tris.Add(b0 + i); tris.Add(b0 + j); tris.Add(b1 + j);
                }
            }

            // flat caps (fan from centre), profile at zero offset
            AddCap(verts, norms, uvs, tris, centres, a0, cornerR, curveSegments,
                   -(halfD + bevelThickness), new Vector3(0, 0, -1), hw);
            AddCap(verts, norms, uvs, tris, centres, a0, cornerR, curveSegments,
                   halfD + bevelThickness, new Vector3(0, 0, 1), hw);

            return Build("DreidelBody", verts, norms, uvs, tris);
        }

        static void AddCap(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
            Vector2[] centres, float[] a0, float rad, int curveSegments, float z, Vector3 n, float hw)
        {
            int centre = verts.Count;
            verts.Add(new Vector3(0, 0, z)); norms.Add(n); uvs.Add(new Vector2(0.5f, 0.5f));
            int start = verts.Count;
            var pts = new List<Vector2>();
            for (int c = 0; c < 4; c++)
                for (int s = 0; s <= curveSegments; s++)
                {
                    float a = Mathf.Deg2Rad * (a0[c] + 90f * s / curveSegments);
                    pts.Add(centres[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad);
                }
            foreach (var p in pts)
            {
                verts.Add(new Vector3(p.x, p.y, z)); norms.Add(n);
                uvs.Add(new Vector2(p.x / (2f * hw) + 0.5f, p.y / (2f * hw) + 0.5f));
            }
            int count = pts.Count;
            bool front = n.z > 0f;
            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                if (front) { tris.Add(centre); tris.Add(start + i); tris.Add(start + j); }
                else { tris.Add(centre); tris.Add(start + j); tris.Add(start + i); }
            }
        }

        // ---------------------------------------------------------------
        /// <summary>Cone / pyramid. radialSegments 4 gives the dreidel's square tip.</summary>
        public static Mesh Cone(float radius, float height, int radialSegments, bool flat = true)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float hh = height * 0.5f;

            for (int i = 0; i < radialSegments; i++)
            {
                float a0 = i / (float)radialSegments * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)radialSegments * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0) * radius, -hh, Mathf.Sin(a0) * radius);
                var p1 = new Vector3(Mathf.Cos(a1) * radius, -hh, Mathf.Sin(a1) * radius);
                var apex = new Vector3(0, hh, 0);
                var n = Vector3.Cross(apex - p0, p1 - p0).normalized;
                int b = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(apex);
                norms.Add(n); norms.Add(n); norms.Add(n);
                uvs.Add(new Vector2(i / (float)radialSegments, 0));
                uvs.Add(new Vector2((i + 1) / (float)radialSegments, 0));
                uvs.Add(new Vector2((i + 0.5f) / radialSegments, 1));
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
            }
            // base cap
            int c = verts.Count;
            verts.Add(new Vector3(0, -hh, 0)); norms.Add(Vector3.down); uvs.Add(new Vector2(0.5f, 0.5f));
            int s0 = verts.Count;
            for (int i = 0; i < radialSegments; i++)
            {
                float a = i / (float)radialSegments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * radius, -hh, Mathf.Sin(a) * radius));
                norms.Add(Vector3.down);
                uvs.Add(new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
            }
            for (int i = 0; i < radialSegments; i++)
            {
                int j = (i + 1) % radialSegments;
                tris.Add(c); tris.Add(s0 + i); tris.Add(s0 + j);
            }
            return Build("Cone", verts, norms, uvs, tris);
        }

        /// <summary>Cylinder with independent top/bottom radii, capped.</summary>
        public static Mesh Cylinder(float rTop, float rBottom, float height, int radialSegments, bool caps = true)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float hh = height * 0.5f;
            float slope = (rBottom - rTop) / Mathf.Max(height, 1e-5f);

            for (int i = 0; i <= radialSegments; i++)
            {
                float t = i / (float)radialSegments;
                float a = t * Mathf.PI * 2f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                var n = new Vector3(ca, slope, sa).normalized;
                verts.Add(new Vector3(ca * rTop, hh, sa * rTop)); norms.Add(n); uvs.Add(new Vector2(t, 1));
                verts.Add(new Vector3(ca * rBottom, -hh, sa * rBottom)); norms.Add(n); uvs.Add(new Vector2(t, 0));
            }
            for (int i = 0; i < radialSegments; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
            if (caps)
            {
                AddDisc(verts, norms, uvs, tris, rTop, hh, Vector3.up, radialSegments);
                AddDisc(verts, norms, uvs, tris, rBottom, -hh, Vector3.down, radialSegments);
            }
            return Build("Cylinder", verts, norms, uvs, tris);
        }

        static void AddDisc(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
            float radius, float y, Vector3 n, int segs)
        {
            if (radius <= 0f) return;
            int c = verts.Count;
            verts.Add(new Vector3(0, y, 0)); norms.Add(n); uvs.Add(new Vector2(0.5f, 0.5f));
            int s0 = verts.Count;
            for (int i = 0; i < segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                verts.Add(new Vector3(ca * radius, y, sa * radius)); norms.Add(n);
                uvs.Add(new Vector2(ca * 0.5f + 0.5f, sa * 0.5f + 0.5f));
            }
            bool up = n.y > 0f;
            for (int i = 0; i < segs; i++)
            {
                int j = (i + 1) % segs;
                if (up) { tris.Add(c); tris.Add(s0 + j); tris.Add(s0 + i); }
                else { tris.Add(c); tris.Add(s0 + i); tris.Add(s0 + j); }
            }
        }

        public static Mesh Sphere(float radius, int widthSegs, int heightSegs)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int y = 0; y <= heightSegs; y++)
            {
                float v = y / (float)heightSegs, phi = v * Mathf.PI;
                for (int x = 0; x <= widthSegs; x++)
                {
                    float u = x / (float)widthSegs, theta = u * Mathf.PI * 2f;
                    var n = new Vector3(-Mathf.Cos(theta) * Mathf.Sin(phi), Mathf.Cos(phi),
                                         Mathf.Sin(theta) * Mathf.Sin(phi));
                    verts.Add(n * radius); norms.Add(n); uvs.Add(new Vector2(u, 1f - v));
                }
            }
            int row = widthSegs + 1;
            for (int y = 0; y < heightSegs; y++)
                for (int x = 0; x < widthSegs; x++)
                {
                    int a = y * row + x, b = a + row;
                    tris.Add(a); tris.Add(b); tris.Add(a + 1);
                    tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
                }
            return Build("Sphere", verts, norms, uvs, tris);
        }

        /// <summary>Torus in the XY plane (matches three.js TorusGeometry orientation).</summary>
        public static Mesh Torus(float radius, float tube, int radialSegs, int tubularSegs)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int j = 0; j <= radialSegs; j++)
                for (int i = 0; i <= tubularSegs; i++)
                {
                    float u = i / (float)tubularSegs * Mathf.PI * 2f;
                    float v = j / (float)radialSegs * Mathf.PI * 2f;
                    var p = new Vector3((radius + tube * Mathf.Cos(v)) * Mathf.Cos(u),
                                        (radius + tube * Mathf.Cos(v)) * Mathf.Sin(u),
                                        tube * Mathf.Sin(v));
                    var centre = new Vector3(radius * Mathf.Cos(u), radius * Mathf.Sin(u), 0);
                    verts.Add(p); norms.Add((p - centre).normalized);
                    uvs.Add(new Vector2(i / (float)tubularSegs, j / (float)radialSegs));
                }
            int row = tubularSegs + 1;
            for (int j = 1; j <= radialSegs; j++)
                for (int i = 1; i <= tubularSegs; i++)
                {
                    int a = row * j + i - 1, b = row * (j - 1) + i - 1, c = row * (j - 1) + i, d = row * j + i;
                    tris.Add(a); tris.Add(b); tris.Add(d);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            return Build("Torus", verts, norms, uvs, tris);
        }

        /// <summary>Flat annulus in the XY plane, double sided when asked.</summary>
        public static Mesh Ring(float inner, float outer, int segs, bool doubleSided = false)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs, a = t * Mathf.PI * 2f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                verts.Add(new Vector3(ca * inner, sa * inner, 0)); norms.Add(Vector3.forward); uvs.Add(new Vector2(t, 0));
                verts.Add(new Vector3(ca * outer, sa * outer, 0)); norms.Add(Vector3.forward); uvs.Add(new Vector2(t, 1));
            }
            for (int i = 0; i < segs; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
            if (doubleSided)
            {
                int off = verts.Count;
                for (int i = 0; i < off; i++) { verts.Add(verts[i]); norms.Add(-norms[i]); uvs.Add(uvs[i]); }
                int n = tris.Count;
                for (int i = 0; i < n; i += 3)
                { tris.Add(off + tris[i]); tris.Add(off + tris[i + 2]); tris.Add(off + tris[i + 1]); }
            }
            return Build("Ring", verts, norms, uvs, tris);
        }

        /// <summary>Disc in the XY plane facing -Z, so a -90° X rotation lays it on the ground.</summary>
        public static Mesh Circle(float radius, int segs)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            verts.Add(Vector3.zero); norms.Add(Vector3.forward); uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                verts.Add(new Vector3(ca * radius, sa * radius, 0)); norms.Add(Vector3.forward);
                uvs.Add(new Vector2(ca * 0.5f + 0.5f, sa * 0.5f + 0.5f));
            }
            for (int i = 1; i <= segs; i++) { tris.Add(0); tris.Add(i); tris.Add(i + 1); }
            return Build("Circle", verts, norms, uvs, tris);
        }

        /// <summary>
        /// A quad for the lettered plaques, with U running right-to-left.
        ///
        /// three.js and Unity build the same rotation matrices, but their camera bases are
        /// mirrored: a three.js camera looking down -Z has screen-right = world +X, while a
        /// Unity camera looking down -Z has screen-right = world -X. Every position, angle
        /// and axis in this port is quoted verbatim from the web build, so the entire scene
        /// renders as its own mirror image — invisible on a four-fold-symmetric dreidel, a
        /// circular table and symmetric candles, but fatal for Hebrew glyphs. Flipping U
        /// here corrects the one thing that actually has a handedness, in one place, instead
        /// of negating a Z or a yaw at forty call sites and losing the ability to read this
        /// code against the original.
        /// </summary>
        public static Mesh PlaqueQuad(float w, float h)
        {
            var m = Quad(w, h);
            var uv = m.uv;
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(1f - uv[i].x, uv[i].y);
            m.uv = uv;
            m.name = "PlaqueQuad";
            return m;
        }

        public static Mesh Quad(float w, float h)
        {
            var m = new Mesh { name = "Quad" };
            float hw = w * 0.5f, hh = h * 0.5f;
            m.vertices = new[] { new Vector3(-hw,-hh,0), new Vector3(hw,-hh,0), new Vector3(hw,hh,0), new Vector3(-hw,hh,0) };
            m.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            m.uv = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// Box split into six submeshes in three.js face order — +X, -X, +Y, -Y, +Z, -Z —
        /// which is what lets the Oil Miracle's fill use a different material for its
        /// surface, its sides and its base.
        /// </summary>
        public static Mesh BoxSixMaterials(float w, float h, float d)
        {
            float x = w * 0.5f, y = h * 0.5f, z = d * 0.5f;
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var faces = new List<int[]>();

            System.Action<Vector3, Vector3, Vector3, Vector3, Vector3> add =
                (a, b, c, dd, n) =>
                {
                    int i = verts.Count;
                    verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(dd);
                    for (int k = 0; k < 4; k++) norms.Add(n);
                    uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                    uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                    faces.Add(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                };

            add(new Vector3(x,-y, z), new Vector3(x,-y,-z), new Vector3(x, y,-z), new Vector3(x, y, z), Vector3.right);
            add(new Vector3(-x,-y,-z), new Vector3(-x,-y, z), new Vector3(-x, y, z), new Vector3(-x, y,-z), Vector3.left);
            add(new Vector3(-x, y, z), new Vector3(x, y, z), new Vector3(x, y,-z), new Vector3(-x, y,-z), Vector3.up);
            add(new Vector3(-x,-y,-z), new Vector3(x,-y,-z), new Vector3(x,-y, z), new Vector3(-x,-y, z), Vector3.down);
            add(new Vector3(-x,-y, z), new Vector3(x,-y, z), new Vector3(x, y, z), new Vector3(-x, y, z), Vector3.forward);
            add(new Vector3(x,-y,-z), new Vector3(-x,-y,-z), new Vector3(-x, y,-z), new Vector3(x, y,-z), Vector3.back);

            var m = new Mesh { name = "Box6" };
            m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs);
            m.subMeshCount = 6;
            for (int i = 0; i < 6; i++) m.SetTriangles(faces[i], i);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// A box in one submesh, for props that wear a single material. (BoxSixMaterials
        /// splits the faces so the Oil Miracle's fill and the dice can dress each one.)
        /// </summary>
        public static Mesh Box(float w, float h, float d)
        {
            var src = BoxSixMaterials(w, h, d);
            var all = new List<int>();
            for (int i = 0; i < src.subMeshCount; i++) all.AddRange(src.GetTriangles(i));
            var m = new Mesh { name = "Box" };
            m.vertices = src.vertices;
            m.normals = src.normals;
            m.uv = src.uv;
            m.subMeshCount = 1;
            m.SetTriangles(all, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Inward-facing sphere for the sky dome (three.js BackSide equivalent).</summary>
        public static Mesh InvertedSphere(float radius, int widthSegs, int heightSegs)
        {
            var m = Sphere(radius, widthSegs, heightSegs);
            var tris = m.triangles;
            for (int i = 0; i < tris.Length; i += 3) { int t = tris[i]; tris[i] = tris[i + 2]; tris[i + 2] = t; }
            var n = m.normals;
            for (int i = 0; i < n.Length; i++) n[i] = -n[i];
            m.triangles = tris; m.normals = n; m.name = "SkyDome";
            return m;
        }

        static Mesh Build(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Split every triangle into its own vertices, for the flat-shaded parts.</summary>
        public static Mesh Faceted(Mesh src)
        {
            var sv = src.vertices; var st = src.triangles; var su = src.uv;
            var verts = new Vector3[st.Length];
            var norms = new Vector3[st.Length];
            var uvs = new Vector2[st.Length];
            var tris = new int[st.Length];
            for (int i = 0; i < st.Length; i += 3)
            {
                Vector3 a = sv[st[i]], b = sv[st[i + 1]], c = sv[st[i + 2]];
                var n = Vector3.Cross(b - a, c - a).normalized;
                for (int k = 0; k < 3; k++)
                {
                    verts[i + k] = sv[st[i + k]];
                    norms[i + k] = n;
                    uvs[i + k] = su != null && su.Length > st[i + k] ? su[st[i + k]] : Vector2.zero;
                    tris[i + k] = i + k;
                }
            }
            var m = new Mesh { name = src.name + "Flat" };
            if (verts.Length > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.vertices = verts; m.normals = norms; m.uv = uvs; m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }
    }
}
