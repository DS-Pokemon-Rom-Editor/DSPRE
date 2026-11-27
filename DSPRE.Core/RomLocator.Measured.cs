// Generated from the measured rows RomInfo kept before the locator; every supported game and language is written
// out. Each value was found at exactly one place by comparing the dump with the US dump of the same game, and
// RomLocatorTests checks each site against the dump it names.
using static DSPRE.RomInfo;

namespace DSPRE
{
    public static partial class RomLocator
    {
        public static Arm9ExpansionSites Arm9ExpansionSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => new Arm9ExpansionSites(0xC80, 0x106B3C, 0x20064F0),   // Europe
                GameLanguages.English => new Arm9ExpansionSites(0xC80, 0x106B3C, 0x20064F0),   // US, and any English revision not listed
                GameLanguages.Japanese => new Arm9ExpansionSites(0xC6E, 0x108418, 0x2006978),
                GameLanguages.French => new Arm9ExpansionSites(0xC80, 0x106CBC, 0x20064F0),
                GameLanguages.German => new Arm9ExpansionSites(0xC80, 0x106C7C, 0x20064F0),
                GameLanguages.Italian => new Arm9ExpansionSites(0xC80, 0x106C1C, 0x20064F0),
                GameLanguages.Spanish => new Arm9ExpansionSites(0xC80, 0x106CDC, 0x20064F0),
                GameLanguages.Korean => new Arm9ExpansionSites(0xCAC, 0x104144, 0x2006518),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => new Arm9ExpansionSites(0xC80, 0x106B44, 0x20064F0),   // Europe
                GameLanguages.English => new Arm9ExpansionSites(0xC80, 0x106B44, 0x20064F0),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => new Arm9ExpansionSites(0xC6E, 0x108420, 0x2006978),
                GameLanguages.Japanese when revision == 5 => new Arm9ExpansionSites(0xC6E, 0x108420, 0x2006978),
                GameLanguages.Japanese when revision == 6 => new Arm9ExpansionSites(0xC6E, 0x108560, 0x2006978),
                GameLanguages.French => new Arm9ExpansionSites(0xC80, 0x106CC4, 0x20064F0),
                GameLanguages.German => new Arm9ExpansionSites(0xC80, 0x106C84, 0x20064F0),
                GameLanguages.Italian => new Arm9ExpansionSites(0xC80, 0x106C24, 0x20064F0),
                GameLanguages.Spanish => new Arm9ExpansionSites(0xC80, 0x106CE4, 0x20064F0),
                GameLanguages.Korean => new Arm9ExpansionSites(0xCAC, 0x10414C, 0x2006518),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => new Arm9ExpansionSites(0xCB4, 0x101574, 0x2006AA4),   // Europe
                GameLanguages.English => new Arm9ExpansionSites(0xCB4, 0x101574, 0x2006AA4),   // US, and any English revision not listed
                GameLanguages.Japanese => new Arm9ExpansionSites(0xCB4, 0x100968, 0x20069E4),
                GameLanguages.French => new Arm9ExpansionSites(0xCB4, 0x101750, 0x2006AB8),
                GameLanguages.German => new Arm9ExpansionSites(0xCB4, 0x101710, 0x2006AB8),
                GameLanguages.Italian => new Arm9ExpansionSites(0xCB4, 0x1016D0, 0x2006AB8),
                GameLanguages.Spanish => new Arm9ExpansionSites(0xCB4, 0x101770, 0x2006AB8),
                GameLanguages.Korean => new Arm9ExpansionSites(0xCB4, 0x102464, 0x2006AB8),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => new Arm9ExpansionSites(0xCD0, 0x110334, 0x2007508),   // Europe
                GameLanguages.English => new Arm9ExpansionSites(0xCD0, 0x110334, 0x2007508),   // US, and any English revision not listed
                GameLanguages.Japanese => new Arm9ExpansionSites(0xCB4, 0x110604, 0x2007420),
                GameLanguages.French => new Arm9ExpansionSites(0xCD0, 0x1110F0, 0x2007508),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new Arm9ExpansionSites(0xCD0, 0x1110B0, 0x2007508),
                GameLanguages.Italian => new Arm9ExpansionSites(0xCD0, 0x111070, 0x2007508),
                GameLanguages.Spanish => new Arm9ExpansionSites(0xCD0, 0x110354, 0x2007508),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => new Arm9ExpansionSites(0xCD0, 0x111AC4, 0x20074D8),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => new Arm9ExpansionSites(0xCD0, 0x110334, 0x2007508),   // Europe
                GameLanguages.English => new Arm9ExpansionSites(0xCD0, 0x110334, 0x2007508),   // US, and any English revision not listed
                GameLanguages.Japanese => new Arm9ExpansionSites(0xCB4, 0x110604, 0x2007420),
                GameLanguages.French => new Arm9ExpansionSites(0xCD0, 0x1110F0, 0x2007508),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new Arm9ExpansionSites(0xCD0, 0x1110B0, 0x2007508),
                GameLanguages.Italian => new Arm9ExpansionSites(0xCD0, 0x111070, 0x2007508),
                GameLanguages.Spanish => new Arm9ExpansionSites(0xCD0, 0x111110, 0x2007508),
                GameLanguages.Korean => new Arm9ExpansionSites(0xCD0, 0x111AC4, 0x20074D8),
                _ => null,
            },
            _ => null,
        };

        public static PickupSlotSites PickupSlotSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => new PickupSlotSites(0xBCDE, 2, 0xBDB0, 0xBDB4),   // Europe
                GameLanguages.English => new PickupSlotSites(0xBCDE, 2, 0xBDB0, 0xBDB4),   // US, and any English revision not listed
                GameLanguages.Japanese => new PickupSlotSites(0x9326, 3, 0x93FC, 0x9400),
                GameLanguages.French => new PickupSlotSites(0xBDE6, 2, 0xBEB8, 0xBEBC),
                GameLanguages.German => new PickupSlotSites(0xBCFA, 2, 0xBDCC, 0xBDD0),
                GameLanguages.Italian => new PickupSlotSites(0xBCFA, 2, 0xBDCC, 0xBDD0),
                GameLanguages.Spanish => new PickupSlotSites(0xBDE6, 2, 0xBEB8, 0xBEBC),
                GameLanguages.Korean => new PickupSlotSites(0xBCFA, 2, 0xBDCC, 0xBDD0),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => new PickupSlotSites(0xBCDE, 2, 0xBDB0, 0xBDB4),   // Europe
                GameLanguages.English => new PickupSlotSites(0xBCDE, 2, 0xBDB0, 0xBDB4),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => new PickupSlotSites(0x9326, 3, 0x93FC, 0x9400),
                GameLanguages.Japanese when revision == 5 => new PickupSlotSites(0x9326, 3, 0x93FC, 0x9400),
                GameLanguages.Japanese when revision == 6 => new PickupSlotSites(0x9326, 3, 0x93FC, 0x9400),
                GameLanguages.French => new PickupSlotSites(0xBDE6, 2, 0xBEB8, 0xBEBC),
                GameLanguages.German => new PickupSlotSites(0xBCFA, 2, 0xBDCC, 0xBDD0),
                GameLanguages.Italian => new PickupSlotSites(0xBCFA, 2, 0xBDCC, 0xBDD0),
                GameLanguages.Spanish => new PickupSlotSites(0xBDE6, 2, 0xBEB8, 0xBEBC),
                GameLanguages.Korean => new PickupSlotSites(0xBCFA, 2, 0xBDCC, 0xBDD0),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => new PickupSlotSites(0xC658, 3, 0xC730, 0xC734),   // Europe
                GameLanguages.English => new PickupSlotSites(0xC658, 3, 0xC730, 0xC734),   // US, and any English revision not listed
                GameLanguages.Japanese => new PickupSlotSites(0xC650, 3, 0xC728, 0xC72C),
                GameLanguages.French => new PickupSlotSites(0xC760, 3, 0xC838, 0xC83C),
                GameLanguages.German => new PickupSlotSites(0xC674, 3, 0xC74C, 0xC750),
                GameLanguages.Italian => new PickupSlotSites(0xC674, 3, 0xC74C, 0xC750),
                GameLanguages.Spanish => new PickupSlotSites(0xC760, 3, 0xC838, 0xC83C),
                GameLanguages.Korean => new PickupSlotSites(0xC670, 3, 0xC748, 0xC74C),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => new PickupSlotSites(0xC880, 3, 0xC958, 0xC95C),   // Europe
                GameLanguages.English => new PickupSlotSites(0xC880, 3, 0xC958, 0xC95C),   // US, and any English revision not listed
                GameLanguages.Japanese => new PickupSlotSites(0xC888, 3, 0xC960, 0xC964),
                GameLanguages.French => new PickupSlotSites(0xC880, 3, 0xC958, 0xC95C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new PickupSlotSites(0xC880, 3, 0xC958, 0xC95C),
                GameLanguages.Italian => new PickupSlotSites(0xC880, 3, 0xC958, 0xC95C),
                GameLanguages.Spanish => new PickupSlotSites(0xC880, 3, 0xC958, 0xC95C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => new PickupSlotSites(0xC884, 3, 0xC95C, 0xC960),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => new PickupSlotSites(0xC880, 3, 0xC958, 0xC95C),   // Europe
                GameLanguages.English => new PickupSlotSites(0xC880, 3, 0xC958, 0xC95C),   // US, and any English revision not listed
                GameLanguages.Japanese => new PickupSlotSites(0xC888, 3, 0xC960, 0xC964),
                GameLanguages.French => new PickupSlotSites(0xC880, 3, 0xC958, 0xC95C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new PickupSlotSites(0xC880, 3, 0xC958, 0xC95C),
                GameLanguages.Italian => new PickupSlotSites(0xC880, 3, 0xC958, 0xC95C),
                GameLanguages.Spanish => new PickupSlotSites(0xC880, 3, 0xC958, 0xC95C),
                GameLanguages.Korean => new PickupSlotSites(0xC884, 3, 0xC95C, 0xC960),
                _ => null,
            },
            _ => null,
        };

        public static MiningCodeSites MiningCodeSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => new MiningCodeSites(0x18F2, 0x18FC, 0x190C, 0x1AEA, 0x1820, 0x1824, 0x1826, 0x182A),   // Europe
                GameLanguages.English => new MiningCodeSites(0x18F2, 0x18FC, 0x190C, 0x1AEA, 0x1820, 0x1824, 0x1826, 0x182A),   // US, and any English revision not listed
                GameLanguages.Japanese => new MiningCodeSites(0x1A8A, 0x1A92, 0x1AA2, 0x1CA6, 0x1D76, 0x1D7A, 0x1D7C, 0x1D80),
                GameLanguages.French => new MiningCodeSites(0x191A, 0x1924, 0x1934, 0x1B12, 0x1848, 0x184C, 0x184E, 0x1852),
                GameLanguages.German => new MiningCodeSites(0x191A, 0x1924, 0x1934, 0x1B12, 0x1848, 0x184C, 0x184E, 0x1852),
                GameLanguages.Italian => new MiningCodeSites(0x191A, 0x1924, 0x1934, 0x1B12, 0x1848, 0x184C, 0x184E, 0x1852),
                GameLanguages.Spanish => new MiningCodeSites(0x191A, 0x1924, 0x1934, 0x1B12, 0x1848, 0x184C, 0x184E, 0x1852),
                GameLanguages.Korean => new MiningCodeSites(0x191A, 0x1924, 0x1934, 0x1B12, 0x1848, 0x184C, 0x184E, 0x1852),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => new MiningCodeSites(0x18F2, 0x18FC, 0x190C, 0x1AEA, 0x1820, 0x1824, 0x1826, 0x182A),   // Europe
                GameLanguages.English => new MiningCodeSites(0x18F2, 0x18FC, 0x190C, 0x1AEA, 0x1820, 0x1824, 0x1826, 0x182A),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => new MiningCodeSites(0x1A8A, 0x1A92, 0x1AA2, 0x1CA6, 0x1D76, 0x1D7A, 0x1D7C, 0x1D80),
                GameLanguages.Japanese when revision == 5 => new MiningCodeSites(0x1A8A, 0x1A92, 0x1AA2, 0x1CA6, 0x1D76, 0x1D7A, 0x1D7C, 0x1D80),
                GameLanguages.Japanese when revision == 6 => new MiningCodeSites(0x1A8A, 0x1A92, 0x1AA2, 0x1CA6, 0x1D76, 0x1D7A, 0x1D7C, 0x1D80),
                GameLanguages.French => new MiningCodeSites(0x191A, 0x1924, 0x1934, 0x1B12, 0x1848, 0x184C, 0x184E, 0x1852),
                GameLanguages.German => new MiningCodeSites(0x191A, 0x1924, 0x1934, 0x1B12, 0x1848, 0x184C, 0x184E, 0x1852),
                GameLanguages.Italian => new MiningCodeSites(0x191A, 0x1924, 0x1934, 0x1B12, 0x1848, 0x184C, 0x184E, 0x1852),
                GameLanguages.Spanish => new MiningCodeSites(0x191A, 0x1924, 0x1934, 0x1B12, 0x1848, 0x184C, 0x184E, 0x1852),
                GameLanguages.Korean => new MiningCodeSites(0x191A, 0x1924, 0x1934, 0x1B12, 0x1848, 0x184C, 0x184E, 0x1852),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => new MiningCodeSites(0x1946, 0x1950, 0x1960, 0x1B3E, 0x0, 0x0, 0x0, 0x0),   // Europe
                GameLanguages.English => new MiningCodeSites(0x1946, 0x1950, 0x1960, 0x1B3E, 0x0, 0x0, 0x0, 0x0),   // US, and any English revision not listed
                GameLanguages.Japanese => new MiningCodeSites(0x1946, 0x1950, 0x1960, 0x1B3E, 0x0, 0x0, 0x0, 0x0),
                GameLanguages.French => new MiningCodeSites(0x1946, 0x1950, 0x1960, 0x1B3E, 0x0, 0x0, 0x0, 0x0),
                GameLanguages.German => new MiningCodeSites(0x1946, 0x1950, 0x1960, 0x1B3E, 0x0, 0x0, 0x0, 0x0),
                GameLanguages.Italian => new MiningCodeSites(0x1946, 0x1950, 0x1960, 0x1B3E, 0x0, 0x0, 0x0, 0x0),
                GameLanguages.Spanish => new MiningCodeSites(0x1946, 0x1950, 0x1960, 0x1B3E, 0x0, 0x0, 0x0, 0x0),
                GameLanguages.Korean => new MiningCodeSites(0x1946, 0x1950, 0x1960, 0x1B3E, 0x0, 0x0, 0x0, 0x0),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            _ => null,
        };

        public static TrainerShinySites TrainerShinySitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => new TrainerShinySites(0x73A84, 0x73AA2, new uint[] { 0x2074640, 0x2074644, 0x206E540, 0x206E540, 0x206EC40, 0x201AB0C }),   // Europe
                GameLanguages.English => new TrainerShinySites(0x73A84, 0x73AA2, new uint[] { 0x2074640, 0x2074644, 0x206E540, 0x206E540, 0x206EC40, 0x201AB0C }),   // US, and any English revision not listed
                GameLanguages.Japanese => new TrainerShinySites(0x7351C, 0x7353A, new uint[] { 0x20740D8, 0x20740DC, 0x206DF80, 0x206DF80, 0x206E680, 0x201A7C0 }),
                GameLanguages.French => new TrainerShinySites(0x73A84, 0x73AA2, new uint[] { 0x2074640, 0x2074644, 0x206E540, 0x206E540, 0x206EC40, 0x201AB0C }),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new TrainerShinySites(0x73A84, 0x73AA2, new uint[] { 0x2074640, 0x2074644, 0x206E540, 0x206E540, 0x206EC40, 0x201AB0C }),
                GameLanguages.Italian => new TrainerShinySites(0x73A84, 0x73AA2, new uint[] { 0x2074640, 0x2074644, 0x206E540, 0x206E540, 0x206EC40, 0x201AB0C }),
                GameLanguages.Spanish => new TrainerShinySites(0x73A7C, 0x73A9A, new uint[] { 0x2074638, 0x207463C, 0x206E538, 0x206E538, 0x206EC38, 0x201AB0C }),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => new TrainerShinySites(0x73B84, 0x73BA2, new uint[] { 0x2074740, 0x2074744, 0x206E63C, 0x206E63C, 0x206ED3C, 0x201AAA4 }),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => new TrainerShinySites(0x73A84, 0x73AA2, new uint[] { 0x2074640, 0x2074644, 0x206E540, 0x206E540, 0x206EC40, 0x201AB0C }),   // Europe
                GameLanguages.English => new TrainerShinySites(0x73A84, 0x73AA2, new uint[] { 0x2074640, 0x2074644, 0x206E540, 0x206E540, 0x206EC40, 0x201AB0C }),   // US, and any English revision not listed
                GameLanguages.Japanese => new TrainerShinySites(0x7351C, 0x7353A, new uint[] { 0x20740D8, 0x20740DC, 0x206DF80, 0x206DF80, 0x206E680, 0x201A7C0 }),
                GameLanguages.French => new TrainerShinySites(0x73A84, 0x73AA2, new uint[] { 0x2074640, 0x2074644, 0x206E540, 0x206E540, 0x206EC40, 0x201AB0C }),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new TrainerShinySites(0x73A84, 0x73AA2, new uint[] { 0x2074640, 0x2074644, 0x206E540, 0x206E540, 0x206EC40, 0x201AB0C }),
                GameLanguages.Italian => new TrainerShinySites(0x73A84, 0x73AA2, new uint[] { 0x2074640, 0x2074644, 0x206E540, 0x206E540, 0x206EC40, 0x201AB0C }),
                GameLanguages.Spanish => new TrainerShinySites(0x73A84, 0x73AA2, new uint[] { 0x2074640, 0x2074644, 0x206E540, 0x206E540, 0x206EC40, 0x201AB0C }),
                GameLanguages.Korean => new TrainerShinySites(0x73B7C, 0x73B9A, new uint[] { 0x2074738, 0x207473C, 0x206E634, 0x206E634, 0x206ED34, 0x201AAA4 }),
                _ => null,
            },
            _ => null,
        };

        public static BuildingRotationSites BuildingRotationSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => new BuildingRotationSites(5, 0x1097E, 0x201CAA8),   // Europe
                GameLanguages.English => new BuildingRotationSites(5, 0x1097E, 0x201CAA8),   // US, and any English revision not listed
                GameLanguages.Japanese => new BuildingRotationSites(5, 0x1128C, 0x201D640),
                GameLanguages.French => new BuildingRotationSites(5, 0x1097E, 0x201CAF4),
                GameLanguages.German => new BuildingRotationSites(5, 0x1097E, 0x201CAF4),
                GameLanguages.Italian => new BuildingRotationSites(5, 0x1097E, 0x201CAF4),
                GameLanguages.Spanish => new BuildingRotationSites(5, 0x1097E, 0x201CAF4),
                GameLanguages.Korean => new BuildingRotationSites(5, 0x1097E, 0x201CC00),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => new BuildingRotationSites(5, 0x1097E, 0x201CAA8),   // Europe
                GameLanguages.English => new BuildingRotationSites(5, 0x1097E, 0x201CAA8),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => new BuildingRotationSites(5, 0x1128C, 0x201D640),
                GameLanguages.Japanese when revision == 5 => new BuildingRotationSites(5, 0x1128C, 0x201D640),
                GameLanguages.Japanese when revision == 6 => new BuildingRotationSites(5, 0x11298, 0x201D664),
                GameLanguages.French => new BuildingRotationSites(5, 0x1097E, 0x201CAF4),
                GameLanguages.German => new BuildingRotationSites(5, 0x1097E, 0x201CAF4),
                GameLanguages.Italian => new BuildingRotationSites(5, 0x1097E, 0x201CAF4),
                GameLanguages.Spanish => new BuildingRotationSites(5, 0x1097E, 0x201CAF4),
                GameLanguages.Korean => new BuildingRotationSites(5, 0x1097E, 0x201CC00),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => new BuildingRotationSites(5, 0x10AD2, 0x201E268),   // Europe
                GameLanguages.English => new BuildingRotationSites(5, 0x10AD2, 0x201E268),   // US, and any English revision not listed
                GameLanguages.Japanese => new BuildingRotationSites(5, 0x10A16, 0x201DEFC),
                GameLanguages.French => new BuildingRotationSites(5, 0x10B22, 0x201E2D8),
                GameLanguages.German => new BuildingRotationSites(5, 0x10B36, 0x201E2D8),
                GameLanguages.Italian => new BuildingRotationSites(5, 0x10B22, 0x201E2D8),
                GameLanguages.Spanish => new BuildingRotationSites(5, 0x10B22, 0x201E2D8),
                GameLanguages.Korean => new BuildingRotationSites(5, 0x10B16, 0x201E3FC),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => new BuildingRotationSites(1, 0xE1C6, 0x2020D2C),   // Europe
                GameLanguages.English => new BuildingRotationSites(1, 0xE1C6, 0x2020D2C),   // US, and any English revision not listed
                GameLanguages.Japanese => new BuildingRotationSites(1, 0xE07E, 0x20209CC),
                GameLanguages.French => new BuildingRotationSites(1, 0xE1C6, 0x2020D2C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new BuildingRotationSites(1, 0xE1C6, 0x2020D2C),
                GameLanguages.Italian => new BuildingRotationSites(1, 0xE1C6, 0x2020D2C),
                GameLanguages.Spanish => new BuildingRotationSites(1, 0xE1C6, 0x2020D2C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => new BuildingRotationSites(1, 0xE1C6, 0x2020CF8),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => new BuildingRotationSites(1, 0xE1C6, 0x2020D2C),   // Europe
                GameLanguages.English => new BuildingRotationSites(1, 0xE1C6, 0x2020D2C),   // US, and any English revision not listed
                GameLanguages.Japanese => new BuildingRotationSites(1, 0xE07E, 0x20209CC),
                GameLanguages.French => new BuildingRotationSites(1, 0xE1C6, 0x2020D2C),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new BuildingRotationSites(1, 0xE1C6, 0x2020D2C),
                GameLanguages.Italian => new BuildingRotationSites(1, 0xE1C6, 0x2020D2C),
                GameLanguages.Spanish => new BuildingRotationSites(1, 0xE1C6, 0x2020D2C),
                GameLanguages.Korean => new BuildingRotationSites(1, 0xE1C6, 0x2020CF8),
                _ => null,
            },
            _ => null,
        };

        public static TrainerPrizeTable TrainerPrizeTableIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Diamond => language switch
            {
                GameLanguages.English when revision == 13 => new TrainerPrizeTable(11, 0, 0x32960, 0x62, false),   // Europe
                GameLanguages.English => new TrainerPrizeTable(11, 0, 0x32960, 0x62, false),   // US, and any English revision not listed
                GameLanguages.Japanese => new TrainerPrizeTable(11, 0, 0x35060, 0x62, false),
                GameLanguages.French => new TrainerPrizeTable(11, 0, 0x32AE0, 0x62, false),
                GameLanguages.German => new TrainerPrizeTable(11, 0, 0x329E0, 0x62, false),
                GameLanguages.Italian => new TrainerPrizeTable(11, 0, 0x329E0, 0x62, false),
                GameLanguages.Spanish => new TrainerPrizeTable(11, 0, 0x32AE0, 0x62, false),
                GameLanguages.Korean => new TrainerPrizeTable(11, 0, 0x3A0A0, 0x62, false),
                _ => null,
            },
            GameVersions.Pearl => language switch
            {
                GameLanguages.English when revision == 13 => new TrainerPrizeTable(11, 0, 0x32960, 0x62, false),   // Europe
                GameLanguages.English => new TrainerPrizeTable(11, 0, 0x32960, 0x62, false),   // US, and any English revision not listed
                GameLanguages.Japanese when revision == 0 => new TrainerPrizeTable(11, 0, 0x35060, 0x62, false),
                GameLanguages.Japanese when revision == 5 => new TrainerPrizeTable(11, 0, 0x35060, 0x62, false),
                GameLanguages.Japanese when revision == 6 => new TrainerPrizeTable(11, 0, 0x35060, 0x62, false),
                GameLanguages.French => new TrainerPrizeTable(11, 0, 0x32AE0, 0x62, false),
                GameLanguages.German => new TrainerPrizeTable(11, 0, 0x329E0, 0x62, false),
                GameLanguages.Italian => new TrainerPrizeTable(11, 0, 0x329E0, 0x62, false),
                GameLanguages.Spanish => new TrainerPrizeTable(11, 0, 0x32AE0, 0x62, false),
                GameLanguages.Korean => new TrainerPrizeTable(11, 0, 0x3A0A0, 0x62, false),
                _ => null,
            },
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => new TrainerPrizeTable(16, 0x816C, 0x359E0, 0x69, false),   // Europe
                GameLanguages.English => new TrainerPrizeTable(16, 0x816C, 0x359E0, 0x69, false),   // US, and any English revision not listed
                GameLanguages.Japanese => new TrainerPrizeTable(16, 0x8164, 0x359E0, 0x69, false),
                GameLanguages.French => new TrainerPrizeTable(16, 0x8274, 0x35AE0, 0x69, false),
                GameLanguages.German => new TrainerPrizeTable(16, 0x8188, 0x35A00, 0x69, false),
                GameLanguages.Italian => new TrainerPrizeTable(16, 0x8188, 0x35A00, 0x69, false),
                GameLanguages.Spanish => new TrainerPrizeTable(16, 0x8274, 0x35AE0, 0x69, false),
                GameLanguages.Korean => new TrainerPrizeTable(16, 0x8184, 0x35A00, 0x69, false),   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => new TrainerPrizeTable(12, 0, 0x34C04, 129, true),   // Europe
                GameLanguages.English => new TrainerPrizeTable(12, 0, 0x34C04, 129, true),   // US, and any English revision not listed
                GameLanguages.Japanese => new TrainerPrizeTable(12, 0, 0x34BC0, 129, true),
                GameLanguages.French => new TrainerPrizeTable(12, 0, 0x34C04, 129, true),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new TrainerPrizeTable(12, 0, 0x34C04, 129, true),
                GameLanguages.Italian => new TrainerPrizeTable(12, 0, 0x34C04, 129, true),
                GameLanguages.Spanish => new TrainerPrizeTable(12, 0, 0x34C04, 129, true),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => new TrainerPrizeTable(12, 0, 0x34C08, 129, true),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => new TrainerPrizeTable(12, 0, 0x34C04, 129, true),   // Europe
                GameLanguages.English => new TrainerPrizeTable(12, 0, 0x34C04, 129, true),   // US, and any English revision not listed
                GameLanguages.Japanese => new TrainerPrizeTable(12, 0, 0x34BC0, 129, true),
                GameLanguages.French => new TrainerPrizeTable(12, 0, 0x34C04, 129, true),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new TrainerPrizeTable(12, 0, 0x34C04, 129, true),
                GameLanguages.Italian => new TrainerPrizeTable(12, 0, 0x34C04, 129, true),
                GameLanguages.Spanish => new TrainerPrizeTable(12, 0, 0x34C04, 129, true),
                GameLanguages.Korean => new TrainerPrizeTable(12, 0, 0x34C08, 129, true),
                _ => null,
            },
            _ => null,
        };

        public static uint? TrainerClassGenderPointerIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English when revision == 10 => 0x793B4,   // Europe
                GameLanguages.English => 0x793B4,   // US, and any English revision not listed
                GameLanguages.Japanese => 0x78C8C,
                GameLanguages.French => 0x79454,
                GameLanguages.German => 0x79454,
                GameLanguages.Italian => 0x79454,
                GameLanguages.Spanish => 0x79454,
                GameLanguages.Korean => 0x798A0,   // needs verifying: measured on a dump that is not a known good dump
                _ => null,
            },
            _ => null,
        };

        public static FollowerTableSites FollowerTablesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => new FollowerTableSites(0xFF088, 0xFE8D4, 0xFECAE),   // Europe
                GameLanguages.English => new FollowerTableSites(0xFF088, 0xFE8D4, 0xFECAE),   // US, and any English revision not listed
                GameLanguages.Japanese => new FollowerTableSites(0xFE808, 0xFE054, 0xFE42E),
                GameLanguages.French => new FollowerTableSites(0xFF06C, 0xFE8B8, 0xFEC92),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new FollowerTableSites(0xFF03C, 0xFE888, 0xFEC62),
                GameLanguages.Italian => new FollowerTableSites(0xFF000, 0xFE84C, 0xFEC26),
                GameLanguages.Spanish => new FollowerTableSites(0xFF070, 0xFE8BC, 0xFEC96),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => new FollowerTableSites(0xFF734, 0xFEF80, 0xFF35A),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => new FollowerTableSites(0xFF088, 0xFE8D4, 0xFECAE),   // Europe
                GameLanguages.English => new FollowerTableSites(0xFF088, 0xFE8D4, 0xFECAE),   // US, and any English revision not listed
                GameLanguages.Japanese => new FollowerTableSites(0xFE808, 0xFE054, 0xFE42E),
                GameLanguages.French => new FollowerTableSites(0xFF06C, 0xFE8B8, 0xFEC92),   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => new FollowerTableSites(0xFF03C, 0xFE888, 0xFEC62),
                GameLanguages.Italian => new FollowerTableSites(0xFF000, 0xFE84C, 0xFEC26),
                GameLanguages.Spanish => new FollowerTableSites(0xFF078, 0xFE8C4, 0xFEC9E),
                GameLanguages.Korean => new FollowerTableSites(0xFF72C, 0xFEF78, 0xFF352),
                _ => null,
            },
            _ => null,
        };

        public static int? PokeathlonMemberTableIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English when revision == 10 => 0xFF7B4,   // Europe
                GameLanguages.English => 0xFF7B4,   // US, and any English revision not listed
                GameLanguages.Japanese => 0xFEF34,
                GameLanguages.French => 0xFF798,   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => 0xFF768,
                GameLanguages.Italian => 0xFF72C,
                GameLanguages.Spanish => 0xFF79C,   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.Korean => 0xFFE60,
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English when revision == 10 => 0xFF7B4,   // Europe
                GameLanguages.English => 0xFF7B4,   // US, and any English revision not listed
                GameLanguages.Japanese => 0xFEF34,
                GameLanguages.French => 0xFF798,   // needs verifying: measured on a dump that is not a known good dump
                GameLanguages.German => 0xFF768,
                GameLanguages.Italian => 0xFF72C,
                GameLanguages.Spanish => 0xFF7A4,
                GameLanguages.Korean => 0xFFE58,
                _ => null,
            },
            _ => null,
        };
    }
}
