using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Lays the party panels' arrangement (file 22) out again at a new size from the tiles it already uses: each panel's
    /// corners and edge tiles where they belong, the plain body tile inside, and the HP bar strip (its "HP" label, bar and
    /// end) where the strip now sits. Only the arrangement changes, so any size works and the tiles stay paintable.
    /// </summary>
    public static class PartyPanelArt
    {
        /// <summary>The arrangement with the lead, normal and empty panels stacked at rows 0, H and 2H, or null with why not.</summary>
        /// <param name="from">The size the arrangement's panels are drawn at now.</param>
        public static byte[] Build(byte[] nscr, PartyPanelSizePatch.Size from, PartyPanelSizePatch.Size to, out string why)
        {
            why = null;
            int map = NitroBgCodec.Find(nscr ?? new byte[0], "NRCS", 0);
            if (map < 0) { why = "The panel art couldn't be read."; return null; }
            int widthTiles = NitroBgCodec.U16(nscr, map + 8) / 8, heightTiles = NitroBgCodec.U16(nscr, map + 10) / 8;
            int entries = map + 0x14;
            if (widthTiles < 32 || 3 * from.Height > heightTiles || 3 * to.Height > heightTiles || from.Width > widthTiles)
            {
                why = "The panel art isn't laid out the way the game reads it.";
                return null;
            }

            byte[] result = (byte[])nscr.Clone();
            ushort At(int x, int y) => (ushort)NitroBgCodec.U16(nscr, entries + (y * widthTiles + x) * 2);
            void Put(int x, int y, ushort e) { result[entries + (y * widthTiles + x) * 2] = (byte)e; result[entries + (y * widthTiles + x) * 2 + 1] = (byte)(e >> 8); }

            for (int block = 0; block < 3; block++)
            {
                int top = block * from.Height, w = from.Width, h = from.Height;
                ushort corner00 = At(0, top), corner10 = At(w - 1, top), corner01 = At(0, top + h - 1), corner11 = At(w - 1, top + h - 1);
                ushort edgeTop = At(w / 2, top), edgeBottom = At(w / 2, top + h - 1), edgeLeft = At(0, top + h / 2), edgeRight = At(w - 1, top + h / 2);
                // The body tile is the one an egg's strip is blanked with, so it is plain by the game's own use of it.
                ushort body = (ushort)(At(w / 2, top + h / 2) & 0xF000 | from.StripTile);
                // The strip as drawn: two label tiles, the bar repeated, its end. The empty panel has none.
                bool strip = block < 2;
                ushort label0 = At(from.StripColumn, top + from.StripRow), label1 = At(from.StripColumn + 1, top + from.StripRow);
                ushort bar = At(from.StripColumn + 2, top + from.StripRow), end = At(from.StripColumn + from.StripLength - 1, top + from.StripRow);

                int newTop = block * to.Height;
                for (int y = 0; y < to.Height; y++)
                    for (int x = 0; x < to.Width; x++)
                    {
                        bool left = x == 0, right = x == to.Width - 1, upper = y == 0, lower = y == to.Height - 1;
                        ushort e = upper && left ? corner00 : upper && right ? corner10 : lower && left ? corner01 : lower && right ? corner11
                                 : upper ? edgeTop : lower ? edgeBottom : left ? edgeLeft : right ? edgeRight : body;
                        if (strip && y == to.StripRow && x >= to.StripColumn && x < to.StripColumn + to.StripLength)
                        {
                            int i = x - to.StripColumn, last = to.StripLength - 1;
                            e = to.StripLength == 1 ? bar : i == last ? end : i == 0 && to.StripLength >= 3 ? label0
                              : i == 1 && to.StripLength >= 3 ? label1 : bar;
                        }
                        Put(x, newTop + y, e);
                    }
            }
            return result;
        }

        /// <summary>
        /// The background (file 17) with the panel area cleared to its own most used tile, so no outline is left where the
        /// panels used to be. The rows from <paramref name="rows"/> down, the message box's frame, stay as they are.
        /// </summary>
        public static byte[] PlainBackground(byte[] nscr, int rows, out string why)
        {
            why = null;
            int map = NitroBgCodec.Find(nscr ?? new byte[0], "NRCS", 0);
            if (map < 0) { why = "The background couldn't be read."; return null; }
            int widthTiles = NitroBgCodec.U16(nscr, map + 8) / 8, heightTiles = NitroBgCodec.U16(nscr, map + 10) / 8;
            int entries = map + 0x14;
            if (widthTiles < 32 || heightTiles < rows) { why = "The background isn't laid out the way the game reads it."; return null; }
            Dictionary<int, int> seen = new Dictionary<int, int>();
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < 32; x++)
                {
                    int e = NitroBgCodec.U16(nscr, entries + (y * widthTiles + x) * 2);
                    seen[e] = seen.TryGetValue(e, out int n) ? n + 1 : 1;
                }
            int fill = seen.OrderByDescending(kv => kv.Value).First().Key;
            byte[] result = (byte[])nscr.Clone();
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < 32; x++)
                {
                    result[entries + (y * widthTiles + x) * 2] = (byte)fill;
                    result[entries + (y * widthTiles + x) * 2 + 1] = (byte)(fill >> 8);
                }
            return result;
        }
    }
}
