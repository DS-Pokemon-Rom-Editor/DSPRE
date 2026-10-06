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
    /// Something from the game crossing the loading card, a new one each time across: a random following Pokémon
    /// in HeartGold and SoulSilver; the player in Diamond, Pearl and Platinum, either one, running, cycling or surfing.
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

        private enum Pace { Walk, Run, Bike }

        // The same entries in Diamond, Pearl and Platinum (SPRITE_HERO, CYCLEHERO, HEROINE, CYCLEHEROINE, SWIMHERO
        // and SWIMHEROINE in pokediamond; OBJ_EVENT_GFX_PLAYER_* in pokeplatinum).
        private static readonly (ushort Entry, Pace Pace)[] Players =
        {
            (0, Pace.Run), (97, Pace.Run), (21, Pace.Bike), (98, Pace.Bike), (178, Pace.Walk), (179, Pace.Walk),
        };
        private const int BikeFrames = 4;
        private Pace _pace;
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
            foreach (Bitmap b in _pictures.Values) b.Dispose();
            _pictures.Clear();
            _shown = null;
        }

        private bool Choose()
        {
            try
            {
                if (!AvaloniaEditorLauncher.IsRomLoaded) return false;
                if (!RomInfo.gameDirs.TryGetValue(RomInfo.DirNames.OWSprites, out (string packedDir, string unpackedDir) dirs)) return false;
                if (!Directory.Exists(dirs.unpackedDir) || Directory.GetFiles(dirs.unpackedDir).Length == 0) return false;

                _pace = Pace.Walk;
                if (RomInfo.gameFamily != RomInfo.GameFamilies.HGSS)
                {
                    // DP and Platinum have no followers, so the player comes by instead.
                    int first = Pick.Next(Players.Length);
                    for (int i = 0; i < Players.Length; i++)
                    {
                        (ushort entry, Pace pace) = Players[(first + i) % Players.Length];
                        int frames = OverworldSprites.FrameCount(entry);
                        if (frames <= 0) continue;
                        _entry = entry; _frames = frames; _pace = pace;
                        return true;
                    }
                    return false;
                }
                for (int tries = 0; tries < 16; tries++)
                {
                    _entry = (ushort)(HgssFollowers.FirstSprite + Pick.Next(HgssFollowers.SpeciesCount));
                    _frames = OverworldSprites.FrameCount(_entry);
                    if (_frames > 0) return true;
                }
                return false;
            }
            catch (Exception ex) { AppLogger.Warn("Loading walker: " + ex.Message); }
            return false;
        }

        private void Step()
        {
            // Two-picture sprites (the HGSS followers) change on the always-running clock, not the walk.
            _cycle.Tick();
            int stepFrames = _pace == Pace.Run ? FieldMovementScript.RunFrames : _pace == Pace.Bike ? BikeFrames : OverworldAnimator.WalkFrames;
            if (_pace == Pace.Run) _cycle.Dash(); else _cycle.Walk(stepFrames);
            // One tile of sixteen pixels per step.
            _x += OverworldSprites.PixelsPerTile * Scale / (double)stepFrames;
            if (_x > Bounds.Width) NextWalker();
            int picture = FieldSpriteAnimation.PictureFor(_frames, FacingRight, _cycle);
            _shown = PictureAt(picture);
            InvalidateVisual();
        }

        // Off the far edge: someone new comes on from the left.
        private void NextWalker()
        {
            _x = -32 * Scale;
            foreach (Bitmap b in _pictures.Values) b.Dispose();
            _pictures.Clear();
            _shown = null;
            if (!Choose()) { _timer.Stop(); IsVisible = false; }
        }

        private Bitmap PictureAt(int picture)
        {
            if (_pictures.TryGetValue(picture, out Bitmap b)) return b;
            OverworldSprites.SpritePixels pix = OverworldSprites.Get(_entry, FacingRight, picture);
            if (pix == null || pix.Width <= 0 || pix.Height <= 0) return null;
            WriteableBitmap bmp = new WriteableBitmap(new PixelSize(pix.Width, pix.Height), new Vector(96, 96),
                                          PixelFormats.Rgba8888, AlphaFormat.Unpremul);
            using (ILockedFramebuffer fb = bmp.Lock())
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
