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
        public string Where => Piece.Touch ? "Touch screen" : "Top screen";
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
            Refresh();
        }

        // ── What the screens are showing ──────────────────────────────────────────
        public ObservableCollection<string> TerrainNames { get; } = new();
        public ObservableCollection<string> TimeNames { get; } = new();
        public ObservableCollection<string> WindowStyleNames { get; } = new();
        public ObservableCollection<string> MenuNames { get; } = new();
        public ObservableCollection<string> BackdropNames { get; } = new();
        private IReadOnlyList<TouchMenu> _menus = Array.Empty<TouchMenu>();

        private int _terrainIndex = 2;      // Lawn, the one most battles use
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
        public string CannotPaintBecause =>
            Selected == null ? ""
            : Selected.IsMissing ? "This piece could not be drawn, so there is nothing to paint."
            : Selected.Piece.CannotEditBecause
              ?? (Selected.Piece.Drawing < 0 ? "This piece is not a single drawing in an archive." : null);

        /// <summary>
        /// Whether the sheet this piece is drawn from can be opened in the Graphics window. That works
        /// even for the pieces the painter has to refuse: the touch screen layers share one sheet, so a
        /// painted picture cannot go back into a single layer, but the sheet itself is still editable.
        /// </summary>
        public bool CanHandOver => Selected?.Piece != null && Selected.Piece.Drawing >= 0;

        /// <summary>Which file the hand-over opens, so the button can say so.</summary>
        public string HandOverWhat => !CanHandOver ? null
            : $"Opens file {Selected.Piece.Drawing} of the {Selected.Piece.Archive} archive.";

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
            return new Both(sheet, members);
        }

        private sealed class Both : IDisposable
        {
            private readonly IDisposable _a, _b;
            public Both(IDisposable a, IDisposable b) { _a = a; _b = b; }
            public void Dispose() { _a.Dispose(); _b.Dispose(); }
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

        public void Detach() => AppEvents.RomPatchStateChanged -= OnPatchStateChanged;

        public bool CanClearTextBackground => HpBarTextPatchOffer.CanOffer;
        public string ClearTextBackgroundHelp => HpBarTextPatchOffer.Tip;

        public void OpenTextBackgroundPatch() => HpBarTextPatchOffer.Open();

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
                    Level = _sampleLevel,
                    Gender = _gender,
                    Status = _status,
                    Doubles = _doubles,
                    NumbersInsteadOfBar = _numbersInsteadOfBar,
                    HealthLeft = _hpLeft,
                });

                string keep = Selected?.Name;
                Pieces.Clear();
                foreach (BattleScreenRenderer.Piece p in _pieces) Pieces.Add(new BattlePieceRow { Piece = p });

                DrawBarPieces();
                TopScreen = ToBitmap(BattleScreenRenderer.Flatten(_pieces, touch: false));
                TouchScreen = ToBitmap(BattleScreenRenderer.Flatten(_pieces, touch: true));

                int at = keep == null ? -1 : Pieces.ToList().FindIndex(r => r.Name == keep);
                _selectedIndex = at;
                OnPropertyChanged(nameof(SelectedIndex));
                RaiseSelection();

                int missing = _pieces.Count(p => p.Rgba == null);
                StatusText = missing == 0
                    ? $"{_pieces.Count} pieces."
                    : $"{_pieces.Count} pieces, {missing} of them could not be drawn.";
            }
            catch (Exception ex)
            {
                StatusText = "The battle screen could not be put together: " + ex.Message;
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
                nameof(SelectedShared), nameof(SelectedIsShared), nameof(CanPaint), nameof(CannotPaintBecause),
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
        // The name, the level and the gender symbol are drawn with the pictures the game itself uses,
        // rather than typed out in a desktop font, and go into the bar's own picture, and so do the
        // health and experience fill, copied from the pieces the battle code keeps.

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
        public bool IsDoubles => _doubles;

        private bool _numbersInsteadOfBar;
        /// <summary>In a double battle the games swap your bars for the numbers on a press.</summary>
        public bool NumbersInsteadOfBar
        {
            get => _numbersInsteadOfBar;
            set { if (_numbersInsteadOfBar == value) return; _numbersInsteadOfBar = value;
                  OnPropertyChanged(nameof(NumbersInsteadOfBar)); Refresh(); }
        }

        /// <summary>One Pokemon a side, or two. The games use a different set of bars for each.</summary>
        public int BattleSizeChoice
        {
            get => _doubles ? 1 : 0;
            set
            {
                bool picked = value == 1;
                if (_doubles == picked) return;
                _doubles = picked;
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
            : BattleGaugePieces.WhyNot ?? "The HP bar colours could not be read.";

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
            catch { return null; }
        }
    }
}
