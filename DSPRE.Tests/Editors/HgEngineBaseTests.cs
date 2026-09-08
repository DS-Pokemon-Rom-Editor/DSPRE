using System;
using System.IO;
using DSPRE.HgEngine;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>Which extracted trees a checkout has, and what DSPRE may read or write in each.</summary>
    public class HgEngineBaseTests
    {
        private readonly ITestOutputHelper _out;
        public HgEngineBaseTests(ITestOutputHelper o) => _out = o;

        private static string Make(params string[] rel)
        {
            string root = Path.Combine(Path.GetTempPath(), "dspre_base_" + Guid.NewGuid().ToString("N"));
            foreach (string r in rel)
            {
                string full = Path.Combine(root, r.Replace('/', Path.DirectorySeparatorChar));
                if (r.EndsWith("/")) Directory.CreateDirectory(full);
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(full));
                    File.WriteAllText(full, "");
                }
            }
            return root;
        }

        [Fact]
        public void ABuiltDsRomCheckoutKeepsBothTrees()
        {
            string root = Make("scripts/dsrom_bridge.py", "base/root/", "base/arm9.bin",
                               "base_dsrom/config.yaml", "base_dsrom/arm9_overlays/overlays.yaml");
            try
            {
                Assert.True(HgEngineBase.SupportsDsRomAt(root));
                Assert.Equal(HgEngineBaseLayout.DsRom, HgEngineBase.LayoutOf(root));
                Assert.Null(HgEngineBase.NoticeFor(root));

                // Writes go to base/ even here: packing copies base/ over base_dsrom/.
                Assert.Equal(Path.Combine(root, "base", "root"),
                    HgEngineBase.FileSystemRootOf(root, HgEngineBaseLayout.DsRom));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void ACheckoutWithTheBridgeButNoBuildSaysSo()
        {
            string root = Make("scripts/dsrom_bridge.py", "base/root/", "base/arm9.bin");
            try
            {
                Assert.True(HgEngineBase.SupportsDsRomAt(root));
                Assert.Equal(HgEngineBaseLayout.Ndstool, HgEngineBase.LayoutOf(root));
                Assert.Contains("has not done so yet", HgEngineBase.NoticeFor(root));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void ACheckoutWithoutTheBridgeIsNotADsRomHgEngine()
        {
            string root = Make("Makefile", "base/root/", "base/arm9.bin");
            try
            {
                Assert.False(HgEngineBase.SupportsDsRomAt(root));
                Assert.Equal(HgEngineBaseLayout.Ndstool, HgEngineBase.LayoutOf(root));
                Assert.Contains("not a ds-rom hg-engine", HgEngineBase.NoticeFor(root));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void ACheckoutThatHasNeverBeenBuiltHasNoTreeAtAll()
        {
            string root = Make("Makefile", "narcs.mk");
            try
            {
                Assert.Equal(HgEngineBaseLayout.None, HgEngineBase.LayoutOf(root));
                Assert.Null(HgEngineBase.FileSystemRootOf(root, HgEngineBaseLayout.None));
            }
            finally { Directory.Delete(root, true); }
        }

        [SkippableFact]
        public void ARealCheckoutReportsItsOwnState()
        {
            string root = Environment.GetEnvironmentVariable("DSPRE_TEST_HGENGINE_CHECKOUT");
            Skip.If(string.IsNullOrWhiteSpace(root) || !Directory.Exists(root),
                "Set DSPRE_TEST_HGENGINE_CHECKOUT to an hg-engine checkout to run this.");

            _out.WriteLine($"layout={HgEngineBase.LayoutOf(root)} dsrom={HgEngineBase.SupportsDsRomAt(root)}");
            _out.WriteLine("notice: " + (HgEngineBase.NoticeFor(root) ?? "(ds-rom ready)"));

            // Whatever it reports has to be self-consistent: a ds-rom layout always supports ds-rom.
            if (HgEngineBase.LayoutOf(root) == HgEngineBaseLayout.DsRom)
                Assert.True(HgEngineBase.SupportsDsRomAt(root));
        }
    }
}
