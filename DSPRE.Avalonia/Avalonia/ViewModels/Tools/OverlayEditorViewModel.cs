using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;

using DSPRE.Avalonia.Data;
namespace DSPRE.Avalonia.ViewModels.Tools
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

    public class OverlayEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
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

        // What was last loaded or saved, so the dirty state compares real values.
        private (bool Compressed, bool Marked)[] _loaded = Array.Empty<(bool, bool)>();
        public bool HasUnsavedChanges
        {
            get
            {
                if (_loaded.Length != Overlays.Count) return false;
                for (int i = 0; i < Overlays.Count; i++)
                    if (_loaded[i] != (Overlays[i].IsCompressed, Overlays[i].IsMarkedCompressed)) return true;
                return false;
            }
        }
        public string UnsavedChangesDescription => "Overlay Editor";
        void IEditorWithUnsavedChanges.SaveChanges() => _ = SaveChangesCore();
        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            await SaveChangesCore();
            return !HasUnsavedChanges;
        }
        public void DiscardChanges() => RevertChanges();

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

        // The value each bulk toggle button applies next.
        private bool _currentValComp = true;
        private bool _currentValMark = true;

        public OverlayEditorViewModel()
        {
            if (Design.IsDesignMode)
            {
                Title = "Overlay Editor (Preview)";
                SaveEnabled = true;
                for (int i = 0; i < 5; i++)
                    Overlays.Add(new OverlayRow
                    {
                        Number = i,
                        IsCompressed = i % 2 == 0,
                        IsMarkedCompressed = i % 2 == 0,
                        RAMAddressHex = $"0200{i * 100:X4}",
                        UncompressedSize = (uint)(4096 + i * 512)
                    });
                return;
            }
            _isDsRomProject = RomInfo.IsDsRomProject;
            SaveEnabled = !_isDsRomProject;
            LoadOverlays();
            SetClean();
            ResetUndo();
        }

        // ----------------------------------------------------------------
        // Commands
        // ----------------------------------------------------------------

        public void ToggleAllCompressed()
        {
            foreach (OverlayRow row in Overlays)
                row.IsCompressed = _currentValComp;
            _currentValComp = !_currentValComp;
            RefreshMismatch();
            SetDirty();
        }

        public void ToggleAllMarked()
        {
            foreach (OverlayRow row in Overlays)
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
            ResetUndo();
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

            List<OverlayRow> original = BuildOriginalList();
            List<OverlayRow> modified = new List<OverlayRow>();
            List<string> modifiedNumbers = new List<string>();

            for (int i = 0; i < original.Count; i++)
            {
                OverlayRow orig = original[i];
                OverlayRow cur  = Overlays[i];
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
            foreach (OverlayRow ovl in modified)
            {
                OverlayUtils.OverlayTable.SetDefaultCompressed(ovl.Number, ovl.IsMarkedCompressed);
                if (ovl.IsCompressed && !OverlayUtils.IsCompressed(ovl.Number))
                    hasCompressing = true; // compression temporarily disabled
                if (!ovl.IsCompressed && OverlayUtils.IsCompressed(ovl.Number))
                    OverlayUtils.Decompress(ovl.Number);
            }

            // Compressing is off, and a decompress can fail, so each row shows what is on disk now.
            foreach (OverlayRow ovl in modified)
                ovl.IsCompressed = OverlayUtils.IsCompressed(ovl.Number);
            RefreshMismatch();

            SetClean();
            SaveNotice.Saved(UnsavedChangesDescription);

            if (hasCompressing)
                await DialogHelper.ShowInfo("Compression is temporarily disabled until we work on a fix.", "Warning");
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
                OverlayRow row = new OverlayRow
                {
                    Number             = i,
                    IsCompressed       = CompressedNow(i),
                    IsMarkedCompressed = OverlayUtils.OverlayTable.IsDefaultCompressed(i),
                    RAMAddressHex      = $"0x{OverlayUtils.OverlayTable.GetRAMAddress(i):X}",
                    UncompressedSize   = OverlayUtils.OverlayTable.GetUncompressedSize(i),
                };
                row.PropertyChanged += (_, e) => { if (e.PropertyName != nameof(OverlayRow.MismatchBrush)) OnRowChanged(); };
                Overlays.Add(row);
            }
            RefreshMismatch();
        }

        // ds-rom keeps overlays flat on disk and compresses the ones its config marks when it builds.
        private bool CompressedNow(int ovNumber) =>
            _isDsRomProject ? OverlayUtils.OverlayTable.IsDefaultCompressed(ovNumber) : OverlayUtils.IsCompressed(ovNumber);

        private List<OverlayRow> BuildOriginalList()
        {
            int count = OverlayUtils.OverlayTable.GetNumberOfOverlays();
            List<OverlayRow> list = new List<OverlayRow>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(new OverlayRow
                {
                    Number             = i,
                    IsCompressed       = CompressedNow(i),
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
                foreach (OverlayRow r in Overlays) r.MismatchBrush = Brushes.Transparent;
                return;
            }
            foreach (OverlayRow r in Overlays)
                r.MismatchBrush = (r.IsCompressed != r.IsMarkedCompressed) ? StatusBrushes.Bad : StatusBrushes.None;
        }

        private bool HasMismatches()
        {
            if (_isDsRomProject) return false;
            foreach (OverlayRow r in Overlays)
                if (r.IsCompressed != r.IsMarkedCompressed) return true;
            return false;
        }

        private void SetDirty()
        {
            _undo?.Record();
            Title = HasUnsavedChanges ? "● Overlay Editor" : "Overlay Editor";
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void SetClean()
        {
            _loaded = new (bool, bool)[Overlays.Count];
            for (int i = 0; i < Overlays.Count; i++) _loaded[i] = (Overlays[i].IsCompressed, Overlays[i].IsMarkedCompressed);
            Title = "Overlay Editor";
            OnPropertyChanged(nameof(HasUnsavedChanges));
            _undo?.MarkSaved();
        }

        // ----------------------------------------------------------------
        // Undo / redo: every row's two checkboxes
        // ----------------------------------------------------------------

        private DSPRE.Avalonia.ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private byte[] TakeState() => DSPRE.Avalonia.UndoJson.Take(Overlays.Select(r => new[] { r.IsCompressed, r.IsMarkedCompressed }).ToArray());

        private void ApplyState(byte[] state)
        {
            bool[][] rows = DSPRE.Avalonia.UndoJson.Read<bool[][]>(state);
            for (int i = 0; i < rows.Length && i < Overlays.Count; i++)
            {
                Overlays[i].IsCompressed = rows[i][0];
                Overlays[i].IsMarkedCompressed = rows[i][1];
            }
            RefreshMismatch();
            SetDirty();
        }

        private void ResetUndo() { _undo = new DSPRE.Avalonia.ByteStateUndo(TakeState, ApplyState, RaiseUndo); RaiseUndo(); }
    }
}
