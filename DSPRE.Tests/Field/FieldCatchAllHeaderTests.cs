using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>The header every unwalked matrix square belongs to.</summary>
    public class FieldCatchAllHeaderTests
    {
        [Theory]
        [InlineData(0)]     // a header with no squares of its own
        [InlineData(1)]
        [InlineData(6)]     // the largest real header in HeartGold
        [InlineData(32)]    // right on the line, still treated as a place
        public void ARealPlaceIsNotTheCatchAll(int squares)
            => Assert.False(FieldCatchAllHeader.IsCatchAll(squares));

        [Theory]
        [InlineData(33)]
        [InlineData(291)]   // what "EVERYWHERE" actually owns
        public void TheCatchAllIsRecognisedByHowMuchItOwns(int squares)
            => Assert.True(FieldCatchAllHeader.IsCatchAll(squares));

        [Fact]
        public void TheLineSitsWellClearOfAnyRealHeader()
        {
            // Six is the largest real one, so the line has room either side rather than sitting on it.
            Assert.True(FieldCatchAllHeader.MostSquaresARealHeaderOwns > 6 * 2);
            Assert.True(FieldCatchAllHeader.MostSquaresARealHeaderOwns < 291);
        }

        [Fact]
        public void ItSaysWhyThereIsNothingToEdit()
        {
            Assert.Contains("nothing here to edit", FieldCatchAllHeader.Explanation);
            Assert.Contains("Pick a header", FieldCatchAllHeader.Explanation);
        }
    }
}
