using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Tells a plain number in a script apart from a variable.
    ///
    /// GetEventWorkValue in script.c:1144 asks GetEventWorkAdrs for the address a number names, and when
    /// there is none it hands the number straight back as the value. GetEventWorkAdrs at :1122 draws the
    /// line: anything below SVWK_START is not a variable at all, from there to SCWK_START is a saved
    /// variable, and from SCWK_START up it is one of the script's own working slots. Those last ones have
    /// names in usescript.h, and 0x800c is the answer a menu or a yes-or-no question left behind.
    /// </summary>
    public static class FieldScriptValues
    {
        /// <summary>SVWK_START. Below this a number is just a number.</summary>
        public const int SavedFirst = 0x4000;

        /// <summary>SCWK_START. From here up it is one of the script's own slots.</summary>
        public const int ScriptFirst = 0x8000;

        private static readonly Dictionary<int, string> Named = new Dictionary<int, string>
        {
            [0x8000] = "PARAM0", [0x8001] = "PARAM1", [0x8002] = "PARAM2", [0x8003] = "PARAM3",
            [0x8004] = "TEMP0",  [0x8005] = "TEMP1",  [0x8006] = "TEMP2",  [0x8007] = "TEMP3",
            [0x8008] = "REG0",   [0x8009] = "REG1",   [0x800a] = "REG2",   [0x800b] = "REG3",
            [0x800c] = "ANSWER",
            [0x800d] = "TARGET_OBJID",
        };

        /// <summary>Whether this number names a variable rather than being a value on its own.</summary>
        public static bool IsVariable(int value) => value >= SavedFirst;

        /// <summary>The script slot's own name, or null when the number is not one of them.</summary>
        public static string NameOf(int value) => Named.TryGetValue(value, out string n) ? n : null;

        /// <summary>
        /// How to write a number that may be either. A plain number reads as itself; a variable reads as
        /// what it is, so a reader can tell the two apart at a glance.
        /// </summary>
        public static string Describe(int value)
        {
            if (!IsVariable(value)) return value.ToString();

            string named = NameOf(value);
            if (named != null) return named;
            if (value >= ScriptFirst) return $"script slot 0x{value:X4}";
            return $"variable 0x{value:X4}";
        }

        /// <summary>What the two ends of a "put this there" command read as.</summary>
        public static string DescribeTarget(int value) =>
            IsVariable(value) ? Describe(value) : $"0x{value:X4}";
    }

    /// <summary>
    /// The four message archives a script can ask for by number.
    ///
    /// EvCmdGetOtherMsgArcID in scr_msg.c:103 keeps them in a table of four and puts the archive's own
    /// number into the variable it is given, so a later message command can read from it. The command
    /// shows nothing itself.
    /// </summary>
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
