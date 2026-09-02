using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>A Pokemon's cry.</summary>
    public class CryPlaybackTests
    {
        [Fact]
        public void TheCrySequenceIsNamedTheWayTheGamesNameIt()
            => Assert.Equal("SEQ_PV", SoundArchive.CrySequenceName);

        [Fact]
        public void NothingIsAskedOfAnArchiveThatIsNotThere()
        {
            // No ROM is open in a test run, so this must come back empty rather than throwing.
            Assert.Null(SoundArchive.RenderCry(25));
            Assert.Null(SoundArchive.RenderCry(0));
            Assert.Null(SoundArchive.RenderCry(-1));
        }

        [Fact]
        public void ASpeciesOutOfRangeAsksForNothing()
        {
            Assert.Null(SoundArchive.RenderCry(int.MaxValue));
            Assert.Null(SoundArchive.RenderCry(int.MinValue));
        }

        [Fact]
        public void TheCrySequenceLookupCopesWithAnArchiveThatHasNoNames()
            => Assert.Equal(-1, SoundArchive.CrySequence(null));
    }
}
