using System;
using System.Collections.Generic;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The counts the game compares against the regional Pokédex, built into the code as one-byte `cmp` immediates:
    /// how many regional species complete it, the professor's last rating step before "complete" (one less), and in
    /// DP and Platinum the highest regional number the regional-to-national lookup reads. Completion ignores the
    /// mythicals the game leaves out: Manaphy in DP, Mew and Celebi in HGSS, none in Platinum.
    /// </summary>
    public static class RegionalDexCount
    {
        public const int Max = byte.MaxValue;

        public readonly record struct Counts(int Completion, int RatingLast, int LookupBound);

        private static int[] IgnoredMythicals => GameFamily switch
        {
            GameFamilies.DP => new[] { 490 },        // Manaphy
            GameFamilies.HGSS => new[] { 151, 251 }, // Mew, Celebi
            _ => Array.Empty<int>(),
        };

        // The US retail counts.
        public static Counts Retail => GameFamily switch
        {
            GameFamilies.DP => new Counts(150, 149, 151),
            GameFamilies.Plat => new Counts(210, 209, 210),
            _ => new Counts(254, 253, -1),
        };

        private static bool HasBound => RomLocator.Where(RomLocator.Thing.RegionalDexLookupBound) != null;

        /// <summary>Why the counts can't be read or set in this ROM, or null.</summary>
        public static string WhyNot()
        {
            foreach (RomLocator.Thing t in new[] { RomLocator.Thing.RegionalDexCompletion, RomLocator.Thing.RegionalDexRatingLast })
                if (RomLocator.Where(t) == null) return "Works on " + RomInfo.DescribeDumps(RomLocator.WorksOn(t)) + ".";
            foreach (RomLocator.Thing t in Sites())
            {
                string why = RomLocator.WhyNot(t, 2);
                if (why != null) return why;
                // The byte must still be the immediate of a `cmp rN, #imm8`, or DSPRE doesn't know this code.
                byte op = RomLocator.Read(t, 2)[1];
                if (op < 0x28 || op > 0x2F) return "The code that checks the regional Pokédex size has been changed, so DSPRE leaves it alone.";
            }
            return null;
        }

        private static IEnumerable<RomLocator.Thing> Sites()
        {
            yield return RomLocator.Thing.RegionalDexCompletion;
            yield return RomLocator.Thing.RegionalDexRatingLast;
            if (HasBound) yield return RomLocator.Thing.RegionalDexLookupBound;
        }

        /// <summary>What the game holds now; the lookup bound is -1 where the game has none.</summary>
        public static Counts Current() => new Counts(
            RomLocator.Read(RomLocator.Thing.RegionalDexCompletion, 1)[0],
            RomLocator.Read(RomLocator.Thing.RegionalDexRatingLast, 1)[0],
            HasBound ? RomLocator.Read(RomLocator.Thing.RegionalDexLookupBound, 1)[0] : -1);

        /// <summary>What the counts should be for a regional order.</summary>
        public static Counts For(IReadOnlyCollection<ushort> order)
        {
            int completion = order.Count(s => !IgnoredMythicals.Contains(s));
            return new Counts(completion, completion - 1, HasBound ? order.Count : -1);
        }

        /// <summary>Why the order's counts can't be written, or null.</summary>
        public static string Problem(IReadOnlyCollection<ushort> order)
        {
            Counts c = For(order);
            if (c.Completion < 1) return "The regional Pokédex needs at least one species the game counts.";
            if (c.Completion > Max || c.LookupBound > Max)
                return $"The game checks the regional Pokédex size with one-byte numbers, so it can count at most {Max} species; this order has {order.Count}.";
            return null;
        }

        public static bool Matches(IReadOnlyCollection<ushort> order) => Current() == For(order);

        public static void Apply(IReadOnlyCollection<ushort> order)
        {
            if (WhyNot() is string why) throw new InvalidOperationException(why);
            if (Problem(order) is string p) throw new InvalidOperationException(p);
            Counts c = For(order);
            RomLocator.Write(RomLocator.Thing.RegionalDexCompletion, new[] { (byte)c.Completion });
            RomLocator.Write(RomLocator.Thing.RegionalDexRatingLast, new[] { (byte)c.RatingLast });
            if (HasBound) RomLocator.Write(RomLocator.Thing.RegionalDexLookupBound, new[] { (byte)c.LookupBound });
        }

        /// <summary>The regional order as saved in the project, or null when it can't be read.</summary>
        public static List<ushort> SavedOrder()
        {
            if (!PokedexLists.TryLoad(out PokedexLists lists, out _)) return null;
            return lists.RegionalOrder.ToList();
        }
    }
}
