using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels.Shell;

namespace DSPRE.Avalonia.Views.Shell
{
    public partial class EmulatorPickerView : Window
    {
        private EmulatorPickerViewModel VM => (EmulatorPickerViewModel)DataContext;

        public EmulatorPickerView()
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = new EmulatorPickerViewModel();
        }

        /// <summary>Asks for an emulator. The chosen kind and path, or null when cancelled.</summary>
        public static async System.Threading.Tasks.Task<(EmulatorKind Kind, string Path)?> AskAsync(Window owner)
        {
            var view = new EmulatorPickerView();
            await view.ShowDialog(owner);
            return view.VM.Confirmed ? (view.VM.Kind, view.VM.Path) : null;
        }

        private async void Browse_Click(object sender, RoutedEventArgs e)
        {
            var filters = OperatingSystem.IsWindows()
                ? new List<FilePickerFileType> { new FilePickerFileType("Programs") { Patterns = new[] { "*.exe" } } }
                : null;
            string path = await DialogHelper.OpenFile(this, "Choose the emulator", filters);
            if (path != null) VM.Path = path;
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            if (VM.TryConfirm()) Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
