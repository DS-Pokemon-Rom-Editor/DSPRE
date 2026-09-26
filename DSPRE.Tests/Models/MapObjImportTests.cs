using DSPRE.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests.Models
{
    public class MapObjImportTests
    {
        private readonly ITestOutputHelper _out;
        public MapObjImportTests(ITestOutputHelper o) { _out = o; }

        private static HashSet<string> Drawn(MapMesh mesh, Func<int, string> pictureOf)
        {
            var all = new HashSet<string>();
            foreach (var face in mesh.Faces)
            {
                var look = face.Material >= 0 ? mesh.LookOf(face.Material) : null;
                for (int i = 0; i < face.Corners.Length; i++)
                {
                    var v = mesh.Vertices[face.Corners[i]];
                    int colour = face.Colour[i] >= 0 ? face.Colour[i] : look?.CornerColour ?? -1;
                    all.Add($"{Math.Round(v.X * 4096)},{Math.Round(v.Y * 4096)},{Math.Round(v.Z * 4096)}"
                          + $"/{Math.Round(face.OnPicture[i].s * 16)},{Math.Round(face.OnPicture[i].t * 16)}"
                          + $"/{pictureOf(face.Material)}/{look?.Key}/{colour}/{face.Normal[i]}");
                }
            }
            return all;
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void AMapWrittenOutAndBroughtBackIsDrawnTheSame(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            string dir = Path.Combine(Path.GetTempPath(), "dspre-objmap-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            int maps = 0;
            try
            {
                foreach (var (name, bytes) in MapModels.Of(project, game == "HeartGold").Take(20))
                {
                    var mesh = MapMesh.Read(bytes, out _);
                    if (mesh == null || mesh.Faces.Count == 0) continue;

                    Func<int, string> pictureOf = m => { int p = mesh.PictureFor(m); return p >= 0 ? mesh.NameOfPicture(p) : $"material{m}"; };
                    var paints = new Dictionary<int, ObjWrite.Paint>();
                    foreach (int m in mesh.Faces.Select(f => f.Material).Distinct())
                    {
                        int c = mesh.ColoursFor(m);
                        paints[m] = new ObjWrite.Paint { Name = pictureOf(m), Width = 64, Height = 32, Palette = c >= 0 ? mesh.NameOfColours(c) : null };
                    }

                    string path = Path.Combine(dir, name + ".obj");
                    var written = ObjWrite.To(path, mesh, paints);
                    Assert.True(written.Whynot == null, written.Whynot);

                    Assert.Equal(MapObjImport.Measure.Dspre, MapObjImport.Guess(path));
                    byte[] model = MapObjImport.Build(path, MapObjImport.Measure.Dspre, _ => (64, 32), out string whynot, out _);
                    Assert.True(model != null, $"{name}: {whynot}");

                    var back = MapMesh.Read(model, out whynot);
                    Assert.True(back != null, $"{name}: {whynot}");
                    Func<int, string> backPicture = m => { int p = back.PictureFor(m); return p >= 0 ? back.NameOfPicture(p) : $"material{m}"; };

                    var had = Drawn(mesh, pictureOf);
                    var now = Drawn(back, backPicture);
                    Assert.True(had.SetEquals(now),
                        $"{name}: {now.Except(had).Count()} corners drawn differently, {had.Except(now).Count()} lost.");
                    maps++;
                }
            }
            finally { Directory.Delete(dir, true); }

            Assert.True(maps > 0, "No map went out and came back, so this proved nothing.");
            _out.WriteLine($"{game}: {maps} maps went out as OBJ and came back drawn the same.");
        }

        [Fact]
        public void AMapStudioObjIsTurnedUpright()
        {
            string path = Path.Combine(Path.GetTempPath(), "dspre-pdsobj-" + Guid.NewGuid().ToString("N") + ".obj");
            try
            {
                File.WriteAllText(path, "v 1 1 2\nv 2 1 2\nv 2 2 2\nv 1 2 2\nvt 0 0\nvt 1 0\nvt 1 1\nvt 0 1\nusemtl grass\nf 1/1 2/2 3/3 4/4\n");
                Assert.Equal(MapObjImport.Measure.MapStudio, MapObjImport.Guess(path));
                byte[] model = MapObjImport.Build(path, MapObjImport.Measure.MapStudio, _ => (32, 32), out string whynot, out _);
                Assert.True(model != null, whynot);

                var mesh = MapMesh.Read(model, out whynot);
                Assert.True(mesh != null, whynot);
                Assert.All(mesh.Vertices, v => Assert.Equal(0.5f, v.Y, 4));
                Assert.Equal(0.25f, mesh.Vertices.Min(v => v.X), 4);
                Assert.Equal(-0.5f, mesh.Vertices.Min(v => v.Z), 4);
            }
            finally { File.Delete(path); File.Delete(Path.ChangeExtension(path, ".mtl")); }
        }
    }
}
