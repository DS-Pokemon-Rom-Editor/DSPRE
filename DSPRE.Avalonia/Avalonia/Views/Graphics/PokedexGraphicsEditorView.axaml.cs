using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Graphics;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class PokedexGraphicsEditorView : UserControl
    {
        public PokedexGraphicsEditorView() : this(new ScreenPagesEditorViewModel(new PokedexBook(), null)) { }

        public PokedexGraphicsEditorView(ScreenPagesEditorViewModel vm)
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = vm;
        }
    }
}
