using System.Linq;
using DSPRE;
using DSPRE.Avalonia.ViewModels.Shell;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Search boxes find names whatever the accents and case typed.</summary>
    public class SearchMatchTests
    {
        [Theory]
        [InlineData("Pokémon Editor", "pokemon")]
        [InlineData("Pokémon Editor", "POKÉMON")]
        [InlineData("Poké Ball", "poke ball")]
        [InlineData("pokemon league", "Pokémon")]
        [InlineData("Flabébé", "flabebe")]
        public void AccentsAndCaseDoNotCount(string text, string query)
        {
            Assert.True(SearchMatch.Contains(text, query));
            Assert.True(SearchMatch.StartsWith(text, query.Split(' ')[0]));
        }

        [Fact]
        public void OtherLettersStillHaveToMatch()
        {
            Assert.False(SearchMatch.Contains("Pokémon", "pokeman"));
            Assert.False(SearchMatch.StartsWith("Great Ball", "poke"));
            Assert.Equal("", SearchMatch.Fold(null));
        }

        [Fact]
        public void ThePaletteFindsPokemonWithoutTheAccent()
        {
            var vm = new CommandPaletteViewModel(DSPRE.Avalonia.AvaloniaEditorLauncher.BuildCommands());
            vm.SearchText = "pokemon editor";
            Assert.Contains(vm.Items, c => c.Name == "Pokémon Editor");
            Assert.Equal("Pokémon Editor", vm.Items.First().Name);
        }
    }
}
