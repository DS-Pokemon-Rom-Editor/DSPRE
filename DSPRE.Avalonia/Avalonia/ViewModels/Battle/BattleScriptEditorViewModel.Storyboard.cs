using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using DSPRE.Avalonia.BattleAnim;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Threading;
using DSPRE.Avalonia.Data;
using static DSPRE.RomInfo;
using Avalonia.Platform;
using System.Runtime.InteropServices;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    public sealed partial class BattleScriptEditorViewModel
    {
        private void RefreshStoryboard()
        {
            OnPropertyChanged(nameof(ShowStoryboard));
            OnPropertyChanged(nameof(StoryboardTitle));
            OnPropertyChanged(nameof(StoryboardWrap));
            if (!HasRows) { Storyboard = ""; StoryboardLines = Array.Empty<BattleAnimStoryboard.Line>(); return; }
            List<WazaSeqCommand> cmds = BuildCommands();
            RecordTimeline(cmds);
            if (IsAnimation)
            {
                List<BattleAnimStoryboard.Line> lines = BattleAnimStoryboard.Build(cmds, _version);
                StoryboardLines = lines;
                Storyboard = lines.Count == 0 ? "(empty script)" : "";
            }
            else
            {
                StoryboardLines = Array.Empty<BattleAnimStoryboard.Line>();
                Storyboard = WazaSeqStoryboard.Build(cmds, _version);
            }
        }

        private readonly WeCellAnimRenderer _cellRenderer = new WeCellAnimRenderer();
        private IReadOnlyList<WeCellAnimRenderer.Frame> _cellFrames = Array.Empty<WeCellAnimRenderer.Frame>();
        private readonly ScriptNarc _particleNarc = new ScriptNarc(DirNames.WazaParticle);
        private BattleAnimPlayer _animPlayer;
        private DispatcherTimer _previewTimer;
        private int _cellFrameIdx, _cellTick, _cellLoops, _previewFrames;
        private const int MaxPreviewFrames = 1200;   // safety cap in script passes

        // The UI timer fires irregularly, so passes are counted from a stopwatch, not one per callback.
        private readonly System.Diagnostics.Stopwatch _clock = new System.Diagnostics.Stopwatch();
        private long _passesRun;

        // Scripts run once per two screen frames. 60 is kept to preview a hack that runs them faster.
        private static int PreviewFps => DSPRE.SettingsManager.Settings?.BattlePreviewFps == 60 ? 60 : 30;

        public bool HasCellAnimation { get; private set; }
        public bool HasParticleAnimation { get; private set; }
        public bool HasPreview => IsAnimation && _fileIndex >= 0;
        public string CellAnimNote { get; private set; } = "";

        /// <summary>What this move does that the preview does not show.</summary>
        public string PreviewNotes => _animPlayer == null || _animPlayer.Notes.Count == 0
            ? "" : "Not drawn: " + string.Join(", ", _animPlayer.Notes);

        public bool HasPreviewNotes => PreviewNotes.Length > 0;

        private int _notesShown;

        private Bitmap _cellPreview;
        public Bitmap CellPreview { get => _cellPreview; private set => Set(ref _cellPreview, value); }
        private Bitmap _backgroundFrame;
        public Bitmap BackgroundFrame { get => _backgroundFrame; private set { if (Set(ref _backgroundFrame, value)) { OnPropertyChanged(nameof(BackgroundBehind)); OnPropertyChanged(nameof(BackgroundOver)); } } }
        private bool _bgOverlay;
        public bool BackgroundIsOverlay { get => _bgOverlay; private set { if (Set(ref _bgOverlay, value)) { OnPropertyChanged(nameof(BackgroundBehind)); OnPropertyChanged(nameof(BackgroundOver)); } } }
        public Bitmap BackgroundBehind => _bgOverlay ? null : _backgroundFrame;   // backdrop-replace (Fly/Dig/Cosmic)
        public Bitmap BackgroundOver => _bgOverlay ? _backgroundFrame : null;     // effect overlay (Surf water sweep)
        private double _cellSX = 1, _cellSY = 1, _cellOpacity = 1, _cellOX, _cellOY;
        public double CellScaleX { get => _cellSX; private set => Set(ref _cellSX, value); }
        public double CellScaleY { get => _cellSY; private set => Set(ref _cellSY, value); }
        public double CellOpacity { get => _cellOpacity; private set => Set(ref _cellOpacity, value); }
        public double CellOffsetX { get => _cellOX; private set => Set(ref _cellOX, value); }
        public double CellOffsetY { get => _cellOY; private set => Set(ref _cellOY, value); }
        // The sprite's content centre, so Surf's wave scales in place.
        private global::Avalonia.RelativePoint _cellOrigin = global::Avalonia.RelativePoint.Center;
        public global::Avalonia.RelativePoint CellOrigin { get => _cellOrigin; private set => Set(ref _cellOrigin, value); }
        public bool IsCellPlaying => _previewTimer != null && _previewTimer.IsEnabled;
        private bool _holding;
        private bool ShowsMove => IsCellPlaying || _holding;
        public bool CanStopPreview => ShowsMove;
        public string CellPlayButtonText => IsCellPlaying ? "Pause" : "Play";
        public string CellPlayButtonIcon => IsCellPlaying ? null : "play";
        private double _bgDarken; public double BackgroundDarken { get => _bgDarken; private set => Set(ref _bgDarken, value); }
        private IBrush _fadeBrush = Brushes.Black; public IBrush FadeBrush { get => _fadeBrush; private set => Set(ref _fadeBrush, value); }
        private double _shakeX; public double ShakeX { get => _shakeX; private set => Set(ref _shakeX, value); }
        private double _shakeY; public double ShakeY { get => _shakeY; private set => Set(ref _shakeY, value); }
        private double _pRot, _pScaleX = 1, _pScaleY = 1, _pTintA, _eRot, _eScaleX = 1, _eScaleY = 1, _eTintA, _pDX, _pDY, _eDX, _eDY;
        private bool _pVis = true, _eVis = true;
        private IBrush _tintBrush = Brushes.Transparent;
        public double PlayerOffsetX { get => _pDX; private set => Set(ref _pDX, value); }
        public double PlayerOffsetY { get => _pDY; private set => Set(ref _pDY, value); }
        public double OpponentOffsetX { get => _eDX; private set => Set(ref _eDX, value); }
        public double OpponentOffsetY { get => _eDY; private set => Set(ref _eDY, value); }
        public double PlayerRotation { get => _pRot; private set => Set(ref _pRot, value); }
        public double PlayerScaleX { get => _pScaleX; private set => Set(ref _pScaleX, value); }
        public double PlayerScaleY { get => _pScaleY; private set => Set(ref _pScaleY, value); }
        public bool PlayerVisible { get => _pVis; private set => Set(ref _pVis, value); }
        public double PlayerTintOpacity { get => _pTintA; private set => Set(ref _pTintA, value); }
        public double OpponentRotation { get => _eRot; private set => Set(ref _eRot, value); }
        public double OpponentScaleX { get => _eScaleX; private set => Set(ref _eScaleX, value); }
        public double OpponentScaleY { get => _eScaleY; private set => Set(ref _eScaleY, value); }
        public bool OpponentVisible { get => _eVis; private set => Set(ref _eVis, value); }
        public double OpponentTintOpacity { get => _eTintA; private set => Set(ref _eTintA, value); }
        public IBrush TintBrush { get => _tintBrush; private set => Set(ref _tintBrush, value); }

        // Backdrop and sprites are placed like the Battle display tab, then blended as the NDS does.
        private readonly BattleSceneCompositor _compositor = new BattleSceneCompositor();
        private Bitmap _sceneComposite;
        public Bitmap SceneComposite { get => _sceneComposite; private set => Set(ref _sceneComposite, value); }

        private bool _sceneLoaded;
        private Bitmap _enemySprite, _playerSprite;
        public Bitmap OpponentSprite { get => _enemySprite; private set => Set(ref _enemySprite, value); }
        public Bitmap PlayerSprite { get => _playerSprite; private set => Set(ref _playerSprite, value); }
        public double OpponentLeft { get; private set; } = 152;
        public double PlayerLeft { get; private set; } = 23;
        public double OpponentTop { get; private set; } = 24;
        public double PlayerTop { get; private set; } = 84;

        // Emitters anchor on the sprite centres: 40 into the 80×80 cell, from the loaded positions.
        private double _atX = 63, _atY = 124, _dfX = 192, _dfY = 64;
        private int _backYOffset, _frontYOffset;

        private bool _attackerIsOpponent;
        public bool AttackerIsOpponent
        {
            get => _attackerIsOpponent;
            set { if (Set(ref _attackerIsOpponent, value)) { bool was = IsCellPlaying; SetupCellPreview(); if (was) ToggleCellPlay(); } }
        }

        public List<string> WeatherNames { get; } = new List<string> { "None", "Rain", "Sandstorm", "Sun", "Hail" };   // after None, the order JumpIfWeather checks
        private int _weatherIndex;
        public int WeatherIndex
        {
            get => _weatherIndex;
            set { if (Set(ref _weatherIndex, Math.Max(0, value))) { bool was = IsCellPlaying; SetupCellPreview(); if (was) ToggleCellPlay(); } }
        }

        public bool HasWeatherCheck => UsesCommand("JumpIfWeather");

        public bool HasContestCheck => GameFamily != GameFamilies.HGSS   // only DP and Platinum have contests
            && (UsesCommand("JumpIfContest") || UsesCommand("JumpIfContestCondition"));

        private bool _contest;
        public bool Contest
        {
            get => _contest;
            set { if (Set(ref _contest, value)) { bool was = IsCellPlaying; SetupCellPreview(); if (was) ToggleCellPlay(); } }
        }

        private bool _soundsOn = true;
        public bool SoundsOn
        {
            get => _soundsOn;
            set
            {
                if (!Set(ref _soundsOn, value)) return;
                if (!value) StopPreviewSounds();
                SetSoundsOn(_animPlayer, Speeds[_speedIndex] == 1);
            }
        }

        private int _volume = 100;
        public int Volume
        {
            get => _volume;
            set => Set(ref _volume, Math.Clamp(value, 0, 100));
        }
    }
}
