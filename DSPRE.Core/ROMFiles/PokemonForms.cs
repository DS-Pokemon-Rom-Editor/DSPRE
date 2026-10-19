using System.Collections.Generic;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>The names of a species' forms, as each game has them.</summary>
    public static class PokemonForms
    {
        /// <summary>Every Gen 4 species with forms, as HeartGold and SoulSilver have them.</summary>
        public static readonly IReadOnlyDictionary<int, string[]> Labels = new Dictionary<int, string[]>
        {
            [172] = new[] { "Normal", "Spiky-eared" },
            [201] = new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S",
                            "T", "U", "V", "W", "X", "Y", "Z", "!", "?" },
            [386] = new[] { "Normal", "Attack", "Defense", "Speed" },
            [412] = new[] { "Plant", "Sandy", "Trash" },
            [413] = new[] { "Plant", "Sandy", "Trash" },
            [422] = new[] { "West Sea", "East Sea" },
            [423] = new[] { "West Sea", "East Sea" },
            [479] = new[] { "Normal", "Heat", "Wash", "Frost", "Fan", "Mow" },
            [487] = new[] { "Altered", "Origin" },
            [492] = new[] { "Land", "Sky" },
            [493] = new[] { "Normal", "Fighting", "Flying", "Poison", "Ground", "Rock", "Bug", "Ghost", "Steel", "???",
                            "Fire", "Water", "Grass", "Electric", "Psychic", "Ice", "Dragon", "Dark" },
        };

        /// <summary>The forms this game gives a species, or null for one with a single form.</summary>
        public static string[] NamesOf(int species)
        {
            if (!Labels.TryGetValue(species, out string[] names)) return null;
            if (species == 172 && GameFamily != GameFamilies.HGSS) return null;
            if ((species == 479 || species == 487 || species == 492) && GameFamily == GameFamilies.DP) return null;
            return names;
        }
    }
}
