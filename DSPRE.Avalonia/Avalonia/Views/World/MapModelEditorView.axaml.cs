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

            // Ctrl+Z and Ctrl+Y go to the focused text box, else to the open tab's undo. The Shape tab has no redo.
            KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(Key.Z, KeyModifiers.Control),
                Command = new EditorWindowChrome.RelayCommand(() =>
                {
                    if (FocusManager?.GetFocusedElement() is TextBox text) { if (text.CanUndo) text.Undo(); }
                    else if (_half == ShapeTab) { if (vm.Shape.CanUndo) vm.Shape.Undo(); }
                    else vm.Tiles.UndoPaint();
                }),
            });
            KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(Key.Y, KeyModifiers.Control),
                Command = new EditorWindowChrome.RelayCommand(() =>
                {
                    if (FocusManager?.GetFocusedElement() is TextBox text) { if (text.CanRedo) text.Redo(); }
                    else if (_half != ShapeTab) vm.Tiles.RedoPaint();
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
