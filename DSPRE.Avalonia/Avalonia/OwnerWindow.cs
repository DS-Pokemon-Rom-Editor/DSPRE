using System;
using System.Linq;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// The last window clicked, typed in or activated, which owns prompts, busy screens and new editors.
    /// The active window is not enough: long work can finish after focus moved or with no DSPRE window in front.
    /// </summary>
    public static class OwnerWindow
    {
        private static WeakReference<Window> _last;

        public static void Install()
        {
            InputElement.PointerPressedEvent.AddClassHandler<Window>((w, _) => Note(w), RoutingStrategies.Tunnel, handledEventsToo: true);
            InputElement.KeyDownEvent.AddClassHandler<Window>((w, _) => Note(w), RoutingStrategies.Tunnel, handledEventsToo: true);
            WindowBase.IsActiveProperty.Changed.AddClassHandler<Window>((w, _) => { if (w.IsActive) Note(w); });
        }

        private static void Note(Window w) => _last = new WeakReference<Window>(w);

        public static Window Current
        {
            get
            {
                if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime d) return null;
                if (_last != null && _last.TryGetTarget(out Window w) && w.IsVisible && d.Windows.Contains(w)) return w;
                return d.Windows.FirstOrDefault(x => x.IsActive && x.IsVisible) ?? d.MainWindow;
            }
        }
    }
}
