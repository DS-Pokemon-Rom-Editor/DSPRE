using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The {COLOR} code as the games' text printer reads it (render_text.c in pokediamond, pokeplatinum and
    /// pokeheartgold). Colour n draws with entries 2n+1 and 2n+2 of the box's palette row.
    /// </summary>
    public static class TextColours
    {
        private const int StoreFirst = 100, StoreCount = 7, SwapStored = 255, NothingStored = 0xFF;

        private static readonly Regex ColourCode = new Regex(@"\{COLOR, *\d+, *(\d+)\}", RegexOptions.Compiled);
        private static readonly Regex AnyCode = new Regex(@"\{[^}]*\}", RegexOptions.Compiled);

        /// <summary>The current colour, and the colour 255 switches back to.</summary>
        public readonly record struct State(int Pair, int Stored)
        {
            public static State Start => new State(0, NothingStored);
            public bool IsStart => Pair == 0 && !HasStored;
            public bool HasStored => Stored >= StoreFirst && Stored < StoreFirst + StoreCount;
        }

        public static State Apply(State state, int value)
        {
            if (value == SwapStored)
            {
                int pair = state.HasStored ? state.Stored - StoreFirst : state.Pair;
                return new State(pair, state.Pair + StoreFirst);
            }
            if (value >= StoreFirst) return state with { Stored = value };
            return state with { Pair = value };
        }

        public static State After(State state, string text)
        {
            if (string.IsNullOrEmpty(text)) return state;
            foreach (Match m in ColourCode.Matches(text))
                if (int.TryParse(m.Groups[1].Value, out int value)) state = Apply(state, value);
            return state;
        }

        /// <summary>Codes that put <see cref="State.Start"/> into <paramref name="state"/>, so a line can be drawn on its own.</summary>
        public static string Prefix(State state)
        {
            if (state.IsStart) return "";
            StringBuilder codes = new StringBuilder();
            if (state.HasStored) codes.Append("{COLOR,0,").Append(state.Stored).Append('}');
            if (state.Pair != 0) codes.Append("{COLOR,0,").Append(state.Pair).Append('}');
            return codes.ToString();
        }

        /// <summary>The printable pieces of <paramref name="line"/>, each with the colour it is drawn in. Other codes are dropped.</summary>
        public static List<(string Text, int Pair)> Runs(string line, ref State state)
        {
            List<(string, int)> runs = new List<(string, int)>();
            if (string.IsNullOrEmpty(line)) return runs;
            int at = 0;
            foreach (Match m in ColourCode.Matches(line))
            {
                AddRun(runs, line.Substring(at, m.Index - at), state.Pair);
                if (int.TryParse(m.Groups[1].Value, out int value)) state = Apply(state, value);
                at = m.Index + m.Length;
            }
            AddRun(runs, line.Substring(at), state.Pair);
            return runs;
        }

        private static void AddRun(List<(string, int)> runs, string piece, int pair)
        {
            string text = WithoutCodes(piece);
            if (text.Length > 0) runs.Add((text, pair));
        }

        /// <summary>The palette entries for ink and shadow. A 16-colour row wraps, as the 4-bit pixels do.</summary>
        public static (int Ink, int Shadow) Entries(int pair) => ((2 * pair + 1) & 15, (2 * pair + 2) & 15);

        public static string WithoutCodes(string text) => string.IsNullOrEmpty(text) ? text ?? "" : AnyCode.Replace(text, "");

        /// <summary>Removes the spaces inside codes, so splitting text at spaces never cuts one.</summary>
        public static string Compact(string text) =>
            string.IsNullOrEmpty(text) ? text : AnyCode.Replace(text, m => m.Value.Replace(" ", ""));
    }
}
