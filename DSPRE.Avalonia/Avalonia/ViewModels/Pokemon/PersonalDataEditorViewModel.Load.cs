using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using DSPRE.Avalonia.Views.Shell;
using DSPRE.Csv;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class PersonalDataEditorViewModel
    {
        private void SetDirty()  { if (_loading) return; RecordUndoSnapshot(); _dirty = true;  Title = "● Personal data editor"; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean()  { _dirty = false; Title = "Personal data editor";  OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private async Task ConfirmDiscardAsync(int newId)
        {
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, null, "Pokémon")) return;
            _dirty = false;
            LoadMon(newId);
        }

        // Set when Species.c couldn't be read for this Pokémon. Save then refuses to write guessed values.
        private string _hgLoadError;
        public string HgLoadError
        {
            get => _hgLoadError;
            private set { if (Set(ref _hgLoadError, value)) OnPropertyChanged(nameof(HasHgLoadError)); }
        }
        public bool HasHgLoadError => _hgLoadError != null;

        private void ShowMonIcon()
        {
            try
            {
                RawImage drawingImg = DSUtils.GetPokePicRaw(DSUtils.ResolveIconId(_currentId), 64, 64);
                MonIconBitmap = ImageConverter.ToAvaloniaBitmap(drawingImg);
            }
            catch (Exception ex) { AppLogger.WarnOnce("PersonalDataEditorViewModel.ShowMonIcon", ex); MonIconBitmap = null; }
        }

        internal void LoadMon(int id)
        {
            if (id < 0 || id >= GetPersonalFilesCount())
            {
                MonIconBitmap = null;
                return;
            }

            _loading = true;
            _currentId = id;
            _current   = new PokemonPersonalData(id);

            // The unpacked copy only refreshes after a build, so Species.c is what edits must start from.
            string loadError = null;
            if (HgEngineProject.IsActive && !HgEngineSpeciesPersonalFields.TryLoadInto(id, _current, out loadError))
                AppLogger.Error($"hg-engine Species.c read failed for species {id}: {loadError}");
            HgLoadError = loadError == null ? null : $"Species.c couldn't be read, so this Pokémon can't be saved: {loadError}";

            LoadExtraMaskTms();
            LoadSourceMachineMoves();
            PopulateFromCurrent();
            LoadHgEngineExtras();
            LoadRetailFollower();
            LoadPokeathlon();
            _hatchResultIndex = HatchResultTable.Read(id); OnPropertyChanged(nameof(HatchResultIndex));

            ShowMonIcon();

            SetClean();
            _loading = false;

            _savedSnapshot = Snapshot();
            _history.Reset(_savedSnapshot);
            _lastCaptureUtc = DateTime.MinValue;
            RaiseUndoState();

            _heldRecord = _pending.Remove(id, out byte[] held);
            if (_heldRecord) TakeImported(held);
        }

        // The caller guards with _loading.
        private void PopulateFromCurrent()
        {
            BaseHP  = _current.BaseHP;  BaseAtk = _current.BaseAtk; BaseDef = _current.BaseDef;
            BaseSpe = _current.BaseSpeed; BaseSpA = _current.BaseSpAtk; BaseSpD = _current.BaseSpDef;
            EvHP  = _current.EvHP;  EvAtk = _current.EvAtk; EvDef = _current.EvDef;
            EvSpe = _current.EvSpeed; EvSpA = _current.EvSpAtk; EvSpD = _current.EvSpDef;

            _type1Index = (int)_current.Type1; OnPropertyChanged(nameof(Type1Index));
            _type2Index = (int)_current.Type2; OnPropertyChanged(nameof(Type2Index));
            _ability1Index = _current.FirstAbility;  OnPropertyChanged(nameof(Ability1Index));
            _ability2Index = _current.SecondAbility; OnPropertyChanged(nameof(Ability2Index));
            _item1Index = _current.Item1; OnPropertyChanged(nameof(Item1Index));
            _item2Index = _current.Item2; OnPropertyChanged(nameof(Item2Index));
            RaiseHeldItemOdds();

            CatchRate = _current.CatchRate; BaseExp = _current.GivenExp;
            GenderVec = _current.GenderVec; EggSteps = _current.EggSteps;
            BaseFriendship = _current.BaseFriendship; EscapeRate = _current.EscapeRate;

            _growthCurveIndex = (int)_current.GrowthCurve; OnPropertyChanged(nameof(GrowthCurveIndex));
            _dexColorIndex    = (int)_current.Color;        OnPropertyChanged(nameof(DexColorIndex));
            _eggGroup1Index   = _current.EggGroup1;         OnPropertyChanged(nameof(EggGroup1Index));
            _eggGroup2Index   = _current.EggGroup2;         OnPropertyChanged(nameof(EggGroup2Index));

            _flipFlag = _current.Flip; OnPropertyChanged(nameof(FlipFlag));
            GenderLabel = GetGenderText(_current.GenderVec);

            RebuildMachineLists();
        }

        private void RebuildMachineLists()
        {
            AddedMachines.Clear();
            AddableMachines.Clear();
            if (_current == null || _machineMoveNames == null) return;
            foreach (int i in TMEditor.DisplayOrder())
            {
                string label = TMEditor.MachineLabelFromIndex(i);
                string move  = _machineMoveNames.Length > i ? _machineMoveNames[i] : $"UNK_{i}";
                MachineRow entry = new MachineRow
                {
                    Index = i,
                    Move = _machineMoves.Length > i ? _machineMoves[i] : 0,
                    Label = LearnedByLevelUp(i) ? $"{label} - {move} (level-up)" : $"{label} - {move}",
                };
                if (HasMachine(i)) AddedMachines.Add(entry);
                else                               AddableMachines.Add(entry);
            }
        }

        private static string GetGenderText(int vec)
        {
            switch (vec)
            {
                case (byte)PokemonGender.Male:    return "100% Male";
                case (byte)PokemonGender.Female:  return "100% Female";
                case (byte)PokemonGender.Unknown: return "Gender unknown";
                default:
                    float femalePct = 100 * ((vec + 1) / 256f);
                    return $"{100 - femalePct:F1}% Male / {femalePct:F1}% Female";
            }
        }
    }
}
