using UnityEngine;
using UnityEngine.UI;
using DreidelRoyale.Core;
using DreidelRoyale.Visual;

namespace DreidelRoyale.UI
{
    /// <summary>
    /// The Backyard's full-2D world. Pair the Backyard table with the Blue Pup dreidel and the
    /// 3D diorama is hidden entirely: this draws the whole scene flat — playroom, doorway,
    /// bunting, rug, toys — with a cartoon dreidel driven by the REAL game state, so spins,
    /// results and the pot all stay authentic. It is a second renderer, not a filter.
    ///
    /// Everything is emitted as one procedural mesh. The source paints it to a canvas, but a
    /// full-screen CPU repaint costs a second on a phone and has to happen again on every
    /// rotation; flat colour, hard edges and simple shapes are exactly what a mesh is good at.
    ///
    /// Coordinates below are written in the source's canvas space — origin top-left, Y down —
    /// and flipped once on the way out, so this reads against the original rather than against
    /// a mirror of it.
    /// </summary>
    public class FlatStage : MaskableGraphic
    {
        public DreidelView View;
        public GameController GC;

        Text _letter;
        float _t;
        float _prevRot, _speed, _tilt, _dy;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void Build()
        {
            var go = UIKit.Node("flat-letter", rectTransform);
            _letter = UIKit.Label(go.transform, "ג", 64, Hex.To("#6aaede"), TextAnchor.MiddleCenter, true);
            UIKit.Stretch(_letter.gameObject);
            UIKit.Rect(go).sizeDelta = new Vector2(200, 200);
            gameObject.SetActive(false);
        }

        public void SetActive(bool on)
        {
            if (gameObject.activeSelf == on) return;
            gameObject.SetActive(on);
            // The diorama is hidden entirely, not dimmed: two dreidels on screen at once would
            // be worse than either alone.
            if (View != null && View.Rig != null && View.Rig.World != null)
                View.Rig.World.gameObject.SetActive(!on);
        }

        void Update()
        {
            if (!gameObject.activeSelf) return;
            _t += Time.deltaTime;
            SetVerticesDirty();
        }

        // ---------------------------------------------------------------
        float _w, _h;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            _w = r.width; _h = r.height;
            if (_w < 1f || _h < 1f) return;

            PaintRoom(vh);
            PaintProps(vh);
            PaintPot(vh);
            PaintDreidel(vh);
        }

        // ---- the room ----
        void PaintRoom(VertexHelper vh)
        {
            float W = _w, H = _h;
            float ceilY = H * 0.10f, floorY = H * 0.60f;

            Rect(vh, 0, 0, W, ceilY, "#fbf1d8");                 // ceiling
            Rect(vh, 0, ceilY - 4, W, 4, "#e8d8ac");             // trim
            Rect(vh, 0, ceilY, W, floorY - ceilY, "#faeab0");    // wall

            var dot = Hex.To("rgba(255,255,255,0.5)");
            for (int i = 0; i < 40; i++)
                Circle(vh, (i * 173) % W, ceilY + ((i * 97) % (floorY - ceilY)), 3, dot, 8);

            // doorway to the garden
            float dw = W * 0.64f, dx0 = (W - dw) * 0.5f, dy0 = H * 0.16f;
            float dh = floorY - dy0, fr = Mathf.Max(8f, W * 0.02f);
            Rect(vh, dx0 - fr, dy0 - fr, dw + fr * 2f, dh + fr, "#8a5a30");   // frame
            Rect(vh, dx0, dy0, dw, dh, "#7ec3ea");                            // sky

            var white = Hex.To("#ffffff");
            PaintCloud(vh, dx0 + dw * 0.22f, dy0 + dh * 0.16f, 1f, white);
            PaintCloud(vh, dx0 + dw * 0.72f, dy0 + dh * 0.10f, 0.8f, white);

            Rect(vh, dx0, dy0 + dh * 0.62f, dw, dh * 0.26f, "#a8d888");       // garden

            // a fruit tree peeking through
            float tx = dx0 + dw * 0.76f, ty = dy0 + dh * 0.40f;
            Rect(vh, tx - 6, dy0 + dh * 0.42f, 12, dh * 0.3f, "#8a5a30");
            var leaf = Hex.To("#7dbb5e");
            Circle(vh, tx, ty - 8, 34, leaf, 18);
            Circle(vh, tx - 26, ty + 6, 24, leaf, 14);
            Circle(vh, tx + 26, ty + 8, 24, leaf, 14);
            var fruit = Hex.To("#e8503c");
            Circle(vh, tx - 14, ty - 16, 4, fruit, 8);
            Circle(vh, tx + 10, ty - 4, 4, fruit, 8);
            Circle(vh, tx + 22, ty + 10, 4, fruit, 8);
            Circle(vh, tx - 24, ty + 8, 4, fruit, 8);

            // railing
            float ry0 = dy0 + dh * 0.80f;
            Rect(vh, dx0, ry0, dw, dh * 0.20f, "#fdfdf6");
            for (float p = dx0 + 8; p < dx0 + dw - 8; p += 22)
                Rect(vh, p, ry0 + 4, 8, dh * 0.20f - 8, "#e2ddc8");
            Rect(vh, dx0, ry0, dw, 6, "#fdfdf6");

            PaintBunting(vh, ceilY);
            PaintFloor(vh, floorY);
            PaintRug(vh);
        }

