using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Installs native-Avalonia implementations of the ROM patch toolbox prompt hooks
    /// (<see cref="DSPRE.PatchToolboxLogic.ConfirmYesNo"/> etc.), so the native toolbox shows
    /// Avalonia dialogs instead of WinForms MessageBoxes while the ROM-writing logic stays shared.
    ///
    /// The patch apply-methods are synchronous, so these dialogs block synchronously: the window is
    /// shown modally and a nested Avalonia dispatcher frame is pumped until the user answers, which
    /// keeps the window fully responsive without freezing the app (cross-platform, no WinForms).
    /// </summary>
    public static class PatchDialogs
    {
        public static void Install()
        {
            DSPRE.PatchToolboxLogic.ConfirmYesNo = (msg, title) => ShowSync(msg, title, yesNo: true) == 1;
            DSPRE.PatchToolboxLogic.ShowInfo = (msg, title) => ShowSync(msg, title, yesNo: false);
            DSPRE.PatchToolboxLogic.ShowError = (msg, title) => ShowSync(msg, title, yesNo: false, error: true);
        }

        private static Window ActiveOwner(Window exclude = null)
        {
            Window current = OwnerWindow.Current;
            if (current != null && !ReferenceEquals(current, exclude)) return current;
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
            {
                foreach (Window w in d.Windows)
                    if (w.IsActive && !ReferenceEquals(w, exclude)) return w;
                if (d.MainWindow != null && !ReferenceEquals(d.MainWindow, exclude)) return d.MainWindow;
            }
            return null;
        }

        // Block until the predicate is true. On the UI thread this runs a nested Avalonia dispatcher
        // frame (cross-platform, pumps the native event loop, no WinForms). On a worker thread it
        // just poll-sleeps while the dialog runs on the UI thread.
        private static void PumpUntil(Func<bool> isDone)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                while (!isDone()) Thread.Sleep(10);
                return;
            }
            DispatcherFrame frame = new DispatcherFrame();
            DispatcherTimer timer = new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.Background,
                (_, _) => { if (isDone()) frame.Continue = false; });
            timer.Start();
            try { Dispatcher.UIThread.PushFrame(frame); }
            finally { timer.Stop(); }
        }

        private static int ShowSync(string message, string title, bool yesNo, bool error = false)
        {
            int result = 0;   // 0 = No / closed, 1 = Yes / OK
            bool closed = false;

            Window win = new Window
            {
                Title = title,
                Width = 460,
                MinHeight = 150,
                CanResize = false,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
            };

            TextBlock msgText = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(16, 16, 16, 12),
            };
            // Only override the foreground for errors: leaving it unset for Info/Confirm lets the
            // theme's implicit TextBlock style apply; explicitly assigning null here (even via a
            // ternary) used to win over the theme at Local priority and rendered the text invisible.
            if (error) msgText.Foreground = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));

            StackPanel btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(8, 0, 12, 12),
                Spacing = 6,
            };

            void AddBtn(string label, int r, bool isDefault = false)
            {
                Button btn = new Button { Content = label, MinWidth = 80, IsDefault = isDefault };
                btn.Click += (_, _) => { result = r; win.Close(); };
                btnRow.Children.Add(btn);
            }

            if (yesNo) { AddBtn("Yes", 1, isDefault: true); AddBtn("No", 0); }
            else { AddBtn("OK", 1, isDefault: true); }

            win.Closed += (_, _) => closed = true;

            StackPanel root = new StackPanel();
            root.Children.Add(msgText);
            root.Children.Add(btnRow);
            win.Content = root;

            Window owner = ActiveOwner(win);
            if (owner != null)
                _ = win.ShowDialog(owner);   // modal; disables owner. We drive the pump ourselves below.
            else
                win.Show();

            PumpUntil(() => closed);
            return result;
        }
    }
}
