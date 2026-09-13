using System.IO;
using DSPRE.HgEngine;
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

        [SkippableFact]
        public void DiamondPicksTheClassAndSpeciesThemes()
        {
            Skip.If(!Directory.Exists(TestRoms.Diamond), "Diamond test project not configured");
            new RomInfo("ADAE", TestRoms.Diamond);
            var t = BattleMusicTables.LoadRom();

            Assert.NotNull(t);
            Assert.Equal(31, t.Combos.Rows.Count);
            Assert.Equal((ushort)0x0C, t.Combos.Rows[0].Transition);
            Assert.Equal((ushort)1117, t.Combos.Rows[0].Sequence);
            Assert.Equal((ushort)0xFFFF, t.Combos.Rows[29].Transition);   // the standard trainer row
            Assert.Equal((ushort)1119, t.Combos.Rows[29].Sequence);
            Assert.Equal((ushort)0xFFFF, t.Combos.Rows[30].Transition);   // the standard wild row
            Assert.Equal((ushort)1116, t.Combos.Rows[30].Sequence);

            Assert.Equal(1117, t.TrainerSequence(62));
            Assert.Equal(1119, t.TrainerSequence(2));    // Youngster
            Assert.Equal(1116, t.WildSequence(19));      // Rattata
        }

        private const string Sounds = @"
#define SEQ_GS_VS_TRAINER             1117
#define SEQ_GS_VS_GYMREADER           1118
#define SEQ_GS_VS_RAIKOU              1123
enum {
    ANIM_MUSIC_COMBO_FALKNER,
    ANIM_MUSIC_COMBO_BUGSY,
    ANIM_MUSIC_COMBO_RAIKOU,
    ANIM_MUSIC_COMBO_JOHTO_TRAINER,
};";

        private const string Source = @"
u16 MainMusicComboTable[][2] = {
    [ANIM_MUSIC_COMBO_FALKNER] = { 0xC, SEQ_GS_VS_GYMREADER },
    // [ANIM_MUSIC_COMBO_BUGSY] = { 0xD, SEQ_GS_VS_TRAINER },
    [ANIM_MUSIC_COMBO_RAIKOU] = { 0xFFFF, SEQ_GS_VS_RAIKOU },
    [ANIM_MUSIC_COMBO_JOHTO_TRAINER] = { 0xFFFF, 1117 },
};
u8 TrainerClassToMusicCombo[][2] = {
    { TRAINERCLASS_LEADER_FALKNER, ANIM_MUSIC_COMBO_FALKNER * 4 },
    { 67, 12 },
};
struct MonBattleMusic PokemonBattleMusic[] = {
    { .species = SPECIES_RAIKOU, .combo = ANIM_MUSIC_COMBO_RAIKOU },
};";

        [Fact]
        public void HgEngineSourceTablesReadByName()
        {
            var t = HgEngineMusicTables.ParseBattle(Source,
                HgEngineSymbolTable.Parse(Sounds),
                HgEngineSymbolTable.Parse("#define TRAINERCLASS_LEADER_FALKNER 66\n"),
                HgEngineSymbolTable.Parse("#define SPECIES_RAIKOU 243\n"));

            Assert.True(t.FromHgEngineSource);
            Assert.Equal(new (ushort, ushort)[] { (0xC, 1118), (0xFFFF, 0), (0xFFFF, 1123), (0xFFFF, 1117) }, t.Combos.Rows);
            Assert.Equal(new (int, int)[] { (66, 0), (67, 3) }, t.Classes.Rows);
            Assert.Equal(new (int, int)[] { (243, 2) }, t.Species.Rows);
        }

        private const string Encounters = @"
