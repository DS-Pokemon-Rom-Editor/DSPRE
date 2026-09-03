using Avalonia.Media;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using DSPRE.HgEngine;
using NSMBe4.DSFileSystem;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using static DSPRE.RomInfo;
using static MKDS_Course_Editor.NSBTP.NSBTP.NSBTP_File;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    /// <summary>
    /// "Battle Display" tab of the Pokémon editor: per-species presentation tweaks that live outside the
    /// personal/sprite data, including the party-icon palette (which of the 3 icon palettes a mon's party
    /// icon uses, 1 byte per species in the ARM9 icon-palette table) and battle-sprite coordinates
    /// (/a/1/8/0). Supported on Diamond/Pearl, Platinum, and HeartGold/SoulSilver; see <see cref="IsAvailable"/>.
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

        // ── hg-engine form selection (SpriteOffsets.c/HeightTable.c only) ───────────────────────
        // hg-engine forms are first-class species IDs with their own SpriteOffsets.c entry; vanilla
        // NARC-backed data (graphics, height.narc) has no such concept, so this picker only re-routes
        // the hg-engine-source reads/writes below. Graphics stay keyed to the base species (_currentId).
        public sealed class FormOption { public string Label { get; set; } public int SpeciesId { get; set; } }
        public ObservableCollection<FormOption> FormOptions { get; } = new ObservableCollection<FormOption>();
        public bool HasForms => FormOptions.Count > 1;

        private int _currentHgeSpeciesId = -1;
        private int _selectedFormIndex;
        public int SelectedFormIndex
        {
            get => _selectedFormIndex;
            set
            {
                if (_selectedFormIndex == value) return;
                _selectedFormIndex = value;
                OnPropertyChanged();
                _currentHgeSpeciesId = (value >= 0 && value < FormOptions.Count) ? FormOptions[value].SpeciesId : _currentId;
                if (!_loading) LoadHgeFormData();
            }
        }

        private HgEngineSymbolTable _hgeSpecies;
        private Dictionary<string, List<HgEngineFormRegistry.FormSlot>> _hgeFormTable;
        private bool _hgeFormLookupTried;

        private void EnsureHgeFormLookup()
        {
            if (_hgeFormLookupTried) return;
            _hgeFormLookupTried = true;
            try
            {
                _hgeSpecies = HgEngineSymbolTable.Load("include/constants/species.h");
                _hgeFormTable = HgEngineFormRegistry.LoadAll();
            }
            catch { _hgeSpecies = null; _hgeFormTable = null; }
        }

        private void LoadFormOptions(int baseId)
        {
            FormOptions.Clear();
            _currentHgeSpeciesId = baseId;
            if (HgEngineProject.IsActive && baseId >= 0)
            {
                EnsureHgeFormLookup();
                FormOptions.Add(new FormOption { Label = "(base form)", SpeciesId = baseId });
                if (_hgeSpecies != null && _hgeFormTable != null
                    && _hgeSpecies.TryGetNameWithPrefix(baseId, "SPECIES_", out string baseDesignator)
                    && _hgeFormTable.TryGetValue(baseDesignator, out var slots))
                {
                    foreach (var slot in slots)
                        if (_hgeSpecies.TryGetValue(slot.SpeciesSymbol, out int formId))
                            FormOptions.Add(new FormOption { Label = FormDisplayName(slot.SpeciesSymbol), SpeciesId = formId });
                }
            }
            _selectedFormIndex = 0;
            OnPropertyChanged(nameof(SelectedFormIndex));
            OnPropertyChanged(nameof(HasForms));
        }

        private static string FormDisplayName(string designator)
        {
            string s = designator.StartsWith("SPECIES_", StringComparison.Ordinal) ? designator.Substring(8) : designator;
            return s.Replace('_', ' ');
        }

        // Re-reads just the hg-engine-source-backed data (Positioning/Frames/Animation) for the newly
        // selected form; graphics/icon/NARC-backed data are unaffected, since they stay keyed to the base species.
        private void LoadHgeFormData()
        {
            if (!HgEngineProject.IsActive) return;
            _loading = true;
            try
            {
                LoadSpriteDataFromHgeSource(_currentHgeSpeciesId);
                LoadAnimFromHgeSource(_currentHgeSpeciesId);
            }
            finally { _loading = false; }
            RaiseLayout();
            RaiseSprites();
        }

        // ── Arena type (real battle-scene backdrop + terrain platforms, preview-only) ─────────────
        // One dropdown picks a GROUND_ID terrain (Gravel/Sand/Lawn/.../Floor); its matching backdrop is
        // auto-paired (BattleGroundRenderer.BackdropForTerrain). Same renderers the Battle Script Editor
        // already uses for its (separate, more granular) Background/Terrain selectors; see
        // DS_Map/Avalonia/Data/BattleGroundRenderer.cs + BattleBgRenderer.cs. Falls back to the bundled
        // placeholder art (HasArenaGraphics=false) if the ROM/NARC is unavailable or decoding fails, so
        // the scene always has a floor. Not saved anywhere, purely how the preview looks.
        private BattleGroundRenderer _groundRenderer;
        private BattleBgRenderer _bgRenderer;

        public IReadOnlyList<string> ArenaTypeNames { get; } = BattleGroundRenderer.TerrainNames;

        private int _arenaTypeIndex = 2;   // "Lawn": a reasonably common default
        public int ArenaTypeIndex
        {
            get => _arenaTypeIndex;
            set { if (Set(ref _arenaTypeIndex, value)) ApplyArena(); }
        }

        private Bitmap _arenaBackdrop, _arenaGroundMine, _arenaGroundEnemy;
        public Bitmap ArenaBackdrop     { get => _arenaBackdrop;     private set => Set(ref _arenaBackdrop, value); }
        public Bitmap ArenaGroundMine   { get => _arenaGroundMine;   private set => Set(ref _arenaGroundMine, value); }
        public Bitmap ArenaGroundEnemy  { get => _arenaGroundEnemy;  private set => Set(ref _arenaGroundEnemy, value); }

        private double _arenaGroundMineLeft, _arenaGroundMineTop, _arenaGroundEnemyLeft, _arenaGroundEnemyTop;
        public double ArenaGroundMineLeft   { get => _arenaGroundMineLeft;   private set => Set(ref _arenaGroundMineLeft, value); }
        public double ArenaGroundMineTop    { get => _arenaGroundMineTop;    private set => Set(ref _arenaGroundMineTop, value); }
        public double ArenaGroundEnemyLeft  { get => _arenaGroundEnemyLeft;  private set => Set(ref _arenaGroundEnemyLeft, value); }
        public double ArenaGroundEnemyTop   { get => _arenaGroundEnemyTop;   private set => Set(ref _arenaGroundEnemyTop, value); }

        private bool _hasArenaGraphics;
        public bool HasArenaGraphics { get => _hasArenaGraphics; private set => Set(ref _hasArenaGraphics, value); }

        private void ApplyArena()
        {
            try
            {
                var ground = _groundRenderer ??= new BattleGroundRenderer();
                var (mine, enemy) = ground.Build(_arenaTypeIndex);
                int bg = BattleGroundRenderer.BackdropForTerrain(_arenaTypeIndex);
                var backdropImg = bg >= 0 ? (_bgRenderer ??= new BattleBgRenderer()).BuildBackdrop(bg) : null;

                bool ok = mine?.Rgba != null && enemy?.Rgba != null && backdropImg?.Rgba != null;
                if (ok)
                {
                    ArenaGroundMine = RgbaToBitmap(mine.Rgba, mine.Width, mine.Height);
                    ArenaGroundMineLeft = mine.Left; ArenaGroundMineTop = mine.Top;
                    ArenaGroundEnemy = RgbaToBitmap(enemy.Rgba, enemy.Width, enemy.Height);
                    ArenaGroundEnemyLeft = enemy.Left; ArenaGroundEnemyTop = enemy.Top;
                    // The shared backdrop tilemap can be taller than the 256×192 scene (some are a stacked
                    // multi-band scrolling texture), so crop to the top-left 256×192 instead of stretching the
                    // whole thing to fit, or a taller source visibly squishes into repeated horizontal bands
                    // (mirrors BattleScriptEditorViewModel.BgToBackdrop, same underlying data).
                    ArenaBackdrop = RgbaToBitmap(CropBackdropRgba(backdropImg.Rgba, backdropImg.Width, backdropImg.Height), 256, 192);
                }
                HasArenaGraphics = ok;
            }
            catch { HasArenaGraphics = false; }
            RenderGauges();
        }

        // Real HP-gauge frames out of the ROM, so a hack's own edited gauge graphic shows here. Same
        // decode as BattleScriptEditorViewModel.RenderGauges; the placeholder art covers a failed decode.
        private Bitmap _gaugePlayerImage, _gaugeEnemyImage;
        public Bitmap GaugePlayerImage { get => _gaugePlayerImage; private set => Set(ref _gaugePlayerImage, value); }
        public Bitmap GaugeEnemyImage { get => _gaugeEnemyImage; private set => Set(ref _gaugeEnemyImage, value); }
        public bool HasRealGauges => _gaugePlayerImage != null || _gaugeEnemyImage != null;
        public bool PlaceholderGaugesVisible => !HasRealGauges;

        // HGSS gauges are cream frames with dark text; DPPt frames are dark with white text.
        public IBrush GaugeTextBrush => gameFamily == GameFamilies.HGSS
            ? new SolidColorBrush(global::Avalonia.Media.Color.FromRgb(0x50, 0x50, 0x50))
            : new SolidColorBrush(global::Avalonia.Media.Color.FromRgb(0xF8, 0xF8, 0xF8));

        private static string[] SafeSpeciesNames() { try { return GetPokemonNames(); } catch { return Array.Empty<string>(); } }
        public string GaugeNameText
        {
            get
            {
                var names = SafeSpeciesNames();
                return (_currentId >= 0 && _currentId < names.Length) ? names[_currentId] : string.Empty;
            }
        }
        public string GaugeLevelText => "Lv5";

        private void RenderGauges()
        {
            try
            {
                var r = _groundRenderer ??= new BattleGroundRenderer();
                GaugePlayerImage = GaugeToBitmap(r.BuildGauge(true));
                GaugeEnemyImage = GaugeToBitmap(r.BuildGauge(false));
            }
            catch { GaugePlayerImage = GaugeEnemyImage = null; }
            OnPropertyChanged(nameof(HasRealGauges));
            OnPropertyChanged(nameof(PlaceholderGaugesVisible));
            OnPropertyChanged(nameof(GaugeTextBrush));
        }

        // GroundImage (256² straight RGBA) -> unpremultiplied BGRA; the frame has transparency.
        private static Bitmap GaugeToBitmap(BattleGroundRenderer.GroundImage g)
        {
            if (g?.Rgba == null) return null;
            return RgbaToBitmap(g.Rgba, g.Width, g.Height);
        }

        // RGBA w×h (battle BG, usually 256×256 or taller) -> exactly 256×192 (top-left crop, black-padded
        // if the source is smaller). Mirrors BattleScriptEditorViewModel.BgToBackdrop.
        private static byte[] CropBackdropRgba(byte[] rgba, int w, int h)
        {
            var outp = new byte[256 * 192 * 4];
            for (int y = 0; y < 192 && y < h; y++)
                for (int x = 0; x < 256 && x < w; x++)
                {
                    int si = (y * w + x) * 4, di = (y * 256 + x) * 4;
                    outp[di] = rgba[si]; outp[di + 1] = rgba[si + 1]; outp[di + 2] = rgba[si + 2]; outp[di + 3] = 255;
                }
            return outp;
        }

        // Straight RGBA byte[] -> an unpremultiplied BGRA Avalonia bitmap (mirrors
        // BattleScriptEditorViewModel.GaugeToBitmap: same conversion, different scene).
        private static Bitmap RgbaToBitmap(byte[] rgba, int w, int h)
        {
            if (rgba == null || w <= 0 || h <= 0) return null;
            var wb = new WriteableBitmap(new global::Avalonia.PixelSize(w, h), new global::Avalonia.Vector(96, 96),
                                         global::Avalonia.Platform.PixelFormat.Bgra8888, global::Avalonia.Platform.AlphaFormat.Unpremul);
            var bgra = new byte[w * h * 4];
            for (int i = 0; i < w * h * 4; i += 4) { bgra[i] = rgba[i + 2]; bgra[i + 1] = rgba[i + 1]; bgra[i + 2] = rgba[i]; bgra[i + 3] = rgba[i + 3]; }
            using (var fb = wb.Lock())
            {
                int rb = fb.RowBytes;
                if (rb == w * 4) System.Runtime.InteropServices.Marshal.Copy(bgra, 0, fb.Address, bgra.Length);
                else for (int y = 0; y < h; y++) System.Runtime.InteropServices.Marshal.Copy(bgra, y * w * 4, fb.Address + y * rb, w * 4);
            }
            return wb;
        }

        // ── Battle mock (shows the mon vs itself: enemy = front sprite, player = back sprite) ──────
        // Layout/coords mirror PokEditor's battle scene (256×192): front sprite at (152, 10 − spriteY),
        // back sprite at (23, 72), enemy shadow at size-specific X (179/174/167 + shadowX), Y 83/83/82.
        private readonly PokemonSpriteEditorViewModel _sprites;

        // The send-out sheet is two 80×80 frames; we loop them so the preview animates like the game.
        // Vanilla (non-hg-engine): both driven off the shared pokeAnim AnimSteps loop (front == back).
        private int _frame;   // 0 or 1

        // hg-engine: frame-cycling comes from data/SpriteOffsets.c's .frontFrames/.backFrames instead,
        // with independent sequences for front and back.
        private System.Collections.Generic.List<(int Frame, int Duration)> _hgeFrontSteps, _hgeBackSteps;
        private int _hgeFrontFrame, _hgeBackFrame;
        private int _hgeFrontStepIndex = -1, _hgeFrontCountdown, _hgeBackStepIndex = -1, _hgeBackCountdown;

        private readonly global::Avalonia.Threading.DispatcherTimer _animTimer;

        // Display mode: "separate" shows one gender; "unified" shows Male + Female side by side. Genders rebuilds to just the real one(s) per species (see RefreshGenderChoices).
        public ObservableCollection<string> Genders { get; } = new ObservableCollection<string> { "Male", "Female" };
        private int _genderIndex;
        public int GenderIndex { get => _genderIndex; set { if (Set(ref _genderIndex, value)) RaiseSprites(); } }
        private bool ShowFemale => _genderIndex >= 0 && _genderIndex < Genders.Count && Genders[_genderIndex] == "Female";
        public bool HasGenderChoice => Genders.Count > 1;

        // Updates Genders in place via ListSync, never Clear(). Clear() fires a Reset that nulls the ComboBox's SelectedIndex and re-enters this method through the GenderIndex setter, which crashed the app.
        private void RefreshGenderChoices()
        {
            bool hasMale = _sprites?.HasSpriteSlot(1) ?? true;
            bool hasFemale = _sprites?.HasSpriteSlot(0) ?? true;
            var wanted = new System.Collections.Generic.List<string>();
            if (hasMale) wanted.Add("Male");
            if (hasFemale) wanted.Add("Female");
            if (wanted.Count == 0) wanted.Add("Male");

            if (!Genders.SequenceEqual(wanted))
            {
                string current = _genderIndex >= 0 && _genderIndex < Genders.Count ? Genders[_genderIndex] : null;
                ListSync.Apply(Genders, wanted);
                int keep = current != null ? Genders.IndexOf(current) : -1;
                int newIndex = keep >= 0 ? keep : 0;
                if (_genderIndex != newIndex) { _genderIndex = newIndex; OnPropertyChanged(nameof(GenderIndex)); }
            }
            OnPropertyChanged(nameof(HasGenderChoice));
            if (!HasGenderChoice) UnifiedDisplay = false;
        }

        private bool _unifiedDisplay;
        public bool UnifiedDisplay { get => _unifiedDisplay; set => Set(ref _unifiedDisplay, value); }

        /// <summary>Highest valid frame index (sheet width/80 − 1); bounds the Frame field and the preview.</summary>
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
            int frame = HgEngineProject.IsActive ? _hgeFrontFrame : _frame;
            return female ? Pick(s.BattleFrontF, s.BattleFrontM, frame) : Pick(s.BattleFrontM, s.BattleFrontF, frame);
        }
        private Bitmap Back(bool female)
        {
            var s = _sprites; if (s == null) return null;
            int frame = HgEngineProject.IsActive ? _hgeBackFrame : _frame;
            return female ? Pick(s.BattleBackF, s.BattleBackM, frame) : Pick(s.BattleBackM, s.BattleBackF, frame);
        }

        // Gender-selected (separate display) + explicit per-gender (unified side-by-side display).
        public Bitmap EnemySprite => Front(ShowFemale);
        public Bitmap PlayerSprite => Back(ShowFemale);
        public Bitmap EnemySpriteM => Front(false);
        public Bitmap PlayerSpriteM => Back(false);
        public Bitmap EnemySpriteF => Front(true);
        public Bitmap PlayerSpriteF => Back(true);

        // Frame-integrity warnings: reuses the Sprite Editor's own blank-frame detection (FrameCellState), cross-checked against whichever pattern data actually drives this preview.
        private PokemonSpriteEditorViewModel.FrameCellState PoseCell(bool front, bool female)
        {
            if (_sprites == null) return null;
            if (front) return female ? _sprites.FemaleFrontNormalFrame : _sprites.MaleFrontNormalFrame;
            return female ? _sprites.FemaleBackNormalFrame : _sprites.MaleBackNormalFrame;
        }

        private static int BlankFrameIndex(PokemonSpriteEditorViewModel.FrameCellState cell)
        {
            if (cell == null) return -1;
            if (!cell.HasFrame1) return 0;
            if (!cell.HasFrame2) return 1;
            return -1;
        }

        // The one real (non-blank) frame index, or -1 when both are real (no issue) or both are blank.
        private static int RealFrameIndex(PokemonSpriteEditorViewModel.FrameCellState cell)
        {
            if (cell == null) return -1;
            if (cell.HasFrame1 && !cell.HasFrame2) return 0;
            if (!cell.HasFrame1 && cell.HasFrame2) return 1;
            return -1;
        }

        private int ClampFrame(int frame)
        {
            int max = MaxFrameIndex;
            return frame < 0 ? 0 : (frame > max ? max : frame);
        }

        private System.Collections.Generic.List<int> ActivePatternFrames(bool front)
        {
            System.Collections.Generic.IEnumerable<int> raw = HgEngineProject.IsActive
                ? (front ? _hgeFrontSteps : _hgeBackSteps)?.Select(s => s.Frame) ?? Enumerable.Empty<int>()
                : AnimSteps.Select(s => s.Frame);
            var visited = raw.Select(ClampFrame).Distinct().ToList();
            if (visited.Count == 0) visited.Add(0);   // no pattern data -> AnimTick holds a static frame 0
            return visited;
        }

        private bool FrameWarning(bool front, bool female) => BlankFrameIndex(PoseCell(front, female)) >= 0;

        // True only when the animation never shows the real frame, so the sprite would be invisible the whole loop.
        private bool FrameWarningSevere(bool front, bool female)
        {
            var cell = PoseCell(front, female);
            int blank = BlankFrameIndex(cell);
            if (blank < 0) return false;
            int real = RealFrameIndex(cell);
            var visited = ActivePatternFrames(front);
            return visited.Contains(blank) && (real < 0 || !visited.Contains(real));
        }

        private string FrameWarningText(bool front, bool female)
        {
            var cell = PoseCell(front, female);
            int blank = BlankFrameIndex(cell);
            if (blank < 0) return null;
            int real = RealFrameIndex(cell);
            var visited = ActivePatternFrames(front);
            if (!visited.Contains(blank)) return "only has one real frame";
            if (real >= 0 && visited.Contains(real))
                return "animation flickers to a blank frame, may be intentional";
            return "animation never shows a real frame, likely a mistake";
        }

        public bool EnemyFrameWarning => FrameWarning(true, ShowFemale);
        public bool EnemyFrameWarningSevere => FrameWarningSevere(true, ShowFemale);
        public string EnemyFrameWarningText => FrameWarningText(true, ShowFemale);
        public bool PlayerFrameWarning => FrameWarning(false, ShowFemale);
        public bool PlayerFrameWarningSevere => FrameWarningSevere(false, ShowFemale);
        public string PlayerFrameWarningText => FrameWarningText(false, ShowFemale);
        public bool EnemyFrameWarningM => FrameWarning(true, false);
        public bool EnemyFrameWarningSevereM => FrameWarningSevere(true, false);
        public string EnemyFrameWarningTextM => FrameWarningText(true, false);
        public bool EnemyFrameWarningF => FrameWarning(true, true);
        public bool EnemyFrameWarningSevereF => FrameWarningSevere(true, true);
        public string EnemyFrameWarningTextF => FrameWarningText(true, true);
        public bool PlayerFrameWarningM => FrameWarning(false, false);
        public bool PlayerFrameWarningSevereM => FrameWarningSevere(false, false);
        public string PlayerFrameWarningTextM => FrameWarningText(false, false);
        public bool PlayerFrameWarningF => FrameWarning(false, true);
        public bool PlayerFrameWarningSevereF => FrameWarningSevere(false, true);
        public string PlayerFrameWarningTextF => FrameWarningText(false, true);

        private void RaiseFrameWarnings()
        {
            OnPropertyChanged(nameof(EnemyFrameWarning)); OnPropertyChanged(nameof(EnemyFrameWarningSevere)); OnPropertyChanged(nameof(EnemyFrameWarningText));
            OnPropertyChanged(nameof(PlayerFrameWarning)); OnPropertyChanged(nameof(PlayerFrameWarningSevere)); OnPropertyChanged(nameof(PlayerFrameWarningText));
            OnPropertyChanged(nameof(EnemyFrameWarningM)); OnPropertyChanged(nameof(EnemyFrameWarningSevereM)); OnPropertyChanged(nameof(EnemyFrameWarningTextM));
            OnPropertyChanged(nameof(EnemyFrameWarningF)); OnPropertyChanged(nameof(EnemyFrameWarningSevereF)); OnPropertyChanged(nameof(EnemyFrameWarningTextF));
            OnPropertyChanged(nameof(PlayerFrameWarningM)); OnPropertyChanged(nameof(PlayerFrameWarningSevereM)); OnPropertyChanged(nameof(PlayerFrameWarningTextM));
            OnPropertyChanged(nameof(PlayerFrameWarningF)); OnPropertyChanged(nameof(PlayerFrameWarningSevereF)); OnPropertyChanged(nameof(PlayerFrameWarningTextF));
        }

        // When viewing an alternate FORM, the form's height_o values (gender-agnostic) drive the preview instead.
        private bool HeightsActive => _hasHeights || (_formMode && _hasFormHeights);
        private int ActFrontH(bool f) => (_formMode && _hasFormHeights) ? _formFrontH : (f ? _frontHeightF : _frontHeightM);
        private int ActBackH(bool f) => (_formMode && _hasFormHeights) ? _formBackH : (f ? _backHeightF : _backHeightM);

        // Base 10, which is what the engine works out: appear Y 50 less half the 80px sprite.
        // A measurement against a screenshot once put this at 11; testers on real battles say 10.
        private double FrontTopFor(int h) => 10 - _spriteY + (HeightsActive ? h : 0);
        private double BackTopFor(int h) => 72 + (HeightsActive ? h : 0);

        public double EnemyLeft => 152;
        public double PlayerLeft => 23;
        public double EnemyTop => FrontTopFor(ActFrontH(ShowFemale));
        public double PlayerTop => BackTopFor(ActBackH(ShowFemale));
        public double EnemyTopM => FrontTopFor(ActFrontH(false));
        public double EnemyTopF => FrontTopFor(ActFrontH(true));
        public double PlayerTopM => BackTopFor(ActBackH(false));
        public double PlayerTopF => BackTopFor(ActBackH(true));

        public bool ShadowSmallVisible => HasSpriteData && _shadowSize == 1;
        public bool ShadowMediumVisible => HasSpriteData && _shadowSize == 2;
        public bool ShadowLargeVisible => HasSpriteData && _shadowSize == 3;
        public double ShadowSmallLeft => 179 + _shadowX;
        public double ShadowMediumLeft => 174 + _shadowX;
        public double ShadowLargeLeft => 167 + _shadowX;

        private void RaiseLayout()
        {
            OnPropertyChanged(nameof(EnemyTop)); OnPropertyChanged(nameof(PlayerTop));
            OnPropertyChanged(nameof(EnemyTopM)); OnPropertyChanged(nameof(EnemyTopF)); OnPropertyChanged(nameof(PlayerTopM)); OnPropertyChanged(nameof(PlayerTopF));
            OnPropertyChanged(nameof(ShadowSmallVisible)); OnPropertyChanged(nameof(ShadowMediumVisible)); OnPropertyChanged(nameof(ShadowLargeVisible));
            OnPropertyChanged(nameof(ShadowSmallLeft)); OnPropertyChanged(nameof(ShadowMediumLeft)); OnPropertyChanged(nameof(ShadowLargeLeft));
        }
        private void RaiseSprites()
        {
            RefreshGenderChoices();
            OnPropertyChanged(nameof(EnemySprite)); OnPropertyChanged(nameof(PlayerSprite));
            OnPropertyChanged(nameof(EnemySpriteM)); OnPropertyChanged(nameof(PlayerSpriteM));
            OnPropertyChanged(nameof(EnemySpriteF)); OnPropertyChanged(nameof(PlayerSpriteF));
            RaiseFrameWarnings();
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

        // A just-imported icon graphic, staged in memory until Save() (same convention as every other
        // field on this tab). Quantized against whatever palette was selected at import time; changing
        // PartyPaletteIndex afterward does not automatically re-quantize a pending import.
        private RawImage _pendingIconGraphic;

        // A party icon holds its animation frames stacked vertically, 32px each, so the preview can show
        // either one. Most icons have two; anything with a single frame hides the picker.
        private const int IconFrameSize = 32;

        private int _iconFrameCount = 1;
        public int IconFrameCount { get => _iconFrameCount; private set { if (Set(ref _iconFrameCount, value)) OnPropertyChanged(nameof(ShowIconFrames)); } }
        public bool ShowIconFrames => _iconFrameCount > 1;

        private int _iconFrame;
        public int IconFrame
        {
            get => _iconFrame;
            set { if (Set(ref _iconFrame, value)) RefreshPreview(); }
        }

        private void RefreshPreview()
        {
            if (!IsAvailable || _currentId <= 0) { IconPreview = null; IconFrameCount = 1; return; }
            try
            {
                RawImage full = _pendingIconGraphic
                    ?? DSPRE.DSUtils.GetMonIconGraphicRaw(IconIdFor(_currentId), _partyPaletteIndex);

                if (full != null && full.Height >= IconFrameSize * 2)
                {
                    IconFrameCount = full.Height / IconFrameSize;
                    if (_iconFrame >= _iconFrameCount) { _iconFrame = 0; OnPropertyChanged(nameof(IconFrame)); }
                    IconPreview = DSPRE.Avalonia.ImageConverter.ToAvaloniaBitmap(CropIconFrame(full, _iconFrame));
                    return;
                }

                IconFrameCount = 1;
                if (_pendingIconGraphic != null)
                {
                    IconPreview = DSPRE.Avalonia.ImageConverter.ToAvaloniaBitmap(_pendingIconGraphic);
                    return;
                }
                // Single-frame icon: fall back to the game's own OAM-composed render.
                var gdi = DSPRE.DSUtils.GetPokePicRaw(IconIdFor(_currentId), 64, 64, paletteIdOverride: _partyPaletteIndex);
                IconPreview = gdi != null ? DSPRE.Avalonia.ImageConverter.ToAvaloniaBitmap(gdi) : null;
            }
            catch { IconPreview = null; IconFrameCount = 1; }
        }

        private static RawImage CropIconFrame(RawImage full, int frame)
        {
            int top = frame * IconFrameSize;
            if (top + IconFrameSize > full.Height) top = 0;
            var outImg = new RawImage(full.Width, IconFrameSize);
            int rowBytes = full.Width * 4;
            for (int y = 0; y < IconFrameSize; y++)
                System.Array.Copy(full.Bgra, (top + y) * rowBytes, outImg.Bgra, y * rowBytes, rowBytes);
            return outImg;
        }

        private bool _fullPaletteExport;
        public bool FullPaletteExport { get => _fullPaletteExport; set => Set(ref _fullPaletteExport, value); }

        private static int IconIdFor(int id) => DSPRE.DSUtils.ResolveIconId(id);
        private static int BaseSpeciesIdFor(int id) => DSPRE.DSUtils.ResolveBaseSpeciesId(id);

        /// <summary>Exports the icon's current raw graphic (on-disk, or the pending import if one hasn't
        /// been saved yet) at native resolution: no OAM padding, suitable for round-tripping.</summary>
        public RawImage ExportIconGraphic()
        {
            if (!IsAvailable || _currentId < 0) return null;
            if (_pendingIconGraphic != null) return _pendingIconGraphic;
            try { return DSPRE.DSUtils.GetMonIconGraphicRaw(IconIdFor(_currentId), _partyPaletteIndex); }
            catch { return null; }
        }

        /// <summary>Exports the icon as a genuine indexed PNG (real embedded 16-color palette table,
        /// not RawImage's always-flattened RGBA) so tools that read PNG palettes see the actual colors,
        /// same intent as the WinForms "export full palette" option. Unavailable while a pending unsaved
        /// import exists, since that replacement isn't on disk in indexed form yet.</summary>
        public byte[] ExportIconGraphicIndexedPng()
        {
            if (!IsAvailable || _currentId < 0 || _pendingIconGraphic != null) return null;
            try
            {
                if (!DSPRE.DSUtils.TryGetMonIconIndexedPixels(IconIdFor(_currentId), _partyPaletteIndex, out byte[] indices, out int w, out int h, out var palette))
                    return null;
                return DSPRE.Avalonia.IndexedPngWriter.Encode4Bpp(indices, w, h, palette);
            }
            catch { return null; }
        }

        /// <summary>Validates and stages a replacement icon graphic; written to disk on Save(). Returns
        /// null on success, or a user-facing error string (size/format mismatch).</summary>
        public string ImportIconGraphic(RawImage newImage)
        {
            if (!IsAvailable || _currentId < 0) return "No Pokémon selected.";
            string error = DSPRE.DSUtils.ValidateMonIconGraphic(IconIdFor(_currentId), newImage);
            if (error != null) return error;

            _pendingIconGraphic = newImage;
            SetDirty();
            RefreshPreview();
            return null;
        }

        // ── Battle sprite / shadow data (family-specific NARC layout) ─────────────────────────
        // Editable: front-sprite Y (signed), shadow X (signed), shadow size; a movement/animation byte
        // (HGSS + Platinum combined record); the per-gender sprite HEIGHTS (DP & Platinum, height.narc: 4
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
                    GameFamilies.HGSS => new CombinedTailSource(DirNames.pokemonSpriteOffsets, 89, hasMovement: true, movementOffset: 1, withHeights: true),
                    GameFamilies.Plat => new CombinedTailSource(DirNames.pokemonSpriteOffsets, 89, hasMovement: true, movementOffset: 1, withHeights: true),
                    GameFamilies.DP => new SeparateByteSource(DirNames.pokeYofs, DirNames.pokeShadowOfx, DirNames.pokeShadow),
                    _ => null,
                };
            }
            catch { _src = null; }
        }

        /// <summary>True when this mon has a sprite-coordinate record (enables those fields).</summary>
        private bool _hasSpriteData;
        public bool HasSpriteData { get => _hasSpriteData; private set => Set(ref _hasSpriteData, value); }

        /// <summary>True only where a movement/animation byte exists (HGSS, Platinum); hides that field on DP.</summary>
        private bool _hasMovementType;
        public bool HasMovementType { get => _hasMovementType; private set { if (Set(ref _hasMovementType, value)) OnPropertyChanged(nameof(ShowMovementType)); } }

        /// <summary>Whether to show the vanilla "Movement type" field in Positioning. Hidden on hg-engine
        /// ROMs: it's the same source field (frontHeader.animation) as the Animation tab's "Front animation
        /// #", and only one place should ever write it (see the comment in SaveSpriteData).</summary>
        public bool ShowMovementType => _hasMovementType && !HgEngineProject.IsActive;

        /// <summary>True where per-gender sprite heights exist (DP, Platinum; height.narc).</summary>
        private bool _hasHeights;
        public bool HasHeights { get => _hasHeights; private set { if (Set(ref _hasHeights, value)) OnPropertyChanged(nameof(ShowBaseHeights)); } }

        /// <summary>True where the raw battle-animation record exists (DP; pokeanm.narc).</summary>
        private bool _hasAnimData;
        public bool HasAnimData { get => _hasAnimData; private set => Set(ref _hasAnimData, value); }

        private bool CanEditSprite => _src != null && _hasSpriteData && !_loading;

        private int _movementType;
        public int MovementType { get => _movementType; set { if (Set(ref _movementType, value) && CanEditSprite) SetDirty(); } }

        private int _spriteY;   // signed −128..127, additive (positive = up, negative = down)
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
        private int _backHeightM; public int BackHeightM { get => _backHeightM; set { if (Set(ref _backHeightM, value)) { if (CanEditSprite) SetDirty(); OnPropertyChanged(nameof(BackHeightUnified)); RaiseLayout(); } } }
        private int _backHeightF; public int BackHeightF { get => _backHeightF; set { if (Set(ref _backHeightF, value)) { if (CanEditSprite) SetDirty(); RaiseLayout(); } } }

        // Modify mode: "unified" exposes one field per axis that writes BOTH genders at once (for the common
        // case where the two genders share a sprite). "separate" exposes the 4 per-gender fields above.
        private bool _unifiedEdit = true;
        public bool UnifiedEdit { get => _unifiedEdit; set => Set(ref _unifiedEdit, value); }
        public int FrontHeightUnified { get => _frontHeightM; set { FrontHeightM = value; FrontHeightF = value; } }
        public int BackHeightUnified { get => _backHeightM; set { BackHeightM = value; BackHeightF = value; } }

        // Alt-form heights (height_o.narc), indexed by the form's otherpoke sprite index.
        private OffsetNarc _formHeightNarc;
        private bool _formNarcTried;
        private bool _formMode;       // mirrors SpriteVM.IsAlternateForms
        private int _formIndex = -1;  // mirrors SpriteVM.SelectedFormIndex
        private int _formFrontH, _formBackH;

        private bool _hasFormHeights;
        public bool HasFormHeights { get => _hasFormHeights; private set { if (Set(ref _hasFormHeights, value)) OnPropertyChanged(nameof(ShowBaseHeights)); } }
        public bool FormMode => _formMode;
        /// <summary>The base per-gender heights apply to the main sprite, so hide them while viewing a form.</summary>
        public bool ShowBaseHeights => _hasHeights && !_formMode;

        public int FormFrontHeight { get => _formFrontH; set { if (Set(ref _formFrontH, value)) { if (!_loading) SetDirty(); RaiseLayout(); } } }
        public int FormBackHeight { get => _formBackH; set { if (Set(ref _formBackH, value)) { if (!_loading) SetDirty(); RaiseLayout(); } } }

        private void EnsureFormNarc()
        {
            if (_formNarcTried) return;
            _formNarcTried = true;
            _formHeightNarc = new OffsetNarc(DirNames.pokeHeightForms, 1);
        }

        private void LoadFormHeights()
        {
            HasFormHeights = false;
            if (!IsAvailable || !_formMode || _formIndex < 0) return;
            EnsureFormNarc();
            if (_formHeightNarc == null) return;
            if (_sprites == null || !_sprites.TryGetCurrentFormHeightIndices(out int backIdx, out int frontIdx)) return;
            try
            {
                var b = _formHeightNarc.GetRecord(backIdx);
                var f = _formHeightNarc.GetRecord(frontIdx);
                if (b == null || f == null || b.Length < 1 || f.Length < 1) return;
                _formBackH = b[0]; _formFrontH = f[0];
                OnPropertyChanged(nameof(FormFrontHeight)); OnPropertyChanged(nameof(FormBackHeight));
                HasFormHeights = true;
            }
            catch { HasFormHeights = false; }
        }

        private void SaveFormHeights()
        {
            if (_formHeightNarc == null || !_hasFormHeights || _formIndex < 0) return;
            if (_sprites == null || !_sprites.TryGetCurrentFormHeightIndices(out int backIdx, out int frontIdx)) return;
            WriteForm(backIdx, _formBackH);
            WriteForm(frontIdx, _formFrontH);
        }
        // An unused slot is a real but empty (0-byte) file, not a missing one - grow it instead of skipping the write.
        private void WriteForm(int idx, int v) { var r = _formHeightNarc.GetRecord(idx); if (r == null) return; if (r.Length < 1) r = new byte[1]; r[0] = (byte)v; _formHeightNarc.PutRecord(idx, r); }

        private void OnSpriteFormChanged()
        {
            _formMode = _sprites != null && _sprites.IsAlternateForms;
            _formIndex = _sprites != null ? _sprites.SelectedFormIndex : -1;
            LoadFormHeights();
            OnPropertyChanged(nameof(FormMode));
            OnPropertyChanged(nameof(ShowBaseHeights));
            RaiseLayout();
        }

        // ── Battle-sprite animation (the Pokémon battle-animation NARC; DP/Plat/HGSS), 28 bytes per Pokémon ──
        // Record layout: [0] front program-anim #, [1] its wait, [2..7] three back program-anim steps
        // {patno,wait}, [8..27] ten "pattern" steps {s8 patno(frame), u8 wait}; patno=-1 (0xFF) terminates.
        // The pattern steps are the on-field sprite wiggle, so they drive the preview loop.
        private const int ANIM_REC_LEN = 28, ANIM_PAT_OFFSET = 8, ANIM_PAT_MAX = 10;
        private OffsetNarc _animNarc;
        private bool _animNarcTried;

        private int _animFrontProg; public int AnimFrontProgNum { get => _animFrontProg; set { if (Set(ref _animFrontProg, value)) { if (!_loading) SetDirty(); if (_scriptTarget == 0) RefreshProgramScript(); else OnPropertyChanged(nameof(ProgramScriptHeader)); } } }
        private int _animFrontWait; public int AnimFrontWait { get => _animFrontWait; set { if (Set(ref _animFrontWait, value) && !_loading) SetDirty(); } }

        // hg-engine only: cryDelay sits alongside animation/animationDelay in each of frontHeader/backHeader.
        private int _animFrontCryDelay; public int AnimFrontCryDelay { get => _animFrontCryDelay; set { if (Set(ref _animFrontCryDelay, value) && !_loading) SetDirty(); } }
        private int _animBackCryDelay; public int AnimBackCryDelay { get => _animBackCryDelay; set { if (Set(ref _animBackCryDelay, value) && !_loading) SetDirty(); } }

        /// <summary>The three back program-animation steps ({number, wait}).</summary>
        public ObservableCollection<AnimProgStep> AnimBack { get; } = new ObservableCollection<AnimProgStep>();
        /// <summary>The pattern (frame) animation steps: the visible send-out/idle wiggle. Drives the preview.</summary>
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

            if (HgEngineProject.IsActive) { LoadAnimFromHgeSource(_currentHgeSpeciesId); return; }

            EnsureAnimNarc();
            var r = _animNarc?.GetRecord(BaseSpeciesIdFor(id));
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

        // AnimSteps stays empty: idle-frame cycling is populated separately in LoadSpriteDataFromHgeSource.
        private void LoadAnimFromHgeSource(int id)
        {
            if (!HgEngineSpriteOffsets.TryLoad(id, out var block, out _)) { OnPropertyChanged(nameof(CanAddAnimStep)); return; }
            if (!block.TryGetInt(new[] { FieldPathSegment.Field("frontHeader"), FieldPathSegment.Field("animation") }, out _animFrontProg)) { OnPropertyChanged(nameof(CanAddAnimStep)); return; }
            block.TryGetInt(new[] { FieldPathSegment.Field("frontHeader"), FieldPathSegment.Field("animationDelay") }, out _animFrontWait);
            block.TryGetInt(new[] { FieldPathSegment.Field("frontHeader"), FieldPathSegment.Field("cryDelay") }, out _animFrontCryDelay);
            block.TryGetInt(new[] { FieldPathSegment.Field("backHeader"), FieldPathSegment.Field("animation") }, out int backProg);
            block.TryGetInt(new[] { FieldPathSegment.Field("backHeader"), FieldPathSegment.Field("animationDelay") }, out int backWait);
            block.TryGetInt(new[] { FieldPathSegment.Field("backHeader"), FieldPathSegment.Field("cryDelay") }, out _animBackCryDelay);
            AddBackStep(backProg, backWait);

            OnPropertyChanged(nameof(AnimFrontProgNum)); OnPropertyChanged(nameof(AnimFrontWait));
            OnPropertyChanged(nameof(AnimFrontCryDelay)); OnPropertyChanged(nameof(AnimBackCryDelay));
            OnPropertyChanged(nameof(CanAddAnimStep));
            HasAnimData = true;
            RefreshProgramScript();
            RestartAnimPreview();
        }

        private void SaveAnim()
        {
            if (!_hasAnimData) return;

            if (HgEngineProject.IsActive)
            {
                var fields = new[]
                {
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("frontHeader"), FieldPathSegment.Field("animation") }, _animFrontProg.ToString()),
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("frontHeader"), FieldPathSegment.Field("animationDelay") }, _animFrontWait.ToString()),
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("frontHeader"), FieldPathSegment.Field("cryDelay") }, _animFrontCryDelay.ToString()),
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("backHeader"), FieldPathSegment.Field("animation") }, (AnimBack.Count > 0 ? AnimBack[0].Number : 0).ToString()),
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("backHeader"), FieldPathSegment.Field("animationDelay") }, (AnimBack.Count > 0 ? AnimBack[0].Wait : 0).ToString()),
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("backHeader"), FieldPathSegment.Field("cryDelay") }, _animBackCryDelay.ToString()),
                };
                HgEngineWriter.TryWriteFields(HgEngineDomain.SpriteOffsets, _currentHgeSpeciesId, fields, out _, out _);
                return;
            }

            if (_animNarc == null) return;
            int animId = BaseSpeciesIdFor(_currentId);
            var r = _animNarc.GetRecord(animId);
            if (r == null || r.Length < ANIM_REC_LEN) return;
            r[0] = (byte)_animFrontProg; r[1] = (byte)_animFrontWait;
            for (int i = 0; i < 3 && i < AnimBack.Count; i++) { r[2 + i * 2] = (byte)AnimBack[i].Number; r[3 + i * 2] = (byte)AnimBack[i].Wait; }
            for (int i = 0; i < ANIM_PAT_MAX; i++)
            {
                if (i < AnimSteps.Count) { r[ANIM_PAT_OFFSET + i * 2] = (byte)(sbyte)AnimSteps[i].Frame; r[ANIM_PAT_OFFSET + i * 2 + 1] = (byte)AnimSteps[i].Wait; }
                else { r[ANIM_PAT_OFFSET + i * 2] = 0xFF; r[ANIM_PAT_OFFSET + i * 2 + 1] = 0; }   // terminator + pad
            }
            _animNarc.PutRecord(animId, r);
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

        // ── hg-engine frame editing (data/SpriteOffsets.c's .frontFrames/.backFrames, 10 slots each) ──
        // Fixed-size (the source C array is SpriteFrame[10]); no add/remove, a slot with FrameNo = -1 is
        // simply unused/terminates the idle-cycle loop, matching the file's own convention. Editing a slot
        // live-updates the preview loop below (RecomputeHgeSteps), same as editing AnimSteps does for vanilla.
        public ObservableCollection<SpriteFrameEntry> FrontFrameEntries { get; } = new ObservableCollection<SpriteFrameEntry>();
        public ObservableCollection<SpriteFrameEntry> BackFrameEntries { get; } = new ObservableCollection<SpriteFrameEntry>();
        public bool HasHgeFrameData => HgEngineProject.IsActive && _hasSpriteData;

        private void LoadFrameEntries(HgEngineSourceBlock block)
        {
            foreach (var e in FrontFrameEntries) e.PropertyChanged -= OnFrameEntryChanged;
            foreach (var e in BackFrameEntries) e.PropertyChanged -= OnFrameEntryChanged;
            FrontFrameEntries.Clear(); BackFrameEntries.Clear();

            foreach (var slot in HgEngineSpriteOffsets.ReadFrameSlots(block, "frontFrames")) AddFrameEntry(FrontFrameEntries, slot);
            foreach (var slot in HgEngineSpriteOffsets.ReadFrameSlots(block, "backFrames")) AddFrameEntry(BackFrameEntries, slot);

            RecomputeHgeSteps();
            OnPropertyChanged(nameof(HasHgeFrameData));
        }

        private void AddFrameEntry(ObservableCollection<SpriteFrameEntry> list, HgEngineSpriteOffsets.SpriteFrameSlot slot)
        {
            var e = new SpriteFrameEntry
            {
                FrameNo = slot.FrameNo, Duration = slot.Duration,
                HorizontalShift = slot.HorizontalShift, VerticalShift = slot.VerticalShift,
            };
            e.PropertyChanged += OnFrameEntryChanged;
            list.Add(e);
        }

        private void OnFrameEntryChanged(object _, PropertyChangedEventArgs __)
        {
            if (_loading) return;
            SetDirty();
            RecomputeHgeSteps();
            RestartAnimPreview();
        }

        // Same "stop at the first negative frameNo" semantics as HgEngineSpriteOffsets.ReadFrameSteps,
        // just re-derived from the live-editable entries instead of a fresh file read.
        private void RecomputeHgeSteps()
        {
            _hgeFrontSteps = StepsFrom(FrontFrameEntries);
            _hgeBackSteps = StepsFrom(BackFrameEntries);
        }

        private static List<(int Frame, int Duration)> StepsFrom(ObservableCollection<SpriteFrameEntry> entries)
        {
            var steps = new List<(int, int)>();
            foreach (var e in entries)
            {
                if (e.FrameNo < 0) break;
                steps.Add((e.FrameNo, e.Duration));
            }
            return steps;
        }

        private void SaveFrames()
        {
            if (!HgEngineProject.IsActive || !_hasSpriteData) return;
            var fields = new List<HgEngineFieldWrite>();
            fields.AddRange(HgEngineSpriteOffsets.BuildFrameWrites("frontFrames", ToSlotData(FrontFrameEntries)));
            fields.AddRange(HgEngineSpriteOffsets.BuildFrameWrites("backFrames", ToSlotData(BackFrameEntries)));
            HgEngineWriter.TryWriteFields(HgEngineDomain.SpriteOffsets, _currentHgeSpeciesId, fields, out _, out _);
        }

        private static List<HgEngineSpriteOffsets.SpriteFrameSlot> ToSlotData(ObservableCollection<SpriteFrameEntry> entries)
        {
            var slots = new List<HgEngineSpriteOffsets.SpriteFrameSlot>(entries.Count);
            foreach (var e in entries)
                slots.Add(new HgEngineSpriteOffsets.SpriteFrameSlot(e.FrameNo, e.Duration, e.HorizontalShift, e.VerticalShift));
            return slots;
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
        public double AnimScaleX { get => _animScaleX; private set => Set(ref _animScaleX, value); }
        public double AnimScaleY { get => _animScaleY; private set => Set(ref _animScaleY, value); }
        public double AnimRotation { get => _animRotation; private set => Set(ref _animRotation, value); }
        public double AnimFadeOpacity { get => _animFadeOpacity; private set => Set(ref _animFadeOpacity, value); }
        private IBrush _animFadeBrush = Brushes.Transparent;
        public IBrush AnimFadeBrush { get => _animFadeBrush; private set => Set(ref _animFadeBrush, value); }

        // Same, for the player/back sprite (our own mon's entry animation, from prg_anm_b).
        private double _animBackOffsetX, _animBackOffsetY, _animBackScaleX = 1, _animBackScaleY = 1, _animBackRotation, _animBackFadeOpacity;
        public double AnimBackOffsetX { get => _animBackOffsetX; private set => Set(ref _animBackOffsetX, value); }
        public double AnimBackOffsetY { get => _animBackOffsetY; private set => Set(ref _animBackOffsetY, value); }
        public double AnimBackScaleX { get => _animBackScaleX; private set => Set(ref _animBackScaleX, value); }
        public double AnimBackScaleY { get => _animBackScaleY; private set => Set(ref _animBackScaleY, value); }
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

        private int _frontDelay, _backDelay;   // pre-delay frames before the front / back program (prg_anm_*_wait)

        // The own-mon back sprite has THREE alternative program animations (prg_anm_b[0..2]); the game plays exactly
        // ONE, chosen from the Pokémon's nature via PokeAnm_GetBackAnmSlotNo (0 = lively, 1 = neutral, 2 = reserved
        // natures). We expose the slot directly so the editor can preview each variant. (PokePrgAnmDataSet)
        private int _backVariant;
        public int BackVariantIndex
        {
            get => _backVariant;
            set { if (Set(ref _backVariant, Math.Clamp(value, 0, 2)) && _progPlaying) { StopProgramAnim(); ToggleProgramAnim(); } }
        }
        public string[] BackVariantOptions { get; } =
            { "Slot 0: lively natures", "Slot 1: neutral natures", "Slot 2: reserved natures" };

        /// <summary>Toggles one-shot playback: the front program animation (prg_anm_f) on the enemy sprite, and the
        /// own mon's selected back variant (prg_anm_b[BackVariant]) on the player sprite, each honouring its wait.</summary>
        public void ToggleProgramAnim()
        {
            if (_progPlaying) { StopProgramAnim(); return; }
            EnsureAnimDefsNarc();
            _frontDelay = Math.Max(0, _animFrontWait);
            _prog = LoadProgram(_animFrontProg);

            int slot = Math.Clamp(_backVariant, 0, 2);
            if (slot < AnimBack.Count) { _backDelay = Math.Max(0, AnimBack[slot].Wait); _progBack = LoadProgram(AnimBack[slot].Number); }
            else { _backDelay = 0; _progBack = null; }

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
        // NOTE: this edits the shared animation script in the pokeanime NARC, so it affects every Pokémon that
        // uses this program-animation number, not just the current mon. Saved via its own "Save script" button.
        public ObservableCollection<ProgramCmdRow> ProgramRows { get; } = new ObservableCollection<ProgramCmdRow>();
        public bool HasProgramScript => ProgramRows.Count > 0;

        // Which script the editor targets: 0 = front (prg_anm_f), 1..3 = back variant slots 0..2 (prg_anm_b[n]).
        private int _scriptTarget;
        public string[] ScriptTargetOptions { get; } = { "Front (prg_anm_f)", "Back slot 0", "Back slot 1", "Back slot 2" };
        public int ScriptTargetIndex
        {
            get => _scriptTarget;
            set { if (Set(ref _scriptTarget, Math.Clamp(value, 0, 3))) RefreshProgramScript(); }
        }

        // The pokeanime NARC file index the editor currently edits (front number, or the selected back slot's number).
        private int CurrentScriptFile()
        {
            if (_scriptTarget <= 0) return _animFrontProg;
            int slot = _scriptTarget - 1;
            return (slot >= 0 && slot < AnimBack.Count) ? AnimBack[slot].Number : -1;
        }

        public string ProgramScriptHeader
        {
            get
            {
                string which = _scriptTarget == 0 ? "Front" : $"Back slot {_scriptTarget - 1}";
                return $"{which} program animation #{CurrentScriptFile()}: script ({ProgramRows.Count} cmds)";
            }
        }
        private bool _scriptDirty;
        public bool ScriptDirty { get => _scriptDirty; private set => Set(ref _scriptDirty, value); }

        private void RefreshProgramScript()
        {
            foreach (var r in ProgramRows) r.PropertyChanged -= OnProgramRowChanged;
            ProgramRows.Clear();
            int file = CurrentScriptFile();
            if (IsAvailable && file >= 0)
            {
                EnsureAnimDefsNarc();
                var bytes = _animDefsNarc?.GetRecord(file);
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
        // Turns the editable rows into a valid command list (args padded/truncated to each opcode's fixed count).
        private List<PastCommand> BuildCommandsFromRows()
        {
            if (_animDefsNarc == null)
                return new List<PastCommand>();   // return empty list

            var cmds = new List<PastCommand>();
            foreach (var row in ProgramRows)
            {
                int n = PokeAnimScript.ArgsFor(row.Op);
                var parsed = ParseIntList(row.ArgsText);
                var args = new int[n];
                for (int i = 0; i < n; i++)
                    args[i] = i < parsed.Count ? parsed[i] : 0;

                cmds.Add(new PastCommand(row.Op, args));
            }
            return cmds;
        }

        public void SaveProgramScript()
        {
            int file = CurrentScriptFile();
            if (_animDefsNarc == null || file < 0) return;
            _animDefsNarc.PutRecord(file, PokeAnimScript.Serialize(BuildCommandsFromRows()));
            StopProgramAnim();
            RefreshProgramScript();   // reflect the canonical (padded) form
        }

        /// <summary>Previews just the script the editor is targeting, on its own sprite (front → enemy, back slot →
        /// player). <paramref name="edited"/> plays the current unsaved rows; otherwise the saved NARC file.</summary>
        public void PlayScript(bool edited)
        {
            EnsureAnimDefsNarc();
            StopProgramAnim();   // clears both players + delays, then we arm just the target

            PokeAnimPlayer player;
            if (edited)
            {
                var cmds = BuildCommandsFromRows();
                player = cmds.Count > 0 ? new PokeAnimPlayer(cmds) : null;
            }
            else player = LoadProgram(CurrentScriptFile());
            if (player == null) return;

            if (_scriptTarget == 0)
            {
                _prog = player;
                _frontDelay = Math.Max(0, _animFrontWait);
            }
            else
            {
                int slot = _scriptTarget - 1;
                _progBack = player;
                _backDelay = (slot >= 0 && slot < AnimBack.Count) ? Math.Max(0, AnimBack[slot].Wait) : 0;
            }

            _progPlaying = true;
            OnPropertyChanged(nameof(IsProgramAnimPlaying)); OnPropertyChanged(nameof(ProgramAnimButtonText));
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
            _frontDelay = 0; _backDelay = 0;
            AnimOffsetX = AnimOffsetY = 0; AnimScaleX = AnimScaleY = 1; AnimRotation = 0; AnimFadeOpacity = 0;
            AnimBackOffsetX = AnimBackOffsetY = 0; AnimBackScaleX = AnimBackScaleY = 1; AnimBackRotation = 0; AnimBackFadeOpacity = 0;
            OnPropertyChanged(nameof(IsProgramAnimPlaying)); OnPropertyChanged(nameof(ProgramAnimButtonText));
        }

        // Advances both program animations one frame (called from the 60 fps timer). Plays ONCE: each side waits its
        // pre-delay then runs its single script; when both have finished the sprites settle and playback stops.
        private void TickProgramAnim()
        {
            if (!_progPlaying) return;

            // Front (enemy): wait the pre-delay, then run the script once.
            if (_prog != null)
            {
                if (_frontDelay > 0) _frontDelay--;
                else if (!_prog.Finished) _prog.Step();
                AnimOffsetX = _prog.OffsetX; AnimOffsetY = _prog.OffsetY;
                AnimScaleX = _prog.ScaleX; AnimScaleY = _prog.ScaleY; AnimRotation = _prog.RotationDegrees;
                AnimFadeOpacity = _prog.FadeStrength;
                if (_prog.FadeStrength > 0) AnimFadeBrush = new SolidColorBrush(Color.FromRgb(_prog.FadeR, _prog.FadeG, _prog.FadeB));
            }

            // Back (player): the single nature-selected variant, with its own pre-delay.
            if (_progBack != null)
            {
                if (_backDelay > 0) _backDelay--;
                else if (!_progBack.Finished) _progBack.Step();
                AnimBackOffsetX = _progBack.OffsetX; AnimBackOffsetY = _progBack.OffsetY;
                AnimBackScaleX = _progBack.ScaleX; AnimBackScaleY = _progBack.ScaleY; AnimBackRotation = _progBack.RotationDegrees;
                AnimBackFadeOpacity = _progBack.FadeStrength;
                if (_progBack.FadeStrength > 0) AnimBackFadeBrush = new SolidColorBrush(Color.FromRgb(_progBack.FadeR, _progBack.FadeG, _progBack.FadeB));
            }

            bool frontDone = _prog == null || (_frontDelay == 0 && _prog.Finished);
            bool backDone = _progBack == null || (_backDelay == 0 && _progBack.Finished);
            if (frontDone && backDone) StopProgramAnim();   // one-shot
        }

        private void LoadSpriteData(int id)
        {
            HasSpriteData = false; HasMovementType = false; HasHeights = false;
            _hgeFrontSteps = null; _hgeBackSteps = null;
            if (!IsAvailable || id < 0) return;
            try
            {
                if (HgEngineProject.IsActive) { LoadSpriteDataFromHgeSource(_currentHgeSpeciesId); return; }

                EnsureSource();
                if (_src == null) return;
                if (!_src.TryLoad(BaseSpeciesIdFor(id), out BattleRec rec)) return;
                _spriteY = rec.FrontY; _shadowX = rec.ShadowX; _shadowSize = rec.ShadowSize; _movementType = rec.Movement;
                _backHeightF = rec.BackF; _backHeightM = rec.BackM; _frontHeightF = rec.FrontF; _frontHeightM = rec.FrontM;
                OnPropertyChanged(nameof(SpriteY)); OnPropertyChanged(nameof(ShadowX)); OnPropertyChanged(nameof(ShadowSize)); OnPropertyChanged(nameof(MovementType));
                OnPropertyChanged(nameof(FrontHeightM)); OnPropertyChanged(nameof(FrontHeightF)); OnPropertyChanged(nameof(BackHeightM)); OnPropertyChanged(nameof(BackHeightF));
                OnPropertyChanged(nameof(FrontHeightUnified)); OnPropertyChanged(nameof(BackHeightUnified));
                HasMovementType = rec.HasMovement; HasHeights = rec.HasHeights;
                HasSpriteData = true;
            }
            catch { HasSpriteData = false; }
        }

        // Heights come from data/HeightTable.c, a separate file from the sprite-offset block above.
        private void LoadSpriteDataFromHgeSource(int id)
        {
            if (!HgEngineSpriteOffsets.TryLoad(id, out var block, out _)) return;
            if (!block.TryGetInt(new[] { FieldPathSegment.Field("frontHeader"), FieldPathSegment.Field("animation") }, out int movement)) return;
            if (!block.TryGetInt(new[] { FieldPathSegment.Field("spriteYOffset") }, out int spriteY)) return;
            if (!block.TryGetInt(new[] { FieldPathSegment.Field("shadowXOffset") }, out int shadowX)) return;
            if (!block.TryGetInt(new[] { FieldPathSegment.Field("shadowSize") }, out int shadowSize)) return;

            LoadFrameEntries(block);

            _spriteY = spriteY; _shadowX = shadowX; _shadowSize = shadowSize; _movementType = movement;

            bool hasHeights = HgEngineHeightTable.TryGet(id, out int femaleBack, out int maleBack, out int femaleFront, out int maleFront);
            _backHeightF = hasHeights ? femaleBack : 0; _backHeightM = hasHeights ? maleBack : 0;
            _frontHeightF = hasHeights ? femaleFront : 0; _frontHeightM = hasHeights ? maleFront : 0;

            OnPropertyChanged(nameof(SpriteY)); OnPropertyChanged(nameof(ShadowX)); OnPropertyChanged(nameof(ShadowSize)); OnPropertyChanged(nameof(MovementType));
            OnPropertyChanged(nameof(FrontHeightM)); OnPropertyChanged(nameof(FrontHeightF)); OnPropertyChanged(nameof(BackHeightM)); OnPropertyChanged(nameof(BackHeightF));
            OnPropertyChanged(nameof(FrontHeightUnified)); OnPropertyChanged(nameof(BackHeightUnified));
            HasMovementType = true; HasHeights = hasHeights;
            HasSpriteData = true;
        }

        private void SaveSpriteData()
        {
            if (!IsAvailable || !_hasSpriteData) return;

            if (HgEngineProject.IsActive)
            {
                // frontHeader.animation is NOT written here: SaveAnim() is its sole writer (see AnimFrontProgNum).
                // Writing it from both places raced, whichever ran second clobbered the other.
                var fields = new[]
                {
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("spriteYOffset") }, _spriteY.ToString()),
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("shadowXOffset") }, _shadowX.ToString()),
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("shadowSize") }, _shadowSize.ToString()),
                };
                HgEngineWriter.TryWriteFields(HgEngineDomain.SpriteOffsets, _currentHgeSpeciesId, fields, out _, out _);
                if (_hasHeights)
                    HgEngineHeightTable.TrySet(_currentHgeSpeciesId, _backHeightF, _backHeightM, _frontHeightF, _frontHeightM, out _);
                return;
            }

            if (_src == null) return;
            var rec = new BattleRec
            {
                FrontY = _spriteY,
                ShadowX = _shadowX,
                ShadowSize = _shadowSize,
                Movement = _movementType,
                HasMovement = _hasMovementType,
                BackF = _backHeightF,
                BackM = _backHeightM,
                FrontF = _frontHeightF,
                FrontM = _frontHeightM,
                HasHeights = _hasHeights,
            };
            _src.Save(BaseSpeciesIdFor(_currentId), in rec);
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
                var files = System.IO.Directory.Exists(_path) ? System.IO.Directory.GetFiles(_path) : System.Array.Empty<string>();
                _multi = files.Length > 1;
                _blob = (!_multi && files.Length == 1) ? System.IO.File.ReadAllBytes(files[0]) : null;
            }

            private string FilePath(int id) => System.IO.Path.Combine(_path, id.ToString("D4"));

            public byte[] GetRecord(int id)
            {
                Ensure();
                if (_multi)
                {
                    string f = FilePath(id);
                    return System.IO.File.Exists(f) ? System.IO.File.ReadAllBytes(f) : null;
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
                if (_multi) { System.IO.File.WriteAllBytes(FilePath(id), rec); return; }
                if (_blob == null) return;
                int off = id * _recLen;
                if (off < 0 || off + rec.Length > _blob.Length) return;
                System.Array.Copy(rec, 0, _blob, off, rec.Length);
                System.IO.File.WriteAllBytes(FilePath(0), _blob);   // single-file blob is "0000"
            }
        }

        /// <summary>height.narc (DP + Platinum): 4 unsigned 1-byte values per mon, file order F-back, M-back,
        /// F-front, M-front, so mon N's slot s is the (N*4 + s)th file (or byte, if it unpacks to one blob).</summary>
        private sealed class HeightNarc
        {
            private const int FB = 0, MB = 1, FF = 2, MF = 3;
            private readonly OffsetNarc _n = new OffsetNarc(DirNames.pokeHeight, 1);
            public void Invalidate() => _n.Invalidate();

            // Single-gender species leave the unused slots empty; don't fail the whole load over that.
            public bool TryLoad(int id, out int backF, out int backM, out int frontF, out int frontM)
            {
                var a = _n.GetRecord(id * 4 + FB); var b = _n.GetRecord(id * 4 + MB);
                var c = _n.GetRecord(id * 4 + FF); var d = _n.GetRecord(id * 4 + MF);
                if (a == null && b == null && c == null && d == null) { backF = backM = frontF = frontM = 0; return false; }
                backF = (a != null && a.Length >= 1) ? a[0] : 0;
                backM = (b != null && b.Length >= 1) ? b[0] : 0;
                frontF = (c != null && c.Length >= 1) ? c[0] : 0;
                frontM = (d != null && d.Length >= 1) ? d[0] : 0;
                return true;
            }

            public void Save(int id, in BattleRec rec)
            {
                Put(id * 4 + FB, rec.BackF); Put(id * 4 + MB, rec.BackM); Put(id * 4 + FF, rec.FrontF); Put(id * 4 + MF, rec.FrontM);
            }
            // An unused slot is a real but empty (0-byte) file, not a missing one - grow it instead of skipping the write.
            private void Put(int idx, int v) { var r = _n.GetRecord(idx); if (r == null) return; if (r.Length < 1) r = new byte[1]; r[0] = (byte)v; _n.PutRecord(idx, r); }
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
            { var r = narc.GetRecord(id); if (r == null) return; if (r.Length < 1) r = new byte[1]; r[0] = v; narc.PutRecord(id, r); }
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

            // Timer stays at 60fps (TickProgramAnim's PAST VM needs that rate); PatternTicksPerWaitUnit scales the pokeanm pattern loop down to its real 1/30s wait unit.
            _animTimer = new global::Avalonia.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000.0 / 60)
            };
            _animTimer.Tick += (_, _) => AnimTick();
            _animTimer.Start();

            if (IsAvailable) ApplyArena();
        }

        /// <summary>Stops the preview timer (e.g. when this VM is used only to compute sprite positions elsewhere).</summary>
        public void Detach() => _animTimer?.Stop();

        // pokeanm's "wait" field is in 1/30s units; the timer ticks at 60fps, so each wait unit is 2 ticks.
        private const int PatternTicksPerWaitUnit = 2;
        private int _patIndex = -1, _patCountdown;
        private void RestartAnimPreview()
        {
            _patIndex = -1; _patCountdown = 0;
            _hgeFrontStepIndex = -1; _hgeFrontCountdown = 0;
            _hgeBackStepIndex = -1; _hgeBackCountdown = 0;
        }
        private bool _framePaused;
        public bool FramePaused { get => _framePaused; set => Set(ref _framePaused, value); }

        private void AnimTick()
        {
            TickProgramAnim();   // program-animation motion (independent of the frame/pattern loop)
            if (_framePaused) return;   // frame (pattern) loop paused for inspection

            if (HgEngineProject.IsActive)
            {
                TickHgeFrames(_hgeFrontSteps, ref _hgeFrontStepIndex, ref _hgeFrontCountdown, ref _hgeFrontFrame);
                TickHgeFrames(_hgeBackSteps, ref _hgeBackStepIndex, ref _hgeBackCountdown, ref _hgeBackFrame);
                return;
            }

            if (AnimSteps.Count == 0)
            {
                if (_frame != 0) { _frame = 0; RaiseSprites(); }   // no pattern data → static first frame
                return;
            }
            if (--_patCountdown > 0) return;
            _patIndex = (_patIndex + 1) % AnimSteps.Count;
            var step = AnimSteps[_patIndex];
            _patCountdown = Math.Max(1, step.Wait) * PatternTicksPerWaitUnit;
            int max = MaxFrameIndex;
            int newFrame = step.Frame < 0 ? 0 : (step.Frame > max ? max : step.Frame);
            if (newFrame != _frame) { _frame = newFrame; RaiseSprites(); }
        }

        // No steps means no real animation for this species; stay on frame 0.
        private void TickHgeFrames(System.Collections.Generic.List<(int Frame, int Duration)> steps, ref int stepIndex, ref int countdown, ref int frame)
        {
            if (steps == null || steps.Count == 0)
            {
                if (frame != 0) { frame = 0; RaiseSprites(); }
                return;
            }
            if (--countdown > 0) return;
            stepIndex = (stepIndex + 1) % steps.Count;
            var step = steps[stepIndex];
            countdown = Math.Max(1, step.Duration) * PatternTicksPerWaitUnit;
            int max = MaxFrameIndex;
            int newFrame = step.Frame < 0 ? 0 : (step.Frame > max ? max : step.Frame);
            if (newFrame != frame) { frame = newFrame; RaiseSprites(); }
        }

        public void LoadMon(int id)
        {
            _loading = true;
            _currentId = id;
            OnPropertyChanged(nameof(GaugeNameText));
            _pendingIconGraphic = null;   // drop any unsaved icon-graphic import from the previous mon
            try
            {
                int pal = 0;
                if (IsAvailable && id >= 0)
                {
                    // monIconPalTableAddress is a vanilla-only ARM9 offset; hg-engine uses IconPaletteTable.c instead.
                    if (!(HgEngineProject.IsActive && HgEngineIconPalette.TryGetPaletteId(id, out pal)))
                        pal = DSPRE.DSUtils.GetMonIconPaletteId(IconIdFor(id));
                }
                _partyPaletteIndex = (pal >= 0 && pal < PartyPalettes.Count) ? pal : 0;
            }
            catch { _partyPaletteIndex = 0; }
            OnPropertyChanged(nameof(PartyPaletteIndex));
            RefreshPreview();
            LoadFormOptions(id);
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
            try
            {
                if (HgEngineProject.IsActive)
                    HgEngineIconPalette.TrySetPaletteId(_currentId, _partyPaletteIndex, out _);
                else
                    DSPRE.DSUtils.SetMonIconPaletteId(IconIdFor(_currentId), (byte)_partyPaletteIndex);
                if (_pendingIconGraphic != null)
                {
                    DSPRE.DSUtils.SetMonIconGraphic(IconIdFor(_currentId), _partyPaletteIndex, _pendingIconGraphic);
                    _pendingIconGraphic = null;
                }
                SaveSpriteData(); SaveFormHeights(); SaveAnim(); SaveFrames(); SetClean();
                RefreshPreview();   // now reflects what was actually written (disk read), not the staged import
            }
            catch { /* surfaced by the global error net */ }
        }
    }

    /// <summary>One pattern-animation step (pokeanm ssanm): which sprite frame to show and for how many 1/30s units. <see cref="Frame"/> is the cell/pattern number (the sheet has frames 0 and 1).</summary>
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

    /// <summary>One hg-engine SpriteFrame slot (data/SpriteOffsets.c's .frontFrames/.backFrames): which raw
    /// sprite frame to show, how long, and its per-frame pixel shift. FrameNo = -1 means "unused."</summary>
    public sealed class SpriteFrameEntry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private int _frameNo = -1; public int FrameNo { get => _frameNo; set { if (_frameNo != value) { _frameNo = value; Raise(nameof(FrameNo)); } } }
        private int _duration; public int Duration { get => _duration; set { if (_duration != value) { _duration = value; Raise(nameof(Duration)); } } }
        private int _horizontalShift; public int HorizontalShift { get => _horizontalShift; set { if (_horizontalShift != value) { _horizontalShift = value; Raise(nameof(HorizontalShift)); } } }
        private int _verticalShift; public int VerticalShift { get => _verticalShift; set { if (_verticalShift != value) { _verticalShift = value; Raise(nameof(VerticalShift)); } } }
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
        public string ArgHint
        {
            get
            {
                var names = DSPRE.Avalonia.Data.PokeAnimScript.ArgNames(Op);
                if (names.Length > 0) return string.Join(", ", names);
                int n = DSPRE.Avalonia.Data.PokeAnimScript.ArgsFor(Op);
                return n == 0 ? "(no args)" : $"{n} arg(s)";
            }
        }
    }
}
