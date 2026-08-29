using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The whole building-animation set, checked across every model and every map rather than by looking
    /// at one town. Skipped when no local project is present.
    /// </summary>
    [Collection("rom")]
    public class BuildingAnimationSweepTests
    {
        private const string Project = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        private static bool Ready
        {
            get
            {
                if (!Directory.Exists(Project)) return false;
                try { new RomInfo("IPKE", Project); } catch { return false; }
                return BuildingAnimationSet.Available;
            }
        }

        private static string Unpacked(string name) => Path.Combine(Project, "unpacked", name);

        private static readonly FieldTimeZone[] AllTimes =
        {
            FieldTimeZone.Morning, FieldTimeZone.Noon, FieldTimeZone.Evening,
            FieldTimeZone.Night, FieldTimeZone.Midnight,
        };

        private static IEnumerable<(int id, BuildingAnimationInfo info)> AllModels()
        {
            foreach (var f in Directory.GetFiles(Unpacked("buildingAnimListOut")).OrderBy(x => x))
                yield return (int.Parse(Path.GetFileName(f)), new BuildingAnimationInfo(File.ReadAllBytes(f)));
        }

        // ── the windmill, which is what caught the scaling bug ──────────────────────────
        [Fact]
        public void TheWindmillSailsStayOnTopOfItAndTurnRightRound()
        {
            if (!Ready) return;

            // Model 27 is New Bark Town's windmill and animation 5 turns it.
            using var fs = File.OpenRead(Path.Combine(Unpacked("exteriorBuildingModels"), "0027"));
            var model = NSBMDLoader.LoadNSBMD(fs).models[0];
            var anim = JointAnimation.Load(File.ReadAllBytes(Path.Combine(Unpacked("buildingAnimations"), "0005")));
            Assert.NotNull(anim);

            // Distances in an animation are stored the way the model stores its own, so they have to come
            // down by the model's scale. Without that the head ends up sixteen times too high.
            Assert.Equal(16f, model.modelScale);

            int head = anim.AnimatedObjects.First();
            var part = model.Objects[head];
            foreach (int frame in new[] { 0, 60, 120, 180, 239 })
            {
                var m = anim.MatrixFor(head, frame, part, model.modelScale);
                Assert.Equal(part.materix[13], m[13], 3);      // stays at the height the model gives it
            }

            // The sails go right round over the animation's 240 frames.
            int sails = anim.AnimatedObjects.Last();
            var seen = Enumerable.Range(0, anim.FrameCount)
                                 .Select(f => anim.MatrixFor(sails, f, model.Objects[sails], model.modelScale)[0])
                                 .ToArray();
            Assert.True(seen.Max() > 0.9f, "the sails never come back round to where they started");
            Assert.True(seen.Min() < -0.9f, "the sails never turn past halfway");
        }

        [Fact]
        public void NoAnimatedPartIsFlungAwayFromWhereItsModelPutsIt()
        {
            if (!Ready) return;

            int samples = 0, near = 0;
            foreach (var (id, info) in AllModels())
            {
                if (!info.Animates) continue;
                string mp = Path.Combine(Unpacked("exteriorBuildingModels"), id.ToString("D4"));
                if (!File.Exists(mp)) continue;

                NSBMD nsbmd;
                try { using var fs = File.OpenRead(mp); nsbmd = NSBMDLoader.LoadNSBMD(fs); } catch { continue; }
                if (nsbmd?.models == null || nsbmd.models.Length == 0) continue;
                var model = nsbmd.models[0];
                float scale = model.modelScale <= 0 ? 1f : model.modelScale;

                foreach (int code in info.UsedCodes)
                {
                    string ap = Path.Combine(Unpacked("buildingAnimations"), code.ToString("D4"));
                    if (!File.Exists(ap)) continue;
                    var anim = JointAnimation.Load(File.ReadAllBytes(ap));
                    if (anim == null || !anim.Moves) continue;

                    foreach (int p in anim.AnimatedObjects)
                    {
                        if (p < 0 || p >= model.Objects.Count) continue;
                        var obj = model.Objects[p];
                        for (int f = 0; f < Math.Min(anim.FrameCount, 60); f += 7)
                        {
                            var m = anim.MatrixFor(p, f, obj, scale);
                            if (m == null) continue;
                            samples++;
                            float away = Math.Abs(m[12] - obj.materix[12])
                                       + Math.Abs(m[13] - obj.materix[13])
                                       + Math.Abs(m[14] - obj.materix[14]);
                            // A few parts genuinely slide about; nothing should be thrown off the model.
                            if (away < 20f) near++;
                        }
                    }
                }
            }

            Assert.True(samples > 500, $"only {samples} samples");
            Assert.Equal(samples, near);
        }

        // ── every time of day ───────────────────────────────────────────────────────────
        [Fact]
        public void EveryClockDrivenModelHasSomethingToShowAtEveryTime()
        {
            if (!Ready) return;

            var timed = AllModels().Where(m => m.info.Animates && m.info.IsTimeOfDay).ToArray();
            Assert.NotEmpty(timed);

            foreach (var (id, _) in timed)
                foreach (var zone in AllTimes)
                    Assert.True(BuildingAnimationSet.CodesToPlay(id, false, zone).Any(),
                        $"model {id} has nothing for {FieldTimeOfDay.Name(zone)}");
        }

        [Fact]
        public void EveryClockDrivenModelReallyChangesThroughTheDay()
        {
            if (!Ready) return;

            foreach (var (id, info) in AllModels().Where(m => m.info.Animates && m.info.IsTimeOfDay))
            {
                var byTime = AllTimes
                    .Select(z => string.Join(",", BuildingAnimationSet.CodesToPlay(id, false, z)))
                    .ToArray();

                // Morning, day, evening and night each get their own; night and the small hours share.
                Assert.True(byTime.Distinct().Count() > 1, $"model {id} shows the same thing all day");
                Assert.Equal(byTime[3], byTime[4]);
            }
        }

        [Fact]
        public void EveryMapResolvesItsAnimationsAtEveryTimeOfDay()
        {
            if (!Ready) return;

            var infos = AllModels().ToDictionary(m => m.id, m => m.info);
            int maps = 0, playing = 0;

            // The two test targets run side by side against the same unpacked project, so on the first
            // run after a build one of them can be part way through unpacking the maps while this one
            // starts counting. Wait for the count to stop changing before reading it, or this sweeps a
            // fraction of the maps and fails for a reason that has nothing to do with animation.
            var files = RomFiles.Settled(Unpacked("maps"));

            foreach (var mp in files)
            {
                MapFile map;
                try { map = new MapFile(mp, RomInfo.GameFamilies.HGSS, true, false); } catch { continue; }
                if (map.buildings == null || map.buildings.Count == 0) continue;
                maps++;

                foreach (var zone in AllTimes)
                    foreach (var b in map.buildings)
                    {
                        if (!infos.TryGetValue((int)b.modelID, out var info) || !info.Animates) continue;
                        // The point is that this never throws, whatever map and whatever time.
                        playing += BuildingAnimationSet.CodesToPlay((int)b.modelID, false, zone).Count();
                    }
            }

            Assert.True(maps > 400, $"only swept {maps} maps");
            Assert.True(playing > 1000, $"only {playing} animations resolved across every map and time");
        }
    }
}
