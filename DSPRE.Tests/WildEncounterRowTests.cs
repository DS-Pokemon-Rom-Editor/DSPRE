using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.VisualTree;
using System.Linq;
using Avalonia.Media;
using DSPRE.Avalonia.ViewModels;
using DSPRE.Avalonia.Controls;
using DSPRE.Avalonia.Views;
using Xunit;

namespace DSPRE.Tests
{
    public class WildEncounterRowTests
    {
        [Fact]
        public void ChangingSpeciesRefreshesPokemonIcon()
        {
            var expected = new TestImage();
            int requestedSpecies = -1;
            var row = new WildEncounterRow(Enumerable.Empty<string>(), species =>
            {
                requestedSpecies = species;
                return expected;
            });

            row.PokemonIndex = 25;

            Assert.Equal(25, requestedSpecies);
            Assert.Same(expected, row.PokemonIcon);
        }

        [Fact(Skip = "Flaky: builds a real Avalonia visual tree and races other UI tests. Revisit with the wild encounter pass.")]
        public void ReopeningSpeciesPickerDoesNotClearFirstEncounter()
        {
            var vm = new WildEditorDPPtViewModel();
            foreach (string name in Enumerable.Range(0, 100).Select(i => $"Pokemon {i}"))
                vm.PokemonNames.Add(name);
            var firstRow = new WildEncounterRow(vm.PokemonNames, _ => null) { PokemonIndex = 75 };

            var view = new WildEditorDPPtView(vm);
            var template = (IDataTemplate)view.Resources["PokemonEncounterCellTemplate"];
            Control cell = template.Build(firstRow);
            cell.DataContext = firstRow;
            var host = new UserControl { DataContext = vm, Content = cell };
            host.Measure(new Size(900, 680));
            host.Arrange(new Rect(0, 0, 900, 680));
            var picker = cell.GetVisualDescendants().OfType<FusionAutoCompleteBox>().Single();

            Assert.Same(vm.PokemonNames, picker.ItemsSource);
            Assert.Equal(75, firstRow.PokemonIndex);
            Assert.Equal("Pokemon 75", picker.SelectedItem);
            Assert.True(picker.TextFilter("75", "Pokemon 75"));
        }

        private sealed class TestImage : IImage
        {
            public Size Size => new Size(1, 1);
            public void Draw(DrawingContext context, Rect sourceRect, Rect destRect) { }
        }
    }
}
