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

namespace DSPRE.Avalonia.ViewModels
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
            _owKeys = RomInfo.OverworldTable.Keys.ToList();
            foreach (var key in _owKeys)
                OwEntries.Add($"OW Entry {key}");

            if (OwEntries.Count > 0)
            {
                _selectedIndex = 0;
                LoadEntry(0);
            }
        }

        // ── Load entry ─────────────────────────────────────────────────────────
        private void LoadEntry(int index)
        {
            if (index < 0 || index >= _owKeys.Count) { CurrentImage = null; _btxData = null; return; }

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
                StatusText = "File not found";
                return;
            }

            RefreshImage();
        }

        private void RefreshImage()
        {
            if (_btxData == null) { CurrentImage = null; return; }
            try
            {
                BTX0.PaletteIndex = _isShiny ? 1u : 0u;
                var raw = BTX0.ReadRaw(_btxData);
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
                if (current == null) return "Current BTX file is unreadable.";
                if (import.Width != current.Width || import.Height != current.Height)
                    return $"Size mismatch. BTX: {current.Width}×{current.Height}, PNG: {import.Width}×{import.Height}";

                uint colors = CountColors(import);
                if (colors > BTX0.ColorCount)
                    return $"Too many colors. BTX limit: {BTX0.ColorCount}, PNG: {colors}";

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
