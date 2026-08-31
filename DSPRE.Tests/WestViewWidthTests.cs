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
    /// <summary>
    /// How wide the lines get, over every script in both games.
    ///
    /// Clipped text was found by looking at five moves on screen, which cannot say whether the rest of
    /// the game is any better. This measures every line the two readable views produce, so the answer
    /// comes from all 1,002 scripts rather than the handful that were photographed. The budget is what
    /// the command pane actually shows at the editor's default size: about 840 pixels of 12-point
    /// Consolas, near enough 116 characters.
    ///
    /// Lines longer than that are not lost, since the pane scrolls sideways and the splitter widens it,
    /// but they cannot be read at a glance, which is what the readable views are for. So this does not
    /// demand zero; it holds the share down to what genuinely long commands account for, and fails if it
    /// creeps back up.
    /// </summary>
    [Collection("rom")]
    public class WestViewWidthTests
    {
        private readonly ITestOutputHelper _out;
        public WestViewWidthTests(ITestOutputHelper o) { _out = o; }

        private const int VisibleChars = 116;

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
        public void HeartGoldsReadableViewsMostlyFitThePane() => Sweep(HeartGold, "IPKE", WazaSeqVersion.HGSS);

        [Fact]
        public void PlatinumsReadableViewsMostlyFitThePane() => Sweep(Platinum, "CPUE", WazaSeqVersion.Plat);

        private void Sweep(string project, string gameCode, WazaSeqVersion version)
        {
            string dir = ScriptDir(project, gameCode);
            Assert.True(dir != null, gameCode + ": the move-effect archive could not be unpacked, so nothing was checked");

            int scripts = 0;
            foreach (var mode in new[] { WestViewMode.Guided, WestViewMode.Script })
            {
                int lines = 0, over = 0, widest = 0;
                string worst = "", worstFile = "";
                var byCommand = new SortedDictionary<string, int>();
                scripts = 0;

                foreach (var f in RomFiles.Settled(dir))
                {
                    var bytes = File.ReadAllBytes(f);
                    if (bytes.Length == 0) continue;
                    var cmds = WestScript.Parse(bytes, version);
                    if (cmds.Count == 0) continue;
                    int pos = 0; foreach (var c in cmds) { c.WordPos = pos; pos += 1 + c.Args.Length; }
                    scripts++;

                    foreach (var l in WestScriptDisplay.Build(cmds, version, mode))
                    {
                        lines++;
                        int w = l.Display.Length;
                        if (w > widest) { widest = w; worst = l.Display.Trim(); worstFile = Path.GetFileName(f); }
                        if (w <= VisibleChars) continue;
                        over++;
                        string key = l.Text.Trim().Split(' ')[0];
                        byCommand[key] = byCommand.TryGetValue(key, out int n) ? n + 1 : 1;
                    }
                }

                double share = 100.0 * over / Math.Max(1, lines);
                _out.WriteLine($"{gameCode} {mode}: {scripts} scripts, {lines} lines, {over} wider than "
                               + $"{VisibleChars} characters ({share:F2}%), widest {widest} in {worstFile}");
                foreach (var kv in byCommand.OrderByDescending(x => x.Value).Take(5))
                    _out.WriteLine($"   {kv.Key}: {kv.Value}");

                Assert.True(scripts >= 500, $"only {scripts} scripts were read");
                Assert.True(lines > 8000, $"only {lines} lines were produced, so this checked very little");

                // Leaving out settings that are switched off took this from about 5% to about 1.5%.
                // Anything above 3% means a change has started pushing lines off the side again.
                Assert.True(share < 3.0,
                    $"{share:F2}% of {mode} lines are too wide to read without scrolling. Widest is {widest} "
                    + $"characters in {worstFile}: {worst}");
            }
        }
    }
}
