using System;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    public class TornWorldFileTests
    {
        private static string TornWorldDir =>
            Path.Combine(TestRoms.Platinum ?? "", "files", "fielddata", "tornworld");

        private static byte[][] Members(string narcPath)
        {
            var narc = NarcAPI.Narc.Open(narcPath);
            try
            {
                var members = new byte[narc.ElementCount][];
                for (int i = 0; i < members.Length; i++) members[i] = narc.GetElementBytes(i);
                return members;
            }
            finally { narc.Free(); }
        }

        [SkippableFact]
        public void TheRetailMapTableNamesTenFloors()
        {
            Skip.If(TestRoms.Platinum == null || !Directory.Exists(TornWorldDir), "No Platinum test ROM.");

            var table = new TornWorldMapTable(Members(Path.Combine(TornWorldDir, "tw_arc.narc"))[0]);

            Assert.Equal(10, table.Floors.Count);
            Assert.Equal(new long[] { 573, 574, 575, 576, 577, 579, 580, 581, 582, 583 },
                         table.Floors.Select(f => f.HeaderId));
            var altitudes = table.Floors.Take(8).Select(f => (int)f.OffsetAltitude).ToList();
            Assert.Equal(altitudes.OrderByDescending(a => a), altitudes);
            Assert.True(table.IsDistortionWorld(576));
            Assert.False(table.IsDistortionWorld(578));
        }

        [SkippableFact]
        public void EveryRetailMapFileReadsBackByteForByte()
        {
            Skip.If(TestRoms.Platinum == null || !Directory.Exists(TornWorldDir), "No Platinum test ROM.");

            var members = Members(Path.Combine(TornWorldDir, "tw_arc.narc"));
            var table = new TornWorldMapTable(members[0]);
            Assert.Equal(members[0], table.ToByteArray());

            foreach (var floor in table.Floors)
            {
                byte[] stored = members[floor.DataMember];
                var parsed = new TornWorldFile(stored);
                Assert.Equal(stored, parsed.ToByteArray());
            }
        }

        [SkippableFact]
        public void EveryPlatformsGridMatchesItsAttributeMember()
        {
            Skip.If(TestRoms.Platinum == null || !Directory.Exists(TornWorldDir), "No Platinum test ROM.");

            var members = Members(Path.Combine(TornWorldDir, "tw_arc.narc"));
            var attributes = Members(Path.Combine(TornWorldDir, "tw_arc_attr.narc"));
            var table = new TornWorldMapTable(members[0]);

            int checkedPlatforms = 0;
            foreach (var floor in table.Floors)
            {
                foreach (var platform in new TornWorldFile(members[floor.DataMember]).Platforms)
                {
                    Assert.InRange(platform.AttributeId, 0, attributes.Length - 1);
                    Assert.Equal(platform.TilesVertical * platform.TilesHorizontal * sizeof(ushort),
                                 attributes[platform.AttributeId].Length);
                    Assert.NotEqual(TornWorldFile.PlatformKind.Invalid, platform.Kind);
                    checkedPlatforms++;
                }
            }
            Assert.True(checkedPlatforms > 0, "No floating platforms were checked.");
        }

        [SkippableFact]
        public void OnlyTheFloorsWithGravityCarryPlatforms()
        {
            Skip.If(TestRoms.Platinum == null || !Directory.Exists(TornWorldDir), "No Platinum test ROM.");

            var members = Members(Path.Combine(TornWorldDir, "tw_arc.narc"));
            var table = new TornWorldMapTable(members[0]);

            var withPlatforms = table.Floors
                .Where(f => new TornWorldFile(members[f.DataMember]).Platforms.Count > 0)
                .Select(f => f.HeaderId)
                .ToList();

            Assert.Equal(new long[] { 574, 575, 576, 577, 583 }, withPlatforms);
        }

        [Fact]
        public void AFileWithEmptySectionsKeepsThemEmpty()
        {
            var file = new TornWorldFile();
            file.GhostProps.Add(new TornWorldFile.GhostProp { GroupId = 3, PropKind = 7, TileX = 1, TileY = 2, TileZ = 3 });

            byte[] written = file.ToByteArray();
            var read = new TornWorldFile(written);

            Assert.Empty(read.Platforms);
            Assert.Empty(read.JumpPoints);
            Assert.Empty(read.CameraRegions);
            Assert.Single(read.GhostProps);
            Assert.Equal(TornWorldFile.HeaderSize + TornWorldFile.GhostHeaderSize + TornWorldFile.GhostPropSize, written.Length);
        }

        [Fact]
        public void EverySectionSurvivesARoundTrip()
        {
            var file = new TornWorldFile { DefaultVisibleGroups = 0x0F0F };
            file.Platforms.Add(new TornWorldFile.FloatingPlatform
            {
                Kind = TornWorldFile.PlatformKind.Ceiling,
                AttributeId = 11,
                Bounds = new TornWorldFile.Bounds { StartX = 5, StartY = 6, StartZ = 7, SizeX = 8, SizeY = 0, SizeZ = 9 },
                TilesVertical = 32,
                TilesHorizontal = 32,
            });
            file.JumpPoints.Add(new TornWorldFile.JumpPoint { PlayerDirection = 2, DisplacementY = -16, TargetKind = 1, TargetPlatformIndex = 3 });
            file.CameraRegions.Add(new TornWorldFile.CameraRegion { AngleX = 62720, PlayerDirection = 1, TransitionSteps = 8 });
            file.GhostTriggers.Add(new TornWorldFile.GhostTrigger { GroupId = 2, ShowProp = 1 });

            var read = new TornWorldFile(file.ToByteArray());

            Assert.Equal(TornWorldFile.PlatformKind.Ceiling, read.Platforms[0].Kind);
            Assert.Equal(11, read.Platforms[0].AttributeId);
            Assert.Equal(9, read.Platforms[0].Bounds.SizeZ);
            Assert.Equal(-16, read.JumpPoints[0].DisplacementY);
            Assert.Equal(3, read.JumpPoints[0].TargetPlatformIndex);
            Assert.Equal(62720, read.CameraRegions[0].AngleX);
            Assert.Equal(8, read.CameraRegions[0].TransitionSteps);
            Assert.Equal(0x0F0F, read.DefaultVisibleGroups);
            Assert.Equal(1, read.GhostTriggers[0].ShowProp);
        }

        [Fact]
        public void TheGridPositionFollowsTheWayThePlatformFaces()
        {
            var west = new TornWorldFile.FloatingPlatform
            {
                Kind = TornWorldFile.PlatformKind.WestWall,
                Bounds = new TornWorldFile.Bounds { StartX = 11, StartY = 258, StartZ = 48, SizeX = 0, SizeY = 3, SizeZ = 9 },
                TilesVertical = 32,
                TilesHorizontal = 32,
            };

            Assert.Equal((3, 0), TornWorldFile.GridPosition(west, 11, 258, 48));
            Assert.Equal((0, 9), TornWorldFile.GridPosition(west, 11, 261, 57));
            Assert.Null(TornWorldFile.GridPosition(west, 11, 258, 99));

            var floor = new TornWorldFile.FloatingPlatform
            {
                Kind = TornWorldFile.PlatformKind.Floor,
                Bounds = new TornWorldFile.Bounds { StartX = 15, StartY = 233, StartZ = 0, SizeX = 31, SizeY = 0, SizeZ = 31 },
                TilesVertical = 32,
                TilesHorizontal = 32,
            };
            Assert.Equal((1, 2), TornWorldFile.GridPosition(floor, 16, 233, 2));

            var grid = new ushort[32 * 32];
            grid[1 + 2 * 32] = 0x8001;
            Assert.Equal((ushort)0x8001, TornWorldFile.AttributeAt(floor, grid, 16, 233, 2));
            Assert.Null(TornWorldFile.AttributeAt(floor, grid, 99, 233, 2));
        }
    }
}
