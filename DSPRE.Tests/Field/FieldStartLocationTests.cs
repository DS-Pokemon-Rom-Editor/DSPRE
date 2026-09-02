using System.Collections.Generic;
using DSPRE;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Opening on the town a new game starts in. </summary>
    public class FieldStartLocationTests
    {
        [Fact]
        public void EachFamilyKnowsItsStartingTown()
        {
            Assert.Equal("T20", FieldStartLocation.TownFor(RomInfo.GameFamilies.HGSS));
            Assert.Equal("T01", FieldStartLocation.TownFor(RomInfo.GameFamilies.Plat));
            Assert.Equal("T01", FieldStartLocation.TownFor(RomInfo.GameFamilies.DP));
            Assert.Equal("T20R0202", FieldStartLocation.RoomFor(RomInfo.GameFamilies.HGSS));
        }

        [Fact]
        public void TheTownIsFoundInAListMeantForShowingPeople()
        {
            // The header list reads like this, with the number in front of the internal name.
            var names = new List<string> { "000 -   MYSTERY", "006 -   D17R1101", "060 -   T20", "064 -   T20R0202" };
            Assert.Equal(2, FieldStartLocation.HeaderFor(RomInfo.GameFamilies.HGSS, names));
        }

        [Fact]
        public void TheTownIsAlsoFoundInNamesReadStraightOutOfTheRom()
        {
            // Those are padded out with zero bytes rather than spaces.
            var names = new List<string> { "MYSTERY\0\0", "D17R1101\0", "T20\0\0\0\0\0\0" };
            Assert.Equal(2, FieldStartLocation.HeaderFor(RomInfo.GameFamilies.HGSS, names));
        }

        [Fact]
        public void TheRoomIsUsedWhenTheTownItselfIsNotThere()
        {
            var names = new List<string> { "000 -   MYSTERY", "064 -   T20R0202" };
            Assert.Equal(1, FieldStartLocation.HeaderFor(RomInfo.GameFamilies.HGSS, names));
        }

        [Fact]
        public void NotFindingItSaysSoRatherThanGuessing()
        {
            var names = new List<string> { "000 -   MYSTERY", "001 -   SOMEWHERE" };
            Assert.Equal(-1, FieldStartLocation.HeaderFor(RomInfo.GameFamilies.HGSS, names));
            Assert.Equal(-1, FieldStartLocation.HeaderFor(RomInfo.GameFamilies.HGSS, null));
            Assert.Equal(-1, FieldStartLocation.HeaderFor(RomInfo.GameFamilies.NULL, names));
        }
    }
}
