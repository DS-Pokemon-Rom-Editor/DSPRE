using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed class ScreenWindowRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly int _scrolled;

        public ScreenWindowRow(ScreenWindowLayout.Window window, string colour, int scrolled)
        {
            Window = window;
            Colour = colour;
            _scrolled = scrolled;
        }

        public ScreenWindowLayout.Window Window { get; }
        public string Name => Window.Name;
        public string Colour { get; }
        /// <summary>The row the game's code forces in the shown state, if any.</summary>
        public int? FixedTop { get; init; }
        public int ShownY => (FixedTop ?? Window.Y) - _scrolled;
        /// <summary>Tiles right of its table place that a window inside a frame is drawn.</summary>
        public int ShiftX { get; init; }
        public int ShownX => Window.X + ShiftX;

        private string _warning = "";
        public string Warning
        {
            get => _warning;
            set
            {
                if (_warning == value) return;
                _warning = value;
                Raise(nameof(Warning));
                Raise(nameof(HasWarning));
            }
        }
        public bool HasWarning => _warning.Length > 0;
        public int Scrolled => _scrolled;

        public Action<ScreenWindowRow, int, int> MoveTo;
        public Action<ScreenWindowRow, int, int> SizeTo;

        public int Across { get => Window.X; set => MoveTo?.Invoke(this, value, Window.Y); }
        public bool OnScreenTiles { get; init; }
        public int MaxAcross => Math.Min(Window.MaxX, OnScreenTiles ? Math.Max(0, ScreenWindowLayout.MapColumns - Window.Width) : 31);
        public int MaxDown => Math.Min(Window.MaxY, OnScreenTiles ? Math.Max(0, ScreenWindowLayout.MapHeight - Window.Height) : 31);
        public int MinAcross => Window.MinX;
        public int MinDown => Window.MinY;
        public int Down { get => Window.Y; set => MoveTo?.Invoke(this, Window.X, value); }
        public bool CanAcross => Window.CanMoveAcross;
        /// <summary>The toolbox key of the patch that lets this window move.</summary>
        public string LockedFor => Window.LockedFor;
        public bool NeedsPatch => Window.LockedFor != null;
        private string Locked => NeedsPatch ? "Moves once the Screen positions patch is in." : "Can't move: the game shares this position with something else.";
        public bool CanDown => Window.CanMoveDown;
        public string AcrossTip => CanAcross ? "Across" : Window.LockReason ?? Locked;
        public string DownTip => !CanDown ? Window.LockReason ?? Locked
            : Window.Bounded ? $"Down, to row {Window.MaxY} at most. The window stays inside its frame."
            : Window.MaxY + Window.Height < DsBgScreen.Height / DsBgScreen.TileSize ? $"Down. The game keeps this window's position in a single byte, so it can't go below row {Window.MaxY}." : "Down";
        public bool Sizable => SizeTo != null;
        public string SizeElsewhere { get; init; }
        public string WideTip => Sizable ? "Width in tiles, up to the original width." : SizeElsewhere ?? "The game's code sets this window's size.";
        public string TallTip => Sizable ? "Height in tiles, up to the original height." : SizeElsewhere ?? "The game's code sets this window's size.";
        public int Wide { get => Window.Width; set => SizeTo?.Invoke(this, value, Window.Height); }
        public int Tall { get => Window.Height; set => SizeTo?.Invoke(this, Window.Width, value); }
        public int MaxWide => Math.Max(1, Window.MaxWidth);

        public Func<int> GetFont { get; init; }
        public Action<int> SetFont { get; init; }
        public IReadOnlyList<string> FontNames { get; init; }
        public bool CanPickFont => SetFont != null;
        public int FontIndex { get => GetFont?.Invoke() ?? 0; set => SetFont?.Invoke(value); }
        public int MaxTall => Math.Max(1, Window.MaxHeight);

        public double Left => ShownX * ScreenWindowLayoutViewModel.Tile;
        public double Top => ShownY * ScreenWindowLayoutViewModel.Tile;
        public double Width => Window.Width * ScreenWindowLayoutViewModel.Tile;
        public double Height => Window.Height * ScreenWindowLayoutViewModel.Tile;

        public void Refresh()
        {
            foreach (string n in new[] { nameof(Across), nameof(Down), nameof(MaxAcross), nameof(MaxDown), nameof(Wide), nameof(Tall), nameof(Left), nameof(Top), nameof(Width), nameof(Height), nameof(FontIndex) })
                Raise(n);
        }
    }
}

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>A screen's touch buttons for its Layout tab. PageOf gives -1 for no page.</summary>
    public sealed record ScreenButtonsLink(Func<ScreenButtonsViewModel> Model, Func<int, int> PageOf,
                                           IReadOnlyDictionary<string, (int Page, int Button)[]> Carries = null);
}
