namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The Great Marsh's daily Pokémon (DPPt) in encdata_ex files 9 (after the National Dex) and 10 (before), 32 slots
    /// each. Each day's pick replaces two 5% slots in each of the six areas.
    /// </summary>
    public sealed class GreatMarshEncounterFile : SpeciesSlotArchive
    {
        public const int SlotCount = 32;
        public const int PostNationalDexFileIndex = 9;
        public const int PreNationalDexFileIndex = 10;

        public GreatMarshEncounterFile(byte[] data) : base(SlotCount, new[]
        {
            new SpeciesSlotGroup("Post-National Dex",
                "Pokémon you can find once you have the National Pokédex, mostly from other regions.", PostNationalDexFileIndex),
            new SpeciesSlotGroup("Pre-National Dex",
                "Pokémon you can find before you have the National Pokédex, all from Sinnoh.", PreNationalDexFileIndex),
        }, data) { }

        public static GreatMarshEncounterFile Load() =>
            new GreatMarshEncounterFile(ReadMembers(SlotCount, PostNationalDexFileIndex, PreNationalDexFileIndex));
    }
}
