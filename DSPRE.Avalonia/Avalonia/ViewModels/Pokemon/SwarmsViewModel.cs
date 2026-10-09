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
    public class SwarmsViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private SwarmTable _table;
        private byte[] _saved;
        private readonly Dictionary<ushort, (string Text, int[] Ids, bool Read)> _speciesCache = new();
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
            foreach (string n in new[] { nameof(HeaderNames), nameof(HasMethod), nameof(Loaded) }) Raise(n);
            StartUndo();
            Changed();
        }

        private ByteStateUndo _undo;
        private void StartUndo()
        {
            _undo = new ByteStateUndo(TakeState, ApplyState, () => { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); });
            Raise(nameof(CanUndo)); Raise(nameof(CanRedo));
        }
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        private byte[] TakeState() => ByteStateUndo.Pack(w =>
        {
            w.Write(_table.Rows.Count);
            foreach (SwarmTable.Row r in _table.Rows) { w.Write(r.Header); w.Write(r.Method); }
        });

        private void ApplyState(byte[] state)
        {
            int at = Selected == null ? -1 : Rows.IndexOf(Selected);
            _table.Rows.Clear();
            ByteStateUndo.Unpack(state, r =>
            {
                for (int n = r.ReadInt32(), i = 0; i < n; i++) _table.Rows.Add(new SwarmTable.Row { Header = r.ReadUInt16(), Method = r.ReadUInt16() });
            });
            Rebuild();
            Selected = Rows.ElementAtOrDefault(Math.Min(at, Rows.Count - 1));
            Changed();
        }

        private void Rebuild()
        {
            Rows.Clear();
            foreach (SwarmTable.Row r in _table.Rows) Rows.Add(new RowViewModel(this, r));
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
                foreach (string n in new[] { nameof(Header), nameof(Method), nameof(Species), nameof(SpeciesIds) })
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            }
        }

        /// <summary>Read from the destination header's encounter file.</summary>
        internal (string Text, int[] Ids, bool Read) SpeciesFor(SwarmTable.Row row)
        {
            ushort file = EncounterFileOf(row.Header);
            if (file == ushort.MaxValue) return ("No wild encounters", Array.Empty<int>(), false);
            if (_speciesCache.TryGetValue((ushort)(file * 4 + row.Method), out (string Text, int[] Ids, bool Read) cached)) return cached;
            string text;
            int[] ids = Array.Empty<int>();
            bool read = false;
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.encounters });
                string path = Path.Combine(gameDirs[DirNames.encounters].unpackedDir, file.ToString("D4"));
                EncounterFile enc;
                if (DSPRE.HgEngine.HgEngineProject.IsActive)
                {
                    // Encounters.c is what the next build uses; the built file only fills what an entry leaves out.
                    EncounterFileHGSS source = File.Exists(path) ? new EncounterFileHGSS(new MemoryStream(File.ReadAllBytes(path))) : new EncounterFileHGSS();
                    if (!DSPRE.HgEngine.HgEngineEncounterSource.TryLoad(file, source, out string sourceError)) throw new IOException(sourceError);
                    enc = source;
                }
                else
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    enc = gameFamily == GameFamilies.HGSS
                        ? new EncounterFileHGSS(new MemoryStream(bytes)) : new EncounterFileDPPt(new MemoryStream(bytes));
                }
                // HGSS: walking, surfing and fishing swarms sit in slots 0, 1 and 3 (slot 2 is night fishing).
                ushort[] mons = gameFamily == GameFamilies.HGSS
                    ? new[] { enc.swarmPokemon[row.Method == 2 ? 3 : Math.Min((int)row.Method, 1)] }
                    : enc.swarmPokemon.Take(2).Distinct().ToArray();
                ids = mons.Where(m => m != 0).Select(m => (int)m).ToArray();
                text = string.Join(", ", mons.Select(m => m == 0 ? "none" : m < _pokemonNames.Length ? _pokemonNames[m] : $"#{m}"))
                    + $" (file {file})";
                read = true;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is IndexOutOfRangeException) { text = $"file {file} unreadable"; }
            _speciesCache[(ushort)(file * 4 + row.Method)] = (text, ids, read);
            return (text, ids, read);
        }

        // An unreadable file is left to the other checks rather than reported as empty.
        private bool HasSwarmSpecies(SwarmTable.Row row)
        {
            (string Text, int[] Ids, bool Read) s = SpeciesFor(row);
            return !s.Read || s.Ids.Length > 0;
        }

        private static ushort EncounterFileOf(ushort header)
        {
            try
            {
                MapHeader h = MapHeader.GetMapHeader(header);
                return h == null || h.wildPokemon == nullEncounterID ? ushort.MaxValue : h.wildPokemon;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException) { return ushort.MaxValue; }
        }

        public void Add()
        {
            if (_table == null) return;
            SwarmTable.Row row = new SwarmTable.Row { Header = _table.Rows.LastOrDefault()?.Header ?? 0 };
            _table.Rows.Add(row);
            RowViewModel vm = new RowViewModel(this, row);
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
            await PatchHandover.OfferAsync("swarmTableExpanded", "Expand the swarm table", "More swarm rows", "Swarms");
        }

        /// <summary>After the toolbox moved the table, follows it to its new place and keeps the rows being edited.</summary>
        public void OnPatchStateChanged()
        {
            if (_table == null || _table.FromSource) return;
            SwarmTable moved;
            try { moved = SwarmTable.Load(); }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException) { return; }
            if (moved.Capacity == _table.Capacity && moved.InExpansion == _table.InExpansion) return;
            List<SwarmTable.Row> rows = _table.Rows.ToList();
            moved.Rows.Clear();
            moved.Rows.AddRange(rows);
            _table = moved;
            Changed();
        }

        /// <summary>Re-reads each destination's Pokémon, which the Wild editor may have changed.</summary>
        public void RefreshSpecies()
        {
            if (_table == null) return;
            _speciesCache.Clear();
            foreach (RowViewModel r in Rows) r.Refresh();
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
            _table.InExpansion ? $"{Rows.Count} destinations · in the expanded ARM9 area"
            : $"{Rows.Count} destinations";

        public string Problem => _table?.Problem(HeaderNames.Length, h => EncounterFileOf(h) != ushort.MaxValue, HasSwarmSpecies) ?? "";
        public bool HasProblem => Problem.Length > 0;

        internal void Changed()
        {
            foreach (string n in new[] { nameof(Status), nameof(Problem), nameof(HasProblem), nameof(HasUnsavedChanges) }) Raise(n);
            _undo?.Record();
        }

        public bool HasUnsavedChanges => _table != null && !_table.Snapshot().AsSpan().SequenceEqual(_saved);
        public string UnsavedChangesDescription => "Swarms";

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            if (_table == null) return true;
            if (_table.NeedsExpansion)
            {
                await PatchHandover.OfferAsync("swarmTableExpanded", "Expand the swarm table", "More swarm rows", "Swarms");
                return false;
            }
            // The Wild editor may have changed a swarm species since the rows were read.
            _speciesCache.Clear();
            if (HasProblem) { await DialogHelper.ShowError(Problem, "Swarms"); return false; }
            if (_table.FromSource)
            {
                (bool saved, string error) = await HgEngineSave.RunAsync(() =>
                {
                    try { _table.Save(HeaderNames.Length, h => EncounterFileOf(h) != ushort.MaxValue, HasSwarmSpecies); return null; }
                    catch (InvalidOperationException e) { return e.Message; }
                });
                if (!saved)
                {
                    if (error != null) await DialogHelper.ShowError("The swarms were not saved:\n" + error, "Swarms");
                    return false;
                }
            }
            else
            {
                try { _table.Save(HeaderNames.Length, h => EncounterFileOf(h) != ushort.MaxValue, HasSwarmSpecies); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException || e is InvalidDataException)
                {
                    await DialogHelper.ShowError("The swarms were not saved:\n" + e.Message, "Swarms");
                    return false;
                }
            }
            _saved = _table.Snapshot();
            _speciesCache.Clear();
            foreach (RowViewModel r in Rows) r.Refresh();
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
            StartUndo();
            Changed();
        }
    }
}