        void PaintCloud(VertexHelper vh, float cx, float cy, float s, Color c)
        {
            Circle(vh, cx, cy, 16 * s, c, 14);
            Circle(vh, cx + 14 * s, cy + 4 * s, 12 * s, c, 12);
            Circle(vh, cx - 14 * s, cy + 4 * s, 12 * s, c, 12);
            Circle(vh, cx + 6 * s, cy - 8 * s, 10 * s, c, 12);
            Rect(vh, cx - 20 * s, cy + 2 * s, 40 * s, 9 * s, c);
        }

        void PaintBunting(VertexHelper vh, float ceilY)
        {
            float W = _w, H = _h;
            float by = ceilY + H * 0.025f;
            var line = Hex.To("#c8a86a");
            var cols = new[] { "#e8503c", "#6aaede", "#ffd24a", "#f2a8c0", "#8fca6e" };

            float prevX = 0f, prevY = by;
            for (float px = 0; px <= W; px += 8)
            {
                float y = by + Mathf.Sin(px / W * Mathf.PI * 3f) * H * 0.012f;
                if (px > 0) Bar(vh, prevX, prevY, px, y, 2f, line);
                prevX = px; prevY = y;
            }

            int nf = Mathf.FloorToInt(W / 54f);
            for (int f = 0; f <= nf; f++)
            {
                float fx = 27 + f * 54;
                float fy = by + Mathf.Sin(fx / W * Mathf.PI * 3f) * H * 0.012f;
                Tri(vh, fx - 11, fy, fx + 11, fy, fx, fy + 20, Hex.To(cols[f % cols.Length]));
            }
        }

        void PaintFloor(VertexHelper vh, float floorY)
        {
            float W = _w, H = _h;
            const int rows = 6;
            float rh = (H - floorY) / rows;
            var planks = new[] { "#b4743e", "#ac6c38", "#ba7a44", "#a86836" };
            var seam = Hex.To("rgba(90,50,20,0.5)");
            for (int r = 0; r < rows; r++)
            {
                Rect(vh, 0, floorY + r * rh, W, rh, planks[r % planks.Length]);
                Rect(vh, 0, floorY + r * rh, W, 3, seam);
                Rect(vh, ((r * 367) % Mathf.Max(1f, W - 80)) + 40, floorY + r * rh, 3, rh, seam);
            }
        }

