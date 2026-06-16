using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.Avalonia.ViewModels;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Covers the quick-open palette's pure logic: contains/keyword matching, prefix &gt; contains &gt; keyword
    /// ranking, empty-query passthrough, and that an injected dynamic provider's entries are listed first.
    /// </summary>
    public class CommandPaletteViewModelTests
    {
        private static CommandItem Cmd(string name, string keywords = "") =>
            new() { Name = name, Keywords = keywords, Run = () => { } };

        private static List<CommandItem> Sample() => new()
        {
            Cmd("Pokémon Editor", "species personal"),
            Cmd("Move Data Editor", "attack"),
            Cmd("Map Editor", "3d buildings"),
            Cmd("Trainer Editor", "battle party"),
        };

        [Fact]
        public void EmptyQuery_ShowsAllInOriginalOrder()
        {
            var vm = new CommandPaletteViewModel(Sample());
            Assert.Equal(
                new[] { "Pokémon Editor", "Move Data Editor", "Map Editor", "Trainer Editor" },
                vm.Items.Select(i => i.Name));
            Assert.Equal(0, vm.SelectedIndex);
        }

        [Fact]
        public void Query_FiltersByNameSubstring_CaseInsensitive()
        {
            var vm = new CommandPaletteViewModel(Sample()) { SearchText = "editor" };
            Assert.Equal(4, vm.Items.Count);     // all contain "Editor"

            vm.SearchText = "trainer";
            Assert.Single(vm.Items);
            Assert.Equal("Trainer Editor", vm.Items[0].Name);
        }

        [Fact]
        public void Query_MatchesKeywords()
        {
            var vm = new CommandPaletteViewModel(Sample()) { SearchText = "battle" };
            Assert.Single(vm.Items);
            Assert.Equal("Trainer Editor", vm.Items[0].Name);
        }

        [Fact]
        public void Ranking_NamePrefixBeatsKeywordOnlyMatch()
        {
            // "ma" prefixes "Map Editor" (score 3) and is a substring of nothing else by name, but
            // appears in no keywords here — add a keyword-only match to prove ordering.
            var cmds = new List<CommandItem>
            {
                Cmd("Zedily", "map"),          // keyword-only match  → score 1
                Cmd("Map Editor", "world"),     // name prefix match    → score 3
            };
            var vm = new CommandPaletteViewModel(cmds) { SearchText = "map" };
            Assert.Equal(new[] { "Map Editor", "Zedily" }, vm.Items.Select(i => i.Name));
        }

        [Fact]
        public void NoMatch_ClearsSelection()
        {
            var vm = new CommandPaletteViewModel(Sample()) { SearchText = "zzzz" };
            Assert.Empty(vm.Items);
            Assert.Equal(-1, vm.SelectedIndex);
            Assert.Null(vm.Selected);
        }

        [Fact]
        public void DynamicProvider_EntriesAppearBeforeStaticMatches()
        {
            Func<string, IEnumerable<CommandItem>> dyn = q =>
                q.Contains("map") ? new[] { Cmd("Go to Map #5") } : Array.Empty<CommandItem>();

            var vm = new CommandPaletteViewModel(Sample(), dyn) { SearchText = "map" };
            Assert.Equal("Go to Map #5", vm.Items[0].Name);              // dynamic entry first
            Assert.Contains(vm.Items, i => i.Name == "Map Editor");      // static match still present
        }

        [Fact]
        public void Selected_TracksSelectedIndex()
        {
            var vm = new CommandPaletteViewModel(Sample());
            vm.SelectedIndex = 2;
            Assert.Equal("Map Editor", vm.Selected.Name);
        }
    }
}
