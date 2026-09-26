using DSPRE.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests.Models
{
    public class ObjWriteTests
    {
        private readonly ITestOutputHelper _out;
        public ObjWriteTests(ITestOutputHelper o) { _out = o; }

        private static string Scratch()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dspre_obj_write");
            Directory.CreateDirectory(dir);
            return dir;
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void AMapWrittenOutAndReadBackHasTheSameCornersAndFaces(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            bool hgss = game == "HeartGold";
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            string dir = Scratch();
            int done = 0;
            long faces = 0;

            foreach (var (name, bytes) in MapModels.Of(project, hgss).Take(40))
            {
                var mesh = MapMesh.Read(bytes, out _);
                if (mesh == null || mesh.Faces.Count == 0) continue;

                string path = Path.Combine(dir, $"{game}-{name}.obj");
                var written = ObjWrite.To(path, mesh, null);
                Assert.True(written.Whynot == null, $"{name}: {written.Whynot}");
                Assert.Equal(mesh.Faces.Count, written.Faces);

                var back = ObjMesh.Read(path, out string why);
                Assert.True(back != null, $"{name}: what was written cannot be read back, {why}");

                Assert.Equal(mesh.Vertices.Count, back.Positions.Count);
                Assert.Equal(mesh.Faces.Count, back.Faces.Count);

                for (int i = 0; i < mesh.Vertices.Count; i++)
                {
                    Assert.True(Math.Abs(back.Positions[i].X - mesh.Vertices[i].X) < 1e-3f
                             && Math.Abs(back.Positions[i].Y - mesh.Vertices[i].Y) < 1e-3f
                             && Math.Abs(back.Positions[i].Z - mesh.Vertices[i].Z) < 1e-3f,
                        $"{name}: corner {i} came back somewhere else.");
                }

                for (int i = 0; i < mesh.Faces.Count; i++)
                    Assert.Equal(mesh.Faces[i].Corners.Length, back.Faces[i].Corners.Count);

                faces += written.Faces;
                done++;
            }

            Assert.True(done > 0, $"No {game} map was written out, so this proved nothing.");
            _out.WriteLine($"{game}: {done} maps written out and read back, {faces} faces.");
        }

        [SkippableFact]
        public void TheNamesThatFindAMapsPicturesAreKeptExactly()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum test project not configured");

            var (name, bytes) = MapModels.Of(TestRoms.Platinum, false).First();
            var mesh = MapMesh.Read(bytes, out _);

            var paints = new Dictionary<int, ObjWrite.Paint>();
            foreach (var face in mesh.Faces)
            {
                if (paints.ContainsKey(face.Material)) continue;
                int picture = mesh.PictureFor(face.Material);
                paints[face.Material] = new ObjWrite.Paint
                {
                    Name = picture >= 0 ? mesh.NameOfPicture(picture) : $"material{face.Material}",
                    Width = 64, Height = 64,
                    Picture = picture >= 0 ? mesh.NameOfPicture(picture) + ".png" : null,
                };
            }

            string path = Path.Combine(Scratch(), "named.obj");
            var written = ObjWrite.To(path, mesh, paints);
            Assert.Null(written.Whynot);

            string obj = File.ReadAllText(path);
            string mtl = File.ReadAllText(Path.ChangeExtension(path, ".mtl"));

            foreach (var paint in paints.Values)
            {
                Assert.Contains($"usemtl {paint.Name}", obj);
                Assert.Contains($"newmtl {paint.Name}", mtl);
                if (paint.Picture != null) Assert.Contains($"map_Kd {paint.Picture}", mtl);
            }

            _out.WriteLine($"{name}: {paints.Count} names kept, "
                         + $"{string.Join(", ", paints.Values.Select(p => p.Name))}");
        }

        [SkippableFact]
        public void AMapWithNothingToWriteIsRefusedRatherThanWrittenEmpty()
        {
            var empty = (MapMesh)Activator.CreateInstance(typeof(MapMesh), nonPublic: true);
            var written = ObjWrite.To(Path.Combine(Scratch(), "empty.obj"), empty, null);
            Assert.NotNull(written.Whynot);
            Assert.False(File.Exists(Path.Combine(Scratch(), "empty.obj")));
        }
    }
}
