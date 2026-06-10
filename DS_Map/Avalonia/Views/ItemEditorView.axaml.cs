using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class ItemEditorView : Window
    {
        private ItemEditorViewModel VM => (ItemEditorViewModel)DataContext;

        public ItemEditorView(ItemEditorViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
            this.FindControl<Button>("SaveAllButton").Click += (_, _) => VM?.SaveChanges();
        }

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            if (VM?.HasUnsavedChanges == true)
            {
                e.Cancel = true;
                bool discard = await DialogHelper.AskYesNo(
                    $"Discard unsaved changes to {VM.UnsavedChangesDescription}?",
                    "Unsaved Changes");
                if (discard) { VM.DiscardChanges(); Close(); }
            }
            base.OnClosing(e);
        }
    }
}
