using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Views.World
{
    public partial class SpawnEditorView : Window
    {
        private SpawnEditorViewModel VM => (SpawnEditorViewModel)DataContext;

        public SpawnEditorView(SpawnEditorViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
            EditorWindowChrome.Attach(this, vm);
            NameRefresh.Bind(this, vm.RefreshNames);
        }

        // Called from matrix editor with pre-selected header + coords
        public SpawnEditorView(HashSet<string> filteredHeaders, List<string> allNames,
                               ushort headerNumber = 0, int matrixX = 0, int matrixY = 0)
            : this(new SpawnEditorViewModel(filteredHeaders, allNames, headerNumber, matrixX, matrixY)) { }

        private void ResetFilter_Click(object sender, RoutedEventArgs e)
            => VM?.ResetFilter();

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await DialogHelper.GuardAsync(() => VM?.SaveChangesAsync() ?? System.Threading.Tasks.Task.CompletedTask,
                "The spawn point wasn't saved.", "Spawn point editor error", this);
    }
}
