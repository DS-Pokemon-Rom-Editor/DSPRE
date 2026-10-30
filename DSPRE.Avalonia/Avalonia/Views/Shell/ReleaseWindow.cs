using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using static DSPRE.Avalonia.Views.Shell.ChangeText;

namespace DSPRE.Avalonia.Views.Shell
{
    /// <summary>
    /// A release: its name, its notes and which files to make, with a preview of how the release reads.
    /// </summary>
    public sealed class ReleaseWindow : Window
    {
        public sealed record Choice(string Name, string Notes, bool Patch, bool Xdelta, bool GitHub, bool Changelog);

        private Choice _choice;

        /// <summary>The release as asked for, or null when cancelled. <paramref name="noGitHub"/> is the reason GitHub can't be used, or null when it can.</summary>
        public static async Task<Choice> ShowAsync(Window owner, string noGitHub, DSPRE.Tracking.ChangeTracker tracker, string xdeltaBase)
        {
            IReadOnlyList<string> tags = await DSPRE.Tracking.GitCli.TagsAsync(tracker.Folder);
            ReleaseWindow win = new ReleaseWindow(noGitHub, tracker, xdeltaBase, tags);
            await win.ShowDialog(owner);
            return win._choice;
        }

        private ReleaseWindow(string noGitHub, DSPRE.Tracking.ChangeTracker tracker, string xdeltaBase, IReadOnlyList<string> tags)
        {
            Title = "Make a release";
            Width = 800;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            TextBox name = new TextBox { PlaceholderText = "v1.0" };
            TextBox notes = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, VerticalContentAlignment = VerticalAlignment.Top };
            CheckBox patch = new CheckBox { Content = "DSPRE patch", IsChecked = true };
            ToolTip.SetTip(patch, "For other DSPRE projects");
            CheckBox xdelta = new CheckBox { Content = "Xdelta for players", IsChecked = false };
            ToolTip.SetTip(xdelta, "Builds the ROM first");
            CheckBox gitHub = new CheckBox { Content = "GitHub release", IsChecked = false, IsEnabled = noGitHub == null };
            ToolTip.SetTip(gitHub, noGitHub);
            ToolTip.SetShowOnDisabled(gitHub, true);
            CheckBox changelog = new CheckBox { Content = "Changelog", IsChecked = false };
            ToolTip.SetTip(changelog, "Saved next to the other files");
            Button changelogOptions = new Button { Content = "Options…", FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            StackPanel changelogRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            changelogRow.Children.Add(changelog);
            changelogRow.Children.Add(changelogOptions);

            StackPanel form = new StackPanel { Spacing = 6, Margin = new global::Avalonia.Thickness(12) };
            form.Children.Add(Subtle("Release name"));
            form.Children.Add(name);
            form.Children.Add(Subtle("Release notes"));
            form.Children.Add(notes);
            form.Children.Add(Subtle("Files"));
            form.Children.Add(patch);
            form.Children.Add(xdelta);
            TextBlock xdeltaNote = Subtle(xdeltaBase);
            xdeltaNote.TextWrapping = TextWrapping.Wrap;
            xdeltaNote.Margin = new global::Avalonia.Thickness(26, -4, 0, 0);
            form.Children.Add(xdeltaNote);
            form.Children.Add(changelogRow);
            form.Children.Add(gitHub);
            TextBlock problem = Subtle("");
            form.Children.Add(problem);

            // The preview: the release as it reads, top to bottom, and the files it makes.
            TextBlock previewTitle = new TextBlock { FontWeight = FontWeight.SemiBold, FontSize = 15, TextWrapping = TextWrapping.Wrap };
            TextBlock previewNotes = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxLines = 4, TextTrimming = TextTrimming.CharacterEllipsis };
            TextBlock previewStart = new TextBlock { TextWrapping = TextWrapping.Wrap };
            Border notesBox = Box("Release notes", previewNotes);
            Border changelogBox = Box("Changelog", previewStart);
            WrapPanel fileChips = new WrapPanel();
            StackPanel preview = new StackPanel { Spacing = 8, Margin = new global::Avalonia.Thickness(12) };
            preview.Children.Add(Subtle("Preview"));
            preview.Children.Add(Box("Release", previewTitle));
            preview.Children.Add(notesBox);
            preview.Children.Add(changelogBox);
            TextBlock filesLabel = Subtle("");
            filesLabel.TextWrapping = TextWrapping.Wrap;
            preview.Children.Add(filesLabel);
            preview.Children.Add(fileChips);
            TextBlock gitHubNote = Subtle("Also published on GitHub, with the patch and xdelta attached.");
            gitHubNote.TextWrapping = TextWrapping.Wrap;
            preview.Children.Add(gitHubNote);

