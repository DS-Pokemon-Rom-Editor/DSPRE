using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>
    /// A sprite walking across the loading card: a following Pokémon in HGSS, the player in DPPt. Shown only when the
    /// overworld sprites are already unpacked, so the card never waits on them.
    /// </summary>
    public class LoadingWalker : Control
    {
        private const int Scale = 2;
        private const int FacingRight = 3;
        private readonly DispatcherTimer _timer;
        private readonly FieldWalkCycle _cycle = new();
        private readonly SpritePictures _pictures = new();

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
            _timer = SpritePictures.Clock(Step);
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
            _pictures.Clear();
            _shown = null;
        }

        private bool Choose()
        {
            try
            {
                if (!AvaloniaEditorLauncher.IsRomLoaded || !OverworldSprites.Unpacked) return false;

                _pace = Pace.Walk;
                if (RomInfo.GameFamily == RomInfo.GameFamilies.HGSS)
                {
                    (_entry, _frames) = NoticeArt.RandomFollower();
                    if (_frames > 0) return true;
                }
                // The player comes by in DP and Platinum, and in HGSS projects whose followers can't be read.
                int first = Random.Shared.Next(Players.Length);
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
            // Never unpacks: the card is up because something else is working.
            _shown = _pictures.At(_entry, FacingRight, picture, ifUnpacked: true);
            InvalidateVisual();
        }

        // Off the far edge: someone new comes on from the left.
        private void NextWalker()
        {
            _x = -32 * Scale;
            _pictures.Clear();
            _shown = null;
            if (!Choose()) { _timer.Stop(); IsVisible = false; }
        }

        public override void Render(DrawingContext context)
        {
            if (_shown == null) return;
            double w = _shown.PixelSize.Width * Scale, h = _shown.PixelSize.Height * Scale;
            SpritePictures.Draw(context, _shown, new Rect(_x, Bounds.Height - h, w, h));
        }
    }
}
