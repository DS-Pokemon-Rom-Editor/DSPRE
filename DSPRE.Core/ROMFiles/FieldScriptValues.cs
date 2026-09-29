using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>Tells a plain number in a script apart from a variable.</summary>
    public static class FieldScriptValues
    {
        /// <summary>Below this a number is just a number.</summary>
        public const int SavedFirst = 0x4000;

        /// <summary>From here up it is one of the script's own slots.</summary>
        public const int ScriptFirst = 0x8000;

        private const int LastSpecial = 0x800d;

        /// <summary>Whether this number names a variable rather than being a value on its own.</summary>
        public static bool IsVariable(int value) => value >= SavedFirst;

        /// <summary>The variable's name as the script database and rotom write it, or null when it has none.</summary>
        public static string NameOf(int value)
        {
            if (value < 0 || value > ushort.MaxValue) return null;
            if (DSPRE.Resources.ScriptDatabase.varNames.TryGetValue((ushort)value, out string n)) return n;
            if (value < ScriptFirst || value > LastSpecial) return null;

            // Spelled as the database spells them for each game, for when no database is loaded.
            bool johto = RomInfo.gameFamily == RomInfo.GameFamilies.HGSS;
            return value switch
            {
                0x800c => johto ? "VAR_SPECIAL_RESULT" : "VAR_RESULT",
                0x800d => johto ? "VAR_SPECIAL_LAST_TALKED" : "VAR_LAST_TALKED",
                _ => johto ? $"VAR_SPECIAL_x{value:X4}" : $"VAR_0x{value:X4}",
            };
        }

        /// <summary>How to write a number that may be either. </summary>
        public static string Describe(int value)
        {
            if (!IsVariable(value)) return value.ToString();

            return NameOf(value) ?? $"VAR_0x{value:X4}";
        }

        /// <summary>What the two ends of a "put this there" command read as.</summary>
        public static string DescribeTarget(int value) =>
            IsVariable(value) ? Describe(value) : $"0x{value:X4}";
    }

    /// <summary>The four message archives a script can ask for by number.</summary>
    public static class FieldSharedMessageArchives
    {
        private static readonly string[] Names =
        {
            "the week siblings", "the HM tutors", "the cameraman", "the shops",
        };

        public static int Count => Names.Length;

        /// <summary>What archive an index picks, or null when it is out of range.</summary>
        public static string NameOf(int index) =>
            index >= 0 && index < Names.Length ? Names[index] : null;
    }
}
