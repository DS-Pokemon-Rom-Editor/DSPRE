using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class WildEditorDPPtView : WildEditorViewBase
    {
        public WildEditorDPPtView(WildEditorDPPtViewModel vm)
        {
            InitializeComponent();
            SlotOddsButton.IsVisible = BetaEditors.Allows("EncounterSlotOddsView");
            DataContext = vm;
        }
    }
}
