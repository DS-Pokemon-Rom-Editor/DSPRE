using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.Gl;
using DSPRE.Avalonia.ViewModels.World;

namespace DSPRE.Avalonia.Views.World
{
    /// <summary>Returns the chosen model number, or null when cancelled.</summary>
    public partial class BuildingModelPickerView : Window
    {
        private BuildingModelPickerViewModel VM => DataContext as BuildingModelPickerViewModel;
        private Gl3DPointerNavigation _nav;

        public BuildingModelPickerView()
        {
            InitializeComponent();
            _nav = new Gl3DPointerNavigation(GlHost, GlView);
        }

        public BuildingModelPickerView(BuildingModelPickerViewModel vm) : this()
        {
            DataContext = vm;
            vm.PreviewChanged += (_, _) => GlView.SetModel(vm.Preview);
            Opened += async (_, _) =>
            {
                SearchBox.Focus();
                await vm.LoadAsync();
                if (vm.Selected != null) ModelList.ScrollIntoView(vm.Selected);
            };
        }

        private void Use_Click(object sender, RoutedEventArgs e) => Close(VM?.Selected?.Id);
        private void Cancel_Click(object sender, RoutedEventArgs e) => Close(null);
        private void List_DoubleTapped(object sender, TappedEventArgs e) { if (VM?.Selected != null) Close(VM.Selected.Id); }
    }
}