        void PaintRug(VertexHelper vh)
        {
            float W = _w, H = _h;
            float rcx = W * 0.5f, rcy = H * 0.80f;
            float rrx = Mathf.Min(W * 0.40f, 260f), rry = rrx * 0.32f;
            Ellipse(vh, rcx, rcy, rrx, rry, Hex.To("#9ccd6a"), 40);
            Ellipse(vh, rcx, rcy, rrx * 0.7f, rry * 0.7f, Hex.To("#f2eecb"), 36);
            var dash = Hex.To("#4a4438");
            for (int d = 0; d < 12; d++)
            {
                float a = d / 12f * Mathf.PI * 2f;
                RotRect(vh, rcx + Mathf.Cos(a) * rrx * 0.58f, rcy + Mathf.Sin(a) * rry * 0.58f,
                        18, 8, a, dash);
            }
        }

        // ---- toys and balloons ----
        void PaintProps(VertexHelper vh)
        {
            float W = _w, H = _h;

            Block(vh, W * 0.08f, H * 0.86f, Mathf.Min(W * 0.10f, 64f), "#8fb8e8", "#6f98c8");
            Block(vh, W * 0.16f, H * 0.90f, Mathf.Min(W * 0.075f, 48f), "#ffd24a", "#dcaa2a");
            Block(vh, W * 0.82f, H * 0.89f, Mathf.Min(W * 0.085f, 56f), "#f2a8c0", "#d287a0");

            float bx = W * 0.88f, by = H * 0.82f, br = Mathf.Min(W * 0.06f, 40f);
            Circle(vh, bx, by, br, Hex.To("#e8503c"), 22);
            Circle(vh, bx - br * 0.35f, by - br * 0.35f, br * 0.22f, Hex.To("#ffffff"), 12);

            // balloons on their strings, bobbing out of phase
            Balloon(vh, W * 0.13f, H * 0.30f + Mathf.Sin(_t * 0.8f) * H * 0.012f,
                    Mathf.Min(W * 0.055f, 36f), "#ffd24a");
            Balloon(vh, W * 0.87f, H * 0.24f + Mathf.Sin(_t * 0.66f + 2.1f) * H * 0.014f,
                    Mathf.Min(W * 0.05f, 32f), "#e8503c");
        }

        void Block(VertexHelper vh, float bx, float by, float s, string col, string col2)
        {
            Rect(vh, bx + s * 0.12f, by - s * 1.12f, s, s, col2);
            Rect(vh, bx, by - s, s, s, col);
        }

        void Balloon(VertexHelper vh, float bx, float by, float br, string col)
        {
            var str = Hex.To("#f2ead2");
            // the string sags away and back, which is what stops it reading as a stick
            float px = bx, py = by + br;
            for (int i = 1; i <= 8; i++)
            {
                float u = i / 8f;
                float qx = Mathf.Lerp(Mathf.Lerp(bx, bx + 8, u), Mathf.Lerp(bx + 8, bx - 4, u), u);
                float qy = Mathf.Lerp(Mathf.Lerp(by + br, by + br + 30, u),
                                      Mathf.Lerp(by + br + 30, by + br + 58, u), u);
                Bar(vh, px, py, qx, qy, 2f, str);
                px = qx; py = qy;
            }
            Ellipse(vh, bx, by, br * 0.9f, br, Hex.To(col), 24);
            Circle(vh, bx - br * 0.3f, by - br * 0.3f, br * 0.2f, Hex.To("#ffffff"), 12);
        }

        /// <summary>Flat coin stacks on the rug, mirroring the real pot.</summary>
        void PaintPot(VertexHelper vh)
        {
            float W = _w, H = _h;
            int pot = GC != null ? Mathf.Min(GC.G.Pot, 14) : 0;
            var edge = Hex.To("#d8a825");
            var face = Hex.To("#ffd24a");
            for (int i = 0; i < pot; i++)
            {
                int st = i % 2, lvl = i / 2;
                float cx = W * 0.5f + (st == 1 ? W * 0.26f : -W * 0.26f);
                float cy = H * 0.80f - lvl * 9;
                Ellipse(vh, cx, cy + 3, 16, 7, edge, 16);
                Ellipse(vh, cx, cy, 16, 7, face, 16);
            }
        }

