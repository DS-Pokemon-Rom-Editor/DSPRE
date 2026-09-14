using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The letters a battle writes onto a gauge. "Lv" and the gender symbol are found by searching the
    /// battle overlay rather than by address, so a romhack that has moved things still works.
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

        /// <summary>The slash between the two HP numbers, which the games place at this tile.</summary>
        private const int SlashTile = 0x45;

        /// <summary>What is wrong with a Pokemon, as the gauge shows it.</summary>
        public enum Status { None, Paralysis, Freeze, Sleep, Poison, Burn }

        private sealed class Layout
        {
            public int AnchorAt;             // bytes from tile 0 to the half tile of "Lv" the search finds
            public int TableTiles;
            public Dictionary<Gender, int[]> Genders;
            public bool GenderIsOneRow;      // two tiles to be drawn four rows lower, not a 2 by 2 block
            public int NoStatusTile;
            public byte DigitShadow;
        }

        // Platinum and HeartGold store each gender block 16 by 16, already lowered four rows, so only half
        // its "Lv" matches the font. Badly poisoned reuses the poison word.
        private static readonly Layout PlatinumAndJohto = new()
        {
            AnchorAt = 0x49 * 32,
            TableTiles = 0x4e,
            Genders = new()
            {
                [Gender.Female] = new[] { 0x3c, 0x3d, 0x48, 0x49 },
                [Gender.Male] = new[] { 0x3e, 0x3f, 0x4a, 0x4b },
                [Gender.Genderless] = new[] { 0x40, 0x41, 0x4c, 0x4d },
            },
            NoStatusTile = 0x26,
            DigitShadow = 2,
        };

        // Diamond and Pearl keep the mark and "Lv" as one 16 by 8 row that the gauge lowers four rows as it draws.
        private static readonly Layout DiamondPearl = new()
        {
            AnchorAt = 0x3d * 32 + 16,
            TableTiles = 0x46,
            Genders = new()
            {
                [Gender.Female] = new[] { 0x3c, 0x3d },
                [Gender.Male] = new[] { 0x3e, 0x3f },
                [Gender.Genderless] = new[] { 0x40, 0x41 },
            },
            GenderIsOneRow = true,
            NoStatusTile = 0x38,
            DigitShadow = 1,
        };

        private static Layout Current => RomInfo.gameFamily == RomInfo.GameFamilies.DP ? DiamondPearl : PlatinumAndJohto;

        private static readonly Dictionary<Status, int> StatusWords = new()
        {
            [Status.Paralysis] = 0x29,
            [Status.Freeze] = 0x2c,
            [Status.Sleep] = 0x2f,
            [Status.Poison] = 0x32,
            [Status.Burn] = 0x35,
        };

        /// <summary>The colour a digit's shadow placeholder becomes in this game's battle.</summary>
        public static byte DigitShadow => Current.DigitShadow;

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
            var layout = Current;
            if (read == null || !layout.Genders.TryGetValue(gender, out int[] tiles)) return null;
            var stored = tiles.Select(t => TileAt(read.Overlay, read.TilesAt + t * 32)).ToArray();
            if (!layout.GenderIsOneRow) return stored;

            // Uncovered rows take the strip's background, or the bar would show through.
            byte ground = stored[1].Pixels[0];
            var block = new[] { new Tile(), new Tile(), new Tile(), new Tile() };
            foreach (var t in block) Array.Fill(t.Pixels, ground);
            for (int side = 0; side < 2; side++)
                for (int y = 0; y < 8; y++)
                    Array.Copy(stored[side].Pixels, y * 8, block[y < 4 ? side : side + 2].Pixels, ((y + 4) % 8) * 8, 8);
            return block;
        }

        /// <summary>
        /// The word the gauge shows for what is wrong with a Pokemon: three tiles across, left to right.
        /// Asking for None gives the blank the games put there when nothing is wrong.
        /// </summary>
        public static Tile[] StatusWord(Status status)
        {
            var read = Read();
            int first = status == Status.None ? Current.NoStatusTile : StatusWords.TryGetValue(status, out int at) ? at : -1;
            if (read == null || first < 0) return null;
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
            RomInfo.GameFamilies.DP => 4,
            RomInfo.GameFamilies.Plat => 4,
            RomInfo.GameFamilies.HGSS => 5,
            _ => -1,
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
                _why = "This game's gauge text is not read yet.";
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

                // That half tile belongs to the female "Lv", whose place in the table differs by family.
                var layout = Current;
                int tilesAt = at - layout.AnchorAt;
                if (tilesAt < 0 || tilesAt + layout.TableTiles * 32 > bytes.Length) continue;

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

            // Not every editor that draws a gauge unpacks the fonts first, and a failed read is kept for the ROM.
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.fonts });
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
