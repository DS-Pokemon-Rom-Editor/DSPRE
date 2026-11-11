using DSPRE.Graphics;
using System;
using System.Linq;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Screens
{
    // The HGSS PC's bottom screen for each of its eleven cursor layouts (pokeheartgold overlay 14, box graphics a/0/1/9).
    public static partial class GameTextScreens
    {
        private enum BoxButton { White, Red, Frame }

        // A button: its look, tile spot and size, and its bank 24 label.
        private sealed record BoxPiece(BoxButton Look, int X, int Y, int W, int H, int Label);

        // A frame screen, the layer it goes on and its tile spot.
        private sealed record BoxFrame(int Member, int Bg, int X, int Y);

        // What each layout shows, in HgssMenuButtons.Pc page order, with any menu open.
        private sealed record BoxLayout(BoxFrame[] Frames, BoxPiece[] Buttons, bool BoxIcons, int PartyAt, bool Trays = false);

        private const int NoParty = -1, PartyLeft = 0, PartyRight = 152;

        private static BoxPiece Menu(int slot, int label, BoxButton look = BoxButton.White) => new BoxPiece(look, 21, 5 + 3 * slot, 11, 3, label);
        private static BoxPiece Corner(int label) => new BoxPiece(BoxButton.White, 24, 21, 8, 3, label);
        private static BoxPiece BoxMove(int y, int label) => new BoxPiece(BoxButton.White, 21, y, 11, 6, label);

        // ov14_021E7BA4 and the button windows at ov014 0x12BB4; labels from the menu lists at 0x1241C.
        private static readonly BoxLayout[] BoxLayouts =
        {
            new(new[] { new BoxFrame(9, 1, 2, 6) }, new[] { Menu(1, 70), Menu(2, 65), Menu(3, 67), Menu(4, 68), Corner(71) }, false, PartyLeft),
            new(new[] { new BoxFrame(12, 0, 0, 6) }, new[] { BoxMove(14, 79), Corner(76) }, false, NoParty),
            new(Array.Empty<BoxFrame>(), new[] { Menu(1, 69), Menu(2, 65), Menu(3, 67), Menu(4, 68), Corner(71) }, true, NoParty),
            new(Array.Empty<BoxFrame>(), new[] { Menu(0, 61, BoxButton.Red), Menu(1, 65), Menu(2, 66), Menu(3, 67), Menu(4, 68), Corner(71) }, true, NoParty),
            new(new[] { new BoxFrame(11, 1, 0, 0), new BoxFrame(8, 1, 21, 6) },
                new[] { new BoxPiece(BoxButton.Frame, 23, 21, 7, 2, 75), new BoxPiece(BoxButton.White, 0, 21, 17, 3, 63) }, true, PartyRight),
            new(new[] { new BoxFrame(8, 1, 2, 6) },
                new[] { new BoxPiece(BoxButton.Frame, 4, 21, 7, 2, 74), Menu(0, 61, BoxButton.Red), Menu(1, 65), Menu(2, 66), Menu(3, 67), Menu(4, 68), Corner(71) },
                false, PartyLeft),
            new(Array.Empty<BoxFrame>(), new[] { Menu(4, 81), Corner(71) }, true, NoParty),
            new(new[] { new BoxFrame(8, 1, 2, 6) }, new[] { new BoxPiece(BoxButton.Frame, 4, 21, 7, 2, 74), Menu(4, 81), Corner(71) }, false, PartyLeft),
            new(new[] { new BoxFrame(10, 0, 21, 6) },
                new[] { new BoxPiece(BoxButton.Frame, 23, 17, 7, 2, 72), new BoxPiece(BoxButton.Frame, 23, 21, 7, 2, 73) }, true, NoParty),
            new(new[] { new BoxFrame(11, 1, 0, 0) },
                new[] { BoxMove(8, 77), new BoxPiece(BoxButton.White, 21, 14, 11, 3, 26), new BoxPiece(BoxButton.White, 21, 17, 11, 3, 27), Corner(76) },
                true, NoParty, Trays: true),
            new(new[] { new BoxFrame(13, 1, 0, 0) }, new[] { BoxMove(14, 78), Corner(76) }, false, NoParty),
            // Not a touch page: Move with nothing picked, the bottom row PARTY PKMN, MOVE and RETURN (US game capture).
            new(Array.Empty<BoxFrame>(), new[] { new BoxPiece(BoxButton.White, 0, 21, 11, 3, 60), new BoxPiece(BoxButton.Red, 12, 21, 11, 3, 61), Corner(62) },
                true, NoParty),
        };

        private const int IdleMoveLayout = 11;

        // The white button border is tiles 0 to 11 of drawing 64, loaded at tile 1000; red buttons use member 14's tiles.
        private const int SelwinTiles = 64, SelwinColours = 65, SelwinFirst = 1000, ButtonTiles = 14;
        private static readonly int[] WhiteBorder = { 0, 1, 2, 3, 5, 9, 10, 11 };
        private static readonly int[] RedColumns = { 0x57, 0x5B, 0x59, 0x58, 0x5C, 0x5A, 0x6B, 0x69, 0x6A };
        // Sample box: one Pokémon per slot, and a party of six.
        private static readonly int[] BoxSample = Enumerable.Range(1, 30).ToArray();
        private static readonly int[] PartySampleSpecies = { 155, 158, 152, 25, 133, 7 };

        /// <summary>The HGSS PC's bottom screen on one of HgssMenuButtons.Pc's layouts.</summary>
        internal static byte[] BoxLayoutHg(int layout) => WithArchive(() => DrawBox(layout));

        private static byte[] DrawBox(int layout)
        {
            if (layout < 0 || layout >= BoxLayouts.Length) return null;
            BoxLayout look = BoxLayouts[layout];
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.BoxGraphics);
            if (s == null) return null;

            ushort[] colours = NitroBgCodec.ReadColours(m(4));
            Palettes(s, colours, 0, 4);
            ushort[] selwin = NitroBgCodec.ReadColours(m(SelwinColours));
            Palettes(s, selwin, 12, 2);
            Layer(s, m, 3, 3, 0, 1, 0);
            Layer(s, m, 2, 2, 0x8000, 3, 2);
            s.SetPalette(14, DsBgScreen.Row(NitroBgCodec.ReadColours(m(40)), 0));
            s.LoadTiles(0, m(16), 604);
            (int _, ushort[] wall) = NitroBgCodec.ReadMap(m(15));
            for (int y = 0; y < 20; y++)
                for (int x = 0; x < 21; x++)
                {
                    int at = y * 21 + x;
                    if (at < wall.Length)
                        s.Copy(3, x, 1 + y, 1, 1, new[] { (ushort)(((wall[at] & 0x3FF) + 604) | (wall[at] & 0x0C00) | 14 << 12) }, 0, 0, 1);
                }
            // BG1 buttons and BG0 text both read member 14's tiles, with the button border after them.
            foreach ((int bg, int priority, int charBase) in new[] { (1, 1, 0x10000), (0, 0, 0x18000) })
            {
                s.InitLayer(bg, priority, charBase);
                s.LoadTiles(charBase, m(ButtonTiles));
                s.LoadTiles(charBase, m(SelwinTiles), SelwinFirst);
            }
            foreach (BoxFrame f in look.Frames)
            {
                (int w, ushort[] map) = NitroBgCodec.ReadMap(m(f.Member));
                s.Copy(f.Bg, f.X, f.Y, w, map.Length / Math.Max(1, w), map, 0, 0, w);
            }
            foreach (BoxPiece b in look.Buttons.Where(b => b.Look != BoxButton.Frame)) PutBoxButton(s, b);
            byte[] rgba = s.Render();
            if (!look.Frames.Any(f => f.Y == 0)) BoxHeader(rgba, m);

            ushort[] row12 = DsBgScreen.Row(selwin, 0), row2 = DsBgScreen.Row(colours, 2), row1 = DsBgScreen.Row(colours, 1);
            FieldFont font = Font(4) ?? SystemFont;
            foreach (BoxPiece b in look.Buttons)
            {
                (ushort fill, ushort ink, ushort shadow) = b.Look switch
                {
                    BoxButton.White => (row12[4], row12[14], row12[15]),
                    BoxButton.Red => ((ushort)0, row2[9], row2[10]),
                    _ => (row1[13], row1[9], row1[10]),
                };
                if (b.Look == BoxButton.White) Fill(rgba, fill, b.X * 8 + 8, b.Y * 8 + 8, b.W * 8 - 16, b.H * 8 - 16);
                else if (b.Look == BoxButton.Frame) Fill(rgba, fill, b.X * 8, b.Y * 8, b.W * 8, b.H * 8);
                string label = Text(24, b.Label);
                int lines = (label ?? "").Split('\n').Length;
                Print(rgba, font, label, b.X * 8 + b.W * 4, b.Y * 8 + (b.H * 8 - lines * 16) / 2, ink, shadow, Align.Centre);
            }

            if (look.Trays) BoxTrays(rgba, m);
            if (look.BoxIcons)
                for (int i = 0; i < BoxSample.Length; i++) PartyIcon(rgba, BoxSample[i], 24 + 24 * (i % 6), 48 + 24 * (i / 6));
            if (look.PartyAt != NoParty)
                for (int i = 0; i < PartySampleSpecies.Length; i++) PartyIcon(rgba, PartySampleSpecies[i], look.PartyAt + 40 + 40 * (i % 2), 64 + 8 * (i % 2) + 32 * (i / 2));
            return rgba;
        }

        // Six 8-bit box pictures sit 34 apart from 43,27, with colour 8 set to the wallpaper colour and a dot per Pokémon.
        private const int TrayDrawing = 70, TrayColours = 71, WallpaperColours = 75, ObjColours = 69, CountLine = 24;
        private static readonly byte[] DotColour = { 0x0E, 0x0F, 0x05, 0x02, 0x0D, 0x0C, 0x06, 0x0B, 0x0A, 0x09 };
        private static (string Folder, byte[] Colours) _sampleBodyColours;

        private static void BoxTrays(byte[] rgba, Func<int, byte[]> m)
        {
            byte[] ncgr = m(TrayDrawing);
            if (NitroBgCodec.ReadDrawingHeader(ncgr) is not { EightBit: true } head || head.DataAt + 1024 > ncgr.Length) return;
            ushort[] obj = new ushort[48];
            ushort[] tray = NitroBgCodec.ReadColours(m(TrayColours)), wall = NitroBgCodec.ReadColours(m(WallpaperColours));
            Array.Copy(tray, 0, obj, 0, Math.Min(16, tray.Length));
            Array.Copy(wall, 0, obj, 16, Math.Min(32, wall.Length));
            byte[] body = SampleBodyColours();
            for (int box = 0; box < 6; box++)
            {
                byte[] art = new byte[1024];
                Array.Copy(ncgr, head.DataAt, art, 0, 1024);
                for (int i = 0; i < art.Length; i++) if (art[i] == 8) art[i] = (byte)(16 + box);
                if (box == 0)
                    for (int slot = 0; slot < BoxSample.Length && slot < body.Length; slot++)
                    {
                        int px = 10 + 2 * (slot % 6), py = 11 + 2 * (slot / 6);
                        for (int y = py; y < py + 2; y++)
                            for (int x = px; x < px + 2; x++) art[((y >> 3) * 4 + (x >> 3)) * 64 + (y & 7) * 8 + (x & 7)] = (byte)(0x20 + DotColour[body[slot]]);
                    }
                int left = 43 + 34 * box - 16, top = 27 - 16;
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        int v = art[((y >> 3) * 4 + (x >> 3)) * 64 + (y & 7) * 8 + (x & 7)];
                        if (v != 0 && v < obj.Length && left + x >= 0 && left + x < W) DsBgScreen.Put(rgba, ((top + y) * W + left + x) * 4, obj[v]);
                    }
            }
            Func<int, byte[]> packed = i => GraphicAssets.Unsqueeze(m(i));
            PartySprite(rgba, packed, 68, 67, 66, 80, 8, 5, -1, ObjColours);
            PartySprite(rgba, packed, 68, 67, 66, 12, 27, 1, -1, ObjColours);
            PartySprite(rgba, packed, 68, 67, 66, 244, 27, 3, -1, ObjColours);
            PartySprite(rgba, packed, 68, 67, 66, 43, 27, 0, -1, ObjColours);
            PartySprite(rgba, packed, 68, 67, 66, 43, 20, 6, -1, ObjColours);
            // The name and count use the grey of 69's second row (US game capture).
            ushort[] ink = DsBgScreen.Row(NitroBgCodec.ReadColours(m(ObjColours)), 1);
            Print(rgba, SystemFont, Text(24, 6), 8 + 48, 0, ink[1], ink[2], Align.Centre);
            string count = Text(24, CountLine, (_, kind, _) => kind == 51 ? BoxSample.Length.ToString() : null);
            Print(rgba, SystemFont, count, 112 + 20, 0, ink[1], ink[2], Align.Centre);
        }

        // The sample box's body colours, read once per project.
        private static byte[] SampleBodyColours()
        {
            string folder = RomInfo.WorkDir;
            if (_sampleBodyColours.Folder == folder && _sampleBodyColours.Colours != null) return _sampleBodyColours.Colours;
            byte[] colours = new byte[BoxSample.Length];
            for (int i = 0; i < BoxSample.Length; i++)
            {
                try { colours[i] = (byte)Math.Clamp((int)new PokemonPersonalData(BoxSample[i]).Color, 0, DotColour.Length - 1); }
                catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { AppLogger.WarnOnce("GameTextScreens.SampleBodyColours", ex); }
            }
            _sampleBodyColours = (folder, colours);
            return colours;
        }

        // The box name ("BOX 1", bank 24 line 6) is centred on the wallpaper title tiles (21 by 3 from screen row 2) in the system font,
        // in colours 2 and 1 of the wallpaper's row. The page arrows are sprites 66 to 69, sequences 1 and 3, at 12,28 and 156,28.
        private static void BoxHeader(byte[] rgba, Func<int, byte[]> m)
        {
            ushort[] wall = DsBgScreen.Row(NitroBgCodec.ReadColours(m(40)), 0);
            Print(rgba, SystemFont, Text(24, 6), 21 * 4, 2 * 8 + 3 * 4 - 8, wall[2], wall[1], Align.Centre);
            Func<int, byte[]> packed = i => GraphicAssets.Unsqueeze(m(i));
            PartySprite(rgba, packed, 68, 67, 66, 12, 28, 1, -1, 69);
            PartySprite(rgba, packed, 68, 67, 66, 156, 28, 3, -1, 69);
        }

        // <summary>Builds a white button's border (ov14_021F5950) or a red button from its three tile columns (ov14_021F5C94).</summary>
        private static void PutBoxButton(DsBgScreen s, BoxPiece b)
        {
            ushort[] block = new ushort[b.W * b.H];
            for (int y = 0; y < b.H; y++)
                for (int x = 0; x < b.W; x++)
                {
                    int column = x == 0 ? 0 : x == b.W - 1 ? 2 : 1, row = y == 0 ? 0 : y == b.H - 1 ? 2 : 1;
                    if (b.Look == BoxButton.Red)
                    {
                        block[y * b.W + x] = (ushort)(RedColumns[column * 3 + row] | 2 << 12);
                        continue;
                    }
                    int edge = (row, column) switch
                    {
                        (0, 0) => 0, (0, 1) => 1, (0, 2) => 2, (1, 0) => 3, (1, 2) => 4, (2, 0) => 5, (2, 1) => 6, (2, 2) => 7, _ => -1,
                    };
                    block[y * b.W + x] = edge < 0 ? (ushort)0 : (ushort)((SelwinFirst + WhiteBorder[edge]) | 12 << 12);
                }
            s.Put(b.Look == BoxButton.Red ? 1 : 0, block, b.X, b.Y, b.W, b.H);
        }
    }
}
