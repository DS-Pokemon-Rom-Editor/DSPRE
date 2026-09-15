using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.MoveData;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    // ── Per-flag observable item ──────────────────────────────────────────────
    public class FlagEntry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public string Name { get; init; }

        private bool _isSet;
        public bool IsSet
        {
            get => _isSet;
            set { if (_isSet == value) return; _isSet = value; OnPropertyChanged(); }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    public class MoveDataEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        // ─── hg-engine source banner ──────────────────────────────────────────────
        public string HgEngineBanner => DSPRE.HgEngine.HgEngineProject.BannerText;
        public bool ShowHgEngineBanner => HgEngineBanner != null;

        // Set when the move's Moves.c entry couldn't be read; saving would write the shown values over it.
        private string _sourceLoadError;
        public string SourceLoadError { get => _sourceLoadError; private set { if (Set(ref _sourceLoadError, value)) OnPropertyChanged(nameof(HasSourceLoadError)); } }
        public bool HasSourceLoadError => _sourceLoadError != null;

        // ── IEditorWithUnsavedChanges ─────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty || _pendingMove != null || _pendingImports.Count > 0;
        public string UnsavedChangesDescription =>
            _pendingMove != null ? $"New move {_pendingMove.DisplayName}"
            : _currentFile != null ? $"Move {_currentId} - {MoveNames[_currentId]}" : "Move Data Editor";
        void IEditorWithUnsavedChanges.SaveChanges() => _ = SaveCommand();
        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            await SaveCommand();
            return !HasUnsavedChanges;
        }
        public void DiscardChanges()
        {
            if (_pendingMove == null && _pendingImports.Count == 0) { SetClean(); return; }
            int back = _pendingMove != null ? _returnIndex : _currentId;
            _pendingImports.Clear();
            Status = "";
            DropPendingMove();
            _selectedMoveIndex = back;
            OnPropertyChanged(nameof(SelectedMoveIndex));
            LoadMove(back);
        }

        // A new move and imported moves exist only here until Save; Discard drops them.
        private HgEngineMoveExpansion.PendingMove _pendingMove;
        private int _returnIndex;
        private readonly Dictionary<int, MoveData> _pendingImports = new();
        // Set while the list itself changes, so the selector's own index updates aren't taken as picks.
        private bool _syncingList;

        private string _status = string.Empty;
        public string Status { get => _status; private set => Set(ref _status, value); }

        // ── Move names / type names / battle sequences ─────────────────────────
        public ObservableCollection<string> MoveNames    { get; } = new();
        public ObservableCollection<string> TypeNames    { get; } = new();
        public ObservableCollection<string> SplitNames   { get; } = new();
        public ObservableCollection<string> RangeItems   { get; } = new();
        public ObservableCollection<string> BattleSeqItems { get; } = new();
        public ObservableCollection<string> ContestNames { get; } = new();
        public ObservableCollection<FlagEntry> Flags     { get; } = new();

        // ── Current move selection ─────────────────────────────────────────────
        private int _selectedMoveIndex;
        public int SelectedMoveIndex
        {
            get => _selectedMoveIndex;
            set
            {
                if (_syncingList || value == _selectedMoveIndex || value < 0 || value >= MoveNames.Count) return;
                if (_dirty || _pendingMove != null) { _ = ConfirmDiscardAsync(value); return; }
                _selectedMoveIndex = value;
                OnPropertyChanged();
                LoadMove(value);
            }
        }

        // ── Move fields ────────────────────────────────────────────────────────
        private int _typeIndex;
        public int TypeIndex { get => _typeIndex; set { if (Set(ref _typeIndex, value) && _currentFile != null) { _currentFile.movetype = (PokemonType)value; SetDirty(); } } }

        private int _splitIndex;
        public int SplitIndex { get => _splitIndex; set { if (Set(ref _splitIndex, value) && _currentFile != null) { _currentFile.split = (MoveSplit)value; SetDirty(); } } }

        private int _rangeIndex;
        public int RangeIndex { get => _rangeIndex; set { if (Set(ref _rangeIndex, value) && _currentFile != null) { _currentFile.target = AttackRangeDescriptions[value].value; SetDirty(); } } }

        private int _battleSeqIndex;
        public int BattleSeqIndex { get => _battleSeqIndex; set { if (Set(ref _battleSeqIndex, value) && _currentFile != null) { _currentFile.battleeffect = (ushort)value; SetDirty(); } } }

        private int _contestIndex;
        public int ContestIndex { get => _contestIndex; set { if (Set(ref _contestIndex, value) && _currentFile != null) { _currentFile.contestConditionType = (ContestCondition)value; SetDirty(); } } }

        private int _power;
        public int Power { get => _power; set { if (Set(ref _power, value) && _currentFile != null) { _currentFile.damage = (byte)value; SetDirty(); } } }

        private int _accuracy;
        public int Accuracy { get => _accuracy; set { if (Set(ref _accuracy, value) && _currentFile != null) { _currentFile.accuracy = (byte)value; SetDirty(); } } }

        private int _pp;
        public int PP { get => _pp; set { if (Set(ref _pp, value) && _currentFile != null) { _currentFile.pp = (byte)value; SetDirty(); } } }

        private int _priority;
        public int Priority { get => _priority; set { if (Set(ref _priority, value) && _currentFile != null) { _currentFile.priority = (sbyte)value; SetDirty(); } } }

        private int _sideEffectPct;
        public int SideEffectPct { get => _sideEffectPct; set { if (Set(ref _sideEffectPct, value) && _currentFile != null) { _currentFile.sideEffectProbability = (byte)value; SetDirty(); } } }

        private int _contestAppeal;
        public int ContestAppeal { get => _contestAppeal; set { if (Set(ref _contestAppeal, value) && _currentFile != null) { _currentFile.contestAppeal = (byte)value; SetDirty(); } } }

        private string _description = string.Empty;
        public string Description { get => _description; set => Set(ref _description, value); }

        private string _title = "Move Data Editor";
        public string Title { get => _title; private set => Set(ref _title, value); }

        // ── Private state ──────────────────────────────────────────────────────
        private MoveData _currentFile;
        private int _currentId;
        private bool _loading;

        // ── Undo / redo (ISupportsUndo) ────────────────────────────────────────
        // Snapshots are the move file's bytes; consecutive edits within CoalesceMs collapse into one step.
        private readonly DSPRE.Avalonia.UndoHistory<byte[]> _history = new();
        private System.DateTime _lastCaptureUtc = System.DateTime.MinValue;
        private const int CoalesceMs = 500;

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public void Undo() { if (_history.CanUndo) ApplyState(_history.Undo()); }
        public void Redo() { if (_history.CanRedo) ApplyState(_history.Redo()); }

        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private void RecordUndoSnapshot()
        {
            if (_currentFile == null) return;
            bool coalesce = (System.DateTime.UtcNow - _lastCaptureUtc).TotalMilliseconds < CoalesceMs;
            _history.Capture(_currentFile.ToByteArray(), coalesce);
            _lastCaptureUtc = System.DateTime.UtcNow;
            RaiseUndoState();
        }

        private void ApplyState(byte[] bytes)
        {
            if (bytes == null) return;
            _loading = true;
            _currentFile = new MoveData(new MemoryStream(bytes));
            PopulateFromCurrentFile();
            _loading = false;

            _dirty = _history.IsDirty;
            RefreshDirty();
            RaiseUndoState();
        }
        private readonly string[] _moveDescriptions;
        private Dictionary<string, int> _typeNameToId;
        private Dictionary<string, MoveSplit> _splitNameToEnum;
        private Dictionary<string, ushort> _rangeNameToValue;

        // ── Constructor ────────────────────────────────────────────────────────
        public MoveDataEditorViewModel()
        {
            if (Design.IsDesignMode)
            {
                // Provide dummy data so the UI renders
                for (int i = 1; i <= 10; i++) MoveNames.Add($"Dummy Move {i}");
                for (int i = 1; i <= 5; i++) TypeNames.Add($"Type {i}");
                SplitNames.Add("Physical"); SplitNames.Add("Special");
                RangeItems.Add("Single target");
                BattleSeqItems.Add("001 - Dummy Effect");
                ContestNames.Add("Cool");
                Flags.Add(new FlagEntry { Name = "Dummy Flag" });
                Description = "Design-time preview, no ROM loaded";
                Title = "Move Data Editor (Preview)";
                _selectedMoveIndex = 0;
                _typeIndex = 0;
                _splitIndex = 0;
                _rangeIndex = 0;
                _battleSeqIndex = 0;
                _contestIndex = 0;
                _power = 40;
                _accuracy = 100;
                _pp = 20;
                _priority = 0;
                _sideEffectPct = 0;
                _contestAppeal = 0;
                return;
            }
            string[] rawDescs = new TextArchive(moveDescriptionsTextNumbers).messages.ToArray();
            _moveDescriptions = rawDescs.Select(x => x.Replace("\\n", Environment.NewLine)).ToArray();

            string[] moveNames = GetAttackNames();
            string[] typeNames = GetTypeNames();
            string[] battleSeqFiles = GetBattleEffectSequenceFiles();
            string[] db = PokeDatabase.MoveData.battleSequenceDescriptions;

            foreach (var n in moveNames) MoveNames.Add(n);
            foreach (var n in typeNames) TypeNames.Add(n);
            // Split / contest dropdowns come from the customisable LabelStore (Tools ▸ Edit Dropdown Labels).
            ReloadSplitContest();
            foreach (var r in AttackRangeDescriptions) RangeItems.Add($"{r.name}: {r.description}");

            for (int i = 0; i < battleSeqFiles.Length; i++)
                BattleSeqItems.Add(i < db.Length && db[i] != null ? $"{i:D3} - {db[i]}" : $"{i:D3} - Undocumented");

            foreach (var flagName in Enum.GetNames(typeof(MoveFlags)).Skip(1))
            {
                var entry = new FlagEntry { Name = flagName };
                entry.PropertyChanged += (_, __) => { if (!_loading && _currentFile != null) { RebuildFlagField(); SetDirty(); } };
                Flags.Add(entry);
            }

            BuildLookupDictionaries(typeNames);

            if (MoveNames.Count > 1)
            {
                _selectedMoveIndex = 1;
                LoadMove(1);
            }
        }

        private void ReloadSplitContest()
        {
            DSPRE.Avalonia.Data.LabelStore.Sync(SplitNames,   "move_split");
            DSPRE.Avalonia.Data.LabelStore.Sync(ContestNames, "move_contest_conditions");
            AppEvents.LabelsChanged -= OnLabelsChanged; AppEvents.LabelsChanged += OnLabelsChanged;
            AppEvents.NamesChanged  -= OnNamesChanged;  AppEvents.NamesChanged  += OnNamesChanged;
        }
        private void OnLabelsChanged(object sender, EventArgs e)
        {
            ReloadSplitContest();
            // Re-resolve the combos' displayed text after the label lists were replaced in place.
            Repoke(_splitIndex,   nameof(SplitIndex),   v => _splitIndex = v);
            Repoke(_contestIndex, nameof(ContestIndex), v => _contestIndex = v);
        }
        private void OnNamesChanged(object sender, EventArgs e)
        {
            // Move names live in a ROM text archive; refresh when the Text editor saves.
            SyncNames();
        }

        /// <summary>The ROM's move names, with an unsaved new move shown at its id.</summary>
        private void SyncNames()
        {
            var names = RomInfo.GetAttackNames().ToList();
            if (_pendingMove != null)
            {
                while (names.Count < _pendingMove.Id) names.Add("");
                names.Insert(_pendingMove.Id, _pendingMove.DisplayName + " (not saved)");
            }
            _syncingList = true;
            try { DSPRE.Avalonia.Data.ListSync.Apply(MoveNames, names); }
            finally { _syncingList = false; }
            OnPropertyChanged(nameof(SelectedMoveIndex));
        }

        private void DropPendingMove()
        {
            if (_pendingMove == null) return;
            _pendingMove = null;
            SyncNames();
        }

        private static MoveData Copy(MoveData move) => new MoveData(new MemoryStream(move.ToByteArray()));

        private void Repoke(int current, string name, Action<int> set)
        {
            if (current < 0) return;
            set(-1); OnPropertyChanged(name);
            global::Avalonia.Threading.Dispatcher.UIThread.Post(
                () => { set(current); OnPropertyChanged(name); },
                global::Avalonia.Threading.DispatcherPriority.Background);
        }
        /// <summary>Unsubscribes from app-wide events; call when the editor window closes.</summary>
        public void Detach() { AppEvents.LabelsChanged -= OnLabelsChanged; AppEvents.NamesChanged -= OnNamesChanged; }

        // ── Commands ──────────────────────────────────────────────────────────

        public async Task SaveCommand()
        {
            if (_currentFile == null) return;
            if (HgEngineProject.IsActive && SourceLoadError != null)
            {
                await DialogHelper.ShowError($"Move {_currentId} was not saved.\n{SourceLoadError}", "Move Data Editor");
                return;
            }
            int id = _currentId;
            var move = _currentFile;
            var pending = _pendingMove;
            // The shown record wins over an import staged for the same move.
            var records = new Dictionary<int, MoveData>(_pendingImports) { [id] = move };
            string subject = pending != null ? pending.DisplayName : records.Count > 1 ? $"{records.Count} moves" : $"Move {id}";

            // The source is what the next sync rebuilds from, so a save that can't reach it is no save.
            if (HgEngineProject.IsActive)
            {
                var (saved, error) = await HgEngineSave.RunAsync(() => pending != null
                    ? (HgEngineMoveExpansion.TryCommitMove(pending, move, out string addError) ? null : addError)
                    : (HgEngineMoveSource.TryWriteMany(records, out string writeError) ? null : writeError));
                if (!saved)
                {
                    if (error != null) await DialogHelper.ShowError($"{subject} not saved.\n{error}", "Move Data Editor");
                    return;
                }
            }

            _pendingImports.Clear();
            Status = string.Empty;
            if (pending != null)
            {
                HgEngineMoveExpansion.CompleteAdd(pending);
                _pendingMove = null;
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.moveData });
                move.SaveToFileDefaultDir(id, showSuccessMessage: false);
                SyncNames();
                AppEvents.RaiseNamesChanged();
                _selectedMoveIndex = id;
                OnPropertyChanged(nameof(SelectedMoveIndex));
                LoadMove(id);   // reads back through the new define
            }
            else
            {
                foreach (var (importId, imported) in records)
                    if (importId != id) imported.SaveToFileDefaultDir(importId, showSuccessMessage: false);
                move.SaveToFileDefaultDir(id, showSuccessMessage: !HgEngineProject.IsActive);
                _history.MarkSaved();   // current state is now the on-disk baseline (undo can still go past it)
            }
            SetClean();
            SaveNotice.Saved(UnsavedChangesDescription);
            RaiseUndoState();
        }

        /// <summary>On hg-engine the move comes from Moves.c; the built copy only fills a field the entry lacks,
        /// which the save then reports.</summary>
        private static MoveData LoadRecord(int id, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) return new MoveData(id);
            string built = Path.Combine(gameDirs[DirNames.moveData].unpackedDir, id.ToString("D4"));
            var move = File.Exists(built) ? new MoveData(id) : new MoveData(new MemoryStream(new byte[16]));
            HgEngineMoveSource.TryLoad(id, move, out error);
            return move;
        }

        /// <summary>hg-engine-only: shapes a brand new move and opens it for editing. Nothing is written
        /// until Save, and Discard drops it.</summary>
        public async Task AddNewMoveAsync(Window owner)
        {
            if (!HgEngineProject.IsActive) return;
            if (_pendingMove != null || _pendingImports.Count > 0)
            {
                string first = _pendingMove != null ? "Save or discard the new move first." : "Save or discard the imported moves first.";
                await DialogHelper.ShowError(first, "Add New Move", owner);
                return;
            }
            if (_dirty && !await DialogHelper.AskYesNo("There are unsaved changes to the current move. Discard and proceed?", "Unsaved Changes", owner))
                return;

            string name = await DialogHelper.PromptText("New move's display name:", "Add New Move", owner: owner);
            if (name == null) return;

            var move = new MoveData(new MemoryStream(new byte[16]));
            if (!HgEngineMoveExpansion.TryPrepareMove(name, out var pending, out string error)
                || !HgEngineMoveExpansion.TryReadTemplate(pending, move, out error))
            {
                await DialogHelper.ShowError($"Could not add the move:\n{error}", "Add New Move", owner);
                return;
            }

            _returnIndex = _selectedMoveIndex;
            _pendingMove = pending;
            SyncNames();

            _loading = true;
            _currentId = pending.Id;
            _currentFile = move;
            SourceLoadError = null;
            PopulateFromCurrentFile();
            _loading = false;
            _selectedMoveIndex = pending.Id;
            OnPropertyChanged(nameof(SelectedMoveIndex));

            _dirty = false;
            _history.Reset(move.ToByteArray());
            _lastCaptureUtc = System.DateTime.MinValue;
            RefreshDirty();
            RaiseUndoState();
        }

        public async Task ExportCommand(Window owner)
        {
            string path = await DialogHelper.SaveFile(owner, "Export Move Data to CSV",
                new[] { DialogHelper.CsvFilter, DialogHelper.AllFilter }, "MoveData.csv");
            if (path == null) return;

            try
            {
                string[] typeNames = GetTypeNames();
                // What is on disk, without this editor's unsaved moves.
                string[] names = RomInfo.GetAttackNames();
                var moves = new SortedDictionary<int, MoveData>();
                var skipped = new List<string>();
                if (HgEngineProject.IsActive)
                {
                    string builtDir = gameDirs[DirNames.moveData].unpackedDir;
                    MoveData BuiltOrEmpty(int id) => File.Exists(Path.Combine(builtDir, id.ToString("D4"))) ? new MoveData(id) : new MoveData(new MemoryStream(new byte[16]));
                    if (!HgEngineMoveSource.TryLoadMany(Enumerable.Range(0, names.Length), BuiltOrEmpty, out moves, out skipped, out string loadError))
                    {
                        await DialogHelper.ShowError($"Error exporting: {loadError}", "Export Error");
                        return;
                    }
                }
                else
                {
                    for (int i = 0; i < names.Length; i++) moves[i] = new MoveData(i);
                }

                using (var writer = new StreamWriter(path))
                {
                    writer.WriteLine("Move ID,Move Name,Move Type,Move Split,Power,Accuracy,Priority,Side Effect Probability,PP,Range");
                    foreach (var (i, move) in moves)
                    {
                        string typeStr  = (int)move.movetype < typeNames.Length ? typeNames[(int)move.movetype] : $"UnknownType_{(int)move.movetype}";
                        string rangeStr = MoveData.GetAttackRangeName(move.target);
                        writer.WriteLine($"{i},{names[i]},{typeStr},{move.split},{move.damage},{move.accuracy},{move.priority},{move.sideEffectProbability},{move.pp},{rangeStr}");
                    }
                }

                string message = $"Move data exported to:\n{path}";
                if (skipped.Count > 0)
                    message += $"\n\n{skipped.Count} move(s) skipped:\n" + string.Join("\n", skipped.Take(10))
                             + (skipped.Count > 10 ? $"\n...and {skipped.Count - 10} more" : "");
                await DialogHelper.ShowInfo(message, "Export Complete");
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Error exporting: {ex.Message}", "Export Error");
            }
        }

        public async Task ImportCommand(Window owner)
        {
            if (_pendingMove != null)
            {
                await DialogHelper.ShowError("Save or discard the new move first.", "Import CSV", owner);
                return;
            }
            string path = await DialogHelper.OpenFile(owner, "Import Move Data from CSV",
                new[] { DialogHelper.CsvFilter, DialogHelper.AllFilter });
            if (path == null) return;

            string[] typeNamesArr = GetTypeNames();
            var result = ValidateAndParseCSV(path, typeNamesArr);

            // Build preview text
            var sb = new StringBuilder();
            sb.AppendLine($"Total rows read:  {result.TotalRowsRead}");
            sb.AppendLine($"Valid entries:    {result.ValidCount}");
            sb.AppendLine($"Errors:           {result.ErrorCount}");
            sb.AppendLine($"Warnings:         {result.Warnings.Count}");
            sb.AppendLine($"Name mismatches:  {result.UniqueNameMismatches.Count}");

            if (result.HasErrors)
            {
                sb.AppendLine("\nERRORS:");
                foreach (var e in result.Errors) sb.AppendLine($"  {e}");
            }
            if (result.HasWarnings)
            {
                sb.AppendLine("\nWARNINGS:");
                foreach (var w in result.Warnings) sb.AppendLine($"  {w}");
            }
            if (result.ValidCount == 0)
            {
                await DialogHelper.ShowError(sb.ToString(), "Import: No Valid Entries");
                return;
            }

            sb.AppendLine($"\n{result.ValidCount} move(s) will be imported. Save writes them. Proceed?");
            bool proceed = await DialogHelper.AskYesNo(sb.ToString(), "Confirm Import");
            if (!proceed) return;

            await StageImportedData(result.ValidEntries);
        }



        // ── Private helpers ────────────────────────────────────────────────────
        private void SetDirty() { if (_loading) return; _dirty = true; RefreshDirty(); RecordUndoSnapshot(); }
        private void SetClean() { _dirty = false; RefreshDirty(); }
        private void RefreshDirty() { Title = HasUnsavedChanges ? "● Move Data Editor" : "Move Data Editor"; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private async Task ConfirmDiscardAsync(int newIndex)
        {
            bool discard = await DialogHelper.AskYesNo(
                _pendingMove != null ? "The new move is not saved. Discard it and proceed?" : "There are unsaved changes to the current move. Discard and proceed?",
                "Unsaved Changes");
            if (!discard) { OnPropertyChanged(nameof(SelectedMoveIndex)); return; }
            _dirty = false;
            if (_pendingMove != null)
            {
                // The list held the new move at its id, so a later pick sits one lower once it is gone.
                if (newIndex > _pendingMove.Id) newIndex--;
                DropPendingMove();
                newIndex = Math.Min(newIndex, MoveNames.Count - 1);
            }
            _selectedMoveIndex = newIndex;
            OnPropertyChanged(nameof(SelectedMoveIndex));
            LoadMove(newIndex);
        }

        private void LoadMove(int id)
        {
            _loading = true;
            _currentId   = id;
            string loadError = null;
            _currentFile = _pendingImports.TryGetValue(id, out var imported) ? Copy(imported) : LoadRecord(id, out loadError);
            SourceLoadError = loadError;
            PopulateFromCurrentFile();
            SetClean();
            _loading = false;

            // Loaded state is the clean baseline for undo on this move; switching moves starts fresh history.
            _history.Reset(_currentFile.ToByteArray());
            _lastCaptureUtc = System.DateTime.MinValue;
            RaiseUndoState();
        }

        /// <summary>Pushes <see cref="_currentFile"/> into the bound fields. Caller must guard with _loading.</summary>
        private void PopulateFromCurrentFile()
        {
            TypeIndex       = (int)_currentFile.movetype;
            SplitIndex      = (int)_currentFile.split;
            BattleSeqIndex  = (int)_currentFile.battleeffect;
            ContestIndex    = (int)_currentFile.contestConditionType;
            Power           = _currentFile.damage;
            Accuracy        = _currentFile.accuracy;
            PP              = _currentFile.pp;
            Priority        = _currentFile.priority;
            SideEffectPct   = _currentFile.sideEffectProbability;
            ContestAppeal   = _currentFile.contestAppeal;
            Description     = _pendingMove == null && _currentId < _moveDescriptions.Length ? _moveDescriptions[_currentId] : string.Empty;

            // Range
            int rangeIdx = 0;
            for (int i = 0; i < AttackRangeDescriptions.Length; i++)
                if (AttackRangeDescriptions[i].value == _currentFile.target) { rangeIdx = i; break; }
            _rangeIndex = rangeIdx;
            OnPropertyChanged(nameof(RangeIndex));

            // Flags
            var flagNames = Enum.GetNames(typeof(MoveFlags)).Skip(1).ToArray();
            for (int i = 0; i < Flags.Count && i < flagNames.Length; i++)
                Flags[i].IsSet = (_currentFile.flagField & (1 << i)) != 0;
        }

        private void RebuildFlagField()
        {
            if (_currentFile == null) return;
            byte field = 0;
            for (int i = 0; i < Flags.Count; i++)
                if (Flags[i].IsSet) field |= (byte)(1 << i);
            _currentFile.flagField = field;
        }

        private void BuildLookupDictionaries(string[] typeNames)
        {
            _typeNameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < typeNames.Length; i++)
                if (!string.IsNullOrEmpty(typeNames[i]) && !_typeNameToId.ContainsKey(typeNames[i]))
                    _typeNameToId[typeNames[i]] = i;

            _splitNameToEnum = new Dictionary<string, MoveSplit>(StringComparer.OrdinalIgnoreCase);
            foreach (MoveSplit s in Enum.GetValues(typeof(MoveSplit)))
                _splitNameToEnum[s.ToString()] = s;

            _rangeNameToValue = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in AttackRangeDescriptions)
                _rangeNameToValue[r.name] = r.value;
        }

        private MoveDataImportResult ValidateAndParseCSV(string filePath, string[] typeNames)
        {
            var result = new MoveDataImportResult();
            try
            {
                var lines = File.ReadAllLines(filePath);
                if (lines.Length == 0) { result.Errors.Add(new MoveImportError(0, "File is empty.")); return result; }

                var header = lines[0].Split(',');
                if (header.Length < 10 || !header[0].Trim().Equals("Move ID", StringComparison.OrdinalIgnoreCase))
                { result.Errors.Add(new MoveImportError(1, "Invalid CSV header.")); return result; }

                result.TotalRowsRead = lines.Length - 1;

                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;
                    var parts = ParseCSVLine(lines[i]);
                    if (parts.Length < 10) { result.Errors.Add(new MoveImportError(i + 1, $"Expected 10 columns, got {parts.Length}.")); continue; }

                    var rowResult = ValidateRow(i + 1, parts, typeNames);
                    result.Warnings.AddRange(rowResult.Warnings);
                    result.NameMismatches.AddRange(rowResult.NameMismatches);
                    if (rowResult.IsValid) result.ValidEntries.Add(rowResult.Entry);
                    else result.Errors.AddRange(rowResult.Errors);
                }
            }
            catch (Exception ex) { result.Errors.Add(new MoveImportError(0, $"Failed to read file: {ex.Message}")); }
            return result;
        }

        private static string[] ParseCSVLine(string line)
        {
            var list = new List<string>();
            var cur  = new StringBuilder();
            bool inQ = false;
            foreach (char c in line)
            {
                if (c == '"')  inQ = !inQ;
                else if (c == ',' && !inQ) { list.Add(cur.ToString().Trim()); cur.Clear(); }
                else cur.Append(c);
            }
            list.Add(cur.ToString().Trim());
            return list.ToArray();
        }

        private MoveRowValidationResult ValidateRow(int lineNumber, string[] parts, string[] typeNames)
        {
            var res = new MoveRowValidationResult { LineNumber = lineNumber };
            var entry = new MoveDataImportEntry();

            if (!int.TryParse(parts[0].Trim(), out int moveId) || moveId < 0 || moveId >= MoveNames.Count)
            { res.Errors.Add(new MoveImportError(lineNumber, $"Invalid Move ID '{parts[0]}'.")); }
            else
            {
                entry.MoveID   = moveId;
                entry.MoveName = MoveNames[moveId];
                string csvName = parts[1].Trim();
                if (!csvName.Equals(MoveNames[moveId], StringComparison.OrdinalIgnoreCase))
                {
                    res.Warnings.Add(new MoveImportWarning(lineNumber, $"Name mismatch for ID {moveId}: ROM='{MoveNames[moveId]}', CSV='{csvName}'."));
                    res.NameMismatches.Add(new MoveNameMismatch(moveId, MoveNames[moveId], csvName, lineNumber));
                }
            }

            if (_typeNameToId.TryGetValue(parts[2].Trim(), out int typeId)) entry.MoveType = (PokemonType)typeId;
            else res.Errors.Add(new MoveImportError(lineNumber, $"Unknown type '{parts[2]}'."));

            if (_splitNameToEnum.TryGetValue(parts[3].Trim(), out MoveSplit split)) entry.Split = split;
            else res.Errors.Add(new MoveImportError(lineNumber, $"Unknown split '{parts[3]}'."));

            if (byte.TryParse(parts[4].Trim(), out byte power))   entry.Power = power;
            else res.Errors.Add(new MoveImportError(lineNumber, $"Invalid power '{parts[4]}'."));

            if (byte.TryParse(parts[5].Trim(), out byte acc))     entry.Accuracy = acc;
            else res.Errors.Add(new MoveImportError(lineNumber, $"Invalid accuracy '{parts[5]}'."));

            if (sbyte.TryParse(parts[6].Trim(), out sbyte prio))  entry.Priority = prio;
            else res.Errors.Add(new MoveImportError(lineNumber, $"Invalid priority '{parts[6]}'."));

            if (byte.TryParse(parts[7].Trim(), out byte fx))      entry.SideEffectProbability = fx;
            else res.Errors.Add(new MoveImportError(lineNumber, $"Invalid effect% '{parts[7]}'."));

            if (byte.TryParse(parts[8].Trim(), out byte pp))      entry.PP = pp;
            else res.Errors.Add(new MoveImportError(lineNumber, $"Invalid PP '{parts[8]}'."));

            if (_rangeNameToValue.TryGetValue(parts[9].Trim(), out ushort rng)) entry.Range = rng;
            else res.Errors.Add(new MoveImportError(lineNumber, $"Unknown range '{parts[9]}'."));

            res.Entry   = entry;
            res.IsValid = res.Errors.Count == 0;
            return res;
        }

        /// <summary>Imported values wait in memory, keyed by move id, until Save or Discard. The current move takes
        /// them directly and its undo history restarts from them.</summary>
        private async Task StageImportedData(List<MoveDataImportEntry> entries)
        {
            int staged = 0;
            bool currentChanged = false;
            var failures = new List<string>();
            foreach (var e in entries)
            {
                MoveData move;
                if (e.MoveID == _currentId && _currentFile != null)
                {
                    if (SourceLoadError != null) { failures.Add($"Move {e.MoveID}: {SourceLoadError}"); continue; }
                    move = _currentFile;
                    currentChanged = true;
                }
                else if (!_pendingImports.TryGetValue(e.MoveID, out move))
                {
                    try
                    {
                        // Effect, flags and contest aren't in the CSV, so on hg-engine they come from Moves.c.
                        move = LoadRecord(e.MoveID, out string loadError);
                        if (loadError != null) { failures.Add($"Move {e.MoveID}: {loadError}"); continue; }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"Failed to read move {e.MoveID}: {ex.Message}");
                        failures.Add($"Move {e.MoveID}: {ex.Message}");
                        continue;
                    }
                }
                move.movetype  = e.MoveType;
                move.split     = e.Split;
                move.damage    = e.Power;
                move.accuracy  = e.Accuracy;
                move.priority  = e.Priority;
                move.sideEffectProbability = e.SideEffectProbability;
                move.pp        = e.PP;
                move.target    = e.Range;
                if (!ReferenceEquals(move, _currentFile)) _pendingImports[e.MoveID] = move;
                staged++;
            }

            if (currentChanged)
            {
                _pendingImports[_currentId] = Copy(_currentFile);
                _loading = true;
                PopulateFromCurrentFile();
                _loading = false;
                _dirty = false;
                _history.Reset(_currentFile.ToByteArray());
                _lastCaptureUtc = System.DateTime.MinValue;
                RaiseUndoState();
            }
            Status = _pendingImports.Count == 0 ? string.Empty
                : _pendingImports.Count == 1 ? "1 move imported, not saved" : $"{_pendingImports.Count} moves imported, not saved";
            RefreshDirty();

            if (failures.Count == 0) return;
            string listed = string.Join("\n", failures.Take(10)) + (failures.Count > 10 ? $"\n...and {failures.Count - 10} more" : "");
            await DialogHelper.ShowError($"{staged} move(s) imported. {failures.Count} were not:\n{listed}", "Import Incomplete");
        }
    }
}
