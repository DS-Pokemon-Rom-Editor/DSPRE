using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Installs native-Avalonia implementations of the Patch Toolbox prompt hooks
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
            DSPRE.PatchToolboxLogic.PickCustomCommandFile = PickScrcmdFile;
        }

        private static Window ActiveOwner(Window exclude = null)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
            {
                foreach (var w in d.Windows)
                    if (w.IsActive && !ReferenceEquals(w, exclude)) return w;
                if (d.MainWindow != null && !ReferenceEquals(d.MainWindow, exclude)) return d.MainWindow;
            }
            return null;
        }

        // Block until the predicate is true. On the UI thread this runs a nested Avalonia dispatcher
        // frame (cross-platform — pumps the native event loop, no WinForms). On a worker thread it
        // just poll-sleeps while the dialog runs on the UI thread.
        private static void PumpUntil(Func<bool> isDone)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                while (!isDone()) Thread.Sleep(10);
                return;
            }
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.Background,
                (_, _) => { if (isDone()) frame.Continue = false; });
            timer.Start();
            try { Dispatcher.UIThread.PushFrame(frame); }
            finally { timer.Stop(); }
        }

        private static int ShowSync(string message, string title, bool yesNo, bool error = false)
        {
            int result = 0;   // 0 = No / closed, 1 = Yes / OK
            bool closed = false;

            var win = new Window
            {
                Title = title,
                Width = 460,
                MinHeight = 150,
                CanResize = false,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
            };

            var msgText = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(16, 16, 16, 12),
                Foreground = error ? new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)) : null,
            };

            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(8, 0, 12, 12),
                Spacing = 6,
            };

            void AddBtn(string label, int r, bool isDefault = false)
            {
                var btn = new Button { Content = label, MinWidth = 80, IsDefault = isDefault };
                btn.Click += (_, _) => { result = r; win.Close(); };
                btnRow.Children.Add(btn);
            }

            if (yesNo) { AddBtn("Yes", 1, isDefault: true); AddBtn("No", 0); }
            else { AddBtn("OK", 1, isDefault: true); }

            win.Closed += (_, _) => closed = true;

            var root = new StackPanel();
            root.Children.Add(msgText);
            root.Children.Add(btnRow);
            win.Content = root;

            var owner = ActiveOwner(win);
            if (owner != null)
                _ = win.ShowDialog(owner);   // modal; disables owner. We drive the pump ourselves below.
            else
                win.Show();

            PumpUntil(() => closed);
            return result;
        }

        private static string PickScrcmdFile()
        {
            var owner = ActiveOwner();
            if (owner == null) return null;

            // Continue on the UI thread so writes are safe to read after the pump loop sees 'done'.
            var uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            string path = null;
            bool done = false;

            owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select custom script command file",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Custom Script Command File") { Patterns = new[] { "*.scrcmd" } }
                }
            }).ContinueWith(t =>
            {
                try
                {
                    var files = t.Result;
                    path = files != null && files.Count > 0 ? files[0].TryGetLocalPath() : null;
                }
                catch { path = null; }
                finally { done = true; }
            }, uiScheduler);

            PumpUntil(() => done);
            return path;
        }
    }
}
