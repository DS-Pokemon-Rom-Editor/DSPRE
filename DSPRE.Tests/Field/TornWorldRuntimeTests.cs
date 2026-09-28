using DSPRE.ROMFiles;
using System.Collections.Generic;
using Xunit;
using Flag = DSPRE.ROMFiles.TornWorldRuntime.PlatformFlag;
using RideEvent = DSPRE.ROMFiles.TornWorldRuntime.RideEvent;

namespace DSPRE.Tests.Field
{
    public class TornWorldRuntimeTests
    {
        private static TornWorldFile.Bounds Box(int x, int y, int z, int sx = 0, int sy = 0, int sz = 0)
            => new TornWorldFile.Bounds
            {
                StartX = (short)x, StartY = (short)y, StartZ = (short)z,
                SizeX = (short)sx, SizeY = (short)sy, SizeZ = (short)sz,
            };

        [Fact]
        public void AWarpInStartsWithTheGamesPlatformFlags()
        {
            var runtime = new TornWorldRuntime(573);
            Assert.Equal((1u << 3) | (1u << 7), runtime.PlatformFlags);

            var onB7F = new TornWorldRuntime(TornWorldRuntime.B7FHeader);
            Assert.Equal((1u << 0) | (1u << 1) | (1u << 2) | (1u << 3) | (1u << 4) | (1u << 7) | (1u << 9) | (1u << 10),
                         onB7F.PlatformFlags);
        }

        [Fact]
        public void APlatformIsThereWhenItsFlagIsElevenOrSet()
        {
            var runtime = new TornWorldRuntime(573);
            Assert.True(runtime.IsPresent(new TornWorldCodeTables.MovingPlatform { PersistedFlag = 11 }));
            Assert.True(runtime.IsPresent(new TornWorldCodeTables.MovingPlatform { PersistedFlag = (uint)Flag.B4F_1 }));
            Assert.False(runtime.IsPresent(new TornWorldCodeTables.MovingPlatform { PersistedFlag = (uint)Flag.B1F_1 }));

            runtime.Set((int)Flag.B1F_1);
            Assert.True(runtime.IsPresent(new TornWorldCodeTables.MovingPlatform { PersistedFlag = (uint)Flag.B1F_1 }));
        }

        [Fact]
        public void AJumpPointOnlyAnswersTheWayItFaces()
        {
            var file = new TornWorldFile();
            file.JumpPoints.Add(new TornWorldFile.JumpPoint { PlayerDirection = 2, Bounds = Box(12, 257, 49) });

            Assert.Null(TornWorldRuntime.JumpPointAt(file, 12, 257, 49, 3));
            Assert.Null(TornWorldRuntime.JumpPointAt(file, 13, 257, 49, 2));
            Assert.Same(file.JumpPoints[0], TornWorldRuntime.JumpPointAt(file, 12, 257, 49, 2));
        }

        [Fact]
        public void TheHopFollowsTheGamesArcOnItsAxis()
        {
            var point = new TornWorldFile.JumpPoint { MovementSteps = 16, JumpAxis = 1, SpriteRotationAngle = 90 };
            var lifts = new List<float>();
            for (int step = 1; step <= 16; step++) lifts.Add(TornWorldRuntime.HopOffset(point, step).y);

            Assert.Equal(new float[] { 6, 8, 10, 11, 12, 12, 12, 11, 10, 9, 8, 6, 4, 0, 0, 0 }, lifts);
            Assert.Equal(45f, TornWorldRuntime.TurnAfter(point, 8));

            point.JumpAxis = 0;
            point.InvertedJump = 1;
            Assert.Equal((-12f, 0f, 0f), TornWorldRuntime.HopOffset(point, 5));
        }

        [Fact]
        public void APropTriggerOnlyFiresFacingItsWay()
        {
            var file = new TornWorldFile { DefaultVisibleGroups = 0x1 };
            file.GhostTriggers.Add(new TornWorldFile.GhostTrigger { GroupId = 2, PlayerDirection = 0, ShowProp = 1, Bounds = Box(5, 10, 5, 1, 0, 1) });
            file.GhostTriggers.Add(new TornWorldFile.GhostTrigger { GroupId = 0, PlayerDirection = 0, ShowProp = 0, Bounds = Box(5, 10, 5) });

            var runtime = new TornWorldRuntime(576);
            runtime.EnterFloor(file);
            Assert.True(runtime.IsGroupVisible(0));
            Assert.False(runtime.IsGroupVisible(2));

            Assert.False(runtime.StepOn(file, 5, 10, 5, 1));
            Assert.True(runtime.StepOn(file, 5, 10, 5, 0));
            Assert.False(runtime.IsGroupVisible(0));
            Assert.True(runtime.IsGroupVisible(2));

            runtime.EnterFloor(file);
            Assert.True(runtime.IsGroupVisible(0));
            Assert.False(runtime.IsGroupVisible(2));
        }

