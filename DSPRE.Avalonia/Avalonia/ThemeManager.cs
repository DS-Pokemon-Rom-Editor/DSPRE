using global::Avalonia;
using global::Avalonia.Styling;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Central place to read/switch the Avalonia UI theme at runtime. The editor chrome brushes
    /// (Editor.*) are defined per ThemeVariant in App.axaml, so flipping
    /// <see cref="Application.RequestedThemeVariant"/> re-skins every editor. This keeps the door
    /// open for a user-facing Light/Dark toggle (wired to the main-window View menu).
    /// </summary>
    public static class ThemeManager
    {
        public static bool IsDark
        {
            get
            {
                var v = Application.Current?.RequestedThemeVariant;
                // Default (unset) follows the app default, which is Dark.
                return v == null || v == ThemeVariant.Default || v == ThemeVariant.Dark;
            }
        }

        public static void SetDark(bool dark)
        {
            if (Application.Current != null)
                Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        public static void Toggle() => SetDark(!IsDark);
    }
}
