using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>A u16 move list in hg-engine's battle source that the battle code walks with NELEMS or to an end marker.</summary>
    public static class HgEngineMoveList
    {
        /// <param name="End">The value that ends the list, for one walked to a marker instead of by count.</param>
        /// <param name="Start">The value the list starts after, for the second part of a table with two.</param>
        public sealed record Info(string Label, string Tip, string File, string Symbol, string End = null, string Start = null);

        private const string OtherCalcs = "src/battle/other_battle_calculators.c";

        public static readonly Info Punching = new("Punching (Iron Fist)", null, OtherCalcs, "PunchingMoveTable");
        public static readonly Info Sound = new("Sound (Soundproof)", null, "src/battle/battle_pokemon.c", "SoundBasedMoveList");

        /// <summary>hg-engine's own lists, beyond the two the games have.</summary>
        public static readonly Info[] Extra =
        {
            new("Triage", "Triage gives these priority", OtherCalcs, "TriageMovesList"),
            new("Ball and bomb", "Bulletproof blocks these", OtherCalcs, "BallAndBombMoveList"),
            new("Wind", "Wind Rider and Wind Power", OtherCalcs, "WindMoveTable"),
            new("Slicing", "Sharpness boosts these", OtherCalcs, "SlicingMoveTable"),
            new("Multi-hit", null, OtherCalcs, "MultiHitMovesList"),
            new("One hit under Parental Bond", null, OtherCalcs, "ParentalBondSingleStrikeMovesList"),
            new("Z move", null, OtherCalcs, "ZMoveList"),
            new("Max move", null, OtherCalcs, "MaxMoveList"),
            new("Blocked by Heal Block", null, OtherCalcs, "HealBlockUnusableMoves"),
            // Mimic stops at 0xFFFE and Metronome reads on to 0xFFFF.
            new("Skipped by Metronome and Mimic", null, OtherCalcs, "sMetronomeMimicMoveBanList", "0xFFFE"),
            new("Skipped by Metronome", null, OtherCalcs, "sMetronomeMimicMoveBanList", "0xFFFF", "0xFFFE"),
            new("Skipped by Sleep Talk", null, "src/battle/battle_script_commands.c", "SleepTalkBanList"),
        };

        private const string MovesHeader = "include/constants/moves.h";

        private static string PathOf(Info info) => System.IO.Path.Combine(HgEngineProject.RepoPathUnc, info.File.Replace('/', System.IO.Path.DirectorySeparatorChar));

        private static CDeclaration Table(Info info, string text) => CSourceFile.For(text).Find(info.Symbol);

        /// <summary>Why the list can't be edited in this checkout, or null.</summary>
        public static string WhyNot(Info info)
        {
            if (!HgEngineProject.IsActive) return "No hg-engine checkout is linked.";
            string path = PathOf(info);
            if (!File.Exists(path) || Table(info, HgEngineFileCache.GetText(path))?.Init == null) return $"{info.File} has no {info.Symbol}.";
            return null;
        }

        // This list's rows run from after Start (when set) up to End (when set), which sits at To.
        private static (int From, int To) Bounds(Info info, string text, CInitList init)
        {
            List<CInitItem> items = init.Items;
            int from = 0;
            if (info.Start != null)
            {
                int start = items.FindIndex(i => i.ValueText(text).Trim() == info.Start);
                if (start < 0) throw new InvalidDataException($"{info.Symbol} has no {info.Start}.");
                from = start + 1;
            }
            int to = items.Count;
            if (info.End != null)
            {
                int end = items.FindIndex(from, i => i.ValueText(text).Trim() == info.End);
                if (end < 0) throw new InvalidDataException($"{info.Symbol} has no {info.End}.");
                to = end;
            }
            return (from, to);
        }

        private static List<CInitItem> Section(Info info, string text, CInitList init)
        {
            (int from, int to) = Bounds(info, text, init);
            return init.Items.GetRange(from, to - from);
        }

        public static List<ushort> Load(Info info)
        {
            string path = PathOf(info);
            List<ushort> listed = Read(info, HgEngineFileCache.GetText(path), HgEngineSymbolTable.Load(MovesHeader));
            HgEngineLoadedEntries.Store(path, LoadedKey(info), ListText(listed));
            return listed;
        }

        private static List<ushort> Read(Info info, string text, HgEngineSymbolTable moves)
        {
            List<ushort> listed = new List<ushort>();
            foreach (CInitItem item in Section(info, text, Table(info, text).Init))
                listed.Add((ushort)MoveOf(info, item, text, moves));
            return listed;
        }

        private static int MoveOf(Info info, CInitItem item, string text, HgEngineSymbolTable moves)
        {
            string name = item.ValueText(text).Trim();
            if (moves == null || !moves.TryGetValue(name, out int id))
                throw new InvalidDataException($"{info.Symbol} lists {name}, which moves.h doesn't name.");
            return id;
        }

        // Saved whole, so the save checks the file still holds what the editor loaded. Two lists can share a table.
        private static string LoadedKey(Info info) => $"@{info.Symbol}:{info.Start}:{info.End}";
        private static string ListText(IEnumerable<ushort> moves) => string.Join(",", moves);

        /// <summary>Writes the list. Rows that stay keep their place and comments, and an end marker stays last.</summary>
        public static void Save(Info info, IReadOnlyList<ushort> wanted)
        {
            HgEngineSymbolTable moves = HgEngineSymbolTable.Load(MovesHeader);
            string path = PathOf(info), text = HgEngineFileCache.GetText(path), nl = text.Contains("\r\n") ? "\r\n" : "\n";
            if (HgEngineLoadedEntries.ChangedOutside(path, LoadedKey(info), ListText(Read(info, text, moves))))
                throw new InvalidOperationException(HgEngineLoadedEntries.ConflictMessage(Path.GetFileName(info.File), new[] { info.Symbol }));
            CDeclaration table = Table(info, text);
            string updated = text;

            List<int> listed = new List<int>();
            foreach (CInitItem item in Section(info, text, table.Init).AsEnumerable().Reverse())
            {
                int move = MoveOf(info, item, text, moves);
                listed.Add(move);
                if (wanted.Contains((ushort)move)) continue;
                updated = HgEngineSourcePatcher.RemoveElement(updated, item.Start, item.End);
            }
            foreach (ushort move in wanted.Where(m => !listed.Contains(m)))
            {
                if (!moves.TryGetNameWithPrefix(move, "MOVE_", out string name)) throw new InvalidOperationException($"moves.h has no MOVE_ name for move {move}.");
                CInitList now = CSourceFile.For(updated).Find(info.Symbol).Init;
                CInitItem end = info.End == null ? null : now.Items[Bounds(info, updated, now).To];
                string indent = now.Items.Count > 0 ? HgEngineSourcePatcher.IndentOfLine(updated, now.Items[^1].Start) : "    ";
                if (end != null)
                    updated = updated.Insert(HgEngineSourcePatcher.LineStart(updated, end.Start), $"{indent}{name},{nl}");
                else
                {
                    int after = now.Items.Count > 0 ? now.Items[^1].End : now.Open + 1;
                    updated = updated.Insert(after, (now.Items.Count > 0 ? "," : "") + $"{nl}{indent}{name}" + (now.Items.Count > 0 ? "" : ","));
                }
            }
            int count = CSourceFile.For(updated).Find(info.Symbol).Init.Items.Count;
            if (table.Dimensions.Count == 1 && table.Dimensions[0] != "[]")
            {
                int size = updated.LastIndexOf(table.Dimensions[0], table.Init.Open, StringComparison.Ordinal);
                updated = updated.Substring(0, size) + $"[{count}]" + updated.Substring(size + table.Dimensions[0].Length);
            }
            // battle.h declares a fixed-size list too, and the two have to agree or the build fails, so both are
            // checked before either is written.
            string header = System.IO.Path.Combine(HgEngineProject.RepoPathUnc, "include", "battle.h"), newHeader = null;
            bool sized = table.Dimensions.Count == 1 && table.Dimensions[0] != "[]";
            if (sized && File.Exists(header))
            {
                string declared = HgEngineFileCache.GetText(header), was = info.Symbol + table.Dimensions[0];
                if (!declared.Contains(was, StringComparison.Ordinal))
                    throw new InvalidOperationException($"include/battle.h doesn't declare {was} the way DSPRE reads, so the list size can't be kept in step.");
                newHeader = declared.Replace(was, $"{info.Symbol}[{count}]", StringComparison.Ordinal);
            }
            string Check(string written) => Read(info, written, moves).OrderBy(m => m).SequenceEqual(wanted.OrderBy(m => m)) ? null : $"{info.Symbol} differs";
            string HeaderCheck(string written) => written.Contains($"{info.Symbol}[{count}]", StringComparison.Ordinal) ? null : "the list size differs";
            string problem = Check(updated) ?? (newHeader != null ? HeaderCheck(newHeader) : null);
            if (problem != null) throw new InvalidOperationException($"{info.File} wouldn't read back as saved ({problem}), so nothing was written.");
            if (!HgEngineVerifiedWrite.TryWrite(path, info.File, updated, Check, out string error)) throw new InvalidOperationException(error);
            if (newHeader != null && !HgEngineVerifiedWrite.TryWrite(header, "include/battle.h", newHeader, HeaderCheck, out error))
                throw new InvalidOperationException(error);
            HgEngineLoadedEntries.Remember(path, LoadedKey(info), ListText(Read(info, updated, moves)));
        }
    }
}