        private static TornWorldCodeTables.Tables Tables()
        {
            var tables = new TornWorldCodeTables.Tables();
            for (int i = 0; i < 22; i++)
                tables.ElevatorPaths.Add(new TornWorldCodeTables.ElevatorPath { Index = i, NextIndex = TornWorldRuntime.NoPath, FlagToSet = 11, FlagToClear = 11 });

            var down = tables.ElevatorPaths[0];
            down.FinalY = -32; down.ChangeMapsY = -16; down.SpeedY = -4f; down.FlagToSet = (int)Flag.B1F_1;

            var first = tables.ElevatorPaths[8];
            first.NextIndex = 9; first.FinalY = -32; first.ChangeMapsY = -18; first.SpeedY = -4f; first.FlagToSet = (int)Flag.B5F_3;
            var second = tables.ElevatorPaths[9];
            second.FinalY = -32; second.ChangeMapsY = -14; second.SpeedY = -4f;

            var up = tables.ElevatorPaths[13];
            up.FinalY = 32; up.ChangeMapsY = 18; up.SpeedY = 4f; up.FlagToSet = (int)Flag.B4F_3; up.FlagToClear = (int)Flag.B5F_1;

            tables.MovingPlatforms[573] = new List<TornWorldCodeTables.MovingPlatform>
            {
                new TornWorldCodeTables.MovingPlatform { Index = 0, TileX = 40, TileY = 289, TileZ = 54, ElevatorPathIndex = 0, ElevatorDirection = 1, DestinationIndex = 1, PersistedFlag = 11 },
            };
            tables.MovingPlatforms[574] = new List<TornWorldCodeTables.MovingPlatform>
            {
                new TornWorldCodeTables.MovingPlatform { Index = 0, TileX = 1, TileY = 1, TileZ = 1, PersistedFlag = 11 },
                new TornWorldCodeTables.MovingPlatform { Index = 1, TileX = 40, TileY = 257, TileZ = 54, ElevatorPathIndex = 1, ElevatorDirection = 0, PersistedFlag = (uint)Flag.B1F_1 },
            };
            return tables;
        }

        [Fact]
        public void ARideShakesThenChangesFloorAtItsOffsetAndBecomesTheNextFloorsTemplate()
        {
            var tables = Tables();
            var runtime = new TornWorldRuntime(573);
            var ride = new TornWorldRuntime.ElevatorRide(runtime, tables, 573, tables.MovingPlatforms[573][0]);

            int changedAt = -1, arrivedAt = -1;
            float mostShake = 0f;
            for (int frame = 1; frame <= 400 && !ride.Done; frame++)
            {
                var happened = ride.Tick();
                if (frame <= 23) mostShake = System.Math.Max(mostShake, System.Math.Abs(ride.Y - 289));
                if (happened == RideEvent.ChangedFloor)
                {
                    changedAt = frame;
                    Assert.Equal(289 - 16, ride.Y);
                    Assert.Equal(574u, ride.Header);
                    Assert.True(runtime.Has(Flag.B1F_1));
                    Assert.Same(tables.MovingPlatforms[574][1], ride.Platform);
                }
                if (happened == RideEvent.Arrived) arrivedAt = frame;
            }

            // One frame to begin, 22 of shaking, then 64 frames for each 16 tiles at 4 units a frame.
            Assert.Equal(6f / 16f, mostShake);
            Assert.Equal(1 + 22 + 64, changedAt);
            Assert.Equal(changedAt + 64, arrivedAt);
            Assert.Equal((40, 257, 54), (ride.EndX, ride.EndY, ride.EndZ));
        }

        [Fact]
        public void AChainedPathChangesFloorEachTimeButOnlyTheLastOneSetsFlags()
        {
            var tables = Tables();
            var runtime = new TornWorldRuntime(576);
            runtime.Clear((int)Flag.B5F_1);
            var platform = new TornWorldCodeTables.MovingPlatform { TileX = 60, TileY = 194, TileZ = 40, ElevatorPathIndex = 8, ElevatorDirection = 1, PersistedFlag = 11 };
            var ride = new TornWorldRuntime.ElevatorRide(runtime, tables, 576, platform);

            var floors = new List<uint>();
            for (int frame = 0; frame < 1000 && !ride.Done; frame++)
                if (ride.Tick() == RideEvent.ChangedFloor) floors.Add(ride.Header);

            Assert.Equal(new uint[] { 577, 579 }, floors);
            Assert.False(runtime.Has(Flag.B5F_3));
            Assert.True(runtime.Has(Flag.B5F_1));
            Assert.Equal(194 - 64, ride.EndY);
        }

        [Fact]
        public void GoingUpOnPathThirteenMovesTheB4FFlagsBeforeItSetsOff()
        {
            var tables = Tables();
            var runtime = new TornWorldRuntime(579);
            runtime.Set((int)Flag.B4F_2);
            runtime.Clear((int)Flag.B4F_1);
            var platform = new TornWorldCodeTables.MovingPlatform { TileX = 60, TileY = 130, TileZ = 40, ElevatorPathIndex = 13, ElevatorDirection = 0, PersistedFlag = 11 };
            var ride = new TornWorldRuntime.ElevatorRide(runtime, tables, 579, platform);

            ride.Tick();

            Assert.True(runtime.Has(Flag.B4F_1));
            Assert.False(runtime.Has(Flag.B4F_2));
        }
    }
}
