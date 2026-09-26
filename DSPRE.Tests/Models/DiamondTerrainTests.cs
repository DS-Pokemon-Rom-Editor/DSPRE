using DSPRE.Models;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace DSPRE.Tests.Models
{
    /// <summary>Walks Diamond and Pearl terrain as the game does: grid cell, band search, triangle test, plane height.</summary>
    public class DiamondTerrainTests
    {
        private sealed class Walk
        {
            public int Cells;
            public int StartX, StartZ, SizeX, SizeZ;
            public (int x, int y, int z)[] Vertices;
            public (int x, int y, int z)[] Normals;
            public (int a, int b, int c, int n, int d)[] Triangles;
            public (int lines, int first)[] Grid;
            public ushort[] Lines;
            public ushort[] Ids;

            public static Walk Read(byte[] data)
            {
                var r = new BinaryReader(new MemoryStream(data));
                r.BaseStream.Position = 8;
                var u = Enumerable.Range(0, 12).Select(_ => (int)r.ReadUInt16()).ToArray();
                int lineBytes = r.ReadInt32(), idBytes = r.ReadInt32();
                var w = new Walk { Cells = r.ReadUInt16() };
                r.ReadUInt16();
                w.StartX = r.ReadInt32(); w.StartZ = r.ReadInt32(); r.ReadInt32(); r.ReadInt32();
                w.SizeX = r.ReadInt32(); w.SizeZ = r.ReadInt32();
                w.Vertices = Enumerable.Range(0, u[1]).Select(_ => (r.ReadInt32(), r.ReadInt32(), r.ReadInt32())).ToArray();
                w.Normals = Enumerable.Range(0, u[3]).Select(_ => (r.ReadInt32(), r.ReadInt32(), r.ReadInt32())).ToArray();
                w.Triangles = Enumerable.Range(0, u[5]).Select(_ => ((int)r.ReadUInt16(), (int)r.ReadUInt16(), (int)r.ReadUInt16(), (int)r.ReadUInt16(), r.ReadInt32())).ToArray();
                w.Grid = Enumerable.Range(0, u[8]).Select(_ => ((int)r.ReadUInt16(), (int)r.ReadUInt16())).ToArray();
                w.Lines = Enumerable.Range(0, lineBytes / 2).Select(_ => r.ReadUInt16()).ToArray();
                w.Ids = Enumerable.Range(0, idBytes / 2).Select(_ => r.ReadUInt16()).ToArray();
                return w;
            }

            private static int LineZ(ushort[] list, int at, int i) => (list[at + i * 5 + 2] << 16) | list[at + i * 5 + 1];

            // Same steps as the game's band search.
            private static bool Band(ushort[] list, int at, int count, int z, out int index)
            {
                index = 0;
                if (count == 0) return false;
                if (count == 1) return true;
                int min = 0, max = count - 1, idx = max / 2;
                while (true)
                {
                    if (LineZ(list, at, idx) > z)
                    {
                        if (max - 1 > min) { max = idx; idx = (min + max) / 2; }
                        else { index = idx; return true; }
                    }
                    else
                    {
                        if (min + 1 < max) { min = idx; idx = (min + max) / 2; }
                        else { index = idx + 1; return true; }
                    }
                }
            }

            private static bool Inside(long px, long pz, (int x, int y, int z) a, (int x, int y, int z) b, (int x, int y, int z) c)
            {
                long Side((int x, int y, int z) p, (int x, int y, int z) q) => (q.x - p.x) * (pz - p.z) - (q.z - p.z) * (px - p.x);
                long s1 = Side(a, b), s2 = Side(b, c), s3 = Side(c, a);
                return (s1 >= 0 && s2 >= 0 && s3 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0);
            }

            /// <summary>Heights of every triangle under a point, in map units.</summary>
            public List<float> HeightsAt(float x, float z)
            {
                int fx = (int)Math.Round(x * 4096), fz = (int)Math.Round(z * 4096);
                long tx = Math.Max(0, fx - StartX), tz = Math.Max(0, fz - StartZ);
                int gx = (int)Math.Min(Cells - 1, (tx << 12) / SizeX >> 12), gz = (int)Math.Min(Cells - 1, (tz << 12) / SizeZ >> 12);
                var (lines, first) = Grid[gx + gz * Cells];
                var found = new List<float>();
                if (!Band(Lines, first * 5, lines, fz, out int band)) return found;
                int at = (first + band) * 5;
                int count = Lines[at], offset = (Lines[at + 4] << 16) | Lines[at + 3];
                for (int i = 0; i < count; i++)
                {
                    var t = Triangles[Ids[offset + i]];
                    if (!Inside(fx, fz, Vertices[t.a], Vertices[t.b], Vertices[t.c])) continue;
                    var n = Normals[t.n];
                    found.Add(-(n.x / 4096f * x + n.z / 4096f * z + t.d / 4096f) / (n.y / 4096f));
                }
                return found;
            }
        }

        private static BdhcBuild.Piece Plate(float x0, float z0, float x1, float z1, float height, float slopeX = 0, float slopeZ = 0)
        {
            var p = new BdhcBuild.Piece { MinX = x0, MaxX = x1, MinZ = z0, MaxZ = z1 };
            p.Tilt(height, slopeX, slopeZ);
            return p;
        }

        [Fact]
        public void EveryPointOfEveryPlateIsFoundAtItsHeightTheWayTheGameLooksItUp()
        {
            // A lawn, a raised block, a slope up to it, and a lot of small plates so the grid splits into cells.
            var plates = new List<BdhcBuild.Piece>
            {
                Plate(-256, -256, 256, 0, 16),
                Plate(-256, 0, 0, 256, 32),
                Plate(0, 0, 256, 128, 24, 0, 8),
            };
            for (int i = 0; i < 40; i++) plates.Add(Plate(-256 + i * 12, 128, -244 + i * 12, 256, 16 + i % 3));

            byte[] data = BdhcBuild.Triangles(plates, out string whynot);
            Assert.Null(whynot);
            Assert.True(BdhcBuild.IsTriangles(data));
            var walk = Walk.Read(data);
            Assert.True(walk.Cells > 1, "Enough plates to split the grid.");

            int checkedPoints = 0;
            var random = new Random(4);
            foreach (var p in plates)
                for (int k = 0; k < 6; k++)
                {
                    float x = p.MinX + (float)random.NextDouble() * (p.MaxX - p.MinX);
                    float z = p.MinZ + (float)random.NextDouble() * (p.MaxZ - p.MinZ);
                    float want = p.HeightAtOrZero(x, z);
                    var got = walk.HeightsAt(x, z);
                    Assert.Contains(got, h => Math.Abs(h - want) < 0.01f);
                    checkedPoints++;
                }
            Assert.True(checkedPoints > 0);
        }

        [Fact]
        public void DiamondTerrainReadsBackAsThePlatesItWasWrittenFrom()
        {
            var plates = new List<BdhcBuild.Piece> { Plate(-256, -256, 256, 0, 16), Plate(-256, 0, 256, 256, 24, 0, 4) };
            byte[] data = BdhcBuild.Triangles(plates, out _);

            var back = BdhcBuild.FromTriangles(data);
            Assert.Equal(2, back.Count);
            Assert.True(BdhcFile.TryParse(data, out var file));
            Assert.Equal(2, file.Plates().Count());
            Assert.Equal(16f, file.Plates().First(p => p.MaxZ <= 0).HeightAt(10, -10), 3);
        }

        [SkippableFact]
        public void TheLookupReadsTheGamesOwnTerrainTheWayItsPlatesSay()
        {
            string maps = Path.Combine(TestRoms.Diamond, "unpacked", "maps");
            Skip.If(!Directory.Exists(maps), "Diamond test project not configured");

            int checkedMaps = 0, checkedPoints = 0;
            foreach (string path in RomFiles.Settled(maps).Take(40))
            {
                byte[] map = File.ReadAllBytes(path);
                if (map.Length < 16) continue;
                int ps = BitConverter.ToInt32(map, 0), bs = BitConverter.ToInt32(map, 4), ms = BitConverter.ToInt32(map, 8), bd = BitConverter.ToInt32(map, 12);
                if (bd < 68 || 16 + ps + bs + ms + bd > map.Length) continue;
                byte[] terrain = map.Skip(16 + ps + bs + ms).Take(bd).ToArray();
                if (!BdhcBuild.IsTriangles(terrain)) continue;

                var walk = Walk.Read(terrain);
                foreach (var t in walk.Triangles)
                {
                    // The middle of each of the game's triangles must be found, at that triangle's own height.
                    var a = walk.Vertices[t.a]; var b = walk.Vertices[t.b]; var c = walk.Vertices[t.c];
                    float x = (a.x + b.x + c.x) / 3f / 4096f, z = (a.z + b.z + c.z) / 3f / 4096f, y = (a.y + b.y + c.y) / 3f / 4096f;
                    Assert.Contains(walk.HeightsAt(x, z), h => Math.Abs(h - y) < 0.05f);
                    checkedPoints++;
                }
                checkedMaps++;
            }
            Assert.True(checkedMaps > 0 && checkedPoints > 0, "No Diamond terrain was read.");
        }
    }
}
