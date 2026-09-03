using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The letters a battle writes onto a gauge, none of which are in the gauge picture: the name comes
    /// from the system font, the numbers from a small number font, and "Lv" and the gender symbol from
    /// tiles in the battle overlay. The overlay is found by searching it for the number font's own "Lv"
    /// rather than by address, so a romhack that has moved things still works. Diamond and Pearl lay
    /// theirs out differently, so <see cref="IsAvailable"/> says no and the caller falls back.
    /// </summary>
    public static class BattleGaugeText
    {
        /// <summary>An 8 by 8 piece of a picture, one palette index a pixel.</summary>
        public sealed class Tile
        {
            public byte[] Pixels = new byte[64];       // 0 to 15, row by row
            public byte At(int x, int y) => Pixels[y * 8 + x];
        }

        public enum Gender { Female, Male, Genderless }

        // An NCGR keeps its pictures after a 48 byte header, which is a tile and a half, so counting
        // in whole tiles from the front of the file lands halfway through a digit.
        private const int NumberFontHeader = 48;
        private const int DigitCount = 10;

        /// <summary>The "Lv" the gauge shares with the number font, which is what finds the overlay.</summary>
        private const int BattleLvRightHalf = 24;

        // In the overlay, each gender block is 16 by 16: two tiles across, two down. The gauge draws the
        // "Lv" four rows lower than the tile top, which is why only half of it matches the font.
        private static readonly Dictionary<Gender, int[]> GenderBlocks = new()
        {
            [Gender.Female] = new[] { 0x3c, 0x3d, 0x48, 0x49 },
            [Gender.Male] = new[] { 0x3e, 0x3f, 0x4a, 0x4b },
            [Gender.Genderless] = new[] { 0x40, 0x41, 0x4c, 0x4d },
        };

        /// <summary>The slash between the two HP numbers, which the games place at this tile.</summary>
        private const int SlashTile = 0x45;

        /// <summary>What is wrong with a Pokemon, as the gauge shows it.</summary>
        public enum Status { None, Paralysis, Freeze, Sleep, Poison, Burn }

        // Each status word is three tiles across and one row tall, which is why they sit three apart.
        // Both Platinum and HeartGold put them in the same places. Badly poisoned has no word of its
        // own: the games show the same one as ordinary poison.
        private static readonly Dictionary<Status, int> StatusWords = new()
        {
            [Status.None] = 0x26,
            [Status.Paralysis] = 0x29,
            [Status.Freeze] = 0x2c,
            [Status.Sleep] = 0x2f,
            [Status.Poison] = 0x32,
            [Status.Burn] = 0x35,
        };

        /// <summary>How many tiles across a status word is.</summary>
        public const int StatusTiles = 3;

        // ── what the caller asks for ──────────────────────────────────────────────────────────────

        /// <summary>Whether this ROM's gauge text can be drawn from what the ROM holds.</summary>
        public static bool IsAvailable => Read() != null;

        /// <summary>Why not, for telling the user, or null when it can be drawn.</summary>
        public static string Unavailable => Read() != null ? null : _why;

        /// <summary>One digit of the level or the HP, as the gauge draws it.</summary>
        public static Tile Digit(int value)
        {
            var read = Read();
            if (read == null || value < 0 || value >= DigitCount) return null;
            return TileAt(read.NumberFont, NumberFontHeader + value * 32);
        }

        /// <summary>The slash the gauge puts between current and maximum HP.</summary>
        public static Tile Slash()
        {
            var read = Read();
            return read == null ? null : TileAt(read.Overlay, read.TilesAt + SlashTile * 32);
        }

        /// <summary>
        /// The gender symbol and the "Lv" beside it, as one 16 by 16 block: two tiles across, two down,
        /// in reading order. Genderless gives the same block with nothing where the symbol would be.
        /// </summary>
        public static Tile[] GenderAndLv(Gender gender)
        {
            var read = Read();
            if (read == null || !GenderBlocks.TryGetValue(gender, out int[] tiles)) return null;
            return tiles.Select(t => TileAt(read.Overlay, read.TilesAt + t * 32)).ToArray();
        }

        /// <summary>
        /// The word the gauge shows for what is wrong with a Pokemon: three tiles across, left to right.
        /// Asking for None gives the blank the games put there when nothing is wrong.
        /// </summary>
        public static Tile[] StatusWord(Status status)
        {
            var read = Read();
            if (read == null || !StatusWords.TryGetValue(status, out int first)) return null;
            return Enumerable.Range(0, StatusTiles)
                             .Select(i => TileAt(read.Overlay, read.TilesAt + (first + i) * 32))
                             .ToArray();
        }

        /// <summary>
        /// The "Lv" as the number font holds it. The gauge keeps the same picture four rows lower, so
        /// this is what says a gender block was read in the right place.
        /// </summary>
        public static Tile NumberFontLv()
        {
            var read = Read();
            return read == null ? null : TileAt(read.NumberFont, BattleLvRightHalf * 32);
        }

        /// <summary>Forgets what was read, for when a different ROM is opened.</summary>
        public static void Reset() { _read = null; _readFor = null; _why = null; }

        // ── reading it ────────────────────────────────────────────────────────────────────────────

        private sealed class Pieces
        {
            public byte[] NumberFont;
            public byte[] Overlay;
            public int TilesAt;              // byte offset of tile 0 of the gauge's own pictures
            public int OverlayNumber;
        }

        private static Pieces _read;
        private static string _readFor;
        private static string _why;

        /// <summary>Which entry of the font archive holds the number font, or -1 when we do not know.</summary>
        private static int NumberFontEntry => RomInfo.gameFamily switch
        {
            RomInfo.GameFamilies.Plat => 4,
            RomInfo.GameFamilies.HGSS => 5,
            _ => -1,                          // DP is laid out differently, see the note on the class
        };

        private static Pieces Read()
        {
            string forRom = RomInfo.workDir ?? "";
            if (_readFor == forRom) return _read;
            _readFor = forRom;
            _read = null;
            _why = null;

            int entry = NumberFontEntry;
            if (entry < 0)
            {
                _why = "Only Platinum and HeartGold or SoulSilver are read this way so far.";
                return null;
            }

            byte[] numbers = ReadNumberFont(entry);
            if (numbers == null) return null;

            // The gauge keeps the same "Lv" as the number font, four rows lower, so half a tile of it
            // matches byte for byte. That is what says which overlay holds the gauge's pictures.
            byte[] needle = numbers.Skip(BattleLvRightHalf * 32).Take(16).ToArray();
            if (needle.Length < 16 || needle.All(b => b == needle[0]))
            {
                _why = "This ROM's number font does not hold the \"Lv\" the gauge is found by.";
                return null;
            }

            int overlays;
            try { overlays = OverlayUtils.OverlayTable.GetNumberOfOverlays(); }
            catch (Exception ex) { _why = "The overlay table could not be read: " + ex.Message; return null; }

            for (int ov = 0; ov < overlays; ov++)
            {
                byte[] bytes = TryReadOverlay(ov);
                if (bytes == null) continue;

                int at = IndexOf(bytes, needle);
                if (at < 0) continue;

                // That half tile is the lower left of the female block, eight tiles before the slash.
                int tilesAt = at - 0x41 * 32 - 8 * 32;
                if (tilesAt < 0 || tilesAt + 0x4e * 32 > bytes.Length) continue;

                _read = new Pieces
                {
                    NumberFont = numbers,
                    Overlay = bytes,
                    TilesAt = tilesAt,
                    OverlayNumber = ov,
                };
                return _read;
            }

            _why = "The battle overlay holding the gauge's \"Lv\" and gender marks was not found.";
            return null;
        }

        private static byte[] ReadNumberFont(int entry)
        {
            if (!RomInfo.gameDirs.ContainsKey(RomInfo.DirNames.fonts))
            {
                _why = "This ROM has no font archive.";
                return null;
            }

            string dir = RomInfo.gameDirs[RomInfo.DirNames.fonts].unpackedDir;
            if (!Directory.Exists(dir)) { _why = "The font archive is not unpacked."; return null; }

            var files = Directory.GetFiles(dir).OrderBy(f => f, StringComparer.Ordinal).ToArray();
            if (entry >= files.Length) { _why = $"The font archive has no entry {entry}."; return null; }

            try
            {
                byte[] raw = File.ReadAllBytes(files[entry]);
                byte[] tiles = raw.Length > 0 && raw[0] == 0x10 ? NSMBe4.ROM.LZ77_Decompress(raw) : raw;
                if (tiles.Length < (BattleLvRightHalf + 1) * 32)
                {
                    _why = "The number font is smaller than the pictures we read out of it.";
                    return null;
                }
                return tiles;
            }
            catch (Exception ex)
            {
                _why = "The number font could not be read: " + ex.Message;
                return null;
            }
        }

        private static byte[] TryReadOverlay(int number)
        {
            try
            {
                string path = OverlayUtils.GetPath(number);
                if (!File.Exists(path)) return null;
                // Only ds-rom projects, where overlays are already decompressed, are read here. A packed
                // overlay simply will not match, and the search moves on.
                return File.ReadAllBytes(path);
            }
            catch { return null; }
        }

        /// <summary>One tile, by its byte offset: the blob does not start on a tile boundary.</summary>
        private static Tile TileAt(byte[] data, int at)
        {
            var made = new Tile();
            if (data == null || at < 0 || at + 32 > data.Length) return made;
            for (int i = 0; i < 32; i++)
            {
                byte b = data[at + i];
                made.Pixels[i * 2] = (byte)(b & 0xF);
                made.Pixels[i * 2 + 1] = (byte)(b >> 4);
            }
            return made;
        }

        private static int IndexOf(byte[] hay, byte[] needle)
        {
            int last = hay.Length - needle.Length;
            for (int i = 0; i <= last; i++)
            {
                if (hay[i] != needle[0]) continue;
                int j = 1;
                while (j < needle.Length && hay[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }

        /// <summary>Where the gauge's pictures were found, for a status line or a test.</summary>
        public static string Where()
        {
            var read = Read();
            return read == null ? null
                 : $"overlay {read.OverlayNumber}, pictures at 0x{read.TilesAt:X}";
        }
    }
}
