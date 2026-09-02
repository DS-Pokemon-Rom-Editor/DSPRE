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
    /// <summary>The preview measured against the real game, in numbers.</summary>
    [Collection("rom")]
    public class WestAgainstTheGameTests
    {
        private readonly ITestOutputHelper _out;
        public WestAgainstTheGameTests(ITestOutputHelper o) { _out = o; }

        private const string Platinum =
            @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents";

        private const string HeartGold = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        private static string ScriptDir(string project = Platinum, string code = "CPUE")
        {
            if (!Directory.Exists(project)) return null;
            try { new RomInfo(code, project); } catch { return null; }
            var narc = new ScriptNarc(DirNames.wazaEffectScripts);
            return narc.Available ? gameDirs[DirNames.wazaEffectScripts].unpackedDir : null;
        }

        /// <summary>What the preview does with a move, frame by frame.</summary>
        private sealed class Run
        {
            public int Frames;
            public int MotionStartsAt = -1;      // first frame either Pokemon has moved from its place
            public double FurthestMove;          // the biggest distance either has moved, in pixels
            public int DarkestAt = -1;           // the frame the screen is furthest from its normal colour
            public double DarkestAmount;         // how far, 0 to 1
            public int FadeStartsAt = -1;        // the first frame the screen is off its normal colour at all
        }

        private static Run Measure(string dir, int move, bool attackerIsEnemy, int ceiling = 400,
                                   WazaSeqVersion version = WazaSeqVersion.Plat)
        {
            string f = Path.Combine(dir, move.ToString("D4"));
            var cmds = WestScript.Parse(File.ReadAllBytes(f), version);
            // The particle archive has to be real: without it every particle lives no time at all and
            // WAIT_PARTICLE returns straight away, which made the preview look four times too quick.
            var w = new WestPlayer(cmds, version, new ScriptNarc(DirNames.wazaParticle),
                                   64, 120, 190, 60,
                                   attackerIsEnemy: attackerIsEnemy, selfTarget: false);
            var r = new Run();
            for (int n = 0; n < ceiling; n++)
            {
                if (w.Finished) break;
                w.Step();
                r.Frames = n + 1;

                for (int m = 0; m < 2; m++)
                {
                    double moved = Math.Abs(w.MonDX[m]) + Math.Abs(w.MonDY[m])
                                 + Math.Abs(w.MonShakeX[m]) + Math.Abs(w.MonShakeY[m]);
                    if (moved > 0.5)
                    {
                        if (r.MotionStartsAt < 0) r.MotionStartsAt = n;
                        if (moved > r.FurthestMove) r.FurthestMove = moved;
                    }
                }
                double dark = Math.Max(w.FadeOpacity, w.BgFlashAmount);
                if (dark > 0.01 && r.FadeStartsAt < 0) r.FadeStartsAt = n;
                if (dark > r.DarkestAmount) { r.DarkestAmount = dark; r.DarkestAt = n; }
            }
            return r;
        }

        /// <summary>HeartGold's Tackle, against what HeartGold did.</summary>
        /// <summary>
        /// Which HeartGold moves flash at all, over the whole set.
        /// This exists to answer a question the captures raised rather than to check the preview: two
        /// HeartGold battles were recorded, 1,944 frames between them covering several turns, and the
        /// screen never moved more than about 5 brightness levels out of 255. Knowing how many of the
        /// 501 moves flash, and whether the ones those two battles could reach are among them, is what
        /// turns "we did not see a flash" into "there was none to see".
        /// </summary>
        [Fact]
        public void HowManyHeartGoldMovesFlashAtAll()
        {
            string dir = ScriptDir(HeartGold, "IPKE");
            Assert.True(dir != null, "HeartGold's move-effect archive could not be unpacked, so nothing was checked");

            int measured = 0, flashing = 0;
            var reachable = new[] { 33, 45, 39, 64, 98 };   // Tackle, Growl, Tail Whip, Peck, Quick Attack
            var reachableFlash = new List<string>();

            foreach (var f in RomFiles.Settled(dir))
            {
                if (!int.TryParse(Path.GetFileName(f), out int move)) continue;
                var r = Measure(dir, move, attackerIsEnemy: false, version: WazaSeqVersion.HGSS);
                if (r.Frames == 0) continue;
                measured++;
                if (r.DarkestAmount > 0.2) flashing++;
                if (reachable.Contains(move) && r.DarkestAmount > 0.2)
                    reachableFlash.Add($"{move} at {r.DarkestAmount:F2}");
            }

            _out.WriteLine($"{measured} HeartGold moves driven; {flashing} of them flash ({100.0 * flashing / measured:F0}%)");
            _out.WriteLine($"of the moves those two battles could use, the ones that flash: "
                         + (reachableFlash.Count == 0 ? "none" : string.Join(", ", reachableFlash)));

            Assert.True(measured >= 400, $"only {measured} moves were driven");

            // This is the point of the test: none of the moves reachable in the recorded battles flashes,
            // so no flash could have been captured there however long the recording ran.
            Assert.Empty(reachableFlash);
        }

        [Fact]
        public void HeartGoldsTackleIsComparedWithWhatHeartGoldDid()
        {
            string dir = ScriptDir(HeartGold, "IPKE");
            Assert.True(dir != null, "HeartGold's move-effect archive could not be unpacked, so nothing was checked");

            var tackle = Measure(dir, 33, attackerIsEnemy: false, version: WazaSeqVersion.HGSS);

            _out.WriteLine("Tackle (move 33) in HeartGold:");
            _out.WriteLine("   game:    a whole turn captured, 864 frames: our Tackle at 248-267 close to the attacker");
            _out.WriteLine("            (x about 60), the foe's move at 461-484 close to it (x about 195), two more after.");
            _out.WriteLine("            Brightness moved 2.7 out of 255 across the lot, so nothing in the turn flashes.");
            _out.WriteLine($"   preview: {tackle.Frames} frames, darkest {tackle.DarkestAmount:F2}, "
                         + $"movement starts at {tackle.MotionStartsAt}, furthest {tackle.FurthestMove:F0}px");

            // HeartGold does not flash for this move, so neither may the preview.
            Assert.True(tackle.DarkestAmount < 0.2,
                $"HeartGold does not flash for Tackle but the preview reached {tackle.DarkestAmount:F2}");

            // The same question for the other moves that turn used.
            foreach (int move in new[] { 45, 39 })   // Growl, Tail Whip
            {
                var r = Measure(dir, move, attackerIsEnemy: move == 39, version: WazaSeqVersion.HGSS);
                _out.WriteLine($"   move {move}: preview {r.Frames} frames, darkest {r.DarkestAmount:F2}");
                Assert.True(r.DarkestAmount < 0.2,
                    $"HeartGold does not flash for move {move} but the preview reached {r.DarkestAmount:F2}");
            }

            // It moves the attacker, and keeps it near where it started.
            Assert.True(tackle.MotionStartsAt >= 0, "HeartGold moves the attacker for Tackle and the preview moved nothing");
            Assert.InRange(tackle.FurthestMove, 1, 120);
            Assert.InRange(tackle.Frames, 10, 130);
        }

        [Fact]
        public void LeerAndTackleAreComparedWithWhatTheGameDid()
        {
            string dir = ScriptDir();
            Assert.True(dir != null, "the move-effect archive could not be unpacked, so nothing was checked");

            // Measured from the capture. Leer is the foe's, so the preview runs it from the enemy side.
            var leer = Measure(dir, 43, attackerIsEnemy: true);
            var tackle = Measure(dir, 33, attackerIsEnemy: false);

            _out.WriteLine("Leer (move 43), the foe's:");
            _out.WriteLine($"   game:    127 frames, screen darkened from 139 to 36 with the low point 34 frames in");
            _out.WriteLine($"   preview: {leer.Frames} frames, darkest {leer.DarkestAmount:F2} at frame {leer.DarkestAt}, "
                         + $"movement starts at {leer.MotionStartsAt}, furthest {leer.FurthestMove:F0}px");
            _out.WriteLine($"            the darkening begins at frame {leer.FadeStartsAt} and takes "
                         + $"{leer.DarkestAt - leer.FadeStartsAt} frames to reach its deepest");
            _out.WriteLine("Tackle (move 33), the player's:");
            _out.WriteLine($"   game:    63 frames, no change in brightness, the change stayed within 44px of the attacker");
            _out.WriteLine($"   preview: {tackle.Frames} frames, darkest {tackle.DarkestAmount:F2} at frame {tackle.DarkestAt}, "
                         + $"movement starts at {tackle.MotionStartsAt}, furthest {tackle.FurthestMove:F0}px");
            _out.WriteLine("Measured and comparable: whether the screen darkens, whether the attacker moves,");
            _out.WriteLine("and roughly how far. Lengths are recorded but not matched, for the reason below.");

            // Leer darkens the screen in the game, so the preview has to darken it too.
            Assert.True(leer.DarkestAmount > 0.2,
                $"the game darkened the screen for Leer but the preview only reached {leer.DarkestAmount:F2}");

            // Tackle does not darken anything in the game, and the preview must not either.
            Assert.True(tackle.DarkestAmount < 0.2,
                $"the game did not darken the screen for Tackle but the preview reached {tackle.DarkestAmount:F2}");

            // Tackle moves the attacker in the game, within about 44px. The preview must move something.
            Assert.True(tackle.MotionStartsAt >= 0, "the game moved the attacker for Tackle and the preview moved nothing");
            Assert.InRange(tackle.FurthestMove, 1, 120);

            // Lengths are recorded rather than matched, and here is why.
            Assert.InRange(leer.Frames, 10, 260);
            Assert.InRange(tackle.Frames, 10, 130);
        }
    }
}
