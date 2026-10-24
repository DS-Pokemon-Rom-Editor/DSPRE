using System;
using System.Collections.Generic;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.Particles
{
    /// <summary>A particle system's camera: the battle's default looks from (0, 0, 4) at the origin, up +Y.</summary>
    public sealed class SplCamera
    {
        public int PosX, PosY, PosZ = 4 * SplFx.One, TargetX, TargetY, TargetZ, UpX, UpY = SplFx.One, UpZ;
        public bool Orthographic = true;

        // Camera_Init's 45 degree field and 4:3 aspect, in fixed point as the SDK keeps them.
        private const int Tan = SplFx.One, Aspect = 5461;

        /// <summary>The view matrix as rows (x, y, z axes and translation), from MTX_LookAt.</summary>
        public int[] View()
        {
            int zx = PosX - TargetX, zy = PosY - TargetY, zz = PosZ - TargetZ;
            SplFx.Normalize(ref zx, ref zy, ref zz);
            int xx = SplFx.Mul(UpY, zz) - SplFx.Mul(UpZ, zy), xy = SplFx.Mul(UpZ, zx) - SplFx.Mul(UpX, zz), xz = SplFx.Mul(UpX, zy) - SplFx.Mul(UpY, zx);
            SplFx.Normalize(ref xx, ref xy, ref xz);
            int yx = SplFx.Mul(zy, xz) - SplFx.Mul(zz, xy), yy = SplFx.Mul(zz, xx) - SplFx.Mul(zx, xz), yz = SplFx.Mul(zx, xy) - SplFx.Mul(zy, xx);
            return new[]
            {
                xx, yx, zx,
                xy, yy, zy,
                xz, yz, zz,
                -SplFx.Dot(xx, xy, xz, PosX, PosY, PosZ), -SplFx.Dot(yx, yy, yz, PosX, PosY, PosZ), -SplFx.Dot(zx, zy, zz, PosX, PosY, PosZ),
            };
        }

        /// <summary>Screen position and depth of a view-space point, in pixels on a 256 by 192 screen.</summary>
        public (double X, double Y, double W) Project(double vx, double vy, double vz)
        {
            double dist = Math.Sqrt(Math.Pow((PosX - TargetX) / 4096.0, 2) + Math.Pow((PosY - TargetY) / 4096.0, 2) + Math.Pow((PosZ - TargetZ) / 4096.0, 2));
            double tan = Tan / 4096.0, aspect = Aspect / 4096.0;
            if (Orthographic)
            {
                double top = tan * dist, right = top * aspect;
                return (128 + vx / right * 128, 96 - vy / top * 96, 1);
            }
            double w = -vz;
            if (w <= 1e-6) return (double.NaN, double.NaN, w);
            return (128 + vx / (w * tan * aspect) * 128, 96 - vy / (w * tan) * 96, w);
        }
    }

    /// <summary>Draws particle systems as the DS does: the particle library's quads (lib/spl/src/spl_draw.c), 6-bit modulate,
    /// 5-bit alpha, opaque first, and no blending between translucent polygons that share an ID.</summary>
    public sealed class SplRenderer
    {
        public const int W = 256, H = 192;

        private sealed class Poly
        {
            public double[] X = new double[4], Y = new double[4], Inv = new double[4], Depth = new double[4], S = new double[4], T = new double[4];
            public int R, G, B, Alpha, Id;
            public SpaTexture Tex;
            public bool Translucent;
        }

        private readonly List<Poly> _polys = new List<Poly>();
        private readonly int[] _r = new int[W * H], _g = new int[W * H], _b = new int[W * H], _a = new int[W * H];
        private readonly double[] _depth = new double[W * H];
        private readonly int[] _xluId = new int[W * H];
        private readonly byte[] _out = new byte[W * H * 4];

        /// <summary>One system to draw: its manager, camera and textures.</summary>
        public void Draw(SplManager mgr, SplCamera cam, IReadOnlyList<SpaTexture> textures)
        {
            int[] view = cam.View();
            for (SplEmitter e = mgr.Active.First; e != null; e = e.Next)
            {
                if (e.RenderingDisabled) continue;
                SplResource r = e.Resource;
                if (r.DrawChildrenFirst)
                {
                    DrawChildren(e, view, cam, textures);
                    if (!r.HideParent) DrawParticles(e, view, cam, textures);
                }
                else
                {
                    if (!r.HideParent) DrawParticles(e, view, cam, textures);
                    DrawChildren(e, view, cam, textures);
                }
            }
        }

        private void DrawParticles(SplEmitter e, int[] view, SplCamera cam, IReadOnlyList<SpaTexture> textures)
        {
            SplResource r = e.Resource;
            for (SplParticle p = e.Particles.First; p != null; p = p.Next)
            {
                SpaTexture tex = Tex(textures, r.HasTexAnim ? p.Texture : r.TextureIndex);
                Build(e, p, view, cam, tex, r.DrawType, r.PolygonRotAxis, r.PolygonReferencePlane, r.DpolFaceEmitter,
                      e.TextureS, e.TextureT, r.PolygonX, r.PolygonY);
            }
        }

        private void DrawChildren(SplEmitter e, int[] view, SplCamera cam, IReadOnlyList<SpaTexture> textures)
        {
            SplResource r = e.Resource;
            SplChild c = r.Child;
            if (!r.HasChild || c == null) return;
            SpaTexture tex = Tex(textures, c.Texture);
            for (SplParticle p = e.Children.First; p != null; p = p.Next)
                Build(e, p, view, cam, tex, c.DrawType, c.PolygonRotAxis, c.PolygonReferencePlane, c.DpolFaceEmitter,
                      e.ChildTextureS, e.ChildTextureT, 0, 0);
        }

        private static SpaTexture Tex(IReadOnlyList<SpaTexture> textures, int i) => textures != null && i >= 0 && i < textures.Count ? textures[i] : null;

        private static (int X, int Y) Scale(SplEmitter e, SplParticle p)
        {
            int sclY = p.BaseScale;
            int sclX = SplFx.Mul(sclY, e.Resource.Aspect);
            switch (e.Resource.ScaleAnimDir)
            {
                case 0: sclX = SplFx.Mul(sclX, p.AnimScale); sclY = SplFx.Mul(sclY, p.AnimScale); break;
                case 1: sclX = SplFx.Mul(sclX, p.AnimScale); break;
                default: sclY = SplFx.Mul(sclY, p.AnimScale); break;
            }
            return (sclX, sclY);
        }

        // MTX_MultVec43: a row vector times rows 0-2, plus row 3, summed in 64 bits and shifted once.
        private static (int X, int Y, int Z) MultVec43(int x, int y, int z, int[] m) =>
            ((int)(((long)x * m[0] + (long)y * m[3] + (long)z * m[6]) >> 12) + m[9],
             (int)(((long)x * m[1] + (long)y * m[4] + (long)z * m[7]) >> 12) + m[10],
             (int)(((long)x * m[2] + (long)y * m[5] + (long)z * m[8]) >> 12) + m[11]);

        private static (int X, int Y, int Z) MultVec33(int x, int y, int z, int[] m) =>
            ((int)(((long)x * m[0] + (long)y * m[3] + (long)z * m[6]) >> 12),
             (int)(((long)x * m[1] + (long)y * m[4] + (long)z * m[7]) >> 12),
             (int)(((long)x * m[2] + (long)y * m[5] + (long)z * m[8]) >> 12));

        // MTX_Concat43 for the 3x3 part: a then b.
        private static int[] Concat33(int[] a, int[] b)
        {
            int[] c = new int[9];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    c[i * 3 + j] = (int)(((long)a[i * 3] * b[j] + (long)a[i * 3 + 1] * b[3 + j] + (long)a[i * 3 + 2] * b[6 + j]) >> 12);
            return c;
        }

        private static int[] Rotation(int axis, int rotation)
        {
            int sin = SplFx.Sin(rotation), cos = SplFx.Cos(rotation);
            if (axis == 0)
                return new[] { cos, 0, -sin, 0, SplFx.One, 0, sin, 0, cos };
            // SPLUtil_RotateXYZ: a turn about the (1, 1, 1) diagonal.
            int c = SplFx.Mul(SplFx.One - cos, 1365);
            int sm = c + SplFx.Mul(sin, 2365), sp = c - SplFx.Mul(sin, 2365);
            c += cos;
            return new[] { c, sp, sm, sm, c, sp, sp, sm, c };
        }

        private void Build(SplEmitter e, SplParticle p, int[] view, SplCamera cam, SpaTexture tex, int drawType, int rotAxis,
                           int refPlane, bool faceEmitter, short texS, short texT, short offX, short offY)
        {
            int alpha = (p.BaseAlpha * (p.AnimAlpha + 1)) >> 5;
            if (alpha == 0) return;
            (int sclX, int sclY) = Scale(e, p);
            SplResource r = e.Resource;
            int tx = p.PosX + p.EmitterPosX, ty = p.PosY + p.EmitterPosY, tz = p.PosZ + p.EmitterPosZ;
            if (r.UseViewSpace) { tx -= r.BaseX; ty -= r.BaseY; tz -= r.BaseZ; }
            // Each vertex maps local (x, y, z) to view space through m (3x3) and the translation.
            double[] m;
            double mx, my, mz;
            bool xz = false;
            if (drawType == 0 || drawType == 1)
            {
                (int X, int Y, int Z) t = MultVec43(tx, ty, tz, view);
                int m00, m01, m10, m11;
                if (drawType == 0)
                {
                    int sin = SplFx.Sin(p.Rotation), cos = SplFx.Cos(p.Rotation);
                    m00 = SplFx.Mul(cos, sclX); m01 = SplFx.Mul(sin, sclX);
                    m10 = SplFx.Mul(-sin, sclY); m11 = SplFx.Mul(cos, sclY);
                }
                else
                {
                    // SPLDraw_DirectionalBillboard: stretched along the velocity across the view direction.
                    int lx = view[2], ly = view[5], lz = view[8];
                    long cx = (long)p.VelY * lz - (long)p.VelZ * ly, cy = (long)p.VelZ * lx - (long)p.VelX * lz, cz = (long)p.VelX * ly - (long)p.VelY * lx;
                    int dx = (int)((cx + 0x800) >> 12), dy = (int)((cy + 0x800) >> 12), dz = (int)((cz + 0x800) >> 12);
                    if (dx == 0 && dy == 0 && dz == 0) return;
                    SplFx.Normalize(ref dx, ref dy, ref dz);
                    (int X, int Y, int Z) d = MultVec33(dx, dy, dz, view);
                    int vx = p.VelX, vy = p.VelY, vz = p.VelZ;
                    SplFx.Normalize(ref vx, ref vy, ref vz);
                    int dot = SplFx.Mul(vx, -view[2]) + SplFx.Mul(vy, -view[5]) + SplFx.Mul(vz, -view[8]);
                    if (dot < 0) dot = -dot;
                    int len = SplFx.Mul(sclY, SplFx.Mul(SplFx.One - dot, r.DbbScale) + SplFx.One);
                    m00 = SplFx.Mul(d.X, sclX); m01 = SplFx.Mul(d.Y, sclX);
                    m10 = SplFx.Mul(-d.Y, len); m11 = SplFx.Mul(d.X, len);
                }
                m = new double[] { m00, m01, 0, m10, m11, 0, 0, 0, SplFx.One };
                mx = t.X; my = t.Y; mz = t.Z;
                if (r.UseViewSpace) { mx += r.BaseX; my += r.BaseY; mz += r.BaseZ; }
            }
            else
            {
                int[] rot = Rotation(Math.Min(rotAxis, 1), p.Rotation);
                if (drawType >= 3)
                {
                    int ux, uy, uz;
                    if (!faceEmitter) { ux = p.VelX; uy = p.VelY; uz = p.VelZ; }
                    else { ux = p.PosX; uy = p.PosY; uz = p.PosZ; }
                    SplFx.Normalize(ref ux, ref uy, ref uz);
                    if (faceEmitter) { ux = -ux; uy = -uy; uz = -uz; }
                    int ax = 0, ay = SplFx.One, az = 0;
                    int dotAxis = SplFx.Dot(ux, uy, uz, ax, ay, az);
                    if (dotAxis > 3277 || dotAxis < -3277) { ax = SplFx.One; ay = 0; }
                    (int X, int Y, int Z) d1 = Cross(ux, uy, uz, ax, ay, az);
                    (int X, int Y, int Z) d2 = Cross(ux, uy, uz, d1.X, d1.Y, d1.Z);
                    int[] orient = { d1.X, d1.Y, d1.Z, ux, uy, uz, d2.X, d2.Y, d2.Z };
                    rot = Concat33(rot, orient);
                }
                int[] scl = { sclX, 0, 0, 0, sclY, 0, 0, 0, sclY };
                int[] load = Concat33(scl, rot);
                // The particle's own matrix then the view matrix: local to world to view.
                int[] v3 = { view[0], view[1], view[2], view[3], view[4], view[5], view[6], view[7], view[8] };
                int[] full = Concat33(load, v3);
                m = new double[9];
                for (int i = 0; i < 9; i++) m[i] = full[i];
                (int X, int Y, int Z) t = MultVec43(tx, ty, tz, view);
                if (r.UseViewSpace) { t = MultVec43(tx + r.BaseX, ty + r.BaseY, tz + r.BaseZ, view); }
                mx = t.X; my = t.Y; mz = t.Z;
                xz = refPlane == 1;
            }

            Poly q = new Poly { Tex = tex, Id = p.PolygonId, Alpha = alpha };
            int c = p.Color, ec = e.Color;
            q.R = ((c & 31) * (ec & 31)) >> 5; q.G = (((c >> 5) & 31) * ((ec >> 5) & 31)) >> 5; q.B = (((c >> 10) & 31) * ((ec >> 10) & 31)) >> 5;
            q.Translucent = alpha < 31 || (tex != null && (tex.Format == 1 || tex.Format == 6));
            double ox = offX, oy = offY;
            double[,] corners = { { ox - 4096, oy + 4096, 0, 0 }, { ox + 4096, oy + 4096, texS, 0 }, { ox + 4096, oy - 4096, texS, texT }, { ox - 4096, oy - 4096, 0, texT } };
            for (int k = 0; k < 4; k++)
            {
                double lx = corners[k, 0], ly = xz ? 0 : corners[k, 1], lz = xz ? corners[k, 1] : 0;
                double vx = (lx * m[0] + ly * m[3] + lz * m[6]) / 4096.0 + mx;
                double vy = (lx * m[1] + ly * m[4] + lz * m[7]) / 4096.0 + my;
                double vz = (lx * m[2] + ly * m[5] + lz * m[8]) / 4096.0 + mz;
                (double X, double Y, double W) s = cam.Project(vx / 4096.0, vy / 4096.0, vz / 4096.0);
                if (double.IsNaN(s.X)) return;
                q.X[k] = s.X; q.Y[k] = s.Y; q.Inv[k] = 1.0 / s.W; q.Depth[k] = -vz / 4096.0;
                q.S[k] = corners[k, 2] / 4096.0; q.T[k] = corners[k, 3] / 4096.0;
            }
            _polys.Add(q);
        }

        private static (int X, int Y, int Z) Cross(int ax, int ay, int az, int bx, int by, int bz) =>
            ((int)(((long)ay * bz - (long)az * by + 0x800) >> 12), (int)(((long)az * bx - (long)ax * bz + 0x800) >> 12), (int)(((long)ax * by - (long)ay * bx + 0x800) >> 12));

        /// <summary>Rasterises everything sent since the last call into premultiplied BGRA and clears the list.</summary>
        public byte[] Flush()
        {
            Array.Clear(_a, 0, _a.Length);
            for (int i = 0; i < _depth.Length; i++) { _depth[i] = double.MaxValue; _xluId[i] = -1; }
            foreach (Poly q in _polys) if (!q.Translucent) Raster(q);
            foreach (Poly q in _polys) if (q.Translucent) Raster(q);
            _polys.Clear();
            for (int i = 0; i < W * H; i++)
            {
                int a = _a[i];
                int o = i * 4;
                if (a == 0) { _out[o] = _out[o + 1] = _out[o + 2] = _out[o + 3] = 0; continue; }
                // The 3D layer blends over the 2D layers as (3D * (a + 1) + 2D * (31 - a)) / 32.
                double k = a >= 31 ? 1.0 : (a + 1) / 32.0;
                _out[o] = (byte)(To8(_b[i]) * k + 0.5);
                _out[o + 1] = (byte)(To8(_g[i]) * k + 0.5);
                _out[o + 2] = (byte)(To8(_r[i]) * k + 0.5);
                _out[o + 3] = (byte)(255 * k + 0.5);
            }
            return _out;
        }

        private static int To8(int c6) => (c6 << 2) | (c6 >> 4);
        private static int E6(int c5) => c5 == 0 ? 0 : c5 * 2 + 1;

        private static int Wrap(int v, int size, bool repeat, bool flip)
        {
            if (!repeat) return Math.Clamp(v, 0, size - 1);
            int period = flip ? size * 2 : size;
            int m = ((v % period) + period) % period;
            return flip && m >= size ? period - 1 - m : m;
        }

        // Scanline fill of a convex quad, sampling at pixel centres, perspective-correct through 1/w.
        private void Raster(Poly q)
        {
            double minY = Math.Min(Math.Min(q.Y[0], q.Y[1]), Math.Min(q.Y[2], q.Y[3]));
            double maxY = Math.Max(Math.Max(q.Y[0], q.Y[1]), Math.Max(q.Y[2], q.Y[3]));
            int y0 = Math.Max(0, (int)Math.Ceiling(minY - 0.5)), y1 = Math.Min(H - 1, (int)Math.Ceiling(maxY - 0.5) - 1);
            int vr = E6(q.R), vg = E6(q.G), vb = E6(q.B);
            for (int y = y0; y <= y1; y++)
            {
                double py = y + 0.5;
                double xl = double.MaxValue, xr = double.MinValue;
                double[] l = null, rr = null;
                for (int k = 0; k < 4; k++)
                {
                    int k2 = (k + 1) & 3;
                    double ya = q.Y[k], yb = q.Y[k2];
                    if ((py < Math.Min(ya, yb)) || (py >= Math.Max(ya, yb)) || ya == yb) continue;
                    double f = (py - ya) / (yb - ya);
                    double x = q.X[k] + (q.X[k2] - q.X[k]) * f;
                    double inv = q.Inv[k] + (q.Inv[k2] - q.Inv[k]) * f;
                    double so = q.S[k] * q.Inv[k] + (q.S[k2] * q.Inv[k2] - q.S[k] * q.Inv[k]) * f;
                    double to = q.T[k] * q.Inv[k] + (q.T[k2] * q.Inv[k2] - q.T[k] * q.Inv[k]) * f;
                    double d = q.Depth[k] + (q.Depth[k2] - q.Depth[k]) * f;
                    double[] v = { x, inv, so, to, d };
                    if (x < xl) { xl = x; l = v; }
                    if (x > xr) { xr = x; rr = v; }
                }
                if (l == null || rr == null) continue;
                int x0 = Math.Max(0, (int)Math.Ceiling(xl - 0.5)), x1 = Math.Min(W - 1, (int)Math.Ceiling(xr - 0.5) - 1);
                for (int x = x0; x <= x1; x++)
                {
                    double f = rr[0] == l[0] ? 0 : (x + 0.5 - l[0]) / (rr[0] - l[0]);
                    double inv = l[1] + (rr[1] - l[1]) * f;
                    double s = (l[2] + (rr[2] - l[2]) * f) / inv, t = (l[3] + (rr[3] - l[3]) * f) / inv;
                    double depth = l[4] + (rr[4] - l[4]) * f;
                    int tr = 63, tg = 63, tb = 63, ta = 31;
                    if (q.Tex != null && q.Tex.Rgba != null && q.Tex.Width > 0 && q.Tex.Height > 0)
                    {
                        int u = Wrap((int)Math.Floor(s * q.Tex.Width), q.Tex.Width, q.Tex.RepeatS, q.Tex.MirrorX);
                        int v = Wrap((int)Math.Floor(t * q.Tex.Height), q.Tex.Height, q.Tex.RepeatT, q.Tex.MirrorY);
                        int o = (v * q.Tex.Width + u) * 4;
                        ta = q.Tex.Rgba[o + 3] >> 3;
                        if (ta == 0) continue;
                        tr = E6(q.Tex.Rgba[o] >> 3); tg = E6(q.Tex.Rgba[o + 1] >> 3); tb = E6(q.Tex.Rgba[o + 2] >> 3);
                    }
                    int a = (((ta + 1) * (q.Alpha + 1)) - 1) >> 5;
                    if (a == 0) continue;
                    int r = ((tr + 1) * (vr + 1) - 1) >> 6, g = ((tg + 1) * (vg + 1) - 1) >> 6, b = ((tb + 1) * (vb + 1) - 1) >> 6;
                    int i = y * W + x;
                    if (depth >= _depth[i]) continue;
                    if (a >= 31 && !q.Translucent)
                    {
                        _r[i] = r; _g[i] = g; _b[i] = b; _a[i] = 31; _depth[i] = depth; _xluId[i] = -1;
                        continue;
                    }
                    if (_xluId[i] == q.Id) continue;
                    if (_a[i] == 0) { _r[i] = r; _g[i] = g; _b[i] = b; _a[i] = a; }
                    else
                    {
                        _r[i] = (r * (a + 1) + _r[i] * (31 - a)) >> 5;
                        _g[i] = (g * (a + 1) + _g[i] * (31 - a)) >> 5;
                        _b[i] = (b * (a + 1) + _b[i] * (31 - a)) >> 5;
                        _a[i] = Math.Max(a, _a[i]);
                    }
                    _xluId[i] = q.Id;
                }
            }
        }
    }
}
