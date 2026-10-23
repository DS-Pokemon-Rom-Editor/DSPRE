using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>
    /// Lists map and building texture packs, previews a texture with a palette, and imports or exports
    /// whole packs.
    /// </summary>
    public class NsbtxEditorViewModel : INotifyPropertyChanged, DSPRE.Editors.IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private Window _owner;
        private bool _suppress;
        private List<NSBMDTexture> _textures = new List<NSBMDTexture>();
        private List<NSBMDPalette> _palettes = new List<NSBMDPalette>();

        public ObservableCollection<string> PackNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> TextureNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> PaletteNames { get; } = new ObservableCollection<string>();

        private bool _mapTextures = true;
        public bool MapTextures
        {
            get => _mapTextures;
            set
            {
                if (_mapTextures == value) return;
                // Each kind of pack keeps its own place, so going back to a tab finds the pack left open there.
                if (_mapTextures) _mapPack = _packIndex; else _buildingPack = _packIndex;
                _mapTextures = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BuildingTextures));
                OnPropertyChanged(nameof(KindIndex));
                if (!_suppress) ReloadPacks();
            }
        }
        public bool BuildingTextures { get => !_mapTextures; set => MapTextures = !value; }

        /// <summary>The tab showing: 0 map textures, 1 building textures.</summary>
        public int KindIndex { get => _mapTextures ? 0 : 1; set => MapTextures = value == 0; }

        private int _mapPack = -1, _buildingPack = -1;

        /// <summary>A pack to open first, for an editor that hands one over (the Area data editor's texture links).</summary>
        public void OpenAt(bool buildings, int pack)
        {
            if (buildings) _buildingPack = pack; else _mapPack = pack;
            if (_owner == null)
            {
                // Not set up yet: SetupAsync opens this kind and pack.
                _mapTextures = !buildings;
                OnPropertyChanged(nameof(MapTextures)); OnPropertyChanged(nameof(BuildingTextures)); OnPropertyChanged(nameof(KindIndex));
                return;
            }
            if (BuildingTextures == buildings) { if (pack >= 0 && pack < PackNames.Count) PackIndex = pack; }
            else MapTextures = !buildings;
        }

        /// <summary>The palette matched by name, or keeps the one showing and says so when none matches.</summary>
        private void MatchPalette(string texture)
        {
            int match = ModelTexturePairing.MatchPaletteIndex(_palettes, texture);
            if (match >= 0) _paletteIndex = match;
            else
            {
                if (_paletteIndex < 0 || _paletteIndex >= _palettes.Count) _paletteIndex = _palettes.Count > 0 ? 0 : -1;
                if (_palettes.Count > 0) StatusText = $"No palette matches {texture}.";
            }
            OnPropertyChanged(nameof(PaletteIndex));
        }

        private int _packIndex = -1;
        public int PackIndex { get => _packIndex; set { if (Set(ref _packIndex, value) && !_suppress && value >= 0) LoadPack(value); } }

        private int _textureIndex = -1;
        public int TextureIndex
        {
            get => _textureIndex;
            set
            {
                if (!Set(ref _textureIndex, value) || _suppress) return;
                string name = value >= 0 && value < _textures.Count ? _textures[value].Texname : "";
                MatchPalette(name);
                RenderPreview();
            }
        }
        private int _paletteIndex = -1;
        public int PaletteIndex { get => _paletteIndex; set { if (Set(ref _paletteIndex, value) && !_suppress) RenderPreview(); } }

        private Bitmap _preview;
        public Bitmap Preview
        {
            get => _preview;
            set
            {
                if (Set(ref _preview, value)) OnPropertyChanged(nameof(HasPreviewReason));
            }
        }

        private string _previewReason = "Pick a texture to preview it.";
        public string PreviewReason
        {
            get => _previewReason;
            private set
            {
                if (Set(ref _previewReason, value)) OnPropertyChanged(nameof(HasPreviewReason));
            }
        }
        public bool HasPreviewReason => Preview == null && !string.IsNullOrWhiteSpace(PreviewReason);

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        public NsbtxEditorViewModel() { if (Design.IsDesignMode) PackNames.Add("Texture pack 0"); }
        public NsbtxEditorViewModel(bool _)
        {
            _pending.Changed += () =>
            {
                // Undo and redo can add or remove packs, so the list is rebuilt around the pack showing.
                if (_mapTextures) _mapPack = _packIndex; else _buildingPack = _packIndex;
                ReloadPacks();
                OnPropertyChanged(nameof(HasUnsavedChanges));
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            };
        }

        private DirNames Archive => _mapTextures ? DirNames.MapTextures : DirNames.BuildingTextures;
        private int TexCount => _pending.Count(Archive);
        private string PackPath(int i) => ArchiveMembers.PathOf(Archive, i);

        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            try
            {
                // Adding or removing a building pack copies or drops its building list too, so that archive is read here.
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.MapTextures, DirNames.BuildingTextures, DirNames.BuildingConfigFiles });
                ReloadPacks();
            }
            catch (Exception ex)
            {
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowErrorAsync($"Couldn't open Map & building textures:\n{ex.Message}", "Map & building textures");
            }
        }

        private void ReloadPacks()
        {
            _suppress = true;
            PackNames.Clear();
            int count = TexCount;
            for (int i = 0; i < count; i++) PackNames.Add((_mapTextures ? "Map textures " : "Building textures ") + i);
            _suppress = false;
            StatusText = $"{count} {(_mapTextures ? "map" : "building")} texture packs.";
            int remembered = _mapTextures ? _mapPack : _buildingPack;
            _packIndex = -1;
            if (PackNames.Count > 0) PackIndex = remembered >= 0 && remembered < PackNames.Count ? remembered : 0;
            else
            {
                TextureNames.Clear();
                PaletteNames.Clear();
                Preview = null;
                PreviewReason = "This game has no texture packs in this group.";
            }
        }

        private void LoadPack(int index)
        {
            try
            {
                byte[] raw = _pending.ReadMember(Archive, index);
                if (raw == null) { StatusText = "Pack not found."; return; }
                if (!IsTextureSet(raw))
                {
                    _textures = new List<NSBMDTexture>();
                    _palettes = new List<NSBMDPalette>();
                    _suppress = true;
                    TextureNames.Clear(); PaletteNames.Clear();
                    _suppress = false;
                    _textureIndex = _paletteIndex = -1;
                    OnPropertyChanged(nameof(TextureIndex));
                    OnPropertyChanged(nameof(PaletteIndex));
                    Preview = null;
                    PreviewReason = WhyNoTextureSet(index, raw);
                    StatusText = $"Pack {index}: no texture set.";
                    return;
                }
                using (MemoryStream ms = new MemoryStream(raw))
                    NSBTXLoader.LoadNsbtx(ms, out _textures, out _palettes);

                _suppress = true;
                TextureNames.Clear(); PaletteNames.Clear();
                foreach (NSBMDTexture t in _textures) TextureNames.Add(string.IsNullOrEmpty(t.Texname) ? $"Texture {TextureNames.Count}" : t.Texname);
                foreach (NSBMDPalette p in _palettes) PaletteNames.Add(string.IsNullOrEmpty(p.Palname) ? $"Palette {PaletteNames.Count}" : p.Palname);
                _suppress = false;

                _textureIndex = TextureNames.Count > 0 ? 0 : -1;
                string texture = _textureIndex >= 0 ? _textures[_textureIndex].Texname : "";
                _paletteIndex = -1;
                MatchPalette(texture);
                OnPropertyChanged(nameof(TextureIndex));
                RenderPreview();
                StatusText = $"Pack {index}: {_textures.Count} textures, {_palettes.Count} palettes.";
            }
            catch (Exception ex)
            {
                StatusText = "Couldn't load: " + ex.Message;
                AppLogger.Error("NSBTX pack load failed: " + ex);
            }
        }

        private static bool IsTextureSet(byte[] b) =>
            b != null && b.Length >= 4 && b[0] == (byte)'B' && b[1] == (byte)'T' && b[2] == (byte)'X' && b[3] == (byte)'0';

        // A building pack's area tables can say why it holds no textures.
        private string WhyNoTextureSet(int index, byte[] raw)
        {
            if (!_mapTextures && BuildingModelTextureSets.IsNoTexturesStandIn(raw))
            {
                IReadOnlyList<int> areas;
                try { areas = BuildingModelTextureSets.AreasThatNeverReadSet(index); }
                catch (Exception ex) { AppLogger.WarnOnce("NsbtxEditorViewModel.WhyNoTextureSet", ex); areas = Array.Empty<int>(); }
                if (areas.Count > 0)
                    return $"No building textures: no buildings in {(areas.Count == 1 ? "area " + areas[0] : "areas " + string.Join(", ", areas))}.";
                return "No building textures.";
            }
            return "This pack isn't a texture set (BTX0).";
        }

        private void RenderPreview()
        {
            Preview = null;
            if (_textureIndex < 0 || _textureIndex >= _textures.Count)
            {
                PreviewReason = "This pack contains no texture to show.";
                return;
            }

            try
            {
                NSBMDTexture tex = _textures[_textureIndex];
                NSBMDPalette pal = _paletteIndex >= 0 && _paletteIndex < _palettes.Count
                    ? _palettes[_paletteIndex] : null;
                if (tex.Format != 7 && pal == null)
                {
                    PreviewReason = "This indexed texture needs a palette, but this pack contains none.";
                    return;
                }
                NSBMDMaterial mat = new NSBMDMaterial
                {
                    Format = tex.Format, Width = tex.Width, Height = tex.Height,
                    Texdata = tex.Texdata, Spdata = tex.Spdata, Color0 = tex.Color0,
                    Paldata = pal?.Paldata,
                };
                NsbmdTextureData decoded = NsbmdTextureDecoder.Decode(mat);
                Preview = decoded != null ? ImageConverter.FromRgba(decoded.Rgba, decoded.Width, decoded.Height) : null;
                PreviewReason = decoded == null
                    ? "This texture is malformed or uses data the preview can't decode." : "";
            }
            catch (Exception ex)
            {
                Preview = null;
                PreviewReason = "This texture couldn't be previewed.";
                AppLogger.Error("NSBTX preview failed: " + ex.Message);
            }
        }

        // Imports, added packs and removed packs wait here until Save; the archives are not touched before.
        private readonly PendingArchiveWrites _pending = new();
        public bool HasUnsavedChanges => _pending.IsDirty;
        // A building pack's list entry follows its pack, so only the packs are named.
        public string UnsavedChangesDescription => DSPRE.Avalonia.Data.StagedArchive.Join("Texture packs", false, string.Join(", ", new[]
        {
            DSPRE.Avalonia.Data.StagedArchive.Describe("map texture pack", "map texture packs", _pending.Pending(DirNames.MapTextures)),
            DSPRE.Avalonia.Data.StagedArchive.Describe("building texture pack", "building texture packs", _pending.Pending(DirNames.BuildingTextures)),
        }.Where(t => t.Length > 0)));
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

        // ── Add / remove texture packs ───────────────────────────────────────────────────
        public void AddPack()
        {
            try
            {
                int newId = PackNames.Count;
                bool configs = !_mapTextures && GameDirs.ContainsKey(DirNames.BuildingConfigFiles);
                byte[] config = configs ? _pending.ReadMember(DirNames.BuildingConfigFiles, 0) : null;
                _pending.Change(step =>
                {
                    step.Put(Archive, newId, _pending.ReadMember(Archive, 0));
                    if (config != null) step.Put(DirNames.BuildingConfigFiles, newId, config);
                });
                PackIndex = newId;
                StatusText = $"Added texture pack {newId}.";
            }
            catch (Exception ex) { _ = DialogHelper.ShowErrorAsync($"Couldn't add the pack:\n{ex.Message}", "Map & building textures"); }
        }

        public async Task RemoveLastPackAsync()
        {
            if (PackNames.Count <= 1) { StatusText = "Can't remove the last pack."; return; }
            int last = PackNames.Count - 1;
            if (!await DialogHelper.AskYesNoAsync($"Delete the last texture pack ({last})?", "Delete texture pack?")) return;
            try
            {
                bool config = !_mapTextures && GameDirs.ContainsKey(DirNames.BuildingConfigFiles)
                              && _pending.ReadMember(DirNames.BuildingConfigFiles, last) != null;
                if (_packIndex == last) PackIndex = last - 1;
                _pending.Change(step =>
                {
                    step.Remove(Archive, last);
                    if (config) step.Remove(DirNames.BuildingConfigFiles, last);
                });
            }
            catch (Exception ex) { _ = DialogHelper.ShowErrorAsync($"Couldn't remove the pack:\n{ex.Message}", "Map & building textures"); }
        }

        // ── Import / export whole packs ─────────────────────────────────────────────────
        /// <summary>The pack's file in the file manager, as last saved.</summary>
        public void ShowFile()
        {
            if (_packIndex < 0) return;
            SystemShell.RevealInFileManager(PackPath(_packIndex));
            if (HasUnsavedChanges) StatusText = "The file holds the pack as last saved.";
        }

        public async Task ExportAsync()
        {
            if (_packIndex < 0) return;
            FilePickerFileType filter = new FilePickerFileType("NSBTX texture pack") { Patterns = new[] { "*.nsbtx", "*.bin" } };
            string suggested = $"Texture pack {_packIndex}.nsbtx";
            string path = await DialogHelper.SaveFileAsync(_owner, "Export texture pack", new[] { filter }, suggested);
            if (path == null) return;
            try { PlainFiles.WriteBytes(path, _pending.ReadMember(Archive, _packIndex)); SaveNotice.Exported(path); }
            catch (Exception ex) { await DialogHelper.ShowErrorAsync($"Couldn't export:\n{ex.Message}", "Map & building textures"); }
        }

        public async Task ImportAsync()
        {
            if (_packIndex < 0) return;
            FilePickerFileType filter = new FilePickerFileType("NSBTX texture pack") { Patterns = new[] { "*.nsbtx", "*.bin", "*.*" } };
            string path = await DialogHelper.OpenFileAsync(_owner, "Import texture pack", new[] { filter });
            if (path == null) return;
            try
            {
                if (!IsTextureSet(File.ReadAllBytes(path)))
                {
                    await DialogHelper.ShowErrorAsync("That file isn't a texture set (BTX0).", "Map & building textures");
                    return;
                }
            }
            catch (Exception ex) { await DialogHelper.ShowErrorAsync($"That file couldn't be read:\n{ex.Message}", "Map & building textures"); return; }

            string question = $"Replace texture pack {_packIndex} with this file?";
            if (!_mapTextures)
            {
                IReadOnlyList<int> unused;
                try { unused = BuildingModelTextureSets.AreasThatNeverReadSet(_packIndex); }
                catch (Exception ex) { AppLogger.WarnOnce("NsbtxEditorViewModel.ImportAsync", ex); unused = Array.Empty<int>(); }
                if (unused.Count > 0)
                    question += " The areas using it have no buildings, so the game won't read it.";
            }
            if (!await DialogHelper.AskYesNoAsync(question, "Import")) return;
            try
            {
                byte[] pack = File.ReadAllBytes(path);
                _pending.Change(step => step.Put(Archive, _packIndex, pack));
                SaveNotice.Imported($"texture pack {_packIndex}", path);
            }
            catch (Exception ex) { await DialogHelper.ShowErrorAsync($"Couldn't import:\n{ex.Message}", "Map & building textures"); }
        }
    }
}
