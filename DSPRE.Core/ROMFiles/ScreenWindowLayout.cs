using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// A screen's text window tables (8 bytes per window) in arm9 or its overlay. A window can't grow past its tiles, because the next
    /// window's tiles follow.
    /// </summary>
    public sealed class ScreenWindowLayout
    {
        public const int MapColumns = 32;
        /// <summary>Windows can sit below the visible rows where the background scrolls (the HGSS party top screen).</summary>
        public const int MapHeight = 32;

        public sealed class Window
        {
            public string Group, Name;
            public int Layer, X, Y, Width, Height, Palette, BaseTile;
            /// <summary>Its size in the ROM. A new size can't be bigger, since the game only reserved those tiles.</summary>
            public int MaxWidth, MaxHeight;
            /// <summary>For a party Pokémon's window, which Pokémon (1 to 6) and part; 0 and null otherwise.</summary>
            public int Pokemon;
            public string Part;
            /// <summary>Set on a window placed in code, which can move but not change size.</summary>
            public RomInfo.ScreenCodeWindow Code;
            /// <summary>False on a code window whose x or y sits in a register the game shares.</summary>
            public bool CanMoveAcross = true, CanMoveDown = true;
            /// <summary>The least x and y its code can hold: a spot that adds 1 to its immediate can't reach 0.</summary>
            public int MinX, MinY;
            public Window Copy() => (Window)MemberwiseClone();
        }

        /// <summary>A party Pokémon's window name, like "Pokémon 2's HP".</summary>
        public static string PokemonWindow(int pokemon, string part) => $"Pokémon {pokemon}'s {part}";

        public List<Window> Windows { get; } = new List<Window>();
        private readonly RomInfo.ScreenWindowTableSite[] _sites;
        private readonly RomInfo.ScreenCodeWindowSites _code;

        private ScreenWindowLayout(RomInfo.ScreenWindowTableSite[] sites, RomInfo.ScreenCodeWindowSites code) { _sites = sites; _code = code; }

        private IEnumerable<Window> TableWindows => Windows.Where(w => w.Code == null);
        private IEnumerable<Window> CodeWindows => Windows.Where(w => w.Code != null);
        private int TableBytes => TableWindows.Count() * 8;

        /// <summary>Null when this ROM's tables aren't known or don't look like the game's.</summary>
        public static ScreenWindowLayout Read(string screen)
        {
            RomInfo.ScreenWindowTableSite[] sites = RomLocator.ScreenWindowSites(screen);
            RomInfo.ScreenCodeWindowSites code = RomLocator.ScreenCodeWindows(screen);
            if (sites == null && code == null) return null;
            sites ??= Array.Empty<RomInfo.ScreenWindowTableSite>();
            try
            {
                ScreenWindowLayout layout = new ScreenWindowLayout(sites, code);
                foreach (RomInfo.ScreenWindowTableSite site in sites)
                {
                    byte[] bytes = ReadSite(site);
                    if (bytes == null || bytes[0] != site.FirstLayer || (bytes[6] | bytes[7] << 8) != site.FirstBaseTile || !Sane(bytes)) return null;
                    for (int i = 0; i < site.Windows.Length; i++)
                    {
                        int at = i * 8;
                        Window w = new Window
                        {
                            Group = site.Group, Name = site.Windows[i].Name, Pokemon = site.Windows[i].Pokemon, Part = site.Windows[i].Part,
                            Layer = bytes[at], X = bytes[at + 1], Y = bytes[at + 2], Width = bytes[at + 3], Height = bytes[at + 4],
                            Palette = bytes[at + 5], BaseTile = bytes[at + 6] | bytes[at + 7] << 8,
                        };
                        (w.MaxWidth, w.MaxHeight) = (w.Width, w.Height);
                        layout.Windows.Add(w);
                    }
                }
                return code == null || layout.ReadCode() ? layout : null;
            }
            catch (Exception ex) { AppLogger.WarnOnce("ScreenWindowLayout.Read." + screen, ex); return null; }
        }

        // Every spot has to hold what the game's code does, or nothing is offered.
        private bool ReadCode()
        {
            if (_code.Overlay >= 0 && OverlayUtils.IsCompressed(_code.Overlay)) OverlayUtils.Decompress(_code.Overlay);
            byte[] file = File.ReadAllBytes(CodeFile);
            foreach (RomInfo.ScreenCodeWindow code in _code.Windows)
            {
                int x = SpotValue(file, code.X, code.FixedX), y = SpotValue(file, code.Y, code.FixedY);
                if (x < 0 || y < 0 || x + code.Width > MapColumns || y + code.Height > MapHeight) return false;
                Windows.Add(new Window
                {
                    Group = code.Group, Name = code.Name, Layer = code.Layer, X = x, Y = y, Width = code.Width, Height = code.Height,
                    MaxWidth = code.Width, MaxHeight = code.Height, Code = code,
                    CanMoveAcross = code.X.Length > 0, CanMoveDown = code.Y.Length > 0,
                    MinX = LeastValue(code.X), MinY = LeastValue(code.Y),
                });
            }
            return true;
        }

        private string CodeFile => _code.Overlay < 0 ? RomInfo.Arm9Path : OverlayUtils.GetPath(_code.Overlay);

        private static int SpotValue(byte[] file, RomInfo.ScreenWindowSpot spot)
        {
            if (spot.Offset + (spot.Word ? 4 : 2) > file.Length) return -1;
            if (spot.Word) return BitConverter.ToInt32(file, (int)spot.Offset);
            ushort op = BitConverter.ToUInt16(file, (int)spot.Offset);
            return ThumbImm8.Is(op, ThumbImm8.Op.Movs) || ThumbImm8.Is(op, ThumbImm8.Op.Adds) ? ThumbImm8.Value(op) + spot.Add : -1;
        }

        // Every place one position sits has to agree. A window with none stays where the game's register has it.
        private static int SpotValue(byte[] file, RomInfo.ScreenWindowSpot[] spots, int fixedAt)
        {
            if (spots.Length == 0) return fixedAt;
            int[] values = spots.Select(s => SpotValue(file, s)).Distinct().ToArray();
            return values.Length == 1 ? values[0] : -1;
        }

        private static int LeastValue(RomInfo.ScreenWindowSpot[] spots) => spots.Length == 0 ? 0 : spots.Max(s => s.Word ? 0 : s.Add);

        private static bool Fits(RomInfo.ScreenWindowSpot spot, int value) => spot.Word || value - spot.Add is >= 0 and <= 0xFF;

        private static void WriteSpot(string path, byte[] file, RomInfo.ScreenWindowSpot spot, int value)
        {
            if (SpotValue(file, spot) == value) return;
            if (!Fits(spot, value)) throw new InvalidDataException($"{value} doesn't fit the game's code for this window.");
            if (spot.Word) DSUtils.WriteToFile(path, BitConverter.GetBytes(value), spot.Offset);
            else DSUtils.WriteToFile(path, new[] { (byte)(value - spot.Add) }, spot.Offset);
        }

        // A real table always has every window on a background, inside the map and with a palette row.
        private static bool Sane(byte[] bytes)
        {
            for (int at = 0; at + 8 <= bytes.Length; at += 8)
                if (bytes[at] > 7 || bytes[at + 3] == 0 || bytes[at + 4] == 0 || bytes[at + 1] + bytes[at + 3] > MapColumns
                    || bytes[at + 2] + bytes[at + 4] > MapHeight || bytes[at + 5] > 15) return false;
            return true;
        }

        private static string FileOf(RomInfo.ScreenWindowTableSite site) =>
            site.Overlay < 0 ? RomInfo.Arm9Path : OverlayUtils.GetPath(site.Overlay);

        private static byte[] ReadSite(RomInfo.ScreenWindowTableSite site)
        {
            if (site.Overlay >= 0 && OverlayUtils.IsCompressed(site.Overlay)) OverlayUtils.Decompress(site.Overlay);
            string path = FileOf(site);
            if (!File.Exists(path)) return null;
            byte[] all = File.ReadAllBytes(path);
            int length = site.Windows.Length * 8;
            if (site.Offset + length > all.Length) return null;
            byte[] bytes = new byte[length];
            Array.Copy(all, site.Offset, bytes, 0, length);
            return bytes;
        }

        /// <summary>In table order, then each code window's x and y.</summary>
        public byte[] ToBytes()
        {
            List<Window> tables = TableWindows.ToList();
            byte[] bytes = new byte[TableBytes + CodeWindows.Count() * 2];
            for (int i = 0; i < tables.Count; i++)
            {
                Window w = tables[i];
                int at = i * 8;
                bytes[at] = (byte)w.Layer; bytes[at + 1] = (byte)w.X; bytes[at + 2] = (byte)w.Y;
                bytes[at + 3] = (byte)w.Width; bytes[at + 4] = (byte)w.Height; bytes[at + 5] = (byte)w.Palette;
                bytes[at + 6] = (byte)w.BaseTile; bytes[at + 7] = (byte)(w.BaseTile >> 8);
            }
            int code = TableBytes;
            foreach (Window w in CodeWindows)
            {
                bytes[code++] = (byte)w.X;
                bytes[code++] = (byte)w.Y;
            }
            return bytes;
        }

        /// <summary>Sets positions and sizes; the maximum sizes stay as read.</summary>
        public void FromBytes(byte[] bytes)
        {
            List<Window> tables = TableWindows.ToList();
            for (int i = 0; i < tables.Count && i * 8 + 8 <= bytes.Length; i++)
            {
                Window w = tables[i];
                int at = i * 8;
                w.X = bytes[at + 1]; w.Y = bytes[at + 2]; w.Width = bytes[at + 3]; w.Height = bytes[at + 4];
            }
            int code = TableBytes;
            foreach (Window w in CodeWindows)
            {
                if (code + 2 > bytes.Length) return;
                w.X = bytes[code++];
                w.Y = bytes[code++];
            }
        }

        /// <summary>The bytes the files hold now, in <see cref="ToBytes"/> order.</summary>
        public byte[] FileBytes()
        {
            IEnumerable<byte> tables = _sites.SelectMany(site => ReadSite(site) ?? new byte[site.Windows.Length * 8]);
            if (_code == null) return tables.ToArray();
            byte[] file = File.ReadAllBytes(CodeFile);
            return tables.Concat(_code.Windows.SelectMany(w => new[] { (byte)SpotValue(file, w.X, w.FixedX), (byte)SpotValue(file, w.Y, w.FixedY) })).ToArray();
        }

        /// <summary>Writes bytes in <see cref="ToBytes"/> order back to the tables and code.</summary>
        public void WriteFile(byte[] all)
        {
            // Every code spot is checked before anything is written, so a bad value can't leave a half-saved layout.
            if (_code != null)
            {
                int at = _sites.Sum(s => s.Windows.Length * 8);
                foreach (RomInfo.ScreenCodeWindow window in _code.Windows)
                {
                    if (window.X.Any(s => !Fits(s, all[at])) || window.Y.Any(s => !Fits(s, all[at + 1])))
                        throw new InvalidDataException($"{window.Name}'s place doesn't fit the game's code.");
                    at += 2;
                }
            }
            int from = 0;
            foreach (RomInfo.ScreenWindowTableSite site in _sites)
            {
                int length = site.Windows.Length * 8;
                DSUtils.WriteToFile(FileOf(site), all, site.Offset, from, length);
                from += length;
            }
            if (_code == null) return;
            string path = CodeFile;
            byte[] file = File.ReadAllBytes(path);
            foreach (RomInfo.ScreenCodeWindow window in _code.Windows)
            {
                foreach (RomInfo.ScreenWindowSpot spot in window.X) WriteSpot(path, file, spot, all[from]);
                foreach (RomInfo.ScreenWindowSpot spot in window.Y) WriteSpot(path, file, spot, all[from + 1]);
                from += 2;
            }
        }
    }
}
