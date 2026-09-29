using System;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests.Pokemon
{
    /// <summary>Expected values are the retail ones in the pokeheartgold decomp's files/data/mushi/mushi_trainer.csv.</summary>
    [Collection("rom")]
    public class BugContestTrainerFileTests
    {
        private static byte[] RetailFile()
        {
            Skip.If(!Directory.Exists(TestRoms.HeartGold), "HeartGold test project not configured");
            new RomInfo("IPKE", TestRoms.HeartGold);
            string path = Filesystem.GetBugContestTrainerPath();
            Skip.If(!File.Exists(path), "HeartGold project has no mushi_trainer.bin");
            return File.ReadAllBytes(path);
        }

        [SkippableFact]
        public void HeartGoldOpponentsReadAsTheGameHasThem()
        {
            byte[] bytes = RetailFile();
            var file = new BugContestTrainerFile(bytes);

            var first = file.Rows[0, 0];
            Assert.Equal(0, first.NationalDex);
            Assert.Equal(BugContestTrainerFile.AnyDay, first.Day);
            Assert.Equal(123, first.Species);
            Assert.Equal(320, first.Score);
            Assert.Equal(20, first.Variation);
            Assert.Equal((byte)1, file.Rows[0, 2].NationalDex);
            Assert.Equal((byte)2, file.Rows[0, 2].Day);
            Assert.Equal(10, file.Rows[9, 0].Species);
            Assert.Equal(220, file.Rows[9, 0].Score);
            Assert.Equal(110, file.Rows[9, 0].Variation);

            Assert.Empty(file.Problems(o => $"#{o}"));
            Assert.Empty(file.Warnings(o => $"#{o}"));
            Assert.Equal(bytes, file.ToBytes());

            Assert.Equal(new[] { "Don", "Ed", "Abby", "William", "Benny", "Barry", "Cindy", "Josh", "Samuel", "Kipp" },
                BugContestTrainerFile.OpponentNames());
        }

        [SkippableFact]
        public void ANewNominalScoreChangesOnlyItsTwoBytes()
        {
            byte[] bytes = RetailFile();
            var file = new BugContestTrainerFile(bytes);
            file.Rows[0, 0].Score = 360;
            byte[] after = file.ToBytes();

            Assert.Equal(new byte[] { 0x68, 0x01 }, after.Skip(4).Take(2));
            Assert.Equal(bytes.Take(4), after.Take(4));
            Assert.Equal(bytes.Skip(6), after.Skip(6));
            Assert.Equal(340, file.Rows[0, 0].LowestScore);
            Assert.Equal(379, file.Rows[0, 0].HighestScore);
        }

        private static BugContestTrainerFile AnyDayFile(int extraBytes = 0)
        {
            var data = new byte[BugContestTrainerFile.Size + extraBytes];
            for (int i = 0; i < BugContestTrainerFile.Opponents * BugContestTrainerFile.RowsPerOpponent; i++)
            {
                int at = i * BugContestTrainerFile.RowSize;
                data[at + 1] = BugContestTrainerFile.AnyDay;
                data[at + 2] = 10;                          // Caterpie
                BitConverter.GetBytes((ushort)200).CopyTo(data, at + 4);
                BitConverter.GetBytes((ushort)20).CopyTo(data, at + 6);
            }
            for (int i = 0; i < extraBytes; i++) data[BugContestTrainerFile.Size + i] = (byte)(i + 1);
            return new BugContestTrainerFile(data);
        }

        [Fact]
        public void AVariationOfZeroIsRefused()
        {
            var file = AnyDayFile();
            Assert.Empty(file.Problems(o => $"#{o}"));

            file.Rows[3, 5].Variation = 0;
            var problem = Assert.Single(file.Problems(o => $"#{o}"));
            Assert.Contains("#3, row 6", problem);
        }

        [Fact]
        public void AnOpponentWithNoRowForAContestDayIsRefused()
        {
            var file = AnyDayFile();
            for (int r = 0; r < BugContestTrainerFile.RowsPerOpponent; r++) file.Rows[2, r].Day = 1;   // Monday only
            file.Rows[2, 0].Day = 4;
            file.Rows[2, 0].NationalDex = 1;   // Thursday, but only after the National Dex

            var problems = file.Problems(o => $"#{o}");
            Assert.Equal(2, problems.Count);
            Assert.Contains("#2 has no row for Tuesday, Thursday, Saturday before", problems[0]);
            Assert.Contains("#2 has no row for Tuesday, Saturday after", problems[1]);
        }

        [Fact]
        public void DaysWithoutAContestNeedNoRow()
        {
            var file = AnyDayFile();
            for (int r = 0; r < BugContestTrainerFile.RowsPerOpponent; r++) file.Rows[2, r].Day = (byte)(2 + 2 * (r % 3));   // Tuesday, Thursday, Saturday only
            Assert.Empty(file.Problems(o => $"#{o}"));
        }

        [Fact]
        public void ScoresOutsideWhatTheResultsShowAreWarned()
        {
            var file = AnyDayFile();
            file.Rows[0, 0].Score = 10;          // 10 - 20 goes below zero
            file.Rows[1, 0].Score = 990;         // 990 + 20 - 1 is four digits
            Assert.Equal(2, file.Warnings(o => $"#{o}").Count);
            Assert.Empty(file.Problems(o => $"#{o}"));
        }

        [Fact]
        public void BytesPastTheOpponentsAreKept()
        {
            var file = AnyDayFile(extraBytes: 6);
            byte[] bytes = file.ToBytes();
            Assert.Equal(BugContestTrainerFile.Size + 6, bytes.Length);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, bytes.Skip(BugContestTrainerFile.Size));
            Assert.Equal(bytes, new BugContestTrainerFile(bytes).ToBytes());
        }

        [Fact]
        public void AShortFileIsRefused()
        {
            Assert.Throws<InvalidDataException>(() => new BugContestTrainerFile(new byte[BugContestTrainerFile.Size - 1]));
        }
    }
}
