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
        private void Load()
        {
            try
            {
                byte[] table = _archive.Available ? _archive.Get(0) : null;
                if (table == null) { StatusText = "This ROM has no Distortion World data."; return; }

                _table = new TornWorldMapTable(table);
                IReadOnlyList<string> labels = HeaderNames();

                foreach (TornWorldMapTable.Floor floor in _table.Floors)
                {
                    string name = labels != null && floor.HeaderId < labels.Count ? labels[(int)floor.HeaderId] : null;
                    Floors.Add(new FloorRow
                    {
                        Floor = floor,
                        Data = new TornWorldFile(_archive.Get(floor.DataMember)),
                        Label = string.IsNullOrWhiteSpace(name) ? $"Header {floor.HeaderId}" : name,
                    });
                }

                StatusText = $"{Floors.Count} floors.";
                if (Floors.Count > 0) SelectedFloorIndex = 0;
                ResetSteps();
            }
            catch (Exception ex)
            {
                AppLogger.Error("DistortionWorld.Load: " + ex.Message);
                StatusText = "Couldn't read Distortion World data: " + ex.Message;
            }
        }

        private static IReadOnlyList<string> HeaderNames()
        {
            try { return HeaderLabels.Friendly(); } catch (Exception ex) { AppLogger.WarnOnce("DistortionWorldViewModel.HeaderNames", ex); return null; }
        }

        private int _shownFloorIndex = -1;

        public void ShowFloorAgain() => ShowFloor();

        private void ShowFloor()
        {
            CommitShownFloor();

            Platforms.Clear(); JumpPoints.Clear(); CameraRegions.Clear();
            GhostProps.Clear(); GhostTriggers.Clear();
            _shownFloorIndex = _selectedFloorIndex;
            if (_selectedFloorIndex < 0 || _selectedFloorIndex >= Floors.Count) { FloorNote = ""; return; }

            FloorRow row = Floors[_selectedFloorIndex];
            foreach (TornWorldFile.FloatingPlatform platform in row.Data.Platforms) Platforms.Add(platform);
            foreach (TornWorldFile.JumpPoint jump in row.Data.JumpPoints) JumpPoints.Add(jump);
            foreach (TornWorldFile.CameraRegion region in row.Data.CameraRegions) CameraRegions.Add(region);
            foreach (TornWorldFile.GhostProp prop in row.Data.GhostProps) GhostProps.Add(prop);
            foreach (TornWorldFile.GhostTrigger trigger in row.Data.GhostTriggers) GhostTriggers.Add(trigger);

            _shownGrids.Clear();
            foreach (TornWorldFile.FloatingPlatform platform in row.Data.Platforms)
            {
                ushort[] grid = Grid(platform.AttributeId);
                if (grid != null) _shownGrids[platform.AttributeId] = grid;
            }
            RememberGridShapes();

            FloorNote = $"File {row.Floor.DataMember}  ·  x {row.Floor.OffsetX}, y {row.Floor.OffsetAltitude}, z {row.Floor.OffsetZ}";

            BuildScene(ShownFloors().Select(f => f.floor).ToList());

            FloorShown?.Invoke();
            _selectedPlatformIndex = SurfaceRows.Count > 0 ? 0 : -1;
            Raise(nameof(SelectedPlatformIndex));
            ShowPlatform();
        }

        private bool _wholeWorld;

        public bool WholeWorld
        {
            get => _wholeWorld;
            set
            {
                if (!Set(ref _wholeWorld, value)) return;
                Raise(nameof(OneFloorOnly));
                BuildScene(ShownFloors().Select(f => f.floor).ToList());
                FloorShown?.Invoke();
            }
        }

        public bool OneFloorOnly
        {
            get => !_wholeWorld;
            set { if (value == _wholeWorld) WholeWorld = !value; }
        }

        public (float x, float y, float z)? SelectedFloorCentre()
        {
            TornWorldMapTable.Floor floor = CurrentFloor?.Floor;
            if (Model3D == null || floor == null) return null;

            int index = 0;
            foreach ((int i, FloorRow shown) in ShownFloors()) if (shown == CurrentFloor) { index = i; break; }
            if (!TryTile(index, floor, floor.OffsetX, floor.OffsetZ, out float originX, out float originZ, out _, out _, out _))
                return null;

            return Model3D.ToNormalized(originX + NsbmdGeometry.MapStride / 2f,
                                        floor.OffsetAltitude * (NsbmdGeometry.TileSize / 2f),
                                        originZ + NsbmdGeometry.MapStride / 2f);
        }

        private int IndexOfShown(FloorRow floor)
        {
            foreach ((int index, FloorRow shown) in ShownFloors()) if (shown == floor) return index;
            return 0;
        }

        private FloorRow CurrentFloor =>
            _selectedFloorIndex >= 0 && _selectedFloorIndex < Floors.Count ? Floors[_selectedFloorIndex] : null;

        private IEnumerable<(int index, FloorRow floor)> ShownFloors()
        {
            if (_wholeWorld)
            {
                for (int i = 0; i < Floors.Count; i++) yield return (i, Floors[i]);
            }
            else if (CurrentFloor != null)
            {
                yield return (0, CurrentFloor);
            }
        }
    }
}
