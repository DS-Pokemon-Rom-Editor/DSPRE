using DSPRE.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests.Models
{
    public class MapMeshTests
    {
        private readonly ITestOutputHelper _out;
        public MapMeshTests(ITestOutputHelper o) { _out = o; }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void AMapNobodyEditedComesBackAsTheBytesItWasReadFrom(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            bool hgss = game == "HeartGold";
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            int read = 0;
            long faces = 0, vertices = 0, corners = 0;

            foreach (var (name, bytes) in MapModels.Of(project, hgss))
            {
                var mesh = MapMesh.Read(bytes, out string whynot);
                Assert.True(mesh != null, $"{name}: {whynot}");

                byte[] back = mesh.Save(out string why);
                Assert.True(back != null, $"{name}: {why}");
                Assert.True(bytes.SequenceEqual(back),
                            $"{name}: saving it untouched gave different bytes.");

                read++;
                faces += mesh.Faces.Count;
                vertices += mesh.Vertices.Count;
                corners += mesh.Vertices.Sum(v => v.Corners.Count);
            }

            Assert.True(read > 0, $"No {game} map was read, so this proved nothing.");
            _out.WriteLine($"{game}: {read} maps, {faces} faces, {vertices} corners "
                         + $"standing for {corners} in the shapes.");
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void MovingOneCornerRewritesOnlyTheShapesItIsIn(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            bool hgss = game == "HeartGold";
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            int moved = 0;

            foreach (var (name, bytes) in MapModels.Of(project, hgss).Take(40))
            {
                var mesh = MapMesh.Read(bytes, out _);
                if (mesh == null || mesh.Vertices.Count < 4) continue;

                var file = NsbmdFile.Read(bytes, out _);

                int pick = mesh.Vertices.Count / 2;
                var chosen = mesh.Vertices[pick];
                float wasX = chosen.X, wasY = chosen.Y, wasZ = chosen.Z;
                var itsShapes = chosen.Corners.Select(c => c.shape).Distinct().ToHashSet();
                mesh.Move(pick, wasX, wasY + 0.5f, wasZ);

                Assert.Equal(itsShapes.OrderBy(x => x), mesh.TouchedShapes);

                byte[] saved = mesh.Save(out string why);
                Assert.True(saved != null, $"{name}: {why}");

                var after = NsbmdFile.Read(saved, out string whynot);
                Assert.True(after != null, $"{name}: what was saved cannot be read, {whynot}");
                for (int i = 0; i < file.Shapes.Count; i++)
                    if (!itsShapes.Contains(i))
                        Assert.True(file.DisplayList(i).SequenceEqual(after.DisplayList(i)),
                                    $"{name}: shape {i} changed although the corner is not in it.");

                var again = MapMesh.Read(saved, out _);
                Assert.Contains(again.Vertices, v =>
                    Math.Abs(v.X - wasX) < 1f / 4096f &&
                    Math.Abs(v.Y - (wasY + 0.5f)) < 1f / 4096f &&
                    Math.Abs(v.Z - wasZ) < 1f / 4096f);

                moved++;
            }

            Assert.True(moved > 0, $"No {game} corner was moved, so this proved nothing.");
            _out.WriteLine($"{game}: {moved} maps had a corner moved.");
        }

        [SkippableFact]
        public void CornersOnTheSameSpotAreOneCornerSoAnEdgeDoesNotTear()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum test project not configured");

            int maps = 0, joinedMaps = 0;
            long standAlone = 0, joined = 0;

            foreach (var (_, bytes) in MapModels.Of(TestRoms.Platinum, false).Take(60))
            {
                var mesh = MapMesh.Read(bytes, out _);
                if (mesh == null || mesh.Vertices.Count == 0) continue;

                maps++;
                int here = mesh.Vertices.Count(v => v.Corners.Count > 1);
                if (here > 0) joinedMaps++;
                joined += here;
                standAlone += mesh.Vertices.Count - here;
            }

            Assert.True(maps > 0, "No Platinum map was read, so this proved nothing.");

            Assert.True(joined > 0, "Not one corner of any map is shared with another.");
            _out.WriteLine($"{joinedMaps} of {maps} maps share corners: {joined} shared, {standAlone} not.");
        }
    }
}
