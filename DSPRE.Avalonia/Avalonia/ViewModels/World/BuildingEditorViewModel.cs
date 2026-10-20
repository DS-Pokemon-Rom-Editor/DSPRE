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
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.World
{
    /// <summary>
    /// Avalonia port of the WinForms <c>BuildingEditor</c>. Browses the building model NARC
    /// (exterior, or interior on HG/SS), renders the selected model in 3D with either its embedded
    /// textures or a building texture pack, imports NSBMD or OBJ, and exports NSBMD, DAE or glTF.
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
        private IReadOnlyList<BuildingModelTextureSet> _textureSets = Array.Empty<BuildingModelTextureSet>();

        public event EventHandler ModelLoaded;
        public NsbmdRenderModel Model3D { get; private set; }

        public ObservableCollection<string> Buildings { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Textures { get; } = new ObservableCollection<string>();

        public bool IsHGSS => GameFamily == GameFamilies.HGSS;

        private bool _interior;
        public bool Interior
        {
            get => _interior;
            set
            {
                if (!Set(ref _interior, value)) return;
                OnPropertyChanged(nameof(ArchiveTab));
                RefreshBuildings();
                _selBuilding = -1;
                SelectedBuildingIndex = Buildings.Count > 0 ? 0 : -1;
            }
        }

        /// <summary>0 for the exterior tab, 1 for the interior one.</summary>
        public int ArchiveTab { get => _interior ? 1 : 0; set => Interior = value == 1; }

        private int _texIndex;
        public int SelectedTextureIndex { get => _texIndex; set { if (Set(ref _texIndex, value) && !_suppress && _selBuilding >= 0) LoadModel(_selBuilding); } }

        private int _selBuilding = -1;
        public int SelectedBuildingIndex { get => _selBuilding; set { if (Set(ref _selBuilding, value) && !_suppress && value >= 0) LoadModel(value); } }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // An import waits here until Save, and the preview reads it from here.
        private readonly DSPRE.Avalonia.Data.PendingArchiveWrites _pending = new();
        public bool HasUnsavedChanges => _pending.IsDirty;
        public string UnsavedChangesDescription
            => DSPRE.Avalonia.Data.StagedArchive.Join("Building models", false, DSPRE.Avalonia.Data.StagedArchive.Describe("building model", "building models", _pending.Pending(Archive)));
        public void SaveChanges()
        {
            if (!_pending.IsDirty) return;
            _pending.Save();
            SaveNotice.Saved(UnsavedChangesDescription);
        }
        public void DiscardChanges() => _pending.Discard();

        public bool CanUndo => _pending.CanUndo;
        public bool CanRedo => _pending.CanRedo;
        public void Undo() => _pending.Undo();
        public void Redo() => _pending.Redo();

        private void OnPendingChanged()
        {
            RefreshBuildings();
            if (_selBuilding >= 0) LoadModel(_selBuilding);
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        public BuildingEditorViewModel() { }
        public BuildingEditorViewModel(bool _) { _pending.Changed += OnPendingChanged; }

        /// <summary>Building model to select once loaded (set before SetupAsync; e.g. from a "Go to Building #N" jump).</summary>
        public int InitialIndex { get; set; }

        private DirNames Archive => _interior ? DirNames.InteriorBuildingModels : DirNames.ExteriorBuildingModels;
        private string BuildingDir() => GameDirs[Archive].unpackedDir;

        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            try
            {
                List<DirNames> dirs = new List<DirNames> { DirNames.ExteriorBuildingModels, DirNames.BuildingTextures };
                if (IsHGSS) dirs.Add(DirNames.InteriorBuildingModels);
                DSUtils.TryUnpackNarcs(dirs);
                try { _textureSets = BuildingModelTextureSets.ReadCurrentRom(); }
                catch (Exception ex) { AppLogger.Error("Building texture sets could not be read: " + ex.Message); }

                _suppress = true;
                Textures.Add("Automatic");
                int texCount = Filesystem.GetBuildingTexturesCount();
                for (int i = 0; i < texCount; i++) Textures.Add("Texture pack " + i);
                _texIndex = 0; OnPropertyChanged(nameof(SelectedTextureIndex));
                _suppress = false;

                RefreshBuildings();
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
                CrashReporter.LogHandled(ex);
                await DialogHelper.ShowErrorAsync($"Failed to set up Building Editor:\n{ex.Message}", "Building editor error");
            }
        }

        // Rebuilt after an import or undo too, so a replaced model shows its own name.
        private void RefreshBuildings()
        {
            bool wasSuppressed = _suppress;
            int keep = _selBuilding;
            _suppress = true;
            Buildings.Clear();
            try
            {
                string dir = BuildingDir();
                if (Directory.Exists(dir))
                {
                    int count = NarcAPI.Narc.MemberCount(dir);
                    for (int i = 0; i < count; i++)
                    {
                        string name = ModelAssets.NameInFile(_pending.ReadMember(Archive, i));
                        Buildings.Add(name == null ? i.ToString("D3") : $"{i:D3}  {name}");
                    }
                }
            }
            catch (Exception ex) { AppLogger.Error("Building list failed: " + ex.Message); }
            _selBuilding = -1;
            if (keep >= 0 && keep < Buildings.Count) SelectedBuildingIndex = keep;
            _suppress = wasSuppressed;
        }

        private void LoadModel(int index)
        {
            Model3D = null;
            try
            {
                _currentData = _pending.ReadMember(Archive, index);
                if (_currentData == null) { ModelLoaded?.Invoke(this, EventArgs.Empty); return; }
                NSBMD nsbmd = NSBMDLoader.LoadNSBMD(new MemoryStream(_currentData));
                byte[] tex = TexturesFor(index, _currentData, nsbmd, out int pack);
                bool painted = ModelAssets.Dress(nsbmd, tex);
                if (nsbmd.Models != null && nsbmd.Models.Length > 0)
                    Model3D = NsbmdGeometry.BuildModel(nsbmd.Models[0]);
                bool wantsTextures = nsbmd.Models != null && nsbmd.Models.Any(m => m.Textures.Any(t => !string.IsNullOrEmpty(t.Texname)));
                StatusText = !painted && !wantsTextures ? $"Building {index}, which uses no textures."
                    : !painted ? $"Building {index}, shown unpainted: pick a texture pack above to see its colours."
                    : _texIndex == 0 && pack >= 0 ? $"Building {index}, painted from texture pack {pack}." : $"Building {index}.";
            }
            catch (Exception ex)
            {
                StatusText = "Render failed: " + ex.Message;
                AppLogger.Error("Building render failed: " + ex.Message);
            }
            ModelLoaded?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// The pictures to paint a model with, or null for none. <paramref name="pack"/> is the texture pack
        /// they came from, or -1 for the model's own.
        /// </summary>
        private byte[] TexturesFor(int index, byte[] modelData, NSBMD model, out int pack)
        {
            pack = -1;
            if (_texIndex > 0)
            {
                pack = _texIndex - 1;
                return ModelAssets.TextureSet(AssetArchive, pack);
            }
            byte[] own = null;
            try { own = NSBUtils.GetTexturesFromTexturedNSBMD(modelData); }
            catch (Exception ex) { AppLogger.WarnOnce("BuildingEditor.OwnTextures", ex); }
            if (own != null && own.Length <= 4) own = null;
            if (own != null && OwnCovers(model, own)) return own;

            // The packs the ROM's areas pair with this model come first, then any pack holding everything it names.
            IEnumerable<int> listed = _textureSets.Where(s => s.Indoor == _interior && s.ModelIds.Contains(index))
                .Select(s => s.TextureSetId).OrderBy(id => id);
            foreach (int id in listed.Concat(Enumerable.Range(0, ModelAssets.TextureSetCount(AssetArchive))))
            {
                byte[] candidate = ModelAssets.TextureSet(AssetArchive, id);
                if (candidate != null && ModelAssets.Coverage(model, candidate).Complete) { pack = id; return candidate; }
            }
            return own;
        }

        private static bool OwnCovers(NSBMD model, byte[] own)
        {
            ModelAssets.TextureCoverage c = ModelAssets.Coverage(model, own);
            return c.RequiredTextures == 0 || c.Complete;
        }

        private ModelAssets.Archive AssetArchive => ModelAssets.All.First(a => a.Dir == Archive);

        public async Task ImportAsync()
        {
            if (_selBuilding < 0) return;
            FilePickerFileType filter = new FilePickerFileType("Models") { Patterns = new[] { "*.nsbmd", "*.obj", "*.bin" } };
            string path = await DialogHelper.OpenFileAsync(_owner, "Import building model", new[] { filter });
            if (path == null) return;
            try
            {
                ModelAssets.Archive archive = AssetArchive;
                int index = _selBuilding;
                string note = null;
                // A mesh is turned into a model on the way in; a model file is checked and goes in as it is.
                string err = string.Equals(Path.GetExtension(path), ".obj", StringComparison.OrdinalIgnoreCase)
                    ? _pending.Import(() => ModelAssets.ImportMesh(archive, index, path, out note))
                    : _pending.Import(() => ModelAssets.ImportRaw(archive, index, path));
                if (err != null) { await DialogHelper.ShowErrorAsync(err, "Building editor error"); return; }
                SaveNotice.Imported($"building model {index}", path);
                if (note != null) StatusText = note;
            }
            catch (Exception ex)
            {
                CrashReporter.LogHandled(ex);
                await DialogHelper.ShowErrorAsync($"Building {_selBuilding} wasn't imported:\n{ex.Message}", "Building editor error");
            }
        }

        public enum ExportFormat { Nsbmd, Dae, Glb }

        private static string Extension(ExportFormat f) => f switch { ExportFormat.Dae => ".dae", ExportFormat.Glb => ".glb", _ => ".nsbmd" };

        private static FilePickerFileType FileType(ExportFormat f) => f switch
        {
            ExportFormat.Dae => new FilePickerFileType("Collada model") { Patterns = new[] { "*.dae" } },
            ExportFormat.Glb => new FilePickerFileType("glTF model") { Patterns = new[] { "*.glb" } },
            _ => new FilePickerFileType("NSBMD model") { Patterns = new[] { "*.nsbmd" } },
        };

        /// <summary>The file name a model exports under, from its number and its name inside the file.</summary>
        private static string FileStem(int index, byte[] data)
        {
            string name = ModelAssets.NameInFile(data);
            if (name == null) return $"building_{index:D3}";
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return $"{index:D3}_{name}";
        }

        public async Task ExportAsync(ExportFormat format)
        {
            if (_selBuilding < 0) return;
            byte[] data = _pending.ReadMember(Archive, _selBuilding);
            if (data == null) return;
            string path = await DialogHelper.SaveFileAsync(_owner, "Export building model", new[] { FileType(format) },
                FileStem(_selBuilding, data) + Extension(format));
            if (path == null) return;
            try
            {
                if (format == ExportFormat.Nsbmd) { PlainFiles.WriteBytes(path, data); SaveNotice.Exported(path); return; }
                string written = ExportThreeD(_selBuilding, data, format == ExportFormat.Glb,
                    Path.GetFileNameWithoutExtension(path), Path.GetDirectoryName(path));
                if (written != null) SaveNotice.Exported(written);
            }
            catch (Exception ex)
            {
                CrashReporter.LogHandled(ex);
                await DialogHelper.ShowErrorAsync($"Building {_selBuilding} wasn't exported:\n{ex.Message}", "Building editor error");
            }
        }

        // The file name the user picked becomes the folder apicula writes into, beside where they picked.
        private string ExportThreeD(int index, byte[] data, bool glb, string name, string folder)
        {
            NSBMD model = NSBMDLoader.LoadNSBMD(new MemoryStream(data));
            byte[] textures = TexturesFor(index, data, model, out int pack);
            // The model's own pictures are already inside it; only a pack has to be added.
            return DSPRE.ModelUtils.ExportModel(name, data, pack >= 0 ? textures : null, glb, folder);
        }

        /// <summary>Exports every model on the current tab into one folder, painted as the texture pack box says.</summary>
        public async Task ExportAllAsync(ExportFormat format)
        {
            if (Buildings.Count == 0) return;
            string folder = await DialogHelper.OpenFolderAsync(_owner, "Export all building models");
            if (folder == null) return;

            List<(int Index, byte[] Data, string Stem)> models = new List<(int, byte[], string)>();
            for (int i = 0; i < Buildings.Count; i++)
            {
                byte[] data = _pending.ReadMember(Archive, i);
                if (data != null) models.Add((i, data, FileStem(i, data)));
            }

            // Asked once here, so the converter never stops to ask about each folder in the background.
            List<string> taken = models.Select(m => format == ExportFormat.Nsbmd
                    ? Path.Combine(folder, m.Stem + ".nsbmd") : Path.Combine(folder, m.Stem))
                .Where(p => File.Exists(p) || (Directory.Exists(p) && Directory.EnumerateFileSystemEntries(p).Any())).ToList();
            if (taken.Count > 0)
            {
                if (!await DialogHelper.AskYesNoAsync($"{taken.Count} of these are already in that folder. Replace them?", "Export all", _owner)) return;
                foreach (string p in taken)
                {
                    if (File.Exists(p)) File.Delete(p);
                    else Directory.Delete(p, recursive: true);
                }
            }

            int done = 0;
            List<string> failed = new List<string>();
            await BusyOverlay.RunAsync($"Exporting {models.Count} building models…", format == ExportFormat.Nsbmd ? "One file per model." : "One folder per model.", () =>
            {
                foreach ((int index, byte[] data, string stem) in models)
                {
                    try
                    {
                        if (format == ExportFormat.Nsbmd) PlainFiles.WriteBytes(Path.Combine(folder, stem + ".nsbmd"), data);
                        else if (ExportThreeD(index, data, format == ExportFormat.Glb, stem, folder) == null) { failed.Add(stem); continue; }
                        done++;
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"Building export of {stem} failed: {ex.Message}");
                        failed.Add(stem);
                    }
                }
            }, _owner);

            if (done > 0) SaveNotice.Exported(folder);
            StatusText = failed.Count == 0
                ? $"Exported {done} building models."
                : $"Exported {done} building models; {failed.Count} failed: {string.Join(", ", failed.Take(5))}{(failed.Count > 5 ? ", …" : "")}.";
        }
    }
}
