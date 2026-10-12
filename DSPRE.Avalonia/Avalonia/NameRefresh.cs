using System;
using Avalonia.Controls;
using Avalonia.Threading;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Keeps a view's name lists current: runs <c>refresh</c> on <see cref="AppEvents.NamesChanged"/> while the view
    /// is shown, and once on showing again if names changed while it was hidden (an unselected tab). Call it from the
    /// view constructor, before the view is first shown.
    /// </summary>
    public static class NameRefresh
    {
        public static void Bind(Control view, Action refresh)
        {
            int seen = AppEvents.NamesVersion;
            EventHandler onChanged = (_, _) =>
            {
                seen = AppEvents.NamesVersion;
                if (Dispatcher.UIThread.CheckAccess()) refresh();
                else Dispatcher.UIThread.Post(refresh);
            };
            void Shown()
            {
                AppEvents.NamesChanged += onChanged;
                if (seen != AppEvents.NamesVersion) { seen = AppEvents.NamesVersion; refresh(); }
            }
            void Hidden() => AppEvents.NamesChanged -= onChanged;
            // A window is the root of its own visual tree, so it uses its open and close instead.
            if (view is Window window)
            {
                window.Opened += (_, _) => Shown();
                window.Closed += (_, _) => Hidden();
            }
            else
            {
                view.AttachedToVisualTree += (_, _) => Shown();
                view.DetachedFromVisualTree += (_, _) => Hidden();
            }
        }
    }
}
