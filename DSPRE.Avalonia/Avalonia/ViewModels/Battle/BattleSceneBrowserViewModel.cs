using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    /// <summary>The battle scenery of every place in the game, with the data that chooses it.</summary>
    public sealed class BattleSceneBrowserViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        private readonly BattleBgRenderer _backdrops = new();
        private readonly BattleGroundRenderer _grounds = new();

        public BattleSceneBrowserViewModel() => Reload();

        public ObservableCollection<BattleScenes.Scene> Scenes { get; } = new();

        public void Reload()
        {
            Scenes.Clear();
            try { foreach (var s in BattleScenes.Read()) Scenes.Add(s); }
            catch (Exception ex) { AppLogger.Error("BattleSceneBrowser.Reload: " + ex.Message); }

            Terrains.Clear();
            for (int t = 0; t < BattleGroundRenderer.TerrainCount; t++)
                Terrains.Add(BattleGroundRenderer.TerrainNames[t]);

            OnPropertyChanged(nameof(FoundSummary));
            Selected = Scenes.FirstOrDefault();
        }

        public string FoundSummary => Scenes.Count == 0
            ? "This game has no battle scenery DSPRE can list. Open a ROM first."
            : $"{Scenes.Count} sets of battle scenery. The number beside each is what a map header carries "
              + "to ask for it.";

        private BattleScenes.Scene _selected;
        public BattleScenes.Scene Selected
        {
            get => _selected;
            set { if (Set(ref _selected, value)) Look(); }
        }

        public ObservableCollection<string> Terrains { get; } = new();

        private int _terrainIndex = 2;      // Lawn, the one most places use
        public int TerrainIndex
        {
            get => _terrainIndex;
            set { if (Set(ref _terrainIndex, value)) Look(); }
        }

        private int _timeOfDay;
        /// <summary>0 day, 1 evening, 2 night. Each set of scenery is painted three ways.</summary>
        public int TimeOfDay
        {
            get => _timeOfDay;
            set { if (Set(ref _timeOfDay, value)) Look(); }
        }

        public ObservableCollection<string> TimesOfDay { get; } = new() { "Day", "Evening", "Night" };

        private Bitmap _picture;
        public Bitmap Picture { get => _picture; private set => Set(ref _picture, value); }

        private string _whynot = "";
        public string Whynot { get => _whynot; private set => Set(ref _whynot, value); }
        public bool HasPicture => _picture != null;
        public bool HasNoPicture => _picture == null && !string.IsNullOrEmpty(_whynot);

        private string _details = "Pick a place on the left to see what it fights on.";
        public string Details { get => _details; private set => Set(ref _details, value); }

        /// <summary>Every place that fights on this scenery, so the number means something.</summary>
        public ObservableCollection<string> Places { get; } = new();

        private string _placesHeader = "";
        public string PlacesHeader { get => _placesHeader; private set => Set(ref _placesHeader, value); }

        private bool _hasPlaces;
        public bool HasPlaces { get => _hasPlaces; private set => Set(ref _hasPlaces, value); }

        private void Look()
        {
            Picture = null;
            Whynot = "";
            if (_selected == null)
            {
                Details = "Pick a place on the left to see what it fights on.";
                Places.Clear();
                HasPlaces = false;
                RaiseAll();
                return;
            }

            var s = _selected;
            Details = $"Battle scenery {s.BackgroundId}. Drawn from file {s.Drawing}, arranged by file "
                    + $"{s.Arrangement}, painted from file {s.PaletteDay + _timeOfDay}. "
                    + (s.Headers.Count == 0
                        ? "No place in this game fights here, so it is spare."
                        : $"{s.Headers.Count} place{(s.Headers.Count == 1 ? "" : "s")} fight here.");

            // The whole list rather than the first forty, in a box that scrolls, so nothing trails off
            // into "and more".
            Places.Clear();
            if (s.PlaceNames.Count > 0)
                foreach (var n in s.PlaceNames.Distinct()) Places.Add(n);
            else
                foreach (var h in s.Headers) Places.Add("Header " + h);
            HasPlaces = Places.Count > 0;
            PlacesHeader = Places.Count == 1 ? "The one place that fights here"
                                             : $"The {Places.Count} places that fight here";

            try
            {
                var bg = _backdrops.BuildBackdrop(s.BackgroundId, _timeOfDay);
                if (bg?.Rgba == null)
                {
                    Whynot = "This scenery could not be drawn. The colours it names may not be a palette "
                           + "in this game.";
                    RaiseAll();
                    return;
                }

                var scene = Compose(bg);
                Picture = ImageConverter.FromRgba(scene.Rgba, scene.Width, scene.Height);
            }
            catch (Exception ex)
            {
                AppLogger.Error("BattleSceneBrowser.Look: " + ex.Message);
                Whynot = "This scenery could not be drawn.";
            }
            RaiseAll();
        }

        /// <summary>What one screen of a battle looks like. </summary>
        private const int ScreenWidth = 256, ScreenHeight = 192;

        /// <summary>Puts the ground on top of the backdrop, cropped to the screen, which is what a battle
        /// actually looks like before anything stands on it.</summary>
        private (byte[] Rgba, int Width, int Height) Compose(BattleBgRenderer.BgImage bg)
        {
            int w = Math.Min(ScreenWidth, bg.Width), h = Math.Min(ScreenHeight, bg.Height);
            var outp = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                Array.Copy(bg.Rgba, y * bg.Width * 4, outp, y * w * 4, w * 4);

            var (mine, enemy) = _grounds.Build(_terrainIndex, _timeOfDay);
            foreach (var piece in new[] { enemy, mine })
            {
                if (piece?.Rgba == null) continue;
                for (int y = 0; y < piece.Height; y++)
                {
                    int ty = piece.Top + y;
                    if (ty < 0 || ty >= h) continue;
                    for (int x = 0; x < piece.Width; x++)
                    {
                        int tx = piece.Left + x;
                        if (tx < 0 || tx >= w) continue;
                        int from = (y * piece.Width + x) * 4, to = (ty * w + tx) * 4;
                        if (piece.Rgba[from + 3] == 0) continue;
                        outp[to] = piece.Rgba[from];
                        outp[to + 1] = piece.Rgba[from + 1];
                        outp[to + 2] = piece.Rgba[from + 2];
                        outp[to + 3] = 255;
                    }
                }
            }
            return (outp, w, h);
        }

        private void RaiseAll()
        {
            OnPropertyChanged(nameof(HasPicture));
            OnPropertyChanged(nameof(HasNoPicture));
        }

        private string _status = "";
        public string Status { get => _status; set => Set(ref _status, value); }

        /// <summary>Which file the Graphics window should open for the piece asked for.</summary>
        public int FileFor(string piece) => _selected == null ? -1 : piece switch
        {
            "drawing" => _selected.Drawing,
            "colours" => _selected.PaletteDay + _timeOfDay,
            _ => _selected.Arrangement,
        };
    }
}
