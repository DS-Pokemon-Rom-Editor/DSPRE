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
    }
}
