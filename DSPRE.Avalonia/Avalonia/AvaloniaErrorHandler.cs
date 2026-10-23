using System;
using Avalonia.Threading;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Catches UI-thread exceptions from async void handlers, so one failing editor doesn't take the others' unsaved work down.
    /// <see cref="CrashReporter"/> handles the fatal ones.
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

        /// <summary>
        /// Unwraps the real failure: Avalonia calls routed-event handlers by reflection, so their exceptions arrive wrapped.
        /// </summary>
        private static Exception Cause(Exception ex)
        {
            while (ex is System.Reflection.TargetInvocationException || (ex is AggregateException agg && agg.InnerExceptions.Count == 1))
            {
                if (ex.InnerException == null) break;
                ex = ex.InnerException;
            }
            return ex;
        }

        private static async void Report(Exception ex)
        {
            if (_showing) return;     // an error dialog is already up; don't stack more on top
            _showing = true;
            try
            {
                string path = CrashReporter.LogHandled(ex);
                await DialogHelper.ShowErrorAsync(
                    "An unexpected error occurred, but the app is still running and your other open " +
                    "editors are unaffected.\n\n" + (Cause(ex)?.Message ?? "Unknown error.") +
                    (path != null ? "\n\nA detailed report was saved to:\n" + path : ""),
                    "Unexpected error",
                    ex?.ToString());
            }
            catch
            {
                // The error handler must never throw: that would re-enter the dispatcher net.
            }
            finally
            {
                _showing = false;
            }
        }
    }
}
