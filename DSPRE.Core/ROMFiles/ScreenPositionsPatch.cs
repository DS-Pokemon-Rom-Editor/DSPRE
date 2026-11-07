using System;
using System.IO;
using System.Linq;
using System.Text;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Lets the Screens editor move windows whose position shares a register with something else, through small routines in the
    /// synthetic overlay. Source: Resources/ROMToolboxDB/ScreenPositions/screen_positions.s.
    /// </summary>
    public static class ScreenPositionsPatch
    {
        public const string Marker = "SCRPOSITNV1\0";

        private const ushort BxLr = 0x4770, Nop = 0x46C0;

        // Where the game sets one register for two things: the place, the instructions there and the routine that sets each value on
        // its own. With no routine, the instruction is swapped in place.
        private sealed record Hook(string Name, RomLocator.Thing Place, int Index, ushort[] Game, ushort[] Routine, ushort[] Swap = null);

        private static readonly Hook[] PlatinumHooks =
        {
            // movs r3, #1; str r3, [sp]  ->  y, then x.
            new Hook("Ball count", RomLocator.Thing.StartMenuBallCount, 0, new ushort[] { 0x2301, 0x9300 },
                     new ushort[] { 0x2301, 0x9300, 0x2301, BxLr }),
            // movs r3, #23; subs r1, r3, r0  ->  the bottom row, then x.
            new Hook("Berry actions", RomLocator.Thing.BagBerryActions, 0, new ushort[] { 0x2317, 0x1A19 },
                     new ushort[] { 0x2117, 0x1A09, 0x2317, BxLr }),
            // movs r2, #153; lsls r2, #2  ->  the row, lsls #5, adds the column.
            new Hook("Markings", RomLocator.Thing.BoxMarkings, 0, new ushort[] { 0x2299, 0x0092 },
                     new ushort[] { 0x2213, 0x0152, 0x3204, BxLr }),
            // mov r3, r2  ->  movs r3, #1, for the title and the entries.
            new Hook("Options title x", RomLocator.Thing.OptionsWindowsX, 0, new ushort[] { 0x1C13 }, null, new ushort[] { 0x2301 }),
            new Hook("Options entries x", RomLocator.Thing.OptionsWindowsX, 1, new ushort[] { 0x1C13 }, null, new ushort[] { 0x2301 }),
        };

        private static readonly Hook[] HgssHooks =
        {
            // movs r3, #7; str r3, [sp]  ->  width 7, then y.
            new Hook("Hall of Fame y", RomLocator.Thing.TrainerCardHallOfFame, 0, new ushort[] { 0x2307, 0x9300 },
                     new ushort[] { 0x2307, 0x9300, 0x2307, BxLr }),
            // movs r2, #0; str r2, [sp, #12]; str r2, [sp, #16]  ->  the source corner, then x.
            new Hook("Hall of Fame x", RomLocator.Thing.TrainerCardHallOfFame, 1, new ushort[] { 0x2200, 0x9203, 0x9204 },
                     new ushort[] { 0x2200, 0x9203, 0x9204, 0x2200, BxLr }),
            // The copy shown after the League: movs r2, #0; str r2, [sp, #16]  ->  the source row, then x.
            new Hook("Hall of Fame x, after the League", RomLocator.Thing.TrainerCardHallOfFame, 2, new ushort[] { 0x2200, 0x9204 },
                     new ushort[] { 0x2200, 0x9204, 0x2200, BxLr }),
            // movs r1, #1; str r1, [sp]  ->  y, then the base tile 1 the code stores later.
            new Hook("Item action y", RomLocator.Thing.BagItemAction, 0, new ushort[] { 0x2101, 0x9100 },
                     new ushort[] { 0x2101, 0x9100, 0x2101, BxLr }),
            // movs r3, #2; str r3, [sp, #8]  ->  height 2, then x.
            new Hook("Item action x", RomLocator.Thing.BagItemAction, 1, new ushort[] { 0x2302, 0x9302 },
                     new ushort[] { 0x2302, 0x9302, 0x2302, BxLr }),
        };

        private static Hook[] Hooks => RomInfo.GameFamily switch
        {
            GameFamilies.Plat => PlatinumHooks,
            GameFamilies.HGSS => HgssHooks,
            _ => Array.Empty<Hook>(),
        };

        private static int CodeStart => SyntheticOverlaySpace.HeaderSize;

        // The routines sit one after another.
        private static int RoutineAt(Hook hook)
        {
            int at = 0;
            foreach (Hook h in Hooks)
            {
                if (h == hook) return at;
                if (h.Routine != null) at += h.Routine.Length * 2;
            }
            throw new ArgumentException("Not one of this game's hooks.", nameof(hook));
        }

        public static int BlockLength => CodeStart + Math.Max(4, Hooks.Where(h => h.Routine != null).Sum(h => h.Routine.Length * 2));

        public static bool Supports => Hooks.Length > 0 && RomInfo.GameLanguage != GameLanguages.Japanese
                                       && Hooks.All(h => RomLocator.Where(h.Place) is { } spot && spot.Offsets.Length > h.Index);

        /// <summary>The overlays the hooks sit in.</summary>
        public static int[] Overlays => Hooks.Select(h => RomLocator.Where(h.Place).Overlay).Where(o => o >= 0).Distinct().OrderBy(o => o).ToArray();

        /// <summary>Whether a hook sits in the ARM9.</summary>
        public static bool WritesArm9 => Hooks.Any(h => RomLocator.Where(h.Place).Overlay < 0);

        private static int OffsetOf(Hook h) => RomLocator.Where(h.Place).Offsets[h.Index];

        private static uint Address(Hook h) => RomLocator.RamBase(RomLocator.Where(h.Place)) + (uint)OffsetOf(h);

        // Only applying unpacks an overlay. A packed one can't hold the patch yet.
        private static string PathOf(Hook h, bool unpack)
        {
            RomLocator.Spot spot = RomLocator.Where(h.Place);
            if (spot.Overlay >= 0 && OverlayUtils.IsCompressed(spot.Overlay))
            {
                if (!unpack) return null;
                OverlayUtils.Decompress(spot.Overlay);
            }
            return RomLocator.PathOf(spot);
        }

        private static ushort[] Read(Hook h, int halves)
        {
            byte[] bytes = DSUtils.ReadFromFile(PathOf(h, unpack: true), (uint)OffsetOf(h), halves * 2);
            return Enumerable.Range(0, halves).Select(i => BitConverter.ToUInt16(bytes, i * 2)).ToArray();
        }

        private static bool IsGame(Hook h) => Read(h, h.Game.Length).SequenceEqual(h.Game);

        /// <summary>Where the block is in the synthetic overlay when the patch is in, or -1.</summary>
        public static long BlockOffset()
        {
            if (!Supports || !File.Exists(Filesystem.ExpArmPath)) return -1;
            try
            {
                Hook first = Hooks.First(h => h.Routine != null);
                if (PathOf(first, unpack: false) is not string path) return -1;
                byte[] call = DSUtils.ReadFromFile(path, (uint)OffsetOf(first), 4);
                if (ThumbBl.Target(call, 0, Address(first)) is not uint routine) return -1;
                long block = (long)routine - SynthOverlayLoadAddress - CodeStart - RoutineAt(first);
                if (block < 0 || block + BlockLength > new FileInfo(Filesystem.ExpArmPath).Length) return -1;
                return Encoding.ASCII.GetString(DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)block, Marker.Length)) == Marker ? block : -1;
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is InvalidOperationException) { return -1; }
        }

        public static bool IsApplied => BlockOffset() >= 0;

        /// <summary>Why the patch can't go in now, or null.</summary>
        public static string WhyNotApplicable()
        {
            if (!Supports) return "Platinum, HeartGold and SoulSilver only, except Japanese.";
            if (!SyntheticOverlaySpace.Available()) return "Apply the ARM9 expansion first.";
            if (IsApplied) return "The patch is already applied.";
            Hook changed = Hooks.FirstOrDefault(h => !IsGame(h));
            return changed == null ? null : $"Another patch already changed the code for {changed.Name}.";
        }

        /// <summary>Writes the routines at <paramref name="blockOffset"/> and sends every place to its routine.</summary>
        public static void Apply(int blockOffset)
        {
            if (WhyNotApplicable() is string why) throw new InvalidOperationException(why);
            byte[] block = new byte[BlockLength];
            SyntheticOverlaySpace.Header(Marker, BlockLength).CopyTo(block, 0);
            uint start = SynthOverlayLoadAddress + (uint)blockOffset + (uint)CodeStart;
            foreach (Hook h in Hooks.Where(h => h.Routine != null))
                for (int i = 0; i < h.Routine.Length; i++) BitConverter.GetBytes(h.Routine[i]).CopyTo(block, CodeStart + RoutineAt(h) + i * 2);
            DSUtils.WriteToFile(Filesystem.ExpArmPath, block, (uint)blockOffset);

            foreach (Hook h in Hooks)
            {
                if (h.Routine == null)
                {
                    DSUtils.WriteToFile(PathOf(h, unpack: true), h.Swap.SelectMany(BitConverter.GetBytes).ToArray(), (uint)OffsetOf(h));
                    continue;
                }
                // A third game instruction becomes a nop after the call.
                byte[] call = ThumbBl.Make(Address(h), start + (uint)RoutineAt(h));
                byte[] bytes = h.Game.Length > 2 ? call.Concat(BitConverter.GetBytes(Nop)).ToArray() : call;
                DSUtils.WriteToFile(PathOf(h, unpack: true), bytes, (uint)OffsetOf(h));
            }
        }

        /// <summary>
        /// A routine's immediate as a Layout spot, counting its instructions from 0, or the swapped instruction itself; null when the
        /// patch isn't in.
        /// </summary>
        public static ScreenWindowSpot Spot(string hook, int instruction, int add = 0)
        {
            Hook h = Hooks.FirstOrDefault(x => x.Name == hook);
            if (h == null) return null;
            if (h.Routine == null) return IsApplied ? new ScreenWindowSpot((uint)OffsetOf(h), Add: add) : null;
            long block = BlockOffset();
            return block < 0 ? null : new ScreenWindowSpot((uint)(block + CodeStart + RoutineAt(h) + instruction * 2), Add: add, Synthetic: true);
        }
    }
}