            void Update()
            {
                string tag = (name.Text ?? "").Trim();
                previewTitle.Text = tag.Length > 0 ? tag : "Changelog";
                string text = (notes.Text ?? "").Trim();
                previewNotes.Text = text.Length > 0 ? text : "No notes";
                previewNotes.Opacity = text.Length > 0 ? 1 : 0.6;
                changelogBox.IsVisible = changelog.IsChecked == true;
                previewStart.Text = ChangelogWindow.ReleaseStart(tracker, tags, tag) + ".";
                string stem = DSPRE.Tracking.ChangesJson.Slug((tracker.Manifest.ProjectName ?? RomInfo.ProjectName) + (tag.Length > 0 ? "-" + tag : ""));
                filesLabel.Text = "Folder " + stem;
                fileChips.Children.Clear();
                if (patch.IsChecked == true) fileChips.Children.Add(Chip(stem + ".dspatch"));
                if (xdelta.IsChecked == true) fileChips.Children.Add(Chip(stem + ".xdelta"));
                if (changelog.IsChecked == true)
                {
                    fileChips.Children.Add(Chip("changelog.md"));
                    fileChips.Children.Add(Chip("pictures"));
                }
                gitHubNote.IsVisible = gitHub.IsChecked == true;
            }
            name.TextChanged += (_, _) => Update();
            notes.TextChanged += (_, _) => Update();
            foreach (CheckBox box in new[] { patch, xdelta, changelog, gitHub }) box.IsCheckedChanged += (_, _) => Update();
            changelogOptions.Click += async (_, _) =>
            {
                await ChangelogWindow.ShowAsync(this, tracker);
                Update();
            };
            Update();

            Button cancel = new Button { Content = "Cancel", IsCancel = true };
            cancel.Click += (_, _) => Close();
            Button release = new Button { Content = "Make", MinWidth = 90, IsDefault = true };
            release.Click += (_, _) =>
            {
                string tag = (name.Text ?? "").Trim();
                // A GitHub release needs a tag, and a tag can't have spaces.
                if (gitHub.IsChecked == true && (tag.Length == 0 || tag.Contains(' ')))
                {
                    problem.Text = "A GitHub release needs a name without spaces, like v1.0.";
                    return;
                }
                if (patch.IsChecked != true && xdelta.IsChecked != true && changelog.IsChecked != true)
                {
                    problem.Text = "Tick at least one file to make.";
                    return;
                }
                _choice = new Choice(tag.Length == 0 ? null : tag, notes.Text, patch.IsChecked == true, xdelta.IsChecked == true, gitHub.IsChecked == true, changelog.IsChecked == true);
                Close();
            };
            StackPanel foot = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right, Margin = new global::Avalonia.Thickness(12) };
            foot.Children.Add(cancel);
            foot.Children.Add(release);

            Grid columns = new Grid { ColumnDefinitions = new ColumnDefinitions("*,260") };
            Grid.SetColumn(form, 0);
            Border side = new Border { Child = preview, BorderThickness = new global::Avalonia.Thickness(1, 0, 0, 0) };
            side.Bind(Border.BorderBrushProperty, side.GetResourceObservable("Editor.Border"));
            Grid.SetColumn(side, 1);
            columns.Children.Add(form);
            columns.Children.Add(side);
            ScrollViewer body = new ScrollViewer { Content = columns };
            Content = DialogSizing.Body(body, foot);
            DialogSizing.Grow(this, body);
            Opened += (_, _) => name.Focus();
        }

        private static Border Box(string caption, Control content)
        {
            StackPanel inside = new StackPanel { Spacing = 3 };
            TextBlock label = Subtle(caption);
            label.FontSize = 11;
            inside.Children.Add(label);
            inside.Children.Add(content);
            Border box = new Border { Child = inside, BorderThickness = new global::Avalonia.Thickness(1), CornerRadius = new global::Avalonia.CornerRadius(4), Padding = new global::Avalonia.Thickness(8, 6) };
            box.Bind(Border.BorderBrushProperty, box.GetResourceObservable("Editor.Border"));
            return box;
        }

        private static Border Chip(string text)
        {
            TextBlock t = new TextBlock { Text = text, FontSize = 11 };
            Border chip = new Border { Child = t, BorderThickness = new global::Avalonia.Thickness(1), CornerRadius = new global::Avalonia.CornerRadius(3), Padding = new global::Avalonia.Thickness(6, 2), Margin = new global::Avalonia.Thickness(0, 0, 6, 6) };
            chip.Bind(Border.BorderBrushProperty, chip.GetResourceObservable("Editor.Border"));
            return chip;
        }
    }
}
