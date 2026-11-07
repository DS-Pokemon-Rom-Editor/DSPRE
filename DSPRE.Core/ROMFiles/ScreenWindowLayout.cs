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
            /// <summary>The furthest column and lowest row its code can hold.</summary>
            public int MaxX = MapColumns, MaxY = MapHeight;
            /// <summary>Set when it has to stay inside a block of tiles, such as the frame it's drawn into.</summary>
            public bool Bounded;
            /// <summary>Why its code stops it moving, or null.</summary>
            public string LockReason;
            /// <summary>The toolbox key of the patch that lets it move, while that patch isn't in.</summary>
            public string LockedFor;
            public Window Copy() => (Window)MemberwiseClone();
        }

        /// <summary>A party Pokémon's window name, like "Pokémon 2's HP".</summary>
        public static string PokemonWindow(int pokemon, string part) => $"Pokémon {pokemon}'s {part}";

        public List<Window> Windows { get; } = new List<Window>();
        private readonly RomInfo.ScreenWindowTableSite[] _sites;
        private RomInfo.ScreenCodeWindowSites _code;

        private readonly string _screen;

        private ScreenWindowLayout(string screen, RomInfo.ScreenWindowTableSite[] sites, RomInfo.ScreenCodeWindowSites code) { _screen = screen; _sites = sites; _code = code; }

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
                ScreenWindowLayout layout = new ScreenWindowLayout(screen, sites, code);
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
                    layout.CheckFollows(site);
                }
                return code == null || layout.ReadCode() ? layout : null;
            }
            catch (Exception ex) { AppLogger.WarnOnce("ScreenWindowLayout.Read." + screen, ex); return null; }
        }

        /// <summary>Takes the code windows' spots again after a patch moved them, keeping the positions being edited.</summary>
        public void RereadCode()
        {
            ScreenWindowLayout fresh = Read(_screen);
            if (fresh?._code == null) return;
            _code = fresh._code;
            foreach (Window w in Windows.Where(w => w.Code != null))
            {
                if (fresh.Windows.FirstOrDefault(f => f.Code != null && f.Group == w.Group && f.Name == w.Name) is not Window f) continue;
                (w.Code, w.CanMoveAcross, w.CanMoveDown, w.LockedFor) = (f.Code, f.CanMoveAcross, f.CanMoveDown, f.LockedFor);
                (w.MinX, w.MinY, w.MaxX, w.MaxY) = (f.MinX, f.MinY, f.MaxX, f.MaxY);
            }
        }

        // Every spot has to hold what the game's code does, or nothing is offered.
        private bool ReadCode()
        {
            if (_code.Overlay >= 0 && OverlayUtils.IsCompressed(_code.Overlay)) OverlayUtils.Decompress(_code.Overlay);
            byte[] file = File.ReadAllBytes(CodeFile);
            bool wholeButtons = _code.Windows.Any(c => c.FreeWithBagButtons) && BagButtonsPatch.IsApplied;
            foreach (RomInfo.ScreenCodeWindow code in _code.Windows)
            {
                int x = SpotValue(file, code.X, code.FixedX), y = SpotValue(file, code.Y, code.FixedY);
                if (x < 0 || y < 0 || x + code.Width > MapColumns || y + code.Height > MapHeight) return false;
                if (code.Packed?.Any(s => SpotValue(file, s) != Packed(x, y)) == true) return false;
                Windows.Add(new Window
                {
                    Group = code.Group, Name = code.Name, Layer = code.Layer, X = x, Y = y, Width = code.Width, Height = code.Height,
                    MaxWidth = code.Width, MaxHeight = code.Height, Code = code,
                    CanMoveAcross = code.X.Length > 0, CanMoveDown = code.Y.Length > 0, LockedFor = code.LockedFor,
                    MinX = Math.Max(0, LeastValue(code.X)), MinY = Math.Max(0, LeastValue(code.Y)),
                    MaxX = MostValue(code.X, MapColumns), MaxY = MostValue(code.Y, MapHeight),
                });
                if (code.Within is (int ax, int ay, int aw, int ah) && !(wholeButtons && code.FreeWithBagButtons))
                {
                    Window w = Windows[^1];
                    (w.MinX, w.MinY) = (Math.Max(w.MinX, ax), Math.Max(w.MinY, ay));
                    (w.MaxX, w.MaxY) = (Math.Min(w.MaxX, ax + aw - w.Width), Math.Min(w.MaxY, ay + ah - w.Height));
                    w.Bounded = true;
                }
            }
            return true;
        }

        // A window the code places again moves only where that code is known and still agrees with the table.
        private void CheckFollows(RomInfo.ScreenWindowTableSite site)
        {
            if (site.Follows == null && site.Locked == null) return;
            byte[] file = File.ReadAllBytes(FileOf(site));
            foreach (Window w in Windows.Where(w => w.Group == site.Group))
            {
                RomInfo.ScreenWindowFollow follow = site.Follows?.FirstOrDefault(f => f.Window == w.Name);
                string where = site.Overlay < 0 ? "the ARM9" : $"overlay {site.Overlay}";
                int FromX(RomInfo.ScreenWindowSpot s) => s.From == null ? 0 : Windows.FirstOrDefault(o => o.Group == site.Group && o.Name == s.From)?.X ?? 0;
                int FromY(RomInfo.ScreenWindowSpot s) => s.From == null ? 0 : Windows.FirstOrDefault(o => o.Group == site.Group && o.Name == s.From)?.Y ?? 0;
                bool locked = site.Locked?.Contains(w.Name) == true
                    || (follow != null && (follow.X.Any(s => SpotValue(file, s) != w.X - FromX(s)) || follow.Y.Any(s => SpotValue(file, s) != w.Y - FromY(s))
                                           || follow.Packed.Any(s => SpotValue(file, s) != Packed(w.X, w.Y))));
                if (locked)
                {
                    w.CanMoveAcross = w.CanMoveDown = false;
                    w.LockReason = $"This window's position is hard-coded in {where}, and DSPRE can't move it on this ROM yet.";
                }
                else if (follow != null)
                {
                    (w.MinX, w.MinY) = (Math.Max(w.MinX, LeastValue(follow.X)), Math.Max(w.MinY, LeastValue(follow.Y)));
                    (w.MaxX, w.MaxY) = (MostValue(follow.X, MapColumns), MostValue(follow.Y, MapHeight));
                    // A packed offset holds 2 × (32 × row + column) in one byte, so the row can't pass 3.
                    if (follow.Packed.Length > 0) w.MaxY = Math.Min(w.MaxY, 0xFF / 2 / MapColumns);
                    if (follow.KeepsRow) w.CanMoveDown = false;
                    if (follow.Within is (int ax, int ay, int aw, int ah))
                    {
                        (w.MinX, w.MinY, w.MaxX, w.MaxY) = (ax, ay, Math.Min(w.MaxX, ax + aw - w.Width), Math.Min(w.MaxY, ay + ah - w.Height));
                        w.Bounded = true;
                    }
                }
            }
        }

        private static int Packed(int x, int y) => 2 * (y * MapColumns + x);

        private string CodeFile => _code.Overlay < 0 ? RomInfo.Arm9Path : OverlayUtils.GetPath(_code.Overlay);

        private static string SyntheticPath => Filesystem.ExpArmPath;

        // A patch routine's spot is read from the synthetic overlay.
        private static byte[] FileFor(byte[] file, RomInfo.ScreenWindowSpot spot) => spot.Synthetic ? SyntheticOverlaySpace.BytesIfAny() ?? Array.Empty<byte>() : file;

        private static int SpotValue(byte[] file, RomInfo.ScreenWindowSpot spot)
        {
            file = FileFor(file, spot);
            if (spot.Offset + (spot.Word ? 4 : 2) > file.Length) return -1;
            if (spot.Word)
            {
                int word = BitConverter.ToInt32(file, (int)spot.Offset) + spot.Add;
                return word % spot.Scale != 0 ? -1 : word / spot.Scale;
            }
            if (spot.Byte)
            {
                int b = file[(int)spot.Offset] + spot.Add;
                return b < 0 || b % spot.Scale != 0 ? -1 : b / spot.Scale;
            }
            if (spot.Half)
            {
                int half = BitConverter.ToInt16(file, (int)spot.Offset) + spot.Add;
                return half < 0 || half % spot.Scale != 0 ? -1 : half / spot.Scale;
            }
            if (!IsInstruction(file, spot)) return -1;
            ushort op = BitConverter.ToUInt16(file, (int)spot.Offset);
            if (spot.Imm3) return ThumbImm3.Value(op) + spot.Add;
            int at = ThumbImm8.Value(op) + spot.Add;
            return at < 0 || at % spot.Scale != 0 ? -1 : at / spot.Scale;
        }

        private static bool IsInstruction(byte[] file, RomInfo.ScreenWindowSpot spot)
        {
            if (spot.Offset + 2 > file.Length) return false;
            ushort op = BitConverter.ToUInt16(file, (int)spot.Offset);
            return spot.Imm3 ? ThumbImm3.Is(op)
                             : ThumbImm8.Is(op, ThumbImm8.Op.Movs) || ThumbImm8.Is(op, ThumbImm8.Op.Adds) || ThumbImm8.Is(op, ThumbImm8.Op.Subs);
        }

        /// <summary>The number a spot stores for a position.</summary>
        internal static int Stored(RomInfo.ScreenWindowSpot spot, int value) => value * spot.Scale - spot.Add;

        // Every place one position sits has to agree. A window with none stays where the game's register has it.
        private static int SpotValue(byte[] file, RomInfo.ScreenWindowSpot[] spots, int fixedAt)
        {
            if (spots.Length == 0) return fixedAt;
            int[] values = spots.Select(s => SpotValue(file, s)).Distinct().ToArray();
            return values.Length == 1 ? values[0] : -1;
        }

        private static int LeastValue(RomInfo.ScreenWindowSpot[] spots) =>
            spots.Length == 0 ? 0 : spots.Max(s => s.Word || s.From != null ? 0 : (int)Math.Ceiling(s.Add / (double)s.Scale));

        private static int MostValue(RomInfo.ScreenWindowSpot[] spots, int limit) =>
            spots.Aggregate(limit, (most, s) => s.Word || s.Half || s.From != null ? most : Math.Min(most, (int)Math.Floor(((s.Imm3 ? 7 : 0xFF) + s.Add) / (double)s.Scale)));

        /// <summary>Whether a spot's instruction or number can hold a position.</summary>
        internal static bool Fits(RomInfo.ScreenWindowSpot spot, int value) =>
            spot.Word || (spot.Half ? Stored(spot, value) is >= short.MinValue and <= short.MaxValue
                                    : Stored(spot, value) is int n && n >= 0 && n <= (spot.Imm3 ? 7 : 0xFF));

        private static void WriteSpot(string path, byte[] file, RomInfo.ScreenWindowSpot spot, int value)
        {
            if (spot.Synthetic) (path, file) = (SyntheticPath, FileFor(file, spot));
            if (SpotValue(file, spot) == value) return;
            if (!Fits(spot, value)) throw new InvalidDataException($"{value} doesn't fit the game's code for this window.");
            // Another patch may have put a call where the instruction was.
            if (!spot.Word && !spot.Half && !spot.Byte && !IsInstruction(file, spot))
                throw new InvalidDataException("The game's code for this window has changed. Reopen the Screens editor.");
            if (spot.Word) DSUtils.WriteToFile(path, BitConverter.GetBytes(Stored(spot, value)), spot.Offset);
            else if (spot.Half) DSUtils.WriteToFile(path, BitConverter.GetBytes((short)Stored(spot, value)), spot.Offset);
            else if (spot.Byte) DSUtils.WriteToFile(path, new[] { (byte)Stored(spot, value) }, spot.Offset);
            else if (spot.Imm3)
            {
                ushort op = BitConverter.ToUInt16(file, (int)spot.Offset);
                DSUtils.WriteToFile(path, BitConverter.GetBytes(ThumbImm3.With(op, Stored(spot, value))), spot.Offset);
            }
            else DSUtils.WriteToFile(path, new[] { (byte)Stored(spot, value) }, spot.Offset);
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

        private static IEnumerable<(RomInfo.ScreenWindowFollow Follow, RomInfo.ScreenWindowSpot Spot, int Value)> Spots(
            RomInfo.ScreenWindowTableSite site, byte[] all, int start)
        {
            int At(string name, int axis)
            {
                int i = Array.FindIndex(site.Windows, w => w.Name == name);
                return i < 0 ? 0 : all[start + i * 8 + axis];
            }
            foreach (RomInfo.ScreenWindowFollow follow in site.Follows ?? Array.Empty<RomInfo.ScreenWindowFollow>())
            {
                if (Array.FindIndex(site.Windows, w => w.Name == follow.Window) < 0) continue;
                int x = At(follow.Window, 1), y = At(follow.Window, 2);
                foreach (RomInfo.ScreenWindowSpot spot in follow.X) yield return (follow, spot, x - (spot.From == null ? 0 : At(spot.From, 1)));
                foreach (RomInfo.ScreenWindowSpot spot in follow.Y) yield return (follow, spot, y - (spot.From == null ? 0 : At(spot.From, 2)));
                foreach (RomInfo.ScreenWindowSpot spot in follow.Packed) yield return (follow, spot, Packed(x, y));
            }
        }

        // The game draws into a window, then its copy, so a copy takes its window's place and size.
        private byte[] WithCopies(byte[] all)
        {
            byte[] bytes = (byte[])all.Clone();
            int start = 0;
            foreach (RomInfo.ScreenWindowTableSite site in _sites)
            {
                for (int i = 0; i < site.Windows.Length; i++)
                {
                    string name = site.Windows[i].Name;
                    if (!name.StartsWith("Copy of ", StringComparison.Ordinal)) continue;
                    int of = Array.FindIndex(site.Windows, w => string.Equals(w.Name, name.Substring(8), StringComparison.OrdinalIgnoreCase));
                    if (of >= 0) Array.Copy(bytes, start + of * 8 + 1, bytes, start + i * 8 + 1, 4);
                }
                start += site.Windows.Length * 8;
            }
            return bytes;
        }

        /// <summary>Writes bytes in <see cref="ToBytes"/> order back to the tables and code.</summary>
        public void WriteFile(byte[] all)
        {
            all = WithCopies(all);
            // Every code spot is checked before anything is written, so a bad value can't leave a half-saved layout.
            int start = 0;
            foreach (RomInfo.ScreenWindowTableSite site in _sites)
            {
                Dictionary<uint, (string Window, int Value)> shared = new Dictionary<uint, (string, int)>();
                foreach ((RomInfo.ScreenWindowFollow follow, RomInfo.ScreenWindowSpot spot, int value) in Spots(site, all, start))
                {
                    if (!Fits(spot, value)) throw new InvalidDataException($"The game can't store {follow.Window}'s new position.");
                    if (shared.TryGetValue(spot.Offset, out (string Window, int Value) other) && other.Value != value)
                        throw new InvalidDataException($"{other.Window} and {follow.Window} have to move together.");
                    shared[spot.Offset] = (follow.Window, value);
                }
                start += site.Windows.Length * 8;
            }
            if (_code != null)
            {
                int at = _sites.Sum(s => s.Windows.Length * 8);
                foreach (RomInfo.ScreenCodeWindow window in _code.Windows)
                {
                    if (window.X.Any(s => !Fits(s, all[at])) || window.Y.Any(s => !Fits(s, all[at + 1])))
                        throw new InvalidDataException($"The game can't store {window.Name}'s new position.");
                    at += 2;
                }
            }
            int from = 0;
            foreach (RomInfo.ScreenWindowTableSite site in _sites)
            {
                int length = site.Windows.Length * 8;
                DSUtils.WriteToFile(FileOf(site), all, site.Offset, from, length);
                byte[] siteFile = site.Follows == null ? null : File.ReadAllBytes(FileOf(site));
                foreach ((RomInfo.ScreenWindowFollow _, RomInfo.ScreenWindowSpot spot, int value) in Spots(site, all, from))
                    WriteSpot(FileOf(site), siteFile, spot, value);
                from += length;
            }
            if (_code == null) return;
            string path = CodeFile;
            byte[] file = File.ReadAllBytes(path);
            foreach (RomInfo.ScreenCodeWindow window in _code.Windows)
            {
                foreach (RomInfo.ScreenWindowSpot spot in window.X) WriteSpot(path, file, spot, all[from]);
                foreach (RomInfo.ScreenWindowSpot spot in window.Y) WriteSpot(path, file, spot, all[from + 1]);
                foreach (RomInfo.ScreenWindowSpot spot in window.Packed ?? Array.Empty<RomInfo.ScreenWindowSpot>())
                    WriteSpot(path, file, spot, Packed(all[from], all[from + 1]));
                from += 2;
            }
        }
    }
}
