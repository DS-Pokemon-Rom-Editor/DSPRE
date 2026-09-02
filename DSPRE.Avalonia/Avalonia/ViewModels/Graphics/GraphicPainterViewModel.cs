using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>Painting one graphic, by the numbers it is really made of.</summary>
    public sealed class GraphicPainterViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        /// <summary>One colour in the list, with the number that picks it.</summary>
        /// <summary>Properties, not fields: the compiled bindings this project uses cannot see fields.</summary>
        public sealed class Swatch
        {
            public int Number { get; init; }
            public IBrush Fill { get; init; }
            public string Tip { get; init; }
        }

        private readonly GraphicAssets.Archive _archive;
        private readonly int _index;
        private GraphicAssets.Indexed _art;
        private byte[] _pixels;          // what is being painted, straightened out
        private uint[] _colours;

        // Every step back, as whole pictures. These are small, at most a few tens of thousands of bytes,
        // so keeping them whole is simpler than remembering which pixels moved and just as quick.
        private readonly Stack<(byte[] pixels, uint[] colours)> _undo = new();

        public GraphicPainterViewModel(GraphicAssets.Archive archive, int index)
        {
            _archive = archive;
            _index = index;
            Load();
        }

        private void Load()
        {
            _art = GraphicAssets.ReadIndexed(_archive, _index, out string why);
            if (_art == null)
            {
                Trouble = why ?? "This entry cannot be painted.";
                OnPropertyChanged(nameof(Trouble));
                OnPropertyChanged(nameof(HasTrouble));
                OnPropertyChanged(nameof(CanPaint));
                return;
            }
            _pixels = (byte[])_art.Indices.Clone();
            _colours = _art.Palette.Take(_art.ColourCount).ToArray();
            BuildSwatches();
            BuildFrames();
            Redraw();
            string named = null;
            try { named = _archive.NameOf?.Invoke(_index); } catch { }
            Title = named == null
                ? $"{_archive.Title}, number {_index}"
                : $"{named} ({_archive.Title}, number {_index})";
            Explain = "Every pixel holds a number that picks one of the colours below. Changing a colour "
                    + "changes every pixel using it.";
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Explain));
            OnPropertyChanged(nameof(CanPaint));
            RaiseView();
        }

        public string Title { get; private set; } = "Paint";
        public string Explain { get; private set; } = "";
        public string Trouble { get; private set; } = "";
        public bool HasTrouble => !string.IsNullOrEmpty(Trouble);
        public bool CanPaint => _art != null;

        public int Width => _art?.Width ?? 0;
        public int Height => _art?.Height ?? 0;

        // Several of these files hold more than one picture.

        public sealed class Frame
        {
            public string Name { get; init; }
            public int X { get; init; }
            public int Y { get; init; }
            public int W { get; init; }
            public int H { get; init; }
        }

        public ObservableCollection<Frame> Frames { get; } = new();
        public bool HasFrames => Frames.Count > 1;

        private void BuildFrames()
        {
            Frames.Clear();
            if (_art == null) return;

            int fw = _archive.FrameWidth, fh = _archive.FrameHeight;
            bool split = fw > 0 && fh > 0 && fw <= _art.Width && fh <= _art.Height
                      && (fw < _art.Width || fh < _art.Height);
            if (split)
            {
                int across = _art.Width / fw, down = _art.Height / fh;
                int n = 1;
                for (int row = 0; row < down; row++)
                    for (int col = 0; col < across; col++)
                        Frames.Add(new Frame { Name = "Frame " + n++, X = col * fw, Y = row * fh, W = fw, H = fh });
            }

            if (Frames.Count > 1)
                Frames.Add(new Frame { Name = "All of it", X = 0, Y = 0, W = _art.Width, H = _art.Height });
            else Frames.Clear();

            // Clear it first: the list was just refilled, so the box has dropped to nothing selected.
            _frameIndex = -1;
            OnPropertyChanged(nameof(FrameIndex));
            _frameIndex = 0;
            OnPropertyChanged(nameof(FrameIndex));
            OnPropertyChanged(nameof(HasFrames));
        }

        private int _frameIndex;
        public int FrameIndex
        {
            get => _frameIndex;
            set { if (Set(ref _frameIndex, value)) RaiseView(); }
        }

        private Frame Showing => _frameIndex >= 0 && _frameIndex < Frames.Count ? Frames[_frameIndex] : null;

        public int ViewX => Showing?.X ?? 0;
        public int ViewY => Showing?.Y ?? 0;
        public int ViewWidth => Showing?.W ?? Width;
        public int ViewHeight => Showing?.H ?? Height;

        private int _zoom = 6;
        /// <summary>How many screen pixels across one pixel of the drawing is shown.</summary>
        public int Zoom
        {
            get => _zoom;
            set { if (Set(ref _zoom, Math.Clamp(value, 1, 32))) RaiseView(); }
        }

        public bool CanZoomIn => _zoom < 32;
        public bool CanZoomOut => _zoom > 1;
        public string ZoomInHelp => CanZoomIn ? "Show the pixels larger." : "Already as large as it goes.";
        public string ZoomOutHelp => CanZoomOut ? "Show the pixels smaller." : "Already as small as it goes.";
        public void ZoomIn() => Zoom = _zoom < 4 ? _zoom + 1 : _zoom + 2;
        public void ZoomOut() => Zoom = _zoom <= 4 ? _zoom - 1 : _zoom - 2;

        public int CanvasWidth => Math.Max(1, ViewWidth * _zoom);
        public int CanvasHeight => Math.Max(1, ViewHeight * _zoom);

        private bool _showGrid = true;
        public bool ShowGrid
        {
            get => _showGrid;
            set { if (Set(ref _showGrid, value)) OnPropertyChanged(nameof(GridOn)); }
        }

        /// <summary>Lines between the pixels are only worth drawing once the pixels are big enough to have
        /// room for them, so below that the grid stays off however the box is ticked.</summary>
        public bool GridOn => _showGrid && _zoom >= 4;

        public string GridHelp => _zoom >= 4
            ? "Draw a line between every pixel."
            : "The pixels are too small to fit lines between. Zoom in first.";

        private void RaiseView()
        {
            Redraw();
            OnPropertyChanged(nameof(ViewX)); OnPropertyChanged(nameof(ViewY));
            OnPropertyChanged(nameof(ViewWidth)); OnPropertyChanged(nameof(ViewHeight));
            OnPropertyChanged(nameof(CanvasWidth)); OnPropertyChanged(nameof(CanvasHeight));
            OnPropertyChanged(nameof(CanZoomIn)); OnPropertyChanged(nameof(CanZoomOut));
            OnPropertyChanged(nameof(ZoomInHelp)); OnPropertyChanged(nameof(ZoomOutHelp));
            OnPropertyChanged(nameof(GridOn)); OnPropertyChanged(nameof(GridHelp));
            OnPropertyChanged(nameof(Measurements));
        }

        public string Measurements
        {
            get
            {
                if (_art == null) return "";
                string size = Showing != null && (Showing.W < _art.Width || Showing.H < _art.Height)
                    ? $"{ViewWidth} by {ViewHeight}, out of {_art.Width} by {_art.Height}"
                    : $"{_art.Width} by {_art.Height}";
                return $"{size}. {_art.ColourCount} colours. Shown {_zoom} times over.";
            }
        }

        public ObservableCollection<Swatch> Colours { get; } = new();

        private void BuildSwatches()
        {
            Colours.Clear();
            for (int i = 0; i < _colours.Length; i++)
            {
                uint c = _colours[i];
                Colours.Add(new Swatch
                {
                    Number = i,
                    Fill = new SolidColorBrush(Color.FromArgb(
                        i == 0 ? (byte)255 : (byte)255,
                        (byte)((c >> 16) & 0xFF), (byte)((c >> 8) & 0xFF), (byte)(c & 0xFF))),
                    Tip = i == 0
                        ? "Colour 0. In most of these graphics this one is the see-through part."
                        : $"Colour {i}.",
                });
            }
            OnPropertyChanged(nameof(Colours));

            // Start with a colour already picked. The brush has one from the outset, so leaving the strip
            // with nothing highlighted showed no sign of which one, and left Change this colour dead.
            SelectedSwatch = Colours.FirstOrDefault(s => s.Number == _ink) ?? Colours.FirstOrDefault();
        }

        private Swatch _selectedSwatch;
        /// <summary>The colour picked in the strip below, which is both what the brush lays down and what
        /// Change this colour would alter.</summary>
        public Swatch SelectedSwatch
        {
            get => _selectedSwatch;
            set
            {
                if (!Set(ref _selectedSwatch, value)) return;
                if (value != null) Ink = value.Number;
                OnPropertyChanged(nameof(CanChangeColour));
                OnPropertyChanged(nameof(ChangeColourHelp));
            }
        }

        public bool CanChangeColour => _art != null && _selectedSwatch != null;

        public string ChangeColourHelp => _art == null
            ? "There is nothing here to paint."
            : _selectedSwatch == null
                ? "Pick one of the colours below first, then this will change it."
                : $"Change colour {_selectedSwatch.Number} itself. Every pixel already using it changes too.";

        private int _ink = 1;
        /// <summary>The number the brush lays down.</summary>
        public int Ink
        {
            get => _ink;
            set { if (Set(ref _ink, Math.Clamp(value, 0, Math.Max(0, _colours?.Length - 1 ?? 0)))) OnPropertyChanged(nameof(InkDescription)); }
        }

        public string InkDescription => _art == null ? "" :
            _ink == 0 ? "Painting with colour 0, which is usually the see-through one."
                      : $"Painting with colour {_ink}.";

        private Bitmap _picture;
        public Bitmap Picture { get => _picture; private set => Set(ref _picture, value); }

        private void Redraw()
        {
            if (_art == null) return;
            var whole = GraphicAssets.Flatten(_pixels, _colours, _art.Width, _art.Height);

            int vx = ViewX, vy = ViewY, vw = ViewWidth, vh = ViewHeight;
            if (vx == 0 && vy == 0 && vw == _art.Width && vh == _art.Height)
            {
                Picture = ImageConverter.FromRgba(whole, _art.Width, _art.Height);
                return;
            }

            var part = new byte[vw * vh * 4];
            for (int y = 0; y < vh; y++)
                Array.Copy(whole, ((vy + y) * _art.Width + vx) * 4, part, y * vw * 4, vw * 4);
            Picture = ImageConverter.FromRgba(part, vw, vh);
        }

        private void Remember()
        {
            _undo.Push(((byte[])_pixels.Clone(), (uint[])_colours.Clone()));
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(UndoHelp));
        }

        public bool CanUndo => _undo.Count > 0;

        public string UndoHelp => _art == null
            ? "There is nothing here to paint."
            : _undo.Count == 0
                ? "Nothing has been changed yet, so there is nothing to step back from."
                : "Step back to how it was before the last stroke or colour change.";

        public void Undo()
        {
            if (_undo.Count == 0) return;
            var (p, c) = _undo.Pop();
            _pixels = p; _colours = c;
            BuildSwatches();
            Redraw();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(UndoHelp));
            Status = "Put back the way it was.";
        }

        /// <summary>Lays the current number down at one spot. Called as the pointer moves, so it only
        /// remembers a step when a stroke starts.</summary>
        public void Paint(int x, int y, bool startOfStroke)
        {
            if (_art == null) return;
            // The pointer gives a spot in the frame on screen, which sits elsewhere in the whole file.
            x += ViewX; y += ViewY;
            if (x < 0 || y < 0 || x >= _art.Width || y >= _art.Height) return;
            if (startOfStroke) Remember();
            int at = y * _art.Width + x;
            if (at < 0 || at >= _pixels.Length) return;
            if (_pixels[at] == (byte)_ink) return;
            _pixels[at] = (byte)_ink;
            Redraw();
        }

        /// <summary>Changes one colour in the list. Every pixel already using it changes with it.</summary>
        public void SetColour(int number, byte r, byte g, byte b)
        {
            if (_art == null || number < 0 || number >= _colours.Length) return;
            Remember();
            _colours[number] = (uint)(0xFF000000 | (r << 16) | (g << 8) | b);
            BuildSwatches();
            Redraw();
            int using_ = _pixels.Count(v => v == number);
            Status = using_ == 0
                ? $"Colour {number} changed. Nothing is using it at the moment."
                : $"Colour {number} changed, and the {using_} pixels using it changed with it.";
        }

        private string _status = "";
        public string Status { get => _status; set => Set(ref _status, value); }

        /// <summary>Writes the painting back into the game. Says why when it cannot, having changed
        /// nothing.</summary>
        public string Save()
        {
            if (_art == null) return Trouble;
            string err = GraphicAssets.WriteIndices(_archive, _index, _pixels, _art);
            if (err != null) return err;

            // The colours are a separate file, so they are written separately and only when they changed.
            if (!_colours.SequenceEqual(_art.Palette.Take(_colours.Length)))
            {
                string perr = GraphicAssets.WritePalette(_archive, _index, _colours);
                if (perr != null)
                    return "The picture went in, but the colours did not: " + perr;
            }
            _undo.Clear();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(UndoHelp));
            return null;
        }
    }
}
