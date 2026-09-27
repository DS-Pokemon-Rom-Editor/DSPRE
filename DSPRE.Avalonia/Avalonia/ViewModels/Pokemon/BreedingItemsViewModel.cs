using System;
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
    /// <summary>Babies that only hatch while a parent holds an incense.</summary>
    public class BreedingItemsViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private IncenseBreedingTable _table;
        private byte[] _saved;

        private ByteStateUndo _undo;
        private void StartUndo() => _undo = new ByteStateUndo(() => _table.ToBytes(), b => { CopyRows(b); Changed(); }, () => { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); });
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        private void CopyRows(byte[] bytes)
        {
            var back = new IncenseBreedingTable(bytes);
            for (int r = 0; r < IncenseBreedingTable.RowCount; r++)
            {
                _table.Rows[r].Baby = back.Rows[r].Baby; _table.Rows[r].Item = back.Rows[r].Item; _table.Rows[r].Fallback = back.Rows[r].Fallback;
                Rows[r].Refresh();
            }
        }

        public string[] SpeciesNames { get; } = Array.Empty<string>();
        public string[] ItemNames { get; } = Array.Empty<string>();
        public ObservableCollection<RowViewModel> Rows { get; } = new ObservableCollection<RowViewModel>();

        public BreedingItemsViewModel() { }

        public BreedingItemsViewModel(bool load)
        {
            if (!load) return;
            SpeciesNames = GetPokemonNames();
            ItemNames = GetItemNames();
            _table = IncenseBreedingTable.Load();
            _saved = _table.ToBytes();
            foreach (var row in _table.Rows) Rows.Add(new RowViewModel(row, Changed) { Number = Rows.Count + 1 });
            StartUndo();
        }

        public sealed class RowViewModel : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly IncenseBreedingTable.Row _row;
            private readonly Action _changed;
            public RowViewModel(IncenseBreedingTable.Row row, Action changed) { _row = row; _changed = changed; }
            public int Number { get; init; }

            public int Baby { get => _row.Baby; set => Put(value, _row.Baby, v => _row.Baby = v, nameof(Baby)); }
            public int Item { get => _row.Item; set => Put(value, _row.Item, v => _row.Item = v, nameof(Item)); }
            public int Fallback { get => _row.Fallback; set => Put(value, _row.Fallback, v => _row.Fallback = v, nameof(Fallback)); }

            // A cleared box sends -1; put it back to the stored value rather than leave it blank. Raised after the
            // binding finishes, since a change raised while it is still writing is ignored.
            private void Put(int value, ushort now, Action<ushort> set, string name)
            {
                if (value < 0) { global::Avalonia.Threading.Dispatcher.UIThread.Post(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name))); return; }
                if (value == now) return;
                set((ushort)value);
                _changed();
            }

            internal void Refresh()
            {
                foreach (var n in new[] { nameof(Baby), nameof(Item), nameof(Fallback) })
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            }
        }

        public string Problem => _table?.Problem(SpeciesNames.Length, ItemNames.Length) ?? "";
        public bool HasProblem => Problem.Length > 0;

        private void Changed()
        {
            Raise(nameof(Problem)); Raise(nameof(HasProblem)); Raise(nameof(HasUnsavedChanges));
            _undo?.Record();
        }

        public bool HasUnsavedChanges => _table != null && !_table.ToBytes().AsSpan().SequenceEqual(_saved);
        public string UnsavedChangesDescription => "Breeding items";

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            if (_table == null) return true;
            if (HasProblem) { await DialogHelper.ShowError(Problem, "Breeding Items"); return false; }
            try { _table.Save(SpeciesNames.Length, ItemNames.Length); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                await DialogHelper.ShowError("The breeding items were not saved:\n" + e.Message, "Breeding Items");
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
            CopyRows(_saved);
            StartUndo();
            Changed();
        }
    }
}
