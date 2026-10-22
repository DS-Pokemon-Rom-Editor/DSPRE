using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Graphics;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class SummaryScreenEditorView : UserControl
    {
        public SummaryScreenEditorView() : this(new ScreenPagesEditorViewModel(new SummaryBook(), null)) { }

        public SummaryScreenEditorView(ScreenPagesEditorViewModel vm, string shows = "All")
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = vm;
            ScreenPagesView pages = this.FindControl<ScreenPagesView>("Pages");
            if (pages != null) pages.Shows = shows;
        }
    }
}
