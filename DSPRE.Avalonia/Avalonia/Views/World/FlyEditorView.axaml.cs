using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels;
using System;

namespace DSPRE.Avalonia.Views.World
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

            // A zero width still leaves a sliver with a live cell in it, so the other family's columns are hidden.
            DataGrid unlock = this.FindControl<DataGrid>("UnlockGrid");
            for (int i = 0; unlock != null && i < unlock.Columns.Count; i++)
                unlock.Columns[i].IsVisible = i < 3 ? _vm.IsDpOrPlat : _vm.IsHgss;
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
            // Runtime should never call this, keep it to avoid errors
            throw new InvalidOperationException("Parameterless constructor only for design time.");
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await _vm.SaveCommand();

        private void Discard_Click(object sender, RoutedEventArgs e) => _vm?.DiscardChanges();
    }
}
