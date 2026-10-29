using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace DSPRE.Avalonia
{
    /// <summary>Message boxes and file pickers for every editor.</summary>
    public static class DialogHelper
    {
        public enum MsgResult { Ok, Yes, No, Cancel }

        /// <summary>
        /// Asks for a colour as three numbers from 0 to 255, with a square showing what they make.
        /// </summary>
        /// <param name="start">The colour the boxes start at, usually the one being changed.</param>
        public static async Task<(byte R, byte G, byte B)?> PickColourAsync(Window owner, string title, (byte R, byte G, byte B)? start = null)
        {
            byte r = 0, g = 0, b = 0;
            (byte, byte, byte)? answer = null;
            (byte R, byte G, byte B) from = start ?? (0, 0, 0);

            Border preview = new Border { Width = 64, Height = 64, BorderThickness = new global::Avalonia.Thickness(1) };
            NumericUpDown rBox = new NumericUpDown { Minimum = 0, Maximum = 255, Value = from.R, Width = 90, Increment = 1, FormatString = "0" };
            NumericUpDown gBox = new NumericUpDown { Minimum = 0, Maximum = 255, Value = from.G, Width = 90, Increment = 1, FormatString = "0" };
            NumericUpDown bBox = new NumericUpDown { Minimum = 0, Maximum = 255, Value = from.B, Width = 90, Increment = 1, FormatString = "0" };

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

            Button ok = new Button { Content = "Use this colour", MinWidth = 130, IsDefault = true };
            Button cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };

            Window win = new Window
            {
                Title = title,
                Width = 340, SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new global::Avalonia.Thickness(14),
                    Spacing = 10,
                    Children =
                    {
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

            DialogSizing.Grow(win);
            ok.Click += (_, __) => { answer = (r, g, b); win.Close(); };
            cancel.Click += (_, __) => win.Close();

            if (owner != null) await win.ShowDialog(owner); else win.Show();
            return answer;
        }

        // ----------------------------------------------------------------
        // Message Boxes (built from plain Avalonia Window, no 3rd-party dep)
        // ----------------------------------------------------------------

        public static Task ShowInfoAsync(string message, string title = "Information")
            => ShowMsgAsync(message, title, MsgButtons.Ok);

        public static Task ShowErrorAsync(string message, string title = "Error", Window owner = null)
            => ShowMsgAsync(message, title, MsgButtons.Ok, owner: owner);

        /// <summary>
        /// Error box with a "Copy details" button: <paramref name="details"/> (e.g. the full
        /// exception text) goes to the clipboard so users can paste it into a bug report.
        /// </summary>
        public static Task ShowErrorAsync(string message, string title, string details)
            => ShowMsgAsync(message, title, MsgButtons.Ok, details);

        /// <summary>Runs something the user started; an unexpected error is logged and shown, and the editor stays open.</summary>
        public static async Task GuardAsync(Func<Task> action, string failed, string title, Window owner = null)
        {
            try { await action(); }
            catch (Exception ex)
            {
                CrashReporter.LogHandled(ex);
                await ShowErrorAsync(failed + "\n" + ex.Message, title, owner);
            }
        }

        /// <summary>
        /// For a save that isn't awaited: shows its failure instead of losing it.
        /// </summary>
        public static async void Watch(Task task, string failed = "The changes weren't saved.", string title = "Couldn't save")
        {
            try { await task; }
            catch (Exception ex)
            {
                CrashReporter.LogHandled(ex);
                await ShowErrorAsync(failed + "\n" + ex.Message, title);
            }
        }

        /// <returns>true = Yes, false = No</returns>
        public static async Task<bool> AskYesNoAsync(string message, string title = "Confirm", Window owner = null)
        {
            MsgResult result = await ShowMsgAsync(message, title, MsgButtons.YesNo, owner: owner);
            return result == MsgResult.Yes;
        }

        /// <returns>MsgResult.Yes / No / Cancel</returns>
        public static Task<MsgResult> AskYesNoCancelAsync(string message, string title = "Confirm")
            => ShowMsgAsync(message, title, MsgButtons.YesNoCancel);

        /// <summary>The window the user is working in, so an editor's prompt shows over that editor and not behind it.</summary>
        private static Window ActiveOwner()
        => OwnerWindow.Current;

        /// <summary>Notice the user can turn off. True when they asked not to see it again.</summary>
        public static async Task<bool> ShowNoticeWithOptOutAsync(string message, string title,
                                                            string dismiss = "OK",
                                                            string never = "Don't show this again")
            => await ShowMsgAsync(message, title, MsgButtons.YesNo, labels: (dismiss, never, null)) == MsgResult.No;

        /// <summary>Two-way question whose buttons say what they do. True for <paramref name="yes"/>.</summary>
        public static async Task<bool> AskTwoWayAsync(string message, string title, string yes, string no, Window owner = null)
            => await ShowMsgAsync(message, title, MsgButtons.YesNo, owner: owner, labels: (yes, no, null)) == MsgResult.Yes;

        /// <summary>Asked by every Discard button: a discard cannot be undone.</summary>
        public static async Task<bool> ConfirmDiscardAsync(global::Avalonia.Visual from)
            => await ShowMsgAsync("Discard your unsaved changes?", "Discard", MsgButtons.YesNo,
                             owner: TopLevel.GetTopLevel(from) as Window, labels: ("Discard", "Keep", null)) == MsgResult.Yes;

        /// <summary>Three-way question whose buttons say what they do instead of Yes and No.</summary>
        public static Task<MsgResult> AskThreeWayAsync(string message, string title,
                                                  string yes, string no, string cancel = "Cancel")
            => ShowMsgAsync(message, title, MsgButtons.YesNoCancel, labels: (yes, no, cancel));

        /// <summary>Two-way question with a checkbox under the text. Returns which button was pressed and the box's state.</summary>
        public static async Task<(bool Yes, bool Checked)> AskWithCheckAsync(string message, string title, string yes, string no,
                                                                         string check, bool isChecked, Window owner = null)
        {
            (bool answer, bool[] ticked) = await AskWithChecksAsync(message, title, yes, no, new[] { (check, isChecked) }, owner);
            return (answer, ticked[0]);
        }

        /// <summary>A question with a check box per option; the ticks come back in the same order.</summary>
        public static async Task<(bool Yes, bool[] Checked)> AskWithChecksAsync(string message, string title, string yes, string no,
                                                                           (string Label, bool IsChecked)[] options, Window owner = null)
        {
            bool answer = false;
            Window win = new Window
            {
                Title = title,
                Width = 460,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.Height,
            };

            TextBlock msgText = new TextBlock
            {
                Text = message,
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                Margin = new global::Avalonia.Thickness(16, 16, 16, 8),
            };
            CheckBox[] boxes = options.Select(o => new CheckBox { Content = o.Label, IsChecked = o.IsChecked, Margin = new global::Avalonia.Thickness(16, 0, 16, 4) }).ToArray();

            Button yesBtn = new Button { Content = yes, MinWidth = 72 };
            yesBtn.Click += (_, _) => { answer = true; win.Close(); };
            Button noBtn = new Button { Content = no, MinWidth = 72, IsCancel = true };
            noBtn.Click += (_, _) => win.Close();
            StackPanel btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new global::Avalonia.Thickness(8, 0, 8, 12),
                Spacing = 6,
            };
            btnRow.Children.Add(yesBtn);
            btnRow.Children.Add(noBtn);

            StackPanel boxList = new StackPanel();
            foreach (CheckBox box in boxes) boxList.Children.Add(box);
            // A long list scrolls instead of growing the window past the screen.
            ScrollViewer list = new ScrollViewer { Content = boxList, MaxHeight = 420 };
            btnRow.Margin = new global::Avalonia.Thickness(8, 8, 8, 12);
            win.Content = DialogSizing.Body(list, btnRow, msgText);
            DialogSizing.Grow(win, list);

            owner ??= ActiveOwner();
            if (owner != null)
                await win.ShowDialog(owner);
            else
            {
                TaskCompletionSource closed = new TaskCompletionSource();
                win.Closed += (_, _) => closed.TrySetResult();
                win.Show();
                await closed.Task;
            }
            return (answer, boxes.Select(b => b.IsChecked == true).ToArray());
        }

        /// <summary>Shows text the user can select and copy, with a Copy button for all of it.</summary>
        public static async Task ShowCopyableTextAsync(string text, string title, Window owner = null)
        {
            Window win = new Window
            {
                Title = title,
                Width = 520,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.Height,
            };

            TextBox box = new TextBox
            {
                Text = text,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                MaxHeight = 360,
                Margin = new global::Avalonia.Thickness(16, 16, 16, 12),
            };

            Button copyBtn = new Button { MinWidth = 72 };
            Controls.Icon.SetKey(copyBtn, "copy");
            copyBtn.Content = "Copy";
            copyBtn.Click += async (_, _) =>
            {
                if (win.Clipboard == null) return;
                await win.Clipboard.SetTextAsync(text);
                copyBtn.Content = "Copied";
            };
            Button closeBtn = new Button { Content = "Close", MinWidth = 72, IsCancel = true, IsDefault = true };
            closeBtn.Click += (_, _) => win.Close();

            StackPanel btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new global::Avalonia.Thickness(8, 0, 8, 12),
                Spacing = 6,
            };
            btnRow.Children.Add(copyBtn);
            btnRow.Children.Add(closeBtn);

            win.Content = DialogSizing.Body(box, btnRow);
            DialogSizing.Grow(win, box);

            owner ??= ActiveOwner();
            if (owner != null)
                await win.ShowDialog(owner);
            else
                win.Show();
        }

        /// <summary>Prompts for a single line of free text. Returns null if cancelled or closed without
        /// confirming; an empty string is a valid (non-null) confirmed answer.</summary>
        public static async Task<string> PromptTextAsync(string message, string title = "Enter a value", string defaultValue = "", Window owner = null)
        {
            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();

            Window win = new Window
            {
                Title = title,
                Width = 420,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.Height,
            };

            TextBlock msgText = new TextBlock
            {
                Text = message,
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                Margin = new global::Avalonia.Thickness(16, 16, 16, 8),
            };

            TextBox input = new TextBox
            {
                Text = defaultValue,
                Margin = new global::Avalonia.Thickness(16, 0, 16, 12),
            };

            StackPanel btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new global::Avalonia.Thickness(8, 0, 8, 12),
                Spacing = 6,
            };

            Button okBtn = new Button { Content = "OK", MinWidth = 72, IsDefault = true };
            okBtn.Click += (_, _) => { tcs.TrySetResult(input.Text ?? ""); win.Close(); };
            Button cancelBtn = new Button { Content = "Cancel", MinWidth = 72, IsCancel = true };
            cancelBtn.Click += (_, _) => { tcs.TrySetResult(null); win.Close(); };
            btnRow.Children.Add(okBtn);
            btnRow.Children.Add(cancelBtn);

            win.Closed += (_, _) => tcs.TrySetResult(null);

            StackPanel root = new StackPanel();
            root.Children.Add(msgText);
            root.Children.Add(input);
            root.Children.Add(btnRow);
            win.Content = root;
            DialogSizing.Grow(win);

            owner ??= ActiveOwner();

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
        public static async Task<string> OpenFileAsync(
            Window owner,
            string title,
            IReadOnlyList<FilePickerFileType> filters = null)
        {
            FilePickerOpenOptions opts = new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = filters
            };

            IReadOnlyList<IStorageFile> files = await owner.StorageProvider.OpenFilePickerAsync(opts);
            return files?.Count > 0 ? files[0].TryGetLocalPath() : null;
        }

        /// <summary>Opens a folder-picker dialog. Returns null if cancelled.</summary>
        public static async Task<string> OpenFolderAsync(Window owner, string title)
        {
            FolderPickerOpenOptions opts = new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            };

            IReadOnlyList<IStorageFolder> folders = await owner.StorageProvider.OpenFolderPickerAsync(opts);
            return folders?.Count > 0 ? folders[0].TryGetLocalPath() : null;
        }

        /// <summary>Opens a file-save dialog. Returns null if cancelled.</summary>
        public static async Task<string> SaveFileAsync(
            Window owner,
            string title,
            IReadOnlyList<FilePickerFileType> filters = null,
            string suggestedFileName = null)
        {
            FilePickerSaveOptions opts = new FilePickerSaveOptions
            {
                Title = title,
                FileTypeChoices = filters,
                SuggestedFileName = suggestedFileName
            };

            IStorageFile file = await owner.StorageProvider.SaveFilePickerAsync(opts);
            return file?.TryGetLocalPath();
        }

        // ----------------------------------------------------------------
        // Common FilePickerFileType presets
        // ----------------------------------------------------------------

        public static readonly FilePickerFileType CsvFilter =
            new FilePickerFileType("CSV files") { Patterns = new[] { "*.csv" } };

        public static readonly FilePickerFileType JsonFilter =
            new FilePickerFileType("JSON files") { Patterns = new[] { "*.json" } };

        public static readonly FilePickerFileType PngFilter =
            new FilePickerFileType("PNG images") { Patterns = new[] { "*.png" } };

        public static readonly FilePickerFileType AllFilter =
            new FilePickerFileType("All files") { Patterns = new[] { "*.*" } };

        public static readonly FilePickerFileType ZipFilter =
            new FilePickerFileType("ZIP archive") { Patterns = new[] { "*.zip" } };

        // ----------------------------------------------------------------
        // Internal: lightweight dialog window built in code (no AXAML)
        // ----------------------------------------------------------------

        private enum MsgButtons { Ok, YesNo, YesNoCancel }

        private static async Task<MsgResult> ShowMsgAsync(string message, string title, MsgButtons buttons, string details = null, Window owner = null,
                                                     (string Yes, string No, string Cancel)? labels = null)
        {
            TaskCompletionSource<MsgResult> tcs = new TaskCompletionSource<MsgResult>();

            Window win = new Window
            {
                Title = title,
                Width = labels == null ? 420 : 560,   // named buttons are wider than Yes and No
                MinHeight = 140,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.Height,
            };

            TextBlock msgText = new TextBlock
            {
                Text = message,
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                Margin = new global::Avalonia.Thickness(16, 16, 16, 12),
            };

            StackPanel btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new global::Avalonia.Thickness(8, 0, 8, 12),
                Spacing = 6,
            };

            void AddBtn(string label, MsgResult result, bool enterOrEscape = false)
            {
                Button btn = new Button { Content = label, MinWidth = 72, IsDefault = enterOrEscape, IsCancel = enterOrEscape };
                btn.Click += (_, _) => { tcs.TrySetResult(result); win.Close(); };
                btnRow.Children.Add(btn);
            }

            switch (buttons)
            {
                // Only a lone OK answers Enter; a question should not be answered by a stray key.
                case MsgButtons.Ok:
                    AddBtn("OK", MsgResult.Ok, enterOrEscape: true);
                    break;
                case MsgButtons.YesNo:
                    AddBtn(labels?.Yes ?? "Yes", MsgResult.Yes);
                    AddBtn(labels?.No ?? "No", MsgResult.No);
                    break;
                case MsgButtons.YesNoCancel:
                    AddBtn(labels?.Yes ?? "Yes", MsgResult.Yes);
                    AddBtn(labels?.No ?? "No", MsgResult.No);
                    AddBtn(labels?.Cancel ?? "Cancel", MsgResult.Cancel);
                    break;
            }

            win.Closed += (_, _) => tcs.TrySetResult(MsgResult.Cancel);

            ScrollViewer text = new ScrollViewer { Content = msgText, MaxHeight = 380 };
            StackPanel bottom = new StackPanel();
            if (!string.IsNullOrEmpty(details))
            {
                Button copyBtn = new Button
                {
                    Content = "Copy details",
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new global::Avalonia.Thickness(16, 0, 0, 8),
                };
                copyBtn.Click += async (_, _) =>
                {
                    IClipboard clipboard = win.Clipboard;
                    if (clipboard != null)
                    {
                        await clipboard.SetTextAsync(message + "\n\n" + details);
                        copyBtn.Content = "Copied";
                    }
                };
                bottom.Children.Add(copyBtn);
            }
            bottom.Children.Add(btnRow);
            win.Content = DialogSizing.Body(text, bottom);
            DialogSizing.Grow(win, text);

            // ShowDialog requires a parent; fall back to Show if none available. Callers may pass an
            // explicit owner (e.g. a modal batch dialog) so nested prompts stack on top of it instead
            // of re-attaching to the main window.
            owner ??= ActiveOwner();

            if (owner != null)
                await win.ShowDialog(owner);
            else
                win.Show();

            return await tcs.Task;
        }
    }
}
