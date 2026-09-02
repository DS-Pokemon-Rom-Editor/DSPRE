using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>
    /// Guided front end for the sprite editor's existing import actions: asks what to change and
    /// which pose(s)/palette(s), then calls the same ImportSprite/ImportNormalPalette/ImportShinyPalette
    /// methods the individual buttons already use, so file-picking and palette-mismatch prompts still
    /// happen exactly the way they already do.
    /// </summary>
    public class SpriteImportWizardViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        public static readonly string[] AllPoses = { "Female Back", "Male Back", "Female Front", "Male Front" };

        private readonly PokemonSpriteEditorViewModel _sprite;
        private readonly Window _owner;

        public bool GenderGapActive { get; }
        public string ExistingGender { get; }
        public string[] AvailableRefPoses { get; }

        public SpriteImportWizardViewModel(PokemonSpriteEditorViewModel sprite, Window owner)
        {
            _sprite = sprite;
            _owner = owner;
            // Two reasons only one gender is really importable: a genuine mono-gender/genderless species, or an alternate form (only Male is ever actually saved, see SaveAlternateForm).
            GenderGapActive = sprite.CanAddOppositeGenderSprites || sprite.IsAlternateForms;
            ExistingGender = sprite.IsAlternateForms ? "Male" : sprite.ExistingGenderName;
            AvailableRefPoses = GenderGapActive
                ? new[] { ExistingGender + " Back", ExistingGender + " Front" }
                : AllPoses;
            RefPose = AvailableRefPoses[AvailableRefPoses.Length > 2 ? 2 : 0];
        }

        // ---- What to change ----
        private string _mode = "image"; // image | palette | full | sheet
        public string Mode
        {
            get => _mode;
            set
            {
                if (!Set(ref _mode, value)) return;
                OnPropertyChanged(nameof(ShowArtworkScope));
                OnPropertyChanged(nameof(ShowPaletteScope));
                OnPropertyChanged(nameof(ShowSheetScope));
                OnPropertyChanged(nameof(RunButtonText));
            }
        }
        public bool ShowArtworkScope => Mode == "image" || Mode == "full";
        public bool ShowPaletteScope => Mode == "palette";
        public bool ShowSheetScope => Mode == "sheet";

        // ---- Sheet scope: one image holds Back+Front together, so it only needs a gender and a color ----
        private string _sheetColorMode = "normal"; // normal | shiny
        public string SheetColorMode { get => _sheetColorMode; set => Set(ref _sheetColorMode, value); }
        private string _sheetGenderMode = "Both"; // Female | Male | Both | Combined
        public string SheetGenderMode { get => _sheetGenderMode; set => Set(ref _sheetGenderMode, value); }

        /// <summary>Only true when the species genuinely has separate male/female art, so a single sheet with all 4 poses actually makes sense.</summary>
        public bool CanUseFullSheet => _sprite.CanUseFullSheet;

        /// <summary>True while this species' sprites are hg-engine source-backed, where only Male Back is a real independent shiny-color file.</summary>
        public bool IsHgEngineActive => _sprite.IsHgEngineSourced;

        /// <summary>Under hg-engine, deriving a shiny palette only makes sense from the one real source pose (Male Back).</summary>
        public bool ShowShinyPaletteOption => !IsHgEngineActive || RefPose == "Male Back";

        // ---- Artwork scope: facing x gender are independent axes ----
        private string _faceMode = "Front"; // Back | Front | Both
        public string FaceMode { get => _faceMode; set => Set(ref _faceMode, value); }
        private string _genderMode = "Both"; // Female | Male | Both
        public string GenderMode { get => _genderMode; set => Set(ref _genderMode, value); }

        // ---- Palette scope: palette applies to all 4 poses, so it just needs one reference image ----
        private bool _includeNormalPalette = true;
        public bool IncludeNormalPalette { get => _includeNormalPalette; set => Set(ref _includeNormalPalette, value); }
        private bool _includeShinyPalette = true;
        public bool IncludeShinyPalette { get => _includeShinyPalette; set => Set(ref _includeShinyPalette, value); }
        private string _refPose;
        public string RefPose
        {
            get => _refPose;
            set
            {
                if (!Set(ref _refPose, value)) return;
                OnPropertyChanged(nameof(ShowShinyPaletteOption));
                if (!ShowShinyPaletteOption) IncludeShinyPalette = false;
            }
        }

        public string RunButtonText => Mode switch { "palette" => "Import Palette", "sheet" => "Import Sheet", _ => "Import Artwork" };

        public List<string> ComputePoseList()
        {
            var genders = GenderGapActive ? new[] { ExistingGender } : (GenderMode == "Both" ? new[] { "Female", "Male" } : new[] { GenderMode });
            var faces = FaceMode == "Both" ? new[] { "Back", "Front" } : new[] { FaceMode };
            var result = new List<string>();
            foreach (var pose in AllPoses)
            {
                var parts = pose.Split(' ');
                if (genders.Contains(parts[0]) && faces.Contains(parts[1])) result.Add(pose);
            }
            return result;
        }

        private static int SlotFor(string pose) => Array.IndexOf(AllPoses, pose);

        public async Task RunAsync()
        {
            if (Mode == "palette")
            {
                int refSlot = SlotFor(RefPose);
                if (IncludeNormalPalette) await _sprite.ImportNormalPalette(refSlot, _owner);
                if (IncludeShinyPalette) await _sprite.ImportShinyPalette(refSlot, _owner);
            }
            else if (Mode == "sheet")
            {
                if (SheetGenderMode == "Combined")
                {
                    if (SheetColorMode == "shiny") await _sprite.ImportShinyFullSheet(_owner);
                    else await _sprite.ImportFullSheet(_owner);
                    return;
                }
                var genders = GenderGapActive ? new[] { ExistingGender } : (SheetGenderMode == "Both" ? new[] { "Female", "Male" } : new[] { SheetGenderMode });
                foreach (var gender in genders)
                {
                    bool female = gender == "Female";
                    if (SheetColorMode == "shiny") await _sprite.ImportShinySpriteSheet(_owner, female);
                    else await _sprite.ImportSpriteSheet(_owner, female);
                }
            }
            else
            {
                foreach (var pose in ComputePoseList())
                    await _sprite.ImportSprite(SlotFor(pose), _owner);
            }
        }
    }
}
