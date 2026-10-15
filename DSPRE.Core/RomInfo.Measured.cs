using System.Collections.Generic;

namespace DSPRE
{
    public partial class RomInfo
    {
        /// <summary>
        /// Revisions whose arm9 and overlays are byte-identical to another's, so every offset keyed by game code and
        /// revision holds for both: European English Diamond and Pearl (13), Platinum, HeartGold and SoulSilver (10).
        /// </summary>
        private static readonly Dictionary<(string id, int rev), (string id, int rev)> CodeTwins = new()
        {
            [("ADAE", 13)] = ("ADAE", 5), [("APAE", 13)] = ("APAE", 5), [("CPUE", 10)] = ("CPUE", 1),
            [("IPKE", 10)] = ("IPKE", 0), [("IPGE", 10)] = ("IPGE", 0),
        };

        /// <summary>This ROM's game code and revision, a code twin read as the revision it copies.</summary>
        public static (string id, int rev) CodeKey =>
            romID != null && CodeTwins.TryGetValue((romID, romRevision), out (string id, int rev) twin) ? twin : (romID, romRevision);

        /// <summary>
        /// The key for code sites (move lists, type chart, swarms, encounter slots): SoulSilver US keeps HeartGold US's
        /// sites byte for byte, so it reads HeartGold's entries.
        /// </summary>
        private static (string id, int rev) SiteKey => CodeKey == ("IPKE", 0) || CodeKey == ("IPGE", 0) ? ("IPKE", 0) : CodeKey;

        /// <summary>Offsets measured on a dump where the per-language tables above lack them or have them wrong.</summary>
        public sealed class Measure
        {
            public uint? HeaderTable, Spawn, FlyTable, InitialMoney, ItemTable, PickupDivisor, RematchTable, EncounterMusic,
                ItemLimitSite, MartCommonCount, MartCommonPointer, MartSpecialtyPointer, DungeonCutin, HiddenItemTable,
                DistortionWorldItemScript, MonIconPaletteLiteral, EffectCombo, EffectComboSecond, PickupCommon, PickupRare, PickupWeight,
                ConditionalMusic, VsPokemonEntry, VsTrainerEntry, OwTable, TrainerClassGender, PrizeTable, PrizePointer, ClassGenderPointer;
            public int? MachineMoves, EggMoves, RematchRowCompare, DexForeignSpecies, DexFieldZones, DexDungeonZones, DexAreaSort,
                FollowerModels, FollowerFemale, FollowerFormCounts, PokeathlonMembers, TrainerScript, PlayerNamesText,
                PokegearPhoneText, TouchMenuText, BugContestText, MenuEntriesText, SystemFontPalette, TrainerNameLength, OwTablePointer,
                ClassJumpTable, ClassJumpCount;
            public uint[] CameraLiterals, HiddenItemCountSites, HiddenItemCountLiterals, Arm9Expansion, TrainerShiny, BuildingRotation;
            public int[] RematchPointers, DexForeignBanks, SharedMessagesText;
        }

