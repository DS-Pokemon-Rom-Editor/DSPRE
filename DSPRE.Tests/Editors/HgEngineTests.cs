using System.Linq;
using DSPRE.HgEngine;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Basic smoke coverage for the hg-engine source-text parsing/patching helpers. There's no real
    /// hg-engine ROM/checkout to integration-test against, so this deliberately stays small: one or two
    /// tests per helper against a synthetic-but-real-shaped snippet, not exhaustive edge-case coverage.
    /// </summary>
    public class HgEngineTests
    {
        // ── HgEngineSourcePatcher: locate/replace/upsert fields in a designated-initializer array ──

        private const string TrainersSnippet = @"
const TrainerData sTrainerData[] = {
    [1] = {
        .name = ""Silver"",
        .data = { .trainerClass = TRAINERCLASS_RIVAL, .battleType = SINGLE_BATTLE },
        .party = {
            { .ivs = 30, .level = 14, .species = SPECIES_GASTLY, .ballSeal = 0 },
            { .ivs = 30, .level = 16, .species = SPECIES_ZUBAT, .ballSeal = 0 },
        },
    },
};
";

        [Fact]
        public void SourcePatcher_TryReplaceField_ReplacesNestedFieldLeavesRestUntouched()
        {
            string text = TrainersSnippet;
            var path = new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("trainerClass") };

            Assert.True(HgEngineSourcePatcher.TryReplaceField(ref text, "1", path, "TRAINERCLASS_ACE_TRAINER"));
            Assert.Contains(".trainerClass = TRAINERCLASS_ACE_TRAINER", text);
            Assert.Contains(".battleType = SINGLE_BATTLE", text);
        }

        [Fact]
        public void SourcePatcher_TryUpsertField_InsertsFieldThatDoesNotExistYet()
        {
            // Needed because hg-engine's optional per-mon fields (e.g. .moves) genuinely aren't declared
            // until a trainer's corresponding data-type flag is first turned on; a replace-only write
            // would silently no-op the first time a user enables one.
            string text = TrainersSnippet;
            var path = new[] { FieldPathSegment.Field("party"), FieldPathSegment.At(0), FieldPathSegment.Field("moves") };

            Assert.True(HgEngineSourcePatcher.TryUpsertField(ref text, "1", path, "{ MOVE_CUT, MOVE_NONE, MOVE_NONE, MOVE_NONE }"));
            Assert.Contains(".moves = { MOVE_CUT, MOVE_NONE, MOVE_NONE, MOVE_NONE },", text);
            Assert.Contains(".species = SPECIES_GASTLY", text);
        }

        private static readonly FieldPathSegment[] PartyPath = { FieldPathSegment.Field("party") };

        private static string NewMon(int index, string indent) => $"{{ .ivs = 0, .level = 1, .species = SPECIES_NONE, .ballSeal = {index} }}";

        [Fact]
        public void SourcePatcher_TrySetArrayCount_DropsPartyMembersPastTheCount()
        {
            string text = TrainersSnippet;

            Assert.True(HgEngineSourcePatcher.TrySetArrayCount(ref text, "1", PartyPath, 1, NewMon));

            var block = new HgEngineSourceBlock(text.Substring(text.IndexOf("[1] = {") + 6));
            var party = block.GetArrayElements(PartyPath);
            Assert.Single(party);
            Assert.Contains("SPECIES_GASTLY", party[0].Raw);
            Assert.DoesNotContain("SPECIES_ZUBAT", text);
            Assert.Contains(".name = \"Silver\"", text);
        }

        [Fact]
        public void SourcePatcher_TrySetArrayCount_AppendsMembersThatFieldWritesCanThenFill()
        {
            string text = TrainersSnippet;

            Assert.True(HgEngineSourcePatcher.TrySetArrayCount(ref text, "1", PartyPath, 4, NewMon));
            Assert.True(HgEngineSourcePatcher.TryUpsertField(ref text, "1",
                new[] { FieldPathSegment.Field("party"), FieldPathSegment.At(3), FieldPathSegment.Field("species") }, "SPECIES_ONIX"));

            var block = new HgEngineSourceBlock(text.Substring(text.IndexOf("[1] = {") + 6));
            var party = block.GetArrayElements(PartyPath);
            Assert.Equal(4, party.Count);
            Assert.Contains("SPECIES_ZUBAT", party[1].Raw);
            Assert.Contains(".ballSeal = 2", party[2].Raw);
            Assert.Contains("SPECIES_ONIX", party[3].Raw);
        }

        [Fact]
        public void SourcePatcher_TryRemoveField_DropsOnlyThatFieldAndIgnoresAnAbsentOne()
        {
            string text = TrainersSnippet;
            var ballSeal = new[] { FieldPathSegment.Field("party"), FieldPathSegment.At(1), FieldPathSegment.Field("ballSeal") };
            var moves = new[] { FieldPathSegment.Field("party"), FieldPathSegment.At(1), FieldPathSegment.Field("moves") };

            Assert.True(HgEngineSourcePatcher.TryRemoveField(ref text, "1", ballSeal));
            string afterRemove = text;
            Assert.True(HgEngineSourcePatcher.TryRemoveField(ref text, "1", moves));

            Assert.Equal(afterRemove, text);
            Assert.False(HgEngineSourcePatcher.TryGetFieldValue(text, "1", ballSeal, out _));
            Assert.True(HgEngineSourcePatcher.TryGetFieldValue(text, "1", new[] { FieldPathSegment.Field("party"), FieldPathSegment.At(0), FieldPathSegment.Field("ballSeal") }, out string first));
            Assert.Equal("0", first);
            Assert.True(HgEngineSourcePatcher.TryGetFieldValue(text, "1", new[] { FieldPathSegment.Field("party"), FieldPathSegment.At(1), FieldPathSegment.Field("species") }, out string species));
            Assert.Equal("SPECIES_ZUBAT", species);
        }

        [Fact]
        public void SourcePatcher_TryUpsertField_PutsANewFieldOnItsOwnLineAfterTheLastOne()
        {
            string text = "const TrainerData d[] = {\n    [2] = {\n        .party = {\n            {\n                .level = 5,\n                .species = SPECIES_ONIX,\n            },\n        },\n    },\n};\n";
            var path = new[] { FieldPathSegment.Field("party"), FieldPathSegment.At(0), FieldPathSegment.Field("ball") };

            Assert.True(HgEngineSourcePatcher.TryUpsertField(ref text, "2", path, "ITEM_POKE_BALL"));

            Assert.Contains("                .species = SPECIES_ONIX,\n                .ball = ITEM_POKE_BALL,\n            },", text);

            string crlf = text.Replace("\n", "\r\n");
            Assert.True(HgEngineSourcePatcher.TrySetArrayCount(ref crlf, "2", new[] { FieldPathSegment.Field("party") }, 2, (i, indent) => "{\n" + indent + "    .level = 1,\n" + indent + "}"));
            Assert.True(HgEngineSourcePatcher.TryUpsertField(ref crlf, "2",
                new[] { FieldPathSegment.Field("party"), FieldPathSegment.At(1), FieldPathSegment.Field("species") }, "SPECIES_ZUBAT"));
            Assert.DoesNotContain("\n", crlf.Replace("\r\n", ""));
            Assert.Contains("SPECIES_ZUBAT", crlf);
        }

        [Fact]
        public void SourcePatcher_ACommentAfterTheLastElementStaysAComment()
        {
            string text = "const TrainerData d[] = {\n    [3] = {\n        .party = {\n            { .level = 5, .species = SPECIES_ONIX } // the rock\n        },\n    },\n};\n";

            Assert.True(HgEngineSourcePatcher.TrySetArrayCount(ref text, "3", new[] { FieldPathSegment.Field("party") }, 2,
                (i, indent) => "{ .level = 1, .species = SPECIES_ZUBAT }"));

            Assert.Contains("{ .level = 5, .species = SPECIES_ONIX }, // the rock", text);
            var party = new HgEngineSourceBlock(text.Substring(text.IndexOf("[3] = {") + 6)).GetArrayElements(new[] { FieldPathSegment.Field("party") });
            Assert.Equal(2, party.Count);
            Assert.Contains("SPECIES_ZUBAT", party[1].Raw);
        }

        [Fact]
        public void SourcePatcher_TryReplaceField_LeavesAnUnchangedMultiLineBlockAsItIs()
        {
            // Headbutt.c and SafariEncounters.c keep their slot lists one per line with comments.
            string text = "const X d[] = {\r\n    [4] = {\r\n        .slots = {\r\n            { SPECIES_HOOTHOOT, 2, 3 }, // morning\r\n            { SPECIES_PINECO, 2, 3 },\r\n        },\r\n    },\r\n};\r\n";
            string original = text;
            var path = new[] { FieldPathSegment.Field("slots") };

            Assert.True(HgEngineSourcePatcher.TryReplaceField(ref text, "4", path, "{ { SPECIES_HOOTHOOT, 2, 3 }, { SPECIES_PINECO, 2, 3 } }"));
            Assert.Same(original, text);

            Assert.True(HgEngineSourcePatcher.TryReplaceField(ref text, "4", path, "{ { SPECIES_HOOTHOOT, 2, 3 }, { SPECIES_PINECO, 5, 6 } }"));
            Assert.Contains("SPECIES_PINECO, 5, 6", text);
            Assert.False(HgEngineSourcePatcher.SameTokens("\"a b\"", "\"ab\""));
        }

        [Fact]
        public void SymbolTable_ResolvesACombinedFlagMacro()
        {
            // trainer_data.h spells F_TRAINER_EXPERT_AI this way.
            var table = HgEngineSymbolTable.Parse(
                "#define F_PRIORITIZE_SUPER_EFFECTIVE (1 << 0)\n#define F_EVALUATE_ATTACKS (1 << 1)\n#define F_EXPERT_ATTACKS (1 << 2)\n" +
                "#define F_TRAINER_EXPERT_AI (F_PRIORITIZE_SUPER_EFFECTIVE | F_EVALUATE_ATTACKS | F_EXPERT_ATTACKS)\n");

            Assert.True(table.TryGetValue("F_TRAINER_EXPERT_AI", out int value));
            Assert.Equal(7, value);
        }

        [Fact]
        public void SymbolTable_TakesTheBranchTheCompilerBuilds()
        {
            // move_data.h defines each marker in both branches of a DISALLOW_DEXIT_GEN test.
            const string header =
                "#if DISALLOW_DEXIT_GEN == 8\n#define FLAG_UNUSABLE_IN_GEN_8 0x20\n#else\n#define FLAG_UNUSABLE_IN_GEN_8 0\n#endif\n" +
                "#if DISALLOW_DEXIT_GEN == 0\n#define FLAG_UNUSABLE_UNIMPLEMENTED 0x20\n#else\n#define FLAG_UNUSABLE_UNIMPLEMENTED 0\n#endif\n" +
                "#ifdef MEGA_EVOLUTIONS\n#define MEGA_ONLY 1\n#endif\n";

            var undefined = HgEngineSymbolTable.Parse(header);
            Assert.True(undefined.TryGetValue("FLAG_UNUSABLE_IN_GEN_8", out int gen8));
            Assert.True(undefined.TryGetValue("FLAG_UNUSABLE_UNIMPLEMENTED", out int unimplemented));
            Assert.Equal(0, gen8);
            Assert.Equal(0x20, unimplemented);
            Assert.False(undefined.TryGetValue("MEGA_ONLY", out _));

            var gen8Build = HgEngineSymbolTable.Parse(header, new System.Collections.Generic.Dictionary<string, int> { ["DISALLOW_DEXIT_GEN"] = 8, ["MEGA_EVOLUTIONS"] = 1 });
            Assert.True(gen8Build.TryGetValue("FLAG_UNUSABLE_IN_GEN_8", out gen8));
            Assert.True(gen8Build.TryGetValue("FLAG_UNUSABLE_UNIMPLEMENTED", out unimplemented));
            Assert.Equal(0x20, gen8);
            Assert.Equal(0, unimplemented);
            Assert.True(gen8Build.TryGetValue("MEGA_ONLY", out _));
        }

        [Fact]
        public void SymbolTable_ResolvesAChainOfAdditionsAndSubtractions()
        {
            var table = HgEngineSymbolTable.Parse("#define NUM_OF_MOVES 923\n#define MOVE_G_MAX_WILDFIRE (NUM_OF_MOVES - 1 + 1)\n");

            Assert.True(table.TryGetValue("MOVE_G_MAX_WILDFIRE", out int value));
            Assert.Equal(923, value);
        }

        [Fact]
        public void FlatArrayField_KeepsAValueWithACommaWholeAndFindsALastEntryWithoutOne()
        {
            string text = "const u16 T[] = {\r\n    [SPECIES_A] = MON_WITH_FORM(SPECIES_B, 1), // note\r\n    [SPECIES_C] = 7\r\n};\r\n";

            Assert.True(HgEngineFlatArrayField.TryGetRawValue(text, "SPECIES_A", out string a));
            Assert.Equal("MON_WITH_FORM(SPECIES_B, 1)", a);
            Assert.True(HgEngineFlatArrayField.TrySetRawValue(ref text, "SPECIES_A", "MON_WITH_FORM(SPECIES_B, 2)"));
            Assert.True(HgEngineFlatArrayField.TrySetRawValue(ref text, "SPECIES_C", "8"));

            Assert.Equal("const u16 T[] = {\r\n    [SPECIES_A] = MON_WITH_FORM(SPECIES_B, 2), // note\r\n    [SPECIES_C] = 8\r\n};\r\n", text);
        }

        [Fact]
        public void ToCStringLiteral_TurnsATypedLineBreakIntoTheGamesMarkup()
        {
            string literal = HgEngineTrainerSource.ToCStringLiteral("Hi!\r\nBye");

            Assert.Equal("\"Hi!\\\\nBye\"", literal);
            Assert.Equal("Hi!\\nBye", new HgEngineSourceBlock("{ .text = " + literal + " }").TryGetString(new[] { FieldPathSegment.Field("text") }, out string back) ? back : null);
        }

        [Theory]
        [InlineData("MON_WITH_FORM(479, 2)", 479, 2)]
        [InlineData("479 | (3 << 11)", 479, 3)]
        [InlineData("0x1DF", 479, 0)]
        [InlineData("4575", 479, 2)]
        public void TrainerSource_TryParseSpecies_ReadsTheFormInEverySpelling(string raw, int species, int form)
        {
            Assert.True(HgEngineTrainerSource.TryParseSpecies(raw, null, out int gotSpecies, out int gotForm));
            Assert.Equal(species, gotSpecies);
            Assert.Equal(form, gotForm);
            Assert.Equal(form > 0 ? $"MON_WITH_FORM(SPECIES_ROTOM, {form})" : "SPECIES_ROTOM", HgEngineTrainerSource.FormatSpecies("SPECIES_ROTOM", gotForm));
        }

        [Theory]
        [InlineData("Sparky", "Sparky")]
        [InlineData("Señor Mime", "SeorMime")]
        [InlineData("ABCDEFGHIJKL", "ABCDEFGHIJ")]
        public void TrainerSource_ToEncodableNickname_KeepsOnlyWhatTheBuildCanEncode(string typed, string kept)
        {
            Assert.Equal(kept, HgEngineTrainerSource.ToEncodableNickname(typed));
        }

        [Fact]
        public void SourcePatcher_SplitArrayValue_SkipsTrailingLineCommentsBetweenElements()
        {
            // Regression guard: a trailing "// label" comment between elements has no comma of its own to
            // separate it from the next value; this previously dropped every entry after the first.
            const string snippet = "{ { ITEM_POTION, 0 }, // New Bark Town\n{ ITEM_NUGGET, 1 }, // Cherrygrove\n};";
            var items = HgEngineSourcePatcher.SplitArrayValue(snippet.Substring(0, snippet.LastIndexOf('}') + 1));
            Assert.Equal(2, items.Count);
        }

        // ── HgEngineSymbolTable: resolve #define / enum constants from real header text ──

        private const string HeaderSnippet = @"
