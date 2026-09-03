using System.IO;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// A Pokemon's cry. These used to assume no ROM was open, which is only true when they run alone:
    /// in a full run another test has already opened one, so asking for Pikachu's cry really did return
    /// sound and the assertions failed. What they were reaching for is checked properly here instead.
    /// </summary>
    [Collection("rom")]
    public class CryPlaybackTests
    {
        private readonly ITestOutputHelper _out;
        public CryPlaybackTests(ITestOutputHelper o) => _out = o;

        [Fact]
        public void TheCrySequenceIsNamedTheWayTheGamesNameIt()
            => Assert.Equal("SEQ_PV", SoundArchive.CrySequenceName);

        /// <summary>
        /// A species number no game has asks nothing of the archive, whether one is open or not. The
        /// zero and the negatives are the ones a caller actually passes by accident.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public void ASpeciesNoGameHasAsksForNothing(int species)
            => Assert.Null(SoundArchive.RenderCry(species));

        /// <summary>An archive with no names at all has no cry sequence, rather than throwing.</summary>
        [Fact]
        public void TheCrySequenceLookupCopesWithAnArchiveThatHasNoNames()
        {
            SoundArchive.Reset();      // the answer is cached, and this asks about a different archive
            Assert.Equal(-1, SoundArchive.CrySequence(null));
            SoundArchive.Reset();      // leave nothing behind for whatever runs next
        }

        /// <summary>
        /// The sequence found is really in the archive that is open, and really named SEQ_PV.
        ///
        /// The answer is cached, and the cache used to survive a ROM switch, so a second game would have
        /// played its cries from the first game's sequence number. This test CANNOT catch that on the
        /// retail games: HeartGold, Platinum and Diamond all keep SEQ_PV at index 2, so a stale number
        /// happens to be the right one. It catches it for any ROM that moves the sequence, which is what
        /// a romhack that rebuilds its sound archive does.
        /// </summary>
        [Fact]
        public void TheCrySequenceBelongsToTheRomThatIsOpen()
        {
            int played = 0;
            foreach (var (code, project, name) in new[]
            {
                ("IPKE", TestRoms.HeartGold, "HeartGold"),
                ("CPUE", TestRoms.Platinum, "Platinum"),
                ("ADAE", TestRoms.Diamond, "Diamond"),
            })
            {
                if (!Directory.Exists(project)) { _out.WriteLine($"{name}: not here, skipped"); continue; }
                new RomInfo(code, project);

                var sdat = SoundArchive.Load();
                if (sdat == null) { _out.WriteLine($"{name}: no sound archive, skipped"); continue; }

                int seq = SoundArchive.CrySequence(sdat);
                _out.WriteLine($"{name}: cries play from sequence {seq} of {sdat.Sequences.Count}");
                Assert.True(seq >= 0, $"{name} should have a cry sequence");
                Assert.True(sdat.SeqNames.TryGetValue(seq, out string named),
                            $"{name}: sequence {seq} is not in this archive at all");
                Assert.Equal(SoundArchive.CrySequenceName, named);
                played++;
            }
            Assert.True(played > 0, "no game was available here, so this proved nothing");
        }
    }
}
