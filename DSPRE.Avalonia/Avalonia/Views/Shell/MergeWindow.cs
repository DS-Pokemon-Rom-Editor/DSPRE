using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using DSPRE.Avalonia.ViewModels.Shell;
using DSPRE.Tracking;
using static DSPRE.Avalonia.Views.Shell.ChangeText;

namespace DSPRE.Avalonia.Views.Shell
{
    /// <summary>
    /// Picks local or remote for each field both sides changed, record by record, then commits the merge.
    /// Each pick takes one side whole, so every record stays a valid change.
    /// </summary>
    public sealed class MergeWindow : Window
    {
        private readonly ChangesViewModel _vm;
        private readonly PendingMerge _merge;
        private readonly ListBox _records = new ListBox { MinWidth = 210 };
        private readonly StackPanel _detail = new StackPanel { Spacing = 10, Margin = new global::Avalonia.Thickness(12) };
        private readonly TextBlock _left = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.SemiBold };
        private readonly Button _finish = new Button { Content = "Finish merge", MinWidth = 110 };
        private bool _done;

        public static async Task ShowAsync(Window owner, ChangesViewModel vm, PendingMerge merge)
        {
            if (owner == null || merge == null) return;
            MergeWindow win = new MergeWindow(vm, merge);
            await win.ShowDialog(owner);
        }

        private MergeWindow(ChangesViewModel vm, PendingMerge merge)
        {
            _vm = vm;
            _merge = merge;
            Title = "Merge changes from " + merge.Upstream;
            Width = 1040;
            Height = 660;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            Button prev = new Button { Content = "Previous" };
            Button next = new Button { Content = "Next" };
            ToolTip.SetTip(prev, "Previous conflict");
            ToolTip.SetTip(next, "Next conflict");
            prev.Click += (_, _) => Step(-1);
            next.Click += (_, _) => Step(1);
            DockPanel top = new DockPanel { Margin = new global::Avalonia.Thickness(12, 10) };
            StackPanel nav = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            nav.Children.Add(_left);
            nav.Children.Add(prev);
            nav.Children.Add(next);
            DockPanel.SetDock(nav, Dock.Right);
            top.Children.Add(nav);
            top.Children.Add(new TextBlock { Text = "Pick Local or Remote for each change both sides made.", VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });

            _records.ItemsSource = merge.Records;
            _records.ItemTemplate = new global::Avalonia.Controls.Templates.FuncDataTemplate<RecordClash>((r, _) => RecordRow(r));
            _records.SelectionChanged += (_, _) => ShowRecord(_records.SelectedItem as RecordClash);

            Button abort = new Button { Content = "Abort merge" };
            ToolTip.SetTip(abort, "Undo the pull");
            abort.Click += async (_, _) =>
            {
                string error = await _vm.AbortMergeAsync();
                if (error != null) await DialogHelper.ShowErrorAsync(error, "Abort merge", this);
                _done = true;
                Close();
            };
            Button later = new Button { Content = "Decide later" };
            ToolTip.SetTip(later, "Keep the merge open");
            later.Click += (_, _) => Close();
            _finish.Click += async (_, _) =>
            {
                string error = await _vm.FinishMergeAsync(_merge);
                if (error != null)
                {
                    await DialogHelper.ShowErrorAsync(error, "Merge not finished", this);
                    return;
                }
                _done = true;
                Close();
            };
            DockPanel foot = new DockPanel { Margin = new global::Avalonia.Thickness(12) };
            DockPanel.SetDock(abort, Dock.Left);
            foot.Children.Add(abort);
            StackPanel right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right };
            right.Children.Add(later);
            right.Children.Add(_finish);
            foot.Children.Add(right);

