namespace DSPRE.ROMFiles
{
    /// <summary>
    /// How HGSS picks a headbutt table: the 12 "normal" slots are common then rare, the 6 "special" slots are secret.
    /// A normal tree's table depends on its list position and the trainer ID's last digit.
    /// </summary>
    public static class HeadbuttRules
    {
        public const int SlotsPerTable = 6;

        /// <summary>Chance of each slot, in percent.</summary>
        public static readonly int[] SlotChance = { 50, 15, 15, 10, 5, 5 };

        public enum Table { None = -1, Common = 0, Rare = 1, Secret = 2 }

        private const Table C = Table.Common, R = Table.Rare, N = Table.None;

        private static readonly Table[][] One =
        {
            new[] { C }, new[] { R }, new[] { C }, new[] { R }, new[] { C },
            new[] { R }, new[] { C }, new[] { R }, new[] { C }, new[] { R },
        };
        private static readonly Table[][] Two =
        {
            new[] { C, R }, new[] { R, C }, new[] { C, R }, new[] { R, C }, new[] { C, R },
            new[] { R, C }, new[] { C, R }, new[] { R, C }, new[] { C, R }, new[] { R, C },
        };
        private static readonly Table[][] Three =
        {
            new[] { C, R, N }, new[] { C, N, R }, new[] { R, C, N }, new[] { R, N, C }, new[] { N, C, R },
            new[] { N, R, C }, new[] { C, R, N }, new[] { C, N, R }, new[] { R, C, N }, new[] { R, N, C },
        };
        private static readonly Table[][] Four =
        {
            new[] { N, C, C, R }, new[] { R, N, C, C }, new[] { R, R, N, C }, new[] { C, R, R, N }, new[] { C, C, R, R },
            new[] { N, C, R, C }, new[] { R, N, C, R }, new[] { C, R, N, C }, new[] { R, C, R, N }, new[] { C, R, C, R },
        };
        private static readonly Table[][] FivePlus =
        {
            new[] { N, C, C, R, R }, new[] { R, N, C, C, R }, new[] { R, R, N, C, C }, new[] { C, R, R, N, C }, new[] { C, C, R, R, N },
            new[] { N, C, R, C, R }, new[] { R, N, C, R, C }, new[] { C, R, N, C, R }, new[] { R, C, R, N, C }, new[] { C, R, C, R, N },
        };

        /// <summary>The table normal tree <paramref name="tree"/> of <paramref name="treeCount"/> uses for a trainer ID
        /// ending in <paramref name="idDigit"/>. From five trees on, the pattern repeats every five.</summary>
        public static Table NormalTreeTable(int tree, int treeCount, int idDigit)
        {
            if (treeCount <= 0 || tree < 0 || tree >= treeCount || idDigit < 0 || idDigit > 9) return Table.None;
            return treeCount switch
            {
                1 => One[idDigit][0],
                2 => Two[idDigit][tree],
                3 => Three[idDigit][tree],
                4 => Four[idDigit][tree],
                _ => FivePlus[idDigit][tree % 5],
            };
        }
    }
}
