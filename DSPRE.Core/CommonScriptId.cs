namespace DSPRE
{
    /// <summary>
    /// Resolves a "common"/global script number against the CommonScript ID reference tables for
    /// Diamond/Pearl, Platinum and HGSS (identical across languages for a given game). Below 2000, a
    /// script number is always local to the current event's own paired script file, which DSPRE already
    /// handles; this resolver is only meant to be consulted as a fallback once that local lookup fails.
    /// Each game has its own table because some ranges changed meaning between games.
    /// </summary>
    public static class CommonScriptId
    {
        public enum Kind { NotCommon, Resolved }

        public sealed class Result
        {
            public Kind Kind;
            public int ScriptArchiveId;
            public int TextArchiveId;

            /// <summary>What the range holds, such as "Hidden items".</summary>
            public string Name;

            // 0-based, exactly as the game's "ID - lowerBound" gives it. DSPRE's own script numbering
            // (ScriptCommandContainer.manualUserID, what the Script editor shows as "Script 1") is 1-based,
            // so use ManualUserId for anything shown to the user or matched against a real ScriptFile.
            public int LocalScriptId;
            public int ManualUserId => LocalScriptId + 1;

            public int RangeLower;
            public int RangeUpper;

            /// <summary>Where the script lives, as shown to people.</summary>
            public string Where => $"{Name}: script file {ScriptArchiveId}, script {ManualUserId}";
        }

        private readonly record struct Bracket(int Lower, int Upper, int ScriptArchive, int TextArchive, string Name);

        // pokediamond LoadScriptsAndMessagesByMapId (arm9/asm/unk_02038C78.s): each branch compares against the
        // range start, loads a script and a message archive, and subtracts that start. Names follow Platinum's.
        private static readonly Bracket[] DpBrackets =
        {
            new(10300, 65536, 977,  496, "Rival talk"),
            new(10200, 10300, 373,  332, "Mystery Gift deliveryman"),
            new(10150, 10200, 1042, 562, "TV interviews"),
            new(10100, 10150, 1041, 563, "TV"),
            new(10000, 10100, 375,  334, "Field moves"),
            new(9950,  10000, 376,  335, "Pokédex rating"),
            new(9900,  9950,  365,  199, "Unused"),
            new(9800,  9900,  206,  203, "Contests"),
            new(9700,  9800,  387,  378, "Partners"),
            new(9600,  9700,  377,  199, "New game"),
            new(9500,  9600,  464,  492, "Day Care"),
            new(9400,  9500,  391,  381, "Poffins"),
            new(9300,  9400,  372,  329, "Group"),
            new(9200,  9300,  388,  379, "Pokémon Center B1F"),
            new(9100,  9200,  0,    9,   "Communication club"),
            new(9000,  9100,  207,  207, "Pokémon Center 2F"),
            new(8970,  9000,  390,  7,   "Poké Radar"),
            new(8950,  8970,  463,  486, "VS Seeker"),
            new(8900,  8950,  389,  380, "Chatot"),
            new(8800,  8900,  462,  485, "Safari Zone"),
            new(8000,  8800,  374,  333, "Hidden items"),
            new(7000,  8000,  370,  325, "Item balls"),
            new(5000,  7000,  1040, 199, "Double battles"),
            new(3000,  5000,  1040, 199, "Trainer battles"),
            new(2800,  3000,  378,  350, "Berry soil"),
            new(2500,  2800,  1,    13,  "Background events"),
            new(2000,  2500,  205,  199, "Common scripts"),
        };

        // pokeplatinum src/script_manager.c SCRIPT_RANGE_TABLE, with archive numbers from scripts.order and
        // generated/text_banks.txt.
        private static readonly Bracket[] PlatBrackets =
        {
            new(10490, 65536, 499,  541, "Scratch-off cards"),
            new(10450, 10490, 500,  16,  "Battle Frontier records"),
            new(10400, 10450, 400,  203, "Pokémon Center daily trainers"),
            new(10300, 10400, 1051, 552, "Rival talk"),
            new(10200, 10300, 407,  379, "Mystery Gift deliveryman"),
            new(10150, 10200, 1116, 621, "TV interviews"),
            new(10100, 10150, 1115, 622, "TV"),
            new(10000, 10100, 409,  381, "Field moves"),
            new(9950,  10000, 411,  383, "Pokédex rating"),
            new(9900,  9950,  397,  213, "Unused"),
            new(9800,  9900,  212,  217, "Contests"),
            new(9700,  9800,  422,  429, "Partners"),
            new(9600,  9700,  412,  213, "New game"),
            new(9500,  9600,  501,  547, "Day Care"),
            new(9400,  9500,  426,  432, "Poffins"),
            new(9300,  9400,  406,  374, "Group"),
            new(9200,  9300,  423,  430, "Pokémon Center B1F"),
            new(9100,  9200,  0,    11,  "Communication club"),
            new(9000,  9100,  213,  221, "Pokémon Center 2F"),
            new(8970,  9000,  425,  7,   "Poké Radar"),
            new(8950,  8970,  498,  539, "VS Seeker"),
            new(8900,  8950,  424,  431, "Chatot"),
            new(8800,  8900,  497,  538, "Safari Zone"),
            new(8000,  8800,  408,  380, "Hidden items"),
            new(7000,  8000,  404,  369, "Item balls"),
            new(5000,  7000,  1114, 213, "Double battles"),
            new(3000,  5000,  1114, 213, "Trainer battles"),
            new(2800,  3000,  413,  397, "Berry soil"),
            new(2500,  2800,  1,    17,  "Background events"),
            new(2000,  2500,  211,  213, "Common scripts"),
        };

        // pokeheartgold src/script_manager.c sScriptBankMapping, with the starts from include/constants/std_script.h.
        private static readonly Bracket[] HgssBrackets =
        {
            new(10490, 65536, 263, 433, "Scratch-off cards"),
            new(10450, 10490, 264, 19,  "Battle Frontier records"),
            new(10440, 10450, 2,   748, "Frontier move tutor"),
            new(10400, 10440, 151, 246, "Bug-Catching Contest"),
            new(10350, 10400, 952, 726, "Trainer House"),
            new(10300, 10350, 734, 444, "Friend"),
            new(10200, 10300, 144, 209, "Mystery Gift deliveryman"),
            new(10150, 10200, 955, 732, "TV interviews"),
            new(10100, 10150, 954, 733, "TV"),
            new(10000, 10100, 146, 211, "Field moves"),
            new(9950,  10000, 148, 666, "Pokédex rating"),
            new(9900,  9950,  136, 40,  "Unused"),
            new(9850,  9900,  167, 312, "Pokéathlon"),
            new(9800,  9850,  166, 43,  "Pokéathlon reception"),
            new(9700,  9800,  163, 266, "Following Pokémon"),
            new(9600,  9700,  149, 40,  "New game"),
            new(9500,  9600,  265, 439, "Day Care"),
            new(9300,  9500,  143, 204, "Group"),
            new(9200,  9300,  164, 267, "Wi-Fi reception"),
            new(9100,  9200,  0,   14,  "Colosseum"),
            new(9000,  9100,  4,   46,  "Communication reception"),
            new(8900,  9000,  165, 268, "Chatot"),
            new(8800,  8900,  262, 427, "Safari Zone"),
            new(8000,  8800,  145, 210, "Hidden items"),
            new(7000,  8000,  141, 199, "Item balls"),
            new(5000,  7000,  953, 40,  "Double battles"),
            new(3000,  5000,  953, 40,  "Trainer battles"),
            new(2800,  3000,  150, 23,  "Apricorn trees"),
            new(2500,  2800,  1,   20,  "Background events"),
            new(2000,  2500,  3,   40,  "Common scripts"),
        };

        /// <summary>Resolves a script number that fell outside the current event's own paired script
        /// file. Only meaningful for numbers &gt;= 2000; everything else returns NotCommon so callers keep
        /// their existing local-only behavior.</summary>
        public static Result Resolve(RomInfo.GameFamilies family, int scriptNumber)
        {
            if (scriptNumber < 2000)
            {
                return new Result { Kind = Kind.NotCommon };
            }

            Bracket[] table;
            if (family == RomInfo.GameFamilies.DP) table = DpBrackets;
            else if (family == RomInfo.GameFamilies.Plat) table = PlatBrackets;
            else if (family == RomInfo.GameFamilies.HGSS) table = HgssBrackets;
            else return new Result { Kind = Kind.NotCommon };

            foreach (Bracket b in table)
            {
                if (scriptNumber < b.Lower || scriptNumber >= b.Upper) continue;
                return new Result
                {
                    Kind = Kind.Resolved,
                    ScriptArchiveId = b.ScriptArchive,
                    TextArchiveId = b.TextArchive,
                    Name = b.Name,
                    LocalScriptId = scriptNumber - b.Lower,
                    RangeLower = b.Lower,
                    RangeUpper = b.Upper,
                };
            }

            return new Result { Kind = Kind.NotCommon };
        }
    }
}