            Grid body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            Border list = new Border { Child = _records, BorderThickness = new global::Avalonia.Thickness(0, 0, 1, 0) };
            list.Bind(Border.BorderBrushProperty, list.GetResourceObservable("Editor.Border"));
            Grid.SetColumn(list, 0);
            ScrollViewer detail = new ScrollViewer { Content = _detail, HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
            Grid.SetColumn(detail, 1);
            body.Children.Add(list);
            body.Children.Add(detail);

            Content = DialogSizing.Body(body, foot, top);
            DialogSizing.Grow(this, body);
            Opened += (_, _) =>
            {
                _records.SelectedItem = merge.Clashing.FirstOrDefault() ?? merge.Records.FirstOrDefault();
                Refresh();
            };
        }

        private Control RecordRow(RecordClash r)
        {
            // Containers are cleared with a null item when the list is rebuilt.
            if (r == null) return new TextBlock();
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            Ellipse dot = new Ellipse { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center };
            dot.Bind(Shape.FillProperty, dot.GetResourceObservable(r.Conflicts.Count == 0 ? "Editor.Good" : r.Resolved ? "Editor.Good" : "Editor.Bad"));
            row.Children.Add(dot);
            row.Children.Add(new TextBlock { Text = r.Title });
            if (r.Conflicts.Count == 0) row.Children.Add(Subtle("merged"));
            else if (!r.Resolved) row.Children.Add(Subtle(r.Conflicts.Count(c => !r.Choices.ContainsKey(c.Path)) + " to pick"));
            return row;
        }

        private void Step(int by)
        {
            List<RecordClash> clashing = _merge.Records.Where(r => r.Conflicts.Count > 0).ToList();
            if (clashing.Count == 0) return;
            int at = clashing.IndexOf(_records.SelectedItem as RecordClash);
            _records.SelectedItem = clashing[((at < 0 ? 0 : at + by) % clashing.Count + clashing.Count) % clashing.Count];
        }

        private void ShowRecord(RecordClash record)
        {
            _detail.Children.Clear();
            if (record == null) return;
            _detail.Children.Add(new TextBlock { Text = record.Title, FontSize = 16, FontWeight = FontWeight.SemiBold });
            Grid sides = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*") };
            sides.Children.Add(Side("LOCAL", "ABrush.ColourfulGlyph.Selected", _merge.LocalSubject, "Use local for all", record, true, 0));
            sides.Children.Add(Side("REMOTE", "Editor.Warn", _merge.RemoteSubject, "Use remote for all", record, false, 2));
            _detail.Children.Add(sides);
            if (record.Conflicts.Count == 0)
            {
                _detail.Children.Add(new TextBlock { Text = "Both sides changed this record in different places, so it merged on its own.", TextWrapping = TextWrapping.Wrap });
                ShowResult(RecordMerge.MergeRecord(record.Common, record.Local, record.Remote, _ => true));
                return;
            }
            Grid table = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,*,*,*,Auto"), RowDefinitions = new RowDefinitions() };
            AddRow(table, Header("Field"), Header("Original"), Header("Local"), Header("Remote"), Header("Use"));
            foreach (FieldConflict c in record.Conflicts)
            {
                RadioButton local = new RadioButton { Content = "Local", GroupName = record.Path + "|" + c.Path, IsChecked = record.Choices.TryGetValue(c.Path, out bool l) && l };
                RadioButton remote = new RadioButton { Content = "Remote", GroupName = record.Path + "|" + c.Path, IsChecked = record.Choices.TryGetValue(c.Path, out bool r) && !r };
                local.IsCheckedChanged += (_, _) => { if (local.IsChecked == true) Pick(record, c.Path, true); };
                remote.IsCheckedChanged += (_, _) => { if (remote.IsChecked == true) Pick(record, c.Path, false); };
                StackPanel pick = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                pick.Children.Add(local);
                pick.Children.Add(remote);
                AddRow(table, Cell(FieldName(c.Path)), Cell(Original(c)), Cell(Value(c.Local, c)), Cell(Value(c.Remote, c)), pick);
            }
            _detail.Children.Add(table);
            if (!record.Resolved)
            {
                _detail.Children.Add(Subtle("Pick a side for every row to see the result."));
                return;
            }
            ShowResult(RecordMerge.MergeRecord(record.Common, record.Local, record.Remote, c => !record.Choices.TryGetValue(c.Path, out bool keep) || keep));
        }

