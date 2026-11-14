using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed partial class ScreenWindowLayoutViewModel
    {

        private readonly RomInfo.PartyGraphicsMembers _members = RomLocator.PartyGraphicsMembers;
        private const RomInfo.DirNames PartyGraphics = RomInfo.DirNames.PartyMenuGraphics;
        // Rebuilds start from the saved art, at the size it was saved at.
        private PartyPanelSizePatch.Size _savedSize;

        // Rebuilds start from the saved file, never from an unsaved rebuild.
        private static byte[] Saved(int member) => NitroBgCodec.Inflate(new ScriptNarc(PartyGraphics).GetFromDisk(member));

        public void RebuildCursor()
        {
            if (_panels == null || _members == null) return;
            (byte[] Cells, byte[] Tiles) built = PartyCursorFrames.Build(Saved(_members.CursorCells), Saved(_members.CursorTiles),
                                                                         _panels.Width, _panels.Height, out string why);
            if (built.Cells == null) { StatusText = why; return; }
            StatusText = "";
            Record(narc => { narc.Put(_members.CursorCells, built.Cells); narc.Put(_members.CursorTiles, built.Tiles); });
        }

        public void RebuildPanelArt()
        {
            if (_panels == null || _members == null) return;
            byte[] art = PartyPanelArt.Build(Saved(_members.PanelArt), _savedSize ?? _panels.Size, _panels.Size, out string why);
            if (art == null) { StatusText = why; return; }
            StatusText = "";
            PlaceWindows();
            Record(narc => narc.Put(_members.PanelArt, art));
        }

        public void RebuildBackground()
        {
            if (_panels == null || _members == null) return;
            byte[] background = PartyPanelArt.PlainBackground(Saved(_members.Background), _members.PanelRows, out string why);
            if (background == null) { StatusText = why; return; }
            StatusText = "";
            Record(narc => narc.Put(_members.Background, background));
        }

        // Pokémon 1's windows (tiles), icon and ball (pixels) in its panel as the editor opened.
        private readonly Dictionary<string, (int X, int Y)> _inPanel;
        private readonly (int X, int Y) _iconInPanel, _ballInPanel;

        private Dictionary<string, (int X, int Y)> InPanel()
        {
            Dictionary<string, (int X, int Y)> places = new Dictionary<string, (int X, int Y)>();
            if (_panels == null) return places;
            (int px, int py) = _panels.Panel(0);
            foreach (ScreenWindowLayout.Window w in _layout.Windows.Where(w => w.Group == SingleGroup && w.Pokemon == 1))
                places[w.Part] = (w.X - px, w.Y - py);
            return places;
        }

        private (int X, int Y) SpriteInPanel(PartyPanelLayout.Sprite sprite)
        {
            (int px, int py) = _panels.Panel(0);
            (int x, int y) = _panels.SpriteAt(0, sprite);
            return (x - px * 8, y - py * 8);
        }

        private const int IconTiles = 4;

        // Uses the ROM's places when everything fits; otherwise stacks the icon, level, name and HP.
        private void PlaceWindows()
        {
            PartyPanelSizePatch.Size size = _panels.Size;
            bool fits = _layout.Windows.Where(w => w.Group == SingleGroup && w.Pokemon == 1 && w.Part != "HP bar").All(w =>
                _inPanel.TryGetValue(w.Part, out (int X, int Y) at) && at.X >= 0 && at.Y >= 0
                && at.X + w.Width <= size.Width && at.Y + w.Height <= size.Height);
            for (int n = 0; n < PartyPanelLayout.PartySize; n++)
            {
                (int px, int py) = _panels.Panel(n);
                ScreenWindowLayout.Window[] windows = _layout.Windows.Where(w => w.Group == SingleGroup && w.Pokemon == n + 1).ToArray();
                ScreenWindowLayout.Window level = windows.FirstOrDefault(w => w.Part == "level");
                (int sx, int sy) = _panels.SpriteAt(n, PartyPanelLayout.Sprite.Status);
                (int statusX, int statusY) = level == null ? (0, 0) : (sx - level.X * 8, sy - level.Y * 8);

                (int iconX, int iconY) = fits ? _iconInPanel : (IconTiles * 4, IconTiles * 4);
                (int ballX, int ballY) = (_ballInPanel.X - _iconInPanel.X + iconX, _ballInPanel.Y - _iconInPanel.Y + iconY);
                (int ix, int iy) = _panels.SpriteAt(n, PartyPanelLayout.Sprite.Icon);
                _panels.MoveSprite(n, PartyPanelLayout.Sprite.Icon, px * 8 + iconX - ix, py * 8 + iconY - iy);
                (int bx, int by) = _panels.SpriteAt(n, PartyPanelLayout.Sprite.Ball);
                _panels.MoveSprite(n, PartyPanelLayout.Sprite.Ball, px * 8 + Math.Max(8, ballX) - bx, py * 8 + Math.Max(8, ballY) - by);
                int hpRow = size.StripRow + 3 <= size.Height ? size.StripRow + 1 : size.StripRow - 2;
                foreach (ScreenWindowLayout.Window w in windows)
                {
                    (int x, int y) = w.Part switch
                    {
                        // Leaves room for the two "HP" label tiles when the strip has them.
                        "HP bar" => (size.StripColumn + (size.StripLength >= 3 ? 2 : 0), size.StripRow),
                        _ when fits => _inPanel[w.Part],
                        "name" => (0, IconTiles),
                        "level" => (IconTiles, 1),
                        "HP" or "comment" => (size.Width - w.Width, hpRow),
                        _ => (w.X - px, w.Y - py),
                    };
                    w.X = Math.Clamp(px + Math.Clamp(x, 0, Math.Max(0, size.Width - w.Width)), 0, ScreenWindowLayout.MapColumns - w.Width);
                    w.Y = Math.Clamp(py + Math.Clamp(y, 0, Math.Max(0, size.Height - w.Height)), 0, ScreenWindowLayout.MapHeight - w.Height);
                }
                if (level != null)
                {
                    (int cx, int cy) = _panels.SpriteAt(n, PartyPanelLayout.Sprite.Status);
                    _panels.MoveSprite(n, PartyPanelLayout.Sprite.Status, level.X * 8 + statusX - cx, level.Y * 8 + statusY - cy);
                }
            }
            SyncPanels();
            foreach (ScreenWindowRow row in Windows) row.Refresh();
            Warn();
        }

        public bool PanelsResizable => _panels?.Resizable == true;
        public bool CanRebuildGraphics => PanelsResizable && _members != null;
        public bool CanOfferSizePatch => _panels != null && !_panels.Resizable && !RomInfo.IsHGE && PartyPanelSizePatch.SupportsCurrentRom;

        private PendingArchiveWrites.Target _fontTable;
        private Dictionary<string, int> _fonts;

        private void OpenFontTable()
        {
            if (_screen != "party" || _fontTable != null || RomInfo.IsHGE || !PartyFontPatch.IsApplied) return;
            _fontTable = new PendingArchiveWrites.Target("fonts", PartyFontPatch.ReadTable, PartyFontPatch.WriteTable);
            _fonts = PartyFontPatch.FontsIn(PartyFontPatch.ReadTable(), _layout.Windows);
        }

        public bool OffersFontPatch => _screen == "party" && _fontTable == null && !RomInfo.IsHGE && PartyFontPatch.SupportsCurrentRom;

        private static IReadOnlyList<string> FontNames => GameFonts.Names;

        // The comment line only shows on screens the Layout doesn't have, so it takes the name font.
        private void SetFont(int font)
        {
            if (_fonts == null || Math.Max(font, 0) == Math.Max(_fonts["name"], 0)) return;
            foreach (string part in PartyFontPatch.Parts) _fonts[part] = font;
            Commit();
        }

        private void OnPatchStateChanged(object sender, EventArgs e)
        {
            // Any patch can move the code the windows' spots sit in.
            if (_layout != null)
            {
                _layout.RereadCode();
                Fill();
            }
            if (_fontTable == null && PartyFontPatch.IsApplied)
            {
                OpenFontTable();
                Raise(nameof(OffersFontPatch));
                Fill();
            }
            if (_panels == null || _panels.Resizable || !PartyPanelSizePatch.IsApplied) return;
            _panels.ReadPatch();
            Refresh();
        }

        public decimal PanelWidth { get => _panels?.Width ?? 0; set => ChangePanelSize(s => FitStrip(s with { Width = (int)value })); }
        public decimal PanelHeight { get => _panels?.Height ?? 0; set => ChangePanelSize(s => FitStrip(s with { Height = (int)value })); }

        private static PartyPanelSizePatch.Size FitStrip(PartyPanelSizePatch.Size s)
        {
            int column = Math.Min(s.StripColumn, Math.Max(0, s.Width - 1));
            int length = Math.Clamp(s.StripLength, 1, s.Width - column);
            return s with { StripColumn = column, StripLength = length, StripRow = Math.Min(s.StripRow, s.Height - 1) };
        }
        public bool HpBarLengthEditable => _panels?.HpBarLengthEditable == true;
        public int MaxHpBarLength => _layout?.Windows.FirstOrDefault(w => w.Group == SingleGroup && w.Part == "HP bar")?.MaxWidth * 8
                                     ?? PartyPanelLayout.GameHpBarLength;
        public decimal HpBarLength
        {
            get => _panels?.HpBarLength ?? PartyPanelLayout.GameHpBarLength;
            set
            {
                int length = Math.Clamp((int)value, 1, MaxHpBarLength);
                if (_panels == null || !_panels.HpBarLengthEditable || length == _panels.HpBarLength) return;
                _panels.HpBarLength = length;
                Commit();
            }
        }

        public decimal StripColumn { get => _panels?.Size.StripColumn ?? 0; set => ChangePanelSize(s => s with { StripColumn = (int)value }); }
        public decimal StripRow { get => _panels?.Size.StripRow ?? 0; set => ChangePanelSize(s => s with { StripRow = (int)value }); }
        public decimal StripLength { get => _panels?.Size.StripLength ?? 0; set => ChangePanelSize(s => s with { StripLength = (int)value }); }

        private void ChangePanelSize(Func<PartyPanelSizePatch.Size, PartyPanelSizePatch.Size> change)
        {
            if (_panels == null || !_panels.Resizable) return;
            PartyPanelSizePatch.Size size = change(_panels.Size);
            if (size == _panels.Size) return;
            if (PartyPanelSizePatch.WhyNotSize(size) is string why) { StatusText = why; RaiseSize(); return; }
            StatusText = "";
            _panels.Resize(size);
            Commit();
        }

        // Each range depends on the others, so no box can go past what the code holds.
        private PartyPanelSizePatch.Size Now => _panels?.Size ?? PartyPanelSizePatch.Original;
        public int MinPanelWidth => 1;
        public int MaxPanelWidth => Math.Min(ScreenWindowLayout.MapColumns, PartyPanelSizePatch.MaxArea / Now.Height);
        public int MinPanelHeight => 1;
        public int MaxPanelHeight => Math.Min(PartyPanelSizePatch.MaxHeight, PartyPanelSizePatch.MaxArea / Now.Width);
        public int MaxStripColumn => Now.Width - Now.StripLength;
        public int MaxStripRow => Now.Height - 1;
        public int MaxStripLength => Now.Width - Now.StripColumn;

        private void RaiseSize()
        {
            foreach (string n in new[] { nameof(PanelsResizable), nameof(CanRebuildGraphics), nameof(CanOfferSizePatch), nameof(MinPanelWidth), nameof(MaxPanelWidth),
                                         nameof(MinPanelHeight), nameof(MaxPanelHeight), nameof(MaxStripColumn), nameof(MaxStripRow),
                                         nameof(MaxStripLength), nameof(PanelWidth), nameof(PanelHeight), nameof(StripColumn),
                                         nameof(StripRow), nameof(StripLength), nameof(HpBarLength), nameof(HpBarLengthEditable) })
                Raise(n);
        }

        private const string BallGroup = "Poké Balls", BallColour = "#E57373";

        private void MoveBall(ScreenWindowRow row, int x, int y)
        {
            int dx = x - row.Window.X, dy = y - row.Window.Y;
            if (dx == 0 && dy == 0) return;
            foreach (int n in _moveAll ? Enumerable.Range(0, PartyPanelLayout.PartySize) : new[] { row.Window.Pokemon - 1 })
            {
                (int bx, int by) = _panels.BallButton(n);
                _panels.MoveBallButton(n, bx + dx, by + dy);
            }
            SyncPanels();
            Raise(nameof(HasUnsavedChanges));
            _ = RedrawAsync();
        }

        private const string SpriteGroup = "Sprites", SpriteColour = "#C8C8C8";
        private static readonly (PartyPanelLayout.Sprite Sprite, string Part, int Width, int Height)[] SpriteRows =
        {
            (PartyPanelLayout.Sprite.Icon, "icon", 4, 4), (PartyPanelLayout.Sprite.Ball, "ball", 2, 2),
            (PartyPanelLayout.Sprite.Status, "status icon", 4, 1),
        };

        private static PartyPanelLayout.Sprite SpriteOf(ScreenWindowRow row) => SpriteRows.First(r => r.Part == row.Window.Part).Sprite;

        // Sprites move in whole tiles so their offset inside a tile is kept.
        private void MoveSprite(ScreenWindowRow row, int x, int y)
        {
            int dx = x - row.Window.X, dy = y - row.Window.Y;
            if (dx == 0 && dy == 0) return;
            PartyPanelLayout.Sprite sprite = SpriteOf(row);
            foreach (int n in _moveAll ? Enumerable.Range(0, PartyPanelLayout.PartySize) : new[] { row.Window.Pokemon - 1 })
                _panels.MoveSprite(n, sprite, dx * 8, dy * 8);
            SyncPanels();
            Raise(nameof(HasUnsavedChanges));
            _ = RedrawAsync();
        }

        private void SyncPanels()
        {
            if (_panels == null) return;
            foreach (ScreenWindowRow row in Windows.Where(IsPanel))
            {
                (row.Window.X, row.Window.Y) = _panels.Panel(row.Window.Pokemon - 1);
                (row.Window.Width, row.Window.Height) = (_panels.Width, _panels.Height);
                row.Refresh();
            }
            foreach (ScreenWindowRow row in Windows.Where(IsSprite))
            {
                (int cx, int cy) = _panels.SpriteAt(row.Window.Pokemon - 1, SpriteOf(row));
                row.Window.X = (int)Math.Floor((cx - row.Window.Width * 4) / 8.0);
                row.Window.Y = (int)Math.Floor((cy - row.Window.Height * 4) / 8.0);
                row.Refresh();
            }
            foreach (ScreenWindowRow row in Windows.Where(IsBall))
            {
                (row.Window.X, row.Window.Y) = _panels.BallButton(row.Window.Pokemon - 1);
                row.Refresh();
            }
            RefreshCursor();
        }
    }
}
