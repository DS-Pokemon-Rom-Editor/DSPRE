using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.World
{
    public partial class HeaderSearchView : Window
    {
        private HeaderSearchViewModel VM => DataContext as HeaderSearchViewModel;

        public HeaderSearchView()
        {
            InitializeComponent();
            Closed += (_, _) => VM?.Dispose();
        }

        public HeaderSearchView(HeaderSearchViewModel vm) : this()
        {
            DataContext = vm;
        }

        /// <summary>Where a picked result goes; the Header editor window when nothing else asked.</summary>
        public System.Action<int> GoTo { get; set; }

        private void Search_Click(object sender, RoutedEventArgs e) => VM?.Search();

        private void Result_DoubleTapped(object sender, TappedEventArgs e)
        {
            int id = VM?.SelectedHeaderId() ?? -1;
            if (id >= 0) (GoTo ?? (h => AvaloniaEditorLauncher.OpenHeaderEditor(h)))(id);
        }
    }
}
