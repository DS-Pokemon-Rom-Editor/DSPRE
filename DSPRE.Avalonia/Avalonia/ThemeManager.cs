using System;
using System.Runtime.InteropServices;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Interactivity;
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
                ThemeVariant v = Application.Current?.RequestedThemeVariant;
                // Default (unset) follows the app default, which is Dark.
                return v == null || v == ThemeVariant.Default || v == ThemeVariant.Dark;
            }
        }

        public static void SetDark(bool dark)
        {
            if (Application.Current != null)
                Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            if (DSPRE.SettingsManager.Settings != null)
            {
                DSPRE.SettingsManager.Settings.darkTheme = dark;
                DSPRE.SettingsManager.Save();
            }
            ApplyTitleBars();
        }

        public static void Toggle() => SetDark(!IsDark);

        /// <summary>Puts back the skin the last session was left on.</summary>
        public static void ApplySaved()
        {
            DspreSettings s = DSPRE.SettingsManager.Settings;
            if (s != null && Application.Current != null)
                Application.Current.RequestedThemeVariant = s.darkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
            WatchTitleBars();
        }

        // ── Windows title bars ─────────────────────────────────────────────────────────────────────
        // The title bar is drawn by Windows, which only darkens it when asked. Windows 11 and Windows 10 from 20H1
        // take attribute 20; earlier Windows 10 builds take 19.

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);

        // SWP_NOSIZE | SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED: redraw the frame, change nothing else.
        private const uint FrameChanged = 0x0037;

        private const int ImmersiveDarkMode = 20, ImmersiveDarkModeBefore20H1 = 19;
        private static bool _watching;

        private static void WatchTitleBars()
        {
            if (_watching || !OperatingSystem.IsWindows()) return;
            _watching = true;
            Control.LoadedEvent.AddClassHandler<Window>((window, _) => ApplyTitleBar(window), RoutingStrategies.Direct);
        }

        private static void ApplyTitleBars()
        {
            if (!OperatingSystem.IsWindows()) return;
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                foreach (Window window in desktop.Windows) ApplyTitleBar(window);
        }

        /// <summary>Darkens or lightens one window's title bar. Safe before the window is shown, once it has a handle.</summary>
        internal static void ApplyTitleBar(Window window)
        {
            if (!OperatingSystem.IsWindows()) return;
            IntPtr handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle == IntPtr.Zero) return;
            int dark = IsDark ? 1 : 0;
            try
            {
                if (DwmSetWindowAttribute(handle, ImmersiveDarkMode, ref dark, sizeof(int)) != 0)
                    DwmSetWindowAttribute(handle, ImmersiveDarkModeBefore20H1, ref dark, sizeof(int));
                // A window already on screen keeps its old caption until the frame is redrawn.
                if (window.IsVisible) SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, FrameChanged);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException) { }
        }
    }
}
