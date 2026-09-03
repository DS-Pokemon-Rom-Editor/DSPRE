using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// The moves chosen for recording really do cover everything a move animation can do.
    /// </summary>
    [Collection("rom")]
    public class MoveCoverageTests
    {
        private readonly ITestOutputHelper _out;
        public MoveCoverageTests(ITestOutputHelper o) { _out = o; }

        private static readonly string HeartGold = TestRoms.HeartGold;
        private static readonly string Platinum = TestRoms.Platinum;

        private sealed class Census
        {
            public string Game;
            public Dictionary<string, List<int>> Moves = new(StringComparer.Ordinal);   // mechanism -> moves
            public Dictionary<int, int> Length = new();                                  // move -> commands
            public string[] Names = Array.Empty<string>();
        }

        private static Census Build(string project, string gameCode, WazaSeqVersion version)
        {
            if (!Directory.Exists(project)) return null;
            try { new RomInfo(gameCode, project); } catch { return null; }
            var narc = new ScriptNarc(DirNames.wazaEffectScripts);
            if (!narc.Available) return null;

            var c = new Census { Game = gameCode };
            try { c.Names = RomInfo.GetAttackNames() ?? Array.Empty<string>(); } catch { }

            foreach (var f in RomFiles.Settled(gameDirs[DirNames.wazaEffectScripts].unpackedDir))
            {
                var bytes = File.ReadAllBytes(f);
                if (bytes.Length == 0) continue;
                if (!int.TryParse(Path.GetFileNameWithoutExtension(f), out int id)) continue;
                var cmds = WestScript.Parse(bytes, version);
                if (cmds.Count == 0) continue;
                int pos = 0; foreach (var x in cmds) { x.WordPos = pos; pos += 1 + x.Args.Length; }
                c.Length[id] = cmds.Count;
                foreach (var m in MoveMechanisms.Of(cmds, version))
                {
                    if (!c.Moves.TryGetValue(m, out var l)) c.Moves[m] = l = new List<int>();
                    l.Add(id);
                }
            }
            return c;
        }

        private static List<Census> BuildBoth()
        {
            var list = new List<Census>();
            var hg = Build(HeartGold, "IPKE", WazaSeqVersion.HGSS);
            var pl = Build(Platinum, "CPUE", WazaSeqVersion.Plat);
            if (hg != null) list.Add(hg);
            if (pl != null) list.Add(pl);
            return list;
        }

        [Fact]
        public void TheChosenMovesCoverEveryMechanismInBothGames()
        {
            var games = BuildBoth();
            Assert.True(games.Count == 2, "both projects are needed and one could not be opened, so nothing was checked");

            var chosen = new HashSet<int>(MoveTestSet.InOrder());
            int pairs = 0;
            var missing = new List<string>();

            foreach (var g in games)
            {
                Assert.True(g.Length.Count >= 500, $"{g.Game}: only {g.Length.Count} scripts were read");
                foreach (var kv in g.Moves)
                {
                    pairs++;
                    if (!kv.Value.Any(chosen.Contains))
                        missing.Add($"{g.Game} {kv.Key} (used by {kv.Value.Count} moves, e.g. {kv.Value[0]})");
                }
            }

            _out.WriteLine($"{games.Count} games, {games.Sum(g => g.Length.Count)} scripts, "
                           + $"{pairs} game-and-mechanism pairs, {chosen.Count} moves chosen");
            _out.WriteLine($"  {MoveTestSet.OpcodeCover.Length} of them cover every opcode and drawing path");

            Assert.True(pairs > 350, $"only {pairs} pairs were found, so the census itself is wrong");
            Assert.True(missing.Count == 0,
                $"{missing.Count} mechanisms have no move in the chosen set:\n" + string.Join("\n", missing.Take(15)));
        }

        [Fact]
        public void TheFirstSeventeenCoverEveryOpcodeAndDrawingPath()
        {
            var games = BuildBoth();
            Assert.True(games.Count == 2, "both projects are needed and one could not be opened, so nothing was checked");

            var front = new HashSet<int>(MoveTestSet.OpcodeCover);
            var missing = new List<string>();
            int checkedPairs = 0;

            foreach (var g in games)
                foreach (var kv in g.Moves)
                {
                    // Only the mechanisms the front of the list is meant to cover.
                    if (kv.Key.StartsWith("routine:", StringComparison.Ordinal)
                        || kv.Key.StartsWith("setting:", StringComparison.Ordinal)) continue;
                    checkedPairs++;
                    if (!kv.Value.Any(front.Contains)) missing.Add($"{g.Game} {kv.Key}");
                }

            _out.WriteLine($"{checkedPairs} opcode and drawing-path pairs; the first "
                           + $"{MoveTestSet.OpcodeCover.Length} moves miss {missing.Count}");
            Assert.True(checkedPairs > 100, $"only {checkedPairs} pairs were checked, so this proves little");
            Assert.True(missing.Count == 0, "the opening set misses: " + string.Join(", ", missing.Take(10)));
        }

        private static string Document(List<Census> games)
        {
            var order = MoveTestSet.InOrder();
            var all = new SortedSet<string>(games.SelectMany(g => g.Moves.Keys), StringComparer.Ordinal);
            var names = games[0].Names;

            string Name(int id) => id >= 0 && id < names.Length && !string.IsNullOrWhiteSpace(names[id])
                                   ? $"{id} {names[id]}" : id.ToString();

            var sb = new StringBuilder();
            sb.Append("[Research](../../ResearchNotes.md) / [Move Research](../MoveResearch.md) / Move Animation Test Coverage\n\n");
            sb.Append("# Which moves to record, and what each one is for\n\n");
            sb.Append("Generated from both ROMs by `MoveCoverageTests`. Do not edit by hand.\n\n");
            sb.Append("Comparing DSPRE's animation preview against the real game means recording moves, and ")
              .Append("recording every move is not practical. These are the moves that between them exercise ")
              .Append("everything a move animation can do.\n\n");
            sb.Append("A mechanism is counted once per game, because most scripts differ between HeartGold ")
              .Append("and Platinum, so covering one says nothing about the other.\n\n");

            foreach (var g in games)
                sb.Append($"- **{g.Game}**: {g.Length.Count} scripts, {g.Moves.Count} distinct mechanisms\n");
            int pairs = games.Sum(g => g.Moves.Count);
            sb.Append($"- **Together**: {all.Count} distinct mechanisms, {pairs} game-and-mechanism pairs, ")
              .Append($"covered by {order.Length} moves\n\n");

            sb.Append("## The order to record them in\n\n");
            sb.Append($"The first {MoveTestSet.OpcodeCover.Length} cover every opcode and every drawing path ")
              .Append("between them, so an error affecting many moves at once shows up early. The rest fill in ")
              .Append("the operator settings and the routines only one or two moves ever call.\n\n");
            sb.Append("| # | move | first covers |\n|---:|---|---|\n");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < order.Length; i++)
            {
                int mv = order[i];
                var firsts = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var g in games)
                    foreach (var kv in g.Moves)
                        if (kv.Value.Contains(mv) && seen.Add(g.Game + "|" + kv.Key)) firsts.Add(kv.Key);

                string what = firsts.Count == 0 ? "nothing new"
                            : string.Join(", ", firsts.Take(4)) + (firsts.Count > 4 ? $" and {firsts.Count - 4} more" : "");
                sb.Append($"| {i + 1} | {Name(mv)} | {what} |\n");
            }

            sb.Append("\n## What these moves do not cover\n\n");
            sb.Append("Every mechanism the sweep can see is covered, so what is listed here is what the "
                    + "sweep cannot see or the recordings cannot reach.\n\n");
            sb.Append("- A move has to actually happen in a staged battle to be recorded. Whirlwind has "
                    + "nothing to force out and Baton Pass has nobody to pass to when the other side holds "
                    + "one Pokemon, so both need a second one on the other side.\n");
            sb.Append("- Moves are counted by what their script asks for. A routine that behaves "
                    + "differently depending on the Pokemon, the damage or the weather is counted once, so "
                    + "covering it proves the routine runs, not that it runs right in every case.\n");
            sb.Append("- The second half of a move that has two animations is reached only by the turn "
                    + "check. Five of the chosen moves have one; the other moves with a turn check in the "
                    + "two games are not in this set.\n");
            sb.Append("- Only these two games are swept. Diamond and Pearl share the format but are not "
                    + "read here.\n");

            sb.Append("\n## Every mechanism, and how many moves use it\n\n");
            sb.Append("| mechanism | HeartGold | Platinum |\n|---|---:|---:|\n");
            foreach (var m in all)
            {
                string Cell(Census g) => g.Moves.TryGetValue(m, out var l) ? l.Count.ToString() : "-";
                sb.Append($"| {m} | {Cell(games[0])} | {Cell(games[1])} |\n");
            }
            return sb.ToString();
        }
    }
}
