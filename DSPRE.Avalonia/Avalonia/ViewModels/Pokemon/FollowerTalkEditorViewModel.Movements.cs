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
        public ObservableCollection<MoveRow> MoveRows { get; } = new ObservableCollection<MoveRow>();
        private void BuildMoveRows()
        {
            MoveRows.Clear();
            for (int n = 1; n <= MoveCount; n++) MoveRows.Add(new MoveRow(this, n));
            _moveChoices = null;
            ReactionEditor?.Refresh();
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
    }
}
