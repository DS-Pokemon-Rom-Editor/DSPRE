using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Two people never share a tile. The engine refuses a step onto a tile another object is standing
    /// on or has just left: FieldOBJ_MoveHitCheckFellowMust compares both the other object's current
    /// position and its old one (fieldobj_move.c:1552-1587), which is what stops somebody cutting
    /// through the gap behind a walker.
    /// </summary>
    public class FieldOccupancyTests
    {
        private static MapCollisionGrid OpenMap()
        {
            var g = new MapCollisionGrid();
            g.Add(0, 0, new byte[MapFile.mapSize, MapFile.mapSize]);
            return g;
        }

        [Fact]
        public void AWalkerHoldsTheTileItIsLeavingUntilItArrives()
        {
            var a = new OverworldAnimator(OverworldMovements.Find(0x03), MoveFacing.Down, 8, 8, 0, 5);

            int guard = 0;
            while (!a.IsWalking && guard++ < 800) a.Advance(1);
            Assert.True(a.IsWalking, "it never started walking");

            // Mid-step the two are different tiles, and they are next to each other.
            Assert.True(a.FromOffsetX != a.OffsetX || a.FromOffsetZ != a.OffsetZ);
            Assert.Equal(1, Math.Abs(a.FromOffsetX - a.OffsetX) + Math.Abs(a.FromOffsetZ - a.OffsetZ));

            // Once the step lands, the tile behind is released.
            for (int i = 0; i < OverworldAnimator.WalkFrames && a.IsWalking; i++) a.Advance(1);
            Assert.Equal(a.OffsetX, a.FromOffsetX);
            Assert.Equal(a.OffsetZ, a.FromOffsetZ);
        }

        [Fact]
        public void ThePlayerHoldsTheTileItIsLeavingUntilItArrives()
        {
            var p = new FieldPlayer(5, 5, MoveFacing.Right, OpenMap());
            Assert.Equal(p.TileX, p.FromX);

            p.Go(MoveFacing.Right);
            Assert.True(p.IsWalking);
            Assert.Equal(6, p.TileX);
            Assert.Equal(5, p.FromX);       // still holding the tile behind

            p.Advance(FieldPlayer.WalkFrames);
            Assert.Equal(6, p.TileX);
            Assert.Equal(6, p.FromX);       // released
        }

        [Fact]
        public void ThePlayerCannotWalkOntoSomebody()
        {
            var p = new FieldPlayer(5, 5, MoveFacing.Right, OpenMap(), (x, z) => x == 6 && z == 5);
            p.Go(MoveFacing.Right);

            Assert.False(p.IsWalking);
            Assert.Equal(5, p.TileX);
            Assert.Equal(MoveFacing.Right, p.Facing);   // it still turns to face them
        }

        [Fact]
        public void TwoWanderersNeverEndUpOnTheSameTile()
        {
            // Two wanderers penned into the same small patch, each refusing tiles the other holds, run
            // the way the preview runs them. If the rule works they never collide, however long it goes.
            var people = new List<OverworldAnimator>();
            var home = new[] { (x: 10, z: 10), (x: 11, z: 10), (x: 10, z: 11), (x: 11, z: 11) };

            bool Held(int x, int z, int mine)
            {
                for (int i = 0; i < people.Count; i++)
                {
                    if (i == mine) continue;
                    int hx = home[i].x + people[i].OffsetX, hz = home[i].z + people[i].OffsetZ;
                    if (hx == x && hz == z) return true;
                    int fx = home[i].x + people[i].FromOffsetX, fz = home[i].z + people[i].FromOffsetZ;
                    if (fx == x && fz == z) return true;
                }
                return false;
            }

            for (int i = 0; i < home.Length; i++)
            {
                int me = i, hx = home[i].x, hz = home[i].z;
                people.Add(new OverworldAnimator(OverworldMovements.Find(0x03), MoveFacing.Down,
                                                 1, 1, 0, 100 + i,
                                                 (dx, dz) => Held(hx + dx, hz + dz, me)));
            }

            int stepsSeen = 0;
            for (int frame = 0; frame < 6000; frame++)
            {
                foreach (var a in people) a.Advance(1);
                if (people.Any(a => a.IsWalking)) stepsSeen++;

                // Nobody may be standing on the same tile as anybody else.
                var standing = people.Select((a, i) => (home[i].x + a.OffsetX, home[i].z + a.OffsetZ)).ToList();
                Assert.Equal(standing.Count, standing.Distinct().Count());
            }

            // The test is only worth anything if they actually moved about.
            Assert.True(stepsSeen > 200, $"they barely moved ({stepsSeen} frames of walking), so this proved nothing");
        }

        [Fact]
        public void NobodyCutsThroughTheGapBehindAWalker()
        {
            // One walker crossing a corridor, one behind it trying to take the tile it just left. The
            // old-position half of the rule is the only thing stopping them meeting in the middle.
            var lead = new OverworldAnimator(OverworldMovements.Find(0x03), MoveFacing.Right, 4, 0, 0, 7);
            int leadHomeX = 20, leadHomeZ = 20;

            var chaser = new OverworldAnimator(OverworldMovements.Find(0x03), MoveFacing.Right, 4, 0, 0, 9,
                (dx, dz) =>
                {
                    int x = 19 + dx, z = 20 + dz;
                    return (leadHomeX + lead.OffsetX == x && leadHomeZ + lead.OffsetZ == z)
                        || (leadHomeX + lead.FromOffsetX == x && leadHomeZ + lead.FromOffsetZ == z);
                });

            for (int frame = 0; frame < 4000; frame++)
            {
                lead.Advance(1);
                chaser.Advance(1);
                Assert.False(leadHomeX + lead.OffsetX == 19 + chaser.OffsetX
                          && leadHomeZ + lead.OffsetZ == 20 + chaser.OffsetZ,
                             $"they ended up on the same tile at frame {frame}");
            }
        }
    }
}
