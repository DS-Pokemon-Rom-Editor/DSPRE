using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>The legacy .script export: when it is read, and when it must be left alone.</summary>
    [Collection("rom")]
    public class ScriptPlaintextTests
    {
        [Fact]
        public void AUseScriptToAMissingScriptIsRefusedRatherThanDropped()
        {
            var end = new ScriptCommand("End", new List<byte[]>(), 0x0002);
            var file = new ScriptFile(
                new List<ScriptCommandContainer>
                {
                    new ScriptCommandContainer(1, ScriptFile.ContainerTypes.Script, commandList: new List<ScriptCommand> { end }),
                    new ScriptCommandContainer(2, ScriptFile.ContainerTypes.Script, usedScriptID: 9),
                    new ScriptCommandContainer(3, ScriptFile.ContainerTypes.Script, commandList: new List<ScriptCommand> { end }),
                },
                new List<ScriptCommandContainer>(), new List<ScriptActionContainer>());

            // Leaving script 2 out of the header would renumber script 3 as 2.
            Assert.Null(file.ToByteArray());

            file.allScripts[1].usedScriptID = 1;
            byte[] bytes = file.ToByteArray();
            Assert.NotNull(bytes);
            int Target(int i) => BitConverter.ToInt32(bytes, i * 4) + i * 4 + 4;
            Assert.Equal(0xFD13, BitConverter.ToUInt16(bytes, 12));
            Assert.Equal(Target(0), Target(1));
            Assert.NotEqual(Target(0), Target(2));
        }

        [SkippableFact]
        public void EditedPlaintextIsReadAndEachReaderGetsItsOwnCopy()
        {
            Skip.If(!Open(TestRoms.Platinum), "Platinum not unpacked here");
            WithLegacyScript((id, binPath, txtPath) =>
            {
                string text = new ScriptFile(id).ToPlainText(includeActions: true);
                Assert.False(string.IsNullOrEmpty(text));
                File.WriteAllText(txtPath, text);
                File.SetLastWriteTimeUtc(txtPath, File.GetLastWriteTimeUtc(binPath).AddMinutes(1));
                ScriptFile.ClearPlaintextCache();

                var first = new ScriptFile(id);
                Assert.False(first.plaintextParseFailed);
                int scripts = first.allScripts.Count;
                var withCommands = first.allScripts.FindIndex(c => c.commands != null && c.commands.Count > 0);
                Assert.True(withCommands >= 0, "no script with commands, so nothing was checked");
                int commands = first.allScripts[withCommands].commands.Count;

                // What a discarded session does to its copy.
                first.allScripts.Add(new ScriptCommandContainer(999, ScriptFile.ContainerTypes.Script, usedScriptID: 1));
                first.allScripts[withCommands].commands.Clear();

                var second = new ScriptFile(id);
                Assert.Equal(scripts, second.allScripts.Count);
                Assert.Equal(commands, second.allScripts[withCommands].commands.Count);
            });
        }

        [SkippableFact]
        public void AnUnchangedExportIsNotReadAndABrokenEditedOneIsNotOverwritten()
        {
            Skip.If(!Open(TestRoms.Platinum), "Platinum not unpacked here");
            WithLegacyScript((id, binPath, txtPath) =>
            {
                const string broken = "//===== SCRIPTS =====//\nnot a script\n";
                File.WriteAllText(txtPath, broken);

                File.SetLastWriteTimeUtc(txtPath, File.GetLastWriteTimeUtc(binPath).AddMinutes(-1));
                ScriptFile.ClearPlaintextCache();
                var unchanged = new ScriptFile(id);
                Assert.False(unchanged.plaintextParseFailed);
                Assert.True(unchanged.allScripts.Count > 0);

                File.SetLastWriteTimeUtc(txtPath, File.GetLastWriteTimeUtc(binPath).AddMinutes(1));
                var edited = new ScriptFile(id);
                Assert.True(edited.plaintextParseFailed);
                Assert.True(edited.SaveToFileDefaultDir(id, showSuccessMessage: false));
                Assert.Equal(broken, File.ReadAllText(txtPath));
            });
        }

        private static bool Open(string project)
        {
            if (!Directory.Exists(project)) return false;
            SettingsManager.Load();
            new RomInfo("CPUE", project);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts, RomInfo.DirNames.textArchives });
            ScriptDatabase.InitializePokemonNames();
            ScriptDatabase.InitializeItemNames();
            ScriptDatabase.InitializeMoveNames();
            ScriptDatabase.InitializeTrainerNames();
            return true;
        }

        private static void SetRotomProject(bool value) =>
            typeof(RomInfo).GetProperty(nameof(RomInfo.hasRotomProject)).SetValue(null, value);

        /// <summary>Runs on a small ordinary script file as a legacy project, putting both files back after.</summary>
        private static void WithLegacyScript(Action<int, string, string> test)
        {
            int id = Enumerable.Range(0, Math.Min(80, Filesystem.GetScriptCount())).FirstOrDefault(Usable, -1);
            Skip.If(id < 0, "no plain script file parsed in the first 80");

            var (binPath, txtPath) = ScriptFile.GetFilePaths(id);
            byte[] bin = File.ReadAllBytes(binPath);
            DateTime binTime = File.GetLastWriteTimeUtc(binPath);
            bool hadTxt = File.Exists(txtPath);
            string txt = hadTxt ? File.ReadAllText(txtPath) : null;
            DateTime txtTime = hadTxt ? File.GetLastWriteTimeUtc(txtPath) : default;
            bool wasRotom = RomInfo.hasRotomProject;
            try
            {
                SetRotomProject(false);
                Directory.CreateDirectory(Path.GetDirectoryName(txtPath));
                test(id, binPath, txtPath);
            }
            finally
            {
                SetRotomProject(wasRotom);
                File.WriteAllBytes(binPath, bin);
                File.SetLastWriteTimeUtc(binPath, binTime);
                if (hadTxt)
                {
                    File.WriteAllText(txtPath, txt);
                    File.SetLastWriteTimeUtc(txtPath, txtTime);
                }
                else if (File.Exists(txtPath))
                {
                    File.Delete(txtPath);
                }
                ScriptFile.ClearPlaintextCache();
            }
        }

        private static bool Usable(int id)
        {
            try
            {
                string path = Filesystem.GetScriptPath(id);
                if (!File.Exists(path)) return false;
                ScriptFile file;
                using (var fs = File.OpenRead(path)) file = new ScriptFile(fs, true, true, id);
                return !file.isLevelScript && !file.parseFailedDueToInvalidCommand
                    && file.allScripts.Any(c => c.commands != null && c.commands.Count > 0)
                    && !string.IsNullOrEmpty(file.ToPlainText(includeActions: true));
            }
            catch { return false; }
        }
    }
}
