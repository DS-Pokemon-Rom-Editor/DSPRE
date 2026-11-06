using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Gives the bottom backgrounds of a screen 256-colour tiles and 16 extended palettes, through routines that load
    /// a new archive member into the extended palettes.
    /// </summary>
    public static class BottomPalettesPatch
    {
        public const string Marker = "BOTTOMPALV1\0";

        // Assembled from bottom_palettes.s.
        private static readonly byte[] Code =
        {
            0xf0, 0xb5, 0x82, 0xb0, 0x04, 0x1c, 0x07, 0x9d, 0x08, 0x9e, 0x00, 0x95,
            0x01, 0x96, 0x06, 0x4f, 0xb8, 0x47, 0x20, 0x1c, 0x05, 0x49, 0x06, 0x22,
            0x05, 0x4b, 0x00, 0x25, 0x00, 0x95, 0x01, 0x96, 0x04, 0x4f, 0xb8, 0x47,
            0x02, 0xb0, 0xf0, 0xbd, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        };
        private const int LoaderAt = 0x28, MemberAt = 0x2C, OffsetAt = 0x30, GameAt = 0x34;
        // The routine's movs r2 load type: PAL_LOAD_SUB_BGEXT, or PAL_LOAD_MAIN_BGEXT for a screen on the top engine.
        private const int LoadTypeAt = 0x16;
        private const byte SubExtendedLoad = 6, MainExtendedLoad = 2;
        private static int CodeStart => SyntheticOverlaySpace.HeaderSize;
        public static int BlockLength => CodeStart + Code.Length;

        // Assembled from recolour_palettes.s.
        private static readonly byte[] RecolourCode =
        {
            0xf0, 0xb5, 0x12, 0x4b, 0x98, 0x47, 0x0f, 0x4f, 0x80, 0x20, 0x38, 0x70,
            0x10, 0x4a, 0x00, 0x26, 0x0d, 0x49, 0x00, 0x24, 0x65, 0x02, 0x55, 0x19,
            0x09, 0xc9, 0x09, 0xc5, 0x09, 0xc9, 0x09, 0xc5, 0x09, 0xc9, 0x09, 0xc5,
            0x09, 0xc9, 0x09, 0xc5, 0x01, 0x34, 0x0b, 0x48, 0x84, 0x42, 0xf1, 0xdb,
            0x01, 0x20, 0x40, 0x03, 0x12, 0x18, 0x01, 0x36, 0x06, 0x48, 0x86, 0x42,
            0xe8, 0xdb, 0x82, 0x20, 0x38, 0x70, 0xf0, 0xbd, 0x48, 0x02, 0x00, 0x04,
            0x00, 0x04, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        };
        private const int RecolourLoadAt = 0x4C, RecolourFirstAt = 0x50, RecolourSlotsAt = 0x54, RecolourRowsAt = 0x58;
        public static int RecolourLength => CodeStart + RecolourCode.Length;
        // Where extended palette slot 0 is while bank H is mapped to the CPU.
        private const uint BankHForCpu = 0x06898000;

        // Maps VRAM bank H to the BG extended palettes of the bottom screen.
        private const uint BankH = 0x80;
        // The top engine's: bank E for its BG extended palettes, and bank D beside A for 128 KB more BG VRAM.
        private const uint BankE = 0x10, BankA = 0x01, BanksAD = 0x09;
        private const byte ColourMode256 = 1;
        // Each BG reads the extended palette slot of its number, 8 KB apiece.
        private const uint SlotSize = 0x2000;

        public const int Palettes = 16, PaletteSize = 256;

        /// <summary>One bottom palette load: its call, and the drawing and palette file it pairs with.</summary>
        /// <param name="Call">The BL that loads the palette.</param>
        /// <param name="TileLimit">256-colour tiles between the character base and the next thing in VRAM.</param>
        public sealed record Load(RomLocator.Thing Call, int Drawing, int Colours, int TileLimit);

        /// <summary>Where the bottom backgrounds of a screen keep their pieces in one game, and where its code sets them up.</summary>
        /// <param name="Name">The screen, as messages name it.</param>
        /// <param name="Archive">The archive the pieces are members of.</param>
        /// <param name="CharBase">The character base the patch moves the layer to, or -1 to keep it.</param>
        /// <param name="Banks">The bottom screen's BG extended palette word in the screen's VRAM bank table. Every spot is in one file, the ARM9 or an overlay.</param>
        /// <param name="ColourModes">The colour mode bytes of the layers' templates.</param>
        /// <param name="Slots">The extended palette slots, by BG number, each load fills.</param>
        public sealed record Screen(string Name, DirNames Archive, Load[] Loads, int CharBase, RomLocator.Thing Banks,
                                    RomLocator.Thing[] ColourModes, int[] Slots)
        {
            /// <summary>A call that rewrites the first <see cref="RecolourRows"/> palette rows while the screen is open, or null.</summary>
            public RomLocator.Thing? Recolour { get; init; }
            public int RecolourRows { get; init; }

            /// <summary>The screen's layer is on the top engine, which the game shows on the bottom screen.</summary>
            public bool TopEngine { get; init; }

            /// <summary>
            /// The size, member and layer instructions of the layer's tile load, when another layer shares its drawing: the
            /// layer then loads a 256-colour copy.
            /// </summary>
            public RomLocator.Thing? TileLoad { get; init; }

            /// <summary>The windows on the layer whose pixels become 16-colour sprites, moved to BG0 to keep 16-colour pixels.</summary>
            public RomLocator.Thing? SpriteWindows { get; init; }

            public int Drawing => Loads[0].Drawing;
            public int Colours => Loads[0].Colours;
            public int TileLimit => Loads[0].TileLimit;
            public int BlockCount => BlockLengths.Count;

            /// <summary>The synthetic overlay blocks Apply writes: one per load and slot, then the recolour routine.</summary>
            public IReadOnlyList<int> BlockLengths => Enumerable.Repeat(BlockLength, Loads.Length * Slots.Length)
                .Concat(Recolour == null ? Array.Empty<int>() : new[] { RecolourLength }).ToList();
        }

        private static Screen One(string name, DirNames archive, Load load, RomLocator.Thing banks, RomLocator.Thing colourMode, int charBase = -1) =>
            new(name, archive, new[] { load }, charBase, banks, new[] { colourMode }, new[] { 1 });

        // Platinum: tiles from 0x0000 up to the map at 0xF000. HGSS: the tiles at 0x8000 have 320 tiles of room before
        // the maps at 0xD000, so they move to 0x4000, past the page frames, which the game loads first and only once.
        private static readonly Screen PlatinumSummary = One("summary screen", DirNames.SummaryGraphics,
            new Load(RomLocator.Thing.SummaryBottomPaletteCall, 16, 14, 0xF000 / 64), RomLocator.Thing.SummaryBottomBanks, RomLocator.Thing.SummaryBottomColourMode);
        private static readonly Screen HgssSummary = One("summary screen", DirNames.SummaryGraphics,
            new Load(RomLocator.Thing.SummaryBottomPaletteCall, 20, 0, (0xD000 - 0x4000) / 64), RomLocator.Thing.SummaryBottomBanks, RomLocator.Thing.SummaryBottomColourMode, 1);
        // Tiles from 0x0000 up to the map at 0xF000. The touch buttons' tiles start at 0x10000. HGSS shows its party
        // screen's sub engine on the top screen.
        private static readonly Screen PlatinumParty = One("party screen", DirNames.PartyMenuGraphics,
            new Load(RomLocator.Thing.PartyBottomPaletteCall, 12, 13, 0xF000 / 64), RomLocator.Thing.PartyBottomBanks, RomLocator.Thing.PartyBottomColourMode);
        // The Poké Ball border, from 0x8000 up to the dial's map at 0xE800. The dial is an affine layer, which always reads
        // the standard palette.
        private static readonly Screen PlatinumBag = One("bag", DirNames.BagGraphics,
            new Load(RomLocator.Thing.BagBottomPaletteCall, 15, 16, (0xE800 - 0x8000) / 64), RomLocator.Thing.BagBottomBanks, RomLocator.Thing.BagBottomColourMode);
        // BG1 to BG3 share one drawing from 0x0000: the box list's up to the first map at 0xD000, compare mode's up to 0xE000.
        private static readonly Screen PlatinumPc = new("PC boxes", DirNames.BoxGraphics,
            new[]
            {
                new Load(RomLocator.Thing.PcBottomPaletteCall, 127, 128, 0xD000 / 64),
                new Load(RomLocator.Thing.PcCompareBottomPaletteCall, 132, 133, 0xE000 / 64),
            },
            -1, RomLocator.Thing.PcBottomBanks, new[] { RomLocator.Thing.PcBottomColourModes }, new[] { 1, 2, 3 });

        // BG1 and BG2 share one drawing from 0x0000 up to the first map at 0xE000. Each pocket recolours rows 0 to 3.
        private static readonly Screen HgssBag = new("bag", DirNames.BagGraphics,
            new[] { new Load(RomLocator.Thing.BagBottomPaletteCall, 46, 38, 0xE000 / 64) },
            -1, RomLocator.Thing.BagBottomBanks, new[] { RomLocator.Thing.BagBottomColourMode }, new[] { 1, 2 })
        {
            Recolour = RomLocator.Thing.BagPocketColoursCall,
            RecolourRows = 4,
        };

        // BG2's backdrop shares its drawing with the keys. Its 256-colour tiles move to 0x20000, in bank D, which keeps tiles 880
        // and up for the name and title windows clear of the maps.
        private static readonly Screen PlatinumNaming = new("naming screen", DirNames.NameInputGraphics,
            new[] { new Load(RomLocator.Thing.NamingBottomPaletteCall, NamingScreenMembers.Tiles, NamingScreenMembers.Colours, 880) },
            8, RomLocator.Thing.NamingBottomBanks, new[] { RomLocator.Thing.NamingBottomColourMode }, new[] { 2 })
        {
            TopEngine = true,
            TileLoad = RomLocator.Thing.NamingBottomTileLoad,
            SpriteWindows = RomLocator.Thing.NamingSpriteWindows,
        };

        /// <summary>The summary screen pieces for the loaded game, or null.</summary>
        public static Screen Summary => GameVersion == GameVersions.Platinum ? PlatinumSummary
            : GameFamily == GameFamilies.HGSS ? HgssSummary : null;

        /// <summary>The party screen pieces for the loaded game, or null.</summary>
        public static Screen Party => GameVersion == GameVersions.Platinum ? PlatinumParty : null;

        /// <summary>The bag pieces for the loaded game, or null.</summary>
        public static Screen Bag => GameVersion == GameVersions.Platinum ? PlatinumBag : GameFamily == GameFamilies.HGSS ? HgssBag : null;

        /// <summary>The PC boxes pieces for the loaded game, or null.</summary>
        public static Screen Pc => GameVersion == GameVersions.Platinum ? PlatinumPc : null;

        /// <summary>The screens the patch can change in the loaded ROM.</summary>
        public static IEnumerable<Screen> Screens => new[] { Summary, Party, Bag, Pc, Naming }.Where(Supports);

        /// <summary>The naming screen pieces for the loaded game, or null.</summary>
        public static Screen Naming => GameVersion == GameVersions.Platinum ? PlatinumNaming : null;

        public static bool Supports(Screen screen) => screen != null && screen.Loads.All(l => RomLocator.Where(l.Call) != null)
            && RomLocator.Where(screen.Banks) != null && screen.ColourModes.All(c => RomLocator.Where(c) != null)
            && new[] { screen.Recolour, screen.TileLoad, screen.SpriteWindows }.All(t => t is not RomLocator.Thing s || RomLocator.Where(s) != null);

        public static bool SupportsCurrentRom => Supports(Summary);

        // Every colour mode byte in the screen's templates. A spot may list several.
        private static IEnumerable<int> ColourModeOffsets(Screen screen) => screen.ColourModes.SelectMany(c => RomLocator.Where(c).Offsets);

        // The template's character base byte follows its screen base, two bytes after the colour mode.
        private static int CharBaseOffset(Screen screen) => RomLocator.Where(screen.ColourModes[0]).Offset + 2;

        /// <summary>The file a screen's code is in: the ARM9, or its overlay.</summary>
        public static string CodePath(Screen screen) =>
            RomLocator.Where(screen.Banks).Overlay is int overlay && overlay >= 0 ? OverlayUtils.GetPath(overlay) : RomInfo.Arm9Path;

        private static byte[] ReadCode(Screen screen) => RomLocator.ReadFile(screen.Banks);

        private static uint Address(RomLocator.Spot spot) =>
            RomLocator.RamAddressOf(spot);

        // Where the BL at a spot goes, or null when there is no BL.
        private static uint? CallAt(byte[] code, RomLocator.Spot spot) => ThumbBl.Target(code, spot.Offset, Address(spot));

        // Where the BLX at a spot goes, an ARM function, or null when there is no BLX.
        private static uint? ArmCallAt(byte[] code, RomLocator.Spot spot) => ThumbBl.Target(code, spot.Offset, Address(spot), blx: true);

        // The routine a load's call goes to when its block is there, or 0.
        private static uint PatchedCode(byte[] code, Screen screen, int load = 0)
        {
            if (!Supports(screen) || !File.Exists(Filesystem.ExpArmPath)) return 0;
            if (CallAt(code, RomLocator.Where(screen.Loads[load].Call)) is not uint routine) return 0;
            long block = (long)routine - RomInfo.SynthOverlayLoadAddress - CodeStart;
            if (block < 0 || block + BlockLength > new FileInfo(Filesystem.ExpArmPath).Length) return 0;
            return Encoding.ASCII.GetString(DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)block, Marker.Length)) == Marker ? routine : 0;
        }

        private static uint PatchedCode(Screen screen, int load = 0)
        {
            try { return Supports(screen) && File.Exists(CodePath(screen)) ? PatchedCode(ReadCode(screen), screen, load) : 0; }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException) { return 0; }
        }

        public static bool IsApplied(Screen screen) => PatchedCode(screen) != 0;

        private static uint Word(uint routine, int at) =>
            BitConverter.ToUInt32(DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)((long)routine - RomInfo.SynthOverlayLoadAddress + at), 4), 0);

        /// <summary>The game's Graphics_LoadPaletteFromOpenNARC, read from a screen's call or, once patched, from its routine.</summary>
        public static uint GameLoader()
        {
            foreach (Screen screen in Screens)
            {
                // A screen whose code file can't be read here doesn't stop the others answering.
                byte[] code;
                try { code = ReadCode(screen); }
                catch (Exception ex) when (ex is IOException || ex is InvalidOperationException) { continue; }
                uint routine = PatchedCode(code, screen);
                if (routine != 0) return Word(routine, GameAt) & ~1u;
                if (Vanilla(code, screen) is uint loader) return loader;
            }
            throw new InvalidOperationException("The bottom palette loads aren't where DSPRE expects them.");
        }

        // Previews ask on every redraw. The answer holds until the code file or the synthetic overlay changes.
        private static readonly Dictionary<(string, string, int), (DateTime Code, DateTime Synth, int Member)> Members = new();

        /// <summary>The archive member holding a load's extended palettes, or -1 when the patch isn't in.</summary>
        public static int PaletteMember(Screen screen, int load = 0)
        {
            if (!Supports(screen)) return -1;
            string code = CodePath(screen);
            DateTime codeTime = File.GetLastWriteTimeUtc(code), synthTime = File.GetLastWriteTimeUtc(Filesystem.ExpArmPath);
            (string, string, int) key = (code, screen.Name, load);
            lock (Members)
                if (Members.TryGetValue(key, out (DateTime Code, DateTime Synth, int Member) known) && known.Code == codeTime && known.Synth == synthTime)
                    return known.Member;
            uint routine = PatchedCode(screen, load);
            int member = routine == 0 ? -1 : (int)Word(routine, MemberAt);
            lock (Members) Members[key] = (codeTime, synthTime, member);
            return member;
        }

        /// <summary>The drawing the screen's layer loads: its 256-colour copy once the patch is in, otherwise the shared one.</summary>
        public static int DrawingMember(Screen screen)
        {
            if (screen?.TileLoad is not RomLocator.Thing load || PaletteMember(screen) < 0) return screen?.Drawing ?? -1;
            try { return ReadCode(screen)[RomLocator.Where(load).Offsets[1]]; }
            catch (Exception ex) when (ex is IOException || ex is IndexOutOfRangeException) { return screen.Drawing; }
        }

        // Instruction edits besides the call, as halfwords the game has and the patch writes.
        private static IEnumerable<(int At, ushort Game, ushort Patched)> Edits(Screen screen, int drawingCopy)
        {
            if (screen.TileLoad is RomLocator.Thing load)
            {
                int[] at = RomLocator.Where(load).Offsets;
                // movs r0,#2 before lsls #12: a size of 0 loads the whole copy, twice as large.
                yield return (at[0], 0x2002, 0x2000);
                yield return (at[1], ThumbImm8.Make(ThumbImm8.Op.Movs, 1, screen.Drawing), ThumbImm8.Make(ThumbImm8.Op.Movs, 1, drawingCopy));
                // adds r3,r1,#0 took the layer from the member's register. movs r3,#2 sets the layer directly.
                yield return (at[2], 0x1C0B, 0x2302);
            }
            if (screen.SpriteWindows is RomLocator.Thing windows)
            {
                int[] at = RomLocator.Where(windows).Offsets;
                yield return (at[0], 0x2202, 0x2200);
                // The last window's height moves from r2 to r0 so r2 can hold BG0. Its palette slot keeps the 0 the loop
                // stored, which Window_Add only reads.
                yield return (at[1], 0x2202, 0x2002);
                yield return (at[2], 0x9202, 0x9002);
                yield return (at[3], 0x9303, 0x2200);
            }
        }

        private static string Changed(Screen screen) =>
            $"Something else has already changed the bottom screen of the {screen.Name}, so DSPRE leaves it alone.";

        /// <summary>Why the patch can't go in on a screen now, or null.</summary>
        public static string WhyNotApplicable(Screen screen)
        {
            if (!Supports(screen)) return "This patch doesn't support this ROM yet.";
            if (!SyntheticOverlaySpace.Available()) return "Apply the ARM9 expansion first.";
            byte[] code = ReadCode(screen);
            if (PatchedCode(code, screen) != 0) return "The patch is already applied.";
            return Vanilla(code, screen) == null ? Changed(screen) : null;
        }

        // The palette loader the screen calls, when every call, the bank and the templates are as the game has them.
        private static uint? Vanilla(byte[] code, Screen screen)
        {
            List<uint?> loaders = screen.Loads.Select(l => CallAt(code, RomLocator.Where(l.Call))).Distinct().ToList();
            if (loaders.Count != 1 || loaders[0] is not uint loader || loader >= RomInfo.SynthOverlayLoadAddress) return null;
            if (BitConverter.ToUInt32(code, RomLocator.Where(screen.Banks).Offset) != 0 || ColourModeOffsets(screen).Any(at => code[at] != 0)) return null;
            if (screen.TopEngine && BitConverter.ToUInt32(code, RomLocator.Where(screen.Banks).Offset - 4) != BankA) return null;
            if (Edits(screen, 0).Any(e => BitConverter.ToUInt16(code, e.At) != e.Game)) return null;
            if (screen.CharBase >= 0 && code[CharBaseOffset(screen)] == screen.CharBase) return null;
            if (screen.Recolour is RomLocator.Thing r && ArmCallAt(code, RomLocator.Where(r)) == null) return null;
            return loader;
        }

        /// <summary>
        /// Writes a chain of routines per load, one per slot, at <paramref name="blockOffsets"/> (load by load), routes each
        /// palette load to its chain and turns the extended palettes on.
        /// </summary>
        /// <param name="paletteMembers">The new member each load's routines load. The caller writes them.</param>
        /// <param name="drawingCopy">The member the caller wrote the layer's 256-colour copy to, for a screen with a tile load.</param>
        public static void Apply(Screen screen, IReadOnlyList<int> blockOffsets, IReadOnlyList<int> paletteMembers, int drawingCopy = -1)
        {
            if (screen.TileLoad != null && drawingCopy is < 0 or > 255) throw new ArgumentException("The drawing copy needs a member from 0 to 255.", nameof(drawingCopy));
            if (blockOffsets.Count != screen.BlockCount || paletteMembers.Count != screen.Loads.Length) throw new ArgumentException("Needs one block per load and slot, the recolour block, and one member per load.");
            if (blockOffsets.Any(o => o % 4 != 0)) throw new ArgumentException("Blocks must start on a 4-byte boundary.", nameof(blockOffsets));
            byte[] code = ReadCode(screen);
            uint loader = Vanilla(code, screen) ?? throw new InvalidOperationException(Changed(screen));
            uint Routine(int block) => RomInfo.SynthOverlayLoadAddress + (uint)blockOffsets[block] + (uint)CodeStart;
            string path = CodePath(screen);

            for (int l = 0; l < screen.Loads.Length; l++)
            {
                // Each routine first makes the call it took over, through the next routine, so the game's load runs once.
                for (int k = 0; k < screen.Slots.Length; k++)
                {
                    int at = l * screen.Slots.Length + k;
                    uint next = k + 1 < screen.Slots.Length ? Routine(at + 1) : loader;
                    byte[] block = new byte[BlockLength];
                    SyntheticOverlaySpace.Header(Marker, BlockLength).CopyTo(block, 0);
                    Code.CopyTo(block, CodeStart);
                    block[CodeStart + LoadTypeAt] = screen.TopEngine ? MainExtendedLoad : SubExtendedLoad;
                    BitConverter.GetBytes(next | 1).CopyTo(block, CodeStart + LoaderAt);
                    BitConverter.GetBytes((uint)paletteMembers[l]).CopyTo(block, CodeStart + MemberAt);
                    BitConverter.GetBytes((uint)screen.Slots[k] * SlotSize).CopyTo(block, CodeStart + OffsetAt);
                    BitConverter.GetBytes(loader | 1).CopyTo(block, CodeStart + GameAt);
                    DSUtils.WriteToFile(Filesystem.ExpArmPath, block, (uint)blockOffsets[at]);
                }
                RomLocator.Spot call = RomLocator.Where(screen.Loads[l].Call);
                DSUtils.WriteToFile(path, ThumbBl.Make(Address(call), Routine(l * screen.Slots.Length)), (uint)call.Offset);
            }
            if (screen.Recolour is RomLocator.Thing recolour)
            {
                // The slots sit side by side, so the routine steps from the first.
                int at = screen.Loads.Length * screen.Slots.Length;
                RomLocator.Spot call = RomLocator.Where(recolour);
                byte[] block = new byte[RecolourLength];
                SyntheticOverlaySpace.Header(Marker, RecolourLength).CopyTo(block, 0);
                RecolourCode.CopyTo(block, CodeStart);
                BitConverter.GetBytes(ArmCallAt(code, call).Value).CopyTo(block, CodeStart + RecolourLoadAt);
                BitConverter.GetBytes(BankHForCpu + (uint)screen.Slots.Min() * SlotSize).CopyTo(block, CodeStart + RecolourFirstAt);
                BitConverter.GetBytes((uint)screen.Slots.Length).CopyTo(block, CodeStart + RecolourSlotsAt);
                BitConverter.GetBytes((uint)screen.RecolourRows).CopyTo(block, CodeStart + RecolourRowsAt);
                DSUtils.WriteToFile(Filesystem.ExpArmPath, block, (uint)blockOffsets[at]);
                DSUtils.WriteToFile(path, ThumbBl.Make(Address(call), Routine(at)), (uint)call.Offset);
            }
            uint banks = (uint)RomLocator.Where(screen.Banks).Offset;
            DSUtils.WriteToFile(path, BitConverter.GetBytes(screen.TopEngine ? BankE : BankH), banks);
            if (screen.TopEngine) DSUtils.WriteToFile(path, BitConverter.GetBytes(BanksAD), banks - 4);
            foreach ((int at, ushort _, ushort patched) in Edits(screen, drawingCopy).ToList())
                DSUtils.WriteToFile(path, BitConverter.GetBytes(patched), (uint)at);
            foreach (int at in ColourModeOffsets(screen)) DSUtils.WriteToFile(path, new[] { ColourMode256 }, (uint)at);
            if (screen.CharBase >= 0) DSUtils.WriteToFile(path, new[] { (byte)screen.CharBase }, (uint)CharBaseOffset(screen));
        }

        /// <summary>Converts a 16-colour drawing to 256-colour tiles and its palette file to 16 extended palettes, with no visible change.</summary>
        public static (byte[] Drawing, byte[] Colours) Widen(byte[] drawing, byte[] colours) => (WideDrawing(drawing), PaletteTwin(NitroBgCodec.Inflate(colours)));

        /// <summary>A stored drawing as 256-colour tiles that look the same, compressed again when it was.</summary>
        public static byte[] WideDrawing(byte[] stored)
        {
            bool squeezed = stored.Length > 4 && stored[0] == 0x10;
            byte[] wide = WidenDrawing(squeezed ? NitroBgCodec.Inflate(stored) : stored);
            return squeezed ? Lz77.Compress(wide) : wide;
        }

        /// <summary>A 16-colour drawing as 256-colour tiles, with each pixel's number unchanged.</summary>
        public static byte[] WidenDrawing(byte[] drawing)
        {
            NitroBgCodec.DrawingHeader head = NitroBgCodec.ReadDrawingHeader(drawing) ?? throw new InvalidDataException("The drawing has no character section.");
            if (head.EightBit) throw new InvalidDataException("The drawing already has 256-colour tiles.");
            byte[] pixels = new byte[head.DataSize * 2];
            for (int i = 0; i < head.DataSize; i++)
            {
                pixels[i * 2] = (byte)(drawing[head.DataAt + i] & 15);
                pixels[i * 2 + 1] = (byte)(drawing[head.DataAt + i] >> 4);
            }
            byte[] wide = NitroBgCodec.WithCharacterData(drawing, head, pixels);
            PutU32(wide, head.SectionAt + 0x0C, 4);
            return wide;
        }

        /// <summary>A palette file as 16 extended palettes: palette n starts with the colours of row n, as widened pixels expect.</summary>
        public static byte[] PaletteTwin(byte[] colours)
        {
            ushort[] rows = NitroBgCodec.ReadColours(colours);
            ushort[] extended = new ushort[Palettes * PaletteSize];
            for (int n = 0; n < Palettes; n++)
                for (int k = 0; k < 16 && n * 16 + k < rows.Length; k++) extended[n * PaletteSize + k] = rows[n * 16 + k];
            return ExtendedPaletteFile(colours, extended);
        }

        /// <summary>A palette file holding 16 extended palettes, on the header of <paramref name="like"/>.</summary>
        public static byte[] ExtendedPaletteFile(byte[] like, ushort[] extended)
        {
            int pltt = NitroBgCodec.Find(like, "TTLP", 0);
            if (pltt < 0) throw new InvalidDataException("The palette file has no palette section.");
            byte[] file = new byte[pltt + 0x18 + extended.Length * 2];
            Array.Copy(like, file, pltt + 0x18);
            for (int i = 0; i < extended.Length; i++) BitConverter.GetBytes(extended[i]).CopyTo(file, pltt + 0x18 + i * 2);
            PutU32(file, pltt + 4, (uint)(0x18 + extended.Length * 2));
            PutU32(file, pltt + 8, 4);
            PutU32(file, pltt + 0x10, (uint)(extended.Length * 2));
            PutU32(file, 0x08, (uint)file.Length);
            // One section: the copied header may count a PMCP section that this file drops.
            BitConverter.GetBytes((ushort)1).CopyTo(file, 0x0E);
            return file;
        }

        private static void PutU32(byte[] d, int at, uint v) => BitConverter.GetBytes(v).CopyTo(d, at);
    }
}
