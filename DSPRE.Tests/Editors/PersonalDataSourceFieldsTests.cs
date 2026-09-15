using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using Xunit;
using PersonalDataEditorViewModel = global::DSPRE.Avalonia.ViewModels.Pokemon.PersonalDataEditorViewModel;

namespace DSPRE.Tests
{
    /// <summary>The Personal Data tab's Species.c field map and its staged hg-engine values, on snippets
    /// copied from the real checkout's Species.c and constant headers.</summary>
    public class PersonalDataSourceFieldsTests
    {
        private static readonly Dictionary<string, HgEngineSymbolTable> Headers = new()
        {
            ["include/constants/pokemon.h"] = HgEngineSymbolTable.Parse(string.Join("\n",
                "#define TYPE_NORMAL   0",
                "#define TYPE_POISON   3",
                "#define TYPE_GRASS    12",
                "#define BODY_COLOR_GREEN  3",
                "#define GROWTH_MEDIUM_FAST 0",
                "#define GROWTH_MEDIUM_SLOW 3",
                "#define EGG_GROUP_MONSTER      1",
                "#define EGG_GROUP_GRASS        7")),
            ["include/constants/battle_constants.h"] = HgEngineSymbolTable.Parse(string.Join("\n",
                "#define TYPE_NORMAL   0",
                "#define TYPE_POISON   3",
                "#define TYPE_GRASS    12")),
            ["include/constants/ability.h"] = HgEngineSymbolTable.Parse(string.Join("\n",
                "#define ABILITY_NONE          0",
                "#define ABILITY_OVERGROW      65")),
            ["include/constants/item.h"] = HgEngineSymbolTable.Parse(string.Join("\n",
                "#define ITEM_NONE                        0",
                "#define ITEM_ORAN_BERRY                  155")),
        };

        private static HgEngineSymbolTable Load(string header) => Headers.TryGetValue(header, out var t) ? t : null;

        private const string Bulbasaur = @"{
        .textData = {
            .name = ""Bulbasaur"",
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
                .rare = ITEM_ORAN_BERRY,
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
    }";

        private static PokemonPersonalData Blank() => new(new MemoryStream(new byte[44]));

        [Fact]
        public void ReadsEveryMappedFieldOfARealEntry()
        {
            var data = Blank();
            Assert.True(HgEngineSpeciesPersonalFields.TryRead(new HgEngineSourceBlock(Bulbasaur), data, Load, out string error), error);

            Assert.Equal(new[] { 45, 49, 49, 65, 65, 45 }, new int[] { data.baseHP, data.baseAtk, data.baseDef, data.baseSpAtk, data.baseSpDef, data.baseSpeed });
            Assert.Equal(new[] { 12, 3 }, new[] { (int)data.type1, (int)data.type2 });
            Assert.Equal(1, data.evSpAtk);
            Assert.Equal(new[] { 0, 155 }, new int[] { data.item1, data.item2 });
            Assert.Equal(new[] { 31, 20, 50 }, new int[] { data.genderVec, data.eggSteps, data.baseFriendship });
            Assert.Equal(PokemonGrowthCurve.MediumSlow, data.growthCurve);
            Assert.Equal(new[] { 1, 7 }, new int[] { data.eggGroup1, data.eggGroup2 });
            Assert.Equal(new[] { 65, 0 }, new int[] { data.firstAbility, data.secondAbility });
            Assert.Equal(PokemonDexColor.Green, data.color);
            Assert.False(data.flip);
        }

        [Fact]
        public void WritingTheReadValuesBackGivesTheSameLiterals()
        {
            var entry = new HgEngineSourceBlock(Bulbasaur);
            var data = Blank();
            Assert.True(HgEngineSpeciesPersonalFields.TryRead(entry, data, Load, out string error), error);

            // No existing entry, so every symbolic literal comes from the reverse lookup, not the source spelling.
            var writes = HgEngineSpeciesPersonalFields.BuildWrites(data, null, Load);

            Assert.Equal(HgEngineSpeciesPersonalFields.All.Count, writes.Count);
            int compared = 0;
            foreach (var write in writes)
            {
                Assert.True(entry.TryGetRaw(write.Path, out string raw), string.Concat(write.Path.Select(p => p.ToString())));
                Assert.Equal(raw, write.ValueLiteral);
                compared++;
            }
            Assert.Equal(28, compared);
        }

        [Fact]
        public void AnUnchangedValueKeepsItsSourceSpelling()
        {
            var entry = new HgEngineSourceBlock(Bulbasaur.Replace(".bodyColor = BODY_COLOR_GREEN", ".bodyColor = 3"));
            var data = Blank();
            Assert.True(HgEngineSpeciesPersonalFields.TryRead(entry, data, Load, out string error), error);

            string Written(HgEngineSourceBlock? existing) => HgEngineSpeciesPersonalFields.BuildWrites(data, existing, Load)
                .Single(w => w.Path.Last().Name == "bodyColor").ValueLiteral;

            Assert.Equal("3", Written(entry));
            Assert.Equal("BODY_COLOR_GREEN", Written(null));
            data.color = PokemonDexColor.Blue;
            Assert.Equal("1", Written(entry));
        }

