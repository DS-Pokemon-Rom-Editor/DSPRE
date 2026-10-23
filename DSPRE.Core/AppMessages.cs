using System;

namespace DSPRE
{
    /// <summary>
    /// User messages and file pickers for the core; the Avalonia shell installs the real dialogs, and the defaults only
    /// log.
    /// </summary>
    public static class AppMessages
    {
        public static Action<string, string> ErrorHook = (msg, title) => AppLogger.Error(Prefix(title) + msg);
        public static Action<string, string> InfoHook = (msg, title) => AppLogger.Info(Prefix(title) + msg);
        public static Action<string, string> WarningHook = (msg, title) => AppLogger.Warn(Prefix(title) + msg);
        // A save or export that worked: a short notice, not a dialog.
        public static Action<string> NoticeHook = msg => AppLogger.Info(msg);

        public enum ConfirmResult { Yes, No, Cancel }

        /// <summary>Yes/No confirmation. Returns true for Yes. Default = false (headless: don't do destructive things).</summary>
        public static Func<string, string, bool> ConfirmHook = (msg, title) => false;

        /// <summary>Yes/No/Cancel confirmation. Default = Cancel (headless: safest no-op).</summary>
        public static Func<string, string, ConfirmResult> ConfirmYesNoCancelHook = (msg, title) => ConfirmResult.Cancel;

        /// <summary>Asks for a save path; <paramref name="filter"/> looks like "Description (*.ext)|*.ext". Null when
        /// cancelled or headless.</summary>
        public static Func<string, string, string, string> SaveFileHook = (title, filter, suggestedName) => null;

        /// <summary>Ask the user for a folder path. Returns null if cancelled (or when running headless).</summary>
        public static Func<string, string> PickFolderHook = title => null;

        /// <summary>Keeps the UI responsive during a long synchronous operation. Does nothing by default.</summary>
        public static Action PumpEventsHook = () => { };

        public static void Error(string message, string title = "Error") => ErrorHook?.Invoke(message, title);
        public static void Info(string message, string title = "") => InfoHook?.Invoke(message, title);
        public static void Warning(string message, string title = "Warning") => WarningHook?.Invoke(message, title);
        public static void Notice(string message) => NoticeHook?.Invoke(message);
        public static bool Confirm(string message, string title = "Confirm") => ConfirmHook?.Invoke(message, title) ?? false;
        public static ConfirmResult ConfirmYesNoCancel(string message, string title = "Confirm") => ConfirmYesNoCancelHook?.Invoke(message, title) ?? ConfirmResult.Cancel;

        public static string PickSaveFile(string title, string filter, string suggestedName = null)
            => SaveFileHook?.Invoke(title, filter, suggestedName);

        public static string PickFolder(string title) => PickFolderHook?.Invoke(title);

        public static void PumpEvents() => PumpEventsHook?.Invoke();

        private static string Prefix(string title) => string.IsNullOrEmpty(title) ? "" : title + ": ";
    }
}
