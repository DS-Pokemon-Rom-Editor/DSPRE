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

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>
    /// Avalonia port of the WinForms <c>SafariZoneEditor</c> (HGSS). Edits the five
    /// encounter groups (Grass / Surf / Old Rod / Good Rod / Super Rod) of a selected
    /// Safari Zone area file. Embedded as a tab in the Encounters editor.
    /// </summary>
    public class SafariZoneEncounterViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
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
            set { if (Set(ref _selectedFileIndex, value) && !_suppress && value >= 0) LoadFile(value); }
        }

        // ── Dirty tracking ───────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Safari Zone Editor";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetDirty() { if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // ── Constructors ──────────────────────────────────────────────────────────
        public SafariZoneEncounterViewModel()
        {
            GrassVM = new SafariZoneGroupViewModel(SpeciesNames);
            SurfVM = new SafariZoneGroupViewModel(SpeciesNames);
            OldRodVM = new SafariZoneGroupViewModel(SpeciesNames);
            GoodRodVM = new SafariZoneGroupViewModel(SpeciesNames);
            SuperRodVM = new SafariZoneGroupViewModel(SpeciesNames);
            if (Design.IsDesignMode) FileNames.Add("Safari Zone");
        }

        public SafariZoneEncounterViewModel(bool _) : this()
        {
            foreach (var g in Groups) g.Changed += (s, e) => SetDirty();
        }

        // ── Setup ────────────────────────────────────────────────────────────────
        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.safariZone, DirNames.textArchives });

                SpeciesNames.Clear();
                foreach (var n in GetPokemonNames()) SpeciesNames.Add(n);

                FileNames.Clear();
                int count = Filesystem.GetSafariZoneCount();
                for (int i = 0; i < count; i++)
                    FileNames.Add(SafariZoneEncounterFile.Names.TryGetValue(i, out var nm) ? nm : $"Safari Zone {i}");

                if (FileNames.Count > 0) SelectedFileIndex = 0;
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Failed to load Safari Zone data:\n{ex.Message}", "Safari Zone Error");
            }
        }

        private bool UseHgEngineSource => DSPRE.HgEngine.HgEngineProject.IsActive;

        private void LoadFile(int id)
        {
            try
            {
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
            }
            catch (Exception ex)
            {
                _ = DialogHelper.ShowError($"Failed to load Safari Zone file {id}:\n{ex.Message}", "Safari Zone Error");
            }
        }

        private void BindGroups()
        {
            GrassVM.SetData(_file.grassEncounterGroup);
            SurfVM.SetData(_file.surfEncounterGroup);
            OldRodVM.SetData(_file.oldRodEncounterGroup);
            GoodRodVM.SetData(_file.goodRodEncounterGroup);
            SuperRodVM.SetData(_file.superRodEncounterGroup);
        }

        // hg-engine isn't one of DSPRE's owned domains for the packed-ROM narc, so the vanilla read above
        // would show a stale packed-ROM snapshot rather than the checkout's real data/SafariEncounters.c.
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
                if (DSPRE.HgEngine.HgEngineSafariEncounters.TryLoadGroup(areaId, type, out var group, out string error))
                    vm.SetData(group);
                else
                {
                    AppLogger.Error($"hg-engine safari zone read failed ({type}, area {areaId}): {error}");
                    vm.SetData(new SafariZoneEncounterGroup());
                }
            }
        }

        // ── Save / import ──────────────────────────────────────────────────────────
        public void Save()
        {
            if (UseHgEngineSource)
            {
                SaveOne(GrassVM, DSPRE.HgEngine.HgEngineSafariEncounters.RodType.Land);
                SaveOne(SurfVM, DSPRE.HgEngine.HgEngineSafariEncounters.RodType.Surf);
                SaveOne(OldRodVM, DSPRE.HgEngine.HgEngineSafariEncounters.RodType.OldRod);
                SaveOne(GoodRodVM, DSPRE.HgEngine.HgEngineSafariEncounters.RodType.GoodRod);
                SaveOne(SuperRodVM, DSPRE.HgEngine.HgEngineSafariEncounters.RodType.SuperRod);
                SetClean();
                return;
            }
            if (_file == null) return;
            _file.SaveToFile();
            SetClean();
        }

        private void SaveOne(SafariZoneGroupViewModel vm, DSPRE.HgEngine.HgEngineSafariEncounters.RodType type)
        {
            var group = vm.CurrentGroup;
            if (group == null) return;
            if (!DSPRE.HgEngine.HgEngineSafariEncounters.TrySaveGroup(_selectedFileIndex, type, group, out string error))
                AppLogger.Error($"hg-engine safari zone write failed ({type}, area {_selectedFileIndex}): {error}");
        }

        public async Task SaveAsAsync()
        {
            if (_file == null) return;
            var filter = new FilePickerFileType("Binary files") { Patterns = new[] { "*.bin" } };
            string path = await DialogHelper.SaveFile(_owner, "Save Safari Zone As", new[] { filter }, "safari_zone.bin");
            if (path == null) return;
            _file.SaveToFile(path);
        }

        public async Task ImportAsync()
        {
            if (_file == null) return;
            var filter = new FilePickerFileType("Binary files") { Patterns = new[] { "*.bin" } };
            string path = await DialogHelper.OpenFile(_owner, "Import Safari Zone File", new[] { filter });
            if (path == null) return;

            try
            {
                _file = new SafariZoneEncounterFile(path);
                BindGroups();
                SetDirty();
                await DialogHelper.ShowInfo("Safari Zone file imported successfully!", "Import Complete");
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Error importing file: {ex.Message}", "Import Error");
            }
        }
    }
}
