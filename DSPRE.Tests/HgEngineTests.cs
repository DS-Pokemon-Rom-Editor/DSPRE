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
            Assert.Equal("Rattata Alolan", name);   // not "Alolan Rattata" — matches the constant verbatim
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
    }
}
