using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed partial class CellAnimationEditorViewModel
    {
        private void Draw()
        {
            if (_file == null || _sequence < 0 || Frames.Count == 0) { Preview = null; return; }
            try
            {
                int at = Math.Clamp(_shown, 0, Frames.Count - 1);
                int cell = Frames[at].Cell;
                // Several animations hold one drawing and move it, so the frame's own shift is applied.
                (int sx, int sy) = _file.ShiftOf(_sequence, at);
                (double degrees, double scaleX, double scaleY) = _file.TurnOf(_sequence, at);

                if (InPoketch && _inCasing)
                {
                    (int X, int Y)[] slots = _app.SpriteSlots ?? new[] { PoketchScreen.MiddleOfScreen };
                    Preview = ImageConverter.FromRgba(_casing.RenderApp(
                        false, 0, false, _app.Tiles, _app.Arrangement, _app.Sprites, _app.Cells, cell,
                        slots, _app.Fills,
                        new PoketchScreen.Motion(degrees, scaleX, scaleY, sx, sy), _app.BgLayer), DsBgScreen.Width, DsBgScreen.Height);
                    OnPropertyChanged(nameof(PlayingFrame));
                    if (_app.SpriteSlots == null && string.IsNullOrEmpty(StatusText))
                        StatusText = "Drawn in the middle, since the game sets its position in code.";
                    return;
                }

                byte[] rgba = new byte[DsBgScreen.Width * DsBgScreen.Height * 4];
                if (_banks.Count > 0 && _characters.Length > 0 && cell >= 0 && cell < _banks.Count)
                    DsBgScreen.DrawCellTurned(rgba, _banks[cell], _characters, _ => _colours,
                                              DsBgScreen.Width / 2 + sx, DsBgScreen.Height / 2 + sy,
                                              degrees, scaleX, scaleY);

                Preview = ImageConverter.FromRgba(rgba, DsBgScreen.Width, DsBgScreen.Height);
                OnPropertyChanged(nameof(PlayingFrame));
            }
            catch (Exception ex)
            {
                AppLogger.Error("Cell animation draw: " + ex.Message);
            }
        }

        public void BackToPoketch()
        {
            if (!InPoketch) return;
            Stop();
            AvaloniaEditorLauncher.OpenBottomScreenEditor(_poketchApp);
        }

        /// <summary>How many cell banks the paired layout holds; drawing numbers stay below it.</summary>
        public int CellCount => _banks.Count;

        // Shared sheet first, then this one, since cells count tiles across both.
        private static byte[] Sheet(ArchiveFiles narc, int own, int shared)
        {
            byte[] ownTiles = NitroBgCodec.ReadCharacters(NitroBgCodec.Inflate(narc.Get(own)));
            if (shared < 0) return ownTiles;

            byte[] first = NitroBgCodec.ReadCharacters(NitroBgCodec.Inflate(narc.Get(shared)));
            if (first.Length == 0) return ownTiles;

            byte[] both = new byte[first.Length + ownTiles.Length];
            first.CopyTo(both, 0);
            ownTiles.CopyTo(both, first.Length);
            return both;
        }
    }
}
