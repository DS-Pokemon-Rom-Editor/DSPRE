using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using static DSPRE.RomInfo;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>Every move-effect script in a real ROM, decoded with our own opcode table.</summary>
    [Collection("rom")]
    public class WestCorpusSweepTests
    {
        private readonly ITestOutputHelper _out;
        public WestCorpusSweepTests(ITestOutputHelper o) { _out = o; }

        private const string HeartGold = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";
        private const string Platinum =
            @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents";

        private static string ScriptDir(string project, string gameCode)
        {
            if (!Directory.Exists(project)) return null;
            try { new RomInfo(gameCode, project); } catch { return null; }
            // The archive is unpacked on demand the same way the editor does it, so this does not depend
            // on somebody having opened the move editor in this project first.
            var narc = new ScriptNarc(DirNames.wazaEffectScripts);
            return narc.Available ? gameDirs[DirNames.wazaEffectScripts].unpackedDir : null;
        }

        /// <summary>The opcode west.h lists but neither game has.</summary>
        [Fact]
        public void TheOpcodeNeitherGameActuallyHasIsNotCounted()
        {
            Assert.Equal(85, WestOpcodes.Count(WazaSeqVersion.Plat));
            Assert.Equal(88, WestOpcodes.Count(WazaSeqVersion.HGSS));
            for (int i = 0; i < WestOpcodes.Count(WazaSeqVersion.HGSS); i++)
                Assert.NotEqual("WEST_POKEOAM_CHECK", WestOpcodes.Name(WazaSeqVersion.HGSS, i));

            // The four that were shifted, at the ids the games really use.
            Assert.Equal("WEST_KEY_WAIT", WestOpcodes.Name(WazaSeqVersion.HGSS, 84));
            Assert.Equal("WEST_FLASH", WestOpcodes.Name(WazaSeqVersion.HGSS, 85));
            Assert.Equal("WEST_HAIKEI_CHG_EX", WestOpcodes.Name(WazaSeqVersion.HGSS, 86));
            Assert.Equal("WEST_BATONTATTI_JP", WestOpcodes.Name(WazaSeqVersion.HGSS, 87));
            Assert.Equal("WEST_KEY_WAIT", WestOpcodes.Name(WazaSeqVersion.Plat, 84));
        }

        [Fact]
        public void EveryHeartGoldScriptDecodesRightThroughToItsEnd()
            => Sweep(HeartGold, "IPKE", WazaSeqVersion.HGSS, 501);

        [Fact]
        public void EveryPlatinumScriptDecodesRightThroughToItsEnd()
            => Sweep(Platinum, "CPUE", WazaSeqVersion.Plat, 400);

        private void Sweep(string project, string gameCode, WazaSeqVersion version, int expected)
        {
            string dir = ScriptDir(project, gameCode);
            Assert.True(dir != null, gameCode + ": the move-effect archive could not be unpacked, so nothing was checked");

            var files = RomFiles.Settled(dir).OrderBy(f => f).ToList();
            Assert.True(files.Count >= expected, $"only {files.Count} scripts were there, expected {expected}");

            var problems = new List<string>();
            int checkedFiles = 0, totalCommands = 0;
            var used = new SortedDictionary<string, int>();

            foreach (var f in files)
            {
                var bytes = File.ReadAllBytes(f);
                if (bytes.Length == 0) continue;
                checkedFiles++;

                var cmds = WestScript.Parse(bytes, version);
                totalCommands += cmds.Count;
                foreach (var c in cmds)
                {
                    string n = WestOpcodes.Name(version, c.OpId);
                    used[n] = used.TryGetValue(n, out int k) ? k + 1 : 1;
                }

                string name = Path.GetFileName(f);
                if (cmds.Count == 0) { problems.Add($"{name}: nothing decoded from {bytes.Length} bytes"); continue; }

                // Reading in step means the words run out exactly as the last command ends.
                var last = cmds[cmds.Count - 1];
                int consumed = last.WordPos + 1 + last.Args.Length;
                if (consumed != bytes.Length / 4)
                    problems.Add($"{name}: stopped after {consumed} of {bytes.Length / 4} words");

                // And every script says where it ends.
                if (!cmds.Any(c => WestOpcodes.Name(version, c.OpId) == "WEST_SEQEND"))
                    problems.Add($"{name}: no SEQEND anywhere in {cmds.Count} commands");
            }

            _out.WriteLine($"{gameCode}: {checkedFiles} scripts, {totalCommands} commands, {used.Count} distinct opcodes");
            foreach (var kv in used.OrderByDescending(k => k.Value))
                _out.WriteLine($"  {kv.Key,-32} {kv.Value}");

            Assert.True(checkedFiles >= expected, $"only {checkedFiles} scripts had any content");
            Assert.True(problems.Count == 0,
                $"{problems.Count} of {checkedFiles} scripts did not read cleanly:\n" +
                string.Join("\n", problems.Take(30)));
        }
    }
}
