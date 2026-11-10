namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Honey tree groups A, B and C (DPPt) in encdata_ex: 2 to 4 in Diamond and Platinum, 5 to 7 in Pearl. Normal trees
    /// pick nothing 10%, A 70% and B 20%; Munchlax trees pick nothing 9%, A 20%, B 70% and C, the Munchlax group, 1%.
    /// </summary>
    /// <remarks>Platinum's files 5 to 7 are copies of 2 to 4 that the game never reads.</remarks>
    public sealed class HoneyTreeEncounterFile : SpeciesSlotArchive
    {
        public const int SlotCount = 6;

        /// <summary>Each slot's odds in percent.</summary>
        public static readonly int[] SlotRates = { 40, 20, 20, 10, 5, 5 };

        public static readonly int[] DiamondPlatinumFileIndices = { 2, 3, 4 };
        public static readonly int[] PearlFileIndices = { 5, 6, 7 };

        public HoneyTreeEncounterFile(byte[] data) : base(SlotCount, new[]
        {
            new SpeciesSlotGroup("Group A (Common)", "70% on normal trees, 20% on Munchlax trees", FileIndices()[0]),
            new SpeciesSlotGroup("Group B (Uncommon)", "20% on normal trees, 70% on Munchlax trees", FileIndices()[1]),
            new SpeciesSlotGroup("Group C (Munchlax)",
                "1% on Munchlax trees only. Each player has 4 Munchlax trees, set by Trainer ID.", FileIndices()[2]),
        }, data) { }

        public static HoneyTreeEncounterFile Load() => new HoneyTreeEncounterFile(ReadMembers(SlotCount, FileIndices()));

        private static int[] FileIndices() =>
            RomInfo.GameVersion == RomInfo.GameVersions.Pearl ? PearlFileIndices : DiamondPlatinumFileIndices;
    }
}
