using System;
using System.IO;
using System.Reflection;
using DSPRE.Avalonia.ViewModels.Text;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>A header's script file number opens that file even when the list skips a number.</summary>
    [Collection("rom")]
    public class ScriptFileSelectionTests
    {
        private static void SetWorkDir(string dir) =>
            typeof(RomInfo).GetProperty(nameof(RomInfo.workDir)).SetValue(null, dir);

        [Fact]
        public void SelectingAScriptFileByNumberOpensThatFileWhenAnEarlierNumberIsMissing()
        {
            string root = Path.Combine(Path.GetTempPath(), "dspre_script_selection_" + Guid.NewGuid().ToString("N"));
            string scripts = Path.Combine(root, "expanded", "scripts");
            Directory.CreateDirectory(scripts);
            string previousWorkDir = RomInfo.workDir;
            try
            {
                // hg-engine builds script 3 from its own source, so the list has no 0003 where it would sort.
                for (int i = 0; i <= 70; i++)
                    if (i != 3) File.WriteAllText(Path.Combine(scripts, $"{i:D4}.rotom"), $"// file {i}\n");
                SetWorkDir(root);

                var vm = new ScriptEditorViewModel(true);
                typeof(ScriptEditorViewModel)
                    .GetMethod("RefreshScriptList", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(vm, null);

                vm.SelectScriptFile(66);

                Assert.Equal("// file 66\n", vm.ScriptText);
                Assert.Equal(65, vm.SelectedScriptIndex);
            }
            finally
            {
                SetWorkDir(previousWorkDir);
                try { Directory.Delete(root, recursive: true); } catch { }
            }
        }
    }
}
