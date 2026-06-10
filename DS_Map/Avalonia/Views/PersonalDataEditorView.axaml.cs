using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class PersonalDataEditorView : UserControl
    {
        private PersonalDataEditorViewModel ViewModel => (PersonalDataEditorViewModel)DataContext;

        public PersonalDataEditorView()
        {
            InitializeComponent();
        }

        public PersonalDataEditorView(PersonalDataEditorViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await ViewModel.SaveCommand();

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            var window = TopLevel.GetTopLevel(this) as Window;
            await ViewModel.ExportCommand(window);
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            var window = TopLevel.GetTopLevel(this) as Window;
            await ViewModel.ImportCommand(window);
        }

        private void AddMachine_Click(object sender, RoutedEventArgs e)
            => ViewModel.AddMachineCommand();

        private void RemoveMachine_Click(object sender, RoutedEventArgs e)
            => ViewModel.RemoveMachineCommand();

        private void AddAll_Click(object sender, RoutedEventArgs e)
            => ViewModel.AddAllMachinesCommand();

        private void RemoveAll_Click(object sender, RoutedEventArgs e)
            => ViewModel.RemoveAllMachinesCommand();
    }
}
