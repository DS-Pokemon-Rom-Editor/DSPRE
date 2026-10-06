using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>The roaming Pokémon: each roamer's species and level, and the routes they move between.</summary>
    public class RoamersViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public sealed class SlotRow : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            internal Action Changed;
            public string Name { get; init; }
            private int _species, _level;
            public int Species { get => _species; set { if (value < 0 || value == _species) return; _species = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Species))); Changed?.Invoke(); } }
            public int Level { get => _level; set { if (value == _level) return; _level = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Level))); Changed?.Invoke(); } }
        }

        public sealed class RouteRow : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            internal Action Changed;
            public int Number { get; init; }
            private int _header;
            private string _next;
            public int Header { get => _header; set { if (value < 0 || value == _header) return; _header = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Header))); Changed?.Invoke(); } }
            /// <summary>The routes a roamer can move to next, by their numbers in this list.</summary>
            public string Next { get => _next; set { if (value == _next) return; _next = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Next))); Changed?.Invoke(); } }
        }

        public ObservableCollection<SlotRow> Slots { get; } = new();
        public ObservableCollection<RouteRow> Routes { get; } = new();
        public string[] PokemonNames { get; private set; } = Array.Empty<string>();
        public string[] HeaderNames { get; private set; } = Array.Empty<string>();

        private RoamerData _data;
        private bool _fromSource;
        private string _saved = "";

        public string WhyNotLoaded { get; private set; } = "";
        public bool Loaded => _data != null || _fromSource;
        private bool _hgeRoutes;
        private string _hgeRoutesSaved = "";
        public bool RoutesEditable => _data != null || _hgeRoutes;
        public string RoutesNote { get; private set; } = "";
        public bool HasRoutesNote => RoutesNote.Length > 0;

        public RoamersViewModel() { }

        public void Setup()
        {
            if (Loaded) return;
            PokemonNames = GetPokemonNames();
            HeaderNames = HeaderLists.GetHeaderListBoxNames().ToArray();
            if (HgEngineProject.IsActive)
            {
                if (!HgEngineRoamers.TryRead(out List<HgEngineRoamers.Slot> slots, out List<int> routes, out string error)) { WhyNotLoaded = error; Raise(nameof(WhyNotLoaded)); return; }
                _fromSource = true;
                for (int s = 0; s < slots.Count; s++) AddSlot(s, slots[s].Species, slots[s].Level);
                string why = HgEngineRoamers.RoutesReadOnlyReason();
                _hgeRoutes = why == null;
                List<List<int>> adjacency = _hgeRoutes ? HgEngineRoamers.ReadAdjacency() : null;
                for (int r = 0; r < routes.Count; r++)
                {
                    RouteRow row = new RouteRow { Number = r + 1, Header = routes[r], Next = adjacency != null ? string.Join(", ", adjacency[r].Select(a => a + 1)) : null };
                    if (_hgeRoutes) row.Changed = Changed;
                    Routes.Add(row);
                }
                RoutesNote = why ?? "";
                _hgeRoutesSaved = RoutesText();
            }
            else
            {
                if (!RoamerData.TryLoad(out _data, out string error)) { WhyNotLoaded = error; Raise(nameof(WhyNotLoaded)); return; }
                for (int s = 0; s < _data.Slots.Count; s++) AddSlot(s, _data.Slots[s].Species, _data.Slots[s].Level);
                for (int r = 0; r < _data.Routes.Count; r++)
                {
                    RouteRow row = new RouteRow { Number = r + 1, Header = _data.Routes[r], Next = string.Join(", ", _data.Adjacency[r].Select(a => a + 1)) };
                    row.Changed = Changed;
                    Routes.Add(row);
                }
                if (_data.Regions is { Length: 2 } g)
                    RoutesNote = $"Roamers 1 and 2 move within routes {g[0].Start + 1} to {g[0].Start + g[0].Count}, the others within {g[1].Start + 1} to {g[1].Start + g[1].Count}.";
            }
            _saved = Snapshot();
            foreach (string n in new[] { nameof(PokemonNames), nameof(HeaderNames), nameof(Loaded), nameof(RoutesEditable), nameof(RoutesNote), nameof(HasRoutesNote) }) Raise(n);
            _undo = new ByteStateUndo(TakeState, ApplyState, () => { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); });
            Raise(nameof(CanUndo)); Raise(nameof(CanRedo));
            Changed();
        }

        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        private byte[] TakeState() => ByteStateUndo.Pack(w =>
        {
            foreach (SlotRow s in Slots) { w.Write(s.Species); w.Write(s.Level); }
            foreach (RouteRow r in Routes) { w.Write(r.Header); w.Write(r.Next ?? ""); }
        });

        private void ApplyState(byte[] state) => ByteStateUndo.Unpack(state, r =>
        {
            foreach (SlotRow s in Slots) { s.Species = r.ReadInt32(); s.Level = r.ReadInt32(); }
            foreach (RouteRow row in Routes) { row.Header = r.ReadInt32(); row.Next = r.ReadString(); }
            Changed();
        });

        private void AddSlot(int index, int species, int level) =>
            Slots.Add(new SlotRow { Name = $"Roamer {index + 1}", Species = species, Level = level, Changed = Changed });

        private string Snapshot() =>
            string.Join(";", Slots.Select(s => $"{s.Species}/{s.Level}")) + "|" + string.Join(";", Routes.Select(r => $"{r.Header}:{r.Next}"));

        private void Changed()
        {
            foreach (string n in new[] { nameof(HasUnsavedChanges), nameof(Problem), nameof(HasProblem) }) Raise(n);
            _undo?.Record();
        }

        // Applies the rows to the data, or says why they can't be.
        private string Apply()
        {
            if (_data == null) return null;
            for (int s = 0; s < Slots.Count; s++) { _data.Slots[s].Species = Slots[s].Species; _data.Slots[s].Level = Slots[s].Level; }
            for (int r = 0; r < Routes.Count; r++)
            {
                _data.Routes[r] = Routes[r].Header;
                string[] next = (Routes[r].Next ?? "").Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                List<int> list = new System.Collections.Generic.List<int>();
                foreach (string n in next)
                {
                    if (!int.TryParse(n, out int k)) return $"Route {r + 1}: \"{n}\" isn't a route number.";
                    list.Add(k - 1);
                }
                _data.Adjacency[r].Clear();
                _data.Adjacency[r].AddRange(list);
            }
            return _data.Problem(HeaderNames.Length, PokemonNames.Length);
        }

        private string RoutesText() => string.Join(";", Routes.Select(r => $"{r.Header}:{r.Next}"));

        // The hg-engine route rows as indexes, or why they can't be read.
        private string ReadHgeRoutes(out List<int> routes, out List<IReadOnlyList<int>> adjacency)
        {
            routes = Routes.Select(r => r.Header).ToList();
            adjacency = new List<IReadOnlyList<int>>();
            for (int r = 0; r < Routes.Count; r++)
            {
                List<int> list = new List<int>();
                foreach (string n in (Routes[r].Next ?? "").Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!int.TryParse(n, out int k) || k < 1 || k > Routes.Count) return $"Route {r + 1}: \"{n}\" isn't a route number from 1 to {Routes.Count}.";
                    list.Add(k - 1);
                }
                if (list.Count < 1 || list.Count > 6) return $"Route {r + 1} needs 1 to 6 next routes.";
                adjacency.Add(list);
            }
            return null;
        }

        public string Problem
        {
            get
            {
                if (!Loaded) return "";
                if (_fromSource)
                {
                    if (_hgeRoutes && ReadHgeRoutes(out _, out _) is string routeProblem) return routeProblem;
                    if (Slots.GroupBy(s => s.Species).Any(g => g.Count() > 1)) return "Two roamers can't be the same species: the game finds the one you battled by species.";
                    return Slots.FirstOrDefault(s => s.Level < 1 || s.Level > 100) is SlotRow bad ? $"{bad.Name}'s level must be 1 to 100." : "";
                }
                return Apply() ?? "";
            }
        }
        public bool HasProblem => Problem.Length > 0;

        public bool HasUnsavedChanges => Loaded && Snapshot() != _saved;
        public string UnsavedChangesDescription => "Roamers";

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            if (!HasUnsavedChanges) return true;
            if (HasProblem) { await DialogHelper.ShowError(Problem, "Roamers"); return false; }
            if (_fromSource)
            {
                List<HgEngineRoamers.Slot> slots = Slots.Select(s => new HgEngineRoamers.Slot(s.Species, s.Level)).ToList();
                bool routesChanged = _hgeRoutes && RoutesText() != _hgeRoutesSaved;
                ReadHgeRoutes(out List<int> routes, out List<IReadOnlyList<int>> adjacency);
                (bool saved, string error) = await HgEngineSave.RunAsync(() =>
                    !HgEngineRoamers.TryWrite(slots, out string e) ? e
                    : routesChanged && !HgEngineRoamers.TryWriteRoutes(routes, adjacency, out e) ? e : null);
                if (!saved) { if (error != null) await DialogHelper.ShowError("The roamers were not saved:\n" + error, "Roamers"); return false; }
            }
            else
            {
                try { Apply(); _data.Save(HeaderNames.Length, PokemonNames.Length); }
                catch (Exception e) when (e is InvalidOperationException || e is System.IO.IOException || e is UnauthorizedAccessException)
                {
                    await DialogHelper.ShowError("The roamers were not saved:\n" + e.Message, "Roamers");
                    return false;
                }
            }
            _saved = Snapshot();
            _hgeRoutesSaved = RoutesText();
            Changed();
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public void DiscardChanges()
        {
            _data = null;
            _fromSource = false;
            _hgeRoutes = false;
            Slots.Clear();
            Routes.Clear();
            Setup();
        }
    }
}
