using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// A Pokemon's cry.
    ///
    /// There is no sequence per Pokemon. The games play one shared sequence and hand it the Pokemon's own
    /// instruments instead of its own: snd_play.c:1091 calls the sequence starter with the species number
    /// where the bank goes and SEQ_PV where the sequence goes. In HeartGold SEQ_PV is sequence 2, its own
    /// bank is 1, and bank N belongs to species N.
    ///
    /// That sequence is four commands: tempo, program 0, volume, pan, then one note with a length of zero.
    /// A length of zero is never counted down (snd_exchannel.c:505 only counts while length is above zero),
    /// so the note sounds until its sample runs out. Reading that as a zero-length note rendered every cry
    /// in the game as silence.
    /// </summary>
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
