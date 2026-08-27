using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class CompileRomView : Window
    {
        private CompileRomViewModel VM => (CompileRomViewModel)DataContext;

        public CompileRomView()
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = new CompileRomViewModel();
            VM.LogLines.CollectionChanged += LogLines_CollectionChanged;
            Closing += (_, e) => { if (!VM.CanClose) e.Cancel = true; };
        }

        private void LogLines_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var listBox = this.FindControl<ListBox>("LogList");
            if (listBox != null && VM.LogLines.Count > 0)
                listBox.ScrollIntoView(VM.LogLines[VM.LogLines.Count - 1]);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        /// <summary>Shows the window modally and kicks off the build; the window can't be closed until
        /// the build finishes (see the Closing handler above), so by the time this returns, the build
        /// (and any exception it raised) has already been fully observed.</summary>
        public async System.Threading.Tasks.Task ShowAndRunAsync(Window owner)
        {
            var runTask = VM.RunAsync();
            await ShowDialog(owner);
            await runTask;
        }
    }
}
