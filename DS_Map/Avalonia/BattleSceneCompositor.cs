using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using global::Avalonia;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Software battle-scene compositor — renders the backdrop, platforms and the two Pokémon (with the WEST
    /// per-mon affine transforms) into one RGB buffer, then composites the move-effect background (BG2) over it
    /// using the real NDS 2D blend (G2_SetBlendAlpha: <c>out = water·ca + sceneBelow·cb</c>). This replaces the
    /// stacked-Avalonia-Image approach so the hardware blend is reproduced exactly instead of approximated. The
    /// particle/cell/chrome layers (which sit ABOVE BG2) remain separate overlays.
    /// </summary>
    public sealed class BattleSceneCompositor
    {
        public const int W = 256, H = 192;
        private byte[] _backdrop;                                   // RGB W*H (opaque battle background)
        private readonly List<(byte[] rgba, int w, int h, int left, int top)> _statics = new();   // platforms
        private (byte[] rgba, int w, int h, int left, int top) _back, _front;   // player(back) / enemy(front) sprites
        private readonly byte[] _scene = new byte[W * H * 3];       // opaque working scene
        private readonly byte[] _out = new byte[W * H * 4];

        public void SetBackdrop(byte[] rgbWxH) => _backdrop = rgbWxH;
        public void ClearStatics() => _statics.Clear();
        public void AddStatic(byte[] rgba, int w, int h, int left, int top) { if (rgba != null) _statics.Add((rgba, w, h, left, top)); }
        public void SetPlayer(byte[] rgba, int w, int h, int left, int top) => _back = (rgba, w, h, left, top);
        public void SetEnemy(byte[] rgba, int w, int h, int left, int top) => _front = (rgba, w, h, left, top);

        public WriteableBitmap Render(WestPlayer west)
        {
            // 1. backdrop (or a HAIKEI background-replace, crossfaded behind the mons).
            if (_backdrop != null && _backdrop.Length == _scene.Length)
            {
                if (west != null && west.RasterActive)
                {
                    // WestSp_WE_Laster: shift each scanline horizontally by amp·sin(phase + y·lineAdd) (ripple/heat-haze).
                    for (int y = 0; y < H; y++)
                    {
                        int off = (int)Math.Round(west.RasterAmp * Math.Sin(west.RasterPhase + y * west.RasterLineAdd));
                        for (int x = 0; x < W; x++)
                        {
                            int sx = ((x - off) % W + W) % W, si = (y * W + sx) * 3, di = (y * W + x) * 3;
                            _scene[di] = _backdrop[si]; _scene[di + 1] = _backdrop[si + 1]; _scene[di + 2] = _backdrop[si + 2];
                        }
                    }
                }
                else Array.Copy(_backdrop, _scene, _scene.Length);
            }
            else Array.Clear(_scene, 0, _scene.Length);
            bool haikei = west != null && west.HasBackground && !west.BackgroundIsOverlay;
            if (haikei) CrossfadeBg(west, west.BgCa);   // ca = opacity of the new backdrop

            // 2. platforms (alpha-over).
            foreach (var s in _statics) BlitAxisAligned(s.rgba, s.w, s.h, s.left, s.top);

            // 2b. WSP_PalColChange grayscale — WeTool_PalGrayScale only grays FADE_MAIN_BG (the BACKGROUND palette),
            //     NOT the OBJ/mons. So desaturate the backdrop+platforms here, BEFORE the mons (they stay in colour).
            if (west != null && west.Grayscale)
                for (int i = 0; i < W * H * 3; i += 3)
                {
                    byte y8 = (byte)((_scene[i] * 77 + _scene[i + 1] * 150 + _scene[i + 2] * 29) >> 8);
                    _scene[i] = _scene[i + 1] = _scene[i + 2] = y8;
                }

            // 2c. ColorConceChangePfd flash on FADE_MAIN_BG — lerp the backdrop+platforms toward a colour (Earthquake's
            //     black↔white pulses). BG-only (same as grayscale), so the mons stay unflashed.
            if (west != null && west.BgFlashAmount > 0)
            {
                double k = Math.Clamp(west.BgFlashAmount, 0, 1); double ik = 1 - k;
                byte fr = west.BgFlashR, fg = west.BgFlashG, fb = west.BgFlashB;
                for (int i = 0; i < W * H * 3; i += 3)
                {
                    _scene[i] = (byte)(_scene[i] * ik + fr * k);
                    _scene[i + 1] = (byte)(_scene[i + 1] * ik + fg * k);
                    _scene[i + 2] = (byte)(_scene[i + 2] * ik + fb * k);
                }
            }

            // 2d. HAIKEI_PAL_FADE / Wish BG flash — PaletteFadeReq on FADE_MAIN_BG ramps the BACKGROUND toward a colour
            //     (Thunder etc. darken toward black; Wish flashes white). BG-only like 2b/2c, so the mons stay LIT — the
            //     game fades only the main-BG palette, not the OBJ/soft-sprite mons (was a full-screen overlay in the view).
            if (west != null && west.FadeOpacity > 0)
            {
                double k = Math.Clamp(west.FadeOpacity, 0, 1); double ik = 1 - k;
                byte fr = west.FadeR, fg = west.FadeG, fb = west.FadeB;
                for (int i = 0; i < W * H * 3; i += 3)
                {
                    _scene[i] = (byte)(_scene[i] * ik + fr * k);
                    _scene[i + 1] = (byte)(_scene[i + 1] * ik + fg * k);
                    _scene[i + 2] = (byte)(_scene[i + 2] * ik + fb * k);
                }
            }

            // 3. the two mons, with their WEST affine transforms + colour tint, back (player) then front (enemy).
            // Null west = the static pre-play scene (mons at rest).
            byte tr = west?.TintR ?? 0, tg = west?.TintG ?? 0, tb = west?.TintB ?? 0;
            // 3a. afterimage ghosts (Double Team etc.) — copies of a mon sprite drawn BEHIND the real mons.
            if (west != null)
                foreach (var gh in west.Ghosts)
                {
                    var gs = gh.Mon == 0 ? _back : _front;
                    BlitMon(gs, true, gh.Dx, gh.Dy, gh.ScaleX, gh.ScaleY, 0, gh.TintA, gh.TintR, gh.TintG, gh.TintB, gh.Alpha);
                }
            for (int m = 0; m < 2; m++)
            {
                var s = m == 0 ? _back : _front;
                bool vis = west?.MonVisible[m] ?? true;
                double dx = (west?.MonDX[m] ?? 0) + (west?.MonShakeX[m] ?? 0);   // persistent slide + transient WT_SHAKE
                double dy = (west?.MonDY[m] ?? 0) + (west?.MonShakeY[m] ?? 0);
                double scx = west?.MonScaleX[m] ?? 1, scy = west?.MonScaleY[m] ?? 1;
                double rot = west?.MonRot[m] ?? 0, ta = west?.MonTintA[m] ?? 0;
                bool warp = west != null && west.MonWarpMon == m;   // per-scanline raster warp (Extrasensory / Acid Armor)
                BlitMon(s, vis, dx, dy, scx, scy, rot, ta, tr, tg, tb, west?.MonAlpha[m] ?? 1.0, (int)(west?.MonMosaic[m] ?? 0), west?.MonClip[m] ?? 1,
                    warp, warp ? west.MonWarpAmp : 0, warp ? west.MonWarpBaseDeg : 0, warp ? west.MonWarpAddPerRow : 0,
                    warp ? west.MonWarpWidthA : 0, warp ? west.MonWarpShimmer : 0);
            }

            // 3a2. POKEOAM_DROP caps — clones of a mon sprite dropped into OAM (Disable's gray shadow, Substitute, …),
            //      scaled/recoloured/mosaic'd by the CAP_* routines. Drawn over the mons (OAM layer).
            if (west != null)
                foreach (var cap in west.Caps)
                {
                    if (!cap.Visible) continue;
                    var cs = cap.SrcMon == 0 ? _back : _front;
                    BlitMon(cs, true, cap.Dx, cap.Dy, cap.ScaleX, cap.ScaleY, cap.RotDeg, cap.TintA, cap.TintR, cap.TintG, cap.TintB, cap.Alpha, (int)cap.Mosaic);
                }

            // 3b. CATS cell actors (OAM cell animations) — drawn over the mons. The Surf (WE_057) wave is now a
            //     normal driven actor here too (no legacy view overlay).
            if (west != null) BlitCellActors(west);

            // 4. WeT02 effect BG (overlay) — blended over the scene with the GX coefficients.
            if (west != null && west.HasBackground && west.BackgroundIsOverlay) OverlayBg(west, west.BgCa, west.BgCb);

            // 5. emit premultiplied BGRA.
            for (int i = 0, j = 0; i < W * H * 3; i += 3, j += 4)
            {
                _out[j + 0] = _scene[i + 2]; _out[j + 1] = _scene[i + 1]; _out[j + 2] = _scene[i + 0]; _out[j + 3] = 255;
            }
            var wb = new WriteableBitmap(new PixelSize(W, H), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            using (var fb = wb.Lock())
            {
                int rb = fb.RowBytes;
                if (rb == W * 4) Marshal.Copy(_out, 0, fb.Address, _out.Length);
                else for (int y = 0; y < H; y++) Marshal.Copy(_out, y * W * 4, fb.Address + y * rb, W * 4);
            }
            return wb;
        }

        // Draw every live CATS cell actor over the scene: render its current cell (cached RGBA, origin at the buffer
        // centre), then place/scale/flip/rotate it so the actor origin lands at (X+frameX, Y+frameY) and alpha-blend
        // it in. Nearest-neighbour inverse map; frame + actor rotation (FrameRotDeg) applied via inverse-rotated sample.
        private void BlitCellActors(WestPlayer west)
        {
            var cells = west.Cells;
            if (cells == null || !cells.Loaded) return;
            foreach (var a in west.CatsActors)
            {
                if (!a.Visible || a.Alpha <= 0) continue;
                var cp = cells.RenderCellRgba(a.CellIndex);
                if (cp.Rgba == null) continue;
                int S = cp.Size; double half = S / 2.0;
                double sclX = a.ScaleX * a.FrameScaleX, sclY = a.ScaleY * a.FrameScaleY;
                if (sclX <= 0.0001 || sclY <= 0.0001) continue;
                double cx = a.X + a.FrameX, cy = a.Y + a.FrameY;   // screen position of the actor origin
                double rot = a.FrameRotDeg * Math.PI / 180.0;      // CATS_ObjectRotationSetCap + frame SRT rotation
                bool rotated = Math.Abs(a.FrameRotDeg) > 0.01;
                double cosR = Math.Cos(-rot), sinR = Math.Sin(-rot);   // inverse (screen→source) rotation
                double ex = rotated ? half * Math.Max(sclX, sclY) * 1.4143 : half * sclX;
                double ey = rotated ? half * Math.Max(sclX, sclY) * 1.4143 : half * sclY;
                int x0 = Math.Max(0, (int)Math.Floor(cx - ex)), x1 = Math.Min(W, (int)Math.Ceiling(cx + ex));
                int y0 = Math.Max(0, (int)Math.Floor(cy - ey)), y1 = Math.Min(H, (int)Math.Ceiling(cy + ey));
                for (int dy = y0; dy < y1; dy++)
                    for (int dx = x0; dx < x1; dx++)
                    {
                        double ox = dx + 0.5 - cx, oy = dy + 0.5 - cy;
                        if (rotated) { double rx = ox * cosR - oy * sinR, ry = ox * sinR + oy * cosR; ox = rx; oy = ry; }
                        double u = ox / sclX + half, v = oy / sclY + half;
                        if (a.FlipH) u = S - u;
                        if (a.FlipV) v = S - v;
                        int sx = (int)u, sy = (int)v;
                        if (sx < 0 || sy < 0 || sx >= S || sy >= S) continue;
                        int si = (sy * S + sx) * 4;
                        byte sa = cp.Rgba[si + 3];
                        if (sa == 0) continue;
                        double k = sa / 255.0 * a.Alpha;
                        int di = (dy * W + dx) * 3;
                        _scene[di + 0] = (byte)(_scene[di + 0] * (1 - k) + cp.Rgba[si + 0] * k);
                        _scene[di + 1] = (byte)(_scene[di + 1] * (1 - k) + cp.Rgba[si + 1] * k);
                        _scene[di + 2] = (byte)(_scene[di + 2] * (1 - k) + cp.Rgba[si + 2] * k);
                    }
            }
        }

        // HAIKEI backdrop-replace: blend the scrolled BG over the current scene at coeff ca (crossfade).
        private void CrossfadeBg(WestPlayer west, double ca)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (!west.TrySampleBg(x, y, out byte r, out byte g, out byte b, out byte a) || a == 0) continue;
                    double k = a / 255.0 * ca; int i = (y * W + x) * 3;
                    _scene[i + 0] = Mix(_scene[i + 0], r, k); _scene[i + 1] = Mix(_scene[i + 1], g, k); _scene[i + 2] = Mix(_scene[i + 2], b, k);
                }
        }

        // WeT02 overlay: out = water·ca + sceneBelow·cb (only where the water plane is opaque).
        private void OverlayBg(WestPlayer west, double ca, double cb)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (!west.TrySampleBg(x, y, out byte r, out byte g, out byte b, out byte a) || a == 0) continue;
                    double wa = a / 255.0; int i = (y * W + x) * 3;
                    // pixel coverage wa scales how much the blend applies vs the untouched scene.
                    _scene[i + 0] = (byte)Math.Clamp(_scene[i + 0] * (1 - wa) + (r * ca + _scene[i + 0] * cb) * wa, 0, 255);
                    _scene[i + 1] = (byte)Math.Clamp(_scene[i + 1] * (1 - wa) + (g * ca + _scene[i + 1] * cb) * wa, 0, 255);
                    _scene[i + 2] = (byte)Math.Clamp(_scene[i + 2] * (1 - wa) + (b * ca + _scene[i + 2] * cb) * wa, 0, 255);
                }
        }

        private void BlitAxisAligned(byte[] rgba, int sw, int sh, int left, int top)
        {
            for (int sy = 0; sy < sh; sy++)
            {
                int dy = top + sy; if (dy < 0 || dy >= H) continue;
                for (int sx = 0; sx < sw; sx++)
                {
                    int dx = left + sx; if (dx < 0 || dx >= W) continue;
                    int si = (sy * sw + sx) * 4; double a = rgba[si + 3] / 255.0; if (a <= 0) continue;
                    int di = (dy * W + dx) * 3;
                    _scene[di + 0] = Mix(_scene[di + 0], rgba[si + 0], a);
                    _scene[di + 1] = Mix(_scene[di + 1], rgba[si + 1], a);
                    _scene[di + 2] = Mix(_scene[di + 2], rgba[si + 2], a);
                }
            }
        }

        // Blits a mon sprite with its WEST transform: scale (X/Y) + rotation about the sprite centre, then translate
        // by the lunge offset; colour-tinted by tintA toward (tr,tg,tb); skipped when not visible. Inverse-sampled.
        private void BlitMon((byte[] rgba, int w, int h, int left, int top) s, bool visible, double dx, double dy,
                             double scaleX, double scaleY, double rotDeg, double tintA, byte tr, byte tg, byte tb,
                             double alphaMul = 1.0, int mosaic = 0, double clip = 1.0,
                             bool warp = false, double warpAmp = 0, double warpBaseDeg = 0, double warpAddPerRow = 0,
                             double warpWidthA = 0, int warpShimmer = 0)
        {
            if (s.rgba == null || !visible || alphaMul <= 0) return;
            int mblk = mosaic > 0 ? mosaic + 1 : 0;   // G2_SetOBJMosaicSize: sample is snapped to (level+1)-px blocks
            // RECT_VIEW wipe: |clip| = visible fraction of the sprite rows; clip>0 reveals from the top, clip<0 from the bottom.
            double clipAbs = Math.Min(1.0, Math.Abs(clip)); bool clipTop = clip >= 0;
            double cx = s.left + s.w / 2.0 + dx, cy = s.top + s.h / 2.0 + dy;
            double rad = rotDeg * Math.PI / 180.0, cos = Math.Cos(rad), sin = Math.Sin(rad);
            double sxAbs = Math.Max(0.01, Math.Abs(scaleX)), syAbs = Math.Max(0.01, Math.Abs(scaleY));
            // dest bbox: the sprite half-extents grown by scale+rotation.
            double hw = s.w / 2.0 * sxAbs, hh = s.h / 2.0 * syAbs;
            double ext = Math.Sqrt(hw * hw + hh * hh);
            int warpPad = warp ? 64 : 0;   // raster warp shifts rows horizontally by up to ~sine+shear ≈ 60px — widen the bbox
            int x0 = Math.Max(0, (int)(cx - ext) - warpPad), x1 = Math.Min(W - 1, (int)(cx + ext) + 1 + warpPad);
            int y0 = Math.Max(0, (int)(cy - ext)), y1 = Math.Min(H - 1, (int)(cy + ext) + 1);
            // Raster warp band: rows are indexed from the top of the SIZE_Y-80 effect band (start = effect_y−8 = center−48).
            const double WidthOfs = 1.0;
            double warpStartY = cy - 48.0;
            for (int y = y0; y <= y1; y++)
            {
                double warpOfsX = 0;
                if (warp)
                {   // ofs_x = sin(baseDeg + addPerRow·(y−start))·(amp ± shimmer) + ((y−center)·width_a)/10
                    double w = warpAmp + (((y & 2) != 0) ? WidthOfs * warpShimmer : -WidthOfs * warpShimmer);
                    double aDeg = warpBaseDeg + warpAddPerRow * (y - warpStartY);
                    warpOfsX = Math.Sin(aDeg * Math.PI / 180.0) * w + ((y - cy) * warpWidthA) / 10.0;
                }
                for (int x = x0; x <= x1; x++)
                {
                    double rx = x - cx - warpOfsX, ry = y - cy;
                    double ux = rx * cos + ry * sin, uy = -rx * sin + ry * cos;   // un-rotate
                    double sxp = ux / scaleX + s.w / 2.0, syp = uy / scaleY + s.h / 2.0;   // un-scale → sprite space
                    int isx = (int)Math.Round(sxp), isy = (int)Math.Round(syp);
                    if (mblk > 0) { isx = isx / mblk * mblk; isy = isy / mblk * mblk; }   // mosaic: snap to block grid
                    if (isx < 0 || isx >= s.w || isy < 0 || isy >= s.h) continue;
                    if (clipAbs < 1.0) { double ny = isy / (double)s.h; if (clipTop ? ny > clipAbs : ny < 1.0 - clipAbs) continue; }
                    int si = (isy * s.w + isx) * 4; double a = s.rgba[si + 3] / 255.0 * alphaMul; if (a <= 0) continue;
                    byte r = s.rgba[si + 0], g = s.rgba[si + 1], b = s.rgba[si + 2];
                    if (tintA > 0) { r = Mix(r, tr, tintA); g = Mix(g, tg, tintA); b = Mix(b, tb, tintA); }
                    int di = (y * W + x) * 3;
                    _scene[di + 0] = Mix(_scene[di + 0], r, a); _scene[di + 1] = Mix(_scene[di + 1], g, a); _scene[di + 2] = Mix(_scene[di + 2], b, a);
                }
            }
        }

        private static byte Mix(byte bg, byte fg, double a) => (byte)Math.Clamp(bg * (1 - a) + fg * a, 0, 255);
    }
}
