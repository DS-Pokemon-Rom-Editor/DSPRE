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

            // Reset first so index 0 on the new group still reloads the fields.
            _morningIndex = _dayIndex = _nightIndex = _objectIndex = -1;
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
        public int MorningSpecies { get => _morningSpecies; set { if (Set(ref _morningSpecies, value) && !_suppress) ApplyNormal(_group?.MorningEncounters, _morningIndex, MorningItems, value, null); } }
        private decimal _morningLevel;
        public decimal MorningLevel { get => _morningLevel; set { if (Set(ref _morningLevel, value) && !_suppress) ApplyNormal(_group?.MorningEncounters, _morningIndex, MorningItems, null, (int)value); } }

        // ── Normal: Day ─────────────────────────────────────────────────────────────
        private int _dayIndex = -1;
        public int DayIndex { get => _dayIndex; set { if (Set(ref _dayIndex, value)) LoadNormal(_group?.DayEncounters, value, v => DaySpecies = v, v => DayLevel = v); } }
        private int _daySpecies = -1;
        public int DaySpecies { get => _daySpecies; set { if (Set(ref _daySpecies, value) && !_suppress) ApplyNormal(_group?.DayEncounters, _dayIndex, DayItems, value, null); } }
        private decimal _dayLevel;
        public decimal DayLevel { get => _dayLevel; set { if (Set(ref _dayLevel, value) && !_suppress) ApplyNormal(_group?.DayEncounters, _dayIndex, DayItems, null, (int)value); } }

        // ── Normal: Night ───────────────────────────────────────────────────────────
        private int _nightIndex = -1;
        public int NightIndex { get => _nightIndex; set { if (Set(ref _nightIndex, value)) LoadNormal(_group?.NightEncounters, value, v => NightSpecies = v, v => NightLevel = v); } }
        private int _nightSpecies = -1;
        public int NightSpecies { get => _nightSpecies; set { if (Set(ref _nightSpecies, value) && !_suppress) ApplyNormal(_group?.NightEncounters, _nightIndex, NightItems, value, null); } }
        private decimal _nightLevel;
        public decimal NightLevel { get => _nightLevel; set { if (Set(ref _nightLevel, value) && !_suppress) ApplyNormal(_group?.NightEncounters, _nightIndex, NightItems, null, (int)value); } }

        // Levels show clamped to the boxes' range so they never coerce; only an edit to that box writes it back.
        private const int MaxShownLevel = 100;

        private void LoadNormal(BindingList<SafariZoneEncounter> src, int index, Action<int> setSpecies, Action<decimal> setLevel)
        {
            if (src == null || index < 0 || index >= src.Count) return;
            _suppress = true;
            setSpecies(src[index].pokemonID < SpeciesNames.Count ? src[index].pokemonID : -1);
            setLevel(Math.Min((int)src[index].level, MaxShownLevel));
            _suppress = false;
        }

        // Only the edited field is written, so an out-of-range value the boxes can't show survives other edits.
        private void ApplyNormal(BindingList<SafariZoneEncounter> src, int index, ObservableCollection<string> display, int? species, int? level)
        {
            if (src == null || index < 0 || index >= src.Count) return;
            if (species is int sp) { if (sp < 0) return; src[index].pokemonID = (ushort)sp; }
            if (level is int lv) src[index].level = (byte)Math.Max(0, Math.Min(255, lv));
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
        public int ObjMorningSpecies { get => _objMorningSpecies; set { if (Set(ref _objMorningSpecies, value) && !_suppress) ApplyObjectEncounter(_group?.MorningEncountersObject, value, null); } }
        private decimal _objMorningLevel;
        public decimal ObjMorningLevel { get => _objMorningLevel; set { if (Set(ref _objMorningLevel, value) && !_suppress) ApplyObjectEncounter(_group?.MorningEncountersObject, null, (int)value); } }

        private int _objDaySpecies = -1;
        public int ObjDaySpecies { get => _objDaySpecies; set { if (Set(ref _objDaySpecies, value) && !_suppress) ApplyObjectEncounter(_group?.DayEncountersObject, value, null); } }
        private decimal _objDayLevel;
        public decimal ObjDayLevel { get => _objDayLevel; set { if (Set(ref _objDayLevel, value) && !_suppress) ApplyObjectEncounter(_group?.DayEncountersObject, null, (int)value); } }

        private int _objNightSpecies = -1;
        public int ObjNightSpecies { get => _objNightSpecies; set { if (Set(ref _objNightSpecies, value) && !_suppress) ApplyObjectEncounter(_group?.NightEncountersObject, value, null); } }
        private decimal _objNightLevel;
        public decimal ObjNightLevel { get => _objNightLevel; set { if (Set(ref _objNightLevel, value) && !_suppress) ApplyObjectEncounter(_group?.NightEncountersObject, null, (int)value); } }

        private int _reqType = -1;
        public int ReqType
        {
            get => _reqType;
            set
            {
                if (!Set(ref _reqType, value)) return;
                OnPropertyChanged(nameof(RequirementTypeWarning));
                if (!_suppress) ApplyRequirement(_group?.ObjectRequirements, value, null);
            }
        }
        private decimal _reqPoints;
        public decimal ReqPoints { get => _reqPoints; set { if (Set(ref _reqPoints, value) && !_suppress) ApplyRequirement(_group?.ObjectRequirements, null, (int)value); } }

        private int _optReqType = -1;
        public int OptReqType { get => _optReqType; set { if (Set(ref _optReqType, value) && !_suppress) ApplyRequirement(_group?.OptionalObjectRequirements, value, null); } }
        private decimal _optReqPoints;
        public decimal OptReqPoints { get => _optReqPoints; set { if (Set(ref _optReqPoints, value) && !_suppress) ApplyRequirement(_group?.OptionalObjectRequirements, null, (int)value); } }

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
            list != null && i < list.Count && list[i].pokemonID < SpeciesNames.Count ? list[i].pokemonID : -1;
        private int LevelOf(BindingList<SafariZoneEncounter> list, int i) =>
            list != null && i < list.Count ? Math.Min((int)list[i].level, MaxShownLevel) : 0;

        private void ApplyObjectEncounter(BindingList<SafariZoneEncounter> list, int? species, int? level)
        {
            int i = _objectIndex;
            if (list == null || i < 0 || i >= list.Count) return;
            if (species is int sp) { if (sp < 0) return; list[i].pokemonID = (ushort)sp; }
            if (level is int lv) list[i].level = (byte)Math.Max(0, Math.Min(255, lv));
            Touch();
        }

        private void ApplyRequirement(BindingList<SafariZoneObjectRequirement> list, int? type, int? points)
        {
            int i = _objectIndex;
            if (list == null || i < 0 || i >= list.Count) return;
            if (type is int t) { if (t < 0) return; list[i].typeID = (byte)t; }
            if (points is int p) list[i].quantity = (byte)Math.Max(0, Math.Min(255, p));
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
