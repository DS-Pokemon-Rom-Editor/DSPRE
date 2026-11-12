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
        private void SetChanged(bool redraw)
        {
            Dirty = true;
            if (redraw)
            {
                _fromAbove.Clear();
                _thumbs.Clear();
            }
            else if (Brush >= 0)
            {
                _thumbs.Remove(Brush);
                foreach ((int tile, int turn) key in _fromAbove.Keys.Where(k => k.tile == Brush).ToList()) _fromAbove.Remove(key);
            }
            FillList();
            FillSlots();
            ShowChosen();
            ShowTile();
            CountLayers();
            GridChanged?.Invoke(this, EventArgs.Empty);
            Preview();
        }

        private void Preview()
        {
            if (_set == null || _map == null) return;

            TileBake.Result baked = TileBake.Of(_painter.Grid, _set);
            if (baked.Whynot != null) { Warning = baked.Whynot; Model3D = null; Raise(nameof(Model3D)); Changed?.Invoke(this, EventArgs.Empty); return; }
            PutBackUntouched(baked, _ramps);
            TileBake.JoinFlat(baked);

            byte[] model = TileBake.ToModel(baked, out string whynot, TileScale);
            if (model == null) { Warning = whynot; return; }

            try
            {
                MapFile shown = new MapFile(new MemoryStream(_map.ToByteArray()), _family,
                                        discardMoveperms: false, showMessages: false);
                shown.LoadMapModel(model, showMessages: false);
                shown.MapModelData = model;

                Model3D = MatrixSceneBuilder.BuildFromPlaced(_family,
                    new[] { (0, 0, shown, _areaId, 0f, 0f, 0f) },
                    NsbmdGeometry.MatrixStitchMode.Grid);
            }
            catch (Exception ex) { AppLogger.Error("MapTiles.Preview: " + ex.Message); }

            Note = $"{_painter.Grid.Painted} cells  ·  {baked.Faces.Count} faces  ·  "
                 + $"{baked.Triangles} triangles  ·  {model.Length:n0} bytes";
            Raise(nameof(Model3D));
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void Rebuild()
        {
            Model3D = null;
            if (_map == null) return;
            try
            {
                Model3D = MatrixSceneBuilder.BuildFromPlaced(_family,
                    new[] { (0, 0, _map, _areaId, 0f, 0f, 0f) },
                    NsbmdGeometry.MatrixStitchMode.Grid);
            }
            catch (Exception ex) { AppLogger.Error("MapTiles.Rebuild: " + ex.Message); }
        }

        // Squares whose tiles changed since From map. Null after an import or a new set: all count.
        private Func<int, int, bool> ChangedSquares => _ripped == null ? null : (c, r) => ChangedSinceRipped(c, r);

        public (int newBuildings, int currentBuildings)? BuildingsApplyWouldReplace()
        {
            if (_map?.Buildings == null || _map.Buildings.Count == 0) return null;
            if (!AlsoSaved || !_saved.TryGetValue("bld", out string bld) || LeavesOut("bld")) return null;
            try
            {
                byte[] newBuildings = DSUtils.ReadFromFile(bld);
                if (newBuildings.AsSpan().SequenceEqual(_map.BuildingsToByteArray())) return null;
                return (newBuildings.Length / MapFile.BuildingHeaderSize, _map.Buildings.Count);
            }
            catch (Exception ex) { AppLogger.WarnOnce("MapTilesViewModel.BuildingsApplyWouldReplace", ex); return null; }
        }

        /// <summary>How many of the map's current plates Apply's terrain rebuild would replace.</summary>
        public int PlatesApplyWouldReplace()
        {
            if (_map?.Bdhc == null || _set == null || !AlsoTerrain || (AlsoSaved && _saved.ContainsKey("bdhc") && !LeavesOut("bdhc"))) return 0;
            TileBake.Result baked = TileBake.Of(_painter.Grid, _set);
            if (baked.Whynot != null) return 0;
            PutBackUntouched(baked, _ramps);
            TileBake.JoinFlat(baked);
            if (AlsoWalls) TileBake.AddWalls(baked, _painter.Grid, _set, HeightChanged, _ramps, null,
                                             (x, z) => WalkedLayer(_painter.Grid, x, z), (x, z, l) => SurfaceOf(_painter.Grid, x, z, l));
            byte[] model = TileBake.ToModel(baked, out _, TileScale);
            MapMesh mesh = model == null ? null : MapMesh.Read(model, out _);
            if (mesh == null) return 0;
            Func<int, int, bool> changed = ChangedSquares;
            Func<int, int, bool> changedOrRamp = changed == null ? null : (c, r) => changed(c, r) || _ramps.Contains((c, r));
            return BdhcBuild.Replaced(_map.Bdhc, BdhcBuild.Propose(mesh, _map.Collisions, _map.Bdhc, _map.KeptPlates, changedOrRamp,
                                                                       WantedGround(new HashSet<(int x, int z)>(_ramps))));
        }

        /// <summary>True once a PDSMS map or a map file came in since the window opened.</summary>
        public bool ImportedSinceOpen { get; set; }

        // The height a changed square is walked at, in map units: its ground tile or a ramp's middle.
        private Func<int, int, float?> WantedGround(ISet<(int x, int z)> ramps)
        {
            TileGrid grid = _painter.Grid;
            float toUnits = 16f / MapTileset.TileWidth;
            float? Ground(int x, int z)
            {
                if (x < 0 || z < 0 || x >= TileGrid.Across || z >= TileGrid.Across) return null;
                int l = GroundLayer(grid, x, z);
                if (l < 0) return null;
                return SurfaceOf(grid, x, z, l);
            }
            return (c, r) =>
            {
                if (_ripped != null && !ChangedSinceRipped(c, r) && !ramps.Contains((c, r))) return null;
                if (Ground(c, r) is not float h) return null;
                if (ramps.Contains((c, r)))
                {
                    float up = h;
                    foreach ((int dx, int dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        if (Ground(c + dx, r + dz) is float n && n > up) up = n;
                    h = (h + up) / 2f;
                }
                return (grid.BaseLift + h) * toUnits;
            };
        }

        public void Apply(bool bringInBuildings = true)
        {
            if (_map == null || _set == null) return;
            if (_project != null) ImportedSinceOpen = true;

            List<(int x, int z)> cannot = new List<(int x, int z)>();
            HashSet<(int x, int z)> ramps = RampsToBuild(out TileGrid bakeGrid, cannot);
            TileBake.Result baked = TileBake.Of(bakeGrid, _set);
            if (baked.Whynot != null) { Warning = baked.Whynot; return; }
            PutBackUntouched(baked, ramps);
            TileBake.JoinFlat(baked);
            List<TileRamps.Run> runs = _rampRuns.Select(run => new TileRamps.Run
            {
                Squares = new HashSet<(int x, int z)>(run.Squares.Where(ramps.Contains)),
                Kind = run.Kind, Fill = run.Fill, Template = run.Template,
            }).Where(run => run.Squares.Count > 0).ToList();
            TileRamps.Report laid = TileRamps.Lay(baked, _painter.Grid, runs, GroundUnder);
            List<(int x, int z)> belowWalls = new List<(int x, int z)>();
            int walls = AlsoWalls ? TileBake.AddWalls(baked, _painter.Grid, _set, HeightChanged, ramps, belowWalls,
                                                      (x, z) => WalkedLayer(_painter.Grid, x, z), (x, z, l) => SurfaceOf(_painter.Grid, x, z, l)) : 0;

            byte[] model = TileBake.ToModel(baked, out string whynot, TileScale);
            if (model == null) { Warning = whynot; return; }

            Remember();

            if (MapFile.TooBigForTheGame(model.Length, 0) is string tooBig) { Warning = tooBig + " Remove detail or split the map."; return; }
            _map.LoadMapModel(model, showMessages: false);
            _map.MapModelData = model;
            _knownModel = model;

            List<string> brought = new List<string>();
            (bool savedGround, bool savedMovement) = BringInSaved(bringInBuildings, brought);

            int stamped = AlsoMovement && SetHasMovement && !savedMovement
                ? TileCollisions.Apply(_painter.Grid, _set, _map.Types, _map.Collisions,
                                        _ripped == null ? null : ChangedSinceRipped) : 0;

            int blocked = BlockCliffsOpenRamps(belowWalls, ramps);
            if (AlsoTerrain && !savedGround) blocked += RebuildTerrain(model, ramps, laid.Plates);

            Dirty = true;
            if (brought.Count == 0 || Warning?.StartsWith("What Map Studio") != true) Warning = null;
            WarnNotBuilt(laid.Skipped, cannot);
            Rebuild();
            Note = $"Applied: {baked.Faces.Count} faces, {model.Length:n0} bytes"
                 + (laid.Made > 0 ? $", {laid.Made} ramps" : "")
                 + (walls > 0 ? $", {walls} walls" : "")
                 + (blocked > 0 ? $", {blocked} squares blocked" : "")
                 + (AlsoTerrain && !savedGround ? ", terrain rebuilt" : "")
                 + (stamped > 0 ? $", {stamped} collision values" : "")
                 + (brought.Count > 0 ? ", PDSMS " + string.Join(", ", brought) : "");
            Changed?.Invoke(this, EventArgs.Empty);
        }

        // Ramp squares lose their flat ground so a slope can be laid there.
        private HashSet<(int x, int z)> RampsToBuild(out TileGrid bakeGrid, List<(int x, int z)> cannot)
        {
            bakeGrid = _painter.Grid;
            HashSet<(int x, int z)> ramps = new HashSet<(int x, int z)>();
            foreach ((int x, int z) in _ramps)
            {
                int l = GroundLayer(_painter.Grid, x, z);
                TileGrid.Square sq = l < 0 ? default : _painter.Grid.At(x, z, l);
                // A ramp replaces its square's ground, which a tile spanning several squares cannot give up alone.
                if (l < 0 || sq.Wide > 1 || sq.Deep > 1) { cannot.Add((x, z)); continue; }
                if (ReferenceEquals(bakeGrid, _painter.Grid)) bakeGrid = _painter.Grid.Clone();
                bakeGrid.Clear(x, z, l);
                ramps.Add((x, z));
            }
            return ramps;
        }

        private (float, MapTileset.Face) GroundUnder(int x, int z)
        {
            int l = GroundLayer(_painter.Grid, x, z);
            if (l < 0) return (0f, null);
            TileGrid.Square sq = _painter.Grid.At(x, z, l);
            MapTileset.Tile tile = _set.Tiles[sq.Tile];
            float height = SurfaceOf(_painter.Grid, x, z, l), top = height - sq.Lift;
            // Use the ground face, not a decal a sliver above it or a hidden surface below.
            MapTileset.Face skin = tile.GroundFace ?? tile.Faces.OrderBy(f => f.Corners.Max(i => Math.Abs(tile.Corners[i].Y - top))).FirstOrDefault();
            return (height, skin);
        }

        private (bool ground, bool movement) BringInSaved(bool bringInBuildings, List<string> brought)
        {
            bool savedGround = false, savedMovement = false;
            if (!AlsoSaved || _saved.Count == 0) return (false, false);
            try
            {
                if (_saved.TryGetValue("per", out string per)) { _map.ImportPermissions(DSUtils.ReadFromFile(per)); savedMovement = true; brought.Add("permissions"); }
                if (_saved.TryGetValue("bgs", out string bgs)) { _map.ImportSoundPlates(DSUtils.ReadFromFile(bgs)); brought.Add("BGS"); }
                if (_saved.TryGetValue("bdhc", out string bdhc) && !LeavesOut("bdhc")) { _map.ImportTerrain(DSUtils.ReadFromFile(bdhc)); savedGround = true; brought.Add("BDHC"); }
                if (_saved.TryGetValue("bld", out string bld) && !LeavesOut("bld"))
                {
                    if (bringInBuildings)
                    {
                        _map.ImportBuildings(DSUtils.ReadFromFile(bld));
                        brought.Add($"{_map.Buildings.Count} buildings");
                    }
                }
            }
            catch (Exception ex) when (AppLogger.Noted("MapTilesViewModel.Apply", ex)) { Warning = "Couldn't read PDSMS files: " + ex.Message; }
            return (savedGround, savedMovement);
        }

        // A wall is a cliff: nobody walks up or down it, so the square at its foot is blocked.
        private int BlockCliffsOpenRamps(List<(int x, int z)> belowWalls, HashSet<(int x, int z)> ramps)
        {
            int cliffs = 0;
            foreach ((int x, int z) in belowWalls.Distinct())
                if ((_map.Collisions[z, x] & 0x80) == 0) { _map.Collisions[z, x] = 0x80; cliffs++; }
            foreach ((int x, int z) in ramps)
                if ((_map.Collisions[z, x] & 0x80) != 0) _map.Collisions[z, x] = 0x00;
            return cliffs;
        }

        private int RebuildTerrain(byte[] model, HashSet<(int x, int z)> ramps, IEnumerable<BdhcBuild.Piece> rampPlates)
        {
            MapMesh mesh = MapMesh.Read(model, out _);
            if (mesh == null) return 0;
            Func<int, int, bool> changed = ChangedSquares;
            Func<int, int, bool> changedOrRamp = changed == null ? null : (c, r) => changed(c, r) || ramps.Contains((c, r));
            // Ramps and stairs are walked on the plates laid with them, whatever steps are drawn.
            List<BdhcBuild.Piece> kept = (_map.KeptPlates ?? new List<BdhcBuild.Piece>()).Concat(rampPlates).ToList();
            byte[] terrain = BdhcBuild.ForMap(mesh, _map.Collisions, _map.Bdhc, out _, kept, changedOrRamp, WantedGround(ramps));
            if (terrain != null) _map.ImportTerrain(terrain);
            return BdhcBuild.BlockUngrounded(_map.Bdhc, _map.Collisions);
        }

        private void WarnNotBuilt(IEnumerable<string> skipped, List<(int x, int z)> cannot)
        {
            List<string> notBuilt = new List<string>(skipped);
            if (cannot.Count > 0)
                notBuilt.Add($"{cannot.Count} ramp square{(cannot.Count > 1 ? "s" : "")} ({string.Join(" ", cannot.Take(6).Select(c => $"{c.x},{c.z}"))}"
                           + $"{(cannot.Count > 6 ? " ..." : "")}) with no ground or on a tile wider than a square");
            if (notBuilt.Count > 0) Warning = "Not built: " + string.Join("; ", notBuilt) + ".";
            string bare = BuildingPicturesMissing();
            if (bare != null) Warning = Warning == null ? bare : Warning + " " + bare;
        }

        public void Undo()
        {
            if (_before.Count == 0 || _map == null) return;

            byte[] whole = _before[_before.Count - 1];
            _before.RemoveAt(_before.Count - 1);
            Raise(nameof(CanUndo));

            try
            {
                MapFile was = new MapFile(new MemoryStream(whole), _family,
                                      discardMoveperms: false, showMessages: false);
                _map.LoadMapModel(was.MapModelData, showMessages: false);
                _map.MapModelData = was.MapModelData;
                _knownModel = was.MapModelData;
                _map.ImportTerrain(was.Bdhc);
                Array.Copy(was.Types, _map.Types, was.Types.Length);
                Array.Copy(was.Collisions, _map.Collisions, was.Collisions.Length);
                _map.Buildings = was.Buildings;
                _map.Bgs = was.Bgs;
            }
            catch (Exception ex) when (AppLogger.Noted("MapTilesViewModel.Undo", ex)) { Warning = ex.Message; return; }

            Dirty = _before.Count > 0;
            Rebuild();
            Note = "Reverted.";
            Changed?.Invoke(this, EventArgs.Empty);
            GridChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
