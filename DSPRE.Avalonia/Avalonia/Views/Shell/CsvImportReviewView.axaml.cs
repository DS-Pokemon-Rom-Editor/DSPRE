using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels.Shell;
using DSPRE.Csv;

namespace DSPRE.Avalonia.Views.Shell
{
    public partial class CsvImportReviewView : Window
    {
        private CsvImportReviewViewModel VM => (CsvImportReviewViewModel)DataContext;

        public CsvImportReviewView() { AvaloniaXamlLoader.Load(this); }

        public CsvImportReviewView(CsvImportReviewViewModel vm) : this() { DataContext = vm; }

        /// <summary>Asks for a CSV file and reviews it. The session to apply, or null when cancelled.</summary>
        public static async Task<CsvImportSession> ReviewAsync(Window owner, CsvImporter importer)
        {
            string path = await DialogHelper.OpenFile(owner, $"{importer.Title} CSV",
                new[] { DialogHelper.CsvFilter, DialogHelper.AllFilter });
            if (path == null) return null;
            return await ReviewAsync(owner, CsvImportSession.Open(importer, path));
        }

        public static async Task<CsvImportSession> ReviewAsync(Window owner, CsvImportSession session)
        {
            CsvImportReviewView view = new CsvImportReviewView(new CsvImportReviewViewModel(session));
            await view.ShowDialog(owner);
            return view.VM.Confirmed ? session : null;
        }

        private void Fix_Click(object sender, RoutedEventArgs e) => VM.UseFix((sender as Button)?.Tag as CsvFix);
        private void Edit_Click(object sender, RoutedEventArgs e) => VM.ApplyEdit();
        private void Edit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            VM.ApplyEdit();
            e.Handled = true;
        }
        private void Skip_Click(object sender, RoutedEventArgs e) => VM.ToggleSkip();
        private void Keep_Click(object sender, RoutedEventArgs e) => VM.ToggleKeep();
        private void SkipErrors_Click(object sender, RoutedEventArgs e) => VM.SkipRowsWithErrors();
        private void KeepWarnings_Click(object sender, RoutedEventArgs e) => VM.KeepAllWarnings();
        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            VM.Accept();
            if (VM.Confirmed) Close();
        }
    }
}
