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
        private static readonly Dictionary<(string dir, uint id), (float x0, float x1, float z0, float z1, float y0, float y1)?> ModelExtent =
            new Dictionary<(string, uint), (float, float, float, float, float, float)?>();

        // Indoor HGSS maps take their building models from the interior archive.
        private string BuildingModelsDir()
        {
            DirNames models = DirNames.ExteriorBuildingModels;
            if (_family == GameFamilies.HGSS)
                try { models = new AreaData(_areaId).BuildingModels; } catch (Exception caught) when (AppLogger.Noted("MapTilesViewModel.BuildingModelsDir", caught)) { }   // without the area data the area is treated as outdoors
            if (models == DirNames.InteriorBuildingModels) DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.InteriorBuildingModels });
            return GameDirs[models].unpackedDir;
        }

        private string BuildingPicturesMissing()
        {
            if (_map?.Buildings == null || _map.Buildings.Count == 0) return null;
            try
            {
                AreaData area = new AreaData(_areaId);
                List<MapImportCheck.BuildingTrouble> found = MapImportCheck.Buildings(_map, _areaId, _family);
                if (found.Count == 0) return null;
                SortedDictionary<uint, List<string>> bad = new SortedDictionary<uint, List<string>>(
                    found.ToDictionary(t => t.Model, t => t.NoSuchModel ? new List<string> { "no such model" } : t.Lacking));
                return $"These buildings draw pink because building pack {area.BuildingsTileset} of area {_areaId} lacks their textures: "
                     + string.Join("; ", bad.Take(4).Select(kv => $"{kv.Key} ({string.Join(", ", kv.Value.Take(3))})"))
                     + (bad.Count > 4 ? $" and {bad.Count - 4} more" : "") + ".";
            }
            catch (Exception ex) { AppLogger.Error("MapTiles.BuildingPictures: " + ex.Message); return null; }
        }

        /// <summary>Squares the map's buildings stand on, so painting under one is no surprise.</summary>
        public List<(int index, double x0, double z0, double x1, double z1)> BuildingSquares()
        {
            List<(int, double, double, double, double)> found = new List<(int, double, double, double, double)>();
            if (_map?.Buildings == null) return found;
            int number = -1;
            string dir;
            try { dir = BuildingModelsDir(); } catch (Exception ex) { AppLogger.WarnOnce("MapTilesViewModel.BuildingSquares", ex); return found; }
            foreach (Building b in _map.Buildings)
            {
                number++;
                if (!ModelExtent.TryGetValue((dir, b.ModelID), out (float x0, float x1, float z0, float z1, float y0, float y1)? extent))
                {
                    extent = null;
                    try
                    {
                        byte[] model = ArchiveMembers.ReadOrNull(Path.Combine(dir, b.ModelID.ToString("D4")));
                        MapMesh mesh = model != null ? MapMesh.Read(model, out _) : null;
                        if (mesh != null && mesh.Vertices.Count > 0)
                        {
                            float s = mesh.ModelScale == 0f ? 1f : mesh.ModelScale;
                            extent = (mesh.Vertices.Min(v => v.X) * s, mesh.Vertices.Max(v => v.X) * s,
                                      mesh.Vertices.Min(v => v.Z) * s, mesh.Vertices.Max(v => v.Z) * s,
                                      mesh.Vertices.Min(v => v.Y) * s, mesh.Vertices.Max(v => v.Y) * s);
                        }
                    }
                    catch (Exception caught) when (AppLogger.Noted("MapTilesViewModel.BuildingSquares.2", caught)) { }   // a model that can't be read has no extent
                    ModelExtent[(dir, b.ModelID)] = extent;
                }
                if (extent is not (float ex0, float ex1, float ez0, float ez1, float ey0, float ey1)) continue;
                // Sizes are scale factors where 16 is one; a quarter turn about y swaps across and down.
                double sx = Math.Max(1u, b.Width) / 16.0, sz = Math.Max(1u, b.Length) / 16.0, sy = Math.Max(1u, b.Height) / 16.0;
                double a0 = ex0 * sx / 16, a1 = ex1 * sx / 16, c0 = ez0 * sz / 16, c1 = ez1 * sz / 16;
                // Map-wide scenery (HGSS wind, shadows, lake surfaces, sky domes) would cover the grid.
                double across = a1 - a0, down = c1 - c0, tall = (ey1 - ey0) * sy / 16;
                if ((tall < 1 && across >= TileGrid.Across / 2 && down >= TileGrid.Across / 2)
                    || across > TileGrid.Across + 1 || down > TileGrid.Across + 1) continue;
                int quarter = (int)Math.Round(Building.U16ToDeg(b.YRotation) / 90.0) & 3;
                if (quarter == 1 || quarter == 3) (a0, a1, c0, c1) = (c0, c1, -a1, -a0);
                if (quarter == 2) (a0, a1, c0, c1) = (-a1, -a0, -c1, -c0);
                double cx = TileGrid.Across / 2 + b.XPosition + b.XFraction / 65536.0;
                double cz = TileGrid.Across / 2 + b.ZPosition + b.ZFraction / 65536.0;
                found.Add((number, cx + a0, cz + c0, cx + a1, cz + c1));
            }
            return found;
        }

        private int _selectedBuilding = -1;
        public int SelectedBuilding
        {
            get => _selectedBuilding;
            set { if (Set(ref _selectedBuilding, value)) GridChanged?.Invoke(this, EventArgs.Empty); }
        }

        /// <summary>Moves warps at or in front of a moved building. Returns how many moved.</summary>
        public Func<(double x0, double z0, double x1, double z1), int, int, int> WarpsFollow { get; set; }

        private (short x, short z, (double x0, double z0, double x1, double z1) was)? _buildingAtPress;

        public void PressBuilding(int index)
        {
            if (_map?.Buildings == null || index < 0 || index >= _map.Buildings.Count) return;
            SelectedBuilding = index;
            Building b = _map.Buildings[index];
            (int index, double x0, double z0, double x1, double z1) rect = BuildingSquares().FirstOrDefault(r => r.index == index);
            _buildingAtPress = (b.XPosition, b.ZPosition, (rect.x0, rect.z0, rect.x1, rect.z1));
            Note = $"Building {index:D2} · model {b.ModelID} · {b.XPosition},{b.ZPosition}";
        }

        public void DragBuilding(int index, int dx, int dz)
        {
            if (_buildingAtPress is not (short x, short z, _) || index != _selectedBuilding) return;
            Building b = _map.Buildings[index];
            b.XPosition = (short)(x + dx);
            b.ZPosition = (short)(z + dz);
            GridChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ReleaseBuilding(int index, int dx, int dz)
        {
            if (_buildingAtPress is not (_, _, (double x0, double z0, double x1, double z1) was) || index != _selectedBuilding) return;
            _buildingAtPress = null;
            if (dx == 0 && dz == 0) return;
            DragBuilding(index, dx, dz);
            Dirty = true;
            int moved = MovePermissions(was, dx, dz);
            int warps = WarpsFollow?.Invoke(was, dx, dz) ?? 0;
            Building b = _map.Buildings[index];
            Note = $"Moved building {index:D2} to {b.XPosition},{b.ZPosition}"
                 + (moved > 0 ? $", its {moved} blocked squares" : "")
                 + (warps > 0 ? $" and {warps} warp{(warps > 1 ? "s" : "")}." : ".")
                 + " Save from the Map editor.";
        }

        private int MovePermissions((double x0, double z0, double x1, double z1) was, int dx, int dz)
        {
            if (_map?.Collisions == null || _map.Types == null) return 0;
            int n = TileGrid.Across;
            // Outlines and blocked squares rarely line up, so touching blocked squares join in.
            bool Within(int x, int z, double reach)
                => x + 0.5 >= was.x0 - reach && x + 0.5 <= was.x1 + reach && z + 0.5 >= was.z0 - reach && z + 0.5 <= was.z1 + reach;
            bool Used(int x, int z) => _map.Collisions[z, x] != 0 || _map.Types[z, x] != 0;
            HashSet<(int x, int z)> taken = new HashSet<(int x, int z)>();
            Queue<(int x, int z)> queue = new Queue<(int x, int z)>();
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                    if (Within(x, z, 0.6) && Used(x, z) && taken.Add((x, z))) queue.Enqueue((x, z));
            while (queue.Count > 0)
            {
                (int qx, int qz) = queue.Dequeue();
                foreach ((int ox, int oz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int x = qx + ox, z = qz + oz;
                    if (x < 0 || z < 0 || x >= n || z >= n || !Within(x, z, 1.2) || !Used(x, z) || !taken.Add((x, z))) continue;
                    queue.Enqueue((x, z));
                }
            }
            List<(int x, int z, byte walk, byte type)> carried = taken.Select(q => (q.x, q.z, walk: _map.Collisions[q.z, q.x], type: _map.Types[q.z, q.x])).ToList();
            foreach ((int x, int z, byte _, byte _) in carried) { _map.Collisions[z, x] = 0; _map.Types[z, x] = 0; }
            int moved = 0;
            foreach ((int x, int z, byte walk, byte type) in carried)
            {
                int tx = x + dx, tz = z + dz;
                if (tx < 0 || tz < 0 || tx >= n || tz >= n) continue;
                _map.Collisions[tz, tx] = walk;
                _map.Types[tz, tx] = type;
                moved++;
            }
            return moved;
        }

        public void OverBuilding(int? index)
        {
            if (index is not int i || _map?.Buildings == null || i >= _map.Buildings.Count) return;
            Building b = _map.Buildings[i];
            Under = $"Building {i:D2}  ·  model {b.ModelID}  ·  at {b.XPosition},{b.ZPosition}  ·  drag the outline to move it";
        }
    }
}
