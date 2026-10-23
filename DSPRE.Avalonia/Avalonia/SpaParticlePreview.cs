using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using global::Avalonia;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Draws <see cref="SpaSimulator"/> particles to a transparent bitmap as the battle particle camera sees them, for
    /// overlay on the battle scene. A texture that can't be decoded draws as a soft dot.
    /// </summary>
    public sealed class SpaParticlePreview
    {
        public readonly struct Layer
        {
            public readonly SpaSimulator Sim;
            public readonly IReadOnlyList<SpaTexture> Textures;   // archive textures; each particle's TexNo indexes this
            public readonly SpaTexture BaseTex;        // fallback (the emitter's base TexNo) when no texture animation
            public readonly double CenterX, CenterY;   // where this emitter sits (attacker / defender, per callback)
            public readonly int DrawType;              // draw type: 1 and 3 are directional billboards, turned along the velocity
            public readonly bool RepeatS, RepeatT;     // emitter tex_repeat ≥ 1; with a texture's flip bit → mirror
            public readonly double Aspect, DbbScale;   // base.aspect (sclX = sclY×aspect); directional stretch along vel
            public readonly double OffsetX, OffsetY;    // base.offset_x/offset_y: quad centre offset in half-size units
            // Depth of this emitter's anchor plane in px-units (+z toward the camera): the player mon sits at
            // ≈0 (0x40) and the ENEMY at −30.5 (−5248/172), farther from the camera,
            // so enemy-side effects render ≈75% the size (the real game's perspective).
            public readonly double BaseZ;
            // The camera-reverse emit callbacks (cb 1/2) with an enemy attacker turn the particle camera 180°,
            // mirroring the whole scene (and flipping rotation chirality).
            public readonly bool ViewReversed;
            public readonly bool FlipS, FlipT;         // flip-texture-S/T: mirror the texture on the quad
            // The full emitter resource, for draw parameters that vary per particle KIND (child drawType /
            // polygon rot-axis / reference plane / dpolFaceEmitter). Null only in legacy callers.
            public readonly SpaEmitter Em;
            // Depth changes neither size nor place.
            public readonly bool Orthographic;
            public Layer(SpaSimulator sim, IReadOnlyList<SpaTexture> textures, SpaTexture baseTex, double centerX, double centerY,
                         int drawType, bool repeatS = false, bool repeatT = false, double aspect = 1.0, double dbbScale = 0.0,
                         double offsetX = 0.0, double offsetY = 0.0, double baseZ = 0.0, bool viewReversed = false,
                         bool flipS = false, bool flipT = false, SpaEmitter em = null, bool orthographic = false)
            { Sim = sim; Textures = textures; BaseTex = baseTex; CenterX = centerX; CenterY = centerY; DrawType = drawType; RepeatS = repeatS; RepeatT = repeatT; Aspect = aspect <= 0 ? 1.0 : aspect; DbbScale = dbbScale; OffsetX = offsetX; OffsetY = offsetY; BaseZ = baseZ; ViewReversed = viewReversed; FlipS = flipS; FlipT = flipT; Em = em; Orthographic = orthographic; }

            public SpaTexture TexFor(int texNo)
                => (Textures != null && texNo >= 0 && texNo < Textures.Count) ? Textures[texNo] : BaseTex;
        }

        public int Width { get; }
        public int Height { get; }

        public double WorldToPx { get; set; } = 1.0;      // particle pixels → screen pixels (positions already /172)
        // Billboard half-size px = base_scl × this: the particle library's quad is ±1.0 world units scaled by base_scl,
        // and world to pixels is /172, so 4096/172 ≈ 23.8.
        public double ScalePx { get; set; } = 4096.0 / 172.0;
        // The battle particle camera sits at z +4.0 looking at the origin; at the z=0 plane that's 4096/172 px a unit,
        // and depth only matters off it (the opponent sits at z −1.28).
        private const double EyeDist = 4.0;
        private const double PxPerUnit = 4096.0 / 172.0;
        public double DotRadiusPx { get; set; } = 4.0;    // soft-dot fallback radius

        private readonly List<Layer> _layers = new List<Layer>();
        private readonly byte[] _buf;

        public SpaParticlePreview(int width = 256, int height = 192)
        {
            Width = width; Height = height;
            _buf = new byte[width * height * 4];
        }

        /// <summary>Adds an emitter layer (the timeline interpreter calls this as CreateEmitter commands fire).</summary>
        public void AddLayer(Layer l) => _layers.Add(l);

        /// <summary>Draws translucent polygons as the DS does: sorted by Y, strict depth test, depth from opaque pixels only.</summary>
        public bool DsRaster { get; set; }

        private readonly record struct Quad(SpaTexture Tex, double Cx, double Cy, double AxX, double AxY, double AyX, double AyY,
            byte R, byte G, byte B, double Alpha, int TileS, int TileT, bool FlipS, bool FlipT, double Depth, int YTop, int YBottom, bool Translucent);

        private readonly List<Quad> _quads = new List<Quad>();
        private double[] _depth;
        private int[] _translucentId;

        public bool HasEmitters => _layers.Count > 0;
        public bool AllFinished => _layers.TrueForAll(l => l.Sim.Finished);

        public void Step() { foreach (Layer l in _layers) l.Sim.Step(); }

        /// <summary>Every live particle's position, for callers that look at particles without drawing them.</summary>
        public IEnumerable<SpaParticleState> LiveParticles()
        {
            foreach (Layer l in _layers)
                foreach (SpaParticleState p in l.Sim.Particles())
                    yield return p;
        }

        public WriteableBitmap RenderFrame()
        {
            RenderBgra();
            WriteableBitmap wb = new WriteableBitmap(new PixelSize(Width, Height), new Vector(96, 96),
                PixelFormat.Bgra8888, AlphaFormat.Premul);
            using (ILockedFramebuffer fb = wb.Lock())
            {
                int rowBytes = fb.RowBytes;
                if (rowBytes == Width * 4)
                    Marshal.Copy(_buf, 0, fb.Address, _buf.Length);
                else
                    for (int y = 0; y < Height; y++)
                        Marshal.Copy(_buf, y * Width * 4, fb.Address + y * rowBytes, Width * 4);
            }
            return wb;
        }

        /// <summary>The frame as premultiplied BGRA rows, for drawing over another picture. The buffer is reused.</summary>
        public byte[] RenderBgra()
        {
            Array.Clear(_buf, 0, _buf.Length);
            _quads.Clear();
            foreach (Layer layer in _layers)
            {
                // The real battle particle camera: eye at
                // (0,0,+4.0) world units looking at the origin, Y up. px-per-world-unit at the z=0 plane is
                // 4096/172 , so the perspective factor for a particle at depth z (px-units,
                // +z toward camera) is f = 4 / (4 − z/23.81): 1.0 exactly at the player plane, ≈0.757 at the
                // enemy plane , matching the game's smaller enemy-side rendering.
                double fBase = layer.Orthographic ? 1.0 : EyeDist / (EyeDist - layer.BaseZ / PxPerUnit);
                if (fBase <= 0) continue;   // anchor behind the camera, nothing sane to draw
                double mirror = layer.ViewReversed ? -1.0 : 1.0;
                foreach (SpaParticleState p in layer.Sim.Particles())
                {
                    // Children draw with their OWN child-resource draw configuration; draw-type 4 uses the same
                    // path as draw-type 3.
                    int drawType = p.IsChild && layer.Em != null ? layer.Em.ChildDrawType : layer.DrawType;
                    bool directional = drawType == 1;             // the directional-billboard draw type
                    bool polygonType = drawType >= 2 && layer.Em != null;   // polygon draw types
                    // The quad spans tileS by tileT texture widths; the texture's repeat and flip bits decide what lies past the first.
                    SpaTexture tex = layer.TexFor(p.TexNo);
                    bool textured = tex != null && tex.Rgba != null && tex.Width > 0 && tex.Height > 0;
                    int tileS = layer.Em?.TileS ?? (layer.RepeatS ? 2 : 1);
                    int tileT = layer.Em?.TileT ?? (layer.RepeatT ? 2 : 1);
                    // Reconstruct the world-plane position from the screen anchor (exact at the anchor plane),
                    // add the particle's own offsets, then project through the camera. +Y up → screen Y flips.
                    double zTot = layer.BaseZ + p.Z;
                    double zEff = layer.ViewReversed ? -zTot : zTot;
                    double depth = EyeDist - zEff / PxPerUnit;
                    if (!layer.Orthographic && depth < 0.25) continue;   // behind / clipping the camera
                    // The orthographic view box matches the perspective one at the target, so scale stays at the anchor plane's.
                    double f = layer.Orthographic ? 1.0 : EyeDist / depth;
                    double worldX = ((layer.CenterX - Width / 2.0) / fBase + p.X * WorldToPx) * mirror;
                    double worldY = (Height / 2.0 - layer.CenterY) / fBase + p.Y * WorldToPx;
                    double px = Width / 2.0 + worldX * f;
                    double py = Height / 2.0 - worldY * f;
                    // Quads draw at full size, as screen-covering sheets like Ominous Wind need; BlitQuad clips to the
                    // screen, so big ones cost nothing.
                    double sc = p.Scale <= 0 ? 1.0 : p.Scale;
                    // Per-axis scales (the scale-anim-direction flag): a Y-only scale anim extends the
                    // quad lengthwise while its width stays base_scl (Seed Flare slashes). Raw values, no
                    // fallback: scale 0 at birth means a hairline quad, exactly like the hardware.
                    // × f: the perspective factor shrinks/grows the quad with its depth, like the real camera.
                    double scX = p.ScaleForX > 0 ? p.ScaleForX : sc;
                    double scY = p.ScaleForY > 0 ? p.ScaleForY : sc;
                    double sclY = Math.Clamp(scY * ScalePx * f, 1, 4096);
                    double sclX = Math.Clamp(scX * layer.Aspect * ScalePx * f, 1, 4096);
                    double axx, axy, ayx, ayy;   // local-X (texture S) and local-Y (texture T) half-axes, screen px
                    if (polygonType)
                    {
                        // Polygon / directional-polygon draw types: a WORLD-space quad (not a billboard).
                        // Basis = Scale(sclX,sclY,sclY) · Rot(polygonRotAxis: Y or the (1,1,1) diagonal) ·
                        // [· Orient(velocity | −position when dpolFaceEmitter)]; the quad spans local X and
                        // local Y (reference plane 0 = XY) or local X and local Z (plane 1 = XZ). Rendered with
                        // EXACT per-pixel ray-plane perspective (the DS projects the quad per-vertex).
                        SpaEmitter em2 = layer.Em;
                        int rotAxis = p.IsChild ? em2.ChildPolyRotAxis : em2.PolyRotAxis;
                        int refPlane = p.IsChild ? em2.ChildPolyRefPlane : em2.PolyRefPlane;
                        double[,] rm = rotAxis == 1 ? RotXYZ(p.Rotation) : RotY(p.Rotation);
                        if (drawType >= 3)
                        {
                            double dvx, dvy, dvz;
                            if (em2.DpolFaceEmitter) { dvx = -p.X; dvy = -p.Y; dvz = -p.Z; }
                            else { dvx = p.VX; dvy = p.VY; dvz = p.VZ; }
                            rm = Mul3(rm, Orient(dvx, dvy, dvz));
                        }
                        int second = refPlane == 0 ? 1 : 2;   // XY plane spans rows 0/1; XZ plane rows 0/2
                        // World-space half-axes + centre in px-units; a reversed (180°-turned) camera mirrors
                        // world X and Z of everything (positions already mirrored via worldX/zEff above).
                        double hxs = scX * layer.Aspect * ScalePx, hys = scY * ScalePx;
                        double[] A3 = { rm[0, 0] * hxs * mirror, rm[0, 1] * hxs, rm[0, 2] * hxs * mirror };
                        double[] B3 = { rm[second, 0] * hys * mirror, rm[second, 1] * hys, rm[second, 2] * hys * mirror };
                        double[] C3 = { worldX, worldY, zEff };
                        if (layer.OffsetX != 0 || layer.OffsetY != 0)
                            for (int k = 0; k < 3; k++) C3[k] += layer.OffsetX * A3[k] + layer.OffsetY * B3[k];
                        if (textured)
                            BlitQuad3D(tex, C3, A3, B3, p.R, p.G, p.B, p.Alpha, tileS, tileT, layer.FlipS, layer.FlipT, layer.Orthographic);
                        else
                            Splat((int)Math.Round(px), (int)Math.Round(py), Math.Max(1, (int)Math.Round(sc * DotRadiusPx * f)), p.R, p.G, p.B, p.Alpha);
                        continue;
                    }
                    else if (directional)
                    {
                        // Directional billboard: STILL a view-plane quad, local-Y along the SCREEN
                        // projection of the velocity, local-X perpendicular. The along-velocity stretch is
                        // VIEW-DEPENDENT: len = sclY·(1 + dbbScale·(1 − |v̂·look|)); a particle moving toward/
                        // away from the camera foreshortens to no stretch. cross(vel, look) == 0 (no screen
                        // velocity) → the particle is NOT drawn at all (the source returns).
                        double vsx = p.VX * mirror, vsy = -p.VY;
                        double vl = Math.Sqrt(vsx * vsx + vsy * vsy);
                        if (vl < 1e-9) continue;                 // exact source behaviour: skip
                        double vx = vsx / vl, vy = vsy / vl;
                        double v3 = Math.Sqrt(p.VX * p.VX + p.VY * p.VY + p.VZ * p.VZ);
                        double towardCam = v3 > 1e-9 ? Math.Abs(p.VZ) / v3 : 0.0;
                        double lenY = Math.Clamp(scY * (1.0 + layer.DbbScale * (1.0 - towardCam)) * ScalePx * f, 1, 4096);
                        ayx = vx * lenY; ayy = vy * lenY;        // along velocity (texture T)
                        axx = -vy * sclX; axy = vx * sclX;       // perpendicular (texture S)
                    }
                    else
                    {
                        axx = sclX; axy = 0; ayx = 0; ayy = sclY;   // axis-aligned billboard (type 0)
                    }
                    // init_rtt + rotation-anim spin, matching the hardware billboard build: localX =
                    // (cosθ·sclX, sinθ·sclX), localY = (−sinθ·sclY, cosθ·sclY) in Y-up view space; mapping to our
                    // Y-down screen gives a = −θ on X-right/Y-down axes. Do NOT re-tune this per move: for a +θ the
                    // quad's top tips screen-LEFT: that IS the game's math (cross-checked against the particle-space
                    // mon positions: player (−X,−Y), enemy (+X,+Y), +Y up, no mirror). Side mirroring in-game comes
                    // from the camera-reverse emit callbacks (turn the camera 180°), not from the billboard rotation.
                    if (p.Rotation != 0 && !polygonType)   // polygons carry their rotation inside the 3D basis above
                    {
                        // A reversed (180°-turned) camera flips rotation chirality along with the X mirror.
                        double a = -p.Rotation * mirror, ca = Math.Cos(a), sa = Math.Sin(a);
                        double nxx = axx * ca - axy * sa, nxy = axx * sa + axy * ca;
                        double nyx = ayx * ca - ayy * sa, nyy = ayx * sa + ayy * ca;
                        axx = nxx; axy = nxy; ayx = nyx; ayy = nyy;
                    }
                    // base.offset_x/offset_y: the particle library centres the quad at (offset_x, offset_y) in
                    // half-size units (drawXYPlane), so the screen centre shifts along the local axes. Local +Y is
                    // screen-up but ay points down, hence −offsetY. It anchors asymmetric billboards like the Bite
                    // jaws.
                    if (layer.OffsetX != 0 || layer.OffsetY != 0)
                    {
                        px += layer.OffsetX * axx - layer.OffsetY * ayx;
                        py += layer.OffsetX * axy - layer.OffsetY * ayy;
                    }
                    if (textured && DsRaster)
                    {
                        double ex = Math.Abs(axx) + Math.Abs(ayx), ey = Math.Abs(axy) + Math.Abs(ayy);
                        int top = (int)Math.Clamp(Math.Floor(py - ey), 0, Height), bottom = (int)Math.Clamp(Math.Ceiling(py + ey), 0, Height);
                        // A3I5 and A5I3 textures make the polygon translucent whatever its alpha.
                        bool translucent = tex.Format == 1 || tex.Format == 6 || PolyAlpha(p.Alpha) < 31;
                        _quads.Add(new Quad(tex, px, py, axx, axy, ayx, ayy, p.R, p.G, p.B, p.Alpha, tileS, tileT,
                            layer.FlipS, layer.FlipT, depth, top, bottom, translucent));
                    }
                    else if (textured)
                        BlitQuad(tex, px, py, axx, axy, ayx, ayy, p.R, p.G, p.B, p.Alpha, tileS, tileT,
                                 layer.FlipS, layer.FlipT);
                    else
                        Splat((int)Math.Round(px), (int)Math.Round(py), Math.Max(1, (int)Math.Round(sc * DotRadiusPx)), p.R, p.G, p.B, p.Alpha);
                }
            }
            if (DsRaster) DrawQueuedDs();
            return _buf;
        }

        // Pinhole eye z in px-units: a world point (x,y,z)pu projects to 128 + x·ZE/(ZE−z), the same
        // camera as the 2D path (f = ZE/(ZE−z)), just expressed for exact per-pixel ray casting.
        private const double EyeZpu = EyeDist * PxPerUnit;

        /// <summary>Exact perspective rendering of a WORLD-space quad (polygon draw types): centre C,
        /// half-axes A/B, all in px-units (+Y up, +Z toward the camera). Per pixel: cast the eye ray,
        /// intersect the quad's plane, map the hit to local (u,v) via the dual basis, sample. This is
        /// what the DS does per-vertex with hardware perspective, no linearisation.</summary>
        private void BlitQuad3D(SpaTexture tex, double[] C, double[] A, double[] B,
                                byte r, byte g, byte b, double alpha, int tileS, int tileT, bool flipS, bool flipT,
                                bool orthographic = false)
        {
            if (alpha <= 0) return;
            double nx = A[1] * B[2] - A[2] * B[1], ny = A[2] * B[0] - A[0] * B[2], nz = A[0] * B[1] - A[1] * B[0];
            double n2 = nx * nx + ny * ny + nz * nz;
            if (n2 < 1e-12) return;   // degenerate (zero-scale) quad
            // Dual basis: u = rel·(B×N)/|N|², v = rel·(N×A)/|N|² (rel = u·A + v·B exactly on the plane).
            double dAx = (B[1] * nz - B[2] * ny) / n2, dAy = (B[2] * nx - B[0] * nz) / n2, dAz = (B[0] * ny - B[1] * nx) / n2;
            double dBx = (ny * A[2] - nz * A[1]) / n2, dBy = (nz * A[0] - nx * A[2]) / n2, dBz = (nx * A[1] - ny * A[0]) / n2;
            // Screen bounding box from the four projected corners.
            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
            for (int sv = -1; sv <= 1; sv += 2)
                for (int su = -1; su <= 1; su += 2)
                {
                    double cx = C[0] + su * A[0] + sv * B[0], cy = C[1] + su * A[1] + sv * B[1], cz = C[2] + su * A[2] + sv * B[2];
                    double ff = 1.0;
                    if (!orthographic) { double depth = EyeZpu - cz; if (depth < 1) depth = 1; ff = EyeZpu / depth; }
                    double sx = Width / 2.0 + cx * ff, sy = Height / 2.0 - cy * ff;
                    if (sx < minX) minX = sx; if (sx > maxX) maxX = sx;
                    if (sy < minY) minY = sy; if (sy > maxY) maxY = sy;
                }
            int x0 = Math.Max(0, (int)Math.Floor(minX)), x1 = Math.Min(Width - 1, (int)Math.Ceiling(maxX));
            int y0 = Math.Max(0, (int)Math.Floor(minY)), y1 = Math.Min(Height - 1, (int)Math.Ceiling(maxY));
            if (x0 > x1 || y0 > y1) return;
            double pez = C[2] - EyeZpu;
            double nDotPE = nx * C[0] + ny * C[1] + nz * pez;   // N·(C − E), E = (0,0,ZE)
            for (int y = y0; y <= y1; y++)
            {
                double dy0 = Height / 2.0 - y;
                for (int x = x0; x <= x1; x++)
                {
                    double dx0 = x - Width / 2.0, qx, qy, qz;
                    if (orthographic)
                    {
                        // Every ray runs straight into the screen, so solve the plane's depth under this pixel.
                        if (Math.Abs(nz) < 1e-9) continue;            // quad seen edge-on
                        qx = dx0 - C[0]; qy = dy0 - C[1];
                        qz = -(nx * qx + ny * qy) / nz;
                    }
                    else
                    {
                        double dz0 = -EyeZpu;                         // ray direction through this pixel
                        double nDotD = nx * dx0 + ny * dy0 + nz * dz0;
                        if (Math.Abs(nDotD) < 1e-9) continue;         // ray parallel to the plane
                        double s = nDotPE / nDotD;
                        if (s <= 1e-6) continue;                      // plane behind the eye
                        qx = s * dx0 - C[0]; qy = s * dy0 - C[1]; qz = EyeZpu + s * dz0 - C[2];
                    }
                    double u = qx * dAx + qy * dAy + qz * dAz;
                    double v = qx * dBx + qy * dBy + qz * dBz;
                    if (u < -1 || u > 1 || v < -1 || v > 1) continue;
                    if (flipS) u = -u;
                    if (flipT) v = -v;
                    // Texcoords: the plane build puts (s,t)=(0,0) at local (−1,+1), texture TOP at +B.
                    int tx = (int)(TexelCoord((u + 1.0) * 0.5 * tileS, tex.RepeatS, tex.MirrorX) * tex.Width);
                    int ty = (int)(TexelCoord((1.0 - v) * 0.5 * tileT, tex.RepeatT, tex.MirrorY) * tex.Height);
                    if (tx < 0) tx = 0; else if (tx >= tex.Width) tx = tex.Width - 1;
                    if (ty < 0) ty = 0; else if (ty >= tex.Height) ty = tex.Height - 1;
                    int ti = (ty * tex.Width + tx) * 4;
                    double ta = tex.Rgba[ti + 3] / 255.0;
                    if (ta <= 0) continue;
                    double contrib = ta * alpha;
                    double invc = 1.0 - contrib;
                    int i = (y * Width + x) * 4;
                    _buf[i + 0] = Over(_buf[i + 0], tex.Rgba[ti + 2] * (b / 255.0) * contrib, invc);
                    _buf[i + 1] = Over(_buf[i + 1], tex.Rgba[ti + 1] * (g / 255.0) * contrib, invc);
                    _buf[i + 2] = Over(_buf[i + 2], tex.Rgba[ti + 0] * (r / 255.0) * contrib, invc);
                    _buf[i + 3] = Over(_buf[i + 3], 255 * contrib, invc);
                }
            }
        }

        /// <summary>Maps a texture coordinate into [0, 1): clamped unless the texture repeats, mirrored on odd repeats when it also flips.</summary>
        internal static double TexelCoord(double c, bool repeat, bool flip)
        {
            const double Edge = 0.999999;
            if (!repeat) return Math.Clamp(c, 0.0, Edge);
            if (!flip) return Math.Clamp(c - Math.Floor(c), 0.0, Edge);
            double m = c % 2.0;
            if (m < 0) m += 2.0;
            return Math.Clamp(m < 1.0 ? m : 2.0 - m, 0.0, Edge);
        }

        // ── World-space quad math for the polygon draw types (matches the hardware quad build) ──
        // Row-major 3×3, row-vector convention (NDS MTX): the rows are the world images of ex/ey/ez.

        private static double[,] Mul3(double[,] a, double[,] b)
        {
            double[,] r = new double[3, 3];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    r[i, j] = a[i, 0] * b[0, j] + a[i, 1] * b[1, j] + a[i, 2] * b[2, j];
            return r;
        }

        // Polygon rotation about the world Y axis (spinning-card leaves).
        private static double[,] RotY(double t)
        {
            double c = Math.Cos(t), s = Math.Sin(t);
            return new[,] { { c, 0.0, -s }, { 0.0, 1.0, 0.0 }, { s, 0.0, c } };
        }

        // Polygon rotation about the normalised (1,1,1) diagonal, the hardware's compact form:
        // C=(1−cos)/3, Sm=C+sin/√3, Sp=C−sin/√3, diagonal C+cos.
        private static double[,] RotXYZ(double t)
        {
            double cos = Math.Cos(t), sin = Math.Sin(t);
            double C = (1.0 - cos) / 3.0;
            double Sm = C + sin * 0.5773502691896258;   // 1/√3
            double Sp = C - sin * 0.5773502691896258;
            C += cos;
            return new[,] { { C, Sm, Sp }, { Sp, C, Sm }, { Sm, Sp, C } };
        }

        // Directional-polygon frame: local Y along `dir` (velocity, or −position for
        // dpolFaceEmitter), local X/Z from cross products with a reference up (Y, or X when nearly parallel).
        private static double[,] Orient(double dx, double dy, double dz)
        {
            double l = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (l < 1e-9) return new[,] { { 1.0, 0.0, 0.0 }, { 0.0, 1.0, 0.0 }, { 0.0, 0.0, 1.0 } };
            double ux = dx / l, uy = dy / l, uz = dz / l;
            double axx = 0, axy = 1, axz = 0;                       // reference axis (0,1,0)
            if (Math.Abs(uy) > 0.8) { axx = 1; axy = 0; }           // |dot| > 0.8 → use (1,0,0)
            double d1x = uy * axz - uz * axy, d1y = uz * axx - ux * axz, d1z = ux * axy - uy * axx;   // u × axis
            double d2x = uy * d1z - uz * d1y, d2y = uz * d1x - ux * d1z, d2z = ux * d1y - uy * d1x;   // u × d1
            return new[,] { { d1x, d1y, d1z }, { ux, uy, uz }, { d2x, d2y, d2z } };
        }

        // Draws one textured quad from its centre and two half-axes in screen pixels (local X along texture S, local Y
        // along T), tinted by the particle's colour and alpha and laid over what is below.
        private void BlitQuad(SpaTexture tex, double cx, double cy, double axX, double axY, double ayX, double ayY,
                              byte r, byte g, byte b, double alpha, int tileS, int tileT,
                              bool flipS = false, bool flipT = false)
        {
            if (alpha <= 0) return;
            // Inverse of the 2×2 [ax ay] basis maps a screen offset (dx,dy) → local (u,v) ∈ [−1,1]² inside the quad.
            double det = axX * ayY - ayX * axY;
            if (Math.Abs(det) < 1e-6) return;
            double inv = 1.0 / det;
            // Axis-aligned bounding box of the rotated quad.
            double ex = Math.Abs(axX) + Math.Abs(ayX), ey = Math.Abs(axY) + Math.Abs(ayY);
            int x0 = Math.Max(0, (int)Math.Floor(cx - ex)), x1 = Math.Min(Width - 1, (int)Math.Ceiling(cx + ex));
            int y0 = Math.Max(0, (int)Math.Floor(cy - ey)), y1 = Math.Min(Height - 1, (int)Math.Ceiling(cy + ey));
            for (int y = y0; y <= y1; y++)
            {
                double dy = y - cy;
                for (int x = x0; x <= x1; x++)
                {
                    double dx = x - cx;
                    double u = (dx * ayY - dy * ayX) * inv;    // local X (texture S), −1..1
                    double v = (axX * dy - axY * dx) * inv;    // local Y (texture T), −1..1
                    if (u < -1 || u > 1 || v < -1 || v > 1) continue;
                    // misc.flipTextureS/T: plain mirror of the texture across the quad centre (distinct from
                    // the quadrant-reflect below, which reconstructs a full sprite from a stored quarter).
                    if (flipS) u = -u;
                    if (flipT) v = -v;
                    // Texcoord (0,0) at the top-left corner to (tileS,tileT) at the bottom-right.
                    int tx = (int)(TexelCoord((u + 1.0) * 0.5 * tileS, tex.RepeatS, tex.MirrorX) * tex.Width);
                    int ty = (int)(TexelCoord((v + 1.0) * 0.5 * tileT, tex.RepeatT, tex.MirrorY) * tex.Height);
                    if (tx < 0) tx = 0; else if (tx >= tex.Width) tx = tex.Width - 1;
                    if (ty < 0) ty = 0; else if (ty >= tex.Height) ty = tex.Height - 1;
                    int ti = (ty * tex.Width + tx) * 4;
                    double ta = tex.Rgba[ti + 3] / 255.0;
                    if (ta <= 0) continue;
                    // The DS blends alpha-over (G3X_AlphaBlend), premultiplied.
                    double contrib = ta * alpha;   // source coverage
                    double invc = 1.0 - contrib;
                    int i = (y * Width + x) * 4;
                    _buf[i + 0] = Over(_buf[i + 0], tex.Rgba[ti + 2] * (b / 255.0) * contrib, invc);   // B
                    _buf[i + 1] = Over(_buf[i + 1], tex.Rgba[ti + 1] * (g / 255.0) * contrib, invc);   // G
                    _buf[i + 2] = Over(_buf[i + 2], tex.Rgba[ti + 0] * (r / 255.0) * contrib, invc);   // R
                    _buf[i + 3] = Over(_buf[i + 3], 255 * contrib, invc);                              // A
                }
            }
        }

        private static int PolyAlpha(double alpha) => (int)Math.Clamp(Math.Round(alpha * 31), 0, 31);

        // Opaque polygons first, then by bottom Y, then top Y; ties keep submission order (stable sort).
        private void DrawQueuedDs()
        {
            int n = Width * Height;
            if (_depth == null || _depth.Length != n) { _depth = new double[n]; _translucentId = new int[n]; }
            Array.Fill(_depth, double.MaxValue);
            Array.Fill(_translucentId, -1);
            List<(Quad Q, int Id)> order = new List<(Quad, int)>(_quads.Count);
            int id = MinPolygonId;
            foreach (Quad q in _quads)
            {
                order.Add((q, id));
                id = id == MaxPolygonId ? MinPolygonId : id + 1;
            }
            List<(Quad Q, int Id)> sorted = order.Select((o, i) => (o, i))
                .OrderBy(x => x.o.Q.Translucent ? 1 : 0).ThenBy(x => x.o.Q.YBottom).ThenBy(x => x.o.Q.YTop).ThenBy(x => x.i)
                .Select(x => x.o).ToList();
            foreach ((Quad q, int polyId) in sorted) BlitQuadDs(q, polyId);
        }

        // The particle library's polygon ID range (both games' particle systems pass 6 to 63).
        private const int MinPolygonId = 6, MaxPolygonId = 63;

        private void BlitQuadDs(Quad q, int polyId)
        {
            int polyAlpha = PolyAlpha(q.Alpha);
            if (polyAlpha == 0) return;
            SpaTexture tex = q.Tex;
            double det = q.AxX * q.AyY - q.AyX * q.AxY;
            if (Math.Abs(det) < 1e-6) return;
            double inv = 1.0 / det;
            double ex = Math.Abs(q.AxX) + Math.Abs(q.AyX), ey = Math.Abs(q.AxY) + Math.Abs(q.AyY);
            int x0 = Math.Max(0, (int)Math.Floor(q.Cx - ex)), x1 = Math.Min(Width - 1, (int)Math.Ceiling(q.Cx + ex));
            int y0 = Math.Max(0, (int)Math.Floor(q.Cy - ey)), y1 = Math.Min(Height - 1, (int)Math.Ceiling(q.Cy + ey));
            for (int y = y0; y <= y1; y++)
            {
                double dy = y - q.Cy;
                for (int x = x0; x <= x1; x++)
                {
                    double dx = x - q.Cx;
                    double u = (dx * q.AyY - dy * q.AyX) * inv, v = (q.AxX * dy - q.AxY * dx) * inv;
                    if (u < -1 || u > 1 || v < -1 || v > 1) continue;
                    if (q.FlipS) u = -u;
                    if (q.FlipT) v = -v;
                    int tx = (int)(TexelCoord((u + 1.0) * 0.5 * q.TileS, tex.RepeatS, tex.MirrorX) * tex.Width);
                    int ty = (int)(TexelCoord((v + 1.0) * 0.5 * q.TileT, tex.RepeatT, tex.MirrorY) * tex.Height);
                    tx = Math.Clamp(tx, 0, tex.Width - 1); ty = Math.Clamp(ty, 0, tex.Height - 1);
                    int ti = (ty * tex.Width + tx) * 4;
                    int texAlpha = (int)Math.Round(tex.Rgba[ti + 3] * 31 / 255.0);
                    if (texAlpha == 0) continue;
                    // Modulate: the texel's alpha scales the polygon's, both 5-bit.
                    int a = ((texAlpha + 1) * (polyAlpha + 1) - 1) >> 5;
                    if (a == 0) continue;
                    int pi = y * Width + x;
                    if (!(q.Depth < _depth[pi])) continue;
                    double r = tex.Rgba[ti + 0] * (q.R / 255.0), g = tex.Rgba[ti + 1] * (q.G / 255.0), b = tex.Rgba[ti + 2] * (q.B / 255.0);
                    int i = pi * 4;
                    if (a == 31)
                    {
                        _buf[i + 0] = (byte)b; _buf[i + 1] = (byte)g; _buf[i + 2] = (byte)r; _buf[i + 3] = 255;
                        _depth[pi] = q.Depth;
                        _translucentId[pi] = -1;
                        continue;
                    }
                    if (_translucentId[pi] == polyId) continue;
                    double c = (a + 1) / 32.0, invc = 1.0 - c;
                    _buf[i + 0] = Over(_buf[i + 0], b * c, invc);
                    _buf[i + 1] = Over(_buf[i + 1], g * c, invc);
                    _buf[i + 2] = Over(_buf[i + 2], r * c, invc);
                    _buf[i + 3] = Over(_buf[i + 3], 255 * c, invc);
                    _translucentId[pi] = polyId;
                }
            }
        }

        // Soft radial dot (fallback when an emitter's texture isn't decodable).
        private void Splat(int cx, int cy, int rad, byte r, byte g, byte b, double alpha)
        {
            if (alpha <= 0) return;
            int x0 = Math.Max(0, cx - rad), x1 = Math.Min(Width - 1, cx + rad);
            int y0 = Math.Max(0, cy - rad), y1 = Math.Min(Height - 1, cy + rad);
            double inv = 1.0 / rad;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    double dx = x - cx, dy = y - cy;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist > rad) continue;
                    double contrib = alpha * (1.0 - dist * inv);
                    if (contrib <= 0) continue;
                    double invc = 1.0 - contrib;
                    int i = (y * Width + x) * 4;
                    _buf[i + 0] = Over(_buf[i + 0], b * contrib, invc);
                    _buf[i + 1] = Over(_buf[i + 1], g * contrib, invc);
                    _buf[i + 2] = Over(_buf[i + 2], r * contrib, invc);
                    _buf[i + 3] = Over(_buf[i + 3], 255 * contrib, invc);
                }
            }
        }

        // Premultiplied alpha-over: out = src_premult + dst·(1−coverage). src is already coverage-premultiplied.
        private static byte Over(byte dst, double srcPremult, double invCoverage)
        {
            int v = (int)(srcPremult + dst * invCoverage + 0.5);
            return v > 255 ? (byte)255 : v < 0 ? (byte)0 : (byte)v;
        }
    }
}
