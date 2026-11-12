using Avalonia.Controls;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.Editors.Utils;
using DSPRE.HgEngine;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using NarcAPI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AvaBitmap = Avalonia.Media.Imaging.Bitmap;
using static DSPRE.RomInfo;
using FormSpriteData = DSPRE.Graphics.AlternateFormSprites.Form;
using Avalonia.Media;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>The Pokémon editor's Sprites tab: four battle sprites, both palettes, forms.</summary>
    public partial class PokemonSpriteEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            if (n == nameof(SelectedVariantIndex) && _followerFollowsForms)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FollowerSpriteEntry)));
        }
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Sprites of {RomInfo.PokemonName(_currentId)}";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _pending = new(); _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private sealed record SpriteState(byte[][] Raw, uint[] Normal, uint[] Shiny, bool[] NormalUsed, bool[] ShinyUsed, byte[] Pending);

        // The opposite gender's copied sprites and heights wait here until Save. Undo states carry them.
        private PendingArchiveWrites _pending = new();
        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private byte[] TakeState() => DSPRE.Avalonia.UndoJson.Take(new SpriteState(_rawSprites, _normalPal, _shinyPal, _normalPalUsed, _shinyPalUsed, _pending.Take()));

        private void ApplyState(byte[] state)
        {
            SpriteState s = DSPRE.Avalonia.UndoJson.Read<SpriteState>(state);
            _rawSprites = s.Raw ?? new byte[4][];
            _normalPal = s.Normal;
            _shinyPal = s.Shiny;
            _normalPalUsed = s.NormalUsed ?? AllUsed();
            _shinyPalUsed = s.ShinyUsed ?? AllUsed();
            if (s.Pending != null) _pending.Restore(s.Pending);
            ShowGenderGap();
            ApplyPalettesAndPublish();
            _dirty = _undo.IsDirty;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void ResetUndo() { _undo = new ByteStateUndo(TakeState, ApplyState, RaiseUndo); RaiseUndo(); }

        private void Edited(object group = null)
        {
            _undo?.Record(group);
            _dirty = _undo?.IsDirty ?? true;
        }

        private AvaBitmap _femaleBackNormal;  public AvaBitmap FemaleBackNormal  { get => _femaleBackNormal;  private set => Set(ref _femaleBackNormal,  value); }
        private AvaBitmap _maleBackNormal;    public AvaBitmap MaleBackNormal    { get => _maleBackNormal;    private set => Set(ref _maleBackNormal,    value); }
        private AvaBitmap _femaleFrontNormal; public AvaBitmap FemaleFrontNormal { get => _femaleFrontNormal; private set => Set(ref _femaleFrontNormal, value); }
        private AvaBitmap _maleFrontNormal;   public AvaBitmap MaleFrontNormal   { get => _maleFrontNormal;   private set => Set(ref _maleFrontNormal,   value); }

        // N 80×80 frames side by side, usually 2. Slots: 0 F-back, 1 M-back, 2 F-front, 3 M-front.
        private int _battleFrameCount = 2;
        public int BattleFrameCount { get => _battleFrameCount; private set => Set(ref _battleFrameCount, value); }
        private System.Collections.Generic.IReadOnlyList<AvaBitmap> _bFrontM, _bFrontF, _bBackM, _bBackF;
        public System.Collections.Generic.IReadOnlyList<AvaBitmap> BattleFrontM { get => _bFrontM; private set => Set(ref _bFrontM, value); }
        public System.Collections.Generic.IReadOnlyList<AvaBitmap> BattleFrontF { get => _bFrontF; private set => Set(ref _bFrontF, value); }
        public System.Collections.Generic.IReadOnlyList<AvaBitmap> BattleBackM  { get => _bBackM;  private set => Set(ref _bBackM, value); }
        public System.Collections.Generic.IReadOnlyList<AvaBitmap> BattleBackF  { get => _bBackF;  private set => Set(ref _bBackF, value); }
        // The same frames in the shiny palette, for a shiny send-out.
        private System.Collections.Generic.IReadOnlyList<AvaBitmap> _bFrontMS, _bFrontFS, _bBackMS, _bBackFS;
        public System.Collections.Generic.IReadOnlyList<AvaBitmap> BattleFrontMShiny { get => _bFrontMS; private set => Set(ref _bFrontMS, value); }
        public System.Collections.Generic.IReadOnlyList<AvaBitmap> BattleFrontFShiny { get => _bFrontFS; private set => Set(ref _bFrontFS, value); }
        public System.Collections.Generic.IReadOnlyList<AvaBitmap> BattleBackMShiny  { get => _bBackMS;  private set => Set(ref _bBackMS, value); }
        public System.Collections.Generic.IReadOnlyList<AvaBitmap> BattleBackFShiny  { get => _bBackFS;  private set => Set(ref _bBackFS, value); }
        private AvaBitmap _bBackF0;  public AvaBitmap BattleBackF0  { get => _bBackF0;  private set => Set(ref _bBackF0, value); }
        private AvaBitmap _bBackF1;  public AvaBitmap BattleBackF1  { get => _bBackF1;  private set => Set(ref _bBackF1, value); }

        private AvaBitmap _femaleBackShiny;   public AvaBitmap FemaleBackShiny   { get => _femaleBackShiny;   private set => Set(ref _femaleBackShiny,   value); }
        private AvaBitmap _maleBackShiny;     public AvaBitmap MaleBackShiny     { get => _maleBackShiny;     private set => Set(ref _maleBackShiny,     value); }
        private AvaBitmap _femaleFrontShiny;  public AvaBitmap FemaleFrontShiny  { get => _femaleFrontShiny;  private set => Set(ref _femaleFrontShiny,  value); }
        private AvaBitmap _maleFrontShiny;    public AvaBitmap MaleFrontShiny    { get => _maleFrontShiny;    private set => Set(ref _maleFrontShiny,    value); }

        // One swatch per palette slot. Normal and Shiny each have one palette for all four poses.
        public class PaletteSwatch
        {
            public global::Avalonia.Media.IBrush Brush { get; set; }
            // Never given a color: an import with under 16 colors leaves black padding here.
            public bool IsPlaceholder { get; set; }
            public int Index { get; set; }
            public bool Shiny { get; set; }
        }
        public ObservableCollection<PaletteSwatch> NormalSwatches { get; } = new();
        public ObservableCollection<PaletteSwatch> ShinySwatches { get; } = new();

        // Which palette slots hold a real color. ROM palettes are all real; only imports leave gaps.
        private bool[] _normalPalUsed = AllUsed();
        private bool[] _shinyPalUsed = AllUsed();
        private static bool[] AllUsed() { bool[] a = new bool[16]; Array.Fill(a, true); return a; }

        // Each cell has its own frame. Animate drives them all from one timer.
        public class FrameCellState : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            private int _frame;
            public Action<int> OnChanged;
            public int Frame
            {
                get => _frame;
                set
                {
                    int v = ((value % 2) + 2) % 2;
                    if (_frame == v) return;
                    _frame = v;
                    Raise(nameof(Frame)); Raise(nameof(IsFrame1)); Raise(nameof(IsFrame2));
                    OnChanged?.Invoke(_frame);
                }
            }
            public bool IsFrame1 => Frame == 0;
            public bool IsFrame2 => Frame == 1;

            private bool _hasFrame1 = true, _hasFrame2 = true;
            public bool HasFrame1 => _hasFrame1;
            public bool HasFrame2 => _hasFrame2;
            public bool ShowFrameToggle => _hasFrame1 && _hasFrame2;

            // Locks Frame onto the real one when only one exists.
            public void SetFrameAvailability(bool hasFrame1, bool hasFrame2)
            {
                if (_hasFrame1 == hasFrame1 && _hasFrame2 == hasFrame2) return;
                _hasFrame1 = hasFrame1; _hasFrame2 = hasFrame2;
                Raise(nameof(ShowFrameToggle));
                if (!hasFrame1 && hasFrame2) Frame = 1;
                else if (hasFrame1 && !hasFrame2) Frame = 0;
            }
        }

        public FrameCellState FemaleBackNormalFrame  { get; } = new();
        public FrameCellState MaleBackNormalFrame    { get; } = new();
        public FrameCellState FemaleFrontNormalFrame { get; } = new();
        public FrameCellState MaleFrontNormalFrame   { get; } = new();
        public FrameCellState FemaleBackShinyFrame   { get; } = new();
        public FrameCellState MaleBackShinyFrame     { get; } = new();
        public FrameCellState FemaleFrontShinyFrame  { get; } = new();
        public FrameCellState MaleFrontShinyFrame    { get; } = new();

        private FrameCellState[] AllFrameCells => new[]
        {
            FemaleBackNormalFrame, MaleBackNormalFrame, FemaleFrontNormalFrame, MaleFrontNormalFrame,
            FemaleBackShinyFrame, MaleBackShinyFrame, FemaleFrontShinyFrame, MaleFrontShinyFrame
        };

        private bool _animateFrames = true;
        public bool AnimateFrames
        {
            get => _animateFrames;
            set { if (Set(ref _animateFrames, value)) OnPropertyChanged(nameof(AnimateButtonText)); }
        }
        public string AnimateButtonText => AnimateFrames ? "Stop" : "Animate";
        private global::Avalonia.Threading.DispatcherTimer _frameTimer;

        public void StopFrameAnimation() => _frameTimer?.Stop();

        private string _statusText = "";
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        // Single-gender species keep stubs for the other gender, which show garbage once the ratio widens;
        // this copies the existing gender's sprites in.
        private bool _canAddOppositeGenderSprites;
        public bool CanAddOppositeGenderSprites { get => _canAddOppositeGenderSprites; private set => Set(ref _canAddOppositeGenderSprites, value); }

        private string _addOppositeGenderLabel = "";
        public string AddOppositeGenderLabel { get => _addOppositeGenderLabel; private set => Set(ref _addOppositeGenderLabel, value); }

        private bool _missingGenderIsFemale;
        public string ExistingGenderName => _missingGenderIsFemale ? "Male" : "Female";

        private int _currentId = -1;
        // Pixel data per slot 0-3 (FemBack, MBack, FFront, MFront), one color index (0-15) per pixel,
        // 160×80. Import writes straight into this array, so it's always what Save() writes out.
        private byte[][] _rawSprites = new byte[4][];
        private uint[] _normalPal;   // 16 colors as packed ARGB (0xAARRGGBB); imports can bring alpha 0 entries
        private uint[] _shinyPal;
        private bool _loadedFromHgEngine;   // this species loaded from hg-engine source, not merely a linked checkout
        public bool IsHgEngineSourced => _loadedFromHgEngine;
        public bool ShowShinyFullSheetImport => CanUseFullSheet && !IsHgEngineSourced;

        public bool ShowFemaleFormImport => !IsAlternateForms;   // forms store one shared back and front sprite, so Male is the one that saves
        public bool ShowFemaleShinyFormImport => !IsHgEngineSourced && !IsAlternateForms;

        private const int SpriteWidth = 160;
        private const int SpriteHeight = 80;

        private static readonly string[] SpriteLabels = { "Female back", "Male back", "Female front", "Male front" };

        public PokemonSpriteEditorViewModel()
        {
            if (!Design.IsDesignMode) return;
            StatusText = "Design preview (no sprites loaded)";
        }

        public PokemonSpriteEditorViewModel(bool _)
        {
            FemaleBackNormalFrame.OnChanged  = f => FemaleBackNormal  = RenderBattleSprite(0, _normalPal, f);
            MaleBackNormalFrame.OnChanged    = f => MaleBackNormal    = RenderBattleSprite(1, _normalPal, f);
            FemaleFrontNormalFrame.OnChanged = f => FemaleFrontNormal = RenderBattleSprite(2, _normalPal, f);
            MaleFrontNormalFrame.OnChanged   = f => MaleFrontNormal   = RenderBattleSprite(3, _normalPal, f);
            FemaleBackShinyFrame.OnChanged   = f => FemaleBackShiny   = RenderBattleSprite(0, _shinyPal, f);
            MaleBackShinyFrame.OnChanged     = f => MaleBackShiny     = RenderBattleSprite(1, _shinyPal, f);
            FemaleFrontShinyFrame.OnChanged  = f => FemaleFrontShiny  = RenderBattleSprite(2, _shinyPal, f);
            MaleFrontShinyFrame.OnChanged    = f => MaleFrontShiny    = RenderBattleSprite(3, _shinyPal, f);

            _frameTimer = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _frameTimer.Tick += (_, __) =>
            {
                if (!AnimateFrames) return;
                foreach (FrameCellState cell in AllFrameCells) if (cell.ShowFrameToggle) cell.Frame = 1 - cell.Frame;
            };
            _frameTimer.Start();
        }

        /// <summary>HeartGold and SoulSilver: the walking Pokémon's models, beside the sprites.</summary>
        public bool ShowFollower => GameFamily == GameFamilies.HGSS && !HgEngineProject.IsActive;

        private (int index, string label)[] _followerModels = Array.Empty<(int, string)>();
        public ObservableCollection<string> FollowerModelNames { get; } = new();
        public bool HasFollowerModels => _followerModels.Length > 0;
        public bool HasFollowerModelChoice => _followerModels.Length > 1 && !_followerFollowsForms;

        // One follower model per battle form, in the same order: the follower shows the form picked above.
        private bool _followerFollowsForms;

        private string _followerNote = "";
        public string FollowerNote
        {
            get => _followerNote;
            private set { if (Set(ref _followerNote, value)) OnPropertyChanged(nameof(HasFollowerNote)); }
        }
        public bool HasFollowerNote => _followerNote.Length > 0;

        private int _followerModelIndex;
        public int FollowerModelIndex
        {
            get => _followerModelIndex;
            set { if (value >= 0 && value < _followerModels.Length && Set(ref _followerModelIndex, value)) OnPropertyChanged(nameof(FollowerSpriteEntry)); }
        }

        /// <summary>The overworld table entry of the chosen model, or -1.</summary>
        public int FollowerSpriteEntry
        {
            get
            {
                int model = _followerFollowsForms ? Math.Max(0, _selectedVariantIndex) : _followerModelIndex;
                return model < _followerModels.Length ? HgssFollowers.FirstSprite + _followerModels[model].index : -1;
            }
        }

        private void LoadFollower(int species)
        {
            _followerModels = Array.Empty<(int, string)>();
            _followerModelIndex = 0;
            FollowerModelNames.Clear();
            string note = "";
            if (ShowFollower)
            {
                try
                {
                    note = HgssFollowers.WhyNot();
                    if (note == null)
                    {
                        DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.OWSprites });
                        _followerModels = HgssFollowers.ModelsOf(species).ToArray();
                        note = _followerModels.Length == 0 ? "This Pokémon has no follower." : "";
                    }
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { note = e.Message; }
                foreach ((int _, string label) in _followerModels) FollowerModelNames.Add(label);
            }
            FollowerNote = note ?? "";
            _followerFollowsForms = _followerModels.Length > 1 && _currentFormData != null && _currentFormData.Length == _followerModels.Length
                                    && _followerModels.All(m => m.label != HgssFollowers.FemaleLabel);
            foreach (string name in new[] { nameof(HasFollowerModels), nameof(HasFollowerModelChoice), nameof(FollowerModelIndex), nameof(FollowerSpriteEntry) })
                OnPropertyChanged(name);
        }
    }
}
