using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Text
{
    public partial class CharMapManagerView : Window
    {
        private CharMapManagerViewModel _vm;

        public CharMapManagerView()
        {
            AvaloniaXamlLoader.Load(this);
            _vm = new CharMapManagerViewModel();
            DataContext = _vm;
            EditorWindowChrome.Attach(this, _vm);
        }

        private async void AddAlias_Click(object sender, RoutedEventArgs e)
            => await _vm.AddAliasCommandAsync();

        private async void RemoveAlias_Click(object sender, RoutedEventArgs e)
            => await _vm.RemoveAliasCommandAsync();

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await _vm.SaveCommandAsync();

        private async void CreateMap_Click(object sender, RoutedEventArgs e)
            => await _vm.CreateMapCommandAsync();

        private async void DeleteMap_Click(object sender, RoutedEventArgs e)
            => await _vm.DeleteMapCommandAsync();

        private async void Reload_Click(object sender, RoutedEventArgs e)
            => await _vm.ReloadCommandAsync();

        private async void OpenFile_Click(object sender, RoutedEventArgs e)
            => await _vm.OpenFileCommandAsync();

        private async void Rebase_Click(object sender, RoutedEventArgs e)
            => await _vm.RebaseCommandAsync();

        private void Search_Click(object sender, RoutedEventArgs e)
            => _vm.SearchCommand();

        private void Search_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _vm.SearchCommand();
                e.Handled = true;
            }
        }

        private void CharMapList_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (_vm.SelectedCharMapIndex >= 0 && _vm.CharMapItems.Count > _vm.SelectedCharMapIndex)
            {
                string item = _vm.CharMapItems[_vm.SelectedCharMapIndex];
                _ = TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(item);
            }
        }

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) (DataContext as DSPRE.Editors.IEditorWithUnsavedChanges)?.DiscardChanges();
        }
    }
}
