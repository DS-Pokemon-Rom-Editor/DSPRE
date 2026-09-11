using System;
using System.IO;
using DSPRE;
using DSPRE.HgEngine;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Opening an hg-engine checkout's own extracted tree as a project. It is the flat ndstool shape
    /// under different names, so it is recognised apart from a DSPRE project rather than mistaken for one.
    /// </summary>
    public class HgEngineBaseProjectTests
    {
        private readonly ITestOutputHelper _out;
        public HgEngineBaseProjectTests(ITestOutputHelper o) => _out = o;

        private static string Make(params string[] rel)
        {
            string root = Path.Combine(Path.GetTempPath(), "dspre_proj_" + Guid.NewGuid().ToString("N"));
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
        public void AnHgEngineBaseTreeIsItsOwnKindOfProject()
        {
            string root = Make("header.bin", "arm9.bin", "overarm9.bin", "root/", "overlay/");
            try
            {
                Assert.Equal(2, DSUtils.GetFolderType(root));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void ADspreNdstoolProjectIsStillAnNdstoolProject()
        {
            string root = Make("header.bin", "arm9.bin", "y9.bin", "data/", "overlay/");
            try
            {
                // The filesystem folder name is the whole difference, so this must not drift into type 2.
                Assert.Equal(1, DSUtils.GetFolderType(root));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void ADsRomProjectStillWinsOverBothOfThem()
        {
            string root = Make("config.yaml", "header.bin", "root/");
            try
            {
                Assert.Equal(0, DSUtils.GetFolderType(root));
            }
            finally { Directory.Delete(root, true); }
        }

        [SkippableFact]
        public void TheOperatorsCheckoutBaseTreeReadsAsOne()
        {
            string checkout = Environment.GetEnvironmentVariable("DSPRE_TEST_HGENGINE_CHECKOUT");
            Skip.If(string.IsNullOrWhiteSpace(checkout), "Set DSPRE_TEST_HGENGINE_CHECKOUT to a checkout.");

            string baseDir = Path.Combine(checkout, HgEngineBase.BaseDirName);
            Skip.If(!Directory.Exists(baseDir), "That checkout has never been built, so it has no base/.");

            int type = DSUtils.GetFolderType(baseDir);
            _out.WriteLine($"{baseDir} -> folder type {type}");
            Assert.Equal(2, type);
        }
    }

    /// <summary>Packing a checkout's own tree without make, as Build and Run does with compiling off.</summary>
    [Collection("rom")]
    public class HgEngineBaseProjectPackTests
    {
        [SkippableFact]
        public void TheBaseTreePacksToTheRomMakeBuiltFromIt()
        {
            string checkout = Environment.GetEnvironmentVariable("DSPRE_TEST_HGENGINE_CHECKOUT");
            Skip.If(string.IsNullOrWhiteSpace(checkout), "Set DSPRE_TEST_HGENGINE_CHECKOUT to a checkout.");

            string baseDir = Path.Combine(checkout, HgEngineBase.BaseDirName);
            string built = Path.Combine(checkout, "test.nds");
            Skip.If(!File.Exists(built) || !Directory.Exists(baseDir), "That checkout has no build.");
            Skip.If(File.GetLastWriteTimeUtc(built) < File.GetLastWriteTimeUtc(Path.Combine(baseDir, "arm9.bin")),
                "base/ changed after test.nds was built.");

            // Loading a project unpacks text archives into it, so the checkout is copied rather than opened.
            string work = Path.Combine(Path.GetTempPath(), $"dspre-base-pack-{Guid.NewGuid():N}");
            string tree = Path.Combine(work, HgEngineBase.BaseDirName);
            string packed = Path.Combine(work, "packed.nds");
            try
            {
                foreach (string file in new[] { "arm9.bin", "arm7.bin", "overarm9.bin", "overarm7.bin", "banner.bin", "header.bin" })
                    CopyInto(Path.Combine(baseDir, file), Path.Combine(tree, file));
                foreach (string folder in new[] { "overlay", "root" })
                    foreach (string file in Directory.EnumerateFiles(Path.Combine(baseDir, folder), "*", SearchOption.AllDirectories))
                        CopyInto(file, Path.Combine(tree, Path.GetRelativePath(baseDir, file)));

                new RomInfo("IPKE", tree);
                Assert.True(DSUtils.RepackROM(packed), "packing the base tree failed");

                byte[] expected = File.ReadAllBytes(built), actual = File.ReadAllBytes(packed);
                Assert.Equal(expected.Length, actual.Length);
                // DSPRE's older ndstool writes a few card control fields, and so the header CRC, differently.
                Assert.True(expected.AsSpan(0x20, 0x40).SequenceEqual(actual.AsSpan(0x20, 0x40)), "the layout table differs");
                Assert.True(expected.AsSpan(0x4000).SequenceEqual(actual.AsSpan(0x4000)), "the packed contents differ");
            }
            finally { if (Directory.Exists(work)) Directory.Delete(work, true); }
        }

        private static void CopyInto(string from, string to)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            File.Copy(from, to);
        }
    }
}
