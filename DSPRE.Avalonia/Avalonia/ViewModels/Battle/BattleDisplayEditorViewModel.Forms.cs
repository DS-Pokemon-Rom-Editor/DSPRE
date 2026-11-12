using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DSPRE.Avalonia.BattleAnim.Particles;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using static MKDS_Course_Editor.NSBTP.NSBTP.NSBTP_File;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    public partial class BattleDisplayEditorViewModel
    {
        // hg-engine forms are species IDs with their own SpriteOffsets.c entry. Graphics stay on the base.
        public sealed class FormOption { public string Label { get; set; } public int SpeciesId { get; set; } }
        public ObservableCollection<FormOption> FormOptions { get; } = new ObservableCollection<FormOption>();
        public bool HasForms => FormOptions.Count > 1;

        private int _currentHgeSpeciesId = -1;
        private int _selectedFormIndex;
        public int SelectedFormIndex
        {
            get => _selectedFormIndex;
            set
            {
                if (_selectedFormIndex == value) return;
                _selectedFormIndex = value;
                OnPropertyChanged();
                _currentHgeSpeciesId = (value >= 0 && value < FormOptions.Count) ? FormOptions[value].SpeciesId : _currentId;
                if (!_loading) LoadHgeFormData();
            }
        }

        private HgEngineSymbolTable _hgeSpecies;
        private Dictionary<string, List<HgEngineFormRegistry.FormSlot>> _hgeFormTable;
        private bool _hgeFormLookupTried;

        private void EnsureHgeFormLookup()
        {
            if (_hgeFormLookupTried) return;
            _hgeFormLookupTried = true;
            try
            {
                _hgeSpecies = HgEngineSymbolTable.Load("include/constants/species.h");
                _hgeFormTable = HgEngineFormRegistry.LoadAll();
            }
            catch (Exception ex) { AppLogger.WarnOnce("BattleDisplayEditorViewModel.EnsureHgeFormLookup", ex); _hgeSpecies = null; _hgeFormTable = null; }
        }

        private void LoadFormOptions(int baseId)
        {
            FormOptions.Clear();
            _currentHgeSpeciesId = baseId;
            if (HgEngineProject.IsActive && baseId >= 0)
            {
                EnsureHgeFormLookup();
                FormOptions.Add(new FormOption { Label = "(base form)", SpeciesId = baseId });
                if (_hgeSpecies != null && _hgeFormTable != null
                    && _hgeSpecies.TryGetNameWithPrefix(baseId, "SPECIES_", out string baseDesignator)
                    && _hgeFormTable.TryGetValue(baseDesignator, out List<HgEngineFormRegistry.FormSlot> slots))
                {
                    foreach (HgEngineFormRegistry.FormSlot slot in slots)
                        if (_hgeSpecies.TryGetValue(slot.SpeciesSymbol, out int formId))
                            FormOptions.Add(new FormOption { Label = FormDisplayName(slot.SpeciesSymbol), SpeciesId = formId });
                }
            }
            _selectedFormIndex = 0;
            OnPropertyChanged(nameof(SelectedFormIndex));
            OnPropertyChanged(nameof(HasForms));
        }

        private static string FormDisplayName(string designator)
        {
            string s = designator.StartsWith("SPECIES_", StringComparison.Ordinal) ? designator.Substring(8) : designator;
            return s.Replace('_', ' ');
        }

        private void LoadHgeFormData()
        {
            if (!HgEngineProject.IsActive) return;
            StopPlayback();
            _loading = true;
            try
            {
                LoadSpriteDataFromHgeSource(_currentHgeSpeciesId);
                LoadAnimFromHgeSource(_currentHgeSpeciesId);
            }
            finally { _loading = false; }
            RaiseLayout();
            RaiseSprites();
        }

        // Preview only. Falls back to bundled art when the ROM's graphics can't be decoded.
        private BattleGroundRenderer _groundRenderer;
        private BattleBgRenderer _bgRenderer;

        public IReadOnlyList<string> ArenaTypeNames { get; } = BattleGroundRenderer.TerrainNames;

        private int _arenaTypeIndex = 2;   // "Lawn": a reasonably common default
        public int ArenaTypeIndex
        {
            get => _arenaTypeIndex;
            set { if (Set(ref _arenaTypeIndex, value)) ApplyArena(); }
        }

        private Bitmap _arenaBackdrop, _arenaGroundPlayer, _arenaGroundOpponent;
        public Bitmap ArenaBackdrop     { get => _arenaBackdrop;     private set => Set(ref _arenaBackdrop, value); }
        public Bitmap ArenaGroundPlayer   { get => _arenaGroundPlayer;   private set => Set(ref _arenaGroundPlayer, value); }
        public Bitmap ArenaGroundOpponent  { get => _arenaGroundOpponent;  private set => Set(ref _arenaGroundOpponent, value); }

        private double _arenaGroundPlayerLeft, _arenaGroundPlayerTop, _arenaGroundOpponentLeft, _arenaGroundOpponentTop;
        public double ArenaGroundPlayerLeft   { get => _arenaGroundPlayerLeft;   private set { if (Set(ref _arenaGroundPlayerLeft, value)) OnPropertyChanged(nameof(ArenaGroundPlayerX)); } }
        public double ArenaGroundPlayerTop    { get => _arenaGroundPlayerTop;    private set => Set(ref _arenaGroundPlayerTop, value); }
        public double ArenaGroundOpponentLeft  { get => _arenaGroundOpponentLeft;  private set { if (Set(ref _arenaGroundOpponentLeft, value)) OnPropertyChanged(nameof(ArenaGroundOpponentX)); } }
        public double ArenaGroundOpponentTop   { get => _arenaGroundOpponentTop;   private set => Set(ref _arenaGroundOpponentTop, value); }
    }
}
