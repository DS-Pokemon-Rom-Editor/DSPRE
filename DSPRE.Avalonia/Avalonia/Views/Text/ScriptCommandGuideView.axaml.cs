using Avalonia.Controls;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Text
{
    public partial class ScriptCommandGuideView : Window
    {
        public ScriptCommandGuideView(ScriptCommandGuideViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
            Grid.Columns[1].Header = vm.SecondColumn;
            Grid.Columns[2].Header = vm.ThirdColumn;
            if (vm.IsEventScript)
            {
                Grid.Columns[1].Width = new DataGridLength(70);
                Grid.Columns[2].Width = new DataGridLength(260);
            }
        }

        public ScriptCommandGuideView() : this(new ScriptCommandGuideViewModel()) { }
    }
}
