using DSPRE.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests.Models
{
    public class DisplayListWalkTests
    {
        private readonly ITestOutputHelper _out;
        public DisplayListWalkTests(ITestOutputHelper o) { _out = o; }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void EveryMapModelHoldsTheCornersAndFacesItSaysItDoes(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            bool hgss = game == "HeartGold";
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            int models = 0, agreed = 0;
            long corners = 0, triangles = 0, quads = 0;
            var disagreed = new List<string>();
            var notAgreed = new List<string>();

            foreach (var (name, bytes) in MapModels.Of(project, hgss))
            {
                var file = NsbmdFile.Read(bytes, out _);
                if (file == null) continue;
                models++;

                int sawCorners = 0, sawTriangles = 0, sawQuads = 0;
                bool ok = true;

                for (int i = 0; i < file.Shapes.Count && ok; i++)
                {
                    var walk = DisplayListWalk.Read(file.DisplayList(i), out string whynot);
                    if (walk == null) { ok = false; disagreed.Add($"{name} shape {i}: {whynot}"); break; }

                    foreach (var run in walk.Runs)
                    {
                        int n = run.Corners.Count;
                        sawCorners += n;
                        switch (run.Kind)
                        {
                            case 0: sawTriangles += n / 3; break;
                            case 1: sawQuads += n / 4; break;
                            case 2: sawTriangles += Math.Max(0, n - 2); break;
                            case 3: sawQuads += Math.Max(0, (n - 2) / 2); break;
                        }
                    }
                }
                if (!ok) continue;

                if (sawCorners == file.SaysCorners && sawTriangles == file.SaysTriangles
                    && sawQuads == file.SaysQuads)
                {
                    agreed++;
                    corners += sawCorners; triangles += sawTriangles; quads += sawQuads;
                }
                else
                {
                    notAgreed.Add(name);
                    disagreed.Add($"{name}: read {sawCorners}/{sawTriangles}/{sawQuads}, "
                                + $"it says {file.SaysCorners}/{file.SaysTriangles}/{file.SaysQuads}");
                }
            }

            _out.WriteLine($"{game}: {agreed} of {models} models agree, "
                         + $"{corners} corners, {triangles} triangles, {quads} quads.");
            foreach (string s in disagreed) _out.WriteLine("  " + s);

            Assert.True(models > 0, $"No {game} map model was read, so this proved nothing.");

            var expected = hgss ? new[] { "0468", "0473", "0475", "0476" } : Array.Empty<string>();
            Assert.Equal(expected, notAgreed.OrderBy(x => x).ToArray());
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void EveryCornerSaysWhichCommandPutItDownAndWhere(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            bool hgss = game == "HeartGold";
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            var byKind = new Dictionary<DisplayListWalk.Put, int>();
            int checkedCorners = 0;

            foreach (var (name, bytes) in MapModels.Of(project, hgss).Take(120))
            {
                var file = NsbmdFile.Read(bytes, out _);
                if (file == null) continue;

                for (int i = 0; i < file.Shapes.Count; i++)
                {
                    byte[] dl = file.DisplayList(i);
                    var walk = DisplayListWalk.Read(dl, out _);
                    if (walk == null) continue;

                    foreach (var run in walk.Runs)
                        foreach (var corner in run.Corners)
                        {
                            Assert.InRange(corner.PutAt, 0, dl.Length - 4);
                            Assert.Equal((byte)corner.How, CommandBefore(dl, corner.PutAt));

                            byKind[corner.How] = byKind.TryGetValue(corner.How, out int had) ? had + 1 : 1;
                            checkedCorners++;
                        }
                }
            }

            Assert.True(checkedCorners > 0, "No corner was checked, so this proved nothing.");
            foreach (var kv in byKind.OrderByDescending(k => k.Value))
                _out.WriteLine($"{game}: {kv.Key} put down {kv.Value} corners.");
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void NotOneCornerPlacedByAShapeIsLeftOutOfTheReading(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            bool hgss = game == "HeartGold";
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            long placed = 0, kept = 0;
            int shapes = 0;

            foreach (var (name, bytes) in MapModels.Of(project, hgss))
            {
                var file = NsbmdFile.Read(bytes, out _);
                if (file == null) continue;

                for (int i = 0; i < file.Shapes.Count; i++)
                {
                    byte[] dl = file.DisplayList(i);
                    var walk = DisplayListWalk.Read(dl, out _);
                    if (walk == null) continue;

                    int here = CornerCommands(dl);
                    int mine = walk.Runs.Sum(r => r.Corners.Count);
                    Assert.True(here == mine,
                        $"{name} shape {i}: the bytes place {here} corners and the reading kept {mine}.");

                    placed += here; kept += mine; shapes++;
                }
            }

            Assert.True(shapes > 0, $"No {game} shape was read, so this proved nothing.");
            _out.WriteLine($"{game}: {shapes} shapes, every one of {placed} corners kept.");
        }

        private static int CornerCommands(byte[] dl)
        {
            int at = 0, n = 0;
            while (at < dl.Length)
            {
                var ops = new byte[4];
                for (int k = 0; k < 4; k++) ops[k] = at + k < dl.Length ? dl[at + k] : (byte)0;
                at += 4;
                for (int k = 0; k < 4; k++)
                {
                    int words = GxDisplayList.TryParamWords(ops[k]);
                    if (words < 0) return n;
                    if (ops[k] >= 0x23 && ops[k] <= 0x28) n++;
                    at += words * 4;
                }
            }
            return n;
        }

        private static byte CommandBefore(byte[] dl, int paramAt)
        {
            int at = 0;
            while (at < dl.Length)
            {
                var ops = new byte[4];
                for (int k = 0; k < 4; k++) ops[k] = at + k < dl.Length ? dl[at + k] : (byte)0;
                at += 4;
                for (int k = 0; k < 4; k++)
                {
                    int words = GxDisplayList.TryParamWords(ops[k]);
                    if (words < 0) return 0;
                    if (words > 0 && at == paramAt) return ops[k];
                    at += words * 4;
                }
            }
            return 0;
        }
    }
}
