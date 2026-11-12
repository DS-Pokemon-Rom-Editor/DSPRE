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
    /// <summary>Tile painting for a map model, PDSMS style.</summary>
    public partial class MapTilesViewModel : INotifyPropertyChanged
    {
        public sealed class TileRow : INotifyPropertyChanged
        {
            public int Index { get; set; }
            public string Name { get; set; }
            public string Pictures { get; set; }
            public string Size { get; set; }
            public global::Avalonia.Media.IBrush Swatch { get; set; }

            public AvBitmap Picture { get; set; }

            // Drawn only when the tooltip first asks for it.
            internal Func<AvBitmap> MakeBig;
            private AvBitmap _big;
            public AvBitmap Big => _big ??= MakeBig?.Invoke();

            private int _uses;
            public int Uses
            {
                get => _uses;
                set { if (_uses == value) return; _uses = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Uses))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UsesText))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Fade))); }
            }
            public double Fade => _uses == 0 ? 0.55 : 1;
            public string UsesText => _uses == 0 ? "not on this map" : _uses == 1 ? "placed once" : $"placed {_uses} times";

            public string Label => $"{Name}  ·  {Pictures}{Size}";
            public override string ToString() => Label;
            public event PropertyChangedEventHandler PropertyChanged;
        }

        public sealed class ToolRow
        {
            public TilePainter.Tool Tool { get; set; }
            public string Icon { get; set; }
            public string Tip { get; set; }
        }

        public sealed class LayerRow : INotifyPropertyChanged
        {
            public int Index { get; set; }
            public string Name => $"Layer {Index + 1}";

            private bool _visible = true;
            public bool Visible
            {
                get => _visible;
                set { if (_visible == value) return; _visible = value; Changed(); VisibilityChanged?.Invoke(this, EventArgs.Empty); }
            }

            private int _painted;
            public int Painted { get => _painted; set { if (_painted == value) return; _painted = value; Changed(); Changed(nameof(Said)); } }
            public string Said => _painted == 0 ? "" : _painted.ToString();

            public event EventHandler VisibilityChanged;
            public event PropertyChangedEventHandler PropertyChanged;
            private void Changed([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        }

        public sealed class HeightRow
        {
            public int Value { get; set; }
            public string Said => Value > 0 ? "+" + Value : Value.ToString();
            public global::Avalonia.Media.IBrush Swatch { get; set; }
        }

        public sealed class SmartRow
        {
            public int Index { get; set; }
            public AvBitmap Picture { get; set; }
        }

        public sealed class SlotRow
        {
            public int Slot { get; set; }
            public AvBitmap Picture { get; set; }
            public string Tip { get; set; }
        }

        private MapFile _map;
        private byte _areaId;
        private GameFamilies _family;
        private MapTileset _set;
        private readonly TilePainter _painter = new TilePainter();
        private readonly List<byte[]> _before = new List<byte[]>();

        private Dictionary<string, (byte[] rgba, int w, int h)> _pictures =
            new Dictionary<string, (byte[] rgba, int w, int h)>(StringComparer.Ordinal);
        private readonly Dictionary<(int tile, int turn), TileGridControl.Look?> _fromAbove = new Dictionary<(int, int), TileGridControl.Look?>();
        private readonly Dictionary<int, AvBitmap> _thumbs = new Dictionary<int, AvBitmap>();

        public NsbmdRenderModel Model3D { get; private set; }

        public NsbmdRenderModel Tile3D { get; private set; }

        public ObservableCollection<TileRow> Tiles { get; } = new ObservableCollection<TileRow>();

        /// <summary>The tile list as shown: tiles this map uses first, narrowed by the search.</summary>
        public ObservableCollection<TileRow> ShownTiles { get; } = new ObservableCollection<TileRow>();

        private string _tileSearch = "";
        public string TileSearch
        {
            get => _tileSearch;
            set { if (_tileSearch == (value ?? "")) return; _tileSearch = value ?? ""; Raise(); ShowTileList(); }
        }

        public TileRow ShownTile
        {
            get => Brush >= 0 && Brush < Tiles.Count ? Tiles[Brush] : null;
            set { if (value != null) Brush = value.Index; }
        }
        public ObservableCollection<LayerRow> Layers { get; } = new ObservableCollection<LayerRow>();
        public ObservableCollection<HeightRow> HeightPalette { get; } = new ObservableCollection<HeightRow>();
        public ObservableCollection<SmartRow> SmartDrawings { get; } = new ObservableCollection<SmartRow>();
        public ObservableCollection<SlotRow> Slots { get; } = new ObservableCollection<SlotRow>();

        public IReadOnlyList<ToolRow> Tools { get; } = new[]
        {
            new ToolRow { Tool = TilePainter.Tool.Paint, Icon = "pencil", Tip = "Paint (right: pick, middle: fill) (B)" },
            new ToolRow { Tool = TilePainter.Tool.Clear, Icon = "eraser", Tip = "Erase (middle: erase area) (E)" },
            new ToolRow { Tool = TilePainter.Tool.Smart, Icon = "smartfill", Tip = "Smart fill (S)" },
            new ToolRow { Tool = TilePainter.Tool.SmartInverted, Icon = "smartfill_inv", Tip = "Inverted smart fill (Shift+S)" },
            new ToolRow { Tool = TilePainter.Tool.Bucket, Icon = "bucket", Tip = "Fill (G)" },
            new ToolRow { Tool = TilePainter.Tool.Picker, Icon = "picker", Tip = "Picker (I)" },
            new ToolRow { Tool = TilePainter.Tool.Line, Icon = "line", Tip = "Line (L)" },
            new ToolRow { Tool = TilePainter.Tool.Rectangle, Icon = "rectangle", Tip = "Rectangle (U)" },
            new ToolRow { Tool = TilePainter.Tool.Ellipse, Icon = "ellipse", Tip = "Ellipse (O)" },
            new ToolRow { Tool = TilePainter.Tool.Select, Icon = "select", Tip = "Select (M)" },
            new ToolRow { Tool = TilePainter.Tool.Lasso, Icon = "lasso", Tip = "Lasso (Q)" },
            new ToolRow { Tool = TilePainter.Tool.Wand, Icon = "wand", Tip = "Magic wand (W)" },
        };

        public MapTilesViewModel()
        {
            for (int i = 0; i < TileGrid.Layers; i++)
            {
                LayerRow row = new LayerRow { Index = i };
                row.VisibilityChanged += (_, _) => GridChanged?.Invoke(this, EventArgs.Empty);
                Layers.Add(row);
            }
            for (int h = TileGrid.HighestStep; h >= TileGrid.LowestStep; h--)
                HeightPalette.Add(new HeightRow
                {
                    Value = h,
                    Swatch = new global::Avalonia.Media.SolidColorBrush(TileGridControl.HeightColour(h)),
                });
        }

        public int Brush
        {
            get => _painter.Brush;
            set
            {
                if (_painter.Brush == value) return;
                _painter.Brush = value;
                Raise();
                Raise(nameof(ShownTile));
                Describe();
                ShowTile();
                ShowChosen();
            }
        }

        public int Turn
        {
            get => _painter.Turn;
            set
            {
                byte turn = (byte)(((value % 4) + 4) % 4);
                if (_painter.Turn == turn) return;
                _painter.Turn = turn;
                Raise();
                Describe();
                ShowTile();
            }
        }

        public int ToolIndex
        {
            get => Array.FindIndex(Tools.ToArray(), t => t.Tool == _painter.Current);
            set
            {
                if (value < 0 || value >= Tools.Count || Tools[value].Tool == _painter.Current) return;
                _painter.Current = Tools[value].Tool;
                Raise();
            }
        }

        public int ActiveLayer
        {
            get => _painter.Layer;
            set
            {
                if (value < 0 || value >= TileGrid.Layers || value == _painter.Layer) return;
                _painter.Layer = value;
                Raise();
                GridChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool HeightsAllLayers
        {
            get => _painter.HeightsAllLayers;
            set { if (_painter.HeightsAllLayers == value) return; _painter.HeightsAllLayers = value; Raise(); }
        }

        private bool _showBuildings = true;
        public bool ShowBuildings
        {
            get => _showBuildings;
            set { if (Set(ref _showBuildings, value)) GridChanged?.Invoke(this, EventArgs.Empty); }
        }

        private bool _dimOtherLayers;
        public bool DimOtherLayers
        {
            get => _dimOtherLayers;
            set { if (Set(ref _dimOtherLayers, value)) GridChanged?.Invoke(this, EventArgs.Empty); }
        }

        public List<string> TileViews { get; } = new List<string> { "Automatic", "Angled", "From above" };

        // Automatic shows flat ground from above, where its texture reads best, and the rest angled.
        private int _tileViewIndex = Math.Clamp(SettingsManager.Settings?.MapTilesView ?? 0, 0, 2);
        public int TileViewIndex
        {
            get => _tileViewIndex;
            set
            {
                if (value < 0 || !Set(ref _tileViewIndex, value)) return;
                if (SettingsManager.Settings != null) { SettingsManager.Settings.MapTilesView = value; try { SettingsManager.Save(); } catch (Exception ex) { AppLogger.Warn("Saving the map tiles view: " + ex.Message); } }
                _thumbs.Clear();
                FillList();
            }
        }

        private bool ShowsFromAbove(MapTileset.Tile tile) => _tileViewIndex switch
        {
            1 => false,
            2 => true,
            _ => tile.IsGround && tile.Corners.Max(c => c.Y) - tile.Corners.Min(c => c.Y) <= MapTileset.TileWidth / 4f,
        };

        private byte[] DrawThumbnail(MapTileset.Tile tile, int size)
        {
            if (!ShowsFromAbove(tile)) return TileThumbnail.Draw(tile, size, Picture);
            int squares = Math.Max(1, Math.Max(tile.Wide, tile.Deep));
            (byte[] rgba, int wide, int tall) drawn = TileThumbnail.DrawFromAbove(tile, 0, Math.Max(1, size / squares), Picture);
            byte[] fitted = new byte[size * size * 4];
            int ox = (size - drawn.wide) / 2, oy = (size - drawn.tall) / 2;
            for (int y = 0; y < drawn.tall; y++)
                for (int x = 0; x < drawn.wide; x++)
                {
                    int tx = ox + x, ty = oy + y;
                    if (tx < 0 || ty < 0 || tx >= size || ty >= size) continue;
                    Array.Copy(drawn.rgba, (y * drawn.wide + x) * 4, fitted, (ty * size + tx) * 4, 4);
                }
            return fitted;
        }

        public bool ShowHeights
        {
            get => _painter.Heights;
            set { if (_painter.Heights == value) return; _painter.Heights = value; Raise(); GridChanged?.Invoke(this, EventArgs.Empty); }
        }

        /// <summary>0 paints the chosen height, 1 raises and 2 lowers by one step.</summary>
        public int HeightWayIndex
        {
            get => (int)_painter.HeightChange;
            set { if ((int)_painter.HeightChange == value || value < 0) return; _painter.HeightChange = (TilePainter.HeightWay)value; Raise(); Raise(nameof(PaintsHeight)); }
        }

        public bool PaintsHeight => _painter.HeightChange == TilePainter.HeightWay.Set;

        public int HeightBrush
        {
            get => _painter.HeightBrush;
            set
            {
                value = Math.Clamp(value, TileGrid.LowestStep, TileGrid.HighestStep);
                if (_painter.HeightBrush == value) return;
                _painter.HeightBrush = value;
                Raise();
                Raise(nameof(HeightIndex));
            }
        }

        public int HeightIndex
        {
            get => TileGrid.HighestStep - HeightBrush;
            set { if (value >= 0) HeightBrush = TileGrid.HighestStep - value; }
        }

        public bool SmartTools
        {
            get => _painter.SmartTools;
            set { if (_painter.SmartTools == value) return; _painter.SmartTools = value; Raise(); }
        }

        public int SmartIndex
        {
            get => _set == null || _set.SmartDrawings.Count == 0 ? -1 : _painter.SmartIndex;
            set
            {
                if (value < 0 || _set == null || value >= _set.SmartDrawings.Count || value == _painter.SmartIndex) return;
                _painter.SmartIndex = value;
                Raise();
                FillSlots();
            }
        }

        public bool HasSmartDrawing => _set != null && _set.SmartDrawings.Count > 0;

        private string _note = "";
        public string Note { get => _note; private set => Set(ref _note, value); }

        private string _under = "";
        public string Under { get => _under; private set => Set(ref _under, value); }

        private string _warning;
        public string Warning { get => _warning; private set => Set(ref _warning, value); }

        private bool _dirty;
        public bool Dirty { get => _dirty; private set => Set(ref _dirty, value); }

        private bool _alsoTerrain = true;
        public bool AlsoTerrain { get => _alsoTerrain; set => Set(ref _alsoTerrain, value); }

        private void ShowTile()
        {
            Tile3D = null;
            if (_set != null && _map != null && Brush >= 0 && Brush < _set.Tiles.Count)
            {
                try
                {
                    MapTileset.Tile tile = _set.Tiles[Brush];
                    TileGrid one = new TileGrid();
                    one.Put(12, 12, Brush, 0f, (byte)Turn, tile.Wide, tile.Deep);

                    byte[] model = TileBake.ToModel(TileBake.Of(one, _set), out _, TileScale);
                    if (model != null)
                    {
                        MapFile shown = new MapFile(new MemoryStream(_map.ToByteArray()), _family,
                                                discardMoveperms: false, showMessages: false);
                        shown.LoadMapModel(model, showMessages: false);
                        shown.MapModelData = model;
                        shown.Buildings.Clear();

                        Tile3D = MatrixSceneBuilder.BuildFromPlaced(_family,
                            new[] { (0, 0, shown, _areaId, 0f, 0f, 0f) },
                            NsbmdGeometry.MatrixStitchMode.Grid);
                    }
                }
                catch (Exception ex) { AppLogger.Error("MapTiles.ShowTile: " + ex.Message); }
            }

            Raise(nameof(Tile3D));
            TileShown?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler TileShown;

        private const float TileScale = 64f;

        private void Describe()
        {
            if (_set == null || Brush < 0 || Brush >= _set.Tiles.Count) return;
            MapTileset.Tile tile = _set.Tiles[Brush];
            Note = $"{tile.Name}  ·  {string.Join(", ", tile.Pictures)}"
                 + (tile.Spreads ? $"  ·  {tile.Wide}x{tile.Deep}" : "");
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
