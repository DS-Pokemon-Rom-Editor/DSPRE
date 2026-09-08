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
}
