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
    /// <summary>The regional Pokédex order and the Pokédex's sort and search lists.</summary>
    public class PokedexListsViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public sealed class Row
        {
            public int Species { get; init; }
            public string Text { get; init; }
        }

        public sealed class ListChoice
        {
            public PokedexLists.ListInfo Info { get; init; }
            public string Text { get; init; }
        }

        private readonly PokedexLists _lists;
        private byte[] _saved;
        private ByteStateUndo _undo;

        public string[] SpeciesNames { get; } = Array.Empty<string>();
        public string LoadError { get; }
        public bool HasLoadError => LoadError != null;

        public PokedexListsViewModel() { }

        public PokedexListsViewModel(bool load)
        {
            if (!load) return;
            SpeciesNames = GetPokemonNames();
            if (!PokedexLists.TryLoad(out _lists, out string error)) { LoadError = error; return; }
            foreach (PokedexLists.ListInfo info in _lists.Lists.Where(i => i.Member != PokedexLists.Regional && i.Group != PokedexLists.Group.Unused))
                ListChoices.Add(new ListChoice { Info = info, Text = info.Name });
            _saved = _lists.Snapshot();
            _undo = new ByteStateUndo(_lists.Snapshot, b => { _lists.Restore(b); RefreshAll(); }, () => { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); });
            _selectedList = ListChoices.FirstOrDefault();
            RefreshAll();
        }

        private string Name(int species) => species < SpeciesNames.Length ? SpeciesNames[species] : $"Species {species}";

        // ── Regional dex ─────────────────────────────────────────────────────────────────────────

        public ObservableCollection<Row> Regional { get; } = new();
        public string RegionalCount => _lists == null ? "" : $"{_lists.RegionalOrder.Count} species";

        private int _regionalIndex = -1;
        public int RegionalIndex { get => _regionalIndex; set { _regionalIndex = value; Raise(); RaiseRegionalButtons(); } }

        private int _regionalAdd = 1;
        public int RegionalAdd { get => _regionalAdd; set { _regionalAdd = value; Raise(); } }

        public bool CanRemoveRegional => _regionalIndex >= 0 && _regionalIndex < Regional.Count;
        public bool CanRaiseRegional => CanRemoveRegional && _regionalIndex > 0;
        public bool CanLowerRegional => CanRemoveRegional && _regionalIndex < Regional.Count - 1;

        private void RaiseRegionalButtons() { Raise(nameof(CanRemoveRegional)); Raise(nameof(CanRaiseRegional)); Raise(nameof(CanLowerRegional)); }

        private void RefreshRegional(int select)
        {
            Regional.Clear();
            List<ushort> order = _lists.RegionalOrder;
            for (int i = 0; i < order.Count; i++) Regional.Add(new Row { Species = order[i], Text = $"{i + 1:D3}  {Name(order[i])}" });
            Raise(nameof(RegionalCount));
            RegionalIndex = Math.Min(select, Regional.Count - 1);
        }

        /// <summary>Adds the picked species after the selected entry, or at the end.</summary>
        public string AddRegional()
        {
            List<ushort> order = _lists.RegionalOrder.ToList();
            if (_regionalAdd <= 0 || _regionalAdd >= _lists.SpeciesCount) return "Pick a species to add.";
            if (order.Contains((ushort)_regionalAdd)) return $"{Name(_regionalAdd)} is already number {order.IndexOf((ushort)_regionalAdd) + 1}.";
            int at = CanRemoveRegional ? _regionalIndex + 1 : order.Count;
            order.Insert(at, (ushort)_regionalAdd);
            _lists.SetRegionalOrder(order);
            Edited();
            RefreshRegional(at);
            return null;
        }

        public void RemoveRegional() => EditRegional(o => o.RemoveAt(_regionalIndex), _regionalIndex);
        public void RaiseRegionalEntry() => EditRegional(o => Swap(o, _regionalIndex, _regionalIndex - 1), _regionalIndex - 1);
        public void LowerRegionalEntry() => EditRegional(o => Swap(o, _regionalIndex, _regionalIndex + 1), _regionalIndex + 1);

        private void EditRegional(Action<List<ushort>> change, int select)
        {
            if (!CanRemoveRegional) return;
            List<ushort> order = _lists.RegionalOrder.ToList();
            change(order);
            _lists.SetRegionalOrder(order);
            Edited();
            RefreshRegional(select);
        }

        private static void Swap(List<ushort> l, int a, int b) => (l[a], l[b]) = (l[b], l[a]);

        // ── Sort lists ───────────────────────────────────────────────────────────────────────────

        public ObservableCollection<ListChoice> ListChoices { get; } = new();

        private ListChoice _selectedList;
        public ListChoice SelectedList
        {
            get => _selectedList;
            set { if (value == null || value == _selectedList) return; _selectedList = value; Raise(); RefreshEntries(0); }
        }

        private int _copy;
        /// <summary>0 for the main archive, 1 for the copy with Altered Giratina; only matters where the two differ.</summary>
        public int CopyIndex { get => _copy; set { if (_copy == value) return; _copy = value; Raise(); RefreshEntries(0); } }
        public string[] CopyNames { get; } = { "With Origin Giratina", "With Altered Giratina" };
        public bool ShowsCopyChoice => _lists != null && _selectedList != null && _lists.DiffersByCopy(_selectedList.Info.Member);

        public ObservableCollection<Row> Entries { get; } = new();
        public string EntriesCount => $"{Entries.Count} species";

        private int _entryIndex = -1;
        public int EntryIndex { get => _entryIndex; set { _entryIndex = value; Raise(); RaiseEntryButtons(); } }

        private int _entryAdd = 1;
        public int EntryAdd { get => _entryAdd; set { _entryAdd = value; Raise(); } }

        public bool CanRemoveEntry => _entryIndex >= 0 && _entryIndex < Entries.Count;
        public bool CanRaiseEntry => CanRemoveEntry && _entryIndex > 0;
        public bool CanLowerEntry => CanRemoveEntry && _entryIndex < Entries.Count - 1;

        private void RaiseEntryButtons() { Raise(nameof(CanRemoveEntry)); Raise(nameof(CanRaiseEntry)); Raise(nameof(CanLowerEntry)); }

        private bool Altered => ShowsCopyChoice && _copy == 1;

        private void RefreshEntries(int select)
        {
            Entries.Clear();
            Raise(nameof(ShowsCopyChoice));
            if (_selectedList == null) return;
            List<ushort> list = _lists.Get(_selectedList.Info.Member, Altered);
            for (int i = 0; i < list.Count; i++) Entries.Add(new Row { Species = list[i], Text = $"{i + 1,3}  {Name(list[i])}" });
            Raise(nameof(EntriesCount));
            EntryIndex = Math.Min(select, Entries.Count - 1);
        }

        public string AddEntry()
        {
            if (_selectedList == null) return null;
            List<ushort> list = _lists.Get(_selectedList.Info.Member, Altered).ToList();
            if (_entryAdd <= 0 || _entryAdd >= _lists.SpeciesCount) return "Pick a species to add.";
            if (list.Contains((ushort)_entryAdd)) return $"{Name(_entryAdd)} is already in {_selectedList.Text}.";
            int at = CanRemoveEntry ? _entryIndex + 1 : list.Count;
            list.Insert(at, (ushort)_entryAdd);
            _lists.Set(_selectedList.Info.Member, list, Altered);
            Edited();
            RefreshEntries(at);
            return null;
        }

        public void RemoveEntry() => EditEntries(l => l.RemoveAt(_entryIndex), _entryIndex);
        public void RaiseEntry() => EditEntries(l => Swap(l, _entryIndex, _entryIndex - 1), _entryIndex - 1);
        public void LowerEntry() => EditEntries(l => Swap(l, _entryIndex, _entryIndex + 1), _entryIndex + 1);

        private void EditEntries(Action<List<ushort>> change, int select)
        {
            if (!CanRemoveEntry) return;
            List<ushort> list = _lists.Get(_selectedList.Info.Member, Altered).ToList();
            change(list);
            _lists.Set(_selectedList.Info.Member, list, Altered);
            Edited();
            RefreshEntries(select);
        }

        /// <summary>Builds every sort list again from the species' names, types, heights, weights and body shapes.</summary>
        public string RebuildLists(out bool changed)
        {
            changed = false;
            byte[] before = _lists.Snapshot();
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.personalPokeData });
                string folder = gameDirs[DirNames.personalPokeData].unpackedDir;
                (int, int) Types(int s)
                {
                    string path = Path.Combine(folder, s.ToString("D4"));
                    if (!File.Exists(path)) return (-1, -1);
                    byte[] b = File.ReadAllBytes(path);
                    return b.Length > 7 ? (b[6], b[7]) : (-1, -1);
                }
                _lists.Rebuild(GetPokemonNames(), Types);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException)
            {
                return "The lists could not be rebuilt:\n" + ex.Message;
            }
            changed = !_lists.Snapshot().AsSpan().SequenceEqual(before);
            if (changed) Edited();
            RefreshAll();
            return null;
        }

        // ── Saving ───────────────────────────────────────────────────────────────────────────────

        private void RefreshAll()
        {
            if (_lists == null) return;
            RefreshRegional(Math.Max(0, _regionalIndex));
            RefreshEntries(Math.Max(0, _entryIndex));
            Raise(nameof(HasUnsavedChanges)); Raise(nameof(Problem)); Raise(nameof(HasProblem)); Raise(nameof(Warning)); Raise(nameof(HasWarning));
        }

        private void Edited()
        {
            _undo?.Record();
            Raise(nameof(HasUnsavedChanges)); Raise(nameof(Problem)); Raise(nameof(HasProblem)); Raise(nameof(Warning)); Raise(nameof(HasWarning));
        }

        public string Problem => _lists?.Problem() ?? "";
        public bool HasProblem => Problem.Length > 0;
        /// <summary>While the game's own regional count differs from the saved order: what it holds and what to do.</summary>
        public string CountNote
        {
            get
            {
                if (_lists == null || RegionalDexCount.WhyNot() != null) return "";
                List<ushort> saved = RegionalDexCount.SavedOrder();
                if (saved == null || RegionalDexCount.Matches(saved)) return "";
                return $"The game still counts the regional Pokédex complete at {RegionalDexCount.Current().Completion} species; "
                       + $"the saved order gives {RegionalDexCount.For(saved).Completion}.";
            }
        }
        public bool HasCountNote => CountNote.Length > 0;

        public void RefreshCountNote() { Raise(nameof(CountNote)); Raise(nameof(HasCountNote)); }

        public string Warning => _lists?.Warning() ?? "";
        public bool HasWarning => Warning.Length > 0;

        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        public bool HasUnsavedChanges => _lists != null && !_lists.Snapshot().AsSpan().SequenceEqual(_saved);
        public string UnsavedChangesDescription => "Pokédex lists";

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            if (_lists == null) return true;
            if (HasProblem) { await DialogHelper.ShowError(Problem, "Pokédex Lists"); return false; }
            try { _lists.Save(); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                await DialogHelper.ShowError("The Pokédex lists were not saved:\n" + e.Message, "Pokédex Lists");
                return false;
            }
            _saved = _lists.Snapshot();
            Raise(nameof(HasUnsavedChanges));
            RefreshCountNote();
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        /// <summary>
        /// After a save: the regional size the game's code checks against, when it no longer matches the saved order
        /// and DSPRE can set it; null otherwise.
        /// </summary>
        public (RegionalDexCount.Counts Now, RegionalDexCount.Counts Next, string Problem)? RegionalCountOffer()
        {
            if (_lists == null || RegionalDexCount.WhyNot() != null) return null;
            List<ushort> order = _lists.RegionalOrder;
            if (RegionalDexCount.Matches(order)) return null;
            return (RegionalDexCount.Current(), RegionalDexCount.For(order), RegionalDexCount.Problem(order));
        }

        public void DiscardChanges()
        {
            if (_lists == null) return;
            _lists.Restore(_saved);
            _undo = new ByteStateUndo(_lists.Snapshot, b => { _lists.Restore(b); RefreshAll(); }, () => { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); });
            RefreshAll();
            Raise(nameof(CanUndo)); Raise(nameof(CanRedo));
        }
    }
}
