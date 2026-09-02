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

        /// <summary>
        /// Asks for a colour as three numbers from 0 to 255, with a square showing what they make.
        /// </summary>
        public static async Task<(byte R, byte G, byte B)?> PickColour(Window owner, string title)
        {
            byte r = 0, g = 0, b = 0;
            (byte, byte, byte)? answer = null;

            var preview = new Border { Width = 64, Height = 64, BorderThickness = new global::Avalonia.Thickness(1) };
            var rBox = new NumericUpDown { Minimum = 0, Maximum = 255, Value = 0, Width = 90, Increment = 1, FormatString = "0" };
            var gBox = new NumericUpDown { Minimum = 0, Maximum = 255, Value = 0, Width = 90, Increment = 1, FormatString = "0" };
            var bBox = new NumericUpDown { Minimum = 0, Maximum = 255, Value = 0, Width = 90, Increment = 1, FormatString = "0" };

            void Refresh()
            {
                r = (byte)(rBox.Value ?? 0); g = (byte)(gBox.Value ?? 0); b = (byte)(bBox.Value ?? 0);
                preview.Background = new global::Avalonia.Media.SolidColorBrush(
                    global::Avalonia.Media.Color.FromRgb(r, g, b));
            }
            rBox.ValueChanged += (_, __) => Refresh();
            gBox.ValueChanged += (_, __) => Refresh();
            bBox.ValueChanged += (_, __) => Refresh();
            Refresh();

            var ok = new Button { Content = "Use this colour", MinWidth = 130, IsDefault = true };
            var cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };

            var win = new Window
            {
                Title = title,
                Width = 340, Height = 250,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new global::Avalonia.Thickness(14),
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock { Text = "How much red, green and blue, each from 0 to 255.",
                                        TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, FontSize = 12 },
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children =
                        {
                            preview,
                            new StackPanel { Spacing = 4, Children =
                            {
                                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children =
                                    { new TextBlock { Text = "Red", Width = 44, VerticalAlignment = VerticalAlignment.Center }, rBox } },
                                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children =
                                    { new TextBlock { Text = "Green", Width = 44, VerticalAlignment = VerticalAlignment.Center }, gBox } },
                                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children =
                                    { new TextBlock { Text = "Blue", Width = 44, VerticalAlignment = VerticalAlignment.Center }, bBox } },
                            } },
                        } },
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8,
                                         HorizontalAlignment = HorizontalAlignment.Right,
                                         Children = { ok, cancel } },
                    },
                },
            };

            ok.Click += (_, __) => { answer = (r, g, b); win.Close(); };
            cancel.Click += (_, __) => win.Close();

            if (owner != null) await win.ShowDialog(owner); else win.Show();
            return answer;
        }

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

        /// <summary>Three-way question whose buttons say what they do instead of Yes and No.</summary>
        public static Task<MsgResult> AskThreeWay(string message, string title,
                                                  string yes, string no, string cancel = "Cancel")
            => ShowMsg(message, title, MsgButtons.YesNoCancel, labels: (yes, no, cancel));

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

        private static async Task<MsgResult> ShowMsg(string message, string title, MsgButtons buttons, string details = null, Window owner = null,
                                                     (string Yes, string No, string Cancel)? labels = null)
        {
            var tcs = new TaskCompletionSource<MsgResult>();

            var win = new Window
            {
                Title = title,
                Width = labels == null ? 420 : 560,   // named buttons are wider than Yes and No
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
                    AddBtn(labels?.Yes ?? "Yes", MsgResult.Yes);
                    AddBtn(labels?.No ?? "No", MsgResult.No);
                    AddBtn(labels?.Cancel ?? "Cancel", MsgResult.Cancel);
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
