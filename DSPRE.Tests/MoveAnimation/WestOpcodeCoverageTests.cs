using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DSPRE;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>No command either game actually uses may be quietly skipped.</summary>
    [Collection("rom")]
    public class WestOpcodeCoverageTests
    {
        private readonly ITestOutputHelper _out;
        public WestOpcodeCoverageTests(ITestOutputHelper o) { _out = o; }

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

        /// <summary>Every opcode name the player has a case for, read out of its own source.</summary>
        private static HashSet<string> HandledByThePlayer()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "DS_Map.sln"))) d = d.Parent;
            Assert.True(d != null, "could not find the repository, so nothing was checked");

            string src = File.ReadAllText(Path.Combine(d.FullName, "DSPRE.Avalonia", "Avalonia", "WestPlayer.cs"));
            var set = new HashSet<string>(Regex.Matches(src, @"case\s+""(WEST_[A-Z0-9_]+)""")
                                                .Select(m => m.Groups[1].Value));
            Assert.True(set.Count > 30, $"only {set.Count} cases were found, so the search itself is wrong");
            return set;
        }

        [Fact]
        public void NothingEitherGameUsesIsQuietlySkipped()
        {
            var handled = HandledByThePlayer();
            var missing = new SortedDictionary<string, int>();
            int checkedScripts = 0;
            var seen = new SortedSet<string>();

            foreach (var (project, code, version) in new[]
                     {
                         (HeartGold, "IPKE", WazaSeqVersion.HGSS),
                         (Platinum, "CPUE", WazaSeqVersion.Plat),
                     })
            {
                string dir = ScriptDir(project, code);
                Assert.True(dir != null, code + ": the move-effect archive could not be unpacked, so nothing was checked");

                foreach (var f in RomFiles.Settled(dir))
                {
                    var bytes = File.ReadAllBytes(f);
                    if (bytes.Length == 0) continue;
                    checkedScripts++;
                    foreach (var c in WestScript.Parse(bytes, version))
                    {
                        string name = WestOpcodes.Name(version, c.OpId);
                        if (name == null) continue;
                        seen.Add(name);
                        if (!handled.Contains(name))
                            missing[name] = missing.TryGetValue(name, out int n) ? n + 1 : 1;
                    }
                }
            }

            _out.WriteLine($"{checkedScripts} scripts across both games, {seen.Count} distinct commands, {handled.Count} handled by the player");
            foreach (var m in missing) _out.WriteLine($"  no case: {m.Key} ({m.Value} times)");

            Assert.True(checkedScripts >= 1000, $"only {checkedScripts} scripts were read");
            Assert.True(missing.Count == 0,
                $"{missing.Count} commands the games use have no case and are skipped without a word: "
                + string.Join(", ", missing.Select(m => $"{m.Key} x{m.Value}")));
        }
    }
}
