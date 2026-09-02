using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>Folding a script into the shorthands it was written in, and back again.</summary>
    [Collection("rom")]
    public class WestMacroFoldTests
    {
        private readonly ITestOutputHelper _out;
        public WestMacroFoldTests(ITestOutputHelper o) { _out = o; }

        private const string HeartGold = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";
        private const string Platinum =
            @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents";

        private static string ScriptDir(string project, string gameCode)
        {
            if (!Directory.Exists(project)) return null;
            try { new RomInfo(gameCode, project); } catch { return null; }
            var narc = new ScriptNarc(DirNames.wazaEffectScripts);
            return narc.Available ? gameDirs[DirNames.wazaEffectScripts].unpackedDir : null;
        }

        [Fact]
        public void FoldingAndUnfoldingEveryHeartGoldScriptGivesBackTheSameCommands()
            => Sweep(HeartGold, "IPKE", WazaSeqVersion.HGSS);

        [Fact]
        public void FoldingAndUnfoldingEveryPlatinumScriptGivesBackTheSameCommands()
            => Sweep(Platinum, "CPUE", WazaSeqVersion.Plat);

        private void Sweep(string project, string gameCode, WazaSeqVersion version)
        {
            string dir = ScriptDir(project, gameCode);
            Assert.True(dir != null, gameCode + ": the move-effect archive could not be unpacked, so nothing was checked");

            var problems = new List<string>();
            int scripts = 0, commands = 0, afterFolding = 0, folds = 0;
            var perMacro = new SortedDictionary<string, int>();
            var lengths = new List<int>();

            foreach (var f in RomFiles.Settled(dir))
            {
                var bytes = File.ReadAllBytes(f);
                if (bytes.Length == 0) continue;
                string name = Path.GetFileName(f);

                var cmds = WestScript.Parse(bytes, version);
                if (cmds.Count == 0) continue;
                scripts++;
                commands += cmds.Count;

                var found = WestMacros.Find(cmds, version);
                folds += found.Count;
                foreach (var fo in found)
                    perMacro[fo.Macro.Name] = perMacro.TryGetValue(fo.Macro.Name, out int n) ? n + 1 : 1;

                // What the reader would see: each fold as one line, everything else as it was.
                int shown = cmds.Count - found.Sum(x => x.Count - 1);
                afterFolding += shown;
                lengths.Add(shown);

                // Put it back and require the same commands, in the same order, with the same words.
                var rebuilt = new List<WazaSeqCommand>();
                int at = 0;
                foreach (var fo in found)
                {
                    for (; at < fo.From; at++) rebuilt.Add(cmds[at]);
                    var back = WestMacros.Unfold(fo.Macro, fo.Settings, version);
                    Assert.True(back != null, $"{name}: {fo.Macro.Name} could not be put back");
                    rebuilt.AddRange(back);
                    at = fo.From + fo.Count;
                }
                for (; at < cmds.Count; at++) rebuilt.Add(cmds[at]);

                if (rebuilt.Count != cmds.Count) { problems.Add($"{name}: {cmds.Count} commands became {rebuilt.Count}"); continue; }
                for (int i = 0; i < cmds.Count; i++)
                {
                    if (rebuilt[i].OpId != cmds[i].OpId || !rebuilt[i].Args.SequenceEqual(cmds[i].Args))
                    {
                        problems.Add($"{name}: command {i} came back different");
                        break;
                    }
                }

                // And the bytes themselves, which is what actually gets written to the ROM.
                var again = WestScript.Serialize(rebuilt);
                if (!again.SequenceEqual(bytes)) problems.Add($"{name}: the bytes came back different");
            }

            lengths.Sort();
            int median = lengths.Count > 0 ? lengths[lengths.Count / 2] : 0;
            _out.WriteLine($"{gameCode}: {scripts} scripts, {commands} commands, {folds} shorthands found");
            _out.WriteLine($"  shown after folding: {afterFolding} ({100.0 * (commands - afterFolding) / commands:F1}% fewer), median per script {median}");
            foreach (var kv in perMacro) _out.WriteLine($"  {kv.Key}: {kv.Value}");

            Assert.True(scripts >= 500, $"only {scripts} scripts were read");
            Assert.True(problems.Count == 0,
                $"{problems.Count} scripts did not survive folding:\n" + string.Join("\n", problems.Take(20)));

            // The whole point was fewer things on screen. If this ever stops being true the folding has
            // silently stopped matching.
            Assert.True(afterFolding < commands * 0.66,
                $"folding only removed {100.0 * (commands - afterFolding) / commands:F1}% of the commands");
        }
    }
}
