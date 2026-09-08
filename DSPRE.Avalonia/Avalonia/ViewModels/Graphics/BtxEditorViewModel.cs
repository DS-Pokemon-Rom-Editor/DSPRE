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
using System.Threading.Tasks;
using static DSPRE.RomInfo;

using DSPRE.Avalonia.Data;
namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed class OverworldGraphicsProfileOption
    {
        public uint AppearanceId { get; init; }
        public uint SpriteMember { get; init; }
        public string Label { get; init; }
        public override string ToString() => Label;
    }

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
            set
            {
                if (value == _selectedIndex) return;
                if (HasUnsavedChanges && value >= 0 && _selectedIndex >= 0)
                {
                    // Snap the list back to the entry still loaded until the user has answered.
                    int requested = value;
                    OnPropertyChanged(nameof(SelectedIndex));
                    _ = SwitchEntryAsync(requested);
                    return;
                }
                if (Set(ref _selectedIndex, value)) LoadEntry(value);
            }
        }

        private async Task SwitchEntryAsync(int requested)
        {
            // Dirty here means edited sprite files, not one record, so Discard drops them all. The
            // guard names that through UnsavedChangesDescription, which reports the count.
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, null, "sprite entry")) return;
            if (Set(ref _selectedIndex, requested)) LoadEntry(requested);
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
        private readonly Dictionary<uint, OverworldSpriteProfileMetadataPatch> _metadataPatches = new();

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
        public void DiscardChanges()
        {
            _modifiedFiles.Clear();
            _metadataPatches.Clear();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(ModifiedCount));
            LoadEntry(_selectedIndex);
        }

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
        /// Image and profile compatibility are validated before the table row is added, so a bad
        /// import cannot leave behind a partially added entry.</summary>
        public string AddEntryWithImage(string appearanceIdText, uint templateMember, uint cloneFrom, string pngPath, string rawBtxPath)
        {
            if (!TryParseId(appearanceIdText, "Appearance ID", out uint appearanceId, out string error)) return error;

            bool hasImage = rawBtxPath != null || pngPath != null;
            uint mmodelMember = hasImage ? OverworldSpriteTableExpansion.AllocateNewMmodelSlot() : templateMember;

            if (!TryValidateTemplateForCloneSource(templateMember, cloneFrom, out error))
                return error;

            byte[] stagedImage = null;
            if (rawBtxPath != null && !TryBuildRawBtx(templateMember, rawBtxPath, out stagedImage, out error))
                return error;
            if (pngPath != null && !TryBuildPngBtx(templateMember, pngPath, out stagedImage, out error))
                return error;

            if (!OverworldSpriteTableExpansion.AddEntry(appearanceId, mmodelMember, cloneFrom, out error))
                return error;

            if (stagedImage != null)
            {
                string newMemberPath = Path.Combine(
                    RomInfo.gameDirs[DirNames.OWSprites].unpackedDir,
                    mmodelMember.ToString("D4"));
                try
                {
                    File.WriteAllBytes(newMemberPath, stagedImage);
                }
                catch (Exception ex)
                {
                    try
                    {
                        if (File.Exists(newMemberPath)) File.Delete(newMemberPath);
                    }
                    catch { }

                    if (!OverworldSpriteTableExpansion.DeleteEntry(appearanceId, out string rollbackError))
                        return $"Could not create the new texture member: {ex.Message}. The table rollback also failed: {rollbackError}";
                    return "Could not create the new texture member; the table entry was rolled back: " + ex.Message;
                }
            }

            RomInfo.ReadOWTable();
            LoadEntryList();

            SelectEntry(_owKeys.IndexOf(appearanceId));
            OnPropertyChanged(nameof(ExpansionStatusText));
            OnPropertyChanged(nameof(CanAddEntry));

            return null;
        }

        /// <summary>Reads <paramref name="templateMember"/>'s existing BTX0 file purely as a
        /// read-only structural template (its bytes are never written back to that slot) and stages
        /// a pixel-perfect copy of <paramref name="rawBtxPath"/>'s texture data for the new entry's
        /// own (already-allocated, independent) mmodel member. Returns null on success.</summary>
        private static bool TryBuildRawBtx(uint templateMember, string rawBtxPath, out byte[] stagedImage, out string error)
        {
            stagedImage = null;
            error = null;
            string templatePath = Path.Combine(RomInfo.gameDirs[DirNames.OWSprites].unpackedDir, templateMember.ToString("D4"));
            if (!File.Exists(templatePath)) { error = "Template texture slot file not found."; return false; }
            try
            {
                byte[] templateData = File.ReadAllBytes(templatePath);
                if (!Btx0Structure.TryInspect(templateData, out Btx0Structure targetStructure, out string targetError))
                { error = "Template texture slot is unreadable: " + targetError; return false; }

                byte[] sourceData = File.ReadAllBytes(rawBtxPath);
                if (!Btx0Structure.TryInspect(sourceData, out Btx0Structure sourceStructure, out string sourceError))
                { error = "Source file isn't a structurally readable BTX0: " + sourceError; return false; }

                if (!targetStructure.HasSameProfileAs(sourceStructure))
                { error = "The raw BTX uses a different dictionary, frame-reuse, texture, or palette layout than the selected profile."; return false; }

                var source = BTX0.ReadRaw(sourceData);
                if (source == null) { error = "Source file isn't a texture DSPRE can write (BTX0, 16-color format)."; return false; }

                stagedImage = sourceData;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>Reads <paramref name="templateMember"/>'s existing BTX0 file purely as a
        /// read-only structural template (its bytes are never written back to that slot, only cloned
        /// into memory and patched there) and stages the patched result for the new entry's own
        /// (already-allocated, independent) mmodel member. Returns null on success.</summary>
        private static bool TryBuildPngBtx(uint templateMember, string pngPath, out byte[] stagedImage, out string error)
        {
            stagedImage = null;
            error = null;
            string templatePath = Path.Combine(RomInfo.gameDirs[DirNames.OWSprites].unpackedDir, templateMember.ToString("D4"));
            if (!File.Exists(templatePath)) { error = "Template texture slot file not found."; return false; }
            try
            {
                byte[] btxData = File.ReadAllBytes(templatePath); // fresh read every call, safe for BTX0.Write to mutate in place
                RawImage import;
                using (var fs = File.OpenRead(pngPath))
                    import = ImageConverter.DecodeRawImage(fs);
                if (import == null) { error = "Image could not be decoded."; return false; }
                var current = BTX0.ReadRaw(btxData);
                if (current == null) { error = "Template texture slot is unreadable."; return false; }
                if (import.Width != current.Width || import.Height != current.Height)
                { error = $"Size mismatch. Template slot: {current.Width}×{current.Height}, PNG: {import.Width}×{import.Height}"; return false; }

                uint colors = CountColors(import);
                if (colors > BTX0.ColorCount)
                { error = $"Too many colors. Limit: {BTX0.ColorCount}, PNG: {colors}"; return false; }

                stagedImage = BTX0.Write(btxData, import);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryValidateTemplateForCloneSource(uint templateMember, uint cloneFrom, out string error)
        {
            error = null;
            if (!RomInfo.OverworldTable.TryGetValue(cloneFrom, out var cloneEntry))
            {
                error = "The clone source is no longer present in the overworld table.";
                return false;
            }

            string directory = RomInfo.gameDirs[DirNames.OWSprites].unpackedDir;
            string templatePath = Path.Combine(directory, templateMember.ToString("D4"));
            string clonePath = Path.Combine(directory, cloneEntry.spriteID.ToString("D4"));
            if (!File.Exists(templatePath) || !File.Exists(clonePath))
            {
                error = "The format template or clone source texture file was not found.";
                return false;
            }

            if (!Btx0Structure.TryInspect(File.ReadAllBytes(templatePath), out Btx0Structure template, out string templateError))
            {
                error = "The format template is not a readable BTX0: " + templateError;
                return false;
            }
            if (!Btx0Structure.TryInspect(File.ReadAllBytes(clonePath), out Btx0Structure clone, out string cloneError))
            {
                error = "The clone source is not a readable BTX0: " + cloneError;
                return false;
            }
            if (!template.HasSameProfileAs(clone))
            {
                error = "The format template does not match the clone source's dictionary, frame-reuse, texture, and palette layout. Choose both from the same graphics profile.";
                return false;
            }
            return true;
        }

        /// Returns null on success, error message on failure.
        public string DeleteSelectedEntry()
        {
            if (!HasSelectedEntry) return "No entry selected.";
            uint key = _owKeys[_selectedIndex];
            if (!OverworldSpriteTableExpansion.DeleteEntry(key, out string error)) return error;

            _modifiedFiles.Remove(key);
            _metadataPatches.Remove(key);

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

        /// <summary>
        /// Returns true when the PNG cannot use the selected entry's current BTX layout and therefore
        /// needs a complete game-local graphics profile. Only profiles whose cloned BTX can hold the
        /// PNG are returned; dimensions alone are not treated as structural compatibility.
        /// </summary>
        public bool TryGetCompatibleProfiles(
            string filePath,
            out List<OverworldGraphicsProfileOption> profiles,
            out string error)
        {
            profiles = new List<OverworldGraphicsProfileOption>();
            error = null;
            if (_btxData == null || !HasSelectedEntry)
            {
                error = "No entry selected.";
                return false;
            }

            try
            {
                RawImage import;
                using (var fs = File.OpenRead(filePath))
                    import = ImageConverter.DecodeRawImage(fs);
                if (import == null)
                {
                    error = "Image could not be decoded.";
                    return false;
                }

                RawImage current = BTX0.ReadRaw(_btxData);
                if (current == null)
                {
                    error = "This entry's texture file isn't a readable 16-color BTX image.";
                    return false;
                }
                if (import.Width == current.Width && import.Height == current.Height)
                    return false;

                uint colorCount = CountColors(import);
                uint targetKey = _owKeys[_selectedIndex];
                string dir = RomInfo.gameDirs[DirNames.OWSprites].unpackedDir;
                var structures = new Dictionary<uint, Btx0Structure>();
                var sharedCounts = RomInfo.OverworldTable.Values
                    .GroupBy(v => v.spriteID)
                    .ToDictionary(g => g.Key, g => g.Count());
                foreach (var entry in RomInfo.OverworldTable)
                {
                    if (entry.Key == targetKey || entry.Value.spriteID == 0x3D3D) continue;
                    if (!structures.TryGetValue(entry.Value.spriteID, out Btx0Structure structure))
                    {
                        string path = Path.Combine(dir, entry.Value.spriteID.ToString("D4"));
                        if (!File.Exists(path) || !Btx0Structure.TryInspect(File.ReadAllBytes(path), out structure, out _))
                            continue;
                        structures.Add(entry.Value.spriteID, structure);
                    }
                    if (
                        structure.SheetWidth != import.Width || structure.SheetHeight != import.Height ||
                        structure.Palettes.Count == 0 || structure.Palettes[0].ColorCapacity < colorCount)
                        continue;

                    int sharedBy = sharedCounts[entry.Value.spriteID];
                    profiles.Add(new OverworldGraphicsProfileOption
                    {
                        AppearanceId = entry.Key,
                        SpriteMember = entry.Value.spriteID,
                        Label = $"{OverworldLabels.Of(entry.Key)} · slot {entry.Value.spriteID} · " +
                            $"{structure.Textures.Count} entries / {structure.UniqueTextureBlockCount} stored frames" +
                            (sharedBy > 1 ? $" · art shared by {sharedBy} appearances" : ""),
                    });
                }

                profiles = profiles
                    .OrderBy(p => p.SpriteMember)
                    .ThenBy(p => p.AppearanceId)
                    .ToList();
                if (profiles.Count == 0)
                    error = $"No existing overworld profile in this ROM accepts a {import.Width}×{import.Height} PNG with {colorCount} colors.";
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public string ImportPngUsingProfile(string filePath, uint sourceAppearanceId)
        {
            if (_btxData == null || !HasSelectedEntry) return "No entry selected.";
            if (!RomInfo.OverworldTable.TryGetValue(sourceAppearanceId, out var sourceEntry))
                return "The selected profile is no longer present in the overworld table.";

            uint targetAppearanceId = _owKeys[_selectedIndex];
            try
            {
                RawImage import;
                using (var fs = File.OpenRead(filePath))
                    import = ImageConverter.DecodeRawImage(fs);
                if (import == null) return "Image could not be decoded.";

                string sourcePath = Path.Combine(
                    RomInfo.gameDirs[DirNames.OWSprites].unpackedDir,
                    sourceEntry.spriteID.ToString("D4"));
                if (!File.Exists(sourcePath)) return "The selected profile's BTX file was not found.";

                byte[] sourceData = File.ReadAllBytes(sourcePath);
                if (!Btx0Structure.TryInspect(sourceData, out Btx0Structure structure, out string structureError))
                    return "The selected profile is not structurally readable: " + structureError;
                if (structure.SheetWidth != import.Width || structure.SheetHeight != import.Height)
                    return $"The selected profile expects {structure.SheetWidth}×{structure.SheetHeight}, but the PNG is {import.Width}×{import.Height}.";
                uint colors = CountColors(import);
                if (structure.Palettes.Count == 0 || colors > structure.Palettes[0].ColorCapacity)
                    return $"The selected profile cannot hold the PNG's {colors} colors.";

                if (!OverworldSpriteProfileMetadata.TryCreatePatch(
                    targetAppearanceId, sourceAppearanceId, out OverworldSpriteProfileMetadataPatch metadataPatch, out string metadataError))
                    return metadataError;

                byte[] newData = (byte[])sourceData.Clone();
                BTX0.PaletteIndex = 0;
                RawImage profileImage = BTX0.ReadRaw(newData);
                if (profileImage == null || profileImage.Width != import.Width || profileImage.Height != import.Height)
                    return "The selected profile is not writable by DSPRE's 16-color importer.";
                if (colors > BTX0.ColorCount)
                    return $"Too many colors. Profile limit: {BTX0.ColorCount}, PNG: {colors}.";

                newData = BTX0.Write(newData, import);
                _btxData = newData;
                _modifiedFiles[targetAppearanceId] = newData;
                _metadataPatches[targetAppearanceId] = metadataPatch;

                RefreshImage();
                StatusText = $"Staged {import.Width}×{import.Height} image with the profile from {OverworldLabels.Of(sourceAppearanceId)}.";
                OnPropertyChanged(nameof(HasUnsavedChanges));
                OnPropertyChanged(nameof(ModifiedCount));
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public string GetSelectedMemberUsageWarning()
        {
            if (!HasSelectedEntry) return null;
            uint targetKey = _owKeys[_selectedIndex];
            uint member = RomInfo.OverworldTable[targetKey].spriteID;
            uint[] users = RomInfo.OverworldTable
                .Where(kv => kv.Value.spriteID == member)
                .Select(kv => kv.Key)
                .ToArray();
            if (users.Length <= 1) return null;
            return $"Texture slot {member} is shared by {users.Length} appearances. Saving this import will change their artwork too. Continue?";
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

            return SaveEntry(key, data) ? 1 : 0;
        }

        public int SaveAll()
        {
            int saved = 0;
            foreach (var kvp in _modifiedFiles.ToList())
            {
                if (SaveEntry(kvp.Key, kvp.Value)) saved++;
            }
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(ModifiedCount));
            if (saved > 0) SaveNotice.Show($"Saved {saved} overworld sprite file{(saved == 1 ? "" : "s")}.");
            return saved;
        }

        private bool SaveEntry(uint key, byte[] data)
        {
            uint sprite = RomInfo.OverworldTable[key].spriteID;
            string path = Path.Combine(RomInfo.gameDirs[DirNames.OWSprites].unpackedDir, sprite.ToString("D4"));
            byte[] original = File.Exists(path) ? File.ReadAllBytes(path) : null;
            bool metadataApplied = false;

            if (_metadataPatches.TryGetValue(key, out OverworldSpriteProfileMetadataPatch patch))
            {
                if (!patch.TryApply(out string metadataError))
                {
                    StatusText = "Save failed: " + metadataError;
                    return false;
                }
                metadataApplied = true;
            }

            try
            {
                File.WriteAllBytes(path, data);
            }
            catch (Exception ex)
            {
                if (original != null)
                {
                    try { File.WriteAllBytes(path, original); } catch { }
                }
                if (metadataApplied && !patch.TryRollback(out string rollbackError))
                    StatusText = $"Save failed: {ex.Message}. Metadata rollback also failed: {rollbackError}";
                else
                    StatusText = "Save failed: " + ex.Message;
                return false;
            }

            _modifiedFiles.Remove(key);
            _metadataPatches.Remove(key);
            StatusText = "Saved.";
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(ModifiedCount));
            return true;
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
