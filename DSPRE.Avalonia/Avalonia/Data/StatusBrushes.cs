using Avalonia;
using Avalonia.Media;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Good, warning and bad colours for status text, taken from whichever theme is on.</summary>
    public static class StatusBrushes
    {
        public static IBrush Good => Look("Editor.Good", Brushes.Green);
        public static IBrush Warn => Look("Editor.Warn", Brushes.DarkOrange);
        public static IBrush Bad => Look("Editor.Bad", Brushes.Red);
        public static IBrush None => Brushes.Transparent;
        public static IBrush Quiet => Look("Editor.Subtle", Brushes.Gray);

        // A dark green that reads on white is nearly invisible on the dark theme, and the other way round,
        // so the colour has to come from the theme rather than be written into the editor.
        private static IBrush Look(string key, IBrush fallback)
        {
            var app = Application.Current;
            if (app != null && app.TryGetResource(key, app.ActualThemeVariant, out object found) && found is IBrush b)
                return b;
            return fallback;
        }
    }
}
