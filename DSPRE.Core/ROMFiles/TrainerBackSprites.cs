using System.Collections.Generic;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Player-side trainer sprites, laid out per entry the same way as the class sprites.
    /// </summary>
    public static class TrainerBackSprites
    {
        public static int FilesPerSprite => TrainerGraphicsLayout.Stride;

        private static readonly string[] Platinum =
        {
            "Lucas", "Dawn", "Barry", "Cheryl", "Riley", "Marley", "Buck", "Mira",
            "Lucas (Diamond and Pearl)", "Dawn (Diamond and Pearl)", "Barry (Diamond and Pearl)",
        };

        private static readonly string[] Diamond =
        {
            "Lucas", "Dawn", "Barry", "Cheryl", "Riley", "Marley", "Buck", "Mira",
        };

        private static readonly string[] HeartGold =
        {
            "Ethan", "Lyra", "Silver", "Lance", "Cheryl", "Riley", "Marley", "Buck", "Mira",
            "Lucas (Diamond and Pearl)", "Dawn (Diamond and Pearl)", "Barry (Diamond and Pearl)",
            "Lucas (Platinum)", "Dawn (Platinum)", "Barry (Platinum)",
            "Ethan, open-hand throw", "Lyra, open-hand throw",
        };

        /// <summary>A name for each back sprite; entries past the retail list are numbered.</summary>
        public static List<string> Names(int count)
        {
            var known = gameFamily switch
            {
                GameFamilies.HGSS => HeartGold,
                GameFamilies.Plat => Platinum,
                GameFamilies.DP => Diamond,
                _ => System.Array.Empty<string>(),
            };
            var names = new List<string>(count);
            for (int i = 0; i < count; i++) names.Add(i < known.Length ? known[i] : $"Back sprite {i}");
            return names;
        }
    }
}
