using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using Slot = DSPRE.ROMFiles.FollowerTalk.Slot;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class FollowerTalkEditorViewModel
    {
        public ObservableCollection<Choice> Places { get; } = new ObservableCollection<Choice>();
        public ObservableCollection<SlotRow> Slots { get; } = new ObservableCollection<SlotRow>();

        private Choice _selectedPlace;
        public Choice SelectedPlace
        {
            get => _selectedPlace;
            set
            {
                if (value == null || ReferenceEquals(_selectedPlace, value)) return;
                _selectedPlace = value;
                Raise();
                BuildSlotRows();
                PlaceChanged?.Invoke();
            }
        }

        /// <summary>Lets the preview show the place's map.</summary>
        public event Action PlaceChanged;

        // Slots in the order the game tries them: 12 general, the place's 30, the other 58 general.
        private void BuildSlotRows()
        {
            int place = _selectedPlace?.Value ?? -1;
            List<SlotRow> rows = new List<SlotRow>();
            int order = 1;
            for (int i = 0; i < FollowerTalk.FirstGeneralSlots; i++) rows.Add(new SlotRow(this, 0, i, order++));
            if (place >= 0) for (int i = 0; i < FollowerTalk.PlaceSlots; i++) rows.Add(new SlotRow(this, FollowerTalk.MemberOfPlace(place), i, order++));
            for (int i = FollowerTalk.FirstGeneralSlots; i < FollowerTalk.GeneralSlots; i++) rows.Add(new SlotRow(this, 0, i, order++));
            Slots.Clear();
            foreach (SlotRow r in rows) Slots.Add(r);
            SelectedSlot = Slots.FirstOrDefault(s => s.Member != 0) ?? Slots.FirstOrDefault();
        }

        private SlotRow _selectedSlot;
        public SlotRow SelectedSlot
        {
            get => _selectedSlot;
            set
            {
                if (ReferenceEquals(_selectedSlot, value)) return;
                _selectedSlot = value;
                Raise();
                SlotEditor = value == null ? null : new SlotEditorVM(this, value);
                Raise(nameof(SlotEditor));
                RaiseSlotState();
                SlotSelected?.Invoke(value);
            }
        }

        public SlotEditorVM SlotEditor { get; private set; }

        /// <summary>Lets the preview play the slot's reaction.</summary>
        public event Action<SlotRow> SlotSelected;

        public bool SlotHasData => OnPlaces && SelectedSlot?.IsEmpty == false;
        public bool SlotIsEmpty => OnPlaces && SelectedSlot?.IsEmpty == true;
        private void RaiseSlotState() { Raise(nameof(SlotHasData)); Raise(nameof(SlotIsEmpty)); }

        private byte[] _clipboard;
        public bool CanPaste => _clipboard != null;

        public void CopySlot()
        {
            if (SelectedSlot?.Data == null) return;
            _clipboard = (byte[])SelectedSlot.Data.Raw.Clone();
            Raise(nameof(CanPaste));
        }

        public void PasteSlot()
        {
            if (SelectedSlot == null || _clipboard == null) return;
            EditSlot(SelectedSlot.Member, SelectedSlot.Index, s => _clipboard.CopyTo(s.Raw, 0), null);
        }

        public void ClearSlot()
        {
            if (SelectedSlot == null) return;
            EditSlot(SelectedSlot.Member, SelectedSlot.Index, s => Array.Clear(s.Raw), null);
        }

        public void FillSlot()
        {
            if (SelectedSlot == null) return;
            EditSlot(SelectedSlot.Member, SelectedSlot.Index, s => { Array.Clear(s.Raw); s.Reaction = 1; s.Chance = 100; }, null);
        }

        /// <summary>Swaps the slot with its file neighbour. The game tries slots in file order.</summary>
        public void MoveSlot(int by)
        {
            SlotRow row = SelectedSlot;
            if (row == null) return;
            int count = row.Member == 0 ? FollowerTalk.GeneralSlots : FollowerTalk.PlaceSlots, to = row.Index + by;
            if (to < 0 || to >= count) return;
            EditSlots(row.Member, s => (s[row.Index], s[to]) = (s[to], s[row.Index]));
            SelectedSlot = Slots.FirstOrDefault(r => r.Member == row.Member && r.Index == to);
        }

        public sealed class SlotRow : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly FollowerTalkEditorViewModel _o;
            public int Member { get; }
            public int Index { get; }
            public int Order { get; }
            public SlotRow(FollowerTalkEditorViewModel o, int member, int index, int order) { _o = o; Member = member; Index = index; Order = order; }

            public Slot Data => _o.SlotAt(Member, Index);
            public string Label => $"{Order}. {(Member == 0 ? "General" : _o.PlaceName(Member - 1))} {Index + 1}";
            public bool IsEmpty => Data?.IsEmpty != false;
            public string Line => IsEmpty ? "Empty" : _o.FirstLine(Data.Reaction);
            public string Chance => IsEmpty ? "" : Data.Chance + "%";
            public string Summary => IsEmpty ? "" : string.Join(" · ", Conditions.Describe(Data));
            public override string ToString() => Label + " " + Line;
            internal void Refresh()
            {
                foreach (string n in new[] { nameof(Line), nameof(Chance), nameof(Summary), nameof(IsEmpty) })
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            }
        }

        public sealed class SlotEditorVM : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly FollowerTalkEditorViewModel _o;
            private readonly SlotRow _row;
            public SlotEditorVM(FollowerTalkEditorViewModel o, SlotRow row)
            {
                _o = o; _row = row;
                foreach (Conditions.Field f in Conditions.All) Fields.Add(new ConditionVM(this, f));
            }

            public string Title => _row.Label;
            public bool IsEmpty => _row.IsEmpty;
            public bool HasData => !IsEmpty;
            public ObservableCollection<ConditionVM> Fields { get; } = new ObservableCollection<ConditionVM>();
            internal Slot Data => _row.Data;
            internal void Edit(Action<Slot> edit, object group) => _o.EditSlot(_row.Member, _row.Index, edit, group);

            public decimal Chance { get => Data?.Chance ?? 0; set => Edit(s => s.Chance = (int)Math.Clamp(value, 0, 100), "chance"); }
            public decimal Reaction { get => Data?.Reaction ?? 0; set => Edit(s => s.Reaction = (int)Math.Clamp(value, 1, FollowerTalk.ReactionLimit), "reaction"); }
            public decimal MaxReaction => Math.Min(FollowerTalk.ReactionLimit, _o.ReactionCount);
            public string ReactionLine => Data == null ? "" : _o.FirstLine(Data.Reaction);

            internal void Refresh()
            {
                foreach (string n in new[] { nameof(Chance), nameof(Reaction), nameof(ReactionLine), nameof(IsEmpty), nameof(HasData) })
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
                foreach (ConditionVM f in Fields) f.Refresh();
            }
        }

        public sealed class ConditionVM : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly SlotEditorVM _s;
            private readonly Conditions.Field _f;
            public ConditionVM(SlotEditorVM s, Conditions.Field f) { _s = s; _f = f; }

            public string Label => _f.Label;
            public string Help => _f.Help;
            public decimal Max => _f.Max;
            private Dictionary<int, string> Names => _f.NamesNow();
            public bool HasNames => Names.Count > 0;

            private List<Choice> _options;
            private int _optionsFor = -1;
            public List<Choice> Options
            {
                get
                {
                    int v = Value;
                    bool listed = v == 0 || Names.ContainsKey(v);
                    if (_options != null && (listed || _optionsFor == v)) return _options;
                    _options = new List<Choice> { new Choice(0, "Any") };
                    _options.AddRange(Names.Select(n => new Choice(n.Key, n.Value)));
                    if (!listed) _options.Add(new Choice(v, $"Value {v}"));
                    _optionsFor = v;
                    return _options;
                }
            }
            private int Value => _s.Data == null ? 0 : _f.Get(_s.Data);
            public Choice Selected
            {
                get => Options.FirstOrDefault(c => c.Value == Value);
                set { if (value != null && value.Value != Value) _s.Edit(d => _f.Set(d, value.Value), null); }
            }
            public decimal Raw
            {
                get => Value;
                set { int v = (int)Math.Clamp(value, 0, _f.Max); if (v != Value) _s.Edit(d => _f.Set(d, v), _f); }
            }
            public string Warning => Value != 0 && _f.Never != null && _f.Never(Value) ? "The game never matches this" : "";
            public bool HasWarning => Warning.Length > 0;
            internal void Refresh()
            {
                foreach (string n in new[] { nameof(Options), nameof(Selected), nameof(Raw), nameof(Warning), nameof(HasWarning) })
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            }
        }
    }
}