        // Generated from byte comparisons of each dump against the US dump of the same game; each value was
        // found at exactly one place, or (Japanese DP header tables) agrees on 96% of its bytes and moves with the rest.
        // Korean Platinum, French HeartGold and SoulSilver, and Spanish HeartGold were measured on dumps that are not
        // known good dumps; their entries are marked and need verifying.
        private static readonly Dictionary<(string id, int rev), Measure> Measured = new()
        {
            // Diamond German, revision 5
            [("ADAD", 5)] = new() { Arm9Expansion = new uint[] { 0xC80, 0x106C7C, 0x20064F0 }, BuildingRotation = new uint[] { 0x1097E, 0x201CAF4 }, DexForeignBanks = new[] { 617, 622, 623 }, DexForeignSpecies = 0xFD134, EffectCombo = 0x475F0, EffectComboSecond = 0x4760C, HiddenItemCountSites = new uint[] { 0x39712, 0x39716, 0x3976A, 0x3976E }, HiddenItemTable = 0xF2DC4, ItemLimitSite = 0x6E76C, MartCommonCount = 0x3FDDC, MartCommonPointer = 0x3FDFC, MartSpecialtyPointer = 0x3FE4C, PrizeTable = 0x329E0, RematchTable = 0x1F3F4 },
            // Diamond English, revision 5
            [("ADAE", 5)] = new() { BuildingRotation = new uint[] { 0x1097E, 0x201CAA8 }, HiddenItemCountSites = new uint[] { 0x396CA, 0x396CE, 0x39722, 0x39726 }, HiddenItemTable = 0xF2DB4, PrizeTable = 0x32960 },
            // Diamond French, revision 5
            [("ADAF", 5)] = new() { BuildingRotation = new uint[] { 0x1097E, 0x201CAF4 }, DexForeignBanks = new[] { 617, 622, 623 }, DexForeignSpecies = 0xFD164, EffectCombo = 0x475F0, EffectComboSecond = 0x4760C, HiddenItemCountSites = new uint[] { 0x39712, 0x39716, 0x3976A, 0x3976E }, HiddenItemTable = 0xF2DF4, ItemLimitSite = 0x6E76C, MartCommonCount = 0x3FDDC, MartCommonPointer = 0x3FDFC, MartSpecialtyPointer = 0x3FE4C, PrizeTable = 0x32AE0, RematchTable = 0x1F3F4 },
            // Diamond Italian, revision 5
            [("ADAI", 5)] = new() { BuildingRotation = new uint[] { 0x1097E, 0x201CAF4 }, DexForeignBanks = new[] { 617, 622, 623 }, DexForeignSpecies = 0xFD0D8, EffectCombo = 0x475F0, EffectComboSecond = 0x4760C, HiddenItemCountSites = new uint[] { 0x39712, 0x39716, 0x3976A, 0x3976E }, HiddenItemTable = 0xF2D68, ItemLimitSite = 0x6E74C, MartCommonCount = 0x3FDDC, MartCommonPointer = 0x3FDFC, MartSpecialtyPointer = 0x3FE4C, PrizeTable = 0x329E0, RematchTable = 0x1F3F4 },
            // Diamond Japanese, revision 0
            [("ADAJ", 0)] = new() { Arm9Expansion = new uint[] { 0xC6E, 0x108418, 0x2006978 }, BuildingRotation = new uint[] { 0x1128C, 0x201D640 }, CameraLiterals = new uint[] { 0x4C48 }, ClassJumpCount = 36, ClassJumpTable = 0x4A010, DexForeignBanks = new[] { 603, 608, 609 }, DexForeignSpecies = 0xFEF00, EffectCombo = 0x4A104, EffectComboSecond = 0x4A0D8, EggMoves = 0x21648, EncounterMusic = 0x4D8C0, FlyTable = 0xF4090, HeaderTable = 0xF0C28, HiddenItemCountSites = new uint[] { 0x3B1C0, 0x3B1C4, 0x3B20E, 0x3B212 }, HiddenItemTable = 0xF4C10, InitialMoney = 0x1F4, ItemLimitSite = 0x731DE, ItemTable = 0xFA3E0, MachineMoves = 0xFA318, MartCommonCount = 0x426D0, MartCommonPointer = 0x426F0, MartSpecialtyPointer = 0x4262C, MonIconPaletteLiteral = 0x6FCEC, PickupDivisor = 0x92FA, PrizeTable = 0x35060, RematchPointers = new[] { 0xA5E4, 0xA618, 0xA698, 0xA6BC }, RematchRowCompare = -1, RematchTable = 0x20448, Spawn = 0xF4A08, TrainerClassGender = 0xF9E3C, TrainerNameLength = 0x6F5F8 },
            // Diamond Korean, revision 0
            [("ADAK", 0)] = new() { Arm9Expansion = new uint[] { 0xCAC, 0x104144, 0x2006518 }, BuildingRotation = new uint[] { 0x1097E, 0x201CC00 }, DexForeignBanks = new[] { 605, 610, 611 }, DexForeignSpecies = 0xF9B44, EffectCombo = 0x47A4C, EffectComboSecond = 0x47A68, EggMoves = 0x2061C, EncounterMusic = 0x4B208, FlyTable = 0xED870, HeaderTable = 0xEA408, HiddenItemCountSites = new uint[] { 0x39B3A, 0x39B3E, 0x39B92, 0x39B96 }, HiddenItemTable = 0xEE400, InitialMoney = 0x1E4, ItemLimitSite = 0x6EBD4, ItemTable = 0xF3C04, MachineMoves = 0xF3B3C, MartCommonCount = 0x40200, MartCommonPointer = 0x40220, MartSpecialtyPointer = 0x40270, MonIconPaletteLiteral = 0x6BCFC, OwTable = 0x22B80, PickupCommon = 0x30758, PickupDivisor = 0xBCCE, PickupRare = 0x3067C, PickupWeight = 0x30B84, PrizeTable = 0x3A0A0, RematchTable = 0x1F3F0, Spawn = 0xEE1E8, TrainerNameLength = 0x6B0FC },
            // Diamond Spanish, revision 5
            [("ADAS", 5)] = new() { BuildingRotation = new uint[] { 0x1097E, 0x201CAF4 }, DexForeignBanks = new[] { 617, 622, 623 }, DexForeignSpecies = 0xFD170, EffectCombo = 0x475F0, EffectComboSecond = 0x4760C, HiddenItemCountSites = new uint[] { 0x39712, 0x39716, 0x3976A, 0x3976E }, HiddenItemTable = 0xF2E00, ItemLimitSite = 0x6E76C, MartCommonCount = 0x3FDDC, MartCommonPointer = 0x3FDFC, MartSpecialtyPointer = 0x3FE4C, PrizeTable = 0x32AE0, RematchTable = 0x1F3F4 },
            // Pearl German, revision 5
            [("APAD", 5)] = new() { Arm9Expansion = new uint[] { 0xC80, 0x106C84, 0x20064F0 }, BuildingRotation = new uint[] { 0x1097E, 0x201CAF4 }, DexForeignBanks = new[] { 617, 622, 623 }, DexForeignSpecies = 0xFD134, EffectCombo = 0x475F0, EffectComboSecond = 0x4760C, HiddenItemCountSites = new uint[] { 0x39712, 0x39716, 0x3976A, 0x3976E }, HiddenItemTable = 0xF2DC4, ItemLimitSite = 0x6E76C, MartCommonCount = 0x3FDDC, MartCommonPointer = 0x3FDFC, MartSpecialtyPointer = 0x3FE4C, PrizeTable = 0x329E0, RematchTable = 0x1F3F4 },
            // Pearl English, revision 5
            [("APAE", 5)] = new() { Arm9Expansion = new uint[] { 0xC80, 0x106B44, 0x20064F0 }, BuildingRotation = new uint[] { 0x1097E, 0x201CAA8 }, DexForeignBanks = new[] { 617, 622, 623 }, DexForeignSpecies = 0xFD120, HiddenItemCountSites = new uint[] { 0x396CA, 0x396CE, 0x39722, 0x39726 }, HiddenItemTable = 0xF2DB4, PrizeTable = 0x32960 },
            // Pearl French, revision 5
            [("APAF", 5)] = new() { Arm9Expansion = new uint[] { 0xC80, 0x106CC4, 0x20064F0 }, BuildingRotation = new uint[] { 0x1097E, 0x201CAF4 }, DexForeignBanks = new[] { 617, 622, 623 }, DexForeignSpecies = 0xFD164, EffectCombo = 0x475F0, EffectComboSecond = 0x4760C, HiddenItemCountSites = new uint[] { 0x39712, 0x39716, 0x3976A, 0x3976E }, HiddenItemTable = 0xF2DF4, ItemLimitSite = 0x6E76C, MartCommonCount = 0x3FDDC, MartCommonPointer = 0x3FDFC, MartSpecialtyPointer = 0x3FE4C, PrizeTable = 0x32AE0, RematchTable = 0x1F3F4 },
            // Pearl Italian, revision 5
            [("APAI", 5)] = new() { Arm9Expansion = new uint[] { 0xC80, 0x106C24, 0x20064F0 }, BuildingRotation = new uint[] { 0x1097E, 0x201CAF4 }, DexForeignBanks = new[] { 617, 622, 623 }, DexForeignSpecies = 0xFD0D8, EffectCombo = 0x475F0, EffectComboSecond = 0x4760C, HiddenItemCountSites = new uint[] { 0x39712, 0x39716, 0x3976A, 0x3976E }, HiddenItemTable = 0xF2D68, ItemLimitSite = 0x6E74C, MartCommonCount = 0x3FDDC, MartCommonPointer = 0x3FDFC, MartSpecialtyPointer = 0x3FE4C, PrizeTable = 0x329E0, RematchTable = 0x1F3F4 },
            // Pearl Japanese, revision 0
            [("APAJ", 0)] = new() { Arm9Expansion = new uint[] { 0xC6E, 0x108420, 0x2006978 }, BuildingRotation = new uint[] { 0x1128C, 0x201D640 }, CameraLiterals = new uint[] { 0x4C48 }, ClassJumpCount = 36, ClassJumpTable = 0x4A010, DexForeignBanks = new[] { 603, 608, 609 }, DexForeignSpecies = 0xFEF04, EffectCombo = 0x4A104, EffectComboSecond = 0x4A0D8, EggMoves = 0x21648, EncounterMusic = 0x4D8C0, FlyTable = 0xF4094, HeaderTable = 0xF0C2C, HiddenItemCountSites = new uint[] { 0x3B1C0, 0x3B1C4, 0x3B20E, 0x3B212 }, HiddenItemTable = 0xF4C14, InitialMoney = 0x1F4, ItemLimitSite = 0x731DE, ItemTable = 0xFA3E4, MachineMoves = 0xFA31C, MartCommonCount = 0x426D0, MartCommonPointer = 0x426F0, MartSpecialtyPointer = 0x4262C, MonIconPaletteLiteral = 0x6FCEC, PickupDivisor = 0x92FA, PrizeTable = 0x35060, RematchPointers = new[] { 0xA5E4, 0xA618, 0xA698, 0xA6BC }, RematchRowCompare = -1, RematchTable = 0x20448, Spawn = 0xF4A0C, TrainerClassGender = 0xF9E40, TrainerNameLength = 0x6F5F8 },
            // Pearl Japanese, revision 5
            [("APAJ", 5)] = new() { Arm9Expansion = new uint[] { 0xC6E, 0x108420, 0x2006978 }, BuildingRotation = new uint[] { 0x1128C, 0x201D640 }, CameraLiterals = new uint[] { 0x4C48 }, ClassJumpCount = 36, ClassJumpTable = 0x4A010, DexForeignBanks = new[] { 603, 608, 609 }, DexForeignSpecies = 0xFEF04, EffectCombo = 0x4A104, EffectComboSecond = 0x4A0D8, EggMoves = 0x21648, EncounterMusic = 0x4D8C0, FlyTable = 0xF4094, HeaderTable = 0xF0C2C, HiddenItemCountSites = new uint[] { 0x3B1C0, 0x3B1C4, 0x3B20E, 0x3B212 }, HiddenItemTable = 0xF4C14, InitialMoney = 0x1F4, ItemLimitSite = 0x731DE, ItemTable = 0xFA3E4, MachineMoves = 0xFA31C, MartCommonCount = 0x426D0, MartCommonPointer = 0x426F0, MartSpecialtyPointer = 0x4262C, MonIconPaletteLiteral = 0x6FCEC, PickupDivisor = 0x92FA, PrizeTable = 0x35060, RematchPointers = new[] { 0xA5E4, 0xA618, 0xA698, 0xA6BC }, RematchRowCompare = -1, RematchTable = 0x20448, Spawn = 0xF4A0C, TrainerClassGender = 0xF9E40, TrainerNameLength = 0x6F5F8 },
            // Pearl Japanese, revision 6
            [("APAJ", 6)] = new() { Arm9Expansion = new uint[] { 0xC6E, 0x108560, 0x2006978 }, BuildingRotation = new uint[] { 0x11298, 0x201D664 }, CameraLiterals = new uint[] { 0x4C54 }, ClassJumpCount = 36, ClassJumpTable = 0x4A0C8, DexForeignBanks = new[] { 603, 608, 609 }, DexForeignSpecies = 0xFF044, EffectCombo = 0x4A1BC, EffectComboSecond = 0x4A190, FlyTable = 0xF41D4, HiddenItemCountSites = new uint[] { 0x3B254, 0x3B258, 0x3B2A2, 0x3B2A6 }, HiddenItemTable = 0xF4D54, InitialMoney = 0x1F4, ItemLimitSite = 0x732DE, ItemTable = 0xFA524, MachineMoves = 0xFA45C, MartCommonCount = 0x42774, MartCommonPointer = 0x42794, MartSpecialtyPointer = 0x426D0, PickupDivisor = 0x92FA, PrizeTable = 0x35060, RematchPointers = new[] { 0xA5F0, 0xA624, 0xA6A4, 0xA6C8 }, RematchRowCompare = -1, RematchTable = 0x20454, Spawn = 0xF4B4C, TrainerClassGender = 0xF9F80, TrainerNameLength = 0x6F6F8 },
            // Pearl Korean, revision 0
            [("APAK", 0)] = new() { Arm9Expansion = new uint[] { 0xCAC, 0x10414C, 0x2006518 }, BuildingRotation = new uint[] { 0x1097E, 0x201CC00 }, DexForeignBanks = new[] { 605, 610, 611 }, DexForeignSpecies = 0xF9B44, EffectCombo = 0x47A4C, EffectComboSecond = 0x47A68, EggMoves = 0x2061C, EncounterMusic = 0x4B208, FlyTable = 0xED870, HeaderTable = 0xEA408, HiddenItemCountSites = new uint[] { 0x39B3A, 0x39B3E, 0x39B92, 0x39B96 }, HiddenItemTable = 0xEE400, InitialMoney = 0x1E4, ItemLimitSite = 0x6EBD4, ItemTable = 0xF3C04, MachineMoves = 0xF3B3C, MartCommonCount = 0x40200, MartCommonPointer = 0x40220, MartSpecialtyPointer = 0x40270, MonIconPaletteLiteral = 0x6BCFC, OwTable = 0x22B80, PickupCommon = 0x30758, PickupDivisor = 0xBCCE, PickupRare = 0x3067C, PickupWeight = 0x30B84, PrizeTable = 0x3A0A0, RematchTable = 0x1F3F0, Spawn = 0xEE1E8, TrainerNameLength = 0x6B0FC },
            // Pearl Spanish, revision 5
            [("APAS", 5)] = new() { Arm9Expansion = new uint[] { 0xC80, 0x106CE4, 0x20064F0 }, BuildingRotation = new uint[] { 0x1097E, 0x201CAF4 }, DexForeignBanks = new[] { 617, 622, 623 }, DexForeignSpecies = 0xFD170, EffectCombo = 0x475F0, EffectComboSecond = 0x4760C, HiddenItemCountSites = new uint[] { 0x39712, 0x39716, 0x3976A, 0x3976E }, HiddenItemTable = 0xF2E00, ItemLimitSite = 0x6E76C, MartCommonCount = 0x3FDDC, MartCommonPointer = 0x3FDFC, MartSpecialtyPointer = 0x3FE4C, PrizeTable = 0x32AE0, RematchTable = 0x1F3F4 },
            // Platinum German, revision 0
            [("CPUD", 0)] = new() { BuildingRotation = new uint[] { 0x10B36, 0x201E2D8 }, ClassGenderPointer = 0x79454, HiddenItemCountLiterals = new uint[] { 0x3F4A4, 0x3F518 }, HiddenItemTable = 0xEA3D0, ItemLimitSite = 0x7CFE8, MartCommonCount = 0x46C18, MartCommonPointer = 0x46C38, MartSpecialtyPointer = 0x46C94, PrizePointer = 0x8188, PrizeTable = 0x35A00, RematchTable = 0x280E4 },
            // Platinum English, revision 1
            [("CPUE", 1)] = new() { BuildingRotation = new uint[] { 0x10AD2, 0x201E268 }, ClassGenderPointer = 0x793B4, HiddenItemCountLiterals = new uint[] { 0x3F400, 0x3F474 }, HiddenItemTable = 0xEA378, PrizePointer = 0x816C, PrizeTable = 0x359E0 },
            // Platinum French, revision 0
            [("CPUF", 0)] = new() { BuildingRotation = new uint[] { 0x10B22, 0x201E2D8 }, ClassGenderPointer = 0x79454, HiddenItemCountLiterals = new uint[] { 0x3F4A4, 0x3F518 }, HiddenItemTable = 0xEA400, MartCommonCount = 0x46C18, MartCommonPointer = 0x46C38, MartSpecialtyPointer = 0x46C94, PrizePointer = 0x8274, PrizeTable = 0x35AE0, RematchTable = 0x280D0 },
            // Platinum Italian, revision 0
            [("CPUI", 0)] = new() { BuildingRotation = new uint[] { 0x10B22, 0x201E2D8 }, ClassGenderPointer = 0x79454, HiddenItemCountLiterals = new uint[] { 0x3F4A4, 0x3F518 }, HiddenItemTable = 0xEA394, ItemLimitSite = 0x7CFE8, MartCommonCount = 0x46C18, MartCommonPointer = 0x46C38, MartSpecialtyPointer = 0x46C94, PrizePointer = 0x8188, PrizeTable = 0x35A00, RematchTable = 0x280D8 },
            // Platinum Japanese, revision 0
            [("CPUJ", 0)] = new() { BuildingRotation = new uint[] { 0x10A16, 0x201DEFC }, ClassGenderPointer = 0x78C8C, DistortionWorldItemScript = 0x8E2A, HiddenItemCountLiterals = new uint[] { 0x3EFBC, 0x3F030 }, HiddenItemTable = 0xE9A4C, ItemLimitSite = 0x7C818, MartCommonCount = 0x466E0, MartCommonPointer = 0x46700, MartSpecialtyPointer = 0x4675C, MenuEntriesText = 360, PrizePointer = 0x8164, PrizeTable = 0x359E0, RematchTable = 0x27EC8, SystemFontPalette = 6 },
            // Platinum Korean, revision 0
            [("CPUK", 0)] = new() { Arm9Expansion = new uint[] { 0xCB4, 0x102464, 0x2006AB8 }, BuildingRotation = new uint[] { 0x10B16, 0x201E3FC }, ClassGenderPointer = 0x798A0, DistortionWorldItemScript = 0x8E2A, EffectCombo = 0x520D4, EffectComboSecond = 0x520F0, EggMoves = 0x2921A, EncounterMusic = 0x55B30, FlyTable = 0xEA23C, HeaderTable = 0xE6AA4, HiddenItemCountLiterals = new uint[] { 0x3F8F0, 0x3F964 }, HiddenItemTable = 0xEAE00, InitialMoney = 0x1EC, ItemLimitSite = 0x7D434, ItemTable = 0xF174C, MachineMoves = 0xF1684, MartCommonCount = 0x47068, MartCommonPointer = 0x47088, MartSpecialtyPointer = 0x470E4, MenuEntriesText = 360, MonIconPaletteLiteral = 0x7A46C, OwTable = 0x2BC2C, PickupCommon = 0x33544, PickupDivisor = 0xC642, PickupRare = 0x33468, PickupWeight = 0x33980, PrizePointer = 0x8184, PrizeTable = 0x35A00, RematchTable = 0x280C0, Spawn = 0xEABB4, TrainerNameLength = 0x796D0 },   // needs verifying: measured on a dump that is not a known good dump
            // Platinum Spanish, revision 0
            [("CPUS", 0)] = new() { BuildingRotation = new uint[] { 0x10B22, 0x201E2D8 }, ClassGenderPointer = 0x79454, HiddenItemCountLiterals = new uint[] { 0x3F4A4, 0x3F518 }, HiddenItemTable = 0xEA40C, ItemLimitSite = 0x7CFE8, MartCommonCount = 0x46C18, MartCommonPointer = 0x46C38, MartSpecialtyPointer = 0x46C94, PrizePointer = 0x8274, PrizeTable = 0x35AE0, RematchTable = 0x280D0 },
            // SoulSilver German, revision 0
            [("IPGD", 0)] = new() { Arm9Expansion = new uint[] { 0xCD0, 0x1110B0, 0x2007508 }, BuildingRotation = new uint[] { 0xE1C6, 0x2020D2C }, DexAreaSort = 0x13D60, DexDungeonZones = 0x13C58, DexFieldZones = 0x13CD4, DungeonCutin = 0x6A384, FollowerFemale = 0xFEC62, FollowerFormCounts = 0xFE888, FollowerModels = 0xFF03C, HiddenItemCountSites = new uint[] { 0x4058E, 0x40592, 0x405E4, 0x405E8 }, HiddenItemTable = 0xFA50C, ItemLimitSite = 0x77CE8, MartCommonCount = 0x48100, MartCommonPointer = 0x48124, MartSpecialtyPointer = 0x48190, PlayerNamesText = 445, PokeathlonMembers = 0xFF768, PrizeTable = 0x34C04, TrainerShiny = new uint[] { 0x73A84, 0x73AA2, 0x2074640, 0x2074644, 0x206E540, 0x206E540, 0x206EC40, 0x201AB0C } },
            // SoulSilver English, revision 0
            [("IPGE", 0)] = new() { BuildingRotation = new uint[] { 0xE1C6, 0x2020D2C }, DexAreaSort = 0x140DC, DexDungeonZones = 0x13FD4, DexFieldZones = 0x14050, FollowerFemale = 0xFECAE, FollowerFormCounts = 0xFE8D4, FollowerModels = 0xFF088, HiddenItemCountSites = new uint[] { 0x4058E, 0x40592, 0x405E4, 0x405E8 }, HiddenItemTable = 0xFA558, PokeathlonMembers = 0xFF7B4, PrizeTable = 0x34C04, TrainerShiny = new uint[] { 0x73A84, 0x73AA2, 0x2074640, 0x2074644, 0x206E540, 0x206E540, 0x206EC40, 0x201AB0C } },
            // SoulSilver French, revision 0
            [("IPGF", 0)] = new() { BuildingRotation = new uint[] { 0xE1C6, 0x2020D2C }, DexAreaSort = 0x13D60, DexDungeonZones = 0x13C58, DexFieldZones = 0x13CD4, DungeonCutin = 0x6A384, FollowerFemale = 0xFEC92, FollowerFormCounts = 0xFE8B8, FollowerModels = 0xFF06C, HiddenItemCountSites = new uint[] { 0x4058E, 0x40592, 0x405E4, 0x405E8 }, HiddenItemTable = 0xFA53C, ItemLimitSite = 0x77CE8, MartCommonCount = 0x48100, MartCommonPointer = 0x48124, MartSpecialtyPointer = 0x48190, PlayerNamesText = 445, PokeathlonMembers = 0xFF798, PrizeTable = 0x34C04, TrainerShiny = new uint[] { 0x73A84, 0x73AA2, 0x2074640, 0x2074644, 0x206E540, 0x206E540, 0x206EC40, 0x201AB0C } },   // needs verifying: measured on a dump that is not a known good dump
            // SoulSilver Italian, revision 0
            [("IPGI", 0)] = new() { BuildingRotation = new uint[] { 0xE1C6, 0x2020D2C }, DexAreaSort = 0x13D60, DexDungeonZones = 0x13C58, DexFieldZones = 0x13CD4, DungeonCutin = 0x6A384, FollowerFemale = 0xFEC26, FollowerFormCounts = 0xFE84C, FollowerModels = 0xFF000, HiddenItemCountSites = new uint[] { 0x4058E, 0x40592, 0x405E4, 0x405E8 }, HiddenItemTable = 0xFA4D0, ItemLimitSite = 0x77CE8, MartCommonCount = 0x48100, MartCommonPointer = 0x48124, MartSpecialtyPointer = 0x48190, PlayerNamesText = 445, PokeathlonMembers = 0xFF72C, PrizeTable = 0x34C04, TrainerShiny = new uint[] { 0x73A84, 0x73AA2, 0x2074640, 0x2074644, 0x206E540, 0x206E540, 0x206EC40, 0x201AB0C } },
            // SoulSilver Japanese, revision 0
            [("IPGJ", 0)] = new() { BugContestText = 239, BuildingRotation = new uint[] { 0xE07E, 0x20209CC }, DexAreaSort = 0x13D6C, DexDungeonZones = 0x13C64, DexFieldZones = 0x13CE0, DungeonCutin = 0x69DE4, FollowerFemale = 0xFE42E, FollowerFormCounts = 0xFE054, FollowerModels = 0xFE808, HiddenItemCountSites = new uint[] { 0x402DA, 0x402DE, 0x40330, 0x40334 }, HiddenItemTable = 0xF9D08, ItemLimitSite = 0x77778, MartCommonCount = 0x47DB8, MartCommonPointer = 0x47DDC, MartSpecialtyPointer = 0x47E48, MenuEntriesText = 189, PokeathlonMembers = 0xFEF34, PokegearPhoneText = 264, PrizeTable = 0x34BC0, SharedMessagesText = new[] { 740, 209, 29, 428 }, SystemFontPalette = 7, TouchMenuText = 194, TrainerScript = 738, TrainerShiny = new uint[] { 0x7351C, 0x7353A, 0x20740D8, 0x20740DC, 0x206DF80, 0x206DF80, 0x206E680, 0x201A7C0 } },
            // SoulSilver Korean, revision 0
            [("IPGK", 0)] = new() { Arm9Expansion = new uint[] { 0xCD0, 0x111AC4, 0x20074D8 }, BugContestText = 241, BuildingRotation = new uint[] { 0xE1C6, 0x2020CF8 }, CameraLiterals = new uint[] { 0x532C, 0x547C }, ConditionalMusic = 0x668CC, DexAreaSort = 0x13E20, DexDungeonZones = 0x13D18, DexFieldZones = 0x13D94, DungeonCutin = 0x6A478, EffectCombo = 0x518BC, EncounterMusic = 0x551D4, FlyTable = 0xFA524, FollowerFemale = 0xFF352, FollowerFormCounts = 0xFEF78, FollowerModels = 0xFF72C, HeaderTable = 0xF7284, HiddenItemCountSites = new uint[] { 0x4081E, 0x40822, 0x40874, 0x40878 }, HiddenItemTable = 0xFABFC, InitialMoney = 0x2F8, ItemLimitSite = 0x77DE0, ItemTable = 0x100838, MachineMoves = 0x100770, MartCommonCount = 0x48390, MartCommonPointer = 0x483B4, MartSpecialtyPointer = 0x48420, MenuEntriesText = 190, MonIconPaletteLiteral = 0x74500, OwTablePointer = 0x21F9CFC, PickupCommon = 0x34B48, PickupDivisor = 0xC856, PickupRare = 0x34A50, PickupWeight = 0x35190, PlayerNamesText = 440, PokeathlonMembers = 0xFFE58, PokegearPhoneText = 266, PrizeTable = 0x34C08, Spawn = 0xFA820, TouchMenuText = 195, TrainerClassGender = 0x100234, TrainerNameLength = 0x73522, TrainerShiny = new uint[] { 0x73B7C, 0x73B9A, 0x2074738, 0x207473C, 0x206E634, 0x206E634, 0x206ED34, 0x201AAA4 }, VsPokemonEntry = 0x519C8, VsTrainerEntry = 0x51984 },
            // SoulSilver Spanish, revision 0
            [("IPGS", 0)] = new() { Arm9Expansion = new uint[] { 0xCD0, 0x111110, 0x2007508 }, BuildingRotation = new uint[] { 0xE1C6, 0x2020D2C }, DexAreaSort = 0x13D60, DexDungeonZones = 0x13C58, DexFieldZones = 0x13CD4, FollowerFemale = 0xFEC9E, FollowerFormCounts = 0xFE8C4, FollowerModels = 0xFF078, HiddenItemCountSites = new uint[] { 0x4058E, 0x40592, 0x405E4, 0x405E8 }, HiddenItemTable = 0xFA548, ItemLimitSite = 0x77CE8, MachineMoves = 0x1000BC, MartCommonCount = 0x48100, MartCommonPointer = 0x48124, MartSpecialtyPointer = 0x48190, PlayerNamesText = 445, PokeathlonMembers = 0xFF7A4, PrizeTable = 0x34C04, TrainerClassGender = 0xFFB80, TrainerShiny = new uint[] { 0x73A84, 0x73AA2, 0x2074640, 0x2074644, 0x206E540, 0x206E540, 0x206EC40, 0x201AB0C } },
            // HeartGold German, revision 0
            [("IPKD", 0)] = new() { Arm9Expansion = new uint[] { 0xCD0, 0x1110B0, 0x2007508 }, BuildingRotation = new uint[] { 0xE1C6, 0x2020D2C }, DexAreaSort = 0x13D64, DexDungeonZones = 0x13C5C, DexFieldZones = 0x13CD8, DungeonCutin = 0x6A384, FollowerFemale = 0xFEC62, FollowerFormCounts = 0xFE888, FollowerModels = 0xFF03C, HiddenItemCountSites = new uint[] { 0x4058E, 0x40592, 0x405E4, 0x405E8 }, HiddenItemTable = 0xFA50C, ItemLimitSite = 0x77CE8, MartCommonCount = 0x48100, MartCommonPointer = 0x48124, MartSpecialtyPointer = 0x48190, PlayerNamesText = 445, PokeathlonMembers = 0xFF768, PrizeTable = 0x34C04, TrainerShiny = new uint[] { 0x73A84, 0x73AA2, 0x2074640, 0x2074644, 0x206E540, 0x206E540, 0x206EC40, 0x201AB0C } },
            // HeartGold English, revision 0
            [("IPKE", 0)] = new() { BuildingRotation = new uint[] { 0xE1C6, 0x2020D2C }, PrizeTable = 0x34C04, TrainerShiny = new uint[] { 0x73A84, 0x73AA2, 0x2074640, 0x2074644, 0x206E540, 0x206E540, 0x206EC40, 0x201AB0C } },
            // HeartGold French, revision 0
            [("IPKF", 0)] = new() { BuildingRotation = new uint[] { 0xE1C6, 0x2020D2C }, DexAreaSort = 0x13D64, DexDungeonZones = 0x13C5C, DexFieldZones = 0x13CD8, DungeonCutin = 0x6A384, FollowerFemale = 0xFEC92, FollowerFormCounts = 0xFE8B8, FollowerModels = 0xFF06C, HiddenItemCountSites = new uint[] { 0x4058E, 0x40592, 0x405E4, 0x405E8 }, HiddenItemTable = 0xFA53C, ItemLimitSite = 0x77CE8, MartCommonCount = 0x48100, MartCommonPointer = 0x48124, MartSpecialtyPointer = 0x48190, PlayerNamesText = 445, PokeathlonMembers = 0xFF798, PrizeTable = 0x34C04, TrainerShiny = new uint[] { 0x73A84, 0x73AA2, 0x2074640, 0x2074644, 0x206E540, 0x206E540, 0x206EC40, 0x201AB0C } },   // needs verifying: measured on a dump that is not a known good dump
            // HeartGold Italian, revision 0
            [("IPKI", 0)] = new() { BuildingRotation = new uint[] { 0xE1C6, 0x2020D2C }, DexAreaSort = 0x13D64, DexDungeonZones = 0x13C5C, DexFieldZones = 0x13CD8, DungeonCutin = 0x6A384, FollowerFemale = 0xFEC26, FollowerFormCounts = 0xFE84C, FollowerModels = 0xFF000, HiddenItemCountSites = new uint[] { 0x4058E, 0x40592, 0x405E4, 0x405E8 }, HiddenItemTable = 0xFA4D0, ItemLimitSite = 0x77CE8, MartCommonCount = 0x48100, MartCommonPointer = 0x48124, MartSpecialtyPointer = 0x48190, PlayerNamesText = 445, PokeathlonMembers = 0xFF72C, PrizeTable = 0x34C04, TrainerShiny = new uint[] { 0x73A84, 0x73AA2, 0x2074640, 0x2074644, 0x206E540, 0x206E540, 0x206EC40, 0x201AB0C } },
            // HeartGold Japanese, revision 0
            [("IPKJ", 0)] = new() { BugContestText = 239, BuildingRotation = new uint[] { 0xE07E, 0x20209CC }, DexAreaSort = 0x13D6C, DexDungeonZones = 0x13C64, DexFieldZones = 0x13CE0, DungeonCutin = 0x69DE4, FollowerFemale = 0xFE42E, FollowerFormCounts = 0xFE054, FollowerModels = 0xFE808, HiddenItemCountSites = new uint[] { 0x402DA, 0x402DE, 0x40330, 0x40334 }, HiddenItemTable = 0xF9D08, ItemLimitSite = 0x77778, MartCommonCount = 0x47DB8, MartCommonPointer = 0x47DDC, MartSpecialtyPointer = 0x47E48, MenuEntriesText = 189, PokeathlonMembers = 0xFEF34, PokegearPhoneText = 264, PrizeTable = 0x34BC0, SharedMessagesText = new[] { 740, 209, 29, 428 }, SystemFontPalette = 7, TouchMenuText = 194, TrainerScript = 738, TrainerShiny = new uint[] { 0x7351C, 0x7353A, 0x20740D8, 0x20740DC, 0x206DF80, 0x206DF80, 0x206E680, 0x201A7C0 } },
            // HeartGold Korean, revision 0
            [("IPKK", 0)] = new() { Arm9Expansion = new uint[] { 0xCD0, 0x111AC4, 0x20074D8 }, BugContestText = 241, BuildingRotation = new uint[] { 0xE1C6, 0x2020CF8 }, CameraLiterals = new uint[] { 0x532C, 0x547C }, ConditionalMusic = 0x668D4, DexAreaSort = 0x13E1C, DexDungeonZones = 0x13D14, DexFieldZones = 0x13D90, DungeonCutin = 0x6A480, EffectCombo = 0x518C4, EncounterMusic = 0x551DC, FlyTable = 0xFA52C, FollowerFemale = 0xFF35A, FollowerFormCounts = 0xFEF80, FollowerModels = 0xFF734, HeaderTable = 0xF728C, HiddenItemCountSites = new uint[] { 0x40826, 0x4082A, 0x4087C, 0x40880 }, HiddenItemTable = 0xFAC04, InitialMoney = 0x2F8, ItemLimitSite = 0x77DE8, ItemTable = 0x100840, MachineMoves = 0x100778, MartCommonCount = 0x48398, MartCommonPointer = 0x483BC, MartSpecialtyPointer = 0x48428, MenuEntriesText = 190, MonIconPaletteLiteral = 0x74508, OwTablePointer = 0x21F9CFC, PickupCommon = 0x34B48, PickupDivisor = 0xC856, PickupRare = 0x34A50, PickupWeight = 0x35190, PlayerNamesText = 440, PokeathlonMembers = 0xFFE60, PokegearPhoneText = 266, PrizeTable = 0x34C08, Spawn = 0xFA828, TouchMenuText = 195, TrainerClassGender = 0x10023C, TrainerNameLength = 0x7352A, TrainerShiny = new uint[] { 0x73B84, 0x73BA2, 0x2074740, 0x2074744, 0x206E63C, 0x206E63C, 0x206ED3C, 0x201AAA4 }, VsPokemonEntry = 0x519D0, VsTrainerEntry = 0x5198C },
            // HeartGold Spanish, revision 0
            [("IPKS", 0)] = new() { BuildingRotation = new uint[] { 0xE1C6, 0x2020D2C }, DexAreaSort = 0x13D64, DexDungeonZones = 0x13C5C, DexFieldZones = 0x13CD8, FollowerFemale = 0xFEC96, FollowerFormCounts = 0xFE8BC, FollowerModels = 0xFF070, HiddenItemCountSites = new uint[] { 0x40586, 0x4058A, 0x405DC, 0x405E0 }, HiddenItemTable = 0xFA540, ItemLimitSite = 0x77CE0, MartCommonCount = 0x480F8, MartCommonPointer = 0x4811C, MartSpecialtyPointer = 0x48188, PlayerNamesText = 445, PokeathlonMembers = 0xFF79C, PrizeTable = 0x34C04, TrainerShiny = new uint[] { 0x73A7C, 0x73A9A, 0x2074638, 0x207463C, 0x206E538, 0x206E538, 0x206EC38, 0x201AB0C } },   // needs verifying: measured on a dump that is not a known good dump
        };

