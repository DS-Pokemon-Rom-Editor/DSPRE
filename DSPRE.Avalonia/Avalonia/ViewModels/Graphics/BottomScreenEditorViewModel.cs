using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>The overworld's bottom screen, with every piece's art and colours.</summary>
    public sealed partial class BottomScreenEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo, IDetachable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        // The HGSS touch menu's icons in overlay 27's order, then CHAT, LOG and the girl's BAG.
        private static readonly string[] IconNames =
            { "POKéDEX icon", "POKéMON icon", "BAG icon", "POKéGEAR icon", "Player icon", "SAVE icon", "OPTIONS icon",
              "RETIRE icon", "CHAT icon", "LOG icon", "Girl's BAG icon" };
        private static readonly int[] IconDrawings = { 18, 21, 24, 30, 33, 36, 39, 42, 45, 48, 27 };
        private static readonly int[] IconKinds = { 0, 1, 2, 3, 4, 5, 6, 7, 11, 12, 2 };
        // The panel's tiles stop at the text windows. Icons and side buttons share 512 sprite tiles.
        private const int PanelTileRoom = 192, IconTileRoom = 20, SideButtonTileRoom = 324;
        private const int MenuPalettes = 7, MenuTiles = 8, MenuMap = 9, IconPalettes = 14, IconCells = 16,
                          ButtonCells = 68, ButtonDrawing = 70;
        private const int ChoicePalettes = 0, ChoiceTiles = 1, PokeBallMap = 9, YesNoMap = 10,
                          CursorPalette = 11, CursorDrawing = 12, CursorCells = 13;
        private const int ThemePalettes = 0, UnavailableTiles = 10, UnavailableMap = 11, UnavailablePalette = 12,
                          CasePalettes = 13, CaseTiles = 14, CaseMap = 15,
                          WatchTiles = 23, WatchMap = 24, WatchDigits = 25;

        private HgssTouchScreen _hgss;
        private PoketchScreen _poketch;
        private FieldFont _font, _iconFont;
        private TextArchive _words;
        private List<BottomScreenPiece> _pieces = new();
        private bool _loading = true;

        // Imports and colour changes wait here until Save; the screens are drawn from them meanwhile.
        public PendingArchiveWrites Pending { get; } = new PendingArchiveWrites();

        public BottomScreenEditorViewModel()
        {
            if (Design.IsDesignMode) { _loading = false; return; }
            Pending.Changed += RaiseSteps;

            IsHgss = GameFamily == GameFamilies.HGSS;
            HasPoketch = GameFamily == GameFamilies.Plat || GameFamily == GameFamilies.DP;
            _coloursPatchMissing = !HgEngine.HgEngineProject.IsActive && FieldMenuColoursPatch.SupportsCurrentRom && !FieldMenuColoursPatch.IsApplied;
            if (_coloursPatchMissing) AppEvents.RomPatchStateChanged += OnRomPatchStateChanged;

            foreach (string t in IsHgss ? new[] { "Menu", "Poké Ball screen" } : new[] { "Pokétch" })
                TabNames.Add(t);

            if (HasPoketch)
            {
                AppNames.Add("Pokétch frame");
                foreach (PoketchApps.App a in PoketchApps.All) AppNames.Add(a.Name);
            }

            try
            {
                _font = FieldFont.LoadFromArchive(HgssTouchScreen.FontEntry) ?? FieldFont.LoadSystemFont();
                _iconFont = FieldFont.LoadFromArchive(HgssTouchScreen.IconFontEntry) ?? _font;
            }
            catch (Exception ex) { AppLogger.Error("Bottom screen font: " + ex.Message); }

            DateTime now = System.DateTime.Now;
            _hour = now.Hour;
            _minute = now.Minute;

            LoadScreens();
            if (IsHgss)
                foreach (int m in HgssTouchScreen.ALabelMessages)
                    ALabelNames.Add(Word(m)?.Trim() is { Length: > 0 } w ? w : "Message " + m);
            _loading = false;
            Refresh();
        }

        public bool IsHgss { get; }
        public bool HasPoketch { get; }

        public ObservableCollection<string> TabNames { get; } = new();

        private int _tabIndex;
        public int TabIndex
        {
            get => _tabIndex;
            set { if (Set(ref _tabIndex, value)) { OnPropertyChanged(nameof(IsMenuTab)); OnPropertyChanged(nameof(IsChoicesTab)); Refresh(); } }
        }

        public bool IsMenuTab => IsHgss && _tabIndex == 0;
        public bool IsChoicesTab => IsHgss && _tabIndex == 1;

        /// <summary>Which screen is being looked at, for naming a window opened from here.</summary>
        public string AppName =>
            _app >= 1 && _app <= PoketchApps.All.Length ? PoketchApps.All[_app - 1].Name
            : IsMenuTab ? "Menu"
            : IsChoicesTab ? "Poké Ball screen"
            : "Pokétch";

        private int _hour, _minute;
        /// <summary>The watch starts at the current time.</summary>
        public int Hour { get => _hour; set { if (Set(ref _hour, Math.Clamp(value, 0, 23))) Draw(); } }
        public int Minute { get => _minute; set { if (Set(ref _minute, Math.Clamp(value, 0, 59))) Draw(); } }

        private int _bank;
        /// <summary>Which of the sprite's cell banks to show, where an application has several.</summary>
        public int CellBank
        {
            get => _bank;
            set
            {
                if (!Set(ref _bank, Math.Max(0, value))) return;
                // Stepping a bank by hand stops the run so the two don't fight over the picture.
                Stop();
                _motion = PoketchScreen.Motion.Still;
                OnPropertyChanged(nameof(BankNote));
                Draw();
            }
        }

        private int _bankCount;
        public int BankCount
        {
            get => _bankCount;
            private set
            {
                if (!Set(ref _bankCount, value)) return;
                OnPropertyChanged(nameof(HasBanks));
                OnPropertyChanged(nameof(BankNote));
            }
        }
        public bool HasBanks => _bankCount > 1;
        public string BankNote => _bankCount <= 0 ? null : $"Cell bank {_bank + 1} of {_bankCount}";

        private bool _showSprites = true;
        /// <summary>The moving pieces, drawn over the screen where the game would put them.</summary>
        public bool ShowSprites { get => _showSprites; set { if (Set(ref _showSprites, value)) Draw(); } }

        private bool _fillContents = true;
        /// <summary>
        /// The parts a running game fills in: a party's health bars and its six slots, a note page. Drawn
        /// at the places the game's own tables give, so these are not stand-ins.
        /// </summary>
        public bool FillContents { get => _fillContents; set { if (Set(ref _fillContents, value)) Draw(); } }

        private bool _hasContents;
        /// <summary>Whether this screen has anything a running game would fill in.</summary>
        public bool HasContents { get => _hasContents; private set => Set(ref _hasContents, value); }

        private Bitmap _screen;
        public Bitmap Screen { get => _screen; private set => Set(ref _screen, value); }
    }
}
