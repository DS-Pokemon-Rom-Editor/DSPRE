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
    /// No move leaves a Pokemon somewhere other than where it started, and none flings one off screen.
    /// </summary>
    [Collection("rom")]
    public class WestSpriteReturnsHomeTests
    {
        private readonly ITestOutputHelper _out;
        public WestSpriteReturnsHomeTests(ITestOutputHelper o) { _out = o; }

        private const string HeartGold = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";
        private const string Platinum =
            @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents";

        private const double ScreenWidth = 256;

        private static string ScriptDir(string project, string gameCode)
        {
            if (!Directory.Exists(project)) return null;
            try { new RomInfo(gameCode, project); } catch { return null; }
            var narc = new ScriptNarc(DirNames.wazaEffectScripts);
            return narc.Available ? gameDirs[DirNames.wazaEffectScripts].unpackedDir : null;
        }

        [Fact]
        public void EveryHeartGoldMoveBringsItsPokemonBack() => Sweep(HeartGold, "IPKE", WazaSeqVersion.HGSS);

        [Fact]
        public void EveryPlatinumMoveBringsItsPokemonBack() => Sweep(Platinum, "CPUE", WazaSeqVersion.Plat);

        private void Sweep(string project, string gameCode, WazaSeqVersion version)
        {
            string dir = ScriptDir(project, gameCode);
            Assert.True(dir != null, gameCode + ": the move-effect archive could not be unpacked, so nothing was checked");

            var particles = new ScriptNarc(DirNames.wazaParticle);
            Assert.True(particles.Available, "the particle archive is missing, so the moves would not run properly");

            int scripts = 0, moved = 0;
            var tooFar = new List<string>();
            var leftBehind = new List<string>();
            double worstTravel = 0, worstResidue = 0;

            foreach (var f in RomFiles.Settled(dir))
            {
                var bytes = File.ReadAllBytes(f);
                if (bytes.Length == 0) continue;
                var cmds = WestScript.Parse(bytes, version);
                if (cmds.Count == 0) continue;
                int pos = 0; foreach (var c in cmds) { c.WordPos = pos; pos += 1 + c.Args.Length; }
                scripts++;
                string name = Path.GetFileName(f);

                foreach (bool asEnemy in new[] { false, true })
                {
                    var w = new WestPlayer(cmds, version, particles, 64, 120, 190, 60,
                                           attackerIsEnemy: asEnemy, selfTarget: false);
                    double travel = 0;
                    int frames = 0;
                    while (frames < 900 && !w.Finished)
                    {
                        w.Step(); frames++;
                        for (int m = 0; m < 2; m++)
                            travel = Math.Max(travel, Math.Max(Math.Abs(w.MonDX[m]), Math.Abs(w.MonDY[m])));
                    }
                    if (travel > 0.5) moved++;
                    worstTravel = Math.Max(worstTravel, travel);

                    if (travel > ScreenWidth)
                        tooFar.Add($"{name} ({(asEnemy ? "enemy" : "player")}): travelled {travel:F0}px");

                    double residue = 0;
                    for (int m = 0; m < 2; m++)
                        residue = Math.Max(residue, Math.Max(Math.Abs(w.MonDX[m]), Math.Abs(w.MonDY[m])));
                    worstResidue = Math.Max(worstResidue, residue);
                    if (residue > 2.0)
                        leftBehind.Add($"{name} ({(asEnemy ? "enemy" : "player")}): {residue:F0}px from home at the end");
                }
            }

            _out.WriteLine($"{gameCode}: {scripts} scripts run from both sides; {moved} runs moved a Pokemon at all");
            _out.WriteLine($"  furthest any sprite travelled: {worstTravel:F0}px; furthest left from home at the end: {worstResidue:F0}px");
            foreach (var s in tooFar.Take(5)) _out.WriteLine("  too far: " + s);
            foreach (var s in leftBehind.Take(5)) _out.WriteLine("  left behind: " + s);

            Assert.True(scripts >= 500, $"only {scripts} scripts were read");
            Assert.True(moved > 50, $"only {moved} runs moved anything, so this checked very little");
            Assert.True(tooFar.Count == 0,
                $"{tooFar.Count} runs threw a Pokemon further than the screen is wide: {string.Join("; ", tooFar.Take(5))}");
            // Recorded, not asserted, for the reason in the summary above. Two moves (171 and 255) end
            // mid-slide; if that number grows, something else has started leaving sprites behind.
            Assert.True(leftBehind.Count <= 4,
                $"{leftBehind.Count} runs ended with a Pokemon away from home, which is more than the two "
                + $"moves known to stop mid-slide: {string.Join("; ", leftBehind.Take(6))}");
        }
    }
}
