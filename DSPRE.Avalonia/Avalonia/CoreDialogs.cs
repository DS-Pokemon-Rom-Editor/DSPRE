using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Installs native-Avalonia implementations of the core <see cref="DSPRE.AppMessages"/> hooks, so
    /// the WinForms-free ROM core (ROMFiles/DSUtils) shows Avalonia dialogs when running under the
    /// Avalonia shell instead of WinForms MessageBoxes.
    /// </summary>
    public static class CoreDialogs
    {
        public static void Install()
        {
            // Message boxes are fire-and-forget: marshal to the UI thread and don't block the caller.
            DSPRE.AppMessages.ErrorHook = (msg, title) =>
                Dispatcher.UIThread.Post(() => _ = DialogHelper.ShowError(msg, Coalesce(title, "Error")));
            DSPRE.AppMessages.InfoHook = (msg, title) =>
                Dispatcher.UIThread.Post(() => _ = DialogHelper.ShowInfo(msg, Coalesce(title, "Information")));
            DSPRE.AppMessages.WarningHook = (msg, title) =>
                Dispatcher.UIThread.Post(() => _ = DialogHelper.ShowError(msg, Coalesce(title, "Warning")));

            // Save picker must return synchronously (the core export APIs are sync); pump a nested
            // Avalonia dispatcher frame while the async picker runs.
            DSPRE.AppMessages.SaveFileHook = PickSaveFileSync;
            DSPRE.AppMessages.PickFolderHook = PickFolderSync;
            DSPRE.AppMessages.ConfirmHook = ShowConfirmSync;
            DSPRE.AppMessages.ConfirmYesNoCancelHook = ShowConfirmCancelSync;
            // PumpEventsHook stays the default no-op: core long-ops run off the UI thread under Avalonia.

            // Placeholder mon icon for undecodable icons, from the avares assets, no GDI.
            DSPRE.DSUtils.MonIconFallbackHook = () => ResourceImages.GetRaw("IconPokeball");
        }

        private static string Coalesce(string s, string fallback) => string.IsNullOrEmpty(s) ? fallback : s;

        private static Window ActiveOwner()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
            {
                foreach (var w in d.Windows)
                    if (w.IsActive) return w;
                return d.MainWindow;
            }
            return null;
        }

        // Convert a WinForms-style filter ("Gen IV Script File (*.scr)|*.scr") into an Avalonia file type.
        private static FilePickerFileType ToFileType(string filter)
        {
            string name = "File";
            var patterns = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(filter))
            {
                var parts = filter.Split('|');
                if (parts.Length >= 1) name = parts[0];
                if (parts.Length >= 2)
                    foreach (var p in parts[1].Split(';'))
                        if (!string.IsNullOrWhiteSpace(p)) patterns.Add(p.Trim());
            }
            if (patterns.Count == 0) patterns.Add("*.*");
            return new FilePickerFileType(name) { Patterns = patterns };
        }

        private static string PickSaveFileSync(string title, string filter, string suggestedName)
        {
            var owner = ActiveOwner();
            if (owner == null) return null;

            var uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            string path = null;
            bool done = false;

            owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title ?? "Save file",
                SuggestedFileName = suggestedName,
                FileTypeChoices = new[] { ToFileType(filter) }
            }).ContinueWith(t =>
            {
                try { path = t.Result?.TryGetLocalPath(); }
                catch { path = null; }
                finally { done = true; }
            }, uiScheduler);

            PumpUntil(() => done);
            return path;
        }

        private static string PickFolderSync(string title)
        {
            var owner = ActiveOwner();
            if (owner == null) return null;

            var uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            string path = null;
            bool done = false;

            owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title ?? "Select folder",
                AllowMultiple = false
            }).ContinueWith(t =>
            {
                try
                {
                    var folders = t.Result;
                    path = folders != null && folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
                }
                catch { path = null; }
                finally { done = true; }
            }, uiScheduler);

            PumpUntil(() => done);
            return path;
        }

        private static bool ShowConfirmSync(string message, string title)
        {
            bool result = false;
            bool closed = false;

            var win = new Window
            {
                Title = string.IsNullOrEmpty(title) ? "Confirm" : title,
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
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                Margin = new global::Avalonia.Thickness(16, 16, 16, 12),
            };

            var btnRow = new StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
                Margin = new global::Avalonia.Thickness(8, 0, 12, 12),
                Spacing = 6,
            };
            void AddBtn(string label, bool r, bool isDefault = false)
            {
                var btn = new Button { Content = label, MinWidth = 80, IsDefault = isDefault };
                btn.Click += (_, _) => { result = r; win.Close(); };
                btnRow.Children.Add(btn);
            }
            AddBtn("Yes", true, isDefault: true);
            AddBtn("No", false);

            win.Closed += (_, _) => closed = true;
            var root = new StackPanel();
            root.Children.Add(msgText);
            root.Children.Add(btnRow);
            win.Content = root;

            var owner = ActiveOwner();
            if (owner != null) _ = win.ShowDialog(owner);
            else win.Show();

            PumpUntil(() => closed);
            return result;
        }

        private static DSPRE.AppMessages.ConfirmResult ShowConfirmCancelSync(string message, string title)
        {
            var result = DSPRE.AppMessages.ConfirmResult.Cancel;
            bool closed = false;

            var win = new Window
            {
                Title = string.IsNullOrEmpty(title) ? "Confirm" : title,
                Width = 480,
                MinHeight = 160,
                CanResize = false,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
            };

            var msgText = new TextBlock
            {
                Text = message,
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                Margin = new global::Avalonia.Thickness(16, 16, 16, 12),
            };

            var btnRow = new StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
                Margin = new global::Avalonia.Thickness(8, 0, 12, 12),
                Spacing = 6,
            };
            void AddBtn(string label, DSPRE.AppMessages.ConfirmResult r, bool isDefault = false)
            {
                var btn = new Button { Content = label, MinWidth = 80, IsDefault = isDefault };
                btn.Click += (_, _) => { result = r; win.Close(); };
                btnRow.Children.Add(btn);
            }
            AddBtn("Yes", DSPRE.AppMessages.ConfirmResult.Yes, isDefault: true);
            AddBtn("No", DSPRE.AppMessages.ConfirmResult.No);
            AddBtn("Cancel", DSPRE.AppMessages.ConfirmResult.Cancel);

            win.Closed += (_, _) => closed = true;
            var root = new StackPanel();
            root.Children.Add(msgText);
            root.Children.Add(btnRow);
            win.Content = root;

            var owner = ActiveOwner();
            if (owner != null) _ = win.ShowDialog(owner);
            else win.Show();

            PumpUntil(() => closed);
            return result;
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
    }
}
