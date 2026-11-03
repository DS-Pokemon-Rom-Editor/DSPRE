using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Pokemon;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class BugContestEditorView : UserControl
    {
        private BugContestEditorViewModel VM => DataContext as BugContestEditorViewModel;

        public BugContestEditorView()
        {
            InitializeComponent();
            NameRefresh.Bind(this, () => VM?.RefreshNames());
        }

        public BugContestEditorView(BugContestEditorViewModel vm) : this()
        {
            DataContext = vm;
            Loaded += (_, _) => TellActiveTab();
            Tabs.SelectionChanged += (_, _) => TellActiveTab();
        }

        private void TellActiveTab()
        {
            if (VM != null) VM.ActiveTab = (Tabs.SelectedItem as TabItem)?.Content is Control c ? c.DataContext : null;
        }

        private void EditEncounters_Click(object sender, RoutedEventArgs e) => AvaloniaEditorLauncher.OpenSpecialEncountersEditorAtBugContest();
    }
}
