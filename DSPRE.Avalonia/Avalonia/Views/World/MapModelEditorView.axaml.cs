using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.World;

namespace DSPRE.Avalonia.Views.World
{
    public partial class MapModelEditorView : Window
    {
        public MapModelEditorView(MapModelEditorViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
            Opened += (_, _) => vm.CountHeaders();

            // The Tiles tab handles its own Ctrl+Z in the painter; the Shape tab has only its toolbar Undo.
            KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(Key.Z, KeyModifiers.Control),
                Command = new EditorWindowChrome.RelayCommand(() =>
                {
                    if (_half == ShapeTab && vm.Shape.CanUndo) vm.Shape.Undo();
                }),
            });
        }

        private const int ShapeTab = 1;
        private int _half;

        public MapModelEditorView() : this(new MapModelEditorViewModel()) { }

        private void Done_Click(object sender, RoutedEventArgs e) => Close();

        private void Half_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, sender) || sender is not TabControl tabs) return;
            _half = tabs.SelectedIndex;
            (DataContext as MapModelEditorViewModel)?.Showing(tabs.SelectedIndex);
        }
    }
}
