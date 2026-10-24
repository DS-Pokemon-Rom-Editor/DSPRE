// Generated from the per-dump site tables RomInfo kept before the locator; every supported game and language is
// written out. Sites outside the US dumps were found by the bytes around them in the US dump of the same game, and
// RomLocatorTests checks each site against the dump it names.
using System;
using static DSPRE.RomInfo;

namespace DSPRE
{
    public static partial class RomLocator
    {
        private static MoveListSites PunchingMovesSites(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => new MoveListSites(0x1E81C, 0x1E534, 15),   // Europe
                GameLanguages.English => new MoveListSites(0x1E81C, 0x1E534, 15),   // US, and any English revision not listed
                GameLanguages.Japanese => new MoveListSites(0x17D6C, 0x17C78, 15),
                GameLanguages.French => new MoveListSites(0x1E958, 0x1E670, 15),
                GameLanguages.German => new MoveListSites(0x1E86C, 0x1E584, 15),
                GameLanguages.Italian => new MoveListSites(0x1E86C, 0x1E584, 15),
                GameLanguages.Spanish => new MoveListSites(0x1E958, 0x1E670, 15),
                GameLanguages.Korean => new MoveListSites(0x1E82C, 0x1E544, 15),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => new MoveListSites(0x1E81C, 0x1E534, 15),   // Europe
                GameLanguages.English => new MoveListSites(0x1E81C, 0x1E534, 15),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => new MoveListSites(0x17D6C, 0x17C78, 15),
                GameLanguages.Japanese when revision == 5 => new MoveListSites(0x17D6C, 0x17C78, 15),
                GameLanguages.Japanese when revision == 6 => new MoveListSites(0x17D6C, 0x17C78, 15),
                GameLanguages.French => new MoveListSites(0x1E958, 0x1E670, 15),
                GameLanguages.German => new MoveListSites(0x1E86C, 0x1E584, 15),
                GameLanguages.Italian => new MoveListSites(0x1E86C, 0x1E584, 15),
                GameLanguages.Spanish => new MoveListSites(0x1E958, 0x1E670, 15),
                GameLanguages.Korean => new MoveListSites(0x1E82C, 0x1E544, 15),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => new MoveListSites(0x1FC2C, 0x1F960, 15),   // Europe
                GameLanguages.English => new MoveListSites(0x1FC2C, 0x1F960, 15),   // US, and any English revision not listed
                GameLanguages.Japanese => new MoveListSites(0x1FC20, 0x1F954, 15),
                GameLanguages.French => new MoveListSites(0x1FD34, 0x1FA68, 15),
                GameLanguages.German => new MoveListSites(0x1FC48, 0x1F97C, 15),
                GameLanguages.Italian => new MoveListSites(0x1FC48, 0x1F97C, 15),
                GameLanguages.Spanish => new MoveListSites(0x1FD34, 0x1FA68, 15),
                GameLanguages.Korean => new MoveListSites(0x1FC44, 0x1F978, 15),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => new MoveListSites(0x20224, 0x1FF58, 15),   // Europe
                GameLanguages.English => new MoveListSites(0x20224, 0x1FF58, 15),   // US, and any English revision not listed
                GameLanguages.Japanese => new MoveListSites(0x2022C, 0x1FF60, 15),
                GameLanguages.French => new MoveListSites(0x20224, 0x1FF58, 15),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new MoveListSites(0x20224, 0x1FF58, 15),
                GameLanguages.Italian => new MoveListSites(0x20224, 0x1FF58, 15),
                GameLanguages.Spanish => new MoveListSites(0x20224, 0x1FF58, 15),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => new MoveListSites(0x20228, 0x1FF5C, 15),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => new MoveListSites(0x20224, 0x1FF58, 15),   // Europe
                GameLanguages.English => new MoveListSites(0x20224, 0x1FF58, 15),   // US, and any English revision not listed
                GameLanguages.Japanese => new MoveListSites(0x2022C, 0x1FF60, 15),
                GameLanguages.French => new MoveListSites(0x20224, 0x1FF58, 15),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new MoveListSites(0x20224, 0x1FF58, 15),
                GameLanguages.Italian => new MoveListSites(0x20224, 0x1FF58, 15),
                GameLanguages.Spanish => new MoveListSites(0x20224, 0x1FF58, 15),
                GameLanguages.Korean => new MoveListSites(0x20228, 0x1FF5C, 15),
                _ => null,
            },
            _ => null,
        };

