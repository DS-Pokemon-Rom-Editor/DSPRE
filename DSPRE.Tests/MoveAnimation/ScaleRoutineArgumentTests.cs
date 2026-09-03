using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// The stretch routine divides by a number the move gives it, and the preview divides by 100 instead.
    /// </summary>
    [Collection("rom")]
    public class ScaleRoutineArgumentTests
    {
        private readonly ITestOutputHelper _out;
        public ScaleRoutineArgumentTests(ITestOutputHelper o) { _out = o; }

        private static readonly string HeartGold = TestRoms.HeartGold;
        private static readonly string Platinum = TestRoms.Platinum;

        // The stretch routine (WestSp_WE_SSPPokeScaleUpDown, routine 42) takes its denominator in gp work 5,
        // which is argument 7 of the call once the routine id and the word count are counted.
        private const int SspPokeScale = 42, DenominatorArg = 7;

        [Theory]
        [InlineData("CPUE", WazaSeqVersion.Plat)]
        [InlineData("IPKE", WazaSeqVersion.HGSS)]
        public void TheStretchRoutineAlwaysAsksForTheSameDenominator(string code, WazaSeqVersion version)
        {
            string project = code == "CPUE" ? Platinum : HeartGold;
            Assert.True(Directory.Exists(project), $"{code}: no unpacked project, so nothing was checked");
            new RomInfo(code, project);

            var files = RomFiles.Settled(gameDirs[DirNames.wazaEffectScripts].unpackedDir);
            Assert.True(files.Length > 400, $"{code}: only {files.Length} scripts, so this proves nothing");

            var seen = new SortedDictionary<int, List<int>>();   // denominator -> moves using it
            int calls = 0;
            for (int move = 0; move < files.Length; move++)
            {
                var bytes = File.ReadAllBytes(files[move]);
                if (bytes.Length == 0) continue;
                foreach (var c in WestScript.Parse(bytes, version))
                {
                    if (WestOpcodes.Name(version, c.OpId) != "WEST_FUNC_CALL") continue;
                    if (c.Args.Length < 1 || c.Args[0] != SspPokeScale) continue;
                    calls++;
                    if (c.Args.Length <= DenominatorArg) continue;
                    int d = c.Args[DenominatorArg];
                    if (!seen.TryGetValue(d, out var l)) seen[d] = l = new List<int>();
                    if (!l.Contains(move)) l.Add(move);
                }
            }

            _out.WriteLine($"{code}: {files.Length} scripts read, {calls} calls to the stretch routine");
            foreach (var kv in seen)
                _out.WriteLine($"  denominator {kv.Key}: {kv.Value.Count} moves, first is {kv.Value[0]}");

            // Which moves use each turning or stretching routine, so one of each can be recorded.
            var users = new SortedDictionary<int, List<int>>();
            for (int move = 0; move < files.Length; move++)
            {
                var bytes = File.ReadAllBytes(files[move]);
                if (bytes.Length == 0) continue;
                foreach (var c in WestScript.Parse(bytes, version))
                {
                    if (WestOpcodes.Name(version, c.OpId) != "WEST_FUNC_CALL" || c.Args.Length < 1) continue;
                    int r = c.Args[0];
                    if (r != 4 && r != 35 && r != 42 && r != 60) continue;
                    if (!users.TryGetValue(r, out var l)) users[r] = l = new List<int>();
                    if (!l.Contains(move)) l.Add(move);
                }
            }
            var names = RomInfo.GetAttackNames() ?? Array.Empty<string>();
            string Nm(int m) => m < names.Length && !string.IsNullOrWhiteSpace(names[m]) ? $"{m} {names[m]}" : m.ToString();
            foreach (var kv in users)
                _out.WriteLine($"  routine {kv.Key}: {kv.Value.Count} moves, e.g. "
                               + string.Join(", ", kv.Value.Take(6).Select(Nm)));

            var others = seen.Keys.Where(k => k != 100).ToList();
            Assert.True(others.Count == 0,
                $"{code}: the stretch routine is asked for a denominator other than 100 by some moves "
                + $"({string.Join(", ", others)}), and the preview divides by 100 for all of them");
        }
    }
}
