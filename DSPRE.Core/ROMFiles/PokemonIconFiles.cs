using System.Collections.Generic;
using DSPRE.Resources;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>What each party icon file shows. Later games only append forms to the shared layout.</summary>
    public static class PokemonIconFiles
    {
        /// <summary>The palettes, layouts and animations every icon shares.</summary>
        public const int SharedFiles = 7;

        public const int EggFile = 501, ManaphyEggFile = 502;
        private const int ManaphySpecies = 490, EggEntry = 494;

        /// <summary>Entries in the species name bank; the Pokémon Editor's form entries follow them.</summary>
        private const int NameBankEntries = 496;

        public sealed class Icon
        {
            public int File;
            public int Species;       // 0 for the egg
            public string Form;       // null for a species' own icon
            public bool IsEgg;
            public int EditorId;      // the Pokémon Editor entry that owns this icon
        }

        private static readonly (int First, int Species, string[] Forms, GameFamilies? OnlyFrom)[] FormRuns =
        {
            (503, 386, new[] { "Attack", "Defense", "Speed" }, null),
            (506, 201, new[] { "A (unused)", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P",
                               "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "!", "?" }, null),
            (534, 412, new[] { "Sandy", "Trash" }, null),
            (536, 413, new[] { "Sandy", "Trash" }, null),
            (538, 422, new[] { "East Sea" }, null),
            (539, 423, new[] { "East Sea" }, null),
            (540, 487, new[] { "Origin" }, GameFamilies.Plat),
            (541, 492, new[] { "Sky" }, GameFamilies.Plat),
            (542, 479, new[] { "Heat", "Wash", "Frost", "Fan", "Mow" }, GameFamilies.Plat),
            (547, 351, new[] { "Sunny", "Rainy", "Snowy" }, GameFamilies.HGSS),
            (550, 421, new[] { "Sunshine" }, GameFamilies.HGSS),
        };

        private static bool GameHas(GameFamilies? onlyFrom) => onlyFrom switch
        {
            null => true,
            GameFamilies.Plat => gameFamily == GameFamilies.Plat || gameFamily == GameFamilies.HGSS,
            _ => gameFamily == onlyFrom,
        };

        // Diamond and Pearl have only the Deoxys and Wormadam form entries.
        private static int FormEntriesInThisGame =>
            gameFamily == GameFamilies.DP ? 5 : PokeDatabase.PersonalData.personalExtraFiles.Length;

        /// <summary>What an icon file is, or null for the shared files and the empty species 0 picture.</summary>
        public static Icon Describe(int file)
        {
            if (file <= SharedFiles) return null;
            if (isHGE || file < EggFile) return new Icon { File = file, Species = file - SharedFiles, EditorId = file - SharedFiles };
            if (file == EggFile) return new Icon { File = file, IsEgg = true, Form = "Egg", EditorId = EggEntry };
            if (file == ManaphyEggFile)
                return new Icon { File = file, Species = ManaphySpecies, IsEgg = true, Form = "Egg", EditorId = ManaphySpecies };

            foreach (var run in FormRuns)
            {
                int at = file - run.First;
                if (at < 0 || at >= run.Forms.Length || !GameHas(run.OnlyFrom)) continue;
                return new Icon { File = file, Species = run.Species, Form = run.Forms[at], EditorId = EditorEntry(file, run.Species) };
            }
            return null;
        }

        /// <summary>A form with a Pokémon Editor entry of its own opens that entry, any other its species.</summary>
        private static int EditorEntry(int file, int species)
        {
            var extras = PokeDatabase.PersonalData.personalExtraFiles;
            for (int i = 0; i < FormEntriesInThisGame && i < extras.Length; i++)
                if (extras[i].iconId == file - SharedFiles) return NameBankEntries + i;
            return species;
        }

        /// <summary>A name for an icon, such as "Unown, B" or "Manaphy, Egg".</summary>
        public static string Label(Icon icon, IReadOnlyList<string> speciesNames)
        {
            if (icon == null) return null;
            if (icon.IsEgg && icon.Species == 0) return "Egg";
            string who = speciesNames != null && icon.Species >= 0 && icon.Species < speciesNames.Count
                ? speciesNames[icon.Species]?.Trim() : null;
            if (string.IsNullOrEmpty(who)) who = "Pokémon " + icon.Species;
            return icon.Form == null ? who : $"{who}, {icon.Form}";
        }
    }
}
