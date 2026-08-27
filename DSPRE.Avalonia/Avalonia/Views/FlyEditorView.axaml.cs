using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels;
using System;

namespace DSPRE.Avalonia.Views
{
    public partial class FlyEditorView : Window
    {
        private FlyEditorViewModel _vm;

        public FlyEditorView(System.Collections.Generic.List<string> headerNames)
        {
            AvaloniaXamlLoader.Load(this);
            _vm = new FlyEditorViewModel(headerNames);
            DataContext = _vm;
            // VM owns the bound Title (+ "*" marker); chrome adds Ctrl+S + the close guard.
            EditorWindowChrome.Attach(this, _vm, manageTitle: false);
        }

        // Parameterless constructor for previewer only
        public FlyEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            if (Design.IsDesignMode)
            {
                // Create a design-time ViewModel (parameterless ctor will provide dummy data)
                _vm = new FlyEditorViewModel();
                DataContext = _vm;
                return;
            }
            // Runtime should never call this – keep it to avoid errors
            throw new InvalidOperationException("Parameterless constructor only for design time.");
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await _vm.SaveCommand();
    }
}
