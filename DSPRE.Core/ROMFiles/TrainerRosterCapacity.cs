using System;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Conservative, revision-gated capacity for binary trainer roster expansion. The script range
    /// and defeated-flag range are independent; the lower remaining capacity always wins.
    /// </summary>
    public sealed class TrainerRosterCapacity
    {
        private TrainerRosterCapacity(int retailTrainerRecordCount, int retailSpecialIndex,
            int maximumSpecialIndex, int defeatedFlagBase, int exclusiveDefeatedFlagLimit,
            string specialIndexLimitReason)
        {
            RetailTrainerRecordCount = retailTrainerRecordCount;
            RetailSpecialIndex = retailSpecialIndex;
            MaximumSpecialIndex = maximumSpecialIndex;
            DefeatedFlagBase = defeatedFlagBase;
            ExclusiveDefeatedFlagLimit = exclusiveDefeatedFlagLimit;
            SpecialIndexLimitReason = specialIndexLimitReason;
        }

        public int RetailTrainerRecordCount { get; }
        public int RetailSpecialIndex { get; }
        public int MaximumSpecialIndex { get; }
        public int DefeatedFlagBase { get; }
        public int ExclusiveDefeatedFlagLimit { get; }
        public string SpecialIndexLimitReason { get; }

        public static bool TryFor(RomInfo.GameVersions version, RomInfo.GameLanguages language,
            out TrainerRosterCapacity capacity, out string error)
        {
            capacity = null;
            error = null;
            if (version == RomInfo.GameVersions.Platinum && language == RomInfo.GameLanguages.English)
            {
                capacity = new TrainerRosterCapacity(
                    retailTrainerRecordCount: 928,
                    retailSpecialIndex: 928,
                    maximumSpecialIndex: 999,
                    defeatedFlagBase: 0x550,
                    exclusiveDefeatedFlagLimit: 0x960,
                    specialIndexLimitReason: "The shared trainer-script range has no room for another entry.");
                return true;
            }

            if (version == RomInfo.GameVersions.HeartGold &&
                language == RomInfo.GameLanguages.English)
            {
                // IDs 738 and 739 use unnamed flags 0x832 and 0x833. The separate retail
                // LAST_TRAINER_INDEX comparison remains correct for those IDs, but not ID 740.
                capacity = new TrainerRosterCapacity(
                    retailTrainerRecordCount: 738,
                    retailSpecialIndex: 739,
                    maximumSpecialIndex: 741,
                    defeatedFlagBase: 0x550,
                    exclusiveDefeatedFlagLimit: 0x834,
                    specialIndexLimitReason: "English HeartGold currently supports at most two added trainers because its separate trainer-boundary comparison is not expanded.");
                return true;
            }

            error = $"Trainer roster capacity is not verified for {version} {language}.";
            return false;
        }

        public bool CanAdd(int trainerRecordCount, int specialIndex, out string error)
        {
            error = null;
            if (trainerRecordCount < RetailTrainerRecordCount || specialIndex < RetailSpecialIndex)
            {
                error = "The trainer roster is smaller than its verified retail baseline.";
                return false;
            }

            int rosterGrowth = trainerRecordCount - RetailTrainerRecordCount;
            if (specialIndex != RetailSpecialIndex + rosterGrowth)
            {
                error = "The trainer count and shared trainer-script boundary have diverged.";
                return false;
            }

            if (specialIndex >= MaximumSpecialIndex)
            {
                error = SpecialIndexLimitReason;
                return false;
            }

            int nextTrainerId = trainerRecordCount;
            int nextDefeatedFlag = checked(DefeatedFlagBase + nextTrainerId);
            if (nextDefeatedFlag >= ExclusiveDefeatedFlagLimit)
            {
                error = "The next trainer defeated flag is outside the verified expansion range.";
                return false;
            }

            return true;
        }

        public int RemainingAdditions(int trainerRecordCount, int specialIndex)
        {
            if (trainerRecordCount < RetailTrainerRecordCount ||
                specialIndex != RetailSpecialIndex + trainerRecordCount - RetailTrainerRecordCount)
            {
                return 0;
            }

            int scriptCapacity = MaximumSpecialIndex - specialIndex;
            int flagCapacity = ExclusiveDefeatedFlagLimit - (DefeatedFlagBase + trainerRecordCount);
            return Math.Max(0, Math.Min(scriptCapacity, flagCapacity));
        }
    }
}
