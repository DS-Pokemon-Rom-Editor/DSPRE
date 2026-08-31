using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.Avalonia.Data
{
    /// <summary>One command inside a shorthand: which command, and what each of its words has to be.</summary>
    public sealed class WestMacroStep
    {
        public string Opcode = "";

        /// <summary>One entry per word. A number is a value that must match exactly; a negative number is
        /// a slot, -1 for the shorthand's first setting, -2 for its second, and so on.</summary>
        public int[] Words = Array.Empty<int>();
    }

    /// <summary>A shorthand the games' own scripts are written in, and the commands it stands for.</summary>
    public sealed class WestMacro
    {
        public string Name = "";
        public string Summary = "";
        /// <summary>What to call each setting the shorthand takes, in order.</summary>
        public string[] Settings = Array.Empty<string>();
        public WestMacroStep[] Steps = Array.Empty<WestMacroStep>();
    }

    /// <summary>
    /// The shorthands the games' move scripts are actually written in.
    ///
    /// A move script in the leak is not the list of commands the editor shows. `LOAD_PARTICLE_DROP 0, X`
    /// is one line somebody wrote and seventeen commands in the ROM, and it appears 476 times, so on its
    /// own it accounts for 8,092 of the 18,641 commands across the 501 scripts: 43 of every 100 things on
    /// screen are that one line. Showing it back as one line is the difference between a script somebody
    /// can read and a wall.
    ///
    /// Folding only ever happens on an exact match: the run of commands must be the right ones in the
    /// right order, and every word that the shorthand fixes must hold that exact value. Only the words
    /// the shorthand leaves open are read back out as its settings. Anything else stays as it is, so a
    /// script that merely looks similar is never quietly rewritten.
    ///
    /// The shapes here were taken from west.h's own macro bodies rather than written by hand.
    /// </summary>
    public static class WestMacros
    {
        private static WestMacroStep S(string op, params int[] words)
            => new WestMacroStep { Opcode = "WEST_" + op, Words = words };

        // Slots, as negative numbers so they cannot be mistaken for a value.
        private const int A = -1, B = -2, C = -3, D = -4;

        private static readonly WestMacro[] All =
        {
            new WestMacro
            {
                Name = "LOAD_PARTICLE_DROP",
                Summary = "Loads a particle set, keeping all four Pokémon on screen while it loads.",
                Settings = new[] { "slot", "set" },
                Steps = new[]
                {
                    S("POKEOAM_RES_INIT"),
                    S("POKEOAM_RES_LOAD", 0), S("POKEOAM_RES_LOAD", 1),
                    S("POKEOAM_RES_LOAD", 2), S("POKEOAM_RES_LOAD", 3),
                    S("POKEOAM_DROP", 4, 0, 0, 0), S("POKEOAM_DROP", 5, 0, 1, 1),
                    S("POKEOAM_DROP", 6, 0, 2, 2), S("POKEOAM_DROP", 7, 0, 3, 3),
                    S("FUNC_CALL", 78, 1, 0),
                    S("LOAD_PARTICLE", A, B),
                    S("WAIT_FLAG"),
                    S("POKEOAM_RES_FREE"),
                    S("POKEOAM_DROP_RESET", 0), S("POKEOAM_DROP_RESET", 1),
                    S("POKEOAM_DROP_RESET", 2), S("POKEOAM_DROP_RESET", 3),
                },
            },

            new WestMacro
            {
                Name = "PT_DROP_EX",
                Summary = "Drops one more copy of a Pokémon and draws the particles against it.",
                Settings = new[] { "who", "drawn as" },
                Steps = new[] { S("POKEOAM_RES_LOAD", 4), S("POKEOAM_DROP", A, 0, 4, 4), S("PT_DROP", B, 0, 4) },
            },

            new WestMacro
            {
                Name = "PT_DROP_EX_2",
                Summary = "Drops one more copy of a Pokémon, into a copy slot you choose.",
                Settings = new[] { "who", "drawn as", "copy", "graphics" },
                Steps = new[] { S("POKEOAM_RES_LOAD", D), S("POKEOAM_DROP", A, 0, C, D), S("PT_DROP", B, 0, C) },
            },

            new WestMacro
            {
                Name = "PT_DROP_RESET_EX",
                Summary = "Puts back the extra copy PT_DROP_EX made.",
                Settings = Array.Empty<string>(),
                Steps = new[] { S("PT_DROP_RESET", 4), S("POKEOAM_DROP_RESET", 4) },
            },

            new WestMacro
            {
                Name = "PT_DROP_RESET_EX_2",
                Summary = "Puts back the extra copy PT_DROP_EX_2 made.",
                Settings = new[] { "copy" },
                Steps = new[] { S("PT_DROP_RESET", A), S("POKEOAM_DROP_RESET", A) },
            },
        };

        public static IReadOnlyList<WestMacro> Known => All;

        /// <summary>One run of commands that a shorthand stands for.</summary>
        public readonly struct Folded
        {
            public readonly WestMacro Macro;
            public readonly int From, Count;
            public readonly int[] Settings;
            public Folded(WestMacro m, int from, int count, int[] settings)
            { Macro = m; From = from; Count = count; Settings = settings; }
        }

        /// <summary>
        /// Every run of commands that is exactly one of the shorthands, earliest first and never
        /// overlapping. Anything not returned here is left exactly as it was.
        /// </summary>
        public static List<Folded> Find(IReadOnlyList<WazaSeqCommand> cmds, WazaSeqVersion version)
        {
            var found = new List<Folded>();
            if (cmds == null) return found;

            int i = 0;
            while (i < cmds.Count)
            {
                Folded? hit = null;
                foreach (var m in All)
                {
                    var settings = TryMatch(cmds, i, m, version);
                    if (settings == null) continue;
                    // Longest wins, so a shorthand that contains a shorter one is not split up.
                    if (hit == null || m.Steps.Length > hit.Value.Count)
                        hit = new Folded(m, i, m.Steps.Length, settings);
                }
                if (hit != null) { found.Add(hit.Value); i += hit.Value.Count; }
                else i++;
            }
            return found;
        }

        /// <summary>The settings a shorthand was used with, or null when this is not that shorthand.</summary>
        private static int[] TryMatch(IReadOnlyList<WazaSeqCommand> cmds, int at, WestMacro m, WazaSeqVersion version)
        {
            if (at + m.Steps.Length > cmds.Count) return null;

            int slots = 0;
            foreach (var s in m.Steps) foreach (int w in s.Words) if (w < 0) slots = Math.Max(slots, -w);
            var settings = new int[slots];
            var filled = new bool[slots];

            for (int k = 0; k < m.Steps.Length; k++)
            {
                var step = m.Steps[k];
                var c = cmds[at + k];
                if (WestOpcodes.Name(version, c.OpId) != step.Opcode) return null;
                if (c.Args.Length != step.Words.Length) return null;

                for (int w = 0; w < step.Words.Length; w++)
                {
                    int want = step.Words[w];
                    if (want >= 0)
                    {
                        if (c.Args[w] != want) return null;   // a fixed word that does not hold
                    }
                    else
                    {
                        int slot = -want - 1;
                        if (filled[slot] && settings[slot] != c.Args[w]) return null;   // the same setting used twice, differently
                        settings[slot] = c.Args[w];
                        filled[slot] = true;
                    }
                }
            }
            return settings;
        }

        /// <summary>The commands a shorthand stands for, which is what unfolding writes back.</summary>
        public static List<WazaSeqCommand> Unfold(WestMacro m, int[] settings, WazaSeqVersion version)
        {
            var outp = new List<WazaSeqCommand>(m.Steps.Length);
            foreach (var step in m.Steps)
            {
                int op = WestOpcodes.Id(version, step.Opcode);
                if (op < 0) return null;
                var args = new int[step.Words.Length];
                for (int w = 0; w < args.Length; w++)
                {
                    int want = step.Words[w];
                    args[w] = want >= 0 ? want
                            : (settings != null && -want - 1 < settings.Length ? settings[-want - 1] : 0);
                }
                outp.Add(new WazaSeqCommand(op, args));
            }
            return outp;
        }
    }
}
