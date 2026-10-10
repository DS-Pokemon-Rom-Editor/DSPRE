using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.Editors;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using DSPRE.ROMFiles;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.DSUtils;
using static DSPRE.RomInfo;
using DSPRE.Avalonia.Views.Shell;
using DSPRE.Csv;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>
    /// ViewModel for the TM/HM Editor Avalonia window.
    /// Implements IEditorWithUnsavedChanges so it participates in ROM-switch prompts.
    /// </summary>
    public class TMEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        // ----------------------------------------------------------------
        // INotifyPropertyChanged
        // ----------------------------------------------------------------

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            if (name == nameof(HasUnsavedChanges)) ClearImportedWhenClean();
        }

        // Shown after a CSV import until the changes are saved or discarded.
        private bool _imported;
        public string ImportNote => _imported ? "CSV import has unsaved changes. Press Save to write them to disk." : "";
        public bool HasImportNote => _imported;
        private void MarkImported() { _imported = true; OnPropertyChanged(nameof(ImportNote)); OnPropertyChanged(nameof(HasImportNote)); }
        private void ClearImportedWhenClean()
        {
            if (!_imported || HasUnsavedChanges) return;
            _imported = false;
            OnPropertyChanged(nameof(ImportNote));
            OnPropertyChanged(nameof(HasImportNote));
        }

        private bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }

        // ----------------------------------------------------------------
        // Private state
        // ----------------------------------------------------------------

        private int[] _curMachineMoves;
        private int[] _curMachinePalettes;
        private bool _palettesKnown;
        private int[] _savedMachineMoves = System.Array.Empty<int>();
        /// <summary>False when a machine has no item row: palettes then can't be read or written.</summary>
        public bool PalettesKnown => _palettesKnown;
        private bool _loading;
        private bool _dirty;

        // ── Undo / redo (ISupportsUndo) ────────────────────────────────────────
        // The whole TM/HM table is one editable unit (one Save writes it all), so history is a single
        // timeline reset once at load. Snapshot = a clone of both arrays.
        private sealed class TMSnapshot { public int[] Moves; public int[] Palettes; }
        private readonly DSPRE.Avalonia.UndoHistory<TMSnapshot> _history = new();
        private DateTime _lastCaptureUtc = DateTime.MinValue;
        private const int CoalesceMs = 500;

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public void Undo() { if (_history.CanUndo) ApplyState(_history.Undo()); }
        public void Redo() { if (_history.CanRedo) ApplyState(_history.Redo()); }
        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private TMSnapshot Snapshot() => new TMSnapshot
        {
            Moves    = (int[])_curMachineMoves.Clone(),
            Palettes = (int[])_curMachinePalettes.Clone(),
        };

        private void ApplyState(TMSnapshot snap)
        {
            if (snap == null) return;
            _loading = true;
            _curMachineMoves    = (int[])snap.Moves.Clone();      // clone so later edits don't mutate history
            _curMachinePalettes = (int[])snap.Palettes.Clone();
            RefreshMachineMoveList();
            OnMachineSelected(_selectedMachineIndex);             // refresh the move/type combos for the shown machine
            _loading = false;

            _dirty = _history.IsDirty;
            Title = _dirty ? "● TM/HM Editor" : "TM/HM Editor";
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RaiseUndoState();
        }

        private void RecordUndoSnapshot()
        {
            if (_curMachineMoves == null) return;
            bool coalesce = (DateTime.UtcNow - _lastCaptureUtc).TotalMilliseconds < CoalesceMs;
            _history.Capture(Snapshot(), coalesce);
            _lastCaptureUtc = DateTime.UtcNow;
            RaiseUndoState();
        }

        // ----------------------------------------------------------------
        // Observable collections (bound to ListBox / ComboBoxes)
        // ----------------------------------------------------------------

        public sealed class MachineRow
        {
            public string Text { get; init; }
            public int Move { get; init; }
            public override string ToString() => Text;
        }
        public ObservableCollection<MachineRow> MachineItems { get; } = new ObservableCollection<MachineRow>();
        public ObservableCollection<string> MoveNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> TypeNames { get; } = new ObservableCollection<string>();

        // ----------------------------------------------------------------
        // Selected indices
        // ----------------------------------------------------------------

        // The list shows TMs first and HMs last, so a list position isn't a machine index.
        private int[] _order = System.Array.Empty<int>();
        private int Pos(int machine) => System.Array.IndexOf(_order, machine);

        private int _selectedMachineIndex = -1;
        /// <summary>The selected row of the machine list; <c>_selectedMachineIndex</c> is the machine it shows.</summary>
        public int SelectedMachineIndex
        {
            get => Pos(_selectedMachineIndex);
            set
            {
                // Replacing or refilling the rows pushes -1 back through the binding.
                if (_machineListChanging) return;
                int machine = value >= 0 && value < _order.Length ? _order[value] : -1;
                if (machine == _selectedMachineIndex) return;
                _selectedMachineIndex = machine;
                OnPropertyChanged();
                if (_loading || machine < 0) return;
                OnMachineSelected(machine);
            }
        }

        private int _selectedMoveIndex = -1;
        public int SelectedMoveIndex
        {
            get => _selectedMoveIndex;
            set
            {
                if (!Set(ref _selectedMoveIndex, value)) return;
                if (_loading || _selectedMachineIndex < 0 || _selectedMachineIndex >= _curMachineMoves.Length) return;

                _curMachineMoves[_selectedMachineIndex] = value;
                string label = TMEditor.MachineLabelFromIndex(_selectedMachineIndex);
                MachineRow row = new MachineRow { Text = $"{label} - {GetMoveNameFromID(value)}", Move = value };
                ChangeMachineList(() => MachineItems[Pos(_selectedMachineIndex)] = row);
                SetDirty(true);
            }
        }

        private int _selectedTypeIndex = -1;
        public int SelectedTypeIndex
        {
            get => _selectedTypeIndex;
            set
            {
                if (!Set(ref _selectedTypeIndex, value)) return;
                if (_loading || !_palettesKnown || _selectedMachineIndex < 0 || _selectedMachineIndex >= _curMachineMoves.Length) return;

                _curMachinePalettes[_selectedMachineIndex] = TypeIndexToPalette(value);
                SetDirty(true);
            }
        }

        // ----------------------------------------------------------------
        // Window title (reflects dirty state)
        // ----------------------------------------------------------------

        private string _title = "TM/HM Editor";
        public string Title
        {
            get => _title;
            private set => Set(ref _title, value);
        }

        // ----------------------------------------------------------------
        // IEditorWithUnsavedChanges
        // ----------------------------------------------------------------

        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "TM/HM Editor";

        void IEditorWithUnsavedChanges.SaveChanges() => SaveChangesCore();
        public void DiscardChanges()
        {
            if (!_dirty || _savedSnap == null) return;
            _history.Reset(_savedSnap);
            _lastCaptureUtc = DateTime.MinValue;
            ApplyState(_savedSnap);
        }

        // The table as it is on disk, for Discard. ApplyState copies out of it, so it is never edited.
        private TMSnapshot _savedSnap;

        // ----------------------------------------------------------------
        // Constructor
        // ----------------------------------------------------------------

        public TMEditorViewModel()
        {
            if (Design.IsDesignMode)
            {
                _curMachineMoves = new int[5];
                _curMachinePalettes = new int[5];
                for (int i = 0; i < 5; i++)
                {
                    MachineItems.Add(new MachineRow { Text = $"TM{i + 1:D2} - Dummy Move {i + 1}" });
                    MoveNames.Add($"Dummy Move {i + 1}");
                    TypeNames.Add($"Type {i + 1}");
                    _curMachineMoves[i] = i;
                    _curMachinePalettes[i] = i;
                }
                Title = "TM/HM Editor (Preview)";
                return;
            }
            TryUnpackNarcs(new List<DirNames> { DirNames.moveData });

            PopulateMoveNames();
            PopulateTypeNames();

            _curMachineMoves = TMEditor.ReadMachineMoves();
            _savedMachineMoves = (int[])_curMachineMoves.Clone();
            // Unreadable palettes show as zero and are never written back.
            int[] palettes = TMEditor.ReadMachinePalettes();
            _palettesKnown = palettes != null;
            _curMachinePalettes = palettes ?? new int[_curMachineMoves.Length];
            RefreshMachineMoveList();
            _savedSnap = Snapshot();
            _history.Reset(_savedSnap);   // loaded table is the clean undo baseline
            AppEvents.NamesChanged += OnNamesChanged;   // live-refresh move/type names from the Text editor

            // Start on the first machine, so the move and palette boxes show something rather than blank.
            if (MachineItems.Count > 0) SelectedMachineIndex = 0;
        }

        // ----------------------------------------------------------------
        // Commands (async, called from View code-behind)
        // ----------------------------------------------------------------

        public void SaveCommand() => SaveChangesCore();

        public void AutoPaletteCommand()
        {
            if (!_palettesKnown || _selectedMachineIndex < 0 || _selectedMachineIndex >= _curMachineMoves.Length)
                return;

            int moveId = _curMachineMoves[_selectedMachineIndex];
            int typeIndex = GetMoveType(moveId);

            _loading = true;
            SelectedTypeIndex = typeIndex;
            _loading = false;

            _curMachinePalettes[_selectedMachineIndex] = TypeIndexToPalette(typeIndex);
            SetDirty(true);
        }

        public async Task AutoPaletteAllCommand(Window owner)
        {
            if (!_palettesKnown) return;
            bool confirmed = await DialogHelper.AskYesNo(
                "This will set the palette of all TMs and HMs based on their move types.\n" +
                "If any of the moves have custom types (e.g. Fairy) they will receive the Normal type palette instead and " +
                "may need to be manually corrected.\nDo you want to continue?",
                "Auto-Set All Palettes");

            if (!confirmed) return;

            for (int i = 0; i < _curMachineMoves.Length; i++)
            {
                int typeIndex = GetMoveType(_curMachineMoves[i]);
                _curMachinePalettes[i] = TypeIndexToPalette(typeIndex);
            }

            if (_selectedMachineIndex >= 0 && _selectedMachineIndex < _curMachinePalettes.Length)
            {
                _loading = true;
                SelectedTypeIndex = PaletteToTypeIndex(_curMachinePalettes[_selectedMachineIndex]);
                _loading = false;
            }

            SetDirty(true);
        }

        public async Task ExportCommand(Window owner)
        {
            string path = await DialogHelper.SaveFile(
                owner,
                "Export Machine Data",
                new[] { DialogHelper.CsvFilter, DialogHelper.AllFilter },
                "machine_data.csv");

            if (path == null) return;

            try
            {
                using StreamWriter writer = new StreamWriter(path);
                MachineCsv.Write(writer, MachineLabels(), MoveNames.ToArray(), _curMachineMoves, _curMachinePalettes);

                await DialogHelper.ShowInfo("Machine data exported successfully.", "Export Complete");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"TM Editor: Failed to export machine data. Exception: {ex.Message}");
                await DialogHelper.ShowError("An error occurred while exporting the machine data. Please try again.", "Export Error");
            }
        }

        public async Task ImportCommand(Window owner)
        {
            MachineCsv importer = new MachineCsv(MachineLabels(), MoveNames.ToArray(), _curMachineMoves, _curMachinePalettes, _palettesKnown,
                palette => TypeIndexToPalette(PaletteToTypeIndex(palette)) == palette);
            CsvImportSession session = await CsvImportReviewView.ReviewAsync(owner, importer);
            if (session == null) return;

            foreach (CsvRecord r in session.Accepted)
            {
                MachineCsv.Entry e = (MachineCsv.Entry)r.Value;
                _curMachineMoves[e.Machine] = e.Move;
                _curMachinePalettes[e.Machine] = e.Palette;
            }
            RefreshMachineMoveList();
            OnMachineSelected(_selectedMachineIndex);
            SetDirty(true);
            MarkImported();
        }

        private string[] MachineLabels() => Enumerable.Range(0, _curMachineMoves.Length).Select(TMEditor.MachineLabelFromIndex).ToArray();

        // ----------------------------------------------------------------
        // Private helpers
        // ----------------------------------------------------------------

        private void OnMachineSelected(int index)
        {
            if (index < 0 || index >= _curMachineMoves.Length || index >= _curMachinePalettes.Length)
                return;

            _loading = true;
            SelectedMoveIndex = _curMachineMoves[index];
            SelectedTypeIndex = PaletteToTypeIndex(_curMachinePalettes[index]);
            _loading = false;
        }

        private void RefreshMachineMoveList()
        {
            string[] names = TMEditor.GetMachineMoveNames(_curMachineMoves);
            ChangeMachineList(() =>
            {
                MachineItems.Clear();
                _order = _order.Length == names.Length ? _order : TMEditor.DisplayOrder().Where(i => i < names.Length).ToArray();
                foreach (int i in _order)
                    MachineItems.Add(new MachineRow { Text = $"{TMEditor.MachineLabelFromIndex(i)} - {names[i]}", Move = _curMachineMoves[i] });
            });
        }

        private bool _machineListChanging;

        /// <summary>Changes the machine rows, then puts the list's selection back on the machine being edited.</summary>
        private void ChangeMachineList(Action change)
        {
            bool was = _machineListChanging;
            _machineListChanging = true;
            try { change(); }
            finally { _machineListChanging = was; }
            OnPropertyChanged(nameof(SelectedMachineIndex));
        }

        private void PopulateMoveNames()
        {
            MoveNames.Clear();
            foreach (string name in RomInfo.GetAttackNames())
                MoveNames.Add(name);
        }

        private void OnNamesChanged(object sender, System.EventArgs e)
        {
            DSPRE.Avalonia.Data.ListSync.Apply(MoveNames, RomInfo.GetAttackNames());
            DSPRE.Avalonia.Data.ListSync.Apply(TypeNames, RomInfo.GetTypeNames());
        }
        /// <summary>Unsubscribes from app-wide events; call when the editor window closes.</summary>
        public void Detach() => AppEvents.NamesChanged -= OnNamesChanged;

        private void PopulateTypeNames()
        {
            TypeNames.Clear();
            foreach (string name in RomInfo.GetTypeNames())
                TypeNames.Add(name);
        }

        private string GetMoveNameFromID(int moveId)
        {
            string[] names = RomInfo.GetAttackNames();
            return (moveId >= 0 && moveId < names.Length) ? names[moveId] : $"UNK_{moveId}";
        }

        // The old description names the old move, so it is offered for rewriting rather than left wrong.
        private async Task UpdateDescriptionsAsync(List<(int Machine, int OldMove, int NewMove)> moved)
        {
            string labels = string.Join(", ", moved.Take(6).Select(m => TMEditor.MachineLabelFromIndex(m.Machine))) + (moved.Count > 6 ? "…" : "");
            if (!await DialogHelper.AskYesNo($"Rewrite the item descriptions of {labels} for their new moves?", "TM/HM Editor")) return;
            try
            {
                TmItemDescriptions.Result result = TmItemDescriptions.Update(moved);
                if (result.Kept.Count > 0)
                    await DialogHelper.ShowInfo("Too long for the bag, left as they were: " + string.Join(", ", result.Kept) + ".", "TM/HM Editor");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                await DialogHelper.ShowError("The descriptions were not rewritten:\n" + e.Message, "TM/HM Editor");
            }
        }

        private bool SaveChangesCore()
        {
            try
            {
                TMEditor.WriteMachines(_curMachineMoves, _palettesKnown ? _curMachinePalettes : null);
                List<(int i, int, int)> moved = Enumerable.Range(0, Math.Min(_curMachineMoves.Length, _savedMachineMoves.Length))
                    .Where(i => _curMachineMoves[i] != _savedMachineMoves[i])
                    .Select(i => (i, _savedMachineMoves[i], _curMachineMoves[i])).ToList();
                _savedMachineMoves = (int[])_curMachineMoves.Clone();
                _savedSnap = Snapshot();
                if (moved.Count > 0 && TmItemDescriptions.WhyNot() == null) _ = UpdateDescriptionsAsync(moved);

                SetDirty(false);
                _history.MarkSaved();
                RaiseUndoState();
                SaveNotice.Saved(UnsavedChangesDescription);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException)
            {
                AppLogger.Error($"TM Editor: Failed to save machine moves or palettes. Exception: {ex.Message}");
                _ = DialogHelper.ShowError("The TMs and HMs were not saved:\n" + ex.Message, "TM/HM Editor");
                return false;
            }
            return true;
        }

        private void SetDirty(bool isDirty)
        {
            if (isDirty && !_loading) RecordUndoSnapshot();   // capture the edit (coalesced) before flagging dirty
            _dirty = isDirty;
            Title = isDirty ? "● TM/HM Editor" : "TM/HM Editor";
            OnPropertyChanged(nameof(HasUnsavedChanges));

            // Avalonia ViewModels are tracked by AvaloniaEditorsRegistry, not the WinForms OpenEditorsRegistry
        }

        private int GetMoveType(int moveId)
        {
            MoveData moveData = new MoveData(moveId);
            return (int)moveData.movetype;
        }

        private static int PaletteToTypeIndex(int paletteID) => TMEditor.PaletteToTypeIndex(paletteID);

        private static int TypeIndexToPalette(int typeIndex) => TMEditor.TypeIndexToPalette(typeIndex);
    }
}
