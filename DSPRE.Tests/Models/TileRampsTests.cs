using DSPRE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DSPRE.Tests.Models
{
    public class TileRampsTests
    {
        private const float High = 0.25f;
        private static readonly MapTileset.Face Grass = new MapTileset.Face { Picture = "grass", Look = MaterialLook.Plain };

        private static Func<int, int, (float, MapTileset.Face)> Ground(Func<int, int, bool> raised)
            => (x, z) => (raised(x, z) ? High : 0f, Grass);

        private static TileRamps.Run Run(TileRamps.Kind kind, params (int x, int z)[] squares)
            => new TileRamps.Run { Kind = kind, Squares = new HashSet<(int x, int z)>(squares) };

        // Terrain height, in squares' model units, at the middle of a point given in squares.
        private static float WalkedAt(TileRamps.Report report, float squareX, float squareZ)
        {
            float x = (-MapTileset.HalfMap + squareX * MapTileset.TileWidth) * 64f;
            float z = (-MapTileset.HalfMap + squareZ * MapTileset.TileWidth) * 64f;
            var plate = report.Plates.Single(p => p.Covers(x, z) && x > p.MinX && x < p.MaxX && z > p.MinZ && z < p.MaxZ);
            return plate.HeightAtOrZero(x, z) / 64f;
        }

        private static void Near(float expected, float actual) => Assert.True(Math.Abs(expected - actual) < 1e-4f, $"expected {expected}, got {actual}");

        [Fact]
        public void ALongSlopeSpreadsItsRiseAndIsWalkedOnOnePlate()
        {
            var r = new TileBake.Result();
            var report = TileRamps.Lay(r, new TileGrid(), new[] { Run(TileRamps.Kind.Slope, (10, 7), (10, 8), (10, 9)) },
                                       Ground((x, z) => z <= 6));

            Assert.Equal(1, report.Made);
            Assert.Single(report.Plates);
            // Three sloped squares, closed on both sides where they stand above the lawn beside them.
            bool Upright(int[] f) => f.Select(i => (r.Corners[i].x, r.Corners[i].z)).Distinct().Count() <= 2;
            Assert.Equal(3, r.Faces.Count(f => !Upright(f)));
            Assert.Equal(6, r.Faces.Count(Upright));
            Near(High / 2f, WalkedAt(report, 10.5f, 8.5f));
            Near(High * 5f / 6f, WalkedAt(report, 10.5f, 7.5f));
            Near(High, r.Corners.Max(c => c.y));
            Near(0f, r.Corners.Min(c => c.y));
        }

        [Fact]
        public void AnInnerCornerRisesTowardBothHigherSides()
        {
            var r = new TileBake.Result();
            var report = TileRamps.Lay(r, new TileGrid(), new[] { Run(TileRamps.Kind.Slope, (9, 7)) },
                                       Ground((x, z) => z <= 6 || x >= 10));

            Assert.Equal(1, report.Made);
            Assert.Equal(4, report.Plates.Count);
            Near(High * 0.25f, WalkedAt(report, 9.25f, 7.75f));
            Near(High * 0.75f, WalkedAt(report, 9.75f, 7.25f));
            Near(High * 0.75f, WalkedAt(report, 9.75f, 7.75f));
        }

        [Fact]
        public void AnOuterCornerRisesOnlyTowardTheHigherDiagonal()
        {
            var r = new TileBake.Result();
            var report = TileRamps.Lay(r, new TileGrid(), new[] { Run(TileRamps.Kind.Slope, (9, 7)) },
                                       Ground((x, z) => z <= 6 && x >= 10));

            Assert.Equal(1, report.Made);
            Near(High * 0.75f, WalkedAt(report, 9.75f, 7.25f));
            Near(High * 0.25f, WalkedAt(report, 9.75f, 7.75f));
        }

        [Fact]
        public void ASlopeTooSteepToWalkIsRefusedWithTheLengthItNeeds()
        {
            var r = new TileBake.Result();
            var report = TileRamps.Lay(r, new TileGrid(), new[] { Run(TileRamps.Kind.Slope, (10, 7)) },
                                       (x, z) => (z <= 6 ? 1f : 0f, Grass));

            Assert.Equal(0, report.Made);
            Assert.Empty(r.Faces);
            Assert.Contains(report.Skipped, s => s.Contains("3 squares"));
        }

        [Fact]
        public void ASlopeTileIsTurnedUphillAndFittedAcrossTheRun()
        {
            // Two squares wide, climbing east by a quarter; its picture repeats every 16 units across.
            float tw = MapTileset.TileWidth;
            var look = MaterialLook.Plain.With(bothSides: false);
            var bank = new MapTileset.Tile { Name = "bank", Wide = 2, Deep = 1 };
            bank.Corners.Add(new MapTileset.Corner { X = 0, Y = 0, Z = 0, S = 0, T = 0 });
            bank.Corners.Add(new MapTileset.Corner { X = 0, Y = 0, Z = 2 * tw, S = 32, T = 0 });
            bank.Corners.Add(new MapTileset.Corner { X = tw, Y = 0.25f, Z = 2 * tw, S = 32, T = 16 });
            bank.Corners.Add(new MapTileset.Corner { X = tw, Y = 0.25f, Z = 0, S = 0, T = 16 });
            bank.Wide = 1; bank.Deep = 2;
            bank.Faces.Add(new MapTileset.Face { Corners = new[] { 0, 1, 2, 3 }, Picture = "bank", Look = look });

            var r = new TileBake.Result();
            var run = new TileRamps.Run { Squares = new HashSet<(int x, int z)> { (10, 7), (11, 7), (12, 7), (13, 7) }, Template = bank };
            var report = TileRamps.Lay(r, new TileGrid(), new[] { run }, Ground((x, z) => z <= 6));

            Assert.Equal(1, report.Made);
            // The tile has no sides of its own, so the two ends are closed down to the lawn.
            bool Upright(int[] f) => f.Select(i => (r.Corners[i].x, r.Corners[i].z)).Distinct().Count() <= 2;
            Assert.Equal(2, r.Faces.Count(Upright));
            int at = r.Faces.FindIndex(f => !Upright(f));
            var pts = r.Faces[at].Select(i => r.Corners[i]).ToArray();
            // Turned to climb north, across the run's four squares, from the lawn to the block.
            Near(-MapTileset.HalfMap + 10 * tw, pts.Min(p => p.x));
            Near(-MapTileset.HalfMap + 14 * tw, pts.Max(p => p.x));
            Near(0f, pts.Where(p => Math.Abs(p.z - (-MapTileset.HalfMap + 8 * tw)) < 1e-4f).Max(p => p.y));
            Near(High, pts.Where(p => Math.Abs(p.z - (-MapTileset.HalfMap + 7 * tw)) < 1e-4f).Min(p => p.y));
            // The picture keeps 16 units a square across, so four squares span 64.
            var uv = r.OnPicture[at];
            Near(64f, uv.Max(q => q.s) - uv.Min(q => q.s));
        }

        [Fact]
        public void AStairTileWhoseSideWallsStandAboveItsTopStillClimbsTheRightWay()
        {
            // Climbs north (towards smaller z), with end walls taller than the top step.
            float tw = MapTileset.TileWidth;
            var look = MaterialLook.Plain.With(bothSides: false);
            var stair = new MapTileset.Tile { Name = "stair", Wide = 2, Deep = 1 };
            void C(float x, float y, float z) => stair.Corners.Add(new MapTileset.Corner { X = x, Y = y, Z = z });
            C(0, 0.25f, 0); C(2 * tw, 0.25f, 0); C(2 * tw, 0, tw); C(0, 0, tw);
            C(0, 0, 0); C(0, 0.3f, 0); C(0, 0.3f, tw); C(0, 0, tw);
            C(2 * tw, 0, 0); C(2 * tw, 0.3f, 0); C(2 * tw, 0.3f, tw); C(2 * tw, 0, tw);
            stair.Faces.Add(new MapTileset.Face { Corners = new[] { 0, 1, 2, 3 }, Picture = "steps", Look = look });
            stair.Faces.Add(new MapTileset.Face { Corners = new[] { 4, 5, 6, 7 }, Picture = "wall", Look = look });
            stair.Faces.Add(new MapTileset.Face { Corners = new[] { 8, 9, 10, 11 }, Picture = "wall", Look = look });

            var r = new TileBake.Result();
            var run = new TileRamps.Run { Squares = new HashSet<(int x, int z)> { (10, 7), (11, 7), (12, 7) }, Template = stair };
            Assert.Equal(1, TileRamps.Lay(r, new TileGrid(), new[] { run }, Ground((x, z) => z <= 6)).Made);

            int at = r.Picture.IndexOf("steps");
            var pts = r.Faces[at].Select(i => r.Corners[i]).ToArray();
            float north = -MapTileset.HalfMap + 7 * tw, south = -MapTileset.HalfMap + 8 * tw;
            Near(High, pts.Where(p => Math.Abs(p.z - north) < 1e-4f).Min(p => p.y));
            Near(0f, pts.Where(p => Math.Abs(p.z - south) < 1e-4f).Max(p => p.y));
            // Walls still stand at the run's two ends, not along it.
            var walls = Enumerable.Range(0, r.Faces.Count).Where(i => r.Picture[i] == "wall").ToList();
            Assert.Equal(2, walls.Count);
            Assert.All(walls, i => Assert.Single(r.Faces[i].Select(k => MathF.Round(r.Corners[k].x, 4)).Distinct()));
        }

        [Fact]
        public void StairsAreDrawnAsStepsButWalkedAsASlope()
        {
            var r = new TileBake.Result();
            var report = TileRamps.Lay(r, new TileGrid(), new[] { Run(TileRamps.Kind.Stairs, (10, 7), (10, 8)) },
                                       Ground((x, z) => z <= 6));

            Assert.Equal(1, report.Made);
            Assert.Single(report.Plates);
            Near(High / 2f, WalkedAt(report, 10.5f, 8f));
            int treads = r.Faces.Count(f => f.All(i => Math.Abs(r.Corners[i].y - r.Corners[f[0]].y) < 1e-5f));
            int risers = r.Faces.Count - treads;
            Assert.True(treads >= 4, $"{treads} treads");
            Assert.True(risers >= 4, $"{risers} risers");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void StairsOnACornerAreSteppedNotSloped(bool inner)
        {
            var r = new TileBake.Result();
            var report = TileRamps.Lay(r, new TileGrid(), new[] { Run(TileRamps.Kind.Stairs, (9, 7)) },
                                       Ground(inner ? (x, z) => z <= 6 || x >= 10 : (x, z) => z <= 6 && x >= 10));

            Assert.Equal(1, report.Made);
            Assert.Equal(4, report.Plates.Count);
            bool Upright(int[] f) => f.Select(i => (r.Corners[i].x, r.Corners[i].z)).Distinct().Count() <= 2;
            var treads = r.Faces.Where(f => !Upright(f)).ToList();
            Assert.NotEmpty(treads);
            Assert.All(treads, f => Assert.All(f, i => Near(r.Corners[f[0]].y, r.Corners[i].y)));
            Assert.Contains(r.Faces, Upright);

            // Every tread sits on a whole step; the top one is level with the high ground, the lowest a step up.
            var heights = treads.Select(f => r.Corners[f[0]].y).Distinct().OrderBy(y => y).ToList();
            float step = heights[0];
            Assert.True(step > 0 && step < High);
            Assert.All(heights, y => Near(MathF.Round(y / step) * step, y));
            Near(High, heights[^1]);

            // Next to the corner that rises the tread is at the top; at the far corner it is the lowest step.
            float TreadAt(float x, float z)
            {
                float wx = -MapTileset.HalfMap + x * MapTileset.TileWidth, wz = -MapTileset.HalfMap + z * MapTileset.TileWidth;
                return treads.Where(f => f.Min(i => r.Corners[i].x) <= wx && f.Max(i => r.Corners[i].x) >= wx
                                      && f.Min(i => r.Corners[i].z) <= wz && f.Max(i => r.Corners[i].z) >= wz)
                             .Select(f => r.Corners[f[0]].y).Single();
            }
            Near(High, TreadAt(9.97f, 7.03f));
            Near(step, TreadAt(9.03f, 7.97f));
        }
    }
}
