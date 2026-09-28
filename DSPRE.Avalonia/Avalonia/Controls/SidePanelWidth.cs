using System;
using System.Linq;
using Avalonia.Controls;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>Restores and remembers the width of a side panel that a GridSplitter resizes.</summary>
    public static class SidePanelWidth
    {
        /// <param name="grid">Columns: the preview (star), the splitter, the side panel.</param>
        public static void Remember(Grid grid, Func<double> load, Action<double> store)
        {
            var preview = grid.ColumnDefinitions[0];
            var side = grid.ColumnDefinitions[2];
            double saved = load();
            if (saved > 0) side.Width = new GridLength(Math.Max(saved, side.MinWidth));
            var splitter = grid.Children.OfType<GridSplitter>().FirstOrDefault();
            if (splitter == null) return;
            splitter.DragCompleted += (_, _) =>
            {
                // The splitter pins both columns in pixels; the preview goes back to taking the rest so it
                // still grows with the window.
                double width = side.ActualWidth;
                side.Width = new GridLength(width);
                preview.Width = new GridLength(1, GridUnitType.Star);
                store(width);
                SettingsManager.Save();
            };
        }
    }
}
