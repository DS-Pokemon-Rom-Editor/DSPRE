using System;
using System.IO;
using System.Linq;
using DSPRE.HgEngine;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Reading and adding to hg-engine's patch lists. These are the operator's own files, so the one
    /// thing that must never happen is losing something on the way back out.
    /// </summary>
    public class HgEnginePatchListTests
    {
        private readonly ITestOutputHelper _out;
        public HgEnginePatchListTests(ITestOutputHelper o) => _out = o;

        private static string Write(string name, params string[] lines)
        {
            string root = Path.Combine(Path.GetTempPath(), "dspre_patch_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, name), string.Join("\n", lines) + "\n");
            return root;
        }

        [Fact]
        public void EveryShapeOfLineIsUnderstood()
        {
            Assert.True(HgEnginePatchList.Parse(HgEnginePatchKind.Hook, "arm9 PokePicLoad 080701EC 1").Parsed);
            Assert.True(HgEnginePatchList.Parse(HgEnginePatchKind.Hook, "0012 LoadMegaOam 0226715C 1").Parsed);
            Assert.True(HgEnginePatchList.Parse(HgEnginePatchKind.Repoint, "arm9 sPocketCounts 08077C14").Parsed);
            Assert.True(HgEnginePatchList.Parse(HgEnginePatchKind.ByteReplacement, "arm9 02078384 60 B4 C0 46").Parsed);

            // Comments, conditionals and includes are not entries and stay as they are.
            Assert.False(HgEnginePatchList.Parse(HgEnginePatchKind.Hook, "# a note").Parsed);
            Assert.False(HgEnginePatchList.Parse(HgEnginePatchKind.Hook, "#include \"include/config.h\"").Parsed);
            Assert.False(HgEnginePatchList.Parse(HgEnginePatchKind.Hook, "").Parsed);

            // A replacement whose value is a define rather than literal bytes is left alone.
            Assert.False(HgEnginePatchList.Parse(HgEnginePatchKind.ByteReplacement, "arm9 02078384 SOME_DEFINE").Parsed);
        }

        [Fact]
        public void AHookWithoutARegisterReplacesTheWholeRoutine()
        {
            var entry = HgEnginePatchList.Parse(HgEnginePatchKind.Hook, "arm9 MyRoutine 02078384");
            Assert.True(entry.Parsed);
            Assert.Equal(-1, entry.Register);
            Assert.Contains("replaces the routine", entry.Describes);
        }

        [Fact]
        public void SavingKeepsEveryLineItDidNotEdit()
        {
            string root = Write("hooks",
                "#include \"include/config.h\"",
                "",
                "# mega evolution",
                "0012 LoadMegaOam 0226715C 1",
                "#0012 disabled_for_now 0802E868 4",
                "arm9 PokePicLoad 080701EC 1");
            try
            {
                var list = HgEnginePatchList.ReadAllAt(root).Single();
                Assert.Equal(6, list.Entries.Count);
                Assert.Equal(2, list.Entries.Count(e => e.Parsed));

                list.Add(-1, "MyNewHook", 0x02012345, 3, null);
                Assert.True(list.Save(out string error), error);

                string[] after = File.ReadAllLines(list.FullPath);
                _out.WriteLine(string.Join("\n", after));

                // The comment, the blank line and the commented-out entry all survive.
                Assert.Contains("#include \"include/config.h\"", after);
                Assert.Contains("# mega evolution", after);
                Assert.Contains("#0012 disabled_for_now 0802E868 4", after);
                Assert.Contains("", after);

                // And the new one is written in the list's own shape.
                Assert.Contains("arm9 MyNewHook 02012345 3", after);
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void AByteReplacementRoundTripsItsBytes()
        {
            string root = Write("bytereplacement", "arm9 02078384 60 B4 C0 46");
            try
            {
                var list = HgEnginePatchList.ReadAllAt(root).Single();
                Assert.True(list.Save(out string error), error);
                Assert.Equal("arm9 02078384 60 B4 C0 46", File.ReadAllLines(list.FullPath)[0]);
            }
            finally { Directory.Delete(root, true); }
        }

        [SkippableFact]
        public void TheRealListsReadWithoutLosingEntries()
        {
            string root = Environment.GetEnvironmentVariable("DSPRE_TEST_HGENGINE");
            Skip.If(string.IsNullOrWhiteSpace(root) || !File.Exists(Path.Combine(root, "hooks")),
                "Set DSPRE_TEST_HGENGINE to a checkout with its patch lists.");

            foreach (var list in HgEnginePatchList.ReadAllAt(root))
            {
                int parsed = list.Entries.Count(e => e.Parsed);
                _out.WriteLine($"{list.FileName}: {list.Entries.Count} lines, {parsed} entries");
                Assert.True(parsed > 0, list.FileName + " should have entries");

                // Rendering an entry has to reproduce a line the parser accepts again, or saving would
                // quietly change what the build does.
                foreach (var entry in list.Entries.Where(e => e.Parsed))
                {
                    var again = HgEnginePatchList.Parse(list.Kind, entry.Render());
                    Assert.True(again.Parsed, entry.Render());
                    Assert.Equal(entry.Address, again.Address);
                    Assert.Equal(entry.OverlayNumber, again.OverlayNumber);
                    Assert.Equal(entry.Symbol, again.Symbol);
                    Assert.Equal(entry.Bytes.Count, again.Bytes.Count);
                }
            }
        }
    }
}