#define SPECIES_BULBASAUR 1
#define SPECIES_MAX_MON_NUM 1075
#define SPECIES_MEGA_START (SPECIES_MAX_MON_NUM + 1)
#define SPECIES_MEGA_VENUSAUR (SPECIES_MEGA_START)
#define FLAG_CONTACT 0x01
#define F_DOUBLE_BATTLE (1 << 7)
// #define SPECIES_FAKEMON_NAME1 (SPECIES_MEGA_START)

enum EvoMethod {
    EVO_NONE = 0,
    EVO_FRIENDSHIP,
    EVO_LEVEL,
};
";

        [Fact]
        public void SymbolTable_ResolvesPlainAndChainedDefines()
        {
            var table = HgEngineSymbolTable.Parse(HeaderSnippet);
            Assert.True(table.TryGetValue("SPECIES_BULBASAUR", out int a));
            Assert.Equal(1, a);
            Assert.True(table.TryGetValue("SPECIES_MEGA_VENUSAUR", out int b));
            Assert.Equal(1076, b);
        }

        [Fact]
        public void SymbolTable_ResolvesHexLiteralsAndShiftExpressions()
        {
            // Regression guard: the operand regex used to only match "-?\d+", silently failing every
            // hex-valued #define; a separate fix was needed for "(1 << N)" shift expressions.
            var table = HgEngineSymbolTable.Parse(HeaderSnippet);
            Assert.True(table.TryGetValue("FLAG_CONTACT", out int flag));
            Assert.Equal(0x01, flag);
            Assert.True(table.TryGetValue("F_DOUBLE_BATTLE", out int shifted));
            Assert.Equal(128, shifted);
        }

        [Fact]
        public void SymbolTable_ResolvesImplicitlyNumberedEnumMembers()
        {
            var table = HgEngineSymbolTable.Parse(HeaderSnippet);
            Assert.True(table.TryGetValue("EVO_FRIENDSHIP", out int v));
            Assert.Equal(1, v);
        }

        [Fact]
        public void SymbolTable_CommentedOutDefineIsNotParsedAsReal()
        {
            var table = HgEngineSymbolTable.Parse(HeaderSnippet);
            Assert.False(table.TryGetValue("SPECIES_FAKEMON_NAME1", out _));
        }

        // ── HgEngineTrainerSource / HgEngineSourceBlock: typed read accessor over a source block ──

        [Fact]
        public void SourceBlock_ReadsPlainAndNestedLiteralFields()
        {
            var block = new HgEngineSourceBlock("{ .level = 16, .setIvs = { .hp = 31 } }");
            Assert.True(block.TryGetInt(new[] { FieldPathSegment.Field("level") }, out int level));
            Assert.Equal(16, level);
            Assert.True(block.TryGetInt(new[] { FieldPathSegment.Field("setIvs"), FieldPathSegment.Field("hp") }, out int hp));
            Assert.Equal(31, hp);
        }

        [Fact]
        public void SourceBlock_TryGetSymbol_FailsClosedWithoutALinkedCheckout()
        {
            var block = new HgEngineSourceBlock("{ .species = SPECIES_ZUBAT }");
            Assert.False(block.TryGetSymbol(new[] { FieldPathSegment.Field("species") }, null, out _));
        }

        [Fact]
        public void ToCStringLiteral_RoundTripsQuotesAndBackslashesThroughTryGetString()
        {
            string original = "Say, \"hello\" \\ friend";
            string literal = HgEngineTrainerSource.ToCStringLiteral(original);
            var block = new HgEngineSourceBlock("{ .text = " + literal + " }");

            Assert.True(block.TryGetString(new[] { FieldPathSegment.Field("text") }, out string roundTripped));
            Assert.Equal(original, roundTripped);
        }

        // ── Small, single-purpose helpers: one happy-path test each ──

        [Fact]
        public void Evolutions_ResolveTarget_SplitsPackedSpeciesAndFormBits()
        {
            // species 2, form 3: 2 | (3 << 11)
            HgEngineEvolutions.ResolveTarget("6146", null, out int id, out int form);
            Assert.Equal(2, id);
            Assert.Equal(3, form);
        }

        [Fact]
        public void FormNames_DerivesAReadableNamePreservingConstantWordOrder()
        {
            var table = HgEngineSymbolTable.Parse("#define SPECIES_RATTATA_ALOLAN 1078\n");
            Assert.True(HgEngineFormNames.TryReadableNameFromConstant(1078, table, out string name));
            Assert.Equal("Rattata Alolan", name);   // not "Alolan Rattata", matches the constant verbatim
        }

        [Fact]
        public void Headbutt_ResolvesFieldNameByEmbeddedIndexNotDeclarationOrder()
        {
            const string snippet = @"
typedef struct PACKED HeadbuttArchiveData {
    HeadbuttFile_002_Union_Room unionRoom;
    HeadbuttFile_009_Route_1 route1;
} HeadbuttArchiveData;
";
            Assert.True(HgEngineHeadbutt.TryFindFieldName(snippet, 9, out string field));
            Assert.Equal("route1", field);
        }

        [Fact]
        public void HiddenItems_ParsesRealEntriesDespitePerEntryTrailingComments()
        {
            const string snippet = "{\n    { ITEM_POTION, 1, 0, 0, 0 }, // New Bark Town\n    { ITEM_NUGGET, 1, 0, 0, 1 }, // Cherrygrove\n}";
            var entries = HgEngineHiddenItems.ParseEntries(snippet, null);
            Assert.Equal(2, entries.Count);
            Assert.Equal(new[] { 0, 1 }, entries.Select(e => e.Index));
        }

        [Fact]
        public void NameSlug_ToSlugProducesAValidUppercaseIdentifier()
        {
            Assert.Equal("MR_MIME_S_ITEM", HgEngineNameSlug.ToSlug("Mr. Mime's Item!"));
        }

        [Fact]
        public void NameSlug_ToUniqueSlugAppendsANumericSuffixOnCollision()
        {
            var table = HgEngineSymbolTable.Parse("#define ITEM_FIRE_BLAST 5\n");
            Assert.Equal("FIRE_BLAST_2", HgEngineNameSlug.ToUniqueSlug("Fire Blast", table, "ITEM_"));
        }

        [Fact]
        public void OverworldFollowerSprite_InsertedEntryLandsBeforeTheTerminatorSentinel()
        {
            string text = @"
struct OVERWORLD_TAG gOWTagToFileNum[] = {
    MON_FOLLOWER_ENTRY(SPECIES_BULBASAUR, OVERWORLD_SIZE_SMALL)
    { 0xFFFF, 0, 0 },
};
";
            Assert.True(HgEngineOverworldFollowerSprite.TryInsertEntry(ref text, "SPECIES_CHARMANDER", "OVERWORLD_SIZE_SMALL"));

            int insertedAt = text.IndexOf("MON_FOLLOWER_ENTRY(SPECIES_CHARMANDER", System.StringComparison.Ordinal);
            int terminatorAt = text.IndexOf("{ 0xFFFF, 0, 0 },", System.StringComparison.Ordinal);
            Assert.True(insertedAt >= 0 && insertedAt < terminatorAt);
        }

        [Fact]
        public void PokemonIcons_ParsesIdToIconPathMappingFromMakefileRules()
        {
            const string mk =
                "build/pokemonicon/1_0001.NCGR: data/graphics/sprites/bulbasaur/icon.png\n" +
                "\t$(GFX) $< $@ -clobbersize -version101 -bitdepth 4\n\n" +
                "ICONGFX_OBJS += build/pokemonicon/1_0001.NCGR\n";

            var map = HgEnginePokemonIcons.ParseMap(mk);
            Assert.Equal("data/graphics/sprites/bulbasaur/icon.png", map[1]);
            Assert.Single(map);   // the ICONGFX_OBJS accumulator line must not be double-counted
        }

        [Fact]
        public void SafariEncounters_ReadsDoublyNestedFieldsFromARealShapedSnippet()
        {
            const string snippet = @"
const SafariZoneAreaEncounterFile __data[] = {
    [SAFARI_ZONE_AREA_PLAINS] = {
        .land = {
            .speciesMorning = { { SPECIES_RATTATA, 15 }, { SPECIES_ABRA, 15 } },
        },
    },
};
";
            Assert.True(HgEngineSourcePatcher.TryGetFieldValue(snippet, "SAFARI_ZONE_AREA_PLAINS",
                new[] { FieldPathSegment.Field("land"), FieldPathSegment.Field("speciesMorning") }, out string raw));
            var slots = HgEngineSourcePatcher.SplitArrayValue(raw);
            Assert.Equal(2, slots.Count);
            Assert.Contains("SPECIES_RATTATA", slots[0]);
        }

        // ── HgEngineSpriteOffsets: SpriteFrame[10] array (frontFrames/backFrames) read + write ──

        private static string TwoSlotFrameArray(string field) =>
            "{ " + field + " = { " +
            "{ .frameNo = 0, .duration = 4, .horizontalShift = 0, .verticalShift = 0 }, " +
            "{ .frameNo = 1, .duration = 10, .horizontalShift = -2, .verticalShift = 3 }, " +
            "{ .frameNo = -1, .duration = 0, .horizontalShift = 0, .verticalShift = 0 } } }";

        [Fact]
        public void SpriteOffsets_ReadFrameSlotsReadsEveryElementVerbatimIncludingUnusedTrailingSlots()
        {
            var block = new HgEngineSourceBlock(TwoSlotFrameArray(".frontFrames"));
            var slots = HgEngineSpriteOffsets.ReadFrameSlots(block, "frontFrames");

            Assert.Equal(3, slots.Count);   // reads through the terminator: every slot is needed to play the run
            Assert.Equal(0, slots[0].FrameNo); Assert.Equal(4, slots[0].Duration);
            Assert.Equal(1, slots[1].FrameNo); Assert.Equal(10, slots[1].Duration);
            Assert.Equal(-2, slots[1].HorizontalShift); Assert.Equal(3, slots[1].VerticalShift);
            Assert.Equal(-1, slots[2].FrameNo);
        }

        // A real entry puts spriteYOffset/shadowXOffset/shadowSize after two nested SpriteFrame[10] arrays.
        // Those writes are issued with unresolvedFields discarded, so a scan that fails to step over the
        // nested braces would drop them silently rather than reporting anything.
        private const string SpriteOffsetsEntry = @"
