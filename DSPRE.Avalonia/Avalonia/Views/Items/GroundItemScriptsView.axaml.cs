using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Items
{
    public partial class GroundItemScriptsView : Window
    {
        private GroundItemScriptsViewModel VM => DataContext as GroundItemScriptsViewModel;

        public GroundItemScriptsView()
        {
            InitializeComponent();
        }

        public GroundItemScriptsView(GroundItemScriptsViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void Add_Click(object sender, RoutedEventArgs e) => VM?.AddEntry();

        private void Remove_Click(object sender, RoutedEventArgs e) => VM?.RemoveSelectedEntry();

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.Discard();
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private bool _closeConfirmed;
        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            if (_closeConfirmed || VM?.HasUnsavedChanges != true) return;
            e.Cancel = true;
            var answer = await DialogHelper.AskYesNoCancel("Save the ground item list before closing?", "Ground Item List");
            if (answer == DialogHelper.MsgResult.Cancel) return;
            if (answer == DialogHelper.MsgResult.Yes) VM.Save();
            _closeConfirmed = true;
            Close();
        }
    }
}
