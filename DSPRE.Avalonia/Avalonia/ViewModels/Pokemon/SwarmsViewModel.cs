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
    /// <summary>Where swarms can happen: one row per possible destination, picked at random each day.</summary>
    public class SwarmsViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private SwarmTable _table;
        private byte[] _saved;
        private readonly Dictionary<ushort, (string Text, int[] Ids)> _speciesCache = new();
        private string[] _pokemonNames = Array.Empty<string>();

        public string[] HeaderNames { get; private set; } = Array.Empty<string>();
        public string[] MethodNames => SwarmTable.MethodNames;
        public bool HasMethod => _table?.HasMethod ?? false;
        public ObservableCollection<RowViewModel> Rows { get; } = new ObservableCollection<RowViewModel>();

        private RowViewModel _selected;
        public RowViewModel Selected { get => _selected; set { _selected = value; Raise(); } }

        public string WhyNotLoaded { get; private set; } = "";
        public bool Loaded => _table != null;

        public SwarmsViewModel() { }

        public void Setup()
        {
            if (_table != null) return;
            string why = SwarmTable.WhyNot();
            if (why != null) { WhyNotLoaded = why; Raise(nameof(WhyNotLoaded)); return; }
            HeaderNames = HeaderLists.GetHeaderListBoxNames().ToArray();
            _pokemonNames = GetPokemonNames();
            _table = SwarmTable.Load();
            _saved = _table.Snapshot();
            _speciesCache.Clear();
            Rebuild();
            foreach (var n in new[] { nameof(HeaderNames), nameof(HasMethod), nameof(Loaded) }) Raise(n);
            Changed();
        }

        private void Rebuild()
        {
            Rows.Clear();
            foreach (var r in _table.Rows) Rows.Add(new RowViewModel(this, r));
        }

        public sealed class RowViewModel : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly SwarmsViewModel _o;
            internal readonly SwarmTable.Row Row;
            public RowViewModel(SwarmsViewModel owner, SwarmTable.Row row) { _o = owner; Row = row; }

            public int Header
            {
                get => Row.Header;
                set
                {
                    if (value < 0) { global::Avalonia.Threading.Dispatcher.UIThread.Post(Refresh); return; }   // a cleared box goes back to the stored header
                    if (value != Row.Header) { Row.Header = (ushort)value; Refresh(); _o.Changed(); }
                }
            }
            public int Method
            {
                get => Row.Method;
                set { if (value >= 0 && value != Row.Method) { Row.Method = (ushort)value; Refresh(); _o.Changed(); } }
            }
            public string Species => _o.SpeciesFor(Row).Text;
            public int[] SpeciesIds => _o.SpeciesFor(Row).Ids;

            internal void Refresh()
            {
                foreach (var n in new[] { nameof(Header), nameof(Method), nameof(Species), nameof(SpeciesIds) })
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            }
        }

        /// <summary>Read from the destination header's encounter file.</summary>
        internal (string Text, int[] Ids) SpeciesFor(SwarmTable.Row row)
        {
            ushort file = EncounterFileOf(row.Header);
            if (file == ushort.MaxValue) return ("No wild encounters", Array.Empty<int>());
            if (_speciesCache.TryGetValue((ushort)(file * 4 + row.Method), out var cached)) return cached;
            string text;
            int[] ids = Array.Empty<int>();
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.encounters });
                byte[] bytes = File.ReadAllBytes(Path.Combine(gameDirs[DirNames.encounters].unpackedDir, file.ToString("D4")));
                EncounterFile enc = gameFamily == GameFamilies.HGSS
                    ? new EncounterFileHGSS(new MemoryStream(bytes)) : new EncounterFileDPPt(new MemoryStream(bytes));
                // HGSS: walking, surfing and fishing swarms sit in slots 0, 1 and 3 (slot 2 is night fishing).
                ushort[] mons = gameFamily == GameFamilies.HGSS
                    ? new[] { enc.swarmPokemon[row.Method == 2 ? 3 : Math.Min((int)row.Method, 1)] }
                    : enc.swarmPokemon.Take(2).Distinct().ToArray();
                ids = mons.Where(m => m != 0).Select(m => (int)m).ToArray();
                text = string.Join(", ", mons.Select(m => m == 0 ? "none" : m < _pokemonNames.Length ? _pokemonNames[m] : $"#{m}"))
                    + $" (file {file})";
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is IndexOutOfRangeException) { text = $"file {file} unreadable"; }
            _speciesCache[(ushort)(file * 4 + row.Method)] = (text, ids);
            return (text, ids);
        }

        private static ushort EncounterFileOf(ushort header)
        {
            try
            {
                var h = MapHeader.GetMapHeader(header);
                return h == null || h.wildPokemon == nullEncounterID ? ushort.MaxValue : h.wildPokemon;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException) { return ushort.MaxValue; }
        }

        public void Add()
        {
            if (_table == null) return;
            var row = new SwarmTable.Row { Header = _table.Rows.LastOrDefault()?.Header ?? 0 };
            _table.Rows.Add(row);
            var vm = new RowViewModel(this, row);
            Rows.Add(vm);
            Selected = vm;
            Changed();
            _ = OfferExpansionAsync();
        }

        private bool _expansionOffered;

        // Offered once when the table first outgrows the game's room; Save asks again if still needed.
        private async Task OfferExpansionAsync()
        {
            if (_expansionOffered || _table?.NeedsExpansion != true) return;
            _expansionOffered = true;
            if (await Arm9ExpansionOffer.EnsureAsync("More swarm rows", "Swarms")) Changed();
        }

        /// <summary>Re-reads each destination's Pokémon, which the Wild editor may have changed.</summary>
        public void RefreshSpecies()
        {
            if (_table == null) return;
            _speciesCache.Clear();
            foreach (var r in Rows) r.Refresh();
        }

        public void Remove()
        {
            if (_table == null || Selected == null) return;
            int at = Rows.IndexOf(Selected);
            _table.Rows.Remove(Selected.Row);
            Rows.Remove(Selected);
            Selected = Rows.ElementAtOrDefault(Math.Min(at, Rows.Count - 1));
            Changed();
        }

        public string Status => _table == null ? "" :
            !_table.FitsWhereItIs && !HasProblem ? $"{Rows.Count} destinations · saving moves the table to the expanded ARM9 area"
            : _table.InExpansion ? $"{Rows.Count} destinations · in the expanded ARM9 area"
            : $"{Rows.Count} destinations";

        public string Problem => _table?.Problem(HeaderNames.Length, h => EncounterFileOf(h) != ushort.MaxValue) ?? "";
        public bool HasProblem => Problem.Length > 0;

        internal void Changed()
        {
            foreach (var n in new[] { nameof(Status), nameof(Problem), nameof(HasProblem), nameof(HasUnsavedChanges) }) Raise(n);
        }

        public bool HasUnsavedChanges => _table != null && !_table.Snapshot().AsSpan().SequenceEqual(_saved);
        public string UnsavedChangesDescription => "Swarms";

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            if (_table == null) return true;
            if (_table.NeedsExpansion && await Arm9ExpansionOffer.EnsureAsync("More swarm rows", "Swarms")) Changed();
            if (HasProblem) { await DialogHelper.ShowError(Problem, "Swarms"); return false; }
            try { _table.Save(HeaderNames.Length, h => EncounterFileOf(h) != ushort.MaxValue); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException || e is InvalidDataException)
            {
                await DialogHelper.ShowError("The swarms were not saved:\n" + e.Message, "Swarms");
                return false;
            }
            _saved = _table.Snapshot();
            _speciesCache.Clear();
            foreach (var r in Rows) r.Refresh();
            Changed();
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public void DiscardChanges()
        {
            if (_table == null) return;
            try { _table = SwarmTable.Load(); }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException || e is ArgumentException)
            {
                _ = DialogHelper.ShowError("The saved swarms couldn't be read back:\n" + e.Message, "Swarms");
                return;
            }
            _saved = _table.Snapshot();
            _speciesCache.Clear();
            Selected = null;
            Rebuild();
            Changed();
        }
    }
}
