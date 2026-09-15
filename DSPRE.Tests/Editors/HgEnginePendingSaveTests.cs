using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>New moves and items, CSV imports and 20-bit prices wait in memory until Save. These cover the
    /// pure halves that decide what Save writes. Snippets are copied in shape from a real checkout.</summary>
    public class HgEnginePendingSaveTests
    {
        private static readonly Dictionary<string, int> Names = new()
        {
            ["MOVE_EFFECT_HIT"] = 0, ["MOVE_EFFECT_SP_ATK_UP"] = 13, ["SPLIT_PHYSICAL"] = 0, ["SPLIT_STATUS"] = 2,
            ["TYPE_NORMAL"] = 0, ["TYPE_GRASS"] = 12, ["RANGE_SINGLE_TARGET"] = 0, ["RANGE_USER"] = 1 << 4,
            ["FLAG_CONTACT"] = 0x01, ["FLAG_PROTECT"] = 0x02, ["CONTEST_COOL"] = 0, ["CONTEST_BEAUTY"] = 1,
            ["APPEAL_BASIC"] = 0x05, ["TRUE"] = 1, ["FALSE"] = 0, ["POCKET_ITEMS"] = 0, ["BATTLE_POCKET_NONE"] = 0,
        };

        private static int? Lookup(string name) => Names.TryGetValue(name, out int v) ? v : null;

        private static MoveData EmptyMove() => new(new MemoryStream(new byte[16]));

        // ── New move ───────────────────────────────────────────────────────────

        private const string MovesHeader =
            "#define MOVE_MALIGNANT_CHAIN  922\n" +
            "#define NUM_OF_CANONICAL_MOVES 923\n" +
            "#define NUM_OF_CUSTOM_MOVES 0\n" +
            "#define NUM_OF_MOVES (NUM_OF_CANONICAL_MOVES + NUM_OF_CUSTOM_MOVES)\n" +
            "#define MOVE_G_MAX_WILDFIRE   (NUM_OF_MOVES - 1 + 1)\n";

        private const string MovesSource =
            "const MoveSourceEntry sMoveSource[NUM_OF_MOVES + 1] = {\n" +
            "    [MOVE_MALIGNANT_CHAIN] = {\n        .data = { .power = 100 },\n    },\n\n" +
            "    [NUM_OF_MOVES] = {\n        .data = { .power = 100 },\n    },\n" +
            "};\n";

        [Fact]
        public void NewMove_PrepareBuildsTheTextsTheImmediateAddWrote()
        {
            var table = HgEngineSymbolTable.Parse(MovesHeader);
            int oldId = table.ByName["NUM_OF_CANONICAL_MOVES"] + table.ByName["NUM_OF_CUSTOM_MOVES"];
            string oldDesignator = "MOVE_" + HgEngineNameSlug.ToUniqueSlug("Moon Beam", table, "MOVE_");
            Assert.True(HgEngineMoveExpansion.TryBuildSources(MovesHeader, MovesSource, "Moon Beam", oldDesignator, 0, oldId, null,
                out string oldHeader, out string oldSource, out string oldError), oldError);

            Assert.True(HgEngineMoveExpansion.TryPrepare(MovesHeader, MovesSource, "Moon Beam", null,
                out var pending, out string header, out string source, out string error), error);

            Assert.Equal(oldId, pending.Id);
            Assert.Equal(oldDesignator, pending.Designator);
            Assert.Equal(oldHeader, header);
            Assert.Equal(oldSource, source);
            Assert.False(HgEngineSymbolTable.Parse(MovesHeader).ByName.ContainsKey(pending.Designator));
        }

        [Fact]
        public void NewMove_CommitWithTheTemplateValuesWritesExactlyThePreparedTexts()
        {
            Assert.True(HgEngineMoveExpansion.TryPrepare(MovesHeader, MovesSource, "Moon Beam", null, out var pending, out string header, out string source, out string error), error);
            var move = EmptyMove();
            Assert.True(HgEngineSourceFields.TryRead(pending.Entry, HgEngineMoveSource.Fields, move, Lookup, out error), error);
            Assert.Equal(5, move.pp);
            Assert.Equal(MoveData.MoveSplit.STATUS, move.split);

            Assert.True(HgEngineMoveExpansion.TryBuildCommit(MovesHeader, MovesSource, pending, move, null, _ => Lookup,
                out string committedHeader, out string committedSource, out error), error);

            Assert.Equal(header, committedHeader);
            Assert.Equal(source, committedSource);
        }

        [Fact]
        public void NewMove_CommitCarriesTheEditsIntoTheNewEntryOnly()
        {
            Assert.True(HgEngineMoveExpansion.TryPrepare(MovesHeader, MovesSource, "Moon Beam", null, out var pending, out _, out string source, out string error), error);
            var move = EmptyMove();
            Assert.True(HgEngineSourceFields.TryRead(pending.Entry, HgEngineMoveSource.Fields, move, Lookup, out error), error);
            move.damage = 80;
            move.pp = 15;

            Assert.True(HgEngineMoveExpansion.TryBuildCommit(MovesHeader, MovesSource, pending, move, null, _ => Lookup,
                out _, out string committed, out error), error);

            Assert.Equal(source.Replace("            .power = 0,\n", "            .power = 80,\n").Replace("            .pp = 5,\n", "            .pp = 15,\n"), committed);
            Assert.True(HgEngineSourcePatcher.TryGetFieldValue(committed, "MOVE_MALIGNANT_CHAIN", HgEngineSourceFields.PathOf("data", "power"), out string untouched));
            Assert.Equal("100", untouched);
        }

        [Fact]
        public void NewMove_CommitRefusesWhenMovesHeaderChangedSinceAdd()
        {
            Assert.True(HgEngineMoveExpansion.TryPrepare(MovesHeader, MovesSource, "Moon Beam", null, out var pending, out _, out _, out string error), error);
            string changed = MovesHeader.Replace("#define NUM_OF_CUSTOM_MOVES 0",
                "#define MOVE_OTHER (NUM_OF_CANONICAL_MOVES + 0)\n\n#define NUM_OF_CUSTOM_MOVES 1");

            Assert.False(HgEngineMoveExpansion.TryBuildCommit(changed, MovesSource, pending, EmptyMove(), null, _ => Lookup,
                out string header, out string source, out error));
            Assert.Null(header);
            Assert.Null(source);
            Assert.Contains("changed", error);
        }

        // ── New item ───────────────────────────────────────────────────────────

        private const string ItemsHeader =
            "#define ITEM_NONE 0\n" +
            "#define ITEM_MASTER_BALL 1\n" +
            "#define MAX_BASE_ITEM_NUM ITEM_MASTER_BALL\n" +
            "#define MAX_TOTAL_ITEM_NUM ITEM_MASTER_BALL\n";

        private static readonly string ItemBody =
            "{\n" +
            "    ITEM_PRICE(0),\n" +
            "    .holdEffect = 0,\n" +
            "    .holdEffectParam = 0,\n" +
            "    .pluckEffect = 0,\n" +
            "    .flingEffect = 0,\n" +
            "    .flingPower = 0,\n" +
            "    .naturalGiftPower = 0,\n" +
            "    .naturalGiftType = TYPE_NORMAL,\n" +
            "    .prevent_toss = FALSE,\n" +
            "    .selectable = FALSE,\n" +
            "    .fieldPocket = POCKET_ITEMS,\n" +
            "    .battlePocket = BATTLE_POCKET_NONE,\n" +
            "    .fieldUseFunc = 0,\n" +
            "    .battleUseFunc = 0,\n" +
            "    .partyUse = 0,\n" +
            "    .partyUseParam = {\n" +
            string.Concat(new[] { "slp_heal", "psn_heal", "brn_heal", "frz_heal", "prz_heal", "cfs_heal", "inf_heal", "guard_spec", "revive",
                "revive_all", "level_up", "evolve" }.Select(f => $"        .{f} = FALSE,\n")) +
            string.Concat(new[] { "atk_stages", "def_stages", "spatk_stages", "spdef_stages", "speed_stages", "accuracy_stages", "critrate_stages" }
                .Select(f => $"        .{f} = 0,\n")) +
            string.Concat(new[] { "pp_up", "pp_max", "pp_restore", "pp_restore_all", "hp_restore", "hp_ev_up", "atk_ev_up", "def_ev_up",
                "speed_ev_up", "spatk_ev_up", "spdef_ev_up", "friendship_mod_lo", "friendship_mod_med", "friendship_mod_hi" }
                .Select(f => $"        .{f} = FALSE,\n")) +
            string.Concat(new[] { "hp_ev_up_param", "atk_ev_up_param", "def_ev_up_param", "speed_ev_up_param", "spatk_ev_up_param",
                "spdef_ev_up_param", "hp_restore_param", "pp_restore_param", "friendship_mod_lo_param", "friendship_mod_med_param",
                "friendship_mod_hi_param" }.Select(f => $"        .{f} = 0,\n")) +
            "    },\n" +
            "}";

        private static readonly string ItemsSource =
            "const ITEMDATA __data[] =\n{\n[ITEM_NONE] =\n" + ItemBody + ",\n\n[ITEM_MASTER_BALL] =\n" + ItemBody + ",\n};\n";

        [Fact]
        public void NewItem_PrepareBuildsTheTextsTheImmediateAddWrote()
        {
            // The expected texts, built one step at a time.
            var table = HgEngineSymbolTable.Parse(ItemsHeader);
            int oldId = table.ByName.Where(kv => kv.Key.StartsWith("ITEM_")).Max(kv => kv.Value) + 1;
            string oldDesignator = "ITEM_" + HgEngineNameSlug.ToUniqueSlug("Moon Cake", table, "ITEM_");
            string oldHeader = ItemsHeader;
            Assert.True(HgEngineHeaderEditor.TryInsertBeforeDefine(ref oldHeader, "MAX_TOTAL_ITEM_NUM", $"#define {oldDesignator} {oldId}\n\n"));
            Assert.True(HgEngineHeaderEditor.TryReplaceDefineValue(ref oldHeader, "MAX_TOTAL_ITEM_NUM", oldDesignator));
            string oldSource = ItemsSource;
            Assert.True(HgEngineHeaderEditor.TryInsertBeforeFinalCloseBrace(ref oldSource, HgEngineItemExpansion.BuildNewEntry(ItemsSource, oldDesignator)));

            Assert.True(HgEngineItemExpansion.TryPrepare(ItemsHeader, ItemsSource, "Moon Cake", null, out var pending, out string header, out string source, out string error), error);

            Assert.Equal(2, pending.Id);
            Assert.Equal(oldDesignator, pending.Designator);
            Assert.Equal(oldHeader, header);
            Assert.Equal(oldSource, source);
            Assert.False(HgEngineSymbolTable.Parse(ItemsHeader).ByName.ContainsKey(pending.Designator));
        }

        [Fact]
        public void NewItem_CommitWritesThePreparedTextsAndThenTheEditedPrice()
        {
            Assert.True(HgEngineItemExpansion.TryPrepare(ItemsHeader, ItemsSource, "Moon Cake", null, out var pending, out string header, out string source, out string error), error);
            var item = new ItemData(new MemoryStream(new byte[36]), pending.Id, hgEngineLayout: true);
            Assert.True(HgEngineItemSource.TryRead(pending.Entry, item, Lookup, out error), error);
            Assert.True(item.Selectable);

            Assert.True(HgEngineItemExpansion.TryBuildCommit(ItemsHeader, ItemsSource, pending, item, null, _ => Lookup, Lookup,
                out string committedHeader, out string committedSource, out error), error);
            Assert.Equal(header, committedHeader);
            Assert.Equal(source, committedSource);

            item.FullPrice = 500000;
            Assert.True(HgEngineItemExpansion.TryBuildCommit(ItemsHeader, ItemsSource, pending, item, null, _ => Lookup, Lookup,
                out _, out string priced, out error), error);
            Assert.Equal(source.Replace("[ITEM_MOON_CAKE] =\n{\n    ITEM_PRICE(0),", "[ITEM_MOON_CAKE] =\n{\n    ITEM_PRICE(500000),"), priced);
            Assert.NotEqual(source, priced);
        }

        // ── Staged CSV import ──────────────────────────────────────────────────

        private static string MoveEntry(string designator, int power, int pp) =>
            $"    [{designator}] = {{\n" +
            "        .data = {\n" +
            "            .effect = MOVE_EFFECT_HIT,\n" +
            "            .split = SPLIT_PHYSICAL,\n" +
            $"            .power = {power},\n" +
            "            .type = TYPE_NORMAL,\n" +
            "            .accuracy = 100,\n" +
            $"            .pp = {pp},\n" +
            "            .effectChance = 0,\n" +
            "        },\n" +
            "        .battle = {\n" +
            "            .target = RANGE_SINGLE_TARGET,\n" +
            "            .priority = 0,\n" +
            "            .flags = FLAG_CONTACT | FLAG_PROTECT,\n" +
            "        },\n" +
            "        .contest = {\n" +
            "            .appeal = APPEAL_BASIC,\n" +
            "            .contestType = CONTEST_COOL,\n" +
            "        },\n" +
            "    },\n";

        private static readonly string ThreeMoves =
            "const struct MoveSourceEntry sMoveSource[NUM_OF_MOVES + 1] = {\n" +
            MoveEntry("MOVE_POUND", 40, 35) + "\n" + MoveEntry("MOVE_KARATE_CHOP", 50, 25) + "\n" + MoveEntry("MOVE_DOUBLE_SLAP", 15, 10) +
            "};\n";

        private static MoveData Read(string text, string designator)
        {
            var move = EmptyMove();
            var index = HgEngineEntryIndex.Build(text);
            Assert.True(HgEngineMoveSource.TryReadEntry(text, index, designator, move, Lookup, out string error), error);
            return move;
        }

        [Fact]
        public void StagedImport_WritesEveryRecordOnceAndLeavesTheRestAlone()
        {
            var pound = Read(ThreeMoves, "MOVE_POUND");
            var slap = Read(ThreeMoves, "MOVE_DOUBLE_SLAP");
            pound.damage = 45;
            slap.damage = 20;
            slap.pp = 15;

            string text = ThreeMoves;
            Assert.True(HgEngineMoveSource.TryApply(ref text, new[] { ("MOVE_DOUBLE_SLAP", slap), ("MOVE_POUND", pound) }, _ => Lookup, out string error), error);

            string expected = ThreeMoves
                .Replace(MoveEntry("MOVE_POUND", 40, 35), MoveEntry("MOVE_POUND", 45, 35))
                .Replace(MoveEntry("MOVE_DOUBLE_SLAP", 15, 10), MoveEntry("MOVE_DOUBLE_SLAP", 20, 15));
            Assert.Equal(expected, text);
            Assert.Equal(3, Regex.Matches(text, @"\.power =").Count);
            Assert.Equal(3, Regex.Matches(text, @"\.pp =").Count);
            Assert.Equal(50, Read(text, "MOVE_KARATE_CHOP").damage);
            Assert.Equal(20, Read(text, "MOVE_DOUBLE_SLAP").damage);

            // Applying the same staged records again changes nothing.
            string again = text;
            Assert.True(HgEngineMoveSource.TryApply(ref again, new[] { ("MOVE_POUND", pound), ("MOVE_DOUBLE_SLAP", slap) }, _ => Lookup, out error), error);
            Assert.Equal(text, again);
        }

        [Fact]
        public void StagedImport_AMissingMoveWritesNothing()
        {
            var pound = Read(ThreeMoves, "MOVE_POUND");
            pound.damage = 99;
            string text = ThreeMoves;

            Assert.False(HgEngineMoveSource.TryApply(ref text, new[] { ("MOVE_POUND", pound), ("MOVE_NOT_THERE", EmptyMove()) }, _ => Lookup, out string error));
            Assert.Equal(ThreeMoves, text);
            Assert.Contains("MOVE_NOT_THERE", error);
        }

        [Fact]
        public void EntryIndex_FindsWhatTryFindEntryFindsInOneScan()
        {
            var index = HgEngineEntryIndex.Build(ThreeMoves);
            int checkedCount = 0;
            foreach (string designator in new[] { "MOVE_POUND", "MOVE_KARATE_CHOP", "MOVE_DOUBLE_SLAP", "NUM_OF_MOVES + 1" })
            {
                Assert.True(HgEngineSourcePatcher.TryFindEntry(ThreeMoves, designator, out int open, out int close));
                Assert.Equal((open, close), index[designator]);
                checkedCount++;
            }
            Assert.True(checkedCount > 0);
            Assert.False(index.ContainsKey("MOVE_NOT_THERE"));
        }

        // ── 20-bit price ───────────────────────────────────────────────────────

        private static string PricedItem(string priceLines) =>
            "[ITEM_A] =\n{\n" + priceLines + "    .holdEffect = 0,\n},\n";

        private static HgEngineSourceBlock Entry(string text)
        {
            Assert.True(HgEngineSourcePatcher.TryFindEntry(text, "ITEM_A", out int open, out int close));
            return new HgEngineSourceBlock(text.Substring(open, close - open + 1));
        }

        private static int ReadPrice(string text)
        {
            Assert.True(HgEngineItemExpansion.TryGetPrice(Entry(text), Lookup, out int price, out string raw), raw);
            return price;
        }

        [Fact]
        public void Price_MacroSpellingRoundTripsTwentyBits()
        {
            string text = PricedItem("    ITEM_PRICE(500000),\n");
            Assert.Equal(500000, ReadPrice(text));

            Assert.True(HgEngineItemExpansion.TryReplacePrice(ref text, "ITEM_A", 1048575));
            Assert.Contains("ITEM_PRICE(1048575),", text);
            Assert.Equal(1048575, ReadPrice(text));

            Assert.True(HgEngineItemExpansion.TryReplacePrice(ref text, "ITEM_A", 70));
            Assert.Equal(70, ReadPrice(text));
        }

        [Fact]
        public void Price_UnchangedValueKeepsTheSpelling()
        {
            string macro = PricedItem("    ITEM_PRICE( 0x7A120 ),\n");
            string split = PricedItem("    .price = 0xA120,\n    .price_high = 7,\n");

            foreach (string original in new[] { macro, split })
            {
                string text = original;
                Assert.Equal(500000, ReadPrice(text));
                Assert.True(HgEngineItemExpansion.TryReplacePrice(ref text, "ITEM_A", 500000));
                Assert.Equal(original, text);
            }
        }

        [Fact]
        public void Price_SplitSpellingRoundTripsTwentyBits()
        {
            string text = PricedItem("    .price = 41248,\n    .price_high = 7,\n");
            Assert.Equal(500000, ReadPrice(text));

            Assert.True(HgEngineItemExpansion.TryReplacePrice(ref text, "ITEM_A", 65536));
            Assert.Contains(".price = 0,", text);
            Assert.Contains(".price_high = 1,", text);
            Assert.Equal(65536, ReadPrice(text));
        }

        [Fact]
        public void Price_SplitSpellingWithoutAHighPartGainsOneOnlyWhenNeeded()
        {
            string text = PricedItem("    .price = 300,\n");
            Assert.True(HgEngineItemExpansion.TryReplacePrice(ref text, "ITEM_A", 400));
            Assert.DoesNotContain("price_high", text);
            Assert.Equal(400, ReadPrice(text));

            Assert.True(HgEngineItemExpansion.TryReplacePrice(ref text, "ITEM_A", 70000));
            Assert.Contains(".price_high = 1,", text);
            Assert.Equal(70000, ReadPrice(text));
        }

        [Fact]
        public void Price_AValueTheBuildWouldTruncateIsRefused()
        {
            string text = PricedItem("    ITEM_PRICE(1048576),\n");
            Assert.False(HgEngineItemExpansion.TryGetPrice(Entry(text), Lookup, out _, out string raw, out bool outOfRange));
            Assert.True(outOfRange);
            Assert.Equal("1048576", raw);

            var item = new ItemData(new MemoryStream(new byte[36]), 1, hgEngineLayout: true);
            Assert.False(HgEngineItemSource.TryRead(Entry(text), item, Lookup, out string error));
            Assert.Contains("out of range", error);

            string before = PricedItem("    .price = 300,\n");
            string attempt = before;
            Assert.False(HgEngineItemExpansion.TryReplacePrice(ref attempt, "ITEM_A", 1048576));
            Assert.Equal(before, attempt);
        }

        private static byte[] BuiltRecord(int price, bool hgEngineLayout)
        {
            var item = new ItemData(new MemoryStream(new byte[36]), 1, hgEngineLayout) { FullPrice = price, HoldEffectParam = 9 };
            return item.ToByteArray();
        }

        [Fact]
        public void BuiltRecord_HgEngineKeepsPriceHighInByte0x22()
        {
            byte[] bytes = BuiltRecord(500000, hgEngineLayout: true);

            Assert.Equal(36, bytes.Length);
            Assert.Equal(500000 & 0xFFFF, BitConverter.ToUInt16(bytes, 0));
            Assert.Equal(7, bytes[0x22]);
            Assert.Equal(0, bytes[0x23]);

            var read = new ItemData(new MemoryStream(bytes), 1, hgEngineLayout: true);
            Assert.Equal(500000, read.FullPrice);
            Assert.Equal(bytes, read.ToByteArray());
        }

        [Fact]
        public void BuiltRecord_VanillaLayoutIsUnchanged()
        {
            byte[] vanilla = BuiltRecord(1200, hgEngineLayout: false);
            Assert.Equal(36, vanilla.Length);
            Assert.Equal(0, vanilla[0x22]);

            // Vanilla padding is never read as a price, and is written back as zero.
            byte[] withPadding = (byte[])vanilla.Clone();
            withPadding[0x22] = 0x07;
            var read = new ItemData(new MemoryStream(withPadding), 1, hgEngineLayout: false);
            Assert.Equal(1200, read.FullPrice);
            Assert.Equal(vanilla, read.ToByteArray());
            Assert.Equal(vanilla, new ItemData(new MemoryStream(vanilla), 1, hgEngineLayout: false).ToByteArray());
        }
    }
}
