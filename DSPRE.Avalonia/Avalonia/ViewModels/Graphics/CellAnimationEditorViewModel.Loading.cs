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
        private void Load()
        {
            try
            {
                ArchiveFiles narc = _source;
                if (!narc.Available) { StatusText = "This game doesn't have that archive."; return; }

                // These files are compressed in the ROM, so saving compresses each one the same way again.
                byte[] stored = narc.Get(_animation);
                _animationMarker = GraphicAssets.SqueezeMarker(stored);
                byte[] raw = GraphicAssets.Unsqueeze(stored);

                _file = NanrFile.Read(raw);
                if (_file == null) { StatusText = "That file isn't a cell animation."; return; }

                if (_cells >= 0)
                {
                    byte[] held = narc.Get(_cells);
                    _layoutMarker = GraphicAssets.SqueezeMarker(held);
                    byte[] layout = GraphicAssets.Unsqueeze(held);
                    _layout = NcerFile.Read(layout);
                    _banks = DsBgScreen.ReadCells(layout);
                }
                if (_sprites >= 0) _characters = Sheet(narc, _sprites, _sharedSheet);
                MarkSaved();
                _history.Reset(Now());
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));

                if (_poketchApp >= 0)
                {
                    _app = PoketchApps.All.FirstOrDefault(a => a.Id == _poketchApp);
                    _casing = PoketchScreen.Load();
                }

                if (_palette >= 0)
                {
                    ushort[] all = NitroBgCodec.ReadColours(NitroBgCodec.Inflate(narc.Get(_palette)));
                    _colours = DsBgScreen.Row(all, Math.Max(0, _paletteRow));
                }
                OnPropertyChanged(nameof(FileNote));
            }
            catch (Exception ex)
            {
                AppLogger.Error("Cell animation load: " + ex.Message);
                StatusText = "That file couldn't be read. " + ex.Message;
            }
        }

        private void FillSequences()
        {
            Sequences.Clear();
            if (_file == null) return;
            for (int i = 0; i < _file.Sequences.Count; i++)
            {
                NanrFile.Sequence s = _file.Sequences[i];
                Sequences.Add(new CellSequenceRow
                {
                    Number = i, Name = s.Name, Frames = s.Frames.Count,
                    Element = s.ElementType, PlayMode = s.PlayMode,
                });
            }
            if (Sequences.Count > 0) SelectedSequence = 0;
        }

        private void FillFrames()
        {
            if (_file == null) return;
            _filling = true;
            foreach (CellFrameRow row in Frames) row.Changed = null;
            Frames.Clear();

            if (_sequence >= 0 && _sequence < _file.Sequences.Count)
            {
                NanrFile.Sequence s = _file.Sequences[_sequence];
                for (int i = 0; i < s.Frames.Count; i++)
                {
                    CellFrameRow row = new CellFrameRow
                    {
                        Number = i,
                        Element = s.ElementType,
                        SharedNote = ShareNote(i),
                    };
                    Fill(row);
                    row.Changed = FrameEdited;
                    Frames.Add(row);
                }

                _playMode = (int)Math.Clamp(s.PlayMode, 1, 4) - 1;
                _loopStart = Math.Clamp(s.LoopStartFrame + 1, 1, Math.Max(1, s.Frames.Count));
                _animationType = Math.Clamp((int)s.AnimationType, 1, 2) - 1;
            }
            _filling = false;
            _lastEditFrame = -1;
            _frame = 0; _shown = 0; _reverse = false;
            OnPropertyChanged(nameof(PlayModeIndex));
            OnPropertyChanged(nameof(CanLoop));
            OnPropertyChanged(nameof(LoopStart));
            OnPropertyChanged(nameof(FrameCount));
            OnPropertyChanged(nameof(AnimationTypeIndex));
            OnPropertyChanged(nameof(AnimationTypeWarning));
            OnPropertyChanged(nameof(HasAnimationTypeWarning));

            _pieceCell = Frames.Count > 0 ? Frames[0].Cell : 0;
            OnPropertyChanged(nameof(PieceCell));
            OnPropertyChanged(nameof(PieceHeading));
            FillPieces();
            Draw();
        }
    }
}
