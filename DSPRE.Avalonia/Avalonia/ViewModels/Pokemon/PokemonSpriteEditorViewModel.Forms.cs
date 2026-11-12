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
    public partial class PokemonSpriteEditorViewModel
    {


        private bool _isAlternateForms = false;
        public bool IsAlternateForms { get => _isAlternateForms; private set => Set(ref _isAlternateForms, value); }

        private int _selectedFormIndex = 0;
        public int SelectedFormIndex { get => _selectedFormIndex; private set => Set(ref _selectedFormIndex, value); }

        /// <summary>All graphics opens here: the form's back sprite, or the species' first file.</summary>
        public (DirNames Dir, int Member) GraphicsTarget =>
            IsAlternateForms && TryGetCurrentFormHeightIndices(out int back, out _)
                ? (DirNames.OtherPokemonBattleSprites, back)
                : (DirNames.PokemonBattleSprites, _currentId * 6);

        /// <summary>The form's back and front sprite indices, also its height_o.narc indices.</summary>
        public bool TryGetCurrentFormHeightIndices(out int backIndex, out int frontIndex)
        {
            backIndex = frontIndex = -1;
            if (_currentFormData == null || _selectedFormIndex < 0 || _selectedFormIndex >= _currentFormData.Length)
                return false;
            FormSpriteData f = _currentFormData[_selectedFormIndex];
            if (f.BackSpriteIndex < 0 || f.FrontSpriteIndex < 0) return false; // hg-engine-native entries have no vanilla index
            backIndex = f.BackSpriteIndex;
            frontIndex = f.FrontSpriteIndex;
            return true;
        }

        public ObservableCollection<string> VariantNames { get; } = new();   // the first is the base sprite itself

        /// <summary>Fires with the species list id to show for the picked form, or the base id.</summary>
        public event Action<int> FormPseudoIdSelected;

        private int _selectedVariantIndex = 0;
        public int SelectedVariantIndex
        {
            get => _selectedVariantIndex;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (value == _selectedVariantIndex) return;
                if (HasUnsavedChanges && value >= 0 && _selectedVariantIndex >= 0)   // LoadMon sets -1 first, so this is a user pick
                {
                    RecordSwitchGuard.SnapBack(() => _selectedVariantIndex, v => _selectedVariantIndex = v, () => OnPropertyChanged(nameof(SelectedVariantIndex)));
                    _ = SwitchVariantAsync(value);
                    return;
                }
                ApplyVariant(value);
            }
        }

        private async Task SwitchVariantAsync(int requested)
        {
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, null, "form")) return;
            ApplyVariant(requested);
        }

        private void ApplyVariant(int value)
        {
            if (!Set(ref _selectedVariantIndex, value, nameof(SelectedVariantIndex))) return;
            if (_currentFormData == null || value < 0 || value >= _currentFormData.Length) return;

            int nativeId = _currentFormData[value].HgEngineSpeciesId;   // a form hg-engine added has no otherpoke entry, so its species is opened
            if (nativeId >= 0)
            {
                if (nativeId != _currentId) JumpToSpecies(nativeId);
                return;
            }

            int migratedId = HgEngineProject.IsActive ? ResolveHgEngineMigratedFormId(_currentFormFamilyBaseId, value) : -1;   // hg-engine may have moved this form to its own species, leaving the otherpoke entry dead
            if (migratedId >= 0)
            {
                JumpToSpecies(migratedId);
                return;
            }

            IsAlternateForms = true;
            SelectedFormIndex = value;
            LoadAlternateForm(value);
            int pseudoId = ResolveFormPseudoId(_currentId, _currentFormData[value].Name);
            FormSharesBaseData = value > 0 && pseudoId < 0;   // index 0 is the default form with the base stats, where the note only confuses
            FormPseudoIdSelected?.Invoke(pseudoId >= 0 ? pseudoId : _currentId);
        }

        // Runs inside a ComboBox click still resolving the old VariantNames; a sync rebuild crashes.
        private void JumpToSpecies(int id) => global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            FormPseudoIdSelected?.Invoke(id);
            LoadMon(id);
        });

        private bool _formSharesBaseData;
        /// <summary>True when the form has no species list entry, so it uses the base data.</summary>
        public bool FormSharesBaseData { get => _formSharesBaseData; private set => Set(ref _formSharesBaseData, value); }
        public string FormSharesBaseDataText => "Shares data with the base form";

        private bool _hasAlternateForms;
        public bool HasAlternateForms { get => _hasAlternateForms; private set => Set(ref _hasAlternateForms, value); }

        private FormSpriteData[] _currentFormData;

        // Names look like "Deoxys - Attack". The dash is the only link to the species: there is no id.
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

        // pl_personal_extra pseudo-species follow the Pokédex. Under hg-engine that range is real species.
        private static (int baseId, string description)? ResolvePseudoFormId(int id)
        {
            if (HgEngineProject.IsActive) return null;
            int extraIndex = id - RomInfo.GetPokemonNames().Length;
            PokeDatabase.PersonalData.PersonalExtraFiles[] extras = PokeDatabase.PersonalData.ExtraFiles;
            if (extraIndex < 0 || extraIndex >= extras.Length) return null;
            return (extras[extraIndex].MonId, extras[extraIndex].Description);
        }

        // Many forms have no species list entry (Unown letters, Castform weather), so -1 is normal.
        private static int ResolveFormPseudoId(int baseId, string formName)
        {
            if (HgEngineProject.IsActive) return -1;
            PokeDatabase.PersonalData.PersonalExtraFiles[] extras = PokeDatabase.PersonalData.ExtraFiles;
            for (int i = 0; i < extras.Length; i++)
                if (extras[i].MonId == baseId && FormNameMatchesDescription(formName, extras[i].Description))
                    return RomInfo.GetPokemonNames().Length + i;
            return -1;
        }

        // formIndex is the form_no. PokeFormDataTbl.c is 1-indexed. -1: hg-engine left this form alone.
        private static int ResolveHgEngineMigratedFormId(int baseId, int formIndex)
        {
            if (formIndex <= 0) return -1;
            HgEngineSymbolTable speciesTable = HgEngineSymbolTable.Load("include/constants/species.h");
            if (speciesTable == null || !speciesTable.TryGetNameWithPrefix(baseId, "SPECIES_", out string designator)) return -1;
            if (!HgEngineFormRegistry.LoadAll().TryGetValue(designator, out List<HgEngineFormRegistry.FormSlot> slots)) return -1;
            int slotIndex = formIndex - 1;
            if (slotIndex >= slots.Count) return -1;
            return speciesTable.TryGetValue(slots[slotIndex].SpeciesSymbol, out int migratedId) ? migratedId : -1;
        }

        private int _currentFormFamilyBaseId = -1;   // the species _currentFormData was built for: the family's base when a form target is open

        private FormSpriteData[] GetAlternateFormsForCurrentSpecies()
        {
            _currentFormFamilyBaseId = _currentId;
            if (_currentId <= 0) return Array.Empty<FormSpriteData>();
            string[] names = HgEngineProject.IsActive   // under hg-engine, species can sit past the name archive's length
                ? RomInfo.GetPokemonNamesWithForms(RomInfo.GetPersonalFilesCount())
                : RomInfo.GetPokemonNames();
            if (_currentId >= names.Length) return Array.Empty<FormSpriteData>();

            List<FormSpriteData> matches = BuildFamilyForms(_currentId, names[_currentId], names);
            if (matches.Count == 0 && HgEngineProject.IsActive)
            {
                int? baseId = FindHgEngineFormFamilyBase(_currentId);
                if (baseId.HasValue && baseId.Value != _currentId && baseId.Value < names.Length)
                {
                    List<FormSpriteData> familyMatches = BuildFamilyForms(baseId.Value, names[baseId.Value], names);
                    if (familyMatches.Count > 0) { matches = familyMatches; _currentFormFamilyBaseId = baseId.Value; }
                }
            }
            return matches.ToArray();
        }

        private List<FormSpriteData> BuildFamilyForms(int baseId, string baseName, string[] names)
        {
            List<FormSpriteData> matches = new List<FormSpriteData>();
            foreach (FormSpriteData f in GetFormDataForCurrentGame())
            {
                string prefix = SpeciesNamePrefix(f.Name);
                if (prefix != null && string.Equals(prefix, baseName, StringComparison.OrdinalIgnoreCase))
                    matches.Add(f);
            }
            if (matches.Count == 0 && HgEngineProject.IsActive)   // only species outside the vanilla table (Megas, regional forms), so none lists twice
                matches.AddRange(GetHgEngineNativeForms(baseId, baseName, names));
            return matches;
        }

        private static IEnumerable<FormSpriteData> GetHgEngineNativeForms(int baseId, string baseName, string[] names)
        {
            HgEngineSymbolTable speciesTable = HgEngineSymbolTable.Load("include/constants/species.h");
            if (speciesTable == null || !speciesTable.TryGetNameWithPrefix(baseId, "SPECIES_", out string designator)) yield break;
            if (!HgEngineFormRegistry.LoadAll().TryGetValue(designator, out List<HgEngineFormRegistry.FormSlot> slots) || slots.Count == 0) yield break;

            yield return new FormSpriteData(baseName, baseId);
            foreach (HgEngineFormRegistry.FormSlot slot in slots)
            {
                if (speciesTable.TryGetValue(slot.SpeciesSymbol, out int formId) && formId >= 0 && formId < names.Length)
                    yield return new FormSpriteData(names[formId], formId);
            }
        }

        private static int? FindHgEngineFormFamilyBase(int id)
        {
            HgEngineSymbolTable speciesTable = HgEngineSymbolTable.Load("include/constants/species.h");
            if (speciesTable == null) return null;
            foreach (KeyValuePair<string, List<HgEngineFormRegistry.FormSlot>> kvp in HgEngineFormRegistry.LoadAll())
            {
                if (!speciesTable.TryGetValue(kvp.Key, out int baseId)) continue;
                foreach (HgEngineFormRegistry.FormSlot slot in kvp.Value)
                    if (speciesTable.TryGetValue(slot.SpeciesSymbol, out int slotId) && slotId == id)
                        return baseId;
            }
            return null;
        }

        private void PopulateVariantList()
        {
            _currentFormData = HasAlternateForms ? GetAlternateFormsForCurrentSpecies() : Array.Empty<FormSpriteData>();
            ListSync.Apply(VariantNames, _currentFormData.Select(f => f.Name).ToList());   // not Clear and Add: a Reset during the ComboBox click that started this crashes
            _selectedVariantIndex = -1;   // not 0: LoadMon assigns the index next, and index 0 must still count as a change
            OnPropertyChanged(nameof(SelectedVariantIndex));
        }

        private void LoadAlternateForm(int formIndex)
        {
            _undo = null;
            RaiseUndo();
            ClearBitmaps();
            StatusText = "";

            if (_currentFormData == null || formIndex < 0 || formIndex >= _currentFormData.Length)
            {
                StatusText = "Invalid form index.";
                return;
            }

            try
            {
                PokemonBattleSpriteArchive narc = PokemonBattleSpriteArchive.Open(DirNames.OtherPokemonBattleSprites);
                if (narc == null)
                {
                    StatusText = "The alternate form sprite archive is missing.";
                    return;
                }

                FormSpriteData form = _currentFormData[formIndex];
                byte[][] rawBmps = new byte[4][];

                byte[] backSprite = narc.ReadSprite(form.BackSpriteIndex);
                if (backSprite != null) { rawBmps[0] = backSprite; rawBmps[1] = backSprite; }

                byte[] frontSprite = narc.ReadSprite(form.FrontSpriteIndex);
                if (frontSprite != null) { rawBmps[2] = frontSprite; rawBmps[3] = frontSprite; }

                uint[] normalPal = narc.ReadPalette(form.NormalPaletteIndex);
                uint[] shinyPal = narc.ReadPalette(form.ShinyPaletteIndex);

                if (normalPal == null)
                {
                    StatusText = "Couldn't load palette for this alternate form.";
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
                ResetUndo();
                OnPropertyChanged(nameof(HasUnsavedChanges));
            }
            catch (Exception ex)
            {
                StatusText = $"Couldn't load alternate form: {ex.Message}";
            }
        }

        // Forms store one shared sprite, so a single-gender or genderless base shows the same gap here.
        private static void ApplyFormGenderGap(byte[][] rawBmps, int baseSpeciesId)
        {
            byte ratio = ReadGenderRatio(baseSpeciesId);
            if (ratio == SpeciesFile.GenderRatioFemale) { rawBmps[1] = null; rawBmps[3] = null; }
            else if (ratio == SpeciesFile.GenderRatioMale || ratio == SpeciesFile.GenderRatioGenderless) { rawBmps[0] = null; rawBmps[2] = null; }
        }

        private static byte ReadGenderRatio(int speciesId)
        {
            try
            {
                return SpeciesFile.GenderRatioOf(speciesId);
            }
            catch (Exception ex) { AppLogger.WarnOnce("PokemonSpriteEditorViewModel.ReadGenderRatio", ex); return 127; }
        }

        private static FormSpriteData[] GetFormDataForCurrentGame() => AlternateFormSprites.ForCurrentGame();   // All graphics reads the same tables
    }
}
