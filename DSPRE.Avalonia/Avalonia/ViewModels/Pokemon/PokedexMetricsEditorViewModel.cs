using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>The Pokédex tab: height, weight, body shape and the size check's scales and offsets. Vanilla edits
    /// the game's Pokédex data archives; hg-engine edits data/Species.c metricsData.</summary>
    public class PokedexMetricsEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private static bool UseHgEngineSource => HgEngineProject.IsActive;

        // pokeheartgold zukan_data.json and hg-engine's DEX_SEARCH_BODYTYPE_* order.
        private static readonly string[] ShapeNames =
        {
            "Quadruped", "Bipedal, tailless", "Bipedal, tailed", "Serpentine", "Two pairs of wings", "One pair of wings",
            "Insectoid", "Head and body", "Head and arms", "Head and legs", "Tentacles", "Fins", "Head only", "Multiple bodies",
        };

        public ObservableCollection<string> BodyShapes { get; } = new();
        public string[] Formes { get; } = { "Altered Forme", "Origin Forme" };

        private PokedexDataArchive _archive;
        private string _archiveError;
        private int _currentId = -1;
        // Index 0 is the species (Altered Giratina where the game has both); 1 is Origin Giratina.
        private PokedexMetrics[] _loaded = new PokedexMetrics[2];
        private PokedexMetrics[] _values = new PokedexMetrics[2];
        private int _forme;
        private bool _loading;

        private string _unavailable;
        /// <summary>Why this entry has nothing to edit, or null.</summary>
        public string Unavailable { get => _unavailable; private set { _unavailable = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsAvailable)); OnPropertyChanged(nameof(IsUnavailable)); } }
        public bool IsAvailable => _unavailable == null;
        public bool IsUnavailable => _unavailable != null;

        public bool ShowForme => !UseHgEngineSource && _archive?.HasOriginGiratina == true && _currentId == SpeciesFile.GIRATINA_ID_NUM;
        public int FormeIndex
        {
            get => _forme;
            set { if (value is 0 or 1 && value != _forme) { _forme = value; OnPropertyChanged(); RaiseFields(); } }
        }

        private PokedexMetrics V => _values[_forme] ?? new PokedexMetrics();

        public decimal HeightMetres { get => V.Height / 10m; set => Edit(m => m.Height = (int)Math.Round(value * 10)); }
        public decimal WeightKilograms { get => V.Weight / 10m; set => Edit(m => m.Weight = (int)Math.Round(value * 10)); }
        public int BodyShapeIndex { get => V.BodyShape; set { if (value >= 0) Edit(m => m.BodyShape = value); } }
        public int FemaleTrainerScale { get => V.FemaleTrainerScale; set => Edit(m => m.FemaleTrainerScale = value); }
        public int FemalePokemonScale { get => V.FemalePokemonScale; set => Edit(m => m.FemalePokemonScale = value); }
        public int MaleTrainerScale { get => V.MaleTrainerScale; set => Edit(m => m.MaleTrainerScale = value); }
        public int MalePokemonScale { get => V.MalePokemonScale; set => Edit(m => m.MalePokemonScale = value); }
        public int FemaleTrainerYOffset { get => V.FemaleTrainerYOffset; set => Edit(m => m.FemaleTrainerYOffset = value); }
        public int FemalePokemonYOffset { get => V.FemalePokemonYOffset; set => Edit(m => m.FemalePokemonYOffset = value); }
        public int MaleTrainerYOffset { get => V.MaleTrainerYOffset; set => Edit(m => m.MaleTrainerYOffset = value); }
        public int MalePokemonYOffset { get => V.MalePokemonYOffset; set => Edit(m => m.MalePokemonYOffset = value); }

        private static readonly string[] FieldNames =
        {
            nameof(HeightMetres), nameof(WeightKilograms), nameof(BodyShapeIndex),
            nameof(FemaleTrainerScale), nameof(FemalePokemonScale), nameof(MaleTrainerScale), nameof(MalePokemonScale),
            nameof(FemaleTrainerYOffset), nameof(FemalePokemonYOffset), nameof(MaleTrainerYOffset), nameof(MalePokemonYOffset),
        };
        private void RaiseFields() { foreach (var n in FieldNames) OnPropertyChanged(n); }

        private void Edit(Action<PokedexMetrics> change)
        {
            if (_loading || _values[_forme] == null) return;
            var next = _values[_forme].Clone();
            change(next);
            if (next.SameAs(_values[_forme])) return;
            _values[_forme] = next;
            Capture();
            RaiseFields();
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        // ─── Habitat ──────────────────────────────────────────────────────────────
        /// <summary>One area's times of day in the habitat lists: four on HGSS, five on DP and Pt.</summary>
        public sealed class AreaRow : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            internal Action<AreaRow, int, bool> Changed;
            public int Group { get; init; }
            public int Id { get; init; }
            public string Name { get; init; }
            /// <summary>Maps (group, time) to the list index.</summary>
            internal Func<int, int, int> ListIndex;
            /// <summary>DP and Pt also list where a species turns up once the National Dex is obtained.</summary>
            public bool HasNationalDex { get; init; }
            private int TimeCount => HasNationalDex ? 5 : 4;
            private readonly bool[] _at = new bool[5];
            private void Set(int time, bool value)
            {
                if (_at[time] == value) return;
                _at[time] = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(TimeNames[time]));
                Changed?.Invoke(this, time, value);
            }
            public bool Morning { get => _at[0]; set => Set(0, value); }
            public bool Day { get => _at[1]; set => Set(1, value); }
            public bool Night { get => _at[2]; set => Set(2, value); }
            public bool Other { get => _at[3]; set => Set(3, value); }
            public bool NationalDex { get => _at[4]; set => Set(4, value); }
            private static readonly string[] TimeNames = { nameof(Morning), nameof(Day), nameof(Night), nameof(Other), nameof(NationalDex) };
            internal void Show(int[][] lists)
            {
                for (int t = 0; t < TimeCount; t++)
                {
                    _at[t] = lists != null && lists[ListIndex(Group, t)].Contains(Id);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(TimeNames[t]));
                }
            }
        }

        public ObservableCollection<AreaRow> SpecialAreas { get; } = new();
        public ObservableCollection<AreaRow> RouteAreas { get; } = new();
        private PokedexAreaData _areaData;
        private bool _areasSetUp;
        private int[][] _areasLoaded, _areas;
        public bool ShowAreas => _areas != null;
        public bool ShowNationalDexAreas => _areaData != null && _areaData.Times == 5;

        private void SetUpAreas()
        {
            if (_areasSetUp) return;
            _areasSetUp = true;
            var groups = new[] { new List<(int Id, string Name)>(), new List<(int Id, string Name)>() };
            if (UseHgEngineSource)
            {
                if (!HgEnginePokedexAreas.TryGetAreas(out groups, out _)) return;
            }
            else
            {
                if (!PokedexAreaData.TryLoad(out _areaData, out _)) return;
                groups = _areaData.Areas();
            }
            var lists = new[] { SpecialAreas, RouteAreas };
            bool national = ShowNationalDexAreas;
            for (int g = 0; g < 2; g++)
                foreach (var (id, name) in groups[g])
                    lists[g].Add(new AreaRow { Group = g, Id = id, Name = name, Changed = OnAreaChanged, ListIndex = AreaList, HasNationalDex = national });
            OnPropertyChanged(nameof(ShowNationalDexAreas));
        }

        private int[][] ReadAreas(int id)
        {
            SetUpAreas();
            if (SpecialAreas.Count + RouteAreas.Count == 0) return null;
            if (UseHgEngineSource) return HgEnginePokedexAreas.TryLoad(id, out var lists, out _) ? lists : null;
            return _areaData != null && id > 0 && id < _areaData.SpeciesCount ? _areaData.Get(id) : null;
        }

        private int AreaList(int group, int time) => _areaData != null ? _areaData.List(group, time) : PokedexAreaData.ListOf(group, time);

        private void ShowAreaRows() { foreach (var r in SpecialAreas.Concat(RouteAreas)) r.Show(_areas); }

        private void OnAreaChanged(AreaRow row, int time, bool on)
        {
            if (_loading || _areas == null) return;
            int k = AreaList(row.Group, time);
            var next = Copy(_areas);
            next[k] = on ? next[k].Where(i => i != row.Id).Append(row.Id).ToArray() : next[k].Where(i => i != row.Id).ToArray();
            _areas = next;
            Capture();
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private static int[][] Copy(int[][] lists) => lists?.Select(l => l.ToArray()).ToArray();
        private static bool SameAreas(int[][] a, int[][] b) => a == null || b == null ? a == b : a.Zip(b).All(z => z.First.SequenceEqual(z.Second));

        // ─── Footprint ────────────────────────────────────────────────────────────
        // The species' footprint drawing, edited in memory and written on Save like the rest of the tab.
        private GraphicAssets.Archive _footprintArchive;
        private int _footprintEntry = -1;
        private byte[] _footprintLoaded, _footprint;

        private Bitmap _footprintImage;
        public Bitmap FootprintImage { get => _footprintImage; private set { _footprintImage = value; OnPropertyChanged(); } }
        public bool ShowFootprint => _footprint != null;

        // Reads of the footprint see this tab's bytes; writes go to <paramref name="write"/> instead of the project.
        private ScriptNarc.Staging FootprintStaging(Action<RomInfo.DirNames, int, byte[]> write = null) => new()
        {
            Read = (dir, id) => dir == RomInfo.DirNames.footprintGraphics && id == _footprintEntry ? _footprint : null,
            Write = write ?? ((_, _, _) => { }),
        };

        private void LoadFootprint(int species)
        {
            _footprintArchive ??= GraphicAssets.All.FirstOrDefault(x => x.Dir == RomInfo.DirNames.footprintGraphics);
            _footprintEntry = -1;
            try { if (_footprintArchive != null) _footprintEntry = GraphicAssets.FootprintEntry(species); } catch { }
            _footprintLoaded = _footprintEntry >= 0 ? new ScriptNarc(RomInfo.DirNames.footprintGraphics).Get(_footprintEntry) : null;
            _footprint = _footprintLoaded;
            ShowFootprintImage();
        }

        private void ShowFootprintImage()
        {
            Bitmap image = null;
            if (_footprint != null)
                using (ScriptNarc.Use(FootprintStaging()))
                {
                    var shown = GraphicAssets.Render(_footprintArchive, _footprintEntry);
                    if (shown.Rgba != null) image = ImageConverter.FromRgba(shown.Rgba, shown.Width, shown.Height);
                }
            FootprintImage = image;
            OnPropertyChanged(nameof(ShowFootprint));
        }

        public string ExportFootprint(string path)
        {
            if (_footprint == null) return "This Pokémon has no footprint.";
            using (ScriptNarc.Use(FootprintStaging())) return GraphicAssets.ExportPng(_footprintArchive, _footprintEntry, path);
        }

        public string ImportFootprint(string path)
        {
            if (_footprint == null) return "This Pokémon has no footprint.";
            byte[] drawn = null;
            bool elsewhere = false;
            string error;
            using (ScriptNarc.Use(FootprintStaging((dir, id, bytes) =>
                   {
                       if (dir == RomInfo.DirNames.footprintGraphics && id == _footprintEntry) drawn = bytes;
                       else elsewhere = true;
                   })))
                error = GraphicAssets.ImportPng(_footprintArchive, _footprintEntry, path, out _);
            if (error != null) return error;
            // Every footprint shares one palette, so a picture that would change it changes them all.
            if (elsewhere) return "That picture needs colours the footprints don't have, so nothing was changed.";
            if (drawn == null || drawn.AsSpan().SequenceEqual(_footprint)) return null;
            _footprint = drawn;
            ShowFootprintImage();
            Capture();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            return null;
        }

        private string WriteFootprintOrError()
        {
            try { WriteFootprint(); return null; }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException) { return "The footprint could not be written: " + ex.Message; }
        }

        private bool FootprintChanged => _footprint != null && _footprintLoaded != null && !_footprint.AsSpan().SequenceEqual(_footprintLoaded);

        private void WriteFootprint()
        {
            if (FootprintChanged) new ScriptNarc(RomInfo.DirNames.footprintGraphics).Put(_footprintEntry, _footprint);
        }

        // ─── Dirty, undo ──────────────────────────────────────────────────────────
        public bool HasUnsavedChanges => Enumerable.Range(0, 2).Any(i => _values[i] != null && !_values[i].SameAs(_loaded[i])) || !SameAreas(_areas, _areasLoaded) || FootprintChanged;
        public string UnsavedChangesDescription => $"Pokédex (Mon {_currentId})";

        private sealed class Snapshot { public PokedexMetrics[] Values; public int[][] Areas; public byte[] Footprint; }
        private readonly UndoHistory<Snapshot> _history = new();
        private DateTime _lastCaptureUtc = DateTime.MinValue;
        private const int CoalesceMs = 500;
        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public void Undo() { if (_history.CanUndo) Apply(_history.Undo()); }
        public void Redo() { if (_history.CanRedo) Apply(_history.Redo()); }

        private Snapshot Take() => new() { Values = _values.Select(v => v?.Clone()).ToArray(), Areas = Copy(_areas), Footprint = _footprint };

        private void Capture()
        {
            bool coalesce = (DateTime.UtcNow - _lastCaptureUtc).TotalMilliseconds < CoalesceMs;
            _history.Capture(Take(), coalesce);
            _lastCaptureUtc = DateTime.UtcNow;
            RaiseUndoState();
        }

        private void Apply(Snapshot s)
        {
            if (s == null) return;
            _values = s.Values.Select(v => v?.Clone()).ToArray();
            _areas = Copy(s.Areas);
            if (s.Footprint != _footprint) { _footprint = s.Footprint; ShowFootprintImage(); }
            _loading = true;
            try { ShowAreaRows(); } finally { _loading = false; }
            RaiseFields();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RaiseUndoState();
        }

        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        // ─── Construction, loading ────────────────────────────────────────────────
        public PokedexMetricsEditorViewModel()
        {
            var names = UseHgEngineSource ? HgEnginePokedexMetrics.BodyShapeNames() : null;
            if (names == null || names.Count == ShapeNames.Length) foreach (var n in ShapeNames) BodyShapes.Add(n);
            else foreach (var n in names) BodyShapes.Add(char.ToUpper(n[0]) + n.Substring(1).ToLower().Replace('_', ' '));
        }

        public void LoadMon(int id)
        {
            _loading = true;
            try
            {
                _currentId = id;
                _forme = 0;
                _loaded = new PokedexMetrics[2];
                string why = null;

                if (UseHgEngineSource)
                {
                    if (HgEnginePokedexMetrics.TryLoad(id, out var m, out string error)) _loaded[0] = m;
                    else why = $"This entry's Pokédex data could not be read: {error}";
                }
                else
                {
                    if (_archive == null && _archiveError == null && !PokedexDataArchive.TryLoad(out _archive, out _archiveError)) _archive = null;
                    if (_archive == null) why = _archiveError;
                    else if (id <= 0 || id >= _archive.SpeciesCount) why = "Alternate forms share the base Pokémon's Pokédex data.";
                    else
                    {
                        _loaded[0] = _archive.Get(id, originGiratina: false);
                        if (ShowFormeFor(id)) _loaded[1] = _archive.Get(id, originGiratina: true);
                    }
                }

                _values = _loaded.Select(v => v?.Clone()).ToArray();
                _areasLoaded = why == null ? ReadAreas(id) : null;
                _areas = Copy(_areasLoaded);
                ShowAreaRows();
                OnPropertyChanged(nameof(ShowAreas));
                LoadFootprint(id);
                Unavailable = why;
                _history.Reset(Take());
                _lastCaptureUtc = DateTime.MinValue;
                OnPropertyChanged(nameof(ShowForme));
                OnPropertyChanged(nameof(FormeIndex));
                RaiseFields();
                OnPropertyChanged(nameof(HasUnsavedChanges));
                RaiseUndoState();
            }
            finally { _loading = false; }
        }

        private bool ShowFormeFor(int id) => _archive?.HasOriginGiratina == true && id == SpeciesFile.GIRATINA_ID_NUM;

        public void DiscardChanges() => LoadMon(_currentId);

        // ─── hg-engine size sorts ─────────────────────────────────────────────────
        public bool CanRebuildSortLists => UseHgEngineSource;

        /// <summary>Lists every species in the size sorts, ordered by the saved heights and weights.</summary>
        public async Task RebuildSortListsAsync()
        {
            int listed = 0;
            var (saved, error) = await HgEngineSave.RunAsync(() =>
                HgEnginePokedexMetrics.TryRebuildSortLists(out listed, out string writeError) ? null : writeError);
            if (saved) SaveNotice.Saved($"Pokédex size sorts ({listed} Pokémon)");
            else if (error != null) await DialogHelper.ShowError($"The size sorts were not rebuilt:\n{error}", "Pokédex");
        }

        // ─── Save ─────────────────────────────────────────────────────────────────
        public void SaveChanges() => _ = SaveAsync();

        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            await SaveAsync();
            return !HasUnsavedChanges;
        }

        private async Task SaveAsync()
        {
            if (!HasUnsavedChanges || _currentId < 0) return;
            int species = _currentId;
            var values = _values.Select(v => v?.Clone()).ToArray();
            var areas = SameAreas(_areas, _areasLoaded) ? null : Copy(_areas);
            var footprint = _footprint;

            if (UseHgEngineSource)
            {
                var (saved, error) = await HgEngineSave.RunAsync(() =>
                    !HgEnginePokedexMetrics.TryWrite(species, values[0], out string writeError) ? writeError
                    : areas != null && !HgEnginePokedexAreas.TryWrite(species, areas, out writeError) ? writeError
                    : WriteFootprintOrError());
                if (!saved)
                {
                    if (error == null) return;
                    AppLogger.Error($"hg-engine Pokédex data write failed for species {species}: {error}");
                    await DialogHelper.ShowError($"Pokédex data was not saved:\n{error}", "Pokédex");
                    return;
                }
            }
            else
            {
                string why = values.Where(v => v != null).Select(PokedexDataArchive.WhyNot).FirstOrDefault(w => w != null);
                if (why != null) { await DialogHelper.ShowError($"Pokédex data was not saved: {why}", "Pokédex"); return; }
                for (int i = 0; i < 2; i++)
                    if (values[i] != null && !values[i].SameAs(_loaded[i])) _archive.Set(species, values[i], originGiratina: i == 1);
                _archive.Save();
                if (areas != null) _areaData.Set(species, areas);
                WriteFootprint();
            }

            if (species != _currentId) return;
            _loaded = values.Select(v => v?.Clone()).ToArray();
            if (areas != null) _areasLoaded = areas;
            _footprintLoaded = footprint;
            SaveNotice.Saved(UnsavedChangesDescription);
            _history.MarkSaved();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RaiseUndoState();
        }
    }
}
