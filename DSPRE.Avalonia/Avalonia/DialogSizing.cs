using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace DSPRE.Avalonia
{
    /// <summary>Lets a dialog be resized from the size it opens at, never smaller.</summary>
    public static class DialogSizing
    {
        // A list keeps its height cap until the user drags an edge, then fills the window.
        public static void Grow(Window win, params Control[] uncap)
        {
            win.CanResize = true;
            win.Opened += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                win.MinWidth = Math.Max(win.MinWidth, win.ClientSize.Width);
                win.MinHeight = Math.Max(win.MinHeight, win.ClientSize.Height);
            }, DispatcherPriority.Loaded);
            win.Resized += (_, e) =>
            {
                if (e.Reason != WindowResizeReason.User) return;
                foreach (Control c in uncap)
                    if (c != null) c.MaxHeight = double.PositiveInfinity;
            };
        }

        // The middle control gets the room left between the top and the buttons.
        public static DockPanel Body(Control fill, Control bottom, params Control[] top)
        {
            DockPanel root = new DockPanel();
            foreach (Control c in top)
            {
                if (c == null) continue;
                DockPanel.SetDock(c, Dock.Top);
                root.Children.Add(c);
            }
            if (bottom != null)
            {
                DockPanel.SetDock(bottom, Dock.Bottom);
                root.Children.Add(bottom);
            }
            if (fill != null) root.Children.Add(fill);
            return root;
        }
    }
}
