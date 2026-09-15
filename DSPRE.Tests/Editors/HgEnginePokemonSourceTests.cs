using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DSPRE.HgEngine;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Source-text shapes the Pokémon editors read and write on an hg-engine checkout, on
    /// snippets copied from the real data files.</summary>
    public class HgEnginePokemonSourceTests
    {
        private static readonly HgEngineSymbolTable Species = HgEngineSymbolTable.Parse(string.Join("\n",
            "#define SPECIES_NONE 0",
            "#define SPECIES_EEVEE 133",
            "#define SPECIES_UNFEZANT 521",
            "#define SPECIES_SYLVEON 700"));

        private static readonly HgEngineSymbolTable[] ParamTables = { HgEngineSymbolTable.Parse("#define TYPE_FAIRY 17\n"), Species };

        // ── Evolutions.c targets ──

        [Theory]
        [InlineData("MON_WITH_FORM(SPECIES_UNFEZANT, 1)", 521, 1)]
        [InlineData("SPECIES_UNFEZANT | (2 << 11)", 521, 2)]
        [InlineData("SPECIES_UNFEZANT", 521, 0)]
        [InlineData("SPECIES_NONE", 0, 0)]
        [InlineData("4617", 521, 2)]
        public void Evolutions_ResolveTarget_ReadsEveryTargetShape(string token, int species, int form)
        {
            Assert.True(HgEngineEvolutions.ResolveTarget(token, Species, out int gotSpecies, out int gotForm));
            Assert.Equal(species, gotSpecies);
            Assert.Equal(form, gotForm);
        }

        [Theory]
        [InlineData("SPECIES_NOT_DEFINED")]
        [InlineData("MON_WITH_FORM(SPECIES_NOT_DEFINED, 1)")]
        public void Evolutions_ResolveTarget_FailsOnAnUnknownName(string token)
        {
            Assert.False(HgEngineEvolutions.ResolveTarget(token, Species, out _, out _));
        }

        [Fact]
        public void Evolutions_BuildEntryLiteral_WritesAFormWithTheMacro()
        {
            string literal = HgEngineEvolutions.BuildEntryLiteral("EVO_LEVEL_FEMALE", 32, 521, 1, null, Species, ParamTables);

            Assert.Equal("{ EVO_LEVEL_FEMALE, 32, MON_WITH_FORM(SPECIES_UNFEZANT, 1) }", literal);
            var parts = HgEngineSourcePatcher.SplitArrayValue(literal);
            Assert.True(HgEngineEvolutions.ResolveTarget(parts[2], Species, out int species, out int form));
            Assert.Equal((521, 1), (species, form));
        }

        [Theory]
        [InlineData("{ EVO_LEVEL_FEMALE, 32, MON_WITH_FORM(SPECIES_UNFEZANT, 1) }", "EVO_LEVEL_FEMALE", 32, 521, 1)]
        [InlineData("{ EVO_LEVEL_FEMALE, 32, SPECIES_UNFEZANT | (1 << 11) }", "EVO_LEVEL_FEMALE", 32, 521, 1)]
        [InlineData("{ EVO_HAS_MOVE_TYPE, TYPE_FAIRY, SPECIES_SYLVEON }", "EVO_HAS_MOVE_TYPE", 17, 700, 0)]
        public void Evolutions_BuildEntryLiteral_KeepsAnUnchangedEntrysSpelling(string existing, string method, int param, int species, int form)
        {
            Assert.Equal(existing, HgEngineEvolutions.BuildEntryLiteral(method, param, species, form, existing, Species, ParamTables));
        }

        [Fact]
        public void Evolutions_BuildEntryLiteral_RewritesAChangedFormInTheFilesStyle()
        {
            string literal = HgEngineEvolutions.BuildEntryLiteral("EVO_LEVEL_FEMALE", 32, 521, 2,
                "{ EVO_LEVEL_FEMALE, 32, SPECIES_UNFEZANT | (1 << 11) }", Species, ParamTables);

            Assert.Equal("{ EVO_LEVEL_FEMALE, 32, MON_WITH_FORM(SPECIES_UNFEZANT, 2) }", literal);
        }

        // ── SpeciesToOWFormFemale.c values ──

        private const string OwFemaleSnippet = @"
const u16 UNUSED SpeciesToOWFormFemale[] =
{
    [SPECIES_BULBASAUR] = FALSE,
    [SPECIES_VENUSAUR] = OW_FEMALE_MASK | SPECIES_VENUSAUR_OVERWORLD_FEMALE,
    [SPECIES_PICHU] = SPECIES_PICHU_OVERWORLD_SPIKY_EARED,
    [SPECIES_UNFEZANT] = TRUE,
};
";

        private static readonly HashSet<string> OwNames = new()
        {
            "FALSE", "TRUE", "OW_FEMALE_MASK", "SPECIES_VENUSAUR_OVERWORLD_FEMALE", "SPECIES_PICHU_OVERWORLD_SPIKY_EARED",
        };

        [Fact]
        public void OwFemaleForm_AcceptsEveryShapeTheRealTableUses()
        {
            int checkedCount = 0;
            foreach (string designator in new[] { "SPECIES_BULBASAUR", "SPECIES_VENUSAUR", "SPECIES_PICHU", "SPECIES_UNFEZANT" })
            {
                Assert.True(HgEngineFlatArrayField.TryGetRawValue(OwFemaleSnippet, designator, out string raw));
                Assert.True(HgEngineSpeciesOwFormFemale.TryValidateExpression(raw, OwNames.Contains, out string error), $"{raw}: {error}");
                checkedCount++;
            }
            Assert.Equal(4, checkedCount);
        }

        [Theory]
        [InlineData("(OW_FEMALE_MASK) | (SPECIES_VENUSAUR_OVERWORLD_FEMALE + 1)")]
        [InlineData("0x8000 | 3")]
        [InlineData("(1 << 15) | -1")]
        public void OwFemaleForm_AcceptsOtherCompleteExpressions(string expression)
        {
            Assert.True(HgEngineSpeciesOwFormFemale.TryValidateExpression(expression, OwNames.Contains, out string error), error);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("F")]
        [InlineData("OW_FEMALE_MASK |")]
        [InlineData("OW_FEMALE_MASK, FALSE")]
        [InlineData("FALSE;")]
        [InlineData("{ FALSE }")]
        [InlineData("(FALSE")]
        [InlineData("FALSE)")]
        [InlineData("OW_FEMALE_MASK & FALSE")]
        [InlineData("OW_FEMALE_MASK || FALSE")]
        [InlineData("FALSE FALSE")]
        [InlineData("0x")]
        [InlineData("12ab")]
        public void OwFemaleForm_RefusesWhatWouldBreakTheTableOrBuild(string expression)
        {
            Assert.False(HgEngineSpeciesOwFormFemale.TryValidateExpression(expression, OwNames.Contains, out string error));
            Assert.False(string.IsNullOrEmpty(error));
        }

        // ── IconPaletteTable.c numeric designators ──

        private const string IconPaletteSnippet = @"
u8 gIconPalTable[] = {
    [SPECIES_NONE]             = 0,
    [SPECIES_498]     = 0,
    [499]                      = 2, // unown a
    [500]                      = 1, // unown b
    [SPECIES_VICTINI]          = 0,
};
";

        [Fact]
        public void IconPalette_ReadsARowDesignatedByNumber()
        {
            var value = HgEngineIconPalette.FindValue(IconPaletteSnippet, 499, "SPECIES_499");
            Assert.NotNull(value);
            Assert.Equal("2", value.Value);
            Assert.Equal("1", HgEngineIconPalette.FindValue(IconPaletteSnippet, 500, null)?.Value);
            Assert.Null(HgEngineIconPalette.FindValue(IconPaletteSnippet, 600, "SPECIES_600"));
        }

        [Fact]
        public void IconPalette_WritesOnlyTheNumberedRow()
        {
            string text = IconPaletteSnippet;
            Assert.True(HgEngineIconPalette.TryReplaceValue(ref text, 500, "SPECIES_500", 2));

            Assert.Contains("[500]                      = 2, // unown b", text);
            Assert.Contains("[499]                      = 2, // unown a", text);
            Assert.Equal(IconPaletteSnippet.Length, text.Length);
            Assert.Equal("0", HgEngineIconPalette.FindValue(text, 498, "SPECIES_498")?.Value);
        }

        // ── Species.c entries ──

        private const string BulbasaurEntry = @"
const SpeciesDataEntry sSpeciesData[MAX_SPECIES_INCLUDING_FORMS + 1] = {
    [SPECIES_BULBASAUR] = {
        .textData = {
            .name = ""Bulbasaur"",
            .pokedexEntry = ""The seed on its back is filled\\nwith nutrients."",
            .classification = ""Seed Pokémon"",
            .height = ""2’04”"",
            .weight = ""15.2 lbs."",
        },
        .speciesData = {
            .baseStats = {
                .hp = 45,
                .attack = 49,
                .defense = 49,
                .spAttack = 65,
                .spDefense = 65,
                .speed = 45,
            },
            .types = { TYPE_GRASS, TYPE_POISON },
            .catchRate = 45,
            .baseExpRewardPadding = 0,
            .evYields = {
                .hp = 0,
                .attack = 0,
                .defense = 0,
                .spAttack = 1,
                .spDefense = 0,
                .speed = 0,
            },
            .wildHeldItems = {
                .common = ITEM_NONE,
                .rare = ITEM_NONE,
            },
            .genderRatio = 31,
            .hatchCycles = 20,
            .baseFriendship = 50,
            .expRate = GROWTH_MEDIUM_SLOW,
            .eggGroups = { EGG_GROUP_MONSTER, EGG_GROUP_GRASS },
            .abilities = { ABILITY_OVERGROW, ABILITY_NONE },
            .safariFleeRate = 0,
            .bodyColor = BODY_COLOR_GREEN,
            .flipSprite = 0,
        },
        .metricsData = {
            .heightDecimetres = 7,
            .weightHectograms = 69,
            .bodyType = DEX_SEARCH_BODYTYPE_QUADRUPED,
            .femaleTrainerScale = 272,
            .femalePokemonScale = 337,
            .maleTrainerScale = 256,
            .malePokemonScale = 337,
            .femaleTrainerYOffset = 8,
            .femalePokemonYOffset = 24,
            .maleTrainerYOffset = 9,
            .malePokemonYOffset = 24,
        },
    },

};";

        // An entry missing whole blocks: no .evYields, .wildHeldItems or .eggGroups.
        private const string MintedEntry = @"
const SpeciesDataEntry sSpeciesData[MAX_SPECIES_INCLUDING_FORMS + 1] = {
    [SPECIES_TESTMON] = {
        .textData = {
            .name = ""Testmon"",
        },
        .speciesData = {
            .baseStats = {
                .hp = 50,
                .attack = 50,
                .defense = 50,
                .spAttack = 50,
                .spDefense = 50,
                .speed = 50,
            },
            .types = { TYPE_NORMAL, TYPE_NORMAL },
            .catchRate = 45,
            .expRate = GROWTH_MEDIUM_FAST,
            .abilities = { ABILITY_NONE, ABILITY_NONE },
        },
    },
};";

        private static SortedSet<string> FieldPaths(string text, string designator)
        {
            Assert.True(HgEngineSourcePatcher.TryFindEntry(text, designator, out int open, out int close));
            var paths = new SortedSet<string>();
            CollectFieldPaths(text, open, close, "", paths);
            return paths;
        }

        private static void CollectFieldPaths(string text, int open, int close, string prefix, SortedSet<string> paths)
        {
            foreach (var (start, end) in ElementScanner.ElementSpans(text, open, close))
            {
                var m = Regex.Match(text.Substring(start, end - start), @"^\.(\w+)\s*=\s*");
                Assert.True(m.Success, text.Substring(start, end - start));
                string path = prefix + "." + m.Groups[1].Value;
                int valueStart = start + m.Length;
                if (text[valueStart] == '{' && BraceScanner.TryFindMatchingBrace(text, valueStart, out int inner)
                    && text.Substring(valueStart + 1, inner - valueStart - 1).TrimStart().StartsWith('.'))
                    CollectFieldPaths(text, valueStart, inner, path, paths);
                else
                    paths.Add(path);
            }
        }

        [Fact]
        public void SpeciesExpansion_TemplateDeclaresEveryFieldARealEntryHas()
        {
            var expected = FieldPaths(BulbasaurEntry, "SPECIES_BULBASAUR");
            Assert.True(expected.Count > 40);

            string table = "const SpeciesDataEntry sSpeciesData[] = {\n};";
            Assert.True(HgEngineHeaderEditor.TryInsertBeforeFinalCloseBrace(ref table, HgEngineSpeciesExpansion.BuildSpeciesEntry("SPECIES_TESTMON", "Mr \"Q\"")));

            Assert.Equal(expected, FieldPaths(table, "SPECIES_TESTMON"));
            Assert.True(HgEngineSourcePatcher.TryGetFieldValue(table, "SPECIES_TESTMON",
                new[] { FieldPathSegment.Field("textData"), FieldPathSegment.Field("name") }, out string name));
            Assert.Equal("\"Mr \\\"Q\\\"\"", name);
        }

        [Fact]
        public void PersonalData_WritesAnAbsentBlockWhole()
        {
            const string designator = "SPECIES_TESTMON";
            static FieldPathSegment[] P(string field, string sub = null, int index = -1)
            {
                var path = new List<FieldPathSegment> { FieldPathSegment.Field("speciesData"), FieldPathSegment.Field(field) };
                if (sub != null) path.Add(FieldPathSegment.Field(sub));
                if (index >= 0) path.Add(FieldPathSegment.At(index));
                return path.ToArray();
            }
            var fields = new List<HgEngineFieldWrite>
            {
                new(P("baseStats", "hp"), "45"),
                new(P("types", index: 0), "TYPE_GRASS"),
                new(P("types", index: 1), "TYPE_POISON"),
                new(P("catchRate"), "3"),
                new(P("evYields", "hp"), "0"),
                new(P("evYields", "spAttack"), "1"),
                new(P("wildHeldItems", "common"), "ITEM_NONE"),
                new(P("wildHeldItems", "rare"), "ITEM_ORAN_BERRY"),
                new(P("genderRatio"), "31"),
                new(P("eggGroups", index: 0), "EGG_GROUP_MONSTER"),
                new(P("eggGroups", index: 1), "EGG_GROUP_GRASS"),
            };

            string text = MintedEntry;
            string unpatched = text;
            Assert.False(HgEngineSourcePatcher.TryUpsertField(ref unpatched, designator, P("evYields", "hp"), "0"));

            var writes = HgEngineSpeciesPersonalFields.CollapseAbsentParents(fields, p => HgEngineSourcePatcher.TryGetFieldValue(text, designator, p, out _));
            Assert.Equal(8, writes.Count);
            foreach (var w in writes)
                Assert.True(HgEngineSourcePatcher.TryUpsertField(ref text, designator, w.Path, w.ValueLiteral), string.Concat(w.Path.Select(s => s.ToString())));

            string Read(FieldPathSegment[] path) => HgEngineSourcePatcher.TryGetFieldValue(text, designator, path, out string v) ? v : null;
            Assert.Equal("45", Read(P("baseStats", "hp")));
            Assert.Equal("50", Read(P("baseStats", "attack")));
            Assert.Equal("TYPE_GRASS", Read(P("types", index: 0)));
            Assert.Equal("1", Read(P("evYields", "spAttack")));
            Assert.Equal("ITEM_ORAN_BERRY", Read(P("wildHeldItems", "rare")));
            Assert.Equal("EGG_GROUP_GRASS", Read(P("eggGroups", index: 1)));
            Assert.Equal("31", Read(P("genderRatio")));
            Assert.Equal("GROWTH_MEDIUM_FAST", Read(P("expRate")));
        }

        // ── PokeFormDataTbl.c ──

        [Fact]
        public void FormRegistry_RefusesMoreSlotsThanTheTableHas()
        {
            var slots = Enumerable.Range(0, HgEngineFormRegistry.MaxFormSlots + 1)
                .Select(_ => new HgEngineFormRegistry.FormSlot(false, "SPECIES_NONE")).ToList();

            Assert.False(HgEngineFormRegistry.TrySaveSpeciesForms(1, slots, out string error));
            Assert.Contains("32", error);
        }

        // Expected ids follow PokeOtherFormMonsNoGet in src/pokemon.c.
        [Theory]
        [InlineData(479, 0, 479)]
        [InlineData(479, 1, 503)]
        [InlineData(479, 6, 479)]
        [InlineData(386, 3, 498)]
        [InlineData(19, 1, 1100)]
        [InlineData(19, 2, 19)]
        public void FormRegistry_ResolvesTheSpeciesAFormReadsItsDataFrom(int species, int form, int expected)
        {
            int Table(int baseId, int formNo) => baseId == 19 && formNo == 1 ? 1100 : 0;
            Assert.Equal(expected, HgEngineFormRegistry.ResolveFormSpecies(species, form, Table));
        }
    }
}
