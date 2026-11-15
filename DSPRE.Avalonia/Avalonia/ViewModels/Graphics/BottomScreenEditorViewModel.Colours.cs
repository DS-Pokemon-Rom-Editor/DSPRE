using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed partial class BottomScreenEditorViewModel
    {
        private void RaiseSelection()
        {
            OnPropertyChanged(nameof(Selected));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectedName));
            OnPropertyChanged(nameof(SelectedWhat));
            OnPropertyChanged(nameof(HasWhat));
            OnPropertyChanged(nameof(CanPaint));
            OnPropertyChanged(nameof(CannotEditBecause));
            OnPropertyChanged(nameof(IsAnimation));
            OnPropertyChanged(nameof(HasColours));
            OnPropertyChanged(nameof(SharedNote));
            OnPropertyChanged(nameof(IsShared));
            OnPropertyChanged(nameof(OffersColoursPatch));
            OnPropertyChanged(nameof(Caution));
            OnPropertyChanged(nameof(HasCaution));
            BuildSwatches();
        }

        private void BuildSwatches()
        {
            Swatches.Clear();
            BottomScreenPiece p = Selected;
            if (p == null || p.PaletteMember < 0) return;

            ushort[] colours = ReadColours(p);
            for (int row = 0; row < p.PaletteRows; row++)
                for (int i = 0; i < 16; i++)
                {
                    int at = (p.PaletteRow + row) * 16 + i;
                    if (at >= colours.Length) continue;
                    uint argb = ToArgb(colours[at]);
                    Swatches.Add(new BottomScreenSwatch
                    {
                        At = at,
                        Argb = argb,
                        Fill = new SolidColorBrush(Color.FromUInt32(argb)),
                        Tip = $"Row {p.PaletteRow + row}, colour {i}",
                    });
                }
        }

        private ushort[] ReadColours(BottomScreenPiece p)
        {
            try
            {
                using IDisposable pending = Pending.Reading();
                return NitroBgCodec.ReadColours(new ScriptNarc(p.Archive).Get(p.PaletteMember));
            }
            catch (Exception ex) { AppLogger.WarnOnce("BottomScreenEditorViewModel.ReadColours", ex); return Array.Empty<ushort>(); }
        }

        public uint ColourAt(int at) => Swatches.FirstOrDefault(s => s.At == at)?.Argb ?? 0xFF000000u;

        /// <summary>Stages one colour for its file and row. One group is one undo step.</summary>
        public void SetColour(int at, uint argb, object group = null)
        {
            BottomScreenPiece p = Selected;
            if (p == null || p.PaletteMember < 0) return;
            try
            {
                byte[] file;
                using (Pending.Reading()) file = new ScriptNarc(p.Archive).Get(p.PaletteMember);
                string trouble = GraphicAssets.PatchPalette(ref file, new[] { argb }, at);
                if (trouble != null) { StatusText = trouble; return; }
                Pending.Change(step => step.Put(p.Archive, p.PaletteMember, file), group);

                // The screens keep what they have read, so they are opened again to show the new colour.
                LoadScreens();
                BuildSwatches();
                Draw();
                StatusText = p.SharedWith == null
                    ? "Colour changed."
                    : "Colour changed. " + p.SharedWith;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Bottom screen colour: " + ex.Message);
                StatusText = "That colour couldn't be saved. " + ex.Message;
            }
        }

        /// <summary>Why a drawing is too big, or null. It sees the new one only inside Import.</summary>
        public string WhyOverRoom(BottomScreenPiece p)
        {
            if (p == null || p.TileRoom <= 0 || p.Drawing < 0) return null;
            int tiles;
            try { tiles = NitroBgCodec.ReadCharacters(NitroBgCodec.Inflate(new ScriptNarc(p.Archive).Get(p.Drawing))).Length / 32; }
            catch (Exception ex) { AppLogger.WarnOnce("BottomScreenEditorViewModel.WhyOverRoom", ex); return null; }
            return tiles <= p.TileRoom ? null
                : $"That drawing needs {tiles} tiles and this screen has room for {p.TileRoom}. Nothing was changed.";
        }

        /// <summary>Runs an import as one pending step. Returns its error or null.</summary>
        public string Import(Func<string> import)
        {
            string error = Pending.Import(import);
            ReloadAfterImport();
            return error;
        }

        public IDisposable Reading() => Pending.Reading();

        public void ReloadAfterImport()
        {
            LoadScreens();
            Refresh();
        }
    }
}
