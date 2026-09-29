using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DSPRE.Avalonia.Data
{
    public enum BattleAnimViewMode
    {
        Guided = 0,
        Script = 1,
        Raw = 2,
    }

    public sealed class BattleAnimLine
    {
        public int Index = -1;

        public int Covers = 1;

        public bool IsHeading;

        public int Depth;

        public string Text = "";

        public string Display => IsHeading ? "── " + Text + " ──" : new string(' ', Depth * 3) + Text;

        public string Detail = "";

        public string Source = "";
    }

    public static class BattleAnimScriptDisplay
    {
        private static string GroupOf(string op) => op switch
        {
            "LoadParticleSystem" or "LoadDebugParticleSystem" or "InitPokemonSpriteManager" or "LoadPokemonSpriteDummyResources"
                or "InitSpriteManager" or "LoadCharResObj" or "LoadPlttRes"
                or "LoadCellResObj" or "LoadAnimResObj" => "What it loads",

            "PlaySoundEffect" or "PlayPannedSoundEffect" or "PanSoundEffects" or "PlayMovingSoundEffectAtkDef" or "PlayLoopedSoundEffect"
                or "PlayDelayedSoundEffect" or "Nop3" or "StopSoundEffect" or "PlayPokemonCry"
                or "WaitForPokemonCries" => "What it sounds like",

            "SwitchBg" or "SwitchBgAnimated" or "RestoreBg" or "WaitForBgSwitch"
                or "WaitForPartialBgSwitch" or "SetBgSwitchVar" or "FlashScreen"
                or "LoadPokemonSpriteIntoBg" or "RemovePokemonSpriteFromBg" => "What the screen does",

            "AddPokemonSprite" or "RemovePokemonSprite" or "FreePokemonSpriteManager" or "CreatePokemonCopy"
                or "RemovePokemonCopy" or "FreeSpriteManager" or "UnloadParticleSystem"
                or "SetPokemonSpriteVisible" => "What it puts back",

            "End" => "Where it ends",

            "Delay" or "WaitForAnimTasks" or "WaitForAllEmitters" or "EndLoop"
                or "BeginLoop" => "How it is timed",

            "JumpByTurn" or "JumpIfBattlerSide" or "Jump" or "JumpIfWeather" or "JumpIfContest"
                or "JumpIfFriendlyFire" or "Call" or "Return" => "Which version plays",

            "SetVar" or "ResetVars" => "Settings for the next command",

            _ => "What happens",
        };

        private static string GroupOfCall(int[] args)
        {
            if (args.Length < 1) return "What happens";
            var r = BattleAnimFuncs.Get(args[0]);
            if (r == null) return "What happens";

            for (int w = 0; w < r.Words.Length && w + 2 < args.Length; w++)
            {
                string meaning = r.Words[w];
                if (string.IsNullOrEmpty(meaning) || !meaning.Contains("target flag")) continue;
                int flag = args[w + 2];
                if ((flag & BattleAnimTargetFlags.Background) != 0) return "What the screen does";
                if ((flag & BattleAnimTargetFlags.Attacker) != 0 && (flag & BattleAnimTargetFlags.Defender) == 0) return "What the attacker does";
                return "What hits the target";
            }

            string sum = r.Summary ?? "";
            if (sum.Contains("background") || sum.Contains("screen") || sum.Contains("colour")) return "What the screen does";
            if (sum.Contains("attacker")) return "What the attacker does";
            return "What happens";
        }

        public static List<BattleAnimLine> Build(IReadOnlyList<WazaSeqCommand> cmds, WazaSeqVersion version,
                                           BattleAnimViewMode mode, Func<int, string> soundName = null)
        {
            var lines = new List<BattleAnimLine>();
            if (cmds == null || cmds.Count == 0) return lines;

            if (mode == BattleAnimViewMode.Raw) { BuildRaw(cmds, version, lines); return lines; }

            var folds = BattleAnimMacros.Find(cmds, version);
            var foldAt = folds.ToDictionary(f => f.From);

            if (mode == BattleAnimViewMode.Guided) BuildGuided(cmds, version, folds, foldAt, lines, soundName);
            else BuildScript(cmds, version, foldAt, lines, soundName);
            return lines;
        }

        private static void BuildRaw(IReadOnlyList<WazaSeqCommand> cmds, WazaSeqVersion version, List<BattleAnimLine> lines)
        {
            for (int i = 0; i < cmds.Count; i++)
            {
                var c = cmds[i];
                string name = BattleAnimCommands.Name(version, c.OpId) ?? "?";
                var sb = new StringBuilder();
                sb.Append($"{c.WordPos,5}  {c.OpId,3}  {(name ?? ""),-26}");
                foreach (int a in c.Args) sb.Append($" {a,11}");
                if (c.Args.Length > 0)
                {
                    sb.Append("   ");
                    foreach (int a in c.Args) sb.Append($" {a:X8}");
                }
                lines.Add(new BattleAnimLine
                {
                    Index = i,
                    Text = sb.ToString(),
                    Detail = DetailFor(name, c.Args, version),
                    Source = SourceFor(name, c.Args),
                });
            }
        }

        private static void BuildScript(IReadOnlyList<WazaSeqCommand> cmds, WazaSeqVersion version,
                                        Dictionary<int, BattleAnimMacros.Folded> foldAt, List<BattleAnimLine> lines,
                                        Func<int, string> soundName)
        {
            int depth = 0;
            for (int i = 0; i < cmds.Count; )
            {
                if (foldAt.TryGetValue(i, out var fold))
                {
                    lines.Add(FoldLine(fold, depth));
                    i += fold.Count;
                    continue;
                }

                var c = cmds[i];
                string name = BattleAnimCommands.Name(version, c.OpId) ?? "?";
                bool closes = name is "EndLoop" or "Return";
                if (closes) depth = Math.Max(0, depth - 1);

                lines.Add(new BattleAnimLine
                {
                    Index = i,
                    Depth = depth,
                    Text = CommandText(name, c.Args, version, soundName),
                    Detail = DetailFor(name, c.Args, version),
                    Source = SourceFor(name, c.Args),
                });

                if (name is "BeginLoop" or "Call") depth++;
                i++;
            }
        }

        private static void BuildGuided(IReadOnlyList<WazaSeqCommand> cmds, WazaSeqVersion version,
                                        List<BattleAnimMacros.Folded> folds, Dictionary<int, BattleAnimMacros.Folded> foldAt,
                                        List<BattleAnimLine> lines, Func<int, string> soundName)
        {
            var byGroup = new List<(string group, BattleAnimLine line)>();
            for (int i = 0; i < cmds.Count; )
            {
                if (foldAt.TryGetValue(i, out var fold))
                {
                    byGroup.Add(("What it loads", FoldLine(fold, 0)));
                    i += fold.Count;
                    continue;
                }
                var c = cmds[i];
                string name = BattleAnimCommands.Name(version, c.OpId) ?? "?";
                string group = name is "CallFunc" or "Nop11"
                    ? GroupOfCall(c.Args) : GroupOf(name);
                byGroup.Add((group, new BattleAnimLine
                {
                    Index = i,
                    Text = CommandText(name, c.Args, version, soundName),
                    Detail = DetailFor(name, c.Args, version),
                    Source = SourceFor(name, c.Args),
                }));
                i++;
            }

            var order = new[]
            {
                "What it loads", "Which version plays", "Settings for the next command",
                "What the attacker does", "What happens", "What hits the target",
                "What the screen does", "What it sounds like", "How it is timed",
                "What it puts back", "Where it ends",
            };
            var groups = byGroup.Select(x => x.group).Distinct()
                                .OrderBy(g => { int at = Array.IndexOf(order, g); return at < 0 ? order.Length : at; })
                                .ToList();
            foreach (var g in groups)
            {
                lines.Add(new BattleAnimLine { IsHeading = true, Text = g });
                foreach (var (group, line) in byGroup)
                    if (group == g) { line.Depth = 1; lines.Add(line); }
            }
        }

        private static BattleAnimLine FoldLine(BattleAnimMacros.Folded f, int depth)
        {
            var sb = new StringBuilder((f.Macro.Name ?? "").PadRight(26));
            for (int s = 0; s < f.Settings.Length; s++)
            {
                string label = s < f.Macro.Settings.Length ? f.Macro.Settings[s] : "setting " + s;
                sb.Append($"  {label}={f.Settings[s]}");
            }
            return new BattleAnimLine
            {
                Index = f.From,
                Covers = f.Count,
                Depth = depth,
                Text = sb.ToString(),
                Detail = f.Macro.Summary + $" One line in the games' own scripts, {f.Count} commands in the ROM.",
            };
        }

        private static string CommandText(string opName, int[] args, WazaSeqVersion version, Func<int, string> soundName)
        {
            if (opName is "CallFunc" or "Nop11" && args.Length >= 2)
            {
                var call = new StringBuilder((opName ?? "").PadRight(22));
                call.Append(RoutineName(args[0]).PadRight(24));
                for (int w = 2; w < args.Length; w++)
                {
                    string meaning = BattleAnimFuncs.WordMeaning(args[0], w - 2);
                    call.Append(meaning != null && meaning.Contains("target flag")
                        ? "  " + BattleAnimTargetFlags.Describe(args[w], brief: true)
                        : $" {args[w],7}");
                }
                return call.ToString();
            }

            var sb = new StringBuilder((opName ?? "").PadRight(22));
            for (int i = 0; i < args.Length; i++)
            {
                string label = BattleAnimSchema.ParamName(opName, i) ?? ("arg " + i);
                string shown = Value(opName, i, args, version, soundName);
                if (shown == "None") continue;
                sb.Append($"  {label}={shown}");
            }
            return sb.ToString();
        }

        private static string Value(string opName, int i, int[] args, WazaSeqVersion version, Func<int, string> soundName)
        {
            int v = args[i];

            if (opName is "CallFunc" or "Nop11")
            {
                if (i == 0) return RoutineName(v);
                if (i == 1) return v.ToString();
                if (args.Length > 0)
                {
                    string meaning = BattleAnimFuncs.WordMeaning(args[0], i - 2);
                    if (meaning != null && meaning.Contains("target flag")) return BattleAnimTargetFlags.Describe(v, brief: true);
                }
            }
            if (opName.StartsWith("PlaySoundEffect", StringComparison.Ordinal) && i == 0 && soundName != null)
            {
                string n = soundName(v);
                if (!string.IsNullOrEmpty(n)) return n;
            }

            var options = BattleAnimSchema.EnumFor(opName, i);
            if (options != null)
            {
                foreach (var o in options)
                    if (o.Value == v) return o.Label;
            }
            return v.ToString();
        }

        private static string DetailFor(string opName, int[] args, WazaSeqVersion version)
        {
            if (opName is "CallFunc" or "Nop11" && args.Length > 0)
            {
                var r = BattleAnimFuncs.Get(args[0]);
                if (r != null)
                {
                    var sb = new StringBuilder(r.Summary);
                    for (int w = 0; w + 2 < args.Length; w++)
                    {
                        string m = BattleAnimFuncs.WordMeaning(args[0], w);
                        if (m == null) continue;
                        string shown = m.Contains("target flag")
                            ? args[w + 2] + " = " + BattleAnimTargetFlags.Describe(args[w + 2])
                            : args[w + 2].ToString();
                        sb.Append($"\n  {shown}: {m}");
                    }
                    return sb.ToString();
                }
            }
            return BattleAnimSchema.OpcodeDoc(opName) ?? "";
        }

        private static string SourceFor(string opName, int[] args)
            => (opName is "CallFunc" or "Nop11") && args.Length > 0
               ? BattleAnimFuncs.Get(args[0])?.Source ?? "" : "";

        public static string RoutineName(int id)
        {
            string custom = LabelStore.GetLabel("battle_anim_funcs", id);
            if (!string.IsNullOrWhiteSpace(custom)) return custom;
            return BattleAnimFuncs.Get(id)?.Name ?? id.ToString();
        }

    }
}
