using System;
using System.Collections.Generic;
using DSPRE;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>How the Vs. Seeker table is found and which rows the game mishandles, on synthetic bytes.</summary>
    public class VsSeekerRematchRulesTests
    {
        private const uint RamBase = 0x021D0D80;

        private static void PutWord(byte[] data, int at, uint value) =>
            BitConverter.GetBytes(value).CopyTo(data, at);

        private static void PutRow(byte[] data, int at, params ushort[] ids)
        {
            for (int i = 0; i < ids.Length; i++) BitConverter.GetBytes(ids[i]).CopyTo(data, at + i * 2);
        }

        private static RematchTable.Row Row(params ushort[] ids) => new RematchTable.Row { Ids = ids };

        [Fact]
        public void AgreeingPointersGiveTheTableOffset()
        {
            var data = new byte[0x400];
            int[] words = { 0x10, 0x20, 0x30 };
            foreach (int at in words) PutWord(data, at, RamBase + 0x100);

            Assert.Equal(0x100, RematchTable.ReadPointedOffset(data, RamBase, data.Length, words));

            PutWord(data, 0x30, RamBase + 0x200);
            Assert.Equal(-1, RematchTable.ReadPointedOffset(data, RamBase, data.Length, words));
        }

        [Fact]
        public void APointerOutsideTheOverlayIsRejected()
        {
            var data = new byte[0x400];
            PutWord(data, 0x10, RamBase + 0x1000);
            Assert.Equal(-1, RematchTable.ReadPointedOffset(data, RamBase, data.Length, new[] { 0x10 }));

            PutWord(data, 0x10, 0x02000000);
            Assert.Equal(-1, RematchTable.ReadPointedOffset(data, RamBase, data.Length, new[] { 0x10 }));
        }

        [Fact]
        public void TheRowCountComesFromTheThumbCompare()
        {
            // cmp r2, #0xF0, as both overlays encode it.
            byte[] data = { 0xF0, 0x2A, 0x00, 0xB5 };
            Assert.Equal(240, RematchTable.ReadCompareImmediate(data, 0));
            Assert.Equal(-1, RematchTable.ReadCompareImmediate(data, 2));
            Assert.Equal(-1, RematchTable.ReadCompareImmediate(data, 4));
        }

        [Fact]
        public void TrainerRowsPassAndCodeDoesNot()
        {
            var data = new byte[0x100];
            PutRow(data, 0x10, 14, 14, 0, 0, 0, 0);
            PutRow(data, 0x1C, 21, 627, 628, 0xFFFF, 629, 0);
            Assert.True(RematchTable.LooksLikeTrainerRows(data, 0x10, 2, data.Length));

            // push {r4, lr} / ldr r0, [pc] style halfwords
            PutRow(data, 0x40, 0xB510, 0x4803, 0x6800, 0xBD10, 0x46C0, 0x2000);
            Assert.False(RematchTable.LooksLikeTrainerRows(data, 0x40, 1, data.Length));

            // A row nothing can look up.
            Assert.False(RematchTable.LooksLikeTrainerRows(data, 0x60, 1, data.Length));

            // Rows running past the data.
            Assert.False(RematchTable.LooksLikeTrainerRows(data, 0x10, 30, data.Length));
        }

        [Fact]
        public void AWellFormedRowHasNoProblems()
        {
            var rows = new List<RematchTable.Row>
            {
                Row(14, 14, 0, 0, 0, 0),
                Row(21, 627, 628, 0xFFFF, 629, 0),
                Row(362, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 777),
            };
            for (int r = 0; r < rows.Count; r++) Assert.Empty(VsSeekerRematchTable.Problems(rows, r));
        }

        [Fact]
        public void ASkipTheSearchCanStopOnIsReported()
        {
            var rows = new List<RematchTable.Row>
            {
                Row(21, 627, 628, 0xFFFF, 0, 0),
                Row(22, 0xFFFF, 0, 0, 0, 0),
                Row(23, 627, 628, 629, 630, 0xFFFF),
            };

            Assert.Contains(VsSeekerRematchTable.Problems(rows, 0), p => p.StartsWith("Rematch 3 "));
            Assert.Contains(VsSeekerRematchTable.Problems(rows, 1), p => p.StartsWith("Rematch 1 "));
            Assert.Contains(VsSeekerRematchTable.Problems(rows, 2), p => p.StartsWith("Rematch 5 "));
        }

        [Fact]
        public void ALaterRowWithTheSameTrainerIsNeverUsed()
        {
            var rows = new List<RematchTable.Row>
            {
                Row(14, 14, 0, 0, 0, 0),
                Row(14, 15, 0, 0, 0, 0),
            };

            Assert.Empty(VsSeekerRematchTable.Problems(rows, 0));
            Assert.Contains(VsSeekerRematchTable.Problems(rows, 1), p => p.StartsWith("Row 0 "));
        }
    }
}