const SpriteFrameData __data[] = {
    [SPECIES_VENUSAUR] = {
        .frontHeader = { .cryDelay = 0, .animation = 1, .animationDelay = 4 },
        .frontFrames = {
                { .frameNo = 1, .duration = 18, .horizontalShift = -4, .verticalShift = 0 },
                { .frameNo = -1, .duration = 0, .horizontalShift = 0, .verticalShift = 0 },
        },
        .backHeader = { .cryDelay = 12, .animation = 132, .animationDelay = 6 },
        .backFrames = {
                { .frameNo = 0, .duration = 18, .horizontalShift = 0, .verticalShift = 0 },
                { .frameNo = -1, .duration = 0, .horizontalShift = 0, .verticalShift = 0 },
        },
        .spriteYOffset = -1,
        .shadowXOffset = -3,
        .shadowSize = 3,
    },
};
";

        [Fact]
        public void SpriteOffsets_ScalarFieldsAfterTheNestedFrameArraysAreReadableAndWritable()
        {
            string text = SpriteOffsetsEntry;

            foreach (var (field, expected) in new[] { ("spriteYOffset", "-1"), ("shadowXOffset", "-3"), ("shadowSize", "3") })
            {
                Assert.True(HgEngineSourcePatcher.TryGetFieldValue(text, "SPECIES_VENUSAUR",
                    new[] { FieldPathSegment.Field(field) }, out string raw), field);
                Assert.Equal(expected, raw.Trim());
            }

            Assert.True(HgEngineSourcePatcher.TryReplaceField(ref text, "SPECIES_VENUSAUR",
                new[] { FieldPathSegment.Field("spriteYOffset") }, "7"));
            Assert.True(HgEngineSourcePatcher.TryReplaceField(ref text, "SPECIES_VENUSAUR",
                new[] { FieldPathSegment.Field("shadowSize") }, "2"));

            Assert.True(HgEngineSourcePatcher.TryGetFieldValue(text, "SPECIES_VENUSAUR",
                new[] { FieldPathSegment.Field("spriteYOffset") }, out string y));
            Assert.Equal("7", y.Trim());
            Assert.True(HgEngineSourcePatcher.TryGetFieldValue(text, "SPECIES_VENUSAUR",
                new[] { FieldPathSegment.Field("shadowSize") }, out string size));
            Assert.Equal("2", size.Trim());

            // The frame arrays either side of them are untouched.
            Assert.True(HgEngineSourcePatcher.TryGetFieldValue(text, "SPECIES_VENUSAUR",
                new[] { FieldPathSegment.Field("frontFrames"), FieldPathSegment.At(0), FieldPathSegment.Field("horizontalShift") }, out string shift));
            Assert.Equal("-4", shift.Trim());
        }

        [Fact]
        public void SpriteOffsets_BuildFrameWritesRoundTripsThroughTryReplaceField()
        {
            string text = "const SpriteFrameData __data[] = {\n    [SPECIES_BULBASAUR] = " + TwoSlotFrameArray(".frontFrames") + ",\n};\n";

            var edited = new System.Collections.Generic.List<DSPRE.ROMFiles.SpriteFrameSlot>
            {
                new DSPRE.ROMFiles.SpriteFrameSlot(frameNo: 5, duration: 20, horizontalShift: 1, verticalShift: -1),
            };
            foreach (var write in HgEngineSpriteOffsets.BuildFrameWrites("frontFrames", edited))
                Assert.True(HgEngineSourcePatcher.TryReplaceField(ref text, "SPECIES_BULBASAUR", write.Path, write.ValueLiteral));

            Assert.True(HgEngineSourcePatcher.TryGetFieldValue(text, "SPECIES_BULBASAUR",
                new[] { FieldPathSegment.Field("frontFrames"), FieldPathSegment.At(0), FieldPathSegment.Field("frameNo") }, out string frameNo));
            Assert.Equal("5", frameNo.Trim());
            Assert.Contains("SPECIES_BULBASAUR", text);   // other entries/labels untouched
        }
    }
}
