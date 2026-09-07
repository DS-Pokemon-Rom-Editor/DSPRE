using System;
using System.IO;
using System.Linq;
using DSPRE;
using Xunit;
using static DSPRE.RomInfo;

namespace DSPRE.Tests.Core
{
    /// <summary>
    /// Proves the whole save path end to end: edit an archive the way an editor does, build the ROM,
    /// then unpack that ROM again and look for the edit in the result.
    ///
    /// This is the shape of failure a round-trip test cannot see. Reading back what you just wrote
    /// only proves the serializer agrees with itself; it says nothing about whether the bytes reached
    /// the ROM. The Building Rotation regression dropped every NARC edit at Save ROM while every
    /// format test still passed.
    /// </summary>
    [Collection("rom")]
    public class SavedRomKeepsArchiveEditsTests
    {
        private const string PlatinumCode = "CPUE";
        private const DirNames Archive = DirNames.trainerProperties;
        private const string RecordId = "0001";

        [SkippableFact]
        public void AnArchiveEditIsStillThereAfterTheRomIsSavedAndReopened()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "the Platinum test project is not available");

            string builtRom;
            byte[] editedBytes;
            int changedOffset;

            using (var experiment = RomExperiment.Open(
                "saved-rom-keeps-archive-edits", TestRoms.Platinum, PlatinumCode))
            {
                experiment.Unpack(Archive);

                string recordPath = Path.Combine(gameDirs[Archive].unpackedDir, RecordId);
                Assert.True(File.Exists(recordPath),
                    $"record {RecordId} is missing from the unpacked {Archive} archive");

                byte[] original = File.ReadAllBytes(recordPath);
                Assert.NotEmpty(original);

                // A sentinel the source byte cannot already be, so a passing assertion later cannot be
                // the original value surviving untouched.
                changedOffset = original.Length - 1;
                byte sentinel = (byte)(original[changedOffset] ^ 0xFF);

                editedBytes = (byte[])original.Clone();
                editedBytes[changedOffset] = sentinel;
                File.WriteAllBytes(recordPath, editedBytes);

                experiment.Note($"archive: {Archive}, record {RecordId}, {original.Length} bytes");
                experiment.Note($"offset {changedOffset}: 0x{original[changedOffset]:X2} -> 0x{sentinel:X2}");

                experiment.Repack(Archive);
                builtRom = experiment.BuildRom("saved-rom-keeps-archive-edits");
                experiment.WriteManifest();
            }

            // Reopen the built ROM as a fresh project, which is what a user gets when they load the
            // ROM they just saved.
            string reopened = Path.Combine(
                Path.GetTempPath(), "DSPRE", "experiments-work",
                "reopened-" + Guid.NewGuid().ToString("N"));
            try
            {
                Assert.True(DSUtils.UnpackRom(builtRom, reopened),
                    "the saved ROM could not be unpacked again");

                SettingsManager.Load();
                new RomInfo(PlatinumCode, reopened);
                DSUtils.TryUnpackNarcs(new System.Collections.Generic.List<DirNames> { Archive });

                string reopenedRecord = Path.Combine(gameDirs[Archive].unpackedDir, RecordId);
                Assert.True(File.Exists(reopenedRecord),
                    $"record {RecordId} is missing after the ROM was reopened");

                byte[] afterSave = File.ReadAllBytes(reopenedRecord);
                Assert.Equal(editedBytes.Length, afterSave.Length);
                Assert.Equal(editedBytes[changedOffset], afterSave[changedOffset]);
                Assert.Equal(editedBytes, afterSave);
            }
            finally
            {
                // This test pointed RomInfo at the reopened copy; put it back on the configured
                // project so the next test in the rom collection does not inherit a deleted path.
                try { new RomInfo(PlatinumCode, TestRoms.Platinum); } catch { }
                if (Directory.Exists(reopened)) Directory.Delete(reopened, recursive: true);
                if (File.Exists(builtRom)) File.Delete(builtRom);
            }
        }
    }
}
