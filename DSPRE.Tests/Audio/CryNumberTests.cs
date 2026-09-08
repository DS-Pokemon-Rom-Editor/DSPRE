using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The species-to-cry-number rule hg-engine's GrabCryNumSpeciesForm implements. Its mega branch
    /// cancels out (CRY_SPECIES_FORMS_BASE and SPECIES_MEGA_START are both SPECIES_MAX_MON_NUM + 1), so
    /// the number is the species itself apart from the slots between the old last species and its first
    /// new one, which the engine sends to Bulbasaur's cry.
    /// </summary>
    public class CryNumberTests
    {
        [Theory]
        [InlineData(1, 1)]
        [InlineData(25, 25)]
        [InlineData(493, 493)]     // last vanilla species, still its own cry
        [InlineData(544, 544)]     // first expanded species, on the far side of the gap
        [InlineData(1148, 1148)]   // the highest cry the checkout ships
        public void ASpeciesWithACryOfItsOwnKeepsItsNumber(int species, int expected)
            => Assert.Equal(expected, SoundArchive.CryNumberFor(species));

        [Theory]
        [InlineData(494)]
        [InlineData(500)]
        [InlineData(543)]
        public void TheLimboSlotsFallBackToTheFirstCry(int species)
            => Assert.Equal(1, SoundArchive.CryNumberFor(species));
    }
}
