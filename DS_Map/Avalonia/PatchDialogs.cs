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
            DSPRE.PatchToolboxLogic.PickSyntheticOverlayOffset = PickSyntheticOverlayOffsetSync;
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
        // frame (cross-platform, pumps the native event loop, no WinForms). On a worker thread it
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
            };
            // Only override the foreground for errors: leaving it unset for Info/Confirm lets the
            // theme's implicit TextBlock style apply; explicitly assigning null here (even via a
            // ternary) used to win over the theme at Local priority and rendered the text invisible.
            if (error) msgText.Foreground = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));

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

        // Same synchronous-pump approach as ShowSync, with a hex-offset TextBox plus live range/status
        // feedback, mirroring the WinForms SyntheticOverlayOffsetDialog so both shells behave identically.
        private static uint? PickSyntheticOverlayOffsetSync(string patchName, string filePath, uint defaultOffset, byte[] expectedBytes, uint loadAddress)
        {
            uint? result = null;
            bool closed = false;
            bool rangeOccupied = false;
            uint parsedOffset = defaultOffset;

            var win = new Window
            {
                Title = "Choose synthetic overlay offset",
                Width = 460,
                MinHeight = 220,
                CanResize = false,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
            };

            var messageText = new TextBlock
            {
                Text = patchName + " will be written to the synthetic overlay. Enter the file offset to use.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(16, 16, 16, 8),
            };

            var offsetBox = new TextBox { Text = defaultOffset.ToString("X"), Margin = new Thickness(16, 0, 16, 8) };
            var rangeText = new TextBlock { Margin = new Thickness(16, 0, 16, 4) };
            var runtimeText = new TextBlock { Margin = new Thickness(16, 0, 16, 4) };
            var statusText = new TextBlock { Margin = new Thickness(16, 0, 16, 8), TextWrapping = TextWrapping.Wrap };

            var okBtn = new Button { Content = "OK", MinWidth = 80, IsDefault = true, IsEnabled = false };
            var cancelBtn = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(8, 0, 12, 12),
                Spacing = 6,
            };
            btnRow.Children.Add(cancelBtn);
            btnRow.Children.Add(okBtn);

            var errorBrush = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
            var okBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));

            void Evaluate()
            {
                okBtn.IsEnabled = false;
                rangeOccupied = false;

                string value = (offsetBox.Text ?? "").Trim();
                if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) value = value.Substring(2);

                if (!uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint offset))
                {
                    rangeText.Text = "";
                    runtimeText.Text = "";
                    statusText.Text = "Enter a valid hexadecimal offset.";
                    statusText.Foreground = errorBrush;
                    return;
                }

                parsedOffset = offset;
                uint endOffset = offset + (uint)expectedBytes.Length - 1;
                rangeText.Text = "File range: 0x" + offset.ToString("X") + " - 0x" + endOffset.ToString("X");
                runtimeText.Text = "Runtime address: 0x" + (loadAddress + offset).ToString("X8");

                if (!File.Exists(filePath))
                {
                    statusText.Text = "Synthetic overlay file was not found.";
                    statusText.Foreground = errorBrush;
                    return;
                }

                long fileLength = new FileInfo(filePath).Length;
                if (offset >= fileLength || (long)offset + expectedBytes.Length > fileLength)
                {
                    statusText.Text = "Selected range is outside the synthetic overlay file.";
                    statusText.Foreground = errorBrush;
                    return;
                }

                if (offset % 4 != 0)
                {
                    statusText.Text = "Offset must be 4-byte aligned.";
                    statusText.Foreground = errorBrush;
                    return;
                }

                byte[] currentBytes = DSUtils.ReadFromFile(filePath, offset, expectedBytes.Length);
                if (currentBytes.Length != expectedBytes.Length)
                {
                    statusText.Text = "Could not read the selected range.";
                    statusText.Foreground = errorBrush;
                    return;
                }

                if (currentBytes.All(b => b == 0))
                {
                    statusText.Text = "This range is empty.";
                    statusText.Foreground = okBrush;
                    okBtn.IsEnabled = true;
                    return;
                }

                rangeOccupied = true;
                statusText.Text = "This range already contains data. Continuing will overwrite it.";
                statusText.Foreground = errorBrush;
                okBtn.IsEnabled = true;
            }

            offsetBox.TextChanged += (_, _) => Evaluate();
            okBtn.Click += (_, _) =>
            {
                if (rangeOccupied && ShowSync(
                    "The selected synthetic overlay range already contains data.\n\n" +
                    "Overwriting it can break another patch or custom code.\n\n" +
                    "Do you want to overwrite this range anyway?",
                    "Overwrite occupied synthetic overlay range?", yesNo: true) != 1)
                {
                    return;
                }
                result = parsedOffset;
                win.Close();
            };
            cancelBtn.Click += (_, _) => { result = null; win.Close(); };
            win.Closed += (_, _) => closed = true;

            var root = new StackPanel();
            root.Children.Add(messageText);
            root.Children.Add(offsetBox);
            root.Children.Add(rangeText);
            root.Children.Add(runtimeText);
            root.Children.Add(statusText);
            root.Children.Add(btnRow);
            win.Content = root;

            Evaluate();

            var owner = ActiveOwner(win);
            if (owner != null)
                _ = win.ShowDialog(owner);
            else
                win.Show();

            PumpUntil(() => closed);
            return result;
        }
    }
}
