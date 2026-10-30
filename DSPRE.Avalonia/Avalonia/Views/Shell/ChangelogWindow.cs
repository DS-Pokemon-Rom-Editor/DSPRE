using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DSPRE.Tracking;
using static DSPRE.Avalonia.Views.Shell.ChangeText;

namespace DSPRE.Avalonia.Views.Shell
{
    /// <summary>
    /// Writes a changelog from the changes folder: options on the left, the text on the right, updated as options
    /// change. The options are kept in the changes folder for everyone on the hack.
    /// </summary>
    public sealed class ChangelogWindow : Window
    {
        private readonly ChangeTracker _tracker;
        private readonly ChangelogOptions _options;
        private readonly string _saved;
        private readonly TextBox _output = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas,monospace"),
            FontSize = 12,
        };
        private readonly TextBlock _status = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        private readonly StackPanel _sections = new StackPanel { Spacing = 4 };
        private List<ChangelogSubject> _subjects;
        private (ChangelogSince, string, int, bool) _builtFor;
        private CancellationTokenSource _pending;
        private readonly SemaphoreSlim _reading = new SemaphoreSlim(1, 1);
        private string _text;
        private List<(string File, byte[] Png)> _pictures = new List<(string, byte[])>();

        public static async Task ShowAsync(Window owner, ChangeTracker tracker)
        {
            IReadOnlyList<string> tags = await GitCli.TagsAsync(tracker.Folder);
            ChangelogWindow win = new ChangelogWindow(tracker, tags);
            await win.ShowDialog(owner);
        }

        /// <summary>A release page: its name, its notes, then the changelog with the saved options.</summary>
        public static async Task<(string Text, List<(string File, byte[] Png)> Pictures)> ForReleaseAsync(ChangeTracker tracker, string name, bool pictures, string notes)
        {
            (string text, List<(string File, byte[] Png)> files) = await ChangelogFor(tracker, name, pictures);
            if (string.IsNullOrWhiteSpace(notes)) return (text, files);
            int after = text.IndexOf("\n\n", StringComparison.Ordinal);
            return (after < 0 ? text : text.Insert(after + 2, notes.Trim().Replace("\r\n", "\n") + "\n\n---\n\n"), files);
        }

        /// <summary>The page without its title line, for a GitHub release, which shows the title itself.</summary>
        public static string WithoutTitle(string text) =>
            text.StartsWith("# ", StringComparison.Ordinal) && text.IndexOf("\n\n", StringComparison.Ordinal) is int at and >= 0 ? text.Substring(at + 2) : text;

        /// <summary>The first line of a release changelog.</summary>
        public static string ReleaseStart(ChangeTracker tracker, IReadOnlyList<string> tags, string name)
        {
            ChangelogOptions o = ChangelogOptions.Load(tracker.Folder);
            return o.Since == ChangelogSince.Release && tags.Contains(o.Release) && o.Release != name ? "Changes since " + o.Release : "Changes since the original game";
        }

        private static async Task<(string Text, List<(string File, byte[] Png)> Pictures)> ChangelogFor(ChangeTracker tracker, string name, bool pictures)
        {
            ChangelogOptions o = ChangelogOptions.Load(tracker.Folder);
            IReadOnlyList<string> tags = await GitCli.TagsAsync(tracker.Folder);
            o.Title = string.IsNullOrWhiteSpace(name) ? o.Title : name;
            // Uses the window's start, except a deleted release or this one, which start from the original game.
            if (o.Since == ChangelogSince.Uncommitted || o.Since == ChangelogSince.Release && (!tags.Contains(o.Release) || o.Release == name))
                o.Since = ChangelogSince.Original;
            if (!pictures) o.Images = ChangelogImages.None;
            List<ChangelogSubject> subjects = await Changelog.BuildAsync(tracker, o, o.Images != ChangelogImages.None);
            string text = ChangelogWriter.Write(subjects, o, PictureFolder(name), out List<(string, byte[])> files);
            return (text, files);
        }

        /// <summary>Where a changelog's pictures go, next to it.</summary>
        public static string PictureFolder(string name) => ChangesJson.Slug(string.IsNullOrWhiteSpace(name) ? "changelog" : name + " changelog");

