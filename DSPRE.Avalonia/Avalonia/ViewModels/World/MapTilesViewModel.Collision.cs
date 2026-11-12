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
        /// <summary>One square of the chosen tile's footprint in the collision grid.</summary>
        public sealed class CollisionCell : INotifyPropertyChanged
        {
            public int Index { get; set; }
            public int X, Y;
            private string _text;
            public string Text { get => _text; set { _text = value; Changed(); } }
            private global::Avalonia.Media.IBrush _fill;
            public global::Avalonia.Media.IBrush Fill { get => _fill; set { _fill = value; Changed(); } }
            private global::Avalonia.Media.IBrush _edge;
            public global::Avalonia.Media.IBrush Edge { get => _edge; set { _edge = value; Changed(); } }
            private string _tip;
            public string Tip { get => _tip; set { _tip = value; Changed(); } }
            public event PropertyChangedEventHandler PropertyChanged;
            private void Changed([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        }

        public ObservableCollection<CollisionCell> CollisionCells { get; } = new ObservableCollection<CollisionCell>();
        public int CollisionColumns => Chosen?.FootprintWide ?? 1;
        public int CollisionRows => Chosen?.FootprintDeep ?? 1;

        // Choices use the names the Map editor paints with; "No default" leaves the map's own value alone.
        public ObservableCollection<string> WalkChoices { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> TypeChoices { get; } = new ObservableCollection<string>();
        private readonly List<int> _walkValues = new List<int>(), _typeValues = new List<int>();

        private RomInfo.GameFamilies _choicesFamily;

        private static void Choices(ObservableCollection<string> names, List<int> values, IEnumerable<(byte value, string label)> table,
                                    int also, Func<byte, string> unknown)
        {
            if (names.Count == 0)
            {
                names.Add("No default"); values.Add(-1);
                foreach ((byte value, string label) in table) { names.Add(label); values.Add(value); }
            }
            if (also >= 0 && !values.Contains(also)) { names.Add(unknown((byte)also)); values.Add(also); }
        }

        private int _chosenCell;
        public int ChosenCell
        {
            get => _chosenCell;
            set
            {
                if (value < 0 || value >= CollisionCells.Count) return;
                _chosenCell = value;
                FillCollisionCells();
            }
        }

        private int CellValue(int layer)
        {
            if (Chosen == null || _chosenCell >= CollisionCells.Count) return -1;
            int[,] grid = Chosen.CollisionGrid(layer);
            CollisionCell cell = CollisionCells[_chosenCell];
            return cell.X < grid.GetLength(0) && cell.Y < grid.GetLength(1) ? grid[cell.X, cell.Y] : -1;
        }

        private void SetCellValue(int layer, int value)
        {
            if (Chosen == null || _chosenCell >= CollisionCells.Count) return;
            int[,] grid = Chosen.CollisionGrid(layer);
            CollisionCell cell = CollisionCells[_chosenCell];
            if (grid[cell.X, cell.Y] == value) return;
            grid[cell.X, cell.Y] = value;
            Dirty = true;
            if (_tileSource.ContainsKey(Chosen)) CollisionUnsaved = true;
            Raise(nameof(SetHasMovement));
            FillCollisionCells();
        }

        public int CellWalkIndex
        {
            get => _walkValues.IndexOf(CellValue(TileCollisions.CollisionLayer));
            set { if (value >= 0 && value < _walkValues.Count) SetCellValue(TileCollisions.CollisionLayer, _walkValues[value]); }
        }

        public int CellTypeIndex
        {
            get => _typeValues.IndexOf(CellValue(TileCollisions.TypeLayer));
            set { if (value >= 0 && value < _typeValues.Count) SetCellValue(TileCollisions.TypeLayer, _typeValues[value]); }
        }

        private static readonly global::Avalonia.Media.IBrush NoDefault = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.FromRgb(0x2A, 0x2D, 0x35));
        private static readonly global::Avalonia.Media.IBrush CellEdge = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.FromRgb(0x12, 0x14, 0x1A));

        private void FillCollisionCells()
        {
            int wide = CollisionColumns, deep = CollisionRows;
            if (Chosen == null || CollisionCells.Count != wide * deep)
            {
                CollisionCells.Clear();
                if (Chosen != null)
                    for (int y = 0; y < deep; y++)
                        for (int x = 0; x < wide; x++) CollisionCells.Add(new CollisionCell { Index = CollisionCells.Count, X = x, Y = y });
                _chosenCell = Math.Clamp(_chosenCell, 0, Math.Max(0, CollisionCells.Count - 1));
            }
            if (Chosen != null)
            {
                int[,] walk = Chosen.CollisionGrid(TileCollisions.CollisionLayer);
                int[,] type = Chosen.CollisionGrid(TileCollisions.TypeLayer);
                GameFamilies family = RomInfo.GameFamily;
                if (family != _choicesFamily)
                {
                    WalkChoices.Clear(); _walkValues.Clear();
                    TypeChoices.Clear(); _typeValues.Clear();
                    _choicesFamily = family;
                }
                string WalkName(byte v) => TilePermissions.CollisionLabel(v, family);
                string TypeName(byte v) => TilePermissions.BehaviourLabel(v, family);
                foreach (CollisionCell cell in CollisionCells)
                {
                    int w = walk[cell.X, cell.Y], t = type[cell.X, cell.Y];
                    Choices(WalkChoices, _walkValues, TilePermissions.CollisionsFor(family).Select(c => (c.Value, c.Label)), w, WalkName);
                    Choices(TypeChoices, _typeValues, TilePermissions.BehavioursFor(family).Select(b => (b.Value, b.Label)), t, TypeName);
                    cell.Text = (w switch { -1 => "-", 0x00 => "Walk", 0x80 => "Block", _ => w.ToString("X2") })
                              + "\n" + (t < 0 ? "-" : t.ToString("X2"));
                    cell.Fill = w < 0 ? NoDefault : PermissionColors.Brush((byte)w, true);
                    cell.Edge = cell.Index == _chosenCell ? global::Avalonia.Media.Brushes.White : CellEdge;
                    cell.Tip = $"{(w < 0 ? "no default" : WalkName((byte)w))} / {(t < 0 ? "no default" : TypeName((byte)t))}";
                }
            }
            Raise(nameof(CollisionColumns));
            Raise(nameof(CollisionRows));
            Raise(nameof(ChosenFromTileset));
            Raise(nameof(CellWalkIndex));
            Raise(nameof(CellTypeIndex));
        }

        private static string Hex(int? value) => value switch { null or -1 => "", -2 => "mixed", int v => v.ToString("X2") };

        private void SetMovement(int layer, string said)
        {
            if (Chosen == null) return;
            said = (said ?? "").Trim();
            if (said == "mixed") return;
            if (said.Length == 0) Chosen.SetWholeCollision(layer, -1);
            else if (int.TryParse(said, System.Globalization.NumberStyles.HexNumber, null, out int v) && v >= 0 && v <= 0xff)
                Chosen.SetWholeCollision(layer, v);
            else { Warning = $"Not a hex byte: {said}"; return; }
            Dirty = true;
            Raise(nameof(SetHasMovement));
        }

        public ObservableCollection<string> TilePictures { get; } = new ObservableCollection<string>();

        private string _lookPicture;
        public string LookPicture
        {
            get => _lookPicture;
            set { if (_lookPicture == value) return; _lookPicture = value; Raise(); RaiseLook(); }
        }

        private MaterialLook CurrentLook
            => Chosen?.Faces.FirstOrDefault(f => f.Picture == _lookPicture)?.Look ?? MaterialLook.Plain;

        public int LookAlpha { get => CurrentLook.Alpha; set => ChangeLook(l => l.With(alpha: value)); }
        public bool LookBothSides { get => CurrentLook.BothSides; set => ChangeLook(l => l.With(bothSides: value)); }
        public bool LookFog { get => CurrentLook.Fog; set => ChangeLook(l => l.With(fog: value)); }
        public bool LookLight0 { get => (CurrentLook.Lights & 1) != 0; set => Light(0, value); }
        public bool LookLight1 { get => (CurrentLook.Lights & 2) != 0; set => Light(1, value); }
        public bool LookLight2 { get => (CurrentLook.Lights & 4) != 0; set => Light(2, value); }
        public bool LookLight3 { get => (CurrentLook.Lights & 8) != 0; set => Light(3, value); }

        private void Light(int which, bool on)
        {
            int lights = CurrentLook.Lights;
            lights = on ? lights | (1 << which) : lights & ~(1 << which);
            ChangeLook(l => l.With(lights: lights));
        }

        private void ChangeLook(Func<MaterialLook, MaterialLook> change)
        {
            if (_set == null || string.IsNullOrEmpty(_lookPicture)) return;
            _set.ChangeLook(_lookPicture, change);
            RaiseLook();
            SetChanged(redraw: false);
        }

        private void RaiseLook()
        {
            foreach (string n in new[] { nameof(LookAlpha), nameof(LookBothSides), nameof(LookFog),
                                         nameof(LookLight0), nameof(LookLight1), nameof(LookLight2), nameof(LookLight3) })
                Raise(n);
        }

        private void ShowChosen()
        {
            TilePictures.Clear();
            if (Chosen != null) foreach (string p in Chosen.Pictures) TilePictures.Add(p);
            _lookPicture = TilePictures.FirstOrDefault();
            foreach (string n in new[] { nameof(HasChosen), nameof(TileName), nameof(TileWide), nameof(TileDeep), nameof(LookPicture),
                                         nameof(TileJoinsAcross), nameof(TileJoinsDown), nameof(TileRepeatsAcross), nameof(TileRepeatsDown),
                                         nameof(TileAcrossTheMap), nameof(TileGroundKind), nameof(TileWalk), nameof(SetHasMovement),
                                         nameof(AnimationNote), nameof(IsAnimated), nameof(CanAnimate) })
                Raise(n);
            RaiseLook();
            FillCollisionCells();
        }
    }
}
