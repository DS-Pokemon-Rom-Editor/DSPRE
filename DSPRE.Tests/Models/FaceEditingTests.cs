using DSPRE.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests.Models
{
    public class FaceEditingTests
    {
        private readonly ITestOutputHelper _out;
        public FaceEditingTests(ITestOutputHelper o) { _out = o; }

        private static List<string> Drawn(MapMesh mesh, int skip = -1)
        {
            var all = new List<string>();
            for (int f = 0; f < mesh.Faces.Count; f++)
            {
                if (f == skip) continue;
                var face = mesh.Faces[f];
                var look = face.Material >= 0 ? mesh.LookOf(face.Material) : null;
                var corners = new List<string>();
                for (int i = 0; i < face.Corners.Length; i++)
                {
                    var v = mesh.Vertices[face.Corners[i]];
                    int colour = face.Colour[i] >= 0 ? face.Colour[i] : look?.CornerColour ?? -1;
                    corners.Add($"{Math.Round(v.X * 4096)},{Math.Round(v.Y * 4096)},{Math.Round(v.Z * 4096)}"
                              + $"/{Math.Round(face.OnPicture[i].s * 16)},{Math.Round(face.OnPicture[i].t * 16)}"
                              + $"/{colour}/{face.Normal[i]}");
                }
                corners.Sort(StringComparer.Ordinal);
                all.Add(face.Material + ":" + string.Join(" ", corners));
            }
            all.Sort(StringComparer.Ordinal);
            return all;
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void TakingAFaceAwayLeavesEverythingElseDrawnTheSame(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            int maps = 0;
            foreach (var (name, bytes) in MapModels.Of(project, game == "HeartGold").Take(25))
            {
                var mesh = MapMesh.Read(bytes, out _);
                if (mesh == null || mesh.Faces.Count < 2) continue;

                int gone = mesh.Faces.Count / 2;
                int shape = mesh.Faces[gone].Shape;
                var expected = Drawn(mesh, skip: gone);
                var file = NsbmdFile.Read(bytes, out _);

                Assert.True(mesh.DeleteFace(gone));
                byte[] saved = mesh.Save(out string whynot);
                Assert.True(saved != null, $"{name}: {whynot}");

                var back = MapMesh.Read(saved, out whynot);
                Assert.True(back != null, $"{name}: {whynot}");
                Assert.Equal(expected, Drawn(back));

                var after = NsbmdFile.Read(saved, out _);
                for (int s = 0; s < file.Shapes.Count; s++)
                    if (s != shape) Assert.Equal(file.DisplayList(s), after.DisplayList(s));
                maps++;
            }
            Assert.True(maps > 0, "No map had a face taken away, so this proved nothing.");
            _out.WriteLine($"{game}: {maps} maps lost one face each and drew every other face the same.");
        }

        [SkippableFact]
        public void AFaceAddedAcrossCornersIsThereAfterSaving()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum test project not configured");
            var (name, bytes) = MapModels.Of(TestRoms.Platinum, false).First();
            var mesh = MapMesh.Read(bytes, out _);
            var like = mesh.Faces[0];
            int before = mesh.Faces.Count;

            int made = mesh.AddFace(new[] { like.Corners[2], like.Corners[1], like.Corners[0] }, 0, 32, 32);
            Assert.True(made >= 0);
            byte[] saved = mesh.Save(out string whynot);
            Assert.True(saved != null, whynot);

            var back = MapMesh.Read(saved, out whynot);
            Assert.True(back != null, whynot);
            Assert.Equal(before + 1, back.Faces.Count);
        }
    }
}
