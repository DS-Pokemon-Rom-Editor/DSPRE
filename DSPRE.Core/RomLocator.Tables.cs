// Generated from the per-dump tables RomInfo kept before the locator; every supported game and language is
// written out. A value repeated between games is checked against the US dump by RomLocatorTests.
using static DSPRE.RomInfo;

namespace DSPRE
{
    public static partial class RomLocator
    {
        // s8[10][3]: friendship change per event and friendship band.
        private static Spot FriendshipChanges(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF7ED4),   // Europe
                GameLanguages.English => Arm9(0xF7ED4),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xF9CF8),
                GameLanguages.French => Arm9(0xF7F18),
                GameLanguages.German => Arm9(0xF7EE8),
                GameLanguages.Italian => Arm9(0xF7E8C),
                GameLanguages.Spanish => Arm9(0xF7F24),
                GameLanguages.Korean => Arm9(0xF3524),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF7ED4),   // Europe
                GameLanguages.English => Arm9(0xF7ED4),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0xF9CFC),
                GameLanguages.Japanese when revision == 5 => Arm9(0xF9CFC),
                GameLanguages.Japanese when revision == 6 => Arm9(0xF9E3C),
                GameLanguages.French => Arm9(0xF7F18),
                GameLanguages.German => Arm9(0xF7EE8),
                GameLanguages.Italian => Arm9(0xF7E8C),
                GameLanguages.Spanish => Arm9(0xF7F24),
                GameLanguages.Korean => Arm9(0xF3524),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xF05A0),   // Europe
                GameLanguages.English => Arm9(0xF05A0),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xEFC30),
                GameLanguages.French => Arm9(0xF0628),
                GameLanguages.German => Arm9(0xF05F8),
                GameLanguages.Italian => Arm9(0xF05BC),
                GameLanguages.Spanish => Arm9(0xF0634),
                GameLanguages.Korean => Arm9(0xF1028),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xFF524),   // Europe
                GameLanguages.English => Arm9(0xFF524),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFECA4),
                GameLanguages.French => Arm9(0xFF508),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0xFF4D8),
                GameLanguages.Italian => Arm9(0xFF49C),
                GameLanguages.Spanish => Arm9(0xFF50C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0xFFBD0),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xFF524),   // Europe
                GameLanguages.English => Arm9(0xFF524),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFECA4),
                GameLanguages.French => Arm9(0xFF508),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0xFF4D8),
                GameLanguages.Italian => Arm9(0xFF49C),
                GameLanguages.Spanish => Arm9(0xFF514),
                GameLanguages.Korean => Arm9(0xFFBC8),
                _ => null,
            },
            _ => null,
        };

        // The battle overlay's type chart: three bytes per matchup, ending at the Foresight marker.
        private static Spot TypeChart(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x30DB8),   // Europe
                GameLanguages.English => Ov(11, 0x30DB8),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(11, 0x33498),
                GameLanguages.French => Ov(11, 0x30F20),
                GameLanguages.German => Ov(11, 0x30E34),
                GameLanguages.Italian => Ov(11, 0x30E34),
                GameLanguages.Spanish => Ov(11, 0x30F20),
                GameLanguages.Korean => Ov(11, 0x30DAC),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x30DB8),   // Europe
                GameLanguages.English => Ov(11, 0x30DB8),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(11, 0x33498),
                GameLanguages.Japanese when revision == 5 => Ov(11, 0x33498),
                GameLanguages.Japanese when revision == 6 => Ov(11, 0x33498),
                GameLanguages.French => Ov(11, 0x30F20),
                GameLanguages.German => Ov(11, 0x30E34),
                GameLanguages.Italian => Ov(11, 0x30E34),
                GameLanguages.Spanish => Ov(11, 0x30F20),
                GameLanguages.Korean => Ov(11, 0x30DAC),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(16, 0x33B94),   // Europe
                GameLanguages.English => Ov(16, 0x33B94),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(16, 0x33B88),
                GameLanguages.French => Ov(16, 0x33C9C),
                GameLanguages.German => Ov(16, 0x33BB0),
                GameLanguages.Italian => Ov(16, 0x33BB0),
                GameLanguages.Spanish => Ov(16, 0x33C9C),
                GameLanguages.Korean => Ov(16, 0x33BAC),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x353BC),   // Europe
                GameLanguages.English => Ov(12, 0x353BC),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x35378),
                GameLanguages.French => Ov(12, 0x353BC),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x353BC),
                GameLanguages.Italian => Ov(12, 0x353BC),
                GameLanguages.Spanish => Ov(12, 0x353BC),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(12, 0x353C0),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x353BC),   // Europe
                GameLanguages.English => Ov(12, 0x353BC),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x35378),
                GameLanguages.French => Ov(12, 0x353BC),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x353BC),
                GameLanguages.Italian => Ov(12, 0x353BC),
                GameLanguages.Spanish => Ov(12, 0x353BC),
                GameLanguages.Korean => Ov(12, 0x353C0),
                _ => null,
            },
            _ => null,
        };

        // Literals holding the chart's address, the address + 1 and the address + 2.
        private static Spot TypeChartPointers(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x18FD8, 0x19100, 0x195D0, 0x19624),   // Europe
                GameLanguages.English => Ov(11, 0x18FD8, 0x19100, 0x195D0, 0x19624),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(11, 0x1CC4C, 0x1CC8C, 0x1D2B8, 0x1D614),
                GameLanguages.French => Ov(11, 0x19114, 0x1923C, 0x1970C, 0x19760),
                GameLanguages.German => Ov(11, 0x19028, 0x19150, 0x19620, 0x19674),
                GameLanguages.Italian => Ov(11, 0x19028, 0x19150, 0x19620, 0x19674),
                GameLanguages.Spanish => Ov(11, 0x19114, 0x1923C, 0x1970C, 0x19760),
                GameLanguages.Korean => Ov(11, 0x19028, 0x19150, 0x19620, 0x19674),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x18FD8, 0x19100, 0x195D0, 0x19624),   // Europe
                GameLanguages.English => Ov(11, 0x18FD8, 0x19100, 0x195D0, 0x19624),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(11, 0x1CC4C, 0x1CC8C, 0x1D2B8, 0x1D614),
                GameLanguages.Japanese when revision == 5 => Ov(11, 0x1CC4C, 0x1CC8C, 0x1D2B8, 0x1D614),
                GameLanguages.Japanese when revision == 6 => Ov(11, 0x1CC4C, 0x1CC8C, 0x1D2B8, 0x1D614),
                GameLanguages.French => Ov(11, 0x19114, 0x1923C, 0x1970C, 0x19760),
                GameLanguages.German => Ov(11, 0x19028, 0x19150, 0x19620, 0x19674),
                GameLanguages.Italian => Ov(11, 0x19028, 0x19150, 0x19620, 0x19674),
                GameLanguages.Spanish => Ov(11, 0x19114, 0x1923C, 0x1970C, 0x19760),
                GameLanguages.Korean => Ov(11, 0x19028, 0x19150, 0x19620, 0x19674),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(16, 0x1A18C, 0x1A2B4, 0x1A780, 0x1A7D4),   // Europe
                GameLanguages.English => Ov(16, 0x1A18C, 0x1A2B4, 0x1A780, 0x1A7D4),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(16, 0x1A180, 0x1A2A8, 0x1A774, 0x1A7C8),
                GameLanguages.French => Ov(16, 0x1A294, 0x1A3BC, 0x1A888, 0x1A8DC),
                GameLanguages.German => Ov(16, 0x1A1A8, 0x1A2D0, 0x1A79C, 0x1A7F0),
                GameLanguages.Italian => Ov(16, 0x1A1A8, 0x1A2D0, 0x1A79C, 0x1A7F0),
                GameLanguages.Spanish => Ov(16, 0x1A294, 0x1A3BC, 0x1A888, 0x1A8DC),
                GameLanguages.Korean => Ov(16, 0x1A1A4, 0x1A2CC, 0x1A798, 0x1A7EC),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x1A78C, 0x1A8B4, 0x1AD80, 0x1ADD4),   // Europe
                GameLanguages.English => Ov(12, 0x1A78C, 0x1A8B4, 0x1AD80, 0x1ADD4),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x1A794, 0x1A8BC, 0x1AD88, 0x1ADDC),
                GameLanguages.French => Ov(12, 0x1A78C, 0x1A8B4, 0x1AD80, 0x1ADD4),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x1A78C, 0x1A8B4, 0x1AD80, 0x1ADD4),
                GameLanguages.Italian => Ov(12, 0x1A78C, 0x1A8B4, 0x1AD80, 0x1ADD4),
                GameLanguages.Spanish => Ov(12, 0x1A78C, 0x1A8B4, 0x1AD80, 0x1ADD4),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(12, 0x1A790, 0x1A8B8, 0x1AD84, 0x1ADD8),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x1A78C, 0x1A8B4, 0x1AD80, 0x1ADD4),   // Europe
                GameLanguages.English => Ov(12, 0x1A78C, 0x1A8B4, 0x1AD80, 0x1ADD4),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x1A794, 0x1A8BC, 0x1AD88, 0x1ADDC),
                GameLanguages.French => Ov(12, 0x1A78C, 0x1A8B4, 0x1AD80, 0x1ADD4),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x1A78C, 0x1A8B4, 0x1AD80, 0x1ADD4),
                GameLanguages.Italian => Ov(12, 0x1A78C, 0x1A8B4, 0x1AD80, 0x1ADD4),
                GameLanguages.Spanish => Ov(12, 0x1A78C, 0x1A8B4, 0x1AD80, 0x1ADD4),
                GameLanguages.Korean => Ov(12, 0x1A790, 0x1A8B8, 0x1AD84, 0x1ADD8),
                _ => null,
            },
            _ => null,
        };

        private static Spot TypeChartPointersPlus1(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x18CAC, 0x1914C, 0x195D4),   // Europe
                GameLanguages.English => Ov(11, 0x18CAC, 0x1914C, 0x195D4),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(11, 0x1CC90, 0x1D174, 0x1D6E0),
                GameLanguages.French => Ov(11, 0x18DE8, 0x19288, 0x19710),
                GameLanguages.German => Ov(11, 0x18CFC, 0x1919C, 0x19624),
                GameLanguages.Italian => Ov(11, 0x18CFC, 0x1919C, 0x19624),
                GameLanguages.Spanish => Ov(11, 0x18DE8, 0x19288, 0x19710),
                GameLanguages.Korean => Ov(11, 0x18CFC, 0x1919C, 0x19624),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x18CAC, 0x1914C, 0x195D4),   // Europe
                GameLanguages.English => Ov(11, 0x18CAC, 0x1914C, 0x195D4),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(11, 0x1CC90, 0x1D174, 0x1D6E0),
                GameLanguages.Japanese when revision == 5 => Ov(11, 0x1CC90, 0x1D174, 0x1D6E0),
                GameLanguages.Japanese when revision == 6 => Ov(11, 0x1CC90, 0x1D174, 0x1D6E0),
                GameLanguages.French => Ov(11, 0x18DE8, 0x19288, 0x19710),
                GameLanguages.German => Ov(11, 0x18CFC, 0x1919C, 0x19624),
                GameLanguages.Italian => Ov(11, 0x18CFC, 0x1919C, 0x19624),
                GameLanguages.Spanish => Ov(11, 0x18DE8, 0x19288, 0x19710),
                GameLanguages.Korean => Ov(11, 0x18CFC, 0x1919C, 0x19624),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(16, 0x19E60, 0x1A300, 0x1A784),   // Europe
                GameLanguages.English => Ov(16, 0x19E60, 0x1A300, 0x1A784),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(16, 0x19E54, 0x1A2F4, 0x1A778),
                GameLanguages.French => Ov(16, 0x19F68, 0x1A408, 0x1A88C),
                GameLanguages.German => Ov(16, 0x19E7C, 0x1A31C, 0x1A7A0),
                GameLanguages.Italian => Ov(16, 0x19E7C, 0x1A31C, 0x1A7A0),
                GameLanguages.Spanish => Ov(16, 0x19F68, 0x1A408, 0x1A88C),
                GameLanguages.Korean => Ov(16, 0x19E78, 0x1A318, 0x1A79C),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x1A460, 0x1A900, 0x1AD84),   // Europe
                GameLanguages.English => Ov(12, 0x1A460, 0x1A900, 0x1AD84),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x1A468, 0x1A908, 0x1AD8C),
                GameLanguages.French => Ov(12, 0x1A460, 0x1A900, 0x1AD84),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x1A460, 0x1A900, 0x1AD84),
                GameLanguages.Italian => Ov(12, 0x1A460, 0x1A900, 0x1AD84),
                GameLanguages.Spanish => Ov(12, 0x1A460, 0x1A900, 0x1AD84),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(12, 0x1A464, 0x1A904, 0x1AD88),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x1A460, 0x1A900, 0x1AD84),   // Europe
                GameLanguages.English => Ov(12, 0x1A460, 0x1A900, 0x1AD84),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x1A468, 0x1A908, 0x1AD8C),
                GameLanguages.French => Ov(12, 0x1A460, 0x1A900, 0x1AD84),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x1A460, 0x1A900, 0x1AD84),
                GameLanguages.Italian => Ov(12, 0x1A460, 0x1A900, 0x1AD84),
                GameLanguages.Spanish => Ov(12, 0x1A460, 0x1A900, 0x1AD84),
                GameLanguages.Korean => Ov(12, 0x1A464, 0x1A904, 0x1AD88),
                _ => null,
            },
            _ => null,
        };

        private static Spot TypeChartPointersPlus2(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x18CB0, 0x19150, 0x195D8),   // Europe
                GameLanguages.English => Ov(11, 0x18CB0, 0x19150, 0x195D8),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(11, 0x1CC94, 0x1D178, 0x1D6E4),
                GameLanguages.French => Ov(11, 0x18DEC, 0x1928C, 0x19714),
                GameLanguages.German => Ov(11, 0x18D00, 0x191A0, 0x19628),
                GameLanguages.Italian => Ov(11, 0x18D00, 0x191A0, 0x19628),
                GameLanguages.Spanish => Ov(11, 0x18DEC, 0x1928C, 0x19714),
                GameLanguages.Korean => Ov(11, 0x18D00, 0x191A0, 0x19628),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x18CB0, 0x19150, 0x195D8),   // Europe
                GameLanguages.English => Ov(11, 0x18CB0, 0x19150, 0x195D8),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(11, 0x1CC94, 0x1D178, 0x1D6E4),
                GameLanguages.Japanese when revision == 5 => Ov(11, 0x1CC94, 0x1D178, 0x1D6E4),
                GameLanguages.Japanese when revision == 6 => Ov(11, 0x1CC94, 0x1D178, 0x1D6E4),
                GameLanguages.French => Ov(11, 0x18DEC, 0x1928C, 0x19714),
                GameLanguages.German => Ov(11, 0x18D00, 0x191A0, 0x19628),
                GameLanguages.Italian => Ov(11, 0x18D00, 0x191A0, 0x19628),
                GameLanguages.Spanish => Ov(11, 0x18DEC, 0x1928C, 0x19714),
                GameLanguages.Korean => Ov(11, 0x18D00, 0x191A0, 0x19628),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(16, 0x19E64, 0x1A304, 0x1A788),   // Europe
                GameLanguages.English => Ov(16, 0x19E64, 0x1A304, 0x1A788),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(16, 0x19E58, 0x1A2F8, 0x1A77C),
                GameLanguages.French => Ov(16, 0x19F6C, 0x1A40C, 0x1A890),
                GameLanguages.German => Ov(16, 0x19E80, 0x1A320, 0x1A7A4),
                GameLanguages.Italian => Ov(16, 0x19E80, 0x1A320, 0x1A7A4),
                GameLanguages.Spanish => Ov(16, 0x19F6C, 0x1A40C, 0x1A890),
                GameLanguages.Korean => Ov(16, 0x19E7C, 0x1A31C, 0x1A7A0),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x1A464, 0x1A904, 0x1AD88),   // Europe
                GameLanguages.English => Ov(12, 0x1A464, 0x1A904, 0x1AD88),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x1A46C, 0x1A90C, 0x1AD90),
                GameLanguages.French => Ov(12, 0x1A464, 0x1A904, 0x1AD88),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x1A464, 0x1A904, 0x1AD88),
                GameLanguages.Italian => Ov(12, 0x1A464, 0x1A904, 0x1AD88),
                GameLanguages.Spanish => Ov(12, 0x1A464, 0x1A904, 0x1AD88),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(12, 0x1A468, 0x1A908, 0x1AD8C),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x1A464, 0x1A904, 0x1AD88),   // Europe
                GameLanguages.English => Ov(12, 0x1A464, 0x1A904, 0x1AD88),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x1A46C, 0x1A90C, 0x1AD90),
                GameLanguages.French => Ov(12, 0x1A464, 0x1A904, 0x1AD88),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x1A464, 0x1A904, 0x1AD88),
                GameLanguages.Italian => Ov(12, 0x1A464, 0x1A904, 0x1AD88),
                GameLanguages.Spanish => Ov(12, 0x1A464, 0x1A904, 0x1AD88),
                GameLanguages.Korean => Ov(12, 0x1A468, 0x1A908, 0x1AD8C),
                _ => null,
            },
            _ => null,
        };

        // Conversion 2's `cmp rN, #count` over the chart, and the `movs r1, #count` its random pick is taken modulo.
        private static Spot TypeChartCount(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x195A4),   // Europe
                GameLanguages.English => Ov(11, 0x195A4),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(11, 0x1CC5E),
                GameLanguages.French => Ov(11, 0x196E0),
                GameLanguages.German => Ov(11, 0x195F4),
                GameLanguages.Italian => Ov(11, 0x195F4),
                GameLanguages.Spanish => Ov(11, 0x196E0),
                GameLanguages.Korean => Ov(11, 0x195F4),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x195A4),   // Europe
                GameLanguages.English => Ov(11, 0x195A4),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(11, 0x1CC5E),
                GameLanguages.Japanese when revision == 5 => Ov(11, 0x1CC5E),
                GameLanguages.Japanese when revision == 6 => Ov(11, 0x1CC5E),
                GameLanguages.French => Ov(11, 0x196E0),
                GameLanguages.German => Ov(11, 0x195F4),
                GameLanguages.Italian => Ov(11, 0x195F4),
                GameLanguages.Spanish => Ov(11, 0x196E0),
                GameLanguages.Korean => Ov(11, 0x195F4),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(16, 0x1A754),   // Europe
                GameLanguages.English => Ov(16, 0x1A754),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(16, 0x1A748),
                GameLanguages.French => Ov(16, 0x1A85C),
                GameLanguages.German => Ov(16, 0x1A770),
                GameLanguages.Italian => Ov(16, 0x1A770),
                GameLanguages.Spanish => Ov(16, 0x1A85C),
                GameLanguages.Korean => Ov(16, 0x1A76C),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x1AD54),   // Europe
                GameLanguages.English => Ov(12, 0x1AD54),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x1AD5C),
                GameLanguages.French => Ov(12, 0x1AD54),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x1AD54),
                GameLanguages.Italian => Ov(12, 0x1AD54),
                GameLanguages.Spanish => Ov(12, 0x1AD54),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(12, 0x1AD58),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x1AD54),   // Europe
                GameLanguages.English => Ov(12, 0x1AD54),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x1AD5C),
                GameLanguages.French => Ov(12, 0x1AD54),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x1AD54),
                GameLanguages.Italian => Ov(12, 0x1AD54),
                GameLanguages.Spanish => Ov(12, 0x1AD54),
                GameLanguages.Korean => Ov(12, 0x1AD58),
                _ => null,
            },
            _ => null,
        };

        private static Spot TypeChartModulus(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x195AC),   // Europe
                GameLanguages.English => Ov(11, 0x195AC),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(11, 0x1CC66),
                GameLanguages.French => Ov(11, 0x196E8),
                GameLanguages.German => Ov(11, 0x195FC),
                GameLanguages.Italian => Ov(11, 0x195FC),
                GameLanguages.Spanish => Ov(11, 0x196E8),
                GameLanguages.Korean => Ov(11, 0x195FC),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x195AC),   // Europe
                GameLanguages.English => Ov(11, 0x195AC),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(11, 0x1CC66),
                GameLanguages.Japanese when revision == 5 => Ov(11, 0x1CC66),
                GameLanguages.Japanese when revision == 6 => Ov(11, 0x1CC66),
                GameLanguages.French => Ov(11, 0x196E8),
                GameLanguages.German => Ov(11, 0x195FC),
                GameLanguages.Italian => Ov(11, 0x195FC),
                GameLanguages.Spanish => Ov(11, 0x196E8),
                GameLanguages.Korean => Ov(11, 0x195FC),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(16, 0x1A75C),   // Europe
                GameLanguages.English => Ov(16, 0x1A75C),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(16, 0x1A750),
                GameLanguages.French => Ov(16, 0x1A864),
                GameLanguages.German => Ov(16, 0x1A778),
                GameLanguages.Italian => Ov(16, 0x1A778),
                GameLanguages.Spanish => Ov(16, 0x1A864),
                GameLanguages.Korean => Ov(16, 0x1A774),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x1AD5C),   // Europe
                GameLanguages.English => Ov(12, 0x1AD5C),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x1AD64),
                GameLanguages.French => Ov(12, 0x1AD5C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x1AD5C),
                GameLanguages.Italian => Ov(12, 0x1AD5C),
                GameLanguages.Spanish => Ov(12, 0x1AD5C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(12, 0x1AD60),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x1AD5C),   // Europe
                GameLanguages.English => Ov(12, 0x1AD5C),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x1AD64),
                GameLanguages.French => Ov(12, 0x1AD5C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x1AD5C),
                GameLanguages.Italian => Ov(12, 0x1AD5C),
                GameLanguages.Spanish => Ov(12, 0x1AD5C),
                GameLanguages.Korean => Ov(12, 0x1AD60),
                _ => null,
            },
            _ => null,
        };

        // pokeplatinum ov5_021F10E8.c FieldEffect_InitRenderObject and pokeheartgold overlay_01_021FD1B8.s: the literal
        // pointing at the table that maps overworld ids to model members. Measured on every Platinum and HGSS dump.
        private static Spot ObjectModelTablePointer(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(5, 0x205C8),   // Europe
                GameLanguages.English => Ov(5, 0x205C8),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(5, 0x20494),
                GameLanguages.French => Ov(5, 0x205D0),
                GameLanguages.German => Ov(5, 0x205E4),
                GameLanguages.Italian => Ov(5, 0x205D8),
                GameLanguages.Spanish => Ov(5, 0x205D0),
                GameLanguages.Korean => Ov(5, 0x205C4),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(1, 0x17B18),   // Europe
                GameLanguages.English => Ov(1, 0x17B18),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(1, 0x179A0),
                GameLanguages.French => Ov(1, 0x17B18),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(1, 0x17B18),
                GameLanguages.Italian => Ov(1, 0x17B18),
                GameLanguages.Spanish => Ov(1, 0x17B18),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(1, 0x17B18),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(1, 0x17B18),   // Europe
                GameLanguages.English => Ov(1, 0x17B18),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(1, 0x179A0),
                GameLanguages.French => Ov(1, 0x17B18),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(1, 0x17B18),
                GameLanguages.Italian => Ov(1, 0x17B18),
                GameLanguages.Spanish => Ov(1, 0x17B18),
                GameLanguages.Korean => Ov(1, 0x17B18),
                _ => null,
            },
            _ => null,
        };

        // The movs that loads that table's entry count, in the same function. Measured on every Platinum and HGSS dump.
        private static Spot ObjectModelCount(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(5, 0x205AA),   // Europe
                GameLanguages.English => Ov(5, 0x205AA),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(5, 0x20476),
                GameLanguages.French => Ov(5, 0x205B2),
                GameLanguages.German => Ov(5, 0x205C6),
                GameLanguages.Italian => Ov(5, 0x205BA),
                GameLanguages.Spanish => Ov(5, 0x205B2),
                GameLanguages.Korean => Ov(5, 0x205A6),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(1, 0x17AFA),   // Europe
                GameLanguages.English => Ov(1, 0x17AFA),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(1, 0x17982),
                GameLanguages.French => Ov(1, 0x17AFA),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(1, 0x17AFA),
                GameLanguages.Italian => Ov(1, 0x17AFA),
                GameLanguages.Spanish => Ov(1, 0x17AFA),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(1, 0x17AFA),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(1, 0x17AFA),   // Europe
                GameLanguages.English => Ov(1, 0x17AFA),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(1, 0x17982),
                GameLanguages.French => Ov(1, 0x17AFA),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(1, 0x17AFA),
                GameLanguages.Italian => Ov(1, 0x17AFA),
                GameLanguages.Spanish => Ov(1, 0x17AFA),
                GameLanguages.Korean => Ov(1, 0x17AFA),
                _ => null,
            },
            _ => null,
        };

        // pokeplatinum gObjectEventGfxOverworldAnimsInitialPosTable and pokeheartgold ov01_022073D8: the literal pointing at the
        // model offset table (id, x, y, z in fx32, ending at id 0xFFFF). Measured on every Platinum and HGSS dump.
        private static Spot ObjectModelPositionsPointer(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(5, 0x1B9AC),   // Europe
                GameLanguages.English => Ov(5, 0x1B9AC),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(5, 0x1B8C4),
                GameLanguages.French => Ov(5, 0x1B9FC),
                GameLanguages.German => Ov(5, 0x1BA10),
                GameLanguages.Italian => Ov(5, 0x1BA04),
                GameLanguages.Spanish => Ov(5, 0x1B9FC),
                GameLanguages.Korean => Ov(5, 0x1B9F0),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(1, 0x133F4),   // Europe
                GameLanguages.English => Ov(1, 0x133F4),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(1, 0x1327C),
                GameLanguages.French => Ov(1, 0x133F4),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(1, 0x133F4),
                GameLanguages.Italian => Ov(1, 0x133F4),
                GameLanguages.Spanish => Ov(1, 0x133F4),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(1, 0x133F4),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(1, 0x133F4),   // Europe
                GameLanguages.English => Ov(1, 0x133F4),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(1, 0x1327C),
                GameLanguages.French => Ov(1, 0x133F4),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(1, 0x133F4),
                GameLanguages.Italian => Ov(1, 0x133F4),
                GameLanguages.Spanish => Ov(1, 0x133F4),
                GameLanguages.Korean => Ov(1, 0x133F4),
                _ => null,
            },
            _ => null,
        };

        // pokediamond ov06_0224A06C: the literal pointing at the six signpost model members, picked by id 0x5B to 0x60
        // (ov06_0224A0F0). Measured on every Diamond and Pearl dump.
        private static Spot SignpostModelsPointer(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(6, 0x10A10),   // Europe
                GameLanguages.English => Ov(6, 0x10A10),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(6, 0x11534),
                GameLanguages.French => Ov(6, 0x109B8),
                GameLanguages.German => Ov(6, 0x109B8),
                GameLanguages.Italian => Ov(6, 0x109B8),
                GameLanguages.Spanish => Ov(6, 0x109B8),
                GameLanguages.Korean => Ov(6, 0x109BC),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(6, 0x10A1C),   // Europe
                GameLanguages.English => Ov(6, 0x10A1C),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(6, 0x11534),
                GameLanguages.French => Ov(6, 0x109C4),
                GameLanguages.German => Ov(6, 0x109C4),
                GameLanguages.Italian => Ov(6, 0x109C4),
                GameLanguages.Spanish => Ov(6, 0x109C4),
                GameLanguages.Korean => Ov(6, 0x109C8),
                _ => null,
            },
            _ => null,
        };

        // pokediamond ov06_0224A8A8: the movs r1 that names the book's model member (id 0xB7). Measured on every Diamond
        // and Pearl dump.
        private static Spot BookModelLoad(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(6, 0x1121A),   // Europe
                GameLanguages.English => Ov(6, 0x1121A),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(6, 0x11BF2),
                GameLanguages.French => Ov(6, 0x111C2),
                GameLanguages.German => Ov(6, 0x111C2),
                GameLanguages.Italian => Ov(6, 0x111C2),
                GameLanguages.Spanish => Ov(6, 0x111C2),
                GameLanguages.Korean => Ov(6, 0x111C6),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(6, 0x11226),   // Europe
                GameLanguages.English => Ov(6, 0x11226),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(6, 0x11BF2),
                GameLanguages.French => Ov(6, 0x111CE),
                GameLanguages.German => Ov(6, 0x111CE),
                GameLanguages.Italian => Ov(6, 0x111CE),
                GameLanguages.Spanish => Ov(6, 0x111CE),
                GameLanguages.Korean => Ov(6, 0x111D2),
                _ => null,
            },
            _ => null,
        };

        // pokediamond ov06_0224AA14: the movs r1 that names the door's model member (id 0xD1). Measured on every Diamond
        // and Pearl dump.
        private static Spot DoorModelLoad(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(6, 0x11386),   // Europe
                GameLanguages.English => Ov(6, 0x11386),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(6, 0x11A7E),
                GameLanguages.French => Ov(6, 0x1132E),
                GameLanguages.German => Ov(6, 0x1132E),
                GameLanguages.Italian => Ov(6, 0x1132E),
                GameLanguages.Spanish => Ov(6, 0x1132E),
                GameLanguages.Korean => Ov(6, 0x11332),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(6, 0x11392),   // Europe
                GameLanguages.English => Ov(6, 0x11392),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(6, 0x11A7E),
                GameLanguages.French => Ov(6, 0x1133A),
                GameLanguages.German => Ov(6, 0x1133A),
                GameLanguages.Italian => Ov(6, 0x1133A),
                GameLanguages.Spanish => Ov(6, 0x1133A),
                GameLanguages.Korean => Ov(6, 0x1133E),
                _ => null,
            },
            _ => null,
        };
    }
}
