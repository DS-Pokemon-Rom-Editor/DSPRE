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
    public partial class FollowerTalkEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly PendingArchiveWrites _pending;
        private readonly PendingArchiveWrites.Target _text;
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
            _pending = new PendingArchiveWrites();
            _text = PendingArchiveWrites.Text("follower-talk-text", FollowerTalkTextArchive, this);
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
            _lines = PendingArchiveWrites.TextLines(_pending.Read(_text));
            _uses = null;
        }

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
            try { _pending.Import(() => { _pending.Stage(_text, PendingArchiveWrites.TextBytes(_lines)); return null; }); }
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

        public int ReactionCount => _reactions.Count;
        public int MoveCount => _moves.Count;
        internal FollowerTalk.ReactionData Reaction(int n) => n >= 1 && n <= _reactions.Count ? _reactions[n - 1] : null;
        internal FollowerTalk.MoveData Move(int n) => n >= 1 && n <= _moves.Count ? _moves[n - 1] : null;
        internal Slot SlotAt(int member, int index) => member >= 0 && member < _files.Count && index < _files[member].Count ? _files[member][index] : null;
        public int GroupOf(int species) => species >= 1 && species <= _groups.Length ? _groups[species - 1] : 0;

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

        /// <summary>The place name for lines: the picked place, or the first when General only is picked.</summary>
        public string CurrentPlaceName => PlaceName(Math.Max(0, _selectedPlace?.Value ?? 0));
        /// <summary>The picked place, or -1 for General only.</summary>
        public int CurrentPlace => _selectedPlace?.Value ?? -1;

        public string PlaceName(int place) => place >= 0 && place < _placeNames.Length ? _placeNames[place] : $"Place {place}";
        public string SpeciesName(int species) => species >= 0 && species < _speciesNames.Length ? _speciesNames[species] : $"Species {species}";
    }
}
