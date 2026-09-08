using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.HgEngine;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>Which text archives and scripts a checkout builds itself, and editing them in place.</summary>
    public class HgEngineOwnedFilesTests
    {
        private readonly ITestOutputHelper _out;
        public HgEngineOwnedFilesTests(ITestOutputHelper o) => _out = o;

        // A checkout laid out like hg-engine's, so discovery is tested without needing a real one.
        private static readonly string DefaultFragment = string.Join(Environment.NewLine,
            "MSGDATA_TARGET := $(FILESYS)/a/0/2/7",
            "MSGDATA_DEPENDENCIES_DIR := data/text",
            "SCR_SEQ_TARGET := $(FILESYS)/a/0/1/2",
            "SCR_SEQ_DEPENDENCIES_DIR := armips/scr_seq",
            "");

        private static string MakeFakeCheckout(params string[] relFiles)
        {
            string root = Path.Combine(Path.GetTempPath(), "dspre_hge_" + Guid.NewGuid().ToString("N"));
            foreach (string rel in relFiles)
            {
                string full = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllText(full, "");
            }
            string fragment = Path.Combine(root, HgEngineOwnedFiles.MakeFragmentRelPath);
            if (!File.Exists(fragment)) File.WriteAllText(fragment, DefaultFragment);
            return root;
        }

        [Fact]
        public void TextArchiveNumbersComeFromTheFileNames()
        {
            string root = MakeFakeCheckout("data/text/010.txt", "data/text/853.txt", "data/text/notes.md");
            try
            {
                var found = ScanText(root);

                Assert.Equal(new[] { 10, 853 }, found.Keys.OrderBy(k => k).ToArray());
                Assert.Equal("data/text/010.txt", found[10].RelPath);
                Assert.Equal(HgEngineOwnership.EditableSource, found[10].Ownership);
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void ScriptNumbersComeFromTheAssemblyFileNames()
        {
            string root = MakeFakeCheckout(
                "armips/scr_seq/scr_seq_00003_commonscript.s",
                "armips/scr_seq/scr_seq_00953_trainerscript.s");
            try
            {
                var found = ScanScripts(root);

                Assert.Equal(new[] { 3, 953 }, found.Keys.OrderBy(k => k).ToArray());
                Assert.EndsWith("scr_seq_00953_trainerscript.s", found[953].RelPath);
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void TheSourceDirectoriesComeFromTheCheckoutsOwnMakeFragment()
        {
            string root = MakeFakeCheckout("elsewhere/strings/010.txt", "elsewhere/asm/scr_seq_00003_x.s");
            File.WriteAllText(Path.Combine(root, "narcs.mk"),
                "MSGDATA_TARGET := $(FILESYS)/a/0/2/7" + Environment.NewLine +
                "MSGDATA_DEPENDENCIES_DIR := elsewhere/strings" + Environment.NewLine +
                "SCR_SEQ_TARGET := $(FILESYS)/a/0/1/2" + Environment.NewLine +
                "SCR_SEQ_DEPENDENCIES_DIR := elsewhere/asm" + Environment.NewLine);
            try
            {
                Assert.Equal(new[] { 10 }, ScanText(root).Keys.ToArray());
                Assert.Equal("elsewhere/strings/010.txt", ScanText(root)[10].RelPath);
                Assert.Equal(new[] { 3 }, ScanScripts(root).Keys.ToArray());
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void AMakeVariableBuiltFromOtherVariablesClaimsNoFiles()
        {
            string root = MakeFakeCheckout("data/text/010.txt");
            File.WriteAllText(Path.Combine(root, "narcs.mk"),
                "MSGDATA_TARGET := $(FILESYS)/a/0/2/7" + Environment.NewLine +
                "MSGDATA_DEPENDENCIES_DIR := $(BUILD)/text" + Environment.NewLine);
            try
            {
                // Nothing resolves $(BUILD) here, so no source directory is known and no file is claimed
                // rather than a plausible-looking path being guessed at.
                Assert.Empty(ScanText(root));

                // The rule itself still stands, so the archive is still known to be hg-engine's.
                Assert.NotNull(HgEngineOwnedFiles.ScanRoot(root).rules.GetValueOrDefault("a/0/2/7"));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void TheValidatorAndCharmapComeFromTheMakeFragment()
        {
            string root = MakeFakeCheckout("data/text/010.txt");
            File.WriteAllText(Path.Combine(root, "narcs.mk"),
                "MSGDATA_TARGET := $(FILESYS)/a/0/2/7" + Environment.NewLine +
                "CHARMAP := charmap.txt" + Environment.NewLine +
                "	$(PYTHON) tools/source/dumptools/validate_text_archive.py $(CHARMAP) $$file" +
                Environment.NewLine);
            try
            {
                Assert.Equal("charmap.txt", HgEngineOwnedFiles.CharMapRelPath(root));
                Assert.Equal("tools/source/dumptools/validate_text_archive.py",
                    HgEngineOwnedFiles.TextValidatorRelPath(root));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void ACheckoutThatDeclaresNoValidatorReportsNone()
        {
            string root = MakeFakeCheckout("data/text/010.txt");
            try
            {
                Assert.Null(HgEngineOwnedFiles.TextValidatorRelPath(root));
                Assert.Null(HgEngineOwnedFiles.CharMapRelPath(root));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void AnArchiveTheBuildGeneratesIsNotEditableEvenWhenASourceFileExists()
        {
            string root = MakeFakeCheckout("data/text/010.txt", "data/text/728.txt");
            File.WriteAllText(Path.Combine(root, "narcs.mk"), string.Join(Environment.NewLine,
                "MSGDATA_TARGET := $(FILESYS)/a/0/2/7",
                "MSGDATA_DEPENDENCIES_DIR := data/text",
                "MSGDATA_COMPILETIME_DEPENDENCIES += $(BUILD)/rawtext/728.txt",
                ""));
            try
            {
                var found = ScanText(root);

                // 010 is hand-edited and wins. 728 is encoded again from build output afterwards, so an
                // edit to its source file never reaches the ROM.
                Assert.Equal(HgEngineOwnership.EditableSource, found[10].Ownership);
                Assert.Equal(HgEngineOwnership.Generated, found[728].Ownership);
                Assert.Null(found[728].FullPath);
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void ARuleThatExtractsTheRomsCopyFirstDoesNotClaimTheWholeArchive()
        {
            string root = MakeFakeCheckout("data/text/010.txt");
            File.WriteAllText(Path.Combine(root, "narcs.mk"), string.Join(Environment.NewLine,
                "MSGDATA_TARGET := $(FILESYS)/a/0/2/7",
                "MSGDATA_DEPENDENCIES_DIR := data/text",
                "MOVEANIM_TARGET := $(FILESYS)/a/0/1/0",
                "$(MSGDATA_NARC): deps",
                "	$(NARCHIVE) extract $(MSGDATA_TARGET) -o $(MSGDATA_DIR) -nf",
                ""));
            try
            {
                var rules = HgEngineOwnedFiles.ScanRoot(root).rules;

                // Text keeps DSPRE's edits to other archives; move animations are rebuilt outright.
                Assert.False(rules["a/0/2/7"].ReplacesWholeArchive);
                Assert.True(rules["a/0/1/0"].ReplacesWholeArchive);
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void ANamedPathIsIdentifiedByWhatSitsBelowTheFilesystemRoot()
        {
            // The sound archive is not an a/x/y/z archive, and it is the one hg-engine rebuilds that
            // the old mapping could not see at all.
            const string sdat = "data/sound/gs_sound_data.sdat";

            Assert.Equal(sdat, HgEngineOwnedFiles.ArchiveOfPath(
                string.Join(Path.DirectorySeparatorChar, "D:", "proj", "files", "data", "sound", "gs_sound_data.sdat")));

            // The same file inside a checkout's own tree, where the filesystem root is named root.
            Assert.Equal(sdat, HgEngineOwnedFiles.ArchiveOfPath(
                string.Join(Path.DirectorySeparatorChar, "D:", "hg", "base", "root", "data", "sound", "gs_sound_data.sdat")));

            // An a/x/y/z archive still resolves the short way.
            Assert.Equal("a/0/2/7", HgEngineOwnedFiles.ArchiveOfPath(
                string.Join(Path.DirectorySeparatorChar, "D:", "proj", "files", "a", "0", "2", "7")));

            Assert.Null(HgEngineOwnedFiles.ArchiveOfPath(
                string.Join(Path.DirectorySeparatorChar, "D:", "somewhere", "else")));
        }

        [Theory]
        // Text and scripts name their slot one way, the asset directories another; both have to resolve
        // or a whole class of archive silently claims nothing.
        [InlineData("010", 10)]
        [InlineData("scr_seq_00953_trainerscript", 953)]
        [InlineData("move_script_0001_POUND", 1)]
        [InlineData("6_06", 6)]
        [InlineData("8_51", 51)]
        [InlineData("8_003", 3)]
        [InlineData("0000-babyboy1", -1)]
        [InlineData("notes", -1)]
        public void ASourceFileNamesTheArchiveSlotItFills(string stem, int expected)
            => Assert.Equal(expected, HgEngineOwnedFiles.IdFromName(stem));

        [SkippableFact]
        public void ARealCheckoutOwnsItsTextArchivesAndScripts()
        {
            string root = Environment.GetEnvironmentVariable("DSPRE_TEST_HGENGINE");
            Skip.If(string.IsNullOrWhiteSpace(root) || !Directory.Exists(root),
                "Set DSPRE_TEST_HGENGINE to an hg-engine checkout to run this.");

            var text = ScanText(root);
            var scripts = ScanScripts(root);

            _out.WriteLine($"{text.Count} text archives, {scripts.Count} scripts, " +
                $"validator {HgEngineOwnedFiles.TextValidatorRelPath(root)}, " +
                $"charmap {HgEngineOwnedFiles.CharMapRelPath(root)}");
            Assert.True(text.Count > 0, "a checkout should own some text archives");
            Assert.True(scripts.Count > 0, "a checkout should own some scripts");

            // Every entry has to name an archive number, or the editor cannot pair it with a ROM archive,
            // and every editable one has to have the file it claims.
            Assert.All(text.Values, f => Assert.True(f.Id >= 0));
            Assert.All(text.Values.Where(f => f.Ownership == HgEngineOwnership.EditableSource),
                f => Assert.True(File.Exists(f.FullPath), f.RelPath));

            // The archives the Battle Script editor covers are rebuilt outright from their own source
            // directories, which is what puts that editor into source mode rather than refusing it.
            var rules = HgEngineOwnedFiles.ScanRoot(root).rules;
            foreach (string archive in new[] { "a/0/0/0", "a/0/3/0", "a/0/0/1", "a/0/1/0", "a/0/6/1" })
            {
                HgEngineRule rule = rules.GetValueOrDefault(archive);
                Assert.True(rule != null, archive + " should be built by the checkout");
                Assert.Equal(HgEngineOwnership.EditableSource, rule.Ownership);
                Assert.True(rule.ReplacesWholeArchive, archive + " is rebuilt outright");
                Assert.False(string.IsNullOrEmpty(rule.SourceDirRelPath), archive + " needs a source directory");
                _out.WriteLine($"{archive} <- {rule.SourceDirRelPath}");
            }

            var generated = text.Values.Where(f => f.Ownership == HgEngineOwnership.Generated).ToList();
            _out.WriteLine($"{generated.Count} generated: {string.Join(", ", generated.Select(f => f.Id).OrderBy(i => i))}");
            Assert.NotEmpty(generated);
        }

        [Fact]
        public void ATextArchiveKeepsItsBlankAndSpaceOnlyMessagesThroughAWrite()
        {
            string root = MakeFakeCheckout("data/text/010.txt");
            try
            {
                var file = ScanText(root)[10];
                var written = new List<string> { "USE", "TRASH", "", "     ", "CONFIRM" };

                Assert.True(HgEngineOwnedFiles.TryWriteLines(file, written, out string writeError), writeError);
                Assert.True(HgEngineOwnedFiles.TryReadLines(file, out List<string> read, out string readError), readError);

                // A message that is blank or only spaces is still a message, and the row numbers after it
                // depend on it surviving.
                Assert.Equal(written, read);
                Assert.DoesNotContain("\r", File.ReadAllText(file.FullPath));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void CrlfIsWrittenOutAsLf()
        {
            string root = MakeFakeCheckout("armips/scr_seq/scr_seq_00003_commonscript.s");
            try
            {
                var file = ScanScripts(root)[3];

                Assert.True(HgEngineOwnedFiles.TryWriteText(file, ".nds\r\n.thumb\r\n", out string error), error);

                Assert.Equal(".nds\n.thumb\n", File.ReadAllText(file.FullPath));
            }
            finally { Directory.Delete(root, true); }
        }

        private static Dictionary<int, HgEngineOwnedFile> ScanText(string root)
            => HgEngineOwnedFiles.ScanRoot(root).files.GetValueOrDefault("a/0/2/7", new());

        private static Dictionary<int, HgEngineOwnedFile> ScanScripts(string root)
            => HgEngineOwnedFiles.ScanRoot(root).files.GetValueOrDefault("a/0/1/2", new());

    }
}
