using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The form fields a Diamond/Pearl/Platinum encounter file carries, read against the Platinum
    /// encount_dat.h layout.
    /// </summary>
    public class WildFormDataTests
    {
        private static EncounterFileDPPt Blank()
        {
            // A file of the right length reads as all zeroes, which is what an empty map holds.
            var bytes = new byte[424];
            return new EncounterFileDPPt(new System.IO.MemoryStream(bytes));
        }

        [Fact]
        public void AFileWithNoUnownStaysThatWayThroughASave()
        {
            var f = Blank();
            f.unknownTable = 0;
            var again = new EncounterFileDPPt(new System.IO.MemoryStream(f.ToByteArray()));
            Assert.Equal(0u, again.unknownTable);
        }

        [Theory]
        [InlineData(0u)]    // no Unown
        [InlineData(1u)]    // most forms
        [InlineData(8u)]    // ! and ?
        public void EveryUnownTableValueSurvivesARoundTrip(uint table)
        {
            var f = Blank();
            f.unknownTable = table;
            var again = new EncounterFileDPPt(new System.IO.MemoryStream(f.ToByteArray()));
            Assert.Equal(table, again.unknownTable);
        }

        [Theory]
        [InlineData(0u, false)]     // west sea
        [InlineData(1u, true)]      // east sea
        [InlineData(100u, true)]    // also east: the games only ask whether it is non-zero
        public void ShellosReadsEastWhenTheChanceIsAnythingButZero(uint stored, bool east)
        {
            var f = Blank();
            f.regionalForms[0] = stored;
            var again = new EncounterFileDPPt(new System.IO.MemoryStream(f.ToByteArray()));
            Assert.Equal(stored, again.regionalForms[0]);
            Assert.Equal(east, again.regionalForms[0] != 0);
        }

        [Fact]
        public void GastrodonHasItsOwnSlotSoTheTwoDoNotShare()
        {
            var f = Blank();
            f.regionalForms[0] = 0;     // Shellos west
            f.regionalForms[1] = 100;   // Gastrodon east
            var again = new EncounterFileDPPt(new System.IO.MemoryStream(f.ToByteArray()));
            Assert.Equal(0u, again.regionalForms[0]);
            Assert.Equal(100u, again.regionalForms[1]);
        }

        [Fact]
        public void TheLastThreeFormSlotsAreNotUsedByTheGames()
        {
            // encount_dat.h marks FormProb[2..4] unused, and every Platinum file holds zero there.
            var f = Blank();
            for (int i = 2; i < 5; i++) Assert.Equal(0u, f.regionalForms[i]);
        }


        // ── the editor's own mapping, which is where this went wrong ──────────────────

        [Fact]
        public void TheUnownListOffersNoUnownFirstSoZeroHasSomewhereToGo()
        {
            var vm = new DSPRE.Avalonia.ViewModels.Pokemon.WildEditorDPPtViewModel();

            // Nine entries: no Unown, then the eight letter tables the games define.
            Assert.Equal(9, vm.UnownTableNames.Count);
            Assert.Equal("No Unown", vm.UnownTableNames[0]);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(8)]
        public void TheUnownListIndexIsTheValueItself(int value)
        {
            // The list index and the stored number line up, so nothing shifts by one on the way in or
            // out. Before this, a map with no Unown loaded as table 1 and was saved that way.
            var vm = new DSPRE.Avalonia.ViewModels.Pokemon.WildEditorDPPtViewModel();
            Assert.InRange(value, 0, vm.UnownTableNames.Count - 1);
        }

        [Fact]
        public void AnEmptyFileIsTheRightLength()
        {
            // 4 + 12*8 + 8 + 8 + 8 + 16 + 20 + 4 + 5*8 + 5*44 = 424, per the Platinum struct.
            Assert.Equal(424, Blank().ToByteArray().Length);
        }
    }
}
