using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>The Pokégear rematch table, found in the overlay the project actually has.</summary>
    [Collection("rom")]
    public class PokegearRematchTableTests
    {
        private readonly ITestOutputHelper _out;
        public PokegearRematchTableTests(ITestOutputHelper o) => _out = o;

        private static readonly string HeartGold = TestRoms.HeartGold;

        private static bool OpenHeartGold()
        {
            if (!Directory.Exists(HeartGold)) return false;
            SettingsManager.Load();
            try { new RomInfo("IPKE", HeartGold); } catch { return false; }
            return PokegearRematchTable.IsSupported;
        }

        [SkippableFact]
        public void TheTableIsFoundInTheOverlayItself()
        {
            Skip.If(!OpenHeartGold(), "HeartGold not unpacked here");

            RematchTable.Location location = PokegearRematchTable.Resolve(out string error);

            Assert.Null(error);
            Assert.NotNull(location);
            _out.WriteLine(location.Description);

            Assert.True(location.FoundInOverlay,
                "the table address should come out of the overlay's own code, not the fallback");
            Assert.Equal(RomInfo.pokegearRematchOverlayNumber, location.OverlayNumber);

            // A derived answer that disagrees with vanilla means the search went wrong.
            Assert.Equal(RomInfo.pokegearRematchFallbackTableOffset, (uint)location.Offset);
        }

        [SkippableFact]
        public void TheRowCountComesFromWhereTheOverlayDataEnds()
        {
            Skip.If(!OpenHeartGold(), "HeartGold not unpacked here");

            RematchTable.Location location = PokegearRematchTable.Resolve(out _);
            Assert.NotNull(location);

            uint ramBase = OverlayUtils.OverlayTable.GetRAMAddress(location.OverlayNumber);
            uint ctorStart = OverlayUtils.OverlayTable.GetStaticInitStart(location.OverlayNumber);
            Assert.True(ctorStart > ramBase, "the overlay table should say where the static initialisers start");

            long expected = (ctorStart - ramBase - location.Offset) / RematchTable.RowSize;
            Assert.Equal(expected, location.RowCount);
            _out.WriteLine($"{location.RowCount} rows between 0x{location.Offset:X} and the initialiser list");
        }

        [SkippableFact]
        public void EveryRowIsCalledByExactlyOnePhoneBookEntry()
        {
            Skip.If(!OpenHeartGold(), "HeartGold not unpacked here");

            List<RematchTable.Row> rows = PokegearRematchTable.ReadAll(out _, out string error);
            Assert.Null(error);
            Assert.True(rows.Count > 0, "the rematch table should have rows");

            Assert.True(PokegearPhoneBook.TryReadTrainerIds(out ushort[] phone, out string phoneError), phoneError);

            var callers = phone.Where(id => id != 0).ToList();
            var rowTrainers = rows.Where(r => !r.IsEmpty).Select(r => r.BaseTrainerId).ToList();

            _out.WriteLine($"{rowTrainers.Count} rows, {callers.Count} Pokégear callers");

            // Untouched, the two sets are the same set.
            Assert.Equal(rowTrainers.Count, rowTrainers.Distinct().Count());
            Assert.Empty(callers.Except(rowTrainers));
            Assert.Empty(rowTrainers.Except(callers));
        }

        [SkippableFact]
        public void RowsLookLikeRematchChains()
        {
            Skip.If(!OpenHeartGold(), "HeartGold not unpacked here");

            List<RematchTable.Row> rows = PokegearRematchTable.ReadAll(out _, out _);
            Assert.True(rows.Count > 0);

            int checkedRows = 0;
            foreach (RematchTable.Row row in rows.Where(r => !r.IsEmpty))
            {
                checkedRows++;
                Assert.NotEqual(0, row.BaseTrainerId);

                // Slot 0 doubles as the level-0 battle, so vanilla repeats it in slot 1.
                Assert.Equal(row.BaseTrainerId, row.Rematch(0));

                // Once a level ends the chain, nothing after it can be a battle.
                bool ended = false;
                for (int level = 0; level < RematchTable.RematchLevelCount; level++)
                {
                    if (row.Rematch(level) == RematchTable.ChainEnd) ended = true;
                    else if (ended) Assert.Fail($"row for trainer {row.BaseTrainerId} has a battle after its chain ended");
                }
            }

            Assert.True(checkedRows > 0, "no rows were checked");
            _out.WriteLine($"{checkedRows} rows checked");
        }

        [SkippableFact]
        public void AWrittenRowComesBackAndTheOriginalIsRestored()
        {
            Skip.If(!OpenHeartGold(), "HeartGold not unpacked here");

            RematchTable.Location location = PokegearRematchTable.Resolve(out _);
            Assert.NotNull(location);

            List<RematchTable.Row> before = RematchTable.ReadAll(location);
            Assert.True(before.Count > 1);

            RematchTable.Row original = before[1];
            RematchTable.Row original0 = before[0];

            try
            {
                RematchTable.Row edited = original.Copy();
                edited.BaseTrainerId = 0x0123;
                edited.SetRematch(0, 0x0123);
                edited.SetRematch(1, RematchTable.NoRematch);
                edited.SetRematch(4, RematchTable.ChainEnd);

                Assert.True(PokegearRematchTable.WriteRow(location, 1, edited, out string error), error);

                List<RematchTable.Row> after = RematchTable.ReadAll(location);
                Assert.Equal(edited.Ids, after[1].Ids);

                // Writing one row must not disturb its neighbour.
                Assert.Equal(original0.Ids, after[0].Ids);
            }
            finally
            {
                Assert.True(PokegearRematchTable.WriteRow(location, 1, original, out string restoreError),
                    restoreError);
                Assert.Equal(original.Ids, RematchTable.ReadAll(location)[1].Ids);
            }
        }
    }
}
