using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using DSPRE.Avalonia.Data;
using System.Text;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class EventEditorViewModel
    {
        public ObservableCollection<string> OwSpriteEntries { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> OwMovementNames { get; } = new ObservableCollection<string>();
        private IReadOnlyList<OverworldMovement> _owMoveTable = new List<OverworldMovement>();

        public bool OwMovementUnknown => _ow != null && OwMovementIndex < 0;
        public string OwMovementUnknownNote => OwMovementUnknown
            ? $"Movement {(int)_owMove} isn't one this game defines. It is kept unless another is picked."
            : null;
        // The record allows -1 for no facing; it sits last so 0-3 stay index == value.
        public ObservableCollection<string> OwOrientationNames { get; } = new ObservableCollection<string> { "Up", "Down", "Left", "Right", "None" };
        private const int OwNoFacingIndex = 4;

        private global::Avalonia.Media.Imaging.Bitmap _owSpritePreview;
        public global::Avalonia.Media.Imaging.Bitmap OwSpritePreview { get => _owSpritePreview; private set => Set(ref _owSpritePreview, value); }
        public bool HasOwSpritePreview => _owSpritePreview != null;

        public bool IsDynamicSprite => _ow != null && IsVariableOverworld(_ow.OverlayTableEntry);
        public string DynamicSpriteTip => IsDynamicSprite
            ? $"Dynamic: a script picks this sprite while playing, from variable 0x{0x4020 + _ow.OverlayTableEntry - FirstVariableOverworld:X4}. Shown as a stand-in."
            : null;

        public NsbmdRenderModel OwModelPreview { get; private set; }
        public bool HasOwModelPreview => OwModelPreview != null;
        public event EventHandler OwModelPreviewChanged;

        private enum OwKind { Normal, Trainer, Item }
        // hg-engine's one item script reads the item from param0 (SightRange).
        private const ushort HgEngineItemScript = GroundItemScriptsLogic.ItemScrMin;
        private const int DummyTrainer = 0;
        private OwKind _owKind;

        private ushort _owRawType;
        public ObservableCollection<string> OwEventTypes { get; } = new ObservableCollection<string>();
        private IReadOnlyList<OverworldEventType> _owTypeTable = new List<OverworldEventType>();

        public int OwEventTypeIndex
        {
            get
            {
                for (int i = 0; i < _owTypeTable.Count; i++) if (_owTypeTable[i].Value == _owRawType) return i;
                return -1;
            }
            set
            {
                if (value < 0 || value >= _owTypeTable.Count) return;
                ushort v = _owTypeTable[value].Value;
                if (v == _owRawType) return;
                _owRawType = v;
                SetOwKind(KindOfType(v));
                if (!_suppress && _ow != null) { _ow.Type = v; Dirty(); }
                RaiseOwTypeInfoChanged();
            }
        }

        public bool OwTypeUnknown => _ow != null && OwEventTypeIndex < 0;
        public string OwTypeUnknownNote => OwTypeUnknown
            ? $"This game doesn't define event type {_owRawType}. It is kept unless a type is picked above."
            : null;

        private OverworldEventType CurrentOwType =>
            _owTypeTable.FirstOrDefault(t => t.Value == _owRawType);
        public string OwTypeNote => CurrentOwType?.Note;
        public bool OwHasTypeNote => !string.IsNullOrEmpty(OwTypeNote);

        /// <summary>param1's meaning depends on the type; hidden when the engine never reads it.</summary>
        public string OwParam1Label => CurrentOwType?.Param1Label;
        public bool OwParam1Visible => !string.IsNullOrEmpty(OwParam1Label);

        public string OwParam0Label => CurrentOwType?.IsTrainer == true ? null : CurrentOwType?.Param0Label;
        public bool OwParam0Visible => !string.IsNullOrEmpty(OwParam0Label);

        // Type 9 runs an empty script when talked to, so its script number is never reached.
        public bool OwTalkRunsNothing => _owRawType == 9;

        private static OwKind KindOfType(ushort t)
            => t == (ushort)Overworld.OwType.ITEM ? OwKind.Item
             : (OverworldEventTypes.Find(RomInfo.GameFamily, t)?.IsTrainer == true ? OwKind.Trainer : OwKind.Normal);

        private void RaiseOwTypeInfoChanged()
        {
            OnPropertyChanged(nameof(OwEventTypeIndex)); OnPropertyChanged(nameof(OwTypeUnknown));
            OnPropertyChanged(nameof(OwTypeUnknownNote)); OnPropertyChanged(nameof(OwTypeNote));
            OnPropertyChanged(nameof(OwHasTypeNote)); OnPropertyChanged(nameof(OwParam1Label));
            OnPropertyChanged(nameof(OwParam1Visible)); OnPropertyChanged(nameof(OwTalkRunsNothing));
            OnPropertyChanged(nameof(OwParam0Label)); OnPropertyChanged(nameof(OwParam0Visible));
            OnPropertyChanged(nameof(OwScriptGenericWarningVisible)); OnPropertyChanged(nameof(OwScriptCommonInfo)); OnPropertyChanged(nameof(OwScriptHasCommonInfo)); OnPropertyChanged(nameof(OwScriptCommonName));
        }

        public decimal OwParam1
        {
            get => _ow?.Param1 ?? 0;
            set { if (_ow == null || _suppress) return; if (_ow.Param1 == (ushort)value) return; _ow.Param1 = (ushort)value; Dirty(); OnPropertyChanged(nameof(OwParam1)); }
        }
        public decimal OwParam2
        {
            get => _ow?.Param2 ?? 0;
            set { if (_ow == null || _suppress) return; if (_ow.Param2 == (ushort)value) return; _ow.Param2 = (ushort)value; Dirty(); OnPropertyChanged(nameof(OwParam2)); }
        }

        // A script number that matches no entry unlocks the field, so the user can still change it.
        public bool OwScriptEnabled => _owKind == OwKind.Normal || OwTrainerIndexOutOfRange || OwItemIndexOutOfRange;

        public bool OwScriptSectionVisible => _owKind != OwKind.Item || OwItemIndexOutOfRange;

        public bool OwTrainerFieldsVisible => _owKind == OwKind.Trainer;

        private const int FirstApricornSprite = 0x0106, LastApricornSprite = 0x0114;

        public bool OwIsApricornTree
        {
            get
            {
                if (_ow == null) return false;
                int sprite = _ow.OverlayTableEntry;
                return sprite >= FirstApricornSprite && sprite <= LastApricornSprite;
            }
        }

        public string OwApricornNote =>
            "This is an apricorn tree. Its colour isn't stored here: this number picks an apricorn bed, and "
            + "the save says what grows there. A different number picks another bed. The sprite above is only "
            + "shown until the game replaces it.";

        public bool IsHGE => RomInfo.IsHGE;
        public bool OwItemFieldsVisibleVanilla => _owKind == OwKind.Item && !IsHGE;
        public bool OwItemFieldsVisibleHGE => _owKind == OwKind.Item && IsHGE;

        public ObservableCollection<string> OwTrainerEntries { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> OwItemEntries { get; } = new ObservableCollection<string>();

        private int _owTrainerIndex = -1;
        public int OwTrainerIndex
        {
            get => _owTrainerIndex;
            set
            {
                if (!Set(ref _owTrainerIndex, value)) return;
                OnPropertyChanged(nameof(OwTrainerIndexOutOfRange)); OnPropertyChanged(nameof(OwTrainerIsDummy)); OnPropertyChanged(nameof(OwTrainerHasNoScript)); OnPropertyChanged(nameof(OwTrainerNoScriptNote)); OnPropertyChanged(nameof(OwScriptEnabled));
                OnPropertyChanged(nameof(OwScriptGenericWarningVisible));
                if (!_suppress && _ow != null && _owKind == OwKind.Trainer) RecomputeTrainerScript();
            }
        }

        public bool OwTrainerIndexOutOfRange => _owKind == OwKind.Trainer && (_owTrainerIndex < 0 || _owTrainerIndex >= OwTrainerEntries.Count);

        public bool OwTrainerIsDummy => _owKind == OwKind.Trainer && _owTrainerIndex == DummyTrainer;
        private TrainerScriptLayout _trainerScripts;
        public bool OwTrainerHasNoScript => _owKind == OwKind.Trainer && _owTrainerIndex > DummyTrainer
            && _trainerScripts != null && !_trainerScripts.HasOwnEntry(_owTrainerIndex);
        public string OwTrainerNoScriptNote =>
            $"Trainer {_owTrainerIndex} has no trainer script of its own. Add trainers in the Trainer editor so the game gets one.";

        private bool _owPartnerTrainer;
        public bool OwPartnerTrainer
        {
            get => _owPartnerTrainer;
            set { if (Set(ref _owPartnerTrainer, value) && !_suppress && _ow != null && _owKind == OwKind.Trainer) RecomputeTrainerScript(); }
        }

        private int _owItemIndex = -1;
        public int OwItemIndex
        {
            get => _owItemIndex;
            set { if (Set(ref _owItemIndex, value) && !_suppress && _ow != null && _owKind == OwKind.Item && value >= 0) ForceOwScript((ushort)(GroundItemScriptsLogic.ItemScrMin + value)); }
        }

        public bool OwItemIndexOutOfRange => _owKind == OwKind.Item && (_owItemIndex < 0 || _owItemIndex >= OwItemEntries.Count);

        public decimal OwItemIdHGE
        {
            get => _ow?.SightRange ?? 0;
            set
            {
                if (_ow == null || _suppress || _owKind != OwKind.Item) return;
                _ow.SightRange = (ushort)value;
                _owSight = value;
                ForceOwScript(HgEngineItemScript);
                OnPropertyChanged(nameof(OwItemIdHGE));
                OnPropertyChanged(nameof(OwSight));
            }
        }

        private void RaiseOwKindChanged()
        {
            OnPropertyChanged(nameof(OwTrainerFieldsVisible));
            OnPropertyChanged(nameof(OwIsApricornTree)); OnPropertyChanged(nameof(OwApricornNote));
            OnPropertyChanged(nameof(OwItemFieldsVisibleVanilla)); OnPropertyChanged(nameof(OwItemFieldsVisibleHGE));
            OnPropertyChanged(nameof(OwTrainerIndexOutOfRange)); OnPropertyChanged(nameof(OwTrainerIsDummy)); OnPropertyChanged(nameof(OwTrainerHasNoScript)); OnPropertyChanged(nameof(OwTrainerNoScriptNote)); OnPropertyChanged(nameof(OwItemIndexOutOfRange));
            OnPropertyChanged(nameof(OwItemIdHGE));
            OnPropertyChanged(nameof(OwScriptEnabled));
            OnPropertyChanged(nameof(OwScriptSectionVisible));
            OnPropertyChanged(nameof(OwScriptCommonInfo));
            OnPropertyChanged(nameof(OwScriptHasCommonInfo)); OnPropertyChanged(nameof(OwScriptCommonName));
            OnPropertyChanged(nameof(OwScriptGenericWarningVisible));
        }

        // Skips OwScript's checks, since a kind change always rewrites the script.
        private void ForceOwScript(ushort value)
        {
            if (_ow != null) _ow.ScriptNumber = value;
            _owScript = value;
            OnPropertyChanged(nameof(OwScript));
            OnPropertyChanged(nameof(OwScriptIndex)); OnPropertyChanged(nameof(OwScriptIndexOutOfRange));
            Dirty();
        }

        private void RecomputeTrainerScript()
        {
            if (_owTrainerIndex < 0) return;
            int idx = _owTrainerIndex;
            ushort scriptNum = (ushort)(idx + (_owPartnerTrainer ? TrainerScripts.DoubleFirst - 1 : TrainerScripts.SingleFirst - 1));
            ForceOwScript(scriptNum);
        }

        // A Standard event's script before it became Trainer or Item, so switching back restores it.
        private ushort? _owNormalScript;

        private static bool ScriptOwnedBy(OwKind kind, ushort script) => kind switch
        {
            OwKind.Trainer => script >= TrainerScripts.SingleFirst && script <= TrainerScripts.DoubleLast,
            OwKind.Item => script >= GroundItemScriptsLogic.ItemScrMin && script < GroundItemScriptsLogic.ItemScrMax,
            _ => false,
        };

        private void SetOwKind(OwKind kind)
        {
            OwKind previous = _owKind;
            bool changed = previous != kind;
            _owKind = kind;
            RaiseOwKindChanged();
            if (!changed || _suppress || _ow == null) return;
            if (previous == OwKind.Normal) _owNormalScript = _ow.ScriptNumber;

            switch (kind)
            {
                case OwKind.Normal:
                    // A message id or a hand-written script number must survive a type change.
                    if (ScriptOwnedBy(previous, _ow.ScriptNumber)) ForceOwScript(_owNormalScript ?? 0);
                    break;
                case OwKind.Item:
                    if (IsHGE)
                    {
                        ForceOwScript(HgEngineItemScript);
                    }
                    else if (_owItemIndex < 0 && OwItemEntries.Count > 0) OwItemIndex = 0;
                    else ForceOwScript((ushort)(GroundItemScriptsLogic.ItemScrMin + Math.Max(_owItemIndex, 0)));
                    break;
                case OwKind.Trainer:
                    // Entry 0 is the dummy slot, so no trainer is picked and the warning asks for a real one.
                    if (_owTrainerIndex >= 0) RecomputeTrainerScript();
                    break;
            }
        }

        private void PopulateOwEventTypes()
        {
            _owTypeTable = OverworldEventTypes.For(RomInfo.GameFamily);
            OwEventTypes.Clear();
            foreach (OverworldEventType t in _owTypeTable) OwEventTypes.Add(t.ToString());

            _owMoveTable = OverworldMovements.For(RomInfo.GameFamily);
            OwMovementNames.Clear();
            foreach (OverworldMovement m in _owMoveTable) OwMovementNames.Add(m.ToString());
        }

        private void PopulateOwTrainerAndItemEntries()
        {
            PopulateOwTrainerEntries();
            PopulateOwItemEntries();
        }

        private void PopulateOwTrainerEntries()
        {
            try
            {
                ListSync.Apply(OwTrainerEntries,
                    new List<string>(TrainerNames.GetAll()));
                _trainerScripts = TrainerScriptLayout.OfProject(GameFamily);
            }
            catch (Exception ex)
            {
                OwTrainerEntries.Clear();
                AppLogger.Error("PopulateOwTrainerEntries: " + ex.Message);
            }
        }

        private void OnNamesChanged(object sender, EventArgs e)
        {
            PopulateOwTrainerEntries();
            OnPropertyChanged(nameof(OwTrainerIndexOutOfRange));
            OnPropertyChanged(nameof(OwScriptEnabled));
        }

        public void Attach()
        {
            AppEvents.NamesChanged -= OnNamesChanged;
            AppEvents.NamesChanged += OnNamesChanged;
            AppEvents.RomPatchStateChanged -= OnPatchStateChanged;
            AppEvents.RomPatchStateChanged += OnPatchStateChanged;
        }

        public void Detach() { AppEvents.NamesChanged -= OnNamesChanged; AppEvents.RomPatchStateChanged -= OnPatchStateChanged; }

        // The standardisation patch renumbers ground items on disk. Reloading drops unsaved edits.
        private void OnPatchStateChanged(object sender, EventArgs e)
        {
            if (IsHGE) return;
            bool standardized;
            try { standardized = PatchToolboxLogic.CheckScriptsStandardizedItemNumbers(); }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException) { return; }
            if (standardized == _itemsStandardized) return;

            PopulateOwItemEntries();
            if (_selectedIndex < 0) return;
            int keepOw = _selOw;
            LoadFile(_selectedIndex);
            // Refilling the item list drops the box's pick, so the overworld is picked again after.
            if (keepOw >= 0)
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => SelectedOverworldIndex = keepOw,
                                                                     global::Avalonia.Threading.DispatcherPriority.Background);
        }

        void IDetachable.Detach() { Detach(); DetachSaves(); }

        public void DetachSaves()
        {
            AppEvents.EventFileSaved -= OnSavedElsewhere;
            AppEvents.MapSaved -= OnMapSavedElsewhere;
            _spritesSaved?.Dispose();
        }

        private bool _itemsStandardized;

        private void PopulateOwItemEntries()
        {
            OwItemEntries.Clear();
            if (IsHGE) return;
            try
            {
                string[] itemNames = RomInfo.GetItemNames();
                _itemsStandardized = PatchToolboxLogic.CheckScriptsStandardizedItemNumbers();
                if (_itemsStandardized)
                {
                    foreach (string name in itemNames) OwItemEntries.Add(name);
                }
                else
                {
                    ScriptFile itemScript = new ScriptFile(RomInfo.ItemScriptFileNumber);
                    foreach ((int scriptIndex, int itemId, int quantity) entry in DSUtils.GetGroundItemScriptEntries(itemScript))
                    {
                        string name = entry.itemId < itemNames.Length ? itemNames[entry.itemId] : ("Item " + entry.itemId);
                        OwItemEntries.Add(entry.quantity + "x " + name);
                    }
                }
            }
            catch (Exception ex) { AppLogger.Error("PopulateOwItemEntries: " + ex.Message); }
        }

        public void RefreshOwItemEntries()
        {
            PopulateOwItemEntries();
            if (_ow == null || _owKind != OwKind.Item || IsHGE) return;

            int itemIdx = _ow.ScriptNumber - GroundItemScriptsLogic.ItemScrMin;
            _owItemIndex = (itemIdx >= 0 && itemIdx < OwItemEntries.Count) ? itemIdx : -1;
            OnPropertyChanged(nameof(OwItemIndex));
            OnPropertyChanged(nameof(OwItemIndexOutOfRange));
            OnPropertyChanged(nameof(OwScriptSectionVisible));
        }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }
    }
}