        // ---- the dreidel, driven by the real game ----
        void PaintDreidel(VertexHelper vh)
        {
            float W = _w, H = _h;
            float rot = View != null ? View.GetRotDeg() : 0f;
            bool lying = View != null && View.IsLying;
            bool spinning = GC != null && GC.IsSpinning;

            // Squash follows actual spin SPEED, not just the angle, so it fades out naturally
            // as the spin dies and never squishes during the topple.
            float dr = Mathf.Abs(Mathf.DeltaAngle(_prevRot, rot));
            _prevRot = rot;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            _speed += (Mathf.Min(1f, dr / (10f * 60f * dt)) - _speed) * 0.12f;

            float s = Mathf.Min(W * 0.42f, H * 0.26f);
            float sq = _speed * 0.6f;
            float sx = 1f - sq * (1f - Mathf.Abs(Mathf.Cos(rot * Mathf.Deg2Rad)));

            // the fall eases in: tilt and settle-height chase their targets
            float tiltTarget = lying ? 1.25f : (spinning ? Mathf.Sin(_t * 11f) * 0.06f * _speed : 0f);
            _tilt += (tiltTarget - _tilt) * 0.14f;
            _dy += ((lying ? s * 0.34f : 0f) - _dy) * 0.14f;

            float cx = W * 0.5f;
            float cy = H * 0.46f + (spinning || lying ? 0f : Mathf.Sin(_t * 1.4f) * s * 0.02f);

            // flat shadow on the rug
            Ellipse(vh, W * 0.5f, H * 0.80f,
                    s * 0.42f + (_dy / Mathf.Max(s * 0.34f, 1e-3f)) * s * 0.14f, s * 0.13f,
                    Hex.To("rgba(90,70,40,0.20)"), 28);

            var pose = new Pose2(cx, cy + _dy, _tilt, sx);

            // handle + knob
            float hw = s * 0.10f, hh = s * 0.42f;
            PoseRect(vh, pose, -hw * 0.5f, -s * 0.5f - hh, hw, hh + s * 0.1f, Hex.To("#f2ead2"));
            PoseCircle(vh, pose, 0, -s * 0.5f - hh, s * 0.10f, Hex.To("#f2ead2"), 16);

            // body, with the tan ear-patch
            PoseRoundRect(vh, pose, -s * 0.5f, -s * 0.5f, s, s, s * 0.16f, Hex.To("#aed3ee"));
            PoseCircle(vh, pose, -s * 0.36f, -s * 0.36f, s * 0.10f, Hex.To("#d8a86a"), 14);

            // tip
            PoseTri(vh, pose, -s * 0.34f, s * 0.5f - 2, s * 0.34f, s * 0.5f - 2, 0, s * 1.02f,
                    Hex.To("#7fb0d8"));

            // face panel and its gold ring
            PoseRoundRect(vh, pose, -s * 0.395f, -s * 0.395f, s * 0.79f, s * 0.79f, s * 0.13f,
                          Hex.To("#f2d78a"));
            PoseRoundRect(vh, pose, -s * 0.37f, -s * 0.37f, s * 0.74f, s * 0.74f, s * 0.12f,
                          Hex.To("#fdfdf6"));

            PlaceLetter(rot, pose, s);

            // motion arcs while it's genuinely whirring - they fade with the spin
            if (_speed > 0.12f)
            {
                var arc = new Color(106 / 255f, 174 / 255f, 222 / 255f, 0.55f * _speed);
                for (int k = 0; k < 2; k++)
                {
                    float sgn = k == 0 ? -1f : 1f, ph = k == 0 ? 0.3f : -0.4f;
                    Arc(vh, cx, cy, s * 0.72f, _t * 7f * sgn + ph, 0.7f * sgn, 3f, arc);
                }
            }
        }

