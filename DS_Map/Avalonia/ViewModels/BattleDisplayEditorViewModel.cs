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

        // ── Battle mock (shows the mon vs itself: enemy = front sprite, player = back sprite) ──────
        // Layout/coords mirror PokEditor's battle scene (256×192): front sprite at (152, 10 − spriteY),
        // back sprite at (23, 72), enemy shadow at size-specific X (179/174/167 + shadowX), Y 83/83/82.
        private readonly PokemonSpriteEditorViewModel _sprites;

        // The send-out sheet is two 80×80 frames; we loop them so the preview animates like the game.
        private int _frame;   // 0 or 1
        private readonly global::Avalonia.Threading.DispatcherTimer _animTimer;

        public Bitmap EnemySprite  => _frame == 0 ? _sprites?.BattleFront0 : _sprites?.BattleFront1;
        public Bitmap PlayerSprite => _frame == 0 ? _sprites?.BattleBack0  : _sprites?.BattleBack1;

        // PokEditor draws front at (152, 10 − globalFrontY − frontModifier) and back at (23, 72 − backModifier),
        // i.e. BOTH sprites take a per-mon Y offset. HGSS exposes one signed sprite-Y byte (86), so we apply it
        // to both. The 24 / 84 bases fold in PokEditor's global/per-mon constants we don't read here.
        public double EnemyLeft => 152;
        public double EnemyTop  => 24 - _spriteY;     // byte 86: + moves the sprite up (top decreases)
        public double PlayerLeft => 23;
        public double PlayerTop  => 84 - _spriteY;

        public bool ShadowSmallVisible  => HasSpriteData && _shadowSize == 1;
        public bool ShadowMediumVisible => HasSpriteData && _shadowSize == 2;
        public bool ShadowLargeVisible  => HasSpriteData && _shadowSize == 3;
        public double ShadowSmallLeft  => 179 + _shadowX;
        public double ShadowMediumLeft => 174 + _shadowX;
        public double ShadowLargeLeft  => 167 + _shadowX;

        private void RaiseLayout()
        {
            OnPropertyChanged(nameof(EnemyTop)); OnPropertyChanged(nameof(PlayerTop));
            OnPropertyChanged(nameof(ShadowSmallVisible)); OnPropertyChanged(nameof(ShadowMediumVisible)); OnPropertyChanged(nameof(ShadowLargeVisible));
            OnPropertyChanged(nameof(ShadowSmallLeft)); OnPropertyChanged(nameof(ShadowMediumLeft)); OnPropertyChanged(nameof(ShadowLargeLeft));
        }
        private void RaiseSprites()
        {
            OnPropertyChanged(nameof(EnemySprite)); OnPropertyChanged(nameof(PlayerSprite));
        }

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

        // ── Battle sprite / shadow coordinates (NARC /a/1/8/0) ────────────────────────────────
        // /a/1/8/0 is a NARC holding ONE file; within it each Pokémon occupies 89 bytes, so mon N's
        // record starts at N*89. byte 1 = movement type on send-out; byte 86 = sprite Y (signed);
        // byte 87 = shadow X (signed); byte 88 = shadow size (0 none/1 small/2 medium/3 large).
        private const int OFF_MOVEMENT = 1, OFF_SPRITE_Y = 86, OFF_SHADOW_X = 87, OFF_SHADOW_SIZE = 88, REC_LEN = 89;
        private bool _spriteNarcReady;
        private byte[] _spriteBlob;     // the whole single file; the current mon's record is at _recOffset
        private int _recOffset = -1;    // = currentId * REC_LEN when this mon has a record, else -1

        /// <summary>True when this mon has a sprite-coordinate record (enables those fields).</summary>
        private bool _hasSpriteData;
        public bool HasSpriteData { get => _hasSpriteData; private set => Set(ref _hasSpriteData, value); }

        private bool CanEditSprite => _spriteBlob != null && _recOffset >= 0 && !_loading;

        private int _movementType;
        public int MovementType { get => _movementType; set { if (Set(ref _movementType, value) && CanEditSprite) { _spriteBlob[_recOffset + OFF_MOVEMENT] = (byte)value; SetDirty(); } } }

        private int _spriteY;   // signed −128..127 (negative = down, positive = up)
        public int SpriteY { get => _spriteY; set { if (Set(ref _spriteY, value)) { if (CanEditSprite) { _spriteBlob[_recOffset + OFF_SPRITE_Y] = (byte)(sbyte)value; SetDirty(); } RaiseLayout(); } } }

        private int _shadowX;   // signed −128..127 (negative = left, positive = right)
        public int ShadowX { get => _shadowX; set { if (Set(ref _shadowX, value)) { if (CanEditSprite) { _spriteBlob[_recOffset + OFF_SHADOW_X] = (byte)(sbyte)value; SetDirty(); } RaiseLayout(); } } }

        private int _shadowSize;   // 0 none / 1 small / 2 medium / 3 large
        public int ShadowSize { get => _shadowSize; set { if (Set(ref _shadowSize, value)) { if (CanEditSprite) { _spriteBlob[_recOffset + OFF_SHADOW_SIZE] = (byte)value; SetDirty(); } RaiseLayout(); } } }

        public ObservableCollection<string> ShadowSizes { get; } =
            new ObservableCollection<string> { "None", "Small", "Medium", "Large" };

        // The NARC unpacks to a single file (index 0 → "0000").
        private string SpriteBlobPath => Path.Combine(gameDirs[DirNames.pokemonSpriteOffsets].unpackedDir, "0000");

        private void EnsureBlobLoaded()
        {
            if (_spriteBlob != null) return;
            if (!_spriteNarcReady) { DSPRE.DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.pokemonSpriteOffsets }); _spriteNarcReady = true; }
            if (File.Exists(SpriteBlobPath)) _spriteBlob = File.ReadAllBytes(SpriteBlobPath);
        }

        private void LoadSpriteData(int id)
        {
            _recOffset = -1;
            HasSpriteData = false;
            if (!IsAvailable || id < 0) return;
            try
            {
                EnsureBlobLoaded();
                if (_spriteBlob == null) return;
                int off = id * REC_LEN;
                if (off + REC_LEN > _spriteBlob.Length) return;   // mon beyond the table
                _recOffset = off;
                _movementType = _spriteBlob[off + OFF_MOVEMENT];
                _spriteY      = (sbyte)_spriteBlob[off + OFF_SPRITE_Y];
                _shadowX      = (sbyte)_spriteBlob[off + OFF_SHADOW_X];
                _shadowSize   = _spriteBlob[off + OFF_SHADOW_SIZE];
                OnPropertyChanged(nameof(MovementType));
                OnPropertyChanged(nameof(SpriteY));
                OnPropertyChanged(nameof(ShadowX));
                OnPropertyChanged(nameof(ShadowSize));
                HasSpriteData = true;
            }
            catch { _recOffset = -1; HasSpriteData = false; }
        }

        private void SaveSpriteData()
        {
            if (!IsAvailable || _spriteBlob == null) return;
            if (File.Exists(SpriteBlobPath)) File.WriteAllBytes(SpriteBlobPath, _spriteBlob);
        }

        // ── IEditorWithUnsavedChanges ─────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Battle Display (Mon {_currentId})";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); _spriteBlob = null; if (_currentId >= 0) LoadMon(_currentId); }   // drop in-memory edits → reload from disk
        private void SetDirty() { if (_loading || _dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        public BattleDisplayEditorViewModel() { }

        /// <summary>Runtime ctor: takes the sibling Sprite VM so the battle mock can show this mon's
        /// front (enemy) and back (player) sprites, refreshing when they re-render.</summary>
        public BattleDisplayEditorViewModel(PokemonSpriteEditorViewModel sprites)
        {
            _sprites = sprites;
            if (_sprites != null)
                _sprites.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName is nameof(PokemonSpriteEditorViewModel.BattleFront0)
                        or nameof(PokemonSpriteEditorViewModel.BattleFront1)
                        or nameof(PokemonSpriteEditorViewModel.BattleBack0)
                        or nameof(PokemonSpriteEditorViewModel.BattleBack1))
                        RaiseSprites();
                };

            // Loop the two send-out frames so the preview animates like the game does on entry.
            _animTimer = new global::Avalonia.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            _animTimer.Tick += (_, _) => { _frame ^= 1; RaiseSprites(); };
            _animTimer.Start();
        }

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
            RaiseLayout();
            RaiseSprites();
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
