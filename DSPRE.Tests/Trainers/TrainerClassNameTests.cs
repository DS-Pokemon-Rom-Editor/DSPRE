using System.Collections.Generic;
using System.IO;
using DSPRE;
using Xunit;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>A trainer whose class has no name still lists, so the Trainer Editor opens.</summary>
    [Collection("rom")]
    public class TrainerClassNameTests
    {
        [SkippableFact]
        public void ATrainerWithAClassPastTheClassNamesIsListedByNumber()
        {
            Skip.IfNot(Directory.Exists(TestRoms.HeartGold), "HeartGold is not unpacked here");
            new RomInfo("IPKE", TestRoms.HeartGold);
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerProperties, DirNames.textArchives });

            string path = Filesystem.GetTrainerPropertiesPath(1);
            byte[] original = File.ReadAllBytes(path);
            try
            {
                byte[] patched = (byte[])original.Clone();
                // hg-engine stores the class as a u16 starting at byte 1.
                patched[1] = 999 & 0xFF;
                patched[2] = 999 >> 8;
                File.WriteAllBytes(path, patched);

                string[] names = TrainerNames.GetAll();

                Assert.StartsWith("[01] Class 999 ", names[1]);
            }
            finally
            {
                File.WriteAllBytes(path, original);
            }
        }
    }
}
