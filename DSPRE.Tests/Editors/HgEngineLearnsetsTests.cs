using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DSPRE.HgEngine;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>hg-engine level-up learnsets: full-width entries and edits to learnsets.json.</summary>
    public class HgEngineLearnsetsTests
    {
        private static readonly HgEngineSymbolTable Species = HgEngineSymbolTable.Parse(string.Join("\n",
            "#define SPECIES_NONE       0",
            "#define SPECIES_BULBASAUR  1",
            "#define SPECIES_IVYSAUR    2",
            "#define SPECIES_MAX_MON_NUM (SPECIES_IVYSAUR)",
            "#define SPECIES_MEGA_START (SPECIES_MAX_MON_NUM + 1)",
            "#define SPECIES_MEGA_VENUSAUR (SPECIES_MEGA_START)",
            ""));

        private static readonly HgEngineSymbolTable Moves = HgEngineSymbolTable.Parse(string.Join("\n",
            "#define MOVE_NONE            0",
            "#define MOVE_VINE_WHIP       22",
            "#define MOVE_TACKLE          33",
            "#define MOVE_GROWL           45",
            "#define MOVE_HI_JUMP_KICK    136",
            "#define MOVE_HIGH_JUMP_KICK  136",
            "#define MOVE_MAX_GEYSER      768",
            ""));

        // Shaped like the real file: json.dump(indent=2), CRLF, no trailing newline.
        private static string Source(string nl = "\r\n") => string.Join(nl,
            "{",
            "  \"SPECIES_BULBASAUR\": {",
            "    \"LevelMoves\": [",
            "      {",
            "        \"Level\": 1,",
            "        \"Move\": \"MOVE_TACKLE\"",
            "      },",
            "      {",
            "        \"Level\": 3,",
            "        \"Move\": \"MOVE_HIGH_JUMP_KICK\"",
            "      }",
            "    ],",
            "    \"MachineMoves\": [",
            "      \"MOVE_VINE_WHIP\"",
            "    ],",
            "    \"EggMoves\": [],",
            "    \"TutorMoves\": []",
            "  },",
            "  \"SPECIES_IVYSAUR\": {",
            "    \"LevelMoves\": [",
            "      {",
            "        \"Level\": 1,",
            "        \"Move\": \"MOVE_GROWL\"",
            "      }",
            "    ],",
            "    \"MachineMoves\": [],",
            "    \"EggMoves\": [",
            "      \"MOVE_GROWL\"",
            "    ],",
            "    \"TutorMoves\": []",
            "  }",
            "}");

        private static byte[] U32(params uint[] values) => values.SelectMany(BitConverter.GetBytes).ToArray();

        [Fact]
        public void SyncedRowKeepsMovesAbove511AndReadsBackAtFullWidth()
        {
            const int maxLevelupMoves = 4;
            byte[] table = U32(
                (1u << 16) | 33, 0xFFFF, 0xFFFF, 0xFFFF,
                (1u << 16) | 45, (7u << 16) | 768, (100u << 16) | 1023, 0xFFFF);

            byte[] row = HgEngineLearnsets.ConvertRow(table, maxLevelupMoves * 4, maxLevelupMoves);
            Assert.Equal(U32((1u << 16) | 45, (7u << 16) | 768, (100u << 16) | 1023, HgEngineLearnsets.Terminator), row);

            var data = new LearnsetData(new MemoryStream(row), wide: true);
            Assert.Equal(new (byte, ushort)[] { (1, 45), (7, 768), (100, 1023) }, Enumerable.Range(0, data.list.Count).Select(i => data.list[i]));
            Assert.Equal(new ushort[] { 45, 768, 1023, 0 }, data.GetLearnsetAtLevel(100));
            Assert.Equal(row, data.ToByteArray());
        }

        [Fact]
        public void VanillaEntriesStillUseNineBitMoves()
        {
            byte[] file = new[] { (ushort)((5 << 9) | 33), (ushort)((12 << 9) | 511), (ushort)0xFFFF }
                .SelectMany(BitConverter.GetBytes).ToArray();

            var data = new LearnsetData(new MemoryStream(file));
            Assert.False(data.IsWide);
            Assert.Equal(new (byte, ushort)[] { (5, 33), (12, 511) }, Enumerable.Range(0, data.list.Count).Select(i => data.list[i]));

            byte[] expected = new[] { (ushort)((5 << 9) | 33), (ushort)((12 << 9) | 511), (ushort)0xFFFF, (ushort)0 }
                .SelectMany(BitConverter.GetBytes).ToArray();
            Assert.Equal(expected, data.ToByteArray());
        }

        [Fact]
        public void WritingOneSpeciesLeavesTheRestOfTheFileIdentical()
        {
            string json = Source();
            var entries = new List<(int, int)> { (1, 33), (7, 768), (100, 22) };

            Assert.True(HgEngineLearnsets.TryApplyLevelMoves(json, 1, entries, Species, Moves, out string updated, out string error), error);

            string expectedList = string.Join("\r\n",
                "[",
                "      {",
                "        \"Level\": 1,",
                "        \"Move\": \"MOVE_TACKLE\"",
                "      },",
                "      {",
                "        \"Level\": 7,",
                "        \"Move\": \"MOVE_MAX_GEYSER\"",
                "      },",
                "      {",
                "        \"Level\": 100,",
                "        \"Move\": \"MOVE_VINE_WHIP\"",
                "      }",
                "    ]");
            int listStart = json.IndexOf('[');
            int listEnd = json.IndexOf("    ],", StringComparison.Ordinal) + "    ]".Length;
            Assert.Equal(json.Substring(0, listStart) + expectedList + json.Substring(listEnd), updated);

            Assert.DoesNotContain("\n", updated.Replace("\r\n", ""));
            using var doc = JsonDocument.Parse(updated);
            Assert.Equal(768, MoveIdOf(doc, "SPECIES_BULBASAUR", 1));
        }

        [Fact]
        public void UnchangedListIsANoOpAndKeepsTheFilesAliasName()
        {
            string json = Source();
            var entries = new List<(int, int)> { (1, 33), (3, 136) };

            Assert.True(HgEngineLearnsets.TryApplyLevelMoves(json, 1, entries, Species, Moves, out string updated, out string error), error);
            Assert.Equal(json, updated);
        }

        [Fact]
        public void LineFeedFilesStayLineFeed()
        {
            string json = Source("\n");
            Assert.True(HgEngineLearnsets.TryApplyLevelMoves(json, 2, new List<(int, int)> { (5, 768) }, Species, Moves, out string updated, out string error), error);

            Assert.DoesNotContain("\r", updated);
            Assert.StartsWith(json.Substring(0, json.IndexOf("\"SPECIES_IVYSAUR\"", StringComparison.Ordinal)), updated);
            using var doc = JsonDocument.Parse(updated);
            Assert.Equal(768, MoveIdOf(doc, "SPECIES_IVYSAUR", 0));
        }

        [Fact]
        public void TooManyMovesIsRefusedWithoutAnyOutput()
        {
            var entries = Enumerable.Range(1, HgEngineLearnsets.BuildMaxRowSlots).Select(i => (i, 33)).ToList();

            Assert.False(HgEngineLearnsets.TryApplyLevelMoves(Source(), 1, entries, Species, Moves, out string updated, out string error));
            Assert.Null(updated);
            Assert.Contains("63", error);

            var atLimit = entries.Take(HgEngineLearnsets.BuildMaxRowSlots - 1).ToList();
            Assert.True(HgEngineLearnsets.TryApplyLevelMoves(Source(), 1, atLimit, Species, Moves, out _, out error), error);
        }

        [Fact]
        public void UnknownMoveIdIsRefused()
        {
            Assert.False(HgEngineLearnsets.TryApplyLevelMoves(Source(), 1, new List<(int, int)> { (1, 999) }, Species, Moves, out string updated, out string error));
            Assert.Null(updated);
            Assert.Contains("999", error);
        }

        [Fact]
        public void FormWithoutItsOwnEntryIsAppendedAfterTheLastSpecies()
        {
            string json = Source();
            Assert.True(HgEngineLearnsets.TryApplyLevelMoves(json, 3, new List<(int, int)> { (1, 45) }, Species, Moves, out string updated, out string error), error);

            string closing = "\r\n  }\r\n}";
            Assert.StartsWith(json.Substring(0, json.Length - "\r\n}".Length), updated);
            Assert.EndsWith(closing, updated);
            using var doc = JsonDocument.Parse(updated);
            Assert.Equal(new[] { "SPECIES_BULBASAUR", "SPECIES_IVYSAUR", "SPECIES_MEGA_VENUSAUR" },
                doc.RootElement.EnumerateObject().Select(p => p.Name));
            Assert.Equal(45, MoveIdOf(doc, "SPECIES_MEGA_VENUSAUR", 0));
        }

        // ── Reading LevelMoves ─────────────────────────────────────────────────

        private static readonly Dictionary<string, string> FormToBase = HgEngineLearnsets.ParseFormToBase(string.Join("\n",
            "const u16 UNUSED FormToSpeciesMapping[] =",
            "{",
            "    [SPECIES_MEGA_VENUSAUR - SPECIES_MEGA_START] = SPECIES_IVYSAUR,",
            "};",
            ""));

        [Fact]
        public void ReadingAListGivesFileOrderAndSavingItBackIsANoOp()
        {
            string json = Source();
            Assert.True(HgEngineLearnsets.TryReadLevelMoves(json, 1, Species, Moves, FormToBase, out var list, out string error), error);
            Assert.Equal(new[] { (1, 33), (3, 136) }, list);

            Assert.True(HgEngineLearnsets.TryApplyLevelMoves(json, 1, list, Species, Moves, out string updated, out error), error);
            Assert.Equal(json, updated);
        }

        [Fact]
        public void FormWithoutItsOwnKeyReadsItsBaseSpeciesList()
        {
            Assert.Equal("SPECIES_IVYSAUR", FormToBase["SPECIES_MEGA_VENUSAUR"]);
            Assert.True(HgEngineLearnsets.TryReadLevelMoves(Source(), 3, Species, Moves, FormToBase, out var list, out string error), error);
            Assert.Equal(new[] { (1, 45) }, list);
        }

        [Fact]
        public void FormWithAnEmptyListOfItsOwnReadsItsBaseSpeciesList()
        {
            string json = Source();
            json = json.Substring(0, json.Length - "\r\n}".Length) + string.Join("\r\n", ",",
                "  \"SPECIES_MEGA_VENUSAUR\": {",
                "    \"LevelMoves\": [],",
                "    \"MachineMoves\": []",
                "  }",
                "}");

            Assert.True(HgEngineLearnsets.TryReadLevelMoves(json, 3, Species, Moves, FormToBase, out var list, out string error), error);
            Assert.Equal(new[] { (1, 45) }, list);
        }

        [Fact]
        public void FormWithItsOwnListKeepsIt()
        {
            Assert.True(HgEngineLearnsets.TryApplyLevelMoves(Source(), 3, new List<(int, int)> { (5, 22) }, Species, Moves, out string json, out string error), error);

            Assert.True(HgEngineLearnsets.TryReadLevelMoves(json, 3, Species, Moves, FormToBase, out var list, out error), error);
            Assert.Equal(new[] { (5, 22) }, list);
        }

        [Fact]
        public void SpeciesWithNoKeyAndNoBaseReadsAnEmptyList()
        {
            Assert.True(HgEngineLearnsets.TryReadLevelMoves(Source(), 0, Species, Moves, FormToBase, out var list, out string error), error);
            Assert.Empty(list);
        }

        [Fact]
        public void UnknownMoveNameIsRefusedWhenReading()
        {
            string json = Source().Replace("\"MOVE_GROWL\"", "\"MOVE_NOT_A_MOVE\"");
            Assert.False(HgEngineLearnsets.TryReadLevelMoves(json, 2, Species, Moves, FormToBase, out _, out string error));
            Assert.Contains("MOVE_NOT_A_MOVE", error);
        }

        private static int MoveIdOf(JsonDocument doc, string species, int index)
        {
            string name = doc.RootElement.GetProperty(species).GetProperty("LevelMoves")[index].GetProperty("Move").GetString();
            Assert.True(Moves.TryGetValue(name, out int id));
            return id;
        }
    }
}
