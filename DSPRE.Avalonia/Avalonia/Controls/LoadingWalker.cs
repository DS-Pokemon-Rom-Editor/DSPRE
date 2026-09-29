using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>
    /// Something from the game walking across the loading card: a random following Pokémon in HeartGold and
    /// SoulSilver, a random walking character in Diamond, Pearl and Platinum, a new one each time across.
    /// Shown only when the overworld sprites are already unpacked, so the card never waits on reading them.
    /// </summary>
    public class LoadingWalker : Control
    {
        private const int Scale = 2;
        private const int FacingRight = 3;
        private const int FrameMs = 1000 / 30;

        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(FrameMs) };
        private readonly FieldWalkCycle _cycle = new();
        private readonly Dictionary<int, Bitmap> _pictures = new();
        private static readonly Random Pick = new();
        private const int OverworldEntriesToTry = 256;
        private ushort _entry;
        private int _frames;
        private double _x;
        private Bitmap _shown;

        public LoadingWalker()
        {
            Height = 32 * Scale;
            IsHitTestVisible = false;
            _timer.Tick += (_, _) => Step();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            IsVisible = Choose();
            if (!IsVisible) return;
            _x = -32 * Scale;
            Step();
            _timer.Start();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _timer.Stop();
            foreach (var b in _pictures.Values) b.Dispose();
            _pictures.Clear();
            _shown = null;
        }

        private bool Choose()
        {
            try
            {
                if (!AvaloniaEditorLauncher.IsRomLoaded) return false;
                if (!RomInfo.gameDirs.TryGetValue(RomInfo.DirNames.OWSprites, out var dirs)) return false;
                if (!Directory.Exists(dirs.unpackedDir) || Directory.GetFiles(dirs.unpackedDir).Length == 0) return false;

                bool hgss = RomInfo.gameFamily == RomInfo.GameFamilies.HGSS;
                // DP and Platinum have no followers, so anyone with a full walk comes by instead.
                for (int tries = 0; tries < 16; tries++)
                {
                    _entry = hgss
                        ? (ushort)(HgssFollowers.FirstSprite + Pick.Next(HgssFollowers.SpeciesCount))
                        : (ushort)Pick.Next(OverworldEntriesToTry);
                    _frames = OverworldSprites.FrameCount(_entry);
                    if (hgss ? _frames > 0 : _frames >= 16) return true;
                }
                if (hgss) return false;
                _entry = 0;
                _frames = OverworldSprites.FrameCount(_entry);
                return _frames > 0;
            }
            catch (Exception ex) { AppLogger.Warn("Loading walker: " + ex.Message); }
            return false;
        }

        private void Step()
        {
            // Two-picture sprites (the HGSS followers) change on the always-running clock, not the walk.
            _cycle.Tick();
            _cycle.Walk(OverworldAnimator.WalkFrames);
            // Walking speed: one tile of sixteen pixels per step.
            _x += OverworldSprites.PixelsPerTile * Scale / (double)OverworldAnimator.WalkFrames;
            if (_x > Bounds.Width) NextWalker();
            int picture = FieldSpriteAnimation.PictureFor(_frames, FacingRight, _cycle);
            _shown = PictureAt(picture);
            InvalidateVisual();
        }

        // Off the far edge: someone new comes on from the left.
        private void NextWalker()
        {
            _x = -32 * Scale;
            foreach (var b in _pictures.Values) b.Dispose();
            _pictures.Clear();
            _shown = null;
            if (!Choose()) { _timer.Stop(); IsVisible = false; }
        }

        private Bitmap PictureAt(int picture)
        {
            if (_pictures.TryGetValue(picture, out var b)) return b;
            var pix = OverworldSprites.Get(_entry, FacingRight, picture);
            if (pix == null || pix.Width <= 0 || pix.Height <= 0) return null;
            var bmp = new WriteableBitmap(new PixelSize(pix.Width, pix.Height), new Vector(96, 96),
                                          PixelFormats.Rgba8888, AlphaFormat.Unpremul);
            using (var fb = bmp.Lock())
            {
                for (int y = 0; y < pix.Height; y++)
                    System.Runtime.InteropServices.Marshal.Copy(pix.Rgba, y * pix.Width * 4, fb.Address + y * fb.RowBytes, pix.Width * 4);
            }
            _pictures[picture] = bmp;
            return bmp;
        }

        public override void Render(DrawingContext context)
        {
            if (_shown == null) return;
            double w = _shown.PixelSize.Width * Scale, h = _shown.PixelSize.Height * Scale;
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
                context.DrawImage(_shown, new Rect(_x, Bounds.Height - h, w, h));
        }
    }
}
