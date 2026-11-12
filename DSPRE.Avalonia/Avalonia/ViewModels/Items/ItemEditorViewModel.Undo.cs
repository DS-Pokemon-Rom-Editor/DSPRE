using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DSPRE;
using DSPRE.Avalonia;
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
using System.Text;
using System.Threading.Tasks;
using static DSPRE.ROMFiles.ItemData;
using static DSPRE.RomInfo;
using AvaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace DSPRE.Avalonia.ViewModels.Items
{
    public partial class ItemEditorViewModel
    {
        private sealed class ItemSnapshot { public byte[] Data; public uint Icon, Palette, ItemDataId, Agb; }
        private readonly UndoHistory<ItemSnapshot> _history = new();
        private DateTime _lastCaptureUtc = DateTime.MinValue;
        private const int CoalesceMs = 500;

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public void Undo() { if (_history.CanUndo) ApplyState(_history.Undo()); }
        public void Redo() { if (_history.CanRedo) ApplyState(_history.Redo()); }
        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private ItemSnapshot Snapshot() => new ItemSnapshot
        {
            Data       = _currentData?.ToByteArray(),
            Icon       = _currentEntry.ItemIcon,
            Palette    = _currentEntry.ItemPalette,
            ItemDataId = _currentEntry.ItemData,
            Agb        = _currentEntry.ItemAGB,
        };

        private void ApplyState(ItemSnapshot snap)
        {
            if (snap == null || _currentData == null) return;
            _isLoading = true;
            _currentEntry.ItemIcon    = snap.Icon;
            _currentEntry.ItemPalette = snap.Palette;
            _currentEntry.ItemData    = snap.ItemDataId;
            _currentEntry.ItemAGB     = snap.Agb;
            if (snap.Data != null) _currentData = new ItemData(new MemoryStream(snap.Data), (int)snap.ItemDataId);
            RefreshEntryBoundProps();
            PopulateFromCurrentData();
            UpdateIcon();
            UpdateSharedDataNote(_selectedItemIndex);
            _isLoading = false;

            _dataDirty = _entryDirty = _history.IsDirty;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RaiseUndoState();
        }

        private void RecordUndoSnapshot()
        {
            if (_isLoading || _currentData == null) return;
            bool coalesce = (DateTime.UtcNow - _lastCaptureUtc).TotalMilliseconds < CoalesceMs;
            _history.Capture(Snapshot(), coalesce);
            _lastCaptureUtc = DateTime.UtcNow;
            RaiseUndoState();
        }

        private void RefreshEntryBoundProps()
        {
            string iconID = _currentEntry.ItemIcon.ToString("D4");
            string palID  = _currentEntry.ItemPalette.ToString("D4");
            _selectedIconImage   = IconImages.Contains(iconID) ? iconID : null;
            _selectedIconPalette = IconPalettes.Contains(palID) ? palID  : null;
            _itemDataId          = (int)_currentEntry.ItemData;
            OnPropertyChanged(nameof(SelectedIconImage));
            OnPropertyChanged(nameof(SelectedIconPalette));
            OnPropertyChanged(nameof(ItemDataId));
        }

        private string _sharedDataNote = "";
        public string SharedDataNote { get => _sharedDataNote; private set { _sharedDataNote = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSharedDataNote)); } }
        public bool HasSharedDataNote => _sharedDataNote.Length > 0;

        // Also runs when the data id changes.
        private void UpdateSharedDataNote(int id)
        {
            if (RomInfo.IsHGE || PlatPatches.Items() == null) { SharedDataNote = ""; return; }
            List<int> others = ItemTable.SharingData(id, (int)_currentEntry.ItemData, ItemNames.Count);
            if (others.Count == 0) { SharedDataNote = ""; return; }
            string names = string.Join(", ", others.Take(4).Select(i => i < ItemNames.Count ? ItemNames[i] : $"Item {i}"));
            SharedDataNote = $"Shares its item data with {names}{(others.Count > 4 ? $" and {others.Count - 4} more" : "")}; editing it changes those too.";
        }
    }
}
