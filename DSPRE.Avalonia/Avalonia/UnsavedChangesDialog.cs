using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using DSPRE.Editors;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Lists all dirty editors before a project change or application exit and lets the user save a
    /// selected subset, discard everything, or cancel the operation.
    /// </summary>
    public sealed class UnsavedChangesDialog : Window
    {
        public sealed class UnsavedEditorInfo
        {
            public string EditorName { get; set; }
            public string Description { get; set; }
            public IEditorWithUnsavedChanges Editor { get; set; }

            public override string ToString()
                => string.IsNullOrWhiteSpace(Description)
                    ? EditorName
                    : $"{EditorName} - {Description}";
        }

        private readonly List<UnsavedEditorInfo> _editors;
        private readonly List<CheckBox> _editorChecks = new List<CheckBox>();
        private readonly List<Button> _buttons = new List<Button>();
        private readonly TaskCompletionSource<bool> _result = new TaskCompletionSource<bool>();
        private bool _completed;
        private bool _busy;

        private UnsavedChangesDialog(IEnumerable<UnsavedEditorInfo> editors)
        {
            _editors = editors.ToList();
            Title = "Unsaved Changes";
            Width = 560;
            Height = 420;
            MinWidth = 440;
            MinHeight = 300;
            CanResize = true;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var root = new DockPanel { Margin = new Thickness(16) };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Margin = new Thickness(0, 12, 0, 0),
            };
            DockPanel.SetDock(buttons, Dock.Bottom);

            var cancel = new Button { Content = "Cancel", MinWidth = 90 };
            cancel.Click += (_, _) => Finish(false);
            _buttons.Add(cancel);
            buttons.Children.Add(cancel);

            var discardAll = new Button { Content = "Discard All", MinWidth = 100 };
            discardAll.Click += async (_, _) => await DiscardAllAsync();
            _buttons.Add(discardAll);
            buttons.Children.Add(discardAll);

            var saveSelected = new Button { Content = "Save Selected", MinWidth = 110 };
            saveSelected.Click += async (_, _) => await SaveSelectedAsync();
            _buttons.Add(saveSelected);
            buttons.Children.Add(saveSelected);
            root.Children.Add(buttons);

            var header = new StackPanel { Spacing = 4 };
            header.Children.Add(new TextBlock
            {
                Text = "The following editors have unsaved changes:",
                FontWeight = FontWeight.SemiBold,
            });
            header.Children.Add(new TextBlock
            {
                Text = "Select which changes to save. Unselected editors will be discarded.",
                Opacity = 0.75,
                TextWrapping = TextWrapping.Wrap,
            });
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var editorList = new StackPanel { Spacing = 5 };
            foreach (var editor in _editors)
            {
                var check = new CheckBox
                {
                    Content = editor.ToString(),
                    IsChecked = true,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                _editorChecks.Add(check);
                editorList.Children.Add(check);
            }

            root.Children.Add(new ScrollViewer
            {
                Content = editorList,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            });

            Content = root;
            KeyDown += OnKeyDown;
            Closed += (_, _) => Complete(false);
        }

        public static Task<bool> ShowIfNeededAsync(
            Window owner,
            IEditorWithUnsavedChanges editor,
            string editorName)
        {
            if (editor == null)
            {
                return Task.FromResult(true);
            }

            return ShowIfNeededAsync(owner, new[]
            {
                new UnsavedEditorInfo
                {
                    EditorName = string.IsNullOrWhiteSpace(editorName) ? editor.GetType().Name : editorName,
                    Description = editor.UnsavedChangesDescription,
                    Editor = editor,
                },
            });
        }

        /// <summary>
        /// Saves one editor and returns null on success, or a user-facing failure reason.
        /// The confirmation dialog uses this for both single-editor and batch saves.
        /// </summary>
        public static async Task<string> TrySaveEditorAsync(IEditorWithUnsavedChanges editor)
        {
            if (editor == null || !editor.HasUnsavedChanges)
            {
                return null;
            }

            try
            {
                if (await editor.SaveChangesAsync() && !editor.HasUnsavedChanges)
                {
                    return null;
                }

                return "The save was canceled or changes remain unsaved.";
            }
            catch (Exception ex)
            {
                AppLogger.Error($"UnsavedChangesDialog: Failed to save {editor.UnsavedChangesDescription}: {ex}");
                return ex.Message;
            }
        }

        /// <summary>Discards one editor and returns null only when no changes remain.</summary>
        public static string TryDiscardEditor(IEditorWithUnsavedChanges editor)
        {
            if (editor == null || !editor.HasUnsavedChanges)
            {
                return null;
            }

            try
            {
                editor.DiscardChanges();
                return editor.HasUnsavedChanges ? "Changes remain after discard." : null;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"UnsavedChangesDialog: Failed to discard {editor.UnsavedChangesDescription}: {ex}");
                return ex.Message;
            }
        }

        public static async Task<bool> ShowIfNeededAsync(
            Window owner,
            IEnumerable<UnsavedEditorInfo> editorsWithChanges)
        {
            var editors = (editorsWithChanges ?? Enumerable.Empty<UnsavedEditorInfo>())
                .Where(info => info?.Editor != null && info.Editor.HasUnsavedChanges)
                .ToList();
            if (editors.Count == 0)
            {
                return true;
            }

            var dialog = new UnsavedChangesDialog(editors);
            if (owner != null)
            {
                await dialog.ShowDialog(owner);
            }
            else
            {
                dialog.Show();
            }

            return await dialog._result.Task;
        }

        private async Task SaveSelectedAsync()
        {
            if (_completed || _busy) return;
            _busy = true;
            SetButtonsEnabled(false);
            try
            {
                var failures = new List<string>();

                // Save first. If any save fails, leave the dialog open so the user can retry or cancel;
                // explicitly unselected editors must not be discarded as a side effect of a failed save.
                for (int i = 0; i < _editors.Count; i++)
                {
                    if (_completed) return;
                    if (_editorChecks[i].IsChecked != true) continue;

                    var info = _editors[i];
                    AppLogger.Info($"UnsavedChangesDialog: Saving {info.EditorName}");
                    string failure = await TrySaveEditorAsync(info.Editor);
                    if (failure != null)
                    {
                        failures.Add($"{info.EditorName}: {failure}");
                    }
                }

                if (_completed) return;

                if (failures.Count > 0)
                {
                    await DialogHelper.ShowError(
                        "One or more editors could not be saved:\n\n" + string.Join("\n", failures),
                        "Save Error", this);
                    return;
                }

                // Only discard explicitly unselected editors once every requested save has completed.
                for (int i = 0; i < _editors.Count; i++)
                {
                    if (_completed) return;
                    if (_editorChecks[i].IsChecked == true) continue;

                    var info = _editors[i];
                    AppLogger.Info($"UnsavedChangesDialog: Discarding changes for {info.EditorName}");
                    string failure = TryDiscardEditor(info.Editor);
                    if (failure != null)
                    {
                        failures.Add($"{info.EditorName}: {failure}");
                    }
                }

                if (_completed) return;

                if (failures.Count > 0)
                {
                    await DialogHelper.ShowError(
                        "One or more editors could not be discarded:\n\n" + string.Join("\n", failures),
                        "Discard Error", this);
                    return;
                }

                Finish(true);
            }
            finally
            {
                if (!_completed)
                {
                    _busy = false;
                    SetButtonsEnabled(true);
                }
            }
        }

        private async Task DiscardAllAsync()
        {
            if (_completed || _busy) return;
            if (!await DialogHelper.AskYesNo(
                "Are you sure you want to discard ALL unsaved changes?\n\nThis cannot be undone.",
                "Confirm Discard", this))
            {
                return;
            }
            if (_completed) return;

            _busy = true;
            SetButtonsEnabled(false);
            try
            {
                var failures = new List<string>();
                foreach (var info in _editors)
                {
                    if (_completed) return;
                    AppLogger.Info($"UnsavedChangesDialog: Discarding changes for {info.EditorName}");
                    string failure = TryDiscardEditor(info.Editor);
                    if (failure != null)
                    {
                        failures.Add($"{info.EditorName}: {failure}");
                    }
                }

                if (_completed) return;

                if (failures.Count > 0)
                {
                    await DialogHelper.ShowError(
                        "One or more editors could not be discarded:\n\n" + string.Join("\n", failures),
                        "Discard Error", this);
                    return;
                }

                Finish(true);
            }
            finally
            {
                if (!_completed)
                {
                    _busy = false;
                    SetButtonsEnabled(true);
                }
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !_busy)
            {
                Finish(false);
                e.Handled = true;
            }
        }

        private void SetButtonsEnabled(bool enabled)
        {
            foreach (var b in _buttons) b.IsEnabled = enabled;
        }

        private void Finish(bool result)
        {
            Complete(result);
            Close();
        }

        private void Complete(bool result)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            _result.TrySetResult(result);
        }
    }
}
