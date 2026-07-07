using System;
using Avalonia.Threading;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Last-resort net for exceptions thrown on the Avalonia UI thread — chiefly from <c>async void</c>
    /// event handlers (Save / Import / close, etc.) that have no try/catch of their own. Without this a
    /// single throw (corrupt file, bad ROM offset, disk full) tears down the whole process and every OTHER
    /// open editor's unsaved work with it. Here we log it, tell the user, and KEEP the app alive.
    ///
    /// This complements <see cref="CrashReporter"/> (which handles truly fatal AppDomain/Task exceptions):
    /// those fire while the process is already dying, whereas this one recovers.
    /// </summary>
    public static class AvaloniaErrorHandler
    {
        private static bool _showing;   // guard against dialog-on-dialog storms

        /// <summary>Hook the dispatcher. Call once during application startup.</summary>
        public static void Install()
        {
            Dispatcher.UIThread.UnhandledException += OnUnhandled;
        }

        private static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Marking it handled stops Avalonia from rethrowing → the process (and every other open
            // editor's unsaved work) survives. We still record and surface the failure.
            e.Handled = true;
            Report(e.Exception);
        }

        private static async void Report(Exception ex)
        {
            if (_showing) return;     // an error dialog is already up; don't stack more on top
            _showing = true;
            try
            {
                string path = CrashReporter.LogHandled(ex);
                await DialogHelper.ShowError(
                    "An unexpected error occurred, but the app is still running and your other open " +
                    "editors are unaffected.\n\n" + (ex?.Message ?? "Unknown error.") +
                    (path != null ? "\n\nA detailed report was saved to:\n" + path : ""),
                    "Unexpected Error",
                    ex?.ToString());
            }
            catch
            {
                // The error handler must never throw — that would re-enter the dispatcher net.
            }
            finally
            {
                _showing = false;
            }
        }
    }
}
