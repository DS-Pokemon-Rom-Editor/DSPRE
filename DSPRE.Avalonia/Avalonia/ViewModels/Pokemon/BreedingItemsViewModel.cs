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
    public class BreedingItemsViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private IncenseBreedingTable _table;
        private byte[] _saved;

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
            foreach (var row in _table.Rows) Rows.Add(new RowViewModel(row, Changed));
        }

        public sealed class RowViewModel : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly IncenseBreedingTable.Row _row;
            private readonly Action _changed;
            public RowViewModel(IncenseBreedingTable.Row row, Action changed) { _row = row; _changed = changed; }

            public int Baby { get => _row.Baby; set { if (value >= 0 && value != _row.Baby) { _row.Baby = (ushort)value; _changed(); } } }
            public int Item { get => _row.Item; set { if (value >= 0 && value != _row.Item) { _row.Item = (ushort)value; _changed(); } } }
            public int Fallback { get => _row.Fallback; set { if (value >= 0 && value != _row.Fallback) { _row.Fallback = (ushort)value; _changed(); } } }

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
            var back = new IncenseBreedingTable(_saved);
            for (int r = 0; r < IncenseBreedingTable.RowCount; r++)
            {
                _table.Rows[r].Baby = back.Rows[r].Baby; _table.Rows[r].Item = back.Rows[r].Item; _table.Rows[r].Fallback = back.Rows[r].Fallback;
                Rows[r].Refresh();
            }
            Changed();
        }
    }
}
