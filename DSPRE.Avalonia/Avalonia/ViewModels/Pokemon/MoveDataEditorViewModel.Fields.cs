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
    public partial class MoveDataEditorViewModel
    {
        private int _typeIndex;
        public int TypeIndex { get => _typeIndex; set { if (Set(ref _typeIndex, value) && _currentFile != null) { _currentFile.Movetype = (PokemonType)value; SetDirty(); } } }

        private int _splitIndex;
        public int SplitIndex { get => _splitIndex; set { if (Set(ref _splitIndex, value) && _currentFile != null) { _currentFile.Split = (MoveSplit)value; SetDirty(); } } }

        private int _rangeIndex;
        public int RangeIndex { get => _rangeIndex; set { if (Set(ref _rangeIndex, value) && _currentFile != null) { _currentFile.Target = AttackRangeDescriptions[value].value; SetDirty(); } } }

        private int _battleSeqIndex;
        public int BattleSeqIndex { get => _battleSeqIndex; set { if (value < 0) return; if (Set(ref _battleSeqIndex, value) && _currentFile != null) { _currentFile.Battleeffect = (ushort)value; SetDirty(); } } }

        private int _contestIndex;
        public int ContestIndex { get => _contestIndex; set { if (Set(ref _contestIndex, value) && _currentFile != null) { _currentFile.ContestConditionType = (ContestCondition)value; SetDirty(); } } }

        private int _power;
        public int Power { get => _power; set { if (Set(ref _power, value) && _currentFile != null) { _currentFile.Damage = (byte)value; SetDirty(); } } }

        private int _accuracy;
        public int Accuracy { get => _accuracy; set { if (Set(ref _accuracy, value) && _currentFile != null) { _currentFile.Accuracy = (byte)value; SetDirty(); } } }

        private int _pp;
        public int PP { get => _pp; set { if (Set(ref _pp, value) && _currentFile != null) { _currentFile.Pp = (byte)value; SetDirty(); } } }

        private int _priority;
        public int Priority { get => _priority; set { if (Set(ref _priority, value) && _currentFile != null) { _currentFile.Priority = (sbyte)value; SetDirty(); } } }

        private int _sideEffectPct;
        // Vanilla ranges, widened for a move already outside them so loading never changes it.
        public int AccuracyMax { get; private set; } = 100;
        public int SideEffectMax { get; private set; } = 100;
        public int PriorityMin { get; private set; } = -7;
        public int PriorityMax { get; private set; } = 5;

        private void SetLimits(int accMax, int effMax, int priMin, int priMax)
        {
            AccuracyMax = accMax; SideEffectMax = effMax; PriorityMin = priMin; PriorityMax = priMax;
            OnPropertyChanged(nameof(AccuracyMax)); OnPropertyChanged(nameof(SideEffectMax));
            OnPropertyChanged(nameof(PriorityMin)); OnPropertyChanged(nameof(PriorityMax));
        }

        public int SideEffectPct { get => _sideEffectPct; set { if (Set(ref _sideEffectPct, value) && _currentFile != null) { _currentFile.SideEffectProbability = (byte)value; SetDirty(); } } }

        private int _contestAppeal;
        public int ContestAppeal
        {
            get => _contestAppeal;
            set
            {
                // A combo with no selection reports -1; that is not a value to store.
                if (value < 0 || !Set(ref _contestAppeal, value)) return;
                OnPropertyChanged(nameof(ContestHearts));
                if (_currentFile != null) { _currentFile.ContestAppeal = (byte)value; SetDirty(); }
            }
        }
        public int ContestHearts => _contestAppeal < MoveData.ContestEffectAppeal.Length ? MoveData.ContestEffectAppeal[_contestAppeal] / 10 : 0;
        public bool HasContestEffects => RomInfo.GameFamily != RomInfo.GameFamilies.HGSS;

        // Effect labels, plus plain numbers for any id past them so every stored byte can show.
        private void SyncContestEffects(int need)
        {
            List<string> labels = LabelStore.Get("move_contest_effects").ToList();
            for (int i = labels.Count; i <= need; i++) labels.Add(i.ToString());
            ListSync.Apply(ContestEffectNames, labels);
        }

        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set
            {
                value = (value ?? "").Replace("\r\n", "\n");
                if (Set(ref _description, value)) { RaiseTextChecks(); if (!_loading) SetDirty(); }
            }
        }
    }
}
