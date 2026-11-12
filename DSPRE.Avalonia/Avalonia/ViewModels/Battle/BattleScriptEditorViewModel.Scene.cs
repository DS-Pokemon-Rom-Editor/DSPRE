using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using DSPRE.Avalonia.BattleAnim;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Threading;
using DSPRE.Avalonia.Data;
using static DSPRE.RomInfo;
using Avalonia.Platform;
using System.Runtime.InteropServices;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    public sealed partial class BattleScriptEditorViewModel
    {
        private bool UsesCommand(string name)
        {
            List<WazaSeqCommand> cmds = BuildCommands();
            if (cmds == null) return false;
            foreach (WazaSeqCommand c in cmds)
                if (BattleAnimCommands.Name(_version, c.OpId) == name) return true;
            return false;
        }

        // Some moves have two animations. JumpByTurn picks one from a value the battle script sets.
        private bool _secondTurnVariant;
        public bool SecondTurnVariant
        {
            get => _secondTurnVariant;
            set { if (Set(ref _secondTurnVariant, value)) { bool was = IsCellPlaying; SetupCellPreview(); if (was) ToggleCellPlay(); } }
        }

        public bool HasTurnCheck => UsesCommand("JumpByTurn");

        private void EnsureScene()
        {
            if (_sceneLoaded) return;
            _sceneLoaded = true;
            ApplyBackdrop();   // backdrop, platforms + gauges don't depend on the displayed species
            ApplyGround();
            RenderGauges();
            LoadDisplayMon(_gaugeSpeciesId);
        }

        private void LoadDisplayMon(int id)
        {
            try
            {
                PokemonSpriteEditorViewModel sv = new PokemonSpriteEditorViewModel(true);
                sv.LoadMon(id);
                BattleDisplayEditorViewModel bd = new BattleDisplayEditorViewModel(sv);
                bd.LoadMon(id);
                bd.Detach();       // we only want its computed positions, stop its preview timer

                OpponentSprite = bd.OpponentSprite ?? First(sv.BattleFrontM) ?? First(sv.BattleFrontF);
                PlayerSprite = bd.PlayerSprite ?? First(sv.BattleBackM) ?? First(sv.BattleBackF);
                OpponentLeft = bd.OpponentLeft; OpponentTop = bd.OpponentTop;
                PlayerLeft = bd.PlayerLeft; PlayerTop = bd.PlayerTop;
                _dfX = OpponentLeft + 40; _dfY = OpponentTop + 40;
                _atX = PlayerLeft + 40; _atY = PlayerTop + 40;
                _backYOffset = bd.HasHeights ? bd.BackHeightM : 0;
                _frontYOffset = bd.HasHeights ? bd.FrontHeightM : 0;

                OnPropertyChanged(nameof(OpponentSprite)); OnPropertyChanged(nameof(PlayerSprite));
                OnPropertyChanged(nameof(OpponentLeft)); OnPropertyChanged(nameof(OpponentTop));
                OnPropertyChanged(nameof(PlayerLeft)); OnPropertyChanged(nameof(PlayerTop));

                if (PlayerSprite != null) { (byte[] px, int pw, int ph) = ToRgba(PlayerSprite); _compositor.SetPlayer(px, pw, ph, (int)PlayerLeft, (int)PlayerTop); }
                if (OpponentSprite != null) { (byte[] ex, int ew, int eh) = ToRgba(OpponentSprite); _compositor.SetOpponent(ex, ew, eh, (int)OpponentLeft, (int)OpponentTop); }
                if (IsAnimation && _sceneLoaded && !IsCellPlaying) SceneComposite = _compositor.Render(null);
            }
            catch { /* no ROM / sprite, backdrop just shows the scene without the mons */ }

            static Bitmap First(System.Collections.Generic.IReadOnlyList<Bitmap> l) => l != null && l.Count > 0 ? l[0] : null;
        }

        private void AddStatic(string asset, int left, int top)
        {
            Bitmap bmp = LoadAsset(asset);
            if (bmp == null) return;
            (byte[] rgba, int w, int h) = ToRgba(bmp);
            _compositor.AddStatic(rgba, w, h, left, top);
        }

        private BattleBgRenderer _bgRenderer;
        private System.Collections.Generic.List<string> _backgroundOptions;
        public System.Collections.Generic.List<string> BackgroundOptions => _backgroundOptions ??= BuildBackgroundOptions();
        private static System.Collections.Generic.List<string> BuildBackgroundOptions()
        {
            List<string> l = new System.Collections.Generic.List<string> { "No background" };
            for (int i = 0; i < BattleBgRenderer.BackdropCount; i++) l.Add($"Backdrop {i}");
            return l;
        }
        // Starts on grass: on black every move that swaps the backdrop looks brighter than in game.
        private int _backgroundIndex = 1;
        public int BackgroundIndex
        {
            get => _backgroundIndex;
            set
            {
                if (!Set(ref _backgroundIndex, value)) return;
                ApplyBackdrop();
                if (IsAnimation && _sceneLoaded && !IsCellPlaying) SceneComposite = _compositor.Render(null);
            }
        }

        private BattleGroundRenderer _groundRenderer;
        private System.Collections.Generic.List<string> _terrainOptions;
        /// <summary>Placeholder, then each terrain's ground tray from the battle object archive.</summary>
        public System.Collections.Generic.List<string> TerrainOptions => _terrainOptions ??= BuildTerrainOptions();
        private static System.Collections.Generic.List<string> BuildTerrainOptions()
        {
            List<string> l = new System.Collections.Generic.List<string> { "Placeholder platforms" };
            l.AddRange(BattleGroundRenderer.TerrainNames);
            return l;
        }
        private int _terrainIndex = 3;   // Lawn
        public int TerrainIndex
        {
            get => _terrainIndex;
            set
            {
                if (!Set(ref _terrainIndex, value)) return;
                ApplyGround();
                if (_terrainIndex > 0)
                {
                    int bg = BattleGroundRenderer.BackdropForTerrain(_terrainIndex - 1);
                    if (bg >= 0) BackgroundIndex = bg + 1;   // +1: option 0 is No background
                }
                if (IsAnimation && _sceneLoaded && !IsCellPlaying) SceneComposite = _compositor.Render(null);
            }
        }

        // Falls back to the placeholder art when the ROM's archive can't be read.
        private void ApplyGround()
        {
            _compositor.ClearStatics();
            bool placed = false;
            if (_terrainIndex > 0)
            {
                try
                {
                    (BattleGroundRenderer.GroundImage player, BattleGroundRenderer.GroundImage opponent) = (_groundRenderer ??= new BattleGroundRenderer()).Build(_terrainIndex - 1);
                    if (opponent?.Rgba != null) { _compositor.AddStatic(opponent.Rgba, opponent.Width, opponent.Height, opponent.Left, opponent.Top); placed = true; }
                    if (player?.Rgba != null) { _compositor.AddStatic(player.Rgba, player.Width, player.Height, player.Left, player.Top); placed = true; }
                }
                catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.ApplyGround", ex); placed = false; }
            }
            if (!placed) { AddStatic("platform_opponent.png", 129, 72); AddStatic("platform_you.png", -42, 122); }
        }

        private Bitmap _gaugePlayerImage, _gaugeOpponentImage;
        public Bitmap GaugePlayerImage { get => _gaugePlayerImage; private set => Set(ref _gaugePlayerImage, value); }
        public Bitmap GaugeOpponentImage { get => _gaugeOpponentImage; private set => Set(ref _gaugeOpponentImage, value); }
        public bool HasRealGauges => _gaugePlayerImage != null || _gaugeOpponentImage != null;
        public bool ShowPlaceholderGauges => !HasRealGauges;

        // Move flag 0x40 keeps the gauges up during the animation; 0x80 hides the shadow.
        private bool _hideGaugesThisMove, _hideShadowThisMove;
        public bool GaugesVisible => !(ShowsMove && _hideGaugesThisMove);
        public bool ShadowHidden => ShowsMove && _hideShadowThisMove;
        public bool RealGaugesVisible => HasRealGauges && GaugesVisible;
        public bool PlaceholderGaugesVisible => !HasRealGauges && GaugesVisible;
        public double PlayerGaugeImageLeft => BattleGaugeComposer.CentreOf(BattleGaugeComposer.Kind.PlayerSingle).X - 128;
        public double PlayerHealthFillLeft => BattleGaugeComposer.HealthFillLeft(BattleGaugeComposer.Kind.PlayerSingle);

        private int GetMoveFlagField(int moveId = -1)
        {
            if (moveId < 0) moveId = _fileIndex;
            if ((Archive)_archiveIndex != Archive.MoveAnimation || moveId < 0) return -1;
            try { return new MoveData(moveId).FlagField; }
            catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.GetMoveFlagField", ex); return -1; }
        }

        private int _gaugeHpPercent = 100, _gaugeLevel = 42, _gaugeMaxHp = 250;
        private int _gaugeSpeciesId = 213;   // Shuckle
        public int GaugeHpPercent { get => _gaugeHpPercent; set { if (Set(ref _gaugeHpPercent, Math.Clamp(value, 0, 100))) RaiseHpProps(); } }
        public int GaugeMaxHp { get => _gaugeMaxHp; set { if (Set(ref _gaugeMaxHp, Math.Max(1, value))) RaiseHpProps(); } }
        public int GaugeCurHp => (int)Math.Round(_gaugeMaxHp * _gaugeHpPercent / 100.0);
        public string GaugeHpText => GaugeCurHp + "/" + _gaugeMaxHp;
        private void RaiseHpProps() { OnPropertyChanged(nameof(GaugeHpBarWidth)); OnPropertyChanged(nameof(GaugeHpBrush)); OnPropertyChanged(nameof(GaugeCurHp)); OnPropertyChanged(nameof(GaugeHpText)); }
        public int GaugeLevel { get => _gaugeLevel; set { if (Set(ref _gaugeLevel, Math.Clamp(value, 1, 100))) { OnPropertyChanged(nameof(GaugeLevelText)); OnPropertyChanged(nameof(GaugeLevelImage)); RenderGauges(); } } }

        private string[] _speciesNames;
        public string[] SpeciesNames => _speciesNames ??= GetPokemonNamesOrEmpty();
        public int GaugeSpeciesIndex
        {
            get => _gaugeSpeciesId;
            set
            {
                if (!Set(ref _gaugeSpeciesId, value)) return;
                OnPropertyChanged(nameof(GaugeNameText));
                OnPropertyChanged(nameof(GaugeNameImage));
                RenderGauges();
                if (_sceneLoaded) LoadDisplayMon(value);
            }
        }
        public string GaugeNameText
        {
            get
            {
                string[] n = SpeciesNames;
                string s = (_gaugeSpeciesId >= 0 && _gaugeSpeciesId < n.Length) ? n[_gaugeSpeciesId] : "SHUCKLE";
                return (s ?? "").ToUpperInvariant();
            }
        }
        public string GaugeLevelText => "Lv" + _gaugeLevel;

        public bool GaugeTextIsReal => Data.GaugeTextImages.Available;
        public global::Avalonia.Media.Imaging.Bitmap GaugeNameImage => Data.GaugeTextImages.Name(GaugeNameText);
        public global::Avalonia.Media.Imaging.Bitmap GaugeLevelImage =>
            Data.GaugeTextImages.Level(_gaugeLevel, ROMFiles.BattleGaugeText.Gender.Genderless);
        public global::Avalonia.Media.Imaging.Bitmap GaugeHealthImage =>
            Data.GaugeTextImages.Health((int)Math.Round(_gaugeMaxHp * _gaugeHpPercent / 100.0), _gaugeMaxHp);
        public double GaugeHpBarWidth => 48.0 * _gaugeHpPercent / 100.0;        // groove is 48px, measured off real battles
        // All three colours were read off real Platinum battles.
        private static IBrush Bar(byte r, byte g, byte b) =>
            new SolidColorBrush(global::Avalonia.Media.Color.FromRgb(r, g, b));
        private static readonly IBrush GaugeGreen = Bar(0x18, 0xC3, 0x20);
        private static readonly IBrush GaugeAmber = Bar(0xEB, 0xAA, 0x00);
        private static readonly IBrush GaugeRed = Bar(0xFB, 0x41, 0x10);
        public IBrush GaugeHpBrush => _gaugeHpPercent > 50 ? GaugeGreen : _gaugeHpPercent > 20 ? GaugeAmber : GaugeRed;
        public IBrush GaugeTextBrush => Data.GaugeTextImages.StandInBrush;

        /// <summary>Off, then the twenty frames the player can pick in Options.</summary>
        public List<string> MessageBoxNames { get; } = MessageBoxNameList();
        private static List<string> MessageBoxNameList()
        {
            List<string> names = new List<string> { "Off" };
            names.AddRange(WindowFrames.Names);
            return names;
        }

        private int _messageBoxIndex = 1;
        public int MessageBoxIndex
        {
            get => _messageBoxIndex;
            set { if (Set(ref _messageBoxIndex, Math.Max(0, value))) RenderMessage(); }
        }

        private Bitmap _messageImage;
        /// <summary>The move's "X used Y!" line in the message box. Null shows the plain box.</summary>
        public Bitmap MessageImage
        {
            get => _messageImage;
            private set { if (Set(ref _messageImage, value)) OnPropertyChanged(nameof(PlainMessageBoxVisible)); }
        }
        public bool PlainMessageBoxVisible => _messageImage == null && _messageBoxIndex > 0;

        private void RenderMessage()
        {
            OnPropertyChanged(nameof(PlainMessageBoxVisible));
            if (_messageBoxIndex <= 0 || (Archive)_archiveIndex != Archive.MoveAnimation || _fileIndex <= 0) { MessageImage = null; return; }
            try
            {
                string[] names = SpeciesNames;
                string name = _gaugeSpeciesId >= 0 && _gaugeSpeciesId < names.Length ? names[_gaugeSpeciesId] : "";
                byte[] rgba = BattleMessageText.MoveUsed(_fileIndex, _attackerIsOpponent, name, _messageBoxIndex - 1);
                MessageImage = rgba == null ? null : ImageConverter.FromRgba(rgba, 256, 192);
            }
            catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.RenderMessage", ex); MessageImage = null; }
        }

        private void RenderGauges()
        {
            try
            {
                BattleGroundRenderer r = _groundRenderer ??= new BattleGroundRenderer();
                GaugePlayerImage = Data.GaugeTextImages.Bar(true, GaugeNameText, _gaugeLevel)
                                ?? r.BuildGauge(true)?.ToBitmap();
                GaugeOpponentImage = Data.GaugeTextImages.Bar(false, GaugeNameText, _gaugeLevel)
                               ?? r.BuildGauge(false)?.ToBitmap();
                RenderMessage();
                OnPropertyChanged(nameof(HasRealGauges));
                OnPropertyChanged(nameof(ShowPlaceholderGauges));
                OnPropertyChanged(nameof(RealGaugesVisible));
                OnPropertyChanged(nameof(PlaceholderGaugesVisible));
                OnPropertyChanged(nameof(PlayerGaugeImageLeft));
                OnPropertyChanged(nameof(PlayerHealthFillLeft));
            }
            catch (Exception ex) { AppLogger.Warn("Battle script preview gauges: " + ex.Message); }
        }

        private void ApplyBackdrop()
        {
            byte[] rgb = null;
            if (_backgroundIndex > 0)
            {
                try
                {
                    BattleBgRenderer.BgImage img = (_bgRenderer ??= new BattleBgRenderer()).BuildBackdrop(_backgroundIndex - 1);
                    if (img?.Rgba != null) rgb = BgToBackdrop(img.Rgba, img.Width, img.Height);
                }
                catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.ApplyBackdrop", ex); rgb = null; }
            }
            // Index 0 is plain black, not placeholder art: effects are easiest to judge against black.
            _compositor.SetBackdrop(rgb ?? new byte[256 * 192 * 3]);
        }

        // Top-left 256×192 of the battle background as opaque RGB. The rest stays black.
        private static byte[] BgToBackdrop(byte[] rgba, int w, int h)
        {
            byte[] outp = new byte[256 * 192 * 3];
            for (int y = 0; y < 192; y++)
                for (int x = 0; x < 256; x++)
                {
                    int di = (y * 256 + x) * 3;
                    if (x < w && y < h)
                    {
                        int si = (y * w + x) * 4;
                        outp[di] = rgba[si]; outp[di + 1] = rgba[si + 1]; outp[di + 2] = rgba[si + 2];
                    }
                }
            return outp;
        }

        private static Bitmap LoadAsset(string name)
        {
            try { return new Bitmap(global::Avalonia.Platform.AssetLoader.Open(new System.Uri($"avares://DSPRE.Avalonia/Avalonia/Assets/Battle/{name}"))); }
            catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.LoadAsset", ex); return null; }
        }

        // Avalonia Bitmap to straight RGBA. CopyPixels gives BGRA premultiplied.
        private static (byte[] rgba, int w, int h) ToRgba(Bitmap bmp)
        {
            int w = bmp.PixelSize.Width, h = bmp.PixelSize.Height;
            byte[] buf = new byte[w * h * 4];
            GCHandle handle = System.Runtime.InteropServices.GCHandle.Alloc(buf, System.Runtime.InteropServices.GCHandleType.Pinned);
            try { bmp.CopyPixels(new global::Avalonia.PixelRect(0, 0, w, h), handle.AddrOfPinnedObject(), buf.Length, w * 4); }
            finally { handle.Free(); }
            byte[] rgba = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++)
            {
                byte b = buf[i * 4], g = buf[i * 4 + 1], r = buf[i * 4 + 2], a = buf[i * 4 + 3];
                if (a > 0 && a < 255) { r = (byte)System.Math.Min(255, r * 255 / a); g = (byte)System.Math.Min(255, g * 255 / a); b = (byte)System.Math.Min(255, b * 255 / a); }
                rgba[i * 4] = r; rgba[i * 4 + 1] = g; rgba[i * 4 + 2] = b; rgba[i * 4 + 3] = a;
            }
            return (rgba, w, h);
        }

        private static byte[] LoadRgb(string name, int w, int h)
        {
            Bitmap bmp = LoadAsset(name);
            if (bmp == null) return null;
            (byte[] rgba, int bw, int bh) = ToRgba(bmp);
            byte[] rgb = new byte[w * h * 3];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int sx = bw == w ? x : x * bw / w, sy = bh == h ? y : y * bh / h;   // nearest-fit if sizes differ
                    int si = (sy * bw + sx) * 4, di = (y * w + x) * 3;
                    rgb[di] = rgba[si]; rgb[di + 1] = rgba[si + 1]; rgb[di + 2] = rgba[si + 2];
                }
            return rgb;
        }
    }
}
