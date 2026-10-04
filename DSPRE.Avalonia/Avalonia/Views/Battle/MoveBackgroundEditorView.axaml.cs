using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Battle;

namespace DSPRE.Avalonia.Views.Battle
{
    public partial class MoveBackgroundEditorView : Window
    {
        private MoveBackgroundEditorViewModel VM => DataContext as MoveBackgroundEditorViewModel;

        public MoveBackgroundEditorView() : this(new MoveBackgroundEditorViewModel()) { }

        public MoveBackgroundEditorView(MoveBackgroundEditorViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
            DSPRE.Avalonia.EditorWindowChrome.Attach(this, vm);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
        private void Add_Click(object sender, RoutedEventArgs e) => VM?.AddRow();
        private void Remove_Click(object sender, RoutedEventArgs e) => VM?.RemoveRow();
    }
}
