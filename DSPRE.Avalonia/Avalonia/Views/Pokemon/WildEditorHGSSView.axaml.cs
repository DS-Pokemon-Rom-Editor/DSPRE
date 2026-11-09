using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class WildEditorHGSSView : WildEditorViewBase
    {
        public WildEditorHGSSView(WildEditorHGSSViewModel vm)
        {
            InitializeComponent();
            SlotOddsButton.IsVisible = BetaEditors.Allows("EncounterSlotOddsView");
            DataContext = vm;
        }
    }
}
