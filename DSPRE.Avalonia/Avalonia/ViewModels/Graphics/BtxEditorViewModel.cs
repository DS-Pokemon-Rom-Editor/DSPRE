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

using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using DSPRE.HgEngine;
namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed class OverworldGraphicsProfileOption
    {
        public uint AppearanceId { get; init; }
        public uint SpriteMember { get; init; }
        public string Label { get; init; }
        public override string ToString() => Label;
    }

    /// <summary>One way of facing, drawn standing, walking and running as the field animates it.</summary>
    public sealed class FacingPreview : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public string Label { get; init; }
        public int Facing { get; init; }
        public double Width { get; init; }
        public double Height { get; init; }
        public bool HasWalking { get; init; }
        public bool HasRunning { get; init; }

        private Bitmap _looking, _walking, _running;
        public Bitmap Looking { get => _looking; set { if (!ReferenceEquals(_looking, value)) { _looking = value; Changed(nameof(Looking)); } } }
        public Bitmap Walking { get => _walking; set { if (!ReferenceEquals(_walking, value)) { _walking = value; Changed(nameof(Walking)); } } }
        public Bitmap Running { get => _running; set { if (!ReferenceEquals(_running, value)) { _running = value; Changed(nameof(Running)); } } }
    }

    public partial class BtxEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        public ObservableCollection<string> OwEntries { get; } = new();
        private List<uint> _owKeys = new();

        private int _selectedIndex = -1;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (value == _selectedIndex) return;
                if (HasUnsavedChanges && value >= 0 && _selectedIndex >= 0)
                {
                    // Snap the list back to the entry still loaded until the user has answered.
                    int requested = value;
                    RecordSwitchGuard.SnapBack(() => _selectedIndex, v => _selectedIndex = v, () => OnPropertyChanged(nameof(SelectedIndex)));
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
            if (Set(ref _selectedIndex, requested, nameof(SelectedIndex))) LoadEntry(requested);
        }

        private Bitmap _currentImage;
        public Bitmap CurrentImage { get => _currentImage; private set { Set(ref _currentImage, value); BuildFacings(); } }

        private string _statusText = "";
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        public bool HasSelectedEntry => _selectedIndex >= 0 && _selectedIndex < _owKeys.Count;

        // Render properties wait for Save like the sprites do, so Discard can still drop them.
        private readonly Dictionary<uint, OverworldSpriteTableExpansion.OwRenderState> _pendingRender = new();
        private int ModifiedEntries => _modifiedFiles.Keys.Union(_pendingRender.Keys).Count();

        public string ModifiedCount =>
            ModifiedEntries > 0 ? $"{ModifiedEntries} unsaved" : "";

        public bool HasUnsavedChanges => ModifiedEntries > 0;
        public string UnsavedChangesDescription =>
            $"{ModifiedEntries} overworld{(ModifiedEntries != 1 ? "s" : "")}";

        public void SaveChanges() => SaveAll();
        public void DiscardChanges()
        {
            _modifiedFiles.Clear();
            _metadataPatches.Clear();
            _pendingRender.Clear();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(ModifiedCount));
            LoadEntry(_selectedIndex);
            ResetUndo();
        }

        public BtxEditorViewModel()
        {
            if (!Design.IsDesignMode) return;
            for (int i = 0; i < 12; i++) OwEntries.Add($"OW entry {i}");
            _selectedIndex = 0;
            _statusText = "Design preview";
        }

        public BtxEditorViewModel(bool _)
        {
            try { _hasRenderTable = OverworldSpriteTableExpansion.IsRenderTableAvailable; }
            catch (Exception ex) { AppLogger.Error("Render table lookup failed: " + ex.Message); }
            LoadEntryList();
            ResetUndo();
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
            foreach (uint key in _owKeys)
                OwEntries.Add(OverworldLabels.Of(key)
                    + (IsExpansionApplied && OverworldSpriteTableExpansion.IsCustomEntry(key) ? " (custom)" : ""));
        }

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
            if (sprite == 0x3D3D)
            {
                _btxData = null;
                CurrentImage = null;
                HasShinyPalette = false;
                OnPropertyChanged(nameof(ShinyPaletteNote));
                StatusText = RomInfo.IsVariableOverworld(key)
                    ? "This id takes its picture from a map variable."
                    : "This object is a 3D model.";
                LoadOverworldProperties(key);
                return;
            }

            if (_modifiedFiles.TryGetValue(key, out byte[] mod))
                _btxData = mod;
            else if (ArchiveMembers.Has(DirNames.OWSprites, (int)sprite))
                _btxData = ArchiveMembers.Read(DirNames.OWSprites, (int)sprite);
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
            if (!HasRenderTable) { ClearOverworldProperties(); return; }

            IsSelectedEntryCustom = OverworldSpriteTableExpansion.IsCustomEntry(key);

            _loadingRenderState = true;
            if (_pendingRender.TryGetValue(key, out OverworldSpriteTableExpansion.OwRenderState state) || OverworldSpriteTableExpansion.TryReadRenderState(key, out state))
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
            OverworldSpriteTableExpansion.OwRenderState state = new OverworldSpriteTableExpansion.OwRenderState
            {
                DrawType = _drawTypeIndex,
                ShadowType = _shadowTypeIndex,
                FootmarkType = _footmarkTypeIndex,
                ReflectType = _reflectTypeIndex,
            };
            TouchRender(key);
            _pendingRender[key] = state;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(ModifiedCount));
            Edited();
        }

        private bool SaveRenderState(uint key)
        {
            if (!_pendingRender.TryGetValue(key, out OverworldSpriteTableExpansion.OwRenderState state)) return true;
            if (!OverworldSpriteTableExpansion.TryWriteRenderState(key, state, out string error))
            {
                return Fail("The draw settings weren't saved:\n" + error);
            }
            _pendingRender.Remove(key);
            return true;
        }

        private void RefreshImage()
        {
            if (_btxData == null) { CurrentImage = null; return; }
            try
            {
                RawImage raw = BTX0.ReadRaw(_btxData, 0);
                HasShinyPalette = raw != null && BTX0.PaletteSize == 64 && BTX0.PaletteCount == 2;
                OnPropertyChanged(nameof(ShinyPaletteNote));
                if (CurrentPaletteIndex != 0)
                    raw = BTX0.ReadRaw(_btxData, CurrentPaletteIndex);
                CurrentImage = raw != null ? ImageConverter.ToAvaloniaBitmap(raw) : null;
                StatusText = CurrentImage != null
                    ? $"{CurrentImage.PixelSize.Width}×{CurrentImage.PixelSize.Height}, {BTX0.ColorCount} colours"
                    : "Unsupported format";
            }
            catch (Exception ex)
            {
                CurrentImage = null;
                StatusText = $"Error: {ex.Message}";
            }
        }

        private uint CurrentPaletteIndex => _isShiny && HasShinyPalette ? 1u : 0u;

        public string GetCurrentFilePath()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _owKeys.Count) return null;
            uint key    = _owKeys[_selectedIndex];
            uint sprite = RomInfo.OverworldTable[key].spriteID;
            return Path.Combine(RomInfo.GameDirs[DirNames.OWSprites].unpackedDir, sprite.ToString("D4"));
        }

        private static uint CountColors(RawImage img) => (uint)BTX0.CountColors(img);
    }
}
