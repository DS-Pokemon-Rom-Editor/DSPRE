using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using DSPRE.HgEngine;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>
    /// A Safari Zone area's five encounter groups (HGSS): grass, surfing and the three rods. A tab of the
    /// Special encounters editor.
    /// </summary>
    public class SafariZoneEncounterViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private Window _owner;
        private bool _suppress;
        private SafariZoneEncounterFile _file;

        public ObservableCollection<string> FileNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> SpeciesNames { get; } = new ObservableCollection<string>();

        public void RefreshNames()
        {
            if (SpeciesNames.Count == 0) return;
            SafariZoneGroupViewModel[] groups = { GrassVM, SurfVM, OldRodVM, GoodRodVM, SuperRodVM };
            foreach (SafariZoneGroupViewModel g in groups) g?.BeginNameRefresh();
            try { DSPRE.Avalonia.Data.ListSync.Apply(SpeciesNames, GetPokemonNames()); }
            finally { foreach (SafariZoneGroupViewModel g in groups) g?.EndNameRefresh(); }
        }

        public SafariZoneGroupViewModel GrassVM { get; }
        public SafariZoneGroupViewModel SurfVM { get; }
        public SafariZoneGroupViewModel OldRodVM { get; }
        public SafariZoneGroupViewModel GoodRodVM { get; }
        public SafariZoneGroupViewModel SuperRodVM { get; }

        private SafariZoneGroupViewModel[] Groups => new[] { GrassVM, SurfVM, OldRodVM, GoodRodVM, SuperRodVM };

        private int _selectedFileIndex = -1;
        public int SelectedFileIndex
        {
            get => _selectedFileIndex;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (value == _selectedFileIndex) return;
                if (_dirty && !_suppress && value >= 0 && _selectedFileIndex >= 0)
                {
                    // Snap the list back to the area still loaded until the user has answered.
                    int requested = value;
                    RecordSwitchGuard.SnapBack(() => _selectedFileIndex, v => _selectedFileIndex = v, () => OnPropertyChanged(nameof(SelectedFileIndex)));
                    _ = SwitchFileAsync(requested);
                    return;
                }
                if (Set(ref _selectedFileIndex, value) && !_suppress && value >= 0) LoadFile(value);
            }
        }

        private async Task SwitchFileAsync(int requested)
        {
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, _owner, "area")) return;
            SetClean();
            if (Set(ref _selectedFileIndex, requested, nameof(SelectedFileIndex))) LoadFile(requested);
        }

        // ── Dirty tracking ───────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Safari Zone encounters";
        public void SaveChanges() => Save();
        // Closing the window saves through here, so a failed save is reported, not only logged.
        public async Task<bool> SaveChangesAsync() { await SaveAsync(); return !HasUnsavedChanges; }
        // Edits live in the loaded area, so discarding reads it again.
        public void DiscardChanges() { if (_dirty && _selectedFileIndex >= 0) LoadFile(_selectedFileIndex); SetClean(); }
        private void SetDirty() { if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // ── Undo / redo: the loaded area, as its file stores it ─────────────────
        private DSPRE.Avalonia.ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private byte[] TakeState()
        {
            SafariZoneEncounterFile f = new SafariZoneEncounterFile(_selectedFileIndex, System.Array.Empty<byte>())
            {
                GrassEncounterGroup = GrassVM.CurrentGroup, SurfEncounterGroup = SurfVM.CurrentGroup,
                OldRodEncounterGroup = OldRodVM.CurrentGroup, GoodRodEncounterGroup = GoodRodVM.CurrentGroup,
                SuperRodEncounterGroup = SuperRodVM.CurrentGroup,
            };
            return f.ToByteArray();
        }

        private void ApplyState(byte[] state)
        {
            SafariZoneEncounterFile f = new SafariZoneEncounterFile(_selectedFileIndex, state);
            if (_file != null) _file = f;
            GrassVM.SetData(f.GrassEncounterGroup, keepSelection: true);
            SurfVM.SetData(f.SurfEncounterGroup, keepSelection: true);
            OldRodVM.SetData(f.OldRodEncounterGroup, keepSelection: true);
            GoodRodVM.SetData(f.GoodRodEncounterGroup, keepSelection: true);
            SuperRodVM.SetData(f.SuperRodEncounterGroup, keepSelection: true);
            if (_undo.IsDirty) SetDirty(); else SetClean();
        }

        private void ResetUndo() { _undo = new DSPRE.Avalonia.ByteStateUndo(TakeState, ApplyState, RaiseUndo); RaiseUndo(); }

        private void Edited()
        {
            _undo?.Record();
            if (_undo == null || _undo.IsDirty) SetDirty(); else SetClean();
        }

        // ── Constructors ──────────────────────────────────────────────────────────
        public SafariZoneEncounterViewModel()
        {
            GrassVM = new SafariZoneGroupViewModel(SpeciesNames);
            SurfVM = new SafariZoneGroupViewModel(SpeciesNames) { WarnWhenNoSlots = true };
            OldRodVM = new SafariZoneGroupViewModel(SpeciesNames);
            GoodRodVM = new SafariZoneGroupViewModel(SpeciesNames);
            SuperRodVM = new SafariZoneGroupViewModel(SpeciesNames);
            if (Design.IsDesignMode) FileNames.Add("Safari Zone");
        }

        public SafariZoneEncounterViewModel(bool _) : this()
        {
            foreach (SafariZoneGroupViewModel g in Groups) g.Changed += (s, e) => Edited();
        }

        // ── Setup ────────────────────────────────────────────────────────────────
        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.SafariZone, DirNames.TextArchives });

                SpeciesNames.Clear();
                foreach (string n in GetPokemonNames()) SpeciesNames.Add(n);

                FileNames.Clear();
                int count = Filesystem.GetSafariZoneCount();
                for (int i = 0; i < count; i++)
                    FileNames.Add(SafariZoneEncounterFile.Names.TryGetValue(i, out string nm) ? nm : $"Safari Zone {i}");

                if (FileNames.Count > 0) SelectedFileIndex = 0;
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowErrorAsync($"Couldn't load the Safari Zone data:\n{ex.Message}", "Special encounters editor");
            }
        }

        private bool UseHgEngineSource => DSPRE.HgEngine.HgEngineProject.IsActive;

        private void LoadFile(int id)
        {
            try
            {
                _hgeReadError = null;
                if (UseHgEngineSource)
                {
                    _file = null;
                    BindGroupsFromHgEngine(id);
                }
                else
                {
                    _file = new SafariZoneEncounterFile(id);
                    BindGroups();
                }
                SetClean();
                ResetUndo();
                if (_hgeReadError != null)
                    _ = DialogHelper.ShowErrorAsync($"{AreaName(id)} couldn't be read from SafariEncounters.c, so it can't be saved here:\n{_hgeReadError}", "Special encounters editor");
            }
            catch (Exception ex)
            {
                if (UseHgEngineSource) _hgeReadError ??= ex.Message;
                CrashReporter.LogHandled(ex);
                _ = DialogHelper.ShowErrorAsync($"Couldn't load Safari Zone file {id}:\n{ex.Message}", "Special encounters editor");
            }
        }

        private string AreaName(int id) => id >= 0 && id < FileNames.Count ? FileNames[id] : $"Area {id}";

        // Set when an area's source couldn't be read: saving the blank groups shown would replace it.
        private string _hgeReadError;

        private void BindGroups()
        {
            GrassVM.SetData(_file.GrassEncounterGroup);
            SurfVM.SetData(_file.SurfEncounterGroup);
            OldRodVM.SetData(_file.OldRodEncounterGroup);
            GoodRodVM.SetData(_file.GoodRodEncounterGroup);
            SuperRodVM.SetData(_file.SuperRodEncounterGroup);
        }

        // With hg-engine the packed NARC is stale, so read the checkout's data/SafariEncounters.c instead.
        private void BindGroupsFromHgEngine(int areaId)
        {
            BindOne(GrassVM, DSPRE.HgEngine.HgEngineSafariEncounters.RodType.Land);
            BindOne(SurfVM, DSPRE.HgEngine.HgEngineSafariEncounters.RodType.Surf);
            BindOne(OldRodVM, DSPRE.HgEngine.HgEngineSafariEncounters.RodType.OldRod);
            BindOne(GoodRodVM, DSPRE.HgEngine.HgEngineSafariEncounters.RodType.GoodRod);
            BindOne(SuperRodVM, DSPRE.HgEngine.HgEngineSafariEncounters.RodType.SuperRod);

            void BindOne(SafariZoneGroupViewModel vm, DSPRE.HgEngine.HgEngineSafariEncounters.RodType type)
            {
                vm.CanEditObjectSlotCount = false;
                if (DSPRE.HgEngine.HgEngineSafariEncounters.TryLoadGroup(areaId, type, out SafariZoneEncounterGroup group, out string error))
                    vm.SetData(group);
                else
                {
                    AppLogger.Error($"hg-engine safari zone read failed ({type}, area {areaId}): {error}");
                    _hgeReadError ??= $"{type}: {error}";
                    vm.SetData(SafariZoneEncounterGroup.Blank());
                }
            }
        }

        // ── Save / import ──────────────────────────────────────────────────────────
        public async Task SaveAsync()
        {
            if (UseHgEngineSource) { await SaveHgEngineAsync(); return; }
            if (!SaveCore(out string failure))
                await DialogHelper.ShowErrorAsync("Couldn't save the Safari Zone data:\n" + failure, "Special encounters editor");
        }

        public void Save()
        {
            if (UseHgEngineSource) { DialogHelper.Watch(SaveHgEngineAsync()); return; }
            if (!SaveCore(out string failure))
                AppLogger.Error("Safari Zone save failed: " + failure);
        }

        private async Task SaveHgEngineAsync()
        {
            if (_hgeReadError != null)
            {
                await DialogHelper.ShowErrorAsync($"{AreaName(_selectedFileIndex)} couldn't be read from SafariEncounters.c, so saving it would replace "
                    + "what is there:\n" + _hgeReadError, "Special encounters editor");
                return;
            }
            List<(HgEngineSafariEncounters.RodType, SafariZoneEncounterGroup)> groups = new List<(DSPRE.HgEngine.HgEngineSafariEncounters.RodType, SafariZoneEncounterGroup)>
            {
                (DSPRE.HgEngine.HgEngineSafariEncounters.RodType.Land, GrassVM.CurrentGroup),
                (DSPRE.HgEngine.HgEngineSafariEncounters.RodType.Surf, SurfVM.CurrentGroup),
                (DSPRE.HgEngine.HgEngineSafariEncounters.RodType.OldRod, OldRodVM.CurrentGroup),
                (DSPRE.HgEngine.HgEngineSafariEncounters.RodType.GoodRod, GoodRodVM.CurrentGroup),
                (DSPRE.HgEngine.HgEngineSafariEncounters.RodType.SuperRod, SuperRodVM.CurrentGroup),
            };
            int area = _selectedFileIndex;
            // One pass for the whole area, so a failure can't leave some rods written and others not.
            (bool saved, string error) = await HgEngineSave.RunAsync(() =>
                DSPRE.HgEngine.HgEngineSafariEncounters.TrySaveGroups(area, groups, out string sourceError) ? null : sourceError);
            if (saved)
            {
                _undo?.MarkSaved();
                SetClean();
                SaveNotice.Saved(UnsavedChangesDescription);
            }
            else if (error != null)
            {
                await DialogHelper.ShowErrorAsync("Couldn't save the Safari Zone data:\n" + error, "Special encounters editor");
            }
        }

        /// <summary>Writes everything and says what went wrong. Only a save that wrote every group
        /// clears the dirty flag, so a failure cannot pass for a save.</summary>
        private bool SaveCore(out string failure)
        {
            failure = null;
            try
            {
                if (_file == null) return true;
                if (!_file.SaveToFile())
                {
                    failure = "the encounter data couldn't be turned into a file.";
                    return false;
                }
                _undo?.MarkSaved();
                SetClean();
                SaveNotice.Saved(UnsavedChangesDescription);
                return true;
            }
            catch (Exception ex)
            {
                failure = ex.Message;
                return false;
            }
        }

        public async Task SaveAsAsync()
        {
            if (_file == null) return;
            FilePickerFileType filter = new FilePickerFileType("Binary files") { Patterns = new[] { "*.bin" } };
            string path = await DialogHelper.SaveFileAsync(_owner, "Save Safari Zone as", new[] { filter }, "safari_zone.bin");
            if (path == null) return;
            try
            {
                if (!_file.SaveToFile(path, showSuccessMessage: false))
                    await DialogHelper.ShowErrorAsync(
                        "The encounter data couldn't be turned into a file.", "Special encounters editor");
                else SaveNotice.Exported(path);
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowErrorAsync($"Couldn't write {path}:\n{ex.Message}", "Special encounters editor");
            }
        }

        public async Task ImportAsync()
        {
            if (_file == null) return;
            FilePickerFileType filter = new FilePickerFileType("Binary files") { Patterns = new[] { "*.bin" } };
            string path = await DialogHelper.OpenFileAsync(_owner, "Import Safari Zone file", new[] { filter });
            if (path == null) return;

            try
            {
                // The imported file saves over the area it was imported into.
                _file = new SafariZoneEncounterFile(path) { ID = _file.ID };
                BindGroups();
                Edited();
                SaveNotice.Imported("Safari Zone encounters", path);
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowErrorAsync($"Couldn't import the file: {ex.Message}", "Special encounters editor");
            }
        }
    }
}
