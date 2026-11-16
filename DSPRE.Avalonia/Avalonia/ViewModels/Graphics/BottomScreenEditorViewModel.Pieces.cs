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
        public ObservableCollection<BottomScreenPieceRow> Pieces { get; } = new();
        public ObservableCollection<BottomScreenSwatch> Swatches { get; } = new();

        private int _selectedIndex = -1;
        public int SelectedIndex
        {
            get => _selectedIndex;
            // Selection drives the Poké Ball preview and menu cursor. Refresh draws itself after raising.
            set { if (Set(ref _selectedIndex, value)) { RaiseSelection(); Draw(); } }
        }

        public BottomScreenPiece Selected =>
            _selectedIndex >= 0 && _selectedIndex < _pieces.Count ? _pieces[_selectedIndex] : null;

        public bool HasSelection => Selected != null;
        public string SelectedName => Selected?.Name ?? "Nothing picked";
        public string SelectedWhat => Selected == null ? "Pick a piece." : Selected.What ?? "";

        public bool HasWhat => Selected == null || !string.IsNullOrEmpty(Selected.What);
        public bool CanPaint => Selected != null && Selected.Drawing >= 0 && Selected.ReadOnlyBecause == null;

        /// <summary>Why the buttons are off for this piece, or null when they are on.</summary>
        public string CannotEditBecause => Selected?.ReadOnlyBecause;

        public bool IsAnimation => Selected != null && Selected.Animation >= 0;
        public bool HasColours => Selected != null && Selected.PaletteMember >= 0;

        public string SharedNote => Selected?.SharedWith;
        public bool IsShared => !string.IsNullOrEmpty(Selected?.SharedWith);
        private bool _coloursPatchMissing;
        public bool OffersColoursPatch => _coloursPatchMissing && Selected?.IconKind >= 0;

        private void OnRomPatchStateChanged(object sender, EventArgs e)
        {
            if (!_coloursPatchMissing || !FieldMenuColoursPatch.IsApplied) return;
            _coloursPatchMissing = false;
            OnPropertyChanged(nameof(OffersColoursPatch));
            // Each icon gets its own colour rows. With unsaved edits the list waits for the next refresh.
            if (!HasUnsavedChanges) Refresh();
        }

        public void Detach() => AppEvents.RomPatchStateChanged -= OnRomPatchStateChanged;

        public string Caution => Selected?.Warning;
        public bool HasCaution => !string.IsNullOrEmpty(Selected?.Warning);

        private string _status = "";
        public string StatusText { get => _status; private set => Set(ref _status, value); }

        public bool HasUnsavedChanges => Pending.IsDirty;
        public string UnsavedChangesDescription => "Bottom screen";

        public void SaveChanges()
        {
            if (!HasUnsavedChanges) return;
            if (!DialogHelper.TrySave(Pending.Save, UnsavedChangesDescription)) return;
            ReloadAfterImport();
            SaveNotice.Saved(UnsavedChangesDescription);
        }

        public void DiscardChanges()
        {
            if (!HasUnsavedChanges) return;
            Pending.Discard();
            ReloadAfterImport();
        }
    }
}