        private static Measure MeasuredHere => romID != null && Measured.TryGetValue(CodeKey, out Measure m) ? m : null;

        // The US-only tables above, found byte for byte in other dumps.
        private static readonly Dictionary<(string id, int rev, GameTable table), TableSpot> MeasuredTableSpots = new()
        {
            [("ADAD", 5, GameTable.WildHeldItemOdds)] = new(-1, 0xF7EE0),
            [("ADAF", 5, GameTable.WildHeldItemOdds)] = new(-1, 0xF7F10),
            [("ADAI", 5, GameTable.WildHeldItemOdds)] = new(-1, 0xF7E84),
            [("ADAJ", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xF9CF0),
            [("ADAK", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xF351C),
            [("ADAS", 5, GameTable.WildHeldItemOdds)] = new(-1, 0xF7F1C),
            [("APAD", 5, GameTable.WildHeldItemOdds)] = new(-1, 0xF7EE0),
            [("APAE", 5, GameTable.WildHeldItemOdds)] = new(-1, 0xF7ECC),
            [("APAF", 5, GameTable.WildHeldItemOdds)] = new(-1, 0xF7F10),
            [("APAI", 5, GameTable.WildHeldItemOdds)] = new(-1, 0xF7E84),
            [("APAJ", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xF9CF4),
            [("APAJ", 5, GameTable.WildHeldItemOdds)] = new(-1, 0xF9CF4),
            [("APAJ", 6, GameTable.WildHeldItemOdds)] = new(-1, 0xF9E34),
            [("APAK", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xF351C),
            [("APAS", 5, GameTable.WildHeldItemOdds)] = new(-1, 0xF7F1C),
            [("CPUD", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xF05CC),
            [("CPUF", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xF05FC),
            [("CPUI", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xF0590),
            [("CPUJ", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xEFC04),
            [("CPUK", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xF0FFC),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xF0608),
            [("IPGD", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xFF498),
            [("IPGE", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xFF4E4),
            [("IPGF", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xFF4C8),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xFF45C),
            [("IPGJ", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xFEC64),
            [("IPGK", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xFFB88),
            [("IPGS", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xFF4D4),
            [("IPKD", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xFF498),
            [("IPKF", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xFF4C8),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xFF45C),
            [("IPKJ", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xFEC64),
            [("IPKK", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xFFB90),
            [("IPKS", 0, GameTable.WildHeldItemOdds)] = new(-1, 0xFF4CC),   // needs verifying: measured on a dump that is not a known good dump
            [("ADAD", 5, GameTable.IncenseBabies)] = new(5, 0x205EA),
            [("ADAF", 5, GameTable.IncenseBabies)] = new(5, 0x205EA),
            [("ADAI", 5, GameTable.IncenseBabies)] = new(5, 0x205EA),
            [("ADAJ", 0, GameTable.IncenseBabies)] = new(5, 0x21610),
            [("ADAK", 0, GameTable.IncenseBabies)] = new(5, 0x205E6),
            [("ADAS", 5, GameTable.IncenseBabies)] = new(5, 0x205EA),
            [("APAD", 5, GameTable.IncenseBabies)] = new(5, 0x205EA),
            [("APAE", 5, GameTable.IncenseBabies)] = new(5, 0x20632),
            [("APAF", 5, GameTable.IncenseBabies)] = new(5, 0x205EA),
            [("APAI", 5, GameTable.IncenseBabies)] = new(5, 0x205EA),
            [("APAJ", 0, GameTable.IncenseBabies)] = new(5, 0x21610),
            [("APAJ", 5, GameTable.IncenseBabies)] = new(5, 0x21610),
            [("APAJ", 6, GameTable.IncenseBabies)] = new(5, 0x2161C),
            [("APAK", 0, GameTable.IncenseBabies)] = new(5, 0x205E6),
            [("APAS", 5, GameTable.IncenseBabies)] = new(5, 0x205EA),
            [("CPUD", 0, GameTable.IncenseBabies)] = new(5, 0x29208),
            [("CPUF", 0, GameTable.IncenseBabies)] = new(5, 0x291F4),
            [("CPUI", 0, GameTable.IncenseBabies)] = new(5, 0x291FC),
            [("CPUJ", 0, GameTable.IncenseBabies)] = new(5, 0x28FDC),
            [("CPUK", 0, GameTable.IncenseBabies)] = new(5, 0x291E4),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.IncenseBabies)] = new(5, 0x291F4),
            [("IPGD", 0, GameTable.IncenseBabies)] = new(-1, 0xFF462),
            [("IPGE", 0, GameTable.IncenseBabies)] = new(-1, 0xFF4AE),
            [("IPGF", 0, GameTable.IncenseBabies)] = new(-1, 0xFF492),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0, GameTable.IncenseBabies)] = new(-1, 0xFF426),
            [("IPGJ", 0, GameTable.IncenseBabies)] = new(-1, 0xFEC2E),
            [("IPGK", 0, GameTable.IncenseBabies)] = new(-1, 0xFFB52),
            [("IPGS", 0, GameTable.IncenseBabies)] = new(-1, 0xFF49E),
            [("IPKD", 0, GameTable.IncenseBabies)] = new(-1, 0xFF462),
            [("IPKF", 0, GameTable.IncenseBabies)] = new(-1, 0xFF492),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0, GameTable.IncenseBabies)] = new(-1, 0xFF426),
            [("IPKJ", 0, GameTable.IncenseBabies)] = new(-1, 0xFEC2E),
            [("IPKK", 0, GameTable.IncenseBabies)] = new(-1, 0xFFB5A),
            [("IPKS", 0, GameTable.IncenseBabies)] = new(-1, 0xFF496),   // needs verifying: measured on a dump that is not a known good dump
            [("ADAD", 5, GameTable.TypeChart)] = new(11, 0x30E34),
            [("ADAF", 5, GameTable.TypeChart)] = new(11, 0x30F20),
            [("ADAI", 5, GameTable.TypeChart)] = new(11, 0x30E34),
            [("ADAJ", 0, GameTable.TypeChart)] = new(11, 0x33498),
            [("ADAK", 0, GameTable.TypeChart)] = new(11, 0x30DAC),
            [("ADAS", 5, GameTable.TypeChart)] = new(11, 0x30F20),
            [("APAD", 5, GameTable.TypeChart)] = new(11, 0x30E34),
            [("APAE", 5, GameTable.TypeChart)] = new(11, 0x30DB8),
            [("APAF", 5, GameTable.TypeChart)] = new(11, 0x30F20),
            [("APAI", 5, GameTable.TypeChart)] = new(11, 0x30E34),
            [("APAJ", 0, GameTable.TypeChart)] = new(11, 0x33498),
            [("APAJ", 5, GameTable.TypeChart)] = new(11, 0x33498),
            [("APAJ", 6, GameTable.TypeChart)] = new(11, 0x33498),
            [("APAK", 0, GameTable.TypeChart)] = new(11, 0x30DAC),
            [("APAS", 5, GameTable.TypeChart)] = new(11, 0x30F20),
            [("CPUD", 0, GameTable.TypeChart)] = new(16, 0x33BB0),
            [("CPUF", 0, GameTable.TypeChart)] = new(16, 0x33C9C),
            [("CPUI", 0, GameTable.TypeChart)] = new(16, 0x33BB0),
            [("CPUJ", 0, GameTable.TypeChart)] = new(16, 0x33B88),
            [("CPUK", 0, GameTable.TypeChart)] = new(16, 0x33BAC),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.TypeChart)] = new(16, 0x33C9C),
            [("IPGD", 0, GameTable.TypeChart)] = new(12, 0x353BC),
            [("IPGE", 0, GameTable.TypeChart)] = new(12, 0x353BC),
            [("IPGF", 0, GameTable.TypeChart)] = new(12, 0x353BC),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0, GameTable.TypeChart)] = new(12, 0x353BC),
            [("IPGJ", 0, GameTable.TypeChart)] = new(12, 0x35378),
            [("IPGK", 0, GameTable.TypeChart)] = new(12, 0x353C0),
            [("IPGS", 0, GameTable.TypeChart)] = new(12, 0x353BC),
            [("IPKD", 0, GameTable.TypeChart)] = new(12, 0x353BC),
            [("IPKF", 0, GameTable.TypeChart)] = new(12, 0x353BC),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0, GameTable.TypeChart)] = new(12, 0x353BC),
            [("IPKJ", 0, GameTable.TypeChart)] = new(12, 0x35378),
            [("IPKK", 0, GameTable.TypeChart)] = new(12, 0x353C0),
            [("IPKS", 0, GameTable.TypeChart)] = new(12, 0x353BC),   // needs verifying: measured on a dump that is not a known good dump
            [("ADAD", 5, GameTable.PoketchTypeChart)] = new(38, 0x8F4),
            [("ADAF", 5, GameTable.PoketchTypeChart)] = new(38, 0x8F4),
            [("ADAI", 5, GameTable.PoketchTypeChart)] = new(38, 0x8F4),
            [("ADAJ", 0, GameTable.PoketchTypeChart)] = new(38, 0x928),
            [("ADAK", 0, GameTable.PoketchTypeChart)] = new(38, 0x8F4),
            [("ADAS", 5, GameTable.PoketchTypeChart)] = new(38, 0x8F4),
            [("APAD", 5, GameTable.PoketchTypeChart)] = new(38, 0x8F4),
            [("APAE", 5, GameTable.PoketchTypeChart)] = new(38, 0x8F4),
            [("APAF", 5, GameTable.PoketchTypeChart)] = new(38, 0x8F4),
            [("APAI", 5, GameTable.PoketchTypeChart)] = new(38, 0x8F4),
            [("APAJ", 0, GameTable.PoketchTypeChart)] = new(38, 0x928),
            [("APAJ", 5, GameTable.PoketchTypeChart)] = new(38, 0x928),
            [("APAJ", 6, GameTable.PoketchTypeChart)] = new(38, 0x928),
            [("APAK", 0, GameTable.PoketchTypeChart)] = new(38, 0x8F4),
            [("APAS", 5, GameTable.PoketchTypeChart)] = new(38, 0x8F4),
            [("CPUD", 0, GameTable.PoketchTypeChart)] = new(43, 0x8F0),
            [("CPUF", 0, GameTable.PoketchTypeChart)] = new(43, 0x8F0),
            [("CPUI", 0, GameTable.PoketchTypeChart)] = new(43, 0x8F0),
            [("CPUJ", 0, GameTable.PoketchTypeChart)] = new(43, 0x8F0),
            [("CPUK", 0, GameTable.PoketchTypeChart)] = new(43, 0x8F4),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.PoketchTypeChart)] = new(43, 0x8F0),
            [("CPUD", 0, GameTable.TutorPool)] = new(5, 0x2FF80),
            [("CPUF", 0, GameTable.TutorPool)] = new(5, 0x2FF6C),
            [("CPUI", 0, GameTable.TutorPool)] = new(5, 0x2FF74),
            [("CPUJ", 0, GameTable.TutorPool)] = new(5, 0x2FD54),
            [("CPUK", 0, GameTable.TutorPool)] = new(5, 0x2FF5C),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.TutorPool)] = new(5, 0x2FF6C),
            [("IPGD", 0, GameTable.TutorPool)] = new(1, 0x23AE0),
            [("IPGE", 0, GameTable.TutorPool)] = new(1, 0x23AE0),
            [("IPGF", 0, GameTable.TutorPool)] = new(1, 0x23AE0),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0, GameTable.TutorPool)] = new(1, 0x23AE0),
            [("IPGJ", 0, GameTable.TutorPool)] = new(1, 0x23954),
            [("IPGK", 0, GameTable.TutorPool)] = new(1, 0x23AE0),
            [("IPGS", 0, GameTable.TutorPool)] = new(1, 0x23AE0),
            [("IPKD", 0, GameTable.TutorPool)] = new(1, 0x23AE0),
            [("IPKF", 0, GameTable.TutorPool)] = new(1, 0x23AE0),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0, GameTable.TutorPool)] = new(1, 0x23AE0),
            [("IPKJ", 0, GameTable.TutorPool)] = new(1, 0x23954),
            [("IPKK", 0, GameTable.TutorPool)] = new(1, 0x23AE0),
            [("IPKS", 0, GameTable.TutorPool)] = new(1, 0x23AE0),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUD", 0, GameTable.TutorCompatibility)] = new(5, 0x30148),
            [("CPUF", 0, GameTable.TutorCompatibility)] = new(5, 0x30134),
            [("CPUI", 0, GameTable.TutorCompatibility)] = new(5, 0x3013C),
            [("CPUJ", 0, GameTable.TutorCompatibility)] = new(5, 0x2FF1C),
            [("CPUK", 0, GameTable.TutorCompatibility)] = new(5, 0x30124),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.TutorCompatibility)] = new(5, 0x30134),
            [("ADAD", 5, GameTable.MiningTreasures)] = new(18, 0x17510),
            [("ADAF", 5, GameTable.MiningTreasures)] = new(18, 0x17510),
            [("ADAI", 5, GameTable.MiningTreasures)] = new(18, 0x17510),
            [("ADAJ", 0, GameTable.MiningTreasures)] = new(18, 0x18030),
            [("ADAK", 0, GameTable.MiningTreasures)] = new(18, 0x17510),
            [("ADAS", 5, GameTable.MiningTreasures)] = new(18, 0x17510),
            [("APAD", 5, GameTable.MiningTreasures)] = new(18, 0x17510),
            [("APAE", 5, GameTable.MiningTreasures)] = new(18, 0x17490),
            [("APAF", 5, GameTable.MiningTreasures)] = new(18, 0x17510),
            [("APAI", 5, GameTable.MiningTreasures)] = new(18, 0x17510),
            [("APAJ", 0, GameTable.MiningTreasures)] = new(18, 0x18030),
            [("APAJ", 5, GameTable.MiningTreasures)] = new(18, 0x18030),
            [("APAJ", 6, GameTable.MiningTreasures)] = new(18, 0x18030),
            [("APAK", 0, GameTable.MiningTreasures)] = new(18, 0x17510),
            [("APAS", 5, GameTable.MiningTreasures)] = new(18, 0x17510),
            [("CPUD", 0, GameTable.MiningTreasures)] = new(23, 0x18D70),
            [("CPUF", 0, GameTable.MiningTreasures)] = new(23, 0x18D70),
            [("CPUI", 0, GameTable.MiningTreasures)] = new(23, 0x18D70),
            [("CPUJ", 0, GameTable.MiningTreasures)] = new(23, 0x18D50),
            [("CPUK", 0, GameTable.MiningTreasures)] = new(23, 0x18D70),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.MiningTreasures)] = new(23, 0x18D70),
            [("ADAD", 5, GameTable.MiningItems)] = new(-1, 0x105DB4),
            [("ADAF", 5, GameTable.MiningItems)] = new(-1, 0x105DF4),
            [("ADAI", 5, GameTable.MiningItems)] = new(-1, 0x105D54),
            [("ADAJ", 0, GameTable.MiningItems)] = new(-1, 0x10752C),
            [("ADAK", 0, GameTable.MiningItems)] = new(-1, 0x103174),
            [("ADAS", 5, GameTable.MiningItems)] = new(-1, 0x105E14),
            [("APAD", 5, GameTable.MiningItems)] = new(-1, 0x105DBC),
            [("APAE", 5, GameTable.MiningItems)] = new(-1, 0x105C7C),
            [("APAF", 5, GameTable.MiningItems)] = new(-1, 0x105DFC),
            [("APAI", 5, GameTable.MiningItems)] = new(-1, 0x105D5C),
            [("APAJ", 0, GameTable.MiningItems)] = new(-1, 0x107534),
            [("APAJ", 5, GameTable.MiningItems)] = new(-1, 0x107534),
            [("APAJ", 6, GameTable.MiningItems)] = new(-1, 0x107674),
            [("APAK", 0, GameTable.MiningItems)] = new(-1, 0x10317C),
            [("APAS", 5, GameTable.MiningItems)] = new(-1, 0x105E1C),
            [("CPUD", 0, GameTable.MiningItems)] = new(-1, 0x100ABC),
            [("CPUF", 0, GameTable.MiningItems)] = new(-1, 0x100AFC),
            [("CPUI", 0, GameTable.MiningItems)] = new(-1, 0x100A7C),
            [("CPUJ", 0, GameTable.MiningItems)] = new(-1, 0xFFD14),
            [("CPUK", 0, GameTable.MiningItems)] = new(-1, 0x1017FC),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.MiningItems)] = new(-1, 0x100B1C),
            [("ADAD", 5, GameTable.BpShopPrices)] = new(-1, 0xF4352),
            [("ADAF", 5, GameTable.BpShopPrices)] = new(-1, 0xF4382),
            [("ADAI", 5, GameTable.BpShopPrices)] = new(-1, 0xF42F6),
            [("ADAJ", 0, GameTable.BpShopPrices)] = new(-1, 0xF6164),
            [("ADAK", 0, GameTable.BpShopPrices)] = new(-1, 0xEF98E),
            [("ADAS", 5, GameTable.BpShopPrices)] = new(-1, 0xF438E),
            [("APAD", 5, GameTable.BpShopPrices)] = new(-1, 0xF4352),
            [("APAE", 5, GameTable.BpShopPrices)] = new(-1, 0xF433E),
            [("APAF", 5, GameTable.BpShopPrices)] = new(-1, 0xF4382),
            [("APAI", 5, GameTable.BpShopPrices)] = new(-1, 0xF42F6),
            [("APAJ", 0, GameTable.BpShopPrices)] = new(-1, 0xF6168),
            [("APAJ", 5, GameTable.BpShopPrices)] = new(-1, 0xF6168),
            [("APAJ", 6, GameTable.BpShopPrices)] = new(-1, 0xF62A8),
            [("APAK", 0, GameTable.BpShopPrices)] = new(-1, 0xEF98E),
            [("APAS", 5, GameTable.BpShopPrices)] = new(-1, 0xF438E),
            [("CPUD", 0, GameTable.BpShopPrices)] = new(7, 0x5A50),
            [("CPUF", 0, GameTable.BpShopPrices)] = new(7, 0x5A50),
            [("CPUI", 0, GameTable.BpShopPrices)] = new(7, 0x5A50),
            [("CPUJ", 0, GameTable.BpShopPrices)] = new(7, 0x58B4),
            [("CPUK", 0, GameTable.BpShopPrices)] = new(7, 0x5A50),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.BpShopPrices)] = new(7, 0x5A50),
            [("CPUD", 0, GameTable.BpShopItems)] = new(-1, 0xEAC78),
            [("CPUF", 0, GameTable.BpShopItems)] = new(-1, 0xEACA8),
            [("CPUI", 0, GameTable.BpShopItems)] = new(-1, 0xEAC3C),
            [("CPUJ", 0, GameTable.BpShopItems)] = new(-1, 0xEA2F4),
            [("CPUK", 0, GameTable.BpShopItems)] = new(-1, 0xEB6A8),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.BpShopItems)] = new(-1, 0xEACB4),
            [("CPUD", 0, GameTable.BpShopTms)] = new(-1, 0xEAC10),
            [("CPUF", 0, GameTable.BpShopTms)] = new(-1, 0xEAC40),
            [("CPUI", 0, GameTable.BpShopTms)] = new(-1, 0xEABD4),
            [("CPUJ", 0, GameTable.BpShopTms)] = new(-1, 0xEA28C),
            [("CPUK", 0, GameTable.BpShopTms)] = new(-1, 0xEB640),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.BpShopTms)] = new(-1, 0xEAC4C),
            [("ADAD", 5, GameTable.HeldItemSameItemBranch)] = new(-1, 0x6A14A),
            [("ADAF", 5, GameTable.HeldItemSameItemBranch)] = new(-1, 0x6A14A),
            [("ADAI", 5, GameTable.HeldItemSameItemBranch)] = new(-1, 0x6A12A),
            [("ADAK", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x6A5B6),
            [("ADAS", 5, GameTable.HeldItemSameItemBranch)] = new(-1, 0x6A14A),
            [("APAD", 5, GameTable.HeldItemSameItemBranch)] = new(-1, 0x6A14A),
            [("APAE", 5, GameTable.HeldItemSameItemBranch)] = new(-1, 0x6A0EE),
            [("APAF", 5, GameTable.HeldItemSameItemBranch)] = new(-1, 0x6A14A),
            [("APAI", 5, GameTable.HeldItemSameItemBranch)] = new(-1, 0x6A12A),
            [("APAK", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x6A5B6),
            [("APAS", 5, GameTable.HeldItemSameItemBranch)] = new(-1, 0x6A14A),
            [("CPUD", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x78006),
            [("CPUF", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x78006),
            [("CPUI", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x78006),
            [("CPUJ", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x7783E),
            [("CPUK", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x78456),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x78006),
            [("IPGD", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x721FE),
            [("IPGE", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x721FE),
            [("IPGF", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x721FE),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x721FE),
            [("IPGJ", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x71C92),
            [("IPGK", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x722F2),
            [("IPGS", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x721FE),
            [("IPKD", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x721FE),
            [("IPKF", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x721FE),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x721FE),
            [("IPKJ", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x71C92),
            [("IPKK", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x722FA),
            [("IPKS", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x721F6),   // needs verifying: measured on a dump that is not a known good dump
            [("ADAJ", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x6B624),
            [("APAJ", 0, GameTable.HeldItemSameItemBranch)] = new(-1, 0x6B624),
            [("APAJ", 5, GameTable.HeldItemSameItemBranch)] = new(-1, 0x6B624),
            [("APAJ", 6, GameTable.HeldItemSameItemBranch)] = new(-1, 0x6B724),
            [("ADAD", 5, GameTable.FriendshipChanges)] = new(-1, 0xF7EE8),
            [("ADAF", 5, GameTable.FriendshipChanges)] = new(-1, 0xF7F18),
            [("ADAI", 5, GameTable.FriendshipChanges)] = new(-1, 0xF7E8C),
            [("ADAJ", 0, GameTable.FriendshipChanges)] = new(-1, 0xF9CF8),
            [("ADAK", 0, GameTable.FriendshipChanges)] = new(-1, 0xF3524),
            [("ADAS", 5, GameTable.FriendshipChanges)] = new(-1, 0xF7F24),
            [("APAD", 5, GameTable.FriendshipChanges)] = new(-1, 0xF7EE8),
            [("APAE", 5, GameTable.FriendshipChanges)] = new(-1, 0xF7ED4),
            [("APAF", 5, GameTable.FriendshipChanges)] = new(-1, 0xF7F18),
            [("APAI", 5, GameTable.FriendshipChanges)] = new(-1, 0xF7E8C),
            [("APAJ", 0, GameTable.FriendshipChanges)] = new(-1, 0xF9CFC),
            [("APAJ", 5, GameTable.FriendshipChanges)] = new(-1, 0xF9CFC),
            [("APAJ", 6, GameTable.FriendshipChanges)] = new(-1, 0xF9E3C),
            [("APAK", 0, GameTable.FriendshipChanges)] = new(-1, 0xF3524),
            [("APAS", 5, GameTable.FriendshipChanges)] = new(-1, 0xF7F24),
            [("CPUD", 0, GameTable.FriendshipChanges)] = new(-1, 0xF05F8),
            [("CPUF", 0, GameTable.FriendshipChanges)] = new(-1, 0xF0628),
            [("CPUI", 0, GameTable.FriendshipChanges)] = new(-1, 0xF05BC),
            [("CPUJ", 0, GameTable.FriendshipChanges)] = new(-1, 0xEFC30),
            [("CPUK", 0, GameTable.FriendshipChanges)] = new(-1, 0xF1028),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.FriendshipChanges)] = new(-1, 0xF0634),
            [("IPGD", 0, GameTable.FriendshipChanges)] = new(-1, 0xFF4D8),
            [("IPGE", 0, GameTable.FriendshipChanges)] = new(-1, 0xFF524),
            [("IPGF", 0, GameTable.FriendshipChanges)] = new(-1, 0xFF508),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0, GameTable.FriendshipChanges)] = new(-1, 0xFF49C),
            [("IPGJ", 0, GameTable.FriendshipChanges)] = new(-1, 0xFECA4),
            [("IPGK", 0, GameTable.FriendshipChanges)] = new(-1, 0xFFBC8),
            [("IPGS", 0, GameTable.FriendshipChanges)] = new(-1, 0xFF514),
            [("IPKD", 0, GameTable.FriendshipChanges)] = new(-1, 0xFF4D8),
            [("IPKF", 0, GameTable.FriendshipChanges)] = new(-1, 0xFF508),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0, GameTable.FriendshipChanges)] = new(-1, 0xFF49C),
            [("IPKJ", 0, GameTable.FriendshipChanges)] = new(-1, 0xFECA4),
            [("IPKK", 0, GameTable.FriendshipChanges)] = new(-1, 0xFFBD0),
            [("IPKS", 0, GameTable.FriendshipChanges)] = new(-1, 0xFF50C),   // needs verifying: measured on a dump that is not a known good dump
            [("ADAD", 5, GameTable.TypeIconBanks)] = new(-1, 0xF8450),
            [("ADAF", 5, GameTable.TypeIconBanks)] = new(-1, 0xF8480),
            [("ADAI", 5, GameTable.TypeIconBanks)] = new(-1, 0xF83F4),
            [("ADAK", 0, GameTable.TypeIconBanks)] = new(-1, 0xF3A8C),
            [("ADAS", 5, GameTable.TypeIconBanks)] = new(-1, 0xF848C),
            [("APAD", 5, GameTable.TypeIconBanks)] = new(-1, 0xF8450),
            [("APAE", 5, GameTable.TypeIconBanks)] = new(-1, 0xF843C),
            [("APAF", 5, GameTable.TypeIconBanks)] = new(-1, 0xF8480),
            [("APAI", 5, GameTable.TypeIconBanks)] = new(-1, 0xF83F4),
            [("APAK", 0, GameTable.TypeIconBanks)] = new(-1, 0xF3A8C),
            [("APAS", 5, GameTable.TypeIconBanks)] = new(-1, 0xF848C),
            [("CPUD", 0, GameTable.TypeIconBanks)] = new(-1, 0xF0BA4),
            [("CPUF", 0, GameTable.TypeIconBanks)] = new(-1, 0xF0BD4),
            [("CPUI", 0, GameTable.TypeIconBanks)] = new(-1, 0xF0B68),
            [("CPUJ", 0, GameTable.TypeIconBanks)] = new(-1, 0xF01DC),
            [("CPUK", 0, GameTable.TypeIconBanks)] = new(-1, 0xF15D4),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.TypeIconBanks)] = new(-1, 0xF0BE0),
            [("IPGD", 0, GameTable.TypeIconBanks)] = new(-1, 0x100058),
            [("IPGE", 0, GameTable.TypeIconBanks)] = new(-1, 0x1000A4),
            [("IPGF", 0, GameTable.TypeIconBanks)] = new(-1, 0x100088),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0, GameTable.TypeIconBanks)] = new(-1, 0x10001C),
            [("IPGJ", 0, GameTable.TypeIconBanks)] = new(-1, 0xFF824),
            [("IPGK", 0, GameTable.TypeIconBanks)] = new(-1, 0x100748),
            [("IPGS", 0, GameTable.TypeIconBanks)] = new(-1, 0x100094),
            [("IPKD", 0, GameTable.TypeIconBanks)] = new(-1, 0x100058),
            [("IPKF", 0, GameTable.TypeIconBanks)] = new(-1, 0x100088),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0, GameTable.TypeIconBanks)] = new(-1, 0x10001C),
            [("IPKJ", 0, GameTable.TypeIconBanks)] = new(-1, 0xFF824),
            [("IPKK", 0, GameTable.TypeIconBanks)] = new(-1, 0x100750),
            [("IPKS", 0, GameTable.TypeIconBanks)] = new(-1, 0x10008C),   // needs verifying: measured on a dump that is not a known good dump
            [("ADAJ", 0, GameTable.TypeIconBanks)] = new(-1, 0xFA20C),
            [("APAJ", 0, GameTable.TypeIconBanks)] = new(-1, 0xFA210),
            [("APAJ", 5, GameTable.TypeIconBanks)] = new(-1, 0xFA210),
            [("APAJ", 6, GameTable.TypeIconBanks)] = new(-1, 0xFA350),
            [("ADAD", 5, GameTable.MoveCategoryIconBanks)] = new(-1, 0xF83E4),
            [("ADAF", 5, GameTable.MoveCategoryIconBanks)] = new(-1, 0xF8414),
            [("ADAI", 5, GameTable.MoveCategoryIconBanks)] = new(-1, 0xF8388),
            [("ADAK", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0xF3A20),
            [("ADAS", 5, GameTable.MoveCategoryIconBanks)] = new(-1, 0xF8420),
            [("APAD", 5, GameTable.MoveCategoryIconBanks)] = new(-1, 0xF83E4),
            [("APAE", 5, GameTable.MoveCategoryIconBanks)] = new(-1, 0xF83D0),
            [("APAF", 5, GameTable.MoveCategoryIconBanks)] = new(-1, 0xF8414),
            [("APAI", 5, GameTable.MoveCategoryIconBanks)] = new(-1, 0xF8388),
            [("APAK", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0xF3A20),
            [("APAS", 5, GameTable.MoveCategoryIconBanks)] = new(-1, 0xF8420),
            [("CPUD", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0xF0B38),
            [("CPUF", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0xF0B68),
            [("CPUI", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0xF0AFC),
            [("CPUJ", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0xF0170),
            [("CPUK", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0xF1568),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0xF0B74),
            [("IPGD", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0xFFFEC),
            [("IPGE", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0x100038),
            [("IPGF", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0x10001C),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0xFFFB0),
            [("IPGJ", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0xFF7B8),
            [("IPGK", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0x1006DC),
            [("IPGS", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0x100028),
            [("IPKD", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0xFFFEC),
            [("IPKF", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0x10001C),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0xFFFB0),
            [("IPKJ", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0xFF7B8),
            [("IPKK", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0x1006E4),
            [("IPKS", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0x100020),   // needs verifying: measured on a dump that is not a known good dump
            [("ADAJ", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0xFA1FC),
            [("APAJ", 0, GameTable.MoveCategoryIconBanks)] = new(-1, 0xFA200),
            [("APAJ", 5, GameTable.MoveCategoryIconBanks)] = new(-1, 0xFA200),
            [("APAJ", 6, GameTable.MoveCategoryIconBanks)] = new(-1, 0xFA340),
            [("ADAD", 5, GameTable.TypeIconMembers)] = new(-1, 0xF83F4),
            [("ADAF", 5, GameTable.TypeIconMembers)] = new(-1, 0xF8424),
            [("ADAI", 5, GameTable.TypeIconMembers)] = new(-1, 0xF8398),
            [("ADAJ", 0, GameTable.TypeIconMembers)] = new(-1, 0xFA224),
            [("ADAK", 0, GameTable.TypeIconMembers)] = new(-1, 0xF3A30),
            [("ADAS", 5, GameTable.TypeIconMembers)] = new(-1, 0xF8430),
            [("APAD", 5, GameTable.TypeIconMembers)] = new(-1, 0xF83F4),
            [("APAE", 5, GameTable.TypeIconMembers)] = new(-1, 0xF83E0),
            [("APAF", 5, GameTable.TypeIconMembers)] = new(-1, 0xF8424),
            [("APAI", 5, GameTable.TypeIconMembers)] = new(-1, 0xF8398),
            [("APAJ", 0, GameTable.TypeIconMembers)] = new(-1, 0xFA228),
            [("APAJ", 5, GameTable.TypeIconMembers)] = new(-1, 0xFA228),
            [("APAJ", 6, GameTable.TypeIconMembers)] = new(-1, 0xFA368),
            [("APAK", 0, GameTable.TypeIconMembers)] = new(-1, 0xF3A30),
            [("APAS", 5, GameTable.TypeIconMembers)] = new(-1, 0xF8430),
            [("CPUD", 0, GameTable.TypeIconMembers)] = new(-1, 0xF0B48),
            [("CPUF", 0, GameTable.TypeIconMembers)] = new(-1, 0xF0B78),
            [("CPUI", 0, GameTable.TypeIconMembers)] = new(-1, 0xF0B0C),
            [("CPUJ", 0, GameTable.TypeIconMembers)] = new(-1, 0xF0180),
            [("CPUK", 0, GameTable.TypeIconMembers)] = new(-1, 0xF1578),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.TypeIconMembers)] = new(-1, 0xF0B84),
            [("IPGD", 0, GameTable.TypeIconMembers)] = new(-1, 0xFFFFC),
            [("IPGE", 0, GameTable.TypeIconMembers)] = new(-1, 0x100048),
            [("IPGF", 0, GameTable.TypeIconMembers)] = new(-1, 0x10002C),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0, GameTable.TypeIconMembers)] = new(-1, 0xFFFC0),
            [("IPGJ", 0, GameTable.TypeIconMembers)] = new(-1, 0xFF7C8),
            [("IPGK", 0, GameTable.TypeIconMembers)] = new(-1, 0x1006EC),
            [("IPGS", 0, GameTable.TypeIconMembers)] = new(-1, 0x100038),
            [("IPKD", 0, GameTable.TypeIconMembers)] = new(-1, 0xFFFFC),
            [("IPKF", 0, GameTable.TypeIconMembers)] = new(-1, 0x10002C),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0, GameTable.TypeIconMembers)] = new(-1, 0xFFFC0),
            [("IPKJ", 0, GameTable.TypeIconMembers)] = new(-1, 0xFF7C8),
            [("IPKK", 0, GameTable.TypeIconMembers)] = new(-1, 0x1006F4),
            [("IPKS", 0, GameTable.TypeIconMembers)] = new(-1, 0x100030),   // needs verifying: measured on a dump that is not a known good dump
            [("ADAD", 5, GameTable.MoveTypeButtonPalettes)] = new(8, 0x18BF4),
            [("ADAF", 5, GameTable.MoveTypeButtonPalettes)] = new(8, 0x18BF4),
            [("ADAI", 5, GameTable.MoveTypeButtonPalettes)] = new(8, 0x18BF4),
            [("ADAJ", 0, GameTable.MoveTypeButtonPalettes)] = new(8, 0x196DC),
            [("ADAK", 0, GameTable.MoveTypeButtonPalettes)] = new(8, 0x18BF8),
            [("ADAS", 5, GameTable.MoveTypeButtonPalettes)] = new(8, 0x18BF4),
            [("APAD", 5, GameTable.MoveTypeButtonPalettes)] = new(8, 0x18BF4),
            [("APAE", 5, GameTable.MoveTypeButtonPalettes)] = new(8, 0x18B84),
            [("APAF", 5, GameTable.MoveTypeButtonPalettes)] = new(8, 0x18BF4),
            [("APAI", 5, GameTable.MoveTypeButtonPalettes)] = new(8, 0x18BF4),
            [("APAJ", 0, GameTable.MoveTypeButtonPalettes)] = new(8, 0x196DC),
            [("APAJ", 5, GameTable.MoveTypeButtonPalettes)] = new(8, 0x196DC),
            [("APAJ", 6, GameTable.MoveTypeButtonPalettes)] = new(8, 0x196DC),
            [("APAK", 0, GameTable.MoveTypeButtonPalettes)] = new(8, 0x18BF8),
            [("APAS", 5, GameTable.MoveTypeButtonPalettes)] = new(8, 0x18BF4),
            [("CPUD", 0, GameTable.MoveTypeButtonPalettes)] = new(11, 0x3D0),
            [("CPUF", 0, GameTable.MoveTypeButtonPalettes)] = new(11, 0x3D0),
            [("CPUI", 0, GameTable.MoveTypeButtonPalettes)] = new(11, 0x3D0),
            [("CPUJ", 0, GameTable.MoveTypeButtonPalettes)] = new(11, 0x3D0),
            [("CPUK", 0, GameTable.MoveTypeButtonPalettes)] = new(11, 0x3D0),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.MoveTypeButtonPalettes)] = new(11, 0x3D0),
            [("IPGD", 0, GameTable.MoveTypeButtonPalettes)] = new(6, 0x3D0),
            [("IPGE", 0, GameTable.MoveTypeButtonPalettes)] = new(6, 0x3D0),
            [("IPGF", 0, GameTable.MoveTypeButtonPalettes)] = new(6, 0x3D0),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0, GameTable.MoveTypeButtonPalettes)] = new(6, 0x3D0),
            [("IPGJ", 0, GameTable.MoveTypeButtonPalettes)] = new(6, 0x3D0),
            [("IPGK", 0, GameTable.MoveTypeButtonPalettes)] = new(6, 0x3D0),
            [("IPGS", 0, GameTable.MoveTypeButtonPalettes)] = new(6, 0x3D0),
            [("IPKD", 0, GameTable.MoveTypeButtonPalettes)] = new(6, 0x3D0),
            [("IPKF", 0, GameTable.MoveTypeButtonPalettes)] = new(6, 0x3D0),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0, GameTable.MoveTypeButtonPalettes)] = new(6, 0x3D0),
            [("IPKJ", 0, GameTable.MoveTypeButtonPalettes)] = new(6, 0x3D0),
            [("IPKK", 0, GameTable.MoveTypeButtonPalettes)] = new(6, 0x3D0),
            [("IPKS", 0, GameTable.MoveTypeButtonPalettes)] = new(6, 0x3D0),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGD", 0, GameTable.PokewalkerSprites)] = new(112, 0xE06A),
            [("IPGE", 0, GameTable.PokewalkerSprites)] = new(112, 0xE07E),
            [("IPGF", 0, GameTable.PokewalkerSprites)] = new(112, 0xE06A),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0, GameTable.PokewalkerSprites)] = new(112, 0xE06A),
            [("IPGJ", 0, GameTable.PokewalkerSprites)] = new(112, 0xDFE2),
            [("IPGK", 0, GameTable.PokewalkerSprites)] = new(112, 0xE06A),
            [("IPGS", 0, GameTable.PokewalkerSprites)] = new(112, 0xE06A),
            [("IPKD", 0, GameTable.PokewalkerSprites)] = new(112, 0xE06A),
            [("IPKF", 0, GameTable.PokewalkerSprites)] = new(112, 0xE06A),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0, GameTable.PokewalkerSprites)] = new(112, 0xE06A),
            [("IPKJ", 0, GameTable.PokewalkerSprites)] = new(112, 0xDFE2),
            [("IPKK", 0, GameTable.PokewalkerSprites)] = new(112, 0xE06A),
            [("IPKS", 0, GameTable.PokewalkerSprites)] = new(112, 0xE06A),   // needs verifying: measured on a dump that is not a known good dump
            [("ADAD", 5, GameTable.PunchingMoves)] = new(11, 0x30D76),
            [("ADAF", 5, GameTable.PunchingMoves)] = new(11, 0x30E62),
            [("ADAI", 5, GameTable.PunchingMoves)] = new(11, 0x30D76),
            [("ADAJ", 0, GameTable.PunchingMoves)] = new(11, 0x333D4),
            [("ADAK", 0, GameTable.PunchingMoves)] = new(11, 0x30CEE),
            [("ADAS", 5, GameTable.PunchingMoves)] = new(11, 0x30E62),
            [("APAD", 5, GameTable.PunchingMoves)] = new(11, 0x30D76),
            [("APAE", 5, GameTable.PunchingMoves)] = new(11, 0x30CFA),
            [("APAF", 5, GameTable.PunchingMoves)] = new(11, 0x30E62),
            [("APAI", 5, GameTable.PunchingMoves)] = new(11, 0x30D76),
            [("APAJ", 0, GameTable.PunchingMoves)] = new(11, 0x333D4),
            [("APAJ", 5, GameTable.PunchingMoves)] = new(11, 0x333D4),
            [("APAJ", 6, GameTable.PunchingMoves)] = new(11, 0x333D4),
            [("APAK", 0, GameTable.PunchingMoves)] = new(11, 0x30CEE),
            [("APAS", 5, GameTable.PunchingMoves)] = new(11, 0x30E62),
            [("CPUD", 0, GameTable.PunchingMoves)] = new(16, 0x33AF2),
            [("CPUF", 0, GameTable.PunchingMoves)] = new(16, 0x33BDE),
            [("CPUI", 0, GameTable.PunchingMoves)] = new(16, 0x33AF2),
            [("CPUJ", 0, GameTable.PunchingMoves)] = new(16, 0x33ACA),
            [("CPUK", 0, GameTable.PunchingMoves)] = new(16, 0x33AEE),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.PunchingMoves)] = new(16, 0x33BDE),
            [("IPGD", 0, GameTable.PunchingMoves)] = new(12, 0x352FE),
            [("IPGE", 0, GameTable.PunchingMoves)] = new(12, 0x352FE),
            [("IPGF", 0, GameTable.PunchingMoves)] = new(12, 0x352FE),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0, GameTable.PunchingMoves)] = new(12, 0x352FE),
            [("IPGJ", 0, GameTable.PunchingMoves)] = new(12, 0x352BA),
            [("IPGK", 0, GameTable.PunchingMoves)] = new(12, 0x35302),
            [("IPGS", 0, GameTable.PunchingMoves)] = new(12, 0x352FE),
            [("IPKD", 0, GameTable.PunchingMoves)] = new(12, 0x352FE),
            [("IPKF", 0, GameTable.PunchingMoves)] = new(12, 0x352FE),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0, GameTable.PunchingMoves)] = new(12, 0x352FE),
            [("IPKJ", 0, GameTable.PunchingMoves)] = new(12, 0x352BA),
            [("IPKK", 0, GameTable.PunchingMoves)] = new(12, 0x35302),
            [("IPKS", 0, GameTable.PunchingMoves)] = new(12, 0x352FE),   // needs verifying: measured on a dump that is not a known good dump
            [("ADAD", 5, GameTable.SoundMoves)] = new(11, 0x32A44),
            [("ADAF", 5, GameTable.SoundMoves)] = new(11, 0x32B44),
            [("ADAI", 5, GameTable.SoundMoves)] = new(11, 0x32A44),
            [("ADAJ", 0, GameTable.SoundMoves)] = new(11, 0x350C4),
            [("ADAK", 0, GameTable.SoundMoves)] = new(11, 0x3A104),
            [("ADAS", 5, GameTable.SoundMoves)] = new(11, 0x32B44),
            [("APAD", 5, GameTable.SoundMoves)] = new(11, 0x32A44),
            [("APAE", 5, GameTable.SoundMoves)] = new(11, 0x329C4),
            [("APAF", 5, GameTable.SoundMoves)] = new(11, 0x32B44),
            [("APAI", 5, GameTable.SoundMoves)] = new(11, 0x32A44),
            [("APAJ", 0, GameTable.SoundMoves)] = new(11, 0x350C4),
            [("APAJ", 5, GameTable.SoundMoves)] = new(11, 0x350C4),
            [("APAJ", 6, GameTable.SoundMoves)] = new(11, 0x350C4),
            [("APAK", 0, GameTable.SoundMoves)] = new(11, 0x3A104),
            [("APAS", 5, GameTable.SoundMoves)] = new(11, 0x32B44),
            [("CPUD", 0, GameTable.SoundMoves)] = new(16, 0x35A6C),
            [("CPUF", 0, GameTable.SoundMoves)] = new(16, 0x35B4C),
            [("CPUI", 0, GameTable.SoundMoves)] = new(16, 0x35A6C),
            [("CPUJ", 0, GameTable.SoundMoves)] = new(16, 0x35A4C),
            [("CPUK", 0, GameTable.SoundMoves)] = new(16, 0x35A6C),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.SoundMoves)] = new(16, 0x35B4C),
            [("IPGD", 0, GameTable.SoundMoves)] = new(12, 0x37360),
            [("IPGE", 0, GameTable.SoundMoves)] = new(12, 0x37360),
            [("IPGF", 0, GameTable.SoundMoves)] = new(12, 0x37360),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0, GameTable.SoundMoves)] = new(12, 0x37360),
            [("IPGJ", 0, GameTable.SoundMoves)] = new(12, 0x37320),
            [("IPGK", 0, GameTable.SoundMoves)] = new(12, 0x37360),
            [("IPGS", 0, GameTable.SoundMoves)] = new(12, 0x37360),
            [("IPKD", 0, GameTable.SoundMoves)] = new(12, 0x37360),
            [("IPKF", 0, GameTable.SoundMoves)] = new(12, 0x37360),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0, GameTable.SoundMoves)] = new(12, 0x37360),
            [("IPKJ", 0, GameTable.SoundMoves)] = new(12, 0x37320),
            [("IPKK", 0, GameTable.SoundMoves)] = new(12, 0x37360),
            [("IPKS", 0, GameTable.SoundMoves)] = new(12, 0x37360),   // needs verifying: measured on a dump that is not a known good dump
            [("ADAD", 5, GameTable.RegionalDexCompletion)] = new(-1, 0x24556),
            [("ADAF", 5, GameTable.RegionalDexCompletion)] = new(-1, 0x24556),
            [("ADAI", 5, GameTable.RegionalDexCompletion)] = new(-1, 0x24556),
            [("ADAK", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x24982),
            [("ADAS", 5, GameTable.RegionalDexCompletion)] = new(-1, 0x24556),
            [("APAD", 5, GameTable.RegionalDexCompletion)] = new(-1, 0x24556),
            [("APAE", 5, GameTable.RegionalDexCompletion)] = new(-1, 0x2450A),
            [("APAF", 5, GameTable.RegionalDexCompletion)] = new(-1, 0x24556),
            [("APAI", 5, GameTable.RegionalDexCompletion)] = new(-1, 0x24556),
            [("APAK", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x24982),
            [("APAS", 5, GameTable.RegionalDexCompletion)] = new(-1, 0x24556),
            [("CPUD", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x26F82),
            [("CPUF", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x26F82),
            [("CPUI", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x26F82),
            [("CPUJ", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x26AC2),
            [("CPUK", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x273CE),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x26F82),
            [("IPGD", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x29F66),
            [("IPGE", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x29F66),
            [("IPGF", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x29F66),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x29F66),
            [("IPGJ", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x29AF2),
            [("IPGK", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x2A1F6),
            [("IPGS", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x29F66),
            [("IPKD", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x29F66),
            [("IPKF", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x29F66),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x29F66),
            [("IPKJ", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x29AF2),
            [("IPKK", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x2A1FA),
            [("IPKS", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x29F62),   // needs verifying: measured on a dump that is not a known good dump
            [("ADAJ", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x258B4),
            [("APAJ", 0, GameTable.RegionalDexCompletion)] = new(-1, 0x258B4),
            [("APAJ", 5, GameTable.RegionalDexCompletion)] = new(-1, 0x258B4),
            [("APAJ", 6, GameTable.RegionalDexCompletion)] = new(-1, 0x258E8),
            [("ADAD", 5, GameTable.RegionalDexRatingLast)] = new(-1, 0x54D78),
            [("ADAF", 5, GameTable.RegionalDexRatingLast)] = new(-1, 0x54D78),
            [("ADAI", 5, GameTable.RegionalDexRatingLast)] = new(-1, 0x54D78),
            [("ADAJ", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x582F4),
            [("ADAK", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x551E0),
            [("ADAS", 5, GameTable.RegionalDexRatingLast)] = new(-1, 0x54D78),
            [("APAD", 5, GameTable.RegionalDexRatingLast)] = new(-1, 0x54D78),
            [("APAE", 5, GameTable.RegionalDexRatingLast)] = new(-1, 0x54D08),
            [("APAF", 5, GameTable.RegionalDexRatingLast)] = new(-1, 0x54D78),
            [("APAI", 5, GameTable.RegionalDexRatingLast)] = new(-1, 0x54D78),
            [("APAJ", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x582F4),
            [("APAJ", 5, GameTable.RegionalDexRatingLast)] = new(-1, 0x582F4),
            [("APAJ", 6, GameTable.RegionalDexRatingLast)] = new(-1, 0x583E0),
            [("APAK", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x551E0),
            [("APAS", 5, GameTable.RegionalDexRatingLast)] = new(-1, 0x54D78),
            [("CPUD", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x5E174),
            [("CPUF", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x5E174),
            [("CPUI", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x5E174),
            [("CPUJ", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x5D998),
            [("CPUK", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x5E5C4),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x5E174),
            [("IPGD", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x5BC5C),
            [("IPGE", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x5BC5C),
            [("IPGF", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x5BC5C),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x5BC5C),
            [("IPGJ", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x5B6C0),
            [("IPGK", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x5BD50),
            [("IPGS", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x5BC5C),
            [("IPKD", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x5BC5C),
            [("IPKF", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x5BC5C),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x5BC5C),
            [("IPKJ", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x5B6C0),
            [("IPKK", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x5BD58),
            [("IPKS", 0, GameTable.RegionalDexRatingLast)] = new(-1, 0x5BC54),   // needs verifying: measured on a dump that is not a known good dump
            [("ADAD", 5, GameTable.RegionalDexLookupBound)] = new(-1, 0x69BC8),
            [("ADAF", 5, GameTable.RegionalDexLookupBound)] = new(-1, 0x69BC8),
            [("ADAI", 5, GameTable.RegionalDexLookupBound)] = new(-1, 0x69BA8),
            [("ADAK", 0, GameTable.RegionalDexLookupBound)] = new(-1, 0x6A034),
            [("ADAS", 5, GameTable.RegionalDexLookupBound)] = new(-1, 0x69BC8),
            [("APAD", 5, GameTable.RegionalDexLookupBound)] = new(-1, 0x69BC8),
            [("APAE", 5, GameTable.RegionalDexLookupBound)] = new(-1, 0x69B6C),
            [("APAF", 5, GameTable.RegionalDexLookupBound)] = new(-1, 0x69BC8),
            [("APAI", 5, GameTable.RegionalDexLookupBound)] = new(-1, 0x69BA8),
            [("APAK", 0, GameTable.RegionalDexLookupBound)] = new(-1, 0x6A034),
            [("APAS", 5, GameTable.RegionalDexLookupBound)] = new(-1, 0x69BC8),
            [("CPUD", 0, GameTable.RegionalDexLookupBound)] = new(-1, 0x77670),
            [("CPUF", 0, GameTable.RegionalDexLookupBound)] = new(-1, 0x77670),
            [("CPUI", 0, GameTable.RegionalDexLookupBound)] = new(-1, 0x77670),
            [("CPUJ", 0, GameTable.RegionalDexLookupBound)] = new(-1, 0x76EA8),
            [("CPUK", 0, GameTable.RegionalDexLookupBound)] = new(-1, 0x77AC0),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.RegionalDexLookupBound)] = new(-1, 0x77670),
            [("ADAJ", 0, GameTable.RegionalDexLookupBound)] = new(-1, 0x6BBF0),
            [("APAJ", 0, GameTable.RegionalDexLookupBound)] = new(-1, 0x6BBF0),
            [("APAJ", 5, GameTable.RegionalDexLookupBound)] = new(-1, 0x6BBF0),
            [("APAJ", 6, GameTable.RegionalDexLookupBound)] = new(-1, 0x6BCF0),
            [("ADAD", 5, GameTable.EvolutionSlotBuffer)] = new(-1, 0x69248),
            [("ADAF", 5, GameTable.EvolutionSlotBuffer)] = new(-1, 0x69248),
            [("ADAI", 5, GameTable.EvolutionSlotBuffer)] = new(-1, 0x69228),
            [("ADAK", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x696B4),
            [("ADAS", 5, GameTable.EvolutionSlotBuffer)] = new(-1, 0x69248),
            [("APAD", 5, GameTable.EvolutionSlotBuffer)] = new(-1, 0x69248),
            [("APAE", 5, GameTable.EvolutionSlotBuffer)] = new(-1, 0x691EC),
            [("APAF", 5, GameTable.EvolutionSlotBuffer)] = new(-1, 0x69248),
            [("APAI", 5, GameTable.EvolutionSlotBuffer)] = new(-1, 0x69228),
            [("APAK", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x696B4),
            [("APAS", 5, GameTable.EvolutionSlotBuffer)] = new(-1, 0x69248),
            [("CPUD", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x76CB8),
            [("CPUF", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x76CB8),
            [("CPUI", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x76CB8),
            [("CPUJ", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x764F0),
            [("CPUK", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x77108),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x76CB8),
            [("IPGD", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x70ECE),
            [("IPGE", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x70ECE),
            [("IPGF", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x70ECE),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x70ECE),
            [("IPGJ", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x70962),
            [("IPGK", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x70FC2),
            [("IPGS", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x70ECE),
            [("IPKD", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x70ECE),
            [("IPKF", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x70ECE),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x70ECE),
            [("IPKJ", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x70962),
            [("IPKK", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x70FCA),
            [("IPKS", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x70EC6),   // needs verifying: measured on a dump that is not a known good dump
            [("ADAJ", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x6C2D2),
            [("APAJ", 0, GameTable.EvolutionSlotBuffer)] = new(-1, 0x6C2D2),
            [("APAJ", 5, GameTable.EvolutionSlotBuffer)] = new(-1, 0x6C2D2),
            [("APAJ", 6, GameTable.EvolutionSlotBuffer)] = new(-1, 0x6C3D2),
            [("ADAD", 5, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x694DA),
            [("ADAF", 5, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x694DA),
            [("ADAI", 5, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x694BA),
            [("ADAK", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x69946),
            [("ADAS", 5, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x694DA),
            [("APAD", 5, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x694DA),
            [("APAE", 5, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x6947E),
            [("APAF", 5, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x694DA),
            [("APAI", 5, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x694BA),
            [("APAK", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x69946),
            [("APAS", 5, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x694DA),
            [("CPUD", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x76F4A),
            [("CPUF", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x76F4A),
            [("CPUI", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x76F4A),
            [("CPUJ", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x76782),
            [("CPUK", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x7739A),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x76F4A),
            [("IPGD", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x71168),
            [("IPGE", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x71168),
            [("IPGF", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x71168),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x71168),
            [("IPGJ", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x70BFC),
            [("IPGK", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x7125C),
            [("IPGS", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x71168),
            [("IPKD", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x71168),
            [("IPKF", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x71168),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x71168),
            [("IPKJ", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x70BFC),
            [("IPKK", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x71264),
            [("IPKS", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x71160),   // needs verifying: measured on a dump that is not a known good dump
            [("ADAJ", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x6C57E),
            [("APAJ", 0, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x6C57E),
            [("APAJ", 5, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x6C57E),
            [("APAJ", 6, GameTable.EvolutionSlotLoopLevel)] = new(-1, 0x6C67E),
            [("ADAD", 5, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x69514),
            [("ADAF", 5, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x69514),
            [("ADAI", 5, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x694F4),
            [("ADAK", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x69980),
            [("ADAS", 5, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x69514),
            [("APAD", 5, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x69514),
            [("APAE", 5, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x694B8),
            [("APAF", 5, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x69514),
            [("APAI", 5, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x694F4),
            [("APAK", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x69980),
            [("APAS", 5, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x69514),
            [("CPUD", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x76F86),
            [("CPUF", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x76F86),
            [("CPUI", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x76F86),
            [("CPUJ", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x767BE),
            [("CPUK", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x773D6),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x76F86),
            [("IPGD", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x7119E),
            [("IPGE", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x7119E),
            [("IPGF", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x7119E),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x7119E),
            [("IPGJ", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x70C32),
            [("IPGK", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x71292),
            [("IPGS", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x7119E),
            [("IPKD", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x7119E),
            [("IPKF", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x7119E),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x7119E),
            [("IPKJ", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x70C32),
            [("IPKK", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x7129A),
            [("IPKS", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x71196),   // needs verifying: measured on a dump that is not a known good dump
            [("ADAJ", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x6C5B4),
            [("APAJ", 0, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x6C5B4),
            [("APAJ", 5, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x6C5B4),
            [("APAJ", 6, GameTable.EvolutionSlotLoopTrade)] = new(-1, 0x6C6B4),
            [("ADAD", 5, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x695A2),
            [("ADAF", 5, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x695A2),
            [("ADAI", 5, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x69582),
            [("ADAK", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x69A0E),
            [("ADAS", 5, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x695A2),
            [("APAD", 5, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x695A2),
            [("APAE", 5, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x69546),
            [("APAF", 5, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x695A2),
            [("APAI", 5, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x69582),
            [("APAK", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x69A0E),
            [("APAS", 5, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x695A2),
            [("CPUD", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x77014),
            [("CPUF", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x77014),
            [("CPUI", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x77014),
            [("CPUJ", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x7684C),
            [("CPUK", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x77464),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x77014),
            [("IPGD", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x7122C),
            [("IPGE", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x7122C),
            [("IPGF", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x7122C),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x7122C),
            [("IPGJ", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x70CC0),
            [("IPGK", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x71320),
            [("IPGS", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x7122C),
            [("IPKD", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x7122C),
            [("IPKF", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x7122C),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x7122C),
            [("IPKJ", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x70CC0),
            [("IPKK", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x71328),
            [("IPKS", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x71224),   // needs verifying: measured on a dump that is not a known good dump
            [("ADAJ", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x6C652),
            [("APAJ", 0, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x6C652),
            [("APAJ", 5, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x6C652),
            [("APAJ", 6, GameTable.EvolutionSlotLoopItem)] = new(-1, 0x6C752),
        };

        // Korean numbers its text banks its own way. Each US bank DSPRE uses, and the Korean bank with the same text,
        // matched by message count and line shape and then read side by side in both languages.
        private static readonly Dictionary<GameFamilies, Dictionary<int, int>> KoreanTextBanks = new()
        {
            [GameFamilies.DP] = new() { [10] = 10, [16] = 15, [320] = 319, [326] = 325, [343] = 341, [344] = 342, [362] = 357, [382] = 376, [552] = 546, [555] = 548, [558] = 551, [559] = 552, [560] = 553, [565] = 557, [587] = 576, [588] = 577, [614] = 602, [615] = 603, [616] = 604, [617] = 605, [619] = 607, [620] = 608, [621] = 609, [622] = 610, [623] = 611 },
            [GameFamilies.Plat] = new() { [12] = 12, [21] = 20, [360] = 359, [361] = 360, [370] = 369, [391] = 389, [392] = 390, [412] = 408, [433] = 428, [610] = 605, [614] = 608, [617] = 611, [618] = 612, [619] = 613, [624] = 617, [646] = 636, [647] = 637, [697] = 687, [701] = 691, [702] = 692, [703] = 693, [704] = 694, [705] = 695, [706] = 696, [707] = 697, [709] = 699, [711] = 701, [712] = 702, [713] = 703, [714] = 704, [715] = 705, [716] = 706, [717] = 707, [719] = 709, [720] = 710, [721] = 711, [722] = 712, [723] = 713 },
            [GameFamilies.HGSS] = new() { [15] = 15, [27] = 26, [30] = 29, [190] = 189, [191] = 190, [196] = 195, [200] = 199, [211] = 210, [221] = 219, [222] = 220, [237] = 233, [246] = 241, [271] = 266, [279] = 274, [435] = 430, [445] = 440, [720] = 715, [724] = 718, [728] = 722, [729] = 723, [730] = 724, [735] = 728, [749] = 742, [750] = 743, [752] = 744, [802] = 795, [803] = 796, [804] = 797, [806] = 799, [807] = 800, [808] = 801, [809] = 802, [810] = 803, [812] = 805, [814] = 807, [816] = 809, [817] = 810, [818] = 811, [819] = 812, [820] = 813, [821] = 814, [822] = 815, [824] = 817, [825] = 818, [826] = 819, [827] = 820, [828] = 821 },
        };
    }
}
