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
using Avalonia.Media.Imaging;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using DSPRE.Avalonia.Views.Shell;
using DSPRE.Csv;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class PersonalDataEditorViewModel
    {
        public ObservableCollection<string> PokemonNames  { get; } = new();
        public ObservableCollection<string> TypeNames     { get; } = new();
        public ObservableCollection<string> AbilityNames  { get; } = new();
        public ObservableCollection<string> ItemNames     { get; } = new();
        public ObservableCollection<string> GrowthCurveNames { get; } = new();
        public ObservableCollection<string> DexColorNames { get; } = new();
        public ObservableCollection<string> EggGroupNames { get; } = new();

        public ObservableCollection<MachineRow> AddedMachines    { get; } = new();
        public ObservableCollection<MachineRow> AddableMachines  { get; } = new();

        /// <summary>A TM or HM in the compatibility lists: its label and the move it teaches.</summary>
        public sealed class MachineRow
        {
            public int Index { get; init; }
            public int Move { get; init; }
            public string Label { get; init; }
            public override string ToString() => Label;
        }

        private int _selectedAddedMachineIndex = -1;
        public int SelectedAddedMachineIndex
        {
            get => _selectedAddedMachineIndex;
            set => Set(ref _selectedAddedMachineIndex, value);
        }

        private int _selectedAddableMachineIndex = -1;
        public int SelectedAddableMachineIndex
        {
            get => _selectedAddableMachineIndex;
            set => Set(ref _selectedAddableMachineIndex, value);
        }

        private int _baseHP;   public int BaseHP   { get => _baseHP;   set { if (Set(ref _baseHP,   value) && !_loading) { _current.BaseHP    = (byte)value; SetDirty(); } } }
        private int _baseAtk;  public int BaseAtk  { get => _baseAtk;  set { if (Set(ref _baseAtk,  value) && !_loading) { _current.BaseAtk   = (byte)value; SetDirty(); } } }
        private int _baseDef;  public int BaseDef  { get => _baseDef;  set { if (Set(ref _baseDef,  value) && !_loading) { _current.BaseDef   = (byte)value; SetDirty(); } } }
        private int _baseSpe;  public int BaseSpe  { get => _baseSpe;  set { if (Set(ref _baseSpe,  value) && !_loading) { _current.BaseSpeed  = (byte)value; SetDirty(); } } }
        private int _baseSpA;  public int BaseSpA  { get => _baseSpA;  set { if (Set(ref _baseSpA,  value) && !_loading) { _current.BaseSpAtk = (byte)value; SetDirty(); } } }
        private int _baseSpD;  public int BaseSpD  { get => _baseSpD;  set { if (Set(ref _baseSpD,  value) && !_loading) { _current.BaseSpDef = (byte)value; SetDirty(); } } }

        private int _evHP;  public int EvHP  { get => _evHP;  set { if (Set(ref _evHP,  value) && !_loading) { _current.EvHP    = (byte)value; SetDirty(); } } }
        private int _evAtk; public int EvAtk { get => _evAtk; set { if (Set(ref _evAtk, value) && !_loading) { _current.EvAtk   = (byte)value; SetDirty(); } } }
        private int _evDef; public int EvDef { get => _evDef; set { if (Set(ref _evDef, value) && !_loading) { _current.EvDef   = (byte)value; SetDirty(); } } }
        private int _evSpe; public int EvSpe { get => _evSpe; set { if (Set(ref _evSpe, value) && !_loading) { _current.EvSpeed = (byte)value; SetDirty(); } } }
        private int _evSpA; public int EvSpA { get => _evSpA; set { if (Set(ref _evSpA, value) && !_loading) { _current.EvSpAtk = (byte)value; SetDirty(); } } }
        private int _evSpD; public int EvSpD { get => _evSpD; set { if (Set(ref _evSpD, value) && !_loading) { _current.EvSpDef = (byte)value; SetDirty(); } } }

        private int _type1Index; public int Type1Index { get => _type1Index; set { if (Set(ref _type1Index, value) && !_loading && _current != null && value >= 0) { _current.Type1 = (PokemonType)value; SetDirty(); } } }
        private int _type2Index; public int Type2Index { get => _type2Index; set { if (Set(ref _type2Index, value) && !_loading && _current != null && value >= 0) { _current.Type2 = (PokemonType)value; SetDirty(); } } }

        private int _ability1Index; public int Ability1Index { get => _ability1Index; set { if (Set(ref _ability1Index, value) && !_loading && _current != null && value >= 0) { _current.FirstAbility  = (ushort)value; SetDirty(); } } }
        private int _ability2Index; public int Ability2Index { get => _ability2Index; set { if (Set(ref _ability2Index, value) && !_loading && _current != null && value >= 0) { _current.SecondAbility = (ushort)value; SetDirty(); } } }

        private int _item1Index; public int Item1Index { get => _item1Index; set { if (!Set(ref _item1Index, value)) return; RaiseHeldItemOdds(); if (!_loading && _current != null) { _current.Item1 = (ushort)value; SetDirty(); } } }
        private int _item2Index; public int Item2Index { get => _item2Index; set { if (!Set(ref _item2Index, value)) return; RaiseHeldItemOdds(); if (!_loading && _current != null) { _current.Item2 = (ushort)value; SetDirty(); } } }

        // The game's odds table, so the chances follow any edit made in Wild held items.
        private DSPRE.ROMFiles.WildHeldItemOdds _heldItemOdds;
        public bool HasHeldItemOdds => _heldItemOdds != null;
        public bool CanOpenWildHeldItems => _heldItemOdds != null && BetaEditors.Allows("WildHeldItemOddsView");
        public string Item1Odds => HeldItemOdds(common: true);
        public string Item2Odds => HeldItemOdds(common: false);

        private string HeldItemOdds(bool common)
        {
            if (_heldItemOdds == null) return null;
            // The same item in both slots is always held.
            if (_item1Index == _item2Index && _item1Index != 0) return common ? "Always held" : "";
            if ((common ? _item1Index : _item2Index) == 0) return "";
            int plain = common ? _heldItemOdds.Normal.CommonPercent : _heldItemOdds.Normal.RarePercent;
            int eyes = common ? _heldItemOdds.CompoundEyes.CommonPercent : _heldItemOdds.CompoundEyes.RarePercent;
            return $"{plain}%, {eyes}% with Compound Eyes";
        }

        private void RaiseHeldItemOdds()
        {
            OnPropertyChanged(nameof(Item1Odds));
            OnPropertyChanged(nameof(Item2Odds));
        }

        private void LoadHeldItemOdds()
        {
            try { _heldItemOdds = DSPRE.ROMFiles.WildHeldItemOdds.WhyNot() == null ? DSPRE.ROMFiles.WildHeldItemOdds.Load() : null; }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is ArgumentException) { _heldItemOdds = null; }
        }

        private int _catchRate;       public int CatchRate       { get => _catchRate;       set { if (Set(ref _catchRate,       value) && !_loading) { _current.CatchRate       = (byte)value; SetDirty(); } } }
        private int _baseExp;         public int BaseExp         { get => _baseExp;         set { if (Set(ref _baseExp,         value) && !_loading) { _current.GivenExp        = (byte)value; SetDirty(); } } }
        private int _genderVec;       public int GenderVec       { get => _genderVec;       set { if (Set(ref _genderVec,       value) && !_loading) { _current.GenderVec       = (byte)value; GenderLabel = GetGenderText(value); SetDirty(); } } }
        private int _eggSteps;        public int EggSteps        { get => _eggSteps;        set { if (Set(ref _eggSteps,        value) && !_loading) { _current.EggSteps        = (byte)value; SetDirty(); } } }
        private int _baseFriendship;  public int BaseFriendship  { get => _baseFriendship;  set { if (Set(ref _baseFriendship,  value) && !_loading) { _current.BaseFriendship  = (byte)value; SetDirty(); } } }
        private int _escapeRate;      public int EscapeRate      { get => _escapeRate;      set { if (Set(ref _escapeRate,      value) && !_loading) { _current.EscapeRate      = (byte)value; SetDirty(); } } }

        private int _growthCurveIndex; public int GrowthCurveIndex { get => _growthCurveIndex; set { if (Set(ref _growthCurveIndex, value) && !_loading && _current != null) { _current.GrowthCurve = (PokemonGrowthCurve)value; SetDirty(); } } }
        private int _dexColorIndex;    public int DexColorIndex    { get => _dexColorIndex;    set { if (Set(ref _dexColorIndex,    value) && !_loading && _current != null) { _current.Color        = (PokemonDexColor)value;    SetDirty(); } } }
        private int _eggGroup1Index;   public int EggGroup1Index   { get => _eggGroup1Index;   set { if (Set(ref _eggGroup1Index,   value) && !_loading && _current != null) { _current.EggGroup1   = (byte)value; SetDirty(); } } }
        private int _eggGroup2Index;   public int EggGroup2Index   { get => _eggGroup2Index;   set { if (Set(ref _eggGroup2Index,   value) && !_loading && _current != null) { _current.EggGroup2   = (byte)value; SetDirty(); } } }
        private int _hatchResultIndex; public int HatchResultIndex { get => _hatchResultIndex; set { if (Set(ref _hatchResultIndex, value) && !_loading)                     SetDirty(); } }
        // pms.narc has one entry per personal file, and form entries hatch as themselves.
        public int HatchResultMaximum => Math.Max(0, PokemonNames.Count - 1);

        private bool _flipFlag;
        public bool FlipFlag
        {
            get => _flipFlag;
            set { if (Set(ref _flipFlag, value) && !_loading && _current != null) { _current.Flip = value; SetDirty(); } }
        }

        private string _genderLabel = string.Empty;
        public string GenderLabel { get => _genderLabel; private set => Set(ref _genderLabel, value); }

        private string _title = "Personal data editor";
        public string Title { get => _title; private set => Set(ref _title, value); }

        private Bitmap _monIconBitmap;
        public Bitmap MonIconBitmap { get => _monIconBitmap; private set => Set(ref _monIconBitmap, value); }
    }
}
