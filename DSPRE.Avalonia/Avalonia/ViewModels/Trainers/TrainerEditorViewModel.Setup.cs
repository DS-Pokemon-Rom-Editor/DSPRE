using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using Avalonia.Threading;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    public partial class TrainerEditorViewModel
    {
        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            StatusText = "Loading trainers…";
            PushLoading();
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> {
                    DirNames.TrainerProperties, DirNames.TrainerParty, DirNames.TrainerGraphics,
                    DirNames.PersonalPokeData, DirNames.MonIcons, DirNames.TextArchives });
                SetMonIconsPalTableAddress();

                DetectPatches();

                foreach (string n in GetPokemonNames()) PokemonNames.Add(n);
                foreach (string n in GetAttackNames()) MoveNames.Add(n);
                foreach (string n in GetItemNames()) ItemNames.Add(n);
                _abilityNames = GetAbilityNames();
                LoadAbilities();

                string[] classNames = GetTrainerClassNames();
                foreach (string n in TrainerListEntries(classNames)) TrainerNames.Add(n);
                AppEvents.NamesChanged += OnNamesChanged;   // live-refresh names from the Text editor
                TextArchive.Saved += OnTextSaved;
                AppEvents.RomPatchStateChanged += OnRomPatchStateChanged;
                AppEvents.ClassIntrosSaved += OnClassIntrosSaved;
                _spriteSaved = AppEvents.WhenSaved(UpdateTrainerSprite, DirNames.TrainerGraphics);
                _propertiesSaved = AppEvents.WhenSaved(OnPropertiesSaved, DirNames.TrainerProperties);

                for (int i = 0; i < classNames.Length; i++) TrainerClassItems.Add($"[{i:D3}] {classNames[i]}");

                AddFlagRows();

                AddPartyRows();

                StatusText = $"{TrainerNames.Count} trainers.";
                if (TrainerNames.Count > 0)
                    SelectedTrainerIndex = Math.Min(Math.Max(0, InitialIndex), TrainerNames.Count - 1);
            }
            catch (Exception ex)
            {
                StatusText = $"Couldn't load trainers: {ex.Message}";
                await DialogHelper.ShowErrorAsync($"Couldn't load trainers:\n{ex.Message}", "Trainer editor");
            }
            finally { PopLoading(); }
        }

        private void DetectPatches()
        {
            _genderEditable = GameFamily == GameFamilies.HGSS || AIBackportEnabled;
            _shinyPatch = GameFamily == GameFamilies.HGSS && TrainerShinyPatch.DetectCurrentProject();
            OnPropertyChanged(nameof(OffersShinyPatch));
            _customPokeballsPatch = TrainerCustomPokeballsPatch.DetectCurrentProject();
            if (_customPokeballsPatch) DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.ItemIcons });
            OnPropertyChanged(nameof(OffersBallPatch));
            _abilityEditable = _genderEditable;
            _ballEnabled = GameFamily != GameFamilies.DP;
            _formVisible = GameFamily != GameFamilies.DP;
        }

        private void AddFlagRows()
        {
            if (IsHgeActive)
            {
                foreach (HgEngineTrainerFieldSchema.NamedFlag flag in HgEngineTrainerFieldSchema.GetAiFlags())
                {
                    AiFlagViewModel f = new AiFlagViewModel(flag.Name, flag.Bit);
                    f.Changed += (s, e) => { if (!_suppress) _hgeAiFlagsKnown = true; SetDirty(); };
                    AiFlags.Add(f);
                }
                foreach (HgEngineTrainerFieldSchema.NamedFlag flag in HgEngineTrainerFieldSchema.GetTrainerDataTypeFlags())
                {
                    AiFlagViewModel f = new AiFlagViewModel(flag.Name, flag.Bit);
                    f.Changed += (s, e) =>
                    {
                        if (!_suppress) _hgeTrainerTypeKnown = true;
                        if (f.Checked && !_suppress) FillHgeDefaults(f.Label);
                        ApplyHgeGatingToParty();
                        RaiseHgeFlagCheckedChanged();
                        SetDirty();
                    };
                    TrainerDataTypeFlags.Add(f);
                }
                IReadOnlyList<HgEngineTrainerFieldSchema.NamedFlag> battleTypes = HgEngineTrainerFieldSchema.GetBattleTypes();
                _battleTypeValues = battleTypes.Select(b => b.Bit).ToArray();
                foreach (HgEngineTrainerFieldSchema.NamedFlag bt in battleTypes) BattleTypeOptions.Add(bt.Name);
            }
            else
            {
                for (int i = 0; i < AiFlagLabels.Length; i++)
                {
                    AiFlagViewModel f = new AiFlagViewModel(AiFlagLabels[i]) { Tip = AiFlagTip(i) };
                    f.Changed += (s, e) => SetDirty();
                    AiFlags.Add(f);
                }
            }
        }

        private void AddPartyRows()
        {
            for (int i = 0; i < TrainerProperties.TrainerItemCount; i++)
            {
                TrainerItemSlotViewModel slot = new TrainerItemSlotViewModel(ItemNames);
                slot.Changed += (s, e) => SetDirty();
                TrainerItems.Add(slot);
            }

            // hg-engine's TrainerPokemonData has no gender field, so the selector is hidden.
            bool genderVisibleForMode = _genderEditable && !IsHgeActive;
            for (int i = 0; i < TrainerFile.PokeInParty; i++)
            {
                TrainerPartyMonViewModel mon = new TrainerPartyMonViewModel(i, PokemonNames, MoveNames, ItemNames,
                    _abilityNames, _abilities, _abilityEditable, genderVisibleForMode, _formVisible, _ballEnabled);
                mon.Changed += (s, e) => SetDirty();
                Party.Add(mon);
            }
            ApplyHgeGatingToParty();
            if (_ballEnabled && (_customPokeballsPatch || Data.TrainerCapsuleCatalog.Available))
            {
                ShowCapsuleNames();
                Data.TrainerCapsuleCatalog.Changed += ShowCapsuleNames;
            }
        }

        private void LoadAbilities()
        {
            // Form species sit past the name list.
            int count = Math.Max(PokemonNames.Count, GetPersonalFilesCount());
            _abilities = new (int, int)[count];
            string dir = GameDirs[DirNames.PersonalPokeData].unpackedDir;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    string path = Path.Combine(dir, i.ToString("D4"));
                    if (!PlainFiles.Exists(path)) { _abilities[i] = (0, 0); continue; }
                    // hg-engine widens both abilities to u16 at other offsets.
                    PokemonPersonalData personal = new PokemonPersonalData(new MemoryStream(PlainFiles.ReadBytes(path)));
                    _abilities[i] = (personal.FirstAbility, personal.SecondAbility);
                }
                catch (Exception ex) { AppLogger.WarnOnce("TrainerEditorViewModel.LoadAbilities", ex); _abilities[i] = (0, 0); }
            }

            // The built copies trail Species.c until the next make.
            if (!IsHgeActive) return;
            if (!HgEngineSpeciesPersonalFields.TryLoadAllAbilities(out Dictionary<int, (int first, int second)> fromSource, out string error))
            {
                AppLogger.Error("Trainer editor abilities couldn't be read from Species.c: " + error);
                return;
            }
            foreach (KeyValuePair<int, (int first, int second)> pair in fromSource)
            {
                if (pair.Key < 0 || pair.Key >= count) continue;
                (int first, int second) = pair.Value;
                _abilities[pair.Key] = (first >= 0 ? first : _abilities[pair.Key].abi1, second >= 0 ? second : _abilities[pair.Key].abi2);
            }
        }
    }
}
