using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Graphics;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class PokedexGraphicsEditorView : Window
    {
        public PokedexGraphicsEditorView() : this(new ScreenPagesEditorViewModel(new PokedexBook(), null)) { }

        public PokedexGraphicsEditorView(ScreenPagesEditorViewModel vm)
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = vm;
            EditorWindowChrome.Attach(this, vm);
            EditorWindowChrome.AttachUndoKeys(this, vm);
        }
    }
}
