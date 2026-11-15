using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.MoveData;
using static DSPRE.RomInfo;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Views.Shell;
using DSPRE.Csv;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
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

    public partial class MoveDataEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo, IDetachable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        public string HgEngineBanner => DSPRE.HgEngine.HgEngineProject.BannerText;
        public bool ShowHgEngineBanner => HgEngineBanner != null;

        // Set when the move's Moves.c entry couldn't be read; saving would write the shown values over it.
        private string _sourceLoadError;
        public string SourceLoadError { get => _sourceLoadError; private set { if (Set(ref _sourceLoadError, value)) OnPropertyChanged(nameof(HasSourceLoadError)); } }
        public bool HasSourceLoadError => _sourceLoadError != null;

        private bool _dirty;
        public bool HasUnsavedChanges => _dirty || _pendingMove != null || _pendingImports.Count > 0;
        public string UnsavedChangesDescription =>
            _pendingMove != null ? $"New move {_pendingMove.DisplayName}"
            : _currentFile != null ? $"Move {_currentId} - {MoveNames[_currentId]}" : "Move data";
        void IEditorWithUnsavedChanges.SaveChanges() => DialogHelper.Watch(SaveCommandAsync());
        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            await SaveCommandAsync();
            return !HasUnsavedChanges;
        }
        public void DiscardChanges()
        {
            RevertCategories();
            if (_pendingMove == null && _pendingImports.Count == 0)
            {
                // The edits live in _currentFile, so reading the move again drops them.
                if (_currentFile != null) LoadMove(_currentId); else SetClean();
                return;
            }
            int back = _pendingMove != null ? _returnIndex : _currentId;
            _pendingImports.Clear();
            StatusText = "";
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
        public string StatusText { get => _status; private set => Set(ref _status, value); }

        public ObservableCollection<string> MoveNames    { get; } = new();
        public ObservableCollection<string> TypeNames    { get; } = new();
        public ObservableCollection<string> SplitNames   { get; } = new();
        public ObservableCollection<string> RangeItems   { get; } = new();
        public ObservableCollection<string> BattleSeqItems { get; } = new();
        public ObservableCollection<string> ContestNames { get; } = new();
        public ObservableCollection<string> ContestEffectNames { get; } = new();
        public ObservableCollection<FlagEntry> Flags     { get; } = new();

        public int MaxMoveIndex => Math.Max(0, MoveNames.Count - 1);
        private int _selectedMoveIndex;
        public int SelectedMoveIndex
        {
            get => _selectedMoveIndex;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (_syncingList || value == _selectedMoveIndex || value < 0 || value >= MoveNames.Count) return;
                if (_dirty || _pendingMove != null) { _ = ConfirmDiscardAsync(value); return; }
                _selectedMoveIndex = value;
                OnPropertyChanged();
                LoadMove(value);
            }
        }

        private string _title = "Move data editor";
        public string Title { get => _title; private set => Set(ref _title, value); }

        private MoveData _currentFile;
        private int _currentId;
        private bool _loading;

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
                Title = "Move data editor (Preview)";
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
            _descBank = new EditableTextBank(MoveDescriptionsTextNumbers);
            try { _machineMoves = TMEditor.ReadMachineMoves(); }
            catch (Exception ex) { AppLogger.Warn("Move data editor: TM moves not read: " + ex.Message); }

            string[] moveNames = GetAttackNames();
            string[] typeNames = GetTypeNames();
            string[] battleSeqFiles = GetBattleEffectSequenceFiles();
            string[] db = PokeDatabase.MoveData.BattleSequenceDescriptions;

            foreach (string n in moveNames) MoveNames.Add(n);
            OnPropertyChanged(nameof(MaxMoveIndex));
            LoadCategories();
            foreach (string n in typeNames) TypeNames.Add(n);
            ReloadSplitContest();
            foreach ((ushort value, string name, string description) r in AttackRangeDescriptions) RangeItems.Add($"{r.name}: {r.description}");

            for (int i = 0; i < battleSeqFiles.Length; i++)
                BattleSeqItems.Add(i < db.Length && db[i] != null ? $"{i:D3} - {db[i]}" : $"{i:D3} - Undocumented");

            foreach (string flagName in Enum.GetNames(typeof(MoveFlags)).Skip(1))
            {
                FlagEntry entry = new FlagEntry { Name = flagName };
                entry.PropertyChanged += (_, __) => { if (!_loading && _currentFile != null) { RebuildFlagField(); SetDirty(); } };
                Flags.Add(entry);
            }

            if (MoveNames.Count > 1)
            {
                _selectedMoveIndex = 1;
                LoadMove(1);
            }
        }

        private void ReloadSplitContest()
        {
            LabelStore.Sync(SplitNames,   "move_split");
            LabelStore.Sync(ContestNames, "move_contest_conditions");
            SyncContestEffects(_contestAppeal);
            AppEvents.LabelsChanged -= OnLabelsChanged; AppEvents.LabelsChanged += OnLabelsChanged;
            AppEvents.NamesChanged  -= OnNamesChanged;  AppEvents.NamesChanged  += OnNamesChanged;
            AppEvents.RomPatchStateChanged -= OnPatchStateChanged; AppEvents.RomPatchStateChanged += OnPatchStateChanged;
        }

        // A list moved by its toolbox patch is reread, unless ticks made here are still unsaved.
        private void OnPatchStateChanged(object sender, EventArgs e)
        {
            if (!CategoriesChanged) LoadCategories();
        }
        private void OnLabelsChanged(object sender, EventArgs e)
        {
            ReloadSplitContest();
            // Re-resolve the combos' displayed text after the label lists were replaced in place.
            Repoke(_splitIndex,   nameof(SplitIndex),   v => _splitIndex = v);
            Repoke(_contestIndex, nameof(ContestIndex), v => _contestIndex = v);
            Repoke(_contestAppeal, nameof(ContestAppeal), v => _contestAppeal = v);
        }
        private void OnNamesChanged(object sender, EventArgs e)
        {
            // Move names live in a ROM text archive; refresh when the Text editor saves.
            SyncNames();
        }

        /// <summary>The ROM's move names, with an unsaved new move shown at its id.</summary>
        private void SyncNames()
        {
            List<string> names = RomInfo.GetAttackNames().ToList();
            if (_pendingMove != null)
            {
                while (names.Count < _pendingMove.Id) names.Add("");
                names.Insert(_pendingMove.Id, _pendingMove.DisplayName + " (not saved)");
            }
            _syncingList = true;
            try { ListSync.Apply(MoveNames, names); }
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
        public void Detach() { AppEvents.LabelsChanged -= OnLabelsChanged; AppEvents.NamesChanged -= OnNamesChanged; AppEvents.RomPatchStateChanged -= OnPatchStateChanged; }

        private void SetDirty() { if (_loading) return; _dirty = true; RefreshDirty(); RecordUndoSnapshot(); }
        private void SetClean() { _dirty = false; RefreshDirty(); }
        private void RefreshDirty() { OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private async Task ConfirmDiscardAsync(int newIndex)
        {
            bool discard = _pendingMove != null
                ? await DialogHelper.AskYesNoAsync("The new move isn't saved. Discard it?", "Unsaved changes")
                : await RecordSwitchGuard.ConfirmLeaveAsync(this, null, "move");
            if (!discard) { RecordSwitchGuard.SnapBack(() => _selectedMoveIndex, v => _selectedMoveIndex = v, () => OnPropertyChanged(nameof(SelectedMoveIndex))); return; }
            _dirty = false;
            RevertCategories();
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
            _currentFile = _pendingImports.TryGetValue(id, out MoveData imported) ? Copy(imported) : LoadRecord(id, out loadError);
            SourceLoadError = loadError;
            PopulateFromCurrentFile();
            LoadTexts();
            SetClean();
            _loading = false;
            RaiseCategories();

            // Loaded state is the clean baseline for undo on this move; switching moves starts fresh history.
            _history.Reset(Snapshot());
            _lastCaptureUtc = System.DateTime.MinValue;
            RaiseUndoState();
        }

        /// <summary>Pushes <see cref="_currentFile"/> into the bound fields. Caller must guard with _loading.</summary>
        private void PopulateFromCurrentFile()
        {
            // A box clamps its value when its limit drops, and the clamp would be written into this move,
            // so the limits only narrow once the new values are in.
            int acc = _currentFile.Accuracy, eff = _currentFile.SideEffectProbability, pri = _currentFile.Priority;
            SetLimits(Math.Max(Math.Max(100, acc), _accuracy), Math.Max(Math.Max(100, eff), _sideEffectPct),
                      Math.Min(Math.Min(-7, pri), _priority), Math.Max(Math.Max(5, pri), _priority));
            TypeIndex       = (int)_currentFile.Movetype;
            SplitIndex      = (int)_currentFile.Split;
            BattleSeqIndex  = (int)_currentFile.Battleeffect;
            ContestIndex    = (int)_currentFile.ContestConditionType;
            Power           = _currentFile.Damage;
            Accuracy        = acc;
            PP              = _currentFile.Pp;
            Priority        = pri;
            SideEffectPct   = eff;
            SyncContestEffects(_currentFile.ContestAppeal);
            ContestAppeal   = _currentFile.ContestAppeal;
            SetLimits(Math.Max(100, acc), Math.Max(100, eff), Math.Min(-7, pri), Math.Max(5, pri));

            int rangeIdx = 0;
            for (int i = 0; i < AttackRangeDescriptions.Length; i++)
                if (AttackRangeDescriptions[i].value == _currentFile.Target) { rangeIdx = i; break; }
            _rangeIndex = rangeIdx;
            OnPropertyChanged(nameof(RangeIndex));

            string[] flagNames = Enum.GetNames(typeof(MoveFlags)).Skip(1).ToArray();
            for (int i = 0; i < Flags.Count && i < flagNames.Length; i++)
                Flags[i].IsSet = (_currentFile.FlagField & (1 << i)) != 0;
        }

        private void RebuildFlagField()
        {
            if (_currentFile == null) return;
            byte field = 0;
            for (int i = 0; i < Flags.Count; i++)
                if (Flags[i].IsSet) field |= (byte)(1 << i);
            _currentFile.FlagField = field;
        }
    }
}
