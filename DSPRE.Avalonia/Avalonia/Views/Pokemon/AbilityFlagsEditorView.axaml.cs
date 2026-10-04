using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Pokemon;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class AbilityFlagsEditorView : Window
    {
        private AbilityFlagsEditorViewModel VM => DataContext as AbilityFlagsEditorViewModel;

        public AbilityFlagsEditorView() : this(new AbilityFlagsEditorViewModel()) { }

        public AbilityFlagsEditorView(AbilityFlagsEditorViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
            DSPRE.Avalonia.EditorWindowChrome.Attach(this, vm);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
    }
}
