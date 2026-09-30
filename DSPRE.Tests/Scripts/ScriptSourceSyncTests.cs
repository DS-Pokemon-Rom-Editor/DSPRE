using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// After a direct binary write only that file's Rotom source is rebuilt: it keeps the project's
    /// names, the old source is backed up, its compile-state entry is merged, and the project compiles
    /// it back to the same binary.
    /// </summary>
    [Collection("rom")]
    public class ScriptSourceSyncTests
    {
        private static void OpenOrSkip()
        {
            Skip.If(!Directory.Exists(TestRoms.HeartGold), "HeartGold test project not configured");
            new RomInfo("IPKE", TestRoms.HeartGold);
            RomInfo.RefreshRotomProjectState();
            Skip.If(!RomInfo.hasRotomProject || !RotomTool.IsAvailable, "HeartGold test project is not a Rotom project");
        }

        private static void CopyTree(string from, string to)
        {
            if (!Directory.Exists(from)) return;
            foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                string dest = Path.Combine(to, Path.GetRelativePath(from, file));
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(file, dest);
            }
        }

        // A copy of the project's rotom side, so the sync and the compile never write the real one.
        private static string CopyProject(string root, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string f in new[] { "rotom.toml", "config.yaml", "header.yaml" })
                if (File.Exists(Path.Combine(root, f))) File.Copy(Path.Combine(root, f), Path.Combine(to, f));
            foreach (string dir in new[] { "arm9", ".rotom", Path.Combine("expanded", "scripts"), Path.Combine("expanded", "textArchives"), Path.Combine("unpacked", "scripts") })
                CopyTree(Path.Combine(root, dir), Path.Combine(to, dir));
            return to;
        }

        [SkippableTheory]
        [InlineData("0010.rotom", 10, "FLAG_UNK_298")]
        [InlineData("0266.json", 266, null)]
        public async Task OnlyThatSourceIsRebuiltAndItCompilesBackToTheSameBinary(string file, int id, string expectedName)
        {
            OpenOrSkip();
            string root = RomInfo.workDir.TrimEnd('\\', '/');
            Skip.If(!File.Exists(Path.Combine(root, "expanded", "scripts", file)), file + " is not in the test project");

            string work = Path.Combine(Path.GetTempPath(), "DSPRE-sync-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                string project = CopyProject(root, work);
                string sources = Path.Combine(project, "expanded", "scripts");
                string other = Directory.GetFiles(sources).First(f => Path.GetFileName(f) != file);
                DateTime otherTime = File.GetLastWriteTimeUtc(other);
                File.WriteAllText(Path.Combine(sources, file), "stale");

                string binaries = Path.Combine(project, "unpacked", "scripts");
                var done = await ScriptSourceSync.RegenerateAsync(new[] { id }, project, sources, i => Path.Combine(binaries, i.ToString("D4")));

                Assert.Equal(new[] { id }, done);
                string text = File.ReadAllText(Path.Combine(sources, file));
                Assert.DoesNotContain("stale", text);
                if (expectedName != null) Assert.Contains(expectedName, text);
                Assert.Equal(otherTime, File.GetLastWriteTimeUtc(other));

                string backup = Directory.GetDirectories(Path.Combine(project, ".rotom", "backups"), "dspre-*")
                    .SelectMany(d => Directory.GetFiles(d, file)).Single();
                Assert.Equal("stale", File.ReadAllText(backup));

                var entries = JsonNode.Parse(File.ReadAllText(Path.Combine(project, ".rotom", "status", "compile-state.json")))["entries"].AsObject();
                var entry = entries.Single(e => e.Key.Replace('\\', '/').EndsWith("expanded/scripts/" + file)).Value;
                Assert.Equal("Decompiled", entry["status"].GetValue<string>());

                // The whole copy compiles, and this file's binary is the game's own.
                var compiled = await RotomTool.RunInAsync(project, "compile", "--json", "--force");
                Assert.True(compiled.Success, RotomTool.FormatDetails(compiled));
                Assert.Equal(File.ReadAllBytes(Filesystem.GetScriptPath(id)),
                             File.ReadAllBytes(Path.Combine(project, "unpacked", "scripts", id.ToString("D4"))));
            }
            finally
            {
                if (Directory.Exists(work)) Directory.Delete(work, true);
            }
        }
    }
}
