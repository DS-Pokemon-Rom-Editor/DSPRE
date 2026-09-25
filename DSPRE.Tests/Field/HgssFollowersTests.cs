using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests.Field
{
    /// <summary>Expected values are the retail ones in the pokeheartgold decomp's follow_mon.c and object tables.</summary>
    [Collection("rom")]
    public class HgssFollowersTests
    {
        [SkippableFact]
        public void HeartGoldFollowersReadAsTheGameHasThem()
        {
            Skip.If(!Directory.Exists(TestRoms.HeartGold), "HeartGold test project not configured");
            new RomInfo("IPKE", TestRoms.HeartGold);
            Assert.Null(HgssFollowers.WhyNot());

            var steelix = HgssFollowers.ModelsOf(208);
            Assert.Equal(new[] { "Normal", "Female" }, steelix.Select(m => m.label));
            Assert.Equal(new[] { "Normal", "Female" }, HgssFollowers.ModelsOf(25).Select(m => m.label));
            foreach (var (index, label) in steelix)
            {
                var m = HgssFollowers.Read(index, label);
                Assert.Equal(HgssFollowers.Size.Large, HgssFollowers.SizeOf(m.Bits));
                Assert.True(m.TooTall);
                Assert.Equal(64, m.TextureWidth);
            }

            var pikachu = HgssFollowers.Read(HgssFollowers.ModelsOf(25)[0].index, "Normal");
            Assert.Equal(HgssFollowers.SmallBits, pikachu.Bits);
            Assert.False(pikachu.TooTall);
            Assert.Equal(HgssFollowers.Walks, pikachu.Motion);
            Assert.Equal(32, pikachu.TextureWidth);

            Assert.Equal(HgssFollowers.Flies, HgssFollowers.Read(HgssFollowers.ModelsOf(12)[0].index, "Normal").Motion);
            Assert.Equal(HgssFollowers.Hovers, HgssFollowers.Read(HgssFollowers.ModelsOf(131)[0].index, "Normal").Motion);
            Assert.Equal(28, HgssFollowers.ModelsOf(201).Count);
            Assert.Equal(565, HgssFollowers.ModelsOf(493).Last().index);
        }

        [SkippableFact]
        public void AWrittenFollowerReadsBackAndLeavesItsNeighboursAlone()
        {
            Skip.If(!Directory.Exists(TestRoms.HeartGold), "HeartGold test project not configured");
            new RomInfo("IPKE", TestRoms.HeartGold);
            Skip.If(HgssFollowers.WhyNot() != null, HgssFollowers.WhyNot() ?? "");

            int model = HgssFollowers.ModelsOf(25)[0].index;
            var before = HgssFollowers.Read(model, "Normal");
            var next = HgssFollowers.Read(model + 1, "Normal");
            string paramPath = Path.Combine(RomInfo.gameDirs[RomInfo.DirNames.followerParams].unpackedDir, model.ToString("D4"));
            byte[] overlay = File.ReadAllBytes(RomInfo.OWtablePath), param = File.ReadAllBytes(paramPath);
            try
            {
                var edited = HgssFollowers.Read(model, "Normal");
                edited.Bits = HgssFollowers.LargeBits; edited.TooTall = true; edited.Motion = HgssFollowers.Hovers;
                HgssFollowers.Write(edited);

                var after = HgssFollowers.Read(model, "Normal");
                Assert.Equal(HgssFollowers.LargeBits, after.Bits);
                Assert.True(after.TooTall);
                Assert.Equal(HgssFollowers.Hovers, after.Motion);
                Assert.Equal(before.Param[0], after.Param[0]);
                Assert.Equal(before.Param[3], after.Param[3]);

                var neighbour = HgssFollowers.Read(model + 1, "Normal");
                Assert.Equal(next.Bits, neighbour.Bits);
                Assert.Equal(next.Param, neighbour.Param);
            }
            finally
            {
                File.WriteAllBytes(RomInfo.OWtablePath, overlay);
                File.WriteAllBytes(paramPath, param);
            }
        }
    }
}
