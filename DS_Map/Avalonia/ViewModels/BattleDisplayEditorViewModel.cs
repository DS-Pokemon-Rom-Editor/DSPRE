using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// "Battle Display" tab of the Pokémon editor: per-species presentation tweaks that live outside the
    /// personal/sprite data. Currently the party-icon palette (which of the 3 icon palettes a mon's party
    /// icon uses — 1 byte per species in the ARM9 icon-palette table). Battle-sprite coordinates
    /// (/a/1/8/0) will be added here next. GATED to HeartGold/SoulSilver (English) for now, since the
    /// underlying offsets are version-specific.
    /// </summary>
    public class BattleDisplayEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        /// <summary>True only on HG/SS English — the version whose offsets we have. The view disables its
        /// controls and shows <see cref="UnavailableText"/> otherwise.</summary>
        public bool IsAvailable => gameFamily == GameFamilies.HGSS && gameLanguage == GameLanguages.English;
        public string UnavailableText =>
            "Battle Display editing is currently only supported on HeartGold / SoulSilver (English).";

        // The 3 party-icon palettes a mon can use (the icons themselves are edited in the icon NARC).
        public ObservableCollection<string> PartyPalettes { get; } =
            new ObservableCollection<string> { "Palette 0", "Palette 1", "Palette 2" };

        private int _currentId = -1;
        private bool _loading;

        private int _partyPaletteIndex;
        public int PartyPaletteIndex
        {
            get => _partyPaletteIndex;
            set { if (Set(ref _partyPaletteIndex, value)) { if (!_loading) SetDirty(); RefreshPreview(); } }
        }

        // Live preview of the party icon rendered with the CURRENTLY-SELECTED palette (before saving).
        private Bitmap _iconPreview;
        public Bitmap IconPreview { get => _iconPreview; private set => Set(ref _iconPreview, value); }

        private void RefreshPreview()
        {
            if (!IsAvailable || _currentId <= 0) { IconPreview = null; return; }
            try
            {
                var gdi = DSPRE.DSUtils.GetPokePic(_currentId, 64, 64, paletteIdOverride: _partyPaletteIndex);
                IconPreview = gdi != null ? DSPRE.Avalonia.ImageConverter.ToAvaloniaBitmap(gdi) : null;
            }
            catch { IconPreview = null; }
        }

        // ── Battle sprite / shadow coordinates (NARC /a/1/8/0, 89 bytes per mon) ──────────────
        // byte 1 = movement type on send-out; byte 86 = sprite Y (signed); byte 87 = shadow X (signed);
        // byte 88 = shadow size (0 none/1 small/2 medium/3 large).
        private const int OFF_MOVEMENT = 1, OFF_SPRITE_Y = 86, OFF_SHADOW_X = 87, OFF_SHADOW_SIZE = 88, REC_LEN = 89;
        private bool _spriteNarcReady;
        private byte[] _spriteData;     // the current mon's 89-byte record, edited in place, written on Save

        /// <summary>True when this mon has a sprite-coordinate record loaded (enables those fields).</summary>
        private bool _hasSpriteData;
        public bool HasSpriteData { get => _hasSpriteData; private set => Set(ref _hasSpriteData, value); }

        private int _movementType;
        public int MovementType { get => _movementType; set { if (Set(ref _movementType, value) && !_loading && _spriteData != null) { _spriteData[OFF_MOVEMENT] = (byte)value; SetDirty(); } } }

        private int _spriteY;   // signed −128..127 (negative = down, positive = up)
        public int SpriteY { get => _spriteY; set { if (Set(ref _spriteY, value) && !_loading && _spriteData != null) { _spriteData[OFF_SPRITE_Y] = (byte)(sbyte)value; SetDirty(); } } }

        private int _shadowX;   // signed −128..127 (negative = left, positive = right)
        public int ShadowX { get => _shadowX; set { if (Set(ref _shadowX, value) && !_loading && _spriteData != null) { _spriteData[OFF_SHADOW_X] = (byte)(sbyte)value; SetDirty(); } } }

        private int _shadowSize;   // 0 none / 1 small / 2 medium / 3 large
        public int ShadowSize { get => _shadowSize; set { if (Set(ref _shadowSize, value) && !_loading && _spriteData != null) { _spriteData[OFF_SHADOW_SIZE] = (byte)value; SetDirty(); } } }

        public ObservableCollection<string> ShadowSizes { get; } =
            new ObservableCollection<string> { "None", "Small", "Medium", "Large" };

        private string SpriteFilePath(int id) =>
            Path.Combine(gameDirs[DirNames.pokemonSpriteOffsets].unpackedDir, id.ToString("D4"));

        private void LoadSpriteData(int id)
        {
            _spriteData = null;
            HasSpriteData = false;
            if (!IsAvailable || id < 0) return;
            try
            {
                if (!_spriteNarcReady) { DSPRE.DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.pokemonSpriteOffsets }); _spriteNarcReady = true; }
                string path = SpriteFilePath(id);
                if (!File.Exists(path)) return;
                byte[] data = File.ReadAllBytes(path);
                if (data.Length < REC_LEN) return;
                _spriteData = data;
                _movementType = data[OFF_MOVEMENT];
                _spriteY      = (sbyte)data[OFF_SPRITE_Y];
                _shadowX      = (sbyte)data[OFF_SHADOW_X];
                _shadowSize   = data[OFF_SHADOW_SIZE];
                OnPropertyChanged(nameof(MovementType));
                OnPropertyChanged(nameof(SpriteY));
                OnPropertyChanged(nameof(ShadowX));
                OnPropertyChanged(nameof(ShadowSize));
                HasSpriteData = true;
            }
            catch { _spriteData = null; HasSpriteData = false; }
        }

        private void SaveSpriteData()
        {
            if (!IsAvailable || _spriteData == null || _currentId < 0) return;
            string path = SpriteFilePath(_currentId);
            if (!File.Exists(path)) return;
            File.WriteAllBytes(path, _spriteData);   // _spriteData was the full record, patched in the setters
        }

        // ── IEditorWithUnsavedChanges ─────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Battle Display (Mon {_currentId})";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); if (_currentId >= 0) LoadMon(_currentId); }
        private void SetDirty() { if (_loading || _dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        public BattleDisplayEditorViewModel() { }

        public void LoadMon(int id)
        {
            _loading = true;
            _currentId = id;
            try
            {
                int pal = (IsAvailable && id >= 0) ? DSPRE.DSUtils.GetMonIconPaletteId(id) : 0;
                _partyPaletteIndex = (pal >= 0 && pal < PartyPalettes.Count) ? pal : 0;
            }
            catch { _partyPaletteIndex = 0; }
            OnPropertyChanged(nameof(PartyPaletteIndex));
            RefreshPreview();
            LoadSpriteData(id);
            SetClean();
            _loading = false;
        }

        public void Save()
        {
            if (!IsAvailable || _currentId < 0) return;
            try { DSPRE.DSUtils.SetMonIconPaletteId(_currentId, (byte)_partyPaletteIndex); SaveSpriteData(); SetClean(); }
            catch { /* surfaced by the global error net */ }
        }
    }
}
