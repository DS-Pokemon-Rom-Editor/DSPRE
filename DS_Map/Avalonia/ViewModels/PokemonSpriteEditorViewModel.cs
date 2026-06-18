using Avalonia.Controls;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.Editors.Utils;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AvaBitmap = Avalonia.Media.Imaging.Bitmap;
using GdiBitmap = System.Drawing.Bitmap;
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
        public void SaveChanges() { /* sprites are saved inline via Save(); */ }
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

        private string _statusText = "";
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        // --- Internal state ----------------------------------------------------------
        private int _currentId = -1;
        // Raw GDI bitmaps indexed 0-3 (FemBack, MBack, FFront, MFront), kept for save
        private GdiBitmap[] _rawSprites = new GdiBitmap[4];
        private System.Drawing.Imaging.ColorPalette _normalPal;
        private System.Drawing.Imaging.ColorPalette _shinyPal;
        // Replacement bitmaps loaded from PNG by the user (index 0-3)
        private readonly GdiBitmap[] _replacementSprites = new GdiBitmap[4];

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

        private FormSpriteData[] _currentFormData;

        /// <summary>
        /// Switches between the main sprite NARC and the alternate-forms NARC.
        /// </summary>
        public void ToggleAlternateFormsMode()
        {
            _isAlternateForms = !_isAlternateForms;
            OnPropertyChanged(nameof(IsAlternateForms));
            OnPropertyChanged(nameof(ToggleButtonText));

            if (_isAlternateForms)
            {
                _currentFormData = GetFormDataForCurrentGame();
                AlternateFormNames.Clear();
                for (int i = 0; i < _currentFormData.Length; i++)
                    AlternateFormNames.Add($"{i:D3} {_currentFormData[i].Name}");
                _selectedFormIndex = 0;
                OnPropertyChanged(nameof(SelectedFormIndex));
                LoadAlternateForm(0);
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
                var rawBmps = new GdiBitmap[4];

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
                System.Drawing.Imaging.ColorPalette normalPal = null, shinyPal = null;
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
            StatusText = "Design preview — no sprites loaded";
        }

        // --- Runtime constructor -----------------------------------------------------
        public PokemonSpriteEditorViewModel(bool _) { /* just constructed; LoadMon called by parent */ }

        // --- Load --------------------------------------------------------------------
        public void LoadMon(int id)
        {
            _currentId = id;
            ClearBitmaps();
            StatusText = "";

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
                var rawBmps = new GdiBitmap[4];
                for (int i = 0; i < 4; i++)
                {
                    int idx = baseOffset + i;
                    if (idx < narc.fe.Length && narc.fe[idx].Size == 6448)
                    {
                        narc.OpenEntry(idx);
                        rawBmps[i] = MakeImage(narc.fs);
                        narc.Close();
                    }
                }

                // Load palettes
                System.Drawing.Imaging.ColorPalette normalPal = null, shinyPal = null;
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
                var imported = new GdiBitmap(path);
                if (imported.Width != 160 || imported.Height != 80)
                {
                    StatusText = $"Sprite must be 160×80 pixels (got {imported.Width}×{imported.Height}).";
                    return;
                }
                _replacementSprites[slot] = imported;
                _rawSprites[slot] = imported;
                ApplyPalettesAndPublish();
                _dirty = true;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = $"Imported {SpriteLabels[slot]}. Save to apply.";
            }
            catch (Exception ex)
            {
                StatusText = $"Import failed: {ex.Message}";
            }
        }

        // --- Export PNG for one sprite slot -----------------------------------------
        public async Task ExportSprite(int slot, Window owner)
        {
            if (slot < 0 || slot > 3 || _rawSprites[slot] == null) return;
            string path = await DialogHelper.SaveFile(owner,
                $"Export {SpriteLabels[slot]} as PNG",
                new[] { DialogHelper.PngFilter, DialogHelper.AllFilter },
                $"mon{_currentId:D3}_{SpriteLabels[slot].Replace(" ", "")}.png");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                _rawSprites[slot].Palette = _normalPal;
                _rawSprites[slot].Save(path, System.Drawing.Imaging.ImageFormat.Png);
                StatusText = $"Exported {SpriteLabels[slot]}.";
            }
            catch (Exception ex) { StatusText = $"Export failed: {ex.Message}"; }
        }

        // --- Helpers -----------------------------------------------------------------
        private void ApplyPalettesAndPublish()
        {
            if (_normalPal == null) return;

            // Publish normal versions
            FemaleBackNormal  = RenderSprite(0, _normalPal);
            MaleBackNormal    = RenderSprite(1, _normalPal);
            FemaleFrontNormal = RenderSprite(2, _normalPal);
            MaleFrontNormal   = RenderSprite(3, _normalPal);

            // Publish shiny versions
            FemaleBackShiny   = RenderSprite(0, _shinyPal);
            MaleBackShiny     = RenderSprite(1, _shinyPal);
            FemaleFrontShiny  = RenderSprite(2, _shinyPal);
            MaleFrontShiny    = RenderSprite(3, _shinyPal);

            // Battle-mock sprites per gender: a LIST of N 80×80 frames (N = sheet width / 80). The pattern
            // animation (pokeanm) picks which frame to show; the count drives the editor's Frame limit.
            var frontRaw = _rawSprites[3] ?? _rawSprites[2];
            BattleFrameCount = frontRaw != null ? Math.Max(1, frontRaw.Width / 80) : 2;
            BattleFrontM = RenderFrames(3, _normalPal, BattleFrameCount);
            BattleFrontF = RenderFrames(2, _normalPal, BattleFrameCount);
            BattleBackM  = RenderFrames(1, _normalPal, BattleFrameCount);
            BattleBackF  = RenderFrames(0, _normalPal, BattleFrameCount);
        }

        // Renders all `count` 80×80 frames of a sprite slot (null list if the slot is empty).
        private System.Collections.Generic.IReadOnlyList<AvaBitmap> RenderFrames(int slot, System.Drawing.Imaging.ColorPalette palette, int count)
        {
            if (_rawSprites[slot] == null) return null;
            var frames = new AvaBitmap[count];
            for (int i = 0; i < count; i++) frames[i] = RenderBattleSprite(slot, palette, i);
            return frames;
        }

        private AvaBitmap RenderSprite(int slot, System.Drawing.Imaging.ColorPalette palette)
        {
            if (_rawSprites[slot] == null) return null;
            try
            {
                var bmp = (GdiBitmap)_rawSprites[slot].Clone();
                bmp.Palette = palette;
                // Scale up 2× so sprites are legible (160×80 → 320×160)
                var scaled = new GdiBitmap(bmp, 320, 160);
                return ImageConverter.ToAvaloniaBitmap(scaled);
            }
            catch { return null; }
        }

        // Crops the 80×80 cell at frame index `frame` (cell at x = frame*80) out of the sheet, into a 32bpp
        // ARGB surface with palette index 0 made transparent (in-game colour 0). Out-of-range → null.
        private AvaBitmap RenderBattleSprite(int slot, System.Drawing.Imaging.ColorPalette palette, int frame)
        {
            if (_rawSprites[slot] == null) return null;
            try
            {
                var src = (GdiBitmap)_rawSprites[slot].Clone();
                src.Palette = palette;
                const int fw = 80;
                if ((frame + 1) * fw > src.Width) return null;
                var argb = new GdiBitmap(fw, src.Height, PixelFormat.Format32bppArgb);
                using (var g = System.Drawing.Graphics.FromImage(argb))
                    g.DrawImage(src,
                        new System.Drawing.Rectangle(0, 0, fw, src.Height),
                        new System.Drawing.Rectangle(frame * fw, 0, fw, src.Height),
                        System.Drawing.GraphicsUnit.Pixel);
                argb.MakeTransparent(palette.Entries[0]);
                return ImageConverter.ToAvaloniaBitmap(argb);
            }
            catch { return null; }
        }

        private void ClearBitmaps()
        {
            FemaleBackNormal = MaleBackNormal = FemaleFrontNormal = MaleFrontNormal = null;
            FemaleBackShiny  = MaleBackShiny  = FemaleFrontShiny  = MaleFrontShiny  = null;
            BattleFrontM = BattleFrontF = BattleBackM = BattleBackF = null;
            _rawSprites = new GdiBitmap[4];
            _normalPal = null; _shinyPal = null;
        }

        // --- Ported from PokemonSpriteEditor: MakeImage / SetPal --------------------

        private static GdiBitmap MakeImage(FileStream fs)
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

            var bmp = new GdiBitmap(160, 80, PixelFormat.Format8bppIndexed);
            byte[] pixels = new byte[12800];
            for (int k = 0; k < 3200; k++)
            {
                pixels[k * 4]     = (byte)(arr[k] & 0xF);
                pixels[k * 4 + 1] = (byte)((arr[k] >> 4) & 0xF);
                pixels[k * 4 + 2] = (byte)((arr[k] >> 8) & 0xF);
                pixels[k * 4 + 3] = (byte)((arr[k] >> 12) & 0xF);
            }
            var bd = bmp.LockBits(new System.Drawing.Rectangle(0, 0, 160, 80), ImageLockMode.WriteOnly, bmp.PixelFormat);
            Marshal.Copy(pixels, 0, bd.Scan0, 12800);
            bmp.UnlockBits(bd);

            // Apply greyscale placeholder palette (replaced when palette is applied in RenderSprite)
            var tmp = new GdiBitmap(1, 1, PixelFormat.Format4bppIndexed);
            var pal = tmp.Palette;
            for (int l = 0; l < 16; l++) pal.Entries[l] = System.Drawing.Color.FromArgb(l << 4, l << 4, l << 4);
            bmp.Palette = pal;
            return bmp;
        }

        private static System.Drawing.Imaging.ColorPalette ReadPalette(FileStream fs)
        {
            fs.Seek(40L, SeekOrigin.Current);
            using var reader = new BinaryReader(fs, System.Text.Encoding.Default, leaveOpen: true);
            ushort[] arr = new ushort[16];
            for (int i = 0; i < 16; i++) arr[i] = reader.ReadUInt16();
            var tmp = new GdiBitmap(1, 1, PixelFormat.Format4bppIndexed);
            var pal = tmp.Palette;
            for (int j = 0; j < 16; j++)
            {
                pal.Entries[j] = System.Drawing.Color.FromArgb(
                    (arr[j] & 0x1F) << 3,
                    ((arr[j] >> 5) & 0x1F) << 3,
                    ((arr[j] >> 10) & 0x1F) << 3);
            }
            return pal;
        }
    }
}
