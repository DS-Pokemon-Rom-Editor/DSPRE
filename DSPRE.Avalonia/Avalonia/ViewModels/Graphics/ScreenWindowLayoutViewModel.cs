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
    /// <summary>A screen's text windows to drag or type into, next to the screens as the game draws them.</summary>
    public sealed class ScreenWindowLayoutViewModel : INotifyPropertyChanged, DSPRE.Editors.IEditorWithUnsavedChanges, ISupportsUndo, IDetachable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        /// <summary>Board pixels per tile; the screens are drawn at double size.</summary>
        public const double Tile = 16;

        private readonly string _screen;
        private readonly ScreenWindowLayout _layout;
        // Null on screens other than the party.
        private readonly PartyPanelLayout _panels;
        private readonly GameTextScreens.Screen _text;
        private readonly PendingArchiveWrites _pending = new PendingArchiveWrites();
        private readonly PendingArchiveWrites.Target _windowTables, _panelTables;

        public ScreenWindowLayoutViewModel() { if (!Design.IsDesignMode) return; }

        public ScreenWindowLayoutViewModel(string screen, string title)
        {
            _screen = screen;
            Title = title;
            _text = GameTextScreens.Screens.FirstOrDefault(s => s.Key == screen);
            _layout = ScreenWindowLayout.Read(screen);
            if (_layout == null) { StatusText = "Not found in this ROM"; return; }
            if (screen == "party") _panels = PartyPanelLayout.Read();
            _windowTables = new PendingArchiveWrites.Target("windows", _layout.FileBytes, _layout.WriteFile);
            if (_panels != null) _panelTables = new PendingArchiveWrites.Target("panels", _panels.FileBytes, _panels.WriteFile);
            OpenFontTable();
            _savedSize = _panels?.Size;
            _inPanel = InPanel();
            if (_panels != null) (_iconInPanel, _ballInPanel) = (SpriteInPanel(PartyPanelLayout.Sprite.Icon), SpriteInPanel(PartyPanelLayout.Sprite.Ball));
            if (HasStates)
                for (int i = 0; i < _text.LayoutStates.Length; i++)
                    if (_text.HasLayoutState?.Invoke(i) != false) States.Add(_text.LayoutStates[i]);
            Fill();
            _pending.Changed += OnPendingChanged;
            PartySample.Changed += OnSampleChanged;
            AppEvents.RomPatchStateChanged += OnPatchStateChanged;
            _fontsSaved = AppEvents.WhenSaved(OnFontsSaved, RomInfo.DirNames.Fonts);
        }

        private readonly IDisposable _fontsSaved;

        // A font added in the Font editor joins the pickers.
        private void OnFontsSaved()
        {
            if (Windows.Any(w => w.CanPickFont && w.FontNames.Count != GameFonts.All.Count)) Fill();
        }

        /// <summary>Raised after every edit, undo and save.</summary>
        public event Action Changed;

        /// <summary>Use while drawing on a worker thread, so the preview shows the unsaved edits.</summary>
        public IDisposable DrawScope() => new Scopes(_pending.Reading(), ScreenWindowLayouts.Use(_screen, _layout, _panels),
                                                     ScreenWindowLayouts.UseFonts(_fonts));

        private sealed class Scopes : IDisposable
        {
            private readonly IDisposable[] _all;
            public Scopes(params IDisposable[] all) { _all = all; }
            public void Dispose() { foreach (IDisposable d in _all.Reverse()) d.Dispose(); }
        }

        // Undo, redo and discard change what's pending, so the tables are read back from it.
        private void OnPendingChanged()
        {
            if (_pending.Read(_windowTables) is byte[] windows) _layout.FromBytes(windows);
            if (_panelTables != null && _pending.Read(_panelTables) is byte[] panels) _panels.FromBytes(panels);
            if (_fontTable != null && _pending.Read(_fontTable) is byte[] fonts) _fonts = PartyFontPatch.FontsIn(fonts, _layout.Windows);
            RaiseUndo();
            Refresh();
            Changed?.Invoke();
        }

        // One undo step for the tables and any rebuilt graphics.
        private void Record(Action<ScriptNarc> graphics = null)
        {
            _pending.Import(() =>
            {
                graphics?.Invoke(new ScriptNarc(PartyGraphics));
                _pending.Stage(_windowTables, _layout.ToBytes());
                if (_panelTables != null) _pending.Stage(_panelTables, _panels.ToBytes());
                // The font table is keyed by where each window is, so it follows every move.
                if (_fontTable != null) _pending.Stage(_fontTable, PartyFontPatch.Table(_layout.Windows, _fonts));
                return null;
            });
        }

        // ── party graphics rebuilt to the panel size: the cursor frame, the panel art and the background ──

        private readonly RomInfo.PartyGraphicsMembers _members = RomLocator.PartyGraphicsMembers;
        private const RomInfo.DirNames PartyGraphics = RomInfo.DirNames.PartyMenuGraphics;
        // Rebuilds start from the saved art, at the size it was saved at.
        private PartyPanelSizePatch.Size _savedSize;

        // Rebuilds start from the saved file, never from an unsaved rebuild.
        private static byte[] Saved(int member) => NitroBgCodec.Inflate(new ScriptNarc(PartyGraphics).GetFromDisk(member));

        public void RebuildCursor()
        {
            if (_panels == null || _members == null) return;
            (byte[] Cells, byte[] Tiles) built = PartyCursorFrames.Build(Saved(_members.CursorCells), Saved(_members.CursorTiles),
                                                                         _panels.Width, _panels.Height, out string why);
            if (built.Cells == null) { StatusText = why; return; }
            StatusText = "";
            Record(narc => { narc.Put(_members.CursorCells, built.Cells); narc.Put(_members.CursorTiles, built.Tiles); });
        }

        public void RebuildPanelArt()
        {
            if (_panels == null || _members == null) return;
            byte[] art = PartyPanelArt.Build(Saved(_members.PanelArt), _savedSize ?? _panels.Size, _panels.Size, out string why);
            if (art == null) { StatusText = why; return; }
            StatusText = "";
            PlaceWindows();
            Record(narc => narc.Put(_members.PanelArt, art));
        }

        public void RebuildBackground()
        {
            if (_panels == null || _members == null) return;
            byte[] background = PartyPanelArt.PlainBackground(Saved(_members.Background), _members.PanelRows, out string why);
            if (background == null) { StatusText = why; return; }
            StatusText = "";
            Record(narc => narc.Put(_members.Background, background));
        }

        // Where Pokémon 1's windows (in tiles) and its icon and ball (in pixels) sit in its panel in the ROM.
        private readonly Dictionary<string, (int X, int Y)> _inPanel;
        private readonly (int X, int Y) _iconInPanel, _ballInPanel;

        private Dictionary<string, (int X, int Y)> InPanel()
        {
            Dictionary<string, (int X, int Y)> places = new Dictionary<string, (int X, int Y)>();
            if (_panels == null) return places;
            (int px, int py) = _panels.Panel(0);
            foreach (ScreenWindowLayout.Window w in _layout.Windows.Where(w => w.Group == SingleGroup && w.Pokemon == 1))
                places[w.Part] = (w.X - px, w.Y - py);
            return places;
        }

        private (int X, int Y) SpriteInPanel(PartyPanelLayout.Sprite sprite)
        {
            (int px, int py) = _panels.Panel(0);
            (int x, int y) = _panels.SpriteAt(0, sprite);
            return (x - px * 8, y - py * 8);
        }

        private const int IconTiles = 4;

        // Uses the ROM's places when everything fits; otherwise stacks the icon, level, name and HP.
        private void PlaceWindows()
        {
            PartyPanelSizePatch.Size size = _panels.Size;
            bool fits = _layout.Windows.Where(w => w.Group == SingleGroup && w.Pokemon == 1 && w.Part != "HP bar").All(w =>
                _inPanel.TryGetValue(w.Part, out (int X, int Y) at) && at.X >= 0 && at.Y >= 0
                && at.X + w.Width <= size.Width && at.Y + w.Height <= size.Height);
            for (int n = 0; n < PartyPanelLayout.PartySize; n++)
            {
                (int px, int py) = _panels.Panel(n);
                ScreenWindowLayout.Window[] windows = _layout.Windows.Where(w => w.Group == SingleGroup && w.Pokemon == n + 1).ToArray();
                ScreenWindowLayout.Window level = windows.FirstOrDefault(w => w.Part == "level");
                // The status icon covers the level, so it moves with it.
                (int sx, int sy) = _panels.SpriteAt(n, PartyPanelLayout.Sprite.Status);
                (int statusX, int statusY) = level == null ? (0, 0) : (sx - level.X * 8, sy - level.Y * 8);

                // The ball stays on the icon's corner, inside the panel.
                (int iconX, int iconY) = fits ? _iconInPanel : (IconTiles * 4, IconTiles * 4);
                (int ballX, int ballY) = (_ballInPanel.X - _iconInPanel.X + iconX, _ballInPanel.Y - _iconInPanel.Y + iconY);
                (int ix, int iy) = _panels.SpriteAt(n, PartyPanelLayout.Sprite.Icon);
                _panels.MoveSprite(n, PartyPanelLayout.Sprite.Icon, px * 8 + iconX - ix, py * 8 + iconY - iy);
                (int bx, int by) = _panels.SpriteAt(n, PartyPanelLayout.Sprite.Ball);
                _panels.MoveSprite(n, PartyPanelLayout.Sprite.Ball, px * 8 + Math.Max(8, ballX) - bx, py * 8 + Math.Max(8, ballY) - by);
                int hpRow = size.StripRow + 3 <= size.Height ? size.StripRow + 1 : size.StripRow - 2;
                foreach (ScreenWindowLayout.Window w in windows)
                {
                    (int x, int y) = w.Part switch
                    {
                        // Leaves room for the two "HP" label tiles when the strip has them.
                        "HP bar" => (size.StripColumn + (size.StripLength >= 3 ? 2 : 0), size.StripRow),
                        _ when fits => _inPanel[w.Part],
                        "name" => (0, IconTiles),
                        "level" => (IconTiles, 1),
                        "HP" or "comment" => (size.Width - w.Width, hpRow),
                        _ => (w.X - px, w.Y - py),
                    };
                    w.X = Math.Clamp(px + Math.Clamp(x, 0, Math.Max(0, size.Width - w.Width)), 0, ScreenWindowLayout.MapColumns - w.Width);
                    w.Y = Math.Clamp(py + Math.Clamp(y, 0, Math.Max(0, size.Height - w.Height)), 0, ScreenWindowLayout.MapHeight - w.Height);
                }
                if (level != null)
                {
                    (int cx, int cy) = _panels.SpriteAt(n, PartyPanelLayout.Sprite.Status);
                    _panels.MoveSprite(n, PartyPanelLayout.Sprite.Status, level.X * 8 + statusX - cx, level.Y * 8 + statusY - cy);
                }
            }
            SyncPanels();
            foreach (ScreenWindowRow row in Windows) row.Refresh();
            Warn();
        }

        private void OnSampleChanged() => _ = RedrawAsync();

        public string Title { get; }
        public string UnsavedChangesDescription => Title + " layout";

        public bool HasStates => _text?.LayoutStates != null && _text.Shows != null;
        public ObservableCollection<string> States { get; } = new ObservableCollection<string>();

        private int _state;
        public int StateIndex
        {
            get => _state;
            set
            {
                if (value < 0 || value == _state) return;
                _state = value;
                Raise();
                Fill();
            }
        }

        public string[] ScreenChoices { get; } = { "Both screens", "Top screen", "Bottom screen" };

        private int _screens;
        public int ScreensIndex
        {
            get => _screens;
            set
            {
                if (value < 0 || value == _screens) return;
                _screens = value;
                Raise();
                RaiseShown();
            }
        }

        public ObservableCollection<ScreenWindowRow> Windows { get; } = new ObservableCollection<ScreenWindowRow>();
        public ObservableCollection<ScreenWindowRow> TopWindows { get; } = new ObservableCollection<ScreenWindowRow>();
        public ObservableCollection<ScreenWindowRow> BottomWindows { get; } = new ObservableCollection<ScreenWindowRow>();

        private Bitmap _top, _bottom;
        public Bitmap TopPicture { get => _top; private set { _top = value; Raise(); } }
        public Bitmap BottomPicture { get => _bottom; private set { _bottom = value; Raise(); } }

        public bool ShowTop => _screens != 2 && (TopWindows.Count > 0 || _top != null);
        public bool ShowBottom => _screens != 1 && (BottomWindows.Count > 0 || _bottom != null);
        private void RaiseShown() { Raise(nameof(ShowTop)); Raise(nameof(ShowBottom)); }

        public bool HasParty => _layout?.Windows.Any(w => w.Pokemon > 0) == true;

        private bool _moveAll;
        public bool MoveAllPokemon { get => _moveAll; set { _moveAll = value; Raise(); } }

        // ── panel size, once the party panel size patch is in ──

        public bool PanelsResizable => _panels?.Resizable == true;
        public bool CanRebuildGraphics => PanelsResizable && _members != null;
        public bool CanOfferSizePatch => _panels != null && !_panels.Resizable && !RomInfo.IsHGE && PartyPanelSizePatch.SupportsCurrentRom;

        public void OpenSizePatch() => AvaloniaEditorLauncher.OpenPatchToolboxAt("partyPanelSize");

        // ── fonts for the names and the line under them, once the party menu fonts patch is in ──

        private PendingArchiveWrites.Target _fontTable;
        private Dictionary<string, int> _fonts;

        private void OpenFontTable()
        {
            if (_screen != "party" || _fontTable != null || RomInfo.IsHGE || !PartyFontPatch.IsApplied) return;
            _fontTable = new PendingArchiveWrites.Target("fonts", PartyFontPatch.ReadTable, PartyFontPatch.WriteTable);
            _fonts = PartyFontPatch.FontsIn(PartyFontPatch.ReadTable(), _layout.Windows);
        }

        public bool OffersFontPatch => _screen == "party" && _fontTable == null && !RomInfo.IsHGE && PartyFontPatch.SupportsCurrentRom;
        public void OpenFontPatch() => AvaloniaEditorLauncher.OpenPatchToolboxAt("partyFonts");

        private static IReadOnlyList<string> FontNames => GameFonts.Names;

        // The comment line only shows on screens the Layout doesn't have, so it takes the name font.
        private void SetFont(int font)
        {
            if (_fonts == null || Math.Max(font, 0) == Math.Max(_fonts["name"], 0)) return;
            foreach (string part in PartyFontPatch.Parts) _fonts[part] = font;
            Commit();
        }

        // The patch keeps the game's size, so unsaved edits stay as they are.
        private void OnPatchStateChanged(object sender, EventArgs e)
        {
            if (_fontTable == null && PartyFontPatch.IsApplied)
            {
                OpenFontTable();
                Raise(nameof(OffersFontPatch));
                Fill();
            }
            if (_panels == null || _panels.Resizable || !PartyPanelSizePatch.IsApplied) return;
            _panels.ReadPatch();
            Refresh();
        }

        public decimal PanelWidth { get => _panels?.Width ?? 0; set => ChangePanelSize(s => FitStrip(s with { Width = (int)value })); }
        public decimal PanelHeight { get => _panels?.Height ?? 0; set => ChangePanelSize(s => FitStrip(s with { Height = (int)value })); }

        // Shrinking the panel is never blocked by the strip.
        private static PartyPanelSizePatch.Size FitStrip(PartyPanelSizePatch.Size s)
        {
            int column = Math.Min(s.StripColumn, Math.Max(0, s.Width - 1));
            int length = Math.Clamp(s.StripLength, 1, s.Width - column);
            return s with { StripColumn = column, StripLength = length, StripRow = Math.Min(s.StripRow, s.Height - 1) };
        }
        public bool HpBarLengthEditable => _panels?.HpBarLengthEditable == true;
        public int MaxHpBarLength => _layout?.Windows.FirstOrDefault(w => w.Group == SingleGroup && w.Part == "HP bar")?.MaxWidth * 8
                                     ?? PartyPanelLayout.GameHpBarLength;
        public decimal HpBarLength
        {
            get => _panels?.HpBarLength ?? PartyPanelLayout.GameHpBarLength;
            set
            {
                int length = Math.Clamp((int)value, 1, MaxHpBarLength);
                if (_panels == null || !_panels.HpBarLengthEditable || length == _panels.HpBarLength) return;
                _panels.HpBarLength = length;
                Commit();
            }
        }

        public decimal StripColumn { get => _panels?.Size.StripColumn ?? 0; set => ChangePanelSize(s => s with { StripColumn = (int)value }); }
        public decimal StripRow { get => _panels?.Size.StripRow ?? 0; set => ChangePanelSize(s => s with { StripRow = (int)value }); }
        public decimal StripLength { get => _panels?.Size.StripLength ?? 0; set => ChangePanelSize(s => s with { StripLength = (int)value }); }

        // A size the code can't hold is refused, with the reason on the status line.
        private void ChangePanelSize(Func<PartyPanelSizePatch.Size, PartyPanelSizePatch.Size> change)
        {
            if (_panels == null || !_panels.Resizable) return;
            PartyPanelSizePatch.Size size = change(_panels.Size);
            if (size == _panels.Size) return;
            if (PartyPanelSizePatch.WhyNotSize(size) is string why) { StatusText = why; RaiseSize(); return; }
            StatusText = "";
            _panels.Resize(size);
            Commit();
        }

        // Each range depends on the others, so no box can go past what the code holds.
        private PartyPanelSizePatch.Size Now => _panels?.Size ?? PartyPanelSizePatch.Original;
        public int MinPanelWidth => 1;
        public int MaxPanelWidth => Math.Min(ScreenWindowLayout.MapColumns, PartyPanelSizePatch.MaxArea / Now.Height);
        public int MinPanelHeight => 1;
        public int MaxPanelHeight => Math.Min(PartyPanelSizePatch.MaxHeight, PartyPanelSizePatch.MaxArea / Now.Width);
        public int MaxStripColumn => Now.Width - Now.StripLength;
        public int MaxStripRow => Now.Height - 1;
        public int MaxStripLength => Now.Width - Now.StripColumn;

        private void RaiseSize()
        {
            foreach (string n in new[] { nameof(PanelsResizable), nameof(CanRebuildGraphics), nameof(CanOfferSizePatch), nameof(MinPanelWidth), nameof(MaxPanelWidth),
                                         nameof(MinPanelHeight), nameof(MaxPanelHeight), nameof(MaxStripColumn), nameof(MaxStripRow),
                                         nameof(MaxStripLength), nameof(PanelWidth), nameof(PanelHeight), nameof(StripColumn),
                                         nameof(StripRow), nameof(StripLength), nameof(HpBarLength), nameof(HpBarLengthEditable) })
                Raise(n);
        }

        // Panels are added first so they sit under their windows.
        private void Fill()
        {
            Windows.Clear();
            TopWindows.Clear();
            BottomWindows.Clear();
            bool mainOnTop = RomInfo.GameFamily != RomInfo.GameFamilies.HGSS;
            int colour = 0;
            bool multi = HasStates && _layout.Windows.Any(w => w.Group == MultiGroup && _text.Shows(_state, w.Group, w.Name));
            if (_panels != null && !multi)
                for (int n = 0; n < PartyPanelLayout.PartySize; n++)
                {
                    ScreenWindowRow row = new ScreenWindowRow(new ScreenWindowLayout.Window
                    {
                        Group = PanelGroup, Name = ScreenWindowLayout.PokemonWindow(n + 1, "panel"), Pokemon = n + 1, Part = "panel", Layer = 2,
                        Width = _panels.Width, Height = _panels.Height,
                    }, PanelColour, 0)
                    {
                        MoveTo = (r, x, y) => { Move(r, x, y); Commit(); },
                    };
                    Windows.Add(row);
                    (mainOnTop ? TopWindows : BottomWindows).Add(row);
                }
            if (_panels != null && !multi)
                foreach ((PartyPanelLayout.Sprite sprite, string part, int width, int height) in SpriteRows)
                    for (int n = 0; n < PartyPanelLayout.PartySize; n++)
                    {
                        ScreenWindowRow row = new ScreenWindowRow(new ScreenWindowLayout.Window
                        {
                            Group = SpriteGroup, Name = ScreenWindowLayout.PokemonWindow(n + 1, part), Pokemon = n + 1, Part = part, Layer = 2,
                            Width = width, Height = height,
                        }, SpriteColour, 0)
                        {
                            MoveTo = (r, x, y) => { Move(r, x, y); Commit(); },
                        };
                        Windows.Add(row);
                        (mainOnTop ? TopWindows : BottomWindows).Add(row);
                    }
            SyncPanels();
            foreach (ScreenWindowLayout.Window w in _layout.Windows.Where(w => !HasStates || _text.Shows(_state, w.Group, w.Name)))
            {
                int scrolled = _text?.RowsScrolled?.Invoke(_state, w.Layer) ?? 0;
                ScreenWindowRow row = new ScreenWindowRow(w, HpBarLayoutEditorViewModel.Colours[colour++ % HpBarLayoutEditorViewModel.Colours.Length], scrolled)
                {
                    MoveTo = (r, x, y) => { Move(r, x, y); Commit(); },
                    SizeTo = (r, width, height) => { Resize(r, width, height); Commit(); },
                    FixedTop = _text?.FixedTop?.Invoke(_state, w),
                    FontNames = FontNames,
                    GetFont = _fonts != null && w.Part == "name" ? () => Math.Max(_fonts["name"], 0) : null,
                    SetFont = _fonts != null && w.Part == "name" ? SetFont : null,
                };
                Windows.Add(row);
                ((w.Layer < 4) == mainOnTop ? TopWindows : BottomWindows).Add(row);
            }
            RaiseShown();
            Warn();
            _ = RedrawAsync();
        }

        private ScreenWindowLayout _savedLayout;

        // Warnings compare against the saved layout.
        private ScreenWindowLayout SavedLayout() => _savedLayout ??= ScreenWindowLayout.Read(_screen);

        private const string SingleGroup = "Pokémon", MultiGroup = "Pokémon in a multi battle", PanelGroup = "Panels";
        private const string PanelColour = "#9E9E9E";

        private static bool IsPanel(ScreenWindowRow row) => row.Window.Group == PanelGroup;
        private static bool IsSprite(ScreenWindowRow row) => row.Window.Group == SpriteGroup;

        // Board box sizes in tiles.
        private const string SpriteGroup = "Sprites", SpriteColour = "#C8C8C8";
        private static readonly (PartyPanelLayout.Sprite Sprite, string Part, int Width, int Height)[] SpriteRows =
        {
            (PartyPanelLayout.Sprite.Icon, "icon", 4, 4), (PartyPanelLayout.Sprite.Ball, "ball", 2, 2),
            (PartyPanelLayout.Sprite.Status, "status icon", 4, 1),
        };

        private static PartyPanelLayout.Sprite SpriteOf(ScreenWindowRow row) => SpriteRows.First(r => r.Part == row.Window.Part).Sprite;

        // Sprites move in whole tiles so their offset inside a tile is kept.
        private void MoveSprite(ScreenWindowRow row, int x, int y)
        {
            int dx = x - row.Window.X, dy = y - row.Window.Y;
            if (dx == 0 && dy == 0) return;
            PartyPanelLayout.Sprite sprite = SpriteOf(row);
            foreach (int n in _moveAll ? Enumerable.Range(0, PartyPanelLayout.PartySize) : new[] { row.Window.Pokemon - 1 })
                _panels.MoveSprite(n, sprite, dx * 8, dy * 8);
            SyncPanels();
            Raise(nameof(HasUnsavedChanges));
            _ = RedrawAsync();
        }

        private void SyncPanels()
        {
            if (_panels == null) return;
            foreach (ScreenWindowRow row in Windows.Where(IsPanel))
            {
                (row.Window.X, row.Window.Y) = _panels.Panel(row.Window.Pokemon - 1);
                (row.Window.Width, row.Window.Height) = (_panels.Width, _panels.Height);
                row.Refresh();
            }
            foreach (ScreenWindowRow row in Windows.Where(IsSprite))
            {
                (int cx, int cy) = _panels.SpriteAt(row.Window.Pokemon - 1, SpriteOf(row));
                row.Window.X = (int)Math.Floor((cx - row.Window.Width * 4) / 8.0);
                row.Window.Y = (int)Math.Floor((cy - row.Window.Height * 4) / 8.0);
                row.Refresh();
            }
        }

        private List<string> PanelNotes(ScreenWindowRow row, int state)
        {
            List<string> notes = new List<string>();
            ScreenWindowLayout.Window p = row.Window;
            int n = p.Pokemon - 1;
            foreach (ScreenWindowRow other in Windows.Where(IsPanel))
            {
                ScreenWindowLayout.Window o = other.Window;
                if (!ReferenceEquals(other, row) && o.X < p.X + p.Width && p.X < o.X + o.Width && o.Y < p.Y + p.Height && p.Y < o.Y + o.Height)
                    notes.Add($"Overlaps {o.Name}");
            }
            foreach (ScreenWindowRow other in Windows.Where(r => !IsPanel(r) && r.Window.Pokemon == 0 && r.Window.Layer < p.Layer))
            {
                (int ox, int oy, int ow, int oh) = GameTextScreens.Placed(_text, other.Window, state);
                if (ox < p.X + p.Width && p.X < ox + ow && oy < p.Y + p.Height && p.Y < oy + oh) notes.Add($"Under {Said(other.Window)}");
            }
            // The game slides even slots left and odd slots right by the panel width, wherever they are.
            if (p.X != (n % 2 == 0 ? 0 : _panels.MaxX))
                notes.Add($"Slides toward the {(n % 2 == 0 ? "left" : "right")} edge when Pokémon switch places");
            return notes;
        }

        // Problems the saved layout already has are the game's own, so only new ones are shown.
        private void Warn()
        {
            if (_text == null || _layout == null) return;
            int state = HasStates ? _state : 0;
            Dictionary<(string, string), List<string>> now = Notes(_layout, state);
            Dictionary<(string, string), List<string>> before = SavedLayout() is ScreenWindowLayout saved ? Notes(saved, state) : new();
            foreach (ScreenWindowRow row in Windows)
            {
                ScreenWindowLayout.Window w = row.Window;
                if (IsPanel(row)) { row.Warning = string.Join("\n", PanelNotes(row, state)); continue; }
                if (IsSprite(row)) continue;
                List<string> notes = now.TryGetValue((w.Group, w.Name), out List<string> n) ? n : new List<string>();
                if (before.TryGetValue((w.Group, w.Name), out List<string> old)) notes = notes.Except(old).ToList();
                ScreenWindowLayout.Window was = _savedLayout?.Windows.FirstOrDefault(o => o.Group == w.Group && o.Name == w.Name);
                if (row.FixedTop is int fixedTop && was != null && was.Y != w.Y) notes.Add($"Stays on row {fixedTop} in this state");
                row.Warning = string.Join("\n", notes);
            }
        }

        private static string Said(ScreenWindowLayout.Window w) =>
            w.Pokemon > 0 ? w.Name : "the " + char.ToLowerInvariant(w.Name[0]) + w.Name.Substring(1);

        private Dictionary<(string, string), List<string>> Notes(ScreenWindowLayout layout, int state)
        {
            List<ScreenWindowLayout.Window> shown = layout.Windows.Where(w => !HasStates || _text.Shows(state, w.Group, w.Name)).ToList();
            Dictionary<string, bool[]> kept = GameTextScreens.ShownTiles(_text, layout, state);
            Dictionary<(string, string), List<string>> all = new Dictionary<(string, string), List<string>>();
            foreach (ScreenWindowLayout.Window w in shown)
            {
                (int x, int y, int width, int height) = GameTextScreens.Placed(_text, w, state);
                List<string> notes = new List<string>();
                foreach (ScreenWindowLayout.Window other in shown)
                {
                    if (ReferenceEquals(other, w) || other.Layer != w.Layer || !GameTextScreens.Together(_text, w, other)
                        || !kept.TryGetValue(other.Name, out bool[] theirs)) continue;
                    (int ox, int oy, int ow, int oh) = GameTextScreens.Placed(_text, other, state);
                    bool overlap = false, lost = false;
                    for (int ty = Math.Max(y, oy); ty < Math.Min(y + height, oy + oh) && ty < 32; ty++)
                        for (int tx = Math.Max(x, ox); tx < Math.Min(x + width, ox + ow) && tx < 32; tx++)
                        {
                            overlap = true;
                            if (theirs[ty * 32 + tx]) lost = true;
                        }
                    if (overlap) notes.Add(lost ? $"Partly hidden by {Said(other)}" : $"Hides part of {Said(other)}");
                }
                // A box on a background in front (lower number, same screen) covers it, frame included.
                foreach (ScreenWindowLayout.Window a in shown)
                {
                    if (a.Layer >= w.Layer || a.Layer / 4 != w.Layer / 4 || !GameTextScreens.Together(_text, w, a)) continue;
                    (int ax, int ay, int aw, int ah) = GameTextScreens.Placed(_text, a, state);
                    (int l, int t, int r, int b) = _text.Frame?.Invoke(a) ?? (0, 0, 0, 0);
                    if (ax - l < x + width && x < ax + aw + r && ay - t < y + height && y < ay + ah + b) notes.Add($"Under {Said(a)}");
                }
                if (_text.Block?.Invoke(w) is (int bx, int by, int bw, int bh)
                    && (x < bx || y < by || x + width > bx + bw || y + height > by + bh))
                    notes.Add("Left behind when Pokémon switch places");
                all[(w.Group, w.Name)] = notes;
            }
            return all;
        }

        /// <summary>The change becomes an undo step when <see cref="Commit"/> runs.</summary>
        public void Move(ScreenWindowRow row, int x, int y)
        {
            if (row == null) return;
            if (IsPanel(row)) { MovePanel(row.Window.Pokemon - 1, x, y); return; }
            if (IsSprite(row)) { MoveSprite(row, x, y); return; }
            ScreenWindowLayout.Window w = row.Window;
            x = Math.Clamp(x, 0, ScreenWindowLayout.MapColumns - w.Width);
            y = Math.Clamp(y, 0, ScreenWindowLayout.MapHeight - w.Height);
            int dx = x - w.X, dy = y - w.Y;
            if (dx == 0 && dy == 0) return;
            foreach (ScreenWindowLayout.Window moved in Following(w))
            {
                moved.X = Math.Clamp(moved.X + dx, 0, ScreenWindowLayout.MapColumns - moved.Width);
                moved.Y = Math.Clamp(moved.Y + dy, 0, ScreenWindowLayout.MapHeight - moved.Height);
            }
            foreach (ScreenWindowRow shown in Windows) shown.Refresh();
            Warn();
            Raise(nameof(HasUnsavedChanges));
            _ = RedrawAsync();
        }

        /// <summary>Never bigger than in the ROM.</summary>
        public void Resize(ScreenWindowRow row, int width, int height)
        {
            if (row == null || IsPanel(row) || IsSprite(row)) return;
            if (width == row.Window.Width && height == row.Window.Height) return;
            foreach (ScreenWindowLayout.Window w in Following(row.Window))
            {
                w.Width = Math.Clamp(width, 1, Math.Min(w.MaxWidth, ScreenWindowLayout.MapColumns - w.X));
                w.Height = Math.Clamp(height, 1, Math.Min(w.MaxHeight, ScreenWindowLayout.MapHeight - w.Y));
            }
            foreach (ScreenWindowRow shown in Windows) shown.Refresh();
            Warn();
            Raise(nameof(HasUnsavedChanges));
            _ = RedrawAsync();
        }

        private void MovePanel(int pokemon, int x, int y)
        {
            (int px, int py) = _panels.Panel(pokemon);
            int dx = Math.Clamp(x, 0, _panels.MaxX) - px, dy = Math.Clamp(y, 0, _panels.MaxY) - py;
            if (dx == 0 && dy == 0) return;
            foreach (int k in _moveAll ? Enumerable.Range(0, PartyPanelLayout.PartySize) : new[] { pokemon })
            {
                (int kx, int ky) = _panels.Panel(k);
                int nx = Math.Clamp(kx + dx, 0, _panels.MaxX), ny = Math.Clamp(ky + dy, 0, _panels.MaxY);
                _panels.Move(k, nx, ny);
                foreach (ScreenWindowLayout.Window w in _layout.Windows.Where(w => w.Group == SingleGroup && w.Pokemon == k + 1))
                {
                    w.X = Math.Clamp(w.X + nx - kx, 0, ScreenWindowLayout.MapColumns - w.Width);
                    w.Y = Math.Clamp(w.Y + ny - ky, 0, ScreenWindowLayout.MapHeight - w.Height);
                }
            }
            SyncPanels();
            foreach (ScreenWindowRow shown in Windows) shown.Refresh();
            Warn();
            Raise(nameof(HasUnsavedChanges));
            _ = RedrawAsync();
        }

        private IEnumerable<ScreenWindowLayout.Window> Following(ScreenWindowLayout.Window w)
        {
            yield return w;
            if (!_moveAll || w.Pokemon == 0) yield break;
            foreach (ScreenWindowLayout.Window other in _layout.Windows)
                if (!ReferenceEquals(other, w) && other.Group == w.Group && other.Pokemon > 0 && other.Part == w.Part) yield return other;
        }

        public void Commit() => Record();

        private void Refresh()
        {
            RaiseSize();
            SyncPanels();
            foreach (ScreenWindowRow row in Windows) row.Refresh();
            Warn();
            Raise(nameof(HasUnsavedChanges));
            _ = RedrawAsync();
        }

        private string _status = "";
        public string StatusText { get => _status; set { _status = value; Raise(); } }

        private int _asked;

        private async Task RedrawAsync()
        {
            int asked = ++_asked;
            int state = HasStates ? _state : -1;
            (byte[] top, byte[] bottom) = await Task.Run(() =>
            {
                using (DrawScope()) return ScreenWindowLayouts.Draw(_screen, state);
            });
            if (asked != _asked) return;
            TopPicture = top == null ? null : ImageConverter.FromRgba(top, 256, 192);
            BottomPicture = bottom == null ? null : ImageConverter.FromRgba(bottom, 256, 192);
            RaiseShown();
        }

        public bool HasUnsavedChanges => _layout != null && _pending.IsDirty;

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            if (_layout == null) return true;
            try
            {
                // Checks everything first, then writes graphics before arm9, so a failed save leaves arm9 untouched.
                if (_pending.Pending(PartyGraphics).Count > 0)
                {
                    await Task.Run(() => DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { PartyGraphics }));
                    if (!System.IO.File.Exists(ArchiveMembers.PathOf(PartyGraphics, _pending.Pending(PartyGraphics)[0].Id)))
                        throw new System.IO.IOException("The party graphics couldn't be unpacked.");
                }
                if (_panelTables != null && _panels.WhyNotWrite(_pending.Read(_panelTables)) is string why)
                    throw new InvalidOperationException(why);
                _pending.Save();
            }
            catch (Exception e)
            {
                CrashReporter.LogHandled(e);
                await DialogHelper.ShowErrorAsync("The window layout wasn't saved:\n" + e.Message, Title + " error");
                return false;
            }
            _savedLayout = null;
            _savedSize = _panels?.Size;
            ScreenWindowLayouts.Forget(_screen);
            Raise(nameof(HasUnsavedChanges));
            SaveNotice.Saved(UnsavedChangesDescription);
            if (CursorMissesPanels()) StatusText = "The cursor frame wasn't rebuilt for the new panel size.";
            return true;
        }

        private bool CursorMissesPanels()
        {
            if (_panels == null || _members == null || !_panels.Resizable) return false;
            try
            {
                return PartyCursorFrames.FrameSize(Saved(_members.CursorCells)) is (int w, int h)
                    && (w != _panels.Width * 8 || h != _panels.Height * 8);
            }
            catch (Exception e) when (e is System.IO.IOException || e is ArgumentException) { return false; }
        }

        public void DiscardChanges()
        {
            if (_layout == null) return;
            _pending.Discard();
        }

        public bool CanUndo => _pending.CanUndo;
        public bool CanRedo => _pending.CanRedo;
        public void Undo() => _pending.Undo();
        public void Redo() => _pending.Redo();
        private void RaiseUndo() { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); }

        public void Detach()
        {
            _pending.Changed -= OnPendingChanged;
            PartySample.Changed -= OnSampleChanged;
            AppEvents.RomPatchStateChanged -= OnPatchStateChanged;
            _fontsSaved?.Dispose();
        }
    }

    public sealed class ScreenWindowRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly int _scrolled;

        /// <param name="scrolled">Rows its background is scrolled up in the shown state.</param>
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

        private string _warning = "";
        /// <summary>One problem per line; empty when there are none.</summary>
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

        /// <summary>Set by the editor so typing a tile works like dragging.</summary>
        public Action<ScreenWindowRow, int, int> MoveTo;
        /// <summary>Set only for the game's own windows, the ones that can be resized.</summary>
        public Action<ScreenWindowRow, int, int> SizeTo;

        public int Across { get => Window.X; set => MoveTo?.Invoke(this, value, Window.Y); }
        public int Down { get => Window.Y; set => MoveTo?.Invoke(this, Window.X, value); }
        public bool Sizable => SizeTo != null;
        public int Wide { get => Window.Width; set => SizeTo?.Invoke(this, value, Window.Height); }
        public int Tall { get => Window.Height; set => SizeTo?.Invoke(this, Window.Width, value); }
        public int MaxWide => Math.Max(1, Window.MaxWidth);

        /// <summary>Set only for the name windows, once the party menu fonts patch is in.</summary>
        public Func<int> GetFont { get; init; }
        public Action<int> SetFont { get; init; }
        public IReadOnlyList<string> FontNames { get; init; }
        public bool CanPickFont => SetFont != null;
        public int FontIndex { get => GetFont?.Invoke() ?? 0; set => SetFont?.Invoke(value); }
        public int MaxTall => Math.Max(1, Window.MaxHeight);

        public double Left => Window.X * ScreenWindowLayoutViewModel.Tile;
        public double Top => ShownY * ScreenWindowLayoutViewModel.Tile;
        public double Width => Window.Width * ScreenWindowLayoutViewModel.Tile;
        public double Height => Window.Height * ScreenWindowLayoutViewModel.Tile;

        public void Refresh()
        {
            foreach (string n in new[] { nameof(Across), nameof(Down), nameof(Wide), nameof(Tall), nameof(Left), nameof(Top), nameof(Width), nameof(Height), nameof(FontIndex) })
                Raise(n);
        }
    }
}
