using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using global::Avalonia;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform;

namespace DSPRE.Avalonia
{
    public sealed class BattleSceneCompositor
    {
        public const int W = 256, H = 192;
        private byte[] _backdrop;
        private readonly List<(byte[] rgba, int w, int h, int left, int top)> _statics = new();
        private (byte[] rgba, int w, int h, int left, int top) _back, _front;
        private readonly byte[] _scene = new byte[W * H * 3];
        private readonly byte[] _out = new byte[W * H * 4];

        public void SetBackdrop(byte[] rgbWxH) => _backdrop = rgbWxH;
        public void ClearStatics() => _statics.Clear();
        public void AddStatic(byte[] rgba, int w, int h, int left, int top) { if (rgba != null) _statics.Add((rgba, w, h, left, top)); }
        public void SetPlayer(byte[] rgba, int w, int h, int left, int top) => _back = (rgba, w, h, left, top);
        public void SetEnemy(byte[] rgba, int w, int h, int left, int top) => _front = (rgba, w, h, left, top);

        public WriteableBitmap Render(BattleAnimPlayer player)
        {
            if (_backdrop != null && _backdrop.Length == _scene.Length)
            {
                if (player != null && player.RasterActive)
                {
                    for (int y = 0; y < H; y++)
                    {
                        int off = (int)Math.Round(player.RasterAmp * Math.Sin(player.RasterPhase + y * player.RasterLineAdd));
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
            bool bgBehind = player != null && player.HasBackground && !player.BackgroundIsOverlay;
            if (bgBehind) CrossfadeBg(player, player.BgCa);

            foreach (var s in _statics) BlitAxisAligned(s.rgba, s.w, s.h, s.left, s.top);

            if (player != null && player.Grayscale)
                for (int i = 0; i < W * H * 3; i += 3)
                {
                    byte y8 = (byte)((_scene[i] * 77 + _scene[i + 1] * 150 + _scene[i + 2] * 29) >> 8);
                    _scene[i] = _scene[i + 1] = _scene[i + 2] = y8;
                }

            if (player != null && player.BgFlashAmount > 0)
            {
                double k = Math.Clamp(player.BgFlashAmount, 0, 1); double ik = 1 - k;
                byte fr = player.BgFlashR, fg = player.BgFlashG, fb = player.BgFlashB;
                for (int i = 0; i < W * H * 3; i += 3)
                {
                    _scene[i] = (byte)(_scene[i] * ik + fr * k);
                    _scene[i + 1] = (byte)(_scene[i + 1] * ik + fg * k);
                    _scene[i + 2] = (byte)(_scene[i + 2] * ik + fb * k);
                }
            }

            if (player != null && player.FadeOpacity > 0)
            {
                double k = Math.Clamp(player.FadeOpacity, 0, 1); double ik = 1 - k;
                byte fr = player.FadeR, fg = player.FadeG, fb = player.FadeB;
                for (int i = 0; i < W * H * 3; i += 3)
                {
                    _scene[i] = (byte)(_scene[i] * ik + fr * k);
                    _scene[i + 1] = (byte)(_scene[i + 1] * ik + fg * k);
                    _scene[i + 2] = (byte)(_scene[i + 2] * ik + fb * k);
                }
            }

            byte tr = player?.TintR ?? 0, tg = player?.TintG ?? 0, tb = player?.TintB ?? 0;
            if (player != null)
                foreach (var gh in player.Ghosts)
                {
                    var gs = gh.Mon == 0 ? _back : _front;
                    BlitMon(gs, true, gh.Dx, gh.Dy, gh.ScaleX, gh.ScaleY, 0, gh.TintA, gh.TintR, gh.TintG, gh.TintB, gh.Alpha);
                }
            for (int m = 0; m < 2; m++)
            {
                var s = m == 0 ? _back : _front;
                bool vis = player?.MonVisible[m] ?? true;
                double dx = (player?.MonDX[m] ?? 0) + (player?.MonShakeX[m] ?? 0);
                double dy = (player?.MonDY[m] ?? 0) + (player?.MonShakeY[m] ?? 0);
                double scx = player?.MonScaleX[m] ?? 1, scy = player?.MonScaleY[m] ?? 1;
                double rot = player?.MonRot[m] ?? 0, ta = player?.MonTintA[m] ?? 0;
                bool warp = player != null && player.MonWarpMon == m;
                BlitMon(s, vis, dx, dy, scx, scy, rot, ta, tr, tg, tb, player?.MonAlpha[m] ?? 1.0, (int)(player?.MonMosaic[m] ?? 0), player?.MonClip[m] ?? 1,
                    warp, warp ? player.MonWarpAmp : 0, warp ? player.MonWarpBaseDeg : 0, warp ? player.MonWarpAddPerRow : 0,
                    warp ? player.MonWarpWidthA : 0, warp ? player.MonWarpShimmer : 0);
            }

            if (player != null)
            {
                var caps = new List<BattleAnimPlayer.DroppedCap>(player.Caps);
                caps.Sort((x, y) => y.Priority.CompareTo(x.Priority));
                foreach (var cap in caps)
                {
                    if (!cap.Visible) continue;
                    var cs = cap.SrcMon == 0 ? _back : _front;
                    BlitMon(cs, true, cap.Dx, cap.Dy, cap.ScaleX, cap.ScaleY, cap.RotDeg, cap.TintA, cap.TintR, cap.TintG, cap.TintB, cap.Alpha, (int)cap.Mosaic,
                            clipOutX0: cap.ClipOutX0, clipOutY0: cap.ClipOutY0, clipOutX1: cap.ClipOutX1, clipOutY1: cap.ClipOutY1);
                }
            }

            if (player != null) BlitCellActors(player);

            if (player != null && player.HasBackground && player.BackgroundIsOverlay) OverlayBg(player, player.BgCa, player.BgCb);

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

        private void BlitCellActors(BattleAnimPlayer player)
        {
            var cells = player.Cells;
            if (cells == null || !cells.Loaded) return;
            foreach (var a in player.SpriteActors)
            {
                if (!a.Visible || a.Alpha <= 0) continue;
                var cp = cells.RenderCellRgba(a.CellIndex);
                if (cp.Rgba == null) continue;
                int S = cp.Size; double half = S / 2.0;
                double sclX = a.ScaleX * a.FrameScaleX, sclY = a.ScaleY * a.FrameScaleY;
                if (sclX <= 0.0001 || sclY <= 0.0001) continue;
                double cx = a.X + a.FrameX, cy = a.Y + a.FrameY;
                double rot = a.FrameRotDeg * Math.PI / 180.0;
                bool rotated = Math.Abs(a.FrameRotDeg) > 0.01;
                double cosR = Math.Cos(-rot), sinR = Math.Sin(-rot);
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

        private void CrossfadeBg(BattleAnimPlayer player, double ca)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (!player.TrySampleBg(x, y, out byte r, out byte g, out byte b, out byte a) || a == 0) continue;
                    double k = a / 255.0 * ca; int i = (y * W + x) * 3;
                    _scene[i + 0] = Mix(_scene[i + 0], r, k); _scene[i + 1] = Mix(_scene[i + 1], g, k); _scene[i + 2] = Mix(_scene[i + 2], b, k);
                }
        }

        private void OverlayBg(BattleAnimPlayer player, double ca, double cb)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (!player.TrySampleBg(x, y, out byte r, out byte g, out byte b, out byte a) || a == 0) continue;
                    double wa = a / 255.0; int i = (y * W + x) * 3;
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

        private void BlitMon((byte[] rgba, int w, int h, int left, int top) s, bool visible, double dx, double dy,
                             double scaleX, double scaleY, double rotDeg, double tintA, byte tr, byte tg, byte tb,
                             double alphaMul = 1.0, int mosaic = 0, double clip = 1.0,
                             bool warp = false, double warpAmp = 0, double warpBaseDeg = 0, double warpAddPerRow = 0,
                             double warpWidthA = 0, int warpShimmer = 0,
                             double clipOutX0 = 0, double clipOutY0 = 0, double clipOutX1 = -1, double clipOutY1 = -1)
        {
            if (s.rgba == null || !visible || alphaMul <= 0) return;
            bool hasClipOut = clipOutX1 >= clipOutX0 && clipOutY1 >= clipOutY0;
            int mblk = mosaic > 0 ? mosaic + 1 : 0;
            double clipAbs = Math.Min(1.0, Math.Abs(clip)); bool clipTop = clip >= 0;
            double cx = s.left + s.w / 2.0 + dx, cy = s.top + s.h / 2.0 + dy;
            double rad = rotDeg * Math.PI / 180.0, cos = Math.Cos(rad), sin = Math.Sin(rad);
            double sxAbs = Math.Max(0.01, Math.Abs(scaleX)), syAbs = Math.Max(0.01, Math.Abs(scaleY));
            double hw = s.w / 2.0 * sxAbs, hh = s.h / 2.0 * syAbs;
            double ext = Math.Sqrt(hw * hw + hh * hh);
            int warpPad = warp ? 64 : 0;
            int x0 = Math.Max(0, (int)(cx - ext) - warpPad), x1 = Math.Min(W - 1, (int)(cx + ext) + 1 + warpPad);
            int y0 = Math.Max(0, (int)(cy - ext)), y1 = Math.Min(H - 1, (int)(cy + ext) + 1);
            const double WidthOfs = 1.0;
            double warpStartY = cy - 48.0;
            for (int y = y0; y <= y1; y++)
            {
                double warpOfsX = 0;
                if (warp)
                {
                    double w = warpAmp + (((y & 2) != 0) ? WidthOfs * warpShimmer : -WidthOfs * warpShimmer);
                    double aDeg = warpBaseDeg + warpAddPerRow * (y - warpStartY);
                    warpOfsX = Math.Sin(aDeg * Math.PI / 180.0) * w + ((y - cy) * warpWidthA) / 10.0;
                }
                for (int x = x0; x <= x1; x++)
                {
                    if (hasClipOut && x >= clipOutX0 && x < clipOutX1 && y >= clipOutY0 && y < clipOutY1) continue;
                    double rx = x - cx - warpOfsX, ry = y - cy;
                    double ux = rx * cos + ry * sin, uy = -rx * sin + ry * cos;
                    double sxp = ux / scaleX + s.w / 2.0, syp = uy / scaleY + s.h / 2.0;
                    int isx = (int)Math.Round(sxp), isy = (int)Math.Round(syp);
                    if (mblk > 0) { isx = isx / mblk * mblk; isy = isy / mblk * mblk; }
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
