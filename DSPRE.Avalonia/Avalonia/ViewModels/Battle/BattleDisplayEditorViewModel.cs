using Avalonia.Media;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
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
            StopPlayback();
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
        public double ArenaGroundMineLeft   { get => _arenaGroundMineLeft;   private set { if (Set(ref _arenaGroundMineLeft, value)) OnPropertyChanged(nameof(ArenaGroundMineX)); } }
        public double ArenaGroundMineTop    { get => _arenaGroundMineTop;    private set => Set(ref _arenaGroundMineTop, value); }
        public double ArenaGroundEnemyLeft  { get => _arenaGroundEnemyLeft;  private set { if (Set(ref _arenaGroundEnemyLeft, value)) OnPropertyChanged(nameof(ArenaGroundEnemyX)); } }
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
                    // Two copies side by side so the intro scroll wraps without a seam.
                    _backdropWidth = Math.Max(256, backdropImg.Width);
                    ArenaBackdrop = RgbaToBitmap(TileBackdropRgba(backdropImg.Rgba, backdropImg.Width, backdropImg.Height, _backdropWidth), _backdropWidth * 2, 192);
                    OnPropertyChanged(nameof(BackdropX));
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
        /// <summary>The gauge picture is 256 wide, centred on the bar.</summary>
        public double PlayerGaugeImageLeft => Data.BattleGaugeComposer.CentreOf(Data.BattleGaugeComposer.Kind.PlayerSingle).X - 128;
        public double PlayerHealthFillLeft => Data.BattleGaugeComposer.HealthFillLeft(Data.BattleGaugeComposer.Kind.PlayerSingle);

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

        // The name and level as the game itself draws them, rather than typed out in a desktop font.
        // Shared with the other two battle previews so all three show the same thing.
        public bool GaugeTextIsReal => Data.GaugeTextImages.Available;
        public global::Avalonia.Media.Imaging.Bitmap GaugeNameImage => Data.GaugeTextImages.Name(GaugeNameText);
        public global::Avalonia.Media.Imaging.Bitmap GaugeLevelImage =>
            Data.GaugeTextImages.Level(5, ROMFiles.BattleGaugeText.Gender.Genderless);

        private void RenderGauges()
        {
            try
            {
                // The name and level go into each bar's own picture, the way a battle writes them.
                // Games whose letters cannot be read fall back to the plain bar.
                var r = _groundRenderer ??= new BattleGroundRenderer();
                GaugePlayerImage = Data.GaugeTextImages.Bar(true, GaugeNameText, 5)
                                ?? GaugeToBitmap(r.BuildGauge(true));
                GaugeEnemyImage = Data.GaugeTextImages.Bar(false, GaugeNameText, 5)
                               ?? GaugeToBitmap(r.BuildGauge(false));
            }
            catch { GaugePlayerImage = GaugeEnemyImage = null; }
            OnPropertyChanged(nameof(HasRealGauges));
            OnPropertyChanged(nameof(PlaceholderGaugesVisible));
            OnPropertyChanged(nameof(PlayerGaugeImageLeft));
            OnPropertyChanged(nameof(PlayerHealthFillLeft));
            OnPropertyChanged(nameof(GaugeTextBrush));
            foreach (var n in new[] { nameof(GaugeTextIsReal), nameof(GaugeNameImage), nameof(GaugeLevelImage) })
                OnPropertyChanged(n);
            RenderMessageBox();
        }

        // The Battle Screen editor's box and font, in the default window style.
        private Bitmap _messageBoxImage;
        private bool _messageBoxTried;
        public Bitmap MessageBoxImage { get => _messageBoxImage; private set => Set(ref _messageBoxImage, value); }
        public bool HasRealMessageBox => _messageBoxImage != null;
        public bool PlaceholderMessageBoxVisible => !HasRealMessageBox;
        public string MessageBoxText => MessageLines().First;
        public string MessageBoxLine2 => MessageLines().Second;

        // During a send-out the box shows whatever the game has printed so far, broken where the game breaks it.
        private (string First, string Second) MessageLines()
        {
            if (_mode != PreviewMode.SendOut || _sendOut == null) return ($"What will {GaugeNameText} do?", "");
            string text = _sendOut.MessageText ?? "";
            int nl = text.IndexOf('\n');
            return nl < 0 ? (text, "") : (text.Substring(0, nl), text.Substring(nl + 1));
        }

        // The words of each message, as the game fills them in.
        private string MessageFor(SendOutMessage message)
        {
            string mon = GaugeNameText;
            string trainer = string.Join(" ", new[] { _trainer.ClassName, _trainer.Name }).Trim();
            if (trainer.Length == 0) trainer = "Trainer";
            return message switch
            {
                SendOutMessage.Challenged => $"You are challenged by\n{trainer}!",
                SendOutMessage.WildAppeared => $"A wild {mon} appeared!",
                SendOutMessage.EnemySentOut => $"{trainer} sent\nout {mon}!",
                SendOutMessage.Go => $"Go! {mon}!",
                _ => "",
            };
        }

        // The box's colours fade in from black at the start of an intro.
        private double _messageBoxDim;
        public double MessageBoxDim { get => _messageBoxDim; private set => Set(ref _messageBoxDim, value); }

        private void RaiseMessage()
        {
            OnPropertyChanged(nameof(MessageBoxText));
            OnPropertyChanged(nameof(MessageBoxLine2));
        }

        private void RenderMessageBox()
        {
            if (!_messageBoxTried && IsAvailable)
            {
                _messageBoxTried = true;
                try
                {
                    Views.Controls.FieldMessageBoxView.Font ??= FieldFont.LoadTalkFont();
                    var box = BattleScreenRenderer.BuildMessageBox(0);
                    MessageBoxImage = box.Rgba != null ? DSPRE.Avalonia.ImageConverter.FromRgba(box.Rgba, box.Width, box.Height) : null;
                }
                catch { MessageBoxImage = null; }
                OnPropertyChanged(nameof(HasRealMessageBox));
                OnPropertyChanged(nameof(PlaceholderMessageBoxVisible));
            }
            RaiseMessage();
        }

        // GroundImage (256² straight RGBA) -> unpremultiplied BGRA; the frame has transparency.
        private static Bitmap GaugeToBitmap(BattleGroundRenderer.GroundImage g)
        {
            if (g?.Rgba == null) return null;
            return RgbaToBitmap(g.Rgba, g.Width, g.Height);
        }

        // RGBA w×h (battle BG, 512×256) -> two copies of its top width×192 side by side, black-padded if smaller.
        private static byte[] TileBackdropRgba(byte[] rgba, int w, int h, int width)
        {
            int outW = width * 2;
            var outp = new byte[outW * 192 * 4];
            for (int y = 0; y < 192 && y < h; y++)
                for (int x = 0; x < outW; x++)
                {
                    int sx = x % width;
                    if (sx >= w) continue;
                    int si = (y * w + sx) * 4, di = (y * outW + x) * 4;
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

        // The sheet is two 80×80 frames; each side plays its own frame run once.
        private List<SpriteFrameSlot> _frontSlots, _backSlots;
        private readonly SpriteFramePlayer _frontFrames = new SpriteFramePlayer();
        private readonly SpriteFramePlayer _backFrames = new SpriteFramePlayer();
        private int _frontFrame, _backFrame;
        private int _frontShift, _backShift;

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
            if (_isShiny) return female ? Pick(s.BattleFrontFShiny, s.BattleFrontMShiny, _frontFrame) : Pick(s.BattleFrontMShiny, s.BattleFrontFShiny, _frontFrame);
            return female ? Pick(s.BattleFrontF, s.BattleFrontM, _frontFrame) : Pick(s.BattleFrontM, s.BattleFrontF, _frontFrame);
        }
        private Bitmap Back(bool female)
        {
            var s = _sprites; if (s == null) return null;
            if (_isShiny) return female ? Pick(s.BattleBackFShiny, s.BattleBackMShiny, _backFrame) : Pick(s.BattleBackMShiny, s.BattleBackFShiny, _backFrame);
            return female ? Pick(s.BattleBackF, s.BattleBackM, _backFrame) : Pick(s.BattleBackM, s.BattleBackF, _backFrame);
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

        private List<int> ActivePatternFrames(bool front) =>
            SpriteFramePlayer.ReachableFrames(front ? _frontSlots : _backSlots).Select(ClampFrame).Distinct().ToList();

        private bool FrameWarning(bool front, bool female) => BlankFrameIndex(PoseCell(front, female)) >= 0;

        // True only when the animation never shows the real frame, so the sprite would be invisible the whole run.
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

        // A frame's horizontalShift moves the sprite and its shadow together (both read transforms.xOffset).
        public double EnemyLeft => 152 + _frontShift;
        public double PlayerLeft => 23 + _backShift;
        public double EnemyTop => FrontTopFor(ActFrontH(ShowFemale));
        public double PlayerTop => BackTopFor(ActBackH(ShowFemale));
        public double EnemyTopM => FrontTopFor(ActFrontH(false));
        public double EnemyTopF => FrontTopFor(ActFrontH(true));
        public double PlayerTopM => BackTopFor(ActBackH(false));
        public double PlayerTopF => BackTopFor(ActBackH(true));

        public bool ShadowSmallVisible => HasSpriteData && _enemyShown && _shadowSize == 1;
        public bool ShadowMediumVisible => HasSpriteData && _enemyShown && _shadowSize == 2;
        public bool ShadowLargeVisible => HasSpriteData && _enemyShown && _shadowSize == 3;
        // In battle the shadow follows the sprite's X, movement included, but not its Y or its script's scale.
        public double ShadowSmallLeft => 179 + _shadowX + _frontShift + _animOffsetX + _enemySlideX;
        public double ShadowMediumLeft => 174 + _shadowX + _frontShift + _animOffsetX + _enemySlideX;
        public double ShadowLargeLeft => 167 + _shadowX + _frontShift + _animOffsetX + _enemySlideX;

        private void RaiseLayout()
        {
            OnPropertyChanged(nameof(EnemyLeft)); OnPropertyChanged(nameof(PlayerLeft));
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
        private int _savedPartyPaletteIndex;
        public int PartyPaletteIndex
        {
            get => _partyPaletteIndex;
            set
            {
                // The ComboBox reports -1 while it refills.
                if (value < 0 || value >= PartyPalettes.Count)
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(PartyPaletteIndex)));
                    return;
                }
                if (Set(ref _partyPaletteIndex, value)) { if (!_loading) SetDirty(); RefreshPreview(); }
            }
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
        // HGSS and Platinum keep offsets and animation in one 89-byte record (see SpeciesSpriteData); DP and
        // Platinum add height.narc, and DP keeps poke_yofs, poke_shadow_ofx, poke_shadow and pokeanm.
        private IBattleOffsetSource _src;
        private bool _srcTried;

        // Platinum and HGSS keep offsets, animation and frame runs in one record. Everything that edits it
        // goes through this one cache, because each write saves the whole archive from its own copy.
        private OffsetNarc _recordNarc;
        private static bool RecordFamily => gameFamily == GameFamilies.Plat || gameFamily == GameFamilies.HGSS;

        private void EnsureSource()
        {
            if (_srcTried) return;
            _srcTried = true;
            try
            {
                if (RecordFamily) _recordNarc = new OffsetNarc(DirNames.pokemonSpriteOffsets, SpeciesSpriteData.Size);
                _src = gameFamily switch
                {
                    GameFamilies.HGSS or GameFamilies.Plat => new CombinedTailSource(_recordNarc, withHeights: true),
                    GameFamilies.DP => new SeparateByteSource(DirNames.pokeYofs, DirNames.pokeShadowOfx, DirNames.pokeShadow),
                    _ => null,
                };
            }
            catch { _src = null; }
        }

        private SpeciesSpriteData ReadRecord()
        {
            EnsureSource();
            return SpeciesSpriteData.Parse(_recordNarc?.GetRecord(BaseSpeciesIdFor(_currentId)));
        }

        private void EditRecord(Action<SpeciesSpriteData> edit)
        {
            var rec = ReadRecord();
            if (rec == null) return;
            edit(rec);
            _recordNarc.PutRecord(BaseSpeciesIdFor(_currentId), rec.ToBytes());
        }

        /// <summary>True when this mon has a sprite-coordinate record (enables those fields).</summary>
        private bool _hasSpriteData;
        public bool HasSpriteData
        {
            get => _hasSpriteData;
            // The frames panel is gated on this too, and it is set after the entries are loaded.
            private set { if (Set(ref _hasSpriteData, value)) OnPropertyChanged(nameof(HasFrameRuns)); }
        }

        /// <summary>True where per-gender sprite heights exist (DP, Platinum; height.narc).</summary>
        private bool _hasHeights;
        public bool HasHeights { get => _hasHeights; private set { if (Set(ref _hasHeights, value)) OnPropertyChanged(nameof(ShowBaseHeights)); } }

        /// <summary>True where this mon has animation numbers and delays to edit.</summary>
        private bool _hasAnimData;
        public bool HasAnimData { get => _hasAnimData; private set => Set(ref _hasAnimData, value); }

        // hg-engine keeps this data in source files, so _src is never built there. Requiring it left every
        // offset, shadow and height edit silently non-dirty, and an editor that never reports unsaved
        // changes is never asked to save: those edits were dropped on close or on switching mon.
        private bool CanEditSprite => (_src != null || HgEngineProject.IsActive) && _hasSpriteData && !_loading;

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

        // ── Battle-sprite animation: movement script numbers, start delays and cry delays ──────────────
        // Platinum and HGSS read these from the sprite record, hg-engine from SpriteOffsets.c, DP from pokeanm.
        private const int ANIM_REC_LEN = 28, ANIM_PAT_OFFSET = 8, ANIM_PAT_MAX = 10;
        private OffsetNarc _animNarc;
        private bool _animNarcTried;

        private int _animFrontProg; public int AnimFrontProgNum { get => _animFrontProg; set { if (Set(ref _animFrontProg, value)) { if (!_loading) SetDirty(); if (_scriptTarget == 0) RefreshProgramScript(); else OnPropertyChanged(nameof(ProgramScriptHeader)); } } }
        private int _animFrontWait; public int AnimFrontWait { get => _animFrontWait; set { if (Set(ref _animFrontWait, value) && !_loading) SetDirty(); } }

        // Platinum, HGSS and hg-engine only: each face also carries the delay before its cry.
        private int _animFrontCryDelay; public int AnimFrontCryDelay { get => _animFrontCryDelay; set { if (Set(ref _animFrontCryDelay, value) && !_loading) SetDirty(); } }
        private int _animBackCryDelay; public int AnimBackCryDelay { get => _animBackCryDelay; set { if (Set(ref _animBackCryDelay, value) && !_loading) SetDirty(); } }

        /// <summary>The back animation ({number, start delay}). One entry, except DP's three pokeanm slots.</summary>
        public ObservableCollection<AnimProgStep> AnimBack { get; } = new ObservableCollection<AnimProgStep>();
        public string AnimBackLabel => RecordFamily ? "Back animation (# and start delay)" : "Back program steps";
        /// <summary>DP's pattern (frame) steps. Platinum, HGSS and hg-engine use the frame runs instead.</summary>
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
            _animFrontCryDelay = _animBackCryDelay = 0;
            OnPropertyChanged(nameof(AnimBackLabel));
            if (!IsAvailable || id < 0) { OnPropertyChanged(nameof(CanAddAnimStep)); return; }

            if (HgEngineProject.IsActive) { LoadAnimFromHgeSource(_currentHgeSpeciesId); return; }
            if (RecordFamily) { LoadAnimFromRecord(); return; }

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
            RecomputePatternSlots();
            OnPropertyChanged(nameof(AnimFrontProgNum)); OnPropertyChanged(nameof(AnimFrontWait)); OnPropertyChanged(nameof(CanAddAnimStep));
            HasAnimData = true;
            RefreshProgramScript();
        }

        private void LoadAnimFromRecord()
        {
            var rec = ReadRecord();
            if (rec == null) { OnPropertyChanged(nameof(CanAddAnimStep)); return; }
            _animFrontProg = rec.Front.Animation; _animFrontWait = rec.Front.StartDelay; _animFrontCryDelay = rec.Front.CryDelay;
            AddBackStep(rec.Back.Animation, rec.Back.StartDelay);
            _animBackCryDelay = rec.Back.CryDelay;
            RaiseAnimFields();
        }

        // AnimSteps stays empty: the frame runs are loaded with the sprite data in LoadSpriteDataFromHgeSource.
        private void LoadAnimFromHgeSource(int id)
        {
            if (!HgEngineSpriteOffsets.TryLoad(id, out var block, out _)) { OnPropertyChanged(nameof(CanAddAnimStep)); return; }
            if (!block.TryGetInt(new[] { FieldPathSegment.Field("frontHeader"), FieldPathSegment.Field("animation") }, out _animFrontProg)) { OnPropertyChanged(nameof(CanAddAnimStep)); return; }
            block.TryGetInt(new[] { FieldPathSegment.Field("frontHeader"), FieldPathSegment.Field("animationDelay") }, out _animFrontWait);
            block.TryGetInt(new[] { FieldPathSegment.Field("frontHeader"), FieldPathSegment.Field("cryDelay") }, out _animFrontCryDelay);
            block.TryGetInt(new[] { FieldPathSegment.Field("backHeader"), FieldPathSegment.Field("animation") }, out int backProg);
            block.TryGetInt(new[] { FieldPathSegment.Field("backHeader"), FieldPathSegment.Field("animationDelay") }, out int backWait);
            block.TryGetInt(new[] { FieldPathSegment.Field("backHeader"), FieldPathSegment.Field("cryDelay") }, out _animBackCryDelay);
            foreach (var s in AnimBack) s.PropertyChanged -= OnAnimStepChanged;
            AnimBack.Clear();
            AddBackStep(backProg, backWait);
            RaiseAnimFields();
        }

        private void RaiseAnimFields()
        {
            OnPropertyChanged(nameof(AnimFrontProgNum)); OnPropertyChanged(nameof(AnimFrontWait));
            OnPropertyChanged(nameof(AnimFrontCryDelay)); OnPropertyChanged(nameof(AnimBackCryDelay));
            OnPropertyChanged(nameof(CanAddAnimStep));
            HasAnimData = true;
            RefreshProgramScript();
        }

        private void SaveAnim()
        {
            if (!_hasAnimData) return;
            int backProg = AnimBack.Count > 0 ? AnimBack[0].Number : 0;
            int backWait = AnimBack.Count > 0 ? AnimBack[0].Wait : 0;

            if (HgEngineProject.IsActive)
            {
                var fields = new[]
                {
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("frontHeader"), FieldPathSegment.Field("animation") }, _animFrontProg.ToString()),
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("frontHeader"), FieldPathSegment.Field("animationDelay") }, _animFrontWait.ToString()),
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("frontHeader"), FieldPathSegment.Field("cryDelay") }, _animFrontCryDelay.ToString()),
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("backHeader"), FieldPathSegment.Field("animation") }, backProg.ToString()),
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("backHeader"), FieldPathSegment.Field("animationDelay") }, backWait.ToString()),
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("backHeader"), FieldPathSegment.Field("cryDelay") }, _animBackCryDelay.ToString()),
                };
                HgEngineWriter.TryWriteFields(HgEngineDomain.SpriteOffsets, _currentHgeSpeciesId, fields, out _, out _);
                return;
            }

            if (RecordFamily)
            {
                EditRecord(rec =>
                {
                    rec.Front.Animation = _animFrontProg; rec.Front.StartDelay = _animFrontWait; rec.Front.CryDelay = _animFrontCryDelay;
                    rec.Back.Animation = backProg; rec.Back.StartDelay = backWait; rec.Back.CryDelay = _animBackCryDelay;
                });
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
        private void OnAnimStepChanged(object _, PropertyChangedEventArgs __) { if (!_loading) { SetDirty(); RecomputePatternSlots(); } }

        public void AddAnimStep()
        {
            if (AnimSteps.Count >= ANIM_PAT_MAX) return;
            AddPatternStep(0, 4);
            OnPropertyChanged(nameof(CanAddAnimStep));
            if (!_loading) { SetDirty(); RecomputePatternSlots(); }
        }
        public void RemoveAnimStep(AnimPatternStep step)
        {
            if (step == null || !AnimSteps.Contains(step)) return;
            step.PropertyChanged -= OnAnimStepChanged;
            AnimSteps.Remove(step);
            OnPropertyChanged(nameof(CanAddAnimStep));
            if (!_loading) { SetDirty(); RecomputePatternSlots(); }
        }

        // DP drives both sprites from its one pattern list; the other families have their own frame runs.
        private void RecomputePatternSlots()
        {
            if (RecordFamily) return;
            var slots = AnimSteps.Select(s => new SpriteFrameSlot(s.Frame, s.Wait, 0, 0)).ToList();
            SetFrameSlots(slots, slots);
        }

        // ── Frame runs (Platinum/HGSS sprite record, hg-engine .frontFrames/.backFrames; 10 slots each) ──
        // Fixed size, no add/remove. FrameNo -1 ends the run.
        public ObservableCollection<SpriteFrameEntry> FrontFrameEntries { get; } = new ObservableCollection<SpriteFrameEntry>();
        public ObservableCollection<SpriteFrameEntry> BackFrameEntries { get; } = new ObservableCollection<SpriteFrameEntry>();
        public bool HasFrameRuns => RecordFamily && _hasSpriteData;

        private void LoadFrameEntries(IEnumerable<SpriteFrameSlot> front, IEnumerable<SpriteFrameSlot> back)
        {
            foreach (var e in FrontFrameEntries) e.PropertyChanged -= OnFrameEntryChanged;
            foreach (var e in BackFrameEntries) e.PropertyChanged -= OnFrameEntryChanged;
            FrontFrameEntries.Clear(); BackFrameEntries.Clear();

            foreach (var slot in front) AddFrameEntry(FrontFrameEntries, slot);
            foreach (var slot in back) AddFrameEntry(BackFrameEntries, slot);

            SetFrameSlots(ToSlotData(FrontFrameEntries), ToSlotData(BackFrameEntries));
            OnPropertyChanged(nameof(HasFrameRuns));
        }

        private void AddFrameEntry(ObservableCollection<SpriteFrameEntry> list, SpriteFrameSlot slot)
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
            SetFrameSlots(ToSlotData(FrontFrameEntries), ToSlotData(BackFrameEntries));
        }

        // The player needs every slot, not the prefix before the first negative one: a frameNo below -1
        // is a counted jump, and the -1 terminator is what ends the run on frame 0.
        private void SetFrameSlots(List<SpriteFrameSlot> front, List<SpriteFrameSlot> back)
        {
            _frontSlots = front;
            _backSlots = back;
            StopPlayback();
            RaiseFrameWarnings();
        }

        private void SaveFrames()
        {
            if (!RecordFamily || !_hasSpriteData) return;
            var front = ToSlotData(FrontFrameEntries);
            var back = ToSlotData(BackFrameEntries);

            if (HgEngineProject.IsActive)
            {
                var fields = new List<HgEngineFieldWrite>();
                fields.AddRange(HgEngineSpriteOffsets.BuildFrameWrites("frontFrames", front));
                fields.AddRange(HgEngineSpriteOffsets.BuildFrameWrites("backFrames", back));
                HgEngineWriter.TryWriteFields(HgEngineDomain.SpriteOffsets, _currentHgeSpeciesId, fields, out _, out _);
                return;
            }

            EditRecord(rec =>
            {
                for (int i = 0; i < SpeciesSpriteData.FrameCount; i++)
                {
                    if (i < front.Count) rec.Front.Frames[i] = front[i];
                    if (i < back.Count) rec.Back.Frames[i] = back[i];
                }
            });
        }

        private static List<SpriteFrameSlot> ToSlotData(ObservableCollection<SpriteFrameEntry> entries)
        {
            var slots = new List<SpriteFrameSlot>(entries.Count);
            foreach (var e in entries)
                slots.Add(new SpriteFrameSlot(e.FrameNo, e.Duration, e.HorizontalShift, e.VerticalShift));
            return slots;
        }

        // ── Playback: frame runs, movement scripts, or the whole send-out, once, on the game's clock ─────
        private enum PreviewMode { None, Frames, Animation, SendOut }
        private PreviewMode _mode;
        private OffsetNarc _animDefsNarc;
        private bool _animDefsTried;
        private PokeAnimPlayer _prog, _progBack;
        private readonly System.Diagnostics.Stopwatch _clock = new System.Diagnostics.Stopwatch();
        private long _ticksRun;

        // Only the live editor makes sound; a view model built for tests stays quiet.
        private readonly bool _sound;

        public bool IsPlaying => _mode != PreviewMode.None;
        public string FramesButtonText => ButtonText(PreviewMode.Frames, "▶ Play frames");
        public string AnimationButtonText => ButtonText(PreviewMode.Animation, "▶ Play animation");
        public string SendOutButtonText => _sendOutLoading ? "Loading…" : ButtonText(PreviewMode.SendOut, "▶ Play send-out");
        private string ButtonText(PreviewMode mode, string idle) => _mode == mode ? "⏹ Stop" : idle;
        public bool CanPlay => _hasAnimData;

        /// <summary>Which sprites the three Play buttons drive. Shared, so the choice follows between tabs.</summary>
        public IReadOnlyList<string> SideOptions { get; } = new[] { "Both sides", "Theirs (front)", "Yours (back)" };
        private int _sideIndex;
        public int SideIndex { get => _sideIndex; set { if (value >= 0 && Set(ref _sideIndex, value)) { StopPlayback(); RaiseAdvanced(); } } }
        private PreviewSides Sides => (PreviewSides)_sideIndex;

        public IReadOnlyList<string> SendOutKindOptions { get; } = new[] { "Wild battle", "Trainer battle" };
        private int _sendOutKindIndex = 1;
        public int SendOutKindIndex
        {
            get => _sendOutKindIndex;
            set { if (value >= 0 && Set(ref _sendOutKindIndex, value)) { StopPlayback(); RaiseAdvanced(); RefreshMusic(); } }
        }
        public bool IsTrainerBattle => _sendOutKindIndex == (int)SendOutKind.Trainer;

        // ── Advanced send-out options ─────────────────────────────────────────────────────────────
        private bool _advancedOpen;
        public bool AdvancedOpen
        {
            get => _advancedOpen;
            set { if (Set(ref _advancedOpen, value) && value) { _ = LoadTrainersAsync(); RefreshMusic(); } }
        }

        /// <summary>The ball thrown: its graphic, burst and the colour the Pokémon appears in.</summary>
        public ObservableCollection<string> BallOptions { get; } = new ObservableCollection<string>();
        private readonly List<int> _ballIds = new List<int>();
        private int _ballIndex;
        public int BallIndex { get => _ballIndex; set { if (value >= 0 && Set(ref _ballIndex, value)) StopPlayback(); } }
        private int SelectedBall => _ballIndex >= 0 && _ballIndex < _ballIds.Count ? _ballIds[_ballIndex] : 4;
        /// <summary>A ball is thrown in a trainer battle, and by you in a wild one.</summary>
        public bool BallMatters => IsTrainerBattle || Sides != PreviewSides.Theirs;

        private void LoadBallOptions()
        {
            _ballIds.Clear();
            var names = new List<string>();
            foreach (var (ball, name) in SendOutGraphics.Balls()) { _ballIds.Add(ball); names.Add(name); }
            ListSync.Apply(BallOptions, names);
            _ballIndex = Math.Max(0, _ballIds.IndexOf(4));   // Poké Ball
            OnPropertyChanged(nameof(BallIndex));
        }

        /// <summary>Every trainer as "id: Class Name", filled when first needed.</summary>
        public ObservableCollection<string> TrainerOptions { get; } = new ObservableCollection<string>();
        private int _trainerIndex = -1;
        private bool _trainersLoading, _trainersLoaded;
        public int TrainerIndex
        {
            get => _trainerIndex;
            set
            {
                if (value < 0 || !Set(ref _trainerIndex, value)) return;
                StopPlayback();
                _trainer = SendOutGraphics.TrainerInfo(value);
                RefreshMusic();
            }
        }
        private (int Class, string ClassName, string Name) _trainer = (-1, "", "");

        private async System.Threading.Tasks.Task LoadTrainersAsync()
        {
            if (_trainersLoading || _trainersLoaded || !IsAvailable) return;
            _trainersLoading = true;
            List<string> list = null;
            int initial = 1;
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    DSPRE.DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerProperties, DirNames.textArchives });
                    list = SendOutGraphics.TrainerList();
                    if (_trainerIndex < 0) initial = SendOutGraphics.DefaultTrainer();
                });
            }
            catch (Exception ex) { AppLogger.Error("Send-out preview could not list trainers: " + ex.Message); }
            _trainersLoading = false;
            if (list == null || list.Count == 0) return;
            _trainersLoaded = true;
            ListSync.Apply(TrainerOptions, list);
            if (_trainerIndex < 0 || _trainerIndex >= list.Count)
            {
                _trainerIndex = Math.Clamp(initial, 0, list.Count - 1);
                _trainer = SendOutGraphics.TrainerInfo(_trainerIndex);
            }
            OnPropertyChanged(nameof(TrainerIndex));
            RefreshMusic();
        }

        private bool _playTrainerIntro = true;
        /// <summary>Off plays the send-out a trainer makes mid-battle: no trainers, no slide.</summary>
        public bool PlayTrainerIntro { get => _playTrainerIntro; set { if (Set(ref _playTrainerIntro, value)) { StopPlayback(); RaiseAdvanced(); } } }

        private bool _trainerSlideIn;
        public bool TrainerSlideIn { get => _trainerSlideIn; set { if (Set(ref _trainerSlideIn, value)) StopPlayback(); } }

        private bool _showPartyBalls = true;
        public bool ShowPartyBalls { get => _showPartyBalls; set { if (Set(ref _showPartyBalls, value)) { StopPlayback(); RaiseAdvanced(); } } }

        private int _partyBallCount = 3;
        public int PartyBallCount { get => _partyBallCount; set { if (Set(ref _partyBallCount, Math.Clamp(value, 1, 6))) StopPlayback(); } }

        private bool _isShiny;
        /// <summary>Shows both sprites in their shiny colours, and a send-out plays the sparkle.</summary>
        public bool IsShiny { get => _isShiny; set { if (Set(ref _isShiny, value)) { StopPlayback(); RaiseSprites(); } } }

        // Diamond and Pearl pick one of three back animations by nature.
        private static readonly byte[] DpBackSlotForNature =
            { 0, 2, 0, 0, 0, 1, 1, 1, 0, 1, 2, 0, 1, 0, 0, 2, 2, 2, 2, 1, 1, 2, 1, 2, 1 };
        public bool CanPickNature => gameFamily == GameFamilies.DP && !HgEngineProject.IsActive;
        public IReadOnlyList<string> NatureOptions { get; } = DVCalculator.Natures.Select(n => n.Split(':')[0]).ToArray();
        private int _natureIndex;
        public int NatureIndex { get => _natureIndex; set { if (value >= 0 && Set(ref _natureIndex, value)) StopPlayback(); } }
        private int BackSlot => CanPickNature ? DpBackSlotForNature[Math.Clamp(_natureIndex, 0, DpBackSlotForNature.Length - 1)] : 0;

        public IReadOnlyList<string> TextSpeedOptions { get; } = new[] { "Slow text", "Mid text", "Fast text", "Instant text" };
        private int _textSpeedIndex = (int)TextSpeed.Mid;
        public int TextSpeedIndex { get => _textSpeedIndex; set { if (value >= 0 && Set(ref _textSpeedIndex, value)) StopPlayback(); } }

        private bool _playMusic = true;
        public bool PlayBattleMusic { get => _playMusic; set { if (Set(ref _playMusic, value)) { StopPlayback(); RefreshMusic(); } } }

        private bool _kantoMusic;
        /// <summary>HGSS plays the Kanto versions of its standard themes in Kanto.</summary>
        public bool KantoMusic { get => _kantoMusic; set { if (Set(ref _kantoMusic, value)) { StopPlayback(); RefreshMusic(); } } }
        public bool ShowKantoMusic => gameFamily == GameFamilies.HGSS;

        private string _battleMusicText = "";
        public string BattleMusicText { get => _battleMusicText; private set => Set(ref _battleMusicText, value); }

        public bool CanChooseTrainer => IsTrainerBattle;
        public bool CanChooseIntro => IsTrainerBattle;
        public bool CanSlideIn => IsTrainerBattle && _playTrainerIntro;
        public bool CanShowPartyBalls => IsTrainerBattle;
        public bool CanCountPartyBalls => IsTrainerBattle && _showPartyBalls;

        private void RaiseAdvanced()
        {
            foreach (var n in new[] { nameof(IsTrainerBattle), nameof(BallMatters), nameof(CanChooseTrainer), nameof(CanChooseIntro),
                                      nameof(CanSlideIn), nameof(CanShowPartyBalls), nameof(CanCountPartyBalls) })
                OnPropertyChanged(n);
        }

        // ── Send-out scene: what the sequence moves besides the two Pokémon ──────────────────────────
        private SendOutSequence _sendOut;
        private SendOutGraphics _gfx;
        private SpaParticlePreview _enemyBurst, _playerBurst;
        private WestPlayer _enemySparkle, _playerSparkle;
        private CellActor _enemyBallActor, _playerBallActor;
        private bool _enemyBallRolling, _playerBallRolling;
        private readonly CellActor[] _enemyRowActors = new CellActor[6], _playerRowActors = new CellActor[6];
        private int _sendOutBall;
        private System.Threading.Tasks.Task<short[]> _cry, _ballOpenSound;
        private object _musicHandle;
        private int _musicVersion;

        public SceneSprite EnemyTrainerSprite { get; } = new SceneSprite();
        public SceneSprite PlayerTrainerSprite { get; } = new SceneSprite();
        public SceneSprite EnemyBallSprite { get; } = new SceneSprite();
        public SceneSprite PlayerBallSprite { get; } = new SceneSprite();

        private Bitmap _enemyBurstImage, _playerBurstImage, _enemySparkleImage, _playerSparkleImage, _partyRowsImage;
        public Bitmap EnemyBurstImage { get => _enemyBurstImage; private set => Set(ref _enemyBurstImage, value); }
        public Bitmap PlayerBurstImage { get => _playerBurstImage; private set => Set(ref _playerBurstImage, value); }
        public Bitmap EnemySparkleImage { get => _enemySparkleImage; private set => Set(ref _enemySparkleImage, value); }
        public Bitmap PlayerSparkleImage { get => _playerSparkleImage; private set => Set(ref _playerSparkleImage, value); }
        public Bitmap PartyRowsImage { get => _partyRowsImage; private set => Set(ref _partyRowsImage, value); }

        private bool _enemyShown = true, _playerShown = true;
        public bool EnemyShown { get => _enemyShown; private set { if (Set(ref _enemyShown, value)) RaiseShadows(); } }
        public bool PlayerShown { get => _playerShown; private set => Set(ref _playerShown, value); }

        // The wild slide moves the sprite and its shadow; the grow-in scales both.
        private double _enemySlideX, _enemyGrow = 1, _playerGrow = 1;
        private double _enemyTint, _playerTint;
        private uint _enemyTintRgb, _playerTintRgb;

        private int _enemyPlatformX, _playerPlatformX, _backdropX, _enemyGaugeX, _playerGaugeX;
        private bool _enemyGaugeShown = true, _playerGaugeShown = true;
        public double ArenaGroundEnemyX => _arenaGroundEnemyLeft + _enemyPlatformX;
        public double ArenaGroundMineX => _arenaGroundMineLeft + _playerPlatformX;
        public double PlaceholderEnemyPlatformX => 129 + _enemyPlatformX;
        public double PlaceholderPlayerPlatformX => -42 + _playerPlatformX;
        public double BackdropX => _backdropX - _backdropWidth;
        private int _backdropWidth = 256;
        public double EnemyGaugeX => _enemyGaugeX;
        public double PlayerGaugeX => _playerGaugeX;
        public bool EnemyGaugeShown => _enemyGaugeShown;
        public bool PlayerGaugeShown => _playerGaugeShown;

        public double ShadowGrow => _enemyGrow;
        // The shadow sits under the sprite's scaled anchor, so it drops while the sprite is small.
        public double ShadowGrowY => (40 - _spriteY) * (1 - _enemyGrow);

        // Live front-sprite transform. The parts are kept because the shadow follows only X.
        private double _animOffsetX, _animOffsetY, _animScaleX = 1, _animScaleY = 1, _animRotation, _animFadeOpacity;
        public double AnimOffsetX
        {
            get => _animOffsetX;
            private set { if (Set(ref _animOffsetX, value)) { OnPropertyChanged(nameof(ShadowSmallLeft)); OnPropertyChanged(nameof(ShadowMediumLeft)); OnPropertyChanged(nameof(ShadowLargeLeft)); } }
        }
        public double AnimOffsetY { get => _animOffsetY; private set => Set(ref _animOffsetY, value); }
        public double AnimScaleX { get => _animScaleX; private set => Set(ref _animScaleX, value); }
        public double AnimScaleY { get => _animScaleY; private set => Set(ref _animScaleY, value); }
        public double AnimRotation { get => _animRotation; private set => Set(ref _animRotation, value); }
        private global::Avalonia.Matrix _animMatrix = global::Avalonia.Matrix.Identity;
        public global::Avalonia.Matrix AnimMatrix { get => _animMatrix; private set => Set(ref _animMatrix, value); }
        public double AnimFadeOpacity { get => _animFadeOpacity; private set => Set(ref _animFadeOpacity, value); }
        private IBrush _animFadeBrush = Brushes.Transparent;
        public IBrush AnimFadeBrush { get => _animFadeBrush; private set => Set(ref _animFadeBrush, value); }

        // Same, for the player/back sprite.
        private double _animBackOffsetX, _animBackOffsetY, _animBackScaleX = 1, _animBackScaleY = 1, _animBackRotation, _animBackFadeOpacity;
        public double AnimBackOffsetX { get => _animBackOffsetX; private set => Set(ref _animBackOffsetX, value); }
        public double AnimBackOffsetY { get => _animBackOffsetY; private set => Set(ref _animBackOffsetY, value); }
        public double AnimBackScaleX { get => _animBackScaleX; private set => Set(ref _animBackScaleX, value); }
        public double AnimBackScaleY { get => _animBackScaleY; private set => Set(ref _animBackScaleY, value); }
        public double AnimBackRotation { get => _animBackRotation; private set => Set(ref _animBackRotation, value); }
        private global::Avalonia.Matrix _animBackMatrix = global::Avalonia.Matrix.Identity;
        public global::Avalonia.Matrix AnimBackMatrix { get => _animBackMatrix; private set => Set(ref _animBackMatrix, value); }
        public double AnimBackFadeOpacity { get => _animBackFadeOpacity; private set => Set(ref _animBackFadeOpacity, value); }
        private IBrush _animBackFadeBrush = Brushes.Transparent;
        public IBrush AnimBackFadeBrush { get => _animBackFadeBrush; private set => Set(ref _animBackFadeBrush, value); }

        private void EnsureAnimDefsNarc()
        {
            if (_animDefsTried) return;
            _animDefsTried = true;
            if (IsAvailable) _animDefsNarc = new OffsetNarc(DirNames.pokeAnimDefs, 1);
        }

        /// <summary>Plays or stops the chosen sides' movement scripts.</summary>
        public void ToggleAnimationPlayback() => TogglePlayback(PreviewMode.Animation);

        /// <summary>Plays the chosen sides' frame runs once, or stops playback.</summary>
        public void ToggleFramePlayback() => TogglePlayback(PreviewMode.Frames);

        /// <summary>Plays or stops the send-out for the chosen sides.</summary>
        public void ToggleSendOutPlayback()
        {
            if (_mode == PreviewMode.SendOut || _animTimer == null) { TogglePlayback(PreviewMode.SendOut); return; }
            _ = PrepareThenSendOutAsync();
        }

        // The first send-out unpacks its archives, and each new theme renders, in the background.
        private bool _sendOutReady, _sendOutLoading, _sendOutPreparing;

        private async System.Threading.Tasks.Task PrepareThenSendOutAsync()
        {
            if (_sendOutPreparing) return;
            _sendOutPreparing = true;
            try
            {
                if (!_sendOutReady)
                {
                    SetSendOutLoading(true);
                    try { await System.Threading.Tasks.Task.Run(SendOutGraphics.Unpack); }
                    catch (Exception ex) { AppLogger.Error("Send-out preview unpack failed: " + ex.Message); }
                    await LoadTrainersAsync();
                    _sendOutReady = true;
                }
                if (_sound && _playMusic)
                {
                    var music = await ChooseMusicAsync();
                    if (music != null && !music.IsCompleted) { SetSendOutLoading(true); await music; }
                }
            }
            finally
            {
                _sendOutPreparing = false;
                SetSendOutLoading(false);
            }
            TogglePlayback(PreviewMode.SendOut);
        }

        private void SetSendOutLoading(bool loading)
        {
            if (_sendOutLoading == loading) return;
            _sendOutLoading = loading;
            OnPropertyChanged(nameof(SendOutButtonText));
        }

        // Finds and names the theme off the UI thread and starts rendering it; null when there is none.
        private async System.Threading.Tasks.Task<System.Threading.Tasks.Task<short[]>> ChooseMusicAsync()
        {
            _gfx ??= new SendOutGraphics();
            var gfx = _gfx;
            int version = ++_musicVersion;
            bool trainer = IsTrainerBattle, kanto = _kantoMusic;
            int trainerClass = Math.Max(0, _trainer.Class), species = _currentId;
            int seq = -1;
            string name = "";
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    seq = gfx.BattleMusic(trainer, trainerClass, species, kanto);
                    if (seq >= 0) name = SendOutGraphics.SequenceName(seq);
                });
            }
            catch (Exception ex) { AppLogger.Error("Battle music lookup failed: " + ex.Message); }
            if (version != _musicVersion) return null;
            BattleMusicText = name;
            return seq >= 0 && _playMusic ? gfx.Music(seq) : null;
        }

        // Renders the current theme ahead of the next play.
        private void RefreshMusic()
        {
            if (!_sound || !IsAvailable || !(_sendOutReady || _advancedOpen)) return;
            _ = ChooseMusicAsync();
        }

        private void StartMusic()
        {
            if (!_sound || !_playMusic || _gfx == null) return;
            int seq = _gfx.BattleMusic(IsTrainerBattle, Math.Max(0, _trainer.Class), _currentId, _kantoMusic);
            var music = seq >= 0 ? _gfx.Music(seq) : null;
            if (music != null && music.IsCompletedSuccessfully) _musicHandle = SendOutGraphics.StartMusic(music.Result);
        }

        private void TogglePlayback(PreviewMode mode)
        {
            bool stopping = _mode == mode;
            if (_mode == PreviewMode.SendOut && _sound) StopSounds();
            StopPlayback();
            if (stopping) return;

            if (mode == PreviewMode.SendOut) { BeginSendOut(); if (_mode != PreviewMode.SendOut) return; }
            else
            {
                bool frames = mode == PreviewMode.Frames, movement = mode == PreviewMode.Animation;
                if (Sides != PreviewSides.Yours) StartFront(frames, movement);
                if (Sides != PreviewSides.Theirs) StartBack(frames, movement);
                PushFrames();
                PushTransforms();
                if (!SpritesPlaying) return;
            }

            _mode = mode;
            _ticksRun = 0;
            _clock.Restart();
            _animTimer?.Start();
            RaisePlaying();
        }

        private void StartFront(bool frames, bool movement)
        {
            EnsureAnimDefsNarc();
            if (frames) _frontFrames.Start(_frontSlots);
            if (movement) _prog = LoadProgram(_animFrontProg, _animFrontWait);
        }

        private void StartBack(bool frames, bool movement)
        {
            EnsureAnimDefsNarc();
            if (frames) _backFrames.Start(_backSlots);
            int slot = BackSlot;
            if (movement && slot < AnimBack.Count) _progBack = LoadProgram(AnimBack[slot].Number, AnimBack[slot].Wait);
        }

        private bool FrontBusy => _frontFrames.Active || (_prog?.Active ?? false);
        private bool BackBusy => _backFrames.Active || (_progBack?.Active ?? false);
        private bool SpritesPlaying => FrontBusy || BackBusy;

        private void BeginSendOut()
        {
            _gfx ??= new SendOutGraphics();
            _sendOutBall = SelectedBall;
            if (_trainer.Class < 0) _trainer = SendOutGraphics.TrainerInfo(_trainerIndex >= 0 ? _trainerIndex : SendOutGraphics.DefaultTrainer());
            bool trainer = IsTrainerBattle;
            int trainerClass = Math.Max(0, _trainer.Class);
            var options = new SendOutOptions
            {
                Kind = (SendOutKind)_sendOutKindIndex,
                Sides = Sides,
                Ball = _sendOutBall,
                TrainerIntro = !trainer || _playTrainerIntro,
                SlideIn = trainer && _playTrainerIntro && _trainerSlideIn,
                ShowPartyBalls = trainer && _showPartyBalls,
                PartyBalls = _partyBallCount,
                Shiny = _isShiny,
                Speed = (TextSpeed)_textSpeedIndex,
                EnemyCryDelay = _animFrontCryDelay,
                PlayerCryDelay = _animBackCryDelay,
                CryZeroIsEight = gameFamily == GameFamilies.HGSS,
                TrainerSequences = trainer ? _gfx.EnemyTrainerSequenceCount(trainerClass) : 1,
                TrainerLandingTicks = trainer ? _gfx.EnemyTrainerSequenceTicks(trainerClass, 1) : 0,
                Text = MessageFor,
            };
            _sendOut = new SendOutSequence(options);
            _waitingSeals.Clear();
            _enemyBurst = new SpaParticlePreview(256, 192);
            _playerBurst = new SpaParticlePreview(256, 192);
            _enemySparkle = _playerSparkle = null;
            var balls = _gfx.BallSequences(_sendOutBall);
            _enemyBallActor = new CellActor(balls, 1);
            _playerBallActor = new CellActor(balls, 0);
            _enemyBallRolling = _playerBallRolling = false;
            var rowSeqs = _gfx.PartyRowSequences();
            for (int i = 0; i < 6; i++) { _enemyRowActors[i] = new CellActor(rowSeqs, 0); _playerRowActors[i] = new CellActor(rowSeqs, 3); }
            if (_sound)
            {
                _cry = SendOutGraphics.Cry(_currentId);
                _ballOpenSound = _gfx.BallOpenSound();
            }
            StartMusic();
            // The first tick shows at once; after that the timer steps it.
            _mode = PreviewMode.SendOut;
            SendOutTick();
        }

        private void StopSounds()
        {
            try { AudioOutput.Current.Stop(); } catch { }
        }

        /// <summary>The Ball Capsule used on send-out, whose seals replace the ball burst; null for the plain burst.</summary>
        public BallCapsule Capsule { get; set; }

        private IReadOnlyList<BallSeal> _seals;
        private sealed class WaitingSeal { public int Ticks; public BallSeal Seal; public int X, Y; public bool Enemy; }
        private readonly List<WaitingSeal> _waitingSeals = new();

        private bool OpenWithCapsule(bool enemySide)
        {
            if (Capsule == null || Capsule.IsEmpty) return false;
            _seals ??= BallSeals.Read();
            foreach (var placed in Capsule.Seals)
            {
                if (placed.Seal <= 0 || placed.Seal >= _seals.Count || _seals[placed.Seal] == null) continue;
                var seal = _seals[placed.Seal];
                _waitingSeals.Add(new WaitingSeal
                {
                    Ticks = SealEffect.DelayTicks(seal, placed.X, placed.Y), Seal = seal, X = placed.X, Y = placed.Y, Enemy = enemySide,
                });
            }
            return true;
        }

        private void LaunchDueSeals()
        {
            for (int i = _waitingSeals.Count - 1; i >= 0; i--)
            {
                var w = _waitingSeals[i];
                if (w.Ticks-- > 0) continue;
                _gfx.AddSeal(w.Enemy ? _enemyBurst : _playerBurst, w.Seal, w.X, w.Y, w.Enemy);
                _waitingSeals.RemoveAt(i);
            }
        }

        /// <summary>Forgets a seal particle file already read, so an edit shows on the next send-out.</summary>
        public void ForgetSealParticles(int entry) => _gfx?.ForgetParticles(entry);

        // Running players step first so the sequence sees what finished; anything started shows its first state.
        private void SendOutTick()
        {
            _prog?.Step();
            _progBack?.Step();
            _frontFrames.Tick();
            _backFrames.Tick();
            bool enemyBursting = (_enemyBurst.HasEmitters && !_enemyBurst.AllFinished) || _waitingSeals.Exists(w => w.Enemy);
            bool playerBursting = (_playerBurst.HasEmitters && !_playerBurst.AllFinished) || _waitingSeals.Exists(w => !w.Enemy);
            StepSparkle(ref _enemySparkle, img => EnemySparkleImage = img);
            StepSparkle(ref _playerSparkle, img => PlayerSparkleImage = img);

            var s = _sendOut;
            s.EnemyBusy = FrontBusy;
            s.PlayerBusy = BackBusy;
            s.EnemyBurstBusy = enemyBursting;
            s.PlayerBurstBusy = playerBursting;
            s.EnemySparkleBusy = _enemySparkle != null;
            s.PlayerSparkleBusy = _playerSparkle != null;
            s.Step();

            if (s.EnemyAnimStarts) StartFront(frames: true, movement: true);
            if (s.PlayerAnimStarts) StartBack(frames: true, movement: true);
            if (s.EnemyBallOpens) { if (!OpenWithCapsule(enemySide: true)) _gfx.AddBurst(_enemyBurst, _sendOutBall, enemySide: true); PlaySound(_ballOpenSound); }
            if (s.PlayerBallOpens) { if (!OpenWithCapsule(enemySide: false)) _gfx.AddBurst(_playerBurst, _sendOutBall, enemySide: false); PlaySound(_ballOpenSound); }
            LaunchDueSeals();
            if (s.EnemyCry) PlaySound(_cry);
            if (s.PlayerCry) PlaySound(_cry);
            if (s.EnemySparkleStarts) _enemySparkle = StartSparkle(enemySide: true);
            if (s.PlayerSparkleStarts) _playerSparkle = StartSparkle(enemySide: false);
            foreach (string name in s.Sounds) PlaySound(_gfx.Sound(name));

            ApplySendOut();
            PushFrames();
            PushTransforms();

            // Particles are drawn where they are, then moved, once a tick.
            EnemyBurstImage = StepBurst(_enemyBurst);
            PlayerBurstImage = StepBurst(_playerBurst);

            if (s.Done && !SpritesPlaying && _enemySparkle == null && _playerSparkle == null && _waitingSeals.Count == 0
                && !(_enemyBurst.HasEmitters && !_enemyBurst.AllFinished) && !(_playerBurst.HasEmitters && !_playerBurst.AllFinished))
                StopPlayback();
        }

        private static Bitmap StepBurst(SpaParticlePreview burst)
        {
            if (!burst.HasEmitters || burst.AllFinished) return null;
            var image = burst.RenderFrame();
            burst.Step();
            return image;
        }

        private WestPlayer StartSparkle(bool enemySide)
        {
            // On the battler's centre, the way the effect's own emitters are placed.
            double x = enemySide ? EnemyLeft + 40 : PlayerLeft + 40;
            double y = enemySide ? EnemyTop + 40 : PlayerTop + 40;
            var player = _gfx.Sparkle(enemySide, x, y);
            if (player != null) player.PlaySound = id => PlaySound(_gfx.Sound(id));
            return player;
        }

        private static void StepSparkle(ref WestPlayer sparkle, Action<Bitmap> show)
        {
            if (sparkle == null) return;
            sparkle.Step();
            if (sparkle.Finished) { sparkle = null; show(null); return; }
            show(sparkle.RenderFrame());
        }

        private void PlaySound(System.Threading.Tasks.Task<short[]> sound)
        {
            if (_sound) SendOutGraphics.Play(sound);
        }

        private void ApplySendOut()
        {
            var s = _sendOut;
            EnemyShown = s.Enemy.Visible;
            PlayerShown = s.Player.Visible;
            _enemySlideX = s.Enemy.OffsetX;
            _enemyGrow = s.Enemy.Scale; _playerGrow = s.Player.Scale;
            _enemyTint = s.Enemy.Tint; _enemyTintRgb = s.Enemy.TintRgb;
            _playerTint = s.Player.Tint; _playerTintRgb = s.Player.TintRgb;
            SetSceneOffsets(s.EnemyPlatformOffsetX, s.PlayerPlatformOffsetX, s.BackdropScrollX,
                s.EnemyGaugeOffsetX, s.PlayerGaugeOffsetX, s.EnemyGaugeVisible, s.PlayerGaugeVisible);

            var et = s.EnemyTrainer;
            ApplyTrainer(EnemyTrainerSprite, et, et.Visible ? _gfx.EnemyTrainer(Math.Max(0, _trainer.Class), et.Sequence, et.SequenceTicks) : null);
            ApplyTrainer(PlayerTrainerSprite, s.PlayerTrainer, s.PlayerTrainer.Visible ? _gfx.PlayerTrainer(s.PlayerTrainer.AnimTicks) : null);
            ApplyBall(EnemyBallSprite, s.EnemyBall, _enemyBallActor, ref _enemyBallRolling);
            ApplyBall(PlayerBallSprite, s.PlayerBall, _playerBallActor, ref _playerBallRolling);
            ApplyRow(s.EnemyRow, _enemyRowActors);
            ApplyRow(s.PlayerRow, _playerRowActors);
            PartyRowsImage = s.EnemyRow.Visible || s.PlayerRow.Visible
                ? _gfx.ComposeRows((s.EnemyRow, _enemyRowActors, false), (s.PlayerRow, _playerRowActors, true))
                : null;

            MessageBoxDim = 1 - s.TextBox;
            RaiseMessage();
            RaiseShadows();
        }

        // Party-row balls spin at the battle's two animation units a tick while they roll.
        private static void ApplyRow(SendOutSequence.RowState row, CellActor[] actors)
        {
            for (int i = 0; i < 6; i++)
            {
                var ball = row.Balls[i];
                if (actors[i].Seq != ball.Sequence) actors[i].SetSeq(ball.Sequence);
                else if (ball.Animating) { actors[i].Tick(); actors[i].Tick(); }
                else if (actors[i].FrameIndex != 0) actors[i].SetSeq(ball.Sequence);
            }
        }

        private static void ApplyTrainer(SceneSprite view, SendOutSequence.TrainerState t, Bitmap image)
        {
            view.Visible = t.Visible && image != null;
            if (!view.Visible) return;
            view.Image = image;
            view.Left = t.X - 80;
            view.Top = t.Y - 80;
        }

        // The ball's cells advance one animation unit a tick while it spins or opens.
        private void ApplyBall(SceneSprite view, SendOutSequence.BallState b, CellActor actor, ref bool rolling)
        {
            if (b.Animating)
            {
                if (!rolling || actor.Seq != b.Sequence) actor.SetSeq(b.Sequence);
                else actor.Tick();
            }
            rolling = b.Animating;

            view.Visible = b.Visible;
            if (!b.Visible) return;
            view.Image = _gfx.BallCell(_sendOutBall, actor.CellIndex);
            view.Left = b.X - 32;
            view.Top = b.Y - 32;
            view.Rotation = b.Rotation * 360.0 / 0x10000;
            view.Flash = b.Flash;
        }

        private void SetSceneOffsets(int enemyPlatform, int playerPlatform, int backdrop, int enemyGauge, int playerGauge, bool enemyGaugeShown, bool playerGaugeShown)
        {
            if (_enemyPlatformX != enemyPlatform)
            {
                _enemyPlatformX = enemyPlatform;
                OnPropertyChanged(nameof(ArenaGroundEnemyX)); OnPropertyChanged(nameof(PlaceholderEnemyPlatformX));
            }
            if (_playerPlatformX != playerPlatform)
            {
                _playerPlatformX = playerPlatform;
                OnPropertyChanged(nameof(ArenaGroundMineX)); OnPropertyChanged(nameof(PlaceholderPlayerPlatformX));
            }
            if (_backdropX != backdrop) { _backdropX = backdrop; OnPropertyChanged(nameof(BackdropX)); }
            if (_enemyGaugeX != enemyGauge) { _enemyGaugeX = enemyGauge; OnPropertyChanged(nameof(EnemyGaugeX)); }
            if (_playerGaugeX != playerGauge) { _playerGaugeX = playerGauge; OnPropertyChanged(nameof(PlayerGaugeX)); }
            if (_enemyGaugeShown != enemyGaugeShown) { _enemyGaugeShown = enemyGaugeShown; OnPropertyChanged(nameof(EnemyGaugeShown)); }
            if (_playerGaugeShown != playerGaugeShown) { _playerGaugeShown = playerGaugeShown; OnPropertyChanged(nameof(PlayerGaugeShown)); }
        }

        // Back to the battle as it stands after the send-out: both Pokémon out, bars in, nothing thrown.
        private void ResetScene()
        {
            _sendOut = null;
            _enemyBurst = _playerBurst = null;
            _enemySparkle = _playerSparkle = null;
            EnemyBurstImage = PlayerBurstImage = EnemySparkleImage = PlayerSparkleImage = PartyRowsImage = null;
            EnemyShown = PlayerShown = true;
            _enemySlideX = 0; _enemyGrow = _playerGrow = 1; _enemyTint = _playerTint = 0;
            SetSceneOffsets(0, 0, 0, 0, 0, true, true);
            EnemyTrainerSprite.Visible = PlayerTrainerSprite.Visible = EnemyBallSprite.Visible = PlayerBallSprite.Visible = false;
            MessageBoxDim = 0;
            RaiseMessage();
            RaiseShadows();
        }

        private void RaiseShadows()
        {
            OnPropertyChanged(nameof(ShadowSmallVisible)); OnPropertyChanged(nameof(ShadowMediumVisible)); OnPropertyChanged(nameof(ShadowLargeVisible));
            OnPropertyChanged(nameof(ShadowSmallLeft)); OnPropertyChanged(nameof(ShadowMediumLeft)); OnPropertyChanged(nameof(ShadowLargeLeft));
            OnPropertyChanged(nameof(ShadowGrow)); OnPropertyChanged(nameof(ShadowGrowY));
        }

        private PokeAnimPlayer LoadProgram(int fileIndex, int startDelay)
        {
            var bytes = _animDefsNarc?.GetRecord(fileIndex);
            var script = bytes != null ? PokeAnimScript.Parse(bytes) : null;
            return (script != null && script.Count > 0) ? new PokeAnimPlayer(script, startDelay) : null;
        }

        public void StopPlayback()
        {
            SendOutGraphics.StopMusic(_musicHandle);
            _musicHandle = null;
            _animTimer?.Stop();
            _clock.Reset();
            _prog = null; _progBack = null;
            _frontFrames.Stop();
            _backFrames.Stop();
            var was = _mode;
            _mode = PreviewMode.None;
            ResetScene();
            PushFrames();
            PushTransforms();
            if (was == PreviewMode.None) return;
            RaisePlaying();
        }

        private void RaisePlaying()
        {
            OnPropertyChanged(nameof(IsPlaying));
            OnPropertyChanged(nameof(FramesButtonText));
            OnPropertyChanged(nameof(AnimationButtonText));
            OnPropertyChanged(nameof(SendOutButtonText));
            RaiseMessage();
        }

        // The UI timer fires irregularly, so ticks are counted against a stopwatch rather than one per callback.
        private void OnAnimTimer()
        {
            if (!IsPlaying) { _animTimer?.Stop(); return; }
            long due = (long)(_clock.Elapsed.TotalSeconds * PreviewFps);
            if (due - _ticksRun > 4) _ticksRun = due - 1;   // after a stall, resume rather than fast-forward
            while (IsPlaying && _ticksRun < due)
            {
                _ticksRun++;
                GameTick();
            }
        }

        internal int FrontFrameShown => _frontFrame;

        internal void GameTick()
        {
            if (_mode == PreviewMode.SendOut) { SendOutTick(); return; }
            _prog?.Step();
            _progBack?.Step();
            _frontFrames.Tick();
            _backFrames.Tick();
            PushFrames();
            PushTransforms();
            if (!SpritesPlaying) StopPlayback();
        }

        private void PushFrames()
        {
            int front = ClampFrame(_frontFrames.SpriteFrame), back = ClampFrame(_backFrames.SpriteFrame);
            if (front != _frontFrame || back != _backFrame)
            {
                _frontFrame = front; _backFrame = back;
                RaiseSprites();
            }
            if (_frontShift != _frontFrames.HorizontalShift || _backShift != _backFrames.HorizontalShift)
            {
                _frontShift = _frontFrames.HorizontalShift; _backShift = _backFrames.HorizontalShift;
                RaiseLayout();
            }
        }

        private void PushTransforms()
        {
            var f = _prog;
            AnimOffsetX = f?.OffsetX ?? 0; AnimOffsetY = f?.OffsetY ?? 0;
            AnimScaleX = f?.ScaleX ?? 1; AnimScaleY = f?.ScaleY ?? 1;
            AnimRotation = f?.RotationDegrees ?? 0;
            AnimMatrix = SpriteMatrix(f, _frontShift, _spriteY) * GrowMatrix(_enemyGrow, _spriteY)
                       * global::Avalonia.Matrix.CreateTranslation(_enemySlideX, 0);
            // A ball's colour or the wild intro's shade is a palette fade too, and wins over the script's own.
            if (_enemyTint > 0) { AnimFadeOpacity = _enemyTint; AnimFadeBrush = TintBrush(_enemyTintRgb); }
            else
            {
                AnimFadeOpacity = f?.FadeStrength ?? 0;
                if (f != null && f.FadeStrength > 0) AnimFadeBrush = new SolidColorBrush(Color.FromRgb(f.FadeR, f.FadeG, f.FadeB));
            }

            var b = _progBack;
            AnimBackOffsetX = b?.OffsetX ?? 0; AnimBackOffsetY = b?.OffsetY ?? 0;
            AnimBackScaleX = b?.ScaleX ?? 1; AnimBackScaleY = b?.ScaleY ?? 1;
            AnimBackRotation = b?.RotationDegrees ?? 0;
            AnimBackMatrix = SpriteMatrix(b, _backShift, 0) * GrowMatrix(_playerGrow, _spriteY);
            if (_playerTint > 0) { AnimBackFadeOpacity = _playerTint; AnimBackFadeBrush = TintBrush(_playerTintRgb); }
            else
            {
                AnimBackFadeOpacity = b?.FadeStrength ?? 0;
                if (b != null && b.FadeStrength > 0) AnimBackFadeBrush = new SolidColorBrush(Color.FromRgb(b.FadeR, b.FadeG, b.FadeB));
            }
        }

        private static IBrush TintBrush(uint rgb) =>
            new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));

        // Growing about the centre while dropping (40 − yOffset) × (1 − scale) is scaling about that point below it.
        private static global::Avalonia.Matrix GrowMatrix(double scale, int yOffset)
        {
            if (scale >= 1) return global::Avalonia.Matrix.Identity;
            double anchorY = 80 - yOffset;
            return global::Avalonia.Matrix.CreateTranslation(-40, -anchorY)
                 * global::Avalonia.Matrix.CreateScale(scale, scale)
                 * global::Avalonia.Matrix.CreateTranslation(40, anchorY);
        }

        // In the 80×80 box: scale about the centre, move, then rotate about the anchor (centre less the frame
        // shift, plus the pivot, and on the front sprite the record's Y offset).
        private static global::Avalonia.Matrix SpriteMatrix(PokeAnimPlayer p, int frameShift, int anchorBelowCentre)
        {
            if (p == null) return global::Avalonia.Matrix.Identity;
            const double centre = 40;
            double pivotX = centre - frameShift + p.OffsetX + p.PivotX;
            double pivotY = centre + anchorBelowCentre + p.OffsetY;
            return global::Avalonia.Matrix.CreateTranslation(-centre, -centre)
                 * global::Avalonia.Matrix.CreateScale(p.ScaleX, p.ScaleY)
                 * global::Avalonia.Matrix.CreateTranslation(centre + p.OffsetX - pivotX, centre + p.OffsetY - pivotY)
                 * global::Avalonia.Matrix.CreateRotation(p.RotationDegrees * Math.PI / 180)
                 * global::Avalonia.Matrix.CreateTranslation(pivotX, pivotY);
        }

        // Battles tick sprite animation at 30 Hz. 60 is kept only to preview a hack that runs it faster.
        public ObservableCollection<string> FrameRateOptions { get; } = new ObservableCollection<string> { "30 fps", "60 fps" };

        /// <summary>Remembered between sessions and preview-only: it never changes what is saved.</summary>
        public int FrameRateIndex
        {
            get => DSPRE.SettingsManager.Settings?.battlePreviewFps == 60 ? 1 : 0;
            set
            {
                int fps = value == 1 ? 60 : 30;
                if (DSPRE.SettingsManager.Settings == null || DSPRE.SettingsManager.Settings.battlePreviewFps == fps) return;
                DSPRE.SettingsManager.Settings.battlePreviewFps = fps;
                try { DSPRE.SettingsManager.Save(); } catch { }
                OnPropertyChanged(nameof(FrameRateIndex));
                if (IsPlaying) { _ticksRun = 0; _clock.Restart(); }
            }
        }

        private int PreviewFps => FrameRateIndex == 1 ? 60 : 30;

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
            StopPlayback();
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

        private void LoadSpriteData(int id)
        {
            HasSpriteData = false; HasHeights = false;
            SetFrameSlots(null, null);
            if (!IsAvailable || id < 0) return;
            try
            {
                if (HgEngineProject.IsActive) { LoadSpriteDataFromHgeSource(_currentHgeSpeciesId); return; }

                EnsureSource();
                if (_src == null) return;
                if (!_src.TryLoad(BaseSpeciesIdFor(id), out BattleRec rec)) return;
                if (RecordFamily)
                {
                    var record = ReadRecord();
                    if (record == null) return;
                    LoadFrameEntries(record.Front.Frames, record.Back.Frames);
                }
                _spriteY = rec.FrontY; _shadowX = rec.ShadowX; _shadowSize = rec.ShadowSize;
                _backHeightF = rec.BackF; _backHeightM = rec.BackM; _frontHeightF = rec.FrontF; _frontHeightM = rec.FrontM;
                OnPropertyChanged(nameof(SpriteY)); OnPropertyChanged(nameof(ShadowX)); OnPropertyChanged(nameof(ShadowSize));
                OnPropertyChanged(nameof(FrontHeightM)); OnPropertyChanged(nameof(FrontHeightF)); OnPropertyChanged(nameof(BackHeightM)); OnPropertyChanged(nameof(BackHeightF));
                OnPropertyChanged(nameof(FrontHeightUnified)); OnPropertyChanged(nameof(BackHeightUnified));
                HasHeights = rec.HasHeights;
                HasSpriteData = true;
            }
            catch { HasSpriteData = false; }
        }

        // Heights come from data/HeightTable.c, a separate file from the sprite-offset block above.
        private void LoadSpriteDataFromHgeSource(int id)
        {
            if (!HgEngineSpriteOffsets.TryLoad(id, out var block, out _)) return;
            if (!block.TryGetInt(new[] { FieldPathSegment.Field("spriteYOffset") }, out int spriteY)) return;
            if (!block.TryGetInt(new[] { FieldPathSegment.Field("shadowXOffset") }, out int shadowX)) return;
            if (!block.TryGetInt(new[] { FieldPathSegment.Field("shadowSize") }, out int shadowSize)) return;

            LoadFrameEntries(HgEngineSpriteOffsets.ReadFrameSlots(block, "frontFrames"), HgEngineSpriteOffsets.ReadFrameSlots(block, "backFrames"));

            _spriteY = spriteY; _shadowX = shadowX; _shadowSize = shadowSize;

            bool hasHeights = HgEngineHeightTable.TryGet(id, out int femaleBack, out int maleBack, out int femaleFront, out int maleFront);
            _backHeightF = hasHeights ? femaleBack : 0; _backHeightM = hasHeights ? maleBack : 0;
            _frontHeightF = hasHeights ? femaleFront : 0; _frontHeightM = hasHeights ? maleFront : 0;

            OnPropertyChanged(nameof(SpriteY)); OnPropertyChanged(nameof(ShadowX)); OnPropertyChanged(nameof(ShadowSize));
            OnPropertyChanged(nameof(FrontHeightM)); OnPropertyChanged(nameof(FrontHeightF)); OnPropertyChanged(nameof(BackHeightM)); OnPropertyChanged(nameof(BackHeightF));
            OnPropertyChanged(nameof(FrontHeightUnified)); OnPropertyChanged(nameof(BackHeightUnified));
            HasHeights = hasHeights;
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
            public int FrontY, ShadowX, ShadowSize;
            public bool HasHeights;
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

        /// <summary>Platinum and HGSS sprite record, plus Platinum's per-gender heights.</summary>
        private sealed class CombinedTailSource : IBattleOffsetSource
        {
            private readonly OffsetNarc _narc;
            private readonly HeightNarc _heights;   // null when withHeights:false
            public CombinedTailSource(OffsetNarc records, bool withHeights)
            { _narc = records; _heights = withHeights ? new HeightNarc() : null; }

            public void Invalidate() { _narc.Invalidate(); _heights?.Invalidate(); }

            public bool TryLoad(int id, out BattleRec rec)
            {
                rec = default;
                var r = _narc.GetRecord(id);
                if (r == null || r.Length < 3) return false;
                int n = r.Length;
                rec.FrontY = (sbyte)r[n - 3]; rec.ShadowX = (sbyte)r[n - 2]; rec.ShadowSize = r[n - 1];
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

            // Polled well above the game rate; OnAnimTimer decides from the stopwatch how many ticks are due.
            _animTimer = new global::Avalonia.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(5)
            };
            _animTimer.Tick += (_, _) => OnAnimTimer();
            _sound = true;

            if (IsAvailable)
            {
                ApplyArena();
                try { LoadBallOptions(); } catch { }
            }
        }

        /// <summary>Stops the preview timer (e.g. when this VM is used only to compute sprite positions elsewhere).</summary>
        public void Detach() => StopPlayback();

        public void LoadMon(int id)
        {
            _loading = true;
            _currentId = id;
            OnPropertyChanged(nameof(GaugeNameText));
            OnPropertyChanged(nameof(GaugeNameImage));
            RenderGauges();
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
            _savedPartyPaletteIndex = _partyPaletteIndex;
            OnPropertyChanged(nameof(PartyPaletteIndex));
            RefreshPreview();
            LoadFormOptions(id);
            LoadSpriteData(id);
            _formMode = _sprites != null && _sprites.IsAlternateForms;
            _formIndex = _sprites != null ? _sprites.SelectedFormIndex : -1;
            LoadFormHeights();
            StopPlayback();
            LoadAnim(id);
            RefreshMusic();
            OnPropertyChanged(nameof(FormMode)); OnPropertyChanged(nameof(ShowBaseHeights)); OnPropertyChanged(nameof(CanPlay));
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
                // Personal Data writes this table as soon as it changes; an untouched value here would undo that.
                if (_partyPaletteIndex != _savedPartyPaletteIndex)
                {
                    if (HgEngineProject.IsActive)
                    {
                        if (!HgEngineIconPalette.TrySetPaletteId(_currentId, _partyPaletteIndex, out string paletteError))
                            throw new InvalidOperationException(paletteError);
                    }
                    else
                        DSPRE.DSUtils.SetMonIconPaletteId(IconIdFor(_currentId), (byte)_partyPaletteIndex);
                    _savedPartyPaletteIndex = _partyPaletteIndex;
                }
                if (_pendingIconGraphic != null)
                {
                    DSPRE.DSUtils.SetMonIconGraphic(IconIdFor(_currentId), _partyPaletteIndex, _pendingIconGraphic);
                    _pendingIconGraphic = null;
                }
                SaveSpriteData(); SaveFormHeights(); SaveAnim(); SaveFrames(); SetClean();
                SaveNotice.Saved(UnsavedChangesDescription);
                RefreshPreview();   // now reflects what was actually written (disk read), not the staged import
            }
            catch (Exception ex)
            {
                AppLogger.Error("Battle Display save failed: " + ex.Message);
                _ = DSPRE.Avalonia.DialogHelper.ShowError("The Battle Display tab could not be saved: " + ex.Message, "Save Error");
            }
        }
    }

    /// <summary>A trainer or ball the send-out preview draws over the battle scene.</summary>
    public sealed class SceneSprite : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private Bitmap _image; public Bitmap Image { get => _image; set { if (_image != value) { _image = value; Raise(nameof(Image)); } } }
        private double _left; public double Left { get => _left; set { if (_left != value) { _left = value; Raise(nameof(Left)); } } }
        private double _top; public double Top { get => _top; set { if (_top != value) { _top = value; Raise(nameof(Top)); } } }
        private bool _visible; public bool Visible { get => _visible; set { if (_visible != value) { _visible = value; Raise(nameof(Visible)); } } }
        private double _rotation; public double Rotation { get => _rotation; set { if (_rotation != value) { _rotation = value; Raise(nameof(Rotation)); } } }
        private double _flash; public double Flash { get => _flash; set { if (_flash != value) { _flash = value; Raise(nameof(Flash)); } } }
    }

    /// <summary>One DP pattern step: which sprite frame (0 or 1) to show and for how many ticks.</summary>
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

    /// <summary>One frame-run slot: sprite frame, duration and pixel shift. FrameNo -1 ends the run.</summary>
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
