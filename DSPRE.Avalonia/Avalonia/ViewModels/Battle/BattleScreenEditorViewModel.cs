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
    public sealed class BattleScreenEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
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
            if (Design.IsDesignMode) { _loading = false; return; }

            // The preview writes in the ROM's own font, which is read once and kept for every window
            // that draws game text.
            try { Views.Controls.FieldMessageBoxView.Font ??= ROMFiles.FieldFont.LoadTalkFont(); }
            catch (Exception ex) { AppLogger.Error("Battle screen font: " + ex.Message); }

            foreach (var t in BattleGroundRenderer.TerrainNames) TerrainNames.Add(t);
            foreach (var t in new[] { "Day", "Evening", "Night" }) TimeNames.Add(t);
            for (int i = 0; i < ROMFiles.FieldWindowFrame.FrameCount; i++)
                WindowStyleNames.Add("Text box style " + (i + 1));

            _loading = false;
            Refresh();
        }

        // ── What the screens are showing ──────────────────────────────────────────
        public ObservableCollection<string> TerrainNames { get; } = new();
        public ObservableCollection<string> TimeNames { get; } = new();
        public ObservableCollection<string> WindowStyleNames { get; } = new();

        private int _terrainIndex = 2;      // Lawn, the one most battles use
        public int TerrainIndex { get => _terrainIndex; set { if (Set(ref _terrainIndex, value)) Refresh(); } }

        private int _timeIndex;
        public int TimeIndex { get => _timeIndex; set { if (Set(ref _timeIndex, value)) Refresh(); } }

        private int _windowStyleIndex;
        public int WindowStyleIndex { get => _windowStyleIndex; set { if (Set(ref _windowStyleIndex, value)) Refresh(); } }

        private bool _showCommandPanel = true;
        public bool ShowCommandPanel { get => _showCommandPanel; set { if (Set(ref _showCommandPanel, value)) Refresh(); } }

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
        public string SelectedWhat => Selected?.Piece.What ?? "Pick a piece, on either screen or in the list.";
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
            Selected == null ? "Pick a piece first."
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

        public bool HasUnsavedChanges => false;
        public string UnsavedChangesDescription => "Battle screen";
        public void SaveChanges() { }
        public void DiscardChanges() { }

        /// <summary>Redraws both screens from the ROM.</summary>
        public void Refresh()
        {
            if (_loading) return;
            try
            {
                _pieces = _renderer.Build(new BattleScreenRenderer.Options
                {
                    TerrainId = _terrainIndex,
                    TimeOfDay = _timeIndex,
                    WindowStyle = _windowStyleIndex,
                    ShowCommandPanel = _showCommandPanel,
                    PokemonName = _sampleName,
                    Level = _sampleLevel,
                    Gender = _gender,
                    Status = _status,
                    Doubles = _doubles,
                    NumbersInsteadOfBar = _numbersInsteadOfBar,
                });

                string keep = Selected?.Name;
                Pieces.Clear();
                foreach (var p in _pieces) Pieces.Add(new BattlePieceRow { Piece = p });

                TopScreen = ToBitmap(BattleScreenRenderer.Flatten(_pieces, touch: false));
                TouchScreen = ToBitmap(BattleScreenRenderer.Flatten(_pieces, touch: true));

                int at = keep == null ? -1 : Pieces.ToList().FindIndex(r => r.Name == keep);
                _selectedIndex = at;
                OnPropertyChanged(nameof(SelectedIndex));
                RaiseSelection();

                int missing = _pieces.Count(p => p.Rgba == null);
                StatusText = missing == 0
                    ? $"{_pieces.Count} pieces, all drawn from this ROM."
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
            var hit = BattleScreenRenderer.At(_pieces, touch, x, y);
            if (hit == null) return;
            SelectedIndex = Pieces.ToList().FindIndex(r => ReferenceEquals(r.Piece, hit));
        }

        private void RaiseSelection()
        {
            foreach (var n in new[]
            {
                nameof(Selected), nameof(HasSelection), nameof(SelectedName), nameof(SelectedWhat),
                nameof(SelectedShared), nameof(SelectedIsShared), nameof(CanPaint), nameof(CannotPaintBecause),
                nameof(CanHandOver), nameof(HandOverWhat),
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

        // ── the gauge's own letters ───────────────────────────────────────────────
        //
        // The name, the level and the gender symbol are drawn with the pictures the game itself uses,
        // rather than typed out in a desktop font, and go into the bar's own picture. Checked against a
        // frame captured from a real battle: 93.6% of their bar and 95.0% of yours. What is left over
        // is the green health fill and the arrow, which the game draws on top rather than into the bar.

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
                var picked = (BattleGaugeText.Gender)Math.Clamp(value, 0, 2);
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
                var picked = (BattleGaugeText.Status)Math.Clamp(value, 0, 5);
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
                Refresh();
            }
        }

        private static Bitmap ToBitmap(byte[] rgba)
        {
            try { return ImageConverter.FromRgba(rgba, BattleScreenRenderer.ScreenWidth, BattleScreenRenderer.ScreenHeight); }
            catch { return null; }
        }
    }
}
