using DSPRE;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Trainer Pokemon personality as the games compute it; class 0 rolls nothing, so the seed itself is shifted.</summary>
    [Collection("rom")]
    public class DVCalculatorTests
    {
        private static uint FromTheGame(uint trainer, uint trainerClass, uint species, byte level, byte difficulty, uint gender)
        {
            uint personality = (uint)difficulty + level + species + trainer;
            uint state = personality;
            for (uint j = 0; j < trainerClass; j++)
            {
                state = state * 1103515245 + 24691;
                personality = state >> 16;
            }
            return (personality << 8) + gender;
        }

        [Theory]
        [InlineData(0u)]
        [InlineData(1u)]
        [InlineData(63u)]
        public void PersonalityMatchesTheGame(uint trainerClass)
        {
            const uint trainer = 57, species = 399;
            const byte level = 8, difficulty = 120;

            DVCalculator.ResetGenderMod(true);
            uint made = DVCalculator.generatePID(trainer, trainerClass, species, level, 127, 0, 0, difficulty);

            Assert.Equal(FromTheGame(trainer, trainerClass, species, level, difficulty, 136), made);
        }
    }
}
