using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Pokemon;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class SwarmsView : UserControl
    {
        private SwarmsViewModel VM => DataContext as SwarmsViewModel;

        public SwarmsView() { InitializeComponent(); }

        // Coming back to the window after editing encounters elsewhere shows the new swarm Pokémon.
        private Window _window;
        protected override void OnAttachedToVisualTree(global::Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _window = TopLevel.GetTopLevel(this) as Window;
            if (_window != null) _window.Activated += Window_Activated;
            AppEvents.RomPatchStateChanged += OnPatchStateChanged;
        }

        protected override void OnDetachedFromVisualTree(global::Avalonia.VisualTreeAttachmentEventArgs e)
        {
            if (_window != null) _window.Activated -= Window_Activated;
            _window = null;
            AppEvents.RomPatchStateChanged -= OnPatchStateChanged;
            base.OnDetachedFromVisualTree(e);
        }

        private void Window_Activated(object sender, System.EventArgs e) => VM?.RefreshSpecies();

        private void OnPatchStateChanged(object sender, System.EventArgs e) => VM?.OnPatchStateChanged();

        private async void Save_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.SaveChangesAsync(); }
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
        private void Add_Click(object sender, RoutedEventArgs e) => VM?.Add();
        private void Remove_Click(object sender, RoutedEventArgs e) => VM?.Remove();

        // Delete removes the selected row, but not while typing in one of its boxes.
        private void List_KeyDown(object sender, global::Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key != global::Avalonia.Input.Key.Delete || e.Source is global::Avalonia.Controls.TextBox || VM?.Selected == null) return;
            VM.Remove();
            e.Handled = true;
        }
    }
}
