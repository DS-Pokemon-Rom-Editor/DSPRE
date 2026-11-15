using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>A HeartGold and SoulSilver headbutt file: 12 normal and 6 special slots, and the normal and special
    /// tree groups, each tree placed by a global x and y.</summary>
    public partial class HeadbuttEncounterViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private Window _owner;
        private bool _suppress;
        private HeadbuttEncounterFile _file;

        public ObservableCollection<string> FileNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Species { get; } = new ObservableCollection<string>();
        public ObservableCollection<HeadbuttEncRow> NormalEncounters { get; } = new ObservableCollection<HeadbuttEncRow>();
        public ObservableCollection<HeadbuttEncRow> SpecialEncounters { get; } = new ObservableCollection<HeadbuttEncRow>();

        public void RefreshNames()
        {
            if (Species.Count == 0) return;
            ListSync.Apply(Species, GetPokemonNames());
            foreach (HeadbuttEncRow row in NormalEncounters) row.Reselect();
            foreach (HeadbuttEncRow row in SpecialEncounters) row.Reselect();
        }
        public ObservableCollection<string> NormalTreeGroups { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> SpecialTreeGroups { get; } = new ObservableCollection<string>();
        public ObservableCollection<HeadbuttTreeRow> SelectedGroupTrees { get; } = new ObservableCollection<HeadbuttTreeRow>();

        private bool _available;
        public bool IsAvailable { get => _available; private set => Set(ref _available, value); }

        private int _selFile = -1;
        public int SelectedFileIndex
        {
            get => _selFile;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (value == _selFile) return;
                if (_dirty && !_suppress && value >= 0 && _selFile >= 0)
                {
                    // Snap the list back to the file still loaded until the user has answered.
                    int requested = value;
                    RecordSwitchGuard.SnapBack(() => _selFile, v => _selFile = v, () => OnPropertyChanged(nameof(SelectedFileIndex)));
                    _ = SwitchFileAsync(requested);
                    return;
                }
                if (Set(ref _selFile, value) && !_suppress && value >= 0) LoadFile(value);
            }
        }

        private async Task SwitchFileAsync(int requested)
        {
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, _owner, "headbutt file")) return;
            SetClean();
            if (Set(ref _selFile, requested, nameof(SelectedFileIndex))) LoadFile(requested);
        }

        private bool _specialGroupActive;
        private int _selGroup = -1;
        public int SelectedNormalGroupIndex { get => _specialGroupActive ? -1 : _selGroup; set { if (value >= 0) { _specialGroupActive = false; _selGroup = value; ShowGroupTrees(); } } }
        public int SelectedSpecialGroupIndex { get => _specialGroupActive ? _selGroup : -1; set { if (value >= 0) { _specialGroupActive = true; _selGroup = value; ShowGroupTrees(); } } }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Headbutt file {_selFile}";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); if (_selFile >= 0) LoadFile(_selFile); }
        private void Dirty()
        {
            _undo?.Record();
            bool dirty = _undo?.IsDirty ?? true;
            if (_dirty == dirty) return;
            _dirty = dirty;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void BuildEncounterRows()
        {
            NormalEncounters.Clear();
            for (int i = 0; i < _file.NormalEncounters.Count; i++)
                NormalEncounters.Add(new HeadbuttEncRow($"Normal {i + 1}", _file.NormalEncounters[i], Species, Dirty));
            SpecialEncounters.Clear();
            for (int i = 0; i < _file.SpecialEncounters.Count; i++)
                SpecialEncounters.Add(new HeadbuttEncRow($"Special {i + 1}", _file.SpecialEncounters[i], Species, Dirty));
        }
        private void OnTreeChanged() { Dirty(); RefreshTreeMarkers(); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        public bool ShowForms => HgEngineProject.IsActive;

        public HeadbuttEncounterViewModel() { }
        public HeadbuttEncounterViewModel(bool _) { }

        /// <summary>Headbutt file to open once loaded (set before SetupAsync; e.g. from a "Go to Headbutt #N" jump), or -1 for the first with trees.</summary>
        public int InitialIndex { get; set; } = -1;

        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            try
            {
                if (GameFamily != GameFamilies.HGSS)
                {
                    StatusText = "Headbutt trees exist only in HeartGold and SoulSilver.";
                    return;
                }
                IsAvailable = true;
                DSUtils.TryUnpackNarcs(new List<DirNames> {
                    DirNames.Headbutt, DirNames.Maps, DirNames.Matrices, DirNames.AreaData,
                    DirNames.DynamicHeaders, DirNames.ExteriorBuildingModels, DirNames.InteriorBuildingModels,
                    DirNames.BuildingTextures, DirNames.MapTextures });
                foreach (string n in GetPokemonNames()) Species.Add(n);
                int count = Filesystem.GetHeadbuttCount();
                for (int i = 0; i < count; i++) FileNames.Add("Headbutt file " + i);
                StatusText = $"{count} headbutt files.";
                if (count > 0) SelectedFileIndex = System.Math.Clamp(InitialIndex < 0 ? FirstWithTrees(count) : InitialIndex, 0, count - 1);
            }
            catch (Exception ex) when (AppLogger.Noted("HeadbuttEncounterViewModel.SetupAsync", ex))
            {
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowErrorAsync($"Couldn't set up the Headbutt editor:\n{ex.Message}", "Special encounters editor");
            }
        }

        /// <summary>Headbutt files go by header number; this makes an empty one for every header that has none.</summary>
        public async Task FillMissingAsync()
        {
            if (HgEngineProject.IsActive) return;
            int headers = GetHeaderCount();
            int have = Filesystem.GetHeadbuttCount();
            if (have >= headers) { await DialogHelper.ShowInfoAsync($"Every header up to {headers - 1} has a headbutt file.", "Headbutt editor"); return; }
            string which = headers - have == 1 ? $"an empty headbutt file {have} for header {have}" : $"empty headbutt files {have} to {headers - 1}, one for each header without one";
            if (!await DialogHelper.AskYesNoAsync($"Add {which}?", "Headbutt editor")) return;
            try
            {
                int made = NewProjectFiles.FillHeadbuttFiles(headers);
                for (int i = FileNames.Count; i < Filesystem.GetHeadbuttCount(); i++) FileNames.Add("Headbutt file " + i);
                StatusText = $"Added {made} empty headbutt files.";
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                await DialogHelper.ShowErrorAsync("Couldn't add headbutt files:\n" + ex.Message, "Special encounters editor");
            }
        }

        // Opened without a file in mind: start on the first file with trees, since file 0 is empty.
        private static int FirstWithTrees(int count)
        {
            for (int i = 0; i < count; i++)
            {
                try
                {
                    HeadbuttEncounterFile file;
                    if (HgEngineProject.IsActive) { if (!HgEngineHeadbutt.TryLoad(i, out file, out _)) continue; }
                    else file = new HeadbuttEncounterFile((ushort)i);
                    if (file.NormalTreeGroups.Count > 0 || file.SpecialTreeGroups.Count > 0) return i;
                }
                catch (System.IO.IOException) { }   // a file that can't be read is skipped
            }
            return 0;
        }

        private void SelectFirstGroup()
        {
            if (_file.NormalTreeGroups.Count > 0) SelectedNormalGroupIndex = 0;
            else if (_file.SpecialTreeGroups.Count > 0) SelectedSpecialGroupIndex = 0;
            else ShowGroupTrees();
        }

        private void LoadFile(int index)
        {
            try
            {
                // With hg-engine the packed NARC is stale, so read the checkout's data/Headbutt.c instead.
                _hgeReadError = null;
                if (HgEngineProject.IsActive)
                {
                    if (!HgEngineHeadbutt.TryLoad(index, out _file, out string err))
                    {
                        _file = new HeadbuttEncounterFile();
                        _hgeReadError = err;
                        AppLogger.Error($"hg-engine headbutt read failed (file {index}): {err}");
                    }
                }
                else
                {
                    _file = new HeadbuttEncounterFile((ushort)index);
                }
                BuildEncounterRows();
                RefreshGroups();
                SetClean();
                ResetUndo();
                StatusText = _hgeReadError != null
                    ? $"Headbutt file {index} couldn't be read from Headbutt.c: {_hgeReadError}"
                    : "";
                OnPropertyChanged(nameof(UnsavedChangesDescription));
                // The map renders once per file, as in the Event editor; picking a group only re-marks trees.
                ResolveMatrix();
                DisplayMap();
                SelectFirstGroup();
            }
            catch (Exception ex)
            {
                CrashReporter.LogHandled(ex);
                _ = DialogHelper.ShowErrorAsync($"Couldn't load headbutt file {index}:\n{ex.Message}", "Special encounters editor");
            }
        }
    }
}
