using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DSPRE.Avalonia.Models;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    /// <summary>
    /// Avalonia port of the WinForms <c>TrainerFlagBulkEditor</c>: bulk-edit trainer AI flags and the
    /// double-battle flag, either by selecting trainers and toggling flags for all of them at once
    /// (By Trainer), or by picking one flag and checking/unchecking it per trainer (By Flag).
    /// Choose Items/Choose Moves aren't included: they control the trainerParty file's binary layout,
    /// not just a flag, so editing them here without touching that file corrupts the party data.
    /// </summary>
    public class TrainerFlagBulkEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        public static readonly string[] FlagNames =
        {
            "AI: Basic", "AI: Evaluate Attack", "AI: Expert", "AI: Setup", "AI: Risky",
            "AI: Prioritize Extremes", "AI: Baton Pass", "AI: Tag Strategy", "AI: Check HP",
            "AI: Weather", "AI: Harassment",
            "Double Battle"
        };
        private const int AI_FLAG_COUNT = TrainerProperties.AI_COUNT;

        private readonly string[] _trainerNames;
        private readonly string[] _trainerClassNames;
        private readonly int _trainerCount;
        private readonly Dictionary<int, TrainerProperties> _trainerData = new();
        private readonly HashSet<int> _selectedTrainerIds = new();
        private bool _suppressTreeEvents;
        private bool _suppressChecklistApply;
        private bool _isDirty;

        public ObservableCollection<TrainerFlagGroupNode> Tree { get; } = new();
        public ObservableCollection<FlagChecklistItem> FlagChecklist { get; } = new();
        public string[] FlagNamesList => FlagNames;

        public void SetMode(bool byFlag) => IsByFlagMode = byFlag;

        public bool IsByTrainerMode => !IsByFlagMode;
        private bool _isByFlagMode;
        public bool IsByFlagMode
        {
            get => _isByFlagMode;
            set
            {
                if (!Set(ref _isByFlagMode, value)) return;
                OnPropertyChanged(nameof(IsByTrainerMode));
                OnPropertyChanged(nameof(SelectAllLabel));
                OnPropertyChanged(nameof(SelectNoneLabel));
                RebuildTree();
                if (!value) RefreshFlagChecklistFromSelection();
                UpdateStatus();
            }
        }

        public string SelectAllLabel => IsByFlagMode ? "Enable All" : "Select All";
        public string SelectNoneLabel => IsByFlagMode ? "Disable All" : "Select None";

        private int _currentFlagIndex;
        public int CurrentFlagIndex
        {
            get => _currentFlagIndex;
            set
            {
                if (!Set(ref _currentFlagIndex, value)) return;
                if (IsByFlagMode) { RebuildTree(); UpdateStatus(); }
            }
        }

        private string _filterText = "";
        public string FilterText
        {
            get => _filterText;
            set { if (Set(ref _filterText, value)) RebuildTree(); }
        }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // ── IEditorWithUnsavedChanges ──
        public bool HasUnsavedChanges => _isDirty;
        public string UnsavedChangesDescription => "Trainer Flag Bulk Editor";
        public void SaveChanges() => SaveAllChanges();
        public void DiscardChanges() { _isDirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        public TrainerFlagBulkEditorViewModel()
        {
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerProperties });

            _trainerNames = DSPRE.TrainerNames.GetAll();
            _trainerClassNames = GetTrainerClassNames();
            _trainerCount = Filesystem.GetTrainerPropertiesCount();

            LoadAllTrainerData();

            foreach (var name in FlagNames)
                FlagChecklist.Add(new FlagChecklistItem { Index = FlagChecklist.Count, Name = name });

            RebuildTree();
            RefreshFlagChecklistFromSelection();
            UpdateStatus();
        }

        private void LoadAllTrainerData()
        {
            string dir = gameDirs[DirNames.trainerProperties].unpackedDir;
            for (int i = 0; i < _trainerCount; i++)
            {
                using var fs = new FileStream(Path.Combine(dir, i.ToString("D4")), FileMode.Open);
                _trainerData[i] = new TrainerProperties((ushort)i, fs);
            }
        }

        private bool GetFlag(TrainerProperties tp, int flagIndex) =>
            flagIndex < AI_FLAG_COUNT ? tp.AI[flagIndex] : tp.doubleBattle;

        private void SetFlag(TrainerProperties tp, int flagIndex, bool value)
        {
            if (flagIndex < AI_FLAG_COUNT) tp.AI[flagIndex] = value;
            else tp.doubleBattle = value;
        }

        private string TrainerLabel(int id) =>
            id >= 0 && id < _trainerNames.Length ? _trainerNames[id] : $"[{id:D2}] ???";

        private string ClassLabel(byte classId) =>
            classId < _trainerClassNames.Length ? _trainerClassNames[classId] : $"Class {classId}";

        // ── Tree building ───────────────────────────────────────────────────
        private void RebuildTree()
        {
            _suppressTreeEvents = true;
            Tree.Clear();

            string filter = FilterText?.Trim();
            bool hasFilter = !string.IsNullOrEmpty(filter);

            var byClass = new SortedDictionary<byte, List<int>>();
            for (int i = 0; i < _trainerCount; i++)
            {
                byte classId = _trainerData[i].trainerClass;
                if (!byClass.TryGetValue(classId, out var list)) byClass[classId] = list = new List<int>();
                list.Add(i);
            }

            foreach (var (classId, memberIds) in byClass)
            {
                var matching = hasFilter
                    ? memberIds.Where(id => TrainerLabel(id).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList()
                    : memberIds;
                if (matching.Count == 0) continue;

                var group = new TrainerFlagGroupNode { ClassId = classId, OnCheckedChanged = OnGroupChecked };
                foreach (var id in matching)
                {
                    var leaf = new TrainerFlagLeafNode
                    {
                        TrainerId = id,
                        DisplayName = TrainerLabel(id),
                        OnCheckedChanged = OnLeafChecked,
                    };
                    leaf.SetCheckedSilent(IsByFlagMode ? GetFlag(_trainerData[id], CurrentFlagIndex) : _selectedTrainerIds.Contains(id));
                    group.Children.Add(leaf);
                }
                UpdateGroupDisplay(group);
                Tree.Add(group);
            }

            _suppressTreeEvents = false;
        }

        private void UpdateGroupDisplay(TrainerFlagGroupNode group)
        {
            int total = group.Children.Count;
            int checkedCount = group.Children.Count(c => c.IsChecked);
            group.DisplayName = $"{ClassLabel(group.ClassId)} [{checkedCount}/{total}]";
            group.SetCheckedSilent(total > 0 && checkedCount == total);
        }

        private void OnLeafChecked(TrainerFlagLeafNode leaf)
        {
            if (_suppressTreeEvents) return;

            ApplyLeafCheckSideEffect(leaf.TrainerId, leaf.IsChecked);

            var group = Tree.FirstOrDefault(g => g.Children.Contains(leaf));
            if (group != null) UpdateGroupDisplay(group);

            if (IsByTrainerMode) RefreshFlagChecklistFromSelection();
            UpdateStatus();
        }

        private void OnGroupChecked(TrainerFlagGroupNode group)
        {
            if (_suppressTreeEvents) return;

            _suppressTreeEvents = true;
            foreach (var child in group.Children)
            {
                child.SetCheckedSilent(group.IsChecked);
                ApplyLeafCheckSideEffect(child.TrainerId, group.IsChecked);
            }
            _suppressTreeEvents = false;
            UpdateGroupDisplay(group);

            if (IsByTrainerMode) RefreshFlagChecklistFromSelection();
            UpdateStatus();
        }

        private void ApplyLeafCheckSideEffect(int trainerId, bool isChecked)
        {
            if (IsByTrainerMode)
            {
                if (isChecked) _selectedTrainerIds.Add(trainerId);
                else _selectedTrainerIds.Remove(trainerId);
            }
            else
            {
                SetFlagForTrainer(trainerId, CurrentFlagIndex, isChecked);
            }
        }

        private void SetFlagForTrainer(int trainerId, int flagIndex, bool enabled)
        {
            var tp = _trainerData[trainerId];
            if (GetFlag(tp, flagIndex) == enabled) return;
            SetFlag(tp, flagIndex, enabled);
            _isDirty = true;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        public void SetAllVisibleLeavesChecked(bool value)
        {
            _suppressTreeEvents = true;
            foreach (var group in Tree)
            {
                foreach (var leaf in group.Children)
                {
                    leaf.SetCheckedSilent(value);
                    ApplyLeafCheckSideEffect(leaf.TrainerId, value);
                }
                UpdateGroupDisplay(group);
            }
            _suppressTreeEvents = false;

            if (IsByTrainerMode) RefreshFlagChecklistFromSelection();
            UpdateStatus();
        }

        // ── Right-hand flag checklist (By Trainer mode) ────────────────────
        private void RefreshFlagChecklistFromSelection()
        {
            for (int f = 0; f < FlagChecklist.Count; f++)
            {
                bool? state;
                if (_selectedTrainerIds.Count == 0)
                {
                    state = false;
                }
                else
                {
                    int haveCount = _selectedTrainerIds.Count(id => GetFlag(_trainerData[id], f));
                    state = haveCount == 0 ? false : haveCount == _selectedTrainerIds.Count ? true : (bool?)null;
                }
                FlagChecklist[f].SetChecked(state);
            }
        }

        public void ToggleFlagForSelection(int flagIndex)
        {
            if (_selectedTrainerIds.Count == 0)
            {
                AppMessages.Info("Select at least one trainer on the left first.", "No Selection");
                return;
            }

            bool enable = FlagChecklist[flagIndex].IsChecked != true;
            foreach (var id in _selectedTrainerIds)
                SetFlagForTrainer(id, flagIndex, enable);

            RefreshFlagChecklistFromSelection();
            UpdateStatus();
        }

        // ── Save ─────────────────────────────────────────────────────────
        public void SaveAllChanges()
        {
            string dir = gameDirs[DirNames.trainerProperties].unpackedDir;
            foreach (var kvp in _trainerData)
                File.WriteAllBytes(Path.Combine(dir, kvp.Key.ToString("D4")), kvp.Value.ToByteArray());

            _isDirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            UpdateStatus("All trainer flag changes have been saved.");
        }

        private void UpdateStatus(string message = null)
        {
            if (message != null) { StatusText = message; return; }

            if (IsByTrainerMode)
            {
                StatusText = $"{_trainerCount} trainers in {Tree.Count} classes. {_selectedTrainerIds.Count} selected." +
                    (_isDirty ? " [Unsaved Changes]" : "");
            }
            else
            {
                string flagLabel = CurrentFlagIndex >= 0 && CurrentFlagIndex < FlagNames.Length ? FlagNames[CurrentFlagIndex] : "?";
                int enabledCount = _trainerData.Count(kvp => GetFlag(kvp.Value, CurrentFlagIndex));
                StatusText = $"{flagLabel}: {enabledCount} of {_trainerCount} trainers have it enabled." +
                    (_isDirty ? " [Unsaved Changes]" : "");
            }
        }
    }
}
