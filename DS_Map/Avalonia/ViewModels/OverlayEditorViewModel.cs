using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Media;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;

namespace DSPRE.Avalonia.ViewModels
{
    public class OverlayRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (Equals(f, v)) return false;
            f = v;
            OnPropertyChanged(n);
            return true;
        }

        public int Number { get; init; }

        private bool _isCompressed;
        public bool IsCompressed
        {
            get => _isCompressed;
            set => Set(ref _isCompressed, value);
        }

        private bool _isMarkedCompressed;
        public bool IsMarkedCompressed
        {
            get => _isMarkedCompressed;
            set => Set(ref _isMarkedCompressed, value);
        }

        public string RAMAddressHex { get; init; }
        public uint UncompressedSize { get; init; }

        private IBrush _mismatchBrush = Brushes.Transparent;
        public IBrush MismatchBrush
        {
            get => _mismatchBrush;
            set => Set(ref _mismatchBrush, value);
        }
    }

    public class OverlayEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (Equals(f, v)) return false;
            f = v;
            OnPropertyChanged(n);
            return true;
        }

        // ----------------------------------------------------------------
        // IEditorWithUnsavedChanges
        // ----------------------------------------------------------------

        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Overlay Editor";
        void IEditorWithUnsavedChanges.SaveChanges() => _ = SaveChangesCore();
        public void DiscardChanges() => SetClean();

        // ----------------------------------------------------------------
        // Observable state
        // ----------------------------------------------------------------

        public ObservableCollection<OverlayRow> Overlays { get; } = new();

        private string _title = "Overlay Editor";
        public string Title { get => _title; private set => Set(ref _title, value); }

        private bool _isDsRomProject;
        public bool IsDsRomProject => _isDsRomProject;
        public bool IsNotDsRomProject => !_isDsRomProject;

        private bool _saveEnabled = true;
        public bool SaveEnabled { get => _saveEnabled; private set => Set(ref _saveEnabled, value); }

        // Toggle state for bulk buttons (flip-flop like the original)
        private bool _currentValComp = true;
        private bool _currentValMark = true;

        // ----------------------------------------------------------------
        // Constructor
        // ----------------------------------------------------------------

        public OverlayEditorViewModel()
        {
            _isDsRomProject = RomInfo.IsDsRomProject;
            SaveEnabled = !_isDsRomProject;
            LoadOverlays();
        }

        // ----------------------------------------------------------------
        // Commands
        // ----------------------------------------------------------------

        public void ToggleAllCompressed()
        {
            foreach (var row in Overlays)
                row.IsCompressed = _currentValComp;
            _currentValComp = !_currentValComp;
            RefreshMismatch();
            SetDirty();
        }

        public void ToggleAllMarked()
        {
            foreach (var row in Overlays)
                row.IsMarkedCompressed = _currentValMark;
            _currentValMark = !_currentValMark;
            RefreshMismatch();
            SetDirty();
        }

        public void OnRowChanged()
        {
            RefreshMismatch();
            SetDirty();
        }

        public void RevertChanges()
        {
            LoadOverlays();
            SetClean();
        }

        public async Task SaveChangesCore()
        {
            if (_isDsRomProject)
            {
                await DialogHelper.ShowInfo(
                    "Overlay compression cannot be modified in ds-rom format.\n\n" +
                    "ds-rom automatically decompresses overlays when extracting and recompresses them when building the ROM.",
                    "Read-Only Mode");
                return;
            }

            var original = BuildOriginalList();
            var modified = new List<OverlayRow>();
            var modifiedNumbers = new List<string>();

            for (int i = 0; i < original.Count; i++)
            {
                var orig = original[i];
                var cur  = Overlays[i];
                if (orig.IsCompressed != cur.IsCompressed || orig.IsMarkedCompressed != cur.IsMarkedCompressed)
                {
                    modified.Add(cur);
                    modifiedNumbers.Add(cur.Number.ToString());
                }
            }

            if (HasMismatches())
            {
                await DialogHelper.ShowInfo(
                    "There are some overlays in a compression state that does not match the set value for compression in the y9 table.\n" +
                    "This may cause errors or lack of usability on hardware.\n" +
                    "You can find the mismatched rows highlighted in RED.\nThis message is purely informational.",
                    "Compression Mark Mismatch");
            }

            bool proceed = await DialogHelper.AskYesNo(
                "This operation will modify the following overlays: " + Environment.NewLine +
                string.Join(", ", modifiedNumbers) + "\nProceed?",
                "Confirmation required");

            if (!proceed) return;

            bool hasCompressing = false;
            foreach (var ovl in modified)
            {
                OverlayUtils.OverlayTable.SetDefaultCompressed(ovl.Number, ovl.IsMarkedCompressed);
                if (ovl.IsCompressed && !OverlayUtils.IsCompressed(ovl.Number))
                    hasCompressing = true; // compression temporarily disabled
                if (!ovl.IsCompressed && OverlayUtils.IsCompressed(ovl.Number))
                    OverlayUtils.Decompress(ovl.Number);
            }

            SetClean();

            if (hasCompressing)
                await DialogHelper.ShowInfo("Compression is temporarily disabled until we work on a fix.", "Warning");
        }

        public async Task<bool> ConfirmCloseAsync()
        {
            if (!_dirty) return true;

            var result = await DialogHelper.AskYesNoCancel(
                "There are unsaved changes to overlays.\nDo you want to save them before closing?",
                "Overlay Editor - Unsaved Changes");

            if (result == DialogHelper.MsgResult.Yes)
            {
                await SaveChangesCore();
                return true;
            }
            return result == DialogHelper.MsgResult.No;
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        private void LoadOverlays()
        {
            Overlays.Clear();
            int count = OverlayUtils.OverlayTable.GetNumberOfOverlays();
            for (int i = 0; i < count; i++)
            {
                var row = new OverlayRow
                {
                    Number             = i,
                    IsCompressed       = OverlayUtils.IsCompressed(i),
                    IsMarkedCompressed = OverlayUtils.OverlayTable.IsDefaultCompressed(i),
                    RAMAddressHex      = $"0x{OverlayUtils.OverlayTable.GetRAMAddress(i):X}",
                    UncompressedSize   = OverlayUtils.OverlayTable.GetUncompressedSize(i),
                };
                row.PropertyChanged += (_, _) => OnRowChanged();
                Overlays.Add(row);
            }
            RefreshMismatch();
        }

        private List<OverlayRow> BuildOriginalList()
        {
            int count = OverlayUtils.OverlayTable.GetNumberOfOverlays();
            var list = new List<OverlayRow>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(new OverlayRow
                {
                    Number             = i,
                    IsCompressed       = OverlayUtils.IsCompressed(i),
                    IsMarkedCompressed = OverlayUtils.OverlayTable.IsDefaultCompressed(i),
                    RAMAddressHex      = string.Empty,
                    UncompressedSize   = 0,
                });
            }
            return list;
        }

        private void RefreshMismatch()
        {
            if (_isDsRomProject)
            {
                foreach (var r in Overlays) r.MismatchBrush = Brushes.Transparent;
                return;
            }
            foreach (var r in Overlays)
                r.MismatchBrush = (r.IsCompressed != r.IsMarkedCompressed) ? Brushes.Red : Brushes.Transparent;
        }

        private bool HasMismatches()
        {
            if (_isDsRomProject) return false;
            foreach (var r in Overlays)
                if (r.IsCompressed != r.IsMarkedCompressed) return true;
            return false;
        }

        private void SetDirty()
        {
            _dirty = true;
            Title = "Overlay Editor*";
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void SetClean()
        {
            _dirty = false;
            Title = "Overlay Editor";
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }
}
