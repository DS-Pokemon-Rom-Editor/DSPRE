using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
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

namespace DSPRE.Avalonia.ViewModels.World
{
    /// <summary>
    /// Avalonia port of the WinForms <c>BuildingEditor</c>. Browses the building model NARC
    /// (exterior, or interior on HG/SS), renders the selected model in 3D with either its embedded
    /// textures or a chosen building tileset, and imports / exports the raw NSBMD.
    /// </summary>
    public class BuildingEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
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

        // Import writes the unpacked archive at once for the preview, so the pre-import bytes are kept for Discard.
        private readonly Dictionary<string, byte[]> _originals = new();
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Building models";
        public void SaveChanges()
        {
            if (!_dirty) return;
            _originals.Clear();
            _dirty = false;
            _undo?.MarkSaved();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            SaveNotice.Saved(UnsavedChangesDescription);
        }
        public void DiscardChanges()
        {
            foreach (var (path, bytes) in _originals)
            {
                try { File.WriteAllBytes(path, bytes); }
                catch (Exception ex) { AppLogger.Error("Building discard: " + ex.Message); }
            }
            _originals.Clear();
            _dirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            if (_selBuilding >= 0) LoadModel(_selBuilding);
            ResetUndo();
        }

        // ── Undo / redo: every imported model file as it stands ──
        private readonly Dictionary<string, byte[]> _historyOriginals = new();
        private DSPRE.Avalonia.ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private static byte[] ReadFile(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;

        private void Keep(string path)
        {
            byte[] now = ReadFile(path);
            if (now != null && !_originals.ContainsKey(path)) _originals[path] = now;
            if (!_historyOriginals.ContainsKey(path)) _historyOriginals[path] = now;
        }

        private byte[] TakeState() => DSPRE.Avalonia.UndoJson.Take(_historyOriginals.Keys.ToDictionary(p => p, ReadFile));

        private void ApplyState(byte[] state)
        {
            var s = DSPRE.Avalonia.UndoJson.Read<Dictionary<string, byte[]>>(state);
            foreach (var path in _historyOriginals.Keys)
            {
                byte[] want = s.TryGetValue(path, out byte[] b) ? b : _historyOriginals[path];
                try { if (want != null) File.WriteAllBytes(path, want); }
                catch (Exception ex) { AppLogger.Error("Building undo: " + ex.Message); }
            }
            if (_selBuilding >= 0) LoadModel(_selBuilding);
            RecountDirty();
        }

        // Unsaved while any imported file differs from what it held when last saved.
        private void RecountDirty()
        {
            _dirty = _originals.Any(kv => { var now = ReadFile(kv.Key); return now == null || !now.AsSpan().SequenceEqual(kv.Value); });
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void ResetUndo()
        {
            _historyOriginals.Clear();
            _undo = new DSPRE.Avalonia.ByteStateUndo(TakeState, ApplyState, RaiseUndo);
            RaiseUndo();
        }

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
                ResetUndo();
                StatusText = $"{Buildings.Count} building models.";
                if (Buildings.Count > 0)
                    SelectedBuildingIndex = System.Math.Clamp(InitialIndex, 0, Buildings.Count - 1);

                // The box clears its own selection when its list arrives without telling the binding, so
                // the pick is pushed out again afterwards or it opens showing no texture pack at all.
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (Textures.Count == 0) return;
                    _suppress = true;
                    _texIndex = -1; OnPropertyChanged(nameof(SelectedTextureIndex));
                    _texIndex = 0;  OnPropertyChanged(nameof(SelectedTextureIndex));
                    _suppress = false;
                }, global::Avalonia.Threading.DispatcherPriority.Background);
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
                BindTextures(nsbmd, _currentData, _texIndex, out bool painted);
                if (nsbmd.models != null && nsbmd.models.Length > 0)
                    Model3D = NsbmdGeometry.BuildModel(nsbmd.models[0]);
                StatusText = painted
                    ? $"Building {index}."
                    : $"Building {index}, shown unpainted: pick a texture pack above to see its colours.";
            }
            catch (Exception ex)
            {
                StatusText = "Render failed: " + ex.Message;
                AppLogger.Error("Building render failed: " + ex.Message);
            }
            ModelLoaded?.Invoke(this, EventArgs.Empty);
        }

        private static void BindTextures(NSBMD nsbmd, byte[] modelData, int texIndex, out bool painted)
        {
            painted = true;
            try
            {
                byte[] tex;
                if (texIndex <= 0)
                {
                    tex = NSBUtils.GetTexturesFromTexturedNSBMD(modelData);
                    if (tex == null || tex.Length <= 4) { painted = false; return; }
                }
                else
                {
                    string tp = Path.Combine(gameDirs[DirNames.buildingTextures].unpackedDir, (texIndex - 1).ToString("D4"));
                    if (!File.Exists(tp)) { painted = false; return; }
                    tex = File.ReadAllBytes(tp);
                }
                nsbmd.materials = NSBTXLoader.LoadNsbtx(new MemoryStream(tex), out nsbmd.Textures, out nsbmd.Palettes);
                nsbmd.MatchTextures();
            }
            catch (Exception ex) { painted = false; AppLogger.Error("Building texture bind failed: " + ex.Message); }
        }

        public async Task ImportAsync()
        {
            if (_selBuilding < 0) return;
            var filter = new FilePickerFileType("Model (.nsbmd)") { Patterns = new[] { "*.nsbmd", "*.bin", "*.*" } };
            string path = await DialogHelper.OpenFile(_owner, "Import building model", new[] { filter });
            if (path == null) return;
            try
            {
                string target = Path.Combine(BuildingDir(), _selBuilding.ToString("D4"));
                Keep(target);
                File.Copy(path, target, true);
                _undo?.Record();
                RecountDirty();
                LoadModel(_selBuilding);
                StatusText = "Imported building model. Save to keep it.";
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error"); }
        }

        public async Task ExportAsync()
        {
            if (_selBuilding < 0) return;
            var filter = new FilePickerFileType("Model (.nsbmd)") { Patterns = new[] { "*.nsbmd" } };
            string path = await DialogHelper.SaveFile(_owner, "Export building model", new[] { filter }, $"building_{_selBuilding:D4}.nsbmd");
            if (path == null) return;
            try { File.Copy(Path.Combine(BuildingDir(), _selBuilding.ToString("D4")), path, true); StatusText = "Exported."; }
            catch (Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }
    }
}
