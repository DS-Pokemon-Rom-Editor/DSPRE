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
            w.Opened += FitHeightOnOpen;
            w.Show();
        }

        /// <summary>No window insists on more height than this, so every editor still fits a small screen.</summary>
        public const double MinimumHeightCap = 800;

        // An editor whose own page scrolls at its opening size grows to show it all, as far as the screen
        // allows, and keeps that as its minimum up to the cap. Lists scrolling inside their own box don't count.
        private static void FitHeightOnOpen(object sender, EventArgs e)
        {
            if (sender is not Window w) return;
            w.Opened -= FitHeightOnOpen;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var screen = w.Screens?.ScreenFromWindow(w) ?? w.Screens?.Primary;
                    double scale = screen?.Scaling ?? 1;
                    // Room for the title bar and frame, which ClientSize leaves out.
                    double room = screen != null ? screen.WorkingArea.Height / scale - 40 : double.PositiveInfinity;
                    double cap = Math.Min(MinimumHeightCap, room);
                    if (w.MinHeight > cap) w.MinHeight = cap;

                    double overflow = 0;
                    foreach (var sv in global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(w))
                    {
                        if (sv is not ScrollViewer s || s.TemplatedParent != null || !s.IsEffectivelyVisible) continue;
                        overflow = Math.Max(overflow, s.Extent.Height - s.Viewport.Height);
                    }
                    if (overflow < 1 || w.WindowState != WindowState.Normal) return;

                    double height = Math.Min(w.ClientSize.Height + Math.Ceiling(overflow), room);
                    if (height <= w.ClientSize.Height) return;
                    w.Height = height;
                    w.MinHeight = Math.Max(w.MinHeight, Math.Min(height, cap));

                    if (screen != null)
                    {
                        int bottom = screen.WorkingArea.Bottom - (int)Math.Ceiling((height + 40) * scale);
                        if (w.Position.Y > bottom)
                            w.Position = new PixelPoint(w.Position.X, Math.Max(screen.WorkingArea.Y, bottom));
                    }
                }
                catch (Exception ex) { AppLogger.Warn("Window height fit: " + ex.Message); }
            }, global::Avalonia.Threading.DispatcherPriority.Background);
        }

        private static Window ActiveWindow()
        => OwnerWindow.Current;
    }
}
