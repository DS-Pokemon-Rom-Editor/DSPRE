// Generated from the per-dump tables RomInfo kept before the locator; every supported game and language is
// written out. A value repeated between games is checked against the US dump by RomLocatorTests.
using static DSPRE.RomInfo;

namespace DSPRE
{
    public static partial class RomLocator
    {
        // sItemOdds[2][2] u16: none-below, rare-from; row 1 is Compound Eyes
        private static Spot WildHeldItemOdds(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF7ECC),   // Europe
                GameLanguages.English => Arm9(0xF7ECC),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xF9CF0),
                GameLanguages.French => Arm9(0xF7F10),
                GameLanguages.German => Arm9(0xF7EE0),
                GameLanguages.Italian => Arm9(0xF7E84),
                GameLanguages.Spanish => Arm9(0xF7F1C),
                GameLanguages.Korean => Arm9(0xF351C),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF7ECC),   // Europe
                GameLanguages.English => Arm9(0xF7ECC),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0xF9CF4),
                GameLanguages.Japanese when revision == 5 => Arm9(0xF9CF4),
                GameLanguages.Japanese when revision == 6 => Arm9(0xF9E34),
                GameLanguages.French => Arm9(0xF7F10),
                GameLanguages.German => Arm9(0xF7EE0),
                GameLanguages.Italian => Arm9(0xF7E84),
                GameLanguages.Spanish => Arm9(0xF7F1C),
                GameLanguages.Korean => Arm9(0xF351C),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xF0574),   // Europe
                GameLanguages.English => Arm9(0xF0574),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xEFC04),
                GameLanguages.French => Arm9(0xF05FC),
                GameLanguages.German => Arm9(0xF05CC),
                GameLanguages.Italian => Arm9(0xF0590),
                GameLanguages.Spanish => Arm9(0xF0608),
                GameLanguages.Korean => Arm9(0xF0FFC),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xFF4E4),   // Europe
                GameLanguages.English => Arm9(0xFF4E4),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFEC64),
                GameLanguages.French => Arm9(0xFF4C8),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0xFF498),
                GameLanguages.Italian => Arm9(0xFF45C),
                GameLanguages.Spanish => Arm9(0xFF4CC),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0xFFB90),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xFF4E4),   // Europe
                GameLanguages.English => Arm9(0xFF4E4),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFEC64),
                GameLanguages.French => Arm9(0xFF4C8),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0xFF498),
                GameLanguages.Italian => Arm9(0xFF45C),
                GameLanguages.Spanish => Arm9(0xFF4D4),
                GameLanguages.Korean => Arm9(0xFFB88),
                _ => null,
            },
            _ => null,
        };

        // 9 x (u16 baby, u16 item, u16 fallback)
        private static Spot IncenseBabies(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(5, 0x20632),   // Europe
                GameLanguages.English => Ov(5, 0x20632),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(5, 0x21610),
                GameLanguages.French => Ov(5, 0x205EA),
                GameLanguages.German => Ov(5, 0x205EA),
                GameLanguages.Italian => Ov(5, 0x205EA),
                GameLanguages.Spanish => Ov(5, 0x205EA),
                GameLanguages.Korean => Ov(5, 0x205E6),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(5, 0x20632),   // Europe
                GameLanguages.English => Ov(5, 0x20632),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(5, 0x21610),
                GameLanguages.Japanese when revision == 5 => Ov(5, 0x21610),
                GameLanguages.Japanese when revision == 6 => Ov(5, 0x2161C),
                GameLanguages.French => Ov(5, 0x205EA),
                GameLanguages.German => Ov(5, 0x205EA),
                GameLanguages.Italian => Ov(5, 0x205EA),
                GameLanguages.Spanish => Ov(5, 0x205EA),
                GameLanguages.Korean => Ov(5, 0x205E6),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(5, 0x291EC),   // Europe
                GameLanguages.English => Ov(5, 0x291EC),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(5, 0x28FDC),
                GameLanguages.French => Ov(5, 0x291F4),
                GameLanguages.German => Ov(5, 0x29208),
                GameLanguages.Italian => Ov(5, 0x291FC),
                GameLanguages.Spanish => Ov(5, 0x291F4),
                GameLanguages.Korean => Ov(5, 0x291E4),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xFF4AE),   // Europe
                GameLanguages.English => Arm9(0xFF4AE),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFEC2E),
                GameLanguages.French => Arm9(0xFF492),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0xFF462),
                GameLanguages.Italian => Arm9(0xFF426),
                GameLanguages.Spanish => Arm9(0xFF496),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0xFFB5A),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xFF4AE),   // Europe
                GameLanguages.English => Arm9(0xFF4AE),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFEC2E),
                GameLanguages.French => Arm9(0xFF492),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0xFF462),
                GameLanguages.Italian => Arm9(0xFF426),
                GameLanguages.Spanish => Arm9(0xFF49E),
                GameLanguages.Korean => Arm9(0xFFB52),
                _ => null,
            },
            _ => null,
        };

        // s8[18][18], the Pokétch move tester's copy (DP/Pt)
        private static Spot PoketchTypeChart(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(38, 0x8F4),   // Europe
                GameLanguages.English => Ov(38, 0x8F4),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(38, 0x928),
                GameLanguages.French => Ov(38, 0x8F4),
                GameLanguages.German => Ov(38, 0x8F4),
                GameLanguages.Italian => Ov(38, 0x8F4),
                GameLanguages.Spanish => Ov(38, 0x8F4),
                GameLanguages.Korean => Ov(38, 0x8F4),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(38, 0x8F4),   // Europe
                GameLanguages.English => Ov(38, 0x8F4),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(38, 0x928),
                GameLanguages.Japanese when revision == 5 => Ov(38, 0x928),
                GameLanguages.Japanese when revision == 6 => Ov(38, 0x928),
                GameLanguages.French => Ov(38, 0x8F4),
                GameLanguages.German => Ov(38, 0x8F4),
                GameLanguages.Italian => Ov(38, 0x8F4),
                GameLanguages.Spanish => Ov(38, 0x8F4),
                GameLanguages.Korean => Ov(38, 0x8F4),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(43, 0x8F0),   // Europe
                GameLanguages.English => Ov(43, 0x8F0),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(43, 0x8F0),
                GameLanguages.French => Ov(43, 0x8F0),
                GameLanguages.German => Ov(43, 0x8F0),
                GameLanguages.Italian => Ov(43, 0x8F0),
                GameLanguages.Spanish => Ov(43, 0x8F0),
                GameLanguages.Korean => Ov(43, 0x8F4),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            _ => null,
        };

        // Pt 38 x 12 bytes, HGSS 52 x 4 bytes
        private static Spot TutorPool(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(5, 0x2FF64),   // Europe
                GameLanguages.English => Ov(5, 0x2FF64),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(5, 0x2FD54),
                GameLanguages.French => Ov(5, 0x2FF6C),
                GameLanguages.German => Ov(5, 0x2FF80),
                GameLanguages.Italian => Ov(5, 0x2FF74),
                GameLanguages.Spanish => Ov(5, 0x2FF6C),
                GameLanguages.Korean => Ov(5, 0x2FF5C),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(1, 0x23AE0),   // Europe
                GameLanguages.English => Ov(1, 0x23AE0),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(1, 0x23954),
                GameLanguages.French => Ov(1, 0x23AE0),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(1, 0x23AE0),
                GameLanguages.Italian => Ov(1, 0x23AE0),
                GameLanguages.Spanish => Ov(1, 0x23AE0),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(1, 0x23AE0),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(1, 0x23AE0),   // Europe
                GameLanguages.English => Ov(1, 0x23AE0),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(1, 0x23954),
                GameLanguages.French => Ov(1, 0x23AE0),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(1, 0x23AE0),
                GameLanguages.Italian => Ov(1, 0x23AE0),
                GameLanguages.Spanish => Ov(1, 0x23AE0),
                GameLanguages.Korean => Ov(1, 0x23AE0),
                _ => null,
            },
            _ => null,
        };

        // Pt 505 x 5 bytes (HGSS keeps it in waza_oshie.bin)
        private static Spot TutorCompatibility(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(5, 0x3012C),   // Europe
                GameLanguages.English => Ov(5, 0x3012C),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(5, 0x2FF1C),
                GameLanguages.French => Ov(5, 0x30134),
                GameLanguages.German => Ov(5, 0x30148),
                GameLanguages.Italian => Ov(5, 0x3013C),
                GameLanguages.Spanish => Ov(5, 0x30134),
                GameLanguages.Korean => Ov(5, 0x30124),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            _ => null,
        };

        // 85 x 20 bytes (DP/Pt)
        private static Spot MiningTreasures(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(18, 0x17490),   // Europe
                GameLanguages.English => Ov(18, 0x17490),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(18, 0x18030),
                GameLanguages.French => Ov(18, 0x17510),
                GameLanguages.German => Ov(18, 0x17510),
                GameLanguages.Italian => Ov(18, 0x17510),
                GameLanguages.Spanish => Ov(18, 0x17510),
                GameLanguages.Korean => Ov(18, 0x17510),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(18, 0x17490),   // Europe
                GameLanguages.English => Ov(18, 0x17490),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(18, 0x18030),
                GameLanguages.Japanese when revision == 5 => Ov(18, 0x18030),
                GameLanguages.Japanese when revision == 6 => Ov(18, 0x18030),
                GameLanguages.French => Ov(18, 0x17510),
                GameLanguages.German => Ov(18, 0x17510),
                GameLanguages.Italian => Ov(18, 0x17510),
                GameLanguages.Spanish => Ov(18, 0x17510),
                GameLanguages.Korean => Ov(18, 0x17510),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(23, 0x18D70),   // Europe
                GameLanguages.English => Ov(23, 0x18D70),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(23, 0x18D50),
                GameLanguages.French => Ov(23, 0x18D70),
                GameLanguages.German => Ov(23, 0x18D70),
                GameLanguages.Italian => Ov(23, 0x18D70),
                GameLanguages.Spanish => Ov(23, 0x18D70),
                GameLanguages.Korean => Ov(23, 0x18D70),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            _ => null,
        };

        // 49 u16: bag item for mining ids 11-59 (sMiningItems)
        private static Spot MiningItems(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x105C74),   // Europe
                GameLanguages.English => Arm9(0x105C74),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x10752C),
                GameLanguages.French => Arm9(0x105DF4),
                GameLanguages.German => Arm9(0x105DB4),
                GameLanguages.Italian => Arm9(0x105D54),
                GameLanguages.Spanish => Arm9(0x105E14),
                GameLanguages.Korean => Arm9(0x103174),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x105C7C),   // Europe
                GameLanguages.English => Arm9(0x105C7C),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0x107534),
                GameLanguages.Japanese when revision == 5 => Arm9(0x107534),
                GameLanguages.Japanese when revision == 6 => Arm9(0x107674),
                GameLanguages.French => Arm9(0x105DFC),
                GameLanguages.German => Arm9(0x105DBC),
                GameLanguages.Italian => Arm9(0x105D5C),
                GameLanguages.Spanish => Arm9(0x105E1C),
                GameLanguages.Korean => Arm9(0x10317C),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x100920),   // Europe
                GameLanguages.English => Arm9(0x100920),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFFD14),
                GameLanguages.French => Arm9(0x100AFC),
                GameLanguages.German => Arm9(0x100ABC),
                GameLanguages.Italian => Arm9(0x100A7C),
                GameLanguages.Spanish => Arm9(0x100B1C),
                GameLanguages.Korean => Arm9(0x1017FC),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            _ => null,
        };

        // Pt 41 x (u16 item, u16 BP); DP 41-row item/price table
        private static Spot BpShopPrices(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF433E),   // Europe
                GameLanguages.English => Arm9(0xF433E),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xF6164),
                GameLanguages.French => Arm9(0xF4382),
                GameLanguages.German => Arm9(0xF4352),
                GameLanguages.Italian => Arm9(0xF42F6),
                GameLanguages.Spanish => Arm9(0xF438E),
                GameLanguages.Korean => Arm9(0xEF98E),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF433E),   // Europe
                GameLanguages.English => Arm9(0xF433E),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0xF6168),
                GameLanguages.Japanese when revision == 5 => Arm9(0xF6168),
                GameLanguages.Japanese when revision == 6 => Arm9(0xF62A8),
                GameLanguages.French => Arm9(0xF4382),
                GameLanguages.German => Arm9(0xF4352),
                GameLanguages.Italian => Arm9(0xF42F6),
                GameLanguages.Spanish => Arm9(0xF438E),
                GameLanguages.Korean => Arm9(0xEF98E),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(7, 0x5A98),   // Europe
                GameLanguages.English => Ov(7, 0x5A98),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(7, 0x58B4),
                GameLanguages.French => Ov(7, 0x5A50),
                GameLanguages.German => Ov(7, 0x5A50),
                GameLanguages.Italian => Ov(7, 0x5A50),
                GameLanguages.Spanish => Ov(7, 0x5A50),
                GameLanguages.Korean => Ov(7, 0x5A50),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            _ => null,
        };

        // Pt left corner list, 0xFFFF-terminated
        private static Spot BpShopItems(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xEAC20),   // Europe
                GameLanguages.English => Arm9(0xEAC20),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xEA2F4),
                GameLanguages.French => Arm9(0xEACA8),
                GameLanguages.German => Arm9(0xEAC78),
                GameLanguages.Italian => Arm9(0xEAC3C),
                GameLanguages.Spanish => Arm9(0xEACB4),
                GameLanguages.Korean => Arm9(0xEB6A8),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            _ => null,
        };

        // Pt right corner list, 0xFFFF-terminated
        private static Spot BpShopTms(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xEABB8),   // Europe
                GameLanguages.English => Arm9(0xEABB8),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xEA28C),
                GameLanguages.French => Arm9(0xEAC40),
                GameLanguages.German => Arm9(0xEAC10),
                GameLanguages.Italian => Arm9(0xEABD4),
                GameLanguages.Spanish => Arm9(0xEAC4C),
                GameLanguages.Korean => Arm9(0xEB640),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            _ => null,
        };

        // the BNE after `cmp item1, item2` in WildMonSetRandomHeldItem
        private static Spot HeldItemSameItemBranch(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x6A0EE),   // Europe
                GameLanguages.English => Arm9(0x6A0EE),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x6B624),
                GameLanguages.French => Arm9(0x6A14A),
                GameLanguages.German => Arm9(0x6A14A),
                GameLanguages.Italian => Arm9(0x6A12A),
                GameLanguages.Spanish => Arm9(0x6A14A),
                GameLanguages.Korean => Arm9(0x6A5B6),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x6A0EE),   // Europe
                GameLanguages.English => Arm9(0x6A0EE),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0x6B624),
                GameLanguages.Japanese when revision == 5 => Arm9(0x6B624),
                GameLanguages.Japanese when revision == 6 => Arm9(0x6B724),
                GameLanguages.French => Arm9(0x6A14A),
                GameLanguages.German => Arm9(0x6A14A),
                GameLanguages.Italian => Arm9(0x6A12A),
                GameLanguages.Spanish => Arm9(0x6A14A),
                GameLanguages.Korean => Arm9(0x6A5B6),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x77F66),   // Europe
                GameLanguages.English => Arm9(0x77F66),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x7783E),
                GameLanguages.French => Arm9(0x78006),
                GameLanguages.German => Arm9(0x78006),
                GameLanguages.Italian => Arm9(0x78006),
                GameLanguages.Spanish => Arm9(0x78006),
                GameLanguages.Korean => Arm9(0x78456),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x721FE),   // Europe
                GameLanguages.English => Arm9(0x721FE),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x71C92),
                GameLanguages.French => Arm9(0x721FE),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x721FE),
                GameLanguages.Italian => Arm9(0x721FE),
                GameLanguages.Spanish => Arm9(0x721F6),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x722FA),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x721FE),   // Europe
                GameLanguages.English => Arm9(0x721FE),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x71C92),
                GameLanguages.French => Arm9(0x721FE),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x721FE),
                GameLanguages.Italian => Arm9(0x721FE),
                GameLanguages.Spanish => Arm9(0x721FE),
                GameLanguages.Korean => Arm9(0x722F2),
                _ => null,
            },
            _ => null,
        };

        // u8[23]: palette bank of each type icon (18 types, then 5 contest conditions)
        private static Spot TypeIconBanks(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF843C),   // Europe
                GameLanguages.English => Arm9(0xF843C),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFA20C),
                GameLanguages.French => Arm9(0xF8480),
                GameLanguages.German => Arm9(0xF8450),
                GameLanguages.Italian => Arm9(0xF83F4),
                GameLanguages.Spanish => Arm9(0xF848C),
                GameLanguages.Korean => Arm9(0xF3A8C),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF843C),   // Europe
                GameLanguages.English => Arm9(0xF843C),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0xFA210),
                GameLanguages.Japanese when revision == 5 => Arm9(0xFA210),
                GameLanguages.Japanese when revision == 6 => Arm9(0xFA350),
                GameLanguages.French => Arm9(0xF8480),
                GameLanguages.German => Arm9(0xF8450),
                GameLanguages.Italian => Arm9(0xF83F4),
                GameLanguages.Spanish => Arm9(0xF848C),
                GameLanguages.Korean => Arm9(0xF3A8C),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xF0B4C),   // Europe
                GameLanguages.English => Arm9(0xF0B4C),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xF01DC),
                GameLanguages.French => Arm9(0xF0BD4),
                GameLanguages.German => Arm9(0xF0BA4),
                GameLanguages.Italian => Arm9(0xF0B68),
                GameLanguages.Spanish => Arm9(0xF0BE0),
                GameLanguages.Korean => Arm9(0xF15D4),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x1000A4),   // Europe
                GameLanguages.English => Arm9(0x1000A4),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFF824),
                GameLanguages.French => Arm9(0x100088),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x100058),
                GameLanguages.Italian => Arm9(0x10001C),
                GameLanguages.Spanish => Arm9(0x10008C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x100750),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x1000A4),   // Europe
                GameLanguages.English => Arm9(0x1000A4),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFF824),
                GameLanguages.French => Arm9(0x100088),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x100058),
                GameLanguages.Italian => Arm9(0x10001C),
                GameLanguages.Spanish => Arm9(0x100094),
                GameLanguages.Korean => Arm9(0x100748),
                _ => null,
            },
            _ => null,
        };

        // u8[3]: palette bank of the physical, special and status icons
        private static Spot MoveCategoryIconBanks(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF83D0),   // Europe
                GameLanguages.English => Arm9(0xF83D0),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFA1FC),
                GameLanguages.French => Arm9(0xF8414),
                GameLanguages.German => Arm9(0xF83E4),
                GameLanguages.Italian => Arm9(0xF8388),
                GameLanguages.Spanish => Arm9(0xF8420),
                GameLanguages.Korean => Arm9(0xF3A20),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF83D0),   // Europe
                GameLanguages.English => Arm9(0xF83D0),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0xFA200),
                GameLanguages.Japanese when revision == 5 => Arm9(0xFA200),
                GameLanguages.Japanese when revision == 6 => Arm9(0xFA340),
                GameLanguages.French => Arm9(0xF8414),
                GameLanguages.German => Arm9(0xF83E4),
                GameLanguages.Italian => Arm9(0xF8388),
                GameLanguages.Spanish => Arm9(0xF8420),
                GameLanguages.Korean => Arm9(0xF3A20),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xF0AE0),   // Europe
                GameLanguages.English => Arm9(0xF0AE0),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xF0170),
                GameLanguages.French => Arm9(0xF0B68),
                GameLanguages.German => Arm9(0xF0B38),
                GameLanguages.Italian => Arm9(0xF0AFC),
                GameLanguages.Spanish => Arm9(0xF0B74),
                GameLanguages.Korean => Arm9(0xF1568),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x100038),   // Europe
                GameLanguages.English => Arm9(0x100038),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFF7B8),
                GameLanguages.French => Arm9(0x10001C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0xFFFEC),
                GameLanguages.Italian => Arm9(0xFFFB0),
                GameLanguages.Spanish => Arm9(0x100020),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x1006E4),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x100038),   // Europe
                GameLanguages.English => Arm9(0x100038),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFF7B8),
                GameLanguages.French => Arm9(0x10001C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0xFFFEC),
                GameLanguages.Italian => Arm9(0xFFFB0),
                GameLanguages.Spanish => Arm9(0x100028),
                GameLanguages.Korean => Arm9(0x1006DC),
                _ => null,
            },
            _ => null,
        };

        // u32[23]: battle object archive member of each type or contest icon
        private static Spot TypeIconMembers(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF83E0),   // Europe
                GameLanguages.English => Arm9(0xF83E0),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFA224),
                GameLanguages.French => Arm9(0xF8424),
                GameLanguages.German => Arm9(0xF83F4),
                GameLanguages.Italian => Arm9(0xF8398),
                GameLanguages.Spanish => Arm9(0xF8430),
                GameLanguages.Korean => Arm9(0xF3A30),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF83E0),   // Europe
                GameLanguages.English => Arm9(0xF83E0),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0xFA228),
                GameLanguages.Japanese when revision == 5 => Arm9(0xFA228),
                GameLanguages.Japanese when revision == 6 => Arm9(0xFA368),
                GameLanguages.French => Arm9(0xF8424),
                GameLanguages.German => Arm9(0xF83F4),
                GameLanguages.Italian => Arm9(0xF8398),
                GameLanguages.Spanish => Arm9(0xF8430),
                GameLanguages.Korean => Arm9(0xF3A30),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xF0AF0),   // Europe
                GameLanguages.English => Arm9(0xF0AF0),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xF0180),
                GameLanguages.French => Arm9(0xF0B78),
                GameLanguages.German => Arm9(0xF0B48),
                GameLanguages.Italian => Arm9(0xF0B0C),
                GameLanguages.Spanish => Arm9(0xF0B84),
                GameLanguages.Korean => Arm9(0xF1578),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x100048),   // Europe
                GameLanguages.English => Arm9(0x100048),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFF7C8),
                GameLanguages.French => Arm9(0x10002C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0xFFFFC),
                GameLanguages.Italian => Arm9(0xFFFC0),
                GameLanguages.Spanish => Arm9(0x100030),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x1006F4),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x100048),   // Europe
                GameLanguages.English => Arm9(0x100048),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFF7C8),
                GameLanguages.French => Arm9(0x10002C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0xFFFFC),
                GameLanguages.Italian => Arm9(0xFFFC0),
                GameLanguages.Spanish => Arm9(0x100038),
                GameLanguages.Korean => Arm9(0x1006EC),
                _ => null,
            },
            _ => null,
        };

        // u32[18]: RAM pointers to each type's 16-colour move button palette
        private static Spot MoveTypeButtonPalettes(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(8, 0x18B84),   // Europe
                GameLanguages.English => Ov(8, 0x18B84),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(8, 0x196DC),
                GameLanguages.French => Ov(8, 0x18BF4),
                GameLanguages.German => Ov(8, 0x18BF4),
                GameLanguages.Italian => Ov(8, 0x18BF4),
                GameLanguages.Spanish => Ov(8, 0x18BF4),
                GameLanguages.Korean => Ov(8, 0x18BF8),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(8, 0x18B84),   // Europe
                GameLanguages.English => Ov(8, 0x18B84),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(8, 0x196DC),
                GameLanguages.Japanese when revision == 5 => Ov(8, 0x196DC),
                GameLanguages.Japanese when revision == 6 => Ov(8, 0x196DC),
                GameLanguages.French => Ov(8, 0x18BF4),
                GameLanguages.German => Ov(8, 0x18BF4),
                GameLanguages.Italian => Ov(8, 0x18BF4),
                GameLanguages.Spanish => Ov(8, 0x18BF4),
                GameLanguages.Korean => Ov(8, 0x18BF8),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(11, 0x3D0),   // Europe
                GameLanguages.English => Ov(11, 0x3D0),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(11, 0x3D0),
                GameLanguages.French => Ov(11, 0x3D0),
                GameLanguages.German => Ov(11, 0x3D0),
                GameLanguages.Italian => Ov(11, 0x3D0),
                GameLanguages.Spanish => Ov(11, 0x3D0),
                GameLanguages.Korean => Ov(11, 0x3D0),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(6, 0x3D0),   // Europe
                GameLanguages.English => Ov(6, 0x3D0),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(6, 0x3D0),
                GameLanguages.French => Ov(6, 0x3D0),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(6, 0x3D0),
                GameLanguages.Italian => Ov(6, 0x3D0),
                GameLanguages.Spanish => Ov(6, 0x3D0),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(6, 0x3D0),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(6, 0x3D0),   // Europe
                GameLanguages.English => Ov(6, 0x3D0),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(6, 0x3D0),
                GameLanguages.French => Ov(6, 0x3D0),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(6, 0x3D0),
                GameLanguages.Italian => Ov(6, 0x3D0),
                GameLanguages.Spanish => Ov(6, 0x3D0),
                GameLanguages.Korean => Ov(6, 0x3D0),
                _ => null,
            },
            _ => null,
        };

        // HGSS u16[494][2]: Pokéwalker picture (a/2/5/6) of each species, male then female
        private static Spot PokewalkerSprites(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(112, 0xE07E),   // Europe
                GameLanguages.English => Ov(112, 0xE07E),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(112, 0xDFE2),
                GameLanguages.French => Ov(112, 0xE06A),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(112, 0xE06A),
                GameLanguages.Italian => Ov(112, 0xE06A),
                GameLanguages.Spanish => Ov(112, 0xE06A),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(112, 0xE06A),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(112, 0xE07E),   // Europe
                GameLanguages.English => Ov(112, 0xE07E),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(112, 0xDFE2),
                GameLanguages.French => Ov(112, 0xE06A),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(112, 0xE06A),
                GameLanguages.Italian => Ov(112, 0xE06A),
                GameLanguages.Spanish => Ov(112, 0xE06A),
                GameLanguages.Korean => Ov(112, 0xE06A),
                _ => null,
            },
            _ => null,
        };

        // u16[15]: the moves Iron Fist boosts (pokeplatinum sPunchingMoves), in the battle overlay
        private static Spot PunchingMoves(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x30CFA),   // Europe
                GameLanguages.English => Ov(11, 0x30CFA),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(11, 0x333D4),
                GameLanguages.French => Ov(11, 0x30E62),
                GameLanguages.German => Ov(11, 0x30D76),
                GameLanguages.Italian => Ov(11, 0x30D76),
                GameLanguages.Spanish => Ov(11, 0x30E62),
                GameLanguages.Korean => Ov(11, 0x30CEE),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x30CFA),   // Europe
                GameLanguages.English => Ov(11, 0x30CFA),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(11, 0x333D4),
                GameLanguages.Japanese when revision == 5 => Ov(11, 0x333D4),
                GameLanguages.Japanese when revision == 6 => Ov(11, 0x333D4),
                GameLanguages.French => Ov(11, 0x30E62),
                GameLanguages.German => Ov(11, 0x30D76),
                GameLanguages.Italian => Ov(11, 0x30D76),
                GameLanguages.Spanish => Ov(11, 0x30E62),
                GameLanguages.Korean => Ov(11, 0x30CEE),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(16, 0x33AD6),   // Europe
                GameLanguages.English => Ov(16, 0x33AD6),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(16, 0x33ACA),
                GameLanguages.French => Ov(16, 0x33BDE),
                GameLanguages.German => Ov(16, 0x33AF2),
                GameLanguages.Italian => Ov(16, 0x33AF2),
                GameLanguages.Spanish => Ov(16, 0x33BDE),
                GameLanguages.Korean => Ov(16, 0x33AEE),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x352FE),   // Europe
                GameLanguages.English => Ov(12, 0x352FE),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x352BA),
                GameLanguages.French => Ov(12, 0x352FE),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x352FE),
                GameLanguages.Italian => Ov(12, 0x352FE),
                GameLanguages.Spanish => Ov(12, 0x352FE),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(12, 0x35302),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x352FE),   // Europe
                GameLanguages.English => Ov(12, 0x352FE),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x352BA),
                GameLanguages.French => Ov(12, 0x352FE),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x352FE),
                GameLanguages.Italian => Ov(12, 0x352FE),
                GameLanguages.Spanish => Ov(12, 0x352FE),
                GameLanguages.Korean => Ov(12, 0x35302),
                _ => null,
            },
            _ => null,
        };

        // u16[12] with spare slots: the moves Soundproof blocks (pokeplatinum sSoundMoves)
        private static Spot SoundMoves(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x329C4),   // Europe
                GameLanguages.English => Ov(11, 0x329C4),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(11, 0x350C4),
                GameLanguages.French => Ov(11, 0x32B44),
                GameLanguages.German => Ov(11, 0x32A44),
                GameLanguages.Italian => Ov(11, 0x32A44),
                GameLanguages.Spanish => Ov(11, 0x32B44),
                GameLanguages.Korean => Ov(11, 0x3A104),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x329C4),   // Europe
                GameLanguages.English => Ov(11, 0x329C4),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(11, 0x350C4),
                GameLanguages.Japanese when revision == 5 => Ov(11, 0x350C4),
                GameLanguages.Japanese when revision == 6 => Ov(11, 0x350C4),
                GameLanguages.French => Ov(11, 0x32B44),
                GameLanguages.German => Ov(11, 0x32A44),
                GameLanguages.Italian => Ov(11, 0x32A44),
                GameLanguages.Spanish => Ov(11, 0x32B44),
                GameLanguages.Korean => Ov(11, 0x3A104),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(16, 0x35A4C),   // Europe
                GameLanguages.English => Ov(16, 0x35A4C),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(16, 0x35A4C),
                GameLanguages.French => Ov(16, 0x35B4C),
                GameLanguages.German => Ov(16, 0x35A6C),
                GameLanguages.Italian => Ov(16, 0x35A6C),
                GameLanguages.Spanish => Ov(16, 0x35B4C),
                GameLanguages.Korean => Ov(16, 0x35A6C),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x37360),   // Europe
                GameLanguages.English => Ov(12, 0x37360),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x37320),
                GameLanguages.French => Ov(12, 0x37360),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x37360),
                GameLanguages.Italian => Ov(12, 0x37360),
                GameLanguages.Spanish => Ov(12, 0x37360),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(12, 0x37360),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x37360),   // Europe
                GameLanguages.English => Ov(12, 0x37360),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x37320),
                GameLanguages.French => Ov(12, 0x37360),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x37360),
                GameLanguages.Italian => Ov(12, 0x37360),
                GameLanguages.Spanish => Ov(12, 0x37360),
                GameLanguages.Korean => Ov(12, 0x37360),
                _ => null,
            },
            _ => null,
        };

        // u8, the `cmp r0, #n` immediate: regional species (less ignored mythicals) that complete the regional dex
        private static Spot RegionalDexCompletion(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x2450A),   // Europe
                GameLanguages.English => Arm9(0x2450A),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x258B4),
                GameLanguages.French => Arm9(0x24556),
                GameLanguages.German => Arm9(0x24556),
                GameLanguages.Italian => Arm9(0x24556),
                GameLanguages.Spanish => Arm9(0x24556),
                GameLanguages.Korean => Arm9(0x24982),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x2450A),   // Europe
                GameLanguages.English => Arm9(0x2450A),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0x258B4),
                GameLanguages.Japanese when revision == 5 => Arm9(0x258B4),
                GameLanguages.Japanese when revision == 6 => Arm9(0x258E8),
                GameLanguages.French => Arm9(0x24556),
                GameLanguages.German => Arm9(0x24556),
                GameLanguages.Italian => Arm9(0x24556),
                GameLanguages.Spanish => Arm9(0x24556),
                GameLanguages.Korean => Arm9(0x24982),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x26F12),   // Europe
                GameLanguages.English => Arm9(0x26F12),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x26AC2),
                GameLanguages.French => Arm9(0x26F82),
                GameLanguages.German => Arm9(0x26F82),
                GameLanguages.Italian => Arm9(0x26F82),
                GameLanguages.Spanish => Arm9(0x26F82),
                GameLanguages.Korean => Arm9(0x273CE),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x29F66),   // Europe
                GameLanguages.English => Arm9(0x29F66),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x29AF2),
                GameLanguages.French => Arm9(0x29F66),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x29F66),
                GameLanguages.Italian => Arm9(0x29F66),
                GameLanguages.Spanish => Arm9(0x29F62),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x2A1FA),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x29F66),   // Europe
                GameLanguages.English => Arm9(0x29F66),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x29AF2),
                GameLanguages.French => Arm9(0x29F66),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x29F66),
                GameLanguages.Italian => Arm9(0x29F66),
                GameLanguages.Spanish => Arm9(0x29F66),
                GameLanguages.Korean => Arm9(0x2A1F6),
                _ => null,
            },
            _ => null,
        };

        // u8, the `cmp r0, #n` immediate: the professor's last rating step before "complete", one less
        private static Spot RegionalDexRatingLast(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x54D08),   // Europe
                GameLanguages.English => Arm9(0x54D08),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x582F4),
                GameLanguages.French => Arm9(0x54D78),
                GameLanguages.German => Arm9(0x54D78),
                GameLanguages.Italian => Arm9(0x54D78),
                GameLanguages.Spanish => Arm9(0x54D78),
                GameLanguages.Korean => Arm9(0x551E0),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x54D08),   // Europe
                GameLanguages.English => Arm9(0x54D08),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0x582F4),
                GameLanguages.Japanese when revision == 5 => Arm9(0x582F4),
                GameLanguages.Japanese when revision == 6 => Arm9(0x583E0),
                GameLanguages.French => Arm9(0x54D78),
                GameLanguages.German => Arm9(0x54D78),
                GameLanguages.Italian => Arm9(0x54D78),
                GameLanguages.Spanish => Arm9(0x54D78),
                GameLanguages.Korean => Arm9(0x551E0),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x5E0D0),   // Europe
                GameLanguages.English => Arm9(0x5E0D0),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x5D998),
                GameLanguages.French => Arm9(0x5E174),
                GameLanguages.German => Arm9(0x5E174),
                GameLanguages.Italian => Arm9(0x5E174),
                GameLanguages.Spanish => Arm9(0x5E174),
                GameLanguages.Korean => Arm9(0x5E5C4),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x5BC5C),   // Europe
                GameLanguages.English => Arm9(0x5BC5C),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x5B6C0),
                GameLanguages.French => Arm9(0x5BC5C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x5BC5C),
                GameLanguages.Italian => Arm9(0x5BC5C),
                GameLanguages.Spanish => Arm9(0x5BC54),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x5BD58),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x5BC5C),   // Europe
                GameLanguages.English => Arm9(0x5BC5C),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x5B6C0),
                GameLanguages.French => Arm9(0x5BC5C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x5BC5C),
                GameLanguages.Italian => Arm9(0x5BC5C),
                GameLanguages.Spanish => Arm9(0x5BC5C),
                GameLanguages.Korean => Arm9(0x5BD50),
                _ => null,
            },
            _ => null,
        };

        // u8, the `cmp r3, #n` immediate: the highest regional number the lookup reads (DP/Pt)
        private static Spot RegionalDexLookupBound(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x69B6C),   // Europe
                GameLanguages.English => Arm9(0x69B6C),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x6BBF0),
                GameLanguages.French => Arm9(0x69BC8),
                GameLanguages.German => Arm9(0x69BC8),
                GameLanguages.Italian => Arm9(0x69BA8),
                GameLanguages.Spanish => Arm9(0x69BC8),
                GameLanguages.Korean => Arm9(0x6A034),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x69B6C),   // Europe
                GameLanguages.English => Arm9(0x69B6C),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0x6BBF0),
                GameLanguages.Japanese when revision == 5 => Arm9(0x6BBF0),
                GameLanguages.Japanese when revision == 6 => Arm9(0x6BCF0),
                GameLanguages.French => Arm9(0x69BC8),
                GameLanguages.German => Arm9(0x69BC8),
                GameLanguages.Italian => Arm9(0x69BA8),
                GameLanguages.Spanish => Arm9(0x69BC8),
                GameLanguages.Korean => Arm9(0x6A034),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x775D0),   // Europe
                GameLanguages.English => Arm9(0x775D0),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x76EA8),
                GameLanguages.French => Arm9(0x77670),
                GameLanguages.German => Arm9(0x77670),
                GameLanguages.Italian => Arm9(0x77670),
                GameLanguages.Spanish => Arm9(0x77670),
                GameLanguages.Korean => Arm9(0x77AC0),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            _ => null,
        };

        // u8, the `movs r1, #n` immediate: GetMonEvolution's buffer, 6 bytes per evolution slot
        private static Spot EvolutionSlotBuffer(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x691EC),   // Europe
                GameLanguages.English => Arm9(0x691EC),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x6C2D2),
                GameLanguages.French => Arm9(0x69248),
                GameLanguages.German => Arm9(0x69248),
                GameLanguages.Italian => Arm9(0x69228),
                GameLanguages.Spanish => Arm9(0x69248),
                GameLanguages.Korean => Arm9(0x696B4),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x691EC),   // Europe
                GameLanguages.English => Arm9(0x691EC),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0x6C2D2),
                GameLanguages.Japanese when revision == 5 => Arm9(0x6C2D2),
                GameLanguages.Japanese when revision == 6 => Arm9(0x6C3D2),
                GameLanguages.French => Arm9(0x69248),
                GameLanguages.German => Arm9(0x69248),
                GameLanguages.Italian => Arm9(0x69228),
                GameLanguages.Spanish => Arm9(0x69248),
                GameLanguages.Korean => Arm9(0x696B4),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x76C18),   // Europe
                GameLanguages.English => Arm9(0x76C18),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x764F0),
                GameLanguages.French => Arm9(0x76CB8),
                GameLanguages.German => Arm9(0x76CB8),
                GameLanguages.Italian => Arm9(0x76CB8),
                GameLanguages.Spanish => Arm9(0x76CB8),
                GameLanguages.Korean => Arm9(0x77108),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x70ECE),   // Europe
                GameLanguages.English => Arm9(0x70ECE),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x70962),
                GameLanguages.French => Arm9(0x70ECE),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x70ECE),
                GameLanguages.Italian => Arm9(0x70ECE),
                GameLanguages.Spanish => Arm9(0x70EC6),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x70FCA),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x70ECE),   // Europe
                GameLanguages.English => Arm9(0x70ECE),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x70962),
                GameLanguages.French => Arm9(0x70ECE),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x70ECE),
                GameLanguages.Italian => Arm9(0x70ECE),
                GameLanguages.Spanish => Arm9(0x70ECE),
                GameLanguages.Korean => Arm9(0x70FC2),
                _ => null,
            },
            _ => null,
        };

        // u8, the `cmp r0, #n` immediate: slots the level-up loop walks
        private static Spot EvolutionSlotLoopLevel(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x6947E),   // Europe
                GameLanguages.English => Arm9(0x6947E),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x6C57E),
                GameLanguages.French => Arm9(0x694DA),
                GameLanguages.German => Arm9(0x694DA),
                GameLanguages.Italian => Arm9(0x694BA),
                GameLanguages.Spanish => Arm9(0x694DA),
                GameLanguages.Korean => Arm9(0x69946),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x6947E),   // Europe
                GameLanguages.English => Arm9(0x6947E),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0x6C57E),
                GameLanguages.Japanese when revision == 5 => Arm9(0x6C57E),
                GameLanguages.Japanese when revision == 6 => Arm9(0x6C67E),
                GameLanguages.French => Arm9(0x694DA),
                GameLanguages.German => Arm9(0x694DA),
                GameLanguages.Italian => Arm9(0x694BA),
                GameLanguages.Spanish => Arm9(0x694DA),
                GameLanguages.Korean => Arm9(0x69946),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x76EAA),   // Europe
                GameLanguages.English => Arm9(0x76EAA),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x76782),
                GameLanguages.French => Arm9(0x76F4A),
                GameLanguages.German => Arm9(0x76F4A),
                GameLanguages.Italian => Arm9(0x76F4A),
                GameLanguages.Spanish => Arm9(0x76F4A),
                GameLanguages.Korean => Arm9(0x7739A),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x71168),   // Europe
                GameLanguages.English => Arm9(0x71168),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x70BFC),
                GameLanguages.French => Arm9(0x71168),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x71168),
                GameLanguages.Italian => Arm9(0x71168),
                GameLanguages.Spanish => Arm9(0x71160),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x71264),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x71168),   // Europe
                GameLanguages.English => Arm9(0x71168),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x70BFC),
                GameLanguages.French => Arm9(0x71168),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x71168),
                GameLanguages.Italian => Arm9(0x71168),
                GameLanguages.Spanish => Arm9(0x71168),
                GameLanguages.Korean => Arm9(0x7125C),
                _ => null,
            },
            _ => null,
        };

        // u8, the `cmp r4, #n` immediate: slots the trade loop walks
        private static Spot EvolutionSlotLoopTrade(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x694B8),   // Europe
                GameLanguages.English => Arm9(0x694B8),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x6C5B4),
                GameLanguages.French => Arm9(0x69514),
                GameLanguages.German => Arm9(0x69514),
                GameLanguages.Italian => Arm9(0x694F4),
                GameLanguages.Spanish => Arm9(0x69514),
                GameLanguages.Korean => Arm9(0x69980),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x694B8),   // Europe
                GameLanguages.English => Arm9(0x694B8),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0x6C5B4),
                GameLanguages.Japanese when revision == 5 => Arm9(0x6C5B4),
                GameLanguages.Japanese when revision == 6 => Arm9(0x6C6B4),
                GameLanguages.French => Arm9(0x69514),
                GameLanguages.German => Arm9(0x69514),
                GameLanguages.Italian => Arm9(0x694F4),
                GameLanguages.Spanish => Arm9(0x69514),
                GameLanguages.Korean => Arm9(0x69980),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x76EE6),   // Europe
                GameLanguages.English => Arm9(0x76EE6),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x767BE),
                GameLanguages.French => Arm9(0x76F86),
                GameLanguages.German => Arm9(0x76F86),
                GameLanguages.Italian => Arm9(0x76F86),
                GameLanguages.Spanish => Arm9(0x76F86),
                GameLanguages.Korean => Arm9(0x773D6),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x7119E),   // Europe
                GameLanguages.English => Arm9(0x7119E),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x70C32),
                GameLanguages.French => Arm9(0x7119E),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x7119E),
                GameLanguages.Italian => Arm9(0x7119E),
                GameLanguages.Spanish => Arm9(0x71196),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x7129A),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x7119E),   // Europe
                GameLanguages.English => Arm9(0x7119E),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x70C32),
                GameLanguages.French => Arm9(0x7119E),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x7119E),
                GameLanguages.Italian => Arm9(0x7119E),
                GameLanguages.Spanish => Arm9(0x7119E),
                GameLanguages.Korean => Arm9(0x71292),
                _ => null,
            },
            _ => null,
        };

        // u8, the `cmp r0, #n` immediate: slots the item loop walks
        private static Spot EvolutionSlotLoopItem(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x69546),   // Europe
                GameLanguages.English => Arm9(0x69546),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x6C652),
                GameLanguages.French => Arm9(0x695A2),
                GameLanguages.German => Arm9(0x695A2),
                GameLanguages.Italian => Arm9(0x69582),
                GameLanguages.Spanish => Arm9(0x695A2),
                GameLanguages.Korean => Arm9(0x69A0E),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x69546),   // Europe
                GameLanguages.English => Arm9(0x69546),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0x6C652),
                GameLanguages.Japanese when revision == 5 => Arm9(0x6C652),
                GameLanguages.Japanese when revision == 6 => Arm9(0x6C752),
                GameLanguages.French => Arm9(0x695A2),
                GameLanguages.German => Arm9(0x695A2),
                GameLanguages.Italian => Arm9(0x69582),
                GameLanguages.Spanish => Arm9(0x695A2),
                GameLanguages.Korean => Arm9(0x69A0E),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x76F74),   // Europe
                GameLanguages.English => Arm9(0x76F74),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x7684C),
                GameLanguages.French => Arm9(0x77014),
                GameLanguages.German => Arm9(0x77014),
                GameLanguages.Italian => Arm9(0x77014),
                GameLanguages.Spanish => Arm9(0x77014),
                GameLanguages.Korean => Arm9(0x77464),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x7122C),   // Europe
                GameLanguages.English => Arm9(0x7122C),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x70CC0),
                GameLanguages.French => Arm9(0x7122C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x7122C),
                GameLanguages.Italian => Arm9(0x7122C),
                GameLanguages.Spanish => Arm9(0x71224),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x71328),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x7122C),   // Europe
                GameLanguages.English => Arm9(0x7122C),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x70CC0),
                GameLanguages.French => Arm9(0x7122C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x7122C),
                GameLanguages.Italian => Arm9(0x7122C),
                GameLanguages.Spanish => Arm9(0x7122C),
                GameLanguages.Korean => Arm9(0x71320),
                _ => null,
            },
            _ => null,
        };
    }
}
