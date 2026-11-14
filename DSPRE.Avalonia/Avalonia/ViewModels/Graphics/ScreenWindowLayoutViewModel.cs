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
    public sealed partial class ScreenWindowLayoutViewModel : INotifyPropertyChanged, DSPRE.Editors.IEditorWithUnsavedChanges, ISupportsUndo, IDetachable, IScreenPreviewSource
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
        // The screen's touch buttons, shared with its Buttons tab, or null when it has none.
        private readonly ScreenButtonsLink _link;
        private readonly ScreenButtonsViewModel _buttons;
        private readonly PendingArchiveWrites.Target _windowTables, _panelTables;

        public ScreenWindowLayoutViewModel() { if (!Design.IsDesignMode) return; }

        public ScreenWindowLayoutViewModel(string screen, string title, ScreenButtonsLink link = null)
        {
            _screen = screen;
            if (link?.Model() is { Buttons: not null } buttons) (_link, _buttons, _pending) = (link, buttons, buttons.Pending);
            Title = title;
            _text = GameTextScreens.Screens.FirstOrDefault(s => s.Key == screen);
            _layout = ScreenWindowLayout.Read(screen);
            if (_layout == null) { StatusText = "Not found in this ROM"; return; }
            if (screen == "party") _panels = PartyPanelLayout.Read();
            _windowTables = new PendingArchiveWrites.Target("windows", _layout.FileBytes, _layout.WriteFile);
            if (_panels != null) _panelTables = new PendingArchiveWrites.Target("panels", _panels.FileBytes, _panels.WriteFile);
            if (_panels != null && !_panels.Resizable && PtPartyStrip.Read() is (int column, int row))
            {
                _ptStrip = _savedPtStrip = (column, row);
                _ptStripTable = new PendingArchiveWrites.Target("strip", () => PtPartyStrip.Read() is (int c, int r) ? new[] { (byte)c, (byte)r } : null,
                                                                 b => PtPartyStrip.Write(b[0], b[1]));
            }
            _bands = _text?.BandsOf?.Invoke() ?? Array.Empty<GameTextScreens.ScreenBand>();
            _swaps = _text?.SwapsOf?.Invoke() ?? Array.Empty<GameTextScreens.ScreenSwap>();
            KeepBandRows();
            KeepSwapSpots();
            OpenCursor();
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
        public IDisposable DrawScope() => new DrawScopes(_pending.Reading(), ScreenWindowLayouts.Use(_screen, _layout, _panels),
                                                     ScreenWindowLayouts.UseButtons(_buttons?.Buttons),
                                                     ScreenWindowLayouts.UseFonts(_fonts), ScreenWindowLayouts.UseMenu(ShownMenu));

        // Undo, redo and discard change what's pending, so the tables are read back from it.
        private void OnPendingChanged()
        {
            if (_pending.Read(_windowTables) is byte[] windows) _layout.FromBytes(windows);
            if (_panelTables != null && _pending.Read(_panelTables) is byte[] panels) _panels.FromBytes(panels);
            if (_ptStripTable != null && _pending.Read(_ptStripTable) is byte[] strip)
            {
                _ptStrip = (strip[0], strip[1]);
                _panels.MoveStrip(strip[0], strip[1]);
            }
            if (_fontTable != null && _pending.Read(_fontTable) is byte[] fonts) _fonts = PartyFontPatch.FontsIn(fonts, _layout.Windows);
            if (_cursorTable != null && _pending.Read(_cursorTable) is byte[] cursor) _flow.FromBytes(cursor);
            if (_menuTable != null && _pending.Read(_menuTable) is byte[] menu) _menu.FromBytes(menu);
            RaiseUndo();
            SyncTouch();
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
                if (_ptStripTable != null) _pending.Stage(_ptStripTable, new[] { (byte)_ptStrip.Column, (byte)_ptStrip.Row });
                // The font table is keyed by where each window is, so it follows every move.
                if (_fontTable != null) _pending.Stage(_fontTable, PartyFontPatch.Table(_layout.Windows, _fonts));
                if (_cursorTable != null) _pending.Stage(_cursorTable, _flow.ToBytes());
                if (_menuTable != null) _pending.Stage(_menuTable, _menu.ToBytes());
                if (_buttons != null) _pending.Stage(_buttons.Tables, _buttons.Buttons.ToBytes());
                StageBands();
                StageSwaps();
                return null;
            });
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
        public Bitmap TopPicture { get => _top; private set { _top = value; Raise(); Raise(nameof(CursorPicture)); } }
        public Bitmap BottomPicture { get => _bottom; private set { _bottom = value; Raise(); Raise(nameof(CursorPicture)); } }

        public bool ShowTop => _screens != 2 && (TopWindows.Count > 0 || _top != null);
        public bool ShowBottom => _screens != 1 && (BottomWindows.Count > 0 || _bottom != null);
        private void RaiseShown() { Raise(nameof(ShowTop)); Raise(nameof(ShowBottom)); }

        public bool HasParty => _layout?.Windows.Any(w => w.Pokemon > 0) == true;

        private bool _moveAll;
        public bool MoveAllPokemon { get => _moveAll; set { _moveAll = value; Raise(); } }

        // Panels are added first so they sit under their windows.
        private void Fill()
        {
            Windows.Clear();
            TopWindows.Clear();
            BottomWindows.Clear();
            bool mainOnTop = _text?.MainOnTopOf?.Invoke() ?? _text?.MainOnTop ?? RomInfo.GameFamily != RomInfo.GameFamilies.HGSS;
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
            if (_panels != null && _panels.HasBallButtons && !multi)
                for (int n = 0; n < PartyPanelLayout.PartySize; n++)
                {
                    ScreenWindowRow row = new ScreenWindowRow(new ScreenWindowLayout.Window
                    {
                        Group = BallGroup, Name = $"Poké Ball {n + 1}", Pokemon = n + 1, Part = "Poké Ball", Layer = 4,
                        Width = PartyPanelLayout.BallButtonTiles, Height = PartyPanelLayout.BallButtonTiles,
                    }, BallColour, 0)
                    {
                        MoveTo = (r, x, y) => { Move(r, x, y); Commit(); },
                    };
                    Windows.Add(row);
                    (mainOnTop ? BottomWindows : TopWindows).Add(row);
                }
            SyncPanels();
            foreach (ScreenWindowLayout.Window w in _layout.Windows.Where(w => !HasStates || _text.Shows(_state, w.Group, w.Name)))
            {
                (int insideX, int insideY) = _text?.Inside?.Invoke(_state, w) ?? (0, 0);
                int scrolled = (_text?.RowsScrolled?.Invoke(_state, w.Layer) ?? 0) - insideY;
                ScreenWindowRow row = new ScreenWindowRow(w, HpBarLayoutEditorViewModel.Colours[colour++ % HpBarLayoutEditorViewModel.Colours.Length], scrolled)
                {
                    ShiftX = insideX,
                    MoveTo = (r, x, y) => { Move(r, x, y); Commit(); },
                    OnScreenTiles = true,
                    SizeTo = w.Code != null ? null : (r, width, height) => { Resize(r, width, height); Commit(); },
                    FixedTop = _text?.FixedTop?.Invoke(_state, w),
                    FontNames = FontNames,
                    GetFont = _fonts != null && w.Part == "name" ? () => Math.Max(_fonts["name"], 0) : null,
                    SetFont = _fonts != null && w.Part == "name" ? SetFont : null,
                };
                Windows.Add(row);
                ((w.Layer < 4) == mainOnTop ? TopWindows : BottomWindows).Add(row);
            }
            AddTouchRows(mainOnTop);
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
        private static bool IsBall(ScreenWindowRow row) => row.Window.Group == BallGroup;

        public void Commit()
        {
            Action<ScriptNarc> art = _stripArt;
            _stripArt = null;
            Record(art);
        }

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
            (byte[] top, byte[] bottom) = await PreviewFileGate.RunAsync(() =>
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
                foreach (RomInfo.DirNames dir in _bands.Select(b => b.Dir).Concat(_swaps.Select(s => s.Dir)).Append(PartyGraphics).Distinct())
                {
                    if (_pending.Pending(dir).Count == 0) continue;
                    await Task.Run(() => DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { dir }));
                    if (!ArchiveMembers.Has(dir, _pending.Pending(dir)[0].Id))
                        throw new System.IO.IOException("The screen's graphics couldn't be unpacked.");
                }
                if (_panelTables != null && _panels.WhyNotWrite(_pending.Read(_panelTables)) is string why)
                    throw new InvalidOperationException(why);
                if (_cursorTable != null && _flow.WhyNotWrite(_pending.Read(_cursorTable)) is string cursorWhy)
                    throw new InvalidOperationException(cursorWhy);
                if (_menuTable != null && _menu.WhyNotWrite(_pending.Read(_menuTable)) is string menuWhy)
                    throw new InvalidOperationException(menuWhy);
                if (_buttons != null && _buttons.Buttons.WhyNotWrite(_pending.Read(_buttons.Tables)) is string buttonsWhy)
                    throw new InvalidOperationException(buttonsWhy);
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
            _savedPtStrip = _ptStrip;
            KeepBandRows();
            KeepSwapSpots();
            ScreenWindowLayouts.Forget(_screen);
            _buttons?.Moved();
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
}
