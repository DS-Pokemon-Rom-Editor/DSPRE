using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
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

        /// <summary>True on the Gen-IV families whose battle-sprite offsets we have (DP, Platinum, HGSS).
        /// The view disables its controls and shows <see cref="UnavailableText"/> otherwise. These NARCs
        /// are language-independent and the party-palette table is version-resolved, so no language gate.</summary>
        public bool IsAvailable => gameFamily == GameFamilies.DP
                                || gameFamily == GameFamilies.Plat
                                || gameFamily == GameFamilies.HGSS;
        public string UnavailableText =>
            "Battle Display editing is supported on Diamond / Pearl, Platinum, and HeartGold / SoulSilver.";

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

        // Display mode: "separate" shows one gender (the GenderIndex pick); "unified" shows Male + Female
        // scenes side by side (the view swaps which block is visible). Genders without a sprite fall back.
        public ObservableCollection<string> Genders { get; } = new ObservableCollection<string> { "Male", "Female" };
        private int _genderIndex;
        public int GenderIndex { get => _genderIndex; set { if (Set(ref _genderIndex, value)) RaiseSprites(); } }
        private bool ShowFemale => _genderIndex == 1;

        private bool _unifiedDisplay;
        public bool UnifiedDisplay { get => _unifiedDisplay; set => Set(ref _unifiedDisplay, value); }

        /// <summary>Highest valid frame index (sheet width/80 − 1) — bounds the Frame field and the preview.</summary>
        public int MaxFrameIndex => System.Math.Max(0, (_sprites?.BattleFrameCount ?? 2) - 1);

        private static Bitmap Pick(System.Collections.Generic.IReadOnlyList<Bitmap> primary,
                                   System.Collections.Generic.IReadOnlyList<Bitmap> fallback, int frame)
        {
            var list = (primary != null && primary.Count > 0) ? primary : fallback;
            if (list == null || list.Count == 0) return null;
            int i = frame < 0 ? 0 : (frame >= list.Count ? list.Count - 1 : frame);
            return list[i];
        }
        private Bitmap Front(bool female)
        {
            var s = _sprites; if (s == null) return null;
            return female ? Pick(s.BattleFrontF, s.BattleFrontM, _frame) : Pick(s.BattleFrontM, s.BattleFrontF, _frame);
        }
        private Bitmap Back(bool female)
        {
            var s = _sprites; if (s == null) return null;
            return female ? Pick(s.BattleBackF, s.BattleBackM, _frame) : Pick(s.BattleBackM, s.BattleBackF, _frame);
        }

        // Gender-selected (separate display) + explicit per-gender (unified side-by-side display).
        public Bitmap EnemySprite  => Front(ShowFemale);
        public Bitmap PlayerSprite => Back(ShowFemale);
        public Bitmap EnemySpriteM => Front(false);
        public Bitmap PlayerSpriteM => Back(false);
        public Bitmap EnemySpriteF => Front(true);
        public Bitmap PlayerSpriteF => Back(true);

        // The sprite IMAGE already bakes in the mon's vertical position, so at LOAD the sprite sits correctly
        // with no extra displacement. To still PREVIEW edits, the heights are applied as a DELTA from the
        // loaded value (×2 per the "half the empty space" research): zero at load, then the sprite tracks the
        // change as you edit. The explicit signed Y offset (HGSS byte / DP poke_yofs) is applied absolutely.
        // Bases 24 / 84 fold in the rest of the scene's constants; calibrate per family if needed.
        private int _oFrontHeightM, _oFrontHeightF, _oBackHeightM, _oBackHeightF;   // values at load
        // When viewing an alternate FORM, the form's height_o values (gender-agnostic) drive the preview instead.
        private bool HeightsActive => _hasHeights || (_formMode && _hasFormHeights);
        private int ActFrontH(bool f) => (_formMode && _hasFormHeights) ? _formFrontH : (f ? _frontHeightF : _frontHeightM);
        private int ActFrontO(bool f) => (_formMode && _hasFormHeights) ? _oFormFrontH : (f ? _oFrontHeightF : _oFrontHeightM);
        private int ActBackH(bool f)  => (_formMode && _hasFormHeights) ? _formBackH : (f ? _backHeightF : _backHeightM);
        private int ActBackO(bool f)  => (_formMode && _hasFormHeights) ? _oFormBackH : (f ? _oBackHeightF : _oBackHeightM);

        // Per the Platinum source (PokeHeightGet → pos_y = appearPos + height), the height is added ×1.
        private double FrontTopFor(int curH, int origH) => 24 - _spriteY + (HeightsActive ? (curH - origH) : 0);
        private double BackTopFor(int curH, int origH)   => HeightsActive ? 84 + (curH - origH) : 84 - _spriteY;

        public double EnemyLeft => 152;
        public double PlayerLeft => 23;
        public double EnemyTop   => FrontTopFor(ActFrontH(ShowFemale), ActFrontO(ShowFemale));
        public double PlayerTop  => BackTopFor(ActBackH(ShowFemale), ActBackO(ShowFemale));
        public double EnemyTopM  => FrontTopFor(ActFrontH(false), ActFrontO(false));
        public double EnemyTopF  => FrontTopFor(ActFrontH(true),  ActFrontO(true));
        public double PlayerTopM => BackTopFor(ActBackH(false), ActBackO(false));
        public double PlayerTopF => BackTopFor(ActBackH(true),  ActBackO(true));

        public bool ShadowSmallVisible  => HasSpriteData && _shadowSize == 1;
        public bool ShadowMediumVisible => HasSpriteData && _shadowSize == 2;
        public bool ShadowLargeVisible  => HasSpriteData && _shadowSize == 3;
        public double ShadowSmallLeft  => 179 + _shadowX;
        public double ShadowMediumLeft => 174 + _shadowX;
        public double ShadowLargeLeft  => 167 + _shadowX;

        private void RaiseLayout()
        {
            OnPropertyChanged(nameof(EnemyTop)); OnPropertyChanged(nameof(PlayerTop));
            OnPropertyChanged(nameof(EnemyTopM)); OnPropertyChanged(nameof(EnemyTopF)); OnPropertyChanged(nameof(PlayerTopM)); OnPropertyChanged(nameof(PlayerTopF));
            OnPropertyChanged(nameof(ShadowSmallVisible)); OnPropertyChanged(nameof(ShadowMediumVisible)); OnPropertyChanged(nameof(ShadowLargeVisible));
            OnPropertyChanged(nameof(ShadowSmallLeft)); OnPropertyChanged(nameof(ShadowMediumLeft)); OnPropertyChanged(nameof(ShadowLargeLeft));
        }
        private void RaiseSprites()
        {
            OnPropertyChanged(nameof(EnemySprite)); OnPropertyChanged(nameof(PlayerSprite));
            OnPropertyChanged(nameof(EnemySpriteM)); OnPropertyChanged(nameof(PlayerSpriteM));
            OnPropertyChanged(nameof(EnemySpriteF)); OnPropertyChanged(nameof(PlayerSpriteF));
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

        // ── Battle sprite / shadow data (family-specific NARC layout) ─────────────────────────
        // Editable: front-sprite Y (signed), shadow X (signed), shadow size; a movement/animation byte
        // (HGSS + Platinum combined record); the per-gender sprite HEIGHTS (DP & Platinum, height.narc — 4
        // unsigned values/mon: back ♀/♂, front ♀/♂); and the raw 28-byte battle-animation record (DP, pokeanm).
        // Storage by family (see battle-sprite-offsets-dp-pt note):
        //   HGSS → one record/mon in pokemonSpriteOffsets (/a/1/8/0, 89 B); last 3 bytes = Y/X/size, byte 1 = movement.
        //   Plat → same combined record in pl_poke_data.narc (movement assumed byte 1 like HGSS) + height.narc.
        //   DP   → poke_yofs / poke_shadow_ofx / poke_shadow (1 B/mon each) + height.narc + pokeanm.narc.
        private IBattleOffsetSource _src;
        private bool _srcTried;

        private void EnsureSource()
        {
            if (_srcTried) return;
            _srcTried = true;
            try
            {
                _src = gameFamily switch
                {
                    GameFamilies.HGSS => new CombinedTailSource(DirNames.pokemonSpriteOffsets, 89, hasMovement: true, movementOffset: 1, withHeights: false),
                    GameFamilies.Plat => new CombinedTailSource(DirNames.pokemonSpriteOffsets, 89, hasMovement: true, movementOffset: 1, withHeights: true),
                    GameFamilies.DP   => new SeparateByteSource(DirNames.pokeYofs, DirNames.pokeShadowOfx, DirNames.pokeShadow),
                    _ => null,
                };
            }
            catch { _src = null; }
        }

        /// <summary>True when this mon has a sprite-coordinate record (enables those fields).</summary>
        private bool _hasSpriteData;
        public bool HasSpriteData { get => _hasSpriteData; private set => Set(ref _hasSpriteData, value); }

        /// <summary>True only where a movement/animation byte exists (HGSS, Platinum) — hides that field on DP.</summary>
        private bool _hasMovementType;
        public bool HasMovementType { get => _hasMovementType; private set => Set(ref _hasMovementType, value); }

        /// <summary>True where per-gender sprite heights exist (DP, Platinum — height.narc).</summary>
        private bool _hasHeights;
        public bool HasHeights { get => _hasHeights; private set { if (Set(ref _hasHeights, value)) OnPropertyChanged(nameof(ShowBaseHeights)); } }

        /// <summary>True where the raw battle-animation record exists (DP — pokeanm.narc).</summary>
        private bool _hasAnimData;
        public bool HasAnimData { get => _hasAnimData; private set => Set(ref _hasAnimData, value); }

        private bool CanEditSprite => _src != null && _hasSpriteData && !_loading;

        private int _movementType;
        public int MovementType { get => _movementType; set { if (Set(ref _movementType, value) && CanEditSprite) SetDirty(); } }

        private int _spriteY;   // signed −128..127 (negative = down, positive = up)
        public int SpriteY { get => _spriteY; set { if (Set(ref _spriteY, value)) { if (CanEditSprite) SetDirty(); RaiseLayout(); } } }

        private int _shadowX;   // signed −128..127 (negative = left, positive = right)
        public int ShadowX { get => _shadowX; set { if (Set(ref _shadowX, value)) { if (CanEditSprite) SetDirty(); RaiseLayout(); } } }

        private int _shadowSize;   // 0 none / 1 small / 2 medium / 3 large
        public int ShadowSize { get => _shadowSize; set { if (Set(ref _shadowSize, value)) { if (CanEditSprite) SetDirty(); RaiseLayout(); } } }

        public ObservableCollection<string> ShadowSizes { get; } =
            new ObservableCollection<string> { "None", "Small", "Medium", "Large" };

        // Per-gender sprite heights (signed). They drive the preview as a delta from the loaded value (see Top math).
        private int _frontHeightM; public int FrontHeightM { get => _frontHeightM; set { if (Set(ref _frontHeightM, value)) { if (CanEditSprite) SetDirty(); OnPropertyChanged(nameof(FrontHeightUnified)); RaiseLayout(); } } }
        private int _frontHeightF; public int FrontHeightF { get => _frontHeightF; set { if (Set(ref _frontHeightF, value)) { if (CanEditSprite) SetDirty(); RaiseLayout(); } } }
        private int _backHeightM;  public int BackHeightM  { get => _backHeightM;  set { if (Set(ref _backHeightM, value)) { if (CanEditSprite) SetDirty(); OnPropertyChanged(nameof(BackHeightUnified)); RaiseLayout(); } } }
        private int _backHeightF;  public int BackHeightF  { get => _backHeightF;  set { if (Set(ref _backHeightF, value)) { if (CanEditSprite) SetDirty(); RaiseLayout(); } } }

        // Modify mode: "unified" exposes one field per axis that writes BOTH genders at once (for the common
        // case where the two genders share a sprite). "separate" exposes the 4 per-gender fields above.
        private bool _unifiedEdit = true;
        public bool UnifiedEdit { get => _unifiedEdit; set => Set(ref _unifiedEdit, value); }
        public int FrontHeightUnified { get => _frontHeightM; set { FrontHeightM = value; FrontHeightF = value; } }
        public int BackHeightUnified  { get => _backHeightM;  set { BackHeightM = value; BackHeightF = value; } }

        // ── Alternate-form sprite heights (height_o.narc; DP/Plat) — follows the Sprite tab's form selector ──
        // height_o: 2 files/form — (formIndex*2) = back (both genders), (formIndex*2 + 1) = front. Signed. The
        // form index mirrors the Sprites tab's global alternate-form list (SelectedFormIndex). [ASSUMPTION: the
        // two lists share an ordering — verify the read values match the form before trusting writes.]
        private OffsetNarc _formHeightNarc;
        private bool _formNarcTried;
        private bool _formMode;       // mirrors SpriteVM.IsAlternateForms
        private int _formIndex = -1;  // mirrors SpriteVM.SelectedFormIndex
        private int _formFrontH, _formBackH, _oFormFrontH, _oFormBackH;

        private bool _hasFormHeights;
        public bool HasFormHeights { get => _hasFormHeights; private set { if (Set(ref _hasFormHeights, value)) OnPropertyChanged(nameof(ShowBaseHeights)); } }
        public bool FormMode => _formMode;
        /// <summary>The base per-gender heights apply to the main sprite, so hide them while viewing a form.</summary>
        public bool ShowBaseHeights => _hasHeights && !_formMode;

        public int FormFrontHeight { get => _formFrontH; set { if (Set(ref _formFrontH, value)) { if (!_loading) SetDirty(); RaiseLayout(); } } }
        public int FormBackHeight  { get => _formBackH;  set { if (Set(ref _formBackH, value)) { if (!_loading) SetDirty(); RaiseLayout(); } } }

        private void EnsureFormNarc()
        {
            if (_formNarcTried) return;
            _formNarcTried = true;
            if (gameFamily == GameFamilies.DP || gameFamily == GameFamilies.Plat)
                _formHeightNarc = new OffsetNarc(DirNames.pokeHeightForms, 1);
        }

        private void LoadFormHeights()
        {
            HasFormHeights = false;
            if (!IsAvailable || !_formMode || _formIndex < 0) return;
            EnsureFormNarc();
            if (_formHeightNarc == null) return;
            try
            {
                var b = _formHeightNarc.GetRecord(_formIndex * 2);
                var f = _formHeightNarc.GetRecord(_formIndex * 2 + 1);
                if (b == null || f == null || b.Length < 1 || f.Length < 1) return;
                _formBackH = (sbyte)b[0]; _formFrontH = (sbyte)f[0];
                _oFormBackH = _formBackH; _oFormFrontH = _formFrontH;
                OnPropertyChanged(nameof(FormFrontHeight)); OnPropertyChanged(nameof(FormBackHeight));
                HasFormHeights = true;
            }
            catch { HasFormHeights = false; }
        }

        private void SaveFormHeights()
        {
            if (_formHeightNarc == null || !_hasFormHeights || _formIndex < 0) return;
            WriteForm(_formIndex * 2, _formBackH);
            WriteForm(_formIndex * 2 + 1, _formFrontH);
        }
        private void WriteForm(int idx, int v) { var r = _formHeightNarc.GetRecord(idx); if (r == null || r.Length < 1) return; r[0] = (byte)v; _formHeightNarc.PutRecord(idx, r); }

        private void OnSpriteFormChanged()
        {
            _formMode = _sprites != null && _sprites.IsAlternateForms;
            _formIndex = _sprites != null ? _sprites.SelectedFormIndex : -1;
            LoadFormHeights();
            OnPropertyChanged(nameof(FormMode));
            OnPropertyChanged(nameof(ShowBaseHeights));
            RaiseLayout();
        }

        // ── Battle-sprite animation (pokeanm.narc; DP/Plat/HGSS) — structured POKE_ANM_DATA (28 bytes/mon) ──
        // Layout (verified from the leaked source): [0] front program-anim #, [1] its wait, [2..7] three back
        // program-anim steps {patno,wait}, [8..27] ten "pattern" steps {s8 patno(frame), u8 wait} — patno=-1
        // (0xFF) terminates. The pattern steps ARE the on-field sprite wiggle, so they drive the mock loop.
        private const int ANIM_REC_LEN = 28, ANIM_PAT_OFFSET = 8, ANIM_PAT_MAX = 10;
        private OffsetNarc _animNarc;
        private bool _animNarcTried;

        private int _animFrontProg; public int AnimFrontProgNum { get => _animFrontProg; set { if (Set(ref _animFrontProg, value)) { if (!_loading) SetDirty(); RefreshProgramScript(); } } }
        private int _animFrontWait; public int AnimFrontWait    { get => _animFrontWait; set { if (Set(ref _animFrontWait, value) && !_loading) SetDirty(); } }

        /// <summary>The three back program-animation steps ({number, wait}).</summary>
        public ObservableCollection<AnimProgStep> AnimBack { get; } = new ObservableCollection<AnimProgStep>();
        /// <summary>The pattern (frame) animation steps — the visible send-out/idle wiggle. Drives the preview.</summary>
        public ObservableCollection<AnimPatternStep> AnimSteps { get; } = new ObservableCollection<AnimPatternStep>();

        public bool CanAddAnimStep => AnimSteps.Count < ANIM_PAT_MAX;

        private void EnsureAnimNarc()
        {
            if (_animNarcTried) return;
            _animNarcTried = true;
            if (IsAvailable) _animNarc = new OffsetNarc(DirNames.pokeAnim, ANIM_REC_LEN);
        }

        private void LoadAnim(int id)
        {
            HasAnimData = false;
            foreach (var s in AnimSteps) s.PropertyChanged -= OnAnimStepChanged;
            foreach (var s in AnimBack) s.PropertyChanged -= OnAnimStepChanged;
            AnimSteps.Clear(); AnimBack.Clear();
            if (!IsAvailable || id < 0) { OnPropertyChanged(nameof(CanAddAnimStep)); return; }
            EnsureAnimNarc();
            var r = _animNarc?.GetRecord(id);
            if (r == null || r.Length < ANIM_REC_LEN) { OnPropertyChanged(nameof(CanAddAnimStep)); return; }
            _animFrontProg = r[0]; _animFrontWait = r[1];
            for (int i = 0; i < 3; i++) AddBackStep(r[2 + i * 2], r[3 + i * 2]);
            for (int i = 0; i < ANIM_PAT_MAX; i++)
            {
                sbyte patno = (sbyte)r[ANIM_PAT_OFFSET + i * 2];
                if (patno < 0) break;   // -1 terminates
                AddPatternStep(patno, r[ANIM_PAT_OFFSET + i * 2 + 1]);
            }
            OnPropertyChanged(nameof(AnimFrontProgNum)); OnPropertyChanged(nameof(AnimFrontWait)); OnPropertyChanged(nameof(CanAddAnimStep));
            HasAnimData = true;
            RefreshProgramScript();
            RestartAnimPreview();
        }

        private void SaveAnim()
        {
            if (_animNarc == null || !_hasAnimData) return;
            var r = _animNarc.GetRecord(_currentId);
            if (r == null || r.Length < ANIM_REC_LEN) return;
            r[0] = (byte)_animFrontProg; r[1] = (byte)_animFrontWait;
            for (int i = 0; i < 3 && i < AnimBack.Count; i++) { r[2 + i * 2] = (byte)AnimBack[i].Number; r[3 + i * 2] = (byte)AnimBack[i].Wait; }
            for (int i = 0; i < ANIM_PAT_MAX; i++)
            {
                if (i < AnimSteps.Count) { r[ANIM_PAT_OFFSET + i * 2] = (byte)(sbyte)AnimSteps[i].Frame; r[ANIM_PAT_OFFSET + i * 2 + 1] = (byte)AnimSteps[i].Wait; }
                else { r[ANIM_PAT_OFFSET + i * 2] = 0xFF; r[ANIM_PAT_OFFSET + i * 2 + 1] = 0; }   // terminator + pad
            }
            _animNarc.PutRecord(_currentId, r);
        }

        private void AddBackStep(int num, int wait) { var s = new AnimProgStep { Number = num, Wait = wait }; s.PropertyChanged += OnAnimStepChanged; AnimBack.Add(s); }
        private void AddPatternStep(int frame, int wait) { var s = new AnimPatternStep { Frame = frame, Wait = wait }; s.PropertyChanged += OnAnimStepChanged; AnimSteps.Add(s); }
        private void OnAnimStepChanged(object _, PropertyChangedEventArgs __) { if (!_loading) { SetDirty(); RestartAnimPreview(); } }

        public void AddAnimStep()
        {
            if (AnimSteps.Count >= ANIM_PAT_MAX) return;
            AddPatternStep(0, 4);
            OnPropertyChanged(nameof(CanAddAnimStep));
            if (!_loading) { SetDirty(); RestartAnimPreview(); }
        }
        public void RemoveAnimStep(AnimPatternStep step)
        {
            if (step == null || !AnimSteps.Contains(step)) return;
            step.PropertyChanged -= OnAnimStepChanged;
            AnimSteps.Remove(step);
            OnPropertyChanged(nameof(CanAddAnimStep));
            if (!_loading) { SetDirty(); RestartAnimPreview(); }
        }

        // ── Program-animation playback (PAST interpreter → live transform on the front sprite) ──────────
        // The front program animation (prg_anm_f) indexes a script in the pokeAnimDefs NARC; PokeAnimPlayer
        // runs it and we push its per-frame transform onto the enemy/front sprite in the preview.
        private OffsetNarc _animDefsNarc;
        private bool _animDefsTried;
        private PokeAnimPlayer _prog, _progBack;
        private bool _progPlaying;

        public bool IsProgramAnimPlaying => _progPlaying;
        public string ProgramAnimButtonText => _progPlaying ? "⏹ Stop" : "▶ Play animation";
        /// <summary>Enabled when this mon has a pokeanm record (→ a front program-animation number to play).</summary>
        public bool CanPlayProgramAnim => _hasAnimData;

        // Live transform pushed to the front sprite's RenderTransform (identity when idle).
        private double _animOffsetX, _animOffsetY, _animScaleX = 1, _animScaleY = 1, _animRotation, _animFadeOpacity;
        public double AnimOffsetX { get => _animOffsetX; private set => Set(ref _animOffsetX, value); }
        public double AnimOffsetY { get => _animOffsetY; private set => Set(ref _animOffsetY, value); }
        public double AnimScaleX  { get => _animScaleX;  private set => Set(ref _animScaleX, value); }
        public double AnimScaleY  { get => _animScaleY;  private set => Set(ref _animScaleY, value); }
        public double AnimRotation { get => _animRotation; private set => Set(ref _animRotation, value); }
        public double AnimFadeOpacity { get => _animFadeOpacity; private set => Set(ref _animFadeOpacity, value); }
        private IBrush _animFadeBrush = Brushes.Transparent;
        public IBrush AnimFadeBrush { get => _animFadeBrush; private set => Set(ref _animFadeBrush, value); }

        // Same, for the player/back sprite (our own mon's entry animation, from prg_anm_b).
        private double _animBackOffsetX, _animBackOffsetY, _animBackScaleX = 1, _animBackScaleY = 1, _animBackRotation, _animBackFadeOpacity;
        public double AnimBackOffsetX { get => _animBackOffsetX; private set => Set(ref _animBackOffsetX, value); }
        public double AnimBackOffsetY { get => _animBackOffsetY; private set => Set(ref _animBackOffsetY, value); }
        public double AnimBackScaleX  { get => _animBackScaleX;  private set => Set(ref _animBackScaleX, value); }
        public double AnimBackScaleY  { get => _animBackScaleY;  private set => Set(ref _animBackScaleY, value); }
        public double AnimBackRotation { get => _animBackRotation; private set => Set(ref _animBackRotation, value); }
        public double AnimBackFadeOpacity { get => _animBackFadeOpacity; private set => Set(ref _animBackFadeOpacity, value); }
        private IBrush _animBackFadeBrush = Brushes.Transparent;
        public IBrush AnimBackFadeBrush { get => _animBackFadeBrush; private set => Set(ref _animBackFadeBrush, value); }

        private void EnsureAnimDefsNarc()
        {
            if (_animDefsTried) return;
            _animDefsTried = true;
            if (IsAvailable) _animDefsNarc = new OffsetNarc(DirNames.pokeAnimDefs, 1);
        }

        /// <summary>Toggles playback of the program animations — front (prg_anm_f) on the enemy sprite and
        /// the own mon's (prg_anm_b[0]) on the player sprite — looping while playing.</summary>
        public void ToggleProgramAnim()
        {
            if (_progPlaying) { StopProgramAnim(); return; }
            EnsureAnimDefsNarc();
            _prog = LoadProgram(_animFrontProg);
            _progBack = AnimBack.Count > 0 ? LoadProgram(AnimBack[0].Number) : null;
            if (_prog == null && _progBack == null) return;
            _progPlaying = true;
            OnPropertyChanged(nameof(IsProgramAnimPlaying)); OnPropertyChanged(nameof(ProgramAnimButtonText));
        }

        private PokeAnimPlayer LoadProgram(int fileIndex)
        {
            var bytes = _animDefsNarc?.GetRecord(fileIndex);
            var script = bytes != null ? PokeAnimScript.Parse(bytes) : null;
            return (script != null && script.Count > 0) ? new PokeAnimPlayer(script) : null;
        }

        // ── Program-animation SCRIPT EDITOR (Phase B): editable PAST command list for the front script ──
        // NOTE: this edits the shared animation script in the pokeanime NARC — it affects every Pokémon that
        // uses this program-animation number, not just the current mon. Saved via its own "Save script" button.
        public ObservableCollection<ProgramCmdRow> ProgramRows { get; } = new ObservableCollection<ProgramCmdRow>();
        public bool HasProgramScript => ProgramRows.Count > 0;
        public string ProgramScriptHeader => $"Front program animation #{_animFrontProg} — script ({ProgramRows.Count} cmds)";
        private bool _scriptDirty;
        public bool ScriptDirty { get => _scriptDirty; private set => Set(ref _scriptDirty, value); }

        private void RefreshProgramScript()
        {
            foreach (var r in ProgramRows) r.PropertyChanged -= OnProgramRowChanged;
            ProgramRows.Clear();
            if (IsAvailable)
            {
                EnsureAnimDefsNarc();
                var bytes = _animDefsNarc?.GetRecord(_animFrontProg);
                var cmds = bytes != null ? PokeAnimScript.Parse(bytes) : null;
                if (cmds != null) foreach (var c in cmds) AddProgramRow(c.Op, c.Args);
            }
            ScriptDirty = false;
            OnPropertyChanged(nameof(HasProgramScript)); OnPropertyChanged(nameof(ProgramScriptHeader));
        }

        private void AddProgramRow(PastOp op, int[] args)
        {
            var row = new ProgramCmdRow { Op = op, ArgsText = string.Join(", ", args) };
            row.PropertyChanged += OnProgramRowChanged;
            ProgramRows.Add(row);
        }
        private void OnProgramRowChanged(object _, PropertyChangedEventArgs __) => ScriptDirty = true;

        public void AddProgramCmd()
        {
            AddProgramRow(PastOp.SetWait, new[] { 1 });
            ScriptDirty = true;
            OnPropertyChanged(nameof(HasProgramScript)); OnPropertyChanged(nameof(ProgramScriptHeader));
        }
        public void RemoveProgramCmd(ProgramCmdRow row)
        {
            if (row == null || !ProgramRows.Contains(row)) return;
            row.PropertyChanged -= OnProgramRowChanged;
            ProgramRows.Remove(row);
            ScriptDirty = true;
            OnPropertyChanged(nameof(HasProgramScript)); OnPropertyChanged(nameof(ProgramScriptHeader));
        }
        public void MoveProgramCmd(ProgramCmdRow row, int dir)
        {
            int i = ProgramRows.IndexOf(row), j = i + dir;
            if (i < 0 || j < 0 || j >= ProgramRows.Count) return;
            ProgramRows.Move(i, j);
            ScriptDirty = true;
        }

        /// <summary>Serializes the edited command list back to the pokeanime NARC file (args padded/truncated to
        /// each opcode's fixed count so the stream stays valid). Repacked into the ROM on the normal save.</summary>
        public void SaveProgramScript()
        {
            if (_animDefsNarc == null) return;
            var cmds = new List<PastCommand>();
            foreach (var row in ProgramRows)
            {
                int n = PokeAnimScript.ArgsFor(row.Op);
                var parsed = ParseIntList(row.ArgsText);
                var args = new int[n];
                for (int i = 0; i < n; i++) args[i] = i < parsed.Count ? parsed[i] : 0;
                cmds.Add(new PastCommand(row.Op, args));
            }
            _animDefsNarc.PutRecord(_animFrontProg, PokeAnimScript.Serialize(cmds));
            StopProgramAnim();
            RefreshProgramScript();   // reflect the canonical (padded) form
        }

        private static List<int> ParseIntList(string s)
        {
            var list = new List<int>();
            if (string.IsNullOrWhiteSpace(s)) return list;
            foreach (var p in s.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = p.Trim();
                bool ok = t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? int.TryParse(t.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out int v)
                    : int.TryParse(t, out v);
                if (ok) list.Add(v);
            }
            return list;
        }

        private void StopProgramAnim()
        {
            _progPlaying = false; _prog = null; _progBack = null;
            AnimOffsetX = AnimOffsetY = 0; AnimScaleX = AnimScaleY = 1; AnimRotation = 0; AnimFadeOpacity = 0;
            AnimBackOffsetX = AnimBackOffsetY = 0; AnimBackScaleX = AnimBackScaleY = 1; AnimBackRotation = 0; AnimBackFadeOpacity = 0;
            OnPropertyChanged(nameof(IsProgramAnimPlaying)); OnPropertyChanged(nameof(ProgramAnimButtonText));
        }

        // Advances both program animations one frame (called from the 60 fps timer). Plays ONCE: when both
        // finish, the sprites settle to rest (END resets the transform) and playback stops.
        private void TickProgramAnim()
        {
            if (!_progPlaying) return;
            if (_prog != null)
            {
                if (!_prog.Finished) _prog.Step();
                AnimOffsetX = _prog.OffsetX; AnimOffsetY = _prog.OffsetY;
                AnimScaleX = _prog.ScaleX; AnimScaleY = _prog.ScaleY; AnimRotation = _prog.RotationDegrees;
                AnimFadeOpacity = _prog.FadeStrength;
                if (_prog.FadeStrength > 0) AnimFadeBrush = new SolidColorBrush(Color.FromRgb(_prog.FadeR, _prog.FadeG, _prog.FadeB));
            }
            if (_progBack != null)
            {
                if (!_progBack.Finished) _progBack.Step();
                AnimBackOffsetX = _progBack.OffsetX; AnimBackOffsetY = _progBack.OffsetY;
                AnimBackScaleX = _progBack.ScaleX; AnimBackScaleY = _progBack.ScaleY; AnimBackRotation = _progBack.RotationDegrees;
                AnimBackFadeOpacity = _progBack.FadeStrength;
                if (_progBack.FadeStrength > 0) AnimBackFadeBrush = new SolidColorBrush(Color.FromRgb(_progBack.FadeR, _progBack.FadeG, _progBack.FadeB));
            }
            bool frontDone = _prog == null || _prog.Finished;
            bool backDone = _progBack == null || _progBack.Finished;
            if (frontDone && backDone) StopProgramAnim();   // one-shot
        }

        private void LoadSpriteData(int id)
        {
            HasSpriteData = false; HasMovementType = false; HasHeights = false;
            if (!IsAvailable || id < 0) return;
            EnsureSource();
            if (_src == null) return;
            try
            {
                if (!_src.TryLoad(id, out BattleRec rec)) return;
                _spriteY = rec.FrontY; _shadowX = rec.ShadowX; _shadowSize = rec.ShadowSize; _movementType = rec.Movement;
                _backHeightF = rec.BackF; _backHeightM = rec.BackM; _frontHeightF = rec.FrontF; _frontHeightM = rec.FrontM;
                _oFrontHeightM = rec.FrontM; _oFrontHeightF = rec.FrontF; _oBackHeightM = rec.BackM; _oBackHeightF = rec.BackF;   // baseline for the delta-preview
                OnPropertyChanged(nameof(SpriteY)); OnPropertyChanged(nameof(ShadowX)); OnPropertyChanged(nameof(ShadowSize)); OnPropertyChanged(nameof(MovementType));
                OnPropertyChanged(nameof(FrontHeightM)); OnPropertyChanged(nameof(FrontHeightF)); OnPropertyChanged(nameof(BackHeightM)); OnPropertyChanged(nameof(BackHeightF));
                OnPropertyChanged(nameof(FrontHeightUnified)); OnPropertyChanged(nameof(BackHeightUnified));
                HasMovementType = rec.HasMovement; HasHeights = rec.HasHeights;
                HasSpriteData = true;
            }
            catch { HasSpriteData = false; }
        }

        private void SaveSpriteData()
        {
            if (!IsAvailable || _src == null || !_hasSpriteData) return;
            var rec = new BattleRec
            {
                FrontY = _spriteY, ShadowX = _shadowX, ShadowSize = _shadowSize, Movement = _movementType, HasMovement = _hasMovementType,
                BackF = _backHeightF, BackM = _backHeightM, FrontF = _frontHeightF, FrontM = _frontHeightM, HasHeights = _hasHeights,
            };
            _src.Save(_currentId, in rec);
        }

        // ── Per-family storage backends ──────────────────────────────────────────────────────
        private struct BattleRec
        {
            public int FrontY, ShadowX, ShadowSize, Movement;
            public bool HasMovement, HasHeights;
            public int BackF, BackM, FrontF, FrontM;   // height.narc, unsigned
        }

        private interface IBattleOffsetSource
        {
            bool TryLoad(int id, out BattleRec rec);
            void Save(int id, in BattleRec rec);
            void Invalidate();
        }

        /// <summary>Reads/writes per-mon records from a NARC that unpacks to either a single blob (record at
        /// id*recLen) or one file per mon (file "NNNN" = the record). Caches in memory; writes back to disk.</summary>
        private sealed class OffsetNarc
        {
            private readonly DirNames _dir;
            private readonly int _recLen;
            private bool _ready, _multi;
            private byte[] _blob;
            private string _path;

            public OffsetNarc(DirNames dir, int recLen) { _dir = dir; _recLen = recLen; }

            public void Invalidate() { _ready = false; _blob = null; }

            private void Ensure()
            {
                if (_ready) return;
                _ready = true;
                DSPRE.DSUtils.TryUnpackNarcs(new List<DirNames> { _dir });
                _path = gameDirs[_dir].unpackedDir;
                var files = Directory.Exists(_path) ? Directory.GetFiles(_path) : System.Array.Empty<string>();
                _multi = files.Length > 1;
                _blob = (!_multi && files.Length == 1) ? File.ReadAllBytes(files[0]) : null;
            }

            private string FilePath(int id) => Path.Combine(_path, id.ToString("D4"));

            public byte[] GetRecord(int id)
            {
                Ensure();
                if (_multi)
                {
                    string f = FilePath(id);
                    return File.Exists(f) ? File.ReadAllBytes(f) : null;
                }
                if (_blob == null) return null;
                int off = id * _recLen;
                if (off < 0 || off + _recLen > _blob.Length) return null;
                var r = new byte[_recLen];
                System.Array.Copy(_blob, off, r, 0, _recLen);
                return r;
            }

            public void PutRecord(int id, byte[] rec)
            {
                Ensure();
                if (_multi) { File.WriteAllBytes(FilePath(id), rec); return; }
                if (_blob == null) return;
                int off = id * _recLen;
                if (off < 0 || off + rec.Length > _blob.Length) return;
                System.Array.Copy(rec, 0, _blob, off, rec.Length);
                File.WriteAllBytes(FilePath(0), _blob);   // single-file blob is "0000"
            }
        }

        /// <summary>height.narc (DP + Platinum): 4 unsigned 1-byte values per mon — file order F-back, M-back,
        /// F-front, M-front, so mon N's slot s is the (N*4 + s)th file (or byte, if it unpacks to one blob).</summary>
        private sealed class HeightNarc
        {
            private const int FB = 0, MB = 1, FF = 2, MF = 3;
            private readonly OffsetNarc _n = new OffsetNarc(DirNames.pokeHeight, 1);
            public void Invalidate() => _n.Invalidate();

            public bool TryLoad(int id, out int backF, out int backM, out int frontF, out int frontM)
            {
                backF = backM = frontF = frontM = 0;
                var a = _n.GetRecord(id * 4 + FB); var b = _n.GetRecord(id * 4 + MB);
                var c = _n.GetRecord(id * 4 + FF); var d = _n.GetRecord(id * 4 + MF);
                if (a == null || b == null || c == null || d == null || a.Length < 1 || b.Length < 1 || c.Length < 1 || d.Length < 1) return false;
                // Signed: in practice these read as signed offsets (e.g. 0xE0 = −32), so reading them unsigned
                // pushed sprites far off-screen by an amount that scaled per-mon.
                backF = (sbyte)a[0]; backM = (sbyte)b[0]; frontF = (sbyte)c[0]; frontM = (sbyte)d[0];
                return true;
            }

            public void Save(int id, in BattleRec rec)
            {
                Put(id * 4 + FB, rec.BackF); Put(id * 4 + MB, rec.BackM); Put(id * 4 + FF, rec.FrontF); Put(id * 4 + MF, rec.FrontM);
            }
            private void Put(int idx, int v) { var r = _n.GetRecord(idx); if (r == null || r.Length < 1) return; r[0] = (byte)v; _n.PutRecord(idx, r); }
        }

        /// <summary>HGSS / Platinum: one combined record per mon; the 3 fields are the LAST 3 bytes (size last),
        /// an optional movement byte at a fixed offset, and (Platinum) the per-gender heights from height.narc.</summary>
        private sealed class CombinedTailSource : IBattleOffsetSource
        {
            private readonly OffsetNarc _narc;
            private readonly bool _hasMovement;
            private readonly int _movementOffset;
            private readonly HeightNarc _heights;   // null when withHeights:false (HGSS)
            public CombinedTailSource(DirNames dir, int recLen, bool hasMovement, int movementOffset, bool withHeights)
            { _narc = new OffsetNarc(dir, recLen); _hasMovement = hasMovement; _movementOffset = movementOffset; _heights = withHeights ? new HeightNarc() : null; }

            public void Invalidate() { _narc.Invalidate(); _heights?.Invalidate(); }

            public bool TryLoad(int id, out BattleRec rec)
            {
                rec = default;
                var r = _narc.GetRecord(id);
                if (r == null || r.Length < 3) return false;
                int n = r.Length;
                rec.FrontY = (sbyte)r[n - 3]; rec.ShadowX = (sbyte)r[n - 2]; rec.ShadowSize = r[n - 1];
                if (_hasMovement && _movementOffset >= 0 && _movementOffset < n) { rec.Movement = r[_movementOffset]; rec.HasMovement = true; }
                if (_heights != null && _heights.TryLoad(id, out int bf, out int bm, out int ff, out int fm))
                { rec.BackF = bf; rec.BackM = bm; rec.FrontF = ff; rec.FrontM = fm; rec.HasHeights = true; }
                return true;
            }

            public void Save(int id, in BattleRec rec)
            {
                var r = _narc.GetRecord(id);
                if (r == null || r.Length < 3) return;
                int n = r.Length;
                r[n - 3] = (byte)(sbyte)rec.FrontY; r[n - 2] = (byte)(sbyte)rec.ShadowX; r[n - 1] = (byte)rec.ShadowSize;
                if (rec.HasMovement && _movementOffset >= 0 && _movementOffset < n) r[_movementOffset] = (byte)rec.Movement;
                _narc.PutRecord(id, r);
                if (_heights != null && rec.HasHeights) _heights.Save(id, in rec);
            }
        }

        /// <summary>Diamond / Pearl: front Y, shadow X and shadow size each live in their own single-byte-per-mon
        /// NARC, plus per-gender heights (height.narc). (pokeanm is handled separately, like form heights.)</summary>
        private sealed class SeparateByteSource : IBattleOffsetSource
        {
            private readonly OffsetNarc _y, _sx, _sz;
            private readonly HeightNarc _heights = new HeightNarc();
            public SeparateByteSource(DirNames yDir, DirNames sxDir, DirNames szDir)
            { _y = new OffsetNarc(yDir, 1); _sx = new OffsetNarc(sxDir, 1); _sz = new OffsetNarc(szDir, 1); }

            public void Invalidate() { _y.Invalidate(); _sx.Invalidate(); _sz.Invalidate(); _heights.Invalidate(); }

            public bool TryLoad(int id, out BattleRec rec)
            {
                rec = default;
                var ry = _y.GetRecord(id); var rx = _sx.GetRecord(id); var rz = _sz.GetRecord(id);
                if (ry == null || rx == null || rz == null || ry.Length < 1 || rx.Length < 1 || rz.Length < 1) return false;
                rec.FrontY = (sbyte)ry[0]; rec.ShadowX = (sbyte)rx[0]; rec.ShadowSize = rz[0];
                if (_heights.TryLoad(id, out int bf, out int bm, out int ff, out int fm))
                { rec.BackF = bf; rec.BackM = bm; rec.FrontF = ff; rec.FrontM = fm; rec.HasHeights = true; }
                return true;
            }

            public void Save(int id, in BattleRec rec)
            {
                WriteByte(_y, id, (byte)(sbyte)rec.FrontY);
                WriteByte(_sx, id, (byte)(sbyte)rec.ShadowX);
                WriteByte(_sz, id, (byte)rec.ShadowSize);
                if (rec.HasHeights) _heights.Save(id, in rec);
            }
            private static void WriteByte(OffsetNarc narc, int id, byte v)
            { var r = narc.GetRecord(id); if (r == null || r.Length < 1) return; r[0] = v; narc.PutRecord(id, r); }
        }

        // ── IEditorWithUnsavedChanges ─────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Battle Display (Mon {_currentId})";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); _src?.Invalidate(); _formHeightNarc?.Invalidate(); _animNarc?.Invalidate(); if (_currentId >= 0) LoadMon(_currentId); }   // drop in-memory edits → reload from disk
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
                    if (e.PropertyName != null && e.PropertyName.StartsWith("Battle", StringComparison.Ordinal))
                    {
                        if (e.PropertyName == nameof(PokemonSpriteEditorViewModel.BattleFrameCount)) OnPropertyChanged(nameof(MaxFrameIndex));
                        RaiseSprites();
                    }
                    else if (e.PropertyName is nameof(PokemonSpriteEditorViewModel.IsAlternateForms)
                             or nameof(PokemonSpriteEditorViewModel.SelectedFormIndex))
                        OnSpriteFormChanged();
                };

            // Drive the preview from the pokeanm pattern steps (≈60 fps; wait values are frames). When a mon has
            // no pattern data, fall back to a simple two-frame toggle so the preview still animates.
            _animTimer = new global::Avalonia.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000.0 / 60)
            };
            _animTimer.Tick += (_, _) => AnimTick();
            _animTimer.Start();
        }

        private int _patIndex = -1, _patCountdown;
        private void RestartAnimPreview() { _patIndex = -1; _patCountdown = 0; }
        private bool _framePaused;
        public bool FramePaused { get => _framePaused; set => Set(ref _framePaused, value); }

        private void AnimTick()
        {
            TickProgramAnim();   // program-animation motion (independent of the frame/pattern loop)
            if (_framePaused) return;   // frame (pattern) loop paused for inspection
            if (AnimSteps.Count == 0)
            {
                if (_frame != 0) { _frame = 0; RaiseSprites(); }   // no pattern data → static first frame
                return;
            }
            if (--_patCountdown > 0) return;
            _patIndex = (_patIndex + 1) % AnimSteps.Count;
            var step = AnimSteps[_patIndex];
            _patCountdown = Math.Max(1, step.Wait);
            int max = MaxFrameIndex;
            int newFrame = step.Frame < 0 ? 0 : (step.Frame > max ? max : step.Frame);
            if (newFrame != _frame) { _frame = newFrame; RaiseSprites(); }
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
            _formMode = _sprites != null && _sprites.IsAlternateForms;
            _formIndex = _sprites != null ? _sprites.SelectedFormIndex : -1;
            LoadFormHeights();
            StopProgramAnim();   // reset any in-flight program animation before switching mon
            LoadAnim(id);
            OnPropertyChanged(nameof(FormMode)); OnPropertyChanged(nameof(ShowBaseHeights)); OnPropertyChanged(nameof(CanPlayProgramAnim));
            RaiseLayout();
            RaiseSprites();
            SetClean();
            _loading = false;
        }

        public void Save()
        {
            if (!IsAvailable || _currentId < 0) return;
            try { DSPRE.DSUtils.SetMonIconPaletteId(_currentId, (byte)_partyPaletteIndex); SaveSpriteData(); SaveFormHeights(); SaveAnim(); SetClean(); }
            catch { /* surfaced by the global error net */ }
        }
    }

    /// <summary>One pattern-animation step (pokeanm ssanm): which sprite frame to show and for how many
    /// 60 fps frames. <see cref="Frame"/> is the cell/pattern number (the sheet has frames 0 and 1).</summary>
    public sealed class AnimPatternStep : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private int _frame; public int Frame { get => _frame; set { if (_frame != value) { _frame = value; Raise(nameof(Frame)); } } }
        private int _wait = 1; public int Wait { get => _wait; set { if (_wait != value) { _wait = value; Raise(nameof(Wait)); } } }
    }

    /// <summary>One back program-animation step (pokeanm prg_anm_b): a program-animation number + wait.</summary>
    public sealed class AnimProgStep : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private int _number; public int Number { get => _number; set { if (_number != value) { _number = value; Raise(nameof(Number)); } } }
        private int _wait; public int Wait { get => _wait; set { if (_wait != value) { _wait = value; Raise(nameof(Wait)); } } }
    }

    /// <summary>One editable row of a PAST program-animation script: an opcode + its argument words (edited as
    /// a comma/space-separated list; padded/truncated to the opcode's fixed arg count on save).</summary>
    public sealed class ProgramCmdRow : INotifyPropertyChanged
    {
        private static readonly DSPRE.Avalonia.Data.PastOp[] _ops =
            (DSPRE.Avalonia.Data.PastOp[])System.Enum.GetValues(typeof(DSPRE.Avalonia.Data.PastOp));
        public System.Collections.Generic.IReadOnlyList<DSPRE.Avalonia.Data.PastOp> Ops => _ops;

        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private DSPRE.Avalonia.Data.PastOp _op;
        public DSPRE.Avalonia.Data.PastOp Op { get => _op; set { if (_op != value) { _op = value; Raise(nameof(Op)); Raise(nameof(ArgHint)); } } }
        private string _argsText = "";
        public string ArgsText { get => _argsText; set { if (_argsText != value) { _argsText = value; Raise(nameof(ArgsText)); } } }
        public string ArgHint => $"{DSPRE.Avalonia.Data.PokeAnimScript.ArgsFor(Op)} arg(s)";
    }
}
