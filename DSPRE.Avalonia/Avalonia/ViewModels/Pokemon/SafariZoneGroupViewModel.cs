using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>
    /// One Safari Zone encounter group: morning, day and night rows side by side, plus the object slots the
    /// three times share, which carry item requirements.
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

        public ObservableCollection<string> SpeciesNames { get; }
        public ObservableCollection<string> ObjectTypeNames { get; } = new ObservableCollection<string>();

        public ObservableCollection<SlotRow> NormalRows { get; } = new ObservableCollection<SlotRow>();
        public ObservableCollection<ObjectRow> ObjectRows { get; } = new ObservableCollection<ObjectRow>();

        public SafariZoneGroupViewModel(ObservableCollection<string> speciesNames)
        {
            SpeciesNames = speciesNames ?? new ObservableCollection<string>();
            foreach (string t in SafariZoneObjectRequirement.ObjectTypes.Values) ObjectTypeNames.Add(t);
        }

        public void SetData(SafariZoneEncounterGroup group)
        {
            _group = group;
            _suppress = true;
            NormalRows.Clear();
            ObjectRows.Clear();
            if (group != null)
            {
                int normal = Math.Max(group.MorningEncounters.Count, Math.Max(group.DayEncounters.Count, group.NightEncounters.Count));
                for (int i = 0; i < normal; i++)
                    NormalRows.Add(new SlotRow(this, i, group.MorningEncounters, group.DayEncounters, group.NightEncounters));
                RebuildObjects();
            }
            _suppress = false;
            OnPropertyChanged(nameof(NoSlotsWarning));
        }

        /// <summary>Rebinds to a group read again (an undo). Rows edit in place, so there is no selection to keep.</summary>
        public void SetData(SafariZoneEncounterGroup group, bool keepSelection) => SetData(group);

        private void RebuildObjects()
        {
            ObjectRows.Clear();
            if (_group == null) return;
            for (int i = 0; i < _group.ObjectRequirements.Count; i++)
                ObjectRows.Add(new ObjectRow(this, i));
        }

        private void Touch() { if (!_suppress) Changed?.Invoke(this, EventArgs.Empty); }

        // Around a rename of the shared species list: a combo that drops its pick must not write it back.
        internal void BeginNameRefresh() => _suppress = true;
        internal void EndNameRefresh() { _suppress = false; SetData(_group); }

        // Levels show clamped to the boxes' range so they never coerce; only an edit to that box writes it back.
        private const int MaxShownLevel = 100;

        /// <summary>True on an hg-engine folder, whose species words also carry a form.</summary>
        public bool ShowForms => DSPRE.HgEngine.HgEngineProject.IsActive;

        private static int BaseSpecies(ushort id) => id & DSPRE.HgEngine.HgEngineTrainerSource.SpeciesMask;
        private static int FormBits(ushort id) => id >> DSPRE.HgEngine.HgEngineTrainerSource.FormShift;

        // A new species keeps the slot's form; the form box beside it shows and changes that.
        private static ushort WithSpecies(ushort id, int species) => (ushort)(species | (FormBits(id) << DSPRE.HgEngine.HgEngineTrainerSource.FormShift));
        private static ushort WithForm(ushort id, int form) => (ushort)(BaseSpecies(id) | (Math.Clamp(form, 0, 31) << DSPRE.HgEngine.HgEngineTrainerSource.FormShift));

        /// <summary>One encounter of one time of day. Only the edited field is written, so a value the boxes can't show survives other edits.</summary>
        public sealed class EncounterCell : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            private readonly SafariZoneGroupViewModel _owner;
            private readonly SafariZoneEncounter _e;

            public EncounterCell(SafariZoneGroupViewModel owner, SafariZoneEncounter e) { _owner = owner; _e = e; }

            public bool Exists => _e != null;
            public bool ShowForms => _owner.ShowForms;
            public ObservableCollection<string> SpeciesNames => _owner.SpeciesNames;
            public int Species => _e == null ? 0 : BaseSpecies(_e.PokemonID);
            public int SpeciesIndex
            {
                get => _e != null && Species < SpeciesNames.Count ? Species : -1;
                set
                {
                    if (_e == null || value < 0 || _owner._suppress || value == Species) return;
                    _e.PokemonID = WithSpecies(_e.PokemonID, value);
                    Raise(); Raise(nameof(Species)); _owner.Touch();
                }
            }
            public decimal Level
            {
                get => _e == null ? 0 : Math.Min((int)_e.Level, MaxShownLevel);
                set
                {
                    if (_e == null || _owner._suppress || (int)value == Math.Min((int)_e.Level, MaxShownLevel)) return;
                    _e.Level = (byte)Math.Clamp((int)value, 0, 255);
                    Raise(); _owner.Touch();
                }
            }
            public decimal Form
            {
                get => _e == null ? 0 : FormBits(_e.PokemonID);
                set
                {
                    if (_e == null || _owner._suppress || (int)value == FormBits(_e.PokemonID)) return;
                    _e.PokemonID = WithForm(_e.PokemonID, (int)value);
                    Raise(); _owner.Touch();
                }
            }
        }

        private static SafariZoneEncounter At(BindingList<SafariZoneEncounter> list, int i) => list != null && i < list.Count ? list[i] : null;

        /// <summary>One normal slot: the same slot number at morning, day and night.</summary>
        public class SlotRow
        {
            public SlotRow(SafariZoneGroupViewModel owner, int index, BindingList<SafariZoneEncounter> morning, BindingList<SafariZoneEncounter> day, BindingList<SafariZoneEncounter> night)
            {
                Label = (index + 1).ToString();
                Morning = new EncounterCell(owner, At(morning, index));
                Day = new EncounterCell(owner, At(day, index));
                Night = new EncounterCell(owner, At(night, index));
            }

            public string Label { get; }
            public EncounterCell Morning { get; }
            public EncounterCell Day { get; }
            public EncounterCell Night { get; }
        }

        /// <summary>One bonus slot, with the objects that unlock it.</summary>
        public sealed class ObjectRow : SlotRow, INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            private readonly SafariZoneGroupViewModel _owner;
            private readonly SafariZoneObjectRequirement _req;
            private readonly SafariZoneObjectRequirement _opt;

            public ObjectRow(SafariZoneGroupViewModel owner, int index)
                : base(owner, index, owner._group.MorningEncountersObject, owner._group.DayEncountersObject, owner._group.NightEncountersObject)
            {
                _owner = owner;
                _req = owner._group.ObjectRequirements[index];
                _opt = owner._group.OptionalObjectRequirements[index];
            }

            public ObservableCollection<string> ObjectTypeNames => _owner.ObjectTypeNames;
            public bool RequirementTypeWarning => _req.TypeID == 0;

            public int ReqType
            {
                get => _req.TypeID;
                set
                {
                    if (value < 0 || _owner._suppress || value == _req.TypeID) return;
                    _req.TypeID = (byte)value;
                    Raise(); Raise(nameof(RequirementTypeWarning)); _owner.Touch();
                }
            }
            public decimal ReqPoints
            {
                get => _req.Quantity;
                set { if (_owner._suppress || (int)value == _req.Quantity) return; _req.Quantity = (byte)Math.Clamp((int)value, 0, 255); Raise(); _owner.Touch(); }
            }
            public int OptReqType
            {
                get => _opt.TypeID;
                set { if (value < 0 || _owner._suppress || value == _opt.TypeID) return; _opt.TypeID = (byte)value; Raise(); _owner.Touch(); }
            }
            public decimal OptReqPoints
            {
                get => _opt.Quantity;
                set { if (_owner._suppress || (int)value == _opt.Quantity) return; _opt.Quantity = (byte)Math.Clamp((int)value, 0, 255); Raise(); _owner.Touch(); }
            }
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
            Touch();
        }
    }
}
