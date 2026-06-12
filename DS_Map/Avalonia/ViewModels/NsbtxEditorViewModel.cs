using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform;
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
    /// Avalonia port of the WinForms <c>NsbtxEditor</c> — the texture-pack viewer/editor.
    /// Lists map / building texture packs; for the selected pack, lists its textures and
    /// palettes and renders a preview of the chosen texture+palette (via the shared
    /// <see cref="NsbmdTextureDecoder"/>). Whole packs can be imported / exported.
    /// </summary>
    public class NsbtxEditorViewModel : INotifyPropertyChanged
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
        public bool MapTextures { get => _mapTextures; set { if (Set(ref _mapTextures, value) && !_suppress) { OnPropertyChanged(nameof(BuildingTextures)); ReloadPacks(); } } }
        public bool BuildingTextures { get => !_mapTextures; set => MapTextures = !value; }

        private int _packIndex = -1;
        public int PackIndex { get => _packIndex; set { if (Set(ref _packIndex, value) && !_suppress && value >= 0) LoadPack(value); } }

        private int _textureIndex = -1;
        public int TextureIndex { get => _textureIndex; set { if (Set(ref _textureIndex, value)) RenderPreview(); } }
        private int _paletteIndex = -1;
        public int PaletteIndex { get => _paletteIndex; set { if (Set(ref _paletteIndex, value)) RenderPreview(); } }

        private Bitmap _preview;
        public Bitmap Preview { get => _preview; set => Set(ref _preview, value); }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        public NsbtxEditorViewModel() { if (Design.IsDesignMode) PackNames.Add("Texture Pack 0"); }
        public NsbtxEditorViewModel(bool _) { }

        private string TexDir => gameDirs[_mapTextures ? DirNames.mapTextures : DirNames.buildingTextures].unpackedDir;
        private int TexCount => _mapTextures ? Filesystem.GetMapTexturesCount() : Filesystem.GetBuildingTexturesCount();
        private string PackPath(int i) => TexDir + "\\" + i.ToString("D4");

        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.mapTextures, DirNames.buildingTextures });
                ReloadPacks();
            }
            catch (Exception ex)
            {
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowError($"Failed to set up NSBTX Editor:\n{ex.Message}", "NSBTX Editor");
            }
        }

        private void ReloadPacks()
        {
            _suppress = true;
            PackNames.Clear();
            int count = TexCount;
            for (int i = 0; i < count; i++) PackNames.Add("Texture Pack " + i);
            _suppress = false;
            StatusText = $"{count} {(_mapTextures ? "map" : "building")} texture packs.";
            if (PackNames.Count > 0) PackIndex = 0; else { TextureNames.Clear(); PaletteNames.Clear(); Preview = null; }
        }

        private void LoadPack(int index)
        {
            try
            {
                string path = PackPath(index);
                if (!File.Exists(path)) { StatusText = "Pack not found."; return; }
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                    NSBTXLoader.LoadNsbtx(fs, out _textures, out _palettes);

                _suppress = true;
                TextureNames.Clear(); PaletteNames.Clear();
                foreach (var t in _textures) TextureNames.Add(string.IsNullOrEmpty(t.texname) ? $"Texture {TextureNames.Count}" : t.texname);
                foreach (var p in _palettes) PaletteNames.Add(string.IsNullOrEmpty(p.palname) ? $"Palette {PaletteNames.Count}" : p.palname);
                _suppress = false;

                TextureIndex = TextureNames.Count > 0 ? 0 : -1;
                PaletteIndex = PaletteNames.Count > 0 ? 0 : -1;
                RenderPreview();
                StatusText = $"Pack {index}: {_textures.Count} textures, {_palettes.Count} palettes.";
            }
            catch (Exception ex)
            {
                StatusText = "Load failed: " + ex.Message;
                AppLogger.Error("NSBTX pack load failed: " + ex);
            }
        }

        private void RenderPreview()
        {
            if (_textureIndex < 0 || _textureIndex >= _textures.Count || _paletteIndex < 0 || _paletteIndex >= _palettes.Count)
            { Preview = null; return; }

            try
            {
                var tex = _textures[_textureIndex];
                var pal = _palettes[_paletteIndex];
                var mat = new NSBMDMaterial
                {
                    format = tex.format, width = tex.width, height = tex.height,
                    texdata = tex.texdata, spdata = tex.spdata, color0 = tex.color0,
                    paldata = pal.paldata,
                };
                var decoded = NsbmdTextureDecoder.Decode(mat);
                Preview = decoded != null ? RgbaToBitmap(decoded.Rgba, decoded.Width, decoded.Height) : null;
            }
            catch (Exception ex) { Preview = null; AppLogger.Error("NSBTX preview failed: " + ex.Message); }
        }

        private static Bitmap RgbaToBitmap(byte[] rgba, int w, int h)
        {
            if (rgba == null || w <= 0 || h <= 0) return null;
            var wb = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
            using (var fb = wb.Lock())
            {
                int srcStride = w * 4, dstStride = fb.RowBytes;
                if (dstStride == srcStride) Marshal.Copy(rgba, 0, fb.Address, Math.Min(rgba.Length, dstStride * h));
                else for (int y = 0; y < h; y++) Marshal.Copy(rgba, y * srcStride, IntPtr.Add(fb.Address, y * dstStride), srcStride);
            }
            return wb;
        }

        // ── Add / remove texture packs ───────────────────────────────────────────────────
        public void AddPack()
        {
            try
            {
                int newId = PackNames.Count;
                File.Copy(PackPath(0), PackPath(newId));
                if (!_mapTextures && gameDirs.ContainsKey(DirNames.buildingConfigFiles))
                {
                    string cfg = gameDirs[DirNames.buildingConfigFiles].unpackedDir;
                    if (File.Exists(cfg + "\\0000")) File.Copy(cfg + "\\0000", cfg + "\\" + newId.ToString("D4"));
                }
                PackNames.Add("Texture Pack " + newId);
                PackIndex = newId;
                StatusText = $"Added texture pack {newId}.";
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Couldn't add pack:\n{ex.Message}", "NSBTX Editor"); }
        }

        public async Task RemoveLastPackAsync()
        {
            if (PackNames.Count <= 1) { StatusText = "Can't remove the last pack."; return; }
            int last = PackNames.Count - 1;
            if (!await DialogHelper.AskYesNo($"Delete the last texture pack ({last})?", "Confirm deletion")) return;
            try
            {
                File.Delete(PackPath(last));
                if (!_mapTextures && gameDirs.ContainsKey(DirNames.buildingConfigFiles))
                {
                    string cfg = gameDirs[DirNames.buildingConfigFiles].unpackedDir + "\\" + last.ToString("D4");
                    if (File.Exists(cfg)) File.Delete(cfg);
                }
                if (_packIndex == last) PackIndex = last - 1;
                PackNames.RemoveAt(last);
                StatusText = $"Removed texture pack {last}.";
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Couldn't remove pack:\n{ex.Message}", "NSBTX Editor"); }
        }

        // ── Import / export whole packs ─────────────────────────────────────────────────
        public async Task ExportAsync()
        {
            if (_packIndex < 0) return;
            var filter = new FilePickerFileType("NSBTX texture pack") { Patterns = new[] { "*.nsbtx", "*.bin" } };
            string suggested = $"Texture Pack {_packIndex}.nsbtx";
            string path = await DialogHelper.SaveFile(_owner, "Export texture pack", new[] { filter }, suggested);
            if (path == null) return;
            try { File.Copy(PackPath(_packIndex), path, true); StatusText = "Exported."; }
            catch (Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }

        public async Task ImportAsync()
        {
            if (_packIndex < 0) return;
            var filter = new FilePickerFileType("NSBTX texture pack") { Patterns = new[] { "*.nsbtx", "*.bin", "*.*" } };
            string path = await DialogHelper.OpenFile(_owner, "Import texture pack", new[] { filter });
            if (path == null) return;
            if (!await DialogHelper.AskYesNo($"Replace texture pack {_packIndex} with this file?", "Import")) return;
            try
            {
                File.Copy(path, PackPath(_packIndex), true);
                LoadPack(_packIndex);
                StatusText = "Imported (written to ROM working dir).";
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error"); }
        }
    }
}
