using System.Linq;
using DSPRE;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Pins the movement tables per game: Diamond/Pearl 0-54, Platinum 0-67, HeartGold/SoulSilver 0-56
    /// without the berry patch at 47.
    /// </summary>
    public class OverworldMovementTests
    {
        [Fact]
        public void DiamondPearlDefinesZeroToFiftyFour()
        {
            var values = OverworldMovements.For(RomInfo.GameFamilies.DP).Select(m => (int)m.Value);
            Assert.Equal(Enumerable.Range(0, 55), values);
        }

        [Fact]
        public void PlatinumDefinesZeroToSixtySeven()
        {
            var values = OverworldMovements.For(RomInfo.GameFamilies.Plat).Select(m => (int)m.Value);
            Assert.Equal(Enumerable.Range(0, 68), values);
        }

        [Fact]
        public void HeartGoldDefinesZeroToFiftySixWithoutTheBerryPatch()
        {
            var values = OverworldMovements.For(RomInfo.GameFamilies.HGSS).Select(m => (int)m.Value);
            Assert.Equal(Enumerable.Range(0, 57).Where(v => v != 47), values);
            Assert.False(OverworldMovements.IsDefined(RomInfo.GameFamilies.HGSS, 47));
            Assert.True(OverworldMovements.IsDefined(RomInfo.GameFamilies.Plat, 47));
        }

        [Fact]
        public void ValuesPastEachGamesListAreNotDefined()
        {
            Assert.False(OverworldMovements.IsDefined(RomInfo.GameFamilies.DP, 55));
            Assert.False(OverworldMovements.IsDefined(RomInfo.GameFamilies.HGSS, 57));
            Assert.False(OverworldMovements.IsDefined(RomInfo.GameFamilies.Plat, 68));
            Assert.False(OverworldMovements.IsDefined(RomInfo.GameFamilies.Plat, 0xFF));
            Assert.False(OverworldMovements.IsDefined(RomInfo.GameFamilies.Plat, 0x1FF));
        }

        [Fact]
        public void WanderAxesAreConstrained()
        {
            Assert.Equal(new[] { MoveFacing.Up, MoveFacing.Down },
                         OverworldMovements.Find(4).Facings.ToArray());
            Assert.Equal(new[] { MoveFacing.Left, MoveFacing.Right },
                         OverworldMovements.Find(5).Facings.ToArray());
            Assert.Equal(4, OverworldMovements.Find(3).Facings.Count);
        }

        [Fact]
        public void EveryRouteHasFourLegs()
        {
            for (int v = 21; v <= 44; v++)
            {
                var route = OverworldMovements.Find(v);
                Assert.Equal(MoveKind.Route, route.Kind);
                Assert.Equal(4, route.Facings.Count);
            }
            Assert.Equal(new[] { MoveFacing.Up, MoveFacing.Right, MoveFacing.Left, MoveFacing.Down },
                         OverworldMovements.Find(21).Facings.ToArray());
            // 37 goes round a rectangle: north, west, south, east.
            Assert.Equal(new[] { MoveFacing.Up, MoveFacing.Left, MoveFacing.Down, MoveFacing.Right },
                         OverworldMovements.Find(37).Facings.ToArray());
        }

        [Fact]
        public void OnlyFourCodesActuallyWalkAtRandom()
        {
            // 67 is 5's handler with only the range checked, so it walks through walls.
            var walking = OverworldMovements.For(RomInfo.GameFamilies.Plat)
                                            .Where(m => m.Kind == MoveKind.Wander)
                                            .Select(m => m.Value).ToArray();
            Assert.Equal(new byte[] { 3, 4, 5, 67 }, walking);
        }

        [Fact]
        public void WalkBackAndForthTakesItsDirectionFromTheEvent()
        {
            Assert.True(OverworldMovements.Find(20).RouteFollowsEventFacing);
            Assert.False(OverworldMovements.Find(21).RouteFollowsEventFacing);
        }

        [Fact]
        public void SpinDirectionsDiffer()
        {
            Assert.False(OverworldMovements.Find(18).SpinClockwise);
            Assert.True(OverworldMovements.Find(19).SpinClockwise);
        }
    }
}
