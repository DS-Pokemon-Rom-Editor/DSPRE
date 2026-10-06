using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>How much friendship each event adds or takes, by how friendly the Pokémon already is.</summary>
    public class FriendshipChangesViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private FriendshipTable _table;
        private byte[] _saved;

        private ByteStateUndo _undo;
        private void StartUndo() => _undo = new ByteStateUndo(() => _table.ToBytes(), b => { CopyValues(b); Changed(); }, () => { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); });
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        private void CopyValues(byte[] bytes)
        {
            FriendshipTable back = new FriendshipTable(bytes);
            for (int e = 0; e < FriendshipTable.Events; e++)
                for (int b = 0; b < FriendshipTable.Bands; b++)
                    _table.Values[e, b] = back.Values[e, b];
            foreach (RowViewModel r in Rows) r.Refresh();
        }

        public string[] BandNames => FriendshipTable.BandNames;
        public ObservableCollection<RowViewModel> Rows { get; } = new ObservableCollection<RowViewModel>();

        public FriendshipChangesViewModel() { }

        public FriendshipChangesViewModel(bool load)
        {
            if (!load) return;
            _table = FriendshipTable.Load();
            _saved = _table.ToBytes();
            for (int e = 0; e < FriendshipTable.Events; e++)
                if (e != 9 || gameFamily != GameFamilies.HGSS)   // HeartGold has no Contests
                    Rows.Add(new RowViewModel(this, e));
            StartUndo();
        }

        public sealed class RowViewModel : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly FriendshipChangesViewModel _o;
            private readonly int _e;
            public RowViewModel(FriendshipChangesViewModel owner, int e) { _o = owner; _e = e; }

            public string Name => FriendshipTable.EventNames[_e] + (FriendshipTable.IsUnused(_e) ? " (unused)" : "");
            public bool Unused => FriendshipTable.IsUnused(_e);
            public decimal Low { get => _o.Get(_e, 0); set => _o.Set(_e, 0, value); }
            public decimal Mid { get => _o.Get(_e, 1); set => _o.Set(_e, 1, value); }
            public decimal High { get => _o.Get(_e, 2); set => _o.Set(_e, 2, value); }

            internal void Refresh()
            {
                foreach (string n in new[] { nameof(Low), nameof(Mid), nameof(High) })
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            }
        }

        internal decimal Get(int e, int b) => _table?.Values[e, b] ?? 0;

        internal void Set(int e, int b, decimal v)
        {
            if (_table == null) return;
            sbyte value = (sbyte)Math.Clamp(v, sbyte.MinValue, sbyte.MaxValue);
            if (_table.Values[e, b] == value) return;
            _table.Values[e, b] = value;
            Changed();
        }

        public string Warning
        {
            get
            {
                if (_table == null) return "";
                List<(int Event, int Band)> risky = _table.Risky().ToList();
                if (risky.Count == 0) return "";
                string where = string.Join(", ", risky.Take(4).Select(r => $"{FriendshipTable.EventNames[r.Event]} ({FriendshipTable.BandNames[r.Band]})"));
                return $"Above +{FriendshipTable.SafeMax}, ball, met-location and Soothe Bell bonuses can overflow into a loss: {where}{(risky.Count > 4 ? "…" : "")}";
            }
        }
        public bool HasWarning => Warning.Length > 0;

        private void Changed()
        {
            foreach (string n in new[] { nameof(Warning), nameof(HasWarning), nameof(HasUnsavedChanges) }) Raise(n);
            _undo?.Record();
        }

        public bool HasUnsavedChanges => _table != null && !_table.ToBytes().AsSpan().SequenceEqual(_saved);
        public string UnsavedChangesDescription => "Friendship changes";

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            if (_table == null) return true;
            try { _table.Save(); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                await DialogHelper.ShowError("The friendship changes were not saved:\n" + e.Message, "Friendship Changes");
                return false;
            }
            _saved = _table.ToBytes();
            Changed();
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public void DiscardChanges()
        {
            if (_table == null) return;
            CopyValues(_saved);
            StartUndo();
            Changed();
        }
    }
}
