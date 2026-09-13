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
    /// <summary>One colour of the row a piece is painted from.</summary>
    public sealed class BottomScreenSwatch
    {
        /// <summary>Where this colour sits in the palette file, counting from its first colour.</summary>
        public int At { get; init; }
        public uint Argb { get; init; }
        public IBrush Fill { get; init; }
        public string Tip { get; init; }
    }

    /// <summary>One line in the list of pieces beside the screen.</summary>
    public sealed class BottomScreenPieceRow
    {
        public BottomScreenPiece Piece { get; init; }
        public string Name => Piece.Name;
        public string Where => Piece.Where;
    }

    /// <summary>
    /// One thing on the bottom screen, and the files it is made of. A piece is drawn from up to three
    /// files: the picture, the arrangement that lays its tiles out, and the layout that groups them into
    /// a sprite.
    /// </summary>
    public sealed class BottomScreenPiece
    {
        public string Name;
        public string What;
        public DirNames Archive;
        public int Drawing = -1;        // the picture, which is what a PNG can replace
        public int Arrangement = -1;    // NSCR, where its tiles go
        public int Cells = -1;          // NCER, how its pieces are grouped
        public int Animation = -1;      // NANR, the frame order
        public int Sprites = -1;        // the sheet the cells take their tiles from
        public int SharedSheet = -1;    // a sheet loaded ahead of it, which the tile numbers count past
        public int PaletteMember = -1;
        public int PaletteRow;
        public int PaletteRows = 1;     // more than one when the piece is painted from several rows

        /// <summary>Filled in once the whole list is known: the other pieces painted from this same row.</summary>
        public string SharedWith;

        /// <summary>Set when the game does not read this from a file, so there is nothing here to edit.</summary>
        public string ReadOnlyBecause;

        /// <summary>A rule worth saying before this one is changed, or null.</summary>
        public string Warning;

        public string Where
        {
            get
            {
                var bits = new List<string>();
                if (Drawing >= 0) bits.Add("drawing " + Drawing);
                if (Arrangement >= 0) bits.Add("arrangement " + Arrangement);
                if (Cells >= 0) bits.Add("layout " + Cells);
                if (Animation >= 0) bits.Add("animation " + Animation);
                if (PaletteMember >= 0) bits.Add("colours " + PaletteMember);
                return string.Join(", ", bits);
            }
        }
    }

    /// <summary>
    /// The bottom screen as the game draws it while you walk around, with the graphics and colours of
    /// every piece of it editable. The top screen beside it is a picture of the game, not something this
    /// window changes; it is there so the bottom screen is seen the way a player sees it.
    /// </summary>
    public sealed class BottomScreenEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        // The HGSS touch menu's seven icons, in the order the panel lays them out.
        private static readonly string[] IconNames =
            { "POKéDEX icon", "POKéMON icon", "BAG icon", "POKéGEAR icon", "Player icon", "SAVE icon", "OPTIONS icon" };
        private static readonly int[] IconDrawings = { 18, 21, 24, 30, 33, 36, 39 };
        private const int MenuPalettes = 7, MenuTiles = 8, MenuMap = 9, IconPalettes = 14, IconCells = 16,
                          ButtonCells = 68, ButtonDrawing = 70;
        private const int ChoicePalettes = 0, ChoiceTiles = 1, PokeBallMap = 9, YesNoMap = 10,
                          CursorPalette = 11, CursorDrawing = 12, CursorCells = 13;
        private const int ThemePalettes = 0, UnavailableTiles = 10, UnavailableMap = 11, UnavailablePalette = 12,
                          CasePalettes = 13, CaseTiles = 14, CaseMap = 15,
                          WatchTiles = 23, WatchMap = 24, WatchDigits = 25;

        private HgssTouchScreen _hgss;
        private PoketchScreen _poketch;
        private FieldFont _font;
        private TextArchive _words;
        private List<BottomScreenPiece> _pieces = new();
        private bool _loading = true;

        // Every edit writes straight into the unpacked file, so stepping back and forth means keeping the
        // bytes from either side of it. These files run from a few hundred bytes to a few kilobytes, so
        // holding whole copies is simpler than working out which bytes moved and cheap enough not to matter.
        private readonly Stack<Step> _undo = new();
        private readonly Stack<Step> _redo = new();

        private readonly record struct Step(DirNames Dir, int Member, byte[] Bytes, string What);

        public BottomScreenEditorViewModel()
        {
            if (Design.IsDesignMode) { _loading = false; return; }

            IsHgss = gameFamily == GameFamilies.HGSS;
            IsPlatinum = gameFamily == GameFamilies.Plat;

            foreach (string t in IsHgss ? new[] { "Menu", "Poké Ball screen" } : new[] { "Pokétch" })
                TabNames.Add(t);

            if (IsPlatinum)
            {
                AppNames.Add("Pokétch frame");
                foreach (var a in PoketchApps.All) AppNames.Add(a.Name);
            }

            try { _font = FieldFont.LoadFromArchive(HgssTouchScreen.FontEntry) ?? FieldFont.LoadSystemFont(); }
            catch (Exception ex) { AppLogger.Error("Bottom screen font: " + ex.Message); }

            var now = System.DateTime.Now;
            _hour = now.Hour;
            _minute = now.Minute;

            LoadScreens();
            _loading = false;
            Refresh();
        }

        public bool IsHgss { get; }
        public bool IsPlatinum { get; }

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
        /// <summary>The watch starts at the time now, which is what somebody would expect to see.</summary>
        public int Hour { get => _hour; set { if (Set(ref _hour, Math.Clamp(value, 0, 23))) Draw(); } }
        public int Minute { get => _minute; set { if (Set(ref _minute, Math.Clamp(value, 0, 59))) Draw(); } }

        private int _bank;
        /// <summary>Which of the sprite's cell banks to show, for the applications that have several.</summary>
        public int CellBank
        {
            get => _bank;
            set
            {
                if (!Set(ref _bank, Math.Max(0, value))) return;
                // Stepping a bank by hand is a way of looking through the drawings, so it stops the run
                // rather than fighting it for the same picture.
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

        // ── What the screen is showing, so an edited piece can be seen in every state ──

        private bool _scriptRunning;
        /// <summary>While a script runs the icons go see-through and only the A button stays solid.</summary>
        public bool ScriptRunning { get => _scriptRunning; set { if (Set(ref _scriptRunning, value)) Draw(); } }

        private bool _shoesOn;
        public bool ShoesOn { get => _shoesOn; set { if (Set(ref _shoesOn, value)) Draw(); } }

        private bool _aHeld;
        public bool AHeld { get => _aHeld; set { if (Set(ref _aHeld, value)) Draw(); } }

        private int _choiceCount = 2;
        public int ChoiceCount { get => _choiceCount; set { if (Set(ref _choiceCount, Math.Clamp(value, 2, 8))) Draw(); } }

        private bool _yesNo = true;
        public bool YesNo { get => _yesNo; set { if (Set(ref _yesNo, value)) Draw(); } }

        private bool _female;
        /// <summary>A girl's Pokétch has the pink casing, a boy's the blue, out of the same file.</summary>
        public bool Female { get => _female; set { if (Set(ref _female, value)) Draw(); } }

        private bool _backlight;
        public bool Backlight { get => _backlight; set { if (Set(ref _backlight, value)) Draw(); } }

        private bool _noPoketchYet;
        public bool NoPoketchYet { get => _noPoketchYet; set { if (Set(ref _noPoketchYet, value)) Draw(); } }

        private int _theme;
        public int Theme { get => _theme; set { if (Set(ref _theme, Math.Max(0, value))) Draw(); } }

        // ── The pieces ────────────────────────────────────────────────────────────────

        public ObservableCollection<BottomScreenPieceRow> Pieces { get; } = new();
        public ObservableCollection<BottomScreenSwatch> Swatches { get; } = new();

        private int _selectedIndex = -1;
        public int SelectedIndex
        {
            get => _selectedIndex;
            // The picked piece decides what the screen shows on the Poké Ball tab, so it redraws. Refresh
            // draws for itself after raising, which is why this sits here and not in RaiseSelection.
            set { if (Set(ref _selectedIndex, value)) { RaiseSelection(); Draw(); } }
        }

        public BottomScreenPiece Selected =>
            _selectedIndex >= 0 && _selectedIndex < _pieces.Count ? _pieces[_selectedIndex] : null;

        public bool HasSelection => Selected != null;
        public string SelectedName => Selected?.Name ?? "Nothing picked";
        public string SelectedWhat => Selected == null ? "Pick a piece." : Selected.What ?? "";

        /// <summary>Most pieces need no explaining, so the line goes away rather than stating the obvious.</summary>
        public bool HasWhat => Selected == null || !string.IsNullOrEmpty(Selected.What);
        public bool CanPaint => Selected != null && Selected.Drawing >= 0 && Selected.ReadOnlyBecause == null;

        /// <summary>Why the buttons are off for this piece, or null when they are on.</summary>
        public string CannotEditBecause => Selected?.ReadOnlyBecause;

        /// <summary>Whether this piece is an animation, which has an editor of its own.</summary>
        public bool IsAnimation => Selected != null && Selected.Animation >= 0;
        public bool HasColours => Selected != null && Selected.PaletteMember >= 0;

        /// <summary>What else is painted from the row being edited, so a shared change is no surprise.</summary>
        public string SharedNote => Selected?.SharedWith;
        public bool IsShared => !string.IsNullOrEmpty(Selected?.SharedWith);

        /// <summary>A rule about this piece worth reading before changing it.</summary>
        public string Caution => Selected?.Warning;
        public bool HasCaution => !string.IsNullOrEmpty(Selected?.Warning);

        private string _status = "";
        public string StatusText { get => _status; private set => Set(ref _status, value); }

        // Edits go straight into the unpacked files, which is where every other editor writes too. The
        // ROM save packs them, so there is nothing held back in here.
        public bool HasUnsavedChanges => false;
        public string UnsavedChangesDescription => "Bottom screen";
        public void SaveChanges() { }
        public void DiscardChanges() { }

        // ── Building and drawing ──────────────────────────────────────────────────────

        private void LoadScreens()
        {
            try
            {
                _hgss = IsHgss ? HgssTouchScreen.Load() : null;
                _poketch = IsPlatinum ? PoketchScreen.Load() : null;
                if (IsPlatinum) LoadAnimation();
                if (RomInfo.fieldTouchMenuTextArchive >= 0)
                    try { _words = new TextArchive(RomInfo.fieldTouchMenuTextArchive); } catch { _words = null; }
            }
            catch (Exception ex) { AppLogger.Error("Bottom screen load: " + ex.Message); }
        }

        // The panel's words carry the same variable tags script text does, and the player's name is one of
        // them. Left raw, the panel reads "STRVAR_1, 3, 0, 0" where a name belongs.
        private string Word(int message)
        {
            if (_words?.messages == null || message < 0 || message >= _words.messages.Count) return null;
            string line = _words.messages[message];
            return line == null ? null
                 : FieldStringVars.Expand(line, (_, kind, buffer) => FieldStringVars.SuggestFor(kind, buffer, null));
        }

        /// <summary>Rebuilds the piece list and redraws, keeping whatever was picked.</summary>
        public void Refresh()
        {
            if (_loading) return;
            string keep = Selected?.Name;

            _pieces = BuildPieces();
            NoteSharing(_pieces);

            Pieces.Clear();
            foreach (var p in _pieces) Pieces.Add(new BottomScreenPieceRow { Piece = p });

            // Keep whatever was picked, and otherwise start on the first piece. Landing on "nothing picked"
            // with the buttons greyed out every time the screen changes is a click for no reason.
            int at = keep == null ? -1 : _pieces.FindIndex(p => p.Name == keep);
            _selectedIndex = at >= 0 ? at : (_pieces.Count > 0 ? 0 : -1);
            OnPropertyChanged(nameof(SelectedIndex));
            RaiseSelection();
            Draw();
        }

        /// <summary>Which of the bottom screens is being looked at.</summary>
        public enum Tab { Menu, Choices, Poketch }

        private Tab CurrentTab => IsMenuTab ? Tab.Menu : IsChoicesTab ? Tab.Choices : Tab.Poketch;

        private List<BottomScreenPiece> BuildPieces() => PiecesFor(CurrentTab, _female, _theme, _backlight, _app);

        /// <summary>The Pokétch frame, then every application in the order the Pokétch cycles them.</summary>
        public ObservableCollection<string> AppNames { get; } = new();

        private int _app;
        /// <summary>0 is the Pokétch frame itself; 1 and up pick an application.</summary>
        public int SelectedApp
        {
            get => _app;
            // Each application has its own animation file, so the run has to be given the new one rather
            // than left holding the last application's frames.
            set { if (Set(ref _app, value)) { Stop(); LoadAnimation(); Refresh(); } }
        }

        /// <summary>
        /// Which application is up, as the Pokétch itself numbers them, or -1 for the frame. An animation
        /// opened from here is told this so it can draw inside the casing rather than on nothing.
        /// </summary>
        public int PoketchAppId =>
            _app >= 1 && _app <= PoketchApps.All.Length ? PoketchApps.All[_app - 1].Id : -1;

        /// <summary>
        /// Every piece of one of the screens, with the files it is drawn from. Static and free of anything
        /// on screen so it can be checked against a real ROM.
        /// </summary>
        public static List<BottomScreenPiece> PiecesFor(Tab tab, bool female = false, int theme = 0,
                                                        bool backlight = false, int app = 0)
        {
            var list = new List<BottomScreenPiece>();
            if (tab == Tab.Menu)
            {
                list.Add(new BottomScreenPiece
                {
                    Name = "Panel",
                    Archive = DirNames.fieldTouchMenu, Drawing = MenuTiles, Arrangement = MenuMap,
                    PaletteMember = MenuPalettes, PaletteRow = 0, PaletteRows = 16,
                });
                for (int i = 0; i < IconNames.Length; i++)
                    list.Add(new BottomScreenPiece
                    {
                        Name = IconNames[i],
                        Archive = DirNames.fieldTouchMenu, Drawing = IconDrawings[i], Cells = IconCells,
                        PaletteMember = IconPalettes, PaletteRow = 0,
                    });
                list.Add(new BottomScreenPiece
                {
                    Name = "Side buttons",
                    What = "Item slots, running shoes, the A button and the mark beside MENU.",
                    Archive = DirNames.fieldTouchMenu, Drawing = ButtonDrawing, Cells = ButtonCells,
                    PaletteMember = MenuPalettes, PaletteRow = 0, PaletteRows = 16,
                });
            }
            else if (tab == Tab.Choices)
            {
                list.Add(new BottomScreenPiece
                {
                    Name = "Poké Ball screen",
                    Archive = DirNames.fieldTouchChoices, Drawing = ChoiceTiles, Arrangement = PokeBallMap,
                    PaletteMember = ChoicePalettes, PaletteRow = 0, PaletteRows = 5,
                });
                list.Add(new BottomScreenPiece
                {
                    Name = "Yes and no boxes",
                    Archive = DirNames.fieldTouchChoices, Arrangement = YesNoMap,
                    PaletteMember = ChoicePalettes, PaletteRow = 0, PaletteRows = 5,
                });
                for (int n = 2; n <= 8; n++)
                    list.Add(new BottomScreenPiece
                    {
                        Name = $"List of {n}",
                        Archive = DirNames.fieldTouchChoices, Arrangement = n,
                        PaletteMember = ChoicePalettes, PaletteRow = 0, PaletteRows = 5,
                    });
                list.Add(new BottomScreenPiece
                {
                    Name = "Answer frame",
                    Archive = DirNames.fieldTouchChoices, Drawing = CursorDrawing, Cells = CursorCells,
                    PaletteMember = CursorPalette, PaletteRow = 0,
                });
            }
            else if (app >= 1)
            {
                var a = PoketchApps.All[Math.Min(app - 1, PoketchApps.All.Length - 1)];
                int row = theme * 2 + (backlight ? 1 : 0);

                if (a.ReadOnlyBecause != null)
                {
                    // Its colours are still worth showing: the theme is the only thing about this screen
                    // that can be changed at all.
                    list.Add(new BottomScreenPiece
                    {
                        Name = a.Name + " colours", What = a.ReadOnlyBecause, Archive = DirNames.poketch,
                        PaletteMember = ThemePalettes, PaletteRow = row,
                        Warning = a.ReadOnlyBecause,
                    });
                    return list;
                }

                if (a.Tiles >= 0)
                    list.Add(new BottomScreenPiece
                    {
                        Name = "Screen",
                        What = $"{a.Name}'s own screen, {a.TilesUsed} of {PoketchApps.TileCeiling} tiles.",
                        Archive = DirNames.poketch, Drawing = a.Tiles, Arrangement = a.Arrangement,
                        PaletteMember = ThemePalettes, PaletteRow = row, Warning = a.Warning,
                    });
                if (a.Sprites >= 0)
                    list.Add(new BottomScreenPiece
                    {
                        Name = "Sprites",
                        What = $"The moving pieces, {a.SpriteTilesUsed} of {PoketchApps.TileCeiling} sprite tiles"
                             + (a.UsesDigitSheet ? ", counting the shared figures." : "."),
                        Archive = DirNames.poketch, Drawing = a.Sprites,
                        PaletteMember = ThemePalettes, PaletteRow = row,
                    });
                if (a.Cells >= 0)
                    list.Add(new BottomScreenPiece
                    {
                        Name = "Sprite positions",
                        Archive = DirNames.poketch, Cells = a.Cells,
                        Animation = a.Animation,
                        Sprites = a.Sprites,
                        SharedSheet = PoketchApps.SharedSheetFor(a),
                        PaletteMember = ThemePalettes, PaletteRow = row,

                        // Pieces are moved in the animation editor, so a screen with no animation has
                        // nowhere to open them. Where the sprite as a whole sits is fixed in the game's code.
                        ReadOnlyBecause = a.Animation >= 0 ? null
                            : "These positions are read from the file, but this screen has no animation to "
                            + "open them in.",
                    });
                if (a.Animation >= 0)
                    list.Add(new BottomScreenPiece
                    {
                        Name = "Animation",
                        What = PoketchApps.AnimationsWithTransforms.Contains(a.Animation)
                             ? "These frames carry a turn or a stretch."
                             : null,
                        Archive = DirNames.poketch, Animation = a.Animation,
                        Cells = a.Cells, Drawing = -1,
                        PaletteMember = ThemePalettes, PaletteRow = row,
                        Sprites = a.Sprites,
                        SharedSheet = PoketchApps.SharedSheetFor(a),
                    });
                return list;
            }
            else
            {
                list.Add(new BottomScreenPiece
                {
                    Name = "Casing",
                    What = "Row 0 is the girl's, row 1 the boy's.",
                    Archive = DirNames.poketch, Drawing = CaseTiles, Arrangement = CaseMap,
                    PaletteMember = CasePalettes, PaletteRow = female ? 0 : 1,
                });
                list.Add(new BottomScreenPiece
                {
                    Name = "Watch face",
                    Archive = DirNames.poketch, Drawing = WatchTiles, Arrangement = WatchMap,
                    PaletteMember = ThemePalettes, PaletteRow = theme * 2 + (backlight ? 1 : 0),
                });
                list.Add(new BottomScreenPiece
                {
                    Name = "Watch digits",
                    Archive = DirNames.poketch, Arrangement = WatchDigits,
                    PaletteMember = ThemePalettes, PaletteRow = theme * 2 + (backlight ? 1 : 0),
                });
                list.Add(new BottomScreenPiece
                {
                    Name = "Before you have one",
                    What = "Shown until the player is given a Pokétch.",
                    Archive = DirNames.poketch, Drawing = UnavailableTiles, Arrangement = UnavailableMap,
                    PaletteMember = UnavailablePalette, PaletteRow = 0,
                });
            }
            return list;
        }

        /// <summary>
        /// Works out which pieces are painted from the same row of the same file, so picking one can say
        /// what else moves with it.
        /// </summary>
        public static void NoteSharing(List<BottomScreenPiece> pieces)
        {
            foreach (var p in pieces)
            {
                if (p.PaletteMember < 0) { p.SharedWith = null; continue; }
                var others = pieces.Where(o => !ReferenceEquals(o, p)
                                            && o.Archive == p.Archive
                                            && o.PaletteMember == p.PaletteMember
                                            && Overlaps(o, p))
                                   .Select(o => o.Name).ToList();
                p.SharedWith = others.Count == 0 ? null
                    : "These colours are shared with " + Join(others) + ", which change with it.";
            }
        }

        private static bool Overlaps(BottomScreenPiece a, BottomScreenPiece b) =>
            a.PaletteRow < b.PaletteRow + b.PaletteRows && b.PaletteRow < a.PaletteRow + a.PaletteRows;

        private static string Join(List<string> names) =>
            names.Count == 1 ? names[0]
                             : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];

        private void Draw()
        {
            if (_loading) return;
            try
            {
                byte[] rgba = null;
                if (IsMenuTab && _hgss != null)
                    rgba = _hgss.RenderMenu(_font, Word, ScriptRunning, AHeld, ShoesOn, -1,
                                            ScriptRunning ? HgssTouchScreen.TalkMessage : HgssTouchScreen.CheckMessage);
                else if (IsChoicesTab && _hgss != null)
                {
                    // Picking a layout on the left shows that layout, so the boxes for five answers are
                    // seen where they land rather than described.
                    var (labels, yesNo) = ChoiceShown();
                    rgba = _hgss.RenderChoices(_font, labels, yesNo, 0, labels != null);
                }
                else if (IsPlatinum && _poketch != null)
                    rgba = PoketchShot();

                Screen = rgba == null ? null : ToBitmap(rgba, DsBgScreen.Width, DsBgScreen.Height);
                if (rgba == null) StatusText = "This screen could not be read from the ROM.";
            }
            catch (Exception ex)
            {
                AppLogger.Error("Bottom screen draw: " + ex.Message);
                StatusText = "This screen could not be drawn. " + ex.Message;
            }
        }

        // The Pokétch draws its casing round whichever application is up. The Digital Watch is the one with
        // moving parts worth showing, so it keeps the fuller drawing with its digits in place.
        private byte[] PoketchShot()
        {
            if (NoPoketchYet) { BankCount = 0; return _poketch.RenderUnavailable(); }

            var app = _app >= 1 && _app <= PoketchApps.All.Length ? PoketchApps.All[_app - 1] : null;
            if (app == null || app.Name == "Digital Watch")
            {
                BankCount = 0;
                return _poketch.RenderWatch(Female, Theme, Backlight, Hour, Minute,
                                            PoketchScreen.Look.Free, PoketchScreen.Look.Free);
            }

            BankCount = app.Cells >= 0 ? _poketch.CellBankCount(app.Cells) : 0;
            HasContents = app.Fills != null || app.SpriteSlots != null;
            return _poketch.RenderApp(Female, Theme, Backlight, app.Tiles, app.Arrangement,
                                      ShowSprites ? app.Sprites : -1,
                                      ShowSprites ? app.Cells : -1,
                                      CellBank,
                                      FillContents ? app.SpriteSlots : null,
                                      FillContents ? app.Fills : null,
                                      _motion);
        }

        // ── Playing the application's own animation ───────────────────────────────────

        private NanrFile _animation;
        private int _sequence;
        private PoketchScreen.Motion _motion = PoketchScreen.Motion.Still;

        private DispatcherTimer _timer;
        private readonly System.Diagnostics.Stopwatch _clock = new();
        private int _frame, _shown, _ticksRun, _held;
        private bool _reverse;

        /// <summary>The Pokétch hands its animations two frames of time per screen refresh.</summary>
        private const int TicksPerRefresh = 2;

        public bool Playing => _timer != null;
        public string PlayLabel => Playing ? "Stop" : "Play";

        /// <summary>Whether this application has an animation to run at all.</summary>
        public bool CanPlay => _animation != null && _animation.Sequences.Count > 0;

        /// <summary>
        /// Which of the animation's sequences to run. Several of these files hold one sequence per pose or
        /// per angle, so the choice matters as much as the play button.
        /// </summary>
        public int PlaySequence
        {
            get => _sequence;
            set
            {
                if (!Set(ref _sequence, value)) return;
                OnPropertyChanged(nameof(PlayNote));
                if (Playing) { Stop(); Play(); }
            }
        }

        public int PlaySequenceCount => _animation?.Sequences.Count ?? 0;

        public string PlayNote
        {
            get
            {
                if (_animation == null) return null;
                if (_sequence < 0 || _sequence >= _animation.Sequences.Count) return null;
                var s = _animation.Sequences[_sequence];
                string mode = s.PlayMode switch
                {
                    2 => "loops",
                    3 => "backwards",
                    4 => "loops backwards",
                    _ => "once",
                };
                return $"Sequence {_sequence} of {_animation.Sequences.Count}, "
                     + $"{s.Frames.Count} frame{(s.Frames.Count == 1 ? "" : "s")}, {mode}";
            }
        }

        // Read alongside the screens so the preview has the frame order and holds to run, not just banks.
        private void LoadAnimation()
        {
            _animation = null;
            _sequence = 0;
            _motion = PoketchScreen.Motion.Still;
            try
            {
                var app = _app >= 1 && _app <= PoketchApps.All.Length ? PoketchApps.All[_app - 1] : null;
                if (app == null || app.Animation < 0) return;
                _animation = NanrFile.Read(
                    NitroBgCodec.Inflate(new ScriptNarc(DirNames.poketch).Get(app.Animation)));
            }
            catch (Exception ex) { AppLogger.Error("Poketch animation: " + ex.Message); }

            OnPropertyChanged(nameof(CanPlay));
            OnPropertyChanged(nameof(PlaySequence));
            OnPropertyChanged(nameof(PlaySequenceCount));
            OnPropertyChanged(nameof(PlayNote));
        }

        public void TogglePlay()
        {
            if (Playing) Stop(); else Play();
        }

        private void Play()
        {
            if (!CanPlay) return;
            if (_sequence < 0 || _sequence >= _animation.Sequences.Count) return;
            if (_animation.Sequences[_sequence].Frames.Count == 0) return;

            uint mode = _animation.Sequences[_sequence].PlayMode;
            _reverse = mode == 3 || mode == 4;
            _frame = _reverse ? _animation.Sequences[_sequence].Frames.Count - 1 : 0;
            _shown = _frame;
            _held = 0; _ticksRun = 0;
            _clock.Restart();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5) };
            _timer.Tick += Beat;
            _timer.Start();
            OnPropertyChanged(nameof(Playing));
            OnPropertyChanged(nameof(PlayLabel));
            ShowFrame();
        }

        /// <summary>Stops the run. The frame it stopped on stays on screen.</summary>
        public void Stop()
        {
            if (_timer == null) return;
            _timer.Stop();
            _timer.Tick -= Beat;
            _timer = null;
            _clock.Stop();
            OnPropertyChanged(nameof(Playing));
            OnPropertyChanged(nameof(PlayLabel));
        }

        // Same rules as the animation editor's playback: zero holds are skipped, loops return to the
        // sequence's loop start, backwards modes turn round at each end.
        private void Beat(object sender, EventArgs e)
        {
            if (!CanPlay || _sequence < 0 || _sequence >= _animation.Sequences.Count) { Stop(); return; }
            var s = _animation.Sequences[_sequence];
            if (s.Frames.Count == 0) { Stop(); return; }

            int due = (int)(_clock.Elapsed.TotalSeconds * 60 * TicksPerRefresh);
            if (due - _ticksRun > 4 * TicksPerRefresh) _ticksRun = due - 1;

            bool loops = s.PlayMode == 2 || s.PlayMode == 4;
            bool backwards = s.PlayMode == 3 || s.PlayMode == 4;
            int loopStart = Math.Clamp(s.LoopStartFrame, 0, s.Frames.Count - 1);
            int was = _shown;

            while (_ticksRun < due && Playing)
            {
                _ticksRun++;
                _held++;

                for (int steps = 0; Playing && _held >= s.Frames[_frame].Delay && steps <= s.Frames.Count; steps++)
                {
                    _held = 0;
                    _frame += _reverse ? -1 : 1;

                    if (_frame >= s.Frames.Count || _frame < loopStart)
                    {
                        if (backwards)
                        {
                            bool atStart = _frame < loopStart;
                            _reverse = !_reverse;
                            _frame = Math.Clamp(_frame, loopStart, s.Frames.Count - 1);
                            if (atStart && !loops) { Stop(); break; }
                        }
                        else if (loops) _frame = loopStart;
                        else { _frame = s.Frames.Count - 1; Stop(); break; }
                    }

                    if (s.Frames[_frame].Delay > 0) { _shown = _frame; break; }
                }
            }
            if (_shown != was) ShowFrame();
        }

        // A frame names a drawing and what to do with it. Both come from the animation, so the preview shows
        // the pose the game would show rather than whichever bank was last stepped to.
        private void ShowFrame()
        {
            if (_animation == null) return;
            var (turn, across, down) = _animation.TurnOf(_sequence, _shown);
            var (shiftX, shiftY) = _animation.ShiftOf(_sequence, _shown);
            _motion = new PoketchScreen.Motion(turn, across, down, shiftX, shiftY);

            int cell = _animation.CellOf(_sequence, _shown);
            if (!Set(ref _bank, Math.Max(0, cell))) Draw();
            else { OnPropertyChanged(nameof(BankNote)); Draw(); }
        }

        // Stand-in wording. A script supplies the real words, so these are only here to show the boxes with
        // something in them.
        private static string[] SampleLabels(int count, bool yesNo) =>
            yesNo ? new[] { "YES", "NO" }
                  : Enumerable.Range(1, count).Select(n => "Answer " + n).ToArray();

        /// <summary>
        /// Which arrangement the preview draws, and its words. A picked layout wins over the toolbar, so
        /// the Poké Ball screen on its own shows no boxes and "List of 5" shows five of them.
        /// </summary>
        private (string[] Labels, bool YesNo) ChoiceShown()
        {
            int count = ChoiceCount;
            bool yesNo = YesNo;

            var p = Selected;
            if (p != null && p.Arrangement >= 0)
            {
                if (p.Arrangement == YesNoMap) { yesNo = true; count = 2; }
                else if (p.Arrangement >= 2 && p.Arrangement <= 8) { yesNo = false; count = p.Arrangement; }
                else if (p.Arrangement == PokeBallMap) return (null, false);
            }

            return (SampleLabels(count, yesNo), yesNo);
        }

        // ── Colours ───────────────────────────────────────────────────────────────────

        private void RaiseSelection()
        {
            OnPropertyChanged(nameof(Selected));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectedName));
            OnPropertyChanged(nameof(SelectedWhat));
            OnPropertyChanged(nameof(HasWhat));
            OnPropertyChanged(nameof(CanPaint));
            OnPropertyChanged(nameof(CannotEditBecause));
            OnPropertyChanged(nameof(IsAnimation));
            OnPropertyChanged(nameof(HasColours));
            OnPropertyChanged(nameof(SharedNote));
            OnPropertyChanged(nameof(IsShared));
            OnPropertyChanged(nameof(Caution));
            OnPropertyChanged(nameof(HasCaution));
            BuildSwatches();
        }

        private void BuildSwatches()
        {
            Swatches.Clear();
            var p = Selected;
            if (p == null || p.PaletteMember < 0) return;

            ushort[] colours = ReadColours(p);
            for (int row = 0; row < p.PaletteRows; row++)
                for (int i = 0; i < 16; i++)
                {
                    int at = (p.PaletteRow + row) * 16 + i;
                    if (at >= colours.Length) continue;
                    uint argb = ToArgb(colours[at]);
                    Swatches.Add(new BottomScreenSwatch
                    {
                        At = at,
                        Argb = argb,
                        Fill = new SolidColorBrush(Color.FromUInt32(argb)),
                        Tip = $"Row {p.PaletteRow + row}, colour {i}",
                    });
                }
        }

        private static ushort[] ReadColours(BottomScreenPiece p)
        {
            try { return DsBgScreen.ReadColours(new ScriptNarc(p.Archive).Get(p.PaletteMember)); }
            catch { return Array.Empty<ushort>(); }
        }

        /// <summary>The colour a swatch stands for, for the editor that changes it.</summary>
        public uint ColourAt(int at) => Swatches.FirstOrDefault(s => s.At == at)?.Argb ?? 0xFF000000u;

        /// <summary>
        /// Writes one colour back into the file it came from, at the row it came from, then redraws. A
        /// palette file here holds many rows side by side, so the row matters as much as the file.
        /// </summary>
        public void SetColour(int at, uint argb)
        {
            var p = Selected;
            if (p == null || p.PaletteMember < 0) return;
            try
            {
                Remember(p.Archive, p.PaletteMember, $"the colour change to {p.Name}");
                var narc = new ScriptNarc(p.Archive);
                byte[] file = narc.Get(p.PaletteMember);
                string trouble = GraphicAssets.PatchPalette(ref file, new[] { argb }, at);
                if (trouble != null) { StatusText = trouble; return; }
                narc.Put(p.PaletteMember, file);

                // The screens keep what they have read, so they are opened again to show the new colour.
                LoadScreens();
                BuildSwatches();
                Draw();
                StatusText = p.SharedWith == null
                    ? "Colour saved."
                    : "Colour saved. " + p.SharedWith;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Bottom screen colour: " + ex.Message);
                StatusText = "That colour could not be saved. " + ex.Message;
            }
        }

        /// <summary>Reads everything again after a drawing has been replaced.</summary>
        public void ReloadAfterImport()
        {
            LoadScreens();
            Refresh();
        }

        // ── Stepping back ─────────────────────────────────────────────────────────────

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        public string UndoWhat => _undo.Count == 0 ? "Nothing to undo" : "Undo " + _undo.Peek().What;
        public string RedoWhat => _redo.Count == 0 ? "Nothing to redo" : "Redo " + _redo.Peek().What;

        /// <summary>
        /// Keeps a copy of a file before it is written to. Call this before every change, including one made
        /// by another window such as the painter. A fresh change starts a new branch, so whatever had been
        /// put back can no longer be put forward again.
        /// </summary>
        public void Remember(DirNames dir, int member, string what)
        {
            if (member < 0) return;
            try
            {
                byte[] before = new ScriptNarc(dir).Get(member);
                if (before == null) return;
                _undo.Push(new Step(dir, member, before, what));
                _redo.Clear();
                RaiseSteps();
            }
            catch (Exception ex) { AppLogger.Error("Bottom screen remember: " + ex.Message); }
        }

        /// <summary>Puts the last change back the way it was.</summary>
        public void Undo() => StepAcross(_undo, _redo, "Put back");

        /// <summary>Does again whatever was just put back.</summary>
        public void Redo() => StepAcross(_redo, _undo, "Done again");

        // Undo and redo are one move in opposite directions: write the bytes waiting on one side, and hand
        // whatever was on disk to the other side so the move can be reversed again.
        private void StepAcross(Stack<Step> from, Stack<Step> to, string said)
        {
            if (from.Count == 0) return;
            var step = from.Pop();
            try
            {
                var narc = new ScriptNarc(step.Dir);
                byte[] now = narc.Get(step.Member);
                narc.Put(step.Member, step.Bytes);
                if (now != null) to.Push(new Step(step.Dir, step.Member, now, step.What));
                LoadScreens();
                Refresh();
                StatusText = said + ": " + step.What;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Bottom screen step: " + ex.Message);
                StatusText = "That change could not be moved. " + ex.Message;
            }
            RaiseSteps();
        }

        private void RaiseSteps()
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(UndoWhat));
            OnPropertyChanged(nameof(RedoWhat));
        }

        public void Say(string what) => StatusText = what;

        private static uint ToArgb(ushort bgr555)
        {
            int r = bgr555 & 0x1F, g = (bgr555 >> 5) & 0x1F, b = (bgr555 >> 10) & 0x1F;
            return 0xFF000000u
                 | (uint)(((r << 3) | (r >> 2)) << 16)
                 | (uint)(((g << 3) | (g >> 2)) << 8)
                 | (uint)((b << 3) | (b >> 2));
        }

        private static Bitmap ToBitmap(byte[] rgba, int width, int height)
        {
            var wb = new WriteableBitmap(new global::Avalonia.PixelSize(width, height),
                                         new global::Avalonia.Vector(96, 96),
                                         PixelFormat.Rgba8888, AlphaFormat.Unpremul);
            using (var fb = wb.Lock())
                System.Runtime.InteropServices.Marshal.Copy(rgba, 0, fb.Address, rgba.Length);
            return wb;
        }
    }
}