        /// <summary>
        /// The letter is real text, so it rides a rotated child rather than being emitted into
        /// the mesh - and it is the same glyph the game's own face resolution names.
        /// </summary>
        void PlaceLetter(float rot, Pose2 pose, float s)
        {
            if (_letter == null) return;
            var side = Rules.ResolveFace(rot);
            _letter.text = side.Char;
            _letter.fontSize = Mathf.Max(8, Mathf.RoundToInt(s * 0.42f));

            var rt = _letter.rectTransform.parent as RectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var p = ToUnity(pose.X, pose.Y + s * 0.03f);
            rt.anchoredPosition = p;
            rt.localRotation = Quaternion.Euler(0, 0, pose.Rot * Mathf.Rad2Deg);
            rt.localScale = new Vector3(pose.ScaleX, 1f, 1f);
            rt.sizeDelta = new Vector2(s, s);
        }

        // ---------------------------------------------------------------
        //  emit helpers - canvas space in, Unity space out
        // ---------------------------------------------------------------
        struct Pose2
        {
            public float X, Y, Rot, ScaleX;
            public Pose2(float x, float y, float rot, float sx) { X = x; Y = y; Rot = rot; ScaleX = sx; }

            /// <summary>A point in the dreidel's own frame, brought back to canvas space.</summary>
            public Vector2 Apply(float lx, float ly)
            {
                lx *= ScaleX;
                float c = Mathf.Cos(Rot), s = Mathf.Sin(Rot);
                return new Vector2(X + lx * c - ly * s, Y + lx * s + ly * c);
            }
        }

        Vector2 ToUnity(float cx, float cy) { return new Vector2(cx - _w * 0.5f, _h * 0.5f - cy); }

        void Vert(VertexHelper vh, float cx, float cy, Color c)
        {
            vh.AddVert(ToUnity(cx, cy), c, new Vector2(0.5f, 0.5f));
        }

