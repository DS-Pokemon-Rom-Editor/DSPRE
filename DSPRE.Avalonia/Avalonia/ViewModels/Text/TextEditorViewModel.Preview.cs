using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.CharMaps;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Text
{
    public partial class TextEditorViewModel
    {
        private readonly List<FieldMessageFrame> _previewFrames = new List<FieldMessageFrame>();
        private int _previewFrame;

        public Func<string, int> MeasureText { get; set; } = t => (t ?? "").Length * 6;

        private bool _showPreview = true;

        public bool ShowPreview
        {
            get => _showPreview;
            set { if (Set(ref _showPreview, value)) RefreshPreview(); }
        }

        private FieldMessageFrame CurrentPreview =>
            _previewFrame >= 0 && _previewFrame < _previewFrames.Count ? _previewFrames[_previewFrame] : null;

        public string PreviewText => CurrentPreview?.Text ?? "";

        public bool HasPreview => CurrentPreview != null;

        public bool HasMoreStops => _previewFrames.Count > 1;

        /// <summary>The game screen this archive is drawn on, null when none is known.</summary>
        public FieldTextArchives.Screen Screen => _current == null ? null : FieldTextArchives.ScreenFor(_current.ID);

        public bool IsOnScreen => Screen != null;

        public ObservableCollection<DSPRE.Avalonia.Data.TextScreenShot> ScreenShots { get; } = new ObservableCollection<DSPRE.Avalonia.Data.TextScreenShot>();

        public bool HasScreenShots => ScreenShots.Count > 0;

        public bool ShotsHaveStops => ShotStops > 1;

        // Screens step through their own boxes, which can differ from the message box's.
        private int ShotStops => ScreenShots.Count == 0 ? 0 : ScreenShots.Max(s => s.Stops);

        private int StopCount => HasScreenShots ? ShotStops : _previewFrames.Count;

        public bool ShotsMoveOn => ShotsHaveStops && ScreenShots.Where(s => s.Stops > 1).All(s => s.MovesOnByItself);

        public bool ShotsWaitForPress => ShotsHaveStops && !ShotsMoveOn;

        private global::Avalonia.Threading.DispatcherTimer _playStops;

        // Plays each box about as long as it stays up in the game.
        private void UpdatePlayStops()
        {
            if (ShotsMoveOn)
            {
                if (_playStops != null) return;
                _playStops = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                _playStops.Tick += (_, _) => NextPreviewStep();
                _playStops.Start();
            }
            else if (_playStops != null)
            {
                _playStops.Stop();
                _playStops = null;
            }
        }

        private int _shotsAsked;

        // Drawn in the background. Only the newest result is shown.
        private async Task RefreshScreenShotsAsync()
        {
            int asked = ++_shotsAsked;
            int bank = _current?.ID ?? -1;
            int line = _selectedLineIndex;
            string text = line >= 0 && line < Lines.Count ? Lines[line]?.Text : null;
            List<DSPRE.Avalonia.Data.TextScreenShot> shots = new List<DSPRE.Avalonia.Data.TextScreenShot>();
            if (_showPreview && bank >= 0 && text != null)
            {
                int stop = _previewFrame;
                try { shots = await Task.Run(() => DSPRE.Avalonia.Data.TextScreenPreviews.For(bank, line, text, stop)); }
                catch (Exception ex) { AppLogger.WarnOnce("TextEditorViewModel.ScreenShots", ex); }
            }
            if (asked != _shotsAsked) return;
            _bankWithShots = shots.Count > 0 ? bank : -1;
            ScreenShots.Clear();
            foreach (DSPRE.Avalonia.Data.TextScreenShot shot in shots) ScreenShots.Add(shot);
            OnPropertyChanged(nameof(HasScreenShots));
            OnPropertyChanged(nameof(ShotsHaveStops));
            OnPropertyChanged(nameof(PreviewStepText));
            OnPropertyChanged(nameof(ShotsMoveOn));
            OnPropertyChanged(nameof(ShotsWaitForPress));
            OnPropertyChanged(nameof(ShowsMessageBox));
            UpdatePlayStops();
        }

        // Hides the box while the next line's pictures are drawn.
        private int _bankWithShots = -1;

        public bool ShowsMessageBox => Screen == null && ScreenShots.Count == 0 && _bankWithShots != (_current?.ID ?? -1);

        public string ScreenName => Screen?.Name ?? "";

        public IReadOnlyList<string> ScreenLines
        {
            get
            {
                FieldTextArchives.Screen screen = Screen;
                string text = _selectedLineIndex >= 0 && _selectedLineIndex < Lines.Count ? Lines[_selectedLineIndex]?.Text : null;
                if (string.IsNullOrEmpty(text) || screen == null) return new[] { "" };
                List<FieldMessageFrame> frames = FieldMessageScript.Frames(FieldStringVars.Expand(text, GameTextScreens.SampleWord),
                                                                           MeasureText, screen.TilesWide * FieldMessageWindow.TileSize,
                                                                           screen.TilesHigh / 2, wrapWhenUnmarked: false);
                return frames.Count > 0 ? frames[0].Lines : new[] { "" };
            }
        }

        public bool PreviewHasMore => CurrentPreview != null && CurrentPreview.Wait != MessageWait.None;

        public string PreviewStepText => StopCount > 1
            ? $"Box {_previewFrame % StopCount + 1} of {StopCount}" : "";

        public string PreviewWaitText
        {
            get
            {
                FieldMessageFrame f = CurrentPreview;
                if (f == null) return "";
                switch (f.Wait)
                {
                    case MessageWait.Clear: return "A clears the box";
                    case MessageWait.Scroll: return "A scrolls up a line";
                    case MessageWait.Simple: return "A continues";
                    default: return "End of message";
                }
            }
        }

        /// <summary>Set when the line will not fit the box as written.</summary>
        public string PreviewWarning
        {
            get
            {
                FieldMessageFrame f = CurrentPreview;
                if (f == null) return null;
                if (f.TooWide && f.TooManyLines) return "This runs past the right edge and past the bottom of the box.";
                if (f.TooWide) return "A line here runs past the right edge of the box.";
                if (f.TooManyLines) return "There are more lines here than the box can show.";
                return null;
            }
        }

        public bool HasPreviewWarning => PreviewWarning != null;

        public ObservableCollection<string> BorderNames { get; } = new ObservableCollection<string>();

        private int _borderIndex;

        public int BorderIndex
        {
            get => _borderIndex;
            set { if (Set(ref _borderIndex, value)) BorderChanged?.Invoke(this, EventArgs.Empty); }
        }

        public event EventHandler BorderChanged;

        public string PreviewFontNote { get; set; }
        public bool HasPreviewFontNote => !string.IsNullOrEmpty(PreviewFontNote);

        public void NextPreviewStep()
        {
            if (StopCount == 0) return;
            _previewFrame = (_previewFrame + 1) % StopCount;
            RaisePreviewChanged();
            if (ShotsHaveStops) _ = RefreshScreenShotsAsync();
        }

        public void RefreshPreview()
        {
            _previewFrames.Clear();
            _previewFrame = 0;

            if (_showPreview && _selectedLineIndex >= 0 && _selectedLineIndex < Lines.Count)
            {
                string text = Lines[_selectedLineIndex]?.Text;
                // The game fills in names and numbers, so the box shows sample words in their place.
                if (!string.IsNullOrEmpty(text))
                    _previewFrames.AddRange(FieldMessageScript.Frames(FieldStringVars.Expand(text, GameTextScreens.SampleWord), MeasureText));
            }
            RaisePreviewChanged();
            _ = RefreshScreenShotsAsync();
        }

        private void RaisePreviewChanged()
        {
            OnPropertyChanged(nameof(PreviewText));
            OnPropertyChanged(nameof(HasPreview));
            OnPropertyChanged(nameof(HasMoreStops));
            OnPropertyChanged(nameof(Screen));
            OnPropertyChanged(nameof(IsOnScreen));
            OnPropertyChanged(nameof(ShowsMessageBox));
            OnPropertyChanged(nameof(ScreenName));
            OnPropertyChanged(nameof(ScreenLines));
            OnPropertyChanged(nameof(PreviewHasMore));
            OnPropertyChanged(nameof(PreviewStepText));
            OnPropertyChanged(nameof(PreviewWaitText));
            OnPropertyChanged(nameof(PreviewWarning));
            OnPropertyChanged(nameof(HasPreviewWarning));
            OnPropertyChanged(nameof(PreviewFontNote));
            OnPropertyChanged(nameof(HasPreviewFontNote));
        }

        public bool CanMoveUp => _selectedLineIndex > 0;
        public bool CanMoveDown => _selectedLineIndex >= 0 && _selectedLineIndex < Lines.Count - 1;

        private bool _hexNumbering = true;
        public bool HexNumbering
        {
            get => _hexNumbering;
            set { if (Set(ref _hexNumbering, value)) { RenumberLines(); SettingsManager.Settings.TextEditorPreferHex = value; } }
        }
    }
}
