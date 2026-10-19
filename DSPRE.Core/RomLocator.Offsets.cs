// Generated from what each supported dump got from RomInfo's per-language offsets and the offsets measured on it,
// before both moved here. Every game and language is written out; RomLocatorTests checks each against its dump.
using static DSPRE.RomInfo;

namespace DSPRE
{
    public static partial class RomLocator
    {
        private static Spot MapHeaders(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xEEDBC),   // Europe
                GameLanguages.English => Arm9(0xEEDBC),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xF0C28),
                GameLanguages.French => Arm9(0xEEDFC),
                GameLanguages.German => Arm9(0xEEDCC),
                GameLanguages.Italian => Arm9(0xEED70),
                GameLanguages.Spanish => Arm9(0xEEE08),
                GameLanguages.Korean => Arm9(0xEA408),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xEEDBC),   // Europe
                GameLanguages.English => Arm9(0xEEDBC),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0xF0C2C),
                GameLanguages.Japanese when revision == 5 => Arm9(0xF0C2C),
                GameLanguages.Japanese when revision == 6 => Arm9(0xF0D6C),
                GameLanguages.French => Arm9(0xEEDFC),
                GameLanguages.German => Arm9(0xEEDCC),
                GameLanguages.Italian => Arm9(0xEED70),
                GameLanguages.Spanish => Arm9(0xEEE08),
                GameLanguages.Korean => Arm9(0xEA408),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xE601C),   // Europe
                GameLanguages.English => Arm9(0xE601C),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xE56F0),
                GameLanguages.French => Arm9(0xE60A4),
                GameLanguages.German => Arm9(0xE6074),
                GameLanguages.Italian => Arm9(0xE6038),
                GameLanguages.Spanish => Arm9(0xE60B0),
                GameLanguages.Korean => Arm9(0xE6AA4),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xF6BE0),   // Europe
                GameLanguages.English => Arm9(0xF6BE0),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xF6390),
                GameLanguages.French => Arm9(0xF6BC4),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0xF6B94),
                GameLanguages.Italian => Arm9(0xF6B58),
                GameLanguages.Spanish => Arm9(0xF6BC8),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0xF728C),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xF6BE0),   // Europe
                GameLanguages.English => Arm9(0xF6BE0),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xF6390),
                GameLanguages.French => Arm9(0xF6BC4),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0xF6B94),
                GameLanguages.Italian => Arm9(0xF6B58),
                GameLanguages.Spanish => Arm9(0xF6BD0),
                GameLanguages.Korean => Arm9(0xF7284),
                _ => null,
            },
            _ => null,
        };

        private static Spot SpawnPoint(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF2B9C),   // Europe
                GameLanguages.English => Arm9(0xF2B9C),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xF4A08),
                GameLanguages.French => Arm9(0xF2BDC),
                GameLanguages.German => Arm9(0xF2BAC),
                GameLanguages.Italian => Arm9(0xF2B50),
                GameLanguages.Spanish => Arm9(0xF2BE8),
                GameLanguages.Korean => Arm9(0xEE1E8),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF2B9C),   // Europe
                GameLanguages.English => Arm9(0xF2B9C),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0xF4A0C),
                GameLanguages.Japanese when revision == 5 => Arm9(0xF4A0C),
                GameLanguages.Japanese when revision == 6 => Arm9(0xF4B4C),
                GameLanguages.French => Arm9(0xF2BDC),
                GameLanguages.German => Arm9(0xF2BAC),
                GameLanguages.Italian => Arm9(0xF2B50),
                GameLanguages.Spanish => Arm9(0xF2BE8),
                GameLanguages.Korean => Arm9(0xEE1E8),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xEA12C),   // Europe
                GameLanguages.English => Arm9(0xEA12C),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xE9800),
                GameLanguages.French => Arm9(0xEA1B4),
                GameLanguages.German => Arm9(0xEA184),
                GameLanguages.Italian => Arm9(0xEA148),
                GameLanguages.Spanish => Arm9(0xEA1C0),
                GameLanguages.Korean => Arm9(0xEABB4),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xFA17C),   // Europe
                GameLanguages.English => Arm9(0xFA17C),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xF992C),
                GameLanguages.French => Arm9(0xFA160),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0xFA130),
                GameLanguages.Italian => Arm9(0xFA0F4),
                GameLanguages.Spanish => Arm9(0xFA164),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0xFA828),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xFA17C),   // Europe
                GameLanguages.English => Arm9(0xFA17C),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xF992C),
                GameLanguages.French => Arm9(0xFA160),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0xFA130),
                GameLanguages.Italian => Arm9(0xFA0F4),
                GameLanguages.Spanish => Arm9(0xFA16C),
                GameLanguages.Korean => Arm9(0xFA820),
                _ => null,
            },
            _ => null,
        };

        private static Spot InitialMoney(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(52, 0x1E4),   // Europe
                GameLanguages.English => Ov(52, 0x1E4),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(52, 0x1F4),
                GameLanguages.French => Ov(52, 0x1E4),
                GameLanguages.German => Ov(52, 0x1E4),
                GameLanguages.Italian => Ov(52, 0x1E4),
                GameLanguages.Spanish => Ov(52, 0x1E4),
                GameLanguages.Korean => Ov(52, 0x1E4),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(52, 0x1E4),   // Europe
                GameLanguages.English => Ov(52, 0x1E4),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(52, 0x1F4),
                GameLanguages.Japanese when revision == 5 => Ov(52, 0x1F4),
                GameLanguages.Japanese when revision == 6 => Ov(52, 0x1F4),
                GameLanguages.French => Ov(52, 0x1E4),
                GameLanguages.German => Ov(52, 0x1E4),
                GameLanguages.Italian => Ov(52, 0x1E4),
                GameLanguages.Spanish => Ov(52, 0x1E4),
                GameLanguages.Korean => Ov(52, 0x1E4),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(57, 0x1EC),   // Europe
                GameLanguages.English => Ov(57, 0x1EC),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(57, 0x1EC),
                GameLanguages.French => Ov(57, 0x1EC),
                GameLanguages.German => Ov(57, 0x1EC),
                GameLanguages.Italian => Ov(57, 0x1EC),
                GameLanguages.Spanish => Ov(57, 0x1EC),
                GameLanguages.Korean => Ov(57, 0x1EC),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(36, 0x2FC),   // Europe
                GameLanguages.English => Ov(36, 0x2FC),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(36, 0x2FC),
                GameLanguages.French => Ov(36, 0x2FC),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(36, 0x2FC),
                GameLanguages.Italian => Ov(36, 0x2FC),
                GameLanguages.Spanish => Ov(36, 0x2FC),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(36, 0x2F8),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(36, 0x2FC),   // Europe
                GameLanguages.English => Ov(36, 0x2FC),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(36, 0x2FC),
                GameLanguages.French => Ov(36, 0x2FC),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(36, 0x2FC),
                GameLanguages.Italian => Ov(36, 0x2FC),
                GameLanguages.Spanish => Ov(36, 0x2FC),
                GameLanguages.Korean => Ov(36, 0x2F8),
                _ => null,
            },
            _ => null,
        };

        private static Spot FlyTable(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF2224),   // Europe
                GameLanguages.English => Arm9(0xF2224),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xF4090),
                GameLanguages.French => Arm9(0xF2264),
                GameLanguages.German => Arm9(0xF2234),
                GameLanguages.Italian => Arm9(0xF21D8),
                GameLanguages.Spanish => Arm9(0xF2270),
                GameLanguages.Korean => Arm9(0xED870),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF2224),   // Europe
                GameLanguages.English => Arm9(0xF2224),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0xF4094),
                GameLanguages.Japanese when revision == 5 => Arm9(0xF4094),
                GameLanguages.Japanese when revision == 6 => Arm9(0xF41D4),
                GameLanguages.French => Arm9(0xF2264),
                GameLanguages.German => Arm9(0xF2234),
                GameLanguages.Italian => Arm9(0xF21D8),
                GameLanguages.Spanish => Arm9(0xF2270),
                GameLanguages.Korean => Arm9(0xED870),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xE97B4),   // Europe
                GameLanguages.English => Arm9(0xE97B4),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xE8E88),
                GameLanguages.French => Arm9(0xE983C),
                GameLanguages.German => Arm9(0xE980C),
                GameLanguages.Italian => Arm9(0xE97D0),
                GameLanguages.Spanish => Arm9(0xE9848),
                GameLanguages.Korean => Arm9(0xEA23C),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xF9E80),   // Europe
                GameLanguages.English => Arm9(0xF9E80),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xF9630),
                GameLanguages.French => Arm9(0xF9E64),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0xF9E34),
                GameLanguages.Italian => Arm9(0xF9DF8),
                GameLanguages.Spanish => Arm9(0xF9E68),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0xFA52C),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xF9E80),   // Europe
                GameLanguages.English => Arm9(0xF9E80),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xF9630),
                GameLanguages.French => Arm9(0xF9E64),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0xF9E34),
                GameLanguages.Italian => Arm9(0xF9DF8),
                GameLanguages.Spanish => Arm9(0xF9E70),
                GameLanguages.Korean => Arm9(0xFA524),
                _ => null,
            },
            _ => null,
        };

        private static Spot TrainerClassGenders(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF8010),   // Europe
                GameLanguages.English => Arm9(0xF8010),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xF9E3C),
                GameLanguages.French => Arm9(0xF8054),
                GameLanguages.German => Arm9(0xF8024),
                GameLanguages.Italian => Arm9(0xF7FC8),
                GameLanguages.Spanish => Arm9(0xF8060),
                GameLanguages.Korean => Arm9(0xF3660),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF8010),   // Europe
                GameLanguages.English => Arm9(0xF8010),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0xF9E40),
                GameLanguages.Japanese when revision == 5 => Arm9(0xF9E40),
                GameLanguages.Japanese when revision == 6 => Arm9(0xF9F80),
                GameLanguages.French => Arm9(0xF8054),
                GameLanguages.German => Arm9(0xF8024),
                GameLanguages.Italian => Arm9(0xF7FC8),
                GameLanguages.Spanish => Arm9(0xF8060),
                GameLanguages.Korean => Arm9(0xF3660),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xF0714),   // Europe
                GameLanguages.English => Arm9(0xF0714),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xEFDA4),
                GameLanguages.French => Arm9(0xF079C),
                GameLanguages.German => Arm9(0xF076C),
                GameLanguages.Italian => Arm9(0xF0730),
                GameLanguages.Spanish => Arm9(0xF07A8),
                GameLanguages.Korean => Arm9(0xF119C),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xFFB90),   // Europe
                GameLanguages.English => Arm9(0xFFB90),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFF310),
                GameLanguages.French => Arm9(0xFFB74),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0xFFB44),
                GameLanguages.Italian => Arm9(0xFFB08),
                GameLanguages.Spanish => Arm9(0xFFB78),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x10023C),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xFFB90),   // Europe
                GameLanguages.English => Arm9(0xFFB90),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFF310),
                GameLanguages.French => Arm9(0xFFB74),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0xFFB44),
                GameLanguages.Italian => Arm9(0xFFB08),
                GameLanguages.Spanish => Arm9(0xFFB80),
                GameLanguages.Korean => Arm9(0x100234),
                _ => null,
            },
            _ => null,
        };

        private static Spot MachineMoves(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF84EC),   // Europe
                GameLanguages.English => Arm9(0xF84EC),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFA318),
                GameLanguages.French => Arm9(0xF8530),
                GameLanguages.German => Arm9(0xF8500),
                GameLanguages.Italian => Arm9(0xF84A4),
                GameLanguages.Spanish => Arm9(0xF853C),
                GameLanguages.Korean => Arm9(0xF3B3C),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF84EC),   // Europe
                GameLanguages.English => Arm9(0xF84EC),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0xFA31C),
                GameLanguages.Japanese when revision == 5 => Arm9(0xFA31C),
                GameLanguages.Japanese when revision == 6 => Arm9(0xFA45C),
                GameLanguages.French => Arm9(0xF8530),
                GameLanguages.German => Arm9(0xF8500),
                GameLanguages.Italian => Arm9(0xF84A4),
                GameLanguages.Spanish => Arm9(0xF853C),
                GameLanguages.Korean => Arm9(0xF3B3C),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xF0BFC),   // Europe
                GameLanguages.English => Arm9(0xF0BFC),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xF028C),
                GameLanguages.French => Arm9(0xF0C84),
                GameLanguages.German => Arm9(0xF0C54),
                GameLanguages.Italian => Arm9(0xF0C18),
                GameLanguages.Spanish => Arm9(0xF0C90),
                GameLanguages.Korean => Arm9(0xF1684),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x1000CC),   // Europe
                GameLanguages.English => Arm9(0x1000CC),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFF84C),
                GameLanguages.French => Arm9(0x1000B0),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x100080),
                GameLanguages.Italian => Arm9(0x100044),
                GameLanguages.Spanish => Arm9(0x1000B4),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x100778),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x1000CC),   // Europe
                GameLanguages.English => Arm9(0x1000CC),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFF84C),
                GameLanguages.French => Arm9(0x1000B0),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x100080),
                GameLanguages.Italian => Arm9(0x100044),
                GameLanguages.Spanish => Arm9(0x1000BC),
                GameLanguages.Korean => Arm9(0x100770),
                _ => null,
            },
            _ => null,
        };

        private static Spot EggMoves(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(5, 0x20668),   // Europe
                GameLanguages.English => Ov(5, 0x20668),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(5, 0x21648),
                GameLanguages.French => Ov(5, 0x20620),
                GameLanguages.German => Ov(5, 0x20620),
                GameLanguages.Italian => Ov(5, 0x20620),
                GameLanguages.Spanish => Ov(5, 0x20620),
                GameLanguages.Korean => Ov(5, 0x2061C),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(5, 0x20668),   // Europe
                GameLanguages.English => Ov(5, 0x20668),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(5, 0x21648),
                GameLanguages.Japanese when revision == 5 => Ov(5, 0x21648),
                GameLanguages.Japanese when revision == 6 => Ov(5, 0x21654),
                GameLanguages.French => Ov(5, 0x20620),
                GameLanguages.German => Ov(5, 0x20620),
                GameLanguages.Italian => Ov(5, 0x20620),
                GameLanguages.Spanish => Ov(5, 0x20620),
                GameLanguages.Korean => Ov(5, 0x2061C),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(5, 0x29222),   // Europe
                GameLanguages.English => Ov(5, 0x29222),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(5, 0x29012),
                GameLanguages.French => Ov(5, 0x2922A),
                GameLanguages.German => Ov(5, 0x2923E),
                GameLanguages.Italian => Ov(5, 0x29232),
                GameLanguages.Spanish => Ov(5, 0x2922A),
                GameLanguages.Korean => Ov(5, 0x2921A),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            _ => null,
        };

        private static Spot TrainerNameLength(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x6AC32),   // Europe
                GameLanguages.English => Arm9(0x6AC32),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x6F5F8),
                GameLanguages.French => Arm9(0x6AC8E),
                GameLanguages.German => Arm9(0x6AC8E),
                GameLanguages.Italian => Arm9(0x6AC6E),
                GameLanguages.Spanish => Arm9(0x6AC8E),
                GameLanguages.Korean => Arm9(0x6B0FC),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x6AC32),   // Europe
                GameLanguages.English => Arm9(0x6AC32),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0x6F5F8),
                GameLanguages.Japanese when revision == 5 => Arm9(0x6F5F8),
                GameLanguages.Japanese when revision == 6 => Arm9(0x6F6F8),
                GameLanguages.French => Arm9(0x6AC8E),
                GameLanguages.German => Arm9(0x6AC8E),
                GameLanguages.Italian => Arm9(0x6AC6E),
                GameLanguages.Spanish => Arm9(0x6AC8E),
                GameLanguages.Korean => Arm9(0x6B0FC),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x791DE),   // Europe
                GameLanguages.English => Arm9(0x791DE),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x78AB6),
                GameLanguages.French => Arm9(0x7927E),
                GameLanguages.German => Arm9(0x7927E),
                GameLanguages.Italian => Arm9(0x7927E),
                GameLanguages.Spanish => Arm9(0x7927E),
                GameLanguages.Korean => Arm9(0x796D0),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x7342E),   // Europe
                GameLanguages.English => Arm9(0x7342E),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x72EC2),
                GameLanguages.French => Arm9(0x7342E),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x7342E),
                GameLanguages.Italian => Arm9(0x7342E),
                GameLanguages.Spanish => Arm9(0x73426),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x7352A),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x7342E),   // Europe
                GameLanguages.English => Arm9(0x7342E),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x72EC2),
                GameLanguages.French => Arm9(0x7342E),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x7342E),
                GameLanguages.Italian => Arm9(0x7342E),
                GameLanguages.Spanish => Arm9(0x7342E),
                GameLanguages.Korean => Arm9(0x73522),
                _ => null,
            },
            _ => null,
        };

        private static Spot ItemTable(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF85B4),   // Europe
                GameLanguages.English => Arm9(0xF85B4),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFA3E0),
                GameLanguages.French => Arm9(0xF85F8),
                GameLanguages.German => Arm9(0xF85C8),
                GameLanguages.Italian => Arm9(0xF856C),
                GameLanguages.Spanish => Arm9(0xF8604),
                GameLanguages.Korean => Arm9(0xF3C04),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF85B4),   // Europe
                GameLanguages.English => Arm9(0xF85B4),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0xFA3E4),
                GameLanguages.Japanese when revision == 5 => Arm9(0xFA3E4),
                GameLanguages.Japanese when revision == 6 => Arm9(0xFA524),
                GameLanguages.French => Arm9(0xF85F8),
                GameLanguages.German => Arm9(0xF85C8),
                GameLanguages.Italian => Arm9(0xF856C),
                GameLanguages.Spanish => Arm9(0xF8604),
                GameLanguages.Korean => Arm9(0xF3C04),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xF0CC4),   // Europe
                GameLanguages.English => Arm9(0xF0CC4),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xF0354),
                GameLanguages.French => Arm9(0xF0D4C),
                GameLanguages.German => Arm9(0xF0D1C),
                GameLanguages.Italian => Arm9(0xF0CE0),
                GameLanguages.Spanish => Arm9(0xF0D58),
                GameLanguages.Korean => Arm9(0xF174C),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x100194),   // Europe
                GameLanguages.English => Arm9(0x100194),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFF914),
                GameLanguages.French => Arm9(0x100178),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x100148),
                GameLanguages.Italian => Arm9(0x10010C),
                GameLanguages.Spanish => Arm9(0x10017C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x100840),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x100194),   // Europe
                GameLanguages.English => Arm9(0x100194),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFF914),
                GameLanguages.French => Arm9(0x100178),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x100148),
                GameLanguages.Italian => Arm9(0x10010C),
                GameLanguages.Spanish => Arm9(0x100184),
                GameLanguages.Korean => Arm9(0x100838),
                _ => null,
            },
            _ => null,
        };

        private static Spot ItemLimit(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x6E710),   // Europe
                GameLanguages.English => Arm9(0x6E710),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x731DE),
                GameLanguages.French => Arm9(0x6E76C),
                GameLanguages.German => Arm9(0x6E76C),
                GameLanguages.Italian => Arm9(0x6E74C),
                GameLanguages.Spanish => Arm9(0x6E76C),
                GameLanguages.Korean => Arm9(0x6EBD4),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x6E710),   // Europe
                GameLanguages.English => Arm9(0x6E710),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0x731DE),
                GameLanguages.Japanese when revision == 5 => Arm9(0x731DE),
                GameLanguages.Japanese when revision == 6 => Arm9(0x732DE),
                GameLanguages.French => Arm9(0x6E76C),
                GameLanguages.German => Arm9(0x6E76C),
                GameLanguages.Italian => Arm9(0x6E74C),
                GameLanguages.Spanish => Arm9(0x6E76C),
                GameLanguages.Korean => Arm9(0x6EBD4),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x7CF48),   // Europe
                GameLanguages.English => Arm9(0x7CF48),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x7C818),
                GameLanguages.French => Arm9(0x7CFE8),
                GameLanguages.German => Arm9(0x7CFE8),
                GameLanguages.Italian => Arm9(0x7CFE8),
                GameLanguages.Spanish => Arm9(0x7CFE8),
                GameLanguages.Korean => Arm9(0x7D434),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x77CE8),   // Europe
                GameLanguages.English => Arm9(0x77CE8),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x77778),
                GameLanguages.French => Arm9(0x77CE8),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x77CE8),
                GameLanguages.Italian => Arm9(0x77CE8),
                GameLanguages.Spanish => Arm9(0x77CE0),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x77DE8),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x77CE8),   // Europe
                GameLanguages.English => Arm9(0x77CE8),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x77778),
                GameLanguages.French => Arm9(0x77CE8),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x77CE8),
                GameLanguages.Italian => Arm9(0x77CE8),
                GameLanguages.Spanish => Arm9(0x77CE8),
                GameLanguages.Korean => Arm9(0x77DE0),
                _ => null,
            },
            _ => null,
        };

        private static Spot PickupDivisor(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0xBCB2),   // Europe
                GameLanguages.English => Ov(11, 0xBCB2),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(11, 0x92FA),
                GameLanguages.French => Ov(11, 0xBDBA),
                GameLanguages.German => Ov(11, 0xBCCE),
                GameLanguages.Italian => Ov(11, 0xBCCE),
                GameLanguages.Spanish => Ov(11, 0xBDBA),
                GameLanguages.Korean => Ov(11, 0xBCCE),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0xBCB2),   // Europe
                GameLanguages.English => Ov(11, 0xBCB2),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(11, 0x92FA),
                GameLanguages.Japanese when revision == 5 => Ov(11, 0x92FA),
                GameLanguages.Japanese when revision == 6 => Ov(11, 0x92FA),
                GameLanguages.French => Ov(11, 0xBDBA),
                GameLanguages.German => Ov(11, 0xBCCE),
                GameLanguages.Italian => Ov(11, 0xBCCE),
                GameLanguages.Spanish => Ov(11, 0xBDBA),
                GameLanguages.Korean => Ov(11, 0xBCCE),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(16, 0xC62A),   // Europe
                GameLanguages.English => Ov(16, 0xC62A),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(16, 0xC622),
                GameLanguages.French => Ov(16, 0xC732),
                GameLanguages.German => Ov(16, 0xC646),
                GameLanguages.Italian => Ov(16, 0xC646),
                GameLanguages.Spanish => Ov(16, 0xC732),
                GameLanguages.Korean => Ov(16, 0xC642),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0xC852),   // Europe
                GameLanguages.English => Ov(12, 0xC852),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0xC85A),
                GameLanguages.French => Ov(12, 0xC852),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0xC852),
                GameLanguages.Italian => Ov(12, 0xC852),
                GameLanguages.Spanish => Ov(12, 0xC852),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(12, 0xC856),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0xC852),   // Europe
                GameLanguages.English => Ov(12, 0xC852),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0xC85A),
                GameLanguages.French => Ov(12, 0xC852),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0xC852),
                GameLanguages.Italian => Ov(12, 0xC852),
                GameLanguages.Spanish => Ov(12, 0xC852),
                GameLanguages.Korean => Ov(12, 0xC856),
                _ => null,
            },
            _ => null,
        };

        private static Spot PickupCommonItems(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x30764),   // Europe
                GameLanguages.English => Ov(11, 0x30764),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(11, 0x32E64),
                GameLanguages.French => Ov(11, 0x308CC),
                GameLanguages.German => Ov(11, 0x307E0),
                GameLanguages.Italian => Ov(11, 0x307E0),
                GameLanguages.Spanish => Ov(11, 0x308CC),
                GameLanguages.Korean => Ov(11, 0x30758),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x30764),   // Europe
                GameLanguages.English => Ov(11, 0x30764),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(11, 0x32E64),
                GameLanguages.Japanese when revision == 5 => Ov(11, 0x32E64),
                GameLanguages.Japanese when revision == 6 => Ov(11, 0x32E64),
                GameLanguages.French => Ov(11, 0x308CC),
                GameLanguages.German => Ov(11, 0x307E0),
                GameLanguages.Italian => Ov(11, 0x307E0),
                GameLanguages.Spanish => Ov(11, 0x308CC),
                GameLanguages.Korean => Ov(11, 0x30758),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(16, 0x3352C),   // Europe
                GameLanguages.English => Ov(16, 0x3352C),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(16, 0x33520),
                GameLanguages.French => Ov(16, 0x33634),
                GameLanguages.German => Ov(16, 0x33548),
                GameLanguages.Italian => Ov(16, 0x33548),
                GameLanguages.Spanish => Ov(16, 0x33634),
                GameLanguages.Korean => Ov(16, 0x33544),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x34B44),   // Europe
                GameLanguages.English => Ov(12, 0x34B44),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x34B00),
                GameLanguages.French => Ov(12, 0x34B44),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x34B44),
                GameLanguages.Italian => Ov(12, 0x34B44),
                GameLanguages.Spanish => Ov(12, 0x34B44),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(12, 0x34B48),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x34B44),   // Europe
                GameLanguages.English => Ov(12, 0x34B44),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x34B00),
                GameLanguages.French => Ov(12, 0x34B44),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x34B44),
                GameLanguages.Italian => Ov(12, 0x34B44),
                GameLanguages.Spanish => Ov(12, 0x34B44),
                GameLanguages.Korean => Ov(12, 0x34B48),
                _ => null,
            },
            _ => null,
        };

        private static Spot PickupRareItems(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x30688),   // Europe
                GameLanguages.English => Ov(11, 0x30688),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(11, 0x32D9C),
                GameLanguages.French => Ov(11, 0x307F0),
                GameLanguages.German => Ov(11, 0x30704),
                GameLanguages.Italian => Ov(11, 0x30704),
                GameLanguages.Spanish => Ov(11, 0x307F0),
                GameLanguages.Korean => Ov(11, 0x3067C),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x30688),   // Europe
                GameLanguages.English => Ov(11, 0x30688),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(11, 0x32D9C),
                GameLanguages.Japanese when revision == 5 => Ov(11, 0x32D9C),
                GameLanguages.Japanese when revision == 6 => Ov(11, 0x32D9C),
                GameLanguages.French => Ov(11, 0x307F0),
                GameLanguages.German => Ov(11, 0x30704),
                GameLanguages.Italian => Ov(11, 0x30704),
                GameLanguages.Spanish => Ov(11, 0x307F0),
                GameLanguages.Korean => Ov(11, 0x3067C),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(16, 0x33450),   // Europe
                GameLanguages.English => Ov(16, 0x33450),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(16, 0x33444),
                GameLanguages.French => Ov(16, 0x33558),
                GameLanguages.German => Ov(16, 0x3346C),
                GameLanguages.Italian => Ov(16, 0x3346C),
                GameLanguages.Spanish => Ov(16, 0x33558),
                GameLanguages.Korean => Ov(16, 0x33468),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x34A4C),   // Europe
                GameLanguages.English => Ov(12, 0x34A4C),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x34A08),
                GameLanguages.French => Ov(12, 0x34A4C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x34A4C),
                GameLanguages.Italian => Ov(12, 0x34A4C),
                GameLanguages.Spanish => Ov(12, 0x34A4C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(12, 0x34A50),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x34A4C),   // Europe
                GameLanguages.English => Ov(12, 0x34A4C),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x34A08),
                GameLanguages.French => Ov(12, 0x34A4C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x34A4C),
                GameLanguages.Italian => Ov(12, 0x34A4C),
                GameLanguages.Spanish => Ov(12, 0x34A4C),
                GameLanguages.Korean => Ov(12, 0x34A50),
                _ => null,
            },
            _ => null,
        };

        private static Spot PickupWeights(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x30B90),   // Europe
                GameLanguages.English => Ov(11, 0x30B90),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(11, 0x32D68),
                GameLanguages.French => Ov(11, 0x30CF8),
                GameLanguages.German => Ov(11, 0x30C0C),
                GameLanguages.Italian => Ov(11, 0x30C0C),
                GameLanguages.Spanish => Ov(11, 0x30CF8),
                GameLanguages.Korean => Ov(11, 0x30B84),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(11, 0x30B90),   // Europe
                GameLanguages.English => Ov(11, 0x30B90),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(11, 0x32D68),
                GameLanguages.Japanese when revision == 5 => Ov(11, 0x32D68),
                GameLanguages.Japanese when revision == 6 => Ov(11, 0x32D68),
                GameLanguages.French => Ov(11, 0x30CF8),
                GameLanguages.German => Ov(11, 0x30C0C),
                GameLanguages.Italian => Ov(11, 0x30C0C),
                GameLanguages.Spanish => Ov(11, 0x30CF8),
                GameLanguages.Korean => Ov(11, 0x30B84),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(16, 0x33968),   // Europe
                GameLanguages.English => Ov(16, 0x33968),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(16, 0x3395C),
                GameLanguages.French => Ov(16, 0x33A70),
                GameLanguages.German => Ov(16, 0x33984),
                GameLanguages.Italian => Ov(16, 0x33984),
                GameLanguages.Spanish => Ov(16, 0x33A70),
                GameLanguages.Korean => Ov(16, 0x33980),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x3518C),   // Europe
                GameLanguages.English => Ov(12, 0x3518C),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x35148),
                GameLanguages.French => Ov(12, 0x3518C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x3518C),
                GameLanguages.Italian => Ov(12, 0x3518C),
                GameLanguages.Spanish => Ov(12, 0x3518C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(12, 0x35190),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(12, 0x3518C),   // Europe
                GameLanguages.English => Ov(12, 0x3518C),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(12, 0x35148),
                GameLanguages.French => Ov(12, 0x3518C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(12, 0x3518C),
                GameLanguages.Italian => Ov(12, 0x3518C),
                GameLanguages.Spanish => Ov(12, 0x3518C),
                GameLanguages.Korean => Ov(12, 0x35190),
                _ => null,
            },
            _ => null,
        };

        private static Spot MartCommonCount(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x3FD94),   // Europe
                GameLanguages.English => Arm9(0x3FD94),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x426D0),
                GameLanguages.French => Arm9(0x3FDDC),
                GameLanguages.German => Arm9(0x3FDDC),
                GameLanguages.Italian => Arm9(0x3FDDC),
                GameLanguages.Spanish => Arm9(0x3FDDC),
                GameLanguages.Korean => Arm9(0x40200),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x3FD94),   // Europe
                GameLanguages.English => Arm9(0x3FD94),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0x426D0),
                GameLanguages.Japanese when revision == 5 => Arm9(0x426D0),
                GameLanguages.Japanese when revision == 6 => Arm9(0x42774),
                GameLanguages.French => Arm9(0x3FDDC),
                GameLanguages.German => Arm9(0x3FDDC),
                GameLanguages.Italian => Arm9(0x3FDDC),
                GameLanguages.Spanish => Arm9(0x3FDDC),
                GameLanguages.Korean => Arm9(0x40200),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x46B74),   // Europe
                GameLanguages.English => Arm9(0x46B74),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x466E0),
                GameLanguages.French => Arm9(0x46C18),
                GameLanguages.German => Arm9(0x46C18),
                GameLanguages.Italian => Arm9(0x46C18),
                GameLanguages.Spanish => Arm9(0x46C18),
                GameLanguages.Korean => Arm9(0x47068),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x48100),   // Europe
                GameLanguages.English => Arm9(0x48100),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x47DB8),
                GameLanguages.French => Arm9(0x48100),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x48100),
                GameLanguages.Italian => Arm9(0x48100),
                GameLanguages.Spanish => Arm9(0x480F8),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x48398),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x48100),   // Europe
                GameLanguages.English => Arm9(0x48100),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x47DB8),
                GameLanguages.French => Arm9(0x48100),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x48100),
                GameLanguages.Italian => Arm9(0x48100),
                GameLanguages.Spanish => Arm9(0x48100),
                GameLanguages.Korean => Arm9(0x48390),
                _ => null,
            },
            _ => null,
        };

        private static Spot MartCommonPointer(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x3FDB4),   // Europe
                GameLanguages.English => Arm9(0x3FDB4),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x426F0),
                GameLanguages.French => Arm9(0x3FDFC),
                GameLanguages.German => Arm9(0x3FDFC),
                GameLanguages.Italian => Arm9(0x3FDFC),
                GameLanguages.Spanish => Arm9(0x3FDFC),
                GameLanguages.Korean => Arm9(0x40220),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x3FDB4),   // Europe
                GameLanguages.English => Arm9(0x3FDB4),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0x426F0),
                GameLanguages.Japanese when revision == 5 => Arm9(0x426F0),
                GameLanguages.Japanese when revision == 6 => Arm9(0x42794),
                GameLanguages.French => Arm9(0x3FDFC),
                GameLanguages.German => Arm9(0x3FDFC),
                GameLanguages.Italian => Arm9(0x3FDFC),
                GameLanguages.Spanish => Arm9(0x3FDFC),
                GameLanguages.Korean => Arm9(0x40220),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x46B94),   // Europe
                GameLanguages.English => Arm9(0x46B94),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x46700),
                GameLanguages.French => Arm9(0x46C38),
                GameLanguages.German => Arm9(0x46C38),
                GameLanguages.Italian => Arm9(0x46C38),
                GameLanguages.Spanish => Arm9(0x46C38),
                GameLanguages.Korean => Arm9(0x47088),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x48124),   // Europe
                GameLanguages.English => Arm9(0x48124),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x47DDC),
                GameLanguages.French => Arm9(0x48124),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x48124),
                GameLanguages.Italian => Arm9(0x48124),
                GameLanguages.Spanish => Arm9(0x4811C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x483BC),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x48124),   // Europe
                GameLanguages.English => Arm9(0x48124),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x47DDC),
                GameLanguages.French => Arm9(0x48124),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x48124),
                GameLanguages.Italian => Arm9(0x48124),
                GameLanguages.Spanish => Arm9(0x48124),
                GameLanguages.Korean => Arm9(0x483B4),
                _ => null,
            },
            _ => null,
        };

        private static Spot MartSpecialtyPointer(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x3FE04),   // Europe
                GameLanguages.English => Arm9(0x3FE04),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x4262C),
                GameLanguages.French => Arm9(0x3FE4C),
                GameLanguages.German => Arm9(0x3FE4C),
                GameLanguages.Italian => Arm9(0x3FE4C),
                GameLanguages.Spanish => Arm9(0x3FE4C),
                GameLanguages.Korean => Arm9(0x40270),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x3FE04),   // Europe
                GameLanguages.English => Arm9(0x3FE04),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0x4262C),
                GameLanguages.Japanese when revision == 5 => Arm9(0x4262C),
                GameLanguages.Japanese when revision == 6 => Arm9(0x426D0),
                GameLanguages.French => Arm9(0x3FE4C),
                GameLanguages.German => Arm9(0x3FE4C),
                GameLanguages.Italian => Arm9(0x3FE4C),
                GameLanguages.Spanish => Arm9(0x3FE4C),
                GameLanguages.Korean => Arm9(0x40270),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x46BF0),   // Europe
                GameLanguages.English => Arm9(0x46BF0),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x4675C),
                GameLanguages.French => Arm9(0x46C94),
                GameLanguages.German => Arm9(0x46C94),
                GameLanguages.Italian => Arm9(0x46C94),
                GameLanguages.Spanish => Arm9(0x46C94),
                GameLanguages.Korean => Arm9(0x470E4),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x48190),   // Europe
                GameLanguages.English => Arm9(0x48190),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x47E48),
                GameLanguages.French => Arm9(0x48190),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x48190),
                GameLanguages.Italian => Arm9(0x48190),
                GameLanguages.Spanish => Arm9(0x48188),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x48428),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x48190),   // Europe
                GameLanguages.English => Arm9(0x48190),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x47E48),
                GameLanguages.French => Arm9(0x48190),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x48190),
                GameLanguages.Italian => Arm9(0x48190),
                GameLanguages.Spanish => Arm9(0x48190),
                GameLanguages.Korean => Arm9(0x48420),
                _ => null,
            },
            _ => null,
        };

        private static Spot HiddenItems(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF2DB4),   // Europe
                GameLanguages.English => Arm9(0xF2DB4),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xF4C10),
                GameLanguages.French => Arm9(0xF2DF4),
                GameLanguages.German => Arm9(0xF2DC4),
                GameLanguages.Italian => Arm9(0xF2D68),
                GameLanguages.Spanish => Arm9(0xF2E00),
                GameLanguages.Korean => Arm9(0xEE400),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xF2DB4),   // Europe
                GameLanguages.English => Arm9(0xF2DB4),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0xF4C14),
                GameLanguages.Japanese when revision == 5 => Arm9(0xF4C14),
                GameLanguages.Japanese when revision == 6 => Arm9(0xF4D54),
                GameLanguages.French => Arm9(0xF2DF4),
                GameLanguages.German => Arm9(0xF2DC4),
                GameLanguages.Italian => Arm9(0xF2D68),
                GameLanguages.Spanish => Arm9(0xF2E00),
                GameLanguages.Korean => Arm9(0xEE400),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xEA378),   // Europe
                GameLanguages.English => Arm9(0xEA378),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xE9A4C),
                GameLanguages.French => Arm9(0xEA400),
                GameLanguages.German => Arm9(0xEA3D0),
                GameLanguages.Italian => Arm9(0xEA394),
                GameLanguages.Spanish => Arm9(0xEA40C),
                GameLanguages.Korean => Arm9(0xEAE00),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xFA558),   // Europe
                GameLanguages.English => Arm9(0xFA558),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xF9D08),
                GameLanguages.French => Arm9(0xFA53C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0xFA50C),
                GameLanguages.Italian => Arm9(0xFA4D0),
                GameLanguages.Spanish => Arm9(0xFA540),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0xFAC04),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0xFA558),   // Europe
                GameLanguages.English => Arm9(0xFA558),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xF9D08),
                GameLanguages.French => Arm9(0xFA53C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0xFA50C),
                GameLanguages.Italian => Arm9(0xFA4D0),
                GameLanguages.Spanish => Arm9(0xFA548),
                GameLanguages.Korean => Arm9(0xFABFC),
                _ => null,
            },
            _ => null,
        };

        private static Spot HiddenItemCounts(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x396CA, 0x396CE, 0x39722, 0x39726),   // Europe
                GameLanguages.English => Arm9(0x396CA, 0x396CE, 0x39722, 0x39726),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x3B1C0, 0x3B1C4, 0x3B20E, 0x3B212),
                GameLanguages.French => Arm9(0x39712, 0x39716, 0x3976A, 0x3976E),
                GameLanguages.German => Arm9(0x39712, 0x39716, 0x3976A, 0x3976E),
                GameLanguages.Italian => Arm9(0x39712, 0x39716, 0x3976A, 0x3976E),
                GameLanguages.Spanish => Arm9(0x39712, 0x39716, 0x3976A, 0x3976E),
                GameLanguages.Korean => Arm9(0x39B3A, 0x39B3E, 0x39B92, 0x39B96),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x396CA, 0x396CE, 0x39722, 0x39726),   // Europe
                GameLanguages.English => Arm9(0x396CA, 0x396CE, 0x39722, 0x39726),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0x3B1C0, 0x3B1C4, 0x3B20E, 0x3B212),
                GameLanguages.Japanese when revision == 5 => Arm9(0x3B1C0, 0x3B1C4, 0x3B20E, 0x3B212),
                GameLanguages.Japanese when revision == 6 => Arm9(0x3B254, 0x3B258, 0x3B2A2, 0x3B2A6),
                GameLanguages.French => Arm9(0x39712, 0x39716, 0x3976A, 0x3976E),
                GameLanguages.German => Arm9(0x39712, 0x39716, 0x3976A, 0x3976E),
                GameLanguages.Italian => Arm9(0x39712, 0x39716, 0x3976A, 0x3976E),
                GameLanguages.Spanish => Arm9(0x39712, 0x39716, 0x3976A, 0x3976E),
                GameLanguages.Korean => Arm9(0x39B3A, 0x39B3E, 0x39B92, 0x39B96),
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x4058E, 0x40592, 0x405E4, 0x405E8),   // Europe
                GameLanguages.English => Arm9(0x4058E, 0x40592, 0x405E4, 0x405E8),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x402DA, 0x402DE, 0x40330, 0x40334),
                GameLanguages.French => Arm9(0x4058E, 0x40592, 0x405E4, 0x405E8),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x4058E, 0x40592, 0x405E4, 0x405E8),
                GameLanguages.Italian => Arm9(0x4058E, 0x40592, 0x405E4, 0x405E8),
                GameLanguages.Spanish => Arm9(0x40586, 0x4058A, 0x405DC, 0x405E0),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x40826, 0x4082A, 0x4087C, 0x40880),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x4058E, 0x40592, 0x405E4, 0x405E8),   // Europe
                GameLanguages.English => Arm9(0x4058E, 0x40592, 0x405E4, 0x405E8),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x402DA, 0x402DE, 0x40330, 0x40334),
                GameLanguages.French => Arm9(0x4058E, 0x40592, 0x405E4, 0x405E8),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x4058E, 0x40592, 0x405E4, 0x405E8),
                GameLanguages.Italian => Arm9(0x4058E, 0x40592, 0x405E4, 0x405E8),
                GameLanguages.Spanish => Arm9(0x4058E, 0x40592, 0x405E4, 0x405E8),
                GameLanguages.Korean => Arm9(0x4081E, 0x40822, 0x40874, 0x40878),
                _ => null,
            },
            _ => null,
        };

        private static Spot HiddenItemCountLiterals(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x3F400, 0x3F474),   // Europe
                GameLanguages.English => Arm9(0x3F400, 0x3F474),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x3EFBC, 0x3F030),
                GameLanguages.French => Arm9(0x3F4A4, 0x3F518),
                GameLanguages.German => Arm9(0x3F4A4, 0x3F518),
                GameLanguages.Italian => Arm9(0x3F4A4, 0x3F518),
                GameLanguages.Spanish => Arm9(0x3F4A4, 0x3F518),
                GameLanguages.Korean => Arm9(0x3F8F0, 0x3F964),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            _ => null,
        };

        private static Spot DistortionWorldItemScript(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(9, 0x8E2A),   // Europe
                GameLanguages.English => Ov(9, 0x8E2A),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(9, 0x8E2A),
                GameLanguages.French => Ov(9, 0x8E2A),
                GameLanguages.German => Ov(9, 0x8E2A),
                GameLanguages.Italian => Ov(9, 0x8E2A),
                GameLanguages.Spanish => Ov(9, 0x8E2A),
                GameLanguages.Korean => Ov(9, 0x8E2A),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            _ => null,
        };

        private static Spot VsSeekerRematches(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(5, 0x1F43C),   // Europe
                GameLanguages.English => Ov(5, 0x1F43C),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(5, 0x20448),
                GameLanguages.French => Ov(5, 0x1F3F4),
                GameLanguages.German => Ov(5, 0x1F3F4),
                GameLanguages.Italian => Ov(5, 0x1F3F4),
                GameLanguages.Spanish => Ov(5, 0x1F3F4),
                GameLanguages.Korean => Ov(5, 0x1F3F0),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(5, 0x1F43C),   // Europe
                GameLanguages.English => Ov(5, 0x1F43C),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(5, 0x20448),
                GameLanguages.Japanese when revision == 5 => Ov(5, 0x20448),
                GameLanguages.Japanese when revision == 6 => Ov(5, 0x20454),
                GameLanguages.French => Ov(5, 0x1F3F4),
                GameLanguages.German => Ov(5, 0x1F3F4),
                GameLanguages.Italian => Ov(5, 0x1F3F4),
                GameLanguages.Spanish => Ov(5, 0x1F3F4),
                GameLanguages.Korean => Ov(5, 0x1F3F0),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(5, 0x280C8),   // Europe
                GameLanguages.English => Ov(5, 0x280C8),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(5, 0x27EC8),
                GameLanguages.French => Ov(5, 0x280D0),
                GameLanguages.German => Ov(5, 0x280E4),
                GameLanguages.Italian => Ov(5, 0x280D8),
                GameLanguages.Spanish => Ov(5, 0x280D0),
                GameLanguages.Korean => Ov(5, 0x280C0),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            _ => null,
        };

        private static Spot VsSeekerRematchPointers(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(5, 0xA3D8, 0xA420, 0xA47C, 0xA494),   // Europe
                GameLanguages.English => Ov(5, 0xA3D8, 0xA420, 0xA47C, 0xA494),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(5, 0xA5E4, 0xA618, 0xA698, 0xA6BC),
                GameLanguages.French => Ov(5, 0xA3D8, 0xA420, 0xA47C, 0xA494),
                GameLanguages.German => Ov(5, 0xA3D8, 0xA420, 0xA47C, 0xA494),
                GameLanguages.Italian => Ov(5, 0xA3D8, 0xA420, 0xA47C, 0xA494),
                GameLanguages.Spanish => Ov(5, 0xA3D8, 0xA420, 0xA47C, 0xA494),
                GameLanguages.Korean => Ov(5, 0xA3D8, 0xA420, 0xA47C, 0xA494),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(5, 0xA3D8, 0xA420, 0xA47C, 0xA494),   // Europe
                GameLanguages.English => Ov(5, 0xA3D8, 0xA420, 0xA47C, 0xA494),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(5, 0xA5E4, 0xA618, 0xA698, 0xA6BC),
                GameLanguages.Japanese when revision == 5 => Ov(5, 0xA5E4, 0xA618, 0xA698, 0xA6BC),
                GameLanguages.Japanese when revision == 6 => Ov(5, 0xA5F0, 0xA624, 0xA6A4, 0xA6C8),
                GameLanguages.French => Ov(5, 0xA3D8, 0xA420, 0xA47C, 0xA494),
                GameLanguages.German => Ov(5, 0xA3D8, 0xA420, 0xA47C, 0xA494),
                GameLanguages.Italian => Ov(5, 0xA3D8, 0xA420, 0xA47C, 0xA494),
                GameLanguages.Spanish => Ov(5, 0xA3D8, 0xA420, 0xA47C, 0xA494),
                GameLanguages.Korean => Ov(5, 0xA3D8, 0xA420, 0xA47C, 0xA494),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(5, 0xB078, 0xB0C0, 0xB11C, 0xB134),   // Europe
                GameLanguages.English => Ov(5, 0xB078, 0xB0C0, 0xB11C, 0xB134),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(5, 0xB078, 0xB0C0, 0xB11C, 0xB134),
                GameLanguages.French => Ov(5, 0xB078, 0xB0C0, 0xB11C, 0xB134),
                GameLanguages.German => Ov(5, 0xB078, 0xB0C0, 0xB11C, 0xB134),
                GameLanguages.Italian => Ov(5, 0xB078, 0xB0C0, 0xB11C, 0xB134),
                GameLanguages.Spanish => Ov(5, 0xB078, 0xB0C0, 0xB11C, 0xB134),
                GameLanguages.Korean => Ov(5, 0xB078, 0xB0C0, 0xB11C, 0xB134),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            _ => null,
        };

        private static Spot VsSeekerRematchRowCompare(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(5, 0xA3D0),   // Europe
                GameLanguages.English => Ov(5, 0xA3D0),   // US, and any English revision not listed
                GameLanguages.French => Ov(5, 0xA3D0),
                GameLanguages.German => Ov(5, 0xA3D0),
                GameLanguages.Italian => Ov(5, 0xA3D0),
                GameLanguages.Spanish => Ov(5, 0xA3D0),
                GameLanguages.Korean => Ov(5, 0xA3D0),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(5, 0xA3D0),   // Europe
                GameLanguages.English => Ov(5, 0xA3D0),   // US, and any English revision not listed
                GameLanguages.French => Ov(5, 0xA3D0),
                GameLanguages.German => Ov(5, 0xA3D0),
                GameLanguages.Italian => Ov(5, 0xA3D0),
                GameLanguages.Spanish => Ov(5, 0xA3D0),
                GameLanguages.Korean => Ov(5, 0xA3D0),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(5, 0xB070),   // Europe
                GameLanguages.English => Ov(5, 0xB070),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(5, 0xB070),
                GameLanguages.French => Ov(5, 0xB070),
                GameLanguages.German => Ov(5, 0xB070),
                GameLanguages.Italian => Ov(5, 0xB070),
                GameLanguages.Spanish => Ov(5, 0xB070),
                GameLanguages.Korean => Ov(5, 0xB070),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            _ => null,
        };

        private static Spot CameraTablePointers(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(5, 0x4908),   // Europe
                GameLanguages.English => Ov(5, 0x4908),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(5, 0x4C48),
                GameLanguages.French => Ov(5, 0x4908),
                GameLanguages.German => Ov(5, 0x4908),
                GameLanguages.Italian => Ov(5, 0x4908),
                GameLanguages.Spanish => Ov(5, 0x4908),
                GameLanguages.Korean => Ov(5, 0x4908),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(5, 0x4908),   // Europe
                GameLanguages.English => Ov(5, 0x4908),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(5, 0x4C48),
                GameLanguages.Japanese when revision == 5 => Ov(5, 0x4C48),
                GameLanguages.Japanese when revision == 6 => Ov(5, 0x4C54),
                GameLanguages.French => Ov(5, 0x4908),
                GameLanguages.German => Ov(5, 0x4908),
                GameLanguages.Italian => Ov(5, 0x4908),
                GameLanguages.Spanish => Ov(5, 0x4908),
                GameLanguages.Korean => Ov(5, 0x4908),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(5, 0x4E24),   // Europe
                GameLanguages.English => Ov(5, 0x4E24),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(5, 0x4E24),
                GameLanguages.French => Ov(5, 0x4E24),
                GameLanguages.German => Ov(5, 0x4E24),
                GameLanguages.Italian => Ov(5, 0x4E24),
                GameLanguages.Spanish => Ov(5, 0x4E24),
                GameLanguages.Korean => Ov(5, 0x4E24),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(1, 0x532C, 0x547C),   // Europe
                GameLanguages.English => Ov(1, 0x532C, 0x547C),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(1, 0x5324, 0x5474),
                GameLanguages.French => Ov(1, 0x532C, 0x547C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(1, 0x532C, 0x547C),
                GameLanguages.Italian => Ov(1, 0x532C, 0x547C),
                GameLanguages.Spanish => Ov(1, 0x532C, 0x547C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(1, 0x532C, 0x547C),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(1, 0x532C, 0x547C),   // Europe
                GameLanguages.English => Ov(1, 0x532C, 0x547C),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(1, 0x5324, 0x5474),
                GameLanguages.French => Ov(1, 0x532C, 0x547C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(1, 0x532C, 0x547C),
                GameLanguages.Italian => Ov(1, 0x532C, 0x547C),
                GameLanguages.Spanish => Ov(1, 0x532C, 0x547C),
                GameLanguages.Korean => Ov(1, 0x532C, 0x547C),
                _ => null,
            },
            _ => null,
        };

        private static Spot EncounterMusicPointer(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x4AD3C),   // Europe
                GameLanguages.English => Arm9(0x4AD3C),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x4D8C0),
                GameLanguages.French => Arm9(0x4ADAC),
                GameLanguages.German => Arm9(0x4ADAC),
                GameLanguages.Italian => Arm9(0x4ADAC),
                GameLanguages.Spanish => Arm9(0x4ADAC),
                GameLanguages.Korean => Arm9(0x4B208),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x4AD3C),   // Europe
                GameLanguages.English => Arm9(0x4AD3C),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0x4D8C0),
                GameLanguages.Japanese when revision == 5 => Arm9(0x4D8C0),
                GameLanguages.Japanese when revision == 6 => Arm9(0x4D9AC),
                GameLanguages.French => Arm9(0x4ADAC),
                GameLanguages.German => Arm9(0x4ADAC),
                GameLanguages.Italian => Arm9(0x4ADAC),
                GameLanguages.Spanish => Arm9(0x4ADAC),
                GameLanguages.Korean => Arm9(0x4B208),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x5563C),   // Europe
                GameLanguages.English => Arm9(0x5563C),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x54F04),
                GameLanguages.French => Arm9(0x556E0),
                GameLanguages.German => Arm9(0x556E0),
                GameLanguages.Italian => Arm9(0x556E0),
                GameLanguages.Spanish => Arm9(0x556E0),
                GameLanguages.Korean => Arm9(0x55B30),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x550E0),   // Europe
                GameLanguages.English => Arm9(0x550E0),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x54B44),
                GameLanguages.French => Arm9(0x550E0),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x550E0),
                GameLanguages.Italian => Arm9(0x550E0),
                GameLanguages.Spanish => Arm9(0x550D8),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x551DC),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x550E0),   // Europe
                GameLanguages.English => Arm9(0x550E0),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x54B44),
                GameLanguages.French => Arm9(0x550E0),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x550E0),
                GameLanguages.Italian => Arm9(0x550E0),
                GameLanguages.Spanish => Arm9(0x550E0),
                GameLanguages.Korean => Arm9(0x551D4),
                _ => null,
            },
            _ => null,
        };

        private static Spot ConditionalMusicPointer(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x667D8),   // Europe
                GameLanguages.English => Arm9(0x667D8),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x66238),
                GameLanguages.French => Arm9(0x667D8),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x667D8),
                GameLanguages.Italian => Arm9(0x667D8),
                GameLanguages.Spanish => Arm9(0x667D0),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x668D4),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x667D8),   // Europe
                GameLanguages.English => Arm9(0x667D8),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x66238),
                GameLanguages.French => Arm9(0x667D8),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x667D8),
                GameLanguages.Italian => Arm9(0x667D8),
                GameLanguages.Spanish => Arm9(0x667D8),
                GameLanguages.Korean => Arm9(0x668CC),
                _ => null,
            },
            _ => null,
        };

        private static Spot DungeonCutinPointer(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x6A384),   // Europe
                GameLanguages.English => Arm9(0x6A384),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x69DE4),
                GameLanguages.French => Arm9(0x6A384),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x6A384),
                GameLanguages.Italian => Arm9(0x6A384),
                GameLanguages.Spanish => Arm9(0x6A37C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x6A480),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x6A384),   // Europe
                GameLanguages.English => Arm9(0x6A384),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x69DE4),
                GameLanguages.French => Arm9(0x6A384),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x6A384),
                GameLanguages.Italian => Arm9(0x6A384),
                GameLanguages.Spanish => Arm9(0x6A384),
                GameLanguages.Korean => Arm9(0x6A478),
                _ => null,
            },
            _ => null,
        };

        private static Spot BattleEffectCombosPointer(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x47580),   // Europe
                GameLanguages.English => Arm9(0x47580),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x4A104),
                GameLanguages.French => Arm9(0x475F0),
                GameLanguages.German => Arm9(0x475F0),
                GameLanguages.Italian => Arm9(0x475F0),
                GameLanguages.Spanish => Arm9(0x475F0),
                GameLanguages.Korean => Arm9(0x47A4C),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x47580),   // Europe
                GameLanguages.English => Arm9(0x47580),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0x4A104),
                GameLanguages.Japanese when revision == 5 => Arm9(0x4A104),
                GameLanguages.Japanese when revision == 6 => Arm9(0x4A1BC),
                GameLanguages.French => Arm9(0x475F0),
                GameLanguages.German => Arm9(0x475F0),
                GameLanguages.Italian => Arm9(0x475F0),
                GameLanguages.Spanish => Arm9(0x475F0),
                GameLanguages.Korean => Arm9(0x47A4C),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x51BE0),   // Europe
                GameLanguages.English => Arm9(0x51BE0),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x514C0),
                GameLanguages.French => Arm9(0x51C84),
                GameLanguages.German => Arm9(0x51C84),
                GameLanguages.Italian => Arm9(0x51C84),
                GameLanguages.Spanish => Arm9(0x51C84),
                GameLanguages.Korean => Arm9(0x520D4),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x517C8),   // Europe
                GameLanguages.English => Arm9(0x517C8),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x51260),
                GameLanguages.French => Arm9(0x517C8),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x517C8),
                GameLanguages.Italian => Arm9(0x517C8),
                GameLanguages.Spanish => Arm9(0x517C0),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x518C4),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x517C8),   // Europe
                GameLanguages.English => Arm9(0x517C8),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x51260),
                GameLanguages.French => Arm9(0x517C8),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x517C8),
                GameLanguages.Italian => Arm9(0x517C8),
                GameLanguages.Spanish => Arm9(0x517C8),
                GameLanguages.Korean => Arm9(0x518BC),
                _ => null,
            },
            _ => null,
        };

        private static Spot BattleEffectCombosSecondPointer(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x4759C),   // Europe
                GameLanguages.English => Arm9(0x4759C),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x4A0D8),
                GameLanguages.French => Arm9(0x4760C),
                GameLanguages.German => Arm9(0x4760C),
                GameLanguages.Italian => Arm9(0x4760C),
                GameLanguages.Spanish => Arm9(0x4760C),
                GameLanguages.Korean => Arm9(0x47A68),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x4759C),   // Europe
                GameLanguages.English => Arm9(0x4759C),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0x4A0D8),
                GameLanguages.Japanese when revision == 5 => Arm9(0x4A0D8),
                GameLanguages.Japanese when revision == 6 => Arm9(0x4A190),
                GameLanguages.French => Arm9(0x4760C),
                GameLanguages.German => Arm9(0x4760C),
                GameLanguages.Italian => Arm9(0x4760C),
                GameLanguages.Spanish => Arm9(0x4760C),
                GameLanguages.Korean => Arm9(0x47A68),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x51BFC),   // Europe
                GameLanguages.English => Arm9(0x51BFC),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x514DC),
                GameLanguages.French => Arm9(0x51CA0),
                GameLanguages.German => Arm9(0x51CA0),
                GameLanguages.Italian => Arm9(0x51CA0),
                GameLanguages.Spanish => Arm9(0x51CA0),
                GameLanguages.Korean => Arm9(0x520F0),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            _ => null,
        };

        private static Spot VsPokemonEntriesPointer(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x518D4),   // Europe
                GameLanguages.English => Arm9(0x518D4),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x5136C),
                GameLanguages.French => Arm9(0x518D4),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x518D4),
                GameLanguages.Italian => Arm9(0x518D4),
                GameLanguages.Spanish => Arm9(0x518CC),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x519D0),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x518D4),   // Europe
                GameLanguages.English => Arm9(0x518D4),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x5136C),
                GameLanguages.French => Arm9(0x518D4),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x518D4),
                GameLanguages.Italian => Arm9(0x518D4),
                GameLanguages.Spanish => Arm9(0x518D4),
                GameLanguages.Korean => Arm9(0x519C8),
                _ => null,
            },
            _ => null,
        };

        private static Spot VsTrainerEntriesPointer(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x51890),   // Europe
                GameLanguages.English => Arm9(0x51890),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x51328),
                GameLanguages.French => Arm9(0x51890),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x51890),
                GameLanguages.Italian => Arm9(0x51890),
                GameLanguages.Spanish => Arm9(0x51888),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x5198C),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x51890),   // Europe
                GameLanguages.English => Arm9(0x51890),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x51328),
                GameLanguages.French => Arm9(0x51890),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x51890),
                GameLanguages.Italian => Arm9(0x51890),
                GameLanguages.Spanish => Arm9(0x51890),
                GameLanguages.Korean => Arm9(0x51984),
                _ => null,
            },
            _ => null,
        };

        private static Spot MonIconPalettePointer(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x6B838),   // Europe
                GameLanguages.English => Arm9(0x6B838),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x6FCEC),
                GameLanguages.French => Arm9(0x6B894),
                GameLanguages.German => Arm9(0x6B894),
                GameLanguages.Italian => Arm9(0x6B874),
                GameLanguages.Spanish => Arm9(0x6B894),
                GameLanguages.Korean => Arm9(0x6BCFC),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0x6B838),   // Europe
                GameLanguages.English => Arm9(0x6B838),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0x6FCEC),
                GameLanguages.Japanese when revision == 5 => Arm9(0x6FCEC),
                GameLanguages.Japanese when revision == 6 => Arm9(0x6FDEC),
                GameLanguages.French => Arm9(0x6B894),
                GameLanguages.German => Arm9(0x6B894),
                GameLanguages.Italian => Arm9(0x6B874),
                GameLanguages.Spanish => Arm9(0x6B894),
                GameLanguages.Korean => Arm9(0x6BCFC),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x79F80),   // Europe
                GameLanguages.English => Arm9(0x79F80),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x79858),
                GameLanguages.French => Arm9(0x7A020),
                GameLanguages.German => Arm9(0x7A020),
                GameLanguages.Italian => Arm9(0x7A020),
                GameLanguages.Spanish => Arm9(0x7A020),
                GameLanguages.Korean => Arm9(0x7A46C),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x74408),   // Europe
                GameLanguages.English => Arm9(0x74408),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x73EA0),
                GameLanguages.French => Arm9(0x74408),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x74408),
                GameLanguages.Italian => Arm9(0x74408),
                GameLanguages.Spanish => Arm9(0x74400),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x74508),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x74408),   // Europe
                GameLanguages.English => Arm9(0x74408),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x73EA0),
                GameLanguages.French => Arm9(0x74408),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x74408),
                GameLanguages.Italian => Arm9(0x74408),
                GameLanguages.Spanish => Arm9(0x74408),
                GameLanguages.Korean => Arm9(0x74500),
                _ => null,
            },
            _ => null,
        };

        private static Spot OverworldTable(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(5, 0x22BCC),   // Europe
                GameLanguages.English => Ov(5, 0x22BCC),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(5, 0x23BAC),
                GameLanguages.French => Ov(5, 0x22B84),
                GameLanguages.German => Ov(5, 0x22B84),
                GameLanguages.Italian => Ov(5, 0x22B84),
                GameLanguages.Spanish => Ov(5, 0x22B84),
                GameLanguages.Korean => Ov(5, 0x22B80),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(5, 0x22BCC),   // Europe
                GameLanguages.English => Ov(5, 0x22BCC),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(5, 0x23BAC),
                GameLanguages.Japanese when revision == 5 => Ov(5, 0x23BAC),
                GameLanguages.Japanese when revision == 6 => Ov(5, 0x23BB8),
                GameLanguages.French => Ov(5, 0x22B84),
                GameLanguages.German => Ov(5, 0x22B84),
                GameLanguages.Italian => Ov(5, 0x22B84),
                GameLanguages.Spanish => Ov(5, 0x22B84),
                GameLanguages.Korean => Ov(5, 0x22B80),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(5, 0x2BC34),   // Europe
                GameLanguages.English => Ov(5, 0x2BC34),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(5, 0x2BA24),
                GameLanguages.French => Ov(5, 0x2BC3C),
                GameLanguages.German => Ov(5, 0x2BC50),
                GameLanguages.Italian => Ov(5, 0x2BC44),
                GameLanguages.Spanish => Ov(5, 0x2BC3C),
                GameLanguages.Korean => Ov(5, 0x2BC2C),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            _ => null,
        };

        private static Spot OverworldTablePointer(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(1, 0x139FC),   // Europe
                GameLanguages.English => Ov(1, 0x139FC),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(1, 0x13884),
                GameLanguages.French => Ov(1, 0x139FC),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(1, 0x139FC),
                GameLanguages.Italian => Ov(1, 0x139FC),
                GameLanguages.Spanish => Ov(1, 0x139FC),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(1, 0x139FC),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(1, 0x139FC),   // Europe
                GameLanguages.English => Ov(1, 0x139FC),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(1, 0x13884),
                GameLanguages.French => Ov(1, 0x139FC),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(1, 0x139FC),
                GameLanguages.Italian => Ov(1, 0x139FC),
                GameLanguages.Spanish => Ov(1, 0x139FC),
                GameLanguages.Korean => Ov(1, 0x139FC),
                _ => null,
            },
            _ => null,
        };

        private static Spot PokedexForeignSpecies(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xFD120),   // Europe
                GameLanguages.English => Arm9(0xFD120),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0xFEF00),
                GameLanguages.French => Arm9(0xFD164),
                GameLanguages.German => Arm9(0xFD134),
                GameLanguages.Italian => Arm9(0xFD0D8),
                GameLanguages.Spanish => Arm9(0xFD170),
                GameLanguages.Korean => Arm9(0xF9B44),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Arm9(0xFD120),   // Europe
                GameLanguages.English => Arm9(0xFD120),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Arm9(0xFEF04),
                GameLanguages.Japanese when revision == 5 => Arm9(0xFEF04),
                GameLanguages.Japanese when revision == 6 => Arm9(0xFF044),
                GameLanguages.French => Arm9(0xFD164),
                GameLanguages.German => Arm9(0xFD134),
                GameLanguages.Italian => Arm9(0xFD0D8),
                GameLanguages.Spanish => Arm9(0xFD170),
                GameLanguages.Korean => Arm9(0xF9B44),
                _ => null,
            },
            _ => null,
        };

        private static Spot PokedexFieldZones(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(18, 0x14054),   // Europe
                GameLanguages.English => Ov(18, 0x14054),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(18, 0x13CE0),
                GameLanguages.French => Ov(18, 0x13CD8),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(18, 0x13CD8),
                GameLanguages.Italian => Ov(18, 0x13CD8),
                GameLanguages.Spanish => Ov(18, 0x13CD8),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(18, 0x13D90),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(18, 0x14050),   // Europe
                GameLanguages.English => Ov(18, 0x14050),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(18, 0x13CE0),
                GameLanguages.French => Ov(18, 0x13CD4),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(18, 0x13CD4),
                GameLanguages.Italian => Ov(18, 0x13CD4),
                GameLanguages.Spanish => Ov(18, 0x13CD4),
                GameLanguages.Korean => Ov(18, 0x13D94),
                _ => null,
            },
            _ => null,
        };

        private static Spot PokedexDungeonZones(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(18, 0x13FD8),   // Europe
                GameLanguages.English => Ov(18, 0x13FD8),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(18, 0x13C64),
                GameLanguages.French => Ov(18, 0x13C5C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(18, 0x13C5C),
                GameLanguages.Italian => Ov(18, 0x13C5C),
                GameLanguages.Spanish => Ov(18, 0x13C5C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(18, 0x13D14),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(18, 0x13FD4),   // Europe
                GameLanguages.English => Ov(18, 0x13FD4),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(18, 0x13C64),
                GameLanguages.French => Ov(18, 0x13C58),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(18, 0x13C58),
                GameLanguages.Italian => Ov(18, 0x13C58),
                GameLanguages.Spanish => Ov(18, 0x13C58),
                GameLanguages.Korean => Ov(18, 0x13D18),
                _ => null,
            },
            _ => null,
        };

        private static Spot PokedexAreaSort(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(18, 0x140E0),   // Europe
                GameLanguages.English => Ov(18, 0x140E0),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(18, 0x13D6C),
                GameLanguages.French => Ov(18, 0x13D64),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(18, 0x13D64),
                GameLanguages.Italian => Ov(18, 0x13D64),
                GameLanguages.Spanish => Ov(18, 0x13D64),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(18, 0x13E1C),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(18, 0x140DC),   // Europe
                GameLanguages.English => Ov(18, 0x140DC),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(18, 0x13D6C),
                GameLanguages.French => Ov(18, 0x13D60),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(18, 0x13D60),
                GameLanguages.Italian => Ov(18, 0x13D60),
                GameLanguages.Spanish => Ov(18, 0x13D60),
                GameLanguages.Korean => Ov(18, 0x13E20),
                _ => null,
            },
            _ => null,
        };

        /// <summary>The three starters' species: three words in the selection overlay, or the halfwords the HGSS arm9 loads them from.</summary>
        private static Spot StarterSpecies(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(64, 0x1B88),   // Europe
                GameLanguages.English => Ov(64, 0x1B88),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(64, 0x30),
                GameLanguages.French => Ov(64, 0x1B88),
                GameLanguages.German => Ov(64, 0x1B88),
                GameLanguages.Italian => Ov(64, 0x1B88),
                GameLanguages.Spanish => Ov(64, 0x1B88),
                GameLanguages.Korean => Ov(64, 0x1B88),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(64, 0x1B88),   // Europe
                GameLanguages.English => Ov(64, 0x1B88),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => Ov(64, 0x30),
                GameLanguages.Japanese when revision == 5 => Ov(64, 0x30),
                GameLanguages.Japanese when revision == 6 => Ov(64, 0x30),
                GameLanguages.French => Ov(64, 0x1B88),
                GameLanguages.German => Ov(64, 0x1B88),
                GameLanguages.Italian => Ov(64, 0x1B88),
                GameLanguages.Spanish => Ov(64, 0x1B88),
                GameLanguages.Korean => Ov(64, 0x1B88),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(78, 0x1BC0),   // Europe
                GameLanguages.English => Ov(78, 0x1BC0),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(78, 0x1BAC),
                GameLanguages.French => Ov(78, 0x1BC0),
                GameLanguages.German => Ov(78, 0x1BC0),
                GameLanguages.Italian => Ov(78, 0x1BC0),
                GameLanguages.Spanish => Ov(78, 0x1BC0),
                GameLanguages.Korean => Ov(78, 0x1BC0),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x108514),   // Europe
                GameLanguages.English => Arm9(0x108514),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x107A60),
                GameLanguages.French => Arm9(0x108538),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x1084FC),
                GameLanguages.Italian => Arm9(0x1084A8),
                GameLanguages.Spanish => Arm9(0x108540),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Arm9(0x108C90),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x108514),   // Europe
                GameLanguages.English => Arm9(0x108514),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x107A60),
                GameLanguages.French => Arm9(0x108538),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x1084FC),
                GameLanguages.Italian => Arm9(0x1084A8),
                GameLanguages.Spanish => Arm9(0x108548),
                GameLanguages.Korean => Arm9(0x108C88),
                _ => null,
            },
            _ => null,
        };

        /// <summary>The selection scene's code that picks each starter's model, which the starter editor rewrites. Japanese Diamond and Pearl load the species from pool words instead (RomInfo.starterSceneSpeciesLiterals).</summary>
        private static Spot StarterScene(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(64, 0x65C),   // Europe
                GameLanguages.English => Ov(64, 0x65C),   // US, and any English revision not listed
                GameLanguages.Japanese => null,
                GameLanguages.French => Ov(64, 0x65C),
                GameLanguages.German => Ov(64, 0x65C),
                GameLanguages.Italian => Ov(64, 0x65C),
                GameLanguages.Spanish => Ov(64, 0x65C),
                GameLanguages.Korean => Ov(64, 0x65C),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(64, 0x65C),   // Europe
                GameLanguages.English => Ov(64, 0x65C),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => null,
                GameLanguages.Japanese when revision == 5 => null,
                GameLanguages.Japanese when revision == 6 => null,
                GameLanguages.French => Ov(64, 0x65C),
                GameLanguages.German => Ov(64, 0x65C),
                GameLanguages.Italian => Ov(64, 0x65C),
                GameLanguages.Spanish => Ov(64, 0x65C),
                GameLanguages.Korean => Ov(64, 0x65C),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(78, 0x670),   // Europe
                GameLanguages.English => Ov(78, 0x670),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(78, 0x65C),
                GameLanguages.French => Ov(78, 0x670),
                GameLanguages.German => Ov(78, 0x670),
                GameLanguages.Italian => Ov(78, 0x670),
                GameLanguages.Spanish => Ov(78, 0x670),
                GameLanguages.Korean => Ov(78, 0x670),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            _ => null,
        };

        /// <summary>The second instruction in the selection scene the starter editor turns into a load.</summary>
        private static Spot StarterSceneInner(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => Ov(64, 0x714),   // Europe
                GameLanguages.English => Ov(64, 0x714),   // US, and any English revision not listed
                GameLanguages.Japanese => null,
                GameLanguages.French => Ov(64, 0x714),
                GameLanguages.German => Ov(64, 0x714),
                GameLanguages.Italian => Ov(64, 0x714),
                GameLanguages.Spanish => Ov(64, 0x714),
                GameLanguages.Korean => Ov(64, 0x714),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => Ov(64, 0x714),   // Europe
                GameLanguages.English => Ov(64, 0x714),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => null,
                GameLanguages.Japanese when revision == 5 => null,
                GameLanguages.Japanese when revision == 6 => null,
                GameLanguages.French => Ov(64, 0x714),
                GameLanguages.German => Ov(64, 0x714),
                GameLanguages.Italian => Ov(64, 0x714),
                GameLanguages.Spanish => Ov(64, 0x714),
                GameLanguages.Korean => Ov(64, 0x714),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Ov(78, 0x728),   // Europe
                GameLanguages.English => Ov(78, 0x728),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(78, 0x714),
                GameLanguages.French => Ov(78, 0x728),
                GameLanguages.German => Ov(78, 0x728),
                GameLanguages.Italian => Ov(78, 0x728),
                GameLanguages.Spanish => Ov(78, 0x728),
                GameLanguages.Korean => Ov(78, 0x728),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            _ => null,
        };

        /// <summary>The three species whose cries the HGSS starter scene plays.</summary>
        private static Spot StarterCries(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(61, 0x1A98),   // Europe
                GameLanguages.English => Ov(61, 0x1A98),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(61, 0x1A98),
                GameLanguages.French => Ov(61, 0x1A98),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(61, 0x1A98),
                GameLanguages.Italian => Ov(61, 0x1A98),
                GameLanguages.Spanish => Ov(61, 0x1A98),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(61, 0x1A98),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(61, 0x1A98),   // Europe
                GameLanguages.English => Ov(61, 0x1A98),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(61, 0x1A98),
                GameLanguages.French => Ov(61, 0x1A98),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(61, 0x1A98),
                GameLanguages.Italian => Ov(61, 0x1A98),
                GameLanguages.Spanish => Ov(61, 0x1A98),
                GameLanguages.Korean => Ov(61, 0x1A98),
                _ => null,
            },
            _ => null,
        };

        /// <summary>The Pokégear rematch table, where the game ships it; the reader still follows the overlay's own code first.</summary>
        private static Spot PokegearRematches(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(26, 0x20C),   // Europe
                GameLanguages.English => Ov(26, 0x20C),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(26, 0x20C),
                GameLanguages.French => Ov(26, 0x20C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(26, 0x20C),
                GameLanguages.Italian => Ov(26, 0x20C),
                GameLanguages.Spanish => Ov(26, 0x20C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(26, 0x20C),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(26, 0x20C),   // Europe
                GameLanguages.English => Ov(26, 0x20C),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(26, 0x20C),
                GameLanguages.French => Ov(26, 0x20C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(26, 0x20C),
                GameLanguages.Italian => Ov(26, 0x20C),
                GameLanguages.Spanish => Ov(26, 0x20C),
                GameLanguages.Korean => Ov(26, 0x20C),
                _ => null,
            },
            _ => null,
        };

        /// <summary>HGSS: the Ruins of Alph, default and Cliff Cave Rock Smash item tables, 0x10 bytes apart. SoulSilver has its own items in them.</summary>
        private static Spot RockSmashItemTables(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(1, 0x23D04),   // Europe
                GameLanguages.English => Ov(1, 0x23D04),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(1, 0x23B78),
                GameLanguages.French => Ov(1, 0x23D04),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(1, 0x23D04),
                GameLanguages.Italian => Ov(1, 0x23D04),
                GameLanguages.Spanish => Ov(1, 0x23D04),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(1, 0x23D04),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(1, 0x23D04),   // Europe
                GameLanguages.English => Ov(1, 0x23D04),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(1, 0x23B78),
                GameLanguages.French => Ov(1, 0x23D04),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(1, 0x23D04),
                GameLanguages.Italian => Ov(1, 0x23D04),
                GameLanguages.Spanish => Ov(1, 0x23D04),
                GameLanguages.Korean => Ov(1, 0x23D04),
                _ => null,
            },
            _ => null,
        };

        /// <summary>The trainer class gender lookup the trainer class metadata patch turns into a veneer; English only, where the patch is measured.</summary>
        private static Spot TrainerClassMetadataGenderHook(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x735F8),   // Europe
                GameLanguages.English => Arm9(0x735F8),   // US, and any English revision not listed
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x735F8),   // Europe
                GameLanguages.English => Arm9(0x735F8),   // US, and any English revision not listed
                _ => null,
            },
            _ => null,
        };

        /// <summary>The eye-contact music lookup the trainer class metadata patch turns into a veneer; English only, where the patch is measured.</summary>
        private static Spot TrainerClassMetadataEyeContactHook(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x55098),   // Europe
                GameLanguages.English => Arm9(0x55098),   // US, and any English revision not listed
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x55098),   // Europe
                GameLanguages.English => Arm9(0x55098),   // US, and any English revision not listed
                _ => null,
            },
            _ => null,
        };

        /// <summary>HGSS: the Battle Frontier Brains' VS records, six rows of 0x0C bytes.</summary>
        private static Spot FrontierBrainTable(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Ov(80, 0x13CB8),   // Europe
                GameLanguages.English => Ov(80, 0x13CB8),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(80, 0x13BA4),
                GameLanguages.French => Ov(80, 0x13CB8),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(80, 0x13CB8),
                GameLanguages.Italian => Ov(80, 0x13CB8),
                GameLanguages.Spanish => Ov(80, 0x13CB8),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => Ov(80, 0x13CC0),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Ov(80, 0x13CB8),   // Europe
                GameLanguages.English => Ov(80, 0x13CB8),   // US, and any English revision not listed
                GameLanguages.Japanese => Ov(80, 0x13BA4),
                GameLanguages.French => Ov(80, 0x13CB8),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Ov(80, 0x13CB8),
                GameLanguages.Italian => Ov(80, 0x13CB8),
                GameLanguages.Spanish => Ov(80, 0x13CB8),
                GameLanguages.Korean => Ov(80, 0x13CC0),
                _ => null,
            },
            _ => null,
        };

        /// <summary>Where hzla's PlatPatches hook the item file lookup; their tool patches US Platinum.</summary>
        private static Spot PlatPatchesItemFileIdHook(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x7CE78),   // Europe
                GameLanguages.English => Arm9(0x7CE78),   // US, and any English revision not listed
                _ => null,
            },
            _ => null,
        };

        /// <summary>Where hzla's PlatPatches hook the TM/HM check; their tool patches US Platinum.</summary>
        private static Spot PlatPatchesItemIsTmHmHook(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x5E060),   // Europe
                GameLanguages.English => Arm9(0x5E060),   // US, and any English revision not listed
                _ => null,
            },
            _ => null,
        };

        /// <summary>The trainer AI function the AI backport replaces; its first bytes tell the backport is in. The backport is for US Platinum.</summary>
        private static Spot AiBackportCheck(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x793B8),   // Europe
                GameLanguages.English => Arm9(0x793B8),   // US, and any English revision not listed
                _ => null,
            },
            _ => null,
        };

        /// <summary>Where the first AI backport release differs from the fixed one.</summary>
        private static Spot AiBackportOutdatedCheck(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x795A2),   // Europe
                GameLanguages.English => Arm9(0x795A2),   // US, and any English revision not listed
                _ => null,
            },
            _ => null,
        };

        /// <summary>
        /// Where the dynamic headers patch writes its header loader: the start of the function that reads a header field
        /// from the arm9 table. Every (header * 0x18) site the patch rewrites sits at the same distance from it in every dump
        /// listed. Korean code is laid out differently.
        /// </summary>
        private static Spot DynamicHeadersInit(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x3A024),   // Europe
                GameLanguages.English => Arm9(0x3A024),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x39BE0),
                GameLanguages.French => Arm9(0x3A0C8),
                GameLanguages.German => Arm9(0x3A0C8),
                GameLanguages.Italian => Arm9(0x3A0C8),
                GameLanguages.Spanish => Arm9(0x3A0C8),
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x3B268),   // Europe
                GameLanguages.English => Arm9(0x3B268),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x3AE20),
                GameLanguages.French => Arm9(0x3B268),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x3B268),
                GameLanguages.Italian => Arm9(0x3B268),
                GameLanguages.Spanish => Arm9(0x3B260),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x3B268),   // Europe
                GameLanguages.English => Arm9(0x3B268),   // US, and any English revision not listed
                GameLanguages.Japanese => Arm9(0x3AE20),
                GameLanguages.French => Arm9(0x3B268),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => Arm9(0x3B268),
                GameLanguages.Italian => Arm9(0x3B268),
                GameLanguages.Spanish => Arm9(0x3B268),
                _ => null,
            },
            _ => null,
        };

        /// <summary>
        /// The first of the matrix 0 limits the matrix expansion patch rewrites; the rest keep their distance from it.
        /// Spanish HeartGold's code sits 8 bytes earlier than the others'.
        /// </summary>
        private static Spot MatrixExpansionCode(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x3AEC6),   // Europe
                GameLanguages.English => Arm9(0x3AEC6),   // US, and any English revision not listed
                GameLanguages.Spanish => Arm9(0x3AEBE),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => Arm9(0x3AEC6),   // Europe
                GameLanguages.English => Arm9(0x3AEC6),   // US, and any English revision not listed
                GameLanguages.Spanish => Arm9(0x3AEC6),
                _ => null,
            },
            _ => null,
        };
    }
}