        private ChangelogWindow(ChangeTracker tracker, IReadOnlyList<string> tags)
        {
            _tracker = tracker;
            _options = ChangelogOptions.Load(tracker.Folder);
            _saved = Serialized(_options);
            if (_options.Since == ChangelogSince.Release && !tags.Contains(_options.Release)) _options.Since = ChangelogSince.Original;
            Title = "Changelog";
            Width = 1100;
            Height = 760;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            StackPanel form = new StackPanel { Spacing = 14, Margin = new global::Avalonia.Thickness(12), Width = 300 };

            TextBox title = new TextBox { Text = _options.Title, PlaceholderText = "Changelog" };
            title.TextChanged += (_, _) => Changed(() => _options.Title = title.Text);
            form.Children.Add(Group("Title", title));

            ComboBox release = new ComboBox { ItemsSource = tags, SelectedItem = _options.Release ?? tags.FirstOrDefault(), IsEnabled = tags.Count > 0, MinWidth = 140 };
            RadioButton fromOriginal = Radio("since", "The original game", _options.Since == ChangelogSince.Original);
            RadioButton fromRelease = Radio("since", "Release", _options.Since == ChangelogSince.Release);
            fromRelease.IsEnabled = tags.Count > 0;
            ToolTip.SetTip(fromRelease, tags.Count > 0 ? null : "No releases yet");
            ToolTip.SetShowOnDisabled(fromRelease, true);
            RadioButton fromCommit = Radio("since", "The last commit", _options.Since == ChangelogSince.Uncommitted);
            fromCommit.IsEnabled = GitCli.IsRepository(tracker.Folder);
            // Only the newly ticked radio acts. The old one is still ticked when this fires.
            foreach ((RadioButton r, ChangelogSince since) in new[] { (fromOriginal, ChangelogSince.Original), (fromRelease, ChangelogSince.Release), (fromCommit, ChangelogSince.Uncommitted) })
                r.IsCheckedChanged += (_, _) =>
                {
                    if (r.IsChecked != true) return;
                    _options.Since = since;
                    _options.Release = release.SelectedItem as string;
                    Changed(null);
                };
            release.SelectionChanged += (_, _) => Changed(() => _options.Release = release.SelectedItem as string);
            StackPanel releaseRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            releaseRow.Children.Add(fromRelease);
            releaseRow.Children.Add(release);
            form.Children.Add(Group("Changes since", fromOriginal, releaseRow, fromCommit));

            RadioButton names = Radio("detail", "Names only", _options.Detail == ChangelogDetail.Names);
            RadioButton areas = Radio("detail", "What was changed", _options.Detail == ChangelogDetail.Areas);
            RadioButton all = Radio("detail", "Every change", _options.Detail == ChangelogDetail.All);
            foreach ((RadioButton r, ChangelogDetail d) in new[] { (names, ChangelogDetail.Names), (areas, ChangelogDetail.Areas), (all, ChangelogDetail.All) })
                r.IsCheckedChanged += (_, _) => { if (r.IsChecked == true) Changed(() => _options.Detail = d); };
            CheckBox before = new CheckBox { Content = "Show what it was before", IsChecked = _options.ShowBefore };
            before.IsCheckedChanged += (_, _) => Changed(() => _options.ShowBefore = before.IsChecked == true);
            form.Children.Add(Group("Detail", names, areas, all, before));

            RadioButton bySubject = Radio("group", "Pokémon, trainer, map", _options.GroupBy == ChangelogGrouping.Subject);
            RadioButton byEditor = Radio("group", "Editor", _options.GroupBy == ChangelogGrouping.Editor);
            CheckBox counts = new CheckBox { Content = "Counts only", IsChecked = _options.CountsOnly, IsEnabled = _options.GroupBy == ChangelogGrouping.Editor };
            foreach ((RadioButton r, ChangelogGrouping grouping) in new[] { (bySubject, ChangelogGrouping.Subject), (byEditor, ChangelogGrouping.Editor) })
                r.IsCheckedChanged += (_, _) =>
                {
                    if (r.IsChecked != true) return;
                    _options.GroupBy = grouping;
                    counts.IsEnabled = grouping == ChangelogGrouping.Editor;
                    Changed(null);
                };
            counts.IsCheckedChanged += (_, _) => Changed(() => _options.CountsOnly = counts.IsChecked == true);
            form.Children.Add(Group("Group by", bySubject, byEditor, counts));

            ComboBox pictures = new ComboBox
            {
                ItemsSource = new[] { "None", "After only", "Before and after" },
                SelectedIndex = (int)_options.Images,
                MinWidth = 180,
            };
            pictures.SelectionChanged += (_, _) => Changed(() => _options.Images = (ChangelogImages)Math.Max(0, pictures.SelectedIndex));
            CheckBox lines = new CheckBox { Content = "Show changed text lines", IsChecked = _options.TextLines };
            lines.IsCheckedChanged += (_, _) => Changed(() => _options.TextLines = lines.IsChecked == true);
            form.Children.Add(Group("Pictures", pictures));
            form.Children.Add(Group("Text", lines));

            Button pick = new Button { Content = "Pick records…" };
            ToolTip.SetTip(pick, "Leave records out");
            pick.Click += async (_, _) =>
            {
                if (await RecordPicker.ShowAsync(this, Changelog.RecordList(_tracker), _options.Hidden)) Changed(null);
            };
            form.Children.Add(Group("Include", _sections, pick));

            Button copy = new Button { Content = "Copy" };
            copy.Click += async (_, _) =>
            {
                if (Clipboard == null || _text == null) return;
                await Clipboard.SetTextAsync(_text);
                KeepOptions();
                _status.Text = "Copied";
            };
            Button save = new Button { Content = "Save…", MinWidth = 90, IsDefault = true };
            save.Click += async (_, _) => await SaveAsync();
            Button close = new Button { Content = "Close", IsCancel = true };
            close.Click += (_, _) => Close();
            DockPanel foot = new DockPanel { Margin = new global::Avalonia.Thickness(12), LastChildFill = true };
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            buttons.Children.Add(copy);
            buttons.Children.Add(save);
            buttons.Children.Add(close);
            DockPanel.SetDock(buttons, Dock.Right);
            foot.Children.Add(buttons);
            foot.Children.Add(_status);
            _status.Bind(TextBlock.ForegroundProperty, _status.GetResourceObservable("Editor.Subtle"));

            Grid body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            ScrollViewer left = new ScrollViewer { Content = form, HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
            Border leftBorder = new Border { Child = left, BorderThickness = new global::Avalonia.Thickness(0, 0, 1, 0) };
            leftBorder.Bind(Border.BorderBrushProperty, leftBorder.GetResourceObservable("Editor.Border"));
            Grid.SetColumn(leftBorder, 0);
            Border right = new Border { Child = _output, Margin = new global::Avalonia.Thickness(12) };
            Grid.SetColumn(right, 1);
            body.Children.Add(leftBorder);
            body.Children.Add(right);

            Content = DialogSizing.Body(body, foot);
            DialogSizing.Grow(this, body);
            Opened += (_, _) => Changed(null);
            Closing += (_, _) => _pending?.Cancel();
        }

        private static RadioButton Radio(string group, string text, bool on) => new RadioButton { GroupName = group, Content = text, IsChecked = on };

        private static StackPanel Group(string heading, params Control[] controls)
        {
            StackPanel group = new StackPanel { Spacing = 4 };
            group.Children.Add(Header(heading));
            foreach (Control c in controls) group.Children.Add(c);
            return group;
        }

        /// <summary>Applies a change of option and writes the changelog again, reading the folder only when what it reads changed.</summary>
        private async void Changed(Action set)
        {
            set?.Invoke();
            _pending?.Cancel();
            CancellationTokenSource cts = _pending = new CancellationTokenSource();
            try
            {
                await Task.Delay(150, cts.Token);
                bool pictures = _options.Images != ChangelogImages.None;
                (ChangelogSince, string, int, bool) reads = (_options.Since, _options.Release, string.Join("|", _options.Hidden.OrderBy(h => h, StringComparer.Ordinal)).GetHashCode(), pictures);
                if (_subjects == null || reads != _builtFor)
                {
                    _status.Text = "Reading the changes…";
                    // One read at a time: they share the game's name lists and record readers.
                    List<ChangelogSubject> subjects;
                    await _reading.WaitAsync();
                    try
                    {
                        if (cts.IsCancellationRequested) return;
                        subjects = await Changelog.BuildAsync(_tracker, _options.Copy(), pictures);
                    }
                    finally
                    {
                        _reading.Release();
                    }
                    if (cts.IsCancellationRequested) return;
                    _subjects = subjects;
                    _builtFor = reads;
                    Sections();
                }
                _text = ChangelogWriter.Write(_subjects, _options, PictureFolder(_options.Title), out _pictures);
                _output.Text = _text;
                int records = _subjects.Where(s => !_options.Off.Contains(s.Section)).SelectMany(s => s.Records).Distinct().Count();
                _status.Text = Plural.Of(records, "record") + (_pictures.Count > 0 ? ", " + Plural.Of(_pictures.Count, "picture") : "");
            }
            catch (TaskCanceledException)
            {
                // A newer option change took over.
            }
            catch (Exception ex)
            {
                CrashReporter.LogHandled(ex);
                _status.Text = "The changelog couldn't be written: " + ex.Message;
            }
        }

        private static readonly string[] DetailNames = { "Names only", "What was changed", "Every change" };

        /// <summary>A row per section the changes touch, to leave it out or give it its own detail.</summary>
        private void Sections()
        {
            _sections.Children.Clear();
            foreach (IGrouping<ChangelogSection, ChangelogSubject> section in _subjects.GroupBy(s => s.Section).OrderBy(g => g.Key))
            {
                ChangelogSection key = section.Key;
                CheckBox on = new CheckBox { Content = Changelog.SectionTitle(key) + " (" + section.Count() + ")", IsChecked = !_options.Off.Contains(key), MinWidth = 160 };
                on.IsCheckedChanged += (_, _) => Changed(() =>
                {
                    if (on.IsChecked == true) _options.Off.Remove(key);
                    else _options.Off.Add(key);
                });
                ComboBox detail = new ComboBox
                {
                    ItemsSource = new[] { "As set above" }.Concat(DetailNames).ToList(),
                    SelectedIndex = _options.SectionDetail.TryGetValue(key, out ChangelogDetail d) ? (int)d + 1 : 0,
                    FontSize = 12,
                    MinWidth = 120,
                };
                ToolTip.SetTip(detail, "Detail for this section");
                detail.SelectionChanged += (_, _) => Changed(() =>
                {
                    if (detail.SelectedIndex <= 0) _options.SectionDetail.Remove(key);
                    else _options.SectionDetail[key] = (ChangelogDetail)(detail.SelectedIndex - 1);
                });
                DockPanel row = new DockPanel();
                DockPanel.SetDock(detail, Dock.Right);
                row.Children.Add(detail);
                row.Children.Add(on);
                _sections.Children.Add(row);
            }
        }

        private async Task SaveAsync()
        {
            if (_text == null) return;
            string suggested = PictureFolder(_options.Title) + ".md";
            string path = await DialogHelper.SaveFileAsync(this, "Save the changelog", new[] { new FilePickerFileType("Markdown") { Patterns = new[] { "*.md" } } }, suggested);
            if (path == null) return;
            try
            {
                string text = Rewrite(_text, PictureFolder(_options.Title), Path.GetFileNameWithoutExtension(path));
                PlainFiles.WriteBytes(path, System.Text.Encoding.UTF8.GetBytes(text));
                SavePictures(Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path)), _pictures);
                KeepOptions();
                SaveNotice.Show("Saved " + Path.GetFileName(path) + (_pictures.Count > 0 ? " and " + Plural.Of(_pictures.Count, "picture") : ""));
            }
            catch (Exception ex)
            {
                CrashReporter.LogHandled(ex);
                await DialogHelper.ShowErrorAsync("The changelog wasn't saved: " + ex.Message, "Changelog", this);
            }
        }

        internal static void SavePictures(string folder, List<(string File, byte[] Png)> pictures)
        {
            if (pictures.Count == 0) return;
            Directory.CreateDirectory(folder);
            foreach ((string file, byte[] png) in pictures) PlainFiles.WriteBytes(Path.Combine(folder, file), png);
        }

        /// <summary>The changelog with its pictures pointed at the folder named after the saved file.</summary>
        internal static string Rewrite(string text, string from, string to) =>
            from == to ? text : text.Replace("(" + Uri.EscapeDataString(from) + "/", "(" + Uri.EscapeDataString(to) + "/");

        private void KeepOptions()
        {
            if (Serialized(_options) == _saved) return;
            try { _options.Save(_tracker.Folder); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { AppLogger.Warn("Changelog: options not kept: " + ex.Message); }
        }

        private static string Serialized(ChangelogOptions o) => System.Text.Json.JsonSerializer.Serialize(o);

        /// <summary>A tick per record, to leave records out of the changelog.</summary>
        private sealed class RecordPicker : Window
        {
            private bool _ok;

            public static async Task<bool> ShowAsync(Window owner, List<(string Key, string Name)> records, HashSet<string> hidden)
            {
                RecordPicker win = new RecordPicker(records, hidden);
                await win.ShowDialog(owner);
                return win._ok;
            }

            private RecordPicker(List<(string Key, string Name)> records, HashSet<string> hidden)
            {
                Title = "Pick records";
                Width = 520;
                Height = 620;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                StackPanel list = new StackPanel { Spacing = 2, Margin = new global::Avalonia.Thickness(12) };
                List<(CheckBox Box, string Key)> boxes = new List<(CheckBox, string)>();
                foreach ((string key, string name) in records)
                {
                    CheckBox box = new CheckBox { Content = name, IsChecked = !hidden.Contains(key) };
                    ToolTip.SetTip(box, key);
                    boxes.Add((box, key));
                    list.Children.Add(box);
                }
                Button cancel = new Button { Content = "Cancel", IsCancel = true };
                cancel.Click += (_, _) => Close();
                Button ok = new Button { Content = "OK", MinWidth = 90, IsDefault = true };
                ok.Click += (_, _) =>
                {
                    hidden.Clear();
                    foreach ((CheckBox box, string key) in boxes)
                        if (box.IsChecked != true) hidden.Add(key);
                    _ok = true;
                    Close();
                };
                StackPanel foot = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right, Margin = new global::Avalonia.Thickness(12) };
                foot.Children.Add(cancel);
                foot.Children.Add(ok);
                ScrollViewer body = new ScrollViewer { Content = list };
                Content = DialogSizing.Body(body, foot, Subtle("Unticked records are left out."));
                DialogSizing.Grow(this, body);
            }
        }
    }
}
