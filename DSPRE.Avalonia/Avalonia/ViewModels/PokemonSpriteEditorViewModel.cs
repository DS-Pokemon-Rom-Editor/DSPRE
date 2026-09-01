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
using FormSpriteData = DSPRE.Avalonia.Data.AlternateFormSprites.Form;

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
            public int Index { get; set; }
            public bool Shiny { get; set; }
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
            // Some ROM sprites (e.g. Deoxys's back sprite) only ever had one frame drawn, the rest is blank padding.
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
        // True only when this species itself loaded from hg-engine source, not just when a checkout is linked.
        private bool _loadedFromHgEngine;
        public bool IsHgEngineSourced => _loadedFromHgEngine;
        public bool ShowShinyFullSheetImport => CanUseFullSheet && !IsHgEngineSourced;

        // Alternate forms only store one shared back/front sprite in the ROM, so Male is the one place that saves.
        public bool ShowFemaleFormImport => !IsAlternateForms;
        public bool ShowFemaleShinyFormImport => !IsHgEngineSourced && !IsAlternateForms;

        private const int SpriteWidth = 160;
        private const int SpriteHeight = 80;

        private static readonly string[] SpriteLabels = { "Female Back", "Male Back", "Female Front", "Male Front" };

        // --- Alternate forms support -------------------------------------------------


        private bool _isAlternateForms = false;
        public bool IsAlternateForms { get => _isAlternateForms; private set => Set(ref _isAlternateForms, value); }

        private int _selectedFormIndex = 0;
        /// <summary>Index into _currentFormData; only meaningful while IsAlternateForms is true (BattleDisplayEditorViewModel mirrors both).</summary>
        public int SelectedFormIndex { get => _selectedFormIndex; private set => Set(ref _selectedFormIndex, value); }

        /// <summary>The current form's back/front otherpoke sprite indices, which are also the correct height_o.narc record indices (same charDataID/fileId formula per species in the real game).</summary>
        public bool TryGetCurrentFormHeightIndices(out int backIndex, out int frontIndex)
        {
            backIndex = frontIndex = -1;
            if (_currentFormData == null || _selectedFormIndex < 0 || _selectedFormIndex >= _currentFormData.Length)
                return false;
            var f = _currentFormData[_selectedFormIndex];
            if (f.BackSpriteIndex < 0 || f.FrontSpriteIndex < 0) return false; // hg-engine-native entries have no vanilla index
            backIndex = f.BackSpriteIndex;
            frontIndex = f.FrontSpriteIndex;
            return true;
        }

        /// <summary>Every alternate form name for the current species. No separate "Base Sprites" entry; the first one already is the real base sprite (see LoadMon).</summary>
        public ObservableCollection<string> VariantNames { get; } = new();

        /// <summary>Fires with the main-list id that should be showing for the picked form: its own pl_personal_extra entry if it has one, otherwise the base species id.</summary>
        public event Action<int> FormPseudoIdSelected;

        private int _selectedVariantIndex = 0;
        /// <summary>Index into _currentFormData. Picking any entry loads straight into that form.</summary>
        public int SelectedVariantIndex
        {
            get => _selectedVariantIndex;
            set
            {
                if (!Set(ref _selectedVariantIndex, value)) return;
                if (_currentFormData == null || value < 0 || value >= _currentFormData.Length) return;

                // hg-engine-native form entry (Mega/Gigantamax/etc, no otherpoke equivalent at all): jump straight there. Its own species id (index 0, "no transformation") is a no-op, already loaded.
                int nativeId = _currentFormData[value].HgEngineSpeciesId;
                if (nativeId >= 0)
                {
                    if (nativeId != _currentId) JumpToSpecies(nativeId);
                    return;
                }

                // hg-engine may have moved this form to its own real species; if so, follow it there instead of reading its now-dead otherpoke entry.
                int migratedId = HgEngineProject.IsActive ? ResolveHgEngineMigratedFormId(_currentFormFamilyBaseId, value) : -1;
                if (migratedId >= 0)
                {
                    JumpToSpecies(migratedId);
                    return;
                }

                IsAlternateForms = true;
                SelectedFormIndex = value;
                LoadAlternateForm(value);
                int pseudoId = ResolveFormPseudoId(_currentId, _currentFormData[value].Name);
                // Index 0 is the species' own default form, so its stats are the base species' stats and
                // the note only confuses there.
                FormSharesBaseData = value > 0 && pseudoId < 0;
                FormPseudoIdSelected?.Invoke(pseudoId >= 0 ? pseudoId : _currentId);
            }
        }

        // Jumping to a different species rebuilds VariantNames for that species, but this is called from
        // inside the very ComboBox item click that's still resolving selection against the OLD list --
        // mutating it synchronously here crashed the app for real (confirmed live). Posting defers the
        // rebuild to the next UI dispatch, after the click has fully finished.
        private void JumpToSpecies(int id) => global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            FormPseudoIdSelected?.Invoke(id);
            LoadMon(id);
        });

        private bool _formSharesBaseData;
        /// <summary>True when the selected form has no entry of its own in the main species list, so Personal Data/Learnset/Evolutions still read and save against the base species while only the sprite itself is form-specific (Unown, Castform, Cherrim, Shellos/Gastrodon, Arceus, Pichu, the egg forms).</summary>
        public bool FormSharesBaseData { get => _formSharesBaseData; private set => Set(ref _formSharesBaseData, value); }
        public string FormSharesBaseDataText => "Stats, type, and other Personal Data are shared with the base Pokémon and will be saved there. Only this sprite belongs to the form.";

        private bool _hasAlternateForms;
        /// <summary>True when the current species has its own entries in the alternate-forms table (Deoxys, Unown, etc.), so the variant dropdown only shows up when it's actually useful.</summary>
        public bool HasAlternateForms { get => _hasAlternateForms; private set => Set(ref _hasAlternateForms, value); }

        private FormSpriteData[] _currentFormData;

        /// <summary>Form table names look like "DEOXYS - Attack"; splitting on the dash is the only way to tell which species an entry belongs to, since there's no id field for it.</summary>
        private static string SpeciesNamePrefix(string formName)
        {
            int dash = formName.IndexOf(" - ", StringComparison.Ordinal);
            return dash < 0 ? null : formName.Substring(0, dash);
        }

        private static bool FormNameMatchesDescription(string formName, string description)
        {
            int dash = formName.IndexOf(" - ", StringComparison.Ordinal);
            return dash >= 0 && string.Equals(formName.Substring(dash + 3), description, StringComparison.OrdinalIgnoreCase);
        }

        // pl_personal_extra pseudo-species run consecutively right after the real Pokédex; resolves one back to its base species + form name.
        // Vanilla-only: under hg-engine that same numeric range is real species (Mega/Gigantamax/etc, see GetPokemonNamesWithForms), not pseudo-forms.
        private static (int baseId, string description)? ResolvePseudoFormId(int id)
        {
            if (HgEngineProject.IsActive) return null;
            int extraIndex = id - RomInfo.GetPokemonNames().Length;
            var extras = PokeDatabase.PersonalData.personalExtraFiles;
            if (extraIndex < 0 || extraIndex >= extras.Length) return null;
            return (extras[extraIndex].monId, extras[extraIndex].description);
        }

        // Reverse of ResolvePseudoFormId: not every form has its own main-list entry (Unown letters, Castform weather, etc. don't), so -1 is a normal result.
        private static int ResolveFormPseudoId(int baseId, string formName)
        {
            if (HgEngineProject.IsActive) return -1;
            var extras = PokeDatabase.PersonalData.personalExtraFiles;
            for (int i = 0; i < extras.Length; i++)
                if (extras[i].monId == baseId && FormNameMatchesDescription(formName, extras[i].description))
                    return RomInfo.GetPokemonNames().Length + i;
            return -1;
        }

        // formIndex equals the real form_no (table entries are written in form order); PokeFormDataTbl.c itself is 1-indexed, so form_no 0 is never in it. -1 means hg-engine hasn't touched this form.
        private static int ResolveHgEngineMigratedFormId(int baseId, int formIndex)
        {
            if (formIndex <= 0) return -1;
            var speciesTable = HgEngineSymbolTable.Load("include/constants/species.h");
            if (speciesTable == null || !speciesTable.TryGetNameWithPrefix(baseId, "SPECIES_", out string designator)) return -1;
            if (!HgEngineFormRegistry.LoadAll().TryGetValue(designator, out var slots)) return -1;
            int slotIndex = formIndex - 1;
            if (slotIndex >= slots.Count) return -1;
            return speciesTable.TryGetValue(slots[slotIndex].SpeciesSymbol, out int migratedId) ? migratedId : -1;
        }

        // The species _currentFormData was actually built for. Usually == _currentId, except when viewing a form target directly (Castform Sunny, Mega Venusaur), where it's the family's real base.
        private int _currentFormFamilyBaseId = -1;

        private FormSpriteData[] GetAlternateFormsForCurrentSpecies()
        {
            _currentFormFamilyBaseId = _currentId;
            if (_currentId <= 0) return Array.Empty<FormSpriteData>();
            // Under hg-engine, real species can sit past the raw name archive's length; the padded list resolves those too.
            string[] names = HgEngineProject.IsActive
                ? RomInfo.GetPokemonNamesWithForms(RomInfo.GetPersonalFilesCount())
                : RomInfo.GetPokemonNames();
            if (_currentId >= names.Length) return Array.Empty<FormSpriteData>();

            var matches = BuildFamilyForms(_currentId, names[_currentId], names);
            if (matches.Count == 0 && HgEngineProject.IsActive)
            {
                int? baseId = FindHgEngineFormFamilyBase(_currentId);
                if (baseId.HasValue && baseId.Value != _currentId && baseId.Value < names.Length)
                {
                    var familyMatches = BuildFamilyForms(baseId.Value, names[baseId.Value], names);
                    if (familyMatches.Count > 0) { matches = familyMatches; _currentFormFamilyBaseId = baseId.Value; }
                }
            }
            return matches.ToArray();
        }

        private List<FormSpriteData> BuildFamilyForms(int baseId, string baseName, string[] names)
        {
            var matches = new List<FormSpriteData>();
            foreach (var f in GetFormDataForCurrentGame())
            {
                string prefix = SpeciesNamePrefix(f.Name);
                if (prefix != null && string.Equals(prefix, baseName, StringComparison.OrdinalIgnoreCase))
                    matches.Add(f);
            }
            // Only species outside the vanilla table (Megas, Gigantamax, regional forms, ...) fall here; the vanilla table already wins for the 13 species it covers, so there's no double-listing.
            if (matches.Count == 0 && HgEngineProject.IsActive)
                matches.AddRange(GetHgEngineNativeForms(baseId, baseName, names));
            return matches;
        }

        private static IEnumerable<FormSpriteData> GetHgEngineNativeForms(int baseId, string baseName, string[] names)
        {
            var speciesTable = HgEngineSymbolTable.Load("include/constants/species.h");
            if (speciesTable == null || !speciesTable.TryGetNameWithPrefix(baseId, "SPECIES_", out string designator)) yield break;
            if (!HgEngineFormRegistry.LoadAll().TryGetValue(designator, out var slots) || slots.Count == 0) yield break;

            yield return new FormSpriteData(baseName, baseId);
            foreach (var slot in slots)
            {
                if (speciesTable.TryGetValue(slot.SpeciesSymbol, out int formId) && formId >= 0 && formId < names.Length)
                    yield return new FormSpriteData(names[formId], formId);
            }
        }

        // Reverse of the id resolution GetHgEngineNativeForms/ResolveHgEngineMigratedFormId do: given a
        // species id that's itself a form target, finds which base species' PokeFormDataTbl.c entry lists it.
        private static int? FindHgEngineFormFamilyBase(int id)
        {
            var speciesTable = HgEngineSymbolTable.Load("include/constants/species.h");
            if (speciesTable == null) return null;
            foreach (var kvp in HgEngineFormRegistry.LoadAll())
            {
                if (!speciesTable.TryGetValue(kvp.Key, out int baseId)) continue;
                foreach (var slot in kvp.Value)
                    if (speciesTable.TryGetValue(slot.SpeciesSymbol, out int slotId) && slotId == id)
                        return baseId;
            }
            return null;
        }

        // Populates VariantNames once per LoadMon (not lazily on first dropdown open), so the dropdown never needs to be cleared/repopulated mid-interaction.
        private void PopulateVariantList()
        {
            _currentFormData = HasAlternateForms ? GetAlternateFormsForCurrentSpecies() : Array.Empty<FormSpriteData>();
            // ListSync, not Clear()+Add(): Clear() fires a Reset that crashed the app for real when it ran while the ComboBox was still handling the click that triggered this rebuild.
            DSPRE.Avalonia.Data.ListSync.Apply(VariantNames, _currentFormData.Select(f => f.Name).ToList());
            // -1, not 0: LoadMon always assigns SelectedVariantIndex right after this, and it needs to
            // register as a real change even when the target index is 0, so the form actually loads.
            _selectedVariantIndex = -1;
            OnPropertyChanged(nameof(SelectedVariantIndex));
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
                    rawBmps[1] = backSprite;
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
                ApplyFormGenderGap(rawBmps, _currentId);

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

        // Alt forms only ever store one shared sprite, so a genuinely single-gender base species (Wormadam is female-only, Deoxys/Unown/Rotom/Giratina/Shaymin are genderless) should show that gap here too, matching how the base species view already does via its own real NARC stub sizes.
        private static void ApplyFormGenderGap(byte[][] rawBmps, int baseSpeciesId)
        {
            byte ratio = ReadGenderRatio(baseSpeciesId);
            if (ratio == SpeciesFile.GENDER_RATIO_FEMALE) { rawBmps[1] = null; rawBmps[3] = null; }
            else if (ratio == SpeciesFile.GENDER_RATIO_MALE || ratio == SpeciesFile.GENDER_RATIO_GENDERLESS) { rawBmps[0] = null; rawBmps[2] = null; }
        }

        private static byte ReadGenderRatio(int speciesId)
        {
            try
            {
                string path = Path.Combine(RomInfo.gameDirs[DirNames.personalPokeData].unpackedDir, speciesId.ToString("D4"));
                if (!File.Exists(path)) return 127;
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                return new SpeciesFile(fs).GenderRatioMaleToFemale;
            }
            catch { return 127; }
        }

        // The tables live in AlternateFormSprites so the Graphics window reads the same ones rather
        // than a second copy that could drift away from these.
        private static FormSpriteData[] GetFormDataForCurrentGame() => DSPRE.Avalonia.Data.AlternateFormSprites.ForCurrentGame();





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
                foreach (var cell in AllFrameCells) if (cell.ShowFrameToggle) cell.Frame = 1 - cell.Frame;
            };
            _frameTimer.Start();
        }

        // --- Load --------------------------------------------------------------------
        public void LoadMon(int id)
        {
            ClearBitmaps();
            StatusText = "";
            IsAlternateForms = false;
            FormSharesBaseData = false;

            // Ids past the real Pokédex are pseudo-species (Deoxys/Wormadam/Giratina/Shaymin/Rotom formes); redirect straight to that form.
            var pseudo = ResolvePseudoFormId(id);
            _currentId = pseudo?.baseId ?? id;
            // This applies under hg-engine too: it only intercepts sprite loading for the handful of species
            // in its own PokeFormDataTbl.c (Castform/Cherrim/Shellos/Gastrodon's newest forms); everything
            // else, including form 0, still falls through to vanilla otherpoke.narc unchanged.
            HasAlternateForms = _currentId > 0 && GetAlternateFormsForCurrentSpecies().Length > 0;
            PopulateVariantList();

            if (pseudo.HasValue)
            {
                int formIdx = Array.FindIndex(_currentFormData, f => FormNameMatchesDescription(f.Name, pseudo.Value.description));
                if (formIdx >= 0) { SelectedVariantIndex = formIdx; return; }
                StatusText = "This form doesn't have its own sprite data.";
                return;
            }

            // Species with form-table entries also store their default form there, not in the main NARC (confirmed against real GameFreak source, PokeGraArcDataGet in poke_tool.c).
            // If the loaded species already IS one of the entries (viewing Castform Sunny or Mega Venusaur directly), select it in place instead of redirecting back into itself.
            int selfIndex = -1;
            for (int i = 0; i < _currentFormData.Length; i++)
            {
                if (_currentFormData[i].HgEngineSpeciesId == _currentId) { selfIndex = i; break; }
                if (HgEngineProject.IsActive && ResolveHgEngineMigratedFormId(_currentFormFamilyBaseId, i) == _currentId) { selfIndex = i; break; }
            }
            if (selfIndex >= 0)
            {
                _selectedVariantIndex = selfIndex;
                OnPropertyChanged(nameof(SelectedVariantIndex));
            }
            else if (_currentFormData.Length > 0)
            {
                SelectedVariantIndex = 0;
                return;
            }

            if (id <= 0)
            {
                StatusText = "No Pokémon selected.";
                return;
            }

            if (HgEngineProject.IsActive && LoadMonFromHgEngineSource(id)) return;

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

        // Reads hg-engine's own source PNGs instead of pokegra.narc. Front is Normal palette, back is Shiny, taken from whichever gender's poses are the real ones; the absent gender's placeholder file is skipped, not read.
        private bool LoadMonFromHgEngineSource(int id)
        {
            string[] posePaths = HgEnginePokemonBattleSprites.TryGetPosePaths(id);
            if (posePaths == null) return false;
            _loadedFromHgEngine = true;

            try
            {
                byte ratio = ReadGenderRatio(id);
                bool skipFemale = ratio == SpeciesFile.GENDER_RATIO_MALE || ratio == SpeciesFile.GENDER_RATIO_GENDERLESS;
                bool skipMale = ratio == SpeciesFile.GENDER_RATIO_FEMALE;
                int normalSlot = skipMale ? 2 : 3;   // Front: Female or Male
                int shinySlot  = skipMale ? 0 : 1;   // Back: Female or Male

                var rawBmps = new byte[4][];
                var hasRealSprite = new bool[4];
                uint[] normalPal = null, shinyPal = null;
                for (int i = 0; i < 4; i++)
                {
                    bool isFemaleSlot = i == 0 || i == 2;
                    if ((isFemaleSlot && skipFemale) || (!isFemaleSlot && skipMale)) continue;

                    byte[] pngBytes = File.ReadAllBytes(posePaths[i]);
                    if (!IndexedPng.TryRead(pngBytes, out byte[] indices, out uint[] pal, out int w, out int h) ||
                        w != SpriteWidth || h != SpriteHeight || pal.Length > 16)
                    {
                        StatusText = $"{SpriteLabels[i]}'s hg-engine sprite isn't a {SpriteWidth}×{SpriteHeight} 16-color indexed PNG.";
                        return true;
                    }
                    rawBmps[i] = indices;
                    hasRealSprite[i] = true;
                    if (i == normalSlot) normalPal = Pad16(pal);
                    if (i == shinySlot) shinyPal = Pad16(pal);
                }

                UpdateOppositeGenderGap(hasRealSprite);
                _rawSprites = rawBmps;
                _normalPal = normalPal;
                _shinyPal = shinyPal ?? normalPal;
                _normalPalUsed = AllUsed();
                _shinyPalUsed = AllUsed();
                ApplyPalettesAndPublish();
                _dirty = false;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = "Loaded from hg-engine source.";
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading hg-engine sprites: {ex.Message}";
            }
            return true;
        }

        private static uint[] Pad16(uint[] palette)
        {
            if (palette.Length == 16) return palette;
            var padded = new uint[16];
            Array.Copy(palette, padded, palette.Length);
            for (int i = palette.Length; i < 16; i++) padded[i] = 0xFF000000u;
            return padded;
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
                byte[] fileBytes = File.ReadAllBytes(path);
                RawImage imported;
                using (var ms = new MemoryStream(fileBytes))
                    imported = ImageConverter.DecodeRawImage(ms);
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

                if (_rawSprites[slot] != null && TryDeriveRecolorPalette(_rawSprites[slot], imported, out uint[] recolorPal, out bool[] recolorUsed))
                {
                    uint[] pal = _normalPal ??= new uint[16];
                    for (int i = 0; i < 16; i++) if (recolorUsed[i]) { pal[i] = recolorPal[i]; _normalPalUsed[i] = true; }
                    ApplyPalettesAndPublish();
                    _dirty = true;
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                    StatusText = $"{SpriteLabels[slot]}'s artwork didn't change, just the colors, so only the palette was updated.";
                    return;
                }

                if (!TryReadIndexedOrQuantize(fileBytes, imported, out byte[] newIndices, out uint[] newPalette, out int usedCount))
                {
                    StatusText = "This image has more than 16 colors. Reduce it to 16 or fewer and try again.";
                    return;
                }

                bool[] newUsed = MakeUsedMask(usedCount);

                if (_normalPal != null && !PaletteEqualsUpTo(_normalPal, newPalette, usedCount))
                {
                    bool keepExisting = await DialogHelper.AskYesNo(
                        $"{SpriteLabels[slot]}'s image uses different colors than the palette already saved for this sprite.\n\n" +
                        "Keep the saved palette and match this image's colors to it? Choosing No replaces the saved palette with this image's own colors instead, which affects every other sprite using it too.",
                        "Palette mismatch", owner);
                    if (keepExisting)
                    {
                        if (!await ConfirmOverwriteIfNeeded(newPalette, usedCount, _normalPal, _normalPalUsed, owner))
                        {
                            StatusText = "Import cancelled.";
                            return;
                        }
                        newIndices = RemapToExistingPalette(newIndices, newPalette, usedCount, _normalPal, _normalPalUsed, out uint[] merged, out bool[] mergedUsed);
                        _normalPal = merged;
                        _normalPalUsed = mergedUsed;
                    }
                    else
                    {
                        if (!await ConfirmFullPaletteReplace(owner))
                        {
                            StatusText = "Import cancelled.";
                            return;
                        }
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
                byte[] fileBytes = File.ReadAllBytes(path);
                RawImage imported;
                using (var ms = new MemoryStream(fileBytes))
                    imported = ImageConverter.DecodeRawImage(ms);
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
                if (!TryReadIndexedOrQuantize(fileBytes, imported, out byte[] childIndices, out uint[] childPalette, out _))
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

                // Fill in only the slots this pose's image actually shows; never blank out a color a different pose already found.
                uint[] pal = shiny ? (_shinyPal ??= new uint[16]) : (_normalPal ??= new uint[16]);
                bool[] used = shiny ? _shinyPalUsed : _normalPalUsed;
                for (int i = 0; i < 16; i++)
                {
                    if (derivedUsed[i]) { pal[i] = derived[i]; used[i] = true; }
                }
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
                    "Palette isn't full", owner);
                if (fillBlack)
                {
                    for (int i = 0; i < 16; i++) used[i] = true;
                    RefreshSwatches(shiny ? ShinySwatches : NormalSwatches, shiny ? _shinyPal : _normalPal, used, shiny);
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
                byte[] indices = _rawSprites[slot];
                uint[] pal = shiny ? _shinyPal : _normalPal;
                if (indices == null || pal == null) { StatusText = "Export failed: nothing to export."; return; }
                File.WriteAllBytes(path, IndexedPng.Write(indices, pal, SpriteWidth, SpriteHeight));
                StatusText = $"Exported {SpriteLabels[slot]}{(shiny ? " (Shiny)" : "")}.";
            }
            catch (Exception ex) { StatusText = $"Export failed: {ex.Message}"; }
        }

        // --- Sprite sheet export/import ------------------------------------------------

        // A sheet is just the given slots' raw arrays (each already both frames, 160 wide) concatenated side by side.
        private static (int backSlot, int frontSlot) GenderSlots(bool female) => female ? (0, 2) : (1, 3);

        private byte[] BuildSheetIndices(params int[] slots)
        {
            foreach (int s in slots) if (_rawSprites[s] == null) return null;
            var sheet = new byte[SpriteWidth * slots.Length * SpriteHeight];
            for (int y = 0; y < SpriteHeight; y++)
                for (int i = 0; i < slots.Length; i++)
                    Array.Copy(_rawSprites[slots[i]], y * SpriteWidth, sheet, y * SpriteWidth * slots.Length + i * SpriteWidth, SpriteWidth);
            return sheet;
        }

        private void WriteSheetIndices(byte[] sheetIndices, params int[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var arr = new byte[SpriteWidth * SpriteHeight];
                for (int y = 0; y < SpriteHeight; y++)
                    Array.Copy(sheetIndices, y * SpriteWidth * slots.Length + i * SpriteWidth, arr, y * SpriteWidth, SpriteWidth);
                _rawSprites[slots[i]] = arr;
            }
        }

        private byte[] BuildGenderSheetIndices(bool female) { var (b, f) = GenderSlots(female); return BuildSheetIndices(b, f); }
        private void WriteGenderSheetIndices(bool female, byte[] sheetIndices) { var (b, f) = GenderSlots(female); WriteSheetIndices(sheetIndices, b, f); }

        // Both genders together (Female Back, Male Back, Female Front, Male Front), for editing a species with real gender differences in one pass.
        private byte[] BuildFullSheetIndices() => BuildSheetIndices(0, 1, 2, 3);
        private void WriteFullSheetIndices(byte[] sheetIndices) => WriteSheetIndices(sheetIndices, 0, 1, 2, 3);

        /// <summary>True only when this species genuinely has separate Male and Female sprites (not the mono-gender/genderless placeholder case), so combining both into one sheet actually makes sense.</summary>
        public bool CanUseFullSheet => _rawSprites[0] != null && _rawSprites[1] != null && _rawSprites[2] != null && _rawSprites[3] != null && !CanAddOppositeGenderSprites && !IsAlternateForms;

        public async Task ExportSpriteSheet(Window owner, bool female, bool shiny = false)
        {
            uint[] pal = shiny ? _shinyPal : _normalPal;
            if (pal == null) { StatusText = "Load a Pokémon first."; return; }
            string genderLabel = female ? "Female" : "Male";
            string colorLabel = shiny ? "Shiny" : "Normal";
            string path = await DialogHelper.SaveFile(owner, $"Export {genderLabel} {colorLabel} Sprite Sheet",
                new[] { DialogHelper.PngFilter, DialogHelper.AllFilter },
                $"mon{_currentId:D3}_{genderLabel}_{colorLabel}_sheet.png");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                byte[] indices = BuildGenderSheetIndices(female);
                if (indices == null) { StatusText = "Export failed: nothing to export."; return; }
                File.WriteAllBytes(path, IndexedPng.Write(indices, pal, SpriteWidth * 2, SpriteHeight));
                StatusText = $"Exported {genderLabel}'s {colorLabel.ToLowerInvariant()} sprite sheet: Back and Front, both frames.";
            }
            catch (Exception ex) { StatusText = $"Export failed: {ex.Message}"; }
        }

        /// <summary>Both genders' Back and Front together in one sheet, for species with real male/female differences. Only makes sense when both genders actually exist (see CanUseFullSheet).</summary>
        public async Task ExportFullSheet(Window owner, bool shiny = false)
        {
            uint[] pal = shiny ? _shinyPal : _normalPal;
            if (pal == null) { StatusText = "Load a Pokémon first."; return; }
            string colorLabel = shiny ? "Shiny" : "Normal";
            string path = await DialogHelper.SaveFile(owner, $"Export {colorLabel} Sprite Sheet (Both Genders)",
                new[] { DialogHelper.PngFilter, DialogHelper.AllFilter },
                $"mon{_currentId:D3}_Both_{colorLabel}_sheet.png");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                byte[] indices = BuildFullSheetIndices();
                if (indices == null) { StatusText = "Export failed: nothing to export."; return; }
                File.WriteAllBytes(path, IndexedPng.Write(indices, pal, SpriteWidth * 4, SpriteHeight));
                StatusText = $"Exported both genders' {colorLabel.ToLowerInvariant()} sprite sheet: Back and Front, both frames.";
            }
            catch (Exception ex) { StatusText = $"Export failed: {ex.Message}"; }
        }

        private async Task<(RawImage image, byte[] fileBytes)> OpenGenderSheet(Window owner, string title) =>
            await OpenSheetFile(owner, title, 2, "Back on the left, Front on the right, each with both animation frames");

        private async Task<(RawImage image, byte[] fileBytes)> OpenFullSheet(Window owner, string title) =>
            await OpenSheetFile(owner, title, 4, "Female Back, Male Back, Female Front, Male Front left to right, each with both animation frames");

        private async Task<(RawImage image, byte[] fileBytes)> OpenSheetFile(Window owner, string title, int poseCount, string layoutHint)
        {
            string path = await DialogHelper.OpenFile(owner, title, new[] { DialogHelper.PngFilter, DialogHelper.AllFilter });
            if (string.IsNullOrEmpty(path)) return (null, null);
            byte[] fileBytes = File.ReadAllBytes(path);
            RawImage imported;
            using (var ms = new MemoryStream(fileBytes))
                imported = ImageConverter.DecodeRawImage(ms);
            if (imported == null) { StatusText = "Image could not be decoded."; return (null, null); }
            int expectedWidth = SpriteWidth * poseCount;
            if (imported.Width != expectedWidth || imported.Height != SpriteHeight)
            {
                StatusText = $"Sprite sheet must be {expectedWidth}×{SpriteHeight} pixels: {layoutHint} (got {imported.Width}×{imported.Height}).";
                return (null, null);
            }
            return (imported, fileBytes);
        }

        public async Task ImportSpriteSheet(Window owner, bool female)
        {
            string genderLabel = female ? "Female" : "Male";
            try
            {
                var (imported, fileBytes) = await OpenGenderSheet(owner, $"Import {genderLabel} Sprite Sheet");
                if (imported == null) return;

                byte[] oldSheetIndices = BuildGenderSheetIndices(female);
                if (oldSheetIndices != null && TryDeriveRecolorPalette(oldSheetIndices, imported, out uint[] recolorPal, out bool[] recolorUsed))
                {
                    uint[] pal = _normalPal ??= new uint[16];
                    for (int i = 0; i < 16; i++) if (recolorUsed[i]) { pal[i] = recolorPal[i]; _normalPalUsed[i] = true; }
                    ApplyPalettesAndPublish();
                    _dirty = true;
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                    StatusText = $"{genderLabel}'s artwork didn't change, just the colors, so only the palette was updated.";
                    return;
                }

                if (!TryReadIndexedOrQuantize(fileBytes, imported, out byte[] newIndices, out uint[] newPalette, out int usedCount))
                {
                    StatusText = "This image has more than 16 colors. Reduce it to 16 or fewer and try again.";
                    return;
                }

                bool[] newUsed = MakeUsedMask(usedCount);
                if (_normalPal != null && !PaletteEqualsUpTo(_normalPal, newPalette, usedCount))
                {
                    bool keepExisting = await DialogHelper.AskYesNo(
                        $"This sheet uses different colors than the palette already saved for {genderLabel}.\n\n" +
                        "Keep the saved palette and match the sheet's colors to it? Choosing No replaces the saved palette with the sheet's own colors instead, which affects every other sprite using it too.",
                        "Palette mismatch", owner);
                    if (keepExisting)
                    {
                        if (!await ConfirmOverwriteIfNeeded(newPalette, usedCount, _normalPal, _normalPalUsed, owner))
                        {
                            StatusText = "Import cancelled.";
                            return;
                        }
                        newIndices = RemapToExistingPalette(newIndices, newPalette, usedCount, _normalPal, _normalPalUsed, out uint[] merged, out bool[] mergedUsed);
                        _normalPal = merged;
                        _normalPalUsed = mergedUsed;
                    }
                    else
                    {
                        if (!await ConfirmFullPaletteReplace(owner))
                        {
                            StatusText = "Import cancelled.";
                            return;
                        }
                        _normalPal = newPalette;
                        _normalPalUsed = newUsed;
                    }
                }
                else if (_normalPal == null)
                {
                    _normalPal = newPalette;
                    _normalPalUsed = newUsed;
                }

                WriteGenderSheetIndices(female, newIndices);
                ApplyPalettesAndPublish();
                _dirty = true;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = $"Imported {genderLabel}'s Back and Front from the sheet. Save to write it to the ROM.";
            }
            catch (Exception ex) { StatusText = $"Import failed: {ex.Message}"; }
        }

        /// <summary>Gets the shiny palette from a sheet; doesn't touch pixels, since shiny shares artwork with Normal.</summary>
        public async Task ImportShinySpriteSheet(Window owner, bool female)
        {
            string genderLabel = female ? "Female" : "Male";
            try
            {
                byte[] parentIndices = BuildGenderSheetIndices(female);
                if (parentIndices == null)
                {
                    StatusText = $"Import {genderLabel}'s Back and Front artwork first, then you can get the shiny palette from a reference sheet.";
                    return;
                }
                var (imported, fileBytes) = await OpenGenderSheet(owner, $"Import {genderLabel} Shiny Reference Sheet");
                if (imported == null) return;
                if (!TryReadIndexedOrQuantize(fileBytes, imported, out byte[] childIndices, out uint[] childPalette, out _))
                {
                    StatusText = "This image has more than 16 colors. Reduce it to 16 or fewer and try again.";
                    return;
                }

                uint[] derived = DeriveAlternatePalette(parentIndices, childIndices, childPalette, out bool[] derivedUsed);
                if (derived == null) { StatusText = "Could not get a shiny palette from this image."; return; }

                uint[] pal = _shinyPal ??= new uint[16];
                for (int i = 0; i < 16; i++)
                {
                    if (derivedUsed[i]) { pal[i] = derived[i]; _shinyPalUsed[i] = true; }
                }
                ApplyPalettesAndPublish();
                _dirty = true;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = $"Got {genderLabel}'s shiny palette from the reference sheet. Save to write it to the ROM.";
            }
            catch (Exception ex) { StatusText = $"Import failed: {ex.Message}"; }
        }

        /// <summary>Imports both genders' artwork at once from one sheet (see ExportFullSheet).</summary>
        public async Task ImportFullSheet(Window owner)
        {
            try
            {
                var (imported, fileBytes) = await OpenFullSheet(owner, "Import Sprite Sheet (Both Genders)");
                if (imported == null) return;

                byte[] oldSheetIndices = BuildFullSheetIndices();
                if (oldSheetIndices != null && TryDeriveRecolorPalette(oldSheetIndices, imported, out uint[] recolorPal, out bool[] recolorUsed))
                {
                    uint[] pal = _normalPal ??= new uint[16];
                    for (int i = 0; i < 16; i++) if (recolorUsed[i]) { pal[i] = recolorPal[i]; _normalPalUsed[i] = true; }
                    ApplyPalettesAndPublish();
                    _dirty = true;
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                    StatusText = "The artwork didn't change, just the colors, so only the palette was updated.";
                    return;
                }

                if (!TryReadIndexedOrQuantize(fileBytes, imported, out byte[] newIndices, out uint[] newPalette, out int usedCount))
                {
                    StatusText = "This image has more than 16 colors. Reduce it to 16 or fewer and try again.";
                    return;
                }

                bool[] newUsed = MakeUsedMask(usedCount);
                if (_normalPal != null && !PaletteEqualsUpTo(_normalPal, newPalette, usedCount))
                {
                    bool keepExisting = await DialogHelper.AskYesNo(
                        "This sheet uses different colors than the palette already saved.\n\n" +
                        "Keep the saved palette and match the sheet's colors to it? Choosing No replaces the saved palette with the sheet's own colors instead, which affects every other sprite using it too.",
                        "Palette mismatch", owner);
                    if (keepExisting)
                    {
                        if (!await ConfirmOverwriteIfNeeded(newPalette, usedCount, _normalPal, _normalPalUsed, owner))
                        {
                            StatusText = "Import cancelled.";
                            return;
                        }
                        newIndices = RemapToExistingPalette(newIndices, newPalette, usedCount, _normalPal, _normalPalUsed, out uint[] merged, out bool[] mergedUsed);
                        _normalPal = merged;
                        _normalPalUsed = mergedUsed;
                    }
                    else
                    {
                        if (!await ConfirmFullPaletteReplace(owner))
                        {
                            StatusText = "Import cancelled.";
                            return;
                        }
                        _normalPal = newPalette;
                        _normalPalUsed = newUsed;
                    }
                }
                else if (_normalPal == null)
                {
                    _normalPal = newPalette;
                    _normalPalUsed = newUsed;
                }

                WriteFullSheetIndices(newIndices);
                ApplyPalettesAndPublish();
                _dirty = true;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = "Imported both genders' Back and Front from the sheet. Save to write it to the ROM.";
            }
            catch (Exception ex) { StatusText = $"Import failed: {ex.Message}"; }
        }

        /// <summary>Gets the shiny palette from a both-genders sheet; doesn't touch pixels (see ImportShinySpriteSheet).</summary>
        public async Task ImportShinyFullSheet(Window owner)
        {
            try
            {
                byte[] parentIndices = BuildFullSheetIndices();
                if (parentIndices == null)
                {
                    StatusText = "Import both genders' Back and Front artwork first, then you can get the shiny palette from a reference sheet.";
                    return;
                }
                var (imported, fileBytes) = await OpenFullSheet(owner, "Import Shiny Reference Sheet (Both Genders)");
                if (imported == null) return;
                if (!TryReadIndexedOrQuantize(fileBytes, imported, out byte[] childIndices, out uint[] childPalette, out _))
                {
                    StatusText = "This image has more than 16 colors. Reduce it to 16 or fewer and try again.";
                    return;
                }

                uint[] derived = DeriveAlternatePalette(parentIndices, childIndices, childPalette, out bool[] derivedUsed);
                if (derived == null) { StatusText = "Could not get a shiny palette from this image."; return; }

                uint[] pal = _shinyPal ??= new uint[16];
                for (int i = 0; i < 16; i++)
                {
                    if (derivedUsed[i]) { pal[i] = derived[i]; _shinyPalUsed[i] = true; }
                }
                ApplyPalettesAndPublish();
                _dirty = true;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = "Got both genders' shiny palette from the reference sheet. Save to write it to the ROM.";
            }
            catch (Exception ex) { StatusText = $"Import failed: {ex.Message}"; }
        }

        // --- Helpers -----------------------------------------------------------------
        private static void RefreshSwatches(ObservableCollection<PaletteSwatch> target, uint[] palette, bool[] used, bool shiny)
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
                    IsPlaceholder = used != null && i < used.Length && !used[i],
                    Index = i,
                    Shiny = shiny
                });
            }
        }

        private void ApplyPalettesAndPublish()
        {
            if (_normalPal == null) return;

            RefreshSwatches(NormalSwatches, _normalPal, _normalPalUsed, false);
            RefreshSwatches(ShinySwatches, _shinyPal, _shinyPalUsed, true);

            UpdateFrameAvailability();
            RenderCurrentFrameForAllCells();

            // Battle-mock sprites per gender: a LIST of N 80×80 frames (N = sheet width / 80). The pattern
            // animation (pokeanm) picks which frame to show; the count drives the editor's Frame limit.
            int frontW = SlotWidth(3) != 0 ? SlotWidth(3) : SlotWidth(2);
            BattleFrameCount = frontW != 0 ? Math.Max(1, frontW / 80) : 2;
            BattleFrontM = RenderFrames(3, _normalPal, BattleFrameCount);
            BattleFrontF = RenderFrames(2, _normalPal, BattleFrameCount);
            BattleBackM  = RenderFrames(1, _normalPal, BattleFrameCount);
            BattleBackF  = RenderFrames(0, _normalPal, BattleFrameCount);
            OnPropertyChanged(nameof(CanUseFullSheet));
            OnPropertyChanged(nameof(IsHgEngineSourced));
            OnPropertyChanged(nameof(ShowShinyFullSheetImport));
            OnPropertyChanged(nameof(ShowFemaleFormImport));
            OnPropertyChanged(nameof(ShowFemaleShinyFormImport));
        }

        // Some real ROM sprites only ever had one frame drawn (Deoxys's back sprite, several Unown letters); the rest is zero-filled padding, not a real second pose.
        private void UpdateFrameAvailability()
        {
            for (int slot = 0; slot < 4; slot++)
            {
                byte[] indices = _rawSprites[slot];
                bool hasFrame1 = indices != null && !IsFrameBlank(indices, 0);
                bool hasFrame2 = indices != null && !IsFrameBlank(indices, 1);
                if (!hasFrame1 && !hasFrame2) hasFrame1 = true; // nothing loaded at all: don't hide both toggle buttons
                FrameCellForSlot(slot, false).SetFrameAvailability(hasFrame1, hasFrame2);
                FrameCellForSlot(slot, true).SetFrameAvailability(hasFrame1, hasFrame2);
            }
        }

        private FrameCellState FrameCellForSlot(int slot, bool shiny) => slot switch
        {
            0 => shiny ? FemaleBackShinyFrame  : FemaleBackNormalFrame,
            1 => shiny ? MaleBackShinyFrame    : MaleBackNormalFrame,
            2 => shiny ? FemaleFrontShinyFrame : FemaleFrontNormalFrame,
            3 => shiny ? MaleFrontShinyFrame   : MaleFrontNormalFrame,
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };

        private static bool IsFrameBlank(byte[] indices, int frame)
        {
            const int fw = 80, h = 80, srcW = 160;
            int x0 = frame * fw;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < fw; x++)
                    if (indices[y * srcW + x0 + x] != 0) return false;
            return true;
        }

        // Live palette edit from the swatch color picker; writes straight into the same array every import path already mutates, so Save persists it exactly like an imported palette.
        public void SetPaletteColor(bool shiny, int index, uint argb)
        {
            uint[] pal = shiny ? _shinyPal : _normalPal;
            if (pal == null || index < 0 || index >= pal.Length) return;
            pal[index] = argb;
            ApplyPalettesAndPublish();
            _dirty = true;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private bool HasSlot(int slot) => _rawSprites[slot] != null;

        // --- Export Wizard support -----------------------------------------------------
        public int CurrentId => _currentId;
        public bool HasSpriteSlot(int slot) => HasSlot(slot);
        public bool HasPalette(bool shiny) => (shiny ? _shinyPal : _normalPal) != null;
        public string SpritePoseLabel(int slot) => SpriteImportWizardViewModel.AllPoses[slot];
        public byte[] GetRawSpriteIndices(int slot) => _rawSprites[slot];
        public byte[] GetRawSheetIndices(bool female) => BuildGenderSheetIndices(female);
        public byte[] GetRawFullSheetIndices() => BuildFullSheetIndices();
        public uint[] GetPalette(bool shiny) => shiny ? _shinyPal : _normalPal;
        public int SpritePixelWidth => SpriteWidth;
        public int SpritePixelHeight => SpriteHeight;
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
            _loadedFromHgEngine = false;
            OnPropertyChanged(nameof(IsHgEngineSourced));
            OnPropertyChanged(nameof(ShowShinyFullSheetImport));
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

            if (_isAlternateForms) { SaveAlternateForm(); return; }

            if (HgEngineProject.IsActive && SaveToHgEngineSource()) return;

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

        // Writes back into otherPokemonBattleSprites at this form's own indices; Male Back/Front (_rawSprites[1]/[3]) are the only slots actually persisted, matching ShowFemaleFormImport.
        private void SaveAlternateForm()
        {
            if (_currentFormData == null || _selectedFormIndex < 0 || _selectedFormIndex >= _currentFormData.Length) return;
            var form = _currentFormData[_selectedFormIndex];

            string packedPath = RomInfo.gameDirs[DirNames.otherPokemonBattleSprites].packedDir;
            if (!File.Exists(packedPath))
            {
                StatusText = "Alternate forms NARC not found. Make sure the ROM is loaded.";
                return;
            }

            var narc = new NarcReader(packedPath);
            WriteFormSprite(narc, form.BackSpriteIndex, _rawSprites[1]);
            WriteFormSprite(narc, form.FrontSpriteIndex, _rawSprites[3]);
            WriteFormPalette(narc, form.NormalPaletteIndex, _normalPal);
            WriteFormPalette(narc, form.ShinyPaletteIndex, _shinyPal);

            _dirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            StatusText = "Saved.";
        }

        private static void WriteFormSprite(NarcReader narc, int idx, byte[] indices)
        {
            if (indices == null || idx < 0 || idx >= narc.fe.Length || narc.fe[idx].Size != 6448) return;
            narc.OpenEntry(idx);
            WriteSpriteEntry(narc.fs, indices);
            narc.Close();
        }

        private static void WriteFormPalette(NarcReader narc, int idx, uint[] palette)
        {
            if (palette == null || idx < 0 || idx >= narc.fe.Length || narc.fe[idx].Size != 72) return;
            narc.OpenEntry(idx);
            WritePaletteEntry(narc.fs, palette);
            narc.Close();
        }

        // Writes straight to hg-engine's source PNGs (front = Normal palette, back = Shiny), mirroring LoadMonFromHgEngineSource, so the edit survives the next make.
        private bool SaveToHgEngineSource()
        {
            string[] posePaths = HgEnginePokemonBattleSprites.TryGetPosePaths(_currentId);
            if (posePaths == null) return false;

            try
            {
                for (int i = 0; i < 4; i++)
                {
                    if (_rawSprites[i] == null) continue;
                    uint[] pal = (i == 0 || i == 1) ? _shinyPal : _normalPal;
                    if (pal == null) continue;
                    byte[] png = IndexedPng.Write(_rawSprites[i], pal, SpriteWidth, SpriteHeight);
                    File.WriteAllBytes(posePaths[i], png);
                }
                _dirty = false;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = "Saved to hg-engine source.";
            }
            catch (Exception ex)
            {
                StatusText = $"Error saving hg-engine sprites: {ex.Message}";
            }
            return true;
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

        // Prefers a PNG's own real embedded palette order (when it genuinely has one) over re-deriving one via first-seen-color scanning, which can scramble a deliberately-authored index order.
        private static bool TryReadIndexedOrQuantize(byte[] fileBytes, RawImage decoded, out byte[] indices, out uint[] palette, out int usedCount)
        {
            if (IndexedPng.TryRead(fileBytes, out byte[] realIndices, out uint[] realPalette, out int w, out int h) &&
                w == decoded.Width && h == decoded.Height && realPalette.Length <= 16)
            {
                indices = realIndices;
                usedCount = realPalette.Length;
                palette = Pad16(realPalette);
                return true;
            }
            return TryReadImageColors(decoded, out indices, out palette, out usedCount);
        }

        /// <summary>Builds a color index (0-15) per pixel plus the list of colors used, in the order they first appear. Fails past 16 distinct colors, the ROM format's own limit.</summary>
        private static bool TryReadImageColors(RawImage img, out byte[] indices, out uint[] palette, out int usedCount)
        {
            int n = img.Width * img.Height;
            indices = new byte[n];
            palette = new uint[16];
            usedCount = 0;

            var seen = new System.Collections.Generic.Dictionary<uint, byte>();
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

        // Same old index showing two different colors here means the artwork's shape changed, not just its colors.
        private static bool TryDeriveRecolorPalette(byte[] oldIndices, RawImage newImg, out uint[] palette, out bool[] used)
        {
            palette = new uint[16];
            used = new bool[16];
            if (oldIndices == null || newImg == null || oldIndices.Length != newImg.Width * newImg.Height) return false;
            for (int p = 0; p < oldIndices.Length; p++)
            {
                int idx = oldIndices[p] & 0xF;
                int o = p * 4;
                uint c = 0xFF000000u | ((uint)newImg.Bgra[o + 2] << 16) | ((uint)newImg.Bgra[o + 1] << 8) | newImg.Bgra[o];
                if (!used[idx]) { palette[idx] = c; used[idx] = true; }
                else if (palette[idx] != c) return false; // same old index shows two colors -> real shape change
            }
            return true;
        }

        // Colors here with no exact match in the existing palette would need a free slot to be added.
        private static int CountUnmatchedColors(uint[] newPalette, int usedCount, uint[] existingPalette)
        {
            int unmatched = 0;
            for (int i = 0; i < usedCount; i++)
            {
                bool found = false;
                for (int j = 0; j < 16; j++) if (existingPalette[j] == newPalette[i]) { found = true; break; }
                if (!found) unmatched++;
            }
            return unmatched;
        }

        private static int CountFreeSlots(bool[] used)
        {
            int free = 0;
            foreach (bool u in used) if (!u) free++;
            return free;
        }

        // Always this destructive (one shared palette, every pose), so this always asks, no threshold.
        private async Task<bool> ConfirmFullPaletteReplace(Window owner)
        {
            return await DialogHelper.AskYesNo(
                "Replacing the palette changes every Normal sprite that uses it, not just this one, " +
                "and can also change how the shiny sprites look since they're drawn from the same artwork. Continue anyway?",
                "This will change other sprites too", owner);
        }

        // Only warns when there isn't enough free room; filling genuine placeholders alone is always safe.
        private async Task<bool> ConfirmOverwriteIfNeeded(uint[] newPalette, int usedCount, uint[] existingPalette, bool[] existingUsed, Window owner)
        {
            int unmatched = CountUnmatchedColors(newPalette, usedCount, existingPalette);
            int free = CountFreeSlots(existingUsed);
            if (unmatched <= free) return true;
            int overwrite = unmatched - free;
            return await DialogHelper.AskYesNo(
                $"This image has {overwrite} color(s) that don't match the saved palette and won't fit in the free slots.\n\n" +
                "Continuing will overwrite existing colors to make room, which can also change the shiny sprites since they're drawn from this same artwork. Continue anyway?",
                "This will overwrite existing colors", owner);
        }

        // Matches colors by value first; a color with no match takes a genuine placeholder slot, never a used one, if any remain.
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
