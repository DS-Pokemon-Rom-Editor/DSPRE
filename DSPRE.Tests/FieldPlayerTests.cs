using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Walking the map as the player in the preview: turn first, then step, and stop at anything the
    /// engine would stop at.
    /// </summary>
    public class FieldPlayerTests
    {
        private static MapCollisionGrid OpenMap(params (int x, int z)[] walls)
        {
            var g = new byte[MapFile.mapSize, MapFile.mapSize];
            foreach (var (x, z) in walls) g[z, x] = MapCollisionGrid.BlockedBit;
            var grid = new MapCollisionGrid();
            grid.Add(0, 0, g);
            return grid;
        }

        [Fact]
        public void PressingANewDirectionTurnsBeforeItWalks()
        {
            var p = new FieldPlayer(5, 5, MoveFacing.Down, OpenMap());

            Assert.Equal(StepResult.Turned, p.Go(MoveFacing.Right));
            Assert.Equal(MoveFacing.Right, p.Facing);
            Assert.Equal(5, p.TileX);

            Assert.Equal(StepResult.Walked, p.Go(MoveFacing.Right));
            Assert.Equal(6, p.TileX);
            Assert.Equal(5, p.TileZ);
        }

        [Fact]
        public void AWallStopsTheStepButKeepsTheFacing()
        {
            var p = new FieldPlayer(5, 5, MoveFacing.Right, OpenMap((6, 5)));

            Assert.Equal(StepResult.Blocked, p.Go(MoveFacing.Right));
            Assert.Equal(5, p.TileX);
            Assert.Equal(MoveFacing.Right, p.Facing);
        }

        [Fact]
        public void SomebodyStandingThereStopsTheStepToo()
        {
            var p = new FieldPlayer(5, 5, MoveFacing.Right, OpenMap(),
                                    (x, z) => x == 6 && z == 5);
            Assert.Equal(StepResult.BlockedByEvent, p.Go(MoveFacing.Right));
            Assert.Equal(5, p.TileX);
        }

        [Fact]
        public void WalkingOffTheLoadedMapIsRefused()
        {
            var p = new FieldPlayer(0, 0, MoveFacing.Left, OpenMap());
            Assert.Equal(StepResult.Blocked, p.Go(MoveFacing.Left));
            Assert.Equal(0, p.TileX);
        }

        [Fact]
        public void TheTileAheadFollowsTheFacing()
        {
            var p = new FieldPlayer(4, 4, MoveFacing.Up, OpenMap());
            Assert.Equal((4, 3), p.TileAhead);
            p.Face(MoveFacing.Down);
            Assert.Equal((4, 5), p.TileAhead);
        }

        [Fact]
        public void ResetPutsThePlayerBackWhereTheyStarted()
        {
            var p = new FieldPlayer(2, 3, MoveFacing.Up, OpenMap());
            p.Go(MoveFacing.Down);
            p.Go(MoveFacing.Down);
            Assert.NotEqual(3, p.TileZ);

            p.Reset();
            Assert.Equal(2, p.TileX);
            Assert.Equal(3, p.TileZ);
            Assert.Equal(MoveFacing.Up, p.Facing);
        }

        // ── what you are interacting with ───────────────────────────────────────────────
        private static EventFile Events(params Event[] events)
        {
            var f = new EventFile();
            foreach (var e in events)
            {
                if (e is Overworld o) f.overworlds.Add(o);
                else if (e is Spawnable sp) f.spawnables.Add(sp);
                else if (e is Trigger t) f.triggers.Add(t);
                else if (e is Warp w) f.warps.Add(w);
            }
            return f;
        }

        private static Overworld Person(int tileX, int tileZ, ushort type, ushort script)
            => new Overworld(1, tileX / MapFile.mapSize, tileZ / MapFile.mapSize)
            {
                type = type,
                scriptNumber = script,
                xMapPosition = (short)(tileX % MapFile.mapSize),
                yMapPosition = (short)(tileZ % MapFile.mapSize),
            };

        [Fact]
        public void ATrainerRunsItsScriptLikeAnybodyElse()
        {
            // The number on a trainer overworld is a script id, not a trainer number. Nothing about
            // talking to one differs from talking to anyone else.
            var events = Events(Person(6, 5, type: 1, script: 3042));
            var ow = FieldInteraction.OverworldAt(events, 6, 5);
            Assert.NotNull(ow);
            Assert.Equal(3042, ow.scriptNumber);
        }

        [Fact]
        public void TheTrainerComesFromTheScriptIdByLookup()
        {
            // GetTrainerIdByScriptId: single battles start at 3000, doubles at 5000, both one-origin.
            Assert.Equal(1, TrainerScripts.TrainerIdFor(3000));
            Assert.Equal(43, TrainerScripts.TrainerIdFor(3042));
            Assert.Equal(1, TrainerScripts.TrainerIdFor(5000));
            Assert.True(TrainerScripts.IsDouble(5000));
            Assert.False(TrainerScripts.IsDouble(4999));

            // A script outside those ranges is not a trainer at all.
            Assert.Null(TrainerScripts.TrainerIdFor(42));
            Assert.Null(TrainerScripts.TrainerIdFor(7000));
        }

        [Fact]
        public void ACounterLetsYouTalkOverIt()
        {
            var grid = OpenMap();
            var types = new byte[MapFile.mapSize, MapFile.mapSize];
            types[5, 6] = MapCollisionGrid.CounterType;      // the desk in front of the player
            grid.AddTypes(0, 0, types);

            var player = new FieldPlayer(5, 5, MoveFacing.Right, grid);
            Assert.Equal((6, 5), player.TileAhead);
            // The talk carries past the counter to whoever is behind it.
            Assert.Equal((7, 5), FieldInteraction.TalkTile(player, grid));
        }

        [Fact]
        public void WithoutACounterTalkingReachesTheTileInFront()
        {
            var grid = OpenMap();
            var player = new FieldPlayer(5, 5, MoveFacing.Right, grid);
            Assert.Equal((6, 5), FieldInteraction.TalkTile(player, grid));
        }

        // ── spawnables ──────────────────────────────────────────────────────────────────
        private static Spawnable Sign(int tileX, int tileZ, ushort type, ushort dir, ushort script)
            => new Spawnable(tileX / MapFile.mapSize, tileZ / MapFile.mapSize)
            {
                type = type,
                dir = dir,
                scriptNumber = script,
                xMapPosition = (short)(tileX % MapFile.mapSize),
                yMapPosition = (short)(tileZ % MapFile.mapSize),
            };

        [Fact]
        public void ASignOnlyAnswersFromTheWayItFaces()
        {
            // A sign you read from below answers a player facing up, and nobody else.
            var events = Events(Sign(6, 5, (ushort)SpawnableKind.Signboard,
                                     (ushort)SpawnableApproach.FromBelow, script: 11));

            Assert.NotNull(FieldInteraction.SpawnableAt(events, 6, 5, MoveFacing.Up));
            Assert.Null(FieldInteraction.SpawnableAt(events, 6, 5, MoveFacing.Down));
            Assert.Null(FieldInteraction.SpawnableAt(events, 6, 5, MoveFacing.Left));
        }

        [Theory]
        [InlineData(SpawnableApproach.FromBelow, MoveFacing.Up, true)]
        [InlineData(SpawnableApproach.FromAbove, MoveFacing.Down, true)]
        [InlineData(SpawnableApproach.FromRight, MoveFacing.Left, true)]
        [InlineData(SpawnableApproach.FromLeft, MoveFacing.Right, true)]
        [InlineData(SpawnableApproach.FromAboveOrBelow, MoveFacing.Up, true)]
        [InlineData(SpawnableApproach.FromAboveOrBelow, MoveFacing.Down, true)]
        [InlineData(SpawnableApproach.FromAboveOrBelow, MoveFacing.Left, false)]
        [InlineData(SpawnableApproach.FromTheSides, MoveFacing.Left, true)]
        [InlineData(SpawnableApproach.FromTheSides, MoveFacing.Right, true)]
        [InlineData(SpawnableApproach.FromTheSides, MoveFacing.Up, false)]
        [InlineData(SpawnableApproach.AnyWay, MoveFacing.Up, true)]
        [InlineData(SpawnableApproach.AnyWay, MoveFacing.Right, true)]
        public void EveryApproachRuleMatchesTheEngine(SpawnableApproach approach, MoveFacing facing, bool expected)
        {
            Assert.Equal(expected, FieldInteraction.CanTalkFrom((int)approach, facing));
        }

        [Fact]
        public void AHiddenItemIgnoresWhichWayYouFaceButNotWhetherItIsGone()
        {
            var events = Events(Sign(6, 5, (ushort)SpawnableKind.HiddenItem,
                                     (ushort)SpawnableApproach.FromBelow, script: 12));

            // Facing does not come into it for a hidden item, only whether it is still there.
            Assert.NotNull(FieldInteraction.SpawnableAt(events, 6, 5, MoveFacing.Down, _ => true));
            Assert.NotNull(FieldInteraction.SpawnableAt(events, 6, 5, MoveFacing.Left, _ => true));
            Assert.Null(FieldInteraction.SpawnableAt(events, 6, 5, MoveFacing.Down, _ => false));
        }

        // ── triggers ────────────────────────────────────────────────────────────────────
        private static Trigger Area(int tileX, int tileZ, ushort w, ushort h,
                                    ushort variable, ushort expected, ushort script)
            => new Trigger(tileX / MapFile.mapSize, tileZ / MapFile.mapSize)
            {
                widthX = w,
                heightY = h,
                variableWatched = variable,
                expectedVarValue = expected,
                scriptNumber = script,
                xMapPosition = (short)(tileX % MapFile.mapSize),
                yMapPosition = (short)(tileZ % MapFile.mapSize),
            };

        [Fact]
        public void ATriggerCoversARectangleFromWhereItSits()
        {
            var events = Events(Area(4, 4, w: 3, h: 2, variable: 0x4001, expected: 1, script: 20));
            int Value(ushort v) => 1;

            Assert.NotNull(FieldInteraction.TriggerAt(events, 4, 4, Value));
            Assert.NotNull(FieldInteraction.TriggerAt(events, 6, 5, Value));   // last tile inside
            Assert.Null(FieldInteraction.TriggerAt(events, 7, 4, Value));      // one past the width
            Assert.Null(FieldInteraction.TriggerAt(events, 4, 6, Value));      // one past the height
            Assert.Null(FieldInteraction.TriggerAt(events, 3, 4, Value));      // before it starts
        }

        [Fact]
        public void ATriggerOnlyFiresWhenItsVariableMatches()
        {
            var events = Events(Area(4, 4, w: 1, h: 1, variable: 0x4001, expected: 7, script: 21));

            Assert.NotNull(FieldInteraction.TriggerAt(events, 4, 4, v => 7));
            Assert.Null(FieldInteraction.TriggerAt(events, 4, 4, v => 6));
        }

        [Fact]
        public void ATriggerIsFoundByPositionAloneWhenNoVariableIsGiven()
        {
            // Used to ask the watcher what the variable holds before deciding.
            var events = Events(Area(4, 4, w: 1, h: 1, variable: 0x4001, expected: 7, script: 21));
            Assert.NotNull(FieldInteraction.TriggerAt(events, 4, 4, null));
        }

        // ── warps ───────────────────────────────────────────────────────────────────────
        [Fact]
        public void AWarpIsFoundUnderTheTileYouStandOn()
        {
            var w = new Warp(0, 0) { header = 42, anchor = 3, xMapPosition = 6, yMapPosition = 5 };
            var events = Events(w);

            var found = FieldInteraction.WarpAt(events, 6, 5);
            Assert.NotNull(found);
            Assert.Equal(42, found.header);
            Assert.Equal(3, found.anchor);
            Assert.Null(FieldInteraction.WarpAt(events, 7, 5));
        }

        [Fact]
        public void EveryonesTileCountsAsOccupied()
        {
            var events = Events(Person(6, 5, type: 0, script: 1));
            var tiles = FieldInteraction.OccupiedTiles(events);
            Assert.Contains((6, 5), tiles);
            Assert.DoesNotContain((7, 5), tiles);
        }
    }
}
