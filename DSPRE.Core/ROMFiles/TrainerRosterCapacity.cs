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
                    specialIndexLimitReason: "No more trainers can be added.");
                return true;
            }

            if (version == RomInfo.GameVersions.HeartGold &&
                language == RomInfo.GameLanguages.English)
            {
                // Defeated flags 0x834 to 0x95F are unused and 0x960 is the first in use. Trainer scripts are 3000 + id - 1,
                // partners start at 5000, and the phone's LAST_TRAINER_INDEX check does the same on both branches.
                capacity = new TrainerRosterCapacity(
                    retailTrainerRecordCount: 738,
                    retailSpecialIndex: 739,
                    maximumSpecialIndex: 1999,
                    defeatedFlagBase: 0x550,
                    exclusiveDefeatedFlagLimit: 0x960,
                    specialIndexLimitReason: "The trainer scripts have no more room.");
                return true;
            }

            error = $"Adding trainers isn't supported for {version} ({language}) yet.";
            return false;
        }

        public bool CanAdd(int trainerRecordCount, int specialIndex, out string error)
        {
            error = null;
            if (trainerRecordCount < RetailTrainerRecordCount || specialIndex < RetailSpecialIndex)
            {
                error = "This ROM has fewer trainers than the game it came from, so DSPRE won't change the list.";
                return false;
            }

            int rosterGrowth = trainerRecordCount - RetailTrainerRecordCount;
            if (specialIndex != RetailSpecialIndex + rosterGrowth)
            {
                error = "The trainer count and the shared trainer script no longer agree, so DSPRE won't change the list.";
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
                error = "No more trainers can be added: the next one has no free defeated flag.";
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
