using Avalonia.Controls;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class ScriptCommandGuideView : Window
    {
        public ScriptCommandGuideView(ScriptCommandGuideViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
        }

        public ScriptCommandGuideView() : this(new ScriptCommandGuideViewModel()) { }
    }
}
