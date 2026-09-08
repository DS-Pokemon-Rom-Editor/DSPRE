using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>Vs. Seeker on the shared reader: same overlay, offset, 240 rows, and a row that survives a write.</summary>
    [Collection("rom")]
    public class VsSeekerRematchTableTests
    {
        private readonly ITestOutputHelper _out;
        public VsSeekerRematchTableTests(ITestOutputHelper o) => _out = o;

        private static bool OpenPlatinum()
        {
            if (!Directory.Exists(TestRoms.Platinum)) return false;
            SettingsManager.Load();
            try { new RomInfo("CPUE", TestRoms.Platinum); } catch { return false; }
            return VsSeekerRematchTable.IsSupported;
        }

        [SkippableFact]
        public void PlatinumResolvesToItsKnownTable()
        {
            Skip.If(!OpenPlatinum(), "Platinum not unpacked here");

            RematchTable.Location location = VsSeekerRematchTable.Resolve(out string error);

            Assert.Null(error);
            Assert.NotNull(location);
            _out.WriteLine(location.Description);

            Assert.Equal(5, location.OverlayNumber);
            Assert.Equal(RomInfo.vsSeekerRematchTableOffset, (uint)location.Offset);
            Assert.Equal(VsSeekerRematchTable.RowCount, location.RowCount);

            // Fixed layout, so the address is not searched for.
            Assert.False(location.FoundInOverlay);
        }

        [SkippableFact]
        public void EveryRowReadsBackAndSomeCarryRematches()
        {
            Skip.If(!OpenPlatinum(), "Platinum not unpacked here");

            List<RematchTable.Row> rows = VsSeekerRematchTable.ReadAll();
            Assert.Equal(VsSeekerRematchTable.RowCount, rows.Count);

            int withRematches = rows.Count(r => !r.IsEmpty &&
                Enumerable.Range(0, RematchTable.RematchLevelCount)
                    .Any(level => r.Rematch(level) != RematchTable.ChainEnd &&
                                  r.Rematch(level) != RematchTable.NoRematch &&
                                  r.Rematch(level) != r.BaseTrainerId));

            _out.WriteLine($"{withRematches} of {rows.Count} rows lead somewhere new");
            Assert.True(withRematches > 0, "Platinum should have rows with a real rematch in them");
        }

        [SkippableFact]
        public void AWrittenRowComesBackAndTheOriginalIsRestored()
        {
            Skip.If(!OpenPlatinum(), "Platinum not unpacked here");

            List<RematchTable.Row> before = VsSeekerRematchTable.ReadAll();
            Assert.True(before.Count > 4);

            RematchTable.Row original = before[3];
            RematchTable.Row neighbour = before[4];

            try
            {
                RematchTable.Row edited = original.Copy();
                edited.SetRematch(0, 0x0321);

                Assert.True(VsSeekerRematchTable.WriteRow(3, edited, out string error), error);

                List<RematchTable.Row> after = VsSeekerRematchTable.ReadAll();
                Assert.Equal(edited.Ids, after[3].Ids);
                Assert.Equal(neighbour.Ids, after[4].Ids);
            }
            finally
            {
                Assert.True(VsSeekerRematchTable.WriteRow(3, original, out string restoreError), restoreError);
                Assert.Equal(original.Ids, VsSeekerRematchTable.ReadAll()[3].Ids);
            }
        }
    }
}
