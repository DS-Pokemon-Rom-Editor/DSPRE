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

        // ── IEditorWithUnsavedChanges ─────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription =>
            _currentFile != null ? $"Move {_currentId} - {MoveNames[_currentId]}" : "Move Data Editor";
        void IEditorWithUnsavedChanges.SaveChanges() => _ = SaveCommand();
        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            await SaveCommand();
            return !HasUnsavedChanges;
        }
        public void DiscardChanges() => SetClean();

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
                if (value == _selectedMoveIndex || value < 0 || value >= MoveNames.Count) return;
                if (_dirty) { _ = ConfirmDiscardAsync(value); return; }
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
            Title = _dirty ? "● Move Data Editor" : "Move Data Editor";
            OnPropertyChanged(nameof(HasUnsavedChanges));
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
            // Move names live in a ROM text archive; refresh when the Text editor saves (the move list is a
            // ListBox, which re-renders replaced items fine, so no combo re-poke needed here).
            DSPRE.Avalonia.Data.ListSync.Apply(MoveNames, RomInfo.GetAttackNames());
        }
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
            _currentFile.SaveToFileDefaultDir(_currentId, showSuccessMessage: true);
            WriteHgEngineSource();
            _history.MarkSaved();   // current state is now the on-disk baseline (undo can still go past it)
            SetClean();
            RaiseUndoState();
        }

        // Curated v1 scope: every MoveData field this editor already exposes. .battle.flags is a bit-OR'd
        // combo in hg-engine's source (FLAG_CONTACT | FLAG_PROTECT | ...); TryGetFlagsExpression
        // decomposes it back into that same "NAME | NAME | ..." form; only a combination with a bit no
        // FLAG_* name covers falls back to a raw number (compiles to the same bits either way).
        private void WriteHgEngineSource()
        {
            if (!HgEngineProject.IsActive || _currentFile == null) return;

            string EffectSymbol(int effect) =>
                HgEngineSymbolTable.Load("include/constants/move_effects.h")?.TryGetNameWithPrefix(effect, "MOVE_EFFECT_", out string n) == true ? n : effect.ToString();
            string SplitSymbol(int split) =>
                HgEngineSymbolTable.Load("include/move_data.h")?.TryGetNameWithPrefix(split, "SPLIT_", out string n) == true ? n : split.ToString();
            string TypeSymbol(int type) =>
                HgEngineSymbolTable.Load("include/constants/pokemon.h")?.TryGetNameWithPrefix(type, "TYPE_", out string n) == true ? n : type.ToString();
            string RangeSymbol(int range) =>
                HgEngineSymbolTable.Load("include/constants/battle_constants.h")?.TryGetNameWithPrefix(range, "RANGE_", out string n) == true ? n : range.ToString();
            string FlagsSymbol(int flags) =>
                HgEngineSymbolTable.Load("include/move_data.h")?.TryGetFlagsExpression(flags, "FLAG_", out string n) == true ? n : flags.ToString();
            string AppealSymbol(int appeal) =>
                HgEngineSymbolTable.Load("include/move_data.h")?.TryGetNameWithPrefix(appeal, "APPEAL_", out string n) == true ? n : appeal.ToString();
            string ContestSymbol(int contest) =>
                HgEngineSymbolTable.Load("include/move_data.h")?.TryGetNameWithPrefix(contest, "CONTEST_", out string n) == true ? n : contest.ToString();

            var fields = new List<HgEngineFieldWrite>
            {
                new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("effect") }, EffectSymbol(_currentFile.battleeffect)),
                new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("split") }, SplitSymbol((int)_currentFile.split)),
                new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("power") }, _currentFile.damage.ToString()),
                new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("type") }, TypeSymbol((int)_currentFile.movetype)),
                new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("accuracy") }, _currentFile.accuracy.ToString()),
                new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("pp") }, _currentFile.pp.ToString()),
                new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("effectChance") }, _currentFile.sideEffectProbability.ToString()),
                new(new[] { FieldPathSegment.Field("battle"), FieldPathSegment.Field("target") }, RangeSymbol(_currentFile.target)),
                new(new[] { FieldPathSegment.Field("battle"), FieldPathSegment.Field("priority") }, _currentFile.priority.ToString()),
                new(new[] { FieldPathSegment.Field("battle"), FieldPathSegment.Field("flags") }, FlagsSymbol(_currentFile.flagField)),
                new(new[] { FieldPathSegment.Field("contest"), FieldPathSegment.Field("appeal") }, AppealSymbol(_currentFile.contestAppeal)),
                new(new[] { FieldPathSegment.Field("contest"), FieldPathSegment.Field("contestType") }, ContestSymbol((int)_currentFile.contestConditionType)),
            };

            if (!HgEngineWriter.TryWriteFields(HgEngineDomain.Moves, _currentId, fields, out var unresolved, out string error))
            { AppLogger.Error($"hg-engine write failed for move {_currentId}: {error}"); return; }

            if (unresolved.Count > 0)
                AppLogger.Info($"hg-engine write for move {_currentId}: source doesn't declare {string.Join(", ", unresolved)}, left unchanged.");
        }

        /// <summary>hg-engine-only: mints a brand new move (define + Moves.c entry + name) and jumps
        /// straight to editing it.</summary>
        public async Task AddNewMoveAsync(Window owner)
        {
            if (!HgEngineProject.IsActive) return;
            string name = await DialogHelper.PromptText("New move's display name:", "Add New Move", owner: owner);
            if (name == null) return;

            if (!HgEngineMoveExpansion.TryAddMove(name, out int newMoveId, out string error))
            {
                await DialogHelper.ShowError($"Could not add the move:\n{error}", "Add New Move", owner);
                return;
            }

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.moveData });
            DSPRE.Avalonia.Data.ListSync.Apply(MoveNames, RomInfo.GetAttackNames());
            AppEvents.RaiseNamesChanged();

            SelectedMoveIndex = newMoveId;
        }

        public async Task ExportCommand(Window owner)
        {
            string path = await DialogHelper.SaveFile(owner, "Export Move Data to CSV",
                new[] { DialogHelper.CsvFilter, DialogHelper.AllFilter }, "MoveData.csv");
            if (path == null) return;

            try
            {
                string[] typeNames = GetTypeNames();
                using var writer = new StreamWriter(path);
                writer.WriteLine("Move ID,Move Name,Move Type,Move Split,Power,Accuracy,Priority,Side Effect Probability,PP,Range");
                for (int i = 0; i < MoveNames.Count; i++)
                {
                    MoveData move = new MoveData(i);
                    string typeStr  = (int)move.movetype < typeNames.Length ? typeNames[(int)move.movetype] : $"UnknownType_{(int)move.movetype}";
                    string rangeStr = MoveData.GetAttackRangeName(move.target);
                    writer.WriteLine($"{i},{MoveNames[i]},{typeStr},{move.split},{move.damage},{move.accuracy},{move.priority},{move.sideEffectProbability},{move.pp},{rangeStr}");
                }
                await DialogHelper.ShowInfo($"Move data exported to:\n{path}", "Export Complete");
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Error exporting: {ex.Message}", "Export Error");
            }
        }

        public async Task ImportCommand(Window owner)
        {
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

            sb.AppendLine($"\n{result.ValidCount} move(s) will be updated. Proceed?");
            bool proceed = await DialogHelper.AskYesNo(sb.ToString(), "Confirm Import");
            if (!proceed) return;

            ApplyImportedData(result.ValidEntries, typeNamesArr);

            // Refresh current move if it was changed
            if (result.ValidEntries.Any(e => e.MoveID == _currentId))
            {
                _loading = true;
                LoadMove(_currentId);
                _loading = false;
            }
        }



        // ── Private helpers ────────────────────────────────────────────────────
        private void SetDirty() { if (_loading) return; _dirty = true; Title = "● Move Data Editor"; OnPropertyChanged(nameof(HasUnsavedChanges)); RecordUndoSnapshot(); }
        private void SetClean() { _dirty = false; Title = "Move Data Editor"; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private async Task ConfirmDiscardAsync(int newIndex)
        {
            bool discard = await DialogHelper.AskYesNo(
                "There are unsaved changes to the current move. Discard and proceed?",
                "Unsaved Changes");
            if (!discard) { OnPropertyChanged(nameof(SelectedMoveIndex)); return; }
            _dirty = false;
            _selectedMoveIndex = newIndex;
            OnPropertyChanged(nameof(SelectedMoveIndex));
            LoadMove(newIndex);
        }

        private void LoadMove(int id)
        {
            _loading = true;
            _currentId   = id;
            _currentFile = new MoveData(id);
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
            Description     = _currentId < _moveDescriptions.Length ? _moveDescriptions[_currentId] : string.Empty;

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

        private void ApplyImportedData(List<MoveDataImportEntry> entries, string[] typeNames)
        {
            int saved = 0;
            foreach (var e in entries)
            {
                try
                {
                    MoveData move  = new MoveData(e.MoveID);
                    move.movetype  = e.MoveType;
                    move.split     = e.Split;
                    move.damage    = e.Power;
                    move.accuracy  = e.Accuracy;
                    move.priority  = e.Priority;
                    move.sideEffectProbability = e.SideEffectProbability;
                    move.pp        = e.PP;
                    move.target    = e.Range;
                    move.SaveToFileDefaultDir(e.MoveID, showSuccessMessage: false);
                    saved++;
                }
                catch (Exception ex) { AppLogger.Error($"Failed to save move {e.MoveID}: {ex.Message}"); }
            }
            _ = DialogHelper.ShowInfo($"Successfully imported and saved {saved} move(s).", "Import Complete");
        }
    }
}
