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
    /// <summary>What HGSS followers say and do when talked to.</summary>
    public class FollowerTalkEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly Data.PendingArchiveWrites _pending;
        private readonly Data.PendingArchiveWrites.Target _text;
        private readonly string[] _placeNames = Array.Empty<string>();
        private readonly string[] _speciesNames = Array.Empty<string>();

        public sealed record Choice(int Value, string Name)
        {
            public override string ToString() => Name;
        }

        // Everything is read once and edited here; Save writes the members that changed.
        private List<List<Slot>> _files = new List<List<Slot>>();
        private List<FollowerTalk.ReactionData> _reactions = new List<FollowerTalk.ReactionData>();
        private List<FollowerTalk.MoveData> _moves = new List<FollowerTalk.MoveData>();
        private byte[] _groups = new byte[FollowerTalk.SpeciesCount];
        private List<string> _lines = new List<string>();

        public FollowerTalkEditorViewModel() { }

        public FollowerTalkEditorViewModel(bool load)
        {
            if (!load) return;
            _pending = new Data.PendingArchiveWrites();
            _text = new Data.PendingArchiveWrites.Target("follower-talk-text",
                () => Pack(new TextArchive(FollowerTalkTextArchive).Messages),
                b => new TextArchive(FollowerTalkTextArchive, Unpack(b)).SaveToExpandedDir(FollowerTalkTextArchive, false, this));
            _placeNames = GetLocationNames().ToArray();
            _speciesNames = GetPokemonNames();
            Load();

            Places.Add(new Choice(-1, "General only"));
            for (int p = 0; p + 1 < _files.Count; p++) Places.Add(new Choice(p, PlaceName(p)));
            for (int s = 1; s <= FollowerTalk.SpeciesCount; s++) SpeciesRows.Add(new SpeciesRow(this, s));
            _allReactions = Enumerable.Range(1, _reactions.Count).Select(n => new ReactionRow(this, n)).ToList();
            FilterReactions();
            BuildMoveRows();

            _pending.Changed += OnPendingChanged;
            TextArchive.Saved += OnTextSaved;
            SelectedPlace = Places.Skip(1).FirstOrDefault(p => _files[FollowerTalk.MemberOfPlace(p.Value)].Any(s => !s.IsEmpty)) ?? Places[0];
            SelectedReactionRow = _allReactions.FirstOrDefault();
            SelectedMoveRow = MoveRows.FirstOrDefault();
        }

        private void Load()
        {
            _files = Enumerable.Range(0, _pending.Count(DirNames.FollowerTalkTriggers))
                .Select(m => FollowerTalk.ReadSlots(_pending.ReadMember(DirNames.FollowerTalkTriggers, m) ?? Array.Empty<byte>())).ToList();
            _reactions = Enumerable.Range(0, _pending.Count(DirNames.FollowerTalkReactions))
                .Select(m => _pending.ReadMember(DirNames.FollowerTalkReactions, m))
                .Select(b => new FollowerTalk.ReactionData(b?.Length == FollowerTalk.ReactionSize ? b : new byte[FollowerTalk.ReactionSize])).ToList();
            _moves = Enumerable.Range(0, _pending.Count(DirNames.FollowerTalkMoves))
                .Select(m => _pending.ReadMember(DirNames.FollowerTalkMoves, m))
                .Select(b => b?.Length == FollowerTalk.MoveSize ? new FollowerTalk.MoveData(b) : FollowerTalk.MoveData.New()).ToList();
            byte[] g = _pending.ReadMember(DirNames.FollowerSpeciesGroups, 0) ?? Array.Empty<byte>();
            _groups = g.Length >= FollowerTalk.SpeciesCount ? g : g.Concat(new byte[FollowerTalk.SpeciesCount - g.Length]).ToArray();
            _lines = Unpack(_pending.Read(_text));
            _uses = null;
        }

        // ── Staging ───────────────────────────────────────────────────────────

        // Set while this editor stages an edit it already holds in memory.
        private bool _own;

        private void Stage(DirNames dir, int id, byte[] bytes, object group)
        {
            _own = true;
            try { _pending.Change(s => s.Put(dir, id, bytes), group); }
            finally { _own = false; }
            _uses = null;
            AfterEdit();
        }

        private void StageLines()
        {
            _own = true;
            try { _pending.Import(() => { _pending.Stage(_text, Pack(_lines)); return null; }); }
            finally { _own = false; }
            AfterEdit();
        }

        private void AfterEdit()
        {
            foreach (string n in new[] { nameof(HasUnsavedChanges), nameof(CanUndo), nameof(CanRedo) }) Raise(n);
            Edited?.Invoke();
        }

        internal void EditSlot(int member, int index, Action<Slot> edit, object group)
        {
            if (member < 0 || member >= _files.Count || index < 0 || index >= _files[member].Count) return;
            edit(_files[member][index]);
            Stage(DirNames.FollowerTalkTriggers, member, FollowerTalk.ToBytes(_files[member]), group);
            Slots.FirstOrDefault(r => r.Member == member && r.Index == index)?.Refresh();
            SlotEditor?.Refresh();
            RaiseSlotState();
        }

        internal void EditSlots(int member, Action<List<Slot>> edit)
        {
            edit(_files[member]);
            Stage(DirNames.FollowerTalkTriggers, member, FollowerTalk.ToBytes(_files[member]), null);
            foreach (SlotRow r in Slots) if (r.Member == member) r.Refresh();
        }

        internal void EditReaction(int n, Action<FollowerTalk.ReactionData> edit, object group = null)
        {
            FollowerTalk.ReactionData r = Reaction(n);
            if (r == null) return;
            edit(r);
            Stage(DirNames.FollowerTalkReactions, n - 1, r.Raw, group);
            _allReactions[n - 1].Refresh();
            if (ReactionEditor?.Number == n) ReactionEditor.Refresh();
            foreach (SlotRow s in Slots) if (s.Data?.Reaction == n) s.Refresh();
        }

        internal void EditMove(int n, Action<FollowerTalk.MoveData> edit, object group = null)
        {
            FollowerTalk.MoveData m = Move(n);
            if (m == null) return;
            edit(m);
            Stage(DirNames.FollowerTalkMoves, n - 1, m.Raw, group);
            MoveRows[n - 1].Refresh();
            if (MoveEditor?.Number == n) MoveEditor.Refresh();
        }

        internal void SetGroup(int species, int group)
        {
            if (species < 1 || species > _groups.Length || _groups[species - 1] == group) return;
            _groups[species - 1] = (byte)group;
            Stage(DirNames.FollowerSpeciesGroups, 0, (byte[])_groups.Clone(), "group" + species);
        }

        // ── Text ────────────────────────────────────────────────────────────────

        private static byte[] Pack(List<string> lines) => Encoding.UTF8.GetBytes(string.Join("\0", lines));
        private static List<string> Unpack(byte[] b) => Encoding.UTF8.GetString(b ?? Array.Empty<byte>()).Split('\0').ToList();
        public int LineCount => _lines.Count;

        public string LineText(int n) => n >= 1 && n <= _lines.Count ? _lines[n - 1] : "";

        internal void SetLine(int n, string text)
        {
            if (n < 1 || n > _lines.Count || _lines[n - 1] == text) return;
            _lines[n - 1] = text;
            StageLines();
            foreach (ReactionRow r in _allReactions) r.Refresh();
            foreach (SlotRow s in Slots) s.Refresh();
        }

        internal int AddLine(string text)
        {
            _lines.Add(text);
            StageLines();
            return _lines.Count;
        }

        private static readonly string[] Vars = { "nickname", "species", "player", "place", "held item" };
        private static readonly Regex VarCode = new Regex(@"\{STRVAR_1, \d+, (\d+), \d+\}", RegexOptions.Compiled);
        private static readonly Regex OtherCode = new Regex(@"\{[A-Z_]+[^}]*\}", RegexOptions.Compiled);

        /// <summary>Shows a line on one row, with name codes as words.</summary>
        public string Readable(int n)
        {
            if (n <= 0) return "";
            string s = VarCode.Replace(LineText(n), m => int.TryParse(m.Groups[1].Value, out int k) && k < Vars.Length ? "[" + Vars[k] + "]" : "[?]");
            return OtherCode.Replace(s.Replace("\\n", " ").Replace("\\r", " ").Replace("\\f", " "), "").Trim();
        }

        public string FirstLine(int reaction)
        {
            FollowerTalk.ReactionData r = Reaction(reaction);
            if (r == null) return "";
            for (int i = 0; i < r.Steps; i++) if (r.Line(i) != 0) return Readable(r.Line(i));
            return "(no line)";
        }

        // ── Reading ───────────────────────────────────────────────────────────

        public int ReactionCount => _reactions.Count;
        public int MoveCount => _moves.Count;
        internal FollowerTalk.ReactionData Reaction(int n) => n >= 1 && n <= _reactions.Count ? _reactions[n - 1] : null;
        internal FollowerTalk.MoveData Move(int n) => n >= 1 && n <= _moves.Count ? _moves[n - 1] : null;
        internal Slot SlotAt(int member, int index) => member >= 0 && member < _files.Count && index < _files[member].Count ? _files[member][index] : null;
        public int GroupOf(int species) => species >= 1 && species <= _groups.Length ? _groups[species - 1] : 0;

        // ── Uses, counted once after each change ──────────────────────────────

        private sealed class Uses
        {
            public readonly Dictionary<int, List<string>> Reactions = new Dictionary<int, List<string>>();
            public readonly Dictionary<int, int> Lines = new Dictionary<int, int>();
            public readonly Dictionary<int, int> Moves = new Dictionary<int, int>();
        }

        private Uses _uses;

        private Uses Count()
        {
            if (_uses != null) return _uses;
            Uses u = new Uses();
            void Add(int reaction, string where)
            {
                if (!u.Reactions.TryGetValue(reaction, out List<string> l)) u.Reactions[reaction] = l = new List<string>();
                l.Add(where);
            }
            for (int m = 0; m < _files.Count; m++)
                for (int i = 0; i < _files[m].Count; i++)
                    if (!_files[m][i].IsEmpty) Add(_files[m][i].Reaction, (m == 0 ? "General" : PlaceName(m - 1)) + " " + (i + 1));
            for (int r = 0; r < _reactions.Count; r++)
            {
                FollowerTalk.ReactionData d = _reactions[r];
                if (d.Question)
                {
                    if (d.YesReaction != 0) Add(d.YesReaction, $"Yes in reaction {r + 1}");
                    if (d.NoReaction != 0) Add(d.NoReaction, $"No in reaction {r + 1}");
                }
                for (int i = 0; i < d.Steps; i++)
                {
                    u.Lines[d.Line(i)] = u.Lines.GetValueOrDefault(d.Line(i)) + 1;
                    u.Moves[d.Move(i)] = u.Moves.GetValueOrDefault(d.Move(i)) + 1;
                }
            }
            return _uses = u;
        }

        public List<string> UsersOfReaction(int n) => Count().Reactions.TryGetValue(n, out List<string> l) ? l : new List<string>();
        public int UsersOfLine(int line) => Count().Lines.GetValueOrDefault(line);
        public int UsersOfMove(int move) => Count().Moves.GetValueOrDefault(move);

        /// <summary>Place name filled into lines: the picked place, or the first place when General only is picked.</summary>
        public string CurrentPlaceName => PlaceName(Math.Max(0, _selectedPlace?.Value ?? 0));
        /// <summary>The picked place, or -1 for General only.</summary>
        public int CurrentPlace => _selectedPlace?.Value ?? -1;

        public string PlaceName(int place) => place >= 0 && place < _placeNames.Length ? _placeNames[place] : $"Place {place}";
        public string SpeciesName(int species) => species >= 0 && species < _speciesNames.Length ? _speciesNames[species] : $"Species {species}";

        // ── Places tab ────────────────────────────────────────────────────────

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

        /// <summary>The slots in the order the game tries them: 12 general, the place's 30, the other 58 general.</summary>
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

        /// <summary>Swaps the slot with its neighbour in the same file; the game tries them in file order.</summary>
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

        // ── Reactions tab ─────────────────────────────────────────────────────

        public ObservableCollection<ReactionRow> ReactionRows { get; } = new ObservableCollection<ReactionRow>();
        private List<ReactionRow> _allReactions = new List<ReactionRow>();

        private string _reactionFilter = "";
        public string ReactionFilter
        {
            get => _reactionFilter;
            set { _reactionFilter = value ?? ""; Raise(); FilterReactions(); }
        }

        private void FilterReactions()
        {
            string q = _reactionFilter.Trim();
            List<ReactionRow> shown = q.Length == 0 ? _allReactions
                : _allReactions.Where(r => r.Number.ToString().StartsWith(q) || r.Line.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
            ReactionRows.Clear();
            foreach (ReactionRow r in shown) ReactionRows.Add(r);
        }

        private ReactionRow _selectedReactionRow;
        public ReactionRow SelectedReactionRow
        {
            get => _selectedReactionRow;
            set
            {
                if (value == null || ReferenceEquals(_selectedReactionRow, value)) return;
                _selectedReactionRow = value;
                Raise();
                ReactionEditor = new ReactionEditorVM(this, value.Number);
                Raise(nameof(ReactionEditor));
                Raise(nameof(HasReaction));
                ReactionSelected?.Invoke(value.Number);
            }
        }

        public ReactionEditorVM ReactionEditor { get; private set; }
        public bool HasReaction => ReactionEditor != null;
        public event Action<int> ReactionSelected;

        /// <summary>Opens a reaction on the Reactions tab, clearing a filter that hides it.</summary>
        public void ShowReaction(int n)
        {
            ReactionRow row = n >= 1 && n <= _allReactions.Count ? _allReactions[n - 1] : null;
            if (row == null) return;
            if (!ReactionRows.Contains(row)) ReactionFilter = "";
            SelectedReactionRow = row;
            SelectedTab = 1;
        }

        private int _selectedTab;
        public int SelectedTab
        {
            get => _selectedTab;
            set { _selectedTab = value; Raise(); Raise(nameof(OnPlaces)); Raise(nameof(OnMoves)); Raise(nameof(OnGroups)); RaiseSlotState(); }
        }
        public bool OnPlaces => _selectedTab == 0;
        public bool OnMoves => _selectedTab == 2;
        public bool OnGroups => _selectedTab == 3;

        public sealed class ReactionRow : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly FollowerTalkEditorViewModel _o;
            public int Number { get; }
            public ReactionRow(FollowerTalkEditorViewModel o, int n) { _o = o; Number = n; }
            private string _line;
            public string Line => _line ??= _o.FirstLine(Number);
            public override string ToString() => Number + " " + Line;
            internal void Refresh() { _line = null; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Line))); }
        }

        public static readonly List<Choice> BubbleChoices = new List<Choice>
        {
            new(0, "None"), new(1, "Heart"), new(2, "Happy"), new(3, "Angry"), new(4, "Sad"), new(5, "Smile"), new(6, "Grumpy"),
            new(7, "Note"), new(8, "Question"), new(9, "Exclamation"), new(10, "Sweat"), new(11, "Troubled"), new(12, "Poison"),
            new(13, "Silence"), new(14, "Sleepy"),
        };
        public static readonly List<Choice> SoundChoices = new List<Choice> { new(0, "None"), new(1, "Cry"), new(2, "Weak cry"), new(3, "Sound effect") };
        // 0 keeps the current facing; a movement starts facing the player.
        public static readonly List<Choice> FacingChoices = new List<Choice> { new(0, "Player"), new(1, "Up"), new(2, "Down"), new(3, "Left"), new(4, "Right") };

        private List<Choice> _moveChoices;
        internal List<Choice> MoveChoices => _moveChoices ??= new List<Choice> { new(0, "None") }
            .Concat(Enumerable.Range(1, MoveCount).Select(k => new Choice(k, $"Movement {k}"))).ToList();

        public sealed class ReactionEditorVM : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            private readonly FollowerTalkEditorViewModel _o;
            public int Number { get; }
            public ReactionEditorVM(FollowerTalkEditorViewModel o, int n) { _o = o; Number = n; BuildSteps(); }

            internal FollowerTalk.ReactionData Data => _o.Reaction(Number);
            internal FollowerTalkEditorViewModel Owner => _o;
            internal void Edit(Action<FollowerTalk.ReactionData> edit, object group = null) => _o.EditReaction(Number, edit, group);

            public string Title => $"Reaction {Number}";
            public string Users
            {
                get
                {
                    List<string> u = _o.UsersOfReaction(Number);
                    return u.Count == 0 ? "Unused, free to reuse" : "Used by " + string.Join(", ", u.Take(6)) + (u.Count > 6 ? $" and {u.Count - 6} more" : "");
                }
            }

            public ObservableCollection<StepVM> Steps { get; } = new ObservableCollection<StepVM>();
            internal void BuildSteps()
            {
                Steps.Clear();
                FollowerTalk.ReactionData d = Data;
                if (d == null) return;
                for (int i = 0; i < d.Steps; i++) Steps.Add(new StepVM(this, i, d.Steps));
                Raise(nameof(CanAddStep));
            }
            public bool CanAddStep => (Data?.Steps ?? FollowerTalk.StepCount) < FollowerTalk.StepCount;
            public void AddStep() => Edit(d => d.AddStep());

            public bool Question { get => Data?.Question == true; set => Edit(d => d.Question = value); }
            public decimal Yes { get => Data?.YesReaction ?? 0; set => Edit(d => d.YesReaction = (int)Math.Clamp(value, 0, FollowerTalk.ReactionLimit), "yes"); }
            public decimal No { get => Data?.NoReaction ?? 0; set => Edit(d => d.NoReaction = (int)Math.Clamp(value, 0, FollowerTalk.ReactionLimit), "no"); }
            public decimal Friendship { get => Data?.FriendshipChange ?? 0; set => Edit(d => d.FriendshipChange = (int)Math.Clamp(value, -128, 127), "friend"); }
            public decimal Mood { get => Data?.MoodChange ?? 0; set => Edit(d => d.MoodChange = (int)Math.Clamp(value, -128, 127), "mood"); }

            public static List<Choice> GiftChoices { get; } = new List<Choice> { new(0, "None") }
                .Concat(Enumerable.Range(1, 5).Select(k => new Choice(1000 + k, $"Shiny Leaf {k}")))
                .Concat(Enumerable.Range(1, 100).Select(k => new Choice(k, $"Accessory {k}"))).ToList();
            public List<Choice> Gifts => GiftChoices;
            public Choice Gift
            {
                get
                {
                    FollowerTalk.ReactionData d = Data;
                    int v = d == null ? 0 : d.Accessory != 0 ? d.Accessory : d.Leaf != 0 ? 1000 + d.Leaf : 0;
                    return GiftChoices.FirstOrDefault(c => c.Value == v) ?? GiftChoices[0];
                }
                set
                {
                    if (value == null) return;
                    Edit(d => { d.Accessory = value.Value is > 0 and < 1000 ? value.Value : 0; d.Leaf = value.Value > 1000 ? value.Value - 1000 : 0; });
                }
            }

            internal void Refresh()
            {
                FollowerTalk.ReactionData d = Data;
                if (d != null && d.Steps != Steps.Count) BuildSteps();
                else
                {
                    // A swap keeps the count, so each step reads its slot again.
                    foreach (StepVM s in Steps) s.Refresh();
                }
                foreach (string n in new[] { nameof(Question), nameof(Yes), nameof(No), nameof(Friendship), nameof(Mood), nameof(Gift), nameof(Users), nameof(CanAddStep) }) Raise(n);
            }
        }

        public sealed class StepVM : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly ReactionEditorVM _r;
            public int Index { get; }
            private readonly int _count;
            public StepVM(ReactionEditorVM r, int index, int count) { _r = r; Index = index; _count = count; }
            private FollowerTalk.ReactionData D => _r.Data;
            private void Edit(Action<FollowerTalk.ReactionData> e, object group = null) => _r.Edit(e, group);

            public string Title => $"Step {Index + 1}";
            public bool CanMoveUp => Index > 0;
            public bool CanMoveDown => Index < _count - 1;
            public void MoveUp() => Edit(d => d.SwapSteps(Index, Index - 1));
            public void MoveDown() => Edit(d => d.SwapSteps(Index, Index + 1));
            public void Remove() => Edit(d => d.RemoveStep(Index));

            public List<Choice> MoveChoices => _r.Owner.MoveChoices;
            public Choice Movement
            {
                get => MoveChoices.ElementAtOrDefault(D?.Move(Index) ?? 0) ?? MoveChoices[0];
                set { if (value != null) Edit(d => d.SetMove(Index, value.Value)); }
            }
            public List<Choice> Bubbles => BubbleChoices;
            public Choice Bubble
            {
                get => BubbleChoices.ElementAtOrDefault(D?.Bubble(Index) ?? 0) ?? BubbleChoices[0];
                set { if (value != null) Edit(d => d.SetBubble(Index, value.Value)); }
            }
            public List<Choice> Sounds => SoundChoices;
            private int SoundValue => D?.Sound(Index) ?? 0;
            public Choice SoundKind
            {
                get => SoundChoices[SoundValue == 0 ? 0 : SoundValue == FollowerTalk.Cry ? 1 : SoundValue == FollowerTalk.WeakCry ? 2 : 3];
                set { if (value != null) Edit(d => d.SetSound(Index, value.Value switch { 1 => FollowerTalk.Cry, 2 => FollowerTalk.WeakCry, 3 => 1, _ => 0 })); }
            }
            public bool IsEffect => SoundKind.Value == 3;
            public decimal Effect { get => SoundValue; set => Edit(d => d.SetSound(Index, (int)Math.Clamp(value, 1, FollowerTalk.Cry - 1)), "se" + Index); }
            public decimal Wait { get => D?.Wait(Index) ?? 0; set => Edit(d => d.SetWait(Index, (int)Math.Clamp(value, 0, 255)), "wait" + Index); }
            public decimal Line { get => D?.Line(Index) ?? 0; set => Edit(d => d.SetLine(Index, (int)Math.Clamp(value, 0, _r.Owner.LineCount)), "line" + Index); }
            public decimal MaxLine => _r.Owner.LineCount;
            public bool HasLine => (D?.Line(Index) ?? 0) != 0;
            public string Text
            {
                get => HasLine ? _r.Owner.LineText(D.Line(Index)).Replace("\\n", "\n") : "";
                set { if (HasLine) _r.Owner.SetLine(D.Line(Index), (value ?? "").Replace("\r\n", "\n").Replace("\n", "\\n")); }
            }
            public void NewLine()
            {
                int n = _r.Owner.AddLine("{STRVAR_1, 1, 0, 0} is looking at you.");
                if (n > 0) Edit(d => d.SetLine(Index, n));
            }
            public string Note
            {
                get
                {
                    FollowerTalk.ReactionData d = D;
                    if (d == null) return "";
                    List<string> notes = new List<string>();
                    if (HasLine) notes.Add($"Line {d.Line(Index)} is used by {Plural.Of(_r.Owner.UsersOfLine(d.Line(Index)), "step")}");
                    if (d.Sound(Index) != 0)
                    {
                        FollowerTalk.MoveData m = _r.Owner.Move(d.Move(Index));
                        bool plays = m != null && Enumerable.Range(0, m.Frames).Any(m.PlaysSound);
                        if (!plays) notes.Add("The sound won't play: no frame in the movement has Sound ticked");
                    }
                    return string.Join(". ", notes);
                }
            }
            internal void Refresh()
            {
                foreach (string n in new[] { nameof(Movement), nameof(Bubble), nameof(SoundKind), nameof(IsEffect), nameof(Effect), nameof(Wait), nameof(Line), nameof(HasLine), nameof(Text), nameof(Note), nameof(MaxLine) })
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            }
        }

        // ── Movements tab ─────────────────────────────────────────────────────

        public ObservableCollection<MoveRow> MoveRows { get; } = new ObservableCollection<MoveRow>();
        private void BuildMoveRows()
        {
            MoveRows.Clear();
            for (int n = 1; n <= MoveCount; n++) MoveRows.Add(new MoveRow(this, n));
            _moveChoices = null;
        }

        private MoveRow _selectedMoveRow;
        public MoveRow SelectedMoveRow
        {
            get => _selectedMoveRow;
            set
            {
                if (value == null || ReferenceEquals(_selectedMoveRow, value)) return;
                _selectedMoveRow = value;
                Raise();
                MoveEditor = new MoveEditorVM(this, value.Number);
                Raise(nameof(MoveEditor));
                MoveSelected?.Invoke(value.Number);
            }
        }
        public MoveEditorVM MoveEditor { get; private set; }
        public event Action<int> MoveSelected;

        public void NewMove()
        {
            FollowerTalk.MoveData m = FollowerTalk.MoveData.New();
            m.AddFrame();
            _moves.Add(m);
            Stage(DirNames.FollowerTalkMoves, _moves.Count - 1, m.Raw, null);
            BuildMoveRows();
            SelectedMoveRow = MoveRows.LastOrDefault();
        }

        public sealed class MoveRow : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly FollowerTalkEditorViewModel _o;
            public int Number { get; }
            public MoveRow(FollowerTalkEditorViewModel o, int n) { _o = o; Number = n; }
            public string Label => $"Movement {Number}";
            public override string ToString() => Label;
            public string Detail
            {
                get
                {
                    int frames = _o.Move(Number)?.Frames ?? 0, used = _o.UsersOfMove(Number);
                    return Plural.Of(frames, "frame") + " · " + (used == 0 ? "unused" : "used by " + Plural.Of(used, "step"));
                }
            }
            internal void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Detail)));
        }

        public sealed class MoveEditorVM : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly FollowerTalkEditorViewModel _o;
            public int Number { get; }
            public MoveEditorVM(FollowerTalkEditorViewModel o, int n) { _o = o; Number = n; Build(); }
            internal FollowerTalk.MoveData Data => _o.Move(Number);
            internal void Edit(Action<FollowerTalk.MoveData> e, object group = null) => _o.EditMove(Number, e, group);
            public string Title => $"Movement {Number}";
            public ObservableCollection<FrameVM> Frames { get; } = new ObservableCollection<FrameVM>();
            private void Build()
            {
                Frames.Clear();
                FollowerTalk.MoveData m = Data;
                if (m == null) return;
                for (int f = 0; f < m.Frames; f++) Frames.Add(new FrameVM(this, f));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanAddFrame)));
            }
            public bool CanAddFrame => (Data?.Frames ?? FollowerTalk.FrameCount) < FollowerTalk.FrameCount;
            public void AddFrame() => Edit(m => m.AddFrame());
            internal void Refresh()
            {
                if ((Data?.Frames ?? 0) != Frames.Count) Build();
                else foreach (FrameVM f in Frames) f.Refresh();
            }
        }

        public sealed class FrameVM : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly MoveEditorVM _m;
            public int Index { get; }
            public FrameVM(MoveEditorVM m, int i) { _m = m; Index = i; }
            private FollowerTalk.MoveData D => _m.Data;
            private void Edit(Action<FollowerTalk.MoveData> e, string what) => _m.Edit(e, what + Index);
            public string Title => (Index + 1).ToString();
            public List<Choice> Facings => FacingChoices;
            public Choice Facing
            {
                get => FacingChoices.ElementAtOrDefault(D?.Facing(Index) ?? 0) ?? FacingChoices[0];
                set { if (value != null) _m.Edit(d => d.SetFacing(Index, value.Value)); }
            }
            public decimal Length { get => D?.Length(Index) ?? 0; set => Edit(d => d.SetLength(Index, (int)Math.Clamp(value, 0, 254)), "len"); }
            public decimal X { get => D?.X(Index) ?? 0; set => Edit(d => d.SetX(Index, (int)Math.Clamp(value, -128, 127)), "x"); }
            public decimal Hop { get => D?.Hop(Index) ?? 0; set => Edit(d => d.SetHop(Index, (int)Math.Clamp(value, -128, 127)), "hop"); }
            public decimal Z { get => D?.Z(Index) ?? 0; set => Edit(d => d.SetZ(Index, (int)Math.Clamp(value, -128, 127)), "z"); }
            public bool Sound { get => D?.PlaysSound(Index) == true; set => _m.Edit(d => d.SetPlaysSound(Index, value)); }
            public void Remove() => _m.Edit(d => d.RemoveFrame(Index));
            internal void Refresh()
            {
                foreach (string n in new[] { nameof(Facing), nameof(Length), nameof(X), nameof(Hop), nameof(Z), nameof(Sound) })
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            }
        }

        // ── Species groups tab ────────────────────────────────────────────────

        public ObservableCollection<SpeciesRow> SpeciesRows { get; } = new ObservableCollection<SpeciesRow>();

        public sealed class SpeciesRow : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly FollowerTalkEditorViewModel _o;
            public int Species { get; }
            public SpeciesRow(FollowerTalkEditorViewModel o, int species) { _o = o; Species = species; }
            public string Name => $"{Species:D3} {_o.SpeciesName(Species)}";
            public override string ToString() => Name;
            public decimal Group { get => _o.GroupOf(Species); set => _o.SetGroup(Species, (int)Math.Clamp(value, 0, 255)); }
            internal void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Group)));
        }

        // ── Undo, redo, discard and saves elsewhere ──────────────────────────

        // Only undo, redo, discard or another editor's save get here: this editor's own edits are already in memory.
        private void OnPendingChanged()
        {
            if (_own) return;
            Load();
            foreach (SlotRow r in Slots) r.Refresh();
            SlotEditor?.Refresh();
            foreach (ReactionRow r in _allReactions) r.Refresh();
            ReactionEditor?.Refresh();
            if (MoveRows.Count != MoveCount) BuildMoveRows();
            else foreach (MoveRow r in MoveRows) r.Refresh();
            MoveEditor?.Refresh();
            foreach (SpeciesRow r in SpeciesRows) r.Refresh();
            RaiseSlotState();
            AfterEdit();
        }

        // The Text editor saved this bank: show its lines unless this editor has its own edits to them.
        private void OnTextSaved(object sender, int id)
        {
            if (id != FollowerTalkTextArchive || ReferenceEquals(sender, this)) return;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(OnPendingChanged);
        }

        /// <summary>Stops listening for other editors' saves when the window closes.</summary>
        public void Detach() => TextArchive.Saved -= OnTextSaved;

        /// <summary>Raised after any change, undo or redo, so the preview can redraw.</summary>
        public event Action Edited;

        public bool CanUndo => _pending?.CanUndo == true;
        public bool CanRedo => _pending?.CanRedo == true;
        public void Undo() => _pending?.Undo();
        public void Redo() => _pending?.Redo();

        public bool HasUnsavedChanges => _pending?.IsDirty == true;
        public string UnsavedChangesDescription => "Follower talk";

        public void SaveChanges() => DialogHelper.Watch(SaveChangesAsync());

        public async Task<bool> SaveChangesAsync()
        {
            if (_pending == null) return true;
            try { _pending.Save(); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                await DialogHelper.ShowErrorAsync("Follower talk wasn't saved:\n" + e.Message, "Follower talk");
                return false;
            }
            AfterEdit();
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public void DiscardChanges() => _pending?.Discard();

        // ── Conditions ────────────────────────────────────────────────────────

        /// <summary>Slot conditions and what each value means in game.</summary>
        public static class Conditions
        {
            public sealed record Field(string Label, Func<Slot, int> Get, Action<Slot, int> Set, int Max,
                Dictionary<int, string> Names, Func<int, bool> Never = null, string Help = null, Func<Dictionary<int, string>> FromRom = null)
            {
                /// <summary>The named values, read from the ROM for maps and tiles.</summary>
                public Dictionary<int, string> NamesNow() => FromRom?.Invoke() ?? Names;
            }

            // Built once per project; every slot row reads them.
            private static Dictionary<int, string> _mapNames, _tileNames;
            private static string _namesFor;

            private static void ReadNames()
            {
                if (_namesFor == WorkDir && _mapNames != null) return;
                _namesFor = WorkDir;
                _mapNames = new Dictionary<int, string>();
                IReadOnlyList<string> headers = HeaderLabels.Friendly();
                for (int h = 0; h < headers.Count; h++) _mapNames[h + 1] = $"{h}: {headers[h]}";
                _tileNames = new Dictionary<int, string>();
                for (int v = 1; v < 256; v++) _tileNames[v] = $"{v:X2} {TilePermissions.BehaviourName((byte)v, GameFamily)}";
            }

            private static Dictionary<int, string> MapNames() { ReadNames(); return _mapNames; }
            private static Dictionary<int, string> TileNames() { ReadNames(); return _tileNames; }

            private static Dictionary<int, string> Named(params string[] names)
            {
                Dictionary<int, string> d = new Dictionary<int, string>();
                for (int i = 0; i < names.Length; i++) d[i + 1] = names[i];
                return d;
            }

            public static readonly string[] Types = { "Normal", "Fire", "Water", "Electric", "Grass", "Ice", "Fighting", "Poison", "Ground", "Flying", "Psychic", "Bug", "Rock", "Ghost", "Dragon", "Dark", "Steel" };

            public static readonly List<Field> All = new List<Field>
            {
                new("HP", s => s.Hp, (s, v) => s.Hp = v, 255, Named("Full", "75% or more", "50% or more", "25% or more", "Below 25%"), v => v > 5,
                    "The follower's HP against its max."),
                new("Friendship", s => s.Friendship, (s, v) => s.Friendship = v, 15, Named("255", "200 to 254", "150 to 199", "90 to 149", "60 to 89", "30 to 59", "1 to 29", "0", "90 or more", "Below 60"), v => v > 10,
                    "The follower's friendship, out of 255."),
                new("Mood", s => s.Mood, (s, v) => s.Mood = v, 15, Named("127", "100 to 126", "50 to 99", "30 to 49", "-29 to 29", "-49 to -30", "-126 to -50", "-127", "0 or more", "Below 0"), v => v > 10,
                    "A hidden mood from -127 to 127 that only counts while the Pokémon follows you. Reactions raise and lower it."),
                new("Status", s => s.Status, (s, v) => s.Status = v, 7, Named("Healthy", "Burned", "Frozen", "Paralysed", "Poisoned", "Fainted", "Any problem"), v => v == 6,
                    "The follower's status problem. A sleeping follower only matches Any problem, and Fainted never matches because a fainted Pokémon can't follow you."),
                new("Held item", s => s.HeldItem, (s, v) => s.HeldItem = v, 31, Named("Ball", "Medicine", "Battle item", "Item", "Mail", "Berry", "TM", "Nothing", "Anything"), v => v > 9,
                    "The bag pocket of the follower's held item. A held key item matches both Nothing and Anything."),
                new("Type", s => s.Type, (s, v) => s.Type = v, 31, Named(Types), v => v > 17,
                    "Matches if either of the follower's types is this one."),
                new("Nature group", s => s.Nature, (s, v) => s.Nature = v, 7, Named("Cheerful", "Calm", "Careful", "Bold", "Lonely", "Easygoing"), v => v > 6,
                    "The game sorts natures into six groups. Cheerful: Naughty, Impish, Jolly, Naive, Sassy. Calm: Relaxed, Lax, Calm, Gentle. Careful: Docile, Serious, Modest, Quiet, Careful. Bold: Hardy, Brave, Adamant, Bold. Lonely: Lonely, Timid, Bashful. Easygoing: Hasty, Mild, Rash, Quirky."),
                new("Gender", s => s.Gender, (s, v) => s.Gender = v, 3, Named("Male", "Female", "No gender"), v => v == 3,
                    "Genderless followers count as female, so No gender never matches."),
                new("Best Pokéathlon stat", s => s.Pokeathlon, (s, v) => s.Pokeathlon = v, 7, Named("Power", "Speed", "Stamina", "Jump", "Skill"), v => v > 5,
                    "The follower's highest Pokéathlon stat. A tie goes to the first in this order: Power, Speed, Jump, Stamina, Skill."),
                new("Time", s => s.Time, (s, v) => s.Time = v, 7, Named("Morning", "Day", "Evening", "Night", "Late night"), v => v > 5,
                    "The time of day on the game clock."),
                new("Weather", s => s.Weather, (s, v) => s.Weather = v, 7, Named("Clear", "Cloudy", "Rain", "Heavy rain", "Thunder"), v => v != 1 && v != 3,
                    "The weather on the current map. The game only reports clear and rain, so Cloudy, Heavy rain and Thunder never match."),
                new("Facing", s => s.Facing, (s, v) => s.Facing = v, 7, Named("Right", "Left", "Up", "Down"), v => v > 4,
                    "The way the follower faces when you talk to it."),
                new("People next to you", s => s.People, (s, v) => s.People = v, 7, Named("1", "2", "3", "4", "5 or more"), v => v > 5,
                    "People and objects on the eight tiles around the player. The follower, smashable rocks, Strength boulders and cuttable trees don't count."),
                new("Hidden items on the map", s => s.HiddenItems, (s, v) => s.HiddenItems = v, 7, Named("1", "2", "3", "4 or more"), v => v > 4,
                    "Hidden items on the current map that haven't been found yet."),
                new("Encounter tile", s => s.EncounterTile, (s, v) => s.EncounterTile = v, 255, Named("Yes", "No"), v => v > 2,
                    "Whether the follower stands where wild Pokémon can appear, like tall grass."),
                new("Species group", s => s.SpeciesGroup, (s, v) => s.SpeciesGroup = v, 255,
                    new Dictionary<int, string> { [250] = "Groups 0 to 19", [251] = "Groups 0 to 130", [252] = "Groups 140 to 149", [253] = "Groups 160 and up", [254] = "Groups 220 and up" }, null,
                    "The follower's group from the Species groups tab, or a range of groups."),
                new("Shiny Leaf missing", s => s.Leaf, (s, v) => s.Leaf = v, 255, Named("Leaf 1", "Leaf 2", "Leaf 3", "Leaf 4", "Leaf 5"), null,
                    "Matches until the follower has this Shiny Leaf, so a reaction that gives the leaf only plays once."),
                new("Map", s => s.Map, (s, v) => s.Map = v, 65535, new Dictionary<int, string>(), null,
                    "Only on this map.", MapNames),
                new("Tile behaviour", s => s.Tile, (s, v) => s.Tile = v, 65535, new Dictionary<int, string>(), null,
                    "The kind of tile the follower stands on, like shallow water or a puddle.", TileNames),
                new("Event flag set", s => s.Flag, (s, v) => s.Flag = v, 65535, new Dictionary<int, string>(), null,
                    "Only once this flag is set."),
                new("Level", s => s.Level, (s, v) => s.Level = v, 3, new Dictionary<int, string>(), v => v > 0,
                    "Broken in the game: any value stops the slot."),
                new("Field move", s => s.FieldMove, (s, v) => s.FieldMove = v, 3, new Dictionary<int, string>(), v => v > 0,
                    "Broken in the game: any value stops the slot."),
            };

            public static IEnumerable<string> Describe(Slot s)
            {
                foreach (Field f in All)
                {
                    int v = f.Get(s);
                    if (v == 0) continue;
                    yield return f.Label + " " + (f.NamesNow().TryGetValue(v, out string n) ? n : v.ToString());
                }
            }
        }
    }

    /// <summary>What each Follower talk field does in game, for its info button.</summary>
    public static class FollowerTalkHelp
    {
        public const string Chance = "How often this slot gets its turn. When the roll fails, the game moves on to the next slot.";
        public const string Reaction = "What happens when this slot is picked. Slots can share a reaction, so an edit to it shows up in all of them.";
        public const string Movement = "How the follower moves before its bubble and line. It's put back where it stood afterwards.";
        public const string Bubble = "The emote over the follower's head, shown after it moves.";
        public const string Sound = "The follower's cry, its weak cry or a sound effect. It only plays on movement frames with Sound ticked, so a step with no movement stays silent.";
        public const string Wait = "A pause after the line, at 30 frames a second.";
        public const string Line = "The text shown. Name codes become the nickname, species, player, place or held item. 0 shows no text.";
        public const string Question = "Asks Yes or No after the last line. Each answer can lead to another reaction.";
        public const string YesNo = "The reaction that plays after this answer. 0 ends the talk.";
        public const string Friendship = "Added to friendship when the reaction ends.";
        public const string Mood = "Added to mood when the reaction ends.";
        public const string Gift = "Given when the reaction ends. A Shiny Leaf the follower already has isn't given again, and no accessory is given while the fashion case is full.";
        public const string Facing = "The way the follower turns on this frame. Player keeps its current way, which is toward the player until a frame turns it.";
        public const string Length = "How long this frame lasts, at 30 a second.";
        public const string X = "Moves the follower sideways, in pixels (16 to a tile). Moves add up from frame to frame.";
        public const string Hop = "Lifts the follower, in pixels. Lifts add up from frame to frame. Diglett and Dugtrio stay on the ground.";
        public const string Z = "Moves the follower up or down the screen, in pixels (16 to a tile). Moves add up from frame to frame.";
        public const string FrameSound = "Plays the step's sound on this frame.";
        public const string Group = "Slots that ask for this group match this species.";
    }
}
