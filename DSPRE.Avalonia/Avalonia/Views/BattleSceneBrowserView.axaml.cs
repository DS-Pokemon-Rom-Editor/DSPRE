using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class BattleSceneBrowserView : Window
    {
        private BattleSceneBrowserViewModel ViewModel => (BattleSceneBrowserViewModel)DataContext;

        public BattleSceneBrowserView() : this(new BattleSceneBrowserViewModel()) { }

        public BattleSceneBrowserView(BattleSceneBrowserViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private void EditDrawing_Click(object sender, RoutedEventArgs e) => HandOver("drawing");
        private void EditColours_Click(object sender, RoutedEventArgs e) => HandOver("colours");

        /// <summary>Painting is the Graphics window's job, so this hands the file over rather than
        /// growing a second brush here.</summary>
        private void HandOver(string piece)
        {
            var vm = ViewModel;
            if (vm?.Selected == null)
            {
                _ = DialogHelper.ShowInfo("Pick a set of scenery on the left first.", "Battle scenes");
                return;
            }
            int file = vm.FileFor(piece);
            if (file < 0)
            {
                _ = DialogHelper.ShowInfo("There is no file to open for that.", "Battle scenes");
                return;
            }
            AvaloniaEditorLauncher.OpenGraphicAt(DSPRE.RomInfo.DirNames.battleBg, file);
            vm.Status = $"Opened file {file} in the Graphics window.";
        }
    }
}
