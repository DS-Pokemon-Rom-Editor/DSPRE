using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DSPRE.HgEngine;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Source-text rules behind hg-engine saves that must either land completely or not at all.
    /// Snippets are copied in shape from a real checkout.</summary>
    public class HgEngineSaveSafetyTests
    {
        // ── Hidden items ─────────────────────────────────────────────────────

        private const string HiddenBlock =
            "{\n" +
            "    { ITEM_POTION, 1, 0, 0, 0 }, // New Bark Town\n" +
            "    { ITEM_NUGGET, 1, 0, 0, 1 }, // Cherrygrove\n" +
            "    { ITEM_NUGGET, 1, 0, 0, 225 }, // Cherrygrove\n" +
            "}";

        private static string Symbol(int id) => id switch { 17 => "ITEM_POTION", 92 => "ITEM_NUGGET", 50 => "ITEM_RARE_CANDY", _ => id.ToString() };

        [Fact]
        public void HiddenItems_RewriteKeepsEachRowsTrailingCommentByPosition()
        {
            var entries = new List<HgEngineHiddenItems.Entry>
            {
                new() { ItemId = 17, Quantity = 1, Index = 0 },
                new() { ItemId = 50, Quantity = 3, Index = 1 },
            };

            Assert.True(HgEngineHiddenItems.TryBuildArrayBlock(HiddenBlock, entries, Symbol, out string block, out string error), error);

            string[] rows = block.Split('\n');
            Assert.Equal("    { ITEM_POTION, 1, 0, 0, 0 }, // New Bark Town", rows[1]);
            Assert.Equal("    { ITEM_RARE_CANDY, 3, 0, 0, 1 }, // Cherrygrove", rows[2]);
            Assert.Equal("}", rows[3]);
            Assert.Equal(2, HgEngineHiddenItems.ParseEntries(block, null).Count);
        }

        [Fact]
        public void HiddenItems_AddedRowsHaveNoCommentAndZeroedUnknowns()
        {
            var entries = HgEngineHiddenItems.ParseEntries(HiddenBlock, null);
            entries.Add(new HgEngineHiddenItems.Entry { ItemId = 50, Quantity = 1, Index = 230 });

            Assert.True(HgEngineHiddenItems.TryBuildArrayBlock(HiddenBlock.Replace("0, 0, 225", "7, 9, 225"), entries, Symbol, out string block, out string error), error);

            string[] rows = block.Split('\n');
            Assert.Equal("    { 0, 1, 7, 9, 225 }, // Cherrygrove", rows[3]);
            Assert.Equal("    { ITEM_RARE_CANDY, 1, 0, 0, 230 },", rows[4]);
        }

        [Fact]
        public void HiddenItems_RefusesABlockWithPreprocessorLines()
        {
            string block = HiddenBlock.Replace("    { ITEM_NUGGET, 1, 0, 0, 1 },", "#ifdef FOO\n    { ITEM_NUGGET, 1, 0, 0, 1 },\n#endif");
            var entries = HgEngineHiddenItems.ParseEntries(HiddenBlock, null);

            Assert.False(HgEngineHiddenItems.TryBuildArrayBlock(block, entries, Symbol, out string rebuilt, out string error));
            Assert.Null(rebuilt);
            Assert.Contains("preprocessor", error);
        }

        // ── New item template and price ────────────────────────────────────────

        private const string RealItemEntry = @"
[ITEM_NONE] =
{
    ITEM_PRICE(0),
    .holdEffect = 0,
    .holdEffectParam = 0,
    .pluckEffect = 0,
    .flingEffect = 0,
    .flingPower = 0,
    .naturalGiftPower = 0,
    .naturalGiftType = TYPE_NORMAL,
    .prevent_toss = FALSE,
    .selectable = FALSE,
    .fieldPocket = POCKET_ITEMS,
    .battlePocket = BATTLE_POCKET_NONE,
    .fieldUseFunc = 0,
    .battleUseFunc = 0,
    .partyUse = 0,
    .partyUseParam = {
        .slp_heal = FALSE,
        .psn_heal = FALSE,
        .brn_heal = FALSE,
        .frz_heal = FALSE,
        .prz_heal = FALSE,
        .cfs_heal = FALSE,
        .inf_heal = FALSE,
        .guard_spec = FALSE,
        .revive = FALSE,
        .revive_all = FALSE,
        .level_up = FALSE,
        .evolve = FALSE,
        .atk_stages = 0,
        .def_stages = 0,
        .spatk_stages = 0,
        .spdef_stages = 0,
        .speed_stages = 0,
        .accuracy_stages = 0,
        .critrate_stages = 0,
        .pp_up = FALSE,
        .pp_max = FALSE,
        .pp_restore = FALSE,
        .pp_restore_all = FALSE,
        .hp_restore = FALSE,
        .hp_ev_up = FALSE,
        .atk_ev_up = FALSE,
        .def_ev_up = FALSE,
        .speed_ev_up = FALSE,
        .spatk_ev_up = FALSE,
        .spdef_ev_up = FALSE,
        .friendship_mod_lo = FALSE,
        .friendship_mod_med = FALSE,
        .friendship_mod_hi = FALSE,
        .hp_ev_up_param = 0,
        .atk_ev_up_param = 0,
        .def_ev_up_param = 0,
        .speed_ev_up_param = 0,
        .spatk_ev_up_param = 0,
        .spdef_ev_up_param = 0,
        .hp_restore_param = 0,
        .pp_restore_param = 0,
        .friendship_mod_lo_param = 0,
        .friendship_mod_med_param = 0,
        .friendship_mod_hi_param = 0,
    },
},
";

        private static List<string> FieldNames(string entry) =>
            Regex.Matches(entry, @"\.(\w+)\s*=").Select(m => m.Groups[1].Value).ToList();

        [Fact]
        public void NewItem_FromTheCheckoutsOwnEntryDeclaresEveryFieldAndIsSelectable()
        {
            string source = "const ITEMDATA __data[] =\n{\n" + RealItemEntry + "\n};\n";
            string entry = HgEngineItemExpansion.BuildNewEntry(source, "ITEM_NEW");

            Assert.StartsWith("\n[ITEM_NEW] =\n{", entry);
            Assert.Equal(FieldNames(RealItemEntry), FieldNames(entry));
            Assert.Contains("ITEM_PRICE(0)", entry);
            Assert.True(HgEngineSourcePatcher.TryGetFieldValue(entry, "ITEM_NEW", new[] { FieldPathSegment.Field("selectable") }, out string selectable));
            Assert.Equal("TRUE", selectable);
        }

        [Fact]
        public void NewItem_FallbackTemplateMatchesARealEntryField_ForField()
        {
            string entry = HgEngineItemExpansion.BuildNewEntry("const ITEMDATA __data[] =\n{\n};\n", "ITEM_NEW");

            Assert.Equal(FieldNames(RealItemEntry), FieldNames(entry));
            Assert.Contains("ITEM_PRICE(0)", entry);
        }

        [Fact]
        public void ItemPrice_ReplacesTheMacroArgumentWithTheFullPrice()
        {
            string text = "[ITEM_A] =\n{\n    ITEM_PRICE(500000),\n    .holdEffect = 0,\n},\n";

            Assert.True(HgEngineItemExpansion.TryReplacePrice(ref text, "ITEM_A", 1234));
            Assert.Contains("ITEM_PRICE(1234),", text);
            Assert.Contains(".holdEffect = 0,", text);
        }

        [Fact]
        public void ItemPrice_WritesAPlainPriceFieldWhenThereIsNoMacro()
        {
            string text = "[ITEM_B] =\n{\n    .price = 0,\n    .holdEffect = 0,\n},\n";

            Assert.True(HgEngineItemExpansion.TryReplacePrice(ref text, "ITEM_B", 300));
            Assert.Contains(".price = 300,", text);
        }

        [Fact]
        public void ItemPrice_FailsWhenTheEntryHasNoPrice()
        {
            string text = "[ITEM_C] =\n{\n    .holdEffect = 0,\n},\n";
            string before = text;

            Assert.False(HgEngineItemExpansion.TryReplacePrice(ref text, "ITEM_C", 300));
            Assert.Equal(before, text);
        }

        [Fact]
        public void CustomItemMessages_OffsetCountsFromTheLastBaseItemInItemMsgOffset()
        {
            string header =
                "#define ITEM_CANARI_BREAD 2684\n" +
                "#define MAX_BASE_ITEM_NUM ITEM_CANARI_BREAD\n" +
                "#define ITEM_MSG_OFFSET(id) (                                                                       \\\n" +
                "    (id) <= ITEM_ENIGMA_STONE ? (id) : (id) <= ITEM_REVEAL_GLASS ? ((id) - (ITEM_ENIGMA_STONE + 1)) \\\n" +
                "        : (id) <= ITEM_CANARI_BREAD                              ? ((id) - (ITEM_LEGEND_PLATE + 1)) \\\n" +
                "                                                                 : ((id) - (ITEM_CANARI_BREAD + 1)))\n";
            var table = HgEngineSymbolTable.Parse(header);

            Assert.Equal(0, HgEngineItemExpansion.CustomMessageOffset(header, table, 2685));
            Assert.Equal(2, HgEngineItemExpansion.CustomMessageOffset(header, table, 2687));
            Assert.Equal(-1, HgEngineItemExpansion.CustomMessageOffset(header, table, 100));
        }

        [Fact]
        public void CustomItemMessages_NameGoesIntoThePlaceholderShape()
        {
            var first = HgEngineItemExpansion.WithCustomNameLine(new List<string> { "a {COLOR 255}Custom Item{COLOR 0}" }, 0, "Moon Cake", null);
            Assert.Equal(new[] { "a {COLOR 255}Moon Cake{COLOR 0}" }, first);

            var second = HgEngineItemExpansion.WithCustomNameLine(first, 1, "Sun Cake", "Moon Cake");
            Assert.Equal(new[] { "a {COLOR 255}Moon Cake{COLOR 0}", "a {COLOR 255}Sun Cake{COLOR 0}" }, second);

            var plural = HgEngineItemExpansion.WithCustomNameLine(new List<string> { "{COLOR 255}Custom Items{COLOR 0}" }, 0, "Moon Cake", null);
            Assert.Equal("{COLOR 255}Moon Cakes{COLOR 0}", plural[0]);
        }

        // ── New move numbering ─────────────────────────────────────────────────

        private const string MovesHeader =
            "#define MOVE_MALIGNANT_CHAIN  922\n" +
            "#define NUM_OF_CANONICAL_MOVES 923\n" +
            "// #define MOVE_CUSTOM_MOVE_1 (NUM_OF_CANONICAL_MOVES)\n" +
            "#define NUM_OF_CUSTOM_MOVES 0\n" +
            "#define NUM_OF_MOVES (NUM_OF_CANONICAL_MOVES + NUM_OF_CUSTOM_MOVES)\n" +
            "#define MOVE_G_MAX_WILDFIRE   (NUM_OF_MOVES - 1 + 1)\n" +
            "#define MOVE_G_MAX_BEFUDDLE   (NUM_OF_MOVES - 1 + 2)\n";

        private const string MovesSource =
            "const MoveSourceEntry sMoveSource[NUM_OF_MOVES + 1] = {\n" +
            "    [MOVE_MALIGNANT_CHAIN] = {\n        .data = { .power = 100 },\n    },\n\n" +
            "    [NUM_OF_MOVES] = {\n        .data = { .power = 100 },\n    },\n" +
            "};\n";

        [Fact]
        public void NewMove_TakesTheOldNumOfMovesSlotAndTheGMaxMovesMoveUp()
        {
            Assert.Equal(923, HgEngineSymbolTable.Parse(MovesHeader).ByName["MOVE_G_MAX_WILDFIRE"]);

            Assert.True(HgEngineMoveExpansion.TryBuildSources(MovesHeader, MovesSource, "Moon Beam", "MOVE_MOON_BEAM", 0, 923, null,
                out string header, out string source, out string error), error);

            var table = HgEngineSymbolTable.Parse(header);
            Assert.Equal(923, table.ByName["MOVE_MOON_BEAM"]);
            Assert.Equal(924, table.ByName["MOVE_G_MAX_WILDFIRE"]);
            Assert.Equal(1, table.ByName["NUM_OF_CUSTOM_MOVES"]);

            int added = source.IndexOf("    [MOVE_MOON_BEAM] = {", StringComparison.Ordinal);
            int terminator = source.IndexOf("    [NUM_OF_MOVES] = {", StringComparison.Ordinal);
            Assert.True(added > 0 && added < terminator);
            Assert.Contains(".name = \"Moon Beam\"", source);
        }

        [Fact]
        public void NewMove_RefusesWhenAnotherMoveWouldKeepTheSameId()
        {
            string header = MovesHeader + "#define MOVE_FIXED_ID 923\n";

            Assert.False(HgEngineMoveExpansion.TryBuildSources(header, MovesSource, "Moon Beam", "MOVE_MOON_BEAM", 0, 923, null,
                out _, out _, out string error));
            Assert.Contains("MOVE_FIXED_ID", error);
        }

        // ── Keeping a field's spelling ─────────────────────────────────────────

        private static readonly Dictionary<string, int> Flags = new()
        {
            ["FLAG_CONTACT"] = 0x01, ["FLAG_PROTECT"] = 0x02, ["FLAG_MIRROR_MOVE"] = 0x10,
            ["FLAG_UNUSABLE_IN_GEN_8"] = 0, ["FLAG_UNUSABLE_UNIMPLEMENTED"] = 0x20, ["TYPE_FAIRY"] = 9, ["TYPE_MYSTERY"] = 9,
        };

        private static int? Resolve(string token) =>
            int.TryParse(token, out int n) ? n : token.StartsWith("0x") ? Convert.ToInt32(token, 16) : Flags.TryGetValue(token, out int v) ? v : null;

        [Fact]
        public void Flags_UnchangedValueKeepsTheOriginalTextIncludingZeroMarkers()
        {
            const string original = "FLAG_MIRROR_MOVE | FLAG_UNUSABLE_IN_GEN_8 | FLAG_PROTECT";
            Assert.Equal(original, HgEngineValueSpelling.MergeFlags(original, 0x12, "FLAG_PROTECT | FLAG_MIRROR_MOVE", Resolve));
        }

        [Fact]
        public void Flags_ChangedValueWritesTheNewExpressionAndKeepsZeroMarkers()
        {
            string merged = HgEngineValueSpelling.MergeFlags("FLAG_MIRROR_MOVE | FLAG_UNUSABLE_IN_GEN_8 | FLAG_PROTECT", 0x13,
                "FLAG_CONTACT | FLAG_PROTECT | FLAG_MIRROR_MOVE", Resolve);
            Assert.Equal("FLAG_MIRROR_MOVE | FLAG_UNUSABLE_IN_GEN_8 | FLAG_PROTECT | FLAG_CONTACT", merged);
        }

        [Fact]
        public void Flags_ChangedValueKeepsTheOriginalOrder()
        {
            Assert.Equal("FLAG_MIRROR_MOVE | FLAG_UNUSABLE_UNIMPLEMENTED | FLAG_CONTACT",
                HgEngineValueSpelling.MergeFlags("FLAG_MIRROR_MOVE | FLAG_UNUSABLE_UNIMPLEMENTED", 0x31,
                    "FLAG_CONTACT | FLAG_MIRROR_MOVE | FLAG_UNUSABLE_UNIMPLEMENTED", Resolve));
            Assert.Equal("FLAG_UNUSABLE_UNIMPLEMENTED | FLAG_CONTACT",
                HgEngineValueSpelling.MergeFlags("FLAG_MIRROR_MOVE | FLAG_UNUSABLE_UNIMPLEMENTED | FLAG_CONTACT", 0x21,
                    "FLAG_CONTACT | FLAG_UNUSABLE_UNIMPLEMENTED", Resolve));
        }

        [Fact]
        public void Flags_ClearingEveryBitLeavesOnlyTheZeroMarkers()
        {
            Assert.Equal("FLAG_UNUSABLE_IN_GEN_8",
                HgEngineValueSpelling.MergeFlags("FLAG_PROTECT | FLAG_UNUSABLE_IN_GEN_8", 0, "0", Resolve));
            Assert.Equal("0", HgEngineValueSpelling.MergeFlags("FLAG_PROTECT | 0x00", 0, "0", Resolve));
        }

        [Fact]
        public void Scalar_KeepsAnAliasWhenTheValueIsUnchanged()
        {
            Assert.Equal("TYPE_MYSTERY", HgEngineValueSpelling.KeepSpelling("TYPE_MYSTERY", 9, "TYPE_FAIRY", Resolve));
            Assert.Equal("TYPE_FAIRY", HgEngineValueSpelling.KeepSpelling("0", 9, "TYPE_FAIRY", Resolve));
            Assert.Equal("TYPE_FAIRY", HgEngineValueSpelling.KeepSpelling(null, 9, "TYPE_FAIRY", Resolve));
        }

        // ── Patch lists ────────────────────────────────────────────────────────

        [Theory]
        [InlineData(HgEnginePatchKind.Hook, "0", true, 0)]
        [InlineData(HgEnginePatchKind.Hook, "7", true, 7)]
        [InlineData(HgEnginePatchKind.Hook, "255", true, 255)]
        [InlineData(HgEnginePatchKind.Hook, "", false, -1)]
        [InlineData(HgEnginePatchKind.Hook, "8", false, -1)]
        [InlineData(HgEnginePatchKind.Hook, "r1", false, -1)]
        [InlineData(HgEnginePatchKind.ArmHook, "12", true, 12)]
        [InlineData(HgEnginePatchKind.ArmHook, " ", false, -1)]
        [InlineData(HgEnginePatchKind.ArmHook, "-1", false, -1)]
        [InlineData(HgEnginePatchKind.ArmHook, "255", false, -1)]
        public void PatchRegister_OnlyValuesMakePyReadsTheWayTheyLookAreAccepted(HgEnginePatchKind kind, string text, bool ok, int expected)
        {
            Assert.Equal(ok, HgEnginePatchList.TryParseRegister(kind, text, out int register, out string error));
            Assert.Equal(expected, register);
            Assert.Equal(ok, error == null);
        }

        [Fact]
        public void PatchParse_KeepsLinesItCannotRewriteAsTheyAre()
        {
            var symbolicRegister = HgEnginePatchList.Parse(HgEnginePatchKind.ArmHook, "arm9 SomeRoutine 02078384 REG_X");
            var threeColumnArm = HgEnginePatchList.Parse(HgEnginePatchKind.ArmHook, "arm9 SomeRoutine 02078384");
            var fiveColumnHook = HgEnginePatchList.Parse(HgEnginePatchKind.Hook, "0012 SomeRoutine 0223B000 1 extra");

            Assert.False(symbolicRegister.Parsed);
            Assert.False(threeColumnArm.Parsed);
            Assert.False(fiveColumnHook.Parsed);
        }

        [Fact]
        public void PatchParse_HookWithoutRegisterRoundTripsToThreeColumns()
        {
            var entry = HgEnginePatchList.Parse(HgEnginePatchKind.Hook, "0012 SomeRoutine 0223B000");

            Assert.True(entry.Parsed);
            Assert.Equal(-1, entry.Register);
            Assert.Equal("0012 SomeRoutine 0223B000", entry.Render());
            Assert.Null(HgEnginePatchList.Problem(entry));
        }

        [Fact]
        public void PatchProblem_FlagsAnArmHookWithNoRegister()
        {
            var entry = new HgEnginePatchEntry { Kind = HgEnginePatchKind.ArmHook, Parsed = true, Symbol = "SomeRoutine", Address = 0x02078384, Register = -1 };
            Assert.NotNull(HgEnginePatchList.Problem(entry));
        }
    }
}
