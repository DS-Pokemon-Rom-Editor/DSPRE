using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.Editors;
using DSPRE.LibNDSFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using static DSPRE.RomInfo;

using DSPRE.Avalonia.Data;
namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public class BtxEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        // ── Collections ────────────────────────────────────────────────────────
        public ObservableCollection<string> OwEntries { get; } = new();
        private List<uint> _owKeys = new();

        // ── Current state ──────────────────────────────────────────────────────
        private int _selectedIndex = -1;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set { if (Set(ref _selectedIndex, value)) LoadEntry(value); }
        }

        private Bitmap _currentImage;
        public Bitmap CurrentImage { get => _currentImage; private set => Set(ref _currentImage, value); }

        private bool _isShiny;
        public bool IsShiny
        {
            get => _isShiny;
            set { if (Set(ref _isShiny, value) && _btxData != null) RefreshImage(); }
        }

        private bool _hasShinyPalette;
        public bool HasShinyPalette { get => _hasShinyPalette; private set => Set(ref _hasShinyPalette, value); }
        public string ShinyPaletteNote => HasShinyPalette
            ? "This entry stores normal and shiny palettes."
            : "This entry stores only its normal palette.";

        private byte[] _btxData;
        private Dictionary<uint, byte[]> _modifiedFiles = new();

        // ── Status ─────────────────────────────────────────────────────────────
        private string _statusText = "";
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        public bool HasSelectedEntry => _selectedIndex >= 0 && _selectedIndex < _owKeys.Count;

        public string ModifiedCount =>
            _modifiedFiles.Count > 0 ? $"{_modifiedFiles.Count} unsaved" : "";

        // ── IEditorWithUnsavedChanges ──────────────────────────────────────────
        public bool HasUnsavedChanges => _modifiedFiles.Count > 0;
        public string UnsavedChangesDescription =>
            $"BTX Editor ({_modifiedFiles.Count} modified file{(_modifiedFiles.Count != 1 ? "s" : "")})";

        public void SaveChanges() => SaveAll();
        public void DiscardChanges() { _modifiedFiles.Clear(); OnPropertyChanged(nameof(HasUnsavedChanges)); OnPropertyChanged(nameof(ModifiedCount)); }

        // ── Platinum overworld properties (render state + expansion patch add/delete) ──────────
        // Everything in this section is Platinum-only. HGSS/DP keep the plain texture browser above.
        public bool IsPlatinum => RomInfo.gameFamily == GameFamilies.Plat;

        public bool IsExpansionApplied => OverworldSpriteTableExpansion.IsApplied;
        public string ExpansionStatusText => IsExpansionApplied
            ? $"Custom Overworld Sprites patch detected. {OverworldSpriteTableExpansion.UsedCount}/{OverworldSpriteTableExpansion.Capacity} custom slots used."
            : "Custom Overworld Sprites patch (hzla PlatPatches) not detected. Add/Delete are disabled. Render-state properties below are still editable.";

        private bool _isSelectedEntryCustom;
        public bool IsSelectedEntryCustom { get => _isSelectedEntryCustom; private set => Set(ref _isSelectedEntryCustom, value); }

        public bool CanAddEntry => IsExpansionApplied && OverworldSpriteTableExpansion.UsedCount < OverworldSpriteTableExpansion.Capacity;
        public bool CanDeleteSelected => IsExpansionApplied && HasSelectedEntry && IsSelectedEntryCustom;

        public string[] DrawTypeOptions { get; } = { "None", "Billboard", "3D model" };
        public string[] ShadowTypeOptions { get; } = { "None", "On" };
        public string[] FootmarkTypeOptions { get; } = { "None", "Normal (2-leg)", "Cycle (bike)" };
        public string[] ReflectTypeOptions { get; } = { "None", "On (billboard reflection)" };

        private bool _hasRenderState;
        public bool HasRenderState { get => _hasRenderState; private set => Set(ref _hasRenderState, value); }

        private bool _loadingRenderState;
        private int _drawTypeIndex, _shadowTypeIndex, _footmarkTypeIndex, _reflectTypeIndex;
        public int DrawTypeIndex { get => _drawTypeIndex; set { if (Set(ref _drawTypeIndex, value)) CommitRenderState(); } }
        public int ShadowTypeIndex { get => _shadowTypeIndex; set { if (Set(ref _shadowTypeIndex, value)) CommitRenderState(); } }
        public int FootmarkTypeIndex { get => _footmarkTypeIndex; set { if (Set(ref _footmarkTypeIndex, value)) CommitRenderState(); } }
        public int ReflectTypeIndex { get => _reflectTypeIndex; set { if (Set(ref _reflectTypeIndex, value)) CommitRenderState(); } }

        private string _rendererInfoText;
        public string RendererInfoText { get => _rendererInfoText; private set => Set(ref _rendererInfoText, value); }
        private string _animationInfoText;
        public string AnimationInfoText { get => _animationInfoText; private set => Set(ref _animationInfoText, value); }

        // ── Design-time constructor ────────────────────────────────────────────
        public BtxEditorViewModel()
        {
            if (!Design.IsDesignMode) return;
            for (int i = 0; i < 12; i++) OwEntries.Add($"OW Entry {i}");
            _selectedIndex = 0;
            _statusText = "Design preview";
        }

        // ── Runtime constructor ────────────────────────────────────────────────
        public BtxEditorViewModel(bool _)
        {
            LoadEntryList();
            if (OwEntries.Count > 0)
            {
                _selectedIndex = 0;
                LoadEntry(0);
            }
        }

        private void LoadEntryList()
        {
            _owKeys = RomInfo.OverworldTable.Keys.ToList();
            OwEntries.Clear();
            foreach (var key in _owKeys)
                OwEntries.Add(OverworldLabels.Of(key)
                    + (IsPlatinum && OverworldSpriteTableExpansion.IsCustomEntry(key) ? " (custom)" : ""));
        }

        // ── Load entry ─────────────────────────────────────────────────────────
        private void LoadEntry(int index)
        {
            _isShiny = false;
            OnPropertyChanged(nameof(IsShiny));
            if (index < 0 || index >= _owKeys.Count)
            {
                CurrentImage = null;
                _btxData = null;
                HasShinyPalette = false;
                OnPropertyChanged(nameof(ShinyPaletteNote));
                ClearOverworldProperties();
                return;
            }

            uint key    = _owKeys[index];
            uint sprite = RomInfo.OverworldTable[key].spriteID;
            string path = Path.Combine(RomInfo.gameDirs[DirNames.OWSprites].unpackedDir, sprite.ToString("D4"));

            if (_modifiedFiles.TryGetValue(key, out byte[] mod))
                _btxData = mod;
            else if (File.Exists(path))
                _btxData = File.ReadAllBytes(path);
            else
            {
                _btxData = null;
                CurrentImage = null;
                HasShinyPalette = false;
                OnPropertyChanged(nameof(ShinyPaletteNote));
                StatusText = "File not found";
                LoadOverworldProperties(key);
                return;
            }

            RefreshImage();
            LoadOverworldProperties(key);
        }

        private void ClearOverworldProperties()
        {
            HasRenderState = false;
            IsSelectedEntryCustom = false;
            RendererInfoText = null;
            AnimationInfoText = null;
            OnPropertyChanged(nameof(CanDeleteSelected));
        }

        private void LoadOverworldProperties(uint key)
        {
            if (!IsPlatinum) { ClearOverworldProperties(); return; }

            IsSelectedEntryCustom = OverworldSpriteTableExpansion.IsCustomEntry(key);

            _loadingRenderState = true;
            if (OverworldSpriteTableExpansion.TryReadRenderState(key, out var state))
            {
                DrawTypeIndex = state.DrawType;
                ShadowTypeIndex = state.ShadowType;
                FootmarkTypeIndex = state.FootmarkType;
                ReflectTypeIndex = state.ReflectType;
                HasRenderState = true;
            }
            else
            {
                HasRenderState = false;
            }
            _loadingRenderState = false;

            if (OverworldSpriteTableExpansion.IsApplied)
            {
                RendererInfoText = FormatRawRow(OverworldSpriteTableExpansion.ReadRawRow(0, key));
                AnimationInfoText = FormatRawRow(OverworldSpriteTableExpansion.ReadRawRow(3, key));
            }
            else
            {
                RendererInfoText = null;
                AnimationInfoText = null;
            }

            OnPropertyChanged(nameof(CanDeleteSelected));
        }

        private static string FormatRawRow(byte[] row) =>
            row == null ? "n/a" : string.Join(" ", row.Select(b => b.ToString("X2")));

        private void CommitRenderState()
        {
            if (_loadingRenderState || !HasRenderState || !HasSelectedEntry) return;
            uint key = _owKeys[_selectedIndex];
            var state = new OverworldSpriteTableExpansion.OwRenderState
            {
                DrawType = _drawTypeIndex,
                ShadowType = _shadowTypeIndex,
                FootmarkType = _footmarkTypeIndex,
                ReflectType = _reflectTypeIndex,
            };
            if (!OverworldSpriteTableExpansion.TryWriteRenderState(key, state, out string error))
                StatusText = "Render-state write failed: " + error;
        }

        // ── Add / Delete custom entries (expansion patch only) ───────────────────
        /// <summary>Adds a new custom overworld entry (called from the "Add Custom Entry…" dialog
        /// once the user confirms it). If an image was picked, it is NEVER written into
        /// <paramref name="templateMember"/> (the slot the user chose in the dropdown); that slot
        /// is only read as a structural template (matching width/height/color-count), which
        /// <see cref="LibNDSFormats.BTX0.Write"/> requires. The actual pixels are written into a
        /// brand-new mmodel NARC member (<see cref="OverworldSpriteTableExpansion.AllocateNewMmodelSlot"/>)
        /// so no existing overworld's art is ever touched. Without an image, the entry just points at
        /// <paramref name="templateMember"/> directly and shares that art on purpose, no write happens.
        /// Returns null on full success; if the table row was added but the image import failed,
        /// still refreshes the list but returns a message saying so.</summary>
        public string AddEntryWithImage(string appearanceIdText, uint templateMember, uint cloneFrom, string pngPath, string rawBtxPath)
        {
            if (!TryParseId(appearanceIdText, "Appearance ID", out uint appearanceId, out string error)) return error;

            bool hasImage = rawBtxPath != null || pngPath != null;
            uint mmodelMember = hasImage ? OverworldSpriteTableExpansion.AllocateNewMmodelSlot() : templateMember;

            if (!OverworldSpriteTableExpansion.AddEntry(appearanceId, mmodelMember, cloneFrom, out error))
                return error;

            RomInfo.ReadOWTable();
            LoadEntryList();

            string imageError = rawBtxPath != null ? StageEntryRawBtx(appearanceId, templateMember, rawBtxPath)
                : pngPath != null ? StageEntryPng(appearanceId, templateMember, pngPath)
                : null;

            SelectEntry(_owKeys.IndexOf(appearanceId));
            OnPropertyChanged(nameof(ExpansionStatusText));
            OnPropertyChanged(nameof(CanAddEntry));

            return imageError != null ? $"Entry was added, but the image import failed: {imageError}" : null;
        }

        /// <summary>Reads <paramref name="templateMember"/>'s existing BTX0 file purely as a
        /// read-only structural template (its bytes are never written back to that slot) and stages
        /// a pixel-perfect copy of <paramref name="rawBtxPath"/>'s texture data for the new entry's
        /// own (already-allocated, independent) mmodel member. Returns null on success.</summary>
        private string StageEntryRawBtx(uint appearanceId, uint templateMember, string rawBtxPath)
        {
            string templatePath = Path.Combine(RomInfo.gameDirs[DirNames.OWSprites].unpackedDir, templateMember.ToString("D4"));
            if (!File.Exists(templatePath)) return "Template texture slot file not found.";
            try
            {
                var target = BTX0.ReadRaw(File.ReadAllBytes(templatePath));
                if (target == null) return "Template texture slot is unreadable.";

                byte[] sourceData = File.ReadAllBytes(rawBtxPath);
                var source = BTX0.ReadRaw(sourceData);
                if (source == null) return "Source file isn't a texture DSPRE can read (BTX0, 16-color format).";

                if (source.Width != target.Width || source.Height != target.Height)
                    return $"Size mismatch. Template slot: {target.Width}×{target.Height}, source texture: {source.Width}×{source.Height}";

                _modifiedFiles[appearanceId] = sourceData;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                OnPropertyChanged(nameof(ModifiedCount));
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>Reads <paramref name="templateMember"/>'s existing BTX0 file purely as a
        /// read-only structural template (its bytes are never written back to that slot, only cloned
        /// into memory and patched there) and stages the patched result for the new entry's own
        /// (already-allocated, independent) mmodel member. Returns null on success.</summary>
        private string StageEntryPng(uint appearanceId, uint templateMember, string pngPath)
        {
            string templatePath = Path.Combine(RomInfo.gameDirs[DirNames.OWSprites].unpackedDir, templateMember.ToString("D4"));
            if (!File.Exists(templatePath)) return "Template texture slot file not found.";
            try
            {
                byte[] btxData = File.ReadAllBytes(templatePath); // fresh read every call, safe for BTX0.Write to mutate in place
                RawImage import;
                using (var fs = File.OpenRead(pngPath))
                    import = ImageConverter.DecodeRawImage(fs);
                if (import == null) return "Image could not be decoded.";
                var current = BTX0.ReadRaw(btxData);
                if (current == null) return "Template texture slot is unreadable.";
                if (import.Width != current.Width || import.Height != current.Height)
                    return $"Size mismatch. Template slot: {current.Width}×{current.Height}, PNG: {import.Width}×{import.Height}";

                uint colors = CountColors(import);
                if (colors > BTX0.ColorCount)
                    return $"Too many colors. Limit: {BTX0.ColorCount}, PNG: {colors}";

                _modifiedFiles[appearanceId] = BTX0.Write(btxData, import);
                OnPropertyChanged(nameof(HasUnsavedChanges));
                OnPropertyChanged(nameof(ModifiedCount));
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// Returns null on success, error message on failure.
        public string DeleteSelectedEntry()
        {
            if (!HasSelectedEntry) return "No entry selected.";
            uint key = _owKeys[_selectedIndex];
            if (!OverworldSpriteTableExpansion.DeleteEntry(key, out string error)) return error;

            RomInfo.ReadOWTable();
            LoadEntryList();
            SelectEntry(OwEntries.Count > 0 ? 0 : -1);
            OnPropertyChanged(nameof(ExpansionStatusText));
            OnPropertyChanged(nameof(CanAddEntry));
            return null;
        }

        /// <summary>Selects an entry and always reloads its data, unlike the SelectedIndex
        /// property setter, which skips the reload when the index number happens not to have
        /// changed even though the underlying entry at that index has (e.g. after Add/Delete
        /// reshuffles the list).</summary>
        private void SelectEntry(int index)
        {
            _selectedIndex = index;
            OnPropertyChanged(nameof(SelectedIndex));
            OnPropertyChanged(nameof(HasSelectedEntry));
            LoadEntry(index);
        }

        private static bool TryParseId(string text, string label, out uint value, out string error)
        {
            value = 0; error = null;
            text = (text ?? "").Trim();
            bool ok = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? uint.TryParse(text.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out value)
                : uint.TryParse(text, out value);
            if (!ok) error = $"{label}: \"{text}\" is not a valid number (decimal or 0x hex).";
            return ok;
        }

        // ── Refresh image ──────────────────────────────────────────────────────
        private void RefreshImage()
        {
            if (_btxData == null) { CurrentImage = null; return; }
            try
            {
                BTX0.PaletteIndex = 0;
                var raw = BTX0.ReadRaw(_btxData);
                HasShinyPalette = raw != null && BTX0.PaletteSize == 64 && BTX0.PaletteCount == 2;
                OnPropertyChanged(nameof(ShinyPaletteNote));
                if (_isShiny && HasShinyPalette)
                {
                    BTX0.PaletteIndex = 1;
                    raw = BTX0.ReadRaw(_btxData);
                }
                CurrentImage = raw != null ? ImageConverter.ToAvaloniaBitmap(raw) : null;
                StatusText = CurrentImage != null
                    ? $"{CurrentImage.PixelSize.Width}×{CurrentImage.PixelSize.Height}, {BTX0.ColorCount} colors"
                    : "Unsupported format";
            }
            catch (Exception ex)
            {
                CurrentImage = null;
                StatusText = $"Error: {ex.Message}";
            }
        }

        // ── Import PNG ─────────────────────────────────────────────────────────
        /// Returns null on success, error message on failure.
        public string ImportPng(string filePath)
        {
            if (_btxData == null || _selectedIndex < 0) return "No entry selected.";
            try
            {
                RawImage import;
                using (var fs = File.OpenRead(filePath))
                    import = ImageConverter.DecodeRawImage(fs);
                if (import == null) return "Image could not be decoded.";
                var current = BTX0.ReadRaw(_btxData);
                if (current == null) return "This entry's texture file isn't a readable image (it may be a 3D model, not a flat texture).";
                if (import.Width != current.Width || import.Height != current.Height)
                    return $"Size mismatch. Existing texture: {current.Width}×{current.Height}, PNG: {import.Width}×{import.Height}";

                uint colors = CountColors(import);
                if (colors > BTX0.ColorCount)
                    return $"Too many colors. Limit: {BTX0.ColorCount}, PNG: {colors}";

                byte[] newData = BTX0.Write(_btxData, import);
                _btxData = newData;

                uint key = _owKeys[_selectedIndex];
                _modifiedFiles[key] = newData;

                RefreshImage();
                OnPropertyChanged(nameof(HasUnsavedChanges));
                OnPropertyChanged(nameof(ModifiedCount));
                return null; // success
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ── Export PNG ─────────────────────────────────────────────────────────
        public bool ExportPng(string filePath)
        {
            if (_btxData == null) return false;
            try
            {
                var raw = BTX0.ReadRaw(_btxData);
                if (raw == null) return false;
                ImageConverter.ToAvaloniaBitmap(raw).Save(filePath, PngBitmapEncoderOptions.Default);
                return true;
            }
            catch { return false; }
        }

        // ── Show file in Explorer ──────────────────────────────────────────────
        public string GetCurrentFilePath()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _owKeys.Count) return null;
            uint key    = _owKeys[_selectedIndex];
            uint sprite = RomInfo.OverworldTable[key].spriteID;
            return Path.Combine(RomInfo.gameDirs[DirNames.OWSprites].unpackedDir, sprite.ToString("D4"));
        }

        // ── Save ───────────────────────────────────────────────────────────────
        public int SaveSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _owKeys.Count) return 0;
            uint key = _owKeys[_selectedIndex];
            if (!_modifiedFiles.TryGetValue(key, out byte[] data)) return 0;

            uint sprite = RomInfo.OverworldTable[key].spriteID;
            string path = Path.Combine(RomInfo.gameDirs[DirNames.OWSprites].unpackedDir, sprite.ToString("D4"));
            File.WriteAllBytes(path, data);
            _modifiedFiles.Remove(key);
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(ModifiedCount));
            return 1;
        }

        public int SaveAll()
        {
            int saved = 0;
            foreach (var kvp in _modifiedFiles.ToList())
            {
                uint sprite = RomInfo.OverworldTable[kvp.Key].spriteID;
                string path = Path.Combine(RomInfo.gameDirs[DirNames.OWSprites].unpackedDir, sprite.ToString("D4"));
                File.WriteAllBytes(path, kvp.Value);
                _modifiedFiles.Remove(kvp.Key);
                saved++;
            }
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(ModifiedCount));
            return saved;
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private static uint CountColors(RawImage img)
        {
            var seen = new HashSet<uint>();
            for (int i = 0; i < img.Bgra.Length; i += 4)
                seen.Add(BitConverter.ToUInt32(img.Bgra, i));
            return (uint)seen.Count;
        }
    }
}
