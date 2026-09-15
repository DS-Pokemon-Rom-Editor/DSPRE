using System;
using System.IO;
using System.Reflection;
using DSPRE.Avalonia.ViewModels.Text;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Picking another script with unsaved edits asks first instead of saving them silently.</summary>
    [Collection("rom")]
    public class ScriptEditorUnsavedSwitchTests
    {
        private static void SetWorkDir(string dir) =>
            typeof(RomInfo).GetProperty(nameof(RomInfo.workDir)).SetValue(null, dir);

        [Fact]
        public void SwitchingScriptsWithUnsavedEditsLeavesTheFileOnDiskAlone()
        {
            string root = Path.Combine(Path.GetTempPath(), "dspre_script_switch_" + Guid.NewGuid().ToString("N"));
            string scripts = Path.Combine(root, "expanded", "scripts");
            Directory.CreateDirectory(scripts);
            string first = Path.Combine(scripts, "0000.rotom");
            File.WriteAllText(first, "// file 0\n");
            File.WriteAllText(Path.Combine(scripts, "0001.rotom"), "// file 1\n");
            string previousWorkDir = RomInfo.workDir;
            try
            {
                SetWorkDir(root);
                var vm = new ScriptEditorViewModel(true);
                typeof(ScriptEditorViewModel)
                    .GetMethod("RefreshScriptList", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(vm, null);

                vm.SelectScriptFile(0);
                Assert.Equal("// file 0\n", vm.ScriptText);
                Assert.False(vm.HasUnsavedChanges);

                vm.ScriptText = "// edited\n";
                Assert.True(vm.HasUnsavedChanges);

                // The prompt cannot be answered here, so the editor must still be on the edited file.
                vm.SelectedScriptIndex = 1;

                Assert.Equal(0, vm.SelectedScriptIndex);
                Assert.Equal("// edited\n", vm.ScriptText);
                Assert.True(vm.HasUnsavedChanges);
                Assert.Equal("// file 0\n", File.ReadAllText(first));
            }
            finally
            {
                SetWorkDir(previousWorkDir);
                try { Directory.Delete(root, recursive: true); } catch { }
            }
        }
    }
}
