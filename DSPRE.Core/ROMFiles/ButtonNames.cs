using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>Button names and lock reasons shared by the screen button pages.</summary>
    public static class ButtonNames
    {
        /// <summary>"Slot 1", "Slot 2" and so on.</summary>
        public static string[] Numbered(string name, int count) => Enumerable.Range(1, count).Select(i => $"{name} {i}").ToArray();

        public static string[] Join(params string[][] parts) => parts.SelectMany(p => p).ToArray();

        public static Dictionary<int, string> Locked(IEnumerable<int> buttons, string why) => buttons.ToDictionary(b => b, _ => why);

        /// <summary>Buttons whose art the game places itself.</summary>
        public static Dictionary<int, string> FixedArt(params int[] buttons) => Locked(buttons, TouchRects.FixedArt);
    }
}
