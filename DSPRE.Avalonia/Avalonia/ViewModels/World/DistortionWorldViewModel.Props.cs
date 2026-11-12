using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using LibNDSFormats.NSBMD;
using DSPRE.Editors;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using System.IO;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class DistortionWorldViewModel
    {
        public (float x, float y, float z) TileFoot(float tileX, float tileZ)
        {
            NsbmdRenderModel scene = Model3D;
            if (scene == null) return (0f, 0f, 0f);

            if (RideActive && (int)Math.Floor(tileX) == _rideTile.x && (int)Math.Floor(tileZ) == _rideTile.z)
            {
                return scene.ToNormalized(
                    _rideFootRaw.x + (_ride.X - _ride.StartX) * NsbmdGeometry.TileSize,
                    _rideFootRaw.y + (_ride.Y - _ride.StartY) * (NsbmdGeometry.TileSize / 2f),
                    _rideFootRaw.z + (_ride.Z - _ride.StartZ) * NsbmdGeometry.TileSize);
            }

            (float x, float y, float z)? raw = RawFoot(tileX, tileZ);
            return raw == null ? (0f, 0f, 0f) : scene.ToNormalized(raw.Value.x, raw.Value.y, raw.Value.z);
        }

        private (float x, float y, float z)? RawFoot(float tileX, float tileZ)
        {
            NsbmdRenderModel scene = Model3D;
            if (scene == null || !TrySurface(tileX, tileZ, out TornWorldSurfaces.Surface surface, out int col, out int row)) return null;

            float acrossCol = tileX - (float)Math.Floor(tileX);
            float acrossRow = tileZ - (float)Math.Floor(tileZ);

            (int x0, int y0, int z0) = surface.WorldAt(col, row);
            (int x1, int y1, int z1) = surface.WorldAt(col + 1, row);
            (int x2, int y2, int z2) = surface.WorldAt(col, row + 1);

            float worldX = x0 + (x1 - x0) * acrossCol + (x2 - x0) * acrossRow;
            float worldY = y0 + (y1 - y0) * acrossCol + (y2 - y0) * acrossRow;
            float worldZ = z0 + (z1 - z0) * acrossCol + (z2 - z0) * acrossRow;

            if (_crossing != null && CurrentFrame != null
                && (int)Math.Floor(tileX) == _crossing.ToTileX && (int)Math.Floor(tileZ) == _crossing.ToTileZ)
            {
                float through = _crossing.Through(CurrentFrame());
                if (through >= 1f) _crossing = null;
                else if (through >= 0f && TrySurface(_crossing.FromTileX, _crossing.FromTileZ, out TornWorldSurfaces.Surface was,
                                                    out int wasCol, out int wasRow))
                {
                    (int fx, int fy, int fz) = was.WorldAt(wasCol, wasRow);
                    worldX = fx + (worldX - fx) * through;
                    worldY = fy + (worldY - fy) * through;
                    worldZ = fz + (worldZ - fz) * through;
                }
            }

            float rawX = (worldX + 0.5f) * NsbmdGeometry.TileSize;
            float rawZ = (worldZ + 0.5f) * NsbmdGeometry.TileSize;
            float rawY = worldY * (NsbmdGeometry.TileSize / 2f);

            if (surface.IsGround)
            {
                int patchX = (int)Math.Floor(tileX / MapFile.MapSize);
                int patchY = (int)Math.Floor(tileZ / MapFile.MapSize);
                if (scene.TryBdhcSurfaceY(patchX, patchY, rawX, rawZ, rawY, out float surfaceY)) rawY = surfaceY;
            }

            return (rawX, rawY, rawZ);
        }

        private sealed class PlacedProp
        {
            public int ModelId;
            public uint Header;
            public TornWorldCodeTables.MovingPlatform Platform;
            public long GhostGroup = -1;
            public bool Carried;
            public int HomeX, HomeY, HomeZ;
        }

        private readonly Dictionary<int, PlacedProp> _placed = new Dictionary<int, PlacedProp>();

        private List<(int cellX, int cellY, PlacedBuilding placed)> StandingModels(IReadOnlyList<FloorRow> floors)
        {
            List<(int, int, PlacedBuilding)> standing = new List<(int, int, PlacedBuilding)>();

            TornWorldCodeTables.Tables tables = TornWorldCodeTables.Read(out string why);
            if (tables == null)
            {
                if (why != null) AppLogger.Error("DistortionWorld.StandingModels: " + why);
                return standing;
            }

            for (int index = 0; index < floors.Count; index++)
            {
                uint header = (uint)floors[index].Floor.HeaderId;
                foreach (TornWorldCodeTables.MovingPlatform platform in tables.PlatformsOn(header))
                    Place(standing, tables, floors[index].Floor, index, platform.PropKind,
                          platform.TileX, platform.TileY, platform.TileZ,
                          new PlacedProp { Header = header, Platform = platform });
                foreach (TornWorldCodeTables.Prop prop in tables.PropsOn(header))
                    Place(standing, tables, floors[index].Floor, index, prop.PropKind,
                          prop.TileX, prop.TileY, prop.TileZ, new PlacedProp { Header = header });
                foreach (TornWorldFile.GhostProp ghost in floors[index].Data.GhostProps)
                    Place(standing, tables, floors[index].Floor, index, ghost.PropKind,
                          ghost.TileX, ghost.TileY, ghost.TileZ,
                          new PlacedProp { Header = header, GhostGroup = ghost.GroupId });

                // A floor loaded mid-ride gets the ridden platform at the start of its current path.
                if (RideActive && _ride.Header == header)
                {
                    int id = Place(standing, tables, floors[index].Floor, index, _ride.Platform.PropKind,
                                   _ride.PathStartX, _ride.PathStartY, _ride.PathStartZ,
                                   new PlacedProp { Header = header, Carried = true });
                    if (id != 0)
                    {
                        _rideAnchor = id;
                        _rideAnchorHome = (_ride.PathStartX, _ride.PathStartY, _ride.PathStartZ);
                    }
                }
            }

            StandingCount = standing.Count;
            return standing;
        }

        private int Place(List<(int, int, PlacedBuilding)> standing, TornWorldCodeTables.Tables tables,
            TornWorldMapTable.Floor floor, int floorIndex, int propKind, int tileX, int tileY, int tileZ,
            PlacedProp info)
        {
            NSBMDModel model = PropModel(tables.ModelFor(propKind));
            if (model == null) return 0;
            if (!TryTile(floorIndex, floor, tileX, tileZ, out _, out _, out int col, out int row, out float altitude)) return 0;

            int modelId = TravellingId();
            info.ModelId = modelId;
            info.HomeX = tileX; info.HomeY = tileY; info.HomeZ = tileZ;
            _placed[modelId] = info;
            RegisterMotion(tables, propKind, modelId, info.Platform != null || info.Carried);

            (float offX, float offY, float offZ) = tables.OffsetFor(propKind);
            float unit = NsbmdGeometry.TileSize / 16f;

            float x = (col + 0.5f) * NsbmdGeometry.TileSize - NsbmdGeometry.MapStride / 2f + offX * unit;
            float z = (row + 0.5f) * NsbmdGeometry.TileSize - NsbmdGeometry.MapStride / 2f + offZ * unit;
            float y = HeightOf(tileY) - altitude + offY * unit;

            // sPropScaleByKind only sizes the culling box (IsPropInView); props are drawn unscaled.
            float scale = (model.ModelScale == 0 ? 1f : model.ModelScale) / 64f;
            float[] transform = Mat4.Multiply(Mat4.Scale(scale, scale, scale),
                                          Mat4.Translate(x / scale, y / scale, z / scale));

            int gridX = floorIndex * GridBand + (tileX - floor.OffsetX) / MapFile.MapSize;
            int gridY = (tileZ - floor.OffsetZ) / MapFile.MapSize;

            if (info.Platform != null && info.Platform.IsElevator)
                _elevatorTiles[(gridX * MapFile.MapSize + col, gridY * MapFile.MapSize + row)] = info;

            standing.Add((gridX, gridY, new PlacedBuilding
            {
                Model = model,
                Transform = transform,
                ModelId = modelId,
                TileX = gridX * MapFile.MapSize + col,
                TileZ = gridY * MapFile.MapSize + row,
            }));
            return modelId;
        }

        private int _travelling;
        private int TravellingId() => -1000 - _travelling++;

        private readonly Dictionary<(int x, int z), PlacedProp> _elevatorTiles = new Dictionary<(int, int), PlacedProp>();

        public Func<int> CurrentFrame;

        private readonly Dictionary<int, (TextureSrtAnimation scrolling, JointAnimation joint)> _kindAnimations
            = new Dictionary<int, (TextureSrtAnimation, JointAnimation)>();

        private void RegisterMotion(TornWorldCodeTables.Tables tables, int propKind, int modelId, bool rides)
        {
            int member = tables.AnimationFor(propKind);

            if (member < 0)
            {
                BuildingAnimationSet.HoverAnimation hover = tables.HoverOffsets.Length > 0
                    ? new BuildingAnimationSet.HoverAnimation(tables.HoverOffsets, tables.HoverStep) : null;
                if (rides) BuildingAnimationSet.Register(modelId, motion: new PlatformMotion(this, modelId, hover));
                else if (hover != null) BuildingAnimationSet.Register(modelId, motion: hover);
                return;
            }

            if (!_kindAnimations.TryGetValue(propKind, out (TextureSrtAnimation scrolling, JointAnimation joint) loaded))
            {
                loaded = (null, null);
                byte[] raw = FieldEffectMember(member);
                if (raw != null && raw.Length >= 4)
                {
                    string magic = System.Text.Encoding.ASCII.GetString(raw, 0, 4);
                    try
                    {
                        if (magic == "BTA0") loaded.scrolling = TextureSrtAnimation.Load(raw);
                        else if (magic == "BCA0") loaded.joint = JointAnimation.Load(raw);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"DistortionWorld.RegisterMotion({propKind}): " + ex.Message);
                    }
                }
                _kindAnimations[propKind] = loaded;
            }

            BuildingAnimationSet.Register(modelId, scrolling: loaded.scrolling, joint: loaded.joint,
                motion: rides ? new PlatformMotion(this, modelId, null) : null);
        }

        // A moving platform's bob, plus the ride when the player is on it.
        private sealed class PlatformMotion : BuildingAnimationSet.WholeModelMotion
        {
            private readonly DistortionWorldViewModel _owner;
            private readonly int _modelId;
            private readonly BuildingAnimationSet.HoverAnimation _hover;

            public PlatformMotion(DistortionWorldViewModel owner, int modelId, BuildingAnimationSet.HoverAnimation hover)
            {
                _owner = owner; _modelId = modelId; _hover = hover;
            }

            public override (float x, float y, float z) At(int frame)
            {
                (float hx, float hy, float hz) = _hover?.At(frame) ?? (0f, 0f, 0f);
                (float rx, float ry, float rz) = _owner.RideShift(_modelId);
                return (hx + rx, hy + ry, hz + rz);
            }
        }

        private static byte[] FieldEffectMember(int member)
        {
            try
            {
                if (!GameDirs.ContainsKey(DirNames.FieldEffectModels)) return null;
                string path = ArchiveMembers.PathOf(DirNames.FieldEffectModels, member);
                return PlainFiles.ReadBytesOrNull(path);
            }
            catch (Exception ex) { AppLogger.WarnOnce("DistortionWorldViewModel.FieldEffectMember", ex); return null; }
        }

        private readonly Dictionary<int, NSBMDModel> _propModels = new Dictionary<int, NSBMDModel>();

        private NSBMDModel PropModel(int member)
        {
            if (member < 0) return null;
            if (_propModels.TryGetValue(member, out NSBMDModel cached)) return cached;

            NSBMDModel model = null;
            try
            {
                if (GameDirs.ContainsKey(DirNames.FieldEffectModels))
                {
                    string path = ArchiveMembers.PathOf(DirNames.FieldEffectModels, member);
                    if (PlainFiles.Exists(path))
                    {
                        using FileStream file = PlainFiles.OpenRead(path);
                        NSBMD nsbmd = NSBMDLoader.LoadNSBMD(file);
                        if (nsbmd?.Models != null && nsbmd.Models.Length > 0)
                        {
                            try { nsbmd.MatchTextures(); } catch { /* it draws untextured */ }
                            model = nsbmd.Models[0];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"DistortionWorld.PropModel({member}): " + ex.Message);
            }

            _propModels[member] = model;
            return model;
        }
    }
}
