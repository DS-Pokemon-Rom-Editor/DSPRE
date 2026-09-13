using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>Every seal's first emitter draws, makes particles and ends, and its sticker draws.</summary>
    [Collection("rom")]
    public class BallSealEffectSweepTests
    {
        private readonly ITestOutputHelper _out;
        public BallSealEffectSweepTests(ITestOutputHelper o) => _out = o;

        // A send-out waits for every burst to end, so one that never ends would hold the preview forever.
        private const int LongestBurstTicks = 900;

        private static string Project(string name) => name switch
        {
            "Diamond" => TestRoms.Diamond, "Platinum" => TestRoms.Platinum, _ => TestRoms.HeartGold,
        };

        [SkippableTheory]
        [InlineData("ADAE", "Diamond")]
        [InlineData("CPUE", "Platinum")]
        [InlineData("IPKE", "HeartGold")]
        public void EverySealBurstsAndEndsAndItsStickerDraws(string code, string name)
        {
            Skip.IfNot(Directory.Exists(Project(name)), $"{name} is not unpacked here");
            new RomInfo(code, Project(name));
            Assert.True(gameDirs.ContainsKey(DirNames.ballParticles), name + " has no ball particle archive mapped");
            Assert.True(gameDirs.ContainsKey(DirNames.sealGraphics), name + " has no seal graphics archive mapped");
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.ballParticles, DirNames.sealGraphics });

            var seals = BallSeals.Read();
            Assert.Equal(BallSeals.Count + 1, seals.Count);
            var particles = new ScriptNarc(DirNames.ballParticles);

            var problems = new List<string>();
            int checkedSeals = 0, longest = 0, busiest = 0;
            foreach (var seal in seals.Skip(1))
            {
                string who = $"{seal.Id} {seal.Name} (particles {seal.Particle}, sticker {seal.Sprite})";
                checkedSeals++;

                byte[] bytes = particles.Get(seal.Particle);
                if (bytes == null || bytes.Length == 0) { problems.Add(who + ": no particle file"); continue; }
                var arc = SpaArchive.Parse(bytes);
                if (arc.Emitters.Count == 0) { problems.Add(who + ": no emitter"); continue; }

                var em = arc.Emitters[0];
                if (em.TexNo < 0 || em.TexNo >= arc.Textures.Count || arc.Textures[em.TexNo].Rgba == null)
                    problems.Add(who + $": texture {em.TexNo} of {arc.Textures.Count} does not draw");

                var sim = new SpaSimulator(em, em.AxisX, em.AxisY);
                int ticks = 0, peak = 0;
                while (!sim.Finished && ticks < LongestBurstTicks) { sim.Step(); peak = Math.Max(peak, sim.AliveCount); ticks++; }
                if (!sim.Finished) problems.Add(who + $": still running after {LongestBurstTicks} ticks");
                if (peak == 0) problems.Add(who + ": never makes a particle");
                longest = Math.Max(longest, ticks);
                busiest = Math.Max(busiest, peak);

                var cells = BallCapsuleGraphics.StickerCells(seal);
                if (cells == null) problems.Add(who + ": sticker files do not load");
                else
                {
                    var px = cells.RenderCellRgba(0);
                    int drawn = 0;
                    if (px.Rgba != null) for (int i = 3; i < px.Rgba.Length; i += 4) if (px.Rgba[i] != 0) drawn++;
                    if (drawn < 20) problems.Add(who + $": sticker draws only {drawn} pixels");
                }

                _out.WriteLine($"{who}: {arc.Emitters.Count} emitters, ends after {ticks} ticks, up to {peak} particles");
            }

            _out.WriteLine($"{name}: {checkedSeals} seals, longest burst {longest} ticks, most particles at once {busiest}");
            Assert.Equal(BallSeals.Count, checkedSeals);
            Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
        }
    }
}
