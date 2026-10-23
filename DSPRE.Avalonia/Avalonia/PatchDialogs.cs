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
    /// The patch toolbox's prompts as modal Avalonia dialogs. The apply methods are synchronous, so each prompt waits
    /// on a dispatcher frame.
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

        // Waits on a nested dispatcher frame on the UI thread, or polls on a worker thread.
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
            // Set the foreground only for errors; setting it at all, even to null, hides the theme's colour.
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
