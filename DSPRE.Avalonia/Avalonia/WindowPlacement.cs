using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Multi-monitor window placement. Editors/pop-ups are opened with <c>.Show()</c> and no owner, so their
    /// XAML <c>WindowStartupLocation="CenterScreen"</c> always lands them on the PRIMARY monitor, even when the
    /// user has dragged the editor they opened it from onto another screen. <see cref="ShowManaged"/> instead
    /// positions the new window on the currently-active window's monitor (a small cascade offset from it), so a
    /// pop-up appears next to the editor you're actually using.
    /// </summary>
    public static class WindowPlacement
    {
        public static void ShowManaged(this Window w)
        {
            // Every editor window opens through here, whether from a menu, the command palette, or a
            // button inside another editor, so this is the one place a beta editor has to be stopped.
            // A hosted editor is known by its view, since every host window shares one class.
            string editorName = w is EditorHostWindow { Content: Control hosted } ? hosted.GetType().Name : w?.GetType().Name;
            if (w != null && !BetaEditors.Allows(editorName))
            {
                string why = BetaEditors.WhyNot(editorName);
                AppLogger.Info("Beta editor not opened: " + editorName);
                _ = DialogHelper.ShowInfo(why, "Not available yet");
                return;
            }

            try
            {
                var active = ActiveWindow();
                if (active != null && !ReferenceEquals(active, w) && active.WindowState != WindowState.Minimized)
                {
                    // Anchor the pop-up on the active window's screen. Prefer centering on that screen; fall back to a
                    // small cascade offset from the active window (which is always on the right monitor) if the screen
                    // metrics aren't available yet.
                    w.WindowStartupLocation = WindowStartupLocation.Manual;
                    var p = active.Position;
                    w.Position = new PixelPoint(p.X + 48, p.Y + 48);
                }
            }
            catch { /* positioning is best-effort, never block opening the window */ }
            w.Show();
        }

        private static Window ActiveWindow()
        => OwnerWindow.Current;
    }
}
