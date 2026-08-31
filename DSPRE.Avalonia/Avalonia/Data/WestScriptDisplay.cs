using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Which of the three ways of reading a move script is on screen.</summary>
    public enum WestViewMode
    {
        /// <summary>Grouped by what part of the move it is, with the shorthands folded away.</summary>
        Guided = 0,
        /// <summary>One command a line, lined up in columns, with loops and branches indented.</summary>
        Script = 1,
        /// <summary>Word for word, nothing folded and nothing hidden.</summary>
        Raw = 2,
    }

    /// <summary>One line on screen, whichever view is showing.</summary>
    public sealed class WestLine
    {
        /// <summary>Which command in the script this line starts at, or -1 for a heading.</summary>
        public int Index = -1;

        /// <summary>How many commands this line stands for. More than one means a folded shorthand.</summary>
        public int Covers = 1;

        /// <summary>A heading rather than a command.</summary>
        public bool IsHeading;

        /// <summary>How far in to indent, for loop and subroutine bodies.</summary>
        public int Depth;

        public string Text = "";

        /// <summary>The line as it appears, with its indent already in it so no view has to add one.</summary>
        public string Display => IsHeading ? "── " + Text + " ──" : new string(' ', Depth * 3) + Text;

        /// <summary>What this line is, for the panel beside it. Empty when there is nothing to say.</summary>
        public string Detail = "";

        /// <summary>Where the fact in <see cref="Detail"/> came from, so it can be checked.</summary>
        public string Source = "";
    }

    /// <summary>
    /// Turns a move script into lines somebody can read, three different ways.
    ///
    /// These are instruction streams with timing in them, so none of the three writes a command out as a
    /// sentence. What makes them readable instead is that the shorthands the scripts were written in are
    /// folded back up, the numbers carry the names the games give them, the columns line up so a run of
    /// near-identical commands shows its one difference at a glance, and loop and subroutine bodies are
    /// indented so the shape of the script is visible.
    /// </summary>
    public static class WestScriptDisplay
    {
        /// <summary>The part of a move a command belongs to, in the order they happen.</summary>
        private static string GroupOf(string op) => op switch
        {
            "WEST_LOAD_PARTICLE" or "WEST_LOAD_PARTICLE_EX" or "WEST_POKEOAM_RES_INIT" or "WEST_POKEOAM_RES_LOAD"
                or "WEST_CATS_RES_INIT" or "WEST_CATS_CAHR_RES_LOAD" or "WEST_CATS_PLTT_RES_LOAD"
                or "WEST_CATS_CELL_RES_LOAD" or "WEST_CATS_CELLANM_RES_LOAD" => "What it loads",

            "WEST_SE" or "WEST_SEPLAY_PAN" or "WEST_SEPAN" or "WEST_SEPAN_FLOW" or "WEST_SE_REPEAT"
                or "WEST_SE_WAITPLAY" or "WEST_SE_TASK" or "WEST_SE_STOP" or "WEST_VOICE_PLAY"
                or "WEST_VOICE_WAIT_STOP" => "What it sounds like",

            "WEST_HAIKEI_CHG" or "WEST_HAIKEI_CHG_EX" or "WEST_HAIKEI_RECOVER" or "WEST_HAIKEI_CHG_WAIT"
                or "WEST_HAIKEI_HALF_WAIT" or "WEST_HAIKEI_PARA_CHG" or "WEST_FLASH"
                or "WEST_POKEBG_DROP" or "WEST_POKEBG_DROP_RESET" => "What the screen does",

            "WEST_POKEOAM_DROP" or "WEST_POKEOAM_DROP_RESET" or "WEST_POKEOAM_RES_FREE" or "WEST_PT_DROP"
                or "WEST_PT_DROP_RESET" or "WEST_CATS_RES_FREE" or "WEST_EXIT_PARTICLE"
                or "WEST_POKE_OAM_ENABLE" => "What it puts back",

            "WEST_SEQEND" => "Where it ends",

            "WEST_WAIT" or "WEST_WAIT_FLAG" or "WEST_WAIT_PARTICLE" or "WEST_LOOP"
                or "WEST_LOOP_LABEL" => "How it is timed",

            // Branches choose between whole versions of the move, which is a different thing from waiting,
            // and burying them among the waits was what made a two-version move unreadable.
            "WEST_TURN_CHK" or "WEST_SIDE_JP" or "WEST_SEQ_JP" or "WEST_TENKI_JP" or "WEST_CONTEST_JP"
                or "WEST_PTAT_JP" or "WEST_SEQ_CALL" or "WEST_END_CALL" => "Which version plays",

            // These fill in the arguments the next command reads. They are setup, not timing.
            "WEST_WORK_SET" or "WEST_WORK_CLEAR" => "Settings for the next command",

            _ => "What happens",
        };

        /// <summary>
        /// Which part of the move a routine call belongs to, from who it acts on.
        ///
        /// A routine that takes a target flag says in that flag whether it is doing something to the
        /// attacker or to the Pokemon on the receiving end, so the call itself decides which group it
        /// goes in rather than a list written out by hand here.
        /// </summary>
        private static string GroupOfCall(int[] args)
        {
            if (args.Length < 1) return "What happens";
            var r = WestRoutines.Get(args[0]);
            if (r == null) return "What happens";

            for (int w = 0; w < r.Words.Length && w + 2 < args.Length; w++)
            {
                string meaning = r.Words[w];
                if (string.IsNullOrEmpty(meaning) || !meaning.Contains("target flag")) continue;
                int flag = args[w + 2];
                if ((flag & WestTargetFlags.Bg) != 0) return "What the screen does";
                if ((flag & WestTargetFlags.M1) != 0 && (flag & WestTargetFlags.E1) == 0) return "What the attacker does";
                return "What hits the target";
            }

            string sum = r.Summary ?? "";
            if (sum.Contains("background") || sum.Contains("screen") || sum.Contains("colour")) return "What the screen does";
            if (sum.Contains("attacker")) return "What the attacker does";
            return "What happens";
        }

        /// <summary>Every line for one view.</summary>
        public static List<WestLine> Build(IReadOnlyList<WazaSeqCommand> cmds, WazaSeqVersion version,
                                           WestViewMode mode, Func<int, string> soundName = null)
        {
            var lines = new List<WestLine>();
            if (cmds == null || cmds.Count == 0) return lines;

            if (mode == WestViewMode.Raw) { BuildRaw(cmds, version, lines); return lines; }

            var folds = WestMacros.Find(cmds, version);
            var foldAt = folds.ToDictionary(f => f.From);

            if (mode == WestViewMode.Guided) BuildGuided(cmds, version, folds, foldAt, lines, soundName);
            else BuildScript(cmds, version, foldAt, lines, soundName);
            return lines;
        }

        // ── word for word ───────────────────────────────────────────────────────────

        private static void BuildRaw(IReadOnlyList<WazaSeqCommand> cmds, WazaSeqVersion version, List<WestLine> lines)
        {
            for (int i = 0; i < cmds.Count; i++)
            {
                var c = cmds[i];
                string name = WestOpcodes.Name(version, c.OpId) ?? "?";
                var sb = new StringBuilder();
                sb.Append($"{c.WordPos,5}  {c.OpId,3}  {Short(name),-26}");
                foreach (int a in c.Args) sb.Append($" {a,11}");
                if (c.Args.Length > 0)
                {
                    sb.Append("   ");
                    foreach (int a in c.Args) sb.Append($" {a:X8}");
                }
                lines.Add(new WestLine
                {
                    Index = i,
                    Text = sb.ToString(),
                    Detail = DetailFor(name, c.Args, version),
                    Source = SourceFor(name, c.Args),
                });
            }
        }

        // ── one command a line, in columns ──────────────────────────────────────────

        private static void BuildScript(IReadOnlyList<WazaSeqCommand> cmds, WazaSeqVersion version,
                                        Dictionary<int, WestMacros.Folded> foldAt, List<WestLine> lines,
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
                string name = WestOpcodes.Name(version, c.OpId) ?? "?";
                bool closes = name is "WEST_LOOP" or "WEST_END_CALL";
                if (closes) depth = Math.Max(0, depth - 1);

                lines.Add(new WestLine
                {
                    Index = i,
                    Depth = depth,
                    Text = CommandText(name, c.Args, version, soundName),
                    Detail = DetailFor(name, c.Args, version),
                    Source = SourceFor(name, c.Args),
                });

                if (name is "WEST_LOOP_LABEL" or "WEST_SEQ_CALL") depth++;
                i++;
            }
        }

        // ── grouped by what part of the move it is ──────────────────────────────────

        private static void BuildGuided(IReadOnlyList<WazaSeqCommand> cmds, WazaSeqVersion version,
                                        List<WestMacros.Folded> folds, Dictionary<int, WestMacros.Folded> foldAt,
                                        List<WestLine> lines, Func<int, string> soundName)
        {
            var byGroup = new List<(string group, WestLine line)>();
            for (int i = 0; i < cmds.Count; )
            {
                if (foldAt.TryGetValue(i, out var fold))
                {
                    byGroup.Add(("What it loads", FoldLine(fold, 0)));
                    i += fold.Count;
                    continue;
                }
                var c = cmds[i];
                string name = WestOpcodes.Name(version, c.OpId) ?? "?";
                string group = name is "WEST_FUNC_CALL" or "WEST_OLDACT_FUNC_CALL"
                    ? GroupOfCall(c.Args) : GroupOf(name);
                byGroup.Add((group, new WestLine
                {
                    Index = i,
                    Text = CommandText(name, c.Args, version, soundName),
                    Detail = DetailFor(name, c.Args, version),
                    Source = SourceFor(name, c.Args),
                }));
                i++;
            }

            // A fixed order, so "Where it ends" is at the end rather than wherever the first SEQEND
            // happened to sit. A move that branches has a spare SEQEND early on, and ordering by first
            // appearance put the ending in the middle of the move.
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
                lines.Add(new WestLine { IsHeading = true, Text = g });
                foreach (var (group, line) in byGroup)
                    if (group == g) { line.Depth = 1; lines.Add(line); }
            }
        }

        // ── the text of one line ────────────────────────────────────────────────────

        private static WestLine FoldLine(WestMacros.Folded f, int depth)
        {
            var sb = new StringBuilder(Short(f.Macro.Name).PadRight(26));
            for (int s = 0; s < f.Settings.Length; s++)
            {
                string label = s < f.Macro.Settings.Length ? f.Macro.Settings[s] : "setting " + s;
                sb.Append($"  {label}={f.Settings[s]}");
            }
            return new WestLine
            {
                Index = f.From,
                Covers = f.Count,
                Depth = depth,
                Text = sb.ToString(),
                Detail = f.Macro.Summary + $" One line in the games' own scripts, {f.Count} commands in the ROM.",
                Source = "west.h",
            };
        }

        private static string CommandText(string opName, int[] args, WazaSeqVersion version, Func<int, string> soundName)
        {
            // A routine call names the routine and then just lists its values. Labelling each one here
            // would push the line off the side, and the panel below already says what every one means,
            // which is where an explanation belongs.
            if (opName is "WEST_FUNC_CALL" or "WEST_OLDACT_FUNC_CALL" && args.Length >= 2)
            {
                var call = new StringBuilder(Short(opName).PadRight(22));
                call.Append(RoutineName(args[0]).PadRight(24));
                for (int w = 2; w < args.Length; w++)
                {
                    string meaning = WestRoutines.WordMeaning(args[0], w - 2);
                    call.Append(meaning != null && meaning.Contains("target flag")
                        ? "  " + WestTargetFlags.Describe(args[w], brief: true)
                        : $" {args[w],7}");
                }
                return call.ToString();
            }

            var sb = new StringBuilder(Short(opName).PadRight(22));
            for (int i = 0; i < args.Length; i++)
            {
                string label = WestParamSchema.ParamName(opName, i) ?? ("arg " + i);
                string shown = Value(opName, i, args, version, soundName);
                // A setting switched off says nothing, and printing all of them made this the longest
                // line in either game. The raw view still shows every word, and the panel below spells
                // out the selected command in full.
                if (shown == "None") continue;
                sb.Append($"  {label}={shown}");
            }
            return sb.ToString();
        }

        /// <summary>A word with the name the games give it, where there is one.</summary>
        private static string Value(string opName, int i, int[] args, WazaSeqVersion version, Func<int, string> soundName)
        {
            int v = args[i];

            // A routine call names the routine, and its words carry the routine's own meanings.
            if (opName is "WEST_FUNC_CALL" or "WEST_OLDACT_FUNC_CALL")
            {
                if (i == 0) return RoutineName(v);
                if (i == 1) return v.ToString();
                if (args.Length > 0)
                {
                    string meaning = WestRoutines.WordMeaning(args[0], i - 2);
                    if (meaning != null && meaning.Contains("target flag")) return WestTargetFlags.Describe(v, brief: true);
                }
            }
            if (opName.StartsWith("WEST_SE", StringComparison.Ordinal) && i == 0 && soundName != null)
            {
                string n = soundName(v);
                if (!string.IsNullOrEmpty(n)) return n;
            }

            // Settings the games give names to, so the reader sees "End (target)" rather than 2.
            var options = WestParamSchema.EnumFor(opName, i);
            if (options != null)
            {
                foreach (var o in options)
                    if (o.Value == v) return o.Label;
            }
            return v.ToString();
        }

        private static string DetailFor(string opName, int[] args, WazaSeqVersion version)
        {
            if (opName is "WEST_FUNC_CALL" or "WEST_OLDACT_FUNC_CALL" && args.Length > 0)
            {
                var r = WestRoutines.Get(args[0]);
                if (r != null)
                {
                    var sb = new StringBuilder(r.Summary);
                    for (int w = 0; w + 2 < args.Length; w++)
                    {
                        string m = WestRoutines.WordMeaning(args[0], w);
                        if (m == null) continue;
                        // A target flag is a bit set, so the number on its own says nothing. Spell it out
                        // here in full, including the part the columnar views leave off.
                        string shown = m.Contains("target flag")
                            ? args[w + 2] + " = " + WestTargetFlags.Describe(args[w + 2])
                            : args[w + 2].ToString();
                        sb.Append($"\n  {shown}: {m}");
                    }
                    return sb.ToString();
                }
            }
            return WestParamSchema.OpcodeDoc(opName) ?? "";
        }

        private static string SourceFor(string opName, int[] args)
            => (opName is "WEST_FUNC_CALL" or "WEST_OLDACT_FUNC_CALL") && args.Length > 0
               ? WestRoutines.Get(args[0])?.Source ?? "" : "";

        /// <summary>
        /// What to call a routine. The games' own name unless somebody has renamed it, in which case
        /// theirs, so a rename made in the raw view shows up in the other two straight away.
        /// </summary>
        public static string RoutineName(int id)
        {
            string custom = LabelStore.GetLabel("west_routines", id);
            if (!string.IsNullOrWhiteSpace(custom)) return custom;
            return WestRoutines.Get(id)?.Name ?? id.ToString();
        }

        /// <summary>The opcode name without the prefix every one of them carries.</summary>
        public static string Short(string opName)
            => opName != null && opName.StartsWith("WEST_", StringComparison.Ordinal) ? opName.Substring(5) : opName ?? "";
    }
}
