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
    /// Avalonia port of the WinForms <c>NsbtxEditor</c>, the texture-pack viewer/editor.
    /// Lists map / building texture packs; for the selected pack, lists its textures and
    /// palettes and renders a preview of the chosen texture+palette (via the shared
    /// <see cref="NsbmdTextureDecoder"/>). Whole packs can be imported / exported.
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

        /// <summary>A pack to open first, for an editor that hands one over (the Area Data editor's texture links).</summary>
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
                string name = value >= 0 && value < _textures.Count ? _textures[value].texname : "";
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
        public NsbtxEditorViewModel(bool _) { }

        private string TexDir => gameDirs[_mapTextures ? DirNames.mapTextures : DirNames.buildingTextures].unpackedDir;
        private int TexCount => _mapTextures ? Filesystem.GetMapTexturesCount() : Filesystem.GetBuildingTexturesCount();
        private string PackPath(int i) => Path.Combine(TexDir, i.ToString("D4"));

        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.mapTextures, DirNames.buildingTextures });
                ReloadPacks();
                ResetUndo();
            }
            catch (Exception ex)
            {
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowError($"Couldn't open the texture editor:\n{ex.Message}", "Map & Building Textures");
            }
        }

        private void ReloadPacks()
        {
            _suppress = true;
            PackNames.Clear();
            int count = TexCount;
            for (int i = 0; i < count; i++) PackNames.Add("Texture pack " + i);
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
                string path = PackPath(index);
                if (!File.Exists(path)) { StatusText = "Pack not found."; return; }
                byte[] raw = File.ReadAllBytes(path);
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
                foreach (NSBMDTexture t in _textures) TextureNames.Add(string.IsNullOrEmpty(t.texname) ? $"Texture {TextureNames.Count}" : t.texname);
                foreach (NSBMDPalette p in _palettes) PaletteNames.Add(string.IsNullOrEmpty(p.palname) ? $"Palette {PaletteNames.Count}" : p.palname);
                _suppress = false;

                _textureIndex = TextureNames.Count > 0 ? 0 : -1;
                string texture = _textureIndex >= 0 ? _textures[_textureIndex].texname : "";
                _paletteIndex = -1;
                MatchPalette(texture);
                OnPropertyChanged(nameof(TextureIndex));
                RenderPreview();
                StatusText = $"Pack {index}: {_textures.Count} textures, {_palettes.Count} palettes.";
            }
            catch (Exception ex)
            {
                StatusText = "Load failed: " + ex.Message;
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
                catch { areas = Array.Empty<int>(); }
                if (areas.Count > 0)
                    return $"No building textures: no buildings in {(areas.Count == 1 ? "area " + areas[0] : "areas " + string.Join(", ", areas))}.";
                return "No building textures.";
            }
            return "This pack is not a texture set (BTX0).";
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
                if (tex.format != 7 && pal == null)
                {
                    PreviewReason = "This indexed texture needs a palette, but this pack contains none.";
                    return;
                }
                NSBMDMaterial mat = new NSBMDMaterial
                {
                    format = tex.format, width = tex.width, height = tex.height,
                    texdata = tex.texdata, spdata = tex.spdata, color0 = tex.color0,
                    paldata = pal?.paldata,
                };
                NsbmdTextureData decoded = NsbmdTextureDecoder.Decode(mat);
                Preview = decoded != null ? RgbaToBitmap(decoded.Rgba, decoded.Width, decoded.Height) : null;
                PreviewReason = decoded == null
                    ? "This texture is malformed or uses data the preview cannot decode." : "";
            }
            catch (Exception ex)
            {
                Preview = null;
                PreviewReason = "This texture could not be previewed.";
                AppLogger.Error("NSBTX preview failed: " + ex.Message);
            }
        }

        private static Bitmap RgbaToBitmap(byte[] rgba, int w, int h)
        {
            if (rgba == null || w <= 0 || h <= 0) return null;
            WriteableBitmap wb = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
            using (ILockedFramebuffer fb = wb.Lock())
            {
                int srcStride = w * 4, dstStride = fb.RowBytes;
                if (dstStride == srcStride) Marshal.Copy(rgba, 0, fb.Address, Math.Min(rgba.Length, dstStride * h));
                else for (int y = 0; y < h; y++) Marshal.Copy(rgba, y * srcStride, IntPtr.Add(fb.Address, y * dstStride), srcStride);
            }
            return wb;
        }

        // ── Save / Discard ───────────────────────────────────────────────────────────────
        // Edits hit the unpacked files at once, so each file's prior bytes (null if new) are kept for Discard.
        private readonly Dictionary<string, byte[]> _originals = new();
        private void Keep(string path)
        {
            byte[] now = File.Exists(path) ? File.ReadAllBytes(path) : null;
            if (!_originals.ContainsKey(path)) _originals[path] = now;
            if (!_historyOriginals.ContainsKey(path)) _historyOriginals[path] = now;
        }
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Texture packs";
        public void SaveChanges()
        {
            if (!_dirty) return;
            _originals.Clear();
            _dirty = false;
            _undo?.MarkSaved();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            SaveNotice.Saved(UnsavedChangesDescription);
        }

        // ── Undo / redo: every touched file as it stands ─────────────────────────────────
        // Each state is the touched files' bytes; applying one writes them back, as the edits themselves do.
        private readonly Dictionary<string, byte[]> _historyOriginals = new();
        private DSPRE.Avalonia.ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private static byte[] Read(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;

        private byte[] TakeState() => DSPRE.Avalonia.UndoJson.Take(_historyOriginals.Keys.ToDictionary(p => p, Read));

        private void ApplyState(byte[] state)
        {
            Dictionary<string, byte[]> s = DSPRE.Avalonia.UndoJson.Read<Dictionary<string, byte[]>>(state);
            foreach (string path in _historyOriginals.Keys)
            {
                byte[] want = s.TryGetValue(path, out byte[] b) ? b : _historyOriginals[path];
                try { if (want == null) { if (File.Exists(path)) File.Delete(path); } else File.WriteAllBytes(path, want); }
                catch (Exception ex) { AppLogger.Error("Texture pack undo: " + ex.Message); }
            }
            int keep = _packIndex;
            ReloadPacks();
            if (keep > 0 && keep < PackNames.Count) PackIndex = keep;
            RecountDirty();
        }

        // Unsaved while any touched file differs from what it held when last saved.
        private void RecountDirty()
        {
            _dirty = _originals.Any(kv => !SameBytes(Read(kv.Key), kv.Value));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private static bool SameBytes(byte[] a, byte[] b) => a == null ? b == null : b != null && a.AsSpan().SequenceEqual(b);

        private void ResetUndo()
        {
            _historyOriginals.Clear();
            _undo = new DSPRE.Avalonia.ByteStateUndo(TakeState, ApplyState, RaiseUndo);
            RaiseUndo();
        }

        private void Edited()
        {
            _undo?.Record();
            RecountDirty();
        }
        public void DiscardChanges()
        {
            foreach ((string path, byte[] bytes) in _originals)
            {
                try { if (bytes == null) { if (File.Exists(path)) File.Delete(path); } else File.WriteAllBytes(path, bytes); }
                catch (Exception ex) { AppLogger.Error("Texture pack discard: " + ex.Message); }
            }
            _originals.Clear();
            _dirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            ReloadPacks();
            ResetUndo();
        }

        // ── Add / remove texture packs ───────────────────────────────────────────────────
        public void AddPack()
        {
            try
            {
                int newId = PackNames.Count;
                Keep(PackPath(newId));
                File.Copy(PackPath(0), PackPath(newId));
                if (!_mapTextures && gameDirs.ContainsKey(DirNames.buildingConfigFiles))
                {
                    string cfg = gameDirs[DirNames.buildingConfigFiles].unpackedDir;
                    if (File.Exists(Path.Combine(cfg, "0000")))
                    {
                        Keep(Path.Combine(cfg, newId.ToString("D4")));
                        File.Copy(Path.Combine(cfg, "0000"), Path.Combine(cfg, newId.ToString("D4")));
                    }
                }
                PackNames.Add("Texture pack " + newId);
                PackIndex = newId;
                StatusText = $"Added texture pack {newId}.";
                Edited();
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Couldn't add pack:\n{ex.Message}", "Map & Building Textures"); }
        }

        public async Task RemoveLastPackAsync()
        {
            if (PackNames.Count <= 1) { StatusText = "Can't remove the last pack."; return; }
            int last = PackNames.Count - 1;
            if (!await DialogHelper.AskYesNo($"Delete the last texture pack ({last})?", "Confirm deletion")) return;
            try
            {
                Keep(PackPath(last));
                File.Delete(PackPath(last));
                if (!_mapTextures && gameDirs.ContainsKey(DirNames.buildingConfigFiles))
                {
                    string cfg = Path.Combine(gameDirs[DirNames.buildingConfigFiles].unpackedDir, last.ToString("D4"));
                    if (File.Exists(cfg)) { Keep(cfg); File.Delete(cfg); }
                }
                if (_packIndex == last) PackIndex = last - 1;
                PackNames.RemoveAt(last);
                StatusText = $"Removed texture pack {last}.";
                Edited();
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Couldn't remove pack:\n{ex.Message}", "Map & Building Textures"); }
        }

        // ── Import / export whole packs ─────────────────────────────────────────────────
        public async Task ExportAsync()
        {
            if (_packIndex < 0) return;
            FilePickerFileType filter = new FilePickerFileType("NSBTX texture pack") { Patterns = new[] { "*.nsbtx", "*.bin" } };
            string suggested = $"Texture Pack {_packIndex}.nsbtx";
            string path = await DialogHelper.SaveFile(_owner, "Export texture pack", new[] { filter }, suggested);
            if (path == null) return;
            try { File.Copy(PackPath(_packIndex), path, true); StatusText = "Exported."; }
            catch (Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }

        public async Task ImportAsync()
        {
            if (_packIndex < 0) return;
            FilePickerFileType filter = new FilePickerFileType("NSBTX texture pack") { Patterns = new[] { "*.nsbtx", "*.bin", "*.*" } };
            string path = await DialogHelper.OpenFile(_owner, "Import texture pack", new[] { filter });
            if (path == null) return;
            try
            {
                if (!IsTextureSet(File.ReadAllBytes(path)))
                {
                    await DialogHelper.ShowError("That file is not a texture set (BTX0).", "Import");
                    return;
                }
            }
            catch (Exception ex) { await DialogHelper.ShowError($"That file could not be read:\n{ex.Message}", "Import"); return; }

            string question = $"Replace texture pack {_packIndex} with this file?";
            if (!_mapTextures)
            {
                IReadOnlyList<int> unused;
                try { unused = BuildingModelTextureSets.AreasThatNeverReadSet(_packIndex); }
                catch { unused = Array.Empty<int>(); }
                if (unused.Count > 0)
                    question += " The areas using it have no buildings, so the game will not read it.";
            }
            if (!await DialogHelper.AskYesNo(question, "Import")) return;
            try
            {
                Keep(PackPath(_packIndex));
                File.Copy(path, PackPath(_packIndex), true);
                LoadPack(_packIndex);
                StatusText = "Imported. Save to keep it.";
                Edited();
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error"); }
        }
    }
}
