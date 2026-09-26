using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels.World;

namespace DSPRE.Avalonia.Views.World
{
    public partial class EventsAfterImportView : Window
    {
        private readonly MapEditorViewModel _vm;
        private readonly List<MapEditorViewModel.EventClash> _clashes;

        public EventsAfterImportView() => AvaloniaXamlLoader.Load(this);

        public EventsAfterImportView(MapEditorViewModel vm, List<MapEditorViewModel.EventClash> clashes) : this()
        {
            _vm = vm;
            _clashes = clashes;
            this.FindControl<ItemsControl>("List").ItemsSource = clashes;
        }

        private void Move_Click(object sender, RoutedEventArgs e)
        {
            int moved = _vm.MoveEvents(_clashes);
            this.FindControl<TextBlock>("Summary").Text = moved == 0 ? "Nothing fixed."
                : $"Fixed {moved}. Save the map to keep {(moved > 1 ? "them" : "it")}.";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
