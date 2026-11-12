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
        // The hg-engine side tables below are data/*.c files outside Species.c. Save writes changed ones.
        public bool ShowHgEngineExtras => DSPRE.HgEngine.HgEngineProject.IsActive;
        public ObservableCollection<string> IconPaletteOptions { get; } = new() { "Palette 0", "Palette 1", "Palette 2" };
        private const int IconPaletteCount = 3;
        public ObservableCollection<string> FollowerBounceOptions { get; } = new();
        private List<(string Name, int Value)> _followerBounceValues = new();

        /// <summary>The hg-engine side-table values and pending follower operations.</summary>
        internal sealed record HgStaged(int HiddenAbility, int BaseExp, int BabyMon, int RegionalDex, int IconPalette,
            int FollowerSize, int FollowerBounceIndex, string OwFemaleForm, int OwSizeClassIndex,
            bool CreateOwEntry, string OwSpritePath, string OwShinySpritePath = null);

        internal delegate bool ExpressionValidator(string expression, out string error);

        private HgStaged _hgLoaded;           // null without a linked checkout
        private int _hgLoadedBounceValue;     // kept when the file's bounce isn't one of the named options

        private int _hgHiddenAbilityIndex;
        public int HgHiddenAbilityIndex
        {
            get => _hgHiddenAbilityIndex;
            set { if (!RejectCleared(value, nameof(HgHiddenAbilityIndex)) && Set(ref _hgHiddenAbilityIndex, value)) StageHg(); }
        }

        private int _hgBaseExp;
        public int HgBaseExp { get => _hgBaseExp; set { if (Set(ref _hgBaseExp, value)) StageHg(); } }

        private int _hgBabyMonIndex;
        public int HgBabyMonIndex
        {
            get => _hgBabyMonIndex;
            set { if (!RejectCleared(value, nameof(HgBabyMonIndex)) && Set(ref _hgBabyMonIndex, value)) StageHg(); }
        }

        private int _hgRegionalDexNumber;
        public int HgRegionalDexNumber { get => _hgRegionalDexNumber; set { if (Set(ref _hgRegionalDexNumber, value)) StageHg(); } }

        private int _hgIconPaletteIndex;
        public int HgIconPaletteIndex
        {
            get => _hgIconPaletteIndex;
            set
            {
                if (value < 0 || value >= IconPaletteOptions.Count)   // the ComboBox reports -1 while it refills
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(HgIconPaletteIndex)));
                    return;
                }
                if (Set(ref _hgIconPaletteIndex, value)) StageHg();
            }
        }

        private int _hgFollowerSize;
        public int HgFollowerSize { get => _hgFollowerSize; set { if (Set(ref _hgFollowerSize, value)) StageHg(); } }

        private int _hgFollowerBounceIndex;
        public int HgFollowerBounceIndex
        {
            get => _hgFollowerBounceIndex;
            set { if (!RejectCleared(value, nameof(HgFollowerBounceIndex)) && Set(ref _hgFollowerBounceIndex, value)) StageHg(); }
        }

        private bool RejectCleared(int value, string name)
        {
            if (value >= 0 || _loading) return false;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(name));
            return true;
        }

        private void StageHg() { if (!_loading && _current != null) SetDirty(); }

        private string _hgOwFemaleFormExpression = "FALSE";
        public string HgOwFemaleFormExpression
        {
            get => _hgOwFemaleFormExpression;
            set
            {
                if (!Set(ref _hgOwFemaleFormExpression, value)) return;
                // The reason shows under the box while typing; Save refuses the value until it is fixed.
                HgOwFemaleFormError = OwFemaleFormProblem(value);
                StageHg();
            }
        }

        private string OwFemaleFormProblem(string expression) =>
            _hgLoaded == null || expression == _hgLoaded.OwFemaleForm
            || HgEngineSpeciesOwFormFemale.TryValidateRawExpression(expression, out string error) ? "" : error;

        private string _hgOwFemaleFormError = "";
        public string HgOwFemaleFormError
        {
            get => _hgOwFemaleFormError;
            private set { if (Set(ref _hgOwFemaleFormError, value)) OnPropertyChanged(nameof(HasHgOwFemaleFormError)); }
        }
        public bool HasHgOwFemaleFormError => !string.IsNullOrEmpty(_hgOwFemaleFormError);

        private void LoadHgEngineExtras()
        {
            _hgLoaded = null;
            _owPendingCreate = false;
            _owPendingSpritePath = null;
            _owPendingShinyPath = null;
            if (!DSPRE.HgEngine.HgEngineProject.IsActive) return;

            _hgHiddenAbilityIndex = HgEngineHiddenAbility.TryGetAbilityId(_currentId, out int ha) ? ha : 0;
            OnPropertyChanged(nameof(HgHiddenAbilityIndex));

            _hgBaseExp = HgEngineBaseExperience.TryGetBaseExp(_currentId, out int be) ? be : 0;
            OnPropertyChanged(nameof(HgBaseExp));

            _hgBabyMonIndex = HgEngineBabyMon.TryGetBabySpecies(_currentId, out int baby) ? baby : 0;
            OnPropertyChanged(nameof(HgBabyMonIndex));

            _hgRegionalDexNumber = HgEngineRegionalDex.TryGetDexNumber(_currentId, out int dex) ? dex : 0;
            OnPropertyChanged(nameof(HgRegionalDexNumber));

            _hgIconPaletteIndex = HgEngineIconPalette.TryGetPaletteId(_currentId, out int pal) ? pal : 0;
            OnPropertyChanged(nameof(HgIconPaletteIndex));

            FollowerBounceOptions.Clear();
            _followerBounceValues = HgEngineFollowerProperties.GetBounceOptions();
            foreach ((string Name, int Value) opt in _followerBounceValues) FollowerBounceOptions.Add(opt.Name);
            HgEngineFollowerProperties.TryGet(_currentId, out int size, out int bounce, out _, out _);
            _hgLoadedBounceValue = bounce;
            _hgFollowerSize = size; OnPropertyChanged(nameof(HgFollowerSize));
            _hgFollowerBounceIndex = _followerBounceValues.FindIndex(o => o.Value == bounce);
            if (_hgFollowerBounceIndex < 0) _hgFollowerBounceIndex = 0;
            OnPropertyChanged(nameof(HgFollowerBounceIndex));

            _hgOwFemaleFormExpression = HgEngineSpeciesOwFormFemale.TryGetRawExpression(_currentId, out string expr) ? expr : "FALSE";
            OnPropertyChanged(nameof(HgOwFemaleFormExpression));
            HgOwFemaleFormError = "";

            LoadOwFollower();
            _hgLoaded = CaptureHg();
        }

        private HgStaged CaptureHg() => ShowHgEngineExtras
            ? new HgStaged(_hgHiddenAbilityIndex, _hgBaseExp, _hgBabyMonIndex, _hgRegionalDexNumber, _hgIconPaletteIndex,
                _hgFollowerSize, _hgFollowerBounceIndex, _hgOwFemaleFormExpression, _owSizeClassIndex, _owPendingCreate, _owPendingSpritePath, _owPendingShinyPath)
            : null;

        private void ApplyHg(HgStaged s)
        {
            if (s == null) return;
            _hgHiddenAbilityIndex = s.HiddenAbility;      OnPropertyChanged(nameof(HgHiddenAbilityIndex));
            _hgBaseExp = s.BaseExp;                       OnPropertyChanged(nameof(HgBaseExp));
            _hgBabyMonIndex = s.BabyMon;                  OnPropertyChanged(nameof(HgBabyMonIndex));
            _hgRegionalDexNumber = s.RegionalDex;         OnPropertyChanged(nameof(HgRegionalDexNumber));
            _hgIconPaletteIndex = s.IconPalette;          OnPropertyChanged(nameof(HgIconPaletteIndex));
            _hgFollowerSize = s.FollowerSize;             OnPropertyChanged(nameof(HgFollowerSize));
            _hgFollowerBounceIndex = s.FollowerBounceIndex; OnPropertyChanged(nameof(HgFollowerBounceIndex));
            _hgOwFemaleFormExpression = s.OwFemaleForm;   OnPropertyChanged(nameof(HgOwFemaleFormExpression));
            _owSizeClassIndex = s.OwSizeClassIndex;       OnPropertyChanged(nameof(OwSizeClassIndex));
            HgOwFemaleFormError = OwFemaleFormProblem(s.OwFemaleForm);
            _owPendingCreate = s.CreateOwEntry;
            _owPendingSpritePath = s.OwSpritePath;
            _owPendingShinyPath = s.OwShinySpritePath;
            RefreshOwPending();
        }

        /// <summary>Why the staged values can't be written, or null. Only changed ones count.</summary>
        internal static string ValidateHgStaged(HgStaged staged, HgStaged loaded, int bounceOptionCount, int sizeClassOptionCount, ExpressionValidator validateOwFemaleForm)
        {
            if (staged == null || loaded == null) return null;
            if (staged.OwFemaleForm != loaded.OwFemaleForm && !validateOwFemaleForm(staged.OwFemaleForm, out string owError))
                return $"Female overworld form: {owError}";
            if (staged.HiddenAbility != loaded.HiddenAbility && staged.HiddenAbility < 0) return "Pick a hidden ability.";
            if (staged.BabyMon != loaded.BabyMon && staged.BabyMon < 0) return "Pick a baby Pokémon.";
            if (staged.BaseExp != loaded.BaseExp && (staged.BaseExp < 0 || staged.BaseExp > ushort.MaxValue))
                return $"Base exp must be 0 to {ushort.MaxValue}.";
            if (staged.RegionalDex != loaded.RegionalDex && (staged.RegionalDex < 0 || staged.RegionalDex > ushort.MaxValue))
                return $"Regional Dex number must be 0 to {ushort.MaxValue}.";
            if (staged.IconPalette != loaded.IconPalette && (staged.IconPalette < 0 || staged.IconPalette >= IconPaletteCount))
                return "Pick an icon palette.";
            if (staged.FollowerSize != loaded.FollowerSize && staged.FollowerSize is < 0 or > 1) return "Follower size must be 0 or 1.";
            if (staged.FollowerBounceIndex != loaded.FollowerBounceIndex && (staged.FollowerBounceIndex < 0 || staged.FollowerBounceIndex >= bounceOptionCount))
                return "Pick a bounce speed.";
            if (staged.OwSizeClassIndex != loaded.OwSizeClassIndex && (staged.OwSizeClassIndex < 0 || staged.OwSizeClassIndex >= sizeClassOptionCount))
                return "Pick a size class.";
            return null;
        }
    }
}
