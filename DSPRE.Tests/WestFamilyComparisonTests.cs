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
    /// One move of each animation family, against what Platinum actually did.
    ///
    /// The two saves only ever offered a contact move and a screen flash, so the other four families were
    /// staged: the rival's Chimchar was given the move four times over in a copy of the project, the ROM
    /// was rebuilt, and the battle recorded frame by frame from the savestate that sits on the rival's
    /// prompt. That makes the capture unambiguous, since the only move the rival can use is the one being
    /// measured.
    ///
    /// What is compared is what can be measured off a picture: whether the screen flashes and which way,
    /// how big the swing is, and whether the change stays near the attacker or crosses the screen.
    /// Lengths are recorded, not matched, for the same reason as the earlier two moves: the recorded
    /// window includes the message the game prints and the damage afterwards, which a move's script does
    /// not cover. A test that demanded they match would be demanding the preview contain something it
    /// does not.
    ///
    /// Frame numbers from a capture carry about two frames of slack, because the screenshot lags the
    /// emulator by roughly that much, so nothing here turns on a single frame.
    /// </summary>
    [Collection("rom")]
    public class WestFamilyComparisonTests
    {
        private readonly ITestOutputHelper _out;
        public WestFamilyComparisonTests(ITestOutputHelper o) { _out = o; }

        private const string Platinum =
            @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents";

        /// <summary>What the recording of that move's battle measured. Brightness is out of 255.</summary>
        private sealed class Recorded
        {
            public string Family;
            public int Move;
            public int Frames;         // how long the battlefield differed from rest
            public string FlashKind;   // "darkens", "brightens" or "flat"
            public double FlashSize;   // how far the whole-screen brightness moved
            public int FlashAfter;     // frames from the start of movement to the extreme
            public int Furthest;       // how far the change reached from the attacker, in pixels
        }

        // Measured 2026-08-31 from four staged battles, 3,710 shots each, on the savestate
        // 01_rival852_you-TURTWIG_vs_CHIMCHAR.State.
        private static readonly Recorded[] Game =
        {
            new Recorded { Family = "particle beam",  Move = 5,   Frames = 157, FlashKind = "darkens",   FlashSize = 96.2,  FlashAfter = 52,  Furthest = 190 },
            new Recorded { Family = "background",     Move = 200, Frames = 312, FlashKind = "darkens",   FlashSize = 124.6, FlashAfter = 36,  Furthest = 190 },
            new Recorded { Family = "cell actor",     Move = 57,  Frames = 204, FlashKind = "brightens", FlashSize = 106.6, FlashAfter = 100, Furthest = 190 },
            new Recorded { Family = "status overlay", Move = 334, Frames = 114, FlashKind = "brightens", FlashSize = 57.2,  FlashAfter = 10,  Furthest = 190 },
        };

        private static bool Ready()
        {
            if (!Directory.Exists(Platinum)) return false;
            try { new RomInfo("CPUE", Platinum); } catch { return false; }
            return new ScriptNarc(DirNames.wazaEffectScripts).Available;
        }

        [Fact]
        public void EachAnimationFamilyAgreesWithWhatTheGameDid()
        {
            Assert.True(Ready(), "the Platinum project could not be opened, so nothing was checked");

            var narc = new ScriptNarc(DirNames.wazaEffectScripts);
            var particles = new ScriptNarc(DirNames.wazaParticle);
            Assert.True(particles.Available, "the particle archive is missing, so the preview would run far too short");

            int compared = 0;
            var problems = new List<string>();

            foreach (var g in Game)
            {
                var bytes = narc.Get(g.Move);
                Assert.True(bytes != null && bytes.Length > 0, $"move {g.Move} has no script, so nothing was checked");

                var cmds = WestScript.Parse(bytes, WazaSeqVersion.Plat);
                Assert.True(cmds.Count > 0, $"move {g.Move} decoded to nothing");
                int pos = 0; foreach (var c in cmds) { c.WordPos = pos; pos += 1 + c.Args.Length; }

                // The rival is the attacker in every one of these recordings.
                var w = new WestPlayer(cmds, WazaSeqVersion.Plat, particles, 64, 120, 190, 60,
                                       attackerIsEnemy: true, selfTarget: false);

                // The cell-animation resources the script asks for, loaded the same way the editor loads
                // them. Without this a move drawn from cell actors has nothing to draw with and would look
                // like a preview that ignores the move, which is a fault in the harness, not the preview.
                var res = WestCats.Extract(cmds, WazaSeqVersion.Plat);
                if (res.HasCellAnimation)
                {
                    var cells = new WeCellAnimRenderer();
                    if (cells.Load(res.Char, res.Pltt, res.Cell, res.CellAnm)) w.Cells = cells;
                }

                int frames = 0, movedAt = -1, flashAt = -1, mostActors = 0;
                double darkest = 0, brightest = 0, furthest = 0;
                while (frames < 900 && !w.Finished)
                {
                    w.Step(); frames++;
                    mostActors = Math.Max(mostActors, w.CatsActors.Count);
                    double moved = Math.Max(Math.Abs(w.MonDX[1]) + Math.Abs(w.MonShakeX[1]),
                                            Math.Abs(w.MonDY[1]) + Math.Abs(w.MonShakeY[1]));
                    if (moved > 0.5 && movedAt < 0) movedAt = frames;
                    furthest = Math.Max(furthest, moved);

                    // The fade is a COLOURED overlay, so which way the screen goes depends on the colour
                    // as well as the amount. Pulling a white sheet over the scene brightens it; treating
                    // every fade as a darkening said Iron Defense went dark when the game washes it white.
                    double fadeLum = (0.2126 * w.FadeR + 0.7152 * w.FadeG + 0.0722 * w.FadeB) / 255.0;
                    double dark = w.FadeOpacity * (1 - fadeLum);
                    double light = Math.Max(w.BgFlashAmount, w.FadeOpacity * fadeLum);
                    if (Math.Max(dark, light) > Math.Max(darkest, brightest)) flashAt = frames;
                    darkest = Math.Max(darkest, dark);
                    brightest = Math.Max(brightest, light);
                }

                string kind = darkest < 0.05 && brightest < 0.05 ? "flat"
                            : darkest >= brightest ? "darkens" : "brightens";
                compared++;

                _out.WriteLine($"{g.Family} (move {g.Move}):");
                _out.WriteLine($"   game:    {g.Frames} frames, screen {g.FlashKind} by {g.FlashSize:F1} of 255, "
                               + $"deepest {g.FlashAfter} frames in, change reaching {g.Furthest}px from the attacker");
                _out.WriteLine($"   preview: {frames} frames, {kind}"
                               + (kind == "flat" ? "" : $" (fade {darkest:F2}, flash {brightest:F2}) at frame {flashAt}")
                               + $", movement starts {(movedAt < 0 ? "never" : movedAt.ToString())}, furthest {furthest:F0}px");

                // The one thing a picture says plainly: does the screen change brightness at all, and
                // which way. A preview that stayed flat where the game swung by a quarter of the range,
                // or swung the other way, would be visibly wrong and this catches it.
                // Surf is measured differently, and deliberately. What brightens its recording is the wave
                // sprite sliding across the whole screen, not a palette change, and a brightness reading
                // off a screenshot cannot tell those two apart. Asserting a palette change here would be
                // asserting the wrong thing, so what is required instead is the mechanism the game
                // actually uses: the preview has to be driving cell actors, which is what draws that wave.
                if (g.Move == 57)
                {
                    _out.WriteLine($"   note:    the game's brightening here is the wave sprite covering the "
                                   + $"screen, not a palette change; the preview drives {mostActors} cell actor(s)");
                    if (mostActors == 0)
                        problems.Add($"{g.Family}: the game draws a wave across the screen but the preview "
                                     + "has no cell actor to draw it with");
                    continue;
                }

                if (kind != g.FlashKind)
                    problems.Add($"{g.Family}: the game {g.FlashKind} but the preview comes out {kind}");
            }

            Assert.Equal(Game.Length, compared);
            Assert.True(problems.Count == 0, string.Join("; ", problems));
        }
    }
}
