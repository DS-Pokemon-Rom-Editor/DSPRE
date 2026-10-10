using DSPRE.HgEngine;

namespace DSPRE.ROMFiles
{
    /// <summary>Prize money: last Pokémon's level x 4 x class multiplier, doubled in a double battle and by Amulet Coin.</summary>
    public static class TrainerPrize
    {
        /// <summary>The class's prize multiplier from wherever this ROM keeps it: the class metadata patch, hg-engine's source, or the game's table.</summary>
        public static bool TryGetMultiplier(int trainerClass, out int multiplier)
        {
            multiplier = 0;
            if (HgEngineProject.IsActive) return HgEngineTrainerClassTables.TryGetPrizeMultiplier(trainerClass, out multiplier);
            if (TrainerClassMetadataStore.DetectCurrentRom(out _) == TrainerClassMetadataDetectionState.SchemaV1)
            {
                if (!TrainerClassMetadataStore.TryReadCommonFields(trainerClass, out TrainerClassMetadataCommonFields fields, out _)) return false;
                multiplier = fields.PrizeCoefficient;
                return true;
            }
            if (!TrainerClassTableExpansion.TryReadPrizeMul(trainerClass, out byte mul, out _)) return false;
            multiplier = mul;
            return true;
        }

        public static int Amount(int lastLevel, int multiplier, bool doubleBattle) => lastLevel * 4 * multiplier * (doubleBattle ? 2 : 1);
    }
}
