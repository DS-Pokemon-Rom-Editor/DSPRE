using System.Linq;
using DSPRE.Avalonia.ViewModels.Text;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    public class StrVarHelpViewModelTests
    {
        [Fact]
        public void SectionsFollowRequestedReadingOrder()
        {
            var vm = new StrVarHelpViewModel();

            Assert.Equal(
                new[] { "Syntax and Tips", "Examples", "Type Reference" },
                vm.Sections.Select(section => section.Title));
        }

        [Fact]
        public void MultipleBufferExampleUsesSinnohDexValuesAndMatchingBuffers()
        {
            var vm = new StrVarHelpViewModel();
            StrVarHelpExample example = vm.Sections
                .Single(section => section.Title == "Examples")
                .Examples.Single(entry => entry.Title.StartsWith("Example 2"));

            Assert.Contains("CountSinnohDexSeen 0x8005", example.Script);
            Assert.Contains("TextNumber 0 0x8005", example.Script);
            Assert.Contains("TextNumber 1 210", example.Script);

            var tags = FieldStringVars.Find(example.Message).ToArray();
            Assert.Equal(2, tags.Length);
            Assert.All(tags, tag => Assert.Equal((1, 50), (tag.family, tag.kind)));
            Assert.Equal(new[] { 0, 1 }, tags.Select(tag => tag.buffer));
        }

        [Fact]
        public void TypeReferenceRetainsDppthgssAndStrvar4Entries()
        {
            var vm = new StrVarHelpViewModel();
            var entries = vm.Sections.Single(section => section.Title == "Type Reference").TypeReferences;

            Assert.Equal(17, entries.Count);
            Assert.Contains(entries, entry => entry.TypeLabel == "15" && entry.Commands.Contains("CMD_765 (Platinum)"));
            Assert.Contains(entries, entry => entry.TypeLabel == "50-55" && entry.Commands.Contains("TextBattleHallStreak (HGSS)"));
            Assert.Contains(entries, entry => entry.Pattern == "{STRVAR_4, 1, ?, 0}" && entry.Commands.Contains("TextPokeathlonCourseName (HGSS)"));
        }
    }
}
