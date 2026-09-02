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
    /// <summary>The second animation a move can hold.</summary>
    [Collection("rom")]
    public class WestTurnVariantTests
    {
        private readonly ITestOutputHelper _out;
        public WestTurnVariantTests(ITestOutputHelper o) { _out = o; }

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

        private static List<WazaSeqCommand> Load(byte[] bytes, WazaSeqVersion v)
        {
            var cmds = WestScript.Parse(bytes, v);
            int pos = 0;
            foreach (var c in cmds) { c.WordPos = pos; pos += 1 + c.Args.Length; }
            return cmds;
        }

        /// <summary>The commands a run actually executed, which is what the two animations differ in.</summary>
        private static List<int> CommandsReached(List<WazaSeqCommand> cmds, WazaSeqVersion v,
                                                 ScriptNarc particles, bool secondVariant)
        {
            var w = new WestPlayer(cmds, v, particles, 64, 120, 190, 60,
                                   attackerIsEnemy: false, selfTarget: false)
            { SecondTurnVariant = secondVariant };
            for (int i = 0; i < 900 && !w.Finished; i++) w.Step();
            return w.CommandsRun.ToList();
        }

        [Fact]
        public void EveryHeartGoldMoveWithTwoAnimationsCanShowBoth() => Sweep(HeartGold, "IPKE", WazaSeqVersion.HGSS);

        [Fact]
        public void EveryPlatinumMoveWithTwoAnimationsCanShowBoth() => Sweep(Platinum, "CPUE", WazaSeqVersion.Plat);

        private void Sweep(string project, string gameCode, WazaSeqVersion version)
        {
            string dir = ScriptDir(project, gameCode);
            Assert.True(dir != null, gameCode + ": the move-effect archive could not be unpacked, so nothing was checked");

            var particles = new ScriptNarc(DirNames.wazaParticle);
            Assert.True(particles.Available, "the particle archive is missing, so the runs would not be comparable");

            int scripts = 0, withTurnCheck = 0, differ = 0;
            var same = new List<string>();
            var sameTarget = new List<string>();

            foreach (var f in RomFiles.Settled(dir))
            {
                var bytes = File.ReadAllBytes(f);
                if (bytes.Length == 0) continue;
                var cmds = Load(bytes, version);
                if (cmds.Count == 0) continue;
                scripts++;

                var checks = cmds.Where(c => WestOpcodes.Name(version, c.OpId) == "WEST_TURN_CHK").ToList();
                if (checks.Count == 0) continue;

                // Where the two offsets actually land.
                bool twoDestinations = checks.Any(c => c.Args.Length >= 2
                                                       && (c.WordPos + 1 + c.Args[0]) != (c.WordPos + 2 + c.Args[1]));
                if (!twoDestinations) { sameTarget.Add(Path.GetFileName(f)); continue; }
                withTurnCheck++;

                var first = CommandsReached(cmds, version, particles, false);
                var second = CommandsReached(Load(bytes, version), version, particles, true);

                if (first.SequenceEqual(second)) same.Add(Path.GetFileName(f));
                else differ++;
            }

            _out.WriteLine($"{gameCode}: {scripts} scripts, {withTurnCheck} of them hold two animations, "
                           + $"{differ} run different commands under the two settings");
            foreach (var s in same) _out.WriteLine("  same either way: " + s);
            foreach (var s in sameTarget) _out.WriteLine("  both offsets point at the same command: " + s);

            Assert.True(scripts >= 500, $"only {scripts} scripts were read");
            Assert.True(withTurnCheck >= 20,
                $"only {withTurnCheck} scripts branch on the turn count, so the sweep found almost nothing to check");
            Assert.True(same.Count == 0,
                $"{same.Count} of {withTurnCheck} moves show the same animation either way, so the setting does nothing "
                + "for them: " + string.Join(", ", same.Take(10)));
        }
    }
}
