using DSPRE;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace DSPRE.Tests.Field
{
    [Collection("rom")]
    public class TornWorldSurfacesTests
    {
        private const int GridBand = 8;

        private static (TornWorldMapTable.Floor entry, TornWorldFile data, List<TornWorldSurfaces.Surface> surfaces)
            Floor(long header)
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum test project not configured");
            new RomInfo("CPUE", TestRoms.Platinum);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> {
                RomInfo.DirNames.maps, RomInfo.DirNames.matrices, RomInfo.DirNames.tornWorld,
                RomInfo.DirNames.tornWorldAttributes });

            var archive = new ScriptNarc(RomInfo.DirNames.tornWorld);
            var attributes = new ScriptNarc(RomInfo.DirNames.tornWorldAttributes);
            Skip.If(!archive.Available, "This ROM has no Distortion World archive.");

            ushort[] GridFor(int id)
            {
                byte[] raw = attributes.Get(id);
                if (raw == null) return null;
                var grid = new ushort[raw.Length / 2];
                for (int i = 0; i < grid.Length; i++) grid[i] = (ushort)(raw[i * 2] | (raw[i * 2 + 1] << 8));
                return grid;
            }

            var table = new TornWorldMapTable(archive.Get(0));
            var entry = table.Floors.First(f => f.HeaderId == header);
            var data = new TornWorldFile(archive.Get(entry.DataMember));
            var matrix = new GameMatrix(MapHeader.GetMapHeader((ushort)header).matrixID);

            MapFile MapAt(int across, int down)
            {
                if (across >= matrix.width || down >= matrix.height) return null;
                int index = matrix.maps[down, across];
                return index == GameMatrix.EMPTY ? null : new MapFile(index, RomInfo.gameFamily, discardMoveperms: false);
            }

            var surfaces = TornWorldSurfaces.ForFloor(entry, data, MapAt, GridFor,
                                                      matrix.width, matrix.height, 0, GridBand);
            return (entry, data, surfaces);
        }

        [SkippableTheory]
        [InlineData(574)]
        [InlineData(576)]
        [InlineData(577)]
        public void GroundJumpPointsAndPropTriggersAreReachedFromTheGround(long header)
        {
            // The game compares these with where the player stands, which on the ground is the terrain's height.
            var (entry, data, surfaces) = Floor(header);
            var ground = surfaces.Where(s => s.IsGround).ToList();
            var boxes = data.JumpPoints.Select(j => j.Bounds)
                .Concat(data.GhostTriggers.Select(g => g.Bounds))
                .Where(b => b.StartY == entry.OffsetAltitude + 1)
                .ToList();
            Skip.If(boxes.Count == 0, "No ground level jump points or triggers on this floor.");

            var missed = new List<string>();
            foreach (var box in boxes)
            {
                bool hit = ground.Any(s =>
                    Enumerable.Range(0, MapFile.mapSize).Any(row => Enumerable.Range(0, MapFile.mapSize).Any(col =>
                    {
                        var (x, y, z) = s.EventAt(col, row);
                        return box.Contains(x, y, z);
                    })));
                if (hit) continue;
                var lifts = ground.Where(s => box.StartX - s.GroundX >= 0 && box.StartX - s.GroundX < MapFile.mapSize
                                             && box.StartZ - s.GroundZ >= 0 && box.StartZ - s.GroundZ < MapFile.mapSize)
                                  .Select(s => s.EventAt(box.StartX - s.GroundX, box.StartZ - s.GroundZ).y);
                missed.Add($"({box.StartX},{box.StartY},{box.StartZ}) size ({box.SizeX},{box.SizeY},{box.SizeZ}) ground y {string.Join("/", lifts)}");
            }
            Assert.True(missed.Count == 0, $"floor altitude {entry.OffsetAltitude}, missed: " + string.Join("; ", missed));
        }

        [SkippableFact]
        public void EverySurfaceOfAFloorGetsAPatchOfItsOwn()
        {
            var (_, data, surfaces) = Floor(575);

            Assert.Equal(4, surfaces.Count(s => s.IsGround));
            Assert.Equal(data.Platforms.Count, surfaces.Count(s => !s.IsGround));
            Assert.Equal(surfaces.Count, surfaces.Select(s => (s.PatchX, s.PatchY)).Distinct().Count());
        }

        [SkippableFact]
        public void AWallIsWalkedUpAndDownItsHeight()
        {
            var (_, _, surfaces) = Floor(575);
            var wall = surfaces.First(s => s.Kind == TornWorldFile.PlatformKind.WestWall);

            var low = wall.WorldAt(0, 0);
            var high = wall.WorldAt(1, 0);
            Assert.Equal(low.x, high.x);
            Assert.Equal(low.z, high.z);
            Assert.NotEqual(low.y, high.y);

            var along = wall.WorldAt(0, 1);
            Assert.Equal(low.y, along.y);
            Assert.Equal(low.z + 1, along.z);
        }

        [SkippableFact]
        public void APlaceOnAPatchComesBackAsItself()
        {
            var (_, _, surfaces) = Floor(575);

            int checkedTiles = 0;
            foreach (var surface in surfaces)
                for (int row = 0; row < 8; row++)
                    for (int col = 0; col < 8; col++)
                    {
                        var (x, y, z) = surface.WorldAt(col, row);
                        Assert.True(surface.TryTileFor(x, y, z, out int backCol, out int backRow),
                                    $"A place on the {surface.Kind} patch did not come back at all.");
                        Assert.Equal(col, backCol);
                        Assert.Equal(row, backRow);
                        checkedTiles++;
                    }

            Assert.True(checkedTiles > 0);
        }

        [SkippableFact]
        public void EveryGravityPointOnB2FLandsOnASurfaceThatIsThere()
        {
            var (_, data, surfaces) = Floor(575);
            Assert.Equal(4, data.JumpPoints.Count);

            int landed = 0;
            foreach (var point in data.JumpPoints)
            {
                int fromX = point.Bounds.StartX, fromY = point.Bounds.StartY, fromZ = point.Bounds.StartZ;
                int toX = fromX + point.DisplacementX;
                int toY = fromY + point.DisplacementY;
                int toZ = fromZ + point.DisplacementZ;

                foreach (var landing in surfaces)
                {
                    if ((int)landing.Kind != point.TargetKind) continue;
                    if (point.TargetPlatformIndex >= 0 && !landing.IsGround
                        && landing.PlatformIndex != point.TargetPlatformIndex) continue;
                    if (!landing.TryTileFor(toX, toY, toZ, out int col, out int row)) continue;

                    landed++;
                    Assert.InRange(col, 0, MapFile.mapSize - 1);
                    Assert.InRange(row, 0, MapFile.mapSize - 1);
                    break;
                }
            }

            Assert.True(landed > 0, "No gravity point on B2F lands on a surface this floor has.");
        }

        [SkippableFact]
        public void APatchStopsWhereItsPlatformDoes()
        {
            var (_, data, surfaces) = Floor(575);

            foreach (var surface in surfaces.Where(s => !s.IsGround))
            {
                var platform = data.Platforms[surface.PlatformIndex];
                bool upright = platform.Kind == TornWorldFile.PlatformKind.WestWall
                            || platform.Kind == TornWorldFile.PlatformKind.EastWall;
                int reachDown = (upright ? platform.Bounds.SizeY : platform.Bounds.SizeX) + 1;
                int reachAcross = platform.Bounds.SizeZ + 1;

                for (int row = 0; row < MapFile.mapSize; row++)
                    for (int col = 0; col < MapFile.mapSize; col++)
                        if (col >= reachDown || row >= reachAcross)
                            Assert.False(surface.CanWalk(col, row),
                                $"The {platform.Kind} patch lets you stand past its own box at {col},{row}.");
            }
        }

        [SkippableFact]
        public void AGravityPointCarriesATurnForThePlayer()
        {
            var (_, data, _) = Floor(575);

            foreach (var point in data.JumpPoints)
                Assert.Equal(90, Math.Abs(point.SpriteRotationAngle));

            Assert.Contains(data.JumpPoints, p => p.SpriteRotationAngle > 0);
            Assert.Contains(data.JumpPoints, p => p.SpriteRotationAngle < 0);
            Assert.Equal(0, data.JumpPoints.Sum(p => p.SpriteRotationAngle));

            foreach (var point in data.JumpPoints) Assert.Equal(16, point.MovementSteps);
        }

        [SkippableFact]
        public void AFloorCarriesBothWhatItsMapHoldsAndWhatItsPlatformsDo()
        {
            var (_, data, surfaces) = Floor(575);

            Assert.Equal(4, data.Platforms.Count(p => p.Kind == TornWorldFile.PlatformKind.Floor));
            Assert.Equal(196, TornWorldSurfaces.CountWalkable(surfaces.Where(s => s.IsGround)));
            Assert.Equal(201, TornWorldSurfaces.CountWalkable(
                surfaces.Where(s => !s.IsGround && s.Kind == TornWorldFile.PlatformKind.Floor)));

            var (_, turnbackData, turnback) = Floor(583);
            Assert.DoesNotContain(turnbackData.Platforms, p => p.Kind == TornWorldFile.PlatformKind.Floor);
            Assert.Equal(272, TornWorldSurfaces.CountWalkable(turnback.Where(s => s.IsGround)));
        }

        [SkippableFact]
        public void TheIslandsAreCrossedByTheJumpTilesRatherThanByWalking()
        {
            var (_, _, surfaces) = Floor(575);

            int jumps = 0, landings = 0;
            foreach (var surface in surfaces)
                for (int row = 0; row < MapFile.mapSize; row++)
                    for (int col = 0; col < MapFile.mapSize; col++)
                    {
                        if (!FieldTileBehaviors.TryJump(surface.Types[row, col], out int dx, out int dz, out int over)) continue;
                        jumps++;
                        if (surface.CanWalk(col - dx, row - dz) && surface.CanWalk(col + dx * over, row + dz * over)) landings++;
                    }

            Assert.True(jumps > 0, "B2F carries no jump tiles at all.");
            Assert.True(landings > 0, "No jump tile on B2F has both somewhere to leave from and somewhere to land.");
        }

        [SkippableFact]
        public void ACameraRegionAnswersForOneWayOfFacingOnly()
        {
            var (_, data, _) = Floor(577);

            var pairs = data.CameraRegions
                .GroupBy(r => (r.Bounds.StartX, r.Bounds.StartY, r.Bounds.StartZ))
                .Where(g => g.Count() > 1)
                .ToList();

            Assert.NotEmpty(pairs);

            foreach (var pair in pairs)
                Assert.Equal(pair.Count(), pair.Select(r => r.PlayerDirection).Distinct().Count());

            Assert.Contains(pairs, pair => pair.Select(r => (r.AngleX, r.AngleY)).Distinct().Count() > 1);
        }

        [SkippableFact]
        public void TheCameraStartsWhereTheDistortionWorldPutsIt()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum test project not configured");
            new RomInfo("CPUE", TestRoms.Platinum);

            var start = new TornWorldFile.CameraRegion();
            Assert.Equal(59.05f, start.PitchDegrees, 2);
            Assert.Equal(0f, start.YawDegrees, 2);
        }

        [SkippableFact]
        public void TheWallsAndCeilingsCanBeWalkedAtAll()
        {
            var (_, _, surfaces) = Floor(577);

            var turned = surfaces.Where(s => !s.IsGround && s.Kind != TornWorldFile.PlatformKind.Floor).ToList();
            Assert.NotEmpty(turned);
            Assert.True(TornWorldSurfaces.CountWalkable(turned) > 0,
                        "Not one tile of B4F's walls or ceilings can be stood on.");
        }

        [SkippableFact]
        public void AWallStandsOnTheSideOfTheRoomItIsNamedFor()
        {
            int checkedWalls = 0;

            foreach (long header in new long[] { 575, 577 })
            {
                var (_, data, surfaces) = Floor(header);
                var ground = surfaces.Where(s => s.IsGround).ToList();
                if (ground.Count == 0) continue;

                int low = ground.Min(s => s.GroundX);
                int high = ground.Max(s => s.GroundX) + MapFile.mapSize - 1;
                int middle = (low + high) / 2;

                foreach (var platform in data.Platforms)
                {
                    if (platform.Kind == TornWorldFile.PlatformKind.WestWall)
                    {
                        Assert.True(platform.Bounds.StartX < middle,
                            $"A west wall on {header} sits at x {platform.Bounds.StartX}, "
                            + $"past the middle of x {low} to {high}.");
                        checkedWalls++;
                    }
                    else if (platform.Kind == TornWorldFile.PlatformKind.EastWall)
                    {
                        Assert.True(platform.Bounds.StartX > middle,
                            $"An east wall on {header} sits at x {platform.Bounds.StartX}, "
                            + $"short of the middle of x {low} to {high}.");
                        checkedWalls++;
                    }
                }
            }

            Assert.True(checkedWalls > 0, "Neither floor carried a wall to check.");
        }
    }
}