        private static MoveListSites SoundMovesSites(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => new MoveListSites(0x19FF4, 0x19F68, 14),   // Europe
                GameLanguages.English => new MoveListSites(0x19FF4, 0x19F68, 14),   // US, and any English revision not listed
                GameLanguages.Japanese => new MoveListSites(0x1C340, 0x1C2AE, 14),
                GameLanguages.French => new MoveListSites(0x1A130, 0x1A0A4, 14),
                GameLanguages.German => new MoveListSites(0x1A044, 0x19FB8, 14),
                GameLanguages.Italian => new MoveListSites(0x1A044, 0x19FB8, 14),
                GameLanguages.Spanish => new MoveListSites(0x1A130, 0x1A0A4, 14),
                GameLanguages.Korean => new MoveListSites(0x1A044, 0x19FB8, 14),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => new MoveListSites(0x19FF4, 0x19F68, 14),   // Europe
                GameLanguages.English => new MoveListSites(0x19FF4, 0x19F68, 14),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => new MoveListSites(0x1C340, 0x1C2AE, 14),
                GameLanguages.Japanese when revision == 5 => new MoveListSites(0x1C340, 0x1C2AE, 14),
                GameLanguages.Japanese when revision == 6 => new MoveListSites(0x1C340, 0x1C2AE, 14),
                GameLanguages.French => new MoveListSites(0x1A130, 0x1A0A4, 14),
                GameLanguages.German => new MoveListSites(0x1A044, 0x19FB8, 14),
                GameLanguages.Italian => new MoveListSites(0x1A044, 0x19FB8, 14),
                GameLanguages.Spanish => new MoveListSites(0x1A130, 0x1A0A4, 14),
                GameLanguages.Korean => new MoveListSites(0x1A044, 0x19FB8, 14),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => new MoveListSites(0x1B1A4, 0x1B118, 26),   // Europe
                GameLanguages.English => new MoveListSites(0x1B1A4, 0x1B118, 26),   // US, and any English revision not listed
                GameLanguages.Japanese => new MoveListSites(0x1B198, 0x1B10C, 26),
                GameLanguages.French => new MoveListSites(0x1B2AC, 0x1B220, 26),
                GameLanguages.German => new MoveListSites(0x1B1C0, 0x1B134, 26),
                GameLanguages.Italian => new MoveListSites(0x1B1C0, 0x1B134, 26),
                GameLanguages.Spanish => new MoveListSites(0x1B2AC, 0x1B220, 26),
                GameLanguages.Korean => new MoveListSites(0x1B1BC, 0x1B130, 26),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => new MoveListSites(0x1B7A4, 0x1B718, 16),   // Europe
                GameLanguages.English => new MoveListSites(0x1B7A4, 0x1B718, 16),   // US, and any English revision not listed
                GameLanguages.Japanese => new MoveListSites(0x1B7AC, 0x1B720, 16),
                GameLanguages.French => new MoveListSites(0x1B7A4, 0x1B718, 16),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new MoveListSites(0x1B7A4, 0x1B718, 16),
                GameLanguages.Italian => new MoveListSites(0x1B7A4, 0x1B718, 16),
                GameLanguages.Spanish => new MoveListSites(0x1B7A4, 0x1B718, 16),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => new MoveListSites(0x1B7A8, 0x1B71C, 16),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => new MoveListSites(0x1B7A4, 0x1B718, 16),   // Europe
                GameLanguages.English => new MoveListSites(0x1B7A4, 0x1B718, 16),   // US, and any English revision not listed
                GameLanguages.Japanese => new MoveListSites(0x1B7AC, 0x1B720, 16),
                GameLanguages.French => new MoveListSites(0x1B7A4, 0x1B718, 16),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new MoveListSites(0x1B7A4, 0x1B718, 16),
                GameLanguages.Italian => new MoveListSites(0x1B7A4, 0x1B718, 16),
                GameLanguages.Spanish => new MoveListSites(0x1B7A4, 0x1B718, 16),
                GameLanguages.Korean => new MoveListSites(0x1B7A8, 0x1B71C, 16),
                _ => null,
            },
            _ => null,
        };

        public static SwarmSites SwarmSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => new SwarmSites(6, 0x17CA0, 28, 4, new[] { 0xC26C, 0xC298 }, Array.Empty<int>(), new[] { 0xC25E, 0xC276 }),   // Europe
                GameLanguages.English => new SwarmSites(6, 0x17CA0, 28, 4, new[] { 0xC26C, 0xC298 }, Array.Empty<int>(), new[] { 0xC25E, 0xC276 }),   // US, and any English revision not listed
                GameLanguages.Japanese => new SwarmSites(6, 0x18778, 28, 4, new[] { 0xC7F8, 0xC810 }, Array.Empty<int>(), new[] { 0xC7D8, 0xC800 }),
                GameLanguages.French => new SwarmSites(6, 0x17C48, 28, 4, new[] { 0xC214, 0xC240 }, Array.Empty<int>(), new[] { 0xC206, 0xC21E }),
                GameLanguages.German => new SwarmSites(6, 0x17C48, 28, 4, new[] { 0xC214, 0xC240 }, Array.Empty<int>(), new[] { 0xC206, 0xC21E }),
                GameLanguages.Italian => new SwarmSites(6, 0x17C48, 28, 4, new[] { 0xC214, 0xC240 }, Array.Empty<int>(), new[] { 0xC206, 0xC21E }),
                GameLanguages.Spanish => new SwarmSites(6, 0x17C48, 28, 4, new[] { 0xC214, 0xC240 }, Array.Empty<int>(), new[] { 0xC206, 0xC21E }),
                GameLanguages.Korean => new SwarmSites(6, 0x17C4C, 28, 4, new[] { 0xC214, 0xC240 }, Array.Empty<int>(), new[] { 0xC206, 0xC21E }),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => new SwarmSites(6, 0x17CAC, 28, 4, new[] { 0xC278, 0xC2A4 }, Array.Empty<int>(), new[] { 0xC26A, 0xC282 }),   // Europe
                GameLanguages.English => new SwarmSites(6, 0x17CAC, 28, 4, new[] { 0xC278, 0xC2A4 }, Array.Empty<int>(), new[] { 0xC26A, 0xC282 }),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => new SwarmSites(6, 0x18778, 28, 4, new[] { 0xC7F8, 0xC810 }, Array.Empty<int>(), new[] { 0xC7D8, 0xC800 }),
                GameLanguages.Japanese when revision == 5 => new SwarmSites(6, 0x18778, 28, 4, new[] { 0xC7F8, 0xC810 }, Array.Empty<int>(), new[] { 0xC7D8, 0xC800 }),
                GameLanguages.Japanese when revision == 6 => new SwarmSites(6, 0x18778, 28, 4, new[] { 0xC7F8, 0xC810 }, Array.Empty<int>(), new[] { 0xC7D8, 0xC800 }),
                GameLanguages.French => new SwarmSites(6, 0x17C54, 28, 4, new[] { 0xC220, 0xC24C }, Array.Empty<int>(), new[] { 0xC212, 0xC22A }),
                GameLanguages.German => new SwarmSites(6, 0x17C54, 28, 4, new[] { 0xC220, 0xC24C }, Array.Empty<int>(), new[] { 0xC212, 0xC22A }),
                GameLanguages.Italian => new SwarmSites(6, 0x17C54, 28, 4, new[] { 0xC220, 0xC24C }, Array.Empty<int>(), new[] { 0xC212, 0xC22A }),
                GameLanguages.Spanish => new SwarmSites(6, 0x17C54, 28, 4, new[] { 0xC220, 0xC24C }, Array.Empty<int>(), new[] { 0xC212, 0xC22A }),
                GameLanguages.Korean => new SwarmSites(6, 0x17C58, 28, 4, new[] { 0xC220, 0xC24C }, Array.Empty<int>(), new[] { 0xC212, 0xC22A }),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => new SwarmSites(6, 0xAF50, 22, 4, new[] { 0x50E8, 0x5114 }, Array.Empty<int>(), new[] { 0x50DA, 0x50F2 }),   // Europe
                GameLanguages.English => new SwarmSites(6, 0xAF50, 22, 4, new[] { 0x50E8, 0x5114 }, Array.Empty<int>(), new[] { 0x50DA, 0x50F2 }),   // US, and any English revision not listed
                GameLanguages.Japanese => new SwarmSites(6, 0xAF50, 22, 4, new[] { 0x50E8, 0x5114 }, Array.Empty<int>(), new[] { 0x50DA, 0x50F2 }),
                GameLanguages.French => new SwarmSites(6, 0xAF50, 22, 4, new[] { 0x50E8, 0x5114 }, Array.Empty<int>(), new[] { 0x50DA, 0x50F2 }),
                GameLanguages.German => new SwarmSites(6, 0xAF50, 22, 4, new[] { 0x50E8, 0x5114 }, Array.Empty<int>(), new[] { 0x50DA, 0x50F2 }),
                GameLanguages.Italian => new SwarmSites(6, 0xAF50, 22, 4, new[] { 0x50E8, 0x5114 }, Array.Empty<int>(), new[] { 0x50DA, 0x50F2 }),
                GameLanguages.Spanish => new SwarmSites(6, 0xAF50, 22, 4, new[] { 0x50E8, 0x5114 }, Array.Empty<int>(), new[] { 0x50DA, 0x50F2 }),
                GameLanguages.Korean => new SwarmSites(6, 0xAF50, 22, 4, new[] { 0x50E8, 0x5114 }, Array.Empty<int>(), new[] { 0x50DA, 0x50F2 }),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => new SwarmSites(-1, 0x108F4C, 20, 4, new[] { 0x97F94, 0x97FF0 }, new[] { 0x97F98 }, new[] { 0x97F70, 0x97FA2 }),   // Europe
                GameLanguages.English => new SwarmSites(-1, 0x108F4C, 20, 4, new[] { 0x97F94, 0x97FF0 }, new[] { 0x97F98 }, new[] { 0x97F70, 0x97FA2 }),   // US, and any English revision not listed
                GameLanguages.Japanese => new SwarmSites(-1, 0x108498, 20, 4, new[] { 0x97794, 0x977F0 }, new[] { 0x97798 }, new[] { 0x97770, 0x977A2 }),
                GameLanguages.French => new SwarmSites(-1, 0x108F70, 20, 4, new[] { 0x97F94, 0x97FF0 }, new[] { 0x97F98 }, new[] { 0x97F70, 0x97FA2 }),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new SwarmSites(-1, 0x108F34, 20, 4, new[] { 0x97F94, 0x97FF0 }, new[] { 0x97F98 }, new[] { 0x97F70, 0x97FA2 }),
                GameLanguages.Italian => new SwarmSites(-1, 0x108EE0, 20, 4, new[] { 0x97F94, 0x97FF0 }, new[] { 0x97F98 }, new[] { 0x97F70, 0x97FA2 }),
                GameLanguages.Spanish => new SwarmSites(-1, 0x108F78, 20, 4, new[] { 0x97F8C, 0x97FE8 }, new[] { 0x97F90 }, new[] { 0x97F68, 0x97F9A }),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => new SwarmSites(-1, 0x1096C8, 20, 4, new[] { 0x98688, 0x986E4 }, new[] { 0x9868C }, new[] { 0x98664, 0x98696 }),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => new SwarmSites(-1, 0x108F4C, 20, 4, new[] { 0x97F94, 0x97FF0 }, new[] { 0x97F98 }, new[] { 0x97F70, 0x97FA2 }),   // Europe
                GameLanguages.English => new SwarmSites(-1, 0x108F4C, 20, 4, new[] { 0x97F94, 0x97FF0 }, new[] { 0x97F98 }, new[] { 0x97F70, 0x97FA2 }),   // US, and any English revision not listed
                GameLanguages.Japanese => new SwarmSites(-1, 0x108498, 20, 4, new[] { 0x97794, 0x977F0 }, new[] { 0x97798 }, new[] { 0x97770, 0x977A2 }),
                GameLanguages.French => new SwarmSites(-1, 0x108F70, 20, 4, new[] { 0x97F94, 0x97FF0 }, new[] { 0x97F98 }, new[] { 0x97F70, 0x97FA2 }),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new SwarmSites(-1, 0x108F34, 20, 4, new[] { 0x97F94, 0x97FF0 }, new[] { 0x97F98 }, new[] { 0x97F70, 0x97FA2 }),
                GameLanguages.Italian => new SwarmSites(-1, 0x108EE0, 20, 4, new[] { 0x97F94, 0x97FF0 }, new[] { 0x97F98 }, new[] { 0x97F70, 0x97FA2 }),
                GameLanguages.Spanish => new SwarmSites(-1, 0x108F80, 20, 4, new[] { 0x97F94, 0x97FF0 }, new[] { 0x97F98 }, new[] { 0x97F70, 0x97FA2 }),
                GameLanguages.Korean => new SwarmSites(-1, 0x1096C0, 20, 4, new[] { 0x98680, 0x986DC }, new[] { 0x98684 }, new[] { 0x9865C, 0x9868E }),
                _ => null,
            },
            _ => null,
        };

        public static BpShopSites BpShopSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => new BpShopSites(-1, -1, -1, 0xF433E, 0x42AEA),   // Europe
                GameLanguages.English => new BpShopSites(-1, -1, -1, 0xF433E, 0x42AEA),   // US, and any English revision not listed
                GameLanguages.Japanese => new BpShopSites(-1, -1, -1, 0xF6164, 0x44B04),
                GameLanguages.French => new BpShopSites(-1, -1, -1, 0xF4382, 0x42B5A),
                GameLanguages.German => new BpShopSites(-1, -1, -1, 0xF4352, 0x42B5A),
                GameLanguages.Italian => new BpShopSites(-1, -1, -1, 0xF42F6, 0x42B5A),
                GameLanguages.Spanish => new BpShopSites(-1, -1, -1, 0xF438E, 0x42B5A),
                GameLanguages.Korean => new BpShopSites(-1, -1, -1, 0xEF98E, 0x42F7E),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => new BpShopSites(-1, -1, -1, 0xF433E, 0x42AEA),   // Europe
                GameLanguages.English => new BpShopSites(-1, -1, -1, 0xF433E, 0x42AEA),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => new BpShopSites(-1, -1, -1, 0xF6168, 0x44B04),
                GameLanguages.Japanese when revision == 5 => new BpShopSites(-1, -1, -1, 0xF6168, 0x44B04),
                GameLanguages.Japanese when revision == 6 => new BpShopSites(-1, -1, -1, 0xF62A8, 0x44BA8),
                GameLanguages.French => new BpShopSites(-1, -1, -1, 0xF4382, 0x42B5A),
                GameLanguages.German => new BpShopSites(-1, -1, -1, 0xF4352, 0x42B5A),
                GameLanguages.Italian => new BpShopSites(-1, -1, -1, 0xF42F6, 0x42B5A),
                GameLanguages.Spanish => new BpShopSites(-1, -1, -1, 0xF438E, 0x42B5A),
                GameLanguages.Korean => new BpShopSites(-1, -1, -1, 0xEF98E, 0x42F7E),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => new BpShopSites(0x100AF0, 0x4F8C, 0x4F82, 0xEBC82, 0x49CE6),   // Europe
                GameLanguages.English => new BpShopSites(0x100AF0, 0x4F8C, 0x4F82, 0xEBC82, 0x49CE6),   // US, and any English revision not listed
                GameLanguages.Japanese => new BpShopSites(0xFFEE4, 0x4DA8, 0x4D9E, 0xEB312, 0x495C2),
                GameLanguages.French => new BpShopSites(0x100CCC, 0x4F44, 0x4F3A, 0xEBD0A, 0x49D8A),
                GameLanguages.German => new BpShopSites(0x100C8C, 0x4F44, 0x4F3A, 0xEBCDA, 0x49D8A),
                GameLanguages.Italian => new BpShopSites(0x100C4C, 0x4F44, 0x4F3A, 0xEBC9E, 0x49D8A),
                GameLanguages.Spanish => new BpShopSites(0x100CEC, 0x4F44, 0x4F3A, 0xEBD16, 0x49D8A),
                GameLanguages.Korean => new BpShopSites(0x1019CC, 0x4F44, 0x4F3A, 0xEC70A, 0x4A1DA),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            _ => null,
        };

        public static HpBarTextSites HpBarTextSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => new HpBarTextSites(11, new[] { 0x29FC2, 0x2A1DC, 0x2A2E8, 0x2A372 }, new[] { 0x29FEE, 0x2A00E, 0x2A032, 0x2A056, 0x2A270, 0x2A296, 0x2A30C, 0x2A32A, 0x2A394 }, new[] { 0x2A0C4 }, new[] { 0x2A158, 0x2A17E }),   // Europe
                GameLanguages.English => new HpBarTextSites(11, new[] { 0x29FC2, 0x2A1DC, 0x2A2E8, 0x2A372 }, new[] { 0x29FEE, 0x2A00E, 0x2A032, 0x2A056, 0x2A270, 0x2A296, 0x2A30C, 0x2A32A, 0x2A394 }, new[] { 0x2A0C4 }, new[] { 0x2A158, 0x2A17E }),   // US, and any English revision not listed
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => new HpBarTextSites(16, new[] { 0x2C7B6, 0x2C94C, 0x2CA58, 0x2CAE2 }, new[] { 0x2C7E2, 0x2C802, 0x2C826, 0x2C84A, 0x2C9E0, 0x2CA06, 0x2CA7C, 0x2CA9A, 0x2CB04 }, new[] { 0x2C8B8, 0x2CB36 }, new[] { 0x2C8DC, 0x2C8F6, 0x2CB58 }),   // Europe
                GameLanguages.English => new HpBarTextSites(16, new[] { 0x2C7B6, 0x2C94C, 0x2CA58, 0x2CAE2 }, new[] { 0x2C7E2, 0x2C802, 0x2C826, 0x2C84A, 0x2C9E0, 0x2CA06, 0x2CA7C, 0x2CA9A, 0x2CB04 }, new[] { 0x2C8B8, 0x2CB36 }, new[] { 0x2C8DC, 0x2C8F6, 0x2CB58 }),   // US, and any English revision not listed
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => new HpBarTextSites(12, new[] { 0x2D93E, 0x2DAD4, 0x2DBE0, 0x2DC6A }, new[] { 0x2D96A, 0x2D98A, 0x2D9AE, 0x2D9D2, 0x2DB68, 0x2DB8E, 0x2DC04, 0x2DC22, 0x2DC8C }, new[] { 0x2DA40, 0x2DCBE }, new[] { 0x2DA64, 0x2DA7E, 0x2DCE0 }),   // Europe
                GameLanguages.English => new HpBarTextSites(12, new[] { 0x2D93E, 0x2DAD4, 0x2DBE0, 0x2DC6A }, new[] { 0x2D96A, 0x2D98A, 0x2D9AE, 0x2D9D2, 0x2DB68, 0x2DB8E, 0x2DC04, 0x2DC22, 0x2DC8C }, new[] { 0x2DA40, 0x2DCBE }, new[] { 0x2DA64, 0x2DA7E, 0x2DCE0 }),   // US, and any English revision not listed
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => new HpBarTextSites(12, new[] { 0x2D93E, 0x2DAD4, 0x2DBE0, 0x2DC6A }, new[] { 0x2D96A, 0x2D98A, 0x2D9AE, 0x2D9D2, 0x2DB68, 0x2DB8E, 0x2DC04, 0x2DC22, 0x2DC8C }, new[] { 0x2DA40, 0x2DCBE }, new[] { 0x2DA64, 0x2DA7E, 0x2DCE0 }),   // Europe
                GameLanguages.English => new HpBarTextSites(12, new[] { 0x2D93E, 0x2DAD4, 0x2DBE0, 0x2DC6A }, new[] { 0x2D96A, 0x2D98A, 0x2D9AE, 0x2D9D2, 0x2DB68, 0x2DB8E, 0x2DC04, 0x2DC22, 0x2DC8C }, new[] { 0x2DA40, 0x2DCBE }, new[] { 0x2DA64, 0x2DA7E, 0x2DCE0 }),   // US, and any English revision not listed
                _ => null,
            },
            _ => null,
        };

        public static GaugeLengthSites GaugeLengthSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => new GaugeLengthSites(11, new[] { 0x2A636, 0x2A6E2, 0x2A6F4, 0x2A788 }, new[] { 0x2A656, 0x2A66E, 0x2A794, 0x2A7BC, 0x2A816 }),   // Europe
                GameLanguages.English => new GaugeLengthSites(11, new[] { 0x2A636, 0x2A6E2, 0x2A6F4, 0x2A788 }, new[] { 0x2A656, 0x2A66E, 0x2A794, 0x2A7BC, 0x2A816 }),   // US, and any English revision not listed
                GameLanguages.Japanese => null,   // the Japanese gauge code is laid out differently
                GameLanguages.French => new GaugeLengthSites(11, new[] { 0x2A79E, 0x2A84A, 0x2A85C, 0x2A8F0 }, new[] { 0x2A7BE, 0x2A7D6, 0x2A8FC, 0x2A924, 0x2A97E }),
                GameLanguages.German => new GaugeLengthSites(11, new[] { 0x2A6B2, 0x2A75E, 0x2A770, 0x2A804 }, new[] { 0x2A6D2, 0x2A6EA, 0x2A810, 0x2A838, 0x2A892 }),
                GameLanguages.Italian => new GaugeLengthSites(11, new[] { 0x2A6B2, 0x2A75E, 0x2A770, 0x2A804 }, new[] { 0x2A6D2, 0x2A6EA, 0x2A810, 0x2A838, 0x2A892 }),
                GameLanguages.Spanish => new GaugeLengthSites(11, new[] { 0x2A79E, 0x2A84A, 0x2A85C, 0x2A8F0 }, new[] { 0x2A7BE, 0x2A7D6, 0x2A8FC, 0x2A924, 0x2A97E }),
                GameLanguages.Korean => new GaugeLengthSites(11, new[] { 0x2A626, 0x2A6D2, 0x2A6E4, 0x2A778 }, new[] { 0x2A646, 0x2A65E, 0x2A784, 0x2A7AC, 0x2A806 }),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => new GaugeLengthSites(11, new[] { 0x2A636, 0x2A6E2, 0x2A6F4, 0x2A788 }, new[] { 0x2A656, 0x2A66E, 0x2A794, 0x2A7BC, 0x2A816 }),   // Europe
                GameLanguages.English => new GaugeLengthSites(11, new[] { 0x2A636, 0x2A6E2, 0x2A6F4, 0x2A788 }, new[] { 0x2A656, 0x2A66E, 0x2A794, 0x2A7BC, 0x2A816 }),   // US, and any English revision not listed
                GameLanguages.Japanese => null,   // the Japanese gauge code is laid out differently
                GameLanguages.French => new GaugeLengthSites(11, new[] { 0x2A79E, 0x2A84A, 0x2A85C, 0x2A8F0 }, new[] { 0x2A7BE, 0x2A7D6, 0x2A8FC, 0x2A924, 0x2A97E }),
                GameLanguages.German => new GaugeLengthSites(11, new[] { 0x2A6B2, 0x2A75E, 0x2A770, 0x2A804 }, new[] { 0x2A6D2, 0x2A6EA, 0x2A810, 0x2A838, 0x2A892 }),
                GameLanguages.Italian => new GaugeLengthSites(11, new[] { 0x2A6B2, 0x2A75E, 0x2A770, 0x2A804 }, new[] { 0x2A6D2, 0x2A6EA, 0x2A810, 0x2A838, 0x2A892 }),
                GameLanguages.Spanish => new GaugeLengthSites(11, new[] { 0x2A79E, 0x2A84A, 0x2A85C, 0x2A8F0 }, new[] { 0x2A7BE, 0x2A7D6, 0x2A8FC, 0x2A924, 0x2A97E }),
                GameLanguages.Korean => new GaugeLengthSites(11, new[] { 0x2A626, 0x2A6D2, 0x2A6E4, 0x2A778 }, new[] { 0x2A646, 0x2A65E, 0x2A784, 0x2A7AC, 0x2A806 }),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => new GaugeLengthSites(16, new[] { 0x2CDA6, 0x2CE52, 0x2CE64, 0x2CEF8 }, new[] { 0x2CDC6, 0x2CDDE, 0x2CF04, 0x2CF2C, 0x2CF86 }),   // Europe
                GameLanguages.English => new GaugeLengthSites(16, new[] { 0x2CDA6, 0x2CE52, 0x2CE64, 0x2CEF8 }, new[] { 0x2CDC6, 0x2CDDE, 0x2CF04, 0x2CF2C, 0x2CF86 }),   // US, and any English revision not listed
                GameLanguages.Japanese => new GaugeLengthSites(16, new[] { 0x2CD9A, 0x2CE46, 0x2CE58, 0x2CEEC }, new[] { 0x2CDBA, 0x2CDD2, 0x2CEF8, 0x2CF20, 0x2CF7A }),
                GameLanguages.French => new GaugeLengthSites(16, new[] { 0x2CEAE, 0x2CF5A, 0x2CF6C, 0x2D000 }, new[] { 0x2CECE, 0x2CEE6, 0x2D00C, 0x2D034, 0x2D08E }),
                GameLanguages.German => new GaugeLengthSites(16, new[] { 0x2CDC2, 0x2CE6E, 0x2CE80, 0x2CF14 }, new[] { 0x2CDE2, 0x2CDFA, 0x2CF20, 0x2CF48, 0x2CFA2 }),
                GameLanguages.Italian => new GaugeLengthSites(16, new[] { 0x2CDC2, 0x2CE6E, 0x2CE80, 0x2CF14 }, new[] { 0x2CDE2, 0x2CDFA, 0x2CF20, 0x2CF48, 0x2CFA2 }),
                GameLanguages.Spanish => new GaugeLengthSites(16, new[] { 0x2CEAE, 0x2CF5A, 0x2CF6C, 0x2D000 }, new[] { 0x2CECE, 0x2CEE6, 0x2D00C, 0x2D034, 0x2D08E }),
                GameLanguages.Korean => new GaugeLengthSites(16, new[] { 0x2CDBE, 0x2CE6A, 0x2CE7C, 0x2CF10 }, new[] { 0x2CDDE, 0x2CDF6, 0x2CF1C, 0x2CF44, 0x2CF9E }),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => new GaugeLengthSites(12, new[] { 0x2DF2E, 0x2DFDA, 0x2DFEC, 0x2E080 }, new[] { 0x2DF4E, 0x2DF66, 0x2E08C, 0x2E0B4, 0x2E10E }),   // Europe
                GameLanguages.English => new GaugeLengthSites(12, new[] { 0x2DF2E, 0x2DFDA, 0x2DFEC, 0x2E080 }, new[] { 0x2DF4E, 0x2DF66, 0x2E08C, 0x2E0B4, 0x2E10E }),   // US, and any English revision not listed
                GameLanguages.Japanese => new GaugeLengthSites(12, new[] { 0x2DEEA, 0x2DF96, 0x2DFA8, 0x2E03C }, new[] { 0x2DF0A, 0x2DF22, 0x2E048, 0x2E070, 0x2E0CA }),
                GameLanguages.French => new GaugeLengthSites(12, new[] { 0x2DF2E, 0x2DFDA, 0x2DFEC, 0x2E080 }, new[] { 0x2DF4E, 0x2DF66, 0x2E08C, 0x2E0B4, 0x2E10E }),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new GaugeLengthSites(12, new[] { 0x2DF2E, 0x2DFDA, 0x2DFEC, 0x2E080 }, new[] { 0x2DF4E, 0x2DF66, 0x2E08C, 0x2E0B4, 0x2E10E }),
                GameLanguages.Italian => new GaugeLengthSites(12, new[] { 0x2DF2E, 0x2DFDA, 0x2DFEC, 0x2E080 }, new[] { 0x2DF4E, 0x2DF66, 0x2E08C, 0x2E0B4, 0x2E10E }),
                GameLanguages.Spanish => new GaugeLengthSites(12, new[] { 0x2DF2E, 0x2DFDA, 0x2DFEC, 0x2E080 }, new[] { 0x2DF4E, 0x2DF66, 0x2E08C, 0x2E0B4, 0x2E10E }),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => new GaugeLengthSites(12, new[] { 0x2DF32, 0x2DFDE, 0x2DFF0, 0x2E084 }, new[] { 0x2DF52, 0x2DF6A, 0x2E090, 0x2E0B8, 0x2E112 }),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => new GaugeLengthSites(12, new[] { 0x2DF2E, 0x2DFDA, 0x2DFEC, 0x2E080 }, new[] { 0x2DF4E, 0x2DF66, 0x2E08C, 0x2E0B4, 0x2E10E }),   // Europe
                GameLanguages.English => new GaugeLengthSites(12, new[] { 0x2DF2E, 0x2DFDA, 0x2DFEC, 0x2E080 }, new[] { 0x2DF4E, 0x2DF66, 0x2E08C, 0x2E0B4, 0x2E10E }),   // US, and any English revision not listed
                GameLanguages.Japanese => new GaugeLengthSites(12, new[] { 0x2DEEA, 0x2DF96, 0x2DFA8, 0x2E03C }, new[] { 0x2DF0A, 0x2DF22, 0x2E048, 0x2E070, 0x2E0CA }),
                GameLanguages.French => new GaugeLengthSites(12, new[] { 0x2DF2E, 0x2DFDA, 0x2DFEC, 0x2E080 }, new[] { 0x2DF4E, 0x2DF66, 0x2E08C, 0x2E0B4, 0x2E10E }),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new GaugeLengthSites(12, new[] { 0x2DF2E, 0x2DFDA, 0x2DFEC, 0x2E080 }, new[] { 0x2DF4E, 0x2DF66, 0x2E08C, 0x2E0B4, 0x2E10E }),
                GameLanguages.Italian => new GaugeLengthSites(12, new[] { 0x2DF2E, 0x2DFDA, 0x2DFEC, 0x2E080 }, new[] { 0x2DF4E, 0x2DF66, 0x2E08C, 0x2E0B4, 0x2E10E }),
                GameLanguages.Spanish => new GaugeLengthSites(12, new[] { 0x2DF2E, 0x2DFDA, 0x2DFEC, 0x2E080 }, new[] { 0x2DF4E, 0x2DF66, 0x2E08C, 0x2E0B4, 0x2E10E }),
                GameLanguages.Korean => new GaugeLengthSites(12, new[] { 0x2DF32, 0x2DFDE, 0x2DFF0, 0x2E084 }, new[] { 0x2DF52, 0x2DF6A, 0x2E090, 0x2E0B8, 0x2E112 }),
                _ => null,
            },
            _ => null,
        };

        // Japanese Diamond and Pearl compare each boundary once more, so their selectors are not the US ones moved.
        private static readonly SlotOddsMethod[] JapaneseDpSlotOdds =
        {
            new SlotOddsMethod("Walking", 6, 12, new[] { new[] { 0x31E0, 0x31E8 }, new[] { 0x31EC, 0x31F4 }, new[] { 0x31F8, 0x3200 }, new[] { 0x3204, 0x320C }, new[] { 0x3210, 0x3218 }, new[] { 0x321C, 0x3224 }, new[] { 0x3228, 0x3230 }, new[] { 0x3234, 0x323C }, new[] { 0x3240, 0x3248 }, new[] { 0x324C } }, 0x74),
            new SlotOddsMethod("Surfing", 6, 5, new[] { new[] { 0x3190, 0x3198 }, new[] { 0x319C, 0x31A4 }, new[] { 0x31A8, 0x31B0 }, new[] { 0x31B4 } }),
            new SlotOddsMethod("Old Rod", 6, 5, new[] { new[] { 0x30FA }, new[] { 0x3102 }, new[] { 0x310A }, new[] { 0x3112 } }),
            new SlotOddsMethod("Good Rod", 6, 5, new[] { new[] { 0x311E }, new[] { 0x3126 }, new[] { 0x312E }, new[] { 0x3136 } }),
            new SlotOddsMethod("Super Rod", 6, 5, new[] { new[] { 0x3142 }, new[] { 0x314A }, new[] { 0x3152 }, new[] { 0x315A } }),
        };

        public static SlotOddsMethod[] SlotOddsIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => DpPtSlotOdds(0x2F2A, 0x2FC6, 0x3024, 0x3048, 0x306C),   // Europe
                GameLanguages.English => DpPtSlotOdds(0x2F2A, 0x2FC6, 0x3024, 0x3048, 0x306C),   // US, and any English revision not listed
                GameLanguages.Japanese => JapaneseDpSlotOdds,
                GameLanguages.French => DpPtSlotOdds(0x2F2A, 0x2FC6, 0x3024, 0x3048, 0x306C),
                GameLanguages.German => DpPtSlotOdds(0x2F2A, 0x2FC6, 0x3024, 0x3048, 0x306C),
                GameLanguages.Italian => DpPtSlotOdds(0x2F2A, 0x2FC6, 0x3024, 0x3048, 0x306C),
                GameLanguages.Spanish => DpPtSlotOdds(0x2F2A, 0x2FC6, 0x3024, 0x3048, 0x306C),
                GameLanguages.Korean => DpPtSlotOdds(0x2F2A, 0x2FC6, 0x3024, 0x3048, 0x306C),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => DpPtSlotOdds(0x2F36, 0x2FD2, 0x3030, 0x3054, 0x3078),   // Europe
                GameLanguages.English => DpPtSlotOdds(0x2F36, 0x2FD2, 0x3030, 0x3054, 0x3078),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => JapaneseDpSlotOdds,
                GameLanguages.Japanese when revision == 5 => JapaneseDpSlotOdds,
                GameLanguages.Japanese when revision == 6 => JapaneseDpSlotOdds,
                GameLanguages.French => DpPtSlotOdds(0x2F36, 0x2FD2, 0x3030, 0x3054, 0x3078),
                GameLanguages.German => DpPtSlotOdds(0x2F36, 0x2FD2, 0x3030, 0x3054, 0x3078),
                GameLanguages.Italian => DpPtSlotOdds(0x2F36, 0x2FD2, 0x3030, 0x3054, 0x3078),
                GameLanguages.Spanish => DpPtSlotOdds(0x2F36, 0x2FD2, 0x3030, 0x3054, 0x3078),
                GameLanguages.Korean => DpPtSlotOdds(0x2F36, 0x2FD2, 0x3030, 0x3054, 0x3078),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => DpPtSlotOdds(0x37E2, 0x387E, 0x38DC, 0x3900, 0x3924),   // Europe
                GameLanguages.English => DpPtSlotOdds(0x37E2, 0x387E, 0x38DC, 0x3900, 0x3924),   // US, and any English revision not listed
                GameLanguages.Japanese => DpPtSlotOdds(0x37E2, 0x387E, 0x38DC, 0x3900, 0x3924),
                GameLanguages.French => DpPtSlotOdds(0x37E2, 0x387E, 0x38DC, 0x3900, 0x3924),
                GameLanguages.German => DpPtSlotOdds(0x37E2, 0x387E, 0x38DC, 0x3900, 0x3924),
                GameLanguages.Italian => DpPtSlotOdds(0x37E2, 0x387E, 0x38DC, 0x3900, 0x3924),
                GameLanguages.Spanish => DpPtSlotOdds(0x37E2, 0x387E, 0x38DC, 0x3900, 0x3924),
                GameLanguages.Korean => DpPtSlotOdds(0x37E2, 0x387E, 0x38DC, 0x3900, 0x3924),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => HgssSlotOdds(0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54),   // Europe
                GameLanguages.English => HgssSlotOdds(0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54),   // US, and any English revision not listed
                GameLanguages.Japanese => HgssSlotOdds(0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54),
                GameLanguages.French => HgssSlotOdds(0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => HgssSlotOdds(0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54),
                GameLanguages.Italian => HgssSlotOdds(0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54),
                GameLanguages.Spanish => HgssSlotOdds(0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => HgssSlotOdds(0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => HgssSlotOdds(0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54),   // Europe
                GameLanguages.English => HgssSlotOdds(0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54),   // US, and any English revision not listed
                GameLanguages.Japanese => HgssSlotOdds(0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54),
                GameLanguages.French => HgssSlotOdds(0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => HgssSlotOdds(0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54),
                GameLanguages.Italian => HgssSlotOdds(0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54),
                GameLanguages.Spanish => HgssSlotOdds(0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54),
                GameLanguages.Korean => HgssSlotOdds(0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54),
                _ => null,
            },
            _ => null,
        };

        public static VsIntroSites VsIntroSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => new VsIntroSites(0x475D4, 36, 5, 0x20008, 31, 5, 0x20458, 8, 8, -1, 0x20430, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),   // Europe
                GameLanguages.English => new VsIntroSites(0x475D4, 36, 5, 0x20008, 31, 5, 0x20458, 8, 8, -1, 0x20430, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),   // US, and any English revision not listed
                GameLanguages.Japanese => new VsIntroSites(0x4A010, 36, 5, 0x21014, 31, 5, 0x21434, 8, 8, -1, 0x2140C, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
                GameLanguages.French => new VsIntroSites(0x47644, 36, 5, 0x1FFC0, 31, 5, 0x20410, 8, 8, -1, 0x203E8, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
                GameLanguages.German => new VsIntroSites(0x47644, 36, 5, 0x1FFC0, 31, 5, 0x20410, 8, 8, -1, 0x203E8, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
                GameLanguages.Italian => new VsIntroSites(0x47644, 36, 5, 0x1FFC0, 31, 5, 0x20410, 8, 8, -1, 0x203E8, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
                GameLanguages.Spanish => new VsIntroSites(0x47644, 36, 5, 0x1FFC0, 31, 5, 0x20410, 8, 8, -1, 0x203E8, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
                GameLanguages.Korean => new VsIntroSites(0x47AA0, 36, 5, 0x1FFBC, 31, 5, 0x2040C, 8, 8, -1, 0x203E4, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => new VsIntroSites(0x475D4, 36, 5, 0x20008, 31, 5, 0x20458, 8, 8, -1, 0x20430, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),   // Europe
                GameLanguages.English => new VsIntroSites(0x475D4, 36, 5, 0x20008, 31, 5, 0x20458, 8, 8, -1, 0x20430, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => new VsIntroSites(0x4A010, 36, 5, 0x21014, 31, 5, 0x21434, 8, 8, -1, 0x2140C, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
                GameLanguages.Japanese when revision == 5 => new VsIntroSites(0x4A010, 36, 5, 0x21014, 31, 5, 0x21434, 8, 8, -1, 0x2140C, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
                GameLanguages.Japanese when revision == 6 => new VsIntroSites(0x4A0C8, 36, 5, 0x21020, 31, 5, 0x21440, 8, 8, -1, 0x21418, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
                GameLanguages.French => new VsIntroSites(0x47644, 36, 5, 0x1FFC0, 31, 5, 0x20410, 8, 8, -1, 0x203E8, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
                GameLanguages.German => new VsIntroSites(0x47644, 36, 5, 0x1FFC0, 31, 5, 0x20410, 8, 8, -1, 0x203E8, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
                GameLanguages.Italian => new VsIntroSites(0x47644, 36, 5, 0x1FFC0, 31, 5, 0x20410, 8, 8, -1, 0x203E8, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
                GameLanguages.Spanish => new VsIntroSites(0x47644, 36, 5, 0x1FFC0, 31, 5, 0x20410, 8, 8, -1, 0x203E8, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
                GameLanguages.Korean => new VsIntroSites(0x47AA0, 36, 5, 0x1FFBC, 31, 5, 0x2040C, 8, 8, -1, 0x203E4, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => new VsIntroSites(0x51C34, 41, 5, 0x28CF4, 31, 5, 0x28FB4, 8, 20, -1, 0x28F8C, -1, -1, -1, -1, new[] { 0x14D94, 0x15098 }, new[] { 0x14F28, 0x150B4 }, 51, 40, 147, 151, 11, -1, -1),   // Europe
                GameLanguages.English => new VsIntroSites(0x51C34, 41, 5, 0x28CF4, 31, 5, 0x28FB4, 8, 20, -1, 0x28F8C, -1, -1, -1, -1, new[] { 0x14D94, 0x15098 }, new[] { 0x14F28, 0x150B4 }, 51, 40, 147, 151, 11, -1, -1),   // US, and any English revision not listed
                GameLanguages.Japanese => new VsIntroSites(0x51514, 41, 5, 0x28AF4, 31, 5, 0x28DA4, 8, 20, -1, 0x28D7C, -1, -1, -1, -1, new[] { 0x14CB0, 0x14FB4 }, new[] { 0x14E44, 0x14FD0 }, 51, 40, 147, 151, 11, -1, -1),
                GameLanguages.French => new VsIntroSites(0x51CD8, 41, 5, 0x28CFC, 31, 5, 0x28FBC, 8, 20, -1, 0x28F94, -1, -1, -1, -1, new[] { 0x14DE4, 0x150E8 }, new[] { 0x14F78, 0x15104 }, 51, 40, 147, 151, 11, -1, -1),
                GameLanguages.German => new VsIntroSites(0x51CD8, 41, 5, 0x28D10, 31, 5, 0x28FD0, 8, 20, -1, 0x28FA8, -1, -1, -1, -1, new[] { 0x14DF8, 0x150FC }, new[] { 0x14F8C, 0x15118 }, 51, 40, 147, 151, 11, -1, -1),
                GameLanguages.Italian => new VsIntroSites(0x51CD8, 41, 5, 0x28D04, 31, 5, 0x28FC4, 8, 20, -1, 0x28F9C, -1, -1, -1, -1, new[] { 0x14DEC, 0x150F0 }, new[] { 0x14F80, 0x1510C }, 51, 40, 147, 151, 11, -1, -1),
                GameLanguages.Spanish => new VsIntroSites(0x51CD8, 41, 5, 0x28CFC, 31, 5, 0x28FBC, 8, 20, -1, 0x28F94, -1, -1, -1, -1, new[] { 0x14DE4, 0x150E8 }, new[] { 0x14F78, 0x15104 }, 51, 40, 147, 151, 11, -1, -1),
                GameLanguages.Korean => new VsIntroSites(0x52128, 41, 5, 0x28CEC, 31, 5, 0x28FAC, 8, 20, -1, 0x28F84, -1, -1, -1, -1, new[] { 0x14DD8, 0x150DC }, new[] { 0x14F6C, 0x150F8 }, 51, 40, 147, 151, 11, -1, -1),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => new VsIntroSites(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x517E4, 0x517D4, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),   // Europe
                GameLanguages.English => new VsIntroSites(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x517E4, 0x517D4, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),   // US, and any English revision not listed
                GameLanguages.Japanese => new VsIntroSites(-1, 0, 1, 0x20E48, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x5127C, 0x5126C, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),
                GameLanguages.French => new VsIntroSites(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x517E4, 0x517D4, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new VsIntroSites(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x517E4, 0x517D4, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),
                GameLanguages.Italian => new VsIntroSites(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x517E4, 0x517D4, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),
                GameLanguages.Spanish => new VsIntroSites(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x517DC, 0x517CC, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => new VsIntroSites(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x518E0, 0x518D0, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => new VsIntroSites(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x517E4, 0x517D4, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),   // Europe
                GameLanguages.English => new VsIntroSites(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x517E4, 0x517D4, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),   // US, and any English revision not listed
                GameLanguages.Japanese => new VsIntroSites(-1, 0, 1, 0x20E48, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x5127C, 0x5126C, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),
                GameLanguages.French => new VsIntroSites(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x517E4, 0x517D4, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new VsIntroSites(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x517E4, 0x517D4, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),
                GameLanguages.Italian => new VsIntroSites(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x517E4, 0x517D4, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),
                GameLanguages.Spanish => new VsIntroSites(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x517E4, 0x517D4, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),
                GameLanguages.Korean => new VsIntroSites(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x518D8, 0x518C8, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),
                _ => null,
            },
            _ => null,
        };

        public static VsIntroMotionSites VsIntroMotionSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => new VsIntroMotionSites(5, 0x20558, 5, 0x1FF7C, new[] { 0, 2, 4, 3 }, null),   // Europe
                GameLanguages.English => new VsIntroMotionSites(5, 0x20558, 5, 0x1FF7C, new[] { 0, 2, 4, 3 }, null),   // US, and any English revision not listed
                GameLanguages.Japanese => new VsIntroMotionSites(5, 0x21534, 5, 0x20F88, new[] { 0, 2, 4, 3 }, null),
                GameLanguages.French => new VsIntroMotionSites(5, 0x20510, 5, 0x1FF34, new[] { 0, 2, 4, 3 }, null),
                GameLanguages.German => new VsIntroMotionSites(5, 0x20510, 5, 0x1FF34, new[] { 0, 2, 4, 3 }, null),
                GameLanguages.Italian => new VsIntroMotionSites(5, 0x20510, 5, 0x1FF34, new[] { 0, 2, 4, 3 }, null),
                GameLanguages.Spanish => new VsIntroMotionSites(5, 0x20510, 5, 0x1FF34, new[] { 0, 2, 4, 3 }, null),
                GameLanguages.Korean => new VsIntroMotionSites(5, 0x2050C, 5, 0x1FF30, new[] { 0, 2, 4, 3 }, null),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => new VsIntroMotionSites(5, 0x20558, 5, 0x1FF7C, new[] { 0, 2, 4, 3 }, null),   // Europe
                GameLanguages.English => new VsIntroMotionSites(5, 0x20558, 5, 0x1FF7C, new[] { 0, 2, 4, 3 }, null),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => new VsIntroMotionSites(5, 0x21534, 5, 0x20F88, new[] { 0, 2, 4, 3 }, null),
                GameLanguages.Japanese when revision == 5 => new VsIntroMotionSites(5, 0x21534, 5, 0x20F88, new[] { 0, 2, 4, 3 }, null),
                GameLanguages.Japanese when revision == 6 => new VsIntroMotionSites(5, 0x21540, 5, 0x20F94, new[] { 0, 2, 4, 3 }, null),
                GameLanguages.French => new VsIntroMotionSites(5, 0x20510, 5, 0x1FF34, new[] { 0, 2, 4, 3 }, null),
                GameLanguages.German => new VsIntroMotionSites(5, 0x20510, 5, 0x1FF34, new[] { 0, 2, 4, 3 }, null),
                GameLanguages.Italian => new VsIntroMotionSites(5, 0x20510, 5, 0x1FF34, new[] { 0, 2, 4, 3 }, null),
                GameLanguages.Spanish => new VsIntroMotionSites(5, 0x20510, 5, 0x1FF34, new[] { 0, 2, 4, 3 }, null),
                GameLanguages.Korean => new VsIntroMotionSites(5, 0x2050C, 5, 0x1FF30, new[] { 0, 2, 4, 3 }, null),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => new VsIntroMotionSites(5, 0x29114, 5, 0x28C08, new[] { 0, 2, 4, 3 }, new[] { 107, 108 }),   // Europe
                GameLanguages.English => new VsIntroMotionSites(5, 0x29114, 5, 0x28C08, new[] { 0, 2, 4, 3 }, new[] { 107, 108 }),   // US, and any English revision not listed
                GameLanguages.Japanese => new VsIntroMotionSites(5, 0x28F04, 5, 0x28A08, new[] { 0, 2, 4, 3 }, new[] { 107, 108 }),
                GameLanguages.French => new VsIntroMotionSites(5, 0x2911C, 5, 0x28C10, new[] { 0, 2, 4, 3 }, new[] { 107, 108 }),
                GameLanguages.German => new VsIntroMotionSites(5, 0x29130, 5, 0x28C24, new[] { 0, 2, 4, 3 }, new[] { 107, 108 }),
                GameLanguages.Italian => new VsIntroMotionSites(5, 0x29124, 5, 0x28C18, new[] { 0, 2, 4, 3 }, new[] { 107, 108 }),
                GameLanguages.Spanish => new VsIntroMotionSites(5, 0x2911C, 5, 0x28C10, new[] { 0, 2, 4, 3 }, new[] { 107, 108 }),
                GameLanguages.Korean => new VsIntroMotionSites(5, 0x2910C, 5, 0x28C00, new[] { 0, 2, 4, 3 }, new[] { 107, 108 }),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => new VsIntroMotionSites(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),   // Europe
                GameLanguages.English => new VsIntroMotionSites(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),   // US, and any English revision not listed
                GameLanguages.Japanese => new VsIntroMotionSites(117, 0xAD4, 1, 0x20F04, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),
                GameLanguages.French => new VsIntroMotionSites(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new VsIntroMotionSites(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),
                GameLanguages.Italian => new VsIntroMotionSites(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),
                GameLanguages.Spanish => new VsIntroMotionSites(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => new VsIntroMotionSites(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => new VsIntroMotionSites(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),   // Europe
                GameLanguages.English => new VsIntroMotionSites(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),   // US, and any English revision not listed
                GameLanguages.Japanese => new VsIntroMotionSites(117, 0xAD4, 1, 0x20F04, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),
                GameLanguages.French => new VsIntroMotionSites(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new VsIntroMotionSites(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),
                GameLanguages.Italian => new VsIntroMotionSites(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),
                GameLanguages.Spanish => new VsIntroMotionSites(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),
                GameLanguages.Korean => new VsIntroMotionSites(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),
                _ => null,
            },
            _ => null,
        };

        /// <summary>
        /// Where BDHCam branches and hooks, for the dumps it has a routine for. Every dump of these games keeps the hooks
        /// in its own place; the routine is built for these four.
        /// </summary>
        public static BdhcamSites BdhcamSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => new BdhcamSites(0x2040C, 5, 0x1E1B4, 0x1E2CC),   // Europe
                GameLanguages.English => new BdhcamSites(0x2040C, 5, 0x1E1B4, 0x1E2CC),   // US, and any English revision not listed
                GameLanguages.Spanish => new BdhcamSites(0x2047C, 5, 0x1E1BC, 0x1E2D4),
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => new BdhcamSites(0x23174, 1, 0x1574C, 0x15864),   // Europe
                GameLanguages.English => new BdhcamSites(0x23174, 1, 0x1574C, 0x15864),   // US, and any English revision not listed
                GameLanguages.Spanish => new BdhcamSites(0x23174, 1, 0x1574C, 0x15864),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => new BdhcamSites(0x23174, 1, 0x1574C, 0x15864),   // Europe
                GameLanguages.English => new BdhcamSites(0x23174, 1, 0x1574C, 0x15864),   // US, and any English revision not listed
                GameLanguages.Spanish => new BdhcamSites(0x23174, 1, 0x1574C, 0x15864),
                _ => null,
            },
            _ => null,
        };

        private static readonly SummaryScreenSites PlatinumSummary = new(
            new[]
            {
                new SummaryWindowTableSite("Static", 0xF4ED0, 36),
                new SummaryWindowTableSite("Info", 0xF4E20, 6),
                new SummaryWindowTableSite("Memo", 0xF4DB0, 1),
                new SummaryWindowTableSite("Skills", 0xF4E50, 8),
                new SummaryWindowTableSite("BattleMoves", 0xF4E90, 8),
                new SummaryWindowTableSite("ContestMoves", 0xF4DF0, 6),
                new SummaryWindowTableSite("Ribbons", 0xF4DD0, 4),
                new SummaryWindowTableSite("Condition", 0xF4DB8, 3),
            },
            new SummaryBarSite(0x8DA46, 0x8DA38, 6, 3, 0xA0C8),
            new SummaryBarSite(0x8DAD8, 0x8DABA, 7, 3, 0x00B4),
            455, 612, 210);

        // The HGSS window tables and the limits on moving them were worked out by Mixone in Ghidra.
        private static readonly SummaryScreenSites HgssSummary = new(
            new[]
            {
                new SummaryWindowTableSite("Static", 0x104D94, 34),
                new SummaryWindowTableSite("Info", 0x104CC4, 8),
                new SummaryWindowTableSite("Skills", 0x104D04, 18),
                new SummaryWindowTableSite("Ribbons", 0x104C84, 8),
            },
            new SummaryBarSite(0x8A160, 0x8A152, 6, 3, 0xF09F),
            new SummaryBarSite(0x8A1F4, 0x8A1D6, 7, 6, 0xE03F),
            302, 722, -1);

        // pokeheartgold asm/unk_0208C3E4.s and unk_02088288.s.
        private static readonly SummaryFontSites HgssSummaryFonts = new(
            new uint[]
            {
                0x0208C5A6, 0x0208C698, 0x0208C7EC, 0x0208C926, 0x0208C966, 0x0208C994, 0x0208CB24, 0x0208CB50, 0x0208CE56, 0x0208CE90,
                0x0208CEC0, 0x0208CFE4, 0x0208D006, 0x0208D028, 0x0208D04A, 0x0208D15A, 0x0208D3F0, 0x0208D5AA, 0x0208D5DA, 0x0208D60A,
                0x0208D63A, 0x0208D66A, 0x0208D7A0, 0x0208D8F0, 0x0208D920, 0x0208D98C, 0x0208DCB2, 0x0208DD6A, 0x0208DE26,
            },
            new uint[] { 0x0208C79C, 0x0208C7BE },
            new uint[] { 0x0208C8F0, 0x0208C944 },
            new uint[] { 0x0208D96A },
            0x0208CE36,
            new uint[] { 0x0208CE6E, 0x0208CEA0 },
            0x02088386, 0x020885AC);

        /// <summary>The summary screen's font calls, measured on the English dumps.</summary>
        public static SummaryFontSites SummaryFontSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold or GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => HgssSummaryFonts,   // Europe
                GameLanguages.English => HgssSummaryFonts,   // US, and any English revision not listed
                _ => null,
            },
            _ => null,
        };

        /// <summary>The summary screen's window tables and bars (arm9 file offsets), measured on the English dumps.</summary>
        public static SummaryScreenSites SummaryScreenSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => PlatinumSummary,   // Europe
                GameLanguages.English => PlatinumSummary,   // US, and any English revision not listed
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => HgssSummary,   // Europe
                GameLanguages.English => HgssSummary,   // US, and any English revision not listed
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => HgssSummary,   // Europe
                GameLanguages.English => HgssSummary,   // US, and any English revision not listed
                _ => null,
            },
            _ => null,
        };
    }
}
