using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using DSPRE.Tracking;
using static DSPRE.Avalonia.Views.Shell.ChangeText;

namespace DSPRE.Avalonia.Views.Shell
{
    /// <summary>
    /// Every record a patch changes, field by field, with a pick for each field this project also changed.
    /// Returns the picks, true where the patch's value is used, or null when cancelled.
    /// </summary>
    public sealed class PatchWindow : Window
    {
        private readonly DsPatch.Plan _plan;
        private readonly Dictionary<(string Record, string Path), bool> _usePatch = new Dictionary<(string, string), bool>();
        private readonly ListBox _records = new ListBox { MinWidth = 230 };
        private readonly StackPanel _detail = new StackPanel { Spacing = 10, Margin = new global::Avalonia.Thickness(12) };
        private bool _apply;

        public static async Task<Dictionary<(string Record, string Path), bool>> ShowAsync(Window owner, DsPatch.Plan plan, string what)
        {
            PatchWindow win = new PatchWindow(plan, what);
            await win.ShowDialog(owner);
            return win._apply ? win._usePatch : null;
        }

        private PatchWindow(DsPatch.Plan plan, string what)
        {
            _plan = plan;
            Title = "Apply " + what;
            Width = 1040;
            Height = 660;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            int clean = plan.Clean.Count, there = plan.AlreadyThere.Count, clashing = plan.Clashing.Count, problems = plan.Problems.Count;
            List<string> parts = new List<string> { Plural.Of(clean, "record") + " to apply" };
            if (clashing > 0) parts.Add(clashing + " with local changes");
            if (there > 0) parts.Add(there + " already applied");
            if (problems > 0) parts.Add(problems + " can't be applied");
            TextBlock top = new TextBlock { Text = string.Join(", ", parts), Margin = new global::Avalonia.Thickness(12, 10), TextWrapping = TextWrapping.Wrap };

            _records.ItemsSource = plan.Records.OrderBy(r => Order(r.Outcome)).ThenBy(r => r.Title, System.StringComparer.Ordinal).ToList();
            _records.ItemTemplate = new global::Avalonia.Controls.Templates.FuncDataTemplate<DsPatch.PlannedRecord>((r, _) => RecordRow(r));
            _records.SelectionChanged += (_, _) => ShowRecord(_records.SelectedItem as DsPatch.PlannedRecord);

            Button cancel = new Button { Content = "Cancel" };
            cancel.Click += (_, _) => Close();
            Button apply = new Button { Content = "Apply", MinWidth = 90, IsEnabled = clean + clashing > 0 };
            apply.Click += (_, _) =>
            {
                _apply = true;
                Close();
            };
            StackPanel foot = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right, Margin = new global::Avalonia.Thickness(12) };
            foot.Children.Add(cancel);
            foot.Children.Add(apply);

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
            Opened += (_, _) => _records.SelectedIndex = 0;
        }

        private static int Order(DsPatch.Outcome o) => o switch
        {
            DsPatch.Outcome.Clashing => 0,
            DsPatch.Outcome.Problem => 1,
            DsPatch.Outcome.Clean => 2,
            _ => 3,
        };

        private static Control RecordRow(DsPatch.PlannedRecord r)
        {
            if (r == null) return new TextBlock();
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            Ellipse dot = new Ellipse { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center };
            dot.Bind(Shape.FillProperty, dot.GetResourceObservable(r.Outcome switch
            {
                DsPatch.Outcome.Clean => "Editor.Good",
                DsPatch.Outcome.Clashing => "Editor.Warn",
                DsPatch.Outcome.Problem => "Editor.Bad",
                _ => "Editor.Subtle",
            }));
            row.Children.Add(dot);
            row.Children.Add(new TextBlock { Text = r.Title });
            if (r.Outcome == DsPatch.Outcome.AlreadyThere) row.Children.Add(Subtle("already applied"));
            return row;
        }

        private void ShowRecord(DsPatch.PlannedRecord record)
        {
            _detail.Children.Clear();
            if (record == null) return;
            _detail.Children.Add(new TextBlock { Text = record.Title, FontSize = 16, FontWeight = FontWeight.SemiBold });
            if (record.Outcome == DsPatch.Outcome.Problem)
            {
                _detail.Children.Add(new TextBlock { Text = "Can't be applied: " + record.Problem, TextWrapping = TextWrapping.Wrap });
                return;
            }
            if (record.Conflicts.Count > 0)
            {
                _detail.Children.Add(Subtle("These have local changes too."));
                Grid clashes = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,*,*,*,Auto"), RowDefinitions = new RowDefinitions() };
                AddRow(clashes, Header("Field"), Header("Original"), Header("Incoming"), Header("Local"), Header("Use"));
                foreach (Conflict c in record.Conflicts)
                {
                    (string, string) key = (record.Title, c.Path);
                    string group = record.Title + "|" + c.Path;
                    bool usePatch = _usePatch.TryGetValue(key, out bool u) && u;
                    RadioButton mine = new RadioButton { Content = "Local", GroupName = group, IsChecked = !usePatch };
                    RadioButton theirs = new RadioButton { Content = "Incoming", GroupName = group, IsChecked = usePatch };
                    mine.IsCheckedChanged += (_, _) => { if (mine.IsChecked == true) _usePatch[key] = false; };
                    theirs.IsCheckedChanged += (_, _) => { if (theirs.IsChecked == true) _usePatch[key] = true; };
                    StackPanel pick = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                    pick.Children.Add(mine);
                    pick.Children.Add(theirs);
                    AddRow(clashes, Cell(FieldName(c.Path)), Cell(Value(c.Was)), Cell(Value(c.Now)), Cell(Value(c.Rom)), pick);
                }
                _detail.Children.Add(clashes);
            }
            if (record.Changes == null)
            {
                _detail.Children.Add(new TextBlock { Text = "Whole files: " + string.Join(", ", record.Files), TextWrapping = TextWrapping.Wrap });
                return;
            }
            List<(string Field, string Was, string Now)> rows = Rows(record.Changes);
            if (record.Conflicts.Count > 0) _detail.Children.Add(Subtle("Everything the patch changes"));
            Grid table = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,*,*"), RowDefinitions = new RowDefinitions() };
            AddRow(table, Header("Field"), Header("Original"), Header("Patch"));
            foreach ((string field, string was, string now) in rows.Take(300)) AddRow(table, Cell(field), Cell(was), Cell(now));
            _detail.Children.Add(table);
            if (rows.Count > 300) _detail.Children.Add(Subtle(Plural.Of(rows.Count - 300, "more change")));
        }

        private static string Value(JsonNode node) => Leaf(node) is JsonObject leaf ? ValueText(leaf[ObjectDiff.Now], "removed") : ValueText(node, "unchanged");

    }
}
