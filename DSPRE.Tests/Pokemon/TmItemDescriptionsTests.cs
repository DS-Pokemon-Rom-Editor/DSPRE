using System;
using System.IO;
using System.Linq;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests.Pokemon
{
    /// <summary>A TM's item description follows its move: the move's description wrapped to fit, or PlatPatches' template.</summary>
    [Collection("rom")]
    public class TmItemDescriptionsTests
    {
        private const int Thunderbolt = 85;

        private static void Restoring(Action body)
        {
            string json = TextArchive.GetFilePaths(TmItemDescriptions.Bank).jsonPath;
            byte[] before = File.Exists(json) ? File.ReadAllBytes(json) : null;
            try { body(); }
            finally
            {
                if (before != null) File.WriteAllBytes(json, before);
                else if (File.Exists(json)) File.Delete(json);
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GameTablesTests.Games), MemberType = typeof(GameTablesTests))]
        public void VanillaTmTakesTheNewMovesDescriptionWrappedToFit(string game)
        {
            GameTablesTests.Open(game);
            Assert.Null(TmItemDescriptions.WhyNot());
            Restoring(() =>
            {
                int oldMove = TMEditor.ReadMachineMoves()[0];
                var result = TmItemDescriptions.Update(new[] { (0, oldMove, Thunderbolt) });
                Assert.Equal(1, result.Updated);
                Assert.Empty(result.Kept);

                string written = new TextArchive(TmItemDescriptions.Bank).messages[TMEditor.MachineItemId(0)];
                string moveText = new TextArchive(RomInfo.moveDescriptionsTextNumbers).messages[Thunderbolt];
                Assert.Equal(Words(moveText), Words(written));
                // No longer than the longest vanilla TM description.
                int maxLines = Enumerable.Range(1, TMEditor.VanillaMachineCount - 1)
                    .Select(i => new TextArchive(TmItemDescriptions.Bank).messages[TMEditor.MachineItemId(i)].Split("\\n").Length).Max();
                Assert.InRange(written.Split("\\n").Length, 1, maxLines);
            });
        }

        [SkippableFact]
        public void PlatPatchesTmKeepsItsTemplate()
        {
            string path = Environment.GetEnvironmentVariable("DSPRE_TEST_PLATPATCHES");
            Skip.If(string.IsNullOrWhiteSpace(path) || !Directory.Exists(path), "DSPRE_TEST_PLATPATCHES is not set to an extracted PlatPatches project");
            new RomInfo("CPUE", path.TrimEnd('\\', '/'));
            PlatPatches.Forget();
            Restoring(() =>
            {
                const int row = 1;   // TM94
                int machine = TMEditor.VanillaMachineCount + row;
                int oldMove = PlatPatches.Tms().MoveIds[row];
                var result = TmItemDescriptions.Update(new[] { (machine, oldMove, Thunderbolt) });
                Assert.Equal(1, result.Updated);
                Assert.Equal(string.Format(TmItemDescriptions.PatchTemplate, RomInfo.GetAttackNames()[Thunderbolt]),
                    new TextArchive(TmItemDescriptions.Bank).messages[TMEditor.MachineItemId(machine)]);
            });
        }

        private static string Words(string s) =>
            string.Join(" ", s.Replace("\\n", " ").Replace("\\r", " ").Replace("\\f", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
