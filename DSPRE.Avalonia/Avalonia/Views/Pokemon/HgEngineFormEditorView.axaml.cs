using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class HgEngineFormEditorView : Window
    {
        private HgEngineFormEditorViewModel VM => (HgEngineFormEditorViewModel)DataContext;

        public HgEngineFormEditorView(HgEngineFormEditorViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
            this.FindControl<Button>("SaveButton").Click += (_, _) => VM?.SaveChanges();
            this.FindControl<Button>("AddSlotButton").Click += (_, _) => VM?.AddSlot();
            DSPRE.Avalonia.EditorWindowChrome.Attach(this, vm);
        }

        private void RemoveSlot_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.DataContext is FormSlotRow row) VM?.RemoveSlot(row);
        }
    }
}
