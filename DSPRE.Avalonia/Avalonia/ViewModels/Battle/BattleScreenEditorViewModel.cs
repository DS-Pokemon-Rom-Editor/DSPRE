using System;
using DSPRE.ROMFiles;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    public sealed class BarPieceGroupRow
    {
        public string Name { get; init; }
        public Bitmap Picture { get; init; }
        public double Width { get; init; }
        public double Height { get; init; }
    }

    /// <summary>One line in the list of pieces beside the screens.</summary>
    public sealed class BattlePieceRow
    {
        public BattleScreenRenderer.Piece Piece { get; init; }
        public string Name => Piece.Name;
        public string Where => Piece.Touch ? "Bottom screen" : "Top screen";
        public string Trouble => Piece.Whynot;
        public bool IsMissing => Piece.Rgba == null;
    }

    /// <summary>
    /// The whole battle, both screens, drawn from the ROM's own graphics, with every piece of it
    /// selectable and editable.
    /// </summary>
    public sealed class BattleScreenEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo, IDetachable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        private readonly BattleScreenRenderer _renderer = new();
        private List<BattleScreenRenderer.Piece> _pieces = new();
        private bool _loading = true;

        public BattleScreenEditorViewModel()
        {
            Pending.Changed += RaiseEdits;
            if (Design.IsDesignMode) { _loading = false; return; }

            // The preview writes in the ROM's own font, which is read once and kept for every window
            // that draws game text.
            try { Views.Controls.FieldMessageBoxView.Font ??= ROMFiles.FieldFont.LoadTalkFont(); }
            catch (Exception ex) { AppLogger.Error("Battle screen font: " + ex.Message); }

            foreach (string t in BattleGroundRenderer.TerrainNames) TerrainNames.Add(t);
            foreach (string t in new[] { "Day", "Evening", "Night" }) TimeNames.Add(t);
            for (int i = 0; i < ROMFiles.FieldWindowFrame.FrameCount; i++)
                WindowStyleNames.Add("Text box style " + (i + 1));
            _menus = BattleScreenRenderer.MenusForGame();
            foreach (TouchMenu m in _menus) MenuNames.Add(BattleScreenRenderer.MenuName(m));
            _menuIndex = Math.Max(0, _menus.ToList().IndexOf(TouchMenu.Command));
            BackdropNames.Add("Same as the ground");
            for (int i = 0; i < BattleBgRenderer.BackdropCount; i++) BackdropNames.Add("Backdrop " + i);

            _loading = false;
            AppEvents.RomPatchStateChanged += OnPatchStateChanged;
            _picturesSaved = AppEvents.WhenSaved(Reload, RomInfo.DirNames.BattleObj, RomInfo.DirNames.BattleBg, RomInfo.DirNames.WindowFrames, RomInfo.DirNames.Fonts);
            Refresh();
        }

        // ── What the screens are showing ──────────────────────────────────────────
        public ObservableCollection<string> TerrainNames { get; } = new();
        public ObservableCollection<string> TimeNames { get; } = new();
        public ObservableCollection<string> WindowStyleNames { get; } = new();
        public ObservableCollection<string> MenuNames { get; } = new();
        public ObservableCollection<string> BackdropNames { get; } = new();
        private IReadOnlyList<TouchMenu> _menus = Array.Empty<TouchMenu>();

        private int _terrainIndex = 2;      // Lawn, used by most battles
        public int TerrainIndex { get => _terrainIndex; set { if (Set(ref _terrainIndex, value)) Refresh(); } }

        private int _timeIndex;
        public int TimeIndex { get => _timeIndex; set { if (Set(ref _timeIndex, value)) Refresh(); } }

        private int _windowStyleIndex;
        public int WindowStyleIndex { get => _windowStyleIndex; set { if (Set(ref _windowStyleIndex, value)) Refresh(); } }

        private int _menuIndex;
        public int MenuIndex { get => _menuIndex; set { if (Set(ref _menuIndex, value)) Refresh(); } }

        /// <summary>0 follows the ground; 1 onwards are the backdrops in order.</summary>
        private int _backdropIndex;
        public int BackdropIndex { get => _backdropIndex; set { if (Set(ref _backdropIndex, value)) Refresh(); } }

        // ── The sample the preview writes, so a changed graphic can be judged ─────
        private string _sampleName = "NIDORAN";
        public string SampleName { get => _sampleName; set { if (Set(ref _sampleName, value)) DrawText(); } }

        private int _sampleLevel = 100;
        public int SampleLevel { get => _sampleLevel; set { if (Set(ref _sampleLevel, value)) DrawText(); } }

        private string _sampleMessage = "Wild PIDGEY appeared!";
        public string SampleMessage { get => _sampleMessage; set { if (Set(ref _sampleMessage, value)) DrawText(); } }

        private double _hpLeft = 1.0;
        public double HpLeft { get => _hpLeft; set { if (Set(ref _hpLeft, Math.Clamp(value, 0, 1))) DrawText(); } }

        // ── The pieces ────────────────────────────────────────────────────────────
        public ObservableCollection<BattlePieceRow> Pieces { get; } = new();

        private int _selectedIndex = -1;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set { if (Set(ref _selectedIndex, value)) RaiseSelection(); }
        }

        public BattlePieceRow Selected =>
            _selectedIndex >= 0 && _selectedIndex < Pieces.Count ? Pieces[_selectedIndex] : null;

        public bool HasSelection => Selected != null;
        public string SelectedName => Selected?.Name ?? "Nothing picked";
        public string SelectedWhat => Selected?.Piece.What ?? "Pick a piece on either screen or in the list.";
        public string SelectedShared => Selected?.Piece.SharedNote;
        public bool SelectedIsShared => !string.IsNullOrEmpty(SelectedShared);

        /// <summary>Where the outline round the picked piece goes, in screen pixels.</summary>
        public double HighlightLeft => Selected?.Piece.PaintedLeft ?? 0;
        public double HighlightTop => Selected?.Piece.PaintedTop ?? 0;
        public double HighlightWidth => Selected?.Piece.PaintedWidth ?? 0;
        public double HighlightHeight => Selected?.Piece.PaintedHeight ?? 0;
        public bool HighlightOnTop => HasSelection && !Selected.Piece.Touch;
        public bool HighlightOnTouch => HasSelection && Selected.Piece.Touch;

        /// <summary>Whether the picked piece is one this editor can hand to the painter.</summary>
        public bool CanPaint => CannotPaintBecause == null;
        public bool TakesButtonArt => Selected?.Piece?.TakesButtonArt == true;
        public bool CanImport => CanPaint || TakesButtonArt;
        public string CannotPaintBecause =>
            Selected == null ? ""
            : Selected.IsMissing ? "This piece couldn't be drawn, so there is nothing to paint."
            : Selected.Piece.CannotEditBecause
              ?? (Selected.Piece.Drawing < 0 ? "This piece isn't a single drawing in an archive." : null);

        /// <summary>The file All graphics opens: a touch layer's screen, or the drawing.</summary>
        public int HandOverFile => Selected?.Piece == null ? -1
            : Selected.Piece.Touch && Selected.Piece.Arrangement >= 0 ? Selected.Piece.Arrangement : Selected.Piece.Drawing;

        public bool CanHandOver => HandOverFile >= 0;

        /// <summary>Which file the hand-over opens, so the button can say so.</summary>
        public string HandOverWhat => !CanHandOver ? null
            : $"Opens file {HandOverFile} of the {Selected.Piece.Archive} archive.";

        /// <summary>
        /// The painter edits the NCGR drawing. The optional layout is an NCER/NSCR that places those
        /// pixels and cannot itself be painted.
        /// </summary>
        public static int PaintableEntry(BattleScreenRenderer.Piece piece) => piece?.Drawing ?? -1;

        // ── The two screens ───────────────────────────────────────────────────────
        private Bitmap _topScreen, _touchScreen;
        public Bitmap TopScreen { get => _topScreen; private set => Set(ref _topScreen, value); }
        public Bitmap TouchScreen { get => _touchScreen; private set => Set(ref _touchScreen, value); }

        private string _statusText = "";
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        // Imports wait here until Save; the screens are drawn from them meanwhile.
        public PendingArchiveWrites Pending { get; } = new PendingArchiveWrites();
        public bool HasUnsavedChanges => Pending.IsDirty;
        public string UnsavedChangesDescription => "Battle screen graphics";
        public bool CanUndo => Pending.CanUndo;
        public bool CanRedo => Pending.CanRedo;

        public void Undo() { if (!Pending.CanUndo) return; Pending.Undo(); Reload(); }
        public void Redo() { if (!Pending.CanRedo) return; Pending.Redo(); Reload(); }

        public void SaveChanges()
        {
            if (!HasUnsavedChanges) return;
            Pending.Save();
            Reload();
            SaveNotice.Saved(UnsavedChangesDescription);
        }

        public void DiscardChanges()
        {
            if (!HasUnsavedChanges) return;
            Pending.Discard();
            Reload();
        }

        /// <summary>Reads on this thread see the pending archive members and bar pieces until disposed.</summary>
        public IDisposable Reading()
        {
            IDisposable members = Pending.Reading();
            IDisposable sheet = BattleGaugePieces.Staged(Pending.Read(HpBarImport.SheetTarget));
            return new Data.DrawScopes(members, sheet);
        }

        /// <summary>Runs an import whose writes, bar pieces included, become one pending step. Returns its error or null.</summary>
        public string Import(Func<string> import)
        {
            string error = Pending.Import(() =>
            {
                using (BattleGaugePieces.Staged(Pending.Read(HpBarImport.SheetTarget))) return import();
            });
            Reload();
            return error;
        }

        /// <summary>Stands in for writing the bar pieces during <see cref="Import"/>.</summary>
        public string StageBarPieces(byte[] sheet) { Pending.Stage(HpBarImport.SheetTarget, sheet); return null; }

        private IDisposable _picturesSaved;

        public void Detach()
        {
            AppEvents.RomPatchStateChanged -= OnPatchStateChanged;
            _picturesSaved?.Dispose();
        }

        public bool CanClearTextBackground => HpBarTextPatchOffer.CanOffer;
        public string ClearTextBackgroundHelp => HpBarTextPatchOffer.Tip;

        private void OnPatchStateChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(CanClearTextBackground));
            Refresh();
        }

        /// <summary>Reads the pieces again after a member was written, including by the painter.</summary>
        public void Reload()
        {
            BattleGaugeTextRenderer.Reset();
            Refresh();
        }

        private void RaiseEdits()
        {
            foreach (string n in new[] { nameof(HasUnsavedChanges), nameof(CanUndo), nameof(CanRedo) })
                OnPropertyChanged(n);
        }

        private string[] _speciesNames;

        // The sprites show the species the sample name spells, or Pidgey.
        private int SampleSpecies()
        {
            try { _speciesNames ??= RomInfo.GetPokemonNames(); }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { AppLogger.WarnOnce("BattleScreenEditorViewModel.SampleSpecies", ex); _speciesNames = Array.Empty<string>(); }
            int found = Array.FindIndex(_speciesNames, n => string.Equals(n, _sampleName?.Trim(), StringComparison.OrdinalIgnoreCase));
            return found > 0 && found <= RomInfo.LastVanillaSpecies ? found : new BattleScreenRenderer.Options().Species;
        }

        /// <summary>Redraws both screens from the ROM.</summary>
        public void Refresh()
        {
            if (_loading) return;
            try
            {
                using IDisposable pending = Reading();
                _pieces = _renderer.Build(new BattleScreenRenderer.Options
                {
                    TerrainId = _terrainIndex,
                    TimeOfDay = _timeIndex,
                    WindowStyle = _windowStyleIndex,
                    Menu = _menuIndex >= 0 && _menuIndex < _menus.Count ? _menus[_menuIndex] : TouchMenu.Background,
                    BackdropId = _backdropIndex - 1,
                    PokemonName = _sampleName,
                    Species = SampleSpecies(),
                    Level = _sampleLevel,
                    Gender = _gender,
                    Status = _status,
                    Doubles = _doubles,
                    BallCounter = _ballCounter,
                    NumbersInsteadOfBar = _numbersInsteadOfBar,
                    HealthLeft = _hpLeft,
                });

                string keep = Selected?.Name;
                Pieces.Clear();
                foreach (BattleScreenRenderer.Piece p in _pieces) Pieces.Add(new BattlePieceRow { Piece = p });

                DrawBarPieces();
                // The gauge slides in before the HP bars come up.
                byte[] top = BattleScreenRenderer.Flatten(_partyGauge ? _pieces.Where(p => !p.Name.EndsWith(" HP bar", StringComparison.Ordinal)).ToList() : _pieces,
                                                          touch: false);
                if (_partyGauge && top != null) GameTextScreens.BattlePartyGauge(top);
                if (_levelUp && top != null) GameTextScreens.BattleLevelUp(top);
                TopScreen = ToBitmap(top);
                byte[] touch = BattleScreenRenderer.Flatten(_pieces, touch: true);
                if (_catchHand && touch != null) GameTextScreens.CatchTutorialHand(touch, _menuIndex >= 0 && _menuIndex < _menus.Count ? _menus[_menuIndex] : TouchMenu.Background);
                TouchScreen = ToBitmap(touch);

                int at = keep == null ? -1 : Pieces.ToList().FindIndex(r => r.Name == keep);
                _selectedIndex = at;
                OnPropertyChanged(nameof(SelectedIndex));
                RaiseSelection();

                int missing = _pieces.Count(p => p.Rgba == null);
                StatusText = missing == 0
                    ? $"{_pieces.Count} pieces."
                    : $"{_pieces.Count} pieces, {missing} of them couldn't be drawn.";
            }
            catch (Exception ex)
            {
                StatusText = "The battle screen couldn't be put together: " + ex.Message;
                AppLogger.Error("BattleScreenEditor.Refresh: " + ex);
            }
        }

        /// <summary>Picks whatever was clicked on one of the screens.</summary>
        public void PickAt(bool touch, int x, int y)
        {
            BattleScreenRenderer.Piece hit = BattleScreenRenderer.At(_pieces, touch, x, y);
            if (hit == null) return;
            SelectedIndex = Pieces.ToList().FindIndex(r => ReferenceEquals(r.Piece, hit));
        }

        private void RaiseSelection()
        {
            foreach (string n in new[]
            {
                nameof(Selected), nameof(HasSelection), nameof(SelectedName), nameof(SelectedWhat),
                nameof(SelectedShared), nameof(SelectedIsShared), nameof(CanPaint), nameof(CannotPaintBecause), nameof(CanImport),
                nameof(CanHandOver), nameof(HandOverWhat), nameof(ShowsBarPieces),
                nameof(HighlightLeft), nameof(HighlightTop), nameof(HighlightWidth), nameof(HighlightHeight),
                nameof(HighlightOnTop), nameof(HighlightOnTouch),
            }) OnPropertyChanged(n);
        }

        // The writing goes into the bar's own picture, so changing it means drawing the bar again.
        private void DrawText()
        {
            OnPropertyChanged(nameof(SampleLevelText));
            OnPropertyChanged(nameof(HpBarWidth));
            Refresh();
        }

        public string SampleLevelText => "Lv" + _sampleLevel;
        public double HpBarWidth => 48 * _hpLeft;

        /// <summary>Where the fill is not copied in from the game's pieces, a plain one is drawn over the bar.</summary>
        public bool ShowsPlainFill => !_doubles && BattleGaugePieces.Locate()?.HasSlots != true;

        // ── the gauge's own letters ───────────────────────────────────────────────
        //
        // The name, level and gender symbol use the game's own letters and are drawn into the bar, along
        // with the health and experience fill copied from the battle code's pieces.

        /// <summary>Whether the real pictures can be used, and what to say when they cannot.</summary>
        public bool GaugeTextIsReal => BattleGaugeTextRenderer.IsAvailable;
        public string GaugeTextNote => BattleGaugeTextRenderer.Unavailable;
        public bool HasGaugeTextNote => GaugeTextNote != null;

        private BattleGaugeText.Gender _gender = BattleGaugeText.Gender.Male;
        public int GenderChoice
        {
            get => (int)_gender;
            set
            {
                BattleGaugeText.Gender picked = (BattleGaugeText.Gender)Math.Clamp(value, 0, 2);
                if (_gender == picked) return;
                _gender = picked;
                OnPropertyChanged(nameof(GenderChoice));
                DrawText();
            }
        }

        private BattleGaugeText.Status _status = BattleGaugeText.Status.None;
        public int StatusChoice
        {
            get => (int)_status;
            set
            {
                BattleGaugeText.Status picked = (BattleGaugeText.Status)Math.Clamp(value, 0, 5);
                if (_status == picked) return;
                _status = picked;
                OnPropertyChanged(nameof(StatusChoice));
                DrawText();
            }
        }

        private bool _doubles;
        // 1 the Safari Zone, 2 Pal Park.
        private int _ballCounter;
        public bool HasBallCounter => RomInfo.GameFamily is RomInfo.GameFamilies.Plat or RomInfo.GameFamilies.HGSS;
        public bool IsDoubles => _doubles;

        private bool _levelUp;
        /// <summary>Shows the stat gains window a level-up puts over the battle.</summary>
        public bool LevelUp
        {
            get => _levelUp;
            set { if (_levelUp == value) return; _levelUp = value; OnPropertyChanged(nameof(LevelUp)); Refresh(); }
        }

        private bool _catchHand;
        /// <summary>Shows the hand that taps for the player in Platinum's catching demo.</summary>
        public bool CatchHand
        {
            get => _catchHand;
            set { if (_catchHand == value) return; _catchHand = value; OnPropertyChanged(nameof(CatchHand)); Refresh(); }
        }

        public bool HasCatchHand => RomInfo.GameFamily == RomInfo.GameFamilies.Plat;

        private bool _partyGauge;
        /// <summary>Shows the arrows and balls that slide in as a trainer battle starts.</summary>
        public bool PartyGauge
        {
            get => _partyGauge;
            set { if (_partyGauge == value) return; _partyGauge = value; OnPropertyChanged(nameof(PartyGauge)); Refresh(); }
        }

        private bool _numbersInsteadOfBar;
        /// <summary>In a double battle the games swap the player's bars for the numbers on a press.</summary>
        public bool NumbersInsteadOfBar
        {
            get => _numbersInsteadOfBar;
            set { if (_numbersInsteadOfBar == value) return; _numbersInsteadOfBar = value;
                  OnPropertyChanged(nameof(NumbersInsteadOfBar)); Refresh(); }
        }

        /// <summary>One Pokemon a side, or two. The games use a different set of bars for each.</summary>
        public int BattleSizeChoice
        {
            get => _doubles ? 1 : _ballCounter > 0 ? _ballCounter + 1 : 0;
            set
            {
                if (value < 0) return;
                bool picked = value == 1;
                int counter = value >= 2 ? value - 1 : 0;
                if (_doubles == picked && _ballCounter == counter) return;
                _doubles = picked;
                _ballCounter = counter;
                OnPropertyChanged(nameof(BattleSizeChoice));
                OnPropertyChanged(nameof(IsDoubles));
                OnPropertyChanged(nameof(ShowsPlainFill));
                Refresh();
            }
        }

        public bool ShowsBarPieces => Selected?.Piece.UsesBarPieces == true;

        private const int BarPieceZoom = 3;

        public ObservableCollection<BarPieceGroupRow> BarPieceGroups { get; } = new();
        public bool HasBarPieces => BarPieceGroups.Count > 0;

        public string BarPiecesNote => HasBarPieces ? null
            : BattleGaugePieces.WhyNot ?? "The HP bar colours couldn't be read.";

        private void DrawBarPieces()
        {
            BarPieceGroups.Clear();
            foreach (HpBarPiecesSheet.Group g in HpBarPiecesSheet.Groups())
            {
                byte[] rgba = HpBarPiecesSheet.GroupRgba(g, out int w, out int h);
                if (rgba == null) { BarPieceGroups.Clear(); break; }
                BarPieceGroups.Add(new BarPieceGroupRow
                {
                    Name = g.Name, Picture = ImageConverter.FromRgba(rgba, w, h),
                    Width = w * BarPieceZoom, Height = h * BarPieceZoom,
                });
            }
            OnPropertyChanged(nameof(HasBarPieces));
            OnPropertyChanged(nameof(BarPiecesNote));
        }

        public string ImportBarPieces(string path)
        {
            return Import(() => HpBarPiecesSheet.Import(path, StageBarPieces));
        }

        private static Bitmap ToBitmap(byte[] rgba)
        {
            try { return ImageConverter.FromRgba(rgba, BattleScreenRenderer.ScreenWidth, BattleScreenRenderer.ScreenHeight); }
            catch (Exception ex) { AppLogger.WarnOnce("BattleScreenEditorViewModel.ToBitmap", ex); return null; }
        }
    }
}
