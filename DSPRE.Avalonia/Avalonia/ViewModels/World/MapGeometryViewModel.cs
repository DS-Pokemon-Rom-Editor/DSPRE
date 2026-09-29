using DSPRE.Avalonia.Gl;
using DSPRE.Models;
using DSPRE.ROMFiles;
using LibNDSFormats.NSBTX;
using System.IO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.World
{
    /// <summary>Edits a map model's geometry: pick, move, repaint, add and delete faces.</summary>
    public class MapGeometryViewModel : INotifyPropertyChanged
    {
        public sealed class FaceRow
        {
            public int Face { get; set; }
            public string Label { get; set; }
            public override string ToString() => Label;
        }

        private MapFile _map;
        private MapMesh _mesh;
        private float[] _toScene;
        private GameFamilies _family;

        private byte _areaId;

        private int _wasBytes, _wasTriangles;

        private readonly List<(byte[] model, byte[] terrain)> _before = new List<(byte[], byte[])>();

        public bool CanUndo => _before.Count > 0;

        public NsbmdRenderModel Model3D { get; private set; }

        public int PickedFace { get; private set; } = -1;

        public int PickedCorner { get; private set; } = -1;

        private string _note = "Click a face to select it.";
        public string Note { get => _note; private set => Set(ref _note, value); }

        private string _sizeNote = "";
        public string SizeNote { get => _sizeNote; private set => Set(ref _sizeNote, value); }

        private string _warning;
        public string Warning { get => _warning; private set => Set(ref _warning, value); }

        private bool _dirty;
        public bool Dirty { get => _dirty; private set => Set(ref _dirty, value); }

        private float _step = 0.25f;
        public float Step { get => _step; set { if (Set(ref _step, value)) Raise(nameof(StepNote)); } }
        public string StepNote => $"{Step:0.###} tile";

        public ObservableCollection<string> AvailablePictures { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> AvailableColours { get; } = new ObservableCollection<string>();

        private readonly List<(string name, int width, int height, int format)> _pictureFacts
            = new List<(string, int, int, int)>();

        private int _pickedPicture = -1;
        public int PickedPicture { get => _pickedPicture; set => Set(ref _pickedPicture, value); }

        private int _pickedColours = -1;
        public int PickedColours { get => _pickedColours; set => Set(ref _pickedColours, value); }

        private bool _canPaint;
        public bool CanPaint { get => _canPaint; private set => Set(ref _canPaint, value); }

        private string _terrainNote = "";
        public string TerrainNote { get => _terrainNote; private set => Set(ref _terrainNote, value); }

        private bool _terrainDisagrees;
        public bool TerrainDisagrees { get => _terrainDisagrees; private set => Set(ref _terrainDisagrees, value); }

        public event EventHandler Changed;

        public bool Ready => _mesh != null;

        public void Open(MapFile map, byte areaId, GameFamilies family)
        {
            _map = null; _mesh = null; _toScene = null; Model3D = null;
            _touched.Clear();
            _offAtOpen = null;
            PickedFace = PickedCorner = -1;
            Dirty = false; Warning = null;
            AvailablePictures.Clear();
            AvailableColours.Clear();

            if (map?.mapModelData == null || map.mapModelData.Length < 16)
            {
                Note = "This map has no model.";
                Raise(nameof(Ready));
                Changed?.Invoke(this, EventArgs.Empty);
                return;
            }

            _family = family;
            _areaId = areaId;
            var mesh = MapMesh.Read(map.mapModelData, out string whynot);
            if (mesh == null)
            {
                Note = whynot;
                Raise(nameof(Ready));
                Changed?.Invoke(this, EventArgs.Empty);
                return;
            }

            _map = map;
            _mesh = mesh;

            try
            {
                Model3D = MatrixSceneBuilder.BuildFromPlaced(family,
                    new[] { (0, 0, map, areaId, 0f, 0f, 0f) },
                    NsbmdGeometry.MatrixStitchMode.Grid);
            }
            catch (Exception ex) { AppLogger.Error("MapGeometry.Scene: " + ex.Message); }

            float modelScale = 1f;
            try { modelScale = map.mapModel?.models?.FirstOrDefault()?.modelScale ?? 1f; } catch { }
            _toScene = Model3D?.CellToScene(0, 0, modelScale);

            LoadWhatTheAreaCarries(areaId);

            _before.Clear();
            Raise(nameof(CanUndo));
            _wasBytes = map.mapModelData.Length;
            _wasTriangles = mesh.Faces.Sum(f => Math.Max(0, f.Corners.Length - 2));

            Note = "Click a face to select it.";
            Measure();
            CheckTerrain();
            Raise(nameof(Ready));
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void LoadWhatTheAreaCarries(byte areaId)
        {
            AvailablePictures.Clear();
            AvailableColours.Clear();
            _pictureFacts.Clear();
            try
            {
                var area = new AreaData(areaId);
                string path = Path.Combine(gameDirs[DirNames.mapTextures].unpackedDir,
                                           area.mapTileset.ToString("D4"));
                if (!File.Exists(path)) return;

                NSBTXLoader.LoadNsbtx(new MemoryStream(File.ReadAllBytes(path)),
                                      out var pictures, out var colours);
                if (pictures != null)
                    foreach (var t in pictures)
                    {
                        if (string.IsNullOrEmpty(t.texname)) continue;
                        AvailablePictures.Add(t.texname);
                        _pictureFacts.Add((t.texname, t.width, t.height, t.format));
                    }
                if (colours != null)
                    foreach (var c in colours)
                        if (!string.IsNullOrEmpty(c.palname)) AvailableColours.Add(c.palname);
            }
            catch (Exception ex) { AppLogger.Error("MapGeometry.AreaPictures: " + ex.Message); }
        }

        public void Paint()
        {
            if (_mesh == null || PickedFace < 0) return;

            int material = _mesh.Faces[PickedFace].Material;
            int picture = _mesh.PictureFor(material);
            int colours = _mesh.ColoursFor(material);

            Warning = WhyNotPaint(material);

            Remember();

            if (picture >= 0 && PickedPicture >= 0 && PickedPicture < AvailablePictures.Count)
                _mesh.Repaint(picture, AvailablePictures[PickedPicture]);
            if (colours >= 0 && PickedColours >= 0 && PickedColours < AvailableColours.Count)
                _mesh.Recolour(colours, AvailableColours[PickedColours]);

            Dirty = _mesh.AnythingChanged;
            Apply();
            Describe();
        }

        private string WhyNotPaint(int material)
        {
            if (PickedPicture < 0 || PickedPicture >= _pictureFacts.Count) return null;

            var mats = _map?.mapModel?.models?.FirstOrDefault()?.Materials;
            if (mats == null || material < 0 || material >= mats.Count) return null;

            var wearing = mats[material];
            var chosen = _pictureFacts[PickedPicture];

            if (wearing.width <= 0 || wearing.height <= 0) return null;

            if (chosen.width != wearing.width || chosen.height != wearing.height)
                return $"{chosen.name} is {chosen.width}x{chosen.height}, the old texture is "
                     + $"{wearing.width}x{wearing.height}.";

            if (chosen.format != wearing.format)
                return $"{chosen.name} uses a different texture format from the old texture.";

            return null;
        }

        public void Told(string what) => Note = what;

        public float[] SceneMatrix => _toScene;

        public bool PickAlong(float ox, float oy, float oz, float dx, float dy, float dz)
        {
            if (_mesh == null) return false;

            bool plate = PickPlate(ox, oy, oz, dx, dy, dz);
            var hit = MeshPick.Nearest(_mesh, _toScene, ox, oy, oz, dx, dy, dz);
            if (hit == null && plate) { Changed?.Invoke(this, EventArgs.Empty); return true; }
            if (hit == null)
            {
                PickedFace = PickedCorner = -1;
                Note = "Nothing under there.";
                Changed?.Invoke(this, EventArgs.Empty);
                return false;
            }

            PickedFace = hit.Value.Face;
            PickedCorner = hit.Value.NearestCorner;
            Raise(nameof(HasPickedFace));

            int material = _mesh.Faces[PickedFace].Material;
            int picture = _mesh.PictureFor(material), colours = _mesh.ColoursFor(material);
            CanPaint = picture >= 0;
            if (picture >= 0)
                PickedPicture = IndexOf(AvailablePictures, _mesh.NameOfPicture(picture));
            if (colours >= 0)
                PickedColours = IndexOf(AvailableColours, _mesh.NameOfColours(colours));

            Describe();
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public bool PickedCornerInScene(out float x, out float y, out float z)
        {
            x = y = z = 0f;
            if (_mesh == null || PickedCorner < 0 || PickedCorner >= _mesh.Vertices.Count) return false;
            var v = _mesh.Vertices[PickedCorner];
            (x, y, z) = MeshPick.Place(_toScene, v.X, v.Y, v.Z);
            return true;
        }

        public void Reread()
        {
            if (_map?.mapModelData == null || _mesh == null) return;
            var mesh = MapMesh.Read(_map.mapModelData, out string whynot);
            if (mesh == null) { Warning = whynot; return; }
            _mesh = mesh;
            PickedFace = PickedCorner = -1;
            Raise(nameof(HasPickedFace));
            Rebuild();
            Measure();
            CheckTerrain();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void Remember()
        {
            if (_map?.mapModelData == null) return;
            _before.Add(((byte[])_map.mapModelData.Clone(), (byte[])(_map.bdhc ?? new byte[0]).Clone()));
            if (_before.Count > 64) _before.RemoveAt(0);
            Raise(nameof(CanUndo));
        }

        public void Undo()
        {
            if (_before.Count == 0 || _map == null) return;

            var (model, terrain) = _before[_before.Count - 1];
            _before.RemoveAt(_before.Count - 1);
            Raise(nameof(CanUndo));

            _map.LoadMapModel(model, showMessages: false);
            _map.mapModelData = model;
            if (terrain.Length > 0) _map.ImportTerrain(terrain);

            _mesh = MapMesh.Read(model, out string whynot);
            if (_mesh == null) { Warning = whynot; return; }

            PickedFace = PickedCorner = -1;
            CanPaint = false;
            Dirty = _before.Count > 0;
            Warning = null;

            Rebuild();
            Measure();
            CheckTerrain();
            Note = _before.Count == 0
                ? "Back to the original model."
                : "Undone.";
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public bool HasPickedFace => _mesh != null && PickedFace >= 0;

        public void DeleteFace()
        {
            if (!HasPickedFace) return;
            Remember();
            Touch(PickedFace);
            _mesh.DeleteFace(PickedFace);
            PickedFace = PickedCorner = -1;
            Dirty = true;
            Apply();
            Note = "Took the face away.";
            Raise(nameof(HasPickedFace));
        }

        private readonly List<int> _newCorners = new List<int>();

        public string NewFaceNote => _newCorners.Count == 0
            ? "Shift+click 3 or 4 vertices."
            : $"{_newCorners.Count} vertices selected.";

        public bool CanMakeFace => _newCorners.Count >= 3;

        public void GatherCorner()
        {
            if (_mesh == null || PickedCorner < 0) return;
            if (_newCorners.Contains(PickedCorner)) _newCorners.Remove(PickedCorner);
            else if (_newCorners.Count < 4) _newCorners.Add(PickedCorner);
            RaiseGathered();
        }

        public void ForgetCorners() { _newCorners.Clear(); RaiseGathered(); }

        private void RaiseGathered() { Raise(nameof(NewFaceNote)); Raise(nameof(CanMakeFace)); }

        public void MakeFace()
        {
            if (_mesh == null || _newCorners.Count < 3) return;
            int like = PickedFace >= 0 ? PickedFace
                     : _mesh.Faces.FindIndex(f => f.Corners.Contains(_newCorners[0]));
            if (like < 0) { Warning = "Select a face to copy its material."; return; }

            int picture = _mesh.PictureFor(_mesh.Faces[like].Material);
            string name = picture >= 0 ? _mesh.NameOfPicture(picture) : null;
            var facts = _pictureFacts.FirstOrDefault(f => f.name == name);

            Remember();
            int made = _mesh.AddFace(_newCorners.ToArray(), like, facts.width, facts.height);
            if (made < 0) { Warning = "Those vertices can't form a face."; return; }
            _newCorners.Clear();
            RaiseGathered();
            PickedFace = made;
            Touch(made);
            Dirty = true;
            Apply();
            Describe();
            Raise(nameof(HasPickedFace));
        }

        private (float x, float y, float z) _dragFrom;
        private float[] _dragSum = new float[3];

        public void BeginDrag()
        {
            if (_mesh == null || PickedCorner < 0) return;
            Remember();
            var v = _mesh.Vertices[PickedCorner];
            _dragFrom = (v.X, v.Y, v.Z);
            _dragSum = new float[3];
        }

        public void DragAxis(int axis, float sceneDelta)
        {
            if (_mesh == null || PickedCorner < 0 || _toScene == null || axis < 0 || axis > 2) return;
            float scale = _toScene[axis * 5];
            if (Math.Abs(scale) < 1e-9f) return;
            _dragSum[axis] += sceneDelta / scale;

            float step = Step * MapTileset.TileWidth;
            float Snap(float d) => (float)Math.Round(d / step) * step;
            var to = (_dragFrom.x + Snap(_dragSum[0]), _dragFrom.y + Snap(_dragSum[1]), _dragFrom.z + Snap(_dragSum[2]));
            var v = _mesh.Vertices[PickedCorner];
            if (v.X == to.Item1 && v.Y == to.Item2 && v.Z == to.Item3) return;

            TouchCorner(PickedCorner);
            _mesh.Move(PickedCorner, to.Item1, to.Item2, to.Item3);
            TouchCorner(PickedCorner);
            Dirty = _mesh.AnythingChanged;
            Apply();
            Describe();
        }

        public void Nudge(float dx, float dy, float dz)
        {
            if (_mesh == null || PickedCorner < 0) return;
            Remember();

            var v = _mesh.Vertices[PickedCorner];
            TouchCorner(PickedCorner);
            _mesh.Move(PickedCorner, v.X + dx, v.Y + dy, v.Z + dz);
            TouchCorner(PickedCorner);
            Dirty = _mesh.AnythingChanged;

            Apply();
            Describe();
        }

        public void PutAt(float x, float y, float z)
        {
            if (_mesh == null || PickedCorner < 0) return;
            TouchCorner(PickedCorner);
            _mesh.Move(PickedCorner, x, y, z);
            TouchCorner(PickedCorner);
            Dirty = _mesh.AnythingChanged;
            Apply();
            Describe();
        }

        private void Apply()
        {
            if (_mesh == null || _map == null) return;

            byte[] made = _mesh.Save(out string whynot);
            if (made == null)
            {
                Warning = whynot;
                return;
            }

            Warning = null;
            if (MapFile.TooBigForTheGame(made.Length, 0) is string tooBigmade) { Told(tooBigmade); return; }
            _map.LoadMapModel(made, showMessages: false);
            _map.mapModelData = made;
            Rebuild();

            Measure();
            CheckTerrain();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private bool _showPlates;
        public bool ShowPlates { get => _showPlates; set { if (Set(ref _showPlates, value)) RebuildPlates(); } }

        public float[] PlateMesh { get; private set; }

        private float _plateBase;
        /// <summary>The plate height, in tiles, the colours count from.</summary>
        public float PlateBase { get => _plateBase; private set { if (Set(ref _plateBase, value)) Raise(nameof(PlateLegend)); } }

        public sealed class Swatch
        {
            public global::Avalonia.Media.IBrush Brush { get; init; }
            public string Said { get; init; }
        }

        public IReadOnlyList<Swatch> PlateLegend =>
            Enumerable.Range(-2, 5).Select(k => new Swatch
            {
                Brush = new global::Avalonia.Media.SolidColorBrush(TileGridControl.HeightColour(k)),
                Said = (PlateBase + k).ToString("0.##"),
            }).ToList();
        public int PlateVertexCount { get; private set; }
        public event EventHandler PlatesChanged;

        private List<BdhcBuild.Piece> _plates = new List<BdhcBuild.Piece>();
        private List<BdhcBuild.Piece> _proposal;
        private int _pickedPlate = -1;

        public bool HasProposal => _proposal != null;
        public bool HasPickedPlate => _proposal == null && _pickedPlate >= 0 && _pickedPlate < _plates.Count;
        private BdhcBuild.Piece Picked => HasPickedPlate ? _plates[_pickedPlate] : null;

        private string _plateNote = "";
        public string PlateNote { get => _plateNote; private set => Set(ref _plateNote, value); }

        private const float Unit = 16f, Edge = 256f;
        private static float Snap(float tiles, float step) => MathF.Round(tiles / step) * step;

        public float PlateLeft { get => Picked == null ? 0 : (Picked.MinX + Edge) / Unit; set => EditPlate(p => p.MinX = Math.Min(Snap(value, 0.25f) * Unit - Edge, p.MaxX - 1f)); }
        public float PlateTop { get => Picked == null ? 0 : (Picked.MinZ + Edge) / Unit; set => EditPlate(p => p.MinZ = Math.Min(Snap(value, 0.25f) * Unit - Edge, p.MaxZ - 1f)); }
        public float PlateRight { get => Picked == null ? 0 : (Picked.MaxX + Edge) / Unit; set => EditPlate(p => p.MaxX = Math.Max(Snap(value, 0.25f) * Unit - Edge, p.MinX + 1f)); }
        public float PlateBottom { get => Picked == null ? 0 : (Picked.MaxZ + Edge) / Unit; set => EditPlate(p => p.MaxZ = Math.Max(Snap(value, 0.25f) * Unit - Edge, p.MinZ + 1f)); }
        public float PlateHeight { get => Picked == null ? 0 : Picked.Height / Unit; set => EditPlate(p => p.Tilt(value * Unit, p.SlopeX, p.SlopeZ), keepTilt: false); }
        public float PlateSlopeX { get => Picked == null ? 0 : Picked.SlopeX / Unit; set => EditPlate(p => p.Tilt(p.Height, value * Unit, p.SlopeZ), keepTilt: false); }
        public float PlateSlopeZ { get => Picked == null ? 0 : Picked.SlopeZ / Unit; set => EditPlate(p => p.Tilt(p.Height, p.SlopeX, value * Unit), keepTilt: false); }
        public bool PlateKept { get => Picked?.Mine == true; set => EditPlate(p => p.Mine = value, keepTilt: true, mark: false); }

        private void EditPlate(Action<BdhcBuild.Piece> change, bool keepTilt = true, bool mark = true)
        {
            var p = Picked;
            if (p == null) return;
            float height = p.Height, sx = p.SlopeX, sz = p.SlopeZ;
            change(p);
            if (keepTilt) p.Tilt(height, sx, sz);
            if (mark) p.Mine = true;
            WritePlates("Plate changed.");
        }

        private void WritePlates(string said)
        {
            if (_map == null) return;
            byte[] made = BdhcBuild.From(_plates, out string whynot);
            if (made == null) { Warning = whynot; return; }
            Remember();
            _map.KeptPlates = _plates.Where(p => p.Mine).Select(p => p.Copy()).ToList();
            _map.ImportTerrain(made);
            int blocked = BdhcBuild.BlockUngrounded(_map.bdhc, _map.collisions);
            if (blocked > 0) said += $" {blocked} squares without ground blocked.";
            Dirty = true;
            Note = said;
            CheckTerrain();
            if (HasPickedPlate) PlateNote = $"Plate {_pickedPlate + 1} of {_plates.Count}" + (Picked.Mine ? ", yours." : ".");
            RaisePlate();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        // Squares under faces this tab added, deleted or moved; proposals leave every other plate alone.
        private readonly HashSet<(int c, int r)> _touched = new HashSet<(int, int)>();

        private bool _proposeWholeMap;
        public bool ProposeWholeMap { get => _proposeWholeMap; set => Set(ref _proposeWholeMap, value); }

        private Func<int, int, bool> Touched => _proposeWholeMap ? null : (c, r) => _touched.Contains((c, r));

        private void Touch(int face)
        {
            if (_mesh == null || face < 0 || face >= _mesh.Faces.Count) return;
            float scale = _mesh.ModelScale == 0f ? 1f : _mesh.ModelScale;
            var corners = _mesh.Faces[face].Corners.Select(i => _mesh.Vertices[i]).ToList();
            int c0 = Math.Clamp((int)MathF.Floor((corners.Min(v => v.X) * scale + Edge) / Unit), 0, 31);
            int c1 = Math.Clamp((int)MathF.Floor((corners.Max(v => v.X) * scale + Edge - 0.01f) / Unit), 0, 31);
            int r0 = Math.Clamp((int)MathF.Floor((corners.Min(v => v.Z) * scale + Edge) / Unit), 0, 31);
            int r1 = Math.Clamp((int)MathF.Floor((corners.Max(v => v.Z) * scale + Edge - 0.01f) / Unit), 0, 31);
            for (int r = r0; r <= r1; r++)
                for (int c = c0; c <= c1; c++) _touched.Add((c, r));
        }

        private void TouchCorner(int vertex)
        {
            if (_mesh == null || vertex < 0 || vertex >= _mesh.Vertices.Count) return;
            for (int f = 0; f < _mesh.Faces.Count; f++)
                if (_mesh.Faces[f].Corners.Contains(vertex)) Touch(f);
        }

        public void ProposePlates()
        {
            if (_mesh == null || _map == null) return;
            _proposal = BdhcBuild.Propose(_mesh, _map.collisions, _map.bdhc, _map.KeptPlates, Touched);
            int yours = _proposal.Count(p => p.Mine);
            PlateNote = $"{_proposal.Count} plates proposed" + (yours > 0 ? $", {yours} of yours kept." : ".");
            if (!_showPlates) ShowPlates = true; else RebuildPlates();
            RaisePlate();
        }

        public void UseProposal()
        {
            if (_proposal == null) return;
            _plates = _proposal;
            _proposal = null;
            _pickedPlate = -1;
            WritePlates($"Terrain: {_plates.Count} plates.");
        }

        public void DiscardProposal()
        {
            _proposal = null;
            RebuildPlates();
            RaisePlate();
        }

        public void AddPlate()
        {
            if (_mesh == null || !HasPickedFace) { Note = "Click a face first."; return; }
            float scale = _mesh.ModelScale == 0f ? 1f : _mesh.ModelScale;
            var corners = _mesh.Faces[PickedFace].Corners.Select(c => _mesh.Vertices[c]).ToList();
            float x = corners.Average(v => v.X) * scale, z = corners.Average(v => v.Z) * scale, y = corners.Average(v => v.Y) * scale;
            float left = MathF.Floor((x + Edge) / Unit) * Unit - Edge, top = MathF.Floor((z + Edge) / Unit) * Unit - Edge;
            var plate = new BdhcBuild.Piece { MinX = left, MaxX = left + Unit, MinZ = top, MaxZ = top + Unit, Mine = true };
            plate.Tilt(y, 0f, 0f);
            _plates.Add(plate);
            _pickedPlate = _plates.Count - 1;
            WritePlates("Plate added.");
        }

        public void DeletePlate()
        {
            if (!HasPickedPlate) return;
            _plates.RemoveAt(_pickedPlate);
            _pickedPlate = -1;
            WritePlates("Plate deleted.");
        }

        private void RaisePlate()
        {
            foreach (var name in new[] { nameof(HasProposal), nameof(HasPickedPlate), nameof(PlateLeft), nameof(PlateTop),
                                         nameof(PlateRight), nameof(PlateBottom), nameof(PlateHeight), nameof(PlateSlopeX),
                                         nameof(PlateSlopeZ), nameof(PlateKept) })
                Raise(name);
        }

        private (float x, float y, float z) PlateCorner(BdhcBuild.Piece plate, float x, float z)
        {
            float scale = _mesh.ModelScale == 0f ? 1f : _mesh.ModelScale;
            return MeshPick.Place(_toScene, x / scale, plate.HeightAtOrZero(x, z) / scale + 0.01f, z / scale);
        }

        // Selects the nearest plate along the ray when plates are shown.
        private bool PickPlate(float ox, float oy, float oz, float dx, float dy, float dz)
        {
            if (!_showPlates || _proposal != null || _toScene == null) return false;
            int best = -1; float nearest = float.MaxValue;
            for (int i = 0; i < _plates.Count; i++)
            {
                var p = _plates[i];
                var a = PlateCorner(p, p.MinX, p.MinZ); var b = PlateCorner(p, p.MaxX, p.MinZ);
                var c = PlateCorner(p, p.MaxX, p.MaxZ); var d = PlateCorner(p, p.MinX, p.MaxZ);
                foreach (var (u, v, w) in new[] { (a, b, c), (a, c, d) })
                    if (MeshPick.Crosses(ox, oy, oz, dx, dy, dz, u, v, w, out float t) && t < nearest) { nearest = t; best = i; }
            }
            _pickedPlate = best;
            if (best >= 0) PlateNote = $"Plate {best + 1} of {_plates.Count}" + (_plates[best].Mine ? ", yours." : ".");
            RebuildPlates();
            RaisePlate();
            return best >= 0;
        }

        private void RebuildPlates()
        {
            PlateMesh = null;
            PlateVertexCount = 0;
            if (_proposal == null)
            {
                var mine = _map?.KeptPlates ?? new List<BdhcBuild.Piece>();
                _plates = BdhcBuild.Read(_map?.bdhc);
                foreach (var p in _plates)
                    p.Mine = mine.Any(k => Math.Abs(k.MinX - p.MinX) < 0.01f && Math.Abs(k.MaxX - p.MaxX) < 0.01f
                                        && Math.Abs(k.MinZ - p.MinZ) < 0.01f && Math.Abs(k.MaxZ - p.MaxZ) < 0.01f
                                        && Math.Abs(k.Height - p.Height) < 0.05f);
                if (_pickedPlate >= _plates.Count) _pickedPlate = -1;
            }
            var shown = _proposal ?? _plates;
            if (_showPlates && _mesh != null && _toScene != null)
            {
                var v = new List<float>();
                // Colours say height against the map's most common plate height, as the Heights view does.
                float usual = shown.Count == 0 ? 0f
                    : shown.GroupBy(q => MathF.Round(q.Height / Unit)).OrderByDescending(g => g.Count()).First().Key * Unit;
                PlateBase = usual / Unit;
                void Quad(BdhcBuild.Piece plate, float x0, float z0, float x1, float z1, (float r, float g, float b) c, float lift)
                {
                    void Corner(float x, float z)
                    {
                        var (sx, sy, sz) = PlateCorner(plate, x, z);
                        v.Add(sx); v.Add(sy + lift); v.Add(sz); v.Add(0); v.Add(0); v.Add(c.r); v.Add(c.g); v.Add(c.b);
                    }
                    Corner(x0, z0); Corner(x1, z0); Corner(x1, z1);
                    Corner(x0, z0); Corner(x1, z1); Corner(x0, z1);
                }
                for (int i = 0; i < shown.Count; i++)
                {
                    var plate = shown[i];
                    var hc = TileGridControl.HeightColour((int)MathF.Round((plate.Height - usual) / Unit));
                    var fill = (hc.R / 255f, hc.G / 255f, hc.B / 255f);
                    bool picked = _proposal == null && i == _pickedPlate;
                    bool slope = Math.Abs(plate.SlopeX) + Math.Abs(plate.SlopeZ) > 0.01f;
                    if (!slope) Quad(plate, plate.MinX, plate.MinZ, plate.MaxX, plate.MaxZ, fill, 0f);
                    else
                    {
                        // Stripes run across the way the slope climbs.
                        bool alongX = Math.Abs(plate.SlopeX) >= Math.Abs(plate.SlopeZ);
                        float from = alongX ? plate.MinX : plate.MinZ, to = alongX ? plate.MaxX : plate.MaxZ, band = Unit / 4f;
                        int k = 0;
                        for (float a = from; a < to - 0.01f; a += band, k++)
                        {
                            float b = Math.Min(to, a + band);
                            var c = k % 2 == 0 ? fill : (fill.Item1 * 0.6f, fill.Item2 * 0.6f, fill.Item3 * 0.6f);
                            if (alongX) Quad(plate, a, plate.MinZ, b, plate.MaxZ, c, 0f);
                            else Quad(plate, plate.MinX, a, plate.MaxX, b, c, 0f);
                        }
                    }
                    // An outline shows where one plate ends; the user's own have a gold one.
                    var edge = picked ? (1f, 1f, 1f) : plate.Mine ? (1f, 0.78f, 0.15f) : (0.06f, 0.06f, 0.08f);
                    float w = picked || plate.Mine ? Unit / 6f : Unit / 12f;
                    Quad(plate, plate.MinX, plate.MinZ, plate.MaxX, plate.MinZ + w, edge, 0.002f);
                    Quad(plate, plate.MinX, plate.MaxZ - w, plate.MaxX, plate.MaxZ, edge, 0.002f);
                    Quad(plate, plate.MinX, plate.MinZ, plate.MinX + w, plate.MaxZ, edge, 0.002f);
                    Quad(plate, plate.MaxX - w, plate.MinZ, plate.MaxX, plate.MaxZ, edge, 0.002f);
                }
                PlateMesh = v.ToArray();
                PlateVertexCount = v.Count / 8;
                if (_proposal == null && !HasPickedPlate) PlateNote = $"{_plates.Count} plates. Click one to edit it.";
            }
            PlatesChanged?.Invoke(this, EventArgs.Empty);
        }

        // Squares whose terrain already disagreed with the model when the window opened, with their walking height.
        private Dictionary<(int x, int z), float> _offAtOpen;

        private void CheckTerrain()
        {
            RebuildPlates();
            TerrainDisagrees = false;
            TerrainNote = "";
            if (_mesh == null || _map?.bdhc == null) return;

            if (!BdhcFile.TryParse(_map.bdhc, out var walking))
            {
                TerrainNote = "Terrain can't be read.";
                return;
            }

            byte[] fromShape = BdhcBuild.ForMap(_mesh, _map.collisions, _map.bdhc, out _);
            if (fromShape == null || !BdhcFile.TryParse(fromShape, out var drawn))
            {
                TerrainNote = "Model has no walkable faces.";
                return;
            }

            int apart = 0, near = 0, looked = 0, yours = 0, kept = 0;
            bool first = _offAtOpen == null;
            if (first) _offAtOpen = new Dictionary<(int x, int z), float>();
            for (int tz = 0; tz < 32; tz++)
                for (int tx = 0; tx < 32; tx++)
                {
                    float rawX = (tx + 0.5f) * NsbmdGeometry.TileSize;
                    float rawZ = (tz + 0.5f) * NsbmdGeometry.TileSize;
                    if (!walking.TryGetHeight(rawX, rawZ, 0f, out float walkY)) continue;

                    looked++;
                    float x = rawX * 64f - 256f, z = rawZ * 64f - 256f;
                    if (_map.KeptPlates.Any(p => p.Covers(x, z))) { yours++; continue; }
                    bool none = !drawn.TryGetHeight(rawX, rawZ, walkY, out float drawY);
                    float off = none ? float.MaxValue : Math.Abs(drawY - walkY);
                    // PDSMS water sits a little below its plates; a quarter tile or less is left alone.
                    if (off > NsbmdGeometry.TileSize / 4f)
                    {
                        if (first) _offAtOpen[(tx, tz)] = walkY;
                        if (_offAtOpen.TryGetValue((tx, tz), out float was) && Math.Abs(was - walkY) < 0.001f) kept++; else apart++;
                    }
                    else if (off > 0.02f) near++;
                }

            TerrainDisagrees = apart > 0;
            string within = (near > 0 ? $", {near} within 1/4 tile" : "") + (yours > 0 ? $", {yours} under your plates" : "")
                          + (kept > 0 ? $", {kept} already different when opened" : "");
            TerrainNote = apart == 0
                ? $"Terrain matches the model ({looked} tiles{within})."
                : $"Terrain differs from the model at {apart} of {looked} tiles{within}.";
        }

        public void RebuildTerrain()
        {
            if (_mesh == null || _map == null) return;

            byte[] made = BdhcBuild.ForMap(_mesh, _map.collisions, _map.bdhc, out string whynot, _map.KeptPlates, Touched);
            if (made == null) { Warning = whynot; return; }

            Remember();
            _map.ImportTerrain(made);
            int blocked = BdhcBuild.BlockUngrounded(_map.bdhc, _map.collisions);
            if (blocked > 0) Note = $"Terrain rebuilt. {blocked} squares without ground blocked.";
            Dirty = true;
            Warning = null;
            CheckTerrain();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public string ImportFrom(string objPath)
        {
            if (_map == null) return "No map open.";
            var measure = MapObjImport.Guess(objPath);
            byte[] model = MapObjImport.Build(objPath, measure, name =>
            {
                var facts = _pictureFacts.FirstOrDefault(f => f.name == name);
                return (facts.width, facts.height);
            }, out string whynot, out var notes, ModelScaleOfMap());
            if (model == null) { Warning = whynot; return whynot; }

            var mesh = MapMesh.Read(model, out whynot);
            if (mesh == null) { Warning = whynot; return whynot; }

            Remember();
            if (MapFile.TooBigForTheGame(model.Length, 0) is string tooBigmodel) return tooBigmodel;
            _map.LoadMapModel(model, showMessages: false);
            _map.mapModelData = model;
            _mesh = mesh;
            PickedFace = PickedCorner = -1;
            Dirty = true;
            Warning = notes.Count > 0 ? string.Join(" ", notes) : null;
            Rebuild();
            Measure();
            CheckTerrain();
            Changed?.Invoke(this, EventArgs.Empty);
            Raise(nameof(HasPickedFace));
            return $"Imported {mesh.Faces.Count} faces"
                 + (measure == MapObjImport.Measure.MapStudio ? " (PDSMS scale)." : ".");
        }

        private float ModelScaleOfMap()
        {
            try
            {
                float scale = _map?.mapModel?.models?.FirstOrDefault()?.modelScale ?? 0f;
                if (scale > 0f) return scale;
            }
            catch { }
            return 64f;
        }

        public string ExportTo(string objPath)
        {
            if (_mesh == null) return "No map open.";

            string folder = Path.GetDirectoryName(objPath) ?? ".";
            var paints = new Dictionary<int, ObjWrite.Paint>();

            foreach (var face in _mesh.Faces)
            {
                if (paints.ContainsKey(face.Material)) continue;

                int picture = _mesh.PictureFor(face.Material);
                string named = picture >= 0 ? _mesh.NameOfPicture(picture) : $"material{face.Material}";
                int colours = _mesh.ColoursFor(face.Material);
                var paint = new ObjWrite.Paint { Name = named, Palette = colours >= 0 ? _mesh.NameOfColours(colours) : null };

                try
                {
                    var mats = _map?.mapModel?.models?.FirstOrDefault()?.Materials;
                    if (mats != null && face.Material >= 0 && face.Material < mats.Count)
                    {
                        var mat = mats[face.Material];
                        paint.Width = Math.Max(1, mat.width);
                        paint.Height = Math.Max(1, mat.height);

                        var picture2 = NsbmdTextureDecoder.Decode(mat);
                        if (picture2 != null && WritePicture(Path.Combine(folder, named + ".png"), picture2))
                            paint.Picture = named + ".png";
                    }
                }
                catch (Exception ex) { AppLogger.Error("MapGeometry.Export: " + ex.Message); }

                paints[face.Material] = paint;
            }

            var written = ObjWrite.To(objPath, _mesh, paints);
            if (written.Whynot != null) return written.Whynot;

            int pictures = paints.Values.Count(p => p.Picture != null);
            return $"Exported {written.Faces} faces, {pictures} textures.";
        }

        private static bool WritePicture(string path, NsbmdTextureData picture)
        {
            if (picture?.Rgba == null || picture.Width <= 0 || picture.Height <= 0) return false;

            var colours = new List<uint>();
            var where = new Dictionary<uint, int>();
            var indices = new byte[picture.Width * picture.Height];

            for (int i = 0; i < indices.Length; i++)
            {
                int at = i * 4;
                if (at + 3 >= picture.Rgba.Length) return false;
                uint colour = (uint)(picture.Rgba[at] | (picture.Rgba[at + 1] << 8)
                                   | (picture.Rgba[at + 2] << 16) | (picture.Rgba[at + 3] << 24));
                if (!where.TryGetValue(colour, out int slot))
                {
                    if (colours.Count >= 256) return false;
                    slot = colours.Count;
                    colours.Add(colour);
                    where[colour] = slot;
                }
                indices[i] = (byte)slot;
            }

            try
            {
                File.WriteAllBytes(path,
                    IndexedPng.Write(indices, colours.ToArray(), picture.Width, picture.Height));
                return true;
            }
            catch { return false; }
        }

        private void Rebuild()
        {
            try
            {
                Model3D = MatrixSceneBuilder.BuildFromPlaced(_family,
                    new[] { (0, 0, _map, _areaId, 0f, 0f, 0f) },
                    NsbmdGeometry.MatrixStitchMode.Grid);
                float modelScale = _map.mapModel?.models?.FirstOrDefault()?.modelScale ?? 1f;
                _toScene = Model3D?.CellToScene(0, 0, modelScale);
            }
            catch (Exception ex) { AppLogger.Error("MapGeometry.Rebuild: " + ex.Message); }
        }

        private void Measure()
        {
            if (_mesh == null || _map?.mapModelData == null) { SizeNote = ""; return; }

            int triangles = _mesh.Faces.Sum(f => Math.Max(0, f.Corners.Length - 2));
            int bytes = _map.mapModelData.Length;

            string grown = "";
            if (_wasBytes > 0 && bytes > _wasBytes)
            {
                int more = bytes - _wasBytes;
                grown = $"  ·  +{more:n0} bytes";
                if (more > _wasBytes / 4)
                    grown += $" (over 25%)";
            }
            if (_wasTriangles > 0 && triangles > _wasTriangles)
                grown += $"  ·  +{triangles - _wasTriangles} triangles";

            SizeNote = $"{_mesh.Faces.Count} faces  ·  {triangles} triangles  ·  {bytes:n0} bytes{grown}";
        }

        private void Describe()
        {
            if (_mesh == null || PickedFace < 0) { Note = "Click a face to select it."; return; }

            var face = _mesh.Faces[PickedFace];
            string shape = face.Shape >= 0 && face.Shape < _mesh.Shapes.Count
                ? _mesh.Shapes[face.Shape].Name : "?";
            string material = face.Material >= 0 ? face.Material.ToString() : "none";
            int named = _mesh.PictureFor(face.Material);
            string picture = named >= 0 ? _mesh.NameOfPicture(named) : PictureFor(face.Material) ?? "none";
            string alsoPaints = named >= 0 && _mesh.Pictures[named].Materials.Count > 1
                ? $" (shared by {_mesh.Pictures[named].Materials.Count} materials)"
                : "";

            string corner = "";
            if (PickedCorner >= 0 && PickedCorner < _mesh.Vertices.Count)
            {
                var v = _mesh.Vertices[PickedCorner];
                int shared = v.Corners.Count;
                corner = $"\nVertex {v.X:0.###}, {v.Y:0.###}, {v.Z:0.###}"
                       + (shared > 1 ? $" (x{shared})" : "");
            }

            Note = $"{shape}, {(face.Corners.Length == 4 ? "quad" : "triangle")}, "
                 + $"material {material}, texture {picture}{alsoPaints}{corner}";
        }

        private string PictureFor(int material)
        {
            if (material < 0) return null;
            try
            {
                var mats = _map?.mapModel?.models?.FirstOrDefault()?.Materials;
                if (mats == null || material >= mats.Count) return null;
                string name = mats[material].texname;
                return string.IsNullOrEmpty(name) ? null : name;
            }
            catch { return null; }
        }

        private static int IndexOf(ObservableCollection<string> among, string name)
        {
            for (int i = 0; i < among.Count; i++) if (among[i] == name) return i;
            return -1;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            Raise(name);
            return true;
        }
    }
}
