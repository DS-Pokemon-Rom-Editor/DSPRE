using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Where the summary screen puts its text windows and its HP and EXP bars. The windows are the game's window
    /// templates in arm9; the bars' column and row are immediates in the code that draws them.
    /// </summary>
    public sealed class SummaryScreenLayout
    {
        public const int MapColumns = 32, MapRows = 24;

        /// <summary>One window template: background (main 0-3, sub 4-7), tile rectangle, palette row and first tile.</summary>
        public sealed class Window
        {
            public int Layer, X, Y, Width, Height, Palette, BaseTile;
            /// <summary>The font picked with the fonts patch, or -1 for the game's.</summary>
            public int Font = -1;
            public int EndTile => BaseTile + Width * Height;
            public Window Copy() => (Window)MemberwiseClone();
        }

        public sealed class Table
        {
            public string Page;
            public List<Window> Windows = new();
        }

        public sealed class Bar
        {
            public int X, Y, Tiles, Layer, FullTile;
        }

        public List<Table> Tables = new();
        public Bar Hp, Exp;
        /// <summary>Whether the bytes include fonts, as they do once the fonts patch is applied.</summary>
        public bool HasFonts;

        public IEnumerable<Window> AllWindows => Tables.SelectMany(t => t.Windows);

        public static bool SupportsCurrentRom => RomLocator.SummaryScreenSites != null;

        private static SummaryScreenSites Sites => RomLocator.SummaryScreenSites ?? throw new NotSupportedException("The summary screen layout isn't known for this ROM.");

        /// <summary>The always-there windows, which share their backgrounds with every page's own.</summary>
        public Table Static => Tables.FirstOrDefault(t => t.Page == "Static");

        // The bytes the layout lives in, in a fixed order: each table, then the bars' column and row instructions.
        private static IEnumerable<(uint Offset, int Length)> Ranges(SummaryScreenSites s)
        {
            foreach (SummaryWindowTableSite t in s.Windows) yield return (t.Offset, t.Count * 8);
            foreach (uint at in new[] { s.Hp.X, s.Hp.Y, s.Exp.X, s.Exp.Y }) yield return (at, 2);
        }

        private static int LayoutLength(SummaryScreenSites s) => Ranges(s).Sum(r => r.Length);
        private static int WindowCount(SummaryScreenSites s) => s.Windows.Sum(t => t.Count);

        /// <summary>The layout's bytes as arm9 holds them now, then one font byte per window when the fonts patch is applied.</summary>
        public static byte[] ReadBytes()
        {
            using MemoryStream all = new();
            foreach ((uint offset, int length) in Ranges(Sites)) all.Write(ARM9.ReadBytes(offset, length));
            int[] fonts = SummaryFontPatch.ReadFonts(FromBytes(all.ToArray()).AllWindows.ToList());
            if (fonts != null) all.Write(fonts.Select(f => (byte)f).ToArray());
            return all.ToArray();
        }

        /// <summary>Writes layout bytes from <see cref="ToBytes"/> or <see cref="ReadBytes"/> into arm9.</summary>
        public static void WriteBytes(byte[] bytes)
        {
            using (ARM9.Writer writer = new())
            {
                int at = 0;
                foreach ((uint offset, int length) in Ranges(Sites))
                {
                    writer.BaseStream.Position = offset;
                    writer.Write(bytes, at, length);
                    at += length;
                }
            }
            SummaryScreenLayout layout = FromBytes(bytes);
            if (layout.HasFonts) SummaryFontPatch.WriteFonts(layout.AllWindows.ToList(), layout.AllWindows.Select(w => w.Font).ToList());
        }

        public static SummaryScreenLayout FromBytes(byte[] bytes)
        {
            SummaryScreenSites s = Sites;
            SummaryScreenLayout layout = new();
            int at = 0;
            foreach (SummaryWindowTableSite site in s.Windows)
            {
                Table table = new() { Page = site.Page };
                for (int i = 0; i < site.Count; i++, at += 8)
                    table.Windows.Add(new Window
                    {
                        Layer = bytes[at], X = bytes[at + 1], Y = bytes[at + 2], Width = bytes[at + 3], Height = bytes[at + 4],
                        Palette = bytes[at + 5], BaseTile = bytes[at + 6] | bytes[at + 7] << 8,
                    });
                layout.Tables.Add(table);
            }
            layout.Hp = new Bar { X = bytes[at], Y = bytes[at + 2], Tiles = s.Hp.Tiles, Layer = s.Hp.Layer, FullTile = s.Hp.FullTile };
            layout.Exp = new Bar { X = bytes[at + 4], Y = bytes[at + 6], Tiles = s.Exp.Tiles, Layer = s.Exp.Layer, FullTile = s.Exp.FullTile };
            int fontsAt = LayoutLength(s);
            layout.HasFonts = bytes.Length >= fontsAt + WindowCount(s);
            if (layout.HasFonts)
                foreach (Window w in layout.AllWindows)
                {
                    byte font = bytes[fontsAt++];
                    w.Font = font == 0xFF ? -1 : font;
                }
            return layout;
        }

        /// <summary>
        /// This layout written over <paramref name="original"/>, the bytes it was read from, so each bar instruction
        /// keeps its opcode and only its immediate changes.
        /// </summary>
        public byte[] ToBytes(byte[] original)
        {
            byte[] bytes = (byte[])original.Clone();
            int at = 0;
            foreach (Table table in Tables)
                foreach (Window w in table.Windows)
                {
                    bytes[at] = (byte)w.Layer; bytes[at + 1] = (byte)w.X; bytes[at + 2] = (byte)w.Y;
                    bytes[at + 3] = (byte)w.Width; bytes[at + 4] = (byte)w.Height; bytes[at + 5] = (byte)w.Palette;
                    bytes[at + 6] = (byte)w.BaseTile; bytes[at + 7] = (byte)(w.BaseTile >> 8);
                    at += 8;
                }
            bytes[at] = (byte)Hp.X; bytes[at + 2] = (byte)Hp.Y;
            bytes[at + 4] = (byte)Exp.X; bytes[at + 6] = (byte)Exp.Y;
            at += 8;
            if (HasFonts && bytes.Length >= at + AllWindows.Count())
                foreach (Window w in AllWindows) bytes[at++] = (byte)w.Font;
            return bytes;
        }

        /// <summary>
        /// Why a window can't go here, or null. It must stay on its background map and not share tiles with a window
        /// shown at the same time (two pages are never up together), and must stay within the tiles the game's layout already uses.
        /// </summary>
        /// <param name="original">The layout as the ROM had it when the editor opened.</param>
        public string WhyNot(Table table, int index, Window wanted, SummaryScreenLayout original)
        {
            if (wanted.Width < 1 || wanted.Height < 1) return "A text box needs at least one tile across and down.";
            if (wanted.X + wanted.Width > MapColumns || wanted.Y + wanted.Height > MapRows) return "That goes off the screen.";
            if (original.TileLimits().TryGetValue(wanted.Layer, out int limit) && wanted.EndTile > limit)
                return $"That needs {wanted.EndTile - limit} more tiles than this screen has for text.";
            int tableAt = Tables.IndexOf(table);
            foreach ((Table other, int i, Window w) in UpWith(table))
            {
                if (ReferenceEquals(other, table) && i == index) continue;
                if (w.Layer != wanted.Layer || w.Width == 0 || w.Height == 0) continue;
                if (wanted.BaseTile >= w.EndTile || w.BaseTile >= wanted.EndTile) continue;
                Window was = original.Tables[tableAt].Windows[index], otherWas = original.Tables[Tables.IndexOf(other)].Windows[i];
                if (was.BaseTile < otherWas.EndTile && otherWas.BaseTile < was.EndTile) continue;
                return "It would need tiles another text box on this page already uses.";
            }
            return null;
        }

        // Every window up at the same time as the windows of this table.
        private IEnumerable<(Table, int, Window)> UpWith(Table table)
        {
            IEnumerable<Table> tables = table.Page == "Static" ? Tables : new[] { Static, table };
            foreach (Table t in tables.Where(t => t != null))
                for (int i = 0; i < t.Windows.Count; i++) yield return (t, i, t.Windows[i]);
        }

        /// <summary>
        /// Gives every window its tiles again, one after another in table order, so a window that grew gets room.
        /// Each table starts where it started in <paramref name="original"/>; windows the game had share tiles keep
        /// sharing them. The always-there windows must still end before the first page window's tiles, and every
        /// page must still end within the game's own last tile. Returns why it can't, or null when it has.
        /// </summary>
        public string Repack(SummaryScreenLayout original)
        {
            Dictionary<int, int> limits = original.TileLimits();
            Dictionary<int, int> pageStart = new();
            for (int t = 0; t < Tables.Count; t++)
            {
                if (Tables[t].Page == "Static") continue;
                foreach (Window w in original.Tables[t].Windows.Where(w => w.Width > 0 && w.Height > 0))
                    pageStart[w.Layer] = Math.Min(pageStart.TryGetValue(w.Layer, out int s) ? s : int.MaxValue, w.BaseTile);
            }
            for (int t = 0; t < Tables.Count; t++)
            {
                List<Window> now = Tables[t].Windows, was = original.Tables[t].Windows;
                foreach (int layer in was.Select(w => w.Layer).Distinct())
                {
                    List<int> at = Enumerable.Range(0, now.Count).Where(i => was[i].Layer == layer && was[i].Width > 0 && was[i].Height > 0).ToList();
                    if (at.Count == 0) continue;
                    int cursor = at.Min(i => was[i].BaseTile);
                    foreach (int i in at)
                    {
                        List<int> sharing = at.Where(j => was[j].BaseTile == was[i].BaseTile).ToList();
                        if (sharing[0] != i)
                        {
                            now[i].BaseTile = now[sharing[0]].BaseTile;
                            continue;
                        }
                        // Windows sharing tiles get room for the biggest of them.
                        now[i].BaseTile = cursor;
                        cursor += sharing.Max(j => now[j].Width * now[j].Height);
                    }
                    if (Tables[t].Page == "Static" && pageStart.TryGetValue(layer, out int start) && cursor > start)
                        return $"The text boxes on every page would need {cursor - start} more tiles than they have.";
                    if (limits.TryGetValue(layer, out int limit) && cursor > limit)
                        return $"That needs {cursor - limit} more tiles than this screen has for text.";
                }
            }
            return null;
        }

        /// <summary>The end of the tiles each background's windows use.</summary>
        public Dictionary<int, int> TileLimits()
        {
            Dictionary<int, int> limits = new();
            foreach (Window w in Tables.SelectMany(t => t.Windows))
                limits[w.Layer] = Math.Max(limits.TryGetValue(w.Layer, out int end) ? end : 0, w.EndTile);
            return limits;
        }
    }
}
