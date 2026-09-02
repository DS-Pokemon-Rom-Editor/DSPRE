#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;

namespace DSPRE.Avalonia.Views.Shell
{
    /// <summary>
    /// Debug-only. Shows the update prompt exactly as a given release would produce it, reading the
    /// changelog files in the working tree so notes for a version that has not shipped yet can be
    /// checked before tagging. Nothing here contacts GitHub or installs anything.
    /// </summary>
    public class ChangelogPreviewWindow : Window
    {
        private readonly TextBox _folderBox = new() { Width = 330 };
        private readonly TextBox _versionBox = new() { Width = 120 };
        private readonly ComboBox _sectionBox = new() { Width = 170 };
        private readonly TextBlock _resolved = new() { Opacity = 0.7, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        private string _loadedFor;
        private bool _updating;

        public ChangelogPreviewWindow()
        {
            Title = "Generate Update Prompt Preview";
            Width = 620;
            Height = 250;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            _folderBox.Text = FindChangelogFolder() ?? "";
            _versionBox.Text = AppInfo.GetDSPREVersion();
            _folderBox.PropertyChanged += (_, e) => { if (e.Property.Name == "Text") Refresh(); };
            _versionBox.PropertyChanged += (_, e) => { if (e.Property.Name == "Text") Refresh(); };
            _sectionBox.SelectionChanged += (_, _) =>
            {
                if (!_updating && _sectionBox.SelectedItem is string s) _versionBox.Text = s;
            };

            var browse = new Button { Content = "Browse…" };
            browse.Click += async (_, _) =>
            {
                string picked = await DialogHelper.OpenFolder(this, "Changelogs folder");
                if (!string.IsNullOrEmpty(picked)) _folderBox.Text = picked;
            };

            var preview = new Button { Content = "Show preview", IsDefault = true };
            preview.Click += async (_, _) => await ShowPreview();

            var close = new Button { Content = "Close", IsCancel = true };
            close.Click += (_, _) => Close();

            var panel = new StackPanel { Spacing = 10, Margin = new Thickness(14) };
            panel.Children.Add(new TextBlock
            {
                Text = "Builds the release notes the same way the release workflow does, then shows the update " +
                       "prompt with them. Reads the changelog files in the working tree.",
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                Opacity = 0.8
            });
            panel.Children.Add(Row("Changelogs folder", _folderBox, browse));
            panel.Children.Add(Row("Version", _versionBox, new TextBlock { Text = "Or pick a section", VerticalAlignment = VerticalAlignment.Center }, _sectionBox));
            panel.Children.Add(_resolved);
            panel.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                Children = { preview, close }
            });
            Content = panel;

            Refresh();
        }

        private static StackPanel Row(string label, params Control[] controls)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock { Text = label, Width = 120, VerticalAlignment = VerticalAlignment.Center });
            foreach (var c in controls) row.Children.Add(c);
            return row;
        }

        private void Refresh()
        {
            if (!TryResolve(out _, out string notes, out string path, out bool sectionOnly))
            {
                _resolved.Text = "Enter a version like 2.3 or 2.2.2.1, and point at a Changelogs folder.";
                return;
            }

            if (_loadedFor != path)
            {
                _loadedFor = path;
                _updating = true;
                var items = new List<string>();
                if (File.Exists(path))
                    foreach (string line in File.ReadAllLines(path))
                        if (line.StartsWith("## ")) items.Add(line.Substring(3).Trim());
                _sectionBox.ItemsSource = items;
                _updating = false;
            }

            string file = Path.GetFileName(path);
            _resolved.Text = notes == null
                ? $"No changelog at {file}, so this release would publish with no notes."
                : $"Release {DisplayVersion(_versionBox.Text)} would use {file}" +
                  (sectionOnly ? $", section \"## {DisplayVersion(_versionBox.Text)}\" only." : ", whole file.");
        }

        private async System.Threading.Tasks.Task ShowPreview()
        {
            if (!TryResolve(out _, out string notes, out _, out _))
            {
                await DialogHelper.ShowInfo("Enter a version like 2.3 and pick a folder that holds the changelog files.",
                    "Nothing to preview");
                return;
            }
            await AppUpdater.ShowUpdatePrompt(AppInfo.GetDSPREVersion(), DisplayVersion(_versionBox.Text), notes, preview: true);
        }

        // Mirrors the "Attach changelog to release notes" step in .github/workflows/update-releases.yaml:
        // a patch release takes just its own section, anything else takes the whole file.
        private bool TryResolve(out int[] parts, out string notes, out string path, out bool sectionOnly)
        {
            parts = null;
            notes = null;
            path = null;
            sectionOnly = false;

            int[] v = ParseVersion(_versionBox.Text);
            if (v == null || !Directory.Exists(_folderBox.Text)) return false;
            parts = v;

            path = Path.Combine(_folderBox.Text, $"CHANGELOG_{v[0]}.{v[1]}_User.md");
            if (!File.Exists(path)) return true;

            string text = File.ReadAllText(path);
            if (v[2] == 0) { notes = text; return true; }

            string heading = "## " + DisplayVersion(_versionBox.Text);
            var kept = new List<string>();
            bool found = false;
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
            {
                if (!found) { if (line == heading) { found = true; kept.Add(line); } continue; }
                if (line.StartsWith("---") || line.StartsWith("## ")) break;
                kept.Add(line);
            }
            if (!found) { notes = text; return true; }

            var sb = new StringBuilder();
            foreach (string line in kept) sb.AppendLine(line);
            notes = sb.ToString();
            sectionOnly = true;
            return true;
        }

        private static int[] ParseVersion(string text)
        {
            string[] bits = (text ?? "").Trim().Split('.');
            if (bits.Length == 0 || bits.Length > 4) return null;
            var v = new int[4];
            for (int i = 0; i < 4; i++)
                if (i < bits.Length && !int.TryParse(bits[i], out v[i])) return null;
            return v;
        }

        private static string DisplayVersion(string text)
        {
            int[] v = ParseVersion(text);
            if (v == null) return text;
            if (v[3] != 0) return $"{v[0]}.{v[1]}.{v[2]}.{v[3]}";
            if (v[2] != 0) return $"{v[0]}.{v[1]}.{v[2]}";
            return $"{v[0]}.{v[1]}";
        }

        /// <summary>Walks up from the running binary to find the repository's Changelogs folder.</summary>
        private static string FindChangelogFolder()
        {
            try
            {
                var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                for (int depth = 0; dir != null && depth < 8; depth++)
                {
                    string candidate = Path.Combine(dir.FullName, "Changelogs");
                    if (Directory.Exists(candidate)) return candidate;
                    dir = dir.Parent;
                }
            }
            catch (Exception ex) { AppLogger.Warn("Couldn't locate the Changelogs folder: " + ex.Message); }
            return null;
        }
    }
}
#endif
