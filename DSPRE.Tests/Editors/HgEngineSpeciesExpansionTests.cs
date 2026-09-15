using System;
using System.IO;
using DSPRE.HgEngine;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Adding a new species: the texts its plan holds, and that nothing reaches disk before the save
    /// commits. Snippets follow the real species.h fakemon block and the end of Species.c.</summary>
    public class HgEngineSpeciesExpansionTests
    {
        private const string Header =
            "#define SPECIES_PECHARUNT 1025\n\n" +
            "#define MAX_CANONICAL_MON_NUM (SPECIES_PECHARUNT)\n\n" +
            "// define your fakemons below like this\n" +
            "// #define SPECIES_FAKEMON_NAME1 (MAX_CANONICAL_MON_NUM + 1)\n\n" +
            "#define NUM_OF_FAKEMONS 0\n\n" +
            "#define SPECIES_MAX_MON_NUM (SPECIES_PECHARUNT + NUM_OF_FAKEMONS)\n";

        private const string SourceHead =
            "const struct SpeciesDataEntry sSpeciesData[] = {\n" +
            "    [SPECIES_PECHARUNT] = {\n" +
            "        .speciesData = {\n" +
            "            .catchRate = 3,\n" +
            "        },\n" +
            "    },\n\n";
        private const string Source = SourceHead + "};\n";

        private static HgEngineSpeciesExpansion.FakemonPlan Plan(string header, string source, string name)
        {
            Assert.True(HgEngineSpeciesExpansion.TryPlanFakemon(name, HgEngineSymbolTable.Parse(header), header, source, out var plan, out string error), error);
            return plan;
        }

        [Fact]
        public void ThePlanHoldsTheTextsTheImmediateAddWrote()
        {
            var plan = Plan(Header, Source, "Test Mon");

            Assert.Equal(1026, plan.SpeciesId);
            Assert.Equal("SPECIES_TEST_MON", plan.Designator);
            Assert.Equal(
                "#define SPECIES_PECHARUNT 1025\n\n" +
                "#define MAX_CANONICAL_MON_NUM (SPECIES_PECHARUNT)\n\n" +
                "// define your fakemons below like this\n" +
                "// #define SPECIES_FAKEMON_NAME1 (MAX_CANONICAL_MON_NUM + 1)\n\n" +
                "#define SPECIES_TEST_MON (MAX_CANONICAL_MON_NUM + 1)\n\n" +
                "#define NUM_OF_FAKEMONS 1\n\n" +
                "#define SPECIES_MAX_MON_NUM (SPECIES_PECHARUNT + NUM_OF_FAKEMONS)\n",
                plan.HeaderText);
            Assert.Equal(SourceHead + HgEngineSpeciesExpansion.BuildSpeciesEntry("SPECIES_TEST_MON", "Test Mon") + "};\n", plan.SourceText);

            var table = HgEngineSymbolTable.Parse(plan.HeaderText);
            Assert.True(table.TryGetValue("SPECIES_TEST_MON", out int id));
            Assert.Equal(plan.SpeciesId, id);
            Assert.True(table.TryGetValue("SPECIES_MAX_MON_NUM", out int max));
            Assert.Equal(1026, max);
        }

        [Fact]
        public void PlanningAgainOnTheWrittenTextsGivesTheNextSpecies()
        {
            var first = Plan(Header, Source, "Test Mon");
            var second = Plan(first.HeaderText, first.SourceText, "Test Mon");

            Assert.Equal(1027, second.SpeciesId);
            Assert.Equal("SPECIES_TEST_MON_2", second.Designator);
            Assert.Contains("#define SPECIES_TEST_MON_2 (MAX_CANONICAL_MON_NUM + 2)\n\n#define NUM_OF_FAKEMONS 2\n", second.HeaderText);
        }

        [Fact]
        public void APlannedSpeciesWritesNothingUntilTheSaveCommits()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dspre-species-add-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string headerPath = Path.Combine(dir, "species.h");
                string sourcePath = Path.Combine(dir, "Species.c");
                File.WriteAllText(headerPath, Header);
                File.WriteAllText(sourcePath, Source);
                var plan = Plan(Header, Source, "Test Mon");
                Assert.NotEqual(Header, plan.HeaderText);

                // A save that is cancelled or fails ends its session without committing.
                using (HgEngineWriteSession.Begin())
                    HgEngineSpeciesExpansion.WriteFakemon(plan, headerPath, sourcePath);
                Assert.Equal(Header, File.ReadAllText(headerPath));
                Assert.Equal(Source, File.ReadAllText(sourcePath));

                using (var session = HgEngineWriteSession.Begin())
                {
                    HgEngineSpeciesExpansion.WriteFakemon(plan, headerPath, sourcePath);
                    Assert.Equal(Header, File.ReadAllText(headerPath));
                    Assert.True(session.Commit(keepLostComments: false, out string error), error);
                }
                Assert.Equal(plan.HeaderText, File.ReadAllText(headerPath));
                Assert.Equal(plan.SourceText, File.ReadAllText(sourcePath));
            }
            finally { Directory.Delete(dir, recursive: true); }
        }
    }
}