        void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 cc, Vector2 d, Color col)
        {
            if (col.a <= 0.003f) return;
            int i = vh.currentVertCount;
            Vert(vh, a.x, a.y, col); Vert(vh, b.x, b.y, col);
            Vert(vh, cc.x, cc.y, col); Vert(vh, d.x, d.y, col);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        void Rect(VertexHelper vh, float x, float y, float w, float h, string hex)
        { Rect(vh, x, y, w, h, Hex.To(hex)); }

        void Rect(VertexHelper vh, float x, float y, float w, float h, Color c)
        {
            Quad(vh, new Vector2(x, y), new Vector2(x + w, y),
                     new Vector2(x + w, y + h), new Vector2(x, y + h), c);
        }

        void RotRect(VertexHelper vh, float cx, float cy, float w, float h, float rot, Color c)
        {
            float co = Mathf.Cos(rot), si = Mathf.Sin(rot);
            System.Func<float, float, Vector2> p = (lx, ly) =>
                new Vector2(cx + lx * co - ly * si, cy + lx * si + ly * co);
            Quad(vh, p(-w * 0.5f, -h * 0.5f), p(w * 0.5f, -h * 0.5f),
                     p(w * 0.5f, h * 0.5f), p(-w * 0.5f, h * 0.5f), c);
        }

        void Tri(VertexHelper vh, float x0, float y0, float x1, float y1, float x2, float y2, Color c)
        {
            if (c.a <= 0.003f) return;
            int i = vh.currentVertCount;
            Vert(vh, x0, y0, c); Vert(vh, x1, y1, c); Vert(vh, x2, y2, c);
            vh.AddTriangle(i, i + 1, i + 2);
        }

        void Circle(VertexHelper vh, float cx, float cy, float r, Color c, int segs)
        { Ellipse(vh, cx, cy, r, r, c, segs); }

        void Ellipse(VertexHelper vh, float cx, float cy, float rx, float ry, Color c, int segs)
        {
            if (c.a <= 0.003f || rx <= 0f || ry <= 0f) return;
            int centre = vh.currentVertCount;
            Vert(vh, cx, cy, c);
            for (int i = 0; i <= segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                Vert(vh, cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry, c);
            }
            for (int i = 0; i < segs; i++) vh.AddTriangle(centre, centre + 1 + i, centre + 2 + i);
        }

        void Bar(VertexHelper vh, float x0, float y0, float x1, float y1, float t, Color c)
        {
            var d = new Vector2(x1 - x0, y1 - y0);
            float len = d.magnitude;
            if (len < 0.001f) return;
            var n = new Vector2(-d.y, d.x) / len * (t * 0.5f);
            Quad(vh, new Vector2(x0 - n.x, y0 - n.y), new Vector2(x1 - n.x, y1 - n.y),
                     new Vector2(x1 + n.x, y1 + n.y), new Vector2(x0 + n.x, y0 + n.y), c);
        }

        void Arc(VertexHelper vh, float cx, float cy, float r, float from, float sweep, float t, Color c)
        {
            const int segs = 10;
            float px = cx + Mathf.Cos(from) * r, py = cy + Mathf.Sin(from) * r;
            for (int i = 1; i <= segs; i++)
            {
                float a = from + sweep * i / segs;
                float qx = cx + Mathf.Cos(a) * r, qy = cy + Mathf.Sin(a) * r;
                Bar(vh, px, py, qx, qy, t, c);
                px = qx; py = qy;
            }
        }

        // ---- posed variants, for the parts that turn with the dreidel ----
        void PoseRect(VertexHelper vh, Pose2 p, float x, float y, float w, float h, Color c)
        {
            Quad(vh, p.Apply(x, y), p.Apply(x + w, y), p.Apply(x + w, y + h), p.Apply(x, y + h), c);
        }

        void PoseTri(VertexHelper vh, Pose2 p, float x0, float y0, float x1, float y1,
                     float x2, float y2, Color c)
        {
            if (c.a <= 0.003f) return;
            var a = p.Apply(x0, y0); var b = p.Apply(x1, y1); var d = p.Apply(x2, y2);
            int i = vh.currentVertCount;
            Vert(vh, a.x, a.y, c); Vert(vh, b.x, b.y, c); Vert(vh, d.x, d.y, c);
            vh.AddTriangle(i, i + 1, i + 2);
        }

        void PoseCircle(VertexHelper vh, Pose2 p, float cx, float cy, float r, Color c, int segs)
        {
            if (c.a <= 0.003f) return;
            var centre = p.Apply(cx, cy);
            int start = vh.currentVertCount;
            Vert(vh, centre.x, centre.y, c);
            for (int i = 0; i <= segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                var q = p.Apply(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
                Vert(vh, q.x, q.y, c);
            }
            for (int i = 0; i < segs; i++) vh.AddTriangle(start, start + 1 + i, start + 2 + i);
        }

        /// <summary>A rounded rectangle as a centre quad plus four sides and four corner fans.</summary>
        void PoseRoundRect(VertexHelper vh, Pose2 p, float x, float y, float w, float h, float r, Color c)
        {
            r = Mathf.Min(r, Mathf.Min(w, h) * 0.5f);
            PoseRect(vh, p, x + r, y, w - 2 * r, h, c);
            PoseRect(vh, p, x, y + r, r, h - 2 * r, c);
            PoseRect(vh, p, x + w - r, y + r, r, h - 2 * r, c);

            var corners = new[]
            {
                new Vector3(x + r, y + r, Mathf.PI),
                new Vector3(x + w - r, y + r, -Mathf.PI / 2f),
                new Vector3(x + w - r, y + h - r, 0f),
                new Vector3(x + r, y + h - r, Mathf.PI / 2f)
            };
            const int segs = 5;
            foreach (var k in corners)
            {
                var centre = p.Apply(k.x, k.y);
                int start = vh.currentVertCount;
                Vert(vh, centre.x, centre.y, c);
                for (int i = 0; i <= segs; i++)
                {
                    float a = k.z + i / (float)segs * (Mathf.PI / 2f);
                    var q = p.Apply(k.x + Mathf.Cos(a) * r, k.y + Mathf.Sin(a) * r);
                    Vert(vh, q.x, q.y, c);
                }
                for (int i = 0; i < segs; i++) vh.AddTriangle(start, start + 1 + i, start + 2 + i);
            }
        }
    }
}
