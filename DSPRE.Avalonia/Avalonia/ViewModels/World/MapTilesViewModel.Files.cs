using DSPRE.Avalonia.Gl;
using DSPRE.Models;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using LibNDSFormats.NSBTX;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using static DSPRE.RomInfo;
using AvBitmap = global::Avalonia.Media.Imaging.Bitmap;
using LibNDSFormats.NSBMD;
using Avalonia.Platform;
using Avalonia.Media.Imaging;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class MapTilesViewModel
    {
        public event EventHandler PendingChanged;

        public void Open(MapFile map, byte areaId, GameFamilies family)
        {
            _map = map; _areaId = areaId; _family = family;
            _knownModel = map?.MapModelData;
            _set = null;
            _painter.Load(new TileGrid(), null);
            _before.Clear();
            _fromAbove.Clear();
            _thumbs.Clear();
            _pictures = PicturesOfTheArea();
            Tiles.Clear();
            ShownTiles.Clear();
            _tileOrder.Clear();
            SmartDrawings.Clear();
            Slots.Clear();
            Brush = -1; Turn = 0; HeightBrush = 0; ActiveLayer = 0;
            Dirty = false; Warning = null;

            Note = map?.MapModelData == null
                ? "This map has no model."
                : "";

            Rebuild();
            RaiseAll();
            Changed?.Invoke(this, EventArgs.Empty);
            GridChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RaiseAll()
        {
            foreach (string n in new[] { nameof(Ready), nameof(CanUndo), nameof(CanUndoPaint), nameof(CanRedoPaint),
                                         nameof(HasCopy), nameof(SmartIndex), nameof(HasSmartDrawing), nameof(ToolIndex),
                                         nameof(ActiveLayer), nameof(ShowHeights), nameof(HeightBrush), nameof(HeightIndex),
                                         nameof(Brush), nameof(Turn), nameof(SmartTools), nameof(HasOwnPictures), nameof(SetHasMovement) })
                Raise(n);
        }

        private byte[] _knownModel;

        public void Revisit()
        {
            if (_map == null || ReferenceEquals(_map.MapModelData, _knownModel)) return;
            Rebuild();
            Changed?.Invoke(this, EventArgs.Empty);
            if (_set != null)
                Warning = "The model changed in the Shape tab. Applying this grid replaces those changes.";
            _knownModel = _map.MapModelData;
        }

        public void TakeApart()
        {
            if (_map?.MapModelData == null) return;
            _knownModel = _map.MapModelData;

            MapMesh mesh = MapMesh.Read(_map.MapModelData, out string whynot);
            if (mesh == null) { Warning = whynot; return; }
            // Squares are measured at scale 64; a few maps declare another, which would misplace every tile.
            mesh.ScaleTo(TileScale);

            Func<int, string> pictureOf = material =>
            {
                int named = mesh.PictureFor(material);
                return named >= 0 ? mesh.NameOfPicture(named) : $"material{material}";
            };
            Func<int, string> paletteOf = material =>
            {
                int named = mesh.ColoursFor(material);
                return named >= 0 ? mesh.NameOfColours(named) : "";
            };

            MapTileset set = MapTileset.FromMap(mesh, pictureOf, "this map", paletteOf);
            List<MapMesh.Face> left = new List<MapMesh.Face>();
            TileGrid grid = TileGrid.Of(mesh, set, pictureOf, out int unmatched, paletteOf, left);
            int learned = TileCollisions.Learn(grid, set, _map.Types, _map.Collisions);
            Load(set, grid);
            _ripped = grid.Clone();
            FindWalkedLayers(grid);
            _painter.GroundSurface = (x, z, l) => SurfaceOf(_painter.Grid, x, z, l);
            List<MapMesh.Face> cut = new List<MapMesh.Face>();
            Dictionary<MapMesh.Face, List<MapMesh.Face>> split = new Dictionary<MapMesh.Face, List<MapMesh.Face>>();
            Dictionary<MapMesh.Face, MapTileset.Piece> placed = new Dictionary<MapMesh.Face, MapTileset.Piece>();
            MapTileset.PiecesOf(mesh, cut, split, placed);
            _wholeFaces = TileBake.WholeFaces(mesh, cut, pictureOf, paletteOf, split, placed);
            // Faces with no free layer stay until painted over. Cut ones already come back whole.
            HashSet<MapMesh.Face> own = new HashSet<MapMesh.Face>(mesh.Faces);
            _leftFaces = TileBake.WholeFaces(mesh, left.Where(own.Contains), pictureOf, paletteOf);

            Note = $"{_set.Tiles.Count} tiles, {grid.Painted} cells"
                 + (learned > 0 ? $", {learned} with default permissions" : "")
                 + (unmatched > 0 ? $", {left.Count} faces kept as they are (no free layer)" : "");
            Warning = null;
        }

        private TileGrid _ripped;

        // From map cuts ground faces into squares. An unchanged face goes back whole.
        private List<TileBake.WholeFace> _wholeFaces;

        private List<TileBake.WholeFace> _leftFaces;

        private int PutBackUntouched(TileBake.Result baked, ICollection<(int x, int z)> ramps)
        {
            if (_wholeFaces == null || _ripped == null) return 0;
            bool Untouched(TileBake.WholeFace w) => w.Squares.All(q => !ChangedSinceRipped(q.x, q.z) && !ramps.Contains(q));
            return TileBake.PutBackWhole(baked, _wholeFaces.Where(Untouched))
                 + TileBake.PutBackAsTheyWere(baked, (_leftFaces ?? new List<TileBake.WholeFace>()).Where(Untouched));
        }

        private bool ChangedSinceRipped(int x, int z)
        {
            for (int layer = 0; layer < TileGrid.Layers; layer++)
            {
                TileGrid.Square now = _painter.Grid.At(x, z, layer);
                TileGrid.Square was = _ripped.At(x, z, layer);
                if (now.Tile != was.Tile || now.Turn != was.Turn || now.FromX != was.FromX || now.FromZ != was.FromZ
                    || _painter.Grid.HeightAt(x, z, layer) != _ripped.HeightAt(x, z, layer)) return true;
            }
            return false;
        }

        private void Load(MapTileset set, TileGrid grid)
        {
            _ownTiles.Clear();
            if (set != null && set.PictureFiles.Count > 0)
                for (int i = 0; i < set.Tiles.Count; i++) _ownTiles.Add(i);
            _ripped = null;
            _wholeFaces = null;
            _leftFaces = null;
            _walkedLayer = null;
            // A search typed for the last tileset would hide the new one's tiles.
            _tileSearch = "";
            Raise(nameof(TileSearch));
            _ramps.Clear();
            _rampRuns.Clear();
            _tileSource.Clear();
            CollisionUnsaved = false;
            _atLoad = grid?.Clone();
            _set = set;
            _painter.Load(grid, set);
            _fromAbove.Clear();
            _thumbs.Clear();
            _pictures = PicturesOfTheArea();
            foreach (KeyValuePair<string, (byte[] rgba, int w, int h)> kv in PicturesOfTheSet(set)) _pictures.TryAdd(kv.Key, kv.Value);

            FillList();
            FillSmart();
            ShowTile();
            // The Tile tab describes the chosen tile, which a new tileset changes even at the same index.
            CollisionCells.Clear();
            ShowChosen();
            CountLayers();
            RaiseAll();
            Changed?.Invoke(this, EventArgs.Empty);
            GridChanged?.Invoke(this, EventArgs.Empty);
        }

        public void BringIn(string path)
        {
            string extension = Path.GetExtension(path);
            if (extension.Equals(".pdsmap", StringComparison.OrdinalIgnoreCase))
            {
                BringInProject(path);
                return;
            }
            // Anything not a Map Studio or OBJ file is judged by its contents: a model, or a whole map.
            if (!extension.Equals(".pdsts", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".obj", StringComparison.OrdinalIgnoreCase))
            {
                ReplaceFrom(path);
                return;
            }

            bool incoming = Path.GetExtension(path).Equals(".pdsts", StringComparison.OrdinalIgnoreCase);
            MapTileset set = incoming
                ? PdstsFile.Read(path, out string whynot)
                : MapTileset.FromObj(path, out whynot);
            if (set == null) { Warning = whynot; return; }
            if (incoming) TileCollisions.ReadMeta(path, set);

            _project = null;
            ProjectMaps.Clear();
            _saved.Clear();
            Raise(nameof(HasProject));
            Raise(nameof(HasSaved));
            Raise(nameof(SavedNote));

            SizePlaces(set);
            Load(set, new TileGrid());
            if (incoming) CameFrom(path, set);
            Warning = MissingPictures(set);

            int spreading = set.Tiles.Count(t => t.Spreads);
            Note = $"{set.Tiles.Count} tiles, {set.SmartDrawings.Count} smart drawings";
        }

        // Replaces this map's model with a model file, or the whole map with a map file.
        private void ReplaceFrom(string path)
        {
            if (_map == null) return;
            byte[] data;
            try { data = DSUtils.ReadFromFile(path); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { Warning = ex.Message; return; }
            if (MapFile.ReadImport(data, _family, out MapFile read, out byte[] model, out bool converted) is string whynot) { Warning = whynot; return; }

            Remember();
            if (model != null)
            {
                _map.LoadMapModel(model, showMessages: false);
                ImportedSinceOpen = true;
                Replaced($"Model imported, {model.Length:n0} bytes.");
                return;
            }
            _map.TakeContentsOf(read, _family);
            ImportedSinceOpen = true;
            Replaced(converted ? "Map imported and converted from the other game's layout." : "Map imported.");
        }

        private void Remember()
        {
            _before.Add((byte[])_map.ToByteArray().Clone());
            if (_before.Count > 32) _before.RemoveAt(0);
            Raise(nameof(CanUndo));
        }

        private void Replaced(string note)
        {
            _knownModel = _map.MapModelData;
            Dirty = true;
            Rebuild();
            Note = note;
            string missing = null;
            try { missing = MapImportCheck.Note(_map, new AreaData(_areaId).MapTileset); }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException) { }   // without the area data there is no missing-texture note
            string replaces = _set != null ? "Applying the tile grid replaces the imported model." : null;
            Warning = replaces != null && missing != null ? replaces + " " + missing : replaces ?? missing;
            Changed?.Invoke(this, EventArgs.Empty);
            // The building outlines on the grid come from the map, which now holds other buildings.
            SelectedBuilding = -1;
            GridChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SizePlaces(MapTileset set)
        {
            Dictionary<string, (byte[] rgba, int w, int h)> own = PicturesOfTheSet(set);
            Dictionary<string, (byte[] rgba, int w, int h)> area = PicturesOfTheArea();
            set.TurnPlacesIntoDots(name =>
                name == null ? (0, 0)
                : own.TryGetValue(name, out (byte[] rgba, int w, int h) existing) ? (existing.w, existing.h)
                : area.TryGetValue(name, out (byte[] rgba, int w, int h) found) ? (found.w, found.h) : (0, 0));
        }

        private string MissingPictures(MapTileset set)
        {
            // Only placed tiles count. A picture or palette missing from the pack draws pink.
            (HashSet<string> pictures, HashSet<string> palettes) = WhatTheAreaCarries();
            HashSet<int> placed = new HashSet<int>(_painter.Grid?.Placed().Select(p => p.square.Tile) ?? Enumerable.Empty<int>());
            List<MapTileset.Face> faces = placed.Where(t => t >= 0 && t < set.Tiles.Count).SelectMany(t => set.Tiles[t].Faces).ToList();
            List<string> missing = faces.Select(f => f.Picture)
                               .Where(p => !string.IsNullOrEmpty(p) && !pictures.Contains(NitroDictionary.Fit(p)))
                               .Concat(faces.Select(f => f.Palette)
                                            .Where(p => !string.IsNullOrEmpty(p) && !palettes.Contains(NitroDictionary.Fit(p)))
                                            .Select(p => p + " (palette)"))
                               .Distinct().ToList();
            return missing.Count == 0 ? null
                : $"Missing textures: {string.Join(", ", missing.Take(6))}"
                + (missing.Count > 6 ? $" and {missing.Count - 6} more" : "")
                + (set.PictureFiles.Count > 0
                    ? ". Use Add textures to ROM."
                    : "");
        }

        private PdsmapFile.Project _project;
        private string _projectPath;

        private readonly Dictionary<string, string> _saved = new Dictionary<string, string>();

        public bool HasSaved => _saved.Count > 0;

        private bool _alsoSaved = true;
        public bool AlsoSaved { get => _alsoSaved; set => Set(ref _alsoSaved, value); }

        public string SavedNote => _saved.Count == 0 ? "" : "Use PDSMS files ("
            + string.Join(", ", _saved.Keys.Select(k => k switch
            {
                "per" => "permissions", "bld" => "buildings", "bgs" => "BGS", "bdhc" => "BDHC", _ => k,
            })) + ")";

        public ObservableCollection<string> ProjectMaps { get; } = new ObservableCollection<string>();
        public bool HasProject => ProjectMaps.Count > 1;

        private int _projectMap = -1;
        public int ProjectMap
        {
            get => _projectMap;
            set
            {
                if (_project == null || value < 0 || value >= _project.Maps.Count || value == _projectMap) return;
                _projectMap = value;
                Raise();
                PaintProjectMap();
            }
        }

        private void BringInProject(string path)
        {
            PdsmapFile.Project project = PdsmapFile.Read(path, out string whynot);
            if (project == null) { Warning = whynot; return; }
            if (!project.HasTileset)
            {
                Warning = $"Tileset not found: {Path.GetFileName(project.TilesetPath ?? "")}";
                return;
            }

            MapTileset set = PdstsFile.Read(project.TilesetPath, out whynot);
            if (set == null) { Warning = whynot; return; }
            TileCollisions.ReadMeta(project.TilesetPath, set);
            SizePlaces(set);

            _project = project;
            _projectPath = path;
            _projectFamily = FamilyOf(project.Game);
            _projectMadeFor = _projectFamily is GameFamilies made && made != _family ? GameName(project.Game) : null;
            ProjectMaps.Clear();
            foreach (PdsmapFile.Map m in project.Maps) ProjectMaps.Add($"Map {m.X},{m.Y}  ·  area {m.Area}");
            Raise(nameof(HasProject));

            Load(set, new TileGrid());
            CameFrom(project.TilesetPath, set);
            _projectMap = -1;
            ProjectMap = 0;
        }

        private string _projectMadeFor;
        private GameFamilies? _projectFamily;

        // Building ids differ per game and DP terrain has its own format. Permissions fit all games.
        private bool LeavesOut(string kind) => kind switch
        {
            "bld" => _projectMadeFor != null,
            "bdhc" => _projectMadeFor != null && (_family == GameFamilies.DP) != (_projectFamily == GameFamilies.DP),
            _ => false,
        };

        private static GameFamilies? FamilyOf(int pdsmsGame) => pdsmsGame switch
        {
            0 or 1 => GameFamilies.DP,
            2 => GameFamilies.Plat,
            3 or 4 => GameFamilies.HGSS,
            _ => null,
        };

        private static string GameName(int pdsmsGame) => pdsmsGame switch
        {
            0 => "Diamond", 1 => "Pearl", 2 => "Platinum", 3 => "HeartGold", 4 => "SoulSilver", _ => "another game",
        };

        private void PaintProjectMap()
        {
            PdsmapFile.Map map = _project.Maps[_projectMap];

            _saved.Clear();
            foreach (KeyValuePair<string, string> part in PdsmapFile.SavedParts(_projectPath, map, _family == GameFamilies.HGSS)) _saved[part.Key] = part.Value;
            Raise(nameof(HasSaved));
            Raise(nameof(SavedNote));
            TileGrid grid = PdsmapFile.ToGrid(map, _set, out int dropped);
            int smart = _painter.SmartIndex;
            _painter.Load(grid, _set);
            _atLoad = grid.Clone();
            _painter.SmartIndex = Math.Min(smart, Math.Max(0, _set.SmartDrawings.Count - 1));
            Edited();
            SortTileList();

            string missing = MissingPictures(_set);
            Warning = dropped > 0
                ? $"{dropped} tiles skipped."
                  + (missing != null ? " " + missing : "")
                : missing;
            if (_saved.Keys.Any(LeavesOut))
                Warning = $"Made for {_projectMadeFor}: its "
                        + string.Join(" and ", _saved.Keys.Where(LeavesOut).Select(k => k == "bld" ? "buildings" : "terrain"))
                        + " are left out, as they differ in this game." + (Warning != null ? " " + Warning : "");
            Note = $"Map {map.X},{map.Y}: {grid.Painted} cells on {Enumerable.Range(0, TileGrid.Layers).Count(grid.HasAnything)} layers";
        }

        // Tiles read from a PDSMS tileset, with the file and position their collision is saved to.
        private readonly Dictionary<MapTileset.Tile, (string path, int listed)> _tileSource =
            new Dictionary<MapTileset.Tile, (string, int)>(ReferenceEqualityComparer.Instance);

        private void CameFrom(string path, MapTileset set)
        {
            foreach ((MapTileset.Tile tile, int listed) in TileCollisions.ListedPlaces(set))
                _tileSource[tile] = (path, listed);
            Raise(nameof(ChosenFromTileset));
        }

        private bool _collisionUnsaved;
        public bool CollisionUnsaved { get => _collisionUnsaved; private set => Set(ref _collisionUnsaved, value); }

        public bool ChosenFromTileset => Chosen != null && _tileSource.ContainsKey(Chosen);

        /// <summary>Writes collision defaults of PDSMS tiles into each tileset's .pdsts.meta.</summary>
        public void SaveCollisionToTileset()
        {
            int written = 0;
            foreach (IGrouping<string, KeyValuePair<MapTileset.Tile, (string path, int listed)>> file in _tileSource.GroupBy(kv => kv.Value.path, StringComparer.OrdinalIgnoreCase))
            {
                int n = TileCollisions.WriteMeta(file.Key, file.Select(kv => (kv.Key, kv.Value.listed)), out string whynot);
                if (n < 0) { Warning = $"Couldn't write {Path.GetFileName(file.Key)}.meta: {whynot}"; return; }
                written += n;
            }
            CollisionUnsaved = false;
            Note = $"Collision of {written} tiles saved to the tileset.";
        }

        public string SaveTo(string path)
        {
            if (_set == null || _set.Tiles.Count == 0) return "No tiles to export.";

            string why = _set.SaveObj(path);
            if (why != null) { Warning = why; return why; }

            Warning = null;
            Note = $"Exported {_set.Tiles.Count} tiles.";
            return null;
        }

        /// <summary>Area data ids of every header showing the map; the map editor fills this in.</summary>
        public Func<List<byte>> AreasOfMap { get; set; }

        public bool HasOwnPictures => _set != null && _set.PictureFiles.Count > 0;
    }
}
