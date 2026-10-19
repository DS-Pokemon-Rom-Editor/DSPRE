using System;

namespace DSPRE
{
    /// <summary>
    /// UI-agnostic user-message + file-picker hooks for the core (ROM logic) layer, so that layer no
    /// longer depends on <c>System.Windows.Forms</c>. Defaults log via <see cref="AppLogger"/> (so the
    /// core is headless-safe); the WinForms shell installs MessageBox / SaveFileDialog implementations
    /// and the Avalonia shell installs native Avalonia dialogs.
    ///
    /// This is the first step toward a WinForms-free, cross-platform core: ROMFiles/DSUtils raise user
    /// messages through here instead of calling MessageBox directly.
    /// </summary>
    public static class AppMessages
    {
        public static Action<string, string> ErrorHook = (msg, title) => AppLogger.Error(Prefix(title) + msg);
        public static Action<string, string> InfoHook = (msg, title) => AppLogger.Info(Prefix(title) + msg);
        public static Action<string, string> WarningHook = (msg, title) => AppLogger.Warn(Prefix(title) + msg);

        public enum ConfirmResult { Yes, No, Cancel }

        /// <summary>Yes/No confirmation. Returns true for Yes. Default = false (headless: don't do destructive things).</summary>
        public static Func<string, string, bool> ConfirmHook = (msg, title) => false;

        /// <summary>Yes/No/Cancel confirmation. Default = Cancel (headless: safest no-op).</summary>
        public static Func<string, string, ConfirmResult> ConfirmYesNoCancelHook = (msg, title) => ConfirmResult.Cancel;

        /// <summary>
        /// Ask the user for a save-file path. <paramref name="filter"/> is WinForms-style
        /// "Description (*.ext)|*.ext". Returns null if cancelled (or when running headless).
        /// </summary>
        public static Func<string, string, string, string> SaveFileHook = (title, filter, suggestedName) => null;

        /// <summary>Ask the user for a folder path. Returns null if cancelled (or when running headless).</summary>
        public static Func<string, string> PickFolderHook = title => null;

        /// <summary>
        /// Keep the UI responsive during a long synchronous operation. Default = no-op (headless / when
        /// the work already runs off the UI thread). The WinForms shell maps this to Application.DoEvents().
        /// </summary>
        public static Action PumpEventsHook = () => { };

        public static void Error(string message, string title = "Error") => ErrorHook?.Invoke(message, title);
        public static void Info(string message, string title = "") => InfoHook?.Invoke(message, title);
        public static void Warning(string message, string title = "Warning") => WarningHook?.Invoke(message, title);
        public static bool Confirm(string message, string title = "Confirm") => ConfirmHook?.Invoke(message, title) ?? false;
        public static ConfirmResult ConfirmYesNoCancel(string message, string title = "Confirm") => ConfirmYesNoCancelHook?.Invoke(message, title) ?? ConfirmResult.Cancel;

        public static string PickSaveFile(string title, string filter, string suggestedName = null)
            => SaveFileHook?.Invoke(title, filter, suggestedName);

        public static string PickFolder(string title) => PickFolderHook?.Invoke(title);

        public static void PumpEvents() => PumpEventsHook?.Invoke();

        private static string Prefix(string title) => string.IsNullOrEmpty(title) ? "" : title + ": ";
    }
}