        [Theory]
        [InlineData(".rare = ITEM_ORAN_BERRY", ".rare = ITEM_NOT_DEFINED", "wildHeldItems.rare")]
        [InlineData(".spAttack = 1,", ".spAttack = 4,", "evYields.spAttack")]
        public void AFieldThatCannotBeTakenFailsTheReadAndChangesNothing(string from, string to, string label)
        {
            string text = Bulbasaur;
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, System.Text.RegularExpressions.Regex.Escape(from)));
            var data = Blank();
            data.baseHP = 7;

            Assert.False(HgEngineSpeciesPersonalFields.TryRead(new HgEngineSourceBlock(text.Replace(from, to)), data, Load, out string error));

            Assert.Contains(label, error);
            Assert.Equal(7, data.baseHP);
            Assert.Equal(0, data.item2);
        }

        [Fact]
        public void AnAbsentBlockKeepsTheRecordsValues()
        {
            const string minted = @"{
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
    }";
            var data = Blank();
            data.evSpeed = 2;
            data.item1 = 155;

            Assert.True(HgEngineSpeciesPersonalFields.TryRead(new HgEngineSourceBlock(minted), data, Load, out string error), error);

            Assert.Equal(50, data.baseHP);
            Assert.Equal(2, data.evSpeed);
            Assert.Equal(155, data.item1);
        }

        [Theory]
        [InlineData(1, 90)]
        [InlineData(0, 45)]
        public void AValueWrittenAsAConfigExpressionReadsAndKeepsItsSpelling(int switchValue, int expected)
        {
            const string ternary = "(CUSTOM_CATCH_RATES) ? (90) : (45)";
            string text = Bulbasaur.Replace(".catchRate = 45,", $".catchRate = {ternary},");
            Assert.NotEqual(Bulbasaur, text);
            HgEngineSymbolTable WithConfig(string header) =>
                header == "include/config.h" ? HgEngineSymbolTable.Parse($"#define CUSTOM_CATCH_RATES {switchValue}") : Load(header);
            var entry = new HgEngineSourceBlock(text);
            var data = Blank();

            Assert.True(HgEngineSpeciesPersonalFields.TryRead(entry, data, WithConfig, out string error), error);
            Assert.Equal(expected, data.catchRate);

            string Written() => HgEngineSpeciesPersonalFields.BuildWrites(data, entry, WithConfig)
                .Single(w => w.Path.Last().Name == "catchRate").ValueLiteral;
            Assert.Equal(ternary, Written());
            data.catchRate = 3;
            Assert.Equal("3", Written());
        }

        // ── Staged side-table values ──

        private static readonly HashSet<string> OwNames = new() { "FALSE", "TRUE", "OW_FEMALE_MASK", "SPECIES_VENUSAUR_OVERWORLD_FEMALE" };

        private static bool ValidateOw(string expression, out string error) =>
            HgEngineSpeciesOwFormFemale.TryValidateExpression(expression, OwNames.Contains, out error);

        private static PersonalDataEditorViewModel.HgStaged Loaded(string owFemale = "FALSE") =>
            new(HiddenAbility: 0, BaseExp: 64, BabyMon: 1, RegionalDex: 1, IconPalette: 1, FollowerSize: 0, FollowerBounceIndex: 0,
                OwFemaleForm: owFemale, OwSizeClassIndex: 0, CreateOwEntry: false, OwSpritePath: null);

        [Fact]
        public void StagingRefusesAnInvalidOwFemaleForm()
        {
            var staged = Loaded() with { OwFemaleForm = "OW_FEMALE_MASK |", BaseExp = 100 };

            string error = PersonalDataEditorViewModel.ValidateHgStaged(staged, Loaded(), 3, 2, ValidateOw);

            Assert.NotNull(error);
            Assert.Contains("incomplete", error);
        }

        [Fact]
        public void StagingAcceptsAValidOwFemaleForm()
        {
            var staged = Loaded() with { OwFemaleForm = "OW_FEMALE_MASK | SPECIES_VENUSAUR_OVERWORLD_FEMALE" };

            Assert.Null(PersonalDataEditorViewModel.ValidateHgStaged(staged, Loaded(), 3, 2, ValidateOw));
        }

        [Fact]
        public void StagingDoesNotRefuseAnUntouchedValue()
        {
            // Unchanged values are not written, so what the file already holds can't block a save.
            var loaded = Loaded("SOMETHING_UNKNOWN");
            var staged = loaded with { BaseExp = 100 };

            Assert.Null(PersonalDataEditorViewModel.ValidateHgStaged(staged, loaded, 3, 2, ValidateOw));
        }

        [Fact]
        public void StagingRefusesAPickerOutsideItsList()
        {
            Assert.NotNull(PersonalDataEditorViewModel.ValidateHgStaged(Loaded() with { FollowerBounceIndex = 3 }, Loaded(), 3, 2, ValidateOw));
            Assert.NotNull(PersonalDataEditorViewModel.ValidateHgStaged(Loaded() with { IconPalette = 3 }, Loaded(), 3, 2, ValidateOw));
            Assert.Null(PersonalDataEditorViewModel.ValidateHgStaged(Loaded() with { FollowerBounceIndex = 2 }, Loaded(), 3, 2, ValidateOw));
        }
    }
}
