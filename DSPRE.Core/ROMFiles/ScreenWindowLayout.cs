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
            public Window Copy() => (Window)MemberwiseClone();
        }

        /// <summary>A party Pokémon's window name, like "Pokémon 2's HP".</summary>
        public static string PokemonWindow(int pokemon, string part) => $"Pokémon {pokemon}'s {part}";

        public List<Window> Windows { get; } = new List<Window>();
        private readonly RomInfo.ScreenWindowTableSite[] _sites;

        private ScreenWindowLayout(RomInfo.ScreenWindowTableSite[] sites) { _sites = sites; }

        /// <summary>Null when this ROM's tables aren't known or don't look like the game's.</summary>
        public static ScreenWindowLayout Read(string screen)
        {
            RomInfo.ScreenWindowTableSite[] sites = RomLocator.ScreenWindowSites(screen);
            if (sites == null) return null;
            try
            {
                ScreenWindowLayout layout = new ScreenWindowLayout(sites);
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
                return layout;
            }
            catch (Exception ex) { AppLogger.WarnOnce("ScreenWindowLayout.Read." + screen, ex); return null; }
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

        /// <summary>In table order.</summary>
        public byte[] ToBytes()
        {
            byte[] bytes = new byte[Windows.Count * 8];
            for (int i = 0; i < Windows.Count; i++)
            {
                Window w = Windows[i];
                int at = i * 8;
                bytes[at] = (byte)w.Layer; bytes[at + 1] = (byte)w.X; bytes[at + 2] = (byte)w.Y;
                bytes[at + 3] = (byte)w.Width; bytes[at + 4] = (byte)w.Height; bytes[at + 5] = (byte)w.Palette;
                bytes[at + 6] = (byte)w.BaseTile; bytes[at + 7] = (byte)(w.BaseTile >> 8);
            }
            return bytes;
        }

        /// <summary>Sets positions and sizes; the maximum sizes stay as read.</summary>
        public void FromBytes(byte[] bytes)
        {
            for (int i = 0; i < Windows.Count && i * 8 + 8 <= bytes.Length; i++)
            {
                Window w = Windows[i];
                int at = i * 8;
                w.X = bytes[at + 1]; w.Y = bytes[at + 2]; w.Width = bytes[at + 3]; w.Height = bytes[at + 4];
            }
        }

        /// <summary>The bytes the files hold now, in <see cref="ToBytes"/> order.</summary>
        public byte[] FileBytes() => _sites.SelectMany(site => ReadSite(site) ?? new byte[site.Windows.Length * 8]).ToArray();

        /// <summary>Writes bytes in <see cref="ToBytes"/> order back to the tables.</summary>
        public void WriteFile(byte[] all)
        {
            int from = 0;
            foreach (RomInfo.ScreenWindowTableSite site in _sites)
            {
                int length = site.Windows.Length * 8;
                DSUtils.WriteToFile(FileOf(site), all, site.Offset, from, length);
                from += length;
            }
        }
    }
}
