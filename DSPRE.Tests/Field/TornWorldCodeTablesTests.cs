using DSPRE;
using DSPRE.ROMFiles;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace DSPRE.Tests.Field
{
    [Collection("rom")]
    public class TornWorldCodeTablesTests
    {
        private static TornWorldCodeTables.Tables Load()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum test project not configured");
            new RomInfo("CPUE", TestRoms.Platinum);
            TornWorldCodeTables.Forget();

            var tables = TornWorldCodeTables.Read(out string error);
            Skip.If(tables == null, "The Distortion World tables could not be read: " + error);
            return tables;
        }

        [SkippableFact]
        public void EveryFloorWithMovingPlatformsHasAsManyAsTheGameDoes()
        {
            var tables = Load();

            var expected = new Dictionary<uint, int>
            {
                [573] = 1, [574] = 2, [575] = 18, [576] = 4,
                [577] = 3, [579] = 3, [580] = 2, [581] = 1,
            };

            foreach (var pair in expected)
                Assert.Equal(pair.Value, tables.PlatformsOn(pair.Key).Count);
        }

        [SkippableFact]
        public void TheFirstPlatformOfB1FIsWhereTheGamePutsIt()
        {
            var tables = Load();
            var platform = tables.PlatformsOn(574).First();

            Assert.Equal(0, platform.Index);
            Assert.Equal(0x28, platform.TileX);
            Assert.Equal(0x101, platform.TileY);
            Assert.Equal(0x36, platform.TileZ);
            Assert.Equal(1, platform.ElevatorPathIndex);
            Assert.Equal(2, platform.PropKind);
        }

        [SkippableFact]
        public void TheFloorsWithPropsCarryThePropsTheGameShows()
        {
            var tables = Load();

            void Check(uint header, int kind, short x, short y, short z)
            {
                var props = tables.PropsOn(header);
                Assert.Single(props);
                Assert.Equal(kind, props[0].PropKind);
                Assert.Equal(x, props[0].TileX);
                Assert.Equal(y, props[0].TileY);
                Assert.Equal(z, props[0].TileZ);
            }

            Check(573, 24, 55, 289, 39);
            Check(579, 21, 106, 153, 78);
            Check(582, 24, 15, 1, 12);
            Check(583, 24, 116, 65, 74);
        }

        [SkippableFact]
        public void EveryPropKindNamesAnArchiveMember()
        {
            var tables = Load();

            Assert.Equal(25, tables.ModelByPropKind.Length);
            Assert.Equal(0x7C, tables.ModelFor(0));
            Assert.Equal(0x94, tables.ModelFor(24));
            Assert.Equal(-1, tables.ModelFor(25));
        }

        [SkippableFact]
        public void OnlyTheFivePropsThatMoveHaveAnAnimation()
        {
            var tables = Load();

            for (int kind = 0; kind < 20; kind++) Assert.Equal(-1, tables.AnimationFor(kind));

            Assert.Equal(0xC6, tables.AnimationFor(20));
            Assert.Equal(0xC8, tables.AnimationFor(21));
            Assert.Equal(0xBF, tables.AnimationFor(22));
            Assert.Equal(0xC0, tables.AnimationFor(23));
            Assert.Equal(0xC1, tables.AnimationFor(24));
        }

        [SkippableFact]
        public void EveryPlatformHoversThroughTheGamesOwnEightDrops()
        {
            var tables = Load();

            Assert.Equal(new float[] { 0f, -1f, -2f, -4f, -5f, -5.5f, -5.75f, -6f }, tables.HoverOffsets);
            Assert.Equal(0.5f, tables.HoverStep);
        }

        [SkippableFact]
        public void OnlyThePlatformsToldToGoUpOrDownAreElevators()
        {
            var tables = Load();

            var b2f = tables.PlatformsOn(575);
            Assert.Equal(18, b2f.Count);
            Assert.Equal(2, b2f.Count(p => p.IsElevator));
            Assert.Equal(0, b2f[0].ElevatorDirection);
            Assert.Equal(1, b2f[7].ElevatorDirection);
            Assert.DoesNotContain(b2f.Where((p, i) => i != 0 && i != 7), p => p.IsElevator);

            Assert.Equal(2, b2f[1].ElevatorDirection);
            Assert.Equal(0, b2f[1].ElevatorPathIndex);
        }

        [SkippableFact]
        public void ACameraRegionTurnsTheCameraFromWhereTheDistortionWorldStartsIt()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum test project not configured");
            new RomInfo("CPUE", TestRoms.Platinum);

            var level = new TornWorldFile.CameraRegion { AngleX = 0, AngleY = 0, AngleZ = 0 };
            Assert.Equal(59.05f, level.PitchDegrees, 2);
            Assert.Equal(0f, level.YawDegrees, 2);

            var tilted = new TornWorldFile.CameraRegion { AngleX = 20, AngleY = 45, AngleZ = 0 };
            Assert.Equal(30.93f, tilted.PitchDegrees, 2);
            Assert.Equal(63.28f, tilted.YawDegrees, 2);

            var overhead = new TornWorldFile.CameraRegion { AngleX = 60, AngleY = 220, AngleZ = 0 };
            Assert.Equal(-25.31f, overhead.PitchDegrees, 1);
            Assert.Equal(309.38f, overhead.YawDegrees, 2);
        }

        [SkippableFact]
        public void EveryPropIsPlacedAndSizedTheWayTheGamePlacesIt()
        {
            var tables = Load();

            Assert.Equal(25, tables.OffsetByPropKind.Length);
            Assert.Equal((0f, -25f, -6f), tables.OffsetFor(0));
            Assert.Equal((0f, -25f, -6f), tables.OffsetFor(5));
            Assert.Equal((-8f, -25f, 2f), tables.OffsetFor(8));
            Assert.Equal((0f, -14f, 8f), tables.OffsetFor(24));

            Assert.Equal(25, tables.ScaleByPropKind.Length);
            Assert.Equal((1.25f, 1.25f, 1.25f), tables.ScaleFor(0));
            Assert.Equal((1.25f, 1.25f, 1.5f), tables.ScaleFor(22));
            Assert.Equal((0f, 0f, 0f), tables.ScaleFor(23));
        }

        [SkippableFact]
        public void TheElevatorRoutesReadBackAsTheGameLaysThemOut()
        {
            var tables = Load();
            Assert.Equal(22, tables.ElevatorPaths.Count);

            var down = tables.PathAt(0);
            Assert.Equal(22, down.NextIndex);
            Assert.Equal(0, down.FinalX);
            Assert.Equal(-0x20, down.FinalY);
            Assert.Equal(-0x10, down.ChangeMapsY);
            Assert.Equal(-4f, down.SpeedY);
            Assert.Equal(128, down.Frames);

            var up = tables.PathAt(1);
            Assert.Equal(0x20, up.FinalY);
            Assert.Equal(0x12, up.ChangeMapsY);
            Assert.Equal(4f, up.SpeedY);
        }
    }
}
