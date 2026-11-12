using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DSPRE.Avalonia.BattleAnim.Particles;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using static MKDS_Course_Editor.NSBTP.NSBTP.NSBTP_File;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    public partial class BattleDisplayEditorViewModel
    {
        private bool _hasArenaGraphics;
        public bool HasArenaGraphics { get => _hasArenaGraphics; private set => Set(ref _hasArenaGraphics, value); }

        private void ApplyArena()
        {
            try
            {
                BattleGroundRenderer ground = _groundRenderer ??= new BattleGroundRenderer();
                (BattleGroundRenderer.GroundImage playerGround, BattleGroundRenderer.GroundImage opponent) = ground.Build(_arenaTypeIndex);
                int bg = BattleGroundRenderer.BackdropForTerrain(_arenaTypeIndex);
                BattleBgRenderer.BgImage backdropImg = bg >= 0 ? (_bgRenderer ??= new BattleBgRenderer()).BuildBackdrop(bg) : null;

                bool ok = playerGround?.Rgba != null && opponent?.Rgba != null && backdropImg?.Rgba != null;
                if (ok)
                {
                    ArenaGroundPlayer = ImageConverter.FromRgba(playerGround.Rgba, playerGround.Width, playerGround.Height);
                    ArenaGroundPlayerLeft = playerGround.Left; ArenaGroundPlayerTop = playerGround.Top;
                    ArenaGroundOpponent = ImageConverter.FromRgba(opponent.Rgba, opponent.Width, opponent.Height);
                    ArenaGroundOpponentLeft = opponent.Left; ArenaGroundOpponentTop = opponent.Top;
                    // Top 192 rows only: taller backdrops squish into bands when stretched.
                    _backdropWidth = Math.Max(256, backdropImg.Width);
                    ArenaBackdrop = ImageConverter.FromRgba(TileBackdropRgba(backdropImg.Rgba, backdropImg.Width, backdropImg.Height, _backdropWidth), _backdropWidth * 2, 192);
                    OnPropertyChanged(nameof(BackdropX));
                }
                HasArenaGraphics = ok;
            }
            catch (Exception ex) { AppLogger.WarnOnce("BattleDisplayEditorViewModel.ApplyArena", ex); HasArenaGraphics = false; }
            RenderGauges();
        }

        // HP-gauge frames from the ROM, so a hack's gauge shows. Placeholder art covers a failed decode.
        private Bitmap _gaugePlayerImage, _gaugeOpponentImage;
        public Bitmap GaugePlayerImage { get => _gaugePlayerImage; private set => Set(ref _gaugePlayerImage, value); }
        public Bitmap GaugeOpponentImage { get => _gaugeOpponentImage; private set => Set(ref _gaugeOpponentImage, value); }
        public bool HasRealGauges => _gaugePlayerImage != null || _gaugeOpponentImage != null;
        public bool PlaceholderGaugesVisible => !HasRealGauges;
        // The gauge picture is 256 wide, centred on the bar.
        public double PlayerGaugeImageLeft => BattleGaugeComposer.CentreOf(BattleGaugeComposer.Kind.PlayerSingle).X - 128;
        public double PlayerHealthFillLeft => BattleGaugeComposer.HealthFillLeft(BattleGaugeComposer.Kind.PlayerSingle);

        public IBrush GaugeTextBrush => Data.GaugeTextImages.StandInBrush;

        public string GaugeNameText
        {
            get
            {
                string[] names = GetPokemonNamesOrEmpty();
                return (_currentId >= 0 && _currentId < names.Length) ? names[_currentId] : string.Empty;
            }
        }
        public string GaugeLevelText => "Lv5";

        // Name and level in the game's own letters, shared with the other two battle previews.
        public bool GaugeTextIsReal => Data.GaugeTextImages.Available;
        public global::Avalonia.Media.Imaging.Bitmap GaugeNameImage => Data.GaugeTextImages.Name(GaugeNameText);
        public global::Avalonia.Media.Imaging.Bitmap GaugeLevelImage =>
            Data.GaugeTextImages.Level(5, ROMFiles.BattleGaugeText.Gender.Genderless);

        private void RenderGauges()
        {
            try
            {
                BattleGroundRenderer r = _groundRenderer ??= new BattleGroundRenderer();
                GaugePlayerImage = Data.GaugeTextImages.Bar(true, GaugeNameText, 5)
                                ?? r.BuildGauge(true)?.ToBitmap();
                GaugeOpponentImage = Data.GaugeTextImages.Bar(false, GaugeNameText, 5)
                               ?? r.BuildGauge(false)?.ToBitmap();
            }
            catch (Exception ex) { AppLogger.WarnOnce("BattleDisplayEditorViewModel.RenderGauges", ex); GaugePlayerImage = GaugeOpponentImage = null; }
            OnPropertyChanged(nameof(HasRealGauges));
            OnPropertyChanged(nameof(PlaceholderGaugesVisible));
            OnPropertyChanged(nameof(PlayerGaugeImageLeft));
            OnPropertyChanged(nameof(PlayerHealthFillLeft));
            OnPropertyChanged(nameof(GaugeTextBrush));
            foreach (string n in new[] { nameof(GaugeTextIsReal), nameof(GaugeNameImage), nameof(GaugeLevelImage) })
                OnPropertyChanged(n);
            RenderMessageBox();
        }

        private Bitmap _messageBoxImage;
        private bool _messageBoxTried;
        public Bitmap MessageBoxImage { get => _messageBoxImage; private set => Set(ref _messageBoxImage, value); }
        public bool HasRealMessageBox => _messageBoxImage != null;
        public bool PlaceholderMessageBoxVisible => !HasRealMessageBox;
        public string MessageBoxText => MessageLines().First;
        public string MessageBoxLine2 => MessageLines().Second;

        private (string First, string Second) MessageLines()
        {
            if (_mode != PreviewMode.SendOut || _sendOut == null) return ($"What will {GaugeNameText} do?", "");
            string text = _sendOut.MessageText ?? "";
            int nl = text.IndexOf('\n');
            return nl < 0 ? (text, "") : (text.Substring(0, nl), text.Substring(nl + 1));
        }

        private string MessageFor(SendOutMessage message)
        {
            string mon = GaugeNameText;
            string trainer = string.Join(" ", new[] { _trainer.ClassName, _trainer.Name }).Trim();
            if (trainer.Length == 0) trainer = "Trainer";
            return message switch
            {
                SendOutMessage.Challenged => $"You are challenged by\n{trainer}!",
                SendOutMessage.WildAppeared => $"A wild {mon} appeared!",
                SendOutMessage.OpponentSentOut => $"{trainer} sent\nout {mon}!",
                SendOutMessage.Go => $"Go! {mon}!",
                _ => "",
            };
        }

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
                    // 0 is the default window style.
                    BattleScreenRenderer.Piece box = BattleScreenRenderer.BuildMessageBox(0);
                    MessageBoxImage = box.Rgba != null ? DSPRE.Avalonia.ImageConverter.FromRgba(box.Rgba, box.Width, box.Height) : null;
                }
                catch { MessageBoxImage = null; }
                OnPropertyChanged(nameof(HasRealMessageBox));
                OnPropertyChanged(nameof(PlaceholderMessageBoxVisible));
            }
            RaiseMessage();
        }

        // Two copies of the top width×192 side by side. Past the picture stays transparent.
        private static byte[] TileBackdropRgba(byte[] rgba, int w, int h, int width)
        {
            int outW = width * 2;
            byte[] outp = new byte[outW * 192 * 4];
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

        private readonly PokemonSpriteEditorViewModel _sprites;

        // The sheet is two 80×80 frames; each side plays its own frame run once.
        private List<SpriteFrameSlot> _frontSlots, _backSlots;
        private readonly SpriteFramePlayer _frontFrames = new SpriteFramePlayer();
        private readonly SpriteFramePlayer _backFrames = new SpriteFramePlayer();
        private int _frontFrame, _backFrame;
        private int _frontShift, _backShift;

        private readonly global::Avalonia.Threading.DispatcherTimer _animTimer;

        public ObservableCollection<string> Genders { get; } = new ObservableCollection<string> { "Male", "Female" };
        private int _genderIndex;
        public int GenderIndex { get => _genderIndex; set { if (Set(ref _genderIndex, value)) RaiseSprites(); } }
        private bool ShowFemale => _genderIndex >= 0 && _genderIndex < Genders.Count && Genders[_genderIndex] == "Female";
        public bool HasGenderChoice => Genders.Count > 1;

        // Updated in place, never cleared: a Reset nulls the ComboBox's selection and re-enters here.
        private void RefreshGenderChoices()
        {
            bool hasMale = _sprites?.HasSpriteSlot(1) ?? true;
            bool hasFemale = _sprites?.HasSpriteSlot(0) ?? true;
            List<string> wanted = new System.Collections.Generic.List<string>();
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

        public int MaxFrameIndex => System.Math.Max(0, (_sprites?.BattleFrameCount ?? 2) - 1);

        private static Bitmap Pick(System.Collections.Generic.IReadOnlyList<Bitmap> primary,
                                   System.Collections.Generic.IReadOnlyList<Bitmap> fallback, int frame)
        {
            IReadOnlyList<Bitmap> list = (primary != null && primary.Count > 0) ? primary : fallback;
            if (list == null || list.Count == 0) return null;
            int i = frame < 0 ? 0 : (frame >= list.Count ? list.Count - 1 : frame);
            return list[i];
        }
        private Bitmap Front(bool female)
        {
            PokemonSpriteEditorViewModel s = _sprites; if (s == null) return null;
            if (_isShiny) return female ? Pick(s.BattleFrontFShiny, s.BattleFrontMShiny, _frontFrame) : Pick(s.BattleFrontMShiny, s.BattleFrontFShiny, _frontFrame);
            return female ? Pick(s.BattleFrontF, s.BattleFrontM, _frontFrame) : Pick(s.BattleFrontM, s.BattleFrontF, _frontFrame);
        }
        private Bitmap Back(bool female)
        {
            PokemonSpriteEditorViewModel s = _sprites; if (s == null) return null;
            if (_isShiny) return female ? Pick(s.BattleBackFShiny, s.BattleBackMShiny, _backFrame) : Pick(s.BattleBackMShiny, s.BattleBackFShiny, _backFrame);
            return female ? Pick(s.BattleBackF, s.BattleBackM, _backFrame) : Pick(s.BattleBackM, s.BattleBackF, _backFrame);
        }

        public Bitmap OpponentSprite => Front(ShowFemale);
        public Bitmap PlayerSprite => Back(ShowFemale);
        public Bitmap OpponentSpriteM => Front(false);
        public Bitmap PlayerSpriteM => Back(false);
        public Bitmap OpponentSpriteF => Front(true);
        public Bitmap PlayerSpriteF => Back(true);

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

        private bool FrameWarningSevere(bool front, bool female)
        {
            PokemonSpriteEditorViewModel.FrameCellState cell = PoseCell(front, female);
            int blank = BlankFrameIndex(cell);
            if (blank < 0) return false;
            int real = RealFrameIndex(cell);
            List<int> visited = ActivePatternFrames(front);
            return visited.Contains(blank) && (real < 0 || !visited.Contains(real));
        }

        private string FrameWarningText(bool front, bool female)
        {
            PokemonSpriteEditorViewModel.FrameCellState cell = PoseCell(front, female);
            int blank = BlankFrameIndex(cell);
            if (blank < 0) return null;
            int real = RealFrameIndex(cell);
            List<int> visited = ActivePatternFrames(front);
            if (!visited.Contains(blank)) return "only has one real frame";
            if (real >= 0 && visited.Contains(real))
                return "animation flickers to a blank frame, which may be intentional";
            return "animation never shows a real frame, which is likely a mistake";
        }

        public bool OpponentFrameWarning => FrameWarning(true, ShowFemale);
        public bool OpponentFrameWarningSevere => FrameWarningSevere(true, ShowFemale);
        public string OpponentFrameWarningText => FrameWarningText(true, ShowFemale);
        public bool PlayerFrameWarning => FrameWarning(false, ShowFemale);
        public bool PlayerFrameWarningSevere => FrameWarningSevere(false, ShowFemale);
        public string PlayerFrameWarningText => FrameWarningText(false, ShowFemale);
        public bool OpponentFrameWarningM => FrameWarning(true, false);
        public bool OpponentFrameWarningSevereM => FrameWarningSevere(true, false);
        public string OpponentFrameWarningTextM => FrameWarningText(true, false);
        public bool OpponentFrameWarningF => FrameWarning(true, true);
        public bool OpponentFrameWarningSevereF => FrameWarningSevere(true, true);
        public string OpponentFrameWarningTextF => FrameWarningText(true, true);
        public bool PlayerFrameWarningM => FrameWarning(false, false);
        public bool PlayerFrameWarningSevereM => FrameWarningSevere(false, false);
        public string PlayerFrameWarningTextM => FrameWarningText(false, false);
        public bool PlayerFrameWarningF => FrameWarning(false, true);
        public bool PlayerFrameWarningSevereF => FrameWarningSevere(false, true);
        public string PlayerFrameWarningTextF => FrameWarningText(false, true);

        private void RaiseFrameWarnings()
        {
            OnPropertyChanged(nameof(OpponentFrameWarning)); OnPropertyChanged(nameof(OpponentFrameWarningSevere)); OnPropertyChanged(nameof(OpponentFrameWarningText));
            OnPropertyChanged(nameof(PlayerFrameWarning)); OnPropertyChanged(nameof(PlayerFrameWarningSevere)); OnPropertyChanged(nameof(PlayerFrameWarningText));
            OnPropertyChanged(nameof(OpponentFrameWarningM)); OnPropertyChanged(nameof(OpponentFrameWarningSevereM)); OnPropertyChanged(nameof(OpponentFrameWarningTextM));
            OnPropertyChanged(nameof(OpponentFrameWarningF)); OnPropertyChanged(nameof(OpponentFrameWarningSevereF)); OnPropertyChanged(nameof(OpponentFrameWarningTextF));
            OnPropertyChanged(nameof(PlayerFrameWarningM)); OnPropertyChanged(nameof(PlayerFrameWarningSevereM)); OnPropertyChanged(nameof(PlayerFrameWarningTextM));
            OnPropertyChanged(nameof(PlayerFrameWarningF)); OnPropertyChanged(nameof(PlayerFrameWarningSevereF)); OnPropertyChanged(nameof(PlayerFrameWarningTextF));
        }

        private bool HeightsActive => _hasHeights || (_formMode && _hasFormHeights);
        private int ActFrontH(bool f) => (_formMode && _hasFormHeights) ? _formFrontH : (f ? _frontHeightF : _frontHeightM);
        private int ActBackH(bool f) => (_formMode && _hasFormHeights) ? _formBackH : (f ? _backHeightF : _backHeightM);

        // Base 10, as the engine computes it: appear Y 50 minus half the 80px sprite.
        private double FrontTopFor(int h) => 10 - _spriteY + (HeightsActive ? h : 0);
        private double BackTopFor(int h) => 72 + (HeightsActive ? h : 0);

        // Positions from PokEditor's battle scene.
        public double OpponentLeft => 152 + _frontShift;
        public double PlayerLeft => 23 + _backShift;
        public double OpponentTop => FrontTopFor(ActFrontH(ShowFemale));
        public double PlayerTop => BackTopFor(ActBackH(ShowFemale));
        public double OpponentTopM => FrontTopFor(ActFrontH(false));
        public double OpponentTopF => FrontTopFor(ActFrontH(true));
        public double PlayerTopM => BackTopFor(ActBackH(false));
        public double PlayerTopF => BackTopFor(ActBackH(true));

        public bool ShadowSmallVisible => HasSpriteData && _enemyShown && _shadowSize == 1;
        public bool ShadowMediumVisible => HasSpriteData && _enemyShown && _shadowSize == 2;
        public bool ShadowLargeVisible => HasSpriteData && _enemyShown && _shadowSize == 3;
        // The shadow follows the sprite's X, movement included, not its Y or the script's scale.
        public double ShadowSmallLeft => 179 + _shadowX + _frontShift + _animOffsetX + _enemySlideX;
        public double ShadowMediumLeft => 174 + _shadowX + _frontShift + _animOffsetX + _enemySlideX;
        public double ShadowLargeLeft => 167 + _shadowX + _frontShift + _animOffsetX + _enemySlideX;

        private void RaiseLayout()
        {
            OnPropertyChanged(nameof(OpponentLeft)); OnPropertyChanged(nameof(PlayerLeft));
            OnPropertyChanged(nameof(OpponentTop)); OnPropertyChanged(nameof(PlayerTop));
            OnPropertyChanged(nameof(OpponentTopM)); OnPropertyChanged(nameof(OpponentTopF)); OnPropertyChanged(nameof(PlayerTopM)); OnPropertyChanged(nameof(PlayerTopF));
            OnPropertyChanged(nameof(ShadowSmallVisible)); OnPropertyChanged(nameof(ShadowMediumVisible)); OnPropertyChanged(nameof(ShadowLargeVisible));
            OnPropertyChanged(nameof(ShadowSmallLeft)); OnPropertyChanged(nameof(ShadowMediumLeft)); OnPropertyChanged(nameof(ShadowLargeLeft));
        }
        private void RaiseSprites()
        {
            RefreshGenderChoices();
            OnPropertyChanged(nameof(OpponentSprite)); OnPropertyChanged(nameof(PlayerSprite));
            OnPropertyChanged(nameof(OpponentSpriteM)); OnPropertyChanged(nameof(PlayerSpriteM));
            OnPropertyChanged(nameof(OpponentSpriteF)); OnPropertyChanged(nameof(PlayerSpriteF));
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

        private Bitmap _iconPreview;
        public Bitmap IconPreview { get => _iconPreview; private set => Set(ref _iconPreview, value); }

        // Quantized to the palette chosen at import.
        private RawImage _pendingIconGraphic;

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
                // Single-frame icon: the game's own OAM-composed render.
                RawImage gdi = DSPRE.DSUtils.GetPokePicRaw(IconIdFor(_currentId), 64, 64, paletteIdOverride: _partyPaletteIndex);
                IconPreview = gdi != null ? DSPRE.Avalonia.ImageConverter.ToAvaloniaBitmap(gdi) : null;
            }
            catch (Exception ex) { AppLogger.WarnOnce("BattleDisplayEditorViewModel.RefreshPreview", ex); IconPreview = null; IconFrameCount = 1; }
        }

        private static RawImage CropIconFrame(RawImage full, int frame)
        {
            int top = frame * IconFrameSize;
            if (top + IconFrameSize > full.Height) top = 0;
            RawImage outImg = new RawImage(full.Width, IconFrameSize);
            int rowBytes = full.Width * 4;
            for (int y = 0; y < IconFrameSize; y++)
                System.Array.Copy(full.Bgra, (top + y) * rowBytes, outImg.Bgra, y * rowBytes, rowBytes);
            return outImg;
        }

        private bool _fullPaletteExport;
        public bool FullPaletteExport { get => _fullPaletteExport; set => Set(ref _fullPaletteExport, value); }

        private static int IconIdFor(int id) => DSPRE.DSUtils.ResolveIconId(id);
        private static int BaseSpeciesIdFor(int id) => DSPRE.DSUtils.ResolveBaseSpeciesId(id);

        public RawImage ExportIconGraphic()
        {
            if (!IsAvailable || _currentId < 0) return null;
            if (_pendingIconGraphic != null) return _pendingIconGraphic;
            try { return DSPRE.DSUtils.GetMonIconGraphicRaw(IconIdFor(_currentId), _partyPaletteIndex); }
            catch (Exception ex) { AppLogger.WarnOnce("BattleDisplayEditorViewModel.ExportIconGraphic", ex); return null; }
        }

        public byte[] ExportIconGraphicIndexedPng()
        {
            if (!IsAvailable || _currentId < 0 || _pendingIconGraphic != null) return null;
            try
            {
                if (!DSPRE.DSUtils.TryGetMonIconIndexedPixels(IconIdFor(_currentId), _partyPaletteIndex, out byte[] indices, out int w, out int h, out System.Drawing.Color[] palette))
                    return null;
                return IndexedPngWriter.Encode4Bpp(indices, w, h, palette);
            }
            catch (Exception ex) { AppLogger.WarnOnce("BattleDisplayEditorViewModel.ExportIconGraphicIndexedPng", ex); return null; }
        }

        /// <summary>Stages a replacement icon graphic. Null on success, else the error.</summary>
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
    }
}
