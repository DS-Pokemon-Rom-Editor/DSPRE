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
                var cmds = BattleAnimScript.Parse(bytes, version);
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
    }
}
