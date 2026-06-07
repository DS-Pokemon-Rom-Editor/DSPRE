using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class MoveDataEditorView : Window
    {
        private MoveDataEditorViewModel ViewModel => (MoveDataEditorViewModel)DataContext;

        public MoveDataEditorView()
        {
            InitializeComponent();
            DataContext = new MoveDataEditorViewModel();
            Closing += OnWindowClosing;
        }

        private async void OnWindowClosing(object sender, WindowClosingEventArgs e)
        {
            if (!ViewModel.HasUnsavedChanges) return;
            e.Cancel = true;
            if (await ViewModel.ConfirmCloseAsync())
            {
                Closing -= OnWindowClosing;
                Close();
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await ViewModel.SaveCommand();

        private async void Export_Click(object sender, RoutedEventArgs e)
            => await ViewModel.ExportCommand(this);

        private async void Import_Click(object sender, RoutedEventArgs e)
            => await ViewModel.ImportCommand(this);
    }
}
