using System;
using System.Collections.Generic;
using static DSPRE.RomInfo;

namespace DSPRE.Resources
{
    /// <summary>
    /// What a map header's battle background number picks, in Platinum and HGSS (pokeplatinum
    /// generated/battle_backgrounds.txt, pokeheartgold include/constants/battle.h). Diamond and Pearl use the first 12:
    /// pokediamond's battle setup bounds the header value below 12 and swaps in 1 while surfing.
    /// </summary>
    public static class BattleBackgrounds
    {
        private static readonly string[] Shared =
        {
            "Plain", "Water", "City", "Forest", "Mountain", "Snow",
            "Indoors 1", "Indoors 2", "Indoors 3", "Cave 1", "Cave 2", "Cave 3",
        };

        private static readonly string[] Frontier =
        {
            "Distortion World", "Battle Tower", "Battle Factory", "Battle Arcade", "Battle Castle", "Battle Hall",
        };

        private static readonly string[] PlatinumNames = Join("Aaron", "Bertha", "Flint", "Lucian", "Cynthia");
        private static readonly string[] HgssNames = Join("Will", "Koga", "Bruno", "Karen", "Lance");

        private static string[] Join(params string[] eliteFour)
        {
            List<string> all = new List<string>(Shared);
            all.AddRange(eliteFour);
            all.AddRange(Frontier);
            return all.ToArray();
        }

        /// <summary>The names for the open game, or none when its values are not known.</summary>
        public static IReadOnlyList<string> Names() => gameFamily switch
        {
            GameFamilies.DP => Shared,
            GameFamilies.Plat => PlatinumNames,
            GameFamilies.HGSS => HgssNames,
            _ => Array.Empty<string>(),
        };
    }
}
