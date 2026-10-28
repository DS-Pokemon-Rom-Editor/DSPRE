using System;
using System.Linq;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
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
        private sealed record BoxLayout(BoxFrame[] Frames, BoxPiece[] Buttons, bool BoxIcons, int PartyAt);

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
                false, NoParty),
            new(new[] { new BoxFrame(13, 1, 0, 0) }, new[] { BoxMove(14, 78), Corner(76) }, false, NoParty),
        };

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

            if (look.BoxIcons)
                for (int i = 0; i < BoxSample.Length; i++) PartyIcon(rgba, BoxSample[i], 24 + 24 * (i % 6), 48 + 24 * (i / 6));
            if (look.PartyAt != NoParty)
                for (int i = 0; i < PartySampleSpecies.Length; i++) PartyIcon(rgba, PartySampleSpecies[i], look.PartyAt + 40 + 40 * (i % 2), 64 + 8 * (i % 2) + 32 * (i / 2));
            return rgba;
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
