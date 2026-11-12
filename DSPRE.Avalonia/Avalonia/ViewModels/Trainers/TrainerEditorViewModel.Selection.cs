using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using Avalonia.Threading;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    public partial class TrainerEditorViewModel
    {
        private int _selectedTrainerIndex = -1;
        public int SelectedTrainerIndex
        {
            get => _selectedTrainerIndex;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (value == _selectedTrainerIndex) return;
                if (_dirty && !_suppress && value >= 0 && _selectedTrainerIndex >= 0)
                {
                    // Snap the list back to the trainer still loaded until the user has answered.
                    int requested = value;
                    RecordSwitchGuard.SnapBack(() => _selectedTrainerIndex, v => _selectedTrainerIndex = v, () => OnPropertyChanged(nameof(SelectedTrainerIndex)));
                    _ = SwitchTrainerAsync(requested);
                    return;
                }
                if (Set(ref _selectedTrainerIndex, value) && !_suppress && value >= 0) _ = LoadTrainerWithBusyIndicatorAsync(value);
            }
        }

        private async Task SwitchTrainerAsync(int requested)
        {
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(new TrainerSwitchTarget(this), _owner, "trainer")) return;
            SetClean();
            if (Set(ref _selectedTrainerIndex, requested, nameof(SelectedTrainerIndex))) await LoadTrainerWithBusyIndicatorAsync(requested);
        }

        // Only the trainer's edits belong here: Save writes both tabs, Discard keeps the class edits.
        private sealed class TrainerSwitchTarget : IEditorWithUnsavedChanges
        {
            private readonly TrainerEditorViewModel _vm;
            public TrainerSwitchTarget(TrainerEditorViewModel vm) => _vm = vm;
            public bool HasUnsavedChanges => _vm._dirty;
            public string UnsavedChangesDescription => _vm.UnsavedChangesDescription;
            public void SaveChanges() => _vm.SaveChanges();
            public async Task<bool> SaveChangesAsync() => await ((IEditorWithUnsavedChanges)_vm).SaveChangesAsync() && !_vm._dirty;
            public void DiscardChanges() => _vm.DiscardTrainer();
        }

        private async Task LoadTrainerWithBusyIndicatorAsync(int index)
        {
            PushLoading();
            await Task.Yield();
            try { LoadTrainer(index); }
            finally { PopLoading(); }
        }

        private string _trainerName = "";
        public string TrainerName { get => _trainerName; set { if (Set(ref _trainerName, value) && !_suppress) SetDirty(); } }
        public int TrainerNameMaxLength { get; } = SafeNameMax();
        private static int SafeNameMax() { try { return TrainerNameMaxChars; } catch (Exception ex) { AppLogger.WarnOnce("TrainerEditorViewModel.SafeNameMax", ex); return TrainerFile.CompressedNameLen(TrainerFile.DefaultNameLen + 1); } }

        private int _trainerClassIndex = -1;
        public int TrainerClassIndex
        {
            get => _trainerClassIndex;
            set
            {
                // The ComboBox reports -1 while its list refills; only a load may clear the class.
                if (value < 0 && !_suppress)
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(TrainerClassIndex)));
                    return;
                }
                if (Set(ref _trainerClassIndex, value)) { if (!_suppress) SetDirty(); UpdateTrainerSprite(); OnPropertyChanged(nameof(ClassGender)); }
            }
        }

        /// <summary>The class's gender, which goes into every party Pokémon's personality.</summary>
        public string ClassGender
        {
            get
            {
                if (_trainerClassIndex < 0 || IsHgeActive) return "";
                try { return DVCalculator.TrainerClassGender.GetTrainerClassGender(_trainerClassIndex) ? "Male" : "Female"; }
                catch (Exception e) when (e is IOException || e is ArgumentException || e is IndexOutOfRangeException) { return ""; }
            }
        }

        private uint _battleType;
        public bool DoubleBattle
        {
            get => (_battleType & 2) != 0;
            set => SetBattleType(value ? _battleType | 2u : _battleType & ~2u);
        }
        public string BattleTypeRaw
        {
            get => _battleType.ToString();
            set { if (uint.TryParse(value?.Trim(), out uint v)) SetBattleType(v); }
        }

        /// <summary>The battle type flags as a number, for the number box.</summary>
        public decimal BattleTypeValue
        {
            get => _battleType;
            set => SetBattleType((uint)Math.Max(0, value));
        }
        private void SetBattleType(uint value)
        {
            if (value == _battleType) return;
            _battleType = value;
            OnPropertyChanged(nameof(DoubleBattle));
            OnPropertyChanged(nameof(BattleTypeRaw));
            OnPropertyChanged(nameof(BattleTypeValue));
            if (!_suppress) SetDirty();
        }
        private bool _chooseMoves;
        public bool ChooseMoves
        {
            get => _chooseMoves;
            set
            {
                if (!Set(ref _chooseMoves, value)) return;
                QueueDerived();
                if (value && !_suppress) FillLevelUpMoves();
                ApplyMovesEnabled();
                if (!_suppress) SetDirty();
            }
        }
        private bool _chooseItems; public bool ChooseItems { get => _chooseItems; set { if (Set(ref _chooseItems, value)) { ApplyItemsEnabled(); if (!_suppress) SetDirty(); } } }

        private decimal _partyCount = 1;
        public decimal PartyCount
        {
            get => _partyCount;
            set { if (Set(ref _partyCount, value)) { ApplyPartyVisibility(); if (!_suppress) SetDirty(); } }
        }

        private readonly TrainerClassSpriteRenderer _sprite = new TrainerClassSpriteRenderer();
        private Bitmap _classImage; public Bitmap ClassImage { get => _classImage; private set => Set(ref _classImage, value); }
        public bool HasClassSprite => _sprite.HasSprite;
        private decimal _classFrame; public decimal ClassFrame { get => _classFrame; set { if (Set(ref _classFrame, value)) RenderSprite(); } }
        private decimal _classFrameMax; public decimal ClassFrameMax { get => _classFrameMax; private set => Set(ref _classFrameMax, value); }
    }
}
