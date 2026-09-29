using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>
    /// ViewModel for a single Safari Zone encounter group (Grass / Surf / Old Rod /
    /// Good Rod / Super Rod). Each group has independent Morning/Day/Night "normal"
    /// encounter lists, plus a set of "object" encounter slots that are shared
    /// (by index) across the three times and carry item requirements.
    ///
    /// Mirrors the WinForms <c>SafariZoneEncounterGroupEditor</c> + its three
    /// <c>SafariZoneEncounterEditorTab</c>s. Raises <see cref="Changed"/> on any edit
    /// so the parent <see cref="SafariZoneEncounterViewModel"/> can mark itself dirty.
    /// </summary>
    public class SafariZoneGroupViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler Changed;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private bool _suppress;
        private SafariZoneEncounterGroup _group;
        internal SafariZoneEncounterGroup CurrentGroup => _group;

        // hg-engine's bonus/"object" slot count is a fixed per-rod-type #define, not user-addable like
        // vanilla's. The parent VM sets this false when hg-engine is active so the in-memory list can't
        // drift out of sync with the real fixed-size array it gets written into.
        private bool _canEditObjectSlotCount = true;
        public bool CanEditObjectSlotCount { get => _canEditObjectSlotCount; set => Set(ref _canEditObjectSlotCount, value); }

        // Set on the surfing group: with no bonus slots the game hands surfing and every rod ten level 5 Magikarp.
        public bool WarnWhenNoSlots { get; set; }
        public bool NoSlotsWarning => WarnWhenNoSlots && _group != null && _group.ObjectRequirements.Count == 0;
        public bool RequirementTypeWarning => _group != null && _objectIndex >= 0 && _reqType == 0;

        public ObservableCollection<string> SpeciesNames { get; }
        public ObservableCollection<string> ObjectTypeNames { get; } = new ObservableCollection<string>();

        // Normal encounter lists (display) per time-of-day.
        public ObservableCollection<string> MorningItems { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> DayItems { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> NightItems { get; } = new ObservableCollection<string>();

        // Object slots (display), one entry per shared object slot.
        public ObservableCollection<string> ObjectItems { get; } = new ObservableCollection<string>();

        public SafariZoneGroupViewModel(ObservableCollection<string> speciesNames)
        {
            SpeciesNames = speciesNames ?? new ObservableCollection<string>();
            foreach (var t in SafariZoneObjectRequirement.ObjectTypes.Values) ObjectTypeNames.Add(t);
        }

        public void SetData(SafariZoneEncounterGroup group)
        {
            _group = group;
            _suppress = true;
            RebuildNormal(MorningItems, group?.MorningEncounters);
            RebuildNormal(DayItems, group?.DayEncounters);
            RebuildNormal(NightItems, group?.NightEncounters);
            RebuildObjects();
            _suppress = false;
            OnPropertyChanged(nameof(NoSlotsWarning));

            MorningIndex = MorningItems.Count > 0 ? 0 : -1;
            DayIndex = DayItems.Count > 0 ? 0 : -1;
            NightIndex = NightItems.Count > 0 ? 0 : -1;
            ObjectIndex = ObjectItems.Count > 0 ? 0 : -1;
        }

        private static void RebuildNormal(ObservableCollection<string> col, BindingList<SafariZoneEncounter> src)
        {
            col.Clear();
            if (src == null) return;
            foreach (var e in src) col.Add(e.ToString());
        }

        private void RebuildObjects()
        {
            ObjectItems.Clear();
            if (_group == null) return;
            for (int i = 0; i < _group.ObjectRequirements.Count; i++)
                ObjectItems.Add($"Object slot {i}");
        }

        private void Touch() { if (!_suppress) Changed?.Invoke(this, EventArgs.Empty); }

        // ── Normal: Morning ─────────────────────────────────────────────────────────
        private int _morningIndex = -1;
        public int MorningIndex { get => _morningIndex; set { if (Set(ref _morningIndex, value)) LoadNormal(_group?.MorningEncounters, value, v => MorningSpecies = v, v => MorningLevel = v); } }
        private int _morningSpecies = -1;
        public int MorningSpecies { get => _morningSpecies; set { if (Set(ref _morningSpecies, value) && !_suppress) ApplyNormal(_group?.MorningEncounters, _morningIndex, MorningItems, value, (int)_morningLevel); } }
        private decimal _morningLevel;
        public decimal MorningLevel { get => _morningLevel; set { if (Set(ref _morningLevel, value) && !_suppress) ApplyNormal(_group?.MorningEncounters, _morningIndex, MorningItems, _morningSpecies, (int)value); } }

        // ── Normal: Day ─────────────────────────────────────────────────────────────
        private int _dayIndex = -1;
        public int DayIndex { get => _dayIndex; set { if (Set(ref _dayIndex, value)) LoadNormal(_group?.DayEncounters, value, v => DaySpecies = v, v => DayLevel = v); } }
        private int _daySpecies = -1;
        public int DaySpecies { get => _daySpecies; set { if (Set(ref _daySpecies, value) && !_suppress) ApplyNormal(_group?.DayEncounters, _dayIndex, DayItems, value, (int)_dayLevel); } }
        private decimal _dayLevel;
        public decimal DayLevel { get => _dayLevel; set { if (Set(ref _dayLevel, value) && !_suppress) ApplyNormal(_group?.DayEncounters, _dayIndex, DayItems, _daySpecies, (int)value); } }

        // ── Normal: Night ───────────────────────────────────────────────────────────
        private int _nightIndex = -1;
        public int NightIndex { get => _nightIndex; set { if (Set(ref _nightIndex, value)) LoadNormal(_group?.NightEncounters, value, v => NightSpecies = v, v => NightLevel = v); } }
        private int _nightSpecies = -1;
        public int NightSpecies { get => _nightSpecies; set { if (Set(ref _nightSpecies, value) && !_suppress) ApplyNormal(_group?.NightEncounters, _nightIndex, NightItems, value, (int)_nightLevel); } }
        private decimal _nightLevel;
        public decimal NightLevel { get => _nightLevel; set { if (Set(ref _nightLevel, value) && !_suppress) ApplyNormal(_group?.NightEncounters, _nightIndex, NightItems, _nightSpecies, (int)value); } }

        private void LoadNormal(BindingList<SafariZoneEncounter> src, int index, Action<int> setSpecies, Action<decimal> setLevel)
        {
            if (src == null || index < 0 || index >= src.Count) return;
            _suppress = true;
            setSpecies(src[index].pokemonID < SpeciesNames.Count ? src[index].pokemonID : 0);
            setLevel(src[index].level);
            _suppress = false;
        }

        private void ApplyNormal(BindingList<SafariZoneEncounter> src, int index, ObservableCollection<string> display, int species, int level)
        {
            if (src == null || index < 0 || index >= src.Count) return;
            src[index].pokemonID = (ushort)(species >= 0 ? species : 0);
            src[index].level = (byte)Math.Max(0, Math.Min(255, level));
            _suppress = true;
            display[index] = src[index].ToString();
            _suppress = false;
            Touch();
        }

        // ── Object slots (shared index across the three times + requirements) ─────────
        private int _objectIndex = -1;
        public int ObjectIndex
        {
            get => _objectIndex;
            set { if (Set(ref _objectIndex, value)) LoadObject(value); }
        }

        private int _objMorningSpecies = -1;
        public int ObjMorningSpecies { get => _objMorningSpecies; set { if (Set(ref _objMorningSpecies, value) && !_suppress) ApplyObjectEncounter(_group?.MorningEncountersObject, value, (int)_objMorningLevel); } }
        private decimal _objMorningLevel;
        public decimal ObjMorningLevel { get => _objMorningLevel; set { if (Set(ref _objMorningLevel, value) && !_suppress) ApplyObjectEncounter(_group?.MorningEncountersObject, _objMorningSpecies, (int)value); } }

        private int _objDaySpecies = -1;
        public int ObjDaySpecies { get => _objDaySpecies; set { if (Set(ref _objDaySpecies, value) && !_suppress) ApplyObjectEncounter(_group?.DayEncountersObject, value, (int)_objDayLevel); } }
        private decimal _objDayLevel;
        public decimal ObjDayLevel { get => _objDayLevel; set { if (Set(ref _objDayLevel, value) && !_suppress) ApplyObjectEncounter(_group?.DayEncountersObject, _objDaySpecies, (int)value); } }

        private int _objNightSpecies = -1;
        public int ObjNightSpecies { get => _objNightSpecies; set { if (Set(ref _objNightSpecies, value) && !_suppress) ApplyObjectEncounter(_group?.NightEncountersObject, value, (int)_objNightLevel); } }
        private decimal _objNightLevel;
        public decimal ObjNightLevel { get => _objNightLevel; set { if (Set(ref _objNightLevel, value) && !_suppress) ApplyObjectEncounter(_group?.NightEncountersObject, _objNightSpecies, (int)value); } }

        private int _reqType = -1;
        public int ReqType
        {
            get => _reqType;
            set
            {
                if (!Set(ref _reqType, value)) return;
                OnPropertyChanged(nameof(RequirementTypeWarning));
                if (!_suppress) ApplyRequirement(_group?.ObjectRequirements, value, (int)_reqPoints);
            }
        }
        private decimal _reqPoints;
        public decimal ReqPoints { get => _reqPoints; set { if (Set(ref _reqPoints, value) && !_suppress) ApplyRequirement(_group?.ObjectRequirements, _reqType, (int)value); } }

        private int _optReqType = -1;
        public int OptReqType { get => _optReqType; set { if (Set(ref _optReqType, value) && !_suppress) ApplyRequirement(_group?.OptionalObjectRequirements, value, (int)_optReqPoints); } }
        private decimal _optReqPoints;
        public decimal OptReqPoints { get => _optReqPoints; set { if (Set(ref _optReqPoints, value) && !_suppress) ApplyRequirement(_group?.OptionalObjectRequirements, _optReqType, (int)value); } }

        private void LoadObject(int index)
        {
            if (_group == null || index < 0 || index >= _group.ObjectRequirements.Count) return;
            _suppress = true;
            ObjMorningSpecies = SpeciesOf(_group.MorningEncountersObject, index);
            ObjMorningLevel = LevelOf(_group.MorningEncountersObject, index);
            ObjDaySpecies = SpeciesOf(_group.DayEncountersObject, index);
            ObjDayLevel = LevelOf(_group.DayEncountersObject, index);
            ObjNightSpecies = SpeciesOf(_group.NightEncountersObject, index);
            ObjNightLevel = LevelOf(_group.NightEncountersObject, index);
            ReqType = _group.ObjectRequirements[index].typeID;
            ReqPoints = _group.ObjectRequirements[index].quantity;
            OptReqType = _group.OptionalObjectRequirements[index].typeID;
            OptReqPoints = _group.OptionalObjectRequirements[index].quantity;
            _suppress = false;
        }

        private int SpeciesOf(BindingList<SafariZoneEncounter> list, int i) =>
            list != null && i < list.Count && list[i].pokemonID < SpeciesNames.Count ? list[i].pokemonID : 0;
        private int LevelOf(BindingList<SafariZoneEncounter> list, int i) =>
            list != null && i < list.Count ? list[i].level : 0;

        private void ApplyObjectEncounter(BindingList<SafariZoneEncounter> list, int species, int level)
        {
            int i = _objectIndex;
            if (list == null || i < 0 || i >= list.Count) return;
            list[i].pokemonID = (ushort)(species >= 0 ? species : 0);
            list[i].level = (byte)Math.Max(0, Math.Min(255, level));
            Touch();
        }

        private void ApplyRequirement(BindingList<SafariZoneObjectRequirement> list, int type, int points)
        {
            int i = _objectIndex;
            if (list == null || i < 0 || i >= list.Count) return;
            list[i].typeID = (byte)Math.Max(0, type);
            list[i].quantity = (byte)Math.Max(0, Math.Min(255, points));
            Touch();
        }

        // ── Add / remove object slot (keeps all six lists + requirements in sync) ─────
        public void AddObjectSlot()
        {
            if (_group == null || !_canEditObjectSlotCount) return;
            _group.MorningEncountersObject.Add(new SafariZoneEncounter());
            _group.DayEncountersObject.Add(new SafariZoneEncounter());
            _group.NightEncountersObject.Add(new SafariZoneEncounter());
            _group.ObjectRequirements.Add(new SafariZoneObjectRequirement(1, 1));
            _group.OptionalObjectRequirements.Add(new SafariZoneObjectRequirement(0, 0));
            _group.ObjectSlots = (byte)_group.ObjectRequirements.Count;
            _suppress = true; RebuildObjects(); _suppress = false;
            OnPropertyChanged(nameof(NoSlotsWarning));
            ObjectIndex = ObjectItems.Count - 1;
            Touch();
        }

        public void RemoveObjectSlot()
        {
            if (_group == null || !_canEditObjectSlotCount || _group.ObjectRequirements.Count == 0) return;
            int last = _group.ObjectRequirements.Count - 1;
            _group.MorningEncountersObject.RemoveAt(last);
            _group.DayEncountersObject.RemoveAt(last);
            _group.NightEncountersObject.RemoveAt(last);
            _group.ObjectRequirements.RemoveAt(last);
            _group.OptionalObjectRequirements.RemoveAt(last);
            _group.ObjectSlots = (byte)_group.ObjectRequirements.Count;
            _suppress = true; RebuildObjects(); _suppress = false;
            OnPropertyChanged(nameof(NoSlotsWarning));
            ObjectIndex = ObjectItems.Count > 0 ? ObjectItems.Count - 1 : -1;
            Touch();
        }
    }
}
