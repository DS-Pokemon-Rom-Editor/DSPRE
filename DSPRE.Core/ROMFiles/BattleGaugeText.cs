using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The letters a battle writes onto a gauge, read from the pieces sheet in the battle overlay and the
    /// number font.
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

        /// <summary>The "Lv" the gauge shares with the number font.</summary>
        private const int BattleLvRightHalf = 24;

        /// <summary>The slash between the two HP numbers, which the games place at this tile.</summary>
        private const int SlashTile = 0x45;

        /// <summary>What is wrong with a Pokemon, as the gauge shows it.</summary>
        public enum Status { None, Paralysis, Freeze, Sleep, Poison, Burn }

        private sealed class Layout
        {
            public Dictionary<Gender, int[]> Genders;
            public bool GenderIsOneRow;      // two tiles to be drawn four rows lower, not a 2 by 2 block
            public int NoStatusTile;
            public byte DigitShadow;
        }

        // Platinum and HeartGold store each gender block 16 by 16, already lowered four rows, so only half
        // its "Lv" matches the font. Badly poisoned reuses the poison word.
        private static readonly Layout PlatinumAndJohto = new()
        {
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
            Pieces read = Read();
            if (read == null || value < 0 || value >= DigitCount) return null;
            return TileAt(read.NumberFont, NumberFontHeader + value * 32);
        }

        /// <summary>The slash the gauge puts between current and maximum HP.</summary>
        public static Tile Slash()
        {
            Pieces read = Read();
            return read == null ? null : TileAt(Sheet(read), SlashTile * 32);
        }

        /// <summary>
        /// The gender symbol and the "Lv" beside it, as one 16 by 16 block: two tiles across, two down,
        /// in reading order. Genderless gives the same block with nothing where the symbol would be.
        /// </summary>
        public static Tile[] GenderAndLv(Gender gender)
        {
            Pieces read = Read();
            Layout layout = Current;
            if (read == null || !layout.Genders.TryGetValue(gender, out int[] tiles)) return null;
            byte[] sheet = Sheet(read);
            Tile[] stored = tiles.Select(t => TileAt(sheet, t * 32)).ToArray();
            if (!layout.GenderIsOneRow) return stored;

            // Uncovered rows take the strip's background, or the bar would show through.
            byte ground = stored[1].Pixels[0];
            Tile[] block = new[] { new Tile(), new Tile(), new Tile(), new Tile() };
            foreach (Tile t in block) Array.Fill(t.Pixels, ground);
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
            Pieces read = Read();
            int first = status == Status.None ? Current.NoStatusTile : StatusWords.TryGetValue(status, out int at) ? at : -1;
            if (read == null || first < 0) return null;
            return Enumerable.Range(0, StatusTiles)
                             .Select(i => TileAt(Sheet(read), (first + i) * 32))
                             .ToArray();
        }

        /// <summary>
        /// The "Lv" as the number font holds it. The gauge keeps the same picture four rows lower, so
        /// this is what says a gender block was read in the right place.
        /// </summary>
        public static Tile NumberFontLv()
        {
            Pieces read = Read();
            return read == null ? null : TileAt(read.NumberFont, BattleLvRightHalf * 32);
        }

        // A preview can stage a sheet that is not written yet; otherwise the overlay's own.
        private static byte[] Sheet(Pieces read) => BattleGaugePieces.StagedSheet
            ?? (BattleGaugePieces.IsOverlayStaged ? BattleGaugePieces.ReadSheet() : null)
            ?? read.Overlay.AsSpan(read.TilesAt, Math.Min(0x4E * 32, read.Overlay.Length - read.TilesAt)).ToArray();

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

            BattleGaugePieces.Found found = BattleGaugePieces.Locate();
            if (found == null) { _why = BattleGaugePieces.WhyNot; return null; }
            byte[] bytes;
            try { bytes = File.ReadAllBytes(OverlayUtils.GetPath(found.Overlay)); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                _why = "The battle overlay could not be read: " + ex.Message;
                return null;
            }

            _read = new Pieces
            {
                NumberFont = numbers,
                Overlay = bytes,
                TilesAt = found.SheetAt,
                OverlayNumber = found.Overlay,
            };
            return _read;
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

            string[] files = Directory.GetFiles(dir).OrderBy(f => f, StringComparer.Ordinal).ToArray();
            if (entry >= files.Length) { _why = $"The font archive has no entry {entry}."; return null; }

            try
            {
                byte[] raw = File.ReadAllBytes(files[entry]);
                byte[] tiles = raw.Length > 0 && raw[0] == 0x10 ? Lz77.Decompress(raw) : raw;
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

        /// <summary>One tile, by its byte offset: the blob does not start on a tile boundary.</summary>
        private static Tile TileAt(byte[] data, int at)
        {
            Tile made = new Tile();
            if (data == null || at < 0 || at + 32 > data.Length) return made;
            for (int i = 0; i < 32; i++)
            {
                byte b = data[at + i];
                made.Pixels[i * 2] = (byte)(b & 0xF);
                made.Pixels[i * 2 + 1] = (byte)(b >> 4);
            }
            return made;
        }

        /// <summary>Where the gauge's pictures were found, for a status line or a test.</summary>
        public static string Where()
        {
            Pieces read = Read();
            return read == null ? null
                 : $"overlay {read.OverlayNumber}, pictures at 0x{read.TilesAt:X}";
        }
    }
}
