using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Shell
{
    public partial class CommandPaletteView : Window
    {
        private CommandPaletteViewModel VM => DataContext as CommandPaletteViewModel;

        public CommandPaletteView()
        {
            InitializeComponent();
        }

        public CommandPaletteView(CommandPaletteViewModel vm) : this()
        {
            DataContext = vm;
            Opened += (_, _) => Dispatcher.UIThread.Post(() => SearchBox.Focus());
            // Type-ahead from the search box; Enter/Esc/arrows drive the list.
            SearchBox.KeyDown += OnKey;
            ResultList.DoubleTapped += (_, _) => Launch();
            KeyDown += OnKey;
        }

        private void OnKey(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:  Launch(); e.Handled = true; break;
                case Key.Escape: Close(); e.Handled = true; break;
                case Key.Down:   Move(1);  e.Handled = true; break;
                case Key.Up:     Move(-1); e.Handled = true; break;
            }
        }

        private void Move(int delta)
        {
            if (VM == null || VM.Items.Count == 0) return;
            int n = VM.Items.Count;
            VM.SelectedIndex = ((VM.SelectedIndex + delta) % n + n) % n;
            ResultList.ScrollIntoView(VM.SelectedIndex);
        }

        private void Launch()
        {
            var cmd = VM?.Selected;
            Close();
            cmd?.Run?.Invoke();
        }
    }
}
