using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Views
{
    public partial class SpawnEditorView : Window
    {
        private SpawnEditorViewModel VM => (SpawnEditorViewModel)DataContext;

        public SpawnEditorView(SpawnEditorViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
            EditorWindowChrome.Attach(this, vm);
        }

        // Called from matrix editor with pre-selected header + coords
        public SpawnEditorView(HashSet<string> filteredHeaders, List<string> allNames,
                               ushort headerNumber = 0, int matrixX = 0, int matrixY = 0)
            : this(new SpawnEditorViewModel(filteredHeaders, allNames, headerNumber, matrixX, matrixY)) { }

        private void ResetFilter_Click(object sender, RoutedEventArgs e)
            => VM?.ResetFilter();

        private void Load_Click(object sender, RoutedEventArgs e)
            => VM?.LoadFromRom();

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await (VM?.SaveChangesAsync() ?? System.Threading.Tasks.Task.CompletedTask);
    }
}
