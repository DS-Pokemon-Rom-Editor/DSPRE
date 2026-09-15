using System;
using System.IO;
using DSPRE.HgEngine;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>What a source save does to comments: a cut leaves its neighbour's comment in place, and
    /// the comments a save deletes are found, labelled, and kept at the end of the file when asked.</summary>
    public class HgEngineCommentSafetyTests
    {
        private const string Trainer = "const TrainerData sTrainerData[] = {\n" +
            "    [5] = {\n" +
            "        .name = \"Joey\",\n" +
            "        .data = {\n" +
            "            .trainerType = TRAINER_DATA_TYPE_NOTHING,\n" +
            "            .aiFlags = 0, // no ai\n" +
            "            .battleType = SINGLE_BATTLE,\n" +
            "        },\n" +
            "        .party = {\n" +
            "            {\n" +
            "                .level = 5, // lvl comment\n" +
            "                .species = SPECIES_RATTATA,\n" +
            "            },\n" +
            "            {\n" +
            "                .level = 7,\n" +
            "                .species = SPECIES_PIDGEY /* bird */,\n" +
            "            },\n" +
            "        },\n" +
            "    },\n" +
            "};\n";

        private static readonly FieldPathSegment[] BattleType = { FieldPathSegment.Field("data"), FieldPathSegment.Field("battleType") };

        [Fact]
        public void RemovingAFieldKeepsTheCommentOnTheLineBefore()
        {
            string text = Trainer;
            Assert.True(HgEngineSourcePatcher.TryRemoveField(ref text, "5", BattleType));

            Assert.Contains(".aiFlags = 0, // no ai\n        },", text);
            Assert.DoesNotContain("battleType", text);
        }

        [Fact]
        public void RemovingAFieldTakesItsOwnComment()
        {
            string text = "[1] = {\n    .a = 1,\n    .b = 2, // about b\n    .c = 3,\n};";
            Assert.True(HgEngineSourcePatcher.TryRemoveField(ref text, "1", new[] { FieldPathSegment.Field("b") }));

            Assert.Equal("[1] = {\n    .a = 1,\n    .c = 3,\n};", text);
        }

        [Fact]
        public void RemovingAFieldAfterABlockCommentKeepsTheComma()
        {
            string text = "[1] = { .a = 5 /* x */, .b = 6, .c = 7 };";
            Assert.True(HgEngineSourcePatcher.TryRemoveField(ref text, "1", new[] { FieldPathSegment.Field("b") }));

            Assert.Equal("[1] = { .a = 5 /* x */, .c = 7 };", text);
        }

        [Fact]
        public void ShrinkingAnArrayKeepsTheLastKeptElementsComment()
        {
            string text = "[2] = {\n    .list = {\n        1, // one\n        2, // two\n        3, // three\n    },\n};";
            Assert.True(HgEngineSourcePatcher.TrySetArrayCount(ref text, "2", new[] { FieldPathSegment.Field("list") }, 1, (_, _) => "0"));

            Assert.Equal("[2] = {\n    .list = {\n        1, // one\n    },\n};", text);
        }

        [Fact]
        public void DeletedCommentsAreNamedByTrainerAndPlace()
        {
            string after = Trainer.Replace(" // lvl comment", "").Replace(" /* bird */", "");
            var lost = HgEngineSourceComments.Lost(Path.Combine("repo", "data", "Trainers.c"), Trainer, after);

            Assert.Equal(2, lost.Count);
            Assert.Equal(("trainer 5", "party 1 level", "lvl comment"), (lost[0].Entry, lost[0].Place, lost[0].Text));
            Assert.Equal(("trainer 5", "party 2 species", "bird"), (lost[1].Entry, lost[1].Place, lost[1].Text));
        }

        [Fact]
        public void AMovedCommentIsNotDeleted()
        {
            string after = Trainer.Replace("            .aiFlags = 0, // no ai\n", "            .aiFlags = 0,\n            // no ai\n");
            Assert.Empty(HgEngineSourceComments.Lost("Trainers.c", Trainer, after));
        }

        [Fact]
        public void KeptCommentsGoAtTheEndOnOneLinePerEntry()
        {
            string after = Trainer.Replace(" // lvl comment", "").Replace(" /* bird */", "");
            var lost = HgEngineSourceComments.Lost("Trainers.c", Trainer, after);

            string kept = HgEngineSourceComments.AppendKept(after, lost);

            Assert.Equal(after + "\n// Comments that existed on trainer 5: party 1 level: lvl comment party 2 species: bird\n", kept);
        }

        [Fact]
        public void ASessionWritesNothingUntilCommittedAndCanKeepComments()
        {
            WithFile(path =>
            {
                string after = Trainer.Replace(" // lvl comment", "");
                using (var session = HgEngineWriteSession.Begin())
                {
                    HgEngineFileCache.WriteText(path, after);

                    Assert.Equal(Trainer, File.ReadAllText(path));
                    Assert.Equal(after, HgEngineFileCache.GetText(path));
                    Assert.Single(session.LostComments());
                    Assert.True(session.Commit(keepLostComments: true, out string error), error);
                }

                string written = File.ReadAllText(path);
                Assert.StartsWith(after, written);
                Assert.EndsWith("// Comments that existed on trainer 5: party 1 level: lvl comment\n", written);
            });
        }

        [Fact]
        public void ASessionDroppedWithoutCommitLeavesTheFile()
        {
            WithFile(path =>
            {
                using (HgEngineWriteSession.Begin())
                    HgEngineFileCache.WriteText(path, Trainer.Replace("Joey", "Mikey"));

                Assert.Equal(Trainer, File.ReadAllText(path));
                Assert.Equal(Trainer, HgEngineFileCache.GetText(path));
            });
        }

        [Fact]
        public void AWriteNobodyAskedAboutKeepsWhatItDeletes()
        {
            WithFile(path =>
            {
                HgEngineFileCache.WriteText(path, Trainer.Replace(" // no ai", ""));

                Assert.EndsWith("// Comments that existed on trainer 5: data aiFlags: no ai\n", File.ReadAllText(path));
            });
        }

        private static void WithFile(Action<string> test)
        {
            string dir = Path.Combine(Path.GetTempPath(), "dspre-comment-safety-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "Trainers.c");
            File.WriteAllText(path, Trainer);
            try { test(path); }
            finally { Directory.Delete(dir, recursive: true); }
        }
    }
}
