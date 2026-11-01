namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Works out what a trade Pokémon gets from its PID. Follows pokeheartgold src/pokemon.c: CreateBoxMon,
    /// GetGenderBySpeciesAndPersonality and SHINY_CHECK.
    /// </summary>
    public static class TradePersonality
    {
        public const int Male = 0, Female = 1, Genderless = 2;
        private const byte AlwaysMale = SpeciesFile.GenderRatioMale, AlwaysFemale = SpeciesFile.GenderRatioFemale, NoGender = SpeciesFile.GenderRatioGenderless;
        private const uint MaxPid = 0x7FFFFFFF, MaxPidHigh = MaxPid >> 16;   // The PID field in the editor stops at 2147483647.
        private const uint ShinyValues = 8;
        private const uint PlainSearch = 25 * 256 * 4;   // Every mix of nature, gender and ability shows up within a quarter of this.

        public static readonly string[] GenderNames = { "Male", "Female", "Genderless" };

        public static int AbilitySlot(uint pid, ushort secondAbility) => secondAbility != 0 ? (int)(pid & 1) : 0;

        public static int Gender(uint pid, byte ratio) => ratio switch
        {
            AlwaysMale => Male,
            AlwaysFemale => Female,
            NoGender => Genderless,
            _ => ratio > (byte)pid ? Female : Male,
        };

        public static int[] Genders(byte ratio) => ratio switch
        {
            AlwaysMale => new[] { Male },
            AlwaysFemale => new[] { Female },
            NoGender => new[] { Genderless },
            _ => new[] { Male, Female },
        };

        public static bool IsShiny(uint otId, uint pid) => ((otId >> 16) ^ (otId & 0xFFFF) ^ (pid >> 16) ^ (pid & 0xFFFF)) < ShinyValues;

        /// <summary>Finds a PID near <paramref name="from"/> with these choices, or returns <paramref name="from"/> if there is none.</summary>
        public static uint Find(uint from, uint otId, int nature, int abilitySlot, int gender, bool shiny, byte ratio, ushort secondAbility)
        {
            bool Fits(uint pid) => DVCalculator.GetNatureFromPID(pid) == nature && AbilitySlot(pid, secondAbility) == abilitySlot && Gender(pid, ratio) == gender;

            if (shiny)
            {
                uint otBits = (otId >> 16) ^ (otId & 0xFFFF);
                for (uint i = 0; i <= MaxPidHigh; i++)
                {
                    uint high = ((from >> 16) + i) & MaxPidHigh;
                    for (uint low = 0; low < ShinyValues; low++)
                    {
                        uint pid = (high << 16) | (high ^ otBits ^ low);
                        if (Fits(pid)) return pid;
                    }
                }
                return from;
            }

            uint start = from < MaxPid - PlainSearch ? from : 0;
            for (uint pid = start; pid < start + PlainSearch; pid++)
                if (Fits(pid) && !IsShiny(otId, pid)) return pid;
            return from;
        }
    }
}
