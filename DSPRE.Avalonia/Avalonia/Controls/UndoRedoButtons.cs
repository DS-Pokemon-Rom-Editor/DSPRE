using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Layout;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>Undo and Redo buttons for whatever <see cref="ISupportsUndo"/> the DataContext is.</summary>
    public class UndoRedoButtons : StackPanel
    {
        private readonly Button _undo = new Button { Content = Icon.Content("undo", "Undo"), IsEnabled = false };
        private readonly Button _redo = new Button { Content = Icon.Content("redo", "Redo"), IsEnabled = false };
        private INotifyPropertyChanged _watched;

        public UndoRedoButtons()
        {
            Orientation = Orientation.Horizontal;
            Spacing = 4;
            VerticalAlignment = VerticalAlignment.Center;
            ToolTip.SetTip(_undo, "Undo (Ctrl+Z)");
            global::Avalonia.Automation.AutomationProperties.SetName(_undo, "Undo");
            global::Avalonia.Automation.AutomationProperties.SetName(_redo, "Redo");
            ToolTip.SetTip(_redo, "Redo (Ctrl+Y)");
            _undo.Click += (_, _) => { (DataContext as ISupportsUndo)?.Undo(); Refresh(); };
            _redo.Click += (_, _) => { (DataContext as ISupportsUndo)?.Redo(); Refresh(); };
            Children.Add(_undo);
            Children.Add(_redo);
        }

        protected override void OnDataContextChanged(System.EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (_watched != null) _watched.PropertyChanged -= Watched_PropertyChanged;
            _watched = DataContext as INotifyPropertyChanged;
            if (_watched != null) _watched.PropertyChanged += Watched_PropertyChanged;
            Refresh();
        }

        private void Watched_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ISupportsUndo.CanUndo) or nameof(ISupportsUndo.CanRedo) or null or "") Refresh();
        }

        private void Refresh()
        {
            var u = DataContext as ISupportsUndo;
            _undo.IsEnabled = u?.CanUndo == true;
            _redo.IsEnabled = u?.CanRedo == true;
        }
    }
}
