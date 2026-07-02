using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using global::Avalonia;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Drives one or more <see cref="SpaSimulator"/>s and renders the current particle state to a transparent
    /// <see cref="WriteableBitmap"/> ready to overlay on the battle scene. Each particle is drawn as its emitter's
    /// real decoded NDS texture (a billboard), modulated by the particle's colour + alpha and blended additively —
    /// the in-game look. Emitters whose texture couldn't be decoded fall back to a soft colour dot. The world→pixel
    /// scale and the emitter centre are TUNABLE (the SPA's small world units → the 256×192 scene around the defender).
    /// </summary>
    public sealed class SpaParticlePreview
    {
        public readonly struct Layer
        {
            public readonly SpaSimulator Sim;
            public readonly IReadOnlyList<SpaTexture> Textures;   // archive textures; each particle's TexNo indexes this
            public readonly SpaTexture BaseTex;        // fallback (the emitter's base TexNo) when no texture animation
            public readonly double CenterX, CenterY;   // where this emitter sits (attacker / defender, per callback)
            public readonly int DrawType;              // SPL_DRAW_* — 1/3 = directional billboard (oriented along velocity)
            public readonly bool RepeatS, RepeatT;     // emitter tex_repeat ≥ 1; with a texture's flip bit → mirror
            public readonly double Aspect, DbbScale;   // base.aspect (sclX = sclY×aspect); directional stretch along vel
            public readonly double OffsetX, OffsetY;    // base.offset_x/offset_y: quad centre offset in half-size units
            public Layer(SpaSimulator sim, IReadOnlyList<SpaTexture> textures, SpaTexture baseTex, double centerX, double centerY,
                         int drawType, bool repeatS = false, bool repeatT = false, double aspect = 1.0, double dbbScale = 0.0,
                         double offsetX = 0.0, double offsetY = 0.0)
            { Sim = sim; Textures = textures; BaseTex = baseTex; CenterX = centerX; CenterY = centerY; DrawType = drawType; RepeatS = repeatS; RepeatT = repeatT; Aspect = aspect <= 0 ? 1.0 : aspect; DbbScale = dbbScale; OffsetX = offsetX; OffsetY = offsetY; }

            public SpaTexture TexFor(int texNo)
                => (Textures != null && texNo >= 0 && texNo < Textures.Count) ? Textures[texNo] : BaseTex;
        }

        public int Width { get; }
        public int Height { get; }

        public double WorldToPx { get; set; } = 1.0;      // particle pixels → screen pixels (positions already /172)
        // billboard half-size px = base_scl × this. The SPL quad is ±FX32_ONE (±1.0 world) scaled by base_scl, and
        // world→pixels is /PT_LCD_DOT, so the faithful factor is FX32_ONE/PT_LCD_DOT = 4096/172 ≈ 23.8.
        public double ScalePx { get; set; } = 4096.0 / 172.0;
        public double DotRadiusPx { get; set; } = 4.0;    // soft-dot fallback radius

        private readonly List<Layer> _layers = new List<Layer>();
        private readonly byte[] _buf;

        public SpaParticlePreview(int width = 256, int height = 192)
        {
            Width = width; Height = height;
            _buf = new byte[width * height * 4];
        }

        /// <summary>Adds an emitter layer (the timeline interpreter calls this as ADD_PARTICLE commands fire).</summary>
        public void AddLayer(Layer l) => _layers.Add(l);

        public bool HasEmitters => _layers.Count > 0;
        public bool AllFinished => _layers.TrueForAll(l => l.Sim.Finished);

        public void Step() { foreach (var l in _layers) l.Sim.Step(); }

        public WriteableBitmap RenderFrame()
        {
            Array.Clear(_buf, 0, _buf.Length);
            foreach (var layer in _layers)
            {
                bool directional = layer.DrawType == 1 || layer.DrawType == 3;   // spl_draw_dbb / dpl
                foreach (var p in layer.Sim.Particles())
                {
                    // The displayed texture can change per particle/frame (SPLResTexAnm). Mirror is per-texture
                    // (its flip bit) gated by the emitter's tex_repeat.
                    var tex = layer.TexFor(p.TexNo);
                    bool textured = tex != null && tex.Rgba != null && tex.Width > 0 && tex.Height > 0;
                    bool mirrorX = textured && tex.MirrorX && layer.RepeatS;
                    bool mirrorY = textured && tex.MirrorY && layer.RepeatT;
                    // +X right, +Y up → screen Y is flipped (particle space has +Y at the top of the LCD).
                    double px = layer.CenterX + p.X * WorldToPx;
                    double py = layer.CenterY - p.Y * WorldToPx;
                    // The quad is sized by base_scale (NOT the texture's pixel size); p.Scale already carries
                    // base_scl × scale-anim. sclX = sclY × aspect (spl_draw_bb/dbb). The texture's S axis maps to the
                    // quad's local-X half-axis, its T axis to local-Y.
                    double sc = p.Scale <= 0 ? 1.0 : p.Scale;
                    double sclY = Math.Clamp(sc * ScalePx, 1, 96);
                    double sclX = Math.Clamp(sc * layer.Aspect * ScalePx, 1, 96);
                    double axx, axy, ayx, ayy;   // local-X (texture S) and local-Y (texture T) half-axes, screen px
                    if (directional && (p.VX != 0 || p.VY != 0))
                    {
                        // spl_draw_dbb: local-Y points ALONG screen velocity (stretched by 1+dbb_scale), local-X is
                        // perpendicular (width = sclX). Screen velocity = (VX, −VY) (+Y up → screen down).
                        double vx = p.VX, vy = -p.VY, vl = Math.Sqrt(vx * vx + vy * vy);
                        vx /= vl; vy /= vl;
                        double lenY = Math.Clamp(sc * (1.0 + layer.DbbScale) * ScalePx, 1, 96);
                        ayx = vx * lenY; ayy = vy * lenY;        // along velocity (texture T)
                        axx = -vy * sclX; axy = vx * sclX;       // perpendicular (texture S)
                    }
                    else
                    {
                        axx = sclX; axy = 0; ayx = 0; ayy = sclY;   // axis-aligned billboard
                    }
                    // init_rtt + rotation-anim spin (SPLResBase): rotate the quad's half-axes. The billboard angle is in
                    // particle space (+Y up); screen +Y is down, so negate. Pin Missile / Sonic Boom / Horn Drill needles
                    // & waves get their real orientation (they were drawn straight up); adds on top of a directional aim.
                    if (p.Rotation != 0)
                    {
                        double a = -p.Rotation, ca = Math.Cos(a), sa = Math.Sin(a);
                        double nxx = axx * ca - axy * sa, nxy = axx * sa + axy * ca;
                        double nyx = ayx * ca - ayy * sa, nyy = ayx * sa + ayy * ca;
                        axx = nxx; axy = nxy; ayx = nyx; ayy = nyy;
                    }
                    // base.offset_x/offset_y: the SPL quad is centred at (offset_x, offset_y) in half-size units
                    // (drawXYPlane), so shift the screen centre along the local axes. Local +Y is world-up = screen-up,
                    // but our ay points screen-down, hence −offsetY. Anchors asymmetric billboards (Bite jaws).
                    if (layer.OffsetX != 0 || layer.OffsetY != 0)
                    {
                        px += layer.OffsetX * axx - layer.OffsetY * ayx;
                        py += layer.OffsetX * axy - layer.OffsetY * ayy;
                    }
                    if (textured)
                        BlitQuad(tex, px, py, axx, axy, ayx, ayy, p.R, p.G, p.B, p.Alpha, mirrorX, mirrorY);
                    else
                        Splat((int)Math.Round(px), (int)Math.Round(py), Math.Max(1, (int)Math.Round(sc * DotRadiusPx)), p.R, p.G, p.B, p.Alpha);
                }
            }

            var wb = new WriteableBitmap(new PixelSize(Width, Height), new Vector(96, 96),
                PixelFormat.Bgra8888, AlphaFormat.Premul);
            using (var fb = wb.Lock())
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

        // Draws the texture onto an oriented quad centred at (cx,cy) whose two half-axes are (axX,axY) [local X /
        // texture S] and (ayX,ayY) [local Y / texture T] in screen pixels — the exact spl_draw_bb/dbb quad. For an
        // axis-aligned billboard the axes are (sclX,0)/(0,sclY); for a directional billboard they rotate with the
        // particle's velocity. Modulated by the particle colour+alpha and blended additively (the glow look).
        private void BlitQuad(SpaTexture tex, double cx, double cy, double axX, double axY, double ayX, double ayY,
                              byte r, byte g, byte b, double alpha, bool mirrorX, bool mirrorY)
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
                    // Mirror (flip-wrap + tex_repeat ≥ 1): the quad maps its CENTRE to the texture's far edge and its
                    // edges to texel 0, so reflect from centre outward as (1 − |t|) → texel; else map −1..1 linearly.
                    int tx = mirrorX ? (int)((1.0 - Math.Abs(u)) * tex.Width) : (int)((u + 1.0) * 0.5 * tex.Width);
                    int ty = mirrorY ? (int)((1.0 - Math.Abs(v)) * tex.Height) : (int)((v + 1.0) * 0.5 * tex.Height);
                    if (tx < 0) tx = 0; else if (tx >= tex.Width) tx = tex.Width - 1;
                    if (ty < 0) ty = 0; else if (ty >= tex.Height) ty = tex.Height - 1;
                    int ti = (ty * tex.Width + tx) * 4;
                    double ta = tex.Rgba[ti + 3] / 255.0;
                    if (ta <= 0) continue;
                    // Faithful DS blend = ALPHA-OVER (spl_manager.c G3X_AlphaBlend(TRUE)), NOT additive: the particle
                    // is laid OVER what's underneath by its coverage, premultiplied (the layer is composited Premul).
                    // Additive over-brightened everything into a glow ("fake" powders/beams).
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
