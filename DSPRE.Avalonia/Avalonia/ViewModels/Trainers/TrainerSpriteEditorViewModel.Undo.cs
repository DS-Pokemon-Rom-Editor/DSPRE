using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    public partial class TrainerSpriteEditorViewModel
    {
        // Undo covers pixels, palettes and sheet imports. The Animations text box has its own.
        private sealed class PartState
        {
            public DrawingFile Tile;
            public NcerFile Sprite;
            public byte[] Tiles;
            public System.Drawing.Color[][] Pal;
            public byte[] Ncgr, Ncer, Nanr;
        }

        private sealed class SpriteState
        {
            public PartState[] Parts;
            public int[] Flat;
            public System.Drawing.Color[][] ActivePal;
        }

        private readonly UndoHistory<SpriteState> _history = new UndoHistory<SpriteState>(100);
        private SpriteState _lastState, _savedState;
        private DateTime _lastCommit = DateTime.MinValue;
        private bool _stroking, _strokeEdited, _restoring;
        // The group of the change being committed and of the last step. One group is one step.
        private object _editGroup, _lastGroup;

        private static System.Drawing.Color[][] CopyPal(System.Drawing.Color[][] pal) =>
            pal?.Select(b => (System.Drawing.Color[])b.Clone()).ToArray();

        private SpriteState TakeState() => new SpriteState
        {
            Parts = _parts.Select(p => new PartState
            {
                Tile = p.Tile, Sprite = p.Sprite, Tiles = (byte[])p.Tile?.Tiles?.Clone(), Pal = CopyPal(p.Pal),
                Ncgr = p.Ncgr, Ncer = p.Ncer, Nanr = p.Nanr,
            }).ToArray(),
            Flat = (int[])_flatIndices?.Clone(),
            ActivePal = CopyPal(_pal),
        };

        private static bool SamePal(System.Drawing.Color[][] a, System.Drawing.Color[][] b) =>
            a == null || b == null ? a == b
            : a.Length == b.Length && a.Zip(b).All(z => z.First.Length == z.Second.Length
                && z.First.Zip(z.Second).All(c => c.First.ToArgb() == c.Second.ToArgb()));

        private static bool SameState(SpriteState a, SpriteState b) =>
            a != null && b != null && a.Parts.Length == b.Parts.Length
            && a.Parts.Zip(b.Parts).All(z => z.First.Tile == z.Second.Tile && z.First.Sprite == z.Second.Sprite
                && (z.First.Tiles ?? Array.Empty<byte>()).AsSpan().SequenceEqual(z.Second.Tiles ?? Array.Empty<byte>())
                && SamePal(z.First.Pal, z.Second.Pal))
            && (a.Flat ?? Array.Empty<int>()).AsSpan().SequenceEqual(b.Flat ?? Array.Empty<int>())
            && SamePal(a.ActivePal, b.ActivePal);

        private void ResetSteps()
        {
            _lastState = _savedState = TakeState();
            _history.Reset(_lastState);
            _stroking = _strokeEdited = false;
            RaiseSteps();
        }

        private void RaiseSteps() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        /// <summary>A pointer drag on the canvas starts; everything it paints is one undo step.</summary>
        public void BeginStroke() { _stroking = true; _strokeEdited = false; }

        public void EndStroke()
        {
            if (!_stroking) return;
            _stroking = false;
            if (_strokeEdited) { _strokeEdited = false; Commit(coalesce: false); }
        }

        private void EditCommitted()
        {
            object group = _editGroup;
            _editGroup = null;
            if (_restoring || _lastState == null) return;
            if (_stroking) { _strokeEdited = true; return; }
            Commit(group != null ? ReferenceEquals(group, _lastGroup) : (DateTime.UtcNow - _lastCommit).TotalMilliseconds < 500, group);
        }

        private void Commit(bool coalesce, object group = null)
        {
            SpriteState now = TakeState();
            if (SameState(now, _lastState)) return;
            _history.Capture(now, coalesce && _history.CanUndo);
            _lastState = now;
            _lastCommit = DateTime.UtcNow;
            _lastGroup = group;
            RaiseSteps();
        }

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;

        public void Undo() { if (_history.CanUndo) { _lastGroup = null; Restore(_history.Undo()); } }
        public void Redo() { if (_history.CanRedo) { _lastGroup = null; Restore(_history.Redo()); } }

        private static void PutPal(System.Drawing.Color[][] pal, System.Drawing.Color[][] colours)
        {
            if (pal == null || colours == null) return;
            for (int b = 0; b < Math.Min(pal.Length, colours.Length); b++)
                for (int i = 0; i < Math.Min(pal[b].Length, colours[b].Length); i++) pal[b][i] = colours[b][i];
        }

        private void Restore(SpriteState state)
        {
            bool layoutChanged = false;
            for (int i = 0; i < Math.Min(_parts.Count, state.Parts.Length); i++)
            {
                SpritePart part = _parts[i];
                PartState was = state.Parts[i];
                layoutChanged |= part.Sprite != was.Sprite || part.Tile != was.Tile;
                part.Tile = was.Tile; part.Sprite = was.Sprite;
                part.Ncgr = was.Ncgr; part.Ncer = was.Ncer; part.Nanr = was.Nanr;
                if (part.Tile != null && was.Tiles != null)
                {
                    if (part.Tile.Tiles != null && part.Tile.Tiles.Length == was.Tiles.Length) Array.Copy(was.Tiles, part.Tile.Tiles, was.Tiles.Length);
                    else part.Tile.SetTiles((byte[])was.Tiles.Clone());
                }
                PutPal(part.Pal, was.Pal);
            }
            if (state.Flat != null) _flatIndices = (int[])state.Flat.Clone();
            PutPal(_pal, state.ActivePal);

            _lastState = state;
            _restoring = true;
            try
            {
                _dirty = !SameState(state, _savedState);
                _paletteDirty = _dirty;
            }
            finally { _restoring = false; }
            OnPropertyChanged(nameof(HasUnsavedChanges));

            if (IsFlatSheetMode)
            {
                BuildPaletteSwatches(Math.Max(0, _activePaletteBank));
                RebuildFlatCanvas();
            }
            else if (layoutChanged)
            {
                // The same refresh a sheet import does, since the cells may differ.
                Activate(Math.Min(_activePart, _parts.Count - 1));
                BuildFrameThumbnails();
                _activePaletteBank = -1;
                _selectedStripFrame = -1;
                SelectedFrameIndex = 0;
                OnPropertyChanged(nameof(FrameCount));
                RebuildAnimCellChoices();
                StopAnimPreview();
                _romAnim = OpenRomAnimations();
                OnPropertyChanged(nameof(CanEditAnimFrames));
                SetAnimJsonTextSilent(AnimationJsonOf(_romAnim ?? OpenFrames(0).Animations) ?? "");
                OnPropertyChanged(nameof(HasAnimation));
            }
            else
            {
                if (_parts.Count > 0) Activate(_activePart);
                if (_activePaletteBank >= 0) BuildPaletteSwatches(_activePaletteBank);
                RebuildCompositedCanvas();
                BuildFrameThumbnails();
                RebuildTopBar();
            }
            RaiseSteps();
        }

        // Apart from _dirty, so an animation-only save leaves the pixel files and scan copy alone.
        private bool _romAnimDirty;
        public string UnsavedChangesDescription => $"{_set.Title} ({_set.Noun} {_trClassID})";

        private readonly TrainerSpriteSet _set = TrainerSpriteSet.Classes;
        public string Title => _set.Title;
        public string ItemLabel => _set.ItemLabel;
        public DirNames Archive => _set.Archive;
        public void SaveChanges()
        {
            if (SaveAll() == null) SaveNotice.Saved(UnsavedChangesDescription);
        }

        public Task<bool> SaveChangesAsync()
        {
            string error = SaveAll();
            if (error != null) return Task.FromException<bool>(new InvalidOperationException(error));
            SaveNotice.Saved(UnsavedChangesDescription);
            return Task.FromResult(!HasUnsavedChanges);
        }

        /// <summary>Saves whichever of the sprite and its animation have edits. Null on success.</summary>
        public string SaveAll()
        {
            if (_dirty)
            {
                string error = Save();
                if (error != null) return error;
            }
            if (_romAnimDirty)
            {
                string error = SaveRomAnimations();
                if (error != null) return error;
            }
            return AnimJsonDirty ? SaveAnimJson() : null;
        }

        /// <summary>Reads the class from disk again, since paint goes straight into memory.</summary>
        public void DiscardChanges() => Load(_trClassID);
    }
}
