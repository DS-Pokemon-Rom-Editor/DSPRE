using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.World
{
    /// <summary>
    /// What an imported map is missing here, most visible first: textures, buildings, then events on squares it
    /// closed. Nothing changes unless the user asks.
    /// </summary>
    public class MapImportReviewViewModel : INotifyPropertyChanged
    {
        public sealed class PackRow
        {
            public int Pack { get; init; }
            public int Undressed { get; init; }
            public string Name => $"Map textures {Pack}";
            public string State => Undressed == 0 ? "Has them all" : $"{Undressed} still grey";
        }

        private readonly MapEditorViewModel _editor;
        private readonly MapFile _map;
        private readonly int _areaId;
        private int _pack;

        public string Title { get; }
        public ObservableCollection<MapImportCheck.Missing> Textures { get; } = new ObservableCollection<MapImportCheck.Missing>();
        public List<MapImportCheck.BuildingTrouble> Buildings { get; }
        public List<MapEditorViewModel.EventClash> Events { get; }
        public ObservableCollection<PackRow> Packs { get; } = new ObservableCollection<PackRow>();

        private int _materials, _undressed;

        public MapImportReviewViewModel(MapEditorViewModel editor, string what, MapFile map, int areaId, int pack,
                                        List<MapImportCheck.BuildingTrouble> buildings, List<MapEditorViewModel.EventClash> events)
        {
            _editor = editor;
            _map = map;
            _areaId = areaId;
            _pack = pack;
            Title = "Import issues: " + what;
            Buildings = buildings ?? new List<MapImportCheck.BuildingTrouble>();
            Events = events ?? new List<MapEditorViewModel.EventClash>();
            foreach (MapEditorViewModel.EventClash clash in Events)
                clash.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MapEditorViewModel.EventClash.Move)) TickChanged(); };
            CheckTextures();
            SelectedTab = HasTextures ? 0 : HasBuildings ? 1 : 2;
        }

        public bool HasAny => HasTextures || HasBuildings || HasEvents;
        public bool HasTextures => _undressed > 0;
        public bool HasBuildings => Buildings.Count > 0;
        public bool HasEvents => Events.Count > 0;
        public string TexturesHeader => $"Textures ({_undressed})";
        public string BuildingsHeader => $"Buildings ({Buildings.Count})";
        public string EventsHeader => $"Events ({Events.Count})";

        public string Summary
        {
            get
            {
                List<string> lines = new List<string>();
                lines.Add(HasTextures ? $"Textures: Map textures {_pack} has none for {_undressed} of the model's {_materials} materials, so they show grey."
                                      : $"Textures: Map textures {_pack} has them all.");
                if (HasBuildings)
                {
                    int missingModels = Buildings.Count(b => b.NoSuchModel);
                    int bare = Buildings.Count - missingModels;
                    lines.Add("Buildings: " + string.Join(", ", new[]
                    {
                        missingModels > 0 ? $"{missingModels} model{(missingModels > 1 ? "s" : "")} this game lacks" : null,
                        bare > 0 ? $"{bare} model{(bare > 1 ? "s" : "")} without textures in the building pack" : null,
                    }.Where(s => s != null)) + ".");
                }
                if (HasEvents) lines.Add($"Events: {Events.Count} now stand on squares the import closed.");
                return string.Join("\n", lines);
            }
        }

        private int _selectedTab;
        public int SelectedTab { get => _selectedTab; set => Set(ref _selectedTab, value); }

        private void CheckTextures()
        {
            (int Materials, int Undressed, List<MapImportCheck.Missing> Names) found;
            try { found = MapImportCheck.MapTextures(_map, MapImportCheck.MapPackPath(_pack)); }
            catch (Exception ex) when (ex is System.IO.IOException || ex is ArgumentException || ex is IndexOutOfRangeException)
            {
                AppLogger.Error("Import review textures: " + ex.Message);
                found = (0, 0, new List<MapImportCheck.Missing>());
            }
            _materials = found.Materials;
            _undressed = found.Undressed;
            Textures.Clear();
            foreach (MapImportCheck.Missing m in found.Names) Textures.Add(m);
            foreach (string n in new[] { nameof(HasTextures), nameof(TexturesHeader), nameof(Summary), nameof(HasAny) }) Raise(n);
        }

        // ── Pack finder ───────────────────────────────────────────────────────────
        private bool _searching;
        public bool Searching { get => _searching; private set { if (Set(ref _searching, value)) Raise(nameof(CanSearch)); } }
        public bool CanSearch => !_searching;
        /// <summary>
        /// Use changes the map textures of the area open in the Map editor; a map no header uses picks its preview
        /// area there.
        /// </summary>
        public bool CanUsePack => _areaId >= 0 && _editor.AreaIdForImport == _areaId;
        private string _packNote;
        public string PackNote { get => _packNote; private set => Set(ref _packNote, value); }

        public async Task FindPacksAsync()
        {
            Searching = true;
            PackNote = "Looking through every map texture pack…";
            List<(int Pack, int Undressed)> ranked;
            try { ranked = await Task.Run(() => MapImportCheck.RankPacks(_map)); }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                PackNote = "Couldn't read the packs: " + ex.Message;
                Searching = false;
                return;
            }
            // Only packs that leave fewer materials grey than the one in use are worth listing.
            List<(int Pack, int Undressed)> better = ranked.Where(r => r.Undressed < _undressed).Take(12).ToList();
            Packs.Clear();
            foreach ((int pack, int undressed) in better) Packs.Add(new PackRow { Pack = pack, Undressed = undressed });
            PackNote = better.Count > 0 ? (_areaId < 0 ? "No header uses this map. Pick its preview area in the Map editor." : null)
                : ranked.All(r => r.Undressed >= _materials) ? "No map texture pack in this ROM has any of them."
                : $"No pack does better than Map textures {_pack}.";
            Searching = false;
        }

        public void UsePack(PackRow row)
        {
            if (row == null || !CanUsePack) return;
            _editor.MapTilesetIndex = row.Pack;
            _pack = row.Pack;
            CheckTextures();
            PackNote = $"Map textures are now pack {row.Pack}, for every header on area {_areaId}. Save the map to keep it.";
        }

        // ── Events ────────────────────────────────────────────────────────────────
        public bool? AllEventsTicked
        {
            get => Events.All(e => e.Move) ? true : Events.Any(e => e.Move) ? null : false;
            set
            {
                bool tick = value == true;
                _bulkTicking = true;
                foreach (MapEditorViewModel.EventClash e in Events) if (!e.Fixed) e.Move = tick;
                _bulkTicking = false;
                _editor.PlanFixes(Events);
            }
        }
        public bool AnyEventTicked => Events.Any(e => e.Move);

        // Select all ticks every row at once; the preview is worked out once after.
        private bool _bulkTicking;
        private void TickChanged()
        {
            Raise(nameof(AllEventsTicked));
            Raise(nameof(AnyEventTicked));
            if (!_bulkTicking) _editor.PlanFixes(Events);
        }

        private string _eventsNote;
        public string EventsNote { get => _eventsNote; private set => Set(ref _eventsNote, value); }

        public void FixSelectedEvents()
        {
            int moved = _editor.MoveEvents(Events.Where(e => e.Move));
            EventsNote = moved == 0 ? "Nothing fixed." : $"Fixed {moved}. Save the map to keep {(moved > 1 ? "them" : "it")}.";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        private bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            Raise(name);
            return true;
        }
    }
}
