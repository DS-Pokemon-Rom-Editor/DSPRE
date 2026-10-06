using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.Avalonia.Data
{
    public sealed class BattleAnimMacroStep
    {
        public string Opcode = "";

        public int[] Words = Array.Empty<int>();
    }

    public sealed class BattleAnimMacro
    {
        public string Name = "";
        public string Summary = "";
        public string[] Settings = Array.Empty<string>();
        public BattleAnimMacroStep[] Steps = Array.Empty<BattleAnimMacroStep>();
    }

    public static class BattleAnimMacros
    {
        private static BattleAnimMacroStep S(string op, params int[] words)
            => new BattleAnimMacroStep { Opcode = op, Words = words };

        private const int A = -1, B = -2, C = -3, D = -4;

        private static readonly BattleAnimMacro[] All =
        {
            new BattleAnimMacro
            {
                Name = "LoadParticleResource",
                Summary = "Loads a particle set, keeping all four Pokémon on screen while it loads.",
                Settings = new[] { "slot", "set" },
                Steps = new[]
                {
                    S("InitPokemonSpriteManager"),
                    S("LoadPokemonSpriteDummyResources", 0), S("LoadPokemonSpriteDummyResources", 1),
                    S("LoadPokemonSpriteDummyResources", 2), S("LoadPokemonSpriteDummyResources", 3),
                    S("AddPokemonSprite", 4, 0, 0, 0), S("AddPokemonSprite", 5, 0, 1, 1),
                    S("AddPokemonSprite", 6, 0, 2, 2), S("AddPokemonSprite", 7, 0, 3, 3),
                    S("CallFunc", 78, 1, 0),
                    S("LoadParticleSystem", A, B),
                    S("WaitForAnimTasks"),
                    S("FreePokemonSpriteManager"),
                    S("RemovePokemonSprite", 0), S("RemovePokemonSprite", 1),
                    S("RemovePokemonSprite", 2), S("RemovePokemonSprite", 3),
                },
            },

            new BattleAnimMacro
            {
                Name = "AddExtraPokemonCopy",
                Summary = "Drops one more copy of a Pokémon and draws the particles against it.",
                Settings = new[] { "who", "drawn as" },
                Steps = new[] { S("LoadPokemonSpriteDummyResources", 4), S("AddPokemonSprite", A, 0, 4, 4), S("CreatePokemonCopy", B, 0, 4) },
            },

            new BattleAnimMacro
            {
                Name = "AddExtraPokemonCopyInSlot",
                Summary = "Drops one more copy of a Pokémon, into a copy slot you choose.",
                Settings = new[] { "who", "drawn as", "copy", "graphics" },
                Steps = new[] { S("LoadPokemonSpriteDummyResources", D), S("AddPokemonSprite", A, 0, C, D), S("CreatePokemonCopy", B, 0, C) },
            },

            new BattleAnimMacro
            {
                Name = "RemoveExtraPokemonCopy",
                Summary = "Puts back the extra copy AddExtraPokemonCopy made.",
                Settings = Array.Empty<string>(),
                Steps = new[] { S("RemovePokemonCopy", 4), S("RemovePokemonSprite", 4) },
            },

            new BattleAnimMacro
            {
                Name = "RemoveExtraPokemonCopyInSlot",
                Summary = "Puts back the extra copy AddExtraPokemonCopyInSlot made.",
                Settings = new[] { "copy" },
                Steps = new[] { S("RemovePokemonCopy", A), S("RemovePokemonSprite", A) },
            },
        };

        public static IReadOnlyList<BattleAnimMacro> Known => All;

        public readonly struct Folded
        {
            public readonly BattleAnimMacro Macro;
            public readonly int From, Count;
            public readonly int[] Settings;
            public Folded(BattleAnimMacro m, int from, int count, int[] settings)
            { Macro = m; From = from; Count = count; Settings = settings; }
        }

        public static List<Folded> Find(IReadOnlyList<WazaSeqCommand> cmds, WazaSeqVersion version)
        {
            List<Folded> found = new List<Folded>();
            if (cmds == null) return found;

            int i = 0;
            while (i < cmds.Count)
            {
                Folded? hit = null;
                foreach (BattleAnimMacro m in All)
                {
                    int[] settings = TryMatch(cmds, i, m, version);
                    if (settings == null) continue;
                    if (hit == null || m.Steps.Length > hit.Value.Count)
                        hit = new Folded(m, i, m.Steps.Length, settings);
                }
                if (hit != null) { found.Add(hit.Value); i += hit.Value.Count; }
                else i++;
            }
            return found;
        }

        private static int[] TryMatch(IReadOnlyList<WazaSeqCommand> cmds, int at, BattleAnimMacro m, WazaSeqVersion version)
        {
            if (at + m.Steps.Length > cmds.Count) return null;

            int slots = 0;
            foreach (BattleAnimMacroStep s in m.Steps) foreach (int w in s.Words) if (w < 0) slots = Math.Max(slots, -w);
            int[] settings = new int[slots];
            bool[] filled = new bool[slots];

            for (int k = 0; k < m.Steps.Length; k++)
            {
                BattleAnimMacroStep step = m.Steps[k];
                WazaSeqCommand c = cmds[at + k];
                if (BattleAnimCommands.Name(version, c.OpId) != step.Opcode) return null;
                if (c.Args.Length != step.Words.Length) return null;

                for (int w = 0; w < step.Words.Length; w++)
                {
                    int want = step.Words[w];
                    if (want >= 0)
                    {
                        if (c.Args[w] != want) return null;
                    }
                    else
                    {
                        int slot = -want - 1;
                        if (filled[slot] && settings[slot] != c.Args[w]) return null;
                        settings[slot] = c.Args[w];
                        filled[slot] = true;
                    }
                }
            }
            return settings;
        }

        public static List<WazaSeqCommand> Unfold(BattleAnimMacro m, int[] settings, WazaSeqVersion version)
        {
            List<WazaSeqCommand> outp = new List<WazaSeqCommand>(m.Steps.Length);
            foreach (BattleAnimMacroStep step in m.Steps)
            {
                int op = BattleAnimCommands.Id(version, step.Opcode);
                if (op < 0) return null;
                int[] args = new int[step.Words.Length];
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
