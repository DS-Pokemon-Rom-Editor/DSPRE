using DSPRE.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests.Models
{
    public class ShapeRewriteTests
    {
        private readonly ITestOutputHelper _out;
        public ShapeRewriteTests(ITestOutputHelper o) { _out = o; }

        private static List<DisplayListWalk.Corner> Corners(byte[] dl)
        {
            var walk = DisplayListWalk.Read(dl, out _);
            return walk == null ? null : walk.Runs.SelectMany(r => r.Corners).ToList();
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void AShapeToldToLeaveEveryCornerWhereItIsKeepsEveryCommandItHad(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            bool hgss = game == "HeartGold";
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            int shapes = 0;
            long corners = 0;

            foreach (var (name, bytes) in MapModels.Of(project, hgss))
            {
                var file = NsbmdFile.Read(bytes, out _);
                if (file == null) continue;

                for (int i = 0; i < file.Shapes.Count; i++)
                {
                    byte[] dl = file.DisplayList(i);
                    var was = Corners(dl);
                    if (was == null || was.Count == 0) continue;

                    var stay = was.ToDictionary(c => c.Index, c => (c.X, c.Y, c.Z));
                    byte[] made = ShapeRewrite.WithCornersAt(dl, stay, out string whynot);
                    Assert.True(made != null, $"{name} shape {i}: {whynot}");

                    var now = Corners(made);
                    Assert.True(now != null, $"{name} shape {i}: what was written cannot be read back.");
                    Assert.Equal(was.Count, now.Count);

                    for (int c = 0; c < was.Count; c++)
                    {
                        Assert.True(was[c].RawX == now[c].RawX && was[c].RawY == now[c].RawY
                                 && was[c].RawZ == now[c].RawZ,
                            $"{name} shape {i} corner {c} moved from "
                          + $"{was[c].RawX},{was[c].RawY},{was[c].RawZ} to "
                          + $"{now[c].RawX},{now[c].RawY},{now[c].RawZ}.");

                        Assert.True(was[c].How == now[c].How,
                            $"{name} shape {i} corner {c} changed from {was[c].How} to {now[c].How} "
                          + "although it did not move.");
                    }

                    shapes++;
                    corners += was.Count;
                }
            }

            Assert.True(shapes > 0, $"No {game} shape was written back, so this proved nothing.");
            _out.WriteLine($"{game}: {shapes} shapes and {corners} corners written back unchanged.");
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void MovingOneCornerMovesThatOneAndLeavesTheRestWhereTheyWere(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            bool hgss = game == "HeartGold";
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            int moved = 0, rewrittenInFull = 0;
            long grewBy = 0;

            foreach (var (name, bytes) in MapModels.Of(project, hgss).Take(80))
            {
                var file = NsbmdFile.Read(bytes, out _);
                if (file == null) continue;

                for (int i = 0; i < file.Shapes.Count; i++)
                {
                    byte[] dl = file.DisplayList(i);
                    var was = Corners(dl);
                    if (was == null || was.Count < 4) continue;

                    int pick = was.Count / 2;
                    var want = was.ToDictionary(c => c.Index, c => (c.X, c.Y, c.Z));
                    want[pick] = (was[pick].X + 0.25f, was[pick].Y + 0.5f, was[pick].Z - 0.125f);

                    byte[] made = ShapeRewrite.WithCornersAt(dl, want, out string whynot);
                    Assert.True(made != null, $"{name} shape {i}: {whynot}");

                    var now = Corners(made);
                    Assert.Equal(was.Count, now.Count);

                    for (int c = 0; c < was.Count; c++)
                    {
                        var (wx, wy, wz) = want[c];
                        Assert.True(Math.Abs(now[c].X - wx) < 1f / 4096f
                                 && Math.Abs(now[c].Y - wy) < 1f / 4096f
                                 && Math.Abs(now[c].Z - wz) < 1f / 4096f,
                            $"{name} shape {i} corner {c} came back at {now[c].X},{now[c].Y},{now[c].Z} "
                          + $"and should be at {wx},{wy},{wz}.");
                    }

                    for (int c = 0; c < was.Count; c++)
                        if (was[c].How != now[c].How) rewrittenInFull++;

                    grewBy += made.Length - dl.Length;
                    moved++;
                }
            }

            Assert.True(moved > 0, $"No {game} corner was moved, so this proved nothing.");
            _out.WriteLine($"{game}: {moved} shapes each had one corner moved, "
                         + $"{rewrittenInFull} corners had to be written out in full, "
                         + $"{grewBy} bytes in total.");
        }

        [SkippableFact]
        public void ACornerAskedToGoFurtherThanTheHardwareDescribesIsRefused()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum test project not configured");

            foreach (var (_, bytes) in MapModels.Of(TestRoms.Platinum, false).Take(1))
            {
                var file = NsbmdFile.Read(bytes, out _);
                Assert.NotNull(file);

                byte[] dl = file.DisplayList(0);
                var was = Corners(dl);
                var want = was.ToDictionary(c => c.Index, c => (c.X, c.Y, c.Z));
                want[0] = (100f, 0f, 0f);

                byte[] made = ShapeRewrite.WithCornersAt(dl, want, out string whynot);
                Assert.Null(made);
                Assert.Contains("out of the 4.12", whynot);
                return;
            }

            Assert.Fail("No Platinum map model was read, so this proved nothing.");
        }
    }
}
