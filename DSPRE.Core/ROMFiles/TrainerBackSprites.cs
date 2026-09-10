using System.Collections.Generic;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Player-side trainer sprites, five files each like the class sprites.
    /// </summary>
    public static class TrainerBackSprites
    {
        public const int FilesPerSprite = 5;

        private static readonly string[] Platinum =
        {
            "Lucas", "Dawn", "Barry", "Cheryl", "Riley", "Marley", "Buck", "Mira",
            "Lucas (Diamond and Pearl)", "Dawn (Diamond and Pearl)", "Barry (Diamond and Pearl)",
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
                _ => System.Array.Empty<string>(),
            };
            var names = new List<string>(count);
            for (int i = 0; i < count; i++) names.Add(i < known.Length ? known[i] : $"Back sprite {i}");
            return names;
        }
    }
}
