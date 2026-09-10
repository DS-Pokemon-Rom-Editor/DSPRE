using System.IO;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Which theme a battle plays. Expected numbers come from the decomp and hg-engine's sndseq.h.
    /// </summary>
    [Collection("rom")]
    public class BattleMusicTablesTests
    {
        private readonly ITestOutputHelper _out;
        public BattleMusicTablesTests(ITestOutputHelper o) => _out = o;

        [SkippableFact]
        public void HeartGoldPicksTheClassAndSpeciesThemes()
        {
            Skip.If(!Directory.Exists(TestRoms.HeartGold), "HeartGold test project not configured");
            new RomInfo("IPKE", TestRoms.HeartGold);
            var t = BattleMusicTables.LoadRom();

            Assert.Equal(45, t.Combos.Rows.Count);
            Assert.Equal(32, t.Classes.Rows.Count);
            Assert.Equal(11, t.Species.Rows.Count);

            Assert.Equal(1117, t.TrainerSequence(2));            // Youngster: SEQ_GS_VS_TRAINER
            Assert.Equal(1126, t.TrainerSequence(2, kanto: true));
            Assert.Equal(1118, t.TrainerSequence(66));           // Falkner: SEQ_GS_VS_GYMREADER
            Assert.Equal(1119, t.TrainerSequence(23));           // rival
            Assert.Equal(1124, t.TrainerSequence(109));          // Red: SEQ_GS_VS_CHAMP
            Assert.Equal(1116, t.WildSequence(19));              // Rattata: SEQ_GS_VS_NORAPOKE
            Assert.Equal(1125, t.WildSequence(19, kanto: true));
            Assert.Equal(1123, t.WildSequence(243));             // Raikou
            Assert.Equal(1133, t.WildSequence(249));             // Lugia
            Assert.Equal(1123, t.WildSequence(243, kanto: true)); // only the standard themes change
        }

        [SkippableFact]
        public void PlatinumPicksTheClassAndSpeciesThemes()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum test project not configured");
            new RomInfo("CPUE", TestRoms.Platinum);
            var t = BattleMusicTables.LoadRom();

            Assert.Equal(35, t.Combos.Rows.Count);
            Assert.Equal(1119, t.TrainerSequence(2));    // Youngster: SEQ_BATTLE_TRAINER
            Assert.Equal(1117, t.TrainerSequence(62));   // Roark: SEQ_BATTLE_GYM_LEADER
            Assert.Equal(1117, t.TrainerSequence(79));   // Volkner
            Assert.Equal(1136, t.TrainerSequence(65));   // Aaron: SEQ_BATTLE_ELITE_FOUR
            Assert.Equal(1122, t.TrainerSequence(69));   // Cynthia: SEQ_BATTLE_CHAMPION
            Assert.Equal(1124, t.TrainerSequence(63));   // rival
            Assert.Equal(1123, t.TrainerSequence(89));   // grunt
            Assert.Equal(1134, t.TrainerSequence(88));   // Saturn
            Assert.Equal(1120, t.TrainerSequence(86));   // Cyrus
            Assert.Equal(1202, t.TrainerSequence(100));  // Factory Head
            Assert.Equal(1116, t.WildSequence(19));      // Rattata: SEQ_BATTLE_WILD_POKEMON
            Assert.Equal(1116, t.WildSequence(492));     // Shaymin keeps the wild theme
            Assert.Equal(1121, t.WildSequence(484));     // Palkia
            Assert.Equal(1118, t.WildSequence(481));     // Mesprit
            Assert.Equal(1125, t.WildSequence(493));     // Arceus
            Assert.Equal(1126, t.WildSequence(485));     // Heatran
            Assert.Equal(1201, t.WildSequence(487));     // Giratina
            Assert.Equal(1204, t.WildSequence(378));     // Regice
        }
    }
}
