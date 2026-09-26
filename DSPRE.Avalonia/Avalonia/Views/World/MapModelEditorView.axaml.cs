using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.World;

namespace DSPRE.Avalonia.Views.World
{
    public partial class MapModelEditorView : Window
    {
        public MapModelEditorView(MapModelEditorViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
            Opened += (_, _) => vm.CountHeaders();
        }

        public MapModelEditorView() : this(new MapModelEditorViewModel()) { }

        private void Done_Click(object sender, RoutedEventArgs e) => Close();

        private void Half_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, sender) || sender is not TabControl tabs) return;
            (DataContext as MapModelEditorViewModel)?.Showing(tabs.SelectedIndex);
        }
    }
}
