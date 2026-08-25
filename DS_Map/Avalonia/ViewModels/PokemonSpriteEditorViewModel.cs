using Avalonia.Controls;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.Editors.Utils;
using NarcAPI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AvaBitmap = Avalonia.Media.Imaging.Bitmap;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Avalonia ViewModel for the Sprites tab in the unified Pokémon editor.
    /// Loads the 4 battle sprites (FemaleBack, MaleBack, FemaleFront, MaleFront)
    /// from the pokemonBattleSprites NARC and displays them with normal / shiny palettes.
    /// Supports alternate forms via the otherPokemonBattleSprites NARC.
    /// </summary>
    public class PokemonSpriteEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        // --- IEditorWithUnsavedChanges -----------------------------------------------
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Sprites (Mon {_currentId})";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // --- Sprite bitmaps (Avalonia bitmaps for the View) --------------------------
        private AvaBitmap _femaleBackNormal;  public AvaBitmap FemaleBackNormal  { get => _femaleBackNormal;  private set => Set(ref _femaleBackNormal,  value); }
        private AvaBitmap _maleBackNormal;    public AvaBitmap MaleBackNormal    { get => _maleBackNormal;    private set => Set(ref _maleBackNormal,    value); }
        private AvaBitmap _femaleFrontNormal; public AvaBitmap FemaleFrontNormal { get => _femaleFrontNormal; private set => Set(ref _femaleFrontNormal, value); }
        private AvaBitmap _maleFrontNormal;   public AvaBitmap MaleFrontNormal   { get => _maleFrontNormal;   private set => Set(ref _maleFrontNormal,   value); }

        // Battle-mock sprites: the sheet is N×80 = N 80×80 frames (N = width/80; usually 2 for the HGSS send-out
        // animation, but future hacks may widen it). Exposed per gender (M/F) as a frame LIST, native size with
        // palette index 0 transparent. The Battle Display tab picks the gender + frame. (Slots: 0 F-back, 1 M-back,
        // 2 F-front, 3 M-front.)
        private int _battleFrameCount = 2;
        public int BattleFrameCount { get => _battleFrameCount; private set => Set(ref _battleFrameCount, value); }
        private System.Collections.Generic.IReadOnlyList<AvaBitmap> _bFrontM, _bFrontF, _bBackM, _bBackF;
        public System.Collections.Generic.IReadOnlyList<AvaBitmap> BattleFrontM { get => _bFrontM; private set => Set(ref _bFrontM, value); }
        public System.Collections.Generic.IReadOnlyList<AvaBitmap> BattleFrontF { get => _bFrontF; private set => Set(ref _bFrontF, value); }
        public System.Collections.Generic.IReadOnlyList<AvaBitmap> BattleBackM  { get => _bBackM;  private set => Set(ref _bBackM, value); }
        public System.Collections.Generic.IReadOnlyList<AvaBitmap> BattleBackF  { get => _bBackF;  private set => Set(ref _bBackF, value); }
        private AvaBitmap _bBackF0;  public AvaBitmap BattleBackF0  { get => _bBackF0;  private set => Set(ref _bBackF0, value); }
        private AvaBitmap _bBackF1;  public AvaBitmap BattleBackF1  { get => _bBackF1;  private set => Set(ref _bBackF1, value); }

        private AvaBitmap _femaleBackShiny;   public AvaBitmap FemaleBackShiny   { get => _femaleBackShiny;   private set => Set(ref _femaleBackShiny,   value); }
        private AvaBitmap _maleBackShiny;     public AvaBitmap MaleBackShiny     { get => _maleBackShiny;     private set => Set(ref _maleBackShiny,     value); }
        private AvaBitmap _femaleFrontShiny;  public AvaBitmap FemaleFrontShiny  { get => _femaleFrontShiny;  private set => Set(ref _femaleFrontShiny,  value); }
        private AvaBitmap _maleFrontShiny;    public AvaBitmap MaleFrontShiny    { get => _maleFrontShiny;    private set => Set(ref _maleFrontShiny,    value); }

        // 16-swatch strips for the paired palette rail: one Normal palette and one Shiny palette,
        // each shared by all four poses (the ROM stores exactly one of each per species/form).
        public class PaletteSwatch
        {
            public global::Avalonia.Media.IBrush Brush { get; set; }
            // True when this slot has never actually been given a color (import produced fewer than
            // 16 colors, so it's black padding, not a real entry the sprite uses).
            public bool IsPlaceholder { get; set; }
        }
        public ObservableCollection<PaletteSwatch> NormalSwatches { get; } = new();
        public ObservableCollection<PaletteSwatch> ShinySwatches { get; } = new();

        // Which of the 16 palette slots are backed by a real color, for Normal/Shiny respectively.
        // ROM-loaded palettes are always all-real; only import can leave some slots as placeholders.
        private bool[] _normalPalUsed = AllUsed();
        private bool[] _shinyPalUsed = AllUsed();
        private static bool[] AllUsed() { var a = new bool[16]; Array.Fill(a, true); return a; }

        // --- Frame animation: shows one 80×80 half of the sprite at a time instead of the full strip.
        // Each cell picks its own frame independently; Animate just drives them all off one timer,
        // and stopping it leaves whatever each cell was last showing. ------------------------------
        public class FrameCellState : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Frame)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFrame1)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFrame2)));
                    OnChanged?.Invoke(_frame);
                }
            }
            public bool IsFrame1 => Frame == 0;
            public bool IsFrame2 => Frame == 1;
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
        public string AnimateButtonText => AnimateFrames ? "Stop Animating" : "Animate Frames";
        private global::Avalonia.Threading.DispatcherTimer _frameTimer;

        public void StopFrameAnimation() => _frameTimer?.Stop();

        private string _statusText = "";
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        // --- Mono-gender / genderless sprite gap ---------------------------------------
        // A species that can only be one gender (or is genderless) has its "other" gender's back+front
        // slots stored as placeholder .bin stubs instead of real RGCN sprites, since the game never
        // needs them. Widening that species' gender ratio later (Personal Data editor) leaves those
        // slots empty and the game shows garbage or crashes. This offers a one-click fix: clone the
        // existing gender's sprites into the missing slots, editable afterward like any other sprite.
        private bool _canAddOppositeGenderSprites;
        public bool CanAddOppositeGenderSprites { get => _canAddOppositeGenderSprites; private set => Set(ref _canAddOppositeGenderSprites, value); }

        private string _addOppositeGenderLabel = "";
        public string AddOppositeGenderLabel { get => _addOppositeGenderLabel; private set => Set(ref _addOppositeGenderLabel, value); }

        private bool _missingGenderIsFemale;
        /// <summary>The gender that DOES have sprites when CanAddOppositeGenderSprites is true; meaningless otherwise.</summary>
        public string ExistingGenderName => _missingGenderIsFemale ? "Male" : "Female";

        // --- Internal state ----------------------------------------------------------
        private int _currentId = -1;
        // Pixel data per slot 0-3 (FemBack, MBack, FFront, MFront), one color index (0-15) per pixel,
        // 160×80. Import writes straight into this array, so it's always what Save() writes out.
        private byte[][] _rawSprites = new byte[4][];
        // 16 colors as packed ARGB (0xAARRGGBB), always opaque (alpha byte FF)
        private uint[] _normalPal;
        private uint[] _shinyPal;

        private const int SpriteWidth = 160;
        private const int SpriteHeight = 80;

        private static readonly string[] SpriteLabels = { "Female Back", "Male Back", "Female Front", "Male Front" };

        // --- Alternate forms support -------------------------------------------------

        /// <summary>
        /// Represents sprite template data for a Pokémon alternate form.
        /// Mirrors PokemonSpriteEditor.FormSpriteData.
        /// </summary>
        private struct FormSpriteData
        {
            public string Name;
            public int BackSpriteIndex;
            public int FrontSpriteIndex;
            public int NormalPaletteIndex;
            public int ShinyPaletteIndex;
            public bool HasGenderDifference;

            public FormSpriteData(string name, int backIdx, int frontIdx, int normalPal, int shinyPal, bool genderDiff = false)
            {
                Name = name;
                BackSpriteIndex = backIdx;
                FrontSpriteIndex = frontIdx;
                NormalPaletteIndex = normalPal;
                ShinyPaletteIndex = shinyPal;
                HasGenderDifference = genderDiff;
            }
        }

        private bool _isAlternateForms = false;
        public bool IsAlternateForms { get => _isAlternateForms; private set => Set(ref _isAlternateForms, value); }

        public ObservableCollection<string> AlternateFormNames { get; } = new();

        private int _selectedFormIndex = 0;
        public int SelectedFormIndex
        {
            get => _selectedFormIndex;
            set
            {
                if (!Set(ref _selectedFormIndex, value)) return;
                if (_isAlternateForms && value >= 0 && _currentFormData != null && value < _currentFormData.Length)
                    LoadAlternateForm(value);
            }
        }

        public string ToggleButtonText => _isAlternateForms ? "← Main Sprites" : "Alternate Forms →";

        private bool _hasAlternateForms;
        /// <summary>True when the current species has its own entries in the alternate-forms table (Deoxys, Unown, etc.), so the toggle only shows up when it's actually useful.</summary>
        public bool HasAlternateForms { get => _hasAlternateForms; private set => Set(ref _hasAlternateForms, value); }

        private FormSpriteData[] _currentFormData;

        /// <summary>Form table names look like "DEOXYS - Attack"; splitting on the dash is the only way to tell which species an entry belongs to, since there's no id field for it.</summary>
        private static string SpeciesNamePrefix(string formName)
        {
            int dash = formName.IndexOf(" - ", StringComparison.Ordinal);
            return dash < 0 ? null : formName.Substring(0, dash);
        }

        private FormSpriteData[] GetAlternateFormsForCurrentSpecies()
        {
            if (_currentId <= 0) return Array.Empty<FormSpriteData>();
            string[] names = RomInfo.GetPokemonNames();
            if (_currentId >= names.Length) return Array.Empty<FormSpriteData>();
            string mySpecies = names[_currentId];

            var matches = new System.Collections.Generic.List<FormSpriteData>();
            foreach (var f in GetFormDataForCurrentGame())
            {
                string prefix = SpeciesNamePrefix(f.Name);
                if (prefix != null && string.Equals(prefix, mySpecies, StringComparison.OrdinalIgnoreCase))
                    matches.Add(f);
            }
            return matches.ToArray();
        }

        /// <summary>
        /// Switches between the main sprite NARC and the alternate-forms NARC, scoped to whichever
        /// entries belong to the currently-loaded species.
        /// </summary>
        public void ToggleAlternateFormsMode()
        {
            _isAlternateForms = !_isAlternateForms;
            OnPropertyChanged(nameof(IsAlternateForms));
            OnPropertyChanged(nameof(ToggleButtonText));

            if (_isAlternateForms)
            {
                _currentFormData = GetAlternateFormsForCurrentSpecies();
                AlternateFormNames.Clear();
                for (int i = 0; i < _currentFormData.Length; i++)
                    AlternateFormNames.Add($"{i:D3} {_currentFormData[i].Name}");

                // ComboBox selection doesn't reliably pick up SelectedIndex=0 set in the same tick as
                // repopulating ItemsSource, so force a real 0->0 transition via -1 on the next UI tick.
                _selectedFormIndex = -1;
                OnPropertyChanged(nameof(SelectedFormIndex));
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => SelectedFormIndex = 0);
            }
            else
            {
                // Return to main sprites
                LoadMon(_currentId);
            }
        }

        private void LoadAlternateForm(int formIndex)
        {
            ClearBitmaps();
            StatusText = "";

            if (_currentFormData == null || formIndex < 0 || formIndex >= _currentFormData.Length)
            {
                StatusText = "Invalid form index.";
                return;
            }

            try
            {
                string packedPath = RomInfo.gameDirs[DirNames.otherPokemonBattleSprites].packedDir;
                if (!File.Exists(packedPath))
                {
                    StatusText = "Alternate forms NARC not found. Make sure the ROM is loaded.";
                    return;
                }

                var narc = new NarcReader(packedPath);
                var form = _currentFormData[formIndex];
                var rawBmps = new byte[4][];

                // Load back sprite
                if (form.BackSpriteIndex >= 0 && form.BackSpriteIndex < narc.fe.Length
                    && narc.fe[form.BackSpriteIndex].Size == 6448)
                {
                    narc.OpenEntry(form.BackSpriteIndex);
                    var backSprite = MakeImage(narc.fs);
                    narc.Close();
                    rawBmps[0] = backSprite;
                    rawBmps[1] = backSprite; // same for both genders unless HasGenderDifference
                }

                // Load front sprite
                if (form.FrontSpriteIndex >= 0 && form.FrontSpriteIndex < narc.fe.Length
                    && narc.fe[form.FrontSpriteIndex].Size == 6448)
                {
                    narc.OpenEntry(form.FrontSpriteIndex);
                    var frontSprite = MakeImage(narc.fs);
                    narc.Close();
                    rawBmps[2] = frontSprite;
                    rawBmps[3] = frontSprite;
                }

                // Load palettes
                uint[] normalPal = null, shinyPal = null;
                if (form.NormalPaletteIndex >= 0 && form.NormalPaletteIndex < narc.fe.Length
                    && narc.fe[form.NormalPaletteIndex].Size == 72)
                {
                    narc.OpenEntry(form.NormalPaletteIndex);
                    normalPal = ReadPalette(narc.fs);
                    narc.Close();
                }
                if (form.ShinyPaletteIndex >= 0 && form.ShinyPaletteIndex < narc.fe.Length
                    && narc.fe[form.ShinyPaletteIndex].Size == 72)
                {
                    narc.OpenEntry(form.ShinyPaletteIndex);
                    shinyPal = ReadPalette(narc.fs);
                    narc.Close();
                }

                if (normalPal == null)
                {
                    StatusText = "Could not load palette for this alternate form.";
                    return;
                }
                if (shinyPal == null) shinyPal = normalPal;

                _rawSprites = rawBmps;
                _normalPal  = normalPal;
                _shinyPal   = shinyPal;
                _normalPalUsed = AllUsed();
                _shinyPalUsed  = AllUsed();

                ApplyPalettesAndPublish();
                _dirty = false;
                OnPropertyChanged(nameof(HasUnsavedChanges));
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading alternate form: {ex.Message}";
            }
        }

        private FormSpriteData[] GetFormDataForCurrentGame()
        {
            switch (RomInfo.gameFamily)
            {
                case RomInfo.GameFamilies.DP:   return GetFormDataDP();
                case RomInfo.GameFamilies.Plat:  return GetFormDataPt();
                default:                         return GetFormDataHGSS();
            }
        }

        // --- Form data tables (mirrors PokemonSpriteEditor) --------------------------

        private static FormSpriteData[] GetFormDataDP() => new FormSpriteData[]
        {
            new("Deoxys - Normal",   0,  1, 134, 135),
            new("Deoxys - Attack",   2,  3, 134, 135),
            new("Deoxys - Defense",  4,  5, 134, 135),
            new("Deoxys - Speed",    6,  7, 134, 135),
            new("Unown - A",  8,  9, 136, 137), new("Unown - B", 10, 11, 136, 137),
            new("Unown - C", 12, 13, 136, 137), new("Unown - D", 14, 15, 136, 137),
            new("Unown - E", 16, 17, 136, 137), new("Unown - F", 18, 19, 136, 137),
            new("Unown - G", 20, 21, 136, 137), new("Unown - H", 22, 23, 136, 137),
            new("Unown - I", 24, 25, 136, 137), new("Unown - J", 26, 27, 136, 137),
            new("Unown - K", 28, 29, 136, 137), new("Unown - L", 30, 31, 136, 137),
            new("Unown - M", 32, 33, 136, 137), new("Unown - N", 34, 35, 136, 137),
            new("Unown - O", 36, 37, 136, 137), new("Unown - P", 38, 39, 136, 137),
            new("Unown - Q", 40, 41, 136, 137), new("Unown - R", 42, 43, 136, 137),
            new("Unown - S", 44, 45, 136, 137), new("Unown - T", 46, 47, 136, 137),
            new("Unown - U", 48, 49, 136, 137), new("Unown - V", 50, 51, 136, 137),
            new("Unown - W", 52, 53, 136, 137), new("Unown - X", 54, 55, 136, 137),
            new("Unown - Y", 56, 57, 136, 137), new("Unown - Z", 58, 59, 136, 137),
            new("Unown - !", 60, 61, 136, 137), new("Unown - ?", 62, 63, 136, 137),
            new("Castform - Normal", 64, 68, 138, 142),
            new("Castform - Sunny",  65, 69, 139, 143),
            new("Castform - Rainy",  66, 70, 140, 144),
            new("Castform - Snowy",  67, 71, 141, 145),
            new("Burmy - Plant", 72, 73, 146, 147),
            new("Burmy - Sandy", 74, 75, 148, 149),
            new("Burmy - Trash", 76, 77, 150, 151),
            new("Wormadam - Plant", 78, 79, 152, 153),
            new("Wormadam - Sandy", 80, 81, 154, 155),
            new("Wormadam - Trash", 82, 83, 156, 157),
            new("Shellos - West",   84, 86, 158, 159, true),
            new("Shellos - East",   85, 87, 160, 161, true),
            new("Gastrodon - West", 88, 90, 162, 163, true),
            new("Gastrodon - East", 89, 91, 164, 165, true),
            new("Cherrim - Overcast",  92, 94, 166, 168, true),
            new("Cherrim - Sunshine",  93, 95, 167, 169, true),
            new("Arceus - Normal",   96,  97, 170, 171),
            new("Arceus - Fighting", 98,  99, 172, 173),
            new("Arceus - Flying",  100, 101, 174, 175),
            new("Arceus - Poison",  102, 103, 176, 177),
            new("Arceus - Ground",  104, 105, 178, 179),
            new("Arceus - Rock",    106, 107, 180, 181),
            new("Arceus - Bug",     108, 109, 182, 183),
            new("Arceus - Ghost",   110, 111, 184, 185),
            new("Arceus - Steel",   112, 113, 186, 187),
            new("Arceus - ???",     114, 115, 188, 189),
            new("Arceus - Fire",    116, 117, 190, 191),
            new("Arceus - Water",   118, 119, 192, 193),
            new("Arceus - Grass",   120, 121, 194, 195),
            new("Arceus - Electric",122, 123, 196, 197),
            new("Arceus - Psychic", 124, 125, 198, 199),
            new("Arceus - Ice",     126, 127, 200, 201),
            new("Arceus - Dragon",  128, 129, 202, 203),
            new("Arceus - Dark",    130, 131, 204, 205),
            new("Egg",         132, 132, 206, 206),
            new("Manaphy Egg", 133, 133, 207, 207),
        };

        private static FormSpriteData[] GetFormDataPt() => new FormSpriteData[]
        {
            new("Deoxys - Normal",   0,  1, 154, 155),
            new("Deoxys - Attack",   2,  3, 154, 155),
            new("Deoxys - Defense",  4,  5, 154, 155),
            new("Deoxys - Speed",    6,  7, 154, 155),
            new("Unown - A",  8,  9, 156, 157), new("Unown - B", 10, 11, 156, 157),
            new("Unown - C", 12, 13, 156, 157), new("Unown - D", 14, 15, 156, 157),
            new("Unown - E", 16, 17, 156, 157), new("Unown - F", 18, 19, 156, 157),
            new("Unown - G", 20, 21, 156, 157), new("Unown - H", 22, 23, 156, 157),
            new("Unown - I", 24, 25, 156, 157), new("Unown - J", 26, 27, 156, 157),
            new("Unown - K", 28, 29, 156, 157), new("Unown - L", 30, 31, 156, 157),
            new("Unown - M", 32, 33, 156, 157), new("Unown - N", 34, 35, 156, 157),
            new("Unown - O", 36, 37, 156, 157), new("Unown - P", 38, 39, 156, 157),
            new("Unown - Q", 40, 41, 156, 157), new("Unown - R", 42, 43, 156, 157),
            new("Unown - S", 44, 45, 156, 157), new("Unown - T", 46, 47, 156, 157),
            new("Unown - U", 48, 49, 156, 157), new("Unown - V", 50, 51, 156, 157),
            new("Unown - W", 52, 53, 156, 157), new("Unown - X", 54, 55, 156, 157),
            new("Unown - Y", 56, 57, 156, 157), new("Unown - Z", 58, 59, 156, 157),
            new("Unown - !", 60, 61, 156, 157), new("Unown - ?", 62, 63, 156, 157),
            new("Castform - Normal", 64, 68, 158, 162),
            new("Castform - Sunny",  65, 69, 159, 163),
            new("Castform - Rainy",  66, 70, 160, 164),
            new("Castform - Snowy",  67, 71, 161, 165),
            new("Burmy - Plant", 72, 73, 166, 167),
            new("Burmy - Sandy", 74, 75, 168, 169),
            new("Burmy - Trash", 76, 77, 170, 171),
            new("Wormadam - Plant", 78, 79, 172, 173),
            new("Wormadam - Sandy", 80, 81, 174, 175),
            new("Wormadam - Trash", 82, 83, 176, 177),
            new("Shellos - West",   84, 86, 178, 179, true),
            new("Shellos - East",   85, 87, 180, 181, true),
            new("Gastrodon - West", 88, 90, 182, 183, true),
            new("Gastrodon - East", 89, 91, 184, 185, true),
            new("Cherrim - Overcast", 92, 94, 186, 188, true),
            new("Cherrim - Sunshine", 93, 95, 187, 189, true),
            new("Arceus - Normal",   96,  97, 190, 191),
            new("Arceus - Fighting", 98,  99, 192, 193),
            new("Arceus - Flying",  100, 101, 194, 195),
            new("Arceus - Poison",  102, 103, 196, 197),
            new("Arceus - Ground",  104, 105, 198, 199),
            new("Arceus - Rock",    106, 107, 200, 201),
            new("Arceus - Bug",     108, 109, 202, 203),
            new("Arceus - Ghost",   110, 111, 204, 205),
            new("Arceus - Steel",   112, 113, 206, 207),
            new("Arceus - ???",     114, 115, 208, 209),
            new("Arceus - Fire",    116, 117, 210, 211),
            new("Arceus - Water",   118, 119, 212, 213),
            new("Arceus - Grass",   120, 121, 214, 215),
            new("Arceus - Electric",122, 123, 216, 217),
            new("Arceus - Psychic", 124, 125, 218, 219),
            new("Arceus - Ice",     126, 127, 220, 221),
            new("Arceus - Dragon",  128, 129, 222, 223),
            new("Arceus - Dark",    130, 131, 224, 225),
            new("Egg",         132, 132, 226, 226),
            new("Manaphy Egg", 133, 133, 227, 227),
            new("Shaymin - Land", 134, 135, 228, 229),
            new("Shaymin - Sky",  136, 137, 230, 231),
            new("Rotom - Normal", 138, 139, 232, 233),
            new("Rotom - Heat",   140, 141, 234, 235),
            new("Rotom - Wash",   142, 143, 236, 237),
            new("Rotom - Frost",  144, 145, 238, 239),
            new("Rotom - Fan",    146, 147, 240, 241),
            new("Rotom - Mow",    148, 149, 242, 243),
            new("Giratina - Altered", 150, 151, 244, 245),
            new("Giratina - Origin",  152, 153, 246, 247),
        };

        private static FormSpriteData[] GetFormDataHGSS() => new FormSpriteData[]
        {
            new("Deoxys - Normal",   0,  1, 158, 159),
            new("Deoxys - Attack",   2,  3, 158, 159),
            new("Deoxys - Defense",  4,  5, 158, 159),
            new("Deoxys - Speed",    6,  7, 158, 159),
            new("Unown - A",  8,  9, 160, 161), new("Unown - B", 10, 11, 160, 161),
            new("Unown - C", 12, 13, 160, 161), new("Unown - D", 14, 15, 160, 161),
            new("Unown - E", 16, 17, 160, 161), new("Unown - F", 18, 19, 160, 161),
            new("Unown - G", 20, 21, 160, 161), new("Unown - H", 22, 23, 160, 161),
            new("Unown - I", 24, 25, 160, 161), new("Unown - J", 26, 27, 160, 161),
            new("Unown - K", 28, 29, 160, 161), new("Unown - L", 30, 31, 160, 161),
            new("Unown - M", 32, 33, 160, 161), new("Unown - N", 34, 35, 160, 161),
            new("Unown - O", 36, 37, 160, 161), new("Unown - P", 38, 39, 160, 161),
            new("Unown - Q", 40, 41, 160, 161), new("Unown - R", 42, 43, 160, 161),
            new("Unown - S", 44, 45, 160, 161), new("Unown - T", 46, 47, 160, 161),
            new("Unown - U", 48, 49, 160, 161), new("Unown - V", 50, 51, 160, 161),
            new("Unown - W", 52, 53, 160, 161), new("Unown - X", 54, 55, 160, 161),
            new("Unown - Y", 56, 57, 160, 161), new("Unown - Z", 58, 59, 160, 161),
            new("Unown - !", 60, 61, 160, 161), new("Unown - ?", 62, 63, 160, 161),
            new("Castform - Normal", 64, 68, 162, 166),
            new("Castform - Sunny",  65, 69, 163, 167),
            new("Castform - Rainy",  66, 70, 164, 168),
            new("Castform - Snowy",  67, 71, 165, 169),
            new("Burmy - Plant", 72, 73, 170, 171),
            new("Burmy - Sandy", 74, 75, 172, 173),
            new("Burmy - Trash", 76, 77, 174, 175),
            new("Wormadam - Plant", 78, 79, 176, 177),
            new("Wormadam - Sandy", 80, 81, 178, 179),
            new("Wormadam - Trash", 82, 83, 180, 181),
            new("Shellos - West",   84, 86, 182, 183, true),
            new("Shellos - East",   85, 87, 184, 185, true),
            new("Gastrodon - West", 88, 90, 186, 187, true),
            new("Gastrodon - East", 89, 91, 188, 189, true),
            new("Cherrim - Overcast", 92, 94, 190, 192, true),
            new("Cherrim - Sunshine", 93, 95, 191, 193, true),
            new("Arceus - Normal",   96,  97, 194, 195),
            new("Arceus - Fighting", 98,  99, 196, 197),
            new("Arceus - Flying",  100, 101, 198, 199),
            new("Arceus - Poison",  102, 103, 200, 201),
            new("Arceus - Ground",  104, 105, 202, 203),
            new("Arceus - Rock",    106, 107, 204, 205),
            new("Arceus - Bug",     108, 109, 206, 207),
            new("Arceus - Ghost",   110, 111, 208, 209),
            new("Arceus - Steel",   112, 113, 210, 211),
            new("Arceus - ???",     114, 115, 212, 213),
            new("Arceus - Fire",    116, 117, 214, 215),
            new("Arceus - Water",   118, 119, 216, 217),
            new("Arceus - Grass",   120, 121, 218, 219),
            new("Arceus - Electric",122, 123, 220, 221),
            new("Arceus - Psychic", 124, 125, 222, 223),
            new("Arceus - Ice",     126, 127, 224, 225),
            new("Arceus - Dragon",  128, 129, 226, 227),
            new("Arceus - Dark",    130, 131, 228, 229),
            new("Egg",         132, 132, 230, 230),
            new("Manaphy Egg", 133, 133, 231, 231),
            new("Shaymin - Land", 134, 135, 232, 233),
            new("Shaymin - Sky",  136, 137, 234, 235),
            new("Rotom - Normal", 138, 139, 236, 237),
            new("Rotom - Heat",   140, 141, 238, 239),
            new("Rotom - Wash",   142, 143, 240, 241),
            new("Rotom - Frost",  144, 145, 242, 243),
            new("Rotom - Fan",    146, 147, 244, 245),
            new("Rotom - Mow",    148, 149, 246, 247),
            new("Giratina - Altered", 150, 151, 248, 249),
            new("Giratina - Origin",  152, 153, 250, 251),
            new("Pichu - Normal",    154, 155, 252, 253),
            new("Pichu - Spiky-ear", 156, 157, 254, 255),
        };

        // --- Design-time constructor -------------------------------------------------
        public PokemonSpriteEditorViewModel()
        {
            if (!Design.IsDesignMode) return;
            StatusText = "Design preview (no sprites loaded)";
        }

        // --- Runtime constructor -----------------------------------------------------
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
                foreach (var cell in AllFrameCells) cell.Frame = 1 - cell.Frame;
            };
            _frameTimer.Start();
        }

        // --- Load --------------------------------------------------------------------
        public void LoadMon(int id)
        {
            _currentId = id;
            ClearBitmaps();
            StatusText = "";
            HasAlternateForms = id > 0 && GetAlternateFormsForCurrentSpecies().Length > 0;

            if (id <= 0)
            {
                StatusText = "No Pokémon selected.";
                return;
            }

            try
            {
                string packedPath = RomInfo.gameDirs[DirNames.pokemonBattleSprites].packedDir;
                if (!File.Exists(packedPath))
                {
                    StatusText = "Battle sprites NARC not found. Make sure the ROM is loaded.";
                    return;
                }

                var narc = new NarcReader(packedPath);
                int baseOffset = id * 6;

                // Load 4 sprites
                var rawBmps = new byte[4][];
                var hasRealSprite = new bool[4];
                for (int i = 0; i < 4; i++)
                {
                    int idx = baseOffset + i;
                    hasRealSprite[i] = idx < narc.fe.Length && narc.fe[idx].Size == 6448;
                    if (hasRealSprite[i])
                    {
                        narc.OpenEntry(idx);
                        rawBmps[i] = MakeImage(narc.fs);
                        narc.Close();
                    }
                }
                UpdateOppositeGenderGap(hasRealSprite);

                // Load palettes
                uint[] normalPal = null, shinyPal = null;
                int palIdx = baseOffset + 4;
                int shinyIdx = baseOffset + 5;
                if (palIdx < narc.fe.Length && narc.fe[palIdx].Size == 72)
                {
                    narc.OpenEntry(palIdx);
                    normalPal = ReadPalette(narc.fs);
                    narc.Close();
                }
                if (shinyIdx < narc.fe.Length && narc.fe[shinyIdx].Size == 72)
                {
                    narc.OpenEntry(shinyIdx);
                    shinyPal = ReadPalette(narc.fs);
                    narc.Close();
                }

                if (normalPal == null)
                {
                    StatusText = "Could not load palette for this Pokémon.";
                    return;
                }
                if (shinyPal == null) shinyPal = normalPal;

                _rawSprites = rawBmps;
                _normalPal  = normalPal;
                _shinyPal   = shinyPal;
                _normalPalUsed = AllUsed();
                _shinyPalUsed  = AllUsed();

                ApplyPalettesAndPublish();
                _dirty = false;
                OnPropertyChanged(nameof(HasUnsavedChanges));
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading sprites: {ex.Message}";
            }
        }

        // --- Import PNG for one sprite slot -----------------------------------------
        public async Task ImportSprite(int slot, Window owner)
        {
            if (slot < 0 || slot > 3) return;
            string path = await DialogHelper.OpenFile(owner, $"Import PNG for {SpriteLabels[slot]}",
                new[] { DialogHelper.PngFilter, DialogHelper.AllFilter });
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                RawImage imported;
                using (var fs = File.OpenRead(path))
                    imported = ImageConverter.DecodeRawImage(fs);
                if (imported == null)
                {
                    StatusText = "Image could not be decoded.";
                    return;
                }
                if (imported.Width != SpriteWidth || imported.Height != SpriteHeight)
                {
                    StatusText = $"Sprite must be {SpriteWidth}×{SpriteHeight} pixels (got {imported.Width}×{imported.Height}).";
                    return;
                }
                if (!TryReadImageColors(imported, out byte[] newIndices, out uint[] newPalette, out int usedCount))
                {
                    StatusText = "This image has more than 16 colors. Reduce it to 16 or fewer and try again.";
                    return;
                }

                bool[] newUsed = MakeUsedMask(usedCount);

                if (_normalPal != null && !PaletteEqualsUpTo(_normalPal, newPalette, usedCount))
                {
                    bool keepExisting = await DialogHelper.AskYesNo(
                        $"{SpriteLabels[slot]}'s image uses different colors than the palette already saved for this sprite.\n\n" +
                        "Keep the saved palette and match this image's colors to it? Choosing No replaces the saved palette with this image's own colors instead.",
                        "Palette mismatch");
                    if (keepExisting)
                    {
                        newIndices = RemapToExistingPalette(newIndices, newPalette, usedCount, _normalPal, _normalPalUsed, out uint[] merged, out bool[] mergedUsed);
                        _normalPal = merged;
                        _normalPalUsed = mergedUsed;
                    }
                    else
                    {
                        _normalPal = newPalette;
                        _normalPalUsed = newUsed;
                    }
                }
                else if (_normalPal == null)
                {
                    _normalPal = newPalette;
                    _normalPalUsed = newUsed;
                }

                _rawSprites[slot] = newIndices;
                ApplyPalettesAndPublish();
                _dirty = true;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = $"Imported {SpriteLabels[slot]}. Save to write it to the ROM.";
            }
            catch (Exception ex)
            {
                StatusText = $"Import failed: {ex.Message}";
            }
        }

        /// <summary>Gets the shiny palette from a reference image; doesn't touch pixels, since shiny shares artwork with Normal.</summary>
        public Task ImportShinyPalette(int slot, Window owner) => ImportPaletteFromReference(slot, owner, shiny: true);

        /// <summary>Gets the normal palette from a reference image, keeping the pixels already saved for this pose.</summary>
        public Task ImportNormalPalette(int slot, Window owner) => ImportPaletteFromReference(slot, owner, shiny: false);

        private async Task ImportPaletteFromReference(int slot, Window owner, bool shiny)
        {
            if (slot < 0 || slot > 3) return;
            string label = shiny ? "shiny" : "normal";
            if (_rawSprites[slot] == null)
            {
                StatusText = $"Load or import {SpriteLabels[slot]}'s artwork first, then you can get the {label} palette from a reference image.";
                return;
            }
            string path = await DialogHelper.OpenFile(owner, $"Import {label} reference for {SpriteLabels[slot]}",
                new[] { DialogHelper.PngFilter, DialogHelper.AllFilter });
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                RawImage imported;
                using (var fs = File.OpenRead(path))
                    imported = ImageConverter.DecodeRawImage(fs);
                if (imported == null)
                {
                    StatusText = "Image could not be decoded.";
                    return;
                }
                if (imported.Width != SpriteWidth || imported.Height != SpriteHeight)
                {
                    StatusText = $"Image must be {SpriteWidth}×{SpriteHeight} pixels to line up with the saved artwork (got {imported.Width}×{imported.Height}).";
                    return;
                }
                if (!TryReadImageColors(imported, out byte[] childIndices, out uint[] childPalette, out _))
                {
                    StatusText = "This image has more than 16 colors. Reduce it to 16 or fewer and try again.";
                    return;
                }

                uint[] derived = DeriveAlternatePalette(_rawSprites[slot], childIndices, childPalette, out bool[] derivedUsed);
                if (derived == null)
                {
                    StatusText = $"Could not get a {label} palette from this image.";
                    return;
                }

                if (shiny) { _shinyPal = derived; _shinyPalUsed = derivedUsed; }
                else { _normalPal = derived; _normalPalUsed = derivedUsed; }
                ApplyPalettesAndPublish();
                _dirty = true;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = $"Got the {label} palette from {SpriteLabels[slot]}'s reference image. Save to write it to the ROM.";
            }
            catch (Exception ex)
            {
                StatusText = $"Import failed: {ex.Message}";
            }
        }

        // --- Export PNG for one sprite slot -----------------------------------------
        public async Task ExportSprite(int slot, Window owner, bool shiny = false)
        {
            if (slot < 0 || slot > 3 || !HasSlot(slot)) return;

            bool[] used = shiny ? _shinyPalUsed : _normalPalUsed;
            int usedCount = 0;
            foreach (bool u in used) if (u) usedCount++;
            if (usedCount < 16)
            {
                bool fillBlack = await DialogHelper.AskYesNo(
                    $"This image has a palette of {usedCount} colors, not the full 16.\n\n" +
                    "Fill in the blanks with black? Choosing No keeps it as a " + usedCount + "-color palette.",
                    "Palette isn't full");
                if (fillBlack)
                {
                    for (int i = 0; i < 16; i++) used[i] = true;
                    RefreshSwatches(shiny ? ShinySwatches : NormalSwatches, shiny ? _shinyPal : _normalPal, used);
                }
            }

            string suffix = shiny ? "Shiny" : "";
            string path = await DialogHelper.SaveFile(owner,
                $"Export {SpriteLabels[slot]}{(shiny ? " (Shiny)" : "")} as PNG",
                new[] { DialogHelper.PngFilter, DialogHelper.AllFilter },
                $"mon{_currentId:D3}_{SpriteLabels[slot].Replace(" ", "")}{suffix}.png");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var raw = ComposeSprite(slot, shiny ? _shinyPal : _normalPal, transparentIndex0: false, frame: -1);
                if (raw == null) { StatusText = "Export failed: nothing to export."; return; }
                ImageConverter.ToAvaloniaBitmap(raw).Save(path, global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                StatusText = $"Exported {SpriteLabels[slot]}{(shiny ? " (Shiny)" : "")}.";
            }
            catch (Exception ex) { StatusText = $"Export failed: {ex.Message}"; }
        }

        // --- Helpers -----------------------------------------------------------------
        private static void RefreshSwatches(ObservableCollection<PaletteSwatch> target, uint[] palette, bool[] used)
        {
            target.Clear();
            if (palette == null) return;
            for (int i = 0; i < palette.Length; i++)
            {
                uint c = palette[i];
                var color = global::Avalonia.Media.Color.FromArgb((byte)(c >> 24), (byte)(c >> 16), (byte)(c >> 8), (byte)c);
                target.Add(new PaletteSwatch
                {
                    Brush = new global::Avalonia.Media.SolidColorBrush(color),
                    IsPlaceholder = used != null && i < used.Length && !used[i]
                });
            }
        }

        private void ApplyPalettesAndPublish()
        {
            if (_normalPal == null) return;

            RefreshSwatches(NormalSwatches, _normalPal, _normalPalUsed);
            RefreshSwatches(ShinySwatches, _shinyPal, _shinyPalUsed);

            RenderCurrentFrameForAllCells();

            // Battle-mock sprites per gender: a LIST of N 80×80 frames (N = sheet width / 80). The pattern
            // animation (pokeanm) picks which frame to show; the count drives the editor's Frame limit.
            int frontW = SlotWidth(3) != 0 ? SlotWidth(3) : SlotWidth(2);
            BattleFrameCount = frontW != 0 ? Math.Max(1, frontW / 80) : 2;
            BattleFrontM = RenderFrames(3, _normalPal, BattleFrameCount);
            BattleFrontF = RenderFrames(2, _normalPal, BattleFrameCount);
            BattleBackM  = RenderFrames(1, _normalPal, BattleFrameCount);
            BattleBackF  = RenderFrames(0, _normalPal, BattleFrameCount);
        }

        private bool HasSlot(int slot) => _rawSprites[slot] != null;
        private int SlotWidth(int slot) => _rawSprites[slot] != null ? SpriteWidth : 0;

        // Renders all `count` 80×80 frames of a sprite slot (null list if the slot is empty).
        private System.Collections.Generic.IReadOnlyList<AvaBitmap> RenderFrames(int slot, uint[] palette, int count)
        {
            if (!HasSlot(slot)) return null;
            var frames = new AvaBitmap[count];
            for (int i = 0; i < count; i++) frames[i] = RenderBattleSprite(slot, palette, i);
            return frames;
        }

        private AvaBitmap RenderSprite(int slot, uint[] palette)
        {
            try
            {
                var raw = ComposeSprite(slot, palette, transparentIndex0: false, frame: -1);
                // Scale up 2× so sprites are legible (160×80 → 320×160)
                return raw != null ? ImageConverter.ToAvaloniaBitmap(Scale2x(raw)) : null;
            }
            catch { return null; }
        }

        /// <summary>Re-renders all 8 pose previews at whichever frame each one is already showing (new pixel/palette data, same frame selection).</summary>
        private void RenderCurrentFrameForAllCells()
        {
            foreach (var cell in AllFrameCells) cell.OnChanged(cell.Frame);
        }

        // Crops the 80×80 cell at frame index `frame` (cell at x = frame*80) out of the sheet,
        // with palette index 0 made transparent (in-game colour 0). Out-of-range → null.
        private AvaBitmap RenderBattleSprite(int slot, uint[] palette, int frame)
        {
            try
            {
                var raw = ComposeSprite(slot, palette, transparentIndex0: true, frame: frame);
                return raw != null ? ImageConverter.ToAvaloniaBitmap(raw) : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Renders a sprite slot to BGRA. <paramref name="frame"/> &gt;= 0 crops the 80-wide cell at
        /// x = frame*80. With <paramref name="transparentIndex0"/>, palette index 0 becomes fully
        /// transparent (in-game colour 0).
        /// </summary>
        private RawImage ComposeSprite(int slot, uint[] palette, bool transparentIndex0, int frame)
        {
            byte[] indices = _rawSprites[slot];
            if (indices == null || palette == null) return null;

            int srcW = SpriteWidth, srcH = SpriteHeight;
            int x0 = 0, w = srcW;
            if (frame >= 0)
            {
                const int fw = 80;
                x0 = frame * fw; w = fw;
                if (x0 + fw > srcW) return null;
            }

            var outImg = new RawImage(w, srcH);
            for (int y = 0; y < srcH; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int o = (y * w + x) * 4;
                    byte pi = indices[y * SpriteWidth + x0 + x];
                    if (transparentIndex0 && pi == 0) continue;   // stays 0,0,0,0
                    uint c = palette[pi & 0xF];
                    outImg.Bgra[o] = (byte)c;
                    outImg.Bgra[o + 1] = (byte)(c >> 8);
                    outImg.Bgra[o + 2] = (byte)(c >> 16);
                    outImg.Bgra[o + 3] = (byte)(c >> 24);
                }
            }
            return outImg;
        }

        // Nearest-neighbor 2× upscale (pixel art, keeps edges crisp).
        private static RawImage Scale2x(RawImage src)
        {
            var dst = new RawImage(src.Width * 2, src.Height * 2);
            for (int y = 0; y < src.Height; y++)
            {
                for (int x = 0; x < src.Width; x++)
                {
                    int s = (y * src.Width + x) * 4;
                    for (int dy = 0; dy < 2; dy++)
                    {
                        for (int dx = 0; dx < 2; dx++)
                        {
                            int d = ((y * 2 + dy) * dst.Width + x * 2 + dx) * 4;
                            dst.Bgra[d] = src.Bgra[s];
                            dst.Bgra[d + 1] = src.Bgra[s + 1];
                            dst.Bgra[d + 2] = src.Bgra[s + 2];
                            dst.Bgra[d + 3] = src.Bgra[s + 3];
                        }
                    }
                }
            }
            return dst;
        }

        private void ClearBitmaps()
        {
            FemaleBackNormal = MaleBackNormal = FemaleFrontNormal = MaleFrontNormal = null;
            FemaleBackShiny  = MaleBackShiny  = FemaleFrontShiny  = MaleFrontShiny  = null;
            BattleFrontM = BattleFrontF = BattleBackM = BattleBackF = null;
            _rawSprites = new byte[4][];
            _normalPal = null; _shinyPal = null;
            _normalPalUsed = AllUsed(); _shinyPalUsed = AllUsed();
            NormalSwatches.Clear();
            ShinySwatches.Clear();
            foreach (var cell in AllFrameCells) cell.Frame = 0;
            CanAddOppositeGenderSprites = false;
            AddOppositeGenderLabel = "";
        }

        // Slots: 0=FemaleBack, 1=MaleBack, 2=FemaleFront, 3=MaleFront. A species can add the missing
        // gender's sprites only when that gender's back+front are BOTH placeholders and the other
        // gender's back+front are BOTH real; anything else (already has all 4, or a partial/corrupt
        // set) is left alone rather than guessed at.
        private void UpdateOppositeGenderGap(bool[] hasRealSprite)
        {
            bool femaleReal = hasRealSprite[0] && hasRealSprite[2];
            bool femaleMissing = !hasRealSprite[0] && !hasRealSprite[2];
            bool maleReal = hasRealSprite[1] && hasRealSprite[3];
            bool maleMissing = !hasRealSprite[1] && !hasRealSprite[3];

            if (maleReal && femaleMissing)
            {
                _missingGenderIsFemale = true;
                CanAddOppositeGenderSprites = true;
                AddOppositeGenderLabel = "Add Female Sprites (copy from Male)";
            }
            else if (femaleReal && maleMissing)
            {
                _missingGenderIsFemale = false;
                CanAddOppositeGenderSprites = true;
                AddOppositeGenderLabel = "Add Male Sprites (copy from Female)";
            }
            else
            {
                CanAddOppositeGenderSprites = false;
                AddOppositeGenderLabel = "";
            }
        }

        /// <summary>
        /// Clones the existing gender's back+front sprites into the currently-missing gender's slots,
        /// so the species has graphics for both genders (needed before its gender ratio can be widened
        /// in the Personal Data editor). The clones are byte-identical to the source until the user
        /// re-imports something different over them. Writes straight to the packed NARC (via an
        /// unpack/copy/repack round trip, since the placeholder slot is a different size than a real
        /// sprite entry) so the fix is immediately visible without requiring a full ROM save.
        /// </summary>
        public async Task AddOppositeGenderSprites(Window owner)
        {
            if (!CanAddOppositeGenderSprites || _currentId <= 0) return;

            string missingGender = _missingGenderIsFemale ? "female" : "male";
            string sourceGender = _missingGenderIsFemale ? "male" : "female";
            bool confirmed = await DialogHelper.AskYesNo(
                $"This Pokémon has no {missingGender} sprites. This will duplicate its {sourceGender} " +
                "back and front sprites into the missing slots, so a gender ratio change won't leave it " +
                "with blank graphics. The duplicates look identical to the existing sprites until you " +
                "import something different over them.\n\nContinue?",
                "Add Opposite Gender Sprites", owner);
            if (!confirmed) return;

            try
            {
                DSPRE.DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.pokemonBattleSprites });
                string unpackedDir = RomInfo.gameDirs[DirNames.pokemonBattleSprites].unpackedDir;
                string packedPath  = RomInfo.gameDirs[DirNames.pokemonBattleSprites].packedDir;

                int baseOffset = _currentId * 6;
                int srcBack  = baseOffset + (_missingGenderIsFemale ? 1 : 0);
                int srcFront = baseOffset + (_missingGenderIsFemale ? 3 : 2);
                int dstBack  = baseOffset + (_missingGenderIsFemale ? 0 : 1);
                int dstFront = baseOffset + (_missingGenderIsFemale ? 2 : 3);

                CopyEntryFile(unpackedDir, srcBack, dstBack);
                CopyEntryFile(unpackedDir, srcFront, dstFront);

                // Re-sync the packed NARC immediately (rather than waiting for the next full "Save ROM"),
                // since every other read in this editor, LoadMon included, goes through the packed file.
                Narc.FromFolder(unpackedDir).Save(packedPath);

                // Sprites alone aren't enough: without height data too, the new gender renders at the wrong Y.
                try
                {
                    DSPRE.DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.pokeHeight });
                    string heightDir = RomInfo.gameDirs[DirNames.pokeHeight].unpackedDir;
                    string heightPackedPath = RomInfo.gameDirs[DirNames.pokeHeight].packedDir;

                    const int FB = 0, MB = 1, FF = 2, MF = 3;
                    int heightBase = _currentId * 4;
                    int srcBackH = heightBase + (_missingGenderIsFemale ? MB : FB);
                    int srcFrontH = heightBase + (_missingGenderIsFemale ? MF : FF);
                    int dstBackH = heightBase + (_missingGenderIsFemale ? FB : MB);
                    int dstFrontH = heightBase + (_missingGenderIsFemale ? FF : MF);

                    CopyEntryFile(heightDir, srcBackH, dstBackH);
                    CopyEntryFile(heightDir, srcFrontH, dstFrontH);
                    Narc.FromFolder(heightDir).Save(heightPackedPath);
                }
                catch (Exception heightEx)
                {
                    AppLogger.Error($"Failed to copy battle-sprite height data for opposite gender: {heightEx.Message}");
                }

                StatusText = $"Added {missingGender} sprites (duplicated from the existing {sourceGender} sprites). " +
                    "Use Import to give them their own look.";
                LoadMon(_currentId);
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to add {missingGender} sprites: {ex.Message}";
            }
        }

        private static void CopyEntryFile(string unpackedDir, int srcIdx, int dstIdx)
            => File.Copy(Path.Combine(unpackedDir, srcIdx.ToString("D4")),
                          Path.Combine(unpackedDir, dstIdx.ToString("D4")), overwrite: true);

        // --- Ported from PokemonSpriteEditor: MakeImage / ReadPalette ----------------

        /// <summary>Decrypts one 6448-byte battle sprite entry to 160×80 4bpp palette indices.</summary>
        private static byte[] MakeImage(FileStream fs)
        {
            fs.Seek(48L, SeekOrigin.Current);
            using var reader = new BinaryReader(fs, System.Text.Encoding.Default, leaveOpen: true);

            ushort[] arr = new ushort[3200];
            for (int i = 0; i < 3200; i++) arr[i] = reader.ReadUInt16();

            uint num = arr[0];
            if (gameFamily != GameFamilies.DP)
            {
                for (int j = 0; j < 3200; j++)
                {
                    unchecked { arr[j] = (ushort)(arr[j] ^ (ushort)(num & 0xFFFF)); num *= 1103515245; num += 24691; }
                }
            }
            else
            {
                num = arr[3199];
                for (int j = 3199; j >= 0; j--)
                {
                    unchecked { arr[j] = (ushort)(arr[j] ^ (ushort)(num & 0xFFFF)); num *= 1103515245; num += 24691; }
                }
            }

            byte[] pixels = new byte[SpriteWidth * SpriteHeight];
            for (int k = 0; k < 3200; k++)
            {
                pixels[k * 4]     = (byte)(arr[k] & 0xF);
                pixels[k * 4 + 1] = (byte)((arr[k] >> 4) & 0xF);
                pixels[k * 4 + 2] = (byte)((arr[k] >> 8) & 0xF);
                pixels[k * 4 + 3] = (byte)((arr[k] >> 12) & 0xF);
            }
            return pixels;
        }

        /// <summary>Reads one 72-byte palette entry as 16 packed-BGRA colors (opaque).</summary>
        private static uint[] ReadPalette(FileStream fs)
        {
            fs.Seek(40L, SeekOrigin.Current);
            using var reader = new BinaryReader(fs, System.Text.Encoding.Default, leaveOpen: true);
            var pal = new uint[16];
            for (int j = 0; j < 16; j++)
            {
                ushort v = reader.ReadUInt16();
                uint r = (uint)((v & 0x1F) << 3);
                uint g = (uint)(((v >> 5) & 0x1F) << 3);
                uint b = (uint)(((v >> 10) & 0x1F) << 3);
                pal[j] = 0xFF000000u | (r << 16) | (g << 8) | b;
            }
            return pal;
        }

        // --- Save: writes every loaded pose plus both palettes back into the ROM's battle-sprite
        // NARC, in place (fixed-size entries, same shape MakeImage/ReadPalette already expect to
        // read back). Ported from PokemonSpriteEditor's SaveChanges_Click/SaveBin/SavePal. ---------

        public void Save()
        {
            if (!_dirty || _currentId <= 0) return;

            string packedPath = RomInfo.gameDirs[DirNames.pokemonBattleSprites].packedDir;
            if (!File.Exists(packedPath))
            {
                StatusText = "Battle sprites NARC not found. Make sure the ROM is loaded.";
                return;
            }

            var narc = new NarcReader(packedPath);
            int baseOffset = _currentId * 6;

            for (int i = 0; i < 4; i++)
            {
                if (_rawSprites[i] == null) continue;
                int idx = baseOffset + i;
                if (idx >= narc.fe.Length || narc.fe[idx].Size != 6448) continue;
                narc.OpenEntry(idx);
                WriteSpriteEntry(narc.fs, _rawSprites[i]);
                narc.Close();
            }
            if (_normalPal != null)
            {
                int idx = baseOffset + 4;
                if (idx < narc.fe.Length && narc.fe[idx].Size == 72) { narc.OpenEntry(idx); WritePaletteEntry(narc.fs, _normalPal); narc.Close(); }
            }
            if (_shinyPal != null)
            {
                int idx = baseOffset + 5;
                if (idx < narc.fe.Length && narc.fe[idx].Size == 72) { narc.OpenEntry(idx); WritePaletteEntry(narc.fs, _shinyPal); narc.Close(); }
            }

            _dirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            StatusText = "Saved.";
        }

        /// <summary>Encrypts 160×80 4bpp palette indices into one 6448-byte battle sprite entry. Ported from PokemonSpriteEditor.SaveBin.</summary>
        private static void WriteSpriteEntry(FileStream fs, byte[] indices)
        {
            ushort[] packed = new ushort[3200];
            for (int i = 0; i < 3200; i++)
            {
                packed[i] = (ushort)((indices[i * 4] & 0xF) | ((indices[i * 4 + 1] & 0xF) << 4) |
                                      ((indices[i * 4 + 2] & 0xF) << 8) | ((indices[i * 4 + 3] & 0xF) << 12));
            }

            // MakeImage reads its seed straight back from this position, so it must literally BE the
            // seed, not the seed XORed with a pixel value, or every position after it decodes wrong.
            if (gameFamily != GameFamilies.DP)
            {
                uint num = 0u;
                packed[0] = (ushort)(num & 0xFFFF);
                num = num * 1103515245 + 24691;
                for (int j = 1; j < 3200; j++)
                {
                    unchecked { packed[j] = (ushort)(packed[j] ^ (ushort)(num & 0xFFFF)); num = num * 1103515245 + 24691; }
                }
            }
            else
            {
                uint seed = 31315u;
                for (int k = 3199; k >= 0; k--) seed += packed[k];
                uint num = seed;
                packed[3199] = (ushort)(num & 0xFFFF);
                num = num * 1103515245 + 24691;
                for (int k = 3198; k >= 0; k--)
                {
                    unchecked { packed[k] = (ushort)(packed[k] ^ (ushort)(num & 0xFFFF)); num = num * 1103515245 + 24691; }
                }
            }

            byte[] header = {
                82, 71, 67, 78, 255, 254, 0, 1, 48, 25, 0, 0, 16, 0, 1, 0,
                82, 65, 72, 67, 32, 25, 0, 0, 10, 0, 20, 0, 3, 0, 0, 0,
                0, 0, 0, 0, 1, 0, 0, 0, 0, 25, 0, 0, 24, 0, 0, 0
            };
            var bw = new BinaryWriter(fs);
            bw.Write(header, 0, 48);
            for (int l = 0; l < 3200; l++) bw.Write(packed[l]);
        }

        /// <summary>Packs 16 ARGB colors into one 72-byte RGB555 palette entry. Ported from PokemonSpriteEditor.SavePal.</summary>
        private static void WritePaletteEntry(FileStream fs, uint[] palette)
        {
            byte[] header = {
                82, 76, 67, 78, 255, 254, 0, 1, 72, 0, 0, 0, 16, 0, 1, 0,
                84, 84, 76, 80, 56, 0, 0, 0, 4, 0, 10, 0, 0, 0, 0, 0,
                32, 0, 0, 0, 16, 0, 0, 0
            };
            var bw = new BinaryWriter(fs);
            bw.Write(header, 0, 40);
            for (int i = 0; i < 16; i++)
            {
                byte r = (byte)(palette[i] >> 16), g = (byte)(palette[i] >> 8), b = (byte)palette[i];
                ushort v = (ushort)(((r >> 3) & 0x1F) | (((g >> 3) & 0x1F) << 5) | (((b >> 3) & 0x1F) << 10));
                bw.Write(v);
            }
        }

        // --- Reading an image's colors and matching palettes, ported from IndexedBitmapHandler ------

        /// <summary>Builds a color index (0-15) per pixel plus the list of colors used, in the order they first appear. Fails past 16 distinct colors, the ROM format's own limit.</summary>
        private static bool TryReadImageColors(RawImage img, out byte[] indices, out uint[] palette, out int usedCount)
        {
            indices = new byte[SpriteWidth * SpriteHeight];
            palette = new uint[16];
            usedCount = 0;

            var seen = new System.Collections.Generic.Dictionary<uint, byte>();
            int n = SpriteWidth * SpriteHeight;
            for (int p = 0; p < n; p++)
            {
                int o = p * 4;
                uint c = 0xFF000000u | ((uint)img.Bgra[o + 2] << 16) | ((uint)img.Bgra[o + 1] << 8) | img.Bgra[o];
                if (!seen.TryGetValue(c, out byte idx))
                {
                    if (seen.Count >= 16) { indices = null; palette = null; usedCount = 0; return false; }
                    idx = (byte)seen.Count;
                    seen[c] = idx;
                    palette[idx] = c;
                }
                indices[p] = idx;
            }
            usedCount = seen.Count;
            for (int i = usedCount; i < 16; i++) palette[i] = 0xFF000000u;
            return true;
        }

        private static bool PaletteEqualsUpTo(uint[] existing, uint[] candidate, int count)
        {
            for (int i = 0; i < count; i++) if (existing[i] != candidate[i]) return false;
            return true;
        }

        private static bool[] MakeUsedMask(int usedCount)
        {
            var a = new bool[16];
            for (int i = 0; i < usedCount && i < 16; i++) a[i] = true;
            return a;
        }

        /// <summary>Points the new image's pixels at the palette that's already saved: matching colors reuse their existing slot, new colors take a genuine placeholder slot rather than overwriting a real existing color the current import just doesn't happen to need.</summary>
        private static byte[] RemapToExistingPalette(byte[] newIndices, uint[] newPalette, int usedCount, uint[] existingPalette, bool[] existingUsed, out uint[] mergedPalette, out bool[] mergedUsed)
        {
            var indexMap = new byte[16];
            var claimed = new bool[16];

            for (int i = 0; i < usedCount; i++)
            {
                int found = -1;
                for (int j = 0; j < 16; j++)
                {
                    if (!claimed[j] && existingPalette[j] == newPalette[i]) { found = j; break; }
                }
                indexMap[i] = found >= 0 ? (byte)found : (byte)255;
                if (found >= 0) claimed[found] = true;
            }

            mergedPalette = (uint[])existingPalette.Clone();
            mergedUsed = (bool[])(existingUsed ?? AllUsed()).Clone();
            for (int i = 0; i < usedCount; i++)
            {
                if (indexMap[i] != 255) continue;
                int freeSlot = -1;
                for (int j = 0; j < 16; j++) if (!claimed[j] && !mergedUsed[j]) { freeSlot = j; break; }
                if (freeSlot < 0)
                    for (int j = 0; j < 16; j++) if (!claimed[j]) { freeSlot = j; break; } // no placeholder left, don't drop the color
                if (freeSlot < 0) freeSlot = 0; // unreachable: usedCount <= 16
                claimed[freeSlot] = true;
                indexMap[i] = (byte)freeSlot;
                mergedPalette[freeSlot] = newPalette[i];
                mergedUsed[freeSlot] = true;
            }

            var outIdx = new byte[newIndices.Length];
            for (int p = 0; p < newIndices.Length; p++) outIdx[p] = indexMap[newIndices[p]];
            return outIdx;
        }

        /// <summary>Builds the shiny palette from a reference image: for each Normal color index, reads whatever color the reference image has at one of the pixels using that index.</summary>
        private static uint[] DeriveAlternatePalette(byte[] parentIndices, byte[] childIndices, uint[] childPalette, out bool[] used)
        {
            used = null;
            if (parentIndices == null || childIndices == null || parentIndices.Length != childIndices.Length) return null;
            var result = new uint[16];
            var found = new bool[16];
            for (int p = 0; p < parentIndices.Length; p++)
            {
                int i = parentIndices[p] & 0xF;
                if (!found[i]) { result[i] = childPalette[childIndices[p]]; found[i] = true; }
            }
            for (int i = 0; i < 16; i++) if (!found[i]) result[i] = 0xFF000000u;
            used = found;
            return result;
        }
    }
}