struct TrainerMusic sTrainerEncounterMusicParam[] = // cues which music sequence occurs upon eyes meeting
    {
        { .class = TRAINERCLASS_LEADER_FALKNER, .music1 = SEQ_GS_VS_TRAINER, .music2 = 1117 },
    };";

        private static HgEngineSymbolTable Classes => HgEngineSymbolTable.Parse("#define TRAINERCLASS_LEADER_FALKNER 66\n#define TRAINERCLASS_LEADER_BUGSY 67\n");

        [Fact]
        public void HgEngineSourceEditsKeepUnchangedTokensAndReadBack()
        {
            var sounds = HgEngineSymbolTable.Parse(Sounds);
            var noSpecies = HgEngineSymbolTable.Parse("#define SPECIES_RAIKOU 243\n");

            var (text, error) = HgEngineMusicTables.SetCombo(Source, 2, 0xFFFF, 1118, sounds);
            Assert.Null(error);
            Assert.Contains("[ANIM_MUSIC_COMBO_RAIKOU] = { 0xFFFF, SEQ_GS_VS_GYMREADER }", text);
            Assert.Contains("// [ANIM_MUSIC_COMBO_BUGSY] = { 0xD, SEQ_GS_VS_TRAINER },", text);   // comments untouched
            Assert.Equal((0xFFFF, 1118), HgEngineMusicTables.ParseBattle(text, sounds, Classes, noSpecies).Combos.Rows[2]);

            (text, error) = HgEngineMusicTables.SetClassCombo(text, 0, 66, 3, sounds, Classes);
            Assert.Null(error);
            Assert.Contains("{ TRAINERCLASS_LEADER_FALKNER, ANIM_MUSIC_COMBO_JOHTO_TRAINER * 4 }", text);
            (text, _) = HgEngineMusicTables.SetClassCombo(text, 1, 67, 2, sounds, Classes);
            Assert.Contains("{ 67, 8 }", text);                 // a class written as a number stays one
            Assert.Equal(new (int, int)[] { (66, 3), (67, 2) }, HgEngineMusicTables.ParseBattle(text, sounds, Classes, noSpecies).Classes.Rows);

            Assert.Equal((null, $"{HgEngineMusicTables.SourceRelPath} has no sTrainerEncounterMusicParam."),
                         HgEngineMusicTables.SetEncounterMusic(Source, 66, 1, 1, sounds, Classes));

            (text, error) = HgEngineMusicTables.SetEncounterMusic(Encounters, 66, 1117, 1123, sounds, Classes);
            Assert.Null(error);
            Assert.Contains(".music1 = SEQ_GS_VS_TRAINER, .music2 = SEQ_GS_VS_RAIKOU", text);
            (text, _) = HgEngineMusicTables.SetEncounterMusic(text, 67, 1118, 1118, sounds, Classes);
            var music = HgEngineMusicTables.ParseEncounterMusic(text, sounds, Classes);
            Assert.Equal((1117, 1123), music[66]);
            Assert.Equal((1118, 1118), music[67]);
            Assert.EndsWith("    };", text);
        }

        [SkippableFact]
        public void HgEngineCheckoutTablesMatchTheVanillaThemes()
        {
            string root = System.Environment.GetEnvironmentVariable("DSPRE_TEST_HGENGINE");
            Skip.If(string.IsNullOrWhiteSpace(root) || !Directory.Exists(root), "Set DSPRE_TEST_HGENGINE to an hg-engine checkout");
            string Read(string rel) => File.ReadAllText(Path.Combine(root, rel));

            var t = HgEngineMusicTables.ParseBattle(Read("src/music_tables.c"),
                HgEngineSymbolTable.Parse(Read("include/constants/sndseq.h")),
                HgEngineSymbolTable.Parse(Read("include/constants/trainerclass.h")),
                HgEngineSymbolTable.Parse(Read("include/constants/species.h")));
            _out.WriteLine($"{t.Combos.Rows.Count} combos, {t.Classes.Rows.Count} classes, {t.Species.Rows.Count} species");

            Assert.True(t.Combos.Rows.Count >= 45);
            Assert.Contains((66, 0), t.Classes.Rows);            // Falkner
            Assert.Equal(1118, t.Combos.Rows[0].Sequence);       // SEQ_GS_VS_GYMREADER
            Assert.Contains((243, 22), t.Species.Rows);          // Raikou
            Assert.Equal(1123, t.Combos.Rows[22].Sequence);
            Assert.Equal(1117, t.Combos.Rows[41].Sequence);      // the standard trainer theme
        }
    }
}
