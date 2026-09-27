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

namespace DSPRE.Avalonia.ViewModels.Items
{
    /// <summary>What the two Battle Point exchange counters sell and for how many points.</summary>
    public class BpShopEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private BpShopData _shop;
        private byte[] _saved;

        private ByteStateUndo _undo;
        private void StartUndo() => _undo = new ByteStateUndo(() => _shop.Snapshot(), b => { _shop.Restore(b); SelectedLeft = SelectedRight = null; Rebuild(); Changed(); }, () => { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); });
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        public string[] ItemNames { get; } = Array.Empty<string>();
        public ObservableCollection<EntryViewModel> Left { get; } = new ObservableCollection<EntryViewModel>();
        public ObservableCollection<EntryViewModel> Right { get; } = new ObservableCollection<EntryViewModel>();

        private EntryViewModel _selectedLeft, _selectedRight;
        public EntryViewModel SelectedLeft { get => _selectedLeft; set { _selectedLeft = value; Raise(); } }
        public EntryViewModel SelectedRight { get => _selectedRight; set { _selectedRight = value; Raise(); } }

        public BpShopEditorViewModel() { }

        public BpShopEditorViewModel(bool load)
        {
            if (!load) return;
            ItemNames = GetItemNames();
            _shop = BpShopData.Load();
            _saved = _shop.Snapshot();
            StartUndo();
            Rebuild();
        }

        public sealed class EntryViewModel : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            internal readonly BpShopData.Entry Entry;
            private readonly Action _changed;
            public EntryViewModel(BpShopData.Entry entry, Action changed) { Entry = entry; _changed = changed; }

            public int Item
            {
                get => Entry.Item;
                set
                {
                    // "None" can't be sold; put the box back to the stored item.
                    if (value <= 0) { global::Avalonia.Threading.Dispatcher.UIThread.Post(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Item)))); return; }
                    if (value != Entry.Item) { Entry.Item = (ushort)value; _changed(); }
                }
            }
            public decimal Price { get => Entry.Price; set { ushort v = (ushort)Math.Clamp(value, 0, ushort.MaxValue); if (v != Entry.Price) { Entry.Price = v; _changed(); } } }
        }

        private void Rebuild()
        {
            Left.Clear(); Right.Clear();
            foreach (var e in _shop.Left) Left.Add(new EntryViewModel(e, Changed));
            foreach (var e in _shop.Right) Right.Add(new EntryViewModel(e, Changed));
        }

        private List<BpShopData.Entry> ListOf(bool right) => right ? _shop.Right : _shop.Left;
        private ObservableCollection<EntryViewModel> RowsOf(bool right) => right ? Right : Left;

        public void Add(bool right)
        {
            if (!CanResize) return;
            var e = new BpShopData.Entry { Item = right ? (ushort)328 : (ushort)1, Price = 1 };
            ListOf(right).Add(e);
            var row = new EntryViewModel(e, Changed);
            RowsOf(right).Add(row);
            if (right) SelectedRight = row; else SelectedLeft = row;
            Changed();
        }

        public void Remove(bool right)
        {
            var row = right ? SelectedRight : SelectedLeft;
            if (!CanResize || row == null) return;
            int at = RowsOf(right).IndexOf(row);
            ListOf(right).Remove(row.Entry);
            RowsOf(right).Remove(row);
            var next = RowsOf(right).ElementAtOrDefault(Math.Min(at, RowsOf(right).Count - 1));
            if (right) SelectedRight = next; else SelectedLeft = next;
            Changed();
        }

        public void Move(bool right, int by)
        {
            var row = right ? SelectedRight : SelectedLeft;
            if (_shop == null || row == null) return;
            var list = ListOf(right);
            int from = list.IndexOf(row.Entry), to = from + by;
            if (to < 0 || to >= list.Count) return;
            list.RemoveAt(from); list.Insert(to, row.Entry);
            RowsOf(right).Move(from, to);
            if (right) SelectedRight = row; else SelectedLeft = row;
            Changed();
        }

        /// <summary>Moves the selected entry to the end of the other counter.</summary>
        public void Transfer(bool fromRight)
        {
            var row = fromRight ? SelectedRight : SelectedLeft;
            if (!CanResize || row == null) return;
            ListOf(fromRight).Remove(row.Entry);
            RowsOf(fromRight).Remove(row);
            ListOf(!fromRight).Add(row.Entry);
            RowsOf(!fromRight).Add(row);
            if (fromRight) { SelectedRight = null; SelectedLeft = row; } else { SelectedLeft = null; SelectedRight = row; }
            Changed();
        }

        public bool CanResize => _shop?.CanResize == true;
        public string LeftHeader => _shop == null ? "Item counter" : $"Item counter ({Left.Count})";
        public string RightHeader => _shop == null ? "TM counter" : $"TM counter ({Right.Count})";

        public string Status
        {
            get
            {
                if (_shop == null) return "";
                if (!_shop.IsPlatinum) return "";
                if (_shop.InPlace) return _shop.FitsInPlace || HasProblem ? "" : "Saving moves the lists to the expanded ARM9 area.";
                return _shop.InExpansion ? "In the expanded ARM9 area." : "";
            }
        }

        public string Problem => _shop?.Problem(ItemNames.Length) ?? "";
        public bool HasProblem => Problem.Length > 0;

        public string Warning => _shop?.RightHasNonTm == true
            ? "The TM counter names the move a TM teaches when you buy it; other items there show a wrong move name." : "";
        public bool HasWarning => Warning.Length > 0;

        private void Changed()
        {
            foreach (var n in new[] { nameof(Problem), nameof(HasProblem), nameof(Warning), nameof(HasWarning), nameof(Status),
                nameof(LeftHeader), nameof(RightHeader), nameof(HasUnsavedChanges) })
                Raise(n);
            _undo?.Record();
        }

        public bool HasUnsavedChanges => _shop != null && !_shop.Snapshot().AsSpan().SequenceEqual(_saved);
        public string UnsavedChangesDescription => "Battle Point shop";

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            if (_shop == null) return true;
            if (HasProblem) { await DialogHelper.ShowError(Problem, "Battle Point Shop"); return false; }
            try { _shop.Save(ItemNames.Length); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException || e is InvalidDataException)
            {
                await DialogHelper.ShowError("The Battle Point shop was not saved:\n" + e.Message, "Battle Point Shop");
                return false;
            }
            _saved = _shop.Snapshot();
            Changed();
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public void DiscardChanges()
        {
            if (_shop == null) return;
            _shop.Restore(_saved);
            SelectedLeft = SelectedRight = null;
            Rebuild();
            StartUndo();
            Changed();
        }
    }
}
