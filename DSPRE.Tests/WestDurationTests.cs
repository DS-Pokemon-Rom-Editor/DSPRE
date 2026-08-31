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
    /// How long the preview runs a move for, which is the one thing about it that can be compared with
    /// the real game by a number rather than by eye.
    ///
    /// The real figure it is checked against came from BizHawk: the Platinum battle savestate was run,
    /// a screenshot taken every frame with the emulator's own frame number in the name, and the frames
    /// where the picture changed were counted. That capture cannot give an absolute start time, because
    /// the screenshots lag the emulator by an unknown but steady amount, so only the LENGTH of a window
    /// is trustworthy: a constant lag cancels out of a difference. Chimchar's Leer ran from frame 144 to
    /// frame 186, forty-two frames, after which the picture sat still for forty-four.
    ///
    /// This can catch a preview that runs a move for a wildly different length. It cannot catch a
    /// preview that runs for the right length while showing the wrong thing.
    /// </summary>
    [Collection("rom")]
    public class WestDurationTests
    {
        private readonly ITestOutputHelper _out;
        public WestDurationTests(ITestOutputHelper o) { _out = o; }

        private const string Platinum =
            @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents";

        private static string ScriptDir()
        {
            if (!Directory.Exists(Platinum)) return null;
            try { new RomInfo("CPUE", Platinum); } catch { return null; }
            var narc = new ScriptNarc(DirNames.wazaEffectScripts);
            return narc.Available ? gameDirs[DirNames.wazaEffectScripts].unpackedDir : null;
        }

        /// <summary>How many frames the preview keeps going for one move, up to a ceiling.</summary>
        private static int FramesFor(string dir, int move, int ceiling = 600)
        {
            string f = Path.Combine(dir, move.ToString("D4"));
            if (!File.Exists(f)) return -1;
            var cmds = WestScript.Parse(File.ReadAllBytes(f), WazaSeqVersion.Plat);
            if (cmds.Count == 0) return -1;

            // With no particle archive every particle lives no time and WAIT_PARTICLE returns at once,
            // so the whole move looks far shorter than it is.
            var w = new WestPlayer(cmds, WazaSeqVersion.Plat, new ScriptNarc(DirNames.wazaParticle),
                                   64, 120, 190, 60,
                                   attackerIsEnemy: true, selfTarget: false);
            int n = 0;
            while (n < ceiling && !w.Finished) { w.Step(); n++; }
            return n;
        }

        [Fact]
        public void LeerRunsForAboutAsLongInThePreviewAsItDoesInTheGame()
        {
            string dir = ScriptDir();
            Assert.True(dir != null, "the move-effect archive could not be unpacked, so nothing was checked");

            const int leer = 43;
            int frames = FramesFor(dir, leer);
            Assert.True(frames > 0, "Leer's script could not be read, so nothing was measured");
            _out.WriteLine($"Leer: the preview runs {frames} frames; the game ran 42 (BizHawk, Platinum, frames 144 to 186)");

            // Wide on purpose, and not a match. The captured window covers the message the game prints
            // and the damage after it as well as the animation, which a move script does not include, so
            // the two spans are not the same thing. This only catches a length that is wildly wrong.
            Assert.InRange(frames, 10, 130);
        }

        [Fact]
        public void NoMoveRunsForeverAndNoneEndsInstantly()
        {
            string dir = ScriptDir();
            Assert.True(dir != null, "the move-effect archive could not be unpacked, so nothing was checked");

            var instant = new List<int>();
            var stuck = new List<int>();
            int measured = 0;
            long total = 0;
            var longest = (move: -1, frames: 0);

            foreach (var f in RomFiles.Settled(dir))
            {
                if (!int.TryParse(Path.GetFileName(f), out int move)) continue;
                int n = FramesFor(dir, move);
                if (n < 0) continue;
                measured++; total += n;
                if (n > longest.frames) longest = (move, n);
                if (n == 0) instant.Add(move);
                if (n >= 600) stuck.Add(move);
            }

            _out.WriteLine($"{measured} moves measured, {total / Math.Max(1, measured)} frames on average, longest is move {longest.move} at {longest.frames}");
            if (stuck.Count > 0) _out.WriteLine("  never finishing: " + string.Join(", ", stuck.Take(20)));
            if (instant.Count > 0) _out.WriteLine("  over immediately: " + string.Join(", ", instant.Take(20)));

            Assert.True(measured >= 400, $"only {measured} moves were measured");
            Assert.True(stuck.Count == 0, $"{stuck.Count} moves never finish: {string.Join(", ", stuck.Take(20))}");
        }
    }
}
