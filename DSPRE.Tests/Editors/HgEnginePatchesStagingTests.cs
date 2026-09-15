using System;
using System.IO;
using System.Linq;
using DSPRE.Avalonia.ViewModels.Tools;
using DSPRE.HgEngine;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The patches window holds an added patch until Save, and Save writes exactly what writing the patch
    /// straight to the list would.
    /// </summary>
    public class HgEnginePatchesStagingTests
    {
        private static readonly string[] Hooks =
        {
            "#include \"include/config.h\"",
            "",
            "# a note",
            "arm9 PokePicLoad 080701EC 1",
        };

        private static string MakeRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "dspre_patch_stage_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "hooks"), string.Join("\n", Hooks) + "\n");
            return root;
        }

        private static HgEnginePatchesViewModel Open(string root, string symbol = "MyNewHook") =>
            new HgEnginePatchesViewModel(() => HgEnginePatchList.ReadAllAt(root))
            {
                NewList = 0,
                NewBinary = "arm9",
                NewSymbol = symbol,
                NewAddress = "02012345",
                NewRegister = "3",
            };

        [Fact]
        public void AnAddedPatchIsWrittenOnlyBySaveAndMatchesTheOldImmediateWrite()
        {
            string immediateRoot = MakeRoot(), stagedRoot = MakeRoot();
            try
            {
                // The list as writing the patch straight away leaves it.
                var list = HgEnginePatchList.ReadAllAt(immediateRoot).Single();
                list.Add(-1, "MyNewHook", 0x02012345, 3, Array.Empty<byte>());
                Assert.True(list.Save(out string error), error);
                byte[] expected = File.ReadAllBytes(list.FullPath);

                string stagedPath = Path.Combine(stagedRoot, "hooks");
                byte[] before = File.ReadAllBytes(stagedPath);
                Assert.NotEqual(before, expected);

                var vm = Open(stagedRoot);
                int rowsBefore = vm.Rows.Count;
                Assert.True(rowsBefore > 0);

                Assert.Null(vm.AddPatch());
                Assert.True(vm.HasUnsavedChanges);
                Assert.Equal(rowsBefore + 1, vm.Rows.Count);
                Assert.Contains(vm.Rows, r => r.IsPending && r.Symbol == "MyNewHook");
                Assert.Equal(before, File.ReadAllBytes(stagedPath));

                Assert.Null(vm.Save());
                Assert.False(vm.HasUnsavedChanges);
                Assert.Equal(expected, File.ReadAllBytes(stagedPath));
                Assert.Equal(rowsBefore + 1, vm.Rows.Count);
                Assert.DoesNotContain(vm.Rows, r => r.IsPending);
            }
            finally
            {
                Directory.Delete(immediateRoot, true);
                Directory.Delete(stagedRoot, true);
            }
        }

        [Fact]
        public void DiscardDropsAnAddedPatchWithoutTouchingTheList()
        {
            string root = MakeRoot();
            try
            {
                string path = Path.Combine(root, "hooks");
                byte[] before = File.ReadAllBytes(path);
                var vm = Open(root);
                int rowsBefore = vm.Rows.Count;

                Assert.Null(vm.AddPatch());
                Assert.True(vm.HasUnsavedChanges);

                vm.DiscardChanges();
                Assert.False(vm.HasUnsavedChanges);
                Assert.Equal(rowsBefore, vm.Rows.Count);
                Assert.DoesNotContain(vm.Rows, r => r.Symbol == "MyNewHook");

                // Nothing is left for a later Save to write.
                Assert.Null(vm.Save());
                Assert.Equal(before, File.ReadAllBytes(path));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void AnEntryTheBuildCannotReadIsRefusedWhenAdded()
        {
            string root = MakeRoot();
            try
            {
                var vm = Open(root, symbol: "Two Words");
                int rowsBefore = vm.Rows.Count;

                Assert.NotNull(vm.AddPatch());
                Assert.False(vm.HasUnsavedChanges);
                Assert.Equal(rowsBefore, vm.Rows.Count);
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
