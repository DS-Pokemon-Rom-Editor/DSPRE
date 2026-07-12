using Avalonia.Input;
using DSPRE.Avalonia.Controls;
using DSPRE.Avalonia.ViewModels;
using System.Collections.ObjectModel;
using Xunit;

namespace DSPRE.Tests
{
    public class FusionAutoCompleteBoxTests
    {
        [Fact]
        public void SelectedIndexAndSelectedItemStaySynchronized()
        {
            var box = new FusionAutoCompleteBox
            {
                ItemsSource = new[] { "Bulbasaur", "Charmander", "Squirtle" }
            };

            box.SelectedIndex = 1;

            Assert.Equal("Charmander", box.SelectedItem);

            box.SelectedItem = "Squirtle";

            Assert.Equal(2, box.SelectedIndex);
        }

        [Fact]
        public void EnterRestoresTheLastValidItemAfterInvalidText()
        {
            var box = new TestFusionAutoCompleteBox
            {
                ItemsSource = new[] { "Bulbasaur", "Charmander", "Squirtle" },
                SelectedIndex = 1
            };

            box.Text = "Not a Pokemon";
            box.Press(Key.Enter);

            Assert.Equal("Charmander", box.Text);
            Assert.Equal("Charmander", box.SelectedItem);
            Assert.Equal(1, box.SelectedIndex);
        }

        [Fact]
        public void FilterMatchesTextInsideFormattedLabelsAndSmallTypos()
        {
            var box = new FusionAutoCompleteBox();

            Assert.True(box.TextFilter("route", "[001] Route 201"));
            Assert.True(box.TextFilter("rute", "[001] Route 201"));
            Assert.False(box.TextFilter("cave", "[001] Route 201"));
        }

        [Fact]
        public void SelectedIndexIsAppliedWhenItemsAreAddedAfterBinding()
        {
            var items = new ObservableCollection<string>();
            var box = new FusionAutoCompleteBox
            {
                ItemsSource = items,
                SelectedIndex = 1
            };

            items.Add("First");
            items.Add("Second");

            Assert.Equal("Second", box.SelectedItem);
        }

        [Fact]
        public void MapHeaderSelectionRejectsIndexesOutsideTheSourceList()
        {
            var vm = new MapEditorViewModel();
            vm.HeaderNames.Add("First header");
            vm.HeaderNames.Add("Second header");
            vm.HeaderId = 1;

            vm.HeaderId = 99;

            Assert.Equal(1, vm.HeaderId);
        }

        private sealed class TestFusionAutoCompleteBox : FusionAutoCompleteBox
        {
            public void Press(Key key)
            {
                OnKeyDown(new KeyEventArgs { Key = key });
            }
        }
    }
}
