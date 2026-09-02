using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Writing a model file the DS can read out of a mesh, and reading it back to see the same shape.
    /// </summary>
    public class NsbmdWriterTests
    {
        private readonly ITestOutputHelper _out;
        public NsbmdWriterTests(ITestOutputHelper o) { _out = o; }

        // ── a mesh to write ───────────────────────────────────────────────────────────────────────

        /// <summary>A cube, written the way any 3D program writes one.</summary>
        private static string CubeObj(float size = 1f) => string.Join("\n", new[]
        {
            "# a cube",
            "mtllib cube.mtl",
            $"v -{size} -{size} -{size}", $"v {size} -{size} -{size}",
            $"v {size} {size} -{size}",   $"v -{size} {size} -{size}",
            $"v -{size} -{size} {size}",  $"v {size} -{size} {size}",
            $"v {size} {size} {size}",    $"v -{size} {size} {size}",
            "vn 0 0 -1", "vn 0 0 1", "vn -1 0 0", "vn 1 0 0", "vn 0 -1 0", "vn 0 1 0",
            "vt 0 0", "vt 1 0", "vt 1 1", "vt 0 1",
            "usemtl paint",
            "f 1/1/1 2/2/1 3/3/1 4/4/1",
            "f 5/1/2 8/4/2 7/3/2 6/2/2",
            "f 1/1/3 4/4/3 8/3/3 5/2/3",
            "f 2/1/4 6/2/4 7/3/4 3/4/4",
            "f 1/1/5 5/2/5 6/3/5 2/4/5",
            "f 4/1/6 3/2/6 7/3/6 8/4/6",
        });

        private static string WriteObj(string dir, string obj, bool withMtl = true, bool withPng = false)
        {
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "cube.obj");
            File.WriteAllText(path, obj);
            if (withMtl)
                File.WriteAllText(Path.Combine(dir, "cube.mtl"), string.Join("\n", new[]
                {
                    "newmtl paint", "Kd 0.8 0.2 0.2", "d 1.0",
                    withPng ? "map_Kd paint.png" : "# no picture",
                }));
            if (withPng)
                File.WriteAllBytes(Path.Combine(dir, "paint.png"), TinyPng(16, 16));
            return path;
        }

        /// <summary>A small indexed PNG, so a texture can be read without a drawing library.</summary>
        private static byte[] TinyPng(int w, int h)
        {
            var indices = new byte[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) indices[y * w + x] = (byte)((x / 4 + y / 4) % 4);
            var palette = new uint[] { 0xFFFF0000, 0xFF00FF00, 0xFF0000FF, 0xFFFFFF00 };
            return DSPRE.Avalonia.IndexedPng.Write(indices, palette, w, h);
        }

        private static string Scratch([System.Runtime.CompilerServices.CallerMemberName] string who = "")
        {
            string dir = Path.Combine(Path.GetTempPath(), "dspre_obj_" + who);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            return dir;
        }

        // ── reading the mesh ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void ACubeIsReadAsEightCornersAndSixFaces()
        {
            var mesh = ObjMesh.Read(WriteObj(Scratch(), CubeObj()), out string why);
            Assert.Null(why);
            Assert.Equal(8, mesh.Positions.Count);
            Assert.Equal(6, mesh.Normals.Count);
            Assert.Equal(4, mesh.TexCoords.Count);
            Assert.Equal(6, mesh.Faces.Count);
            Assert.Equal(12, mesh.Triangles);            // six four-sided faces make twelve triangles
            Assert.Single(mesh.Materials);
            Assert.Equal("paint", mesh.Materials[0].Name);
            Assert.Equal(0.8f, mesh.Materials[0].Red, 2);
        }

        [Fact]
        public void APictureUpsideDownInAnObjComesOutTheRightWayUp()
        {
            // OBJ counts up from the bottom of a picture and the DS counts down from the top.
            var mesh = ObjMesh.Read(WriteObj(Scratch(), CubeObj()), out _);
            Assert.Equal(1f, mesh.TexCoords[0].V, 3);     // written as 0
            Assert.Equal(0f, mesh.TexCoords[2].V, 3);     // written as 1
        }

        [Fact]
        public void AnObjWithNoFacesIsRefusedRatherThanWrittenEmpty()
        {
            string dir = Scratch();
            File.WriteAllText(Path.Combine(dir, "cube.obj"), "v 0 0 0\nv 1 0 0\nv 0 1 0\n");
            var mesh = ObjMesh.Read(Path.Combine(dir, "cube.obj"), out string why);
            Assert.Null(mesh);
            Assert.Contains("no faces", why);
        }

        [Fact]
        public void AFaceNamingACornerThatIsNotThereIsLeftOutAndSaidSo()
        {
            string dir = Scratch();
            File.WriteAllText(Path.Combine(dir, "cube.obj"),
                "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\nf 1 2 99\n");
            var mesh = ObjMesh.Read(Path.Combine(dir, "cube.obj"), out string why);
            Assert.Null(why);
            Assert.Single(mesh.Faces);
            Assert.Contains(mesh.Notes, n => n.Contains("left out"));
        }

        // ── writing the model ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void ACubeBecomesAModelFileThatSaysWhatItHolds()
        {
            var mesh = ObjMesh.Read(WriteObj(Scratch(), CubeObj()), out _);
            var made = NsbmdWriter.Build(mesh, null);
            Assert.Null(made.Whynot);
            Assert.Equal(12, made.Triangles);
            Assert.Equal(1, made.Materials);
            Assert.Equal(1, made.Shapes);

            byte[] f = made.Bytes;
            Assert.Equal("BMD0", Tag(f, 0));
            Assert.Equal(0xFEFF, U16(f, 4));
            Assert.Equal(f.Length, (int)U32(f, 8));
            Assert.Equal(16, U16(f, 12));
            Assert.Equal(1, U16(f, 14));                 // no pictures, so only the model block
            Assert.Equal("MDL0", Tag(f, (int)U32(f, 16)));
        }

        [Fact]
        public void AMeshWithAPictureGetsBothBlocks()
        {
            string dir = Scratch();
            var mesh = ObjMesh.Read(WriteObj(dir, CubeObj(), withPng: true), out _);
            Assert.NotNull(mesh.Materials[0].TexturePath);

            var tex = ReadTexture(mesh.Materials[0]);
            Assert.Null(tex.Whynot);
            Assert.Equal(16, tex.Width);
            Assert.Equal(DsTexture.Kind.SixteenColours, tex.Format);   // four colours fit in sixteen

            var made = NsbmdWriter.Build(mesh, new[] { tex });
            Assert.Null(made.Whynot);
            byte[] f = made.Bytes;
            Assert.Equal(2, U16(f, 14));
            Assert.Equal("MDL0", Tag(f, (int)U32(f, 16)));
            Assert.Equal("TEX0", Tag(f, (int)U32(f, 20)));
            Assert.Equal(f.Length, (int)U32(f, 8));
        }

        [Fact]
        public void AMeshFurtherOutThanTheHardwareCountsIsWrittenSmallerAndSaysSo()
        {
            // The hardware keeps a distance in twelve bits after the point, so nothing may sit further
            // than about eight units out without being scaled down first.
            var mesh = ObjMesh.Read(WriteObj(Scratch(), CubeObj(64f)), out _);
            var made = NsbmdWriter.Build(mesh, null);
            Assert.Null(made.Whynot);
            Assert.Contains(made.Notes, n => n.Contains("times smaller"));
        }

        [Fact]
        public void AMeshTooBigToDrawIsRefusedWithItsSize()
        {
            var mesh = new ObjMesh();
            mesh.Materials.Add(new ObjMesh.Material { Name = "m" });
            mesh.Positions.Add(new ObjMesh.Vec3 { X = 0, Y = 0, Z = 0 });
            mesh.Positions.Add(new ObjMesh.Vec3 { X = 1, Y = 0, Z = 0 });
            mesh.Positions.Add(new ObjMesh.Vec3 { X = 0, Y = 1, Z = 0 });
            for (int i = 0; i <= NsbmdWriter.MostTriangles; i++)
                mesh.Faces.Add(new ObjMesh.Face
                {
                    Material = 0,
                    Corners = new List<ObjMesh.Corner>
                    {
                        new ObjMesh.Corner { Position = 0, Normal = -1, TexCoord = -1 },
                        new ObjMesh.Corner { Position = 1, Normal = -1, TexCoord = -1 },
                        new ObjMesh.Corner { Position = 2, Normal = -1, TexCoord = -1 },
                    },
                });

            var made = NsbmdWriter.Build(mesh, null);
            Assert.NotNull(made.Whynot);
            Assert.Contains(NsbmdWriter.MostTriangles.ToString(), made.Whynot);
            Assert.Null(made.Bytes);
        }

        [Fact]
        public void OneOfSomethingIsNotCalledOneSomethings()
        {
            var mesh = ObjMesh.Read(WriteObj(Scratch(), CubeObj()), out _);
            var made = NsbmdWriter.Build(mesh, null);
            Assert.Contains("1 shape,", made.Summary);
            Assert.Contains("1 material,", made.Summary);
            Assert.Contains("0 pictures.", made.Summary);
            Assert.DoesNotContain("1 shapes", made.Summary);
        }

        // ── pictures ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void APictureThatIsNotAPowerOfTwoIsRefusedWithTheSizeThatWouldFit()
        {
            var rgba = new byte[20 * 30 * 4];
            var t = DsTexture.From(rgba, 20, 30, "odd");
            Assert.NotNull(t.Whynot);
            Assert.Contains("32 by 32", t.Whynot);
        }

        [Theory]
        [InlineData(10, DsTexture.Kind.SixteenColours)]
        [InlineData(100, DsTexture.Kind.TwoHundredFiftySix)]
        [InlineData(1000, DsTexture.Kind.StraightColour)]
        public void HowManyColoursAPictureHasDecidesHowItIsWritten(int colours, DsTexture.Kind want)
        {
            const int w = 64, h = 64;
            var rgba = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++)
            {
                int n = i % colours;
                rgba[i * 4] = (byte)((n % 32) * 8);
                rgba[i * 4 + 1] = (byte)((n / 32 % 32) * 8);
                rgba[i * 4 + 2] = (byte)((n / 1024) * 8);
                rgba[i * 4 + 3] = 255;
            }
            var t = DsTexture.From(rgba, w, h, "test");
            Assert.Null(t.Whynot);
            Assert.Equal(want, t.Format);
        }

        [Fact]
        public void TheSizeOfAPictureIsWrittenAsHowManyTimesItDoublesFromEight()
        {
            Assert.Equal(0, DsTexture.SizeCode(8));
            Assert.Equal(1, DsTexture.SizeCode(16));
            Assert.Equal(3, DsTexture.SizeCode(64));
            Assert.Equal(7, DsTexture.SizeCode(1024));
        }

        [Fact]
        public void ASeeThroughPictureKeepsItsFirstColourClearAndSaysSoInTheWordTheHardwareReads()
        {
            const int w = 32, h = 32;
            var rgba = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++)
            {
                rgba[i * 4] = 200; rgba[i * 4 + 1] = 40; rgba[i * 4 + 2] = 40;
                rgba[i * 4 + 3] = (byte)(i % 3 == 0 ? 0 : 255);
            }
            var t = DsTexture.From(rgba, w, h, "holes");
            Assert.Null(t.Whynot);
            Assert.True(t.FirstColourIsClear);
            Assert.Equal(0u, t.Colours[0]);                       // the clear slot is left empty
            Assert.NotEqual(0u, (t.ImageParam(0) >> 29) & 1);     // and the word says so
        }

        [Fact]
        public void ModelsAreWrittenToDiskSoAnotherReaderCanBeAskedWhetherTheyAreRight()
        {
            string outDir = Environment.GetEnvironmentVariable("DSPRE_OBJ_OUT");
            if (string.IsNullOrEmpty(outDir)) return;
            Directory.CreateDirectory(outDir);

            string dir = Scratch();
            var plain = ObjMesh.Read(WriteObj(dir, CubeObj()), out _);
            var made = NsbmdWriter.Build(plain, null);
            Assert.Null(made.Whynot);
            File.WriteAllBytes(Path.Combine(outDir, "cube_plain.nsbmd"), made.Bytes);
            _out.WriteLine($"cube_plain.nsbmd {made.Bytes.Length} bytes: {made.Summary}");

            string dir2 = Path.Combine(Scratch(), "painted");
            var painted = ObjMesh.Read(WriteObj(dir2, CubeObj(), withPng: true), out _);
            var tex = ReadTexture(painted.Materials[0]);
            var made2 = NsbmdWriter.Build(painted, new[] { tex });
            Assert.Null(made2.Whynot);
            File.WriteAllBytes(Path.Combine(outDir, "cube_painted.nsbmd"), made2.Bytes);
            _out.WriteLine($"cube_painted.nsbmd {made2.Bytes.Length} bytes: {made2.Summary}");

            var big = ObjMesh.Read(WriteObj(Path.Combine(Scratch(), "big"), CubeObj(64f)), out _);
            var made3 = NsbmdWriter.Build(big, null);
            File.WriteAllBytes(Path.Combine(outDir, "cube_big.nsbmd"), made3.Bytes);
            _out.WriteLine($"cube_big.nsbmd {made3.Bytes.Length} bytes: {made3.Summary}");
        }

        /// <summary>
        /// Turns every OBJ in one folder into a model file in another. Used by the round trip that
        /// takes the ROM's own models out and puts them back.
        /// </summary>
        [Fact]
        public void EveryObjInAFolderIsTurnedIntoAModel()
        {
            string from = Environment.GetEnvironmentVariable("DSPRE_OBJ_IN");
            string to = Environment.GetEnvironmentVariable("DSPRE_OBJ_OUT");
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return;
            Directory.CreateDirectory(to);

            int made = 0, refused = 0;
            var reasons = new Dictionary<string, int>();
            foreach (string obj in Directory.GetFiles(from, "*.obj").OrderBy(x => x))
            {
                var mesh = ObjMesh.Read(obj, out string why);
                if (mesh == null) { refused++; Count(reasons, why); continue; }

                var textures = new List<DsTexture>();
                foreach (var m in mesh.Materials.Where(x => x.TexturePath != null))
                {
                    byte[] png = File.ReadAllBytes(m.TexturePath);
                    if (!DSPRE.Avalonia.AnyPng.TryReadRgba(png, out var rgba, out int w, out int h, out _))
                        continue;
                    var t = DsTexture.From(rgba, w, h, m.Name);
                    if (t.Whynot == null) textures.Add(t);
                }

                var built = NsbmdWriter.Build(mesh, textures);
                if (built.Whynot != null) { refused++; Count(reasons, built.Whynot); continue; }
                File.WriteAllBytes(Path.Combine(to,
                    Path.GetFileNameWithoutExtension(obj) + ".nsbmd"), built.Bytes);
                made++;
            }
            _out.WriteLine($"{made} models written, {refused} refused.");
            foreach (var kv in reasons.OrderByDescending(k => k.Value).Take(6))
                _out.WriteLine($"  {kv.Value} x {kv.Key}");
        }

        private static void Count(Dictionary<string, int> into, string why)
        {
            string kind = System.Text.RegularExpressions.Regex.Replace(why ?? "?", @"\d+", "N");
            into[kind] = into.TryGetValue(kind, out int n) ? n + 1 : 1;
        }

        // ── helpers ───────────────────────────────────────────────────────────────────────────────

        private static DsTexture ReadTexture(ObjMesh.Material m)
        {
            byte[] file = File.ReadAllBytes(m.TexturePath);
            Assert.True(DSPRE.Avalonia.AnyPng.TryReadRgba(file, out byte[] rgba, out int w, out int h,
                                                          out string why), why);
            return DsTexture.From(rgba, w, h, m.Name);
        }

        private static string Tag(byte[] d, int at) => Encoding.ASCII.GetString(d, at, 4);
        private static int U16(byte[] d, int at) => d[at] | (d[at + 1] << 8);
        private static uint U32(byte[] d, int at) =>
            (uint)(d[at] | (d[at + 1] << 8) | (d[at + 2] << 16) | (d[at + 3] << 24));
    }
}
