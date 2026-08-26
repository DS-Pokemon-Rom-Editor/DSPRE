using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Wraps Avalonia 12 async message-box and file-dialog APIs behind a simple static interface.
    /// Use this everywhere instead of System.Windows.Forms.MessageBox or OpenFileDialog/SaveFileDialog.
    /// </summary>
    public static class DialogHelper
    {
        // ----------------------------------------------------------------
        // Message box result enum (replaces WinForms DialogResult)
        // ----------------------------------------------------------------

        public enum MsgResult { Ok, Yes, No, Cancel }

        // ----------------------------------------------------------------
        // Message Boxes (built from plain Avalonia Window, no 3rd-party dep)
        // ----------------------------------------------------------------

        public static Task ShowInfo(string message, string title = "Information")
            => ShowMsg(message, title, MsgButtons.Ok);

        public static Task ShowError(string message, string title = "Error", Window owner = null)
            => ShowMsg(message, title, MsgButtons.Ok, owner: owner);

        /// <summary>
        /// Error box with a "Copy details" button: <paramref name="details"/> (e.g. the full
        /// exception text) goes to the clipboard so users can paste it into a bug report.
        /// </summary>
        public static Task ShowError(string message, string title, string details)
            => ShowMsg(message, title, MsgButtons.Ok, details);

        /// <returns>true = Yes, false = No</returns>
        public static async Task<bool> AskYesNo(string message, string title = "Confirm", Window owner = null)
        {
            var result = await ShowMsg(message, title, MsgButtons.YesNo, owner: owner);
            return result == MsgResult.Yes;
        }

        /// <returns>MsgResult.Yes / No / Cancel</returns>
        public static Task<MsgResult> AskYesNoCancel(string message, string title = "Confirm")
            => ShowMsg(message, title, MsgButtons.YesNoCancel);

        /// <summary>Prompts for a single line of free text. Returns null if cancelled or closed without
        /// confirming; an empty string is a valid (non-null) confirmed answer.</summary>
        public static async Task<string> PromptText(string message, string title = "Enter a value", string defaultValue = "", Window owner = null)
        {
            var tcs = new TaskCompletionSource<string>();

            var win = new Window
            {
                Title = title,
                Width = 420,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.Height,
            };

            var msgText = new TextBlock
            {
                Text = message,
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                Margin = new global::Avalonia.Thickness(16, 16, 16, 8),
            };

            var input = new TextBox
            {
                Text = defaultValue,
                Margin = new global::Avalonia.Thickness(16, 0, 16, 12),
            };

            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new global::Avalonia.Thickness(8, 0, 8, 12),
                Spacing = 6,
            };

            var okBtn = new Button { Content = "OK", MinWidth = 72, IsDefault = true };
            okBtn.Click += (_, _) => { tcs.TrySetResult(input.Text ?? ""); win.Close(); };
            var cancelBtn = new Button { Content = "Cancel", MinWidth = 72, IsCancel = true };
            cancelBtn.Click += (_, _) => { tcs.TrySetResult(null); win.Close(); };
            btnRow.Children.Add(okBtn);
            btnRow.Children.Add(cancelBtn);

            win.Closed += (_, _) => tcs.TrySetResult(null);

            var root = new StackPanel();
            root.Children.Add(msgText);
            root.Children.Add(input);
            root.Children.Add(btnRow);
            win.Content = root;

            if (owner == null)
            {
                var app = global::Avalonia.Application.Current?.ApplicationLifetime
                    as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
                owner = app?.MainWindow;
            }

            input.AttachedToVisualTree += (_, _) => input.Focus();

            if (owner != null)
                await win.ShowDialog(owner);
            else
                win.Show();

            string result = await tcs.Task;
            return result?.Trim();
        }

        // ----------------------------------------------------------------
        // File Dialogs  (requires the owning Window)
        // ----------------------------------------------------------------

        /// <summary>Opens a file-open dialog. Returns null if cancelled.</summary>
        public static async Task<string> OpenFile(
            Window owner,
            string title,
            IReadOnlyList<FilePickerFileType> filters = null)
        {
            var opts = new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = filters
            };

            var files = await owner.StorageProvider.OpenFilePickerAsync(opts);
            return files?.Count > 0 ? files[0].TryGetLocalPath() : null;
        }

        /// <summary>Opens a folder-picker dialog. Returns null if cancelled.</summary>
        public static async Task<string> OpenFolder(Window owner, string title)
        {
            var opts = new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            };

            var folders = await owner.StorageProvider.OpenFolderPickerAsync(opts);
            return folders?.Count > 0 ? folders[0].TryGetLocalPath() : null;
        }

        /// <summary>Opens a file-save dialog. Returns null if cancelled.</summary>
        public static async Task<string> SaveFile(
            Window owner,
            string title,
            IReadOnlyList<FilePickerFileType> filters = null,
            string suggestedFileName = null)
        {
            var opts = new FilePickerSaveOptions
            {
                Title = title,
                FileTypeChoices = filters,
                SuggestedFileName = suggestedFileName
            };

            var file = await owner.StorageProvider.SaveFilePickerAsync(opts);
            return file?.TryGetLocalPath();
        }

        // ----------------------------------------------------------------
        // Common FilePickerFileType presets
        // ----------------------------------------------------------------

        public static readonly FilePickerFileType CsvFilter =
            new FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } };

        public static readonly FilePickerFileType PngFilter =
            new FilePickerFileType("PNG Images") { Patterns = new[] { "*.png" } };

        public static readonly FilePickerFileType AllFilter =
            new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } };

        public static readonly FilePickerFileType ZipFilter =
            new FilePickerFileType("ZIP Archive") { Patterns = new[] { "*.zip" } };

        // ----------------------------------------------------------------
        // Internal: lightweight dialog window built in code (no AXAML)
        // ----------------------------------------------------------------

        private enum MsgButtons { Ok, YesNo, YesNoCancel }

        private static async Task<MsgResult> ShowMsg(string message, string title, MsgButtons buttons, string details = null, Window owner = null)
        {
            var tcs = new TaskCompletionSource<MsgResult>();

            var win = new Window
            {
                Title = title,
                Width = 420,
                MinHeight = 140,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.Height,
            };

            var msgText = new TextBlock
            {
                Text = message,
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                Margin = new global::Avalonia.Thickness(16, 16, 16, 12),
            };

            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new global::Avalonia.Thickness(8, 0, 8, 12),
                Spacing = 6,
            };

            void AddBtn(string label, MsgResult result)
            {
                var btn = new Button { Content = label, MinWidth = 72 };
                btn.Click += (_, _) => { tcs.TrySetResult(result); win.Close(); };
                btnRow.Children.Add(btn);
            }

            switch (buttons)
            {
                case MsgButtons.Ok:
                    AddBtn("OK", MsgResult.Ok);
                    break;
                case MsgButtons.YesNo:
                    AddBtn("Yes", MsgResult.Yes);
                    AddBtn("No", MsgResult.No);
                    break;
                case MsgButtons.YesNoCancel:
                    AddBtn("Yes", MsgResult.Yes);
                    AddBtn("No", MsgResult.No);
                    AddBtn("Cancel", MsgResult.Cancel);
                    break;
            }

            win.Closed += (_, _) => tcs.TrySetResult(MsgResult.Cancel);

            var root = new StackPanel();
            root.Children.Add(new ScrollViewer { Content = msgText, MaxHeight = 380 });
            if (!string.IsNullOrEmpty(details))
            {
                var copyBtn = new Button
                {
                    Content = "Copy details",
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new global::Avalonia.Thickness(16, 0, 0, 8),
                };
                copyBtn.Click += async (_, _) =>
                {
                    var clipboard = win.Clipboard;
                    if (clipboard != null)
                    {
                        await clipboard.SetTextAsync(message + "\n\n" + details);
                        copyBtn.Content = "Copied!";
                    }
                };
                root.Children.Add(copyBtn);
            }
            root.Children.Add(btnRow);
            win.Content = root;

            // ShowDialog requires a parent; fall back to Show if none available. Callers may pass an
            // explicit owner (e.g. a modal batch dialog) so nested prompts stack on top of it instead
            // of re-attaching to the main window.
            if (owner == null)
            {
                var app = global::Avalonia.Application.Current?.ApplicationLifetime
                    as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
                owner = app?.MainWindow;
            }

            if (owner != null)
                await win.ShowDialog(owner);
            else
                win.Show();

            return await tcs.Task;
        }
    }
}
