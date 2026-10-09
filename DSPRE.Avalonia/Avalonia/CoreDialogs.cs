using System;
using System.Collections.Generic;
using System.Linq;
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
            // Unpacking runs on worker threads, so the question is handed to the UI thread and waited on.
            DSPRE.NarcSync.AskHook = (conflicts, saving) => Dispatcher.UIThread.CheckAccess()
                ? AskArchiveDrift(conflicts, saving)
                : Dispatcher.UIThread.Invoke(() => AskArchiveDrift(conflicts, saving));
            // PumpEventsHook stays the default no-op: core long-ops run off the UI thread under Avalonia.

            // Placeholder mon icon for undecodable icons, from the avares assets, no GDI.
            DSPRE.DSUtils.MonIconFallbackHook = () => ResourceImages.GetRaw("IconPokeball");
        }

        private static string Coalesce(string s, string fallback) => string.IsNullOrEmpty(s) ? fallback : s;

        private static Window ActiveOwner()
        => OwnerWindow.Current;

        // Convert a WinForms-style filter ("Gen IV Script File (*.scr)|*.scr") into an Avalonia file type.
        private static FilePickerFileType ToFileType(string filter)
        {
            string name = "File";
            List<string> patterns = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(filter))
            {
                string[] parts = filter.Split('|');
                if (parts.Length >= 1) name = parts[0];
                if (parts.Length >= 2)
                    foreach (string p in parts[1].Split(';'))
                        if (!string.IsNullOrWhiteSpace(p)) patterns.Add(p.Trim());
            }
            if (patterns.Count == 0) patterns.Add("*.*");
            return new FilePickerFileType(name) { Patterns = patterns };
        }

        private static string PickSaveFileSync(string title, string filter, string suggestedName)
        {
            Window owner = ActiveOwner();
            if (owner == null) return null;

            TaskScheduler uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
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
            Window owner = ActiveOwner();
            if (owner == null) return null;

            TaskScheduler uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
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
                    IReadOnlyList<IStorageFolder> folders = t.Result;
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

            Window win = new Window
            {
                Title = string.IsNullOrEmpty(title) ? "Confirm" : title,
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
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                Margin = new global::Avalonia.Thickness(16, 16, 16, 12),
            };

            StackPanel btnRow = new StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
                Margin = new global::Avalonia.Thickness(8, 0, 12, 12),
                Spacing = 6,
            };
            void AddBtn(string label, bool r, bool isDefault = false)
            {
                Button btn = new Button { Content = label, MinWidth = 80, IsDefault = isDefault };
                btn.Click += (_, _) => { result = r; win.Close(); };
                btnRow.Children.Add(btn);
            }
            AddBtn("Yes", true, isDefault: true);
            AddBtn("No", false);

            win.Closed += (_, _) => closed = true;
            StackPanel root = new StackPanel();
            root.Children.Add(msgText);
            root.Children.Add(btnRow);
            win.Content = root;

            Window owner = ActiveOwner();
            if (owner != null) _ = win.ShowDialog(owner);
            else win.Show();

            PumpUntil(() => closed);
            return result;
        }

        /// <summary>
        /// One row per archive whose unpacked folder no longer matches it: the user says which side is newer. While
        /// Save ROM waits every row needs an answer, since it packs each folder over its archive next.
        /// </summary>
        private static IReadOnlyList<DSPRE.NarcSync.Choice> AskArchiveDrift(IReadOnlyList<DSPRE.NarcSync.Conflict> conflicts, bool saving)
        {
            List<DSPRE.NarcSync.Choice> picked = null;
            bool closed = false;

            bool firstCheck = conflicts.Any(c => c.Drift == DSPRE.NarcSync.Drift.Differs);
            string intro = firstCheck
                ? "DSPRE now checks each archive against its unpacked folder, and these differ. Editors and Save ROM use "
                  + "the unpacked folder, so an archive changed with another tool would be overwritten. Which side is newer?"
                : "These archives were changed outside DSPRE since it last unpacked them. Editors and Save ROM use the "
                  + "unpacked folder, so keeping it overwrites the outside change. Which side is newer?";
            if (saving)
                intro += "\n\nSaving continues once each one is answered. A newer archive is unpacked first, and you "
                         + "can look it over before anything is written.";
            string[] options = saving
                ? new[] { "Unpacked folder is newer", "Archive is newer" }
                : new[] { "Unpacked folder is newer", "Archive is newer", "Ask me later" };

            Window win = new Window
            {
                Title = "Archive changed",
                Width = 620,
                MinHeight = 200,
                MaxHeight = 640,
                CanResize = false,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
            };

            StackPanel rows = new StackPanel { Spacing = 6, Margin = new global::Avalonia.Thickness(16, 0, 16, 8) };
            List<ComboBox> boxes = new List<ComboBox>();
            foreach (DSPRE.NarcSync.Conflict c in conflicts)
            {
                ComboBox box = new ComboBox
                {
                    ItemsSource = options,
                    // Nothing is picked for the user while Save ROM waits; the guess could cost their work.
                    SelectedIndex = saving ? -1 : 2,
                    PlaceholderText = "Which is newer?",
                    Width = 220,
                };
                boxes.Add(box);
                Grid row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                TextBlock name = new TextBlock
                {
                    Text = $"{c.Label} ({c.Name})",
                    VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                    TextTrimming = global::Avalonia.Media.TextTrimming.PrefixCharacterEllipsis,
                };
                ToolTip.SetTip(name, c.UnpackedPath);
                Grid.SetColumn(box, 1);
                row.Children.Add(name);
                row.Children.Add(box);
                rows.Children.Add(row);
            }

            StackPanel btnRow = new StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
                Margin = new global::Avalonia.Thickness(8, 4, 12, 12),
                Spacing = 6,
            };
            Button apply = new Button { Content = saving ? "Continue" : "Apply", MinWidth = 80, IsDefault = true };
            void Ready() => apply.IsEnabled = boxes.All(b => b.SelectedIndex >= 0);
            foreach (ComboBox b in boxes) b.SelectionChanged += (_, _) => Ready();
            Ready();
            apply.Click += (_, _) =>
            {
                picked = boxes.Select(b => b.SelectedIndex switch
                {
                    0 => DSPRE.NarcSync.Choice.KeepFolder,
                    1 => DSPRE.NarcSync.Choice.UseArchive,
                    _ => DSPRE.NarcSync.Choice.Later,
                }).ToList();
                win.Close();
            };
            Button later = new Button { Content = saving ? "Cancel" : "Later", MinWidth = 80, IsCancel = true };
            later.Click += (_, _) => win.Close();
            btnRow.Children.Add(apply);
            btnRow.Children.Add(later);

            StackPanel root = new StackPanel();
            root.Children.Add(new TextBlock
            {
                Text = intro,
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                Margin = new global::Avalonia.Thickness(16, 16, 16, 10),
            });
            root.Children.Add(new ScrollViewer { Content = rows, MaxHeight = 360 });
            root.Children.Add(new TextBlock
            {
                Text = "The side that is replaced is kept in the project's backups/archives folder.",
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                Opacity = 0.7,
                Margin = new global::Avalonia.Thickness(16, 4, 16, 0),
            });
            root.Children.Add(btnRow);
            win.Content = root;
            win.Closed += (_, _) => closed = true;

            Window owner = ActiveOwner();
            if (owner != null) _ = win.ShowDialog(owner);
            else win.Show();

            PumpUntil(() => closed);
            return picked;
        }

        private static DSPRE.AppMessages.ConfirmResult ShowConfirmCancelSync(string message, string title)
        {
            AppMessages.ConfirmResult result = DSPRE.AppMessages.ConfirmResult.Cancel;
            bool closed = false;

            Window win = new Window
            {
                Title = string.IsNullOrEmpty(title) ? "Confirm" : title,
                Width = 480,
                MinHeight = 160,
                CanResize = false,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
            };

            TextBlock msgText = new TextBlock
            {
                Text = message,
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                Margin = new global::Avalonia.Thickness(16, 16, 16, 12),
            };

            StackPanel btnRow = new StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
                Margin = new global::Avalonia.Thickness(8, 0, 12, 12),
                Spacing = 6,
            };
            void AddBtn(string label, DSPRE.AppMessages.ConfirmResult r, bool isDefault = false)
            {
                Button btn = new Button { Content = label, MinWidth = 80, IsDefault = isDefault };
                btn.Click += (_, _) => { result = r; win.Close(); };
                btnRow.Children.Add(btn);
            }
            AddBtn("Yes", DSPRE.AppMessages.ConfirmResult.Yes, isDefault: true);
            AddBtn("No", DSPRE.AppMessages.ConfirmResult.No);
            AddBtn("Cancel", DSPRE.AppMessages.ConfirmResult.Cancel);

            win.Closed += (_, _) => closed = true;
            StackPanel root = new StackPanel();
            root.Children.Add(msgText);
            root.Children.Add(btnRow);
            win.Content = root;

            Window owner = ActiveOwner();
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
            DispatcherFrame frame = new DispatcherFrame();
            DispatcherTimer timer = new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.Background,
                (_, _) => { if (isDone()) frame.Continue = false; });
            timer.Start();
            try { Dispatcher.UIThread.PushFrame(frame); }
            finally { timer.Stop(); }
        }
    }
}
