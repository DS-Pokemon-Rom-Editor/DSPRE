using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>The Move, Item and HGSS Wild editors read and write one field map each on an hg-engine
    /// checkout. Snippets are copied in shape from a real checkout.</summary>
    public class HgEngineEditorSourceTests
    {
        private static readonly HgEngineSymbolTable Config = HgEngineSymbolTable.Parse(string.Join("\n",
            "#define GEN_CHAMPIONS 99",
            "#ifdef DEBUG_BATTLE_SCENARIOS",
            "#define GEN_LATEST GEN_CHAMPIONS",
            "#else",
            "#define GEN_LATEST 9",
            "#endif",
            "#define NATURAL_GIFT_POWER_GEN GEN_LATEST",
            "#define DISALLOW_DEXIT_GEN 0",
            "#define CHAMPIONS_POWER_CHANGES 1",
            "#define CHAMPIONS_TYPE_CHANGES 1",
            ""));

        private static readonly HgEngineSymbolTable MoveHeaders = HgEngineSymbolTable.Parse(string.Join("\n",
            "#define MOVE_EFFECT_HIT 0",
            "#define MOVE_EFFECT_SP_ATK_UP 13",
            "#define SPLIT_PHYSICAL 0",
            "#define SPLIT_STATUS 2",
            "#define TYPE_NORMAL 0",
            "#define TYPE_GROUND 4",
            "#define TYPE_GRASS 12",
            "#define RANGE_SINGLE_TARGET 0",
            "#define RANGE_ALL_ADJACENT (1 << 3)",
            "#define RANGE_USER (1 << 4)",
            "#define FLAG_CONTACT 0x01",
            "#define FLAG_PROTECT 0x02",
            "#define FLAG_MAGIC_COAT 0x04",
            "#define FLAG_SNATCH 0x08",
            "#define FLAG_MIRROR_MOVE 0x10",
            "#define FLAG_KEEP_HP_BAR 0x40",
            "#if DISALLOW_DEXIT_GEN == 8",
            "#define FLAG_UNUSABLE_IN_GEN_8 0x20",
            "#else",
            "#define FLAG_UNUSABLE_IN_GEN_8 0",
            "#endif",
            "#if DISALLOW_DEXIT_GEN == 0",
            "#define FLAG_UNUSABLE_UNIMPLEMENTED 0x20",
            "#else",
            "#define FLAG_UNUSABLE_UNIMPLEMENTED 0",
            "#endif",
            "#define APPEAL_BASIC 0x05",
            "#define APPEAL_DOUBLE_NEXT_SCORE 0x0B",
            "#define CONTEST_BEAUTY 1",
            "#define CONTEST_TOUGH 4",
            ""), Config.ByName);

        private static readonly HgEngineSymbolTable Types = HgEngineSymbolTable.Parse("#define TRUE 1\n#define FALSE 0\n");

        private static Func<string, int?> Lookup(params HgEngineSymbolTable[] tables) => name =>
        {
            foreach (var table in tables)
                if (table.TryGetValue(name, out int value)) return value;
            return null;
        };

        private static HgEngineSourceBlock Entry(string text, string designator)
        {
            Assert.True(HgEngineSourcePatcher.TryFindEntry(text, designator, out int open, out int close));
            return new HgEngineSourceBlock(text.Substring(open, close - open + 1));
        }

        /// <summary>Reads the entry into the model, writes the model back through the same map and spelling rules.</summary>
        private static string RoundTrip<T>(string text, string designator, IReadOnlyList<HgEngineSourceField<T>> fields, T model, Func<string, int?> lookup)
        {
            var entry = Entry(text, designator);
            var writes = HgEngineValueSpelling.Preserve(entry, HgEngineSourceFields.Writes(fields, model, Array.Empty<string>()), _ => lookup);
            Assert.Equal(fields.Count, writes.Count);
            foreach (var write in writes)
                Assert.True(HgEngineSourcePatcher.TryReplaceField(ref text, designator, write.Path, write.ValueLiteral), string.Concat(write.Path.Select(p => p.ToString())));
            return text;
        }

        private static MoveData EmptyMove() => new(new MemoryStream(new byte[16]));

        // ── Moves.c ────────────────────────────────────────────────────────────

        private const string MovesSource =
            "const struct MoveSourceEntry sMoveSource[NUM_OF_MOVES + 1] = {\r\n" +
            "    [MOVE_GROWTH] = {\r\n" +
            "        .names = {\r\n" +
            "            .name = \"Growth\",\r\n" +
            "        },\r\n" +
            "        .data = {\r\n" +
            "            .effect = MOVE_EFFECT_SP_ATK_UP,\r\n" +
            "            .split = SPLIT_STATUS,\r\n" +
            "            .power = ((CHAMPIONS_POWER_CHANGES) ? (90) : (80)),\r\n" +
            "            .type = ((CHAMPIONS_TYPE_CHANGES) ? (TYPE_GRASS) : (TYPE_NORMAL)),\r\n" +
            "            .accuracy = 0,\r\n" +
            "            .pp = 20,\r\n" +
            "            .effectChance = 0,\r\n" +
            "        },\r\n" +
            "        .battle = {\r\n" +
            "            .target = RANGE_ALL_ADJACENT | RANGE_USER,\r\n" +
            "            .priority = -3,\r\n" +
            "            .flags = FLAG_MIRROR_MOVE | FLAG_UNUSABLE_IN_GEN_8 | FLAG_UNUSABLE_UNIMPLEMENTED | FLAG_MAGIC_COAT,\r\n" +
            "        },\r\n" +
            "        .contest = {\r\n" +
            "            .appeal = APPEAL_DOUBLE_NEXT_SCORE,\r\n" +
            "            .contestType = CONTEST_BEAUTY,\r\n" +
            "        },\r\n" +
            "        .description = \"The user\\u2019s body is\\\\nforced to grow.\",\r\n" +
            "    },\r\n" +
            "};\r\n";

        [Fact]
        public void Moves_ReadsEverySavedFieldIncludingConfigSwitchesAndFlagExpressions()
        {
            var move = EmptyMove();
            Assert.True(HgEngineSourceFields.TryRead(Entry(MovesSource, "MOVE_GROWTH"), HgEngineMoveSource.Fields, move, Lookup(MoveHeaders, Config), out string error), error);

            Assert.Equal(13, move.battleeffect);
            Assert.Equal(MoveData.MoveSplit.STATUS, move.split);
            Assert.Equal(90, move.damage);
            Assert.Equal(12, (int)move.movetype);
            Assert.Equal(20, move.pp);
            Assert.Equal((1 << 3) | (1 << 4), move.target);
            Assert.Equal(-3, move.priority);
            Assert.Equal(0x10 | 0x20 | 0x04, move.flagField);
            Assert.Equal(0x0B, move.contestAppeal);
            Assert.Equal(MoveData.ContestCondition.BEAUTIFUL, move.contestConditionType);
        }

        [Fact]
        public void Moves_UnchangedValuesWriteBackIdenticalText()
        {
            var lookup = Lookup(MoveHeaders, Config);
            var move = EmptyMove();
            Assert.True(HgEngineSourceFields.TryRead(Entry(MovesSource, "MOVE_GROWTH"), HgEngineMoveSource.Fields, move, lookup, out string error), error);

            Assert.Equal(MovesSource, RoundTrip(MovesSource, "MOVE_GROWTH", HgEngineMoveSource.Fields, move, lookup));
        }

        [Fact]
        public void Moves_AChangedValueRewritesOnlyThatField()
        {
            var lookup = Lookup(MoveHeaders, Config);
            var move = EmptyMove();
            Assert.True(HgEngineSourceFields.TryRead(Entry(MovesSource, "MOVE_GROWTH"), HgEngineMoveSource.Fields, move, lookup, out string error), error);
            move.damage = 75;

            string written = RoundTrip(MovesSource, "MOVE_GROWTH", HgEngineMoveSource.Fields, move, lookup);
            Assert.Equal(MovesSource.Replace(".power = ((CHAMPIONS_POWER_CHANGES) ? (90) : (80)),", ".power = 75,"), written);
        }

        [Fact]
        public void Moves_AnUnknownNameIsRefusedAndNothingIsApplied()
        {
            string text = MovesSource.Replace("FLAG_MAGIC_COAT,", "FLAG_NOT_IN_HEADER,");
            var move = EmptyMove();

            Assert.False(HgEngineSourceFields.TryRead(Entry(text, "MOVE_GROWTH"), HgEngineMoveSource.Fields, move, Lookup(MoveHeaders, Config), out string error));
            Assert.Contains(".battle.flags", error);
            Assert.Equal(new byte[16].Take(14), move.ToByteArray().Take(14));
        }

        [Fact]
        public void Moves_AValueTheFileCannotHoldIsRefused()
        {
            string text = MovesSource.Replace(".pp = 20,", ".pp = 300,");
            Assert.False(HgEngineSourceFields.TryRead(Entry(text, "MOVE_GROWTH"), HgEngineMoveSource.Fields, EmptyMove(), Lookup(MoveHeaders, Config), out string error));
            Assert.Contains(".data.pp", error);
        }

        [Fact]
        public void Moves_AFieldTheEntryLacksIsSkippedNotAnError()
        {
            string text = MovesSource.Replace("            .effectChance = 0,\r\n", "");
            var move = EmptyMove();
            move.sideEffectProbability = 42;

            Assert.True(HgEngineSourceFields.TryRead(Entry(text, "MOVE_GROWTH"), HgEngineMoveSource.Fields, move, Lookup(MoveHeaders, Config), out string error), error);
            Assert.Equal(42, move.sideEffectProbability);
            Assert.Equal(20, move.pp);
        }

        // ── itemdata.c ─────────────────────────────────────────────────────────

        private static readonly HgEngineSymbolTable ItemHeaders = HgEngineSymbolTable.Parse(string.Join("\n",
            "#define POCKET_ITEMS 0",
            "#define POCKET_BERRIES 4",
            "#define POCKET_TMMATERIALS_GEN_9 POCKET_ITEMS",
            "#define FPOCKET_MATERIAL POCKET_TMMATERIALS_GEN_9",
            "#define BATTLE_POCKET_NONE 0",
            "#define BATTLE_POCKET_HP_RESTORE 4",
            "#define BATTLE_POCKET_STATUS_HEALERS 8",
            "#define TUIBAMU_NONE 0",
            "#define HOLD_EFFECT_NONE 0",
            "#define HOLD_EFFECT_PRZ_RESTORE 5",
            "#define SOUBI_NONE HOLD_EFFECT_NONE",
            "#define TYPE_FIRE 10",
            ""));

        private const string ItemSource =
            "const ITEMDATA __data[] =\n{\n" +
            "[ITEM_CHERI_BERRY] =\n" +
            "{\n" +
            "    ITEM_PRICE(500000),\n" +
            "    .holdEffect = HOLD_EFFECT_PRZ_RESTORE,\n" +
            "    .holdEffectParam = 0,\n" +
            "    .pluckEffect = TUIBAMU_NONE,\n" +
            "    .flingEffect = FALSE,\n" +
            "    .flingPower = 10,\n" +
            "    .naturalGiftPower = (NATURAL_GIFT_POWER_GEN < 6 ? 60 : 80),\n" +
            "    .naturalGiftType = TYPE_FIRE,\n" +
            "    .prevent_toss = FALSE,\n" +
            "    .selectable = TRUE,\n" +
            "    .fieldPocket = FPOCKET_MATERIAL,\n" +
            "    .battlePocket = BATTLE_POCKET_HP_RESTORE | BATTLE_POCKET_STATUS_HEALERS,\n" +
            "    .fieldUseFunc = 8,\n" +
            "    .battleUseFunc = FALSE,\n" +
            "    .partyUse = 1,\n" +
            "    .partyUseParam = {\n" +
            "        .slp_heal = FALSE,\n" +
            "        .psn_heal = FALSE,\n" +
            "        .brn_heal = FALSE,\n" +
            "        .frz_heal = FALSE,\n" +
            "        .prz_heal = TRUE,\n" +
            "        .cfs_heal = FALSE,\n" +
            "        .inf_heal = FALSE,\n" +
            "        .guard_spec = FALSE,\n" +
            "        .revive = FALSE,\n" +
            "        .revive_all = FALSE,\n" +
            "        .level_up = FALSE,\n" +
            "        .evolve = FALSE,\n" +
            "        .atk_stages = 0,\n" +
            "        .def_stages = 0,\n" +
            "        .spatk_stages = 0,\n" +
            "        .spdef_stages = 0,\n" +
            "        .speed_stages = 0,\n" +
            "        .accuracy_stages = 0,\n" +
            "        .critrate_stages = 0,\n" +
            "        .pp_up = FALSE,\n" +
            "        .pp_max = FALSE,\n" +
            "        .pp_restore = FALSE,\n" +
            "        .pp_restore_all = FALSE,\n" +
            "        .hp_restore = FALSE,\n" +
            "        .hp_ev_up = 0,\n" +
            "        .atk_ev_up = 0,\n" +
            "        .def_ev_up = 0,\n" +
            "        .speed_ev_up = 0,\n" +
            "        .spatk_ev_up = 0,\n" +
            "        .spdef_ev_up = 0,\n" +
            "        .friendship_mod_lo = TRUE,\n" +
            "        .friendship_mod_med = TRUE,\n" +
            "        .friendship_mod_hi = TRUE,\n" +
            "        .hp_ev_up_param = 0,\n" +
            "        .atk_ev_up_param = 0,\n" +
            "        .def_ev_up_param = 0,\n" +
            "        .speed_ev_up_param = 0,\n" +
            "        .spatk_ev_up_param = 0,\n" +
            "        .spdef_ev_up_param = 0,\n" +
            "        .hp_restore_param = FALSE,\n" +
            "        .pp_restore_param = 0,\n" +
            "        .friendship_mod_lo_param = 1,\n" +
            "        .friendship_mod_med_param = 1,\n" +
            "        .friendship_mod_hi_param = -1,\n" +
            "    },\n" +
            "},\n" +
            "};\n";

        private static ItemData EmptyItem() => new(new MemoryStream(new byte[36]), 149, hgEngineLayout: true);

        [Fact]
        public void Items_ReadsEverySavedFieldAndThePrice()
        {
            var item = EmptyItem();
            Assert.True(HgEngineItemSource.TryRead(Entry(ItemSource, "ITEM_CHERI_BERRY"), item, Lookup(ItemHeaders, Types, Config), out string error), error);

            Assert.Equal(500000, item.FullPrice);
            Assert.Equal(500000 & 0xFFFF, item.price);
            Assert.Equal(HoldEffect.PrzRestore, item.holdEffect);
            Assert.Equal(80, item.NaturalGiftPower);
            Assert.Equal(NaturalGiftType.Fire, item.naturalGiftType);
            Assert.True(item.Selectable);
            Assert.Equal(FieldPocket.Items, item.fieldPocket);
            Assert.Equal(BattlePocket.HpRestore | BattlePocket.StatusHealers, item.battlePocket);
            Assert.Equal(FieldUseFunc.Berry, item.fieldUseFunc);
            Assert.True(item.PartyUseParam.PrzHeal);
            Assert.True(item.PartyUseParam.FriendshipHigh);
            Assert.Equal(-1, item.PartyUseParam.FriendshipHighValue);
        }

        [Fact]
        public void Items_UnchangedValuesAndPriceWriteBackIdenticalText()
        {
            var lookup = Lookup(ItemHeaders, Types, Config);
            var item = EmptyItem();
            Assert.True(HgEngineItemSource.TryRead(Entry(ItemSource, "ITEM_CHERI_BERRY"), item, lookup, out string error), error);

            string text = RoundTrip(ItemSource, "ITEM_CHERI_BERRY", HgEngineItemSource.Fields, item, lookup);
            Assert.True(HgEngineItemExpansion.TryReplacePrice(ref text, "ITEM_CHERI_BERRY", item.FullPrice));
            Assert.Equal(ItemSource, text);
        }

        [Fact]
        public void Items_ABoolFieldHoldingTwoIsRefused()
        {
            string text = ItemSource.Replace(".prz_heal = TRUE,", ".prz_heal = 2,");
            Assert.False(HgEngineItemSource.TryRead(Entry(text, "ITEM_CHERI_BERRY"), EmptyItem(), Lookup(ItemHeaders, Types, Config), out string error));
            Assert.Contains(".partyUseParam.prz_heal", error);
        }

        [Theory]
        [InlineData("    ITEM_PRICE(800),\n", 800)]
        [InlineData("    ITEM_PRICE( 0x1F4 ),\n", 500)]
        [InlineData("    .price = 300,\n", 300)]
        public void ItemPrice_ReadsEveryShapeThePriceWriterHandles(string priceLine, int expected)
        {
            string text = "[ITEM_A] =\n{\n" + priceLine + "    .holdEffect = 0,\n},\n";

            Assert.True(HgEngineItemExpansion.TryGetPrice(Entry(text, "ITEM_A"), Lookup(), out int price, out string raw));
            Assert.Equal(expected, price);
            Assert.NotNull(raw);

            Assert.True(HgEngineItemExpansion.TryReplacePrice(ref text, "ITEM_A", 1234));
            Assert.True(HgEngineItemExpansion.TryGetPrice(Entry(text, "ITEM_A"), Lookup(), out int rewritten, out _));
            Assert.Equal(1234, rewritten);
        }

        [Fact]
        public void ItemPrice_MissingIsNotReadableButHasNoText()
        {
            string text = "[ITEM_C] =\n{\n    .holdEffect = 0,\n},\n";
            Assert.False(HgEngineItemExpansion.TryGetPrice(Entry(text, "ITEM_C"), Lookup(), out _, out string raw));
            Assert.Null(raw);
        }

        [Fact]
        public void ItemPrice_AnUnknownNameIsReportedWithItsText()
        {
            string text = "[ITEM_D] =\n{\n    ITEM_PRICE(PRICE_NOT_DEFINED),\n    .holdEffect = 0,\n},\n";
            Assert.False(HgEngineItemExpansion.TryGetPrice(Entry(text, "ITEM_D"), Lookup(), out _, out string raw));
            Assert.Equal("PRICE_NOT_DEFINED", raw);

            Assert.False(HgEngineItemSource.TryRead(Entry(text, "ITEM_D"), EmptyItem(), Lookup(), out string error));
            Assert.Contains("PRICE_NOT_DEFINED", error);
        }

        // ── Encounters.c ───────────────────────────────────────────────────────

        private static readonly HgEngineSymbolTable Species = HgEngineSymbolTable.Parse(string.Join("\n",
            "#define SPECIES_NONE 0",
            "#define SPECIES_PIDGEY 16",
            "#define SPECIES_TENTACOOL 72",
            "#define SPECIES_TENTACRUEL 73",
            "#define SPECIES_SHELLDER 90",
            "#define SPECIES_MAGIKARP 129",
            "#define SPECIES_CHINCHOU 170",
            "#define SPECIES_LANTURN 171",
            ""));

        private static string Column(string indent, string value, int count) =>
            string.Concat(Enumerable.Repeat(indent + value + ",\n", count));

        private static readonly string EncountersSource =
            "const EncounterData __data[] =\n{\n" +
            "    [ENCDATA_T20_NEW_BARK_TOWN] = {\n" +
            "        .rateWalk = 0,\n" +
            "        .rateSurf = 15,\n" +
            "        .rateRockSmash = 0,\n" +
            "        .rateOldRod = 25,\n" +
            "        .rateGoodRod = 50,\n" +
            "        .rateSuperRod = 75,\n" +
            "        .landSlots = {\n" +
            "            .levels = {\n" +
            "                2, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0\n" +
            "            },\n" +
            "            .speciesMorning = {\n" + "                SPECIES_PIDGEY,\n" + Column("                ", "SPECIES_NONE", 11) + "            },\n" +
            "            .speciesDay = {\n" + Column("                ", "SPECIES_NONE", 12) + "            },\n" +
            "            .speciesNight = {\n" + Column("                ", "SPECIES_NONE", 11) + "                SPECIES_PIDGEY,\n" + "            },\n" +
            "        },\n" +
            "        .hoennSoundSpecies = {\n" + Column("            ", "SPECIES_NONE", 2) + "        },\n" +
            "        .sinnohSoundSpecies = {\n" + Column("            ", "SPECIES_NONE", 2) + "        },\n" +
            "        .surfSlots = {\n" +
            "            { 15, 25, SPECIES_TENTACOOL },\n" +
            "            { 10, 20, SPECIES_TENTACOOL },\n" +
            "            { 15, 25, SPECIES_TENTACRUEL },\n" +
            "            { 15, 25, SPECIES_TENTACRUEL },\n" +
            "            { 15, 25, SPECIES_TENTACRUEL },\n" +
            "        },\n" +
            "        .rockSmashSlots = {\n" +
            "            { 0, 0, SPECIES_NONE },\n" +
            "            { 0, 0, SPECIES_NONE },\n" +
            "        },\n" +
            "        .oldRodSlots = {\n" +
            "            { 10, 10, SPECIES_MAGIKARP },\n" +
            "            { 10, 10, SPECIES_MAGIKARP },\n" +
            "            { 10, 10, SPECIES_MAGIKARP },\n" +
            "            { 10, 10, SPECIES_TENTACOOL },\n" +
            "            { 10, 10, SPECIES_TENTACOOL },\n" +
            "        },\n" +
            "        .goodRodSlots = {\n" +
            "            { 20, 20, SPECIES_MAGIKARP },\n" +
            "            { 20, 20, SPECIES_TENTACOOL },\n" +
            "            { 20, 20, SPECIES_CHINCHOU },\n" +
            "            { 20, 20, SPECIES_SHELLDER },\n" +
            "            { 20, 20, SPECIES_CHINCHOU },\n" +
            "        },\n" +
            "        .superRodSlots = {\n" +
            "            { 40, 40, SPECIES_CHINCHOU },\n" +
            "            { 40, 40, SPECIES_SHELLDER },\n" +
            "            { 40, 40, SPECIES_TENTACRUEL },\n" +
            "            { 40, 40, SPECIES_LANTURN },\n" +
            "            { 40, 40, SPECIES_TENTACRUEL },\n" +
            "        },\n" +
            "        .landSwarm = SPECIES_NONE,\n" +
            "        .surfSwarm = SPECIES_TENTACOOL,\n" +
            "        .nightFish = SPECIES_SHELLDER,\n" +
            "        .fishSwarm = SPECIES_MAGIKARP,\n" +
            "    },\n" +
            "};\n";

        [Fact]
        public void Encounters_ReadsEverySavedFieldAndWritesBackIdenticalText()
        {
            var lookup = Lookup(Species);
            var table = new EncounterFileHGSS();
            Assert.True(HgEngineSourceFields.TryRead(Entry(EncountersSource, "ENCDATA_T20_NEW_BARK_TOWN"), HgEngineEncounterSource.Fields, table, lookup, out string error), error);

            Assert.Equal(15, table.surfRate);
            Assert.Equal(75, table.superRodRate);
            Assert.Equal(new byte[] { 2, 3 }, table.walkingLevels.Take(2));
            Assert.Equal(16, table.morningPokemon[0]);
            Assert.Equal(16, table.nightPokemon[11]);
            Assert.Equal((10, 20, 72), ((int)table.surfMinLevels[1], (int)table.surfMaxLevels[1], (int)table.surfPokemon[1]));
            Assert.Equal(171, table.superRodPokemon[3]);
            Assert.Equal(new ushort[] { 0, 72, 90, 129 }, table.swarmPokemon);

            Assert.Equal(EncountersSource, RoundTrip(EncountersSource, "ENCDATA_T20_NEW_BARK_TOWN", HgEngineEncounterSource.Fields, table, lookup));
        }

        // ── Expressions ────────────────────────────────────────────────────────

        [Theory]
        [InlineData("((CHAMPIONS_POWER_CHANGES) ? (90) : (80))", 90)]
        [InlineData("NATURAL_GIFT_POWER_GEN < 6 ? 60 : 80", 80)]
        [InlineData("FLAG_MIRROR_MOVE | FLAG_UNUSABLE_IN_GEN_8 | FLAG_CONTACT", 0x11)]
        [InlineData("-3", -3)]
        [InlineData("0x40", 0x40)]
        [InlineData("(1 << 3) + 2 * 3", 14)]
        public void Expression_EvaluatesTheShapesDataFilesUse(string text, int expected)
        {
            Assert.True(HgEngineSourceExpression.TryEvaluate(text, Lookup(MoveHeaders, Config), out int value));
            Assert.Equal(expected, value);
        }

        [Theory]
        [InlineData("FLAG_CONTACT | NOT_DEFINED")]
        [InlineData("CHAMPIONS_POWER_CHANGES ? 90")]
        [InlineData("(u16)5")]
        [InlineData("")]
        public void Expression_RefusesWhatItCannotResolve(string text)
        {
            Assert.False(HgEngineSourceExpression.TryEvaluate(text, Lookup(MoveHeaders, Config), out _));
        }
    }
}
