using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Graphics;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class SummaryScreenEditorView : Window
    {
        public SummaryScreenEditorView() : this(new ScreenPagesEditorViewModel(new SummaryBook(), null)) { }

        public SummaryScreenEditorView(ScreenPagesEditorViewModel vm)
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = vm;
            EditorWindowChrome.Attach(this, vm);
            EditorWindowChrome.AttachUndoKeys(this, vm);
        }
    }
}
