using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Avalonia port of the WinForms <c>BuildingEditor</c>. Browses the building model NARC
    /// (exterior, or interior on HG/SS), renders the selected model in 3D with either its embedded
    /// textures or a chosen building tileset, and imports / exports the raw NSBMD.
    /// </summary>
    public class BuildingEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private Window _owner;
        private bool _suppress;
        private byte[] _currentData;

        public event EventHandler ModelLoaded;
        public NsbmdRenderModel Model3D { get; private set; }

        public ObservableCollection<string> Buildings { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Textures { get; } = new ObservableCollection<string>();

        public bool IsHGSS => gameFamily == GameFamilies.HGSS;

        private bool _interior;
        public bool Interior { get => _interior; set { if (Set(ref _interior, value)) { RefreshBuildings(); SelectedBuildingIndex = Buildings.Count > 0 ? 0 : -1; } } }

        private int _texIndex;
        public int SelectedTextureIndex { get => _texIndex; set { if (Set(ref _texIndex, value) && !_suppress && _selBuilding >= 0) LoadModel(_selBuilding); } }

        private int _selBuilding = -1;
        public int SelectedBuildingIndex { get => _selBuilding; set { if (Set(ref _selBuilding, value) && !_suppress && value >= 0) LoadModel(value); } }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // No in-place editing (import/export only), so never "dirty".
        public bool HasUnsavedChanges => false;
        public string UnsavedChangesDescription => "Building Editor";
        public void SaveChanges() { }
        public void DiscardChanges() { }

        public BuildingEditorViewModel() { }
        public BuildingEditorViewModel(bool _) { }

        /// <summary>Building model to select once loaded (set before SetupAsync; e.g. from a "Go to Building #N" jump).</summary>
        public int InitialIndex { get; set; }

        private string BuildingDir() => gameDirs[_interior ? DirNames.interiorBuildingModels : DirNames.exteriorBuildingModels].unpackedDir;

        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            try
            {
                var dirs = new List<DirNames> { DirNames.exteriorBuildingModels, DirNames.buildingTextures };
                if (IsHGSS) dirs.Add(DirNames.interiorBuildingModels);
                DSUtils.TryUnpackNarcs(dirs);

                _suppress = true;
                Textures.Add("Embedded textures");
                int texCount = Filesystem.GetBuildingTexturesCount();
                for (int i = 0; i < texCount; i++) Textures.Add("Texture " + i);
                _texIndex = 0; OnPropertyChanged(nameof(SelectedTextureIndex));
                _suppress = false;

                RefreshBuildings();
                StatusText = $"{Buildings.Count} building models.";
                if (Buildings.Count > 0)
                    SelectedBuildingIndex = System.Math.Clamp(InitialIndex, 0, Buildings.Count - 1);
            }
            catch (Exception ex)
            {
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowError($"Failed to set up Building Editor:\n{ex.Message}", "Building Editor");
            }
        }

        private void RefreshBuildings()
        {
            Buildings.Clear();
            try
            {
                string dir = BuildingDir();
                if (!Directory.Exists(dir)) return;
                int count = Directory.GetFiles(dir).Length;
                for (int i = 0; i < count; i++) Buildings.Add("Building " + i.ToString("D3"));
            }
            catch (Exception ex) { AppLogger.Error("Building list failed: " + ex.Message); }
        }

        private void LoadModel(int index)
        {
            Model3D = null;
            try
            {
                string path = Path.Combine(BuildingDir(), index.ToString("D4"));
                if (!File.Exists(path)) { ModelLoaded?.Invoke(this, EventArgs.Empty); return; }
                _currentData = File.ReadAllBytes(path);
                var nsbmd = NSBMDLoader.LoadNSBMD(new MemoryStream(_currentData));
                BindTextures(nsbmd, _currentData, _texIndex);
                if (nsbmd.models != null && nsbmd.models.Length > 0)
                    Model3D = NsbmdGeometry.BuildModel(nsbmd.models[0]);
                StatusText = $"Building {index}.";
            }
            catch (Exception ex)
            {
                StatusText = "Render failed: " + ex.Message;
                AppLogger.Error("Building render failed: " + ex.Message);
            }
            ModelLoaded?.Invoke(this, EventArgs.Empty);
        }

        private static void BindTextures(NSBMD nsbmd, byte[] modelData, int texIndex)
        {
            try
            {
                byte[] tex;
                if (texIndex <= 0)
                {
                    tex = NSBUtils.GetTexturesFromTexturedNSBMD(modelData);
                    if (tex == null || tex.Length <= 4) return;   // model has no embedded textures
                }
                else
                {
                    string tp = Path.Combine(gameDirs[DirNames.buildingTextures].unpackedDir, (texIndex - 1).ToString("D4"));
                    if (!File.Exists(tp)) return;
                    tex = File.ReadAllBytes(tp);
                }
                nsbmd.materials = NSBTXLoader.LoadNsbtx(new MemoryStream(tex), out nsbmd.Textures, out nsbmd.Palettes);
                nsbmd.MatchTextures();
            }
            catch (Exception ex) { AppLogger.Error("Building texture bind failed: " + ex.Message); }
        }

        public async Task ImportAsync()
        {
            if (_selBuilding < 0) return;
            var filter = new FilePickerFileType("NSBMD model") { Patterns = new[] { "*.nsbmd", "*.bin", "*.*" } };
            string path = await DialogHelper.OpenFile(_owner, "Import building model (NSBMD)", new[] { filter });
            if (path == null) return;
            try
            {
                File.Copy(path, Path.Combine(BuildingDir(), _selBuilding.ToString("D4")), true);
                LoadModel(_selBuilding);
                StatusText = "Imported building model.";
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error"); }
        }

        public async Task ExportAsync()
        {
            if (_selBuilding < 0) return;
            var filter = new FilePickerFileType("NSBMD model") { Patterns = new[] { "*.nsbmd" } };
            string path = await DialogHelper.SaveFile(_owner, "Export building model (NSBMD)", new[] { filter }, $"building_{_selBuilding:D4}.nsbmd");
            if (path == null) return;
            try { File.Copy(Path.Combine(BuildingDir(), _selBuilding.ToString("D4")), path, true); StatusText = "Exported."; }
            catch (Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }
    }
}
