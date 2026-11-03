using System;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Sets a tab's view model up the first time its view shows, so an editor with several tabs opens without reading
    /// every tab's files. A setup that throws is tried again the next time the tab shows.
    /// </summary>
    public static class TabSetup
    {
        public static void OnFirstShow(Control view, Func<Window, Task> setup)
        {
            bool done = false;
            view.Loaded += async (_, _) =>
            {
                if (done || Design.IsDesignMode) return;
                done = true;
                try { await setup(TopLevel.GetTopLevel(view) as Window); }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    done = false;
                    CrashReporter.LogHandled(ex);
                }
            };
        }

        public static void OnFirstShow(Control view, Action setup) =>
            OnFirstShow(view, _ => { setup(); return Task.CompletedTask; });
    }
}
