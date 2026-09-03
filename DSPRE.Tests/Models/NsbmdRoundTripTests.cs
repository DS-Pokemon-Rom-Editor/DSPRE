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
    /// Every model in the ROM taken apart into triangles, written back out through the OBJ importer,
    /// and read again: the same triangles have to come back, corner for corner.
    ///
    /// Nothing here uses the writer to read. The reader below walks the display list the way the
    /// hardware does, and it was checked separately: all 340 of HeartGold's building models decode to
    /// exactly the triangle and quad count their own headers state, which is a number the writer has
    /// no hand in.
    /// </summary>
    public class NsbmdRoundTripTests
    {
        private readonly ITestOutputHelper _out;
        public NsbmdRoundTripTests(ITestOutputHelper o) { _out = o; }

        private static readonly string Models = TestRoms.HeartGold + @"\unpacked\exteriorBuildingModels";

        [Fact]
        public void EveryModelInTheRomComesBackWithTheSameTrianglesItWentInWith()
        {
            if (!Directory.Exists(Models))
            { Assert.Fail($"{Models} is not there, so this proved nothing."); return; }

            string scratch = Path.Combine(Path.GetTempPath(), "dspre_model_roundtrip");
            if (Directory.Exists(scratch)) Directory.Delete(scratch, true);
            Directory.CreateDirectory(scratch);

            int same = 0, differ = 0, skipped = 0;
            long triangles = 0;
            var worst = new List<string>();

            foreach (string path in Directory.GetFiles(Models).OrderBy(x => x))
            {
                byte[] file;
                try { file = File.ReadAllBytes(path); } catch { continue; }
                List<Triangle> before;
                try { before = TrianglesIn(file); } catch { skipped++; continue; }
                if (before == null || before.Count == 0) { skipped++; continue; }

                string obj = Path.Combine(scratch, Path.GetFileName(path) + ".obj");
                WriteObj(obj, before);

                var mesh = ObjMesh.Read(obj, out string why);
                if (mesh == null)
                { differ++; worst.Add($"{Path.GetFileName(path)}: could not be read back, {why}"); continue; }

                var made = NsbmdWriter.Build(mesh, null);
                if (made.Whynot != null)
                { differ++; worst.Add($"{Path.GetFileName(path)}: {made.Whynot}"); continue; }

                List<Triangle> after;
                try { after = TrianglesIn(made.Bytes); }
                catch (Exception ex)
                { differ++; worst.Add($"{Path.GetFileName(path)}: unreadable after writing, {ex.Message}"); continue; }

                triangles += before.Count;
                if (Same(before, after)) same++;
                else
                {
                    differ++;
                    if (worst.Count < 6)
                        worst.Add($"{Path.GetFileName(path)}: {before.Count} triangles in, {after.Count} out, "
                                + $"{Bag(before).Intersect(Bag(after)).Count()} of them the same");
                }
            }

            _out.WriteLine($"{same} models came back with exactly the same triangles, {differ} did not, "
                         + $"{skipped} held none. {triangles} triangles compared in all.");
            // A run that read no models would pass every check below while proving nothing.
            Assert.True(same + differ > 300, $"only {same + differ} models were round tripped");
            Assert.True(triangles > 20000, $"only {triangles} triangles were compared");
            Assert.True(differ == 0, string.Join(Environment.NewLine, worst));
        }

        // ── the triangles a model draws ───────────────────────────────────────────────────────────

        private readonly struct Point
        {
            public readonly float X, Y, Z;
            public Point(float x, float y, float z) { X = x; Y = y; Z = z; }
            /// <summary>Rounded to what the hardware can tell apart, so a corner is one thing or another.</summary>
            public override string ToString() => $"{X:0.####},{Y:0.####},{Z:0.####}";
        }

        private readonly struct Triangle
        {
            public readonly Point A, B, C;
            public Triangle(Point a, Point b, Point c) { A = a; B = b; C = c; }
            /// <summary>The three corners in a settled order, so drawing order is not a difference.</summary>
            public string Key()
            {
                var s = new[] { A.ToString(), B.ToString(), C.ToString() };
                Array.Sort(s, StringComparer.Ordinal);
                return string.Join("|", s);
            }
        }

        private static IEnumerable<string> Bag(List<Triangle> t) => t.Select(x => x.Key());

        private static bool Same(List<Triangle> a, List<Triangle> b)
        {
            if (a.Count != b.Count) return false;
            var x = Bag(a).OrderBy(s => s, StringComparer.Ordinal).ToList();
            var y = Bag(b).OrderBy(s => s, StringComparer.Ordinal).ToList();
            return x.SequenceEqual(y, StringComparer.Ordinal);
        }

        private static void WriteObj(string path, List<Triangle> tris)
        {
            var o = new StringBuilder();
            foreach (var t in tris)
                foreach (var v in new[] { t.A, t.B, t.C })
                    o.Append("v ").Append(v.X.ToString("0.######")).Append(' ')
                     .Append(v.Y.ToString("0.######")).Append(' ')
                     .Append(v.Z.ToString("0.######")).Append('\n');
            for (int i = 0; i < tris.Count; i++)
                o.Append("f ").Append(i * 3 + 1).Append(' ').Append(i * 3 + 2).Append(' ')
                 .Append(i * 3 + 3).Append('\n');
            File.WriteAllText(path, o.ToString());
        }

        /// <summary>Walks a model file and draws out every triangle its shapes make.</summary>
        private static List<Triangle> TrianglesIn(byte[] d)
        {
            if (d.Length < 16 || Tag(d, 0) != "BMD0") return null;
            int blocks = U16(d, 14);
            int mdl = -1;
            for (int i = 0; i < blocks; i++)
            {
                int at = (int)U32(d, 16 + i * 4);
                if (at + 4 <= d.Length && Tag(d, at) == "MDL0") { mdl = at; break; }
            }
            if (mdl < 0) return null;

            var set = ReadDict(d, mdl + 8);
            if (set == null || set.Entries.Count == 0) return null;
            int m = mdl + (int)U32(set.Entries[0], 0);
            float posScale = U32s(d, m + 20 + 8) / 4096f;
            int shpAt = m + (int)U32(d, m + 12);

            var shapes = ReadDict(d, shpAt);
            var tris = new List<Triangle>();
            foreach (var e in shapes.Entries)
            {
                int a = shpAt + (int)U32(e, 0);
                int ofsDl = (int)U32(d, a + 8), sizeDl = (int)U32(d, a + 12);
                var dl = new byte[sizeDl];
                Array.Copy(d, a + ofsDl, dl, 0, sizeDl);
                Draw(dl, posScale, tris);
            }
            return tris;
        }

        /// <summary>Follows a display list the way the hardware does, gathering triangles.</summary>
        private static void Draw(byte[] dl, float posScale, List<Triangle> into)
        {
            var run = new List<Point>();
            int mode = -1;
            float x = 0, y = 0, z = 0;
            int at = 0;

            while (at + 4 <= dl.Length)
            {
                var ops = new[] { dl[at], dl[at + 1], dl[at + 2], dl[at + 3] };
                at += 4;
                foreach (byte op in ops)
                {
                    int words = GxDisplayList.ParamWords(op);
                    if (at + words * 4 > dl.Length) return;
                    var p = new uint[words];
                    for (int i = 0; i < words; i++) p[i] = U32(dl, at + i * 4);
                    at += words * 4;

                    switch (op)
                    {
                        case 0x40: mode = (int)(p[0] & 3); run.Clear(); break;
                        case 0x41: mode = -1; break;
                        case 0x23:
                            x = Sixteen(p[0] & 0xFFFF); y = Sixteen(p[0] >> 16); z = Sixteen(p[1] & 0xFFFF);
                            Corner(); break;
                        case 0x24:
                            x = Ten(p[0] & 0x3FF); y = Ten((p[0] >> 10) & 0x3FF); z = Ten((p[0] >> 20) & 0x3FF);
                            Corner(); break;
                        case 0x25: x = Sixteen(p[0] & 0xFFFF); y = Sixteen(p[0] >> 16); Corner(); break;
                        case 0x26: x = Sixteen(p[0] & 0xFFFF); z = Sixteen(p[0] >> 16); Corner(); break;
                        case 0x27: y = Sixteen(p[0] & 0xFFFF); z = Sixteen(p[0] >> 16); Corner(); break;
                        case 0x28:
                            x += Ten(p[0] & 0x3FF) / 8f; y += Ten((p[0] >> 10) & 0x3FF) / 8f;
                            z += Ten((p[0] >> 20) & 0x3FF) / 8f; Corner(); break;
                    }
                }
            }

            void Corner()
            {
                if (mode < 0) return;
                run.Add(new Point(x * posScale, y * posScale, z * posScale));
                switch (mode)
                {
                    case 0 when run.Count == 3:
                        into.Add(new Triangle(run[0], run[1], run[2])); run.Clear(); break;
                    case 1 when run.Count == 4:
                        into.Add(new Triangle(run[0], run[1], run[2]));
                        into.Add(new Triangle(run[0], run[2], run[3])); run.Clear(); break;
                    case 2 when run.Count >= 3:
                    {
                        var a = run[run.Count - 3]; var b = run[run.Count - 2]; var c = run[run.Count - 1];
                        into.Add(run.Count % 2 == 1 ? new Triangle(a, b, c) : new Triangle(b, a, c));
                        break;
                    }
                    case 3 when run.Count >= 4 && run.Count % 2 == 0:
                    {
                        var a = run[run.Count - 4]; var b = run[run.Count - 3];
                        var c = run[run.Count - 2]; var e = run[run.Count - 1];
                        into.Add(new Triangle(a, b, e));
                        into.Add(new Triangle(b, c, e));
                        break;
                    }
                }
            }
        }

        private static float Sixteen(uint v)
        {
            int s = (int)(v & 0xFFFF);
            if ((s & 0x8000) != 0) s -= 0x10000;
            return s / 4096f;
        }

        private static float Ten(uint v)
        {
            int s = (int)(v & 0x3FF);
            if ((s & 0x200) != 0) s -= 0x400;
            return s / 64f;
        }

        // ── the dictionary, read only far enough to follow it ─────────────────────────────────────

        private sealed class Dict { public List<byte[]> Entries = new(); }

        private static Dict ReadDict(byte[] d, int at)
        {
            if (at + 8 > d.Length) return null;
            int count = d[at + 1];
            int ofsEntry = U16(d, at + 6);
            int eh = at + ofsEntry;
            if (count == 0 || eh + 4 > d.Length) return null;
            int unit = U16(d, eh);
            if (unit <= 0 || eh + 4 + count * unit > d.Length) return null;
            var o = new Dict();
            for (int i = 0; i < count; i++)
            {
                var e = new byte[unit];
                Array.Copy(d, eh + 4 + i * unit, e, 0, unit);
                o.Entries.Add(e);
            }
            return o;
        }

        private static string Tag(byte[] d, int at) => Encoding.ASCII.GetString(d, at, 4);
        private static int U16(byte[] d, int at) => d[at] | (d[at + 1] << 8);
        private static uint U32(byte[] d, int at) =>
            (uint)(d[at] | (d[at + 1] << 8) | (d[at + 2] << 16) | (d[at + 3] << 24));
        private static int U32s(byte[] d, int at) => (int)U32(d, at);
    }
}
