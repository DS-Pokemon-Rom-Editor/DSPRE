using System;
using System.Collections.Generic;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Friendship change per event for friendship below 100, 100 to 199, and 200 or more. Positive changes then get
    /// +1 from a Luxury Ball, +1 at the met location and ×1.5 with a Soothe Bell, all in a signed byte.
    /// </summary>
    public sealed class FriendshipTable
    {
        public const int Events = 10, Bands = 3, Size = Events * Bands;

        /// <summary>The largest change that can't overflow a signed byte once every bonus applies.</summary>
        public const int SafeMax = 83;

        public static readonly string[] EventNames =
        {
            "Level up", "Vitamin", "Battle item", "Gym Leader, Elite Four or Champion battle", "Learning a TM or HM",
            "Walking", "Fainting", "Surviving poison in the field", "Fainting to a much stronger foe", "Winning a Contest",
        };

        public static readonly string[] BandNames = { "Below 100", "100 to 199", "200 and up" };

        public sbyte[,] Values { get; } = new sbyte[Events, Bands];

        /// <summary>Events the game never reads from this table; items use their own item data.</summary>
        public static bool IsUnused(int e) => e == 1 || e == 2 || (e == 9 && gameFamily == GameFamilies.HGSS);

        public static string WhyNot() => GameTableFile.WhyNot(GameTable.FriendshipChanges, Size);

        public FriendshipTable(byte[] data)
        {
            for (int e = 0; e < Events; e++)
                for (int b = 0; b < Bands; b++)
                    Values[e, b] = unchecked((sbyte)data[e * Bands + b]);
        }

        public static FriendshipTable Load() => new FriendshipTable(GameTableFile.Read(GameTable.FriendshipChanges, Size));

        public byte[] ToBytes()
        {
            var data = new byte[Size];
            for (int e = 0; e < Events; e++)
                for (int b = 0; b < Bands; b++)
                    data[e * Bands + b] = unchecked((byte)Values[e, b]);
            return data;
        }

        /// <summary>Changes that can overflow into a loss once the bonuses apply.</summary>
        public IEnumerable<(int Event, int Band)> Risky() =>
            from e in Enumerable.Range(0, Events)
            from b in Enumerable.Range(0, Bands)
            where !IsUnused(e) && Values[e, b] > SafeMax
            select (e, b);

        public void Save() => GameTableFile.Write(GameTable.FriendshipChanges, ToBytes());
    }
}