        // The merged record as it will be written: every field it changes, from the original to the result.
        private void ShowResult(JsonObject result)
        {
            _detail.Children.Add(new TextBlock { Text = "Result", FontWeight = FontWeight.SemiBold, Margin = new global::Avalonia.Thickness(0, 6, 0, 0) });
            if (result?["changes"] is not JsonNode changes)
            {
                _detail.Children.Add(Subtle("Back to the original: no changes left."));
                return;
            }
            Grid table = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,*,*"), RowDefinitions = new RowDefinitions() };
            AddRow(table, Header("Field"), Header("Original"), Header("Result"));
            foreach ((string field, string was, string now) in Rows(changes).Take(300)) AddRow(table, Cell(field), Cell(was), Cell(now));
            _detail.Children.Add(table);
        }

        private Control Side(string label, string brush, string commit, string useAll, RecordClash record, bool local, int column)
        {
            Border box = new Border { BorderThickness = new global::Avalonia.Thickness(1), CornerRadius = new global::Avalonia.CornerRadius(4), Padding = new global::Avalonia.Thickness(8) };
            box.Bind(Border.BorderBrushProperty, box.GetResourceObservable("Editor.Border"));
            StackPanel stack = new StackPanel { Spacing = 6 };
            TextBlock name = new TextBlock { Text = label, FontWeight = FontWeight.Bold };
            name.Bind(TextBlock.ForegroundProperty, name.GetResourceObservable(brush));
            stack.Children.Add(name);
            if (commit != null) stack.Children.Add(new TextBlock { Text = commit, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
            if (record.Conflicts.Count > 0)
            {
                Button all = new Button { Content = useAll, HorizontalAlignment = HorizontalAlignment.Left };
                all.Click += (_, _) =>
                {
                    foreach (FieldConflict c in record.Conflicts) record.Choices[c.Path] = local;
                    ShowRecord(record);
                    Refresh();
                };
                stack.Children.Add(all);
            }
            box.Child = stack;
            Grid.SetColumn(box, column);
            return box;
        }

        private void Pick(RecordClash record, string path, bool local)
        {
            if (record.Choices.TryGetValue(path, out bool was) && was == local) return;
            record.Choices[path] = local;
            ShowRecord(record);
            Refresh();
        }

        private void Refresh()
        {
            int left = _merge.Records.Sum(r => r.Conflicts.Count(c => !r.Choices.ContainsKey(c.Path)));
            _left.Text = left == 0 ? "Every conflict picked" : Plural.Of(left, "conflict") + " left";
            _finish.IsEnabled = left == 0;
            ToolTip.SetTip(_finish, left == 0 ? null : "Pick every conflict first");
            ToolTip.SetShowOnDisabled(_finish, true);
            RecordClash selected = _records.SelectedItem as RecordClash;
            _records.ItemsSource = null;
            _records.ItemsSource = _merge.Records;
            _records.SelectedItem = selected;
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            if (!_done) _ = _vm.RefreshViewAsync();
        }

        private static string Original(FieldConflict c) =>
            ValueText(Leaf(c.Local)?[ObjectDiff.Was] ?? Leaf(c.Remote)?[ObjectDiff.Was] ?? Leaf(c.Base)?[ObjectDiff.Was], "unchanged");

        private static string Value(JsonNode side, FieldConflict c)
        {
            if (side == null) return Original(c) + " (back to original)";
            if (Leaf(side) is JsonObject leaf) return ValueText(leaf[ObjectDiff.Now], "removed");
            if (side is JsonObject op && op.ContainsKey(ObjectDiff.Add)) return "added " + ValueText(op[ObjectDiff.Add], "");
            if (side is JsonObject rem && rem.ContainsKey(ObjectDiff.Remove)) return "removed";
            return ValueText(side, "");
        }
    }
}
