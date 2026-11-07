using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The Pokédex part of the bottom screen palettes patch: sub BG2 gets 256-colour tiles and extended palettes through
    /// hooks on the two Pokédex loaders, which load 256-colour copies and palette twins kept as new archive members.
    /// </summary>
    public static class PokedexBottomPalettes
    {
        public const string Marker = "DEXBOTPALV1\0";

        // Assembled from pokedex_palettes.s.
        private static readonly byte[] Code =
        {
            0x06, 0x2b, 0x0b, 0xd1, 0x30, 0xb4, 0x21, 0x4c, 0x25, 0x68, 0x01, 0x35,
            0x05, 0xd0, 0x01, 0x3d, 0x8d, 0x42, 0x01, 0xd0, 0x08, 0x34, 0xf7, 0xe7,
            0x61, 0x68, 0x30, 0xbc, 0x10, 0xb4, 0x17, 0x4c, 0xa4, 0x46, 0x10, 0xbc,
            0x60, 0x47, 0xf0, 0xb5, 0x82, 0xb0, 0x04, 0x1c, 0x0d, 0x1c, 0x16, 0x1c,
            0x1f, 0x1c, 0x07, 0x98, 0x00, 0x90, 0x08, 0x98, 0x01, 0x90, 0x11, 0x48,
            0x84, 0x46, 0x20, 0x1c, 0xe0, 0x47, 0x04, 0x2e, 0x17, 0xd1, 0x12, 0x49,
            0x0a, 0x68, 0x01, 0x32, 0x13, 0xd0, 0x01, 0x3a, 0xaa, 0x42, 0x01, 0xd0,
            0x08, 0x31, 0xf7, 0xe7, 0x49, 0x68, 0x0b, 0x48, 0x20, 0x58, 0x06, 0x22,
            0x3b, 0x01, 0x01, 0x26, 0xb6, 0x03, 0x9b, 0x19, 0x07, 0x9e, 0x36, 0x01,
            0x00, 0x96, 0x08, 0x9e, 0x01, 0x96, 0x04, 0x4e, 0xb0, 0x47, 0x02, 0xb0,
            0xf0, 0xbd, 0xc0, 0x46, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        };
        private const int TileAt = 0x00, PaletteAt = 0x26;
        private const int TileLoaderAt = 0x7C, PaletteLoaderAt = 0x80, GameLoaderAt = 0x84, ArchiveAt = 0x88, TilesAt = 0x8C, PalettesAt = 0x90;

        // Where PokedexGraphicData keeps its open archive, from the file loader's `movs r2, #0x9f; lsls r2, #2`.
        private const uint ArchiveOffset = 0x27C;

        /// <summary>Drawings the Pokédex loads into sub BG2: scroll_sub_background and entry_sub.</summary>
        public static readonly int[] Drawings = { 29, 34 };
        /// <summary>
        /// Every palette file the Pokédex loads into the bottom palette: the list's default, Sinnoh and National files, the
        /// entry pages' banner_sinnoh and banner_national, and background_sub_2.
        /// </summary>
        public static readonly int[] Colours = { 0, 1, 6, 9, 24, 25 };

        /// <summary>256-colour tiles between BG2's character base 0x8000 and the wheel's at 0x10000.</summary>
        public const int TileLimit = (0x10000 - 0x8000) / 64;

        private static int CodeStart => SyntheticOverlaySpace.HeaderSize;
        private static int TablesStart => CodeStart + Code.Length;
        public static int BlockLength => TablesStart + (Drawings.Length * 8 + 4) + (Colours.Length * 8 + 4);

        private const uint BankH = 0x80;
        private const byte ColourMode256 = 1;

        private static RomLocator.Spot PaletteLoader => RomLocator.Where(RomLocator.Thing.PokedexPaletteLoader);
        private static RomLocator.Spot TileLoader => RomLocator.Where(RomLocator.Thing.PokedexTileLoader);
        private static RomLocator.Spot Banks => RomLocator.Where(RomLocator.Thing.PokedexBottomBanks);
        private static RomLocator.Spot ColourMode => RomLocator.Where(RomLocator.Thing.PokedexBottomColourMode);

        public static bool SupportsCurrentRom => PaletteLoader != null && TileLoader != null && Banks != null && ColourMode != null && BottomPalettesPatch.SupportsCurrentRom;

        private static int Overlay => PaletteLoader.Overlay;
        private static uint OverlayAddress => OverlayUtils.OverlayTable.GetRAMAddress(Overlay);

        private static byte[] ReadOverlay() => File.ReadAllBytes(OverlayUtils.DecompressedPath(Overlay));

        // Every BL in the overlay that goes to target.
        private static List<int> Callers(byte[] ov, uint target)
        {
            List<int> found = new();
            uint start = OverlayAddress;
            for (int at = 0; at + 4 <= ov.Length; at += 2)
                if (ThumbBl.Target(ov, at, start + (uint)at) == target) found.Add(at);
            return found;
        }

        private static uint PatchedCode(byte[] ov)
        {
            if (!SupportsCurrentRom || !File.Exists(Filesystem.ExpArmPath)) return 0;
            if (BitConverter.ToUInt32(ov, Banks.Offset) != BankH) return 0;
            long block = SyntheticOverlaySpace.Blocks(SyntheticOverlaySpace.Bytes(), Marker).Select(b => b.Start).DefaultIfEmpty(-1).First();
            return block < 0 ? 0 : RomInfo.SynthOverlayLoadAddress + (uint)block + (uint)CodeStart;
        }

        public static bool IsApplied
        {
            get
            {
                try { return SupportsCurrentRom && PatchedCode(ReadOverlay()) != 0; }
                catch (Exception ex) when (ex is IOException || ex is ArgumentException) { return false; }
            }
        }

        /// <summary>Member pairs from the block: each drawing and palette file with its 256-colour copy, or null when the patch isn't in.</summary>
        public static (Dictionary<int, int> Drawings, Dictionary<int, int> Colours)? Twins
        {
            get
            {
                if (!IsApplied) return null;
                byte[] synth = SyntheticOverlaySpace.Bytes();
                long block = SyntheticOverlaySpace.Blocks(synth, Marker).First().Start;
                Dictionary<int, int> Read(int at, int room)
                {
                    Dictionary<int, int> pairs = new();
                    for (int n = 0, i = (int)block + at; n < room && i + 8 <= synth.Length && BitConverter.ToInt32(synth, i) != -1; n++, i += 8)
                        pairs[BitConverter.ToInt32(synth, i)] = BitConverter.ToInt32(synth, i + 4);
                    return pairs;
                }
                return (Read(TablesStart, Drawings.Length), Read(TablesStart + Drawings.Length * 8 + 4, Colours.Length));
            }
        }

        public static string WhyNotApplicable()
        {
            if (!SupportsCurrentRom) return "This patch doesn't support this ROM yet.";
            byte[] ov = ReadOverlay();
            if (PatchedCode(ov) != 0) return "The Pokédex part is already applied.";
            if (BitConverter.ToUInt32(ov, Banks.Offset) != 0 || ov[ColourMode.Offset] != 0)
                return "Something else has already changed the Pokédex bottom screen, so DSPRE leaves it alone.";
            if (Callers(ov, OverlayAddress + (uint)TileLoader.Offset).Count == 0 || Callers(ov, OverlayAddress + (uint)PaletteLoader.Offset).Count == 0)
                return "The Pokédex loaders aren't where DSPRE expects them.";
            return null;
        }

        /// <summary>Writes the hooks at <paramref name="blockOffset"/>, routes both loaders' calls to them and turns the extended palettes on.</summary>
        /// <param name="drawingTwins">Each BG2 drawing and its 256-colour copy. The caller writes the copies.</param>
        /// <param name="colourTwins">Each bottom palette file and its extended twin. The caller writes the twins.</param>
        public static void Apply(int blockOffset, IReadOnlyDictionary<int, int> drawingTwins, IReadOnlyDictionary<int, int> colourTwins, uint gameLoader)
        {
            if (blockOffset % 4 != 0) throw new ArgumentException("The block must start on a 4-byte boundary.", nameof(blockOffset));
            byte[] ov = ReadOverlay();
            uint tileLoader = OverlayAddress + (uint)TileLoader.Offset, paletteLoader = OverlayAddress + (uint)PaletteLoader.Offset;
            List<int> tileCalls = Callers(ov, tileLoader), paletteCalls = Callers(ov, paletteLoader);

            uint code = RomInfo.SynthOverlayLoadAddress + (uint)blockOffset + (uint)CodeStart;
            byte[] block = new byte[BlockLength];
            SyntheticOverlaySpace.Header(Marker, BlockLength).CopyTo(block, 0);
            Code.CopyTo(block, CodeStart);
            void Word(int at, uint v) => BitConverter.GetBytes(v).CopyTo(block, CodeStart + at);
            Word(TileLoaderAt, tileLoader | 1);
            Word(PaletteLoaderAt, paletteLoader | 1);
            Word(GameLoaderAt, gameLoader | 1);
            Word(ArchiveAt, ArchiveOffset);
            int tiles = TablesStart, palettes = TablesStart + Drawings.Length * 8 + 4;
            Word(TilesAt, RomInfo.SynthOverlayLoadAddress + (uint)blockOffset + (uint)tiles);
            Word(PalettesAt, RomInfo.SynthOverlayLoadAddress + (uint)blockOffset + (uint)palettes);
            if (drawingTwins.Count > Drawings.Length || colourTwins.Count > Colours.Length)
                throw new ArgumentException("More files than the Pokédex block holds.");
            void Table(int at, IReadOnlyDictionary<int, int> pairs)
            {
                int i = 0;
                foreach ((int from, int to) in pairs)
                {
                    BitConverter.GetBytes(from).CopyTo(block, at + i * 8);
                    BitConverter.GetBytes(to).CopyTo(block, at + i * 8 + 4);
                    i++;
                }
                BitConverter.GetBytes(-1).CopyTo(block, at + i * 8);
            }
            Table(tiles, drawingTwins);
            Table(palettes, colourTwins);
            DSUtils.WriteToFile(Filesystem.ExpArmPath, block, (uint)blockOffset);

            string path = OverlayUtils.GetPath(Overlay);
            foreach (int at in tileCalls) DSUtils.WriteToFile(path, ThumbBl.Make(OverlayAddress + (uint)at, code + TileAt), (uint)at);
            foreach (int at in paletteCalls) DSUtils.WriteToFile(path, ThumbBl.Make(OverlayAddress + (uint)at, code + PaletteAt), (uint)at);
            DSUtils.WriteToFile(path, BitConverter.GetBytes(BankH), (uint)Banks.Offset);
            DSUtils.WriteToFile(path, new[] { ColourMode256 }, (uint)ColourMode.Offset);
        }
    }
}
