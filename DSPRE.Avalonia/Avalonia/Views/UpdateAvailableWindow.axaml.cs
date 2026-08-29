using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Controls.Documents;
using global::Avalonia.Layout;
using global::Avalonia.Media;

namespace DSPRE.Avalonia.Views
{
    /// <summary>
    /// The "a new version is available" prompt, showing the release notes rather than only the
    /// version numbers. Built in code because it is a handful of stacked text blocks and the
    /// Markdown has to become real controls anyway.
    /// </summary>
    public class UpdateAvailableWindow : Window
    {
        public bool Install { get; private set; }

        public UpdateAvailableWindow(string currentVersion, string availableVersion, string notes, bool preview)
        {
            Title = preview ? "Update Prompt Preview" : "New Update Available";
            Width = 660;
            Height = 540;
            MinWidth = 460;
            MinHeight = 320;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var headline = new TextBlock
            {
                Text = preview
                    ? "This is how the update prompt will look for this release."
                    : "A new DSPRE version is available.",
                FontWeight = FontWeight.SemiBold,
                FontSize = 15
            };

            var details = new TextBlock
            {
                Text = $"Installed: {currentVersion}          Available: {availableVersion}",
                Opacity = 0.7,
                Margin = new Thickness(0, 2, 0, 0)
            };

            var body = new StackPanel { Spacing = 6 };
            RenderMarkdown(body, string.IsNullOrWhiteSpace(notes)
                ? "Release notes are not available for this version."
                : notes);

            var scroller = new ScrollViewer
            {
                Content = new Border { Padding = new Thickness(10), Child = body },
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            if (!preview)
            {
                var install = new Button { Content = "Install now", IsDefault = true };
                install.Click += (_, _) => { Install = true; Close(); };
                buttons.Children.Add(install);
            }

            var close = new Button { Content = preview ? "Close" : "Not now", IsCancel = true };
            close.Click += (_, _) => Close();
            buttons.Children.Add(close);

            var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"), Margin = new Thickness(14) };
            Grid.SetRow(headline, 0);
            Grid.SetRow(details, 1);
            Grid.SetRow(scroller, 2);
            Grid.SetRow(buttons, 3);
            scroller.Margin = new Thickness(0, 10, 0, 10);
            grid.Children.Add(headline);
            grid.Children.Add(details);
            grid.Children.Add(scroller);
            grid.Children.Add(buttons);
            Content = grid;
        }

        private static readonly Regex LinkPattern = new(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);
        private static readonly Regex BoldPattern = new(@"\*\*([^*]+)\*\*", RegexOptions.Compiled);
        private static readonly Regex ItalicPattern = new(@"(?<!\*)\*([^*]+)\*(?!\*)", RegexOptions.Compiled);

        /// <summary>
        /// Renders the small slice of Markdown the changelogs use: headings, bullets, rules, bold,
        /// italics and links. Not a general parser, and deliberately not a dependency.
        /// </summary>
        private static void RenderMarkdown(StackPanel host, string markdown)
        {
            string pending = null;
            bool pendingBullet = false;

            void Flush()
            {
                if (pending == null) return;
                host.Children.Add(Paragraph(pending, pendingBullet));
                pending = null;
                pendingBullet = false;
            }

            foreach (string raw in markdown.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();

                if (line.Length == 0) { Flush(); continue; }

                if (line.StartsWith("---"))
                {
                    Flush();
                    host.Children.Add(new Border
                    {
                        Height = 1,
                        Background = Brushes.Gray,
                        Opacity = 0.35,
                        Margin = new Thickness(0, 6, 0, 6)
                    });
                    continue;
                }

                if (line.StartsWith("### ")) { Flush(); host.Children.Add(Heading(line.Substring(4), 14)); continue; }
                if (line.StartsWith("## "))  { Flush(); host.Children.Add(Heading(line.Substring(3), 17)); continue; }
                if (line.StartsWith("# "))   { Flush(); host.Children.Add(Heading(line.Substring(2), 20)); continue; }

                if (line.StartsWith("- ") || line.StartsWith("* "))
                {
                    Flush();
                    pending = line.Substring(2);
                    pendingBullet = true;
                    continue;
                }

                // A wrapped continuation of whatever block is open.
                pending = pending == null ? line : pending + " " + line;
            }
            Flush();
        }

        private static TextBlock Heading(string text, double size) => new()
        {
            Text = text,
            FontSize = size,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 2)
        };

        private static Control Paragraph(string text, bool bullet)
        {
            var block = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap };
            AddInlines(block, text);
            if (!bullet) return block;

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("14,*") };
            var dot = new TextBlock { Text = "•", VerticalAlignment = VerticalAlignment.Top };
            Grid.SetColumn(dot, 0);
            Grid.SetColumn(block, 1);
            row.Children.Add(dot);
            row.Children.Add(block);
            return row;
        }

        private static void AddInlines(SelectableTextBlock block, string text)
        {
            // Links first: they carry their own click behaviour, the rest is plain styling.
            var pieces = new List<(string text, string url, FontStyle style, FontWeight weight)>();
            int pos = 0;
            foreach (Match m in LinkPattern.Matches(text))
            {
                if (m.Index > pos) AddStyled(pieces, text.Substring(pos, m.Index - pos));
                pieces.Add((m.Groups[1].Value, m.Groups[2].Value, FontStyle.Normal, FontWeight.Normal));
                pos = m.Index + m.Length;
            }
            if (pos < text.Length) AddStyled(pieces, text.Substring(pos));

            string firstUrl = null;
            foreach (var (t, url, style, weight) in pieces)
            {
                if (url != null)
                {
                    block.Inlines.Add(new Run(t) { Foreground = Brushes.DodgerBlue, TextDecorations = TextDecorations.Underline });
                    firstUrl ??= url;
                    continue;
                }
                block.Inlines.Add(new Run(t) { FontStyle = style, FontWeight = weight });
            }

            // A Run has no click event, so the line itself opens its link on double-tap. The text stays
            // selectable either way, so the address can always just be copied out.
            if (firstUrl != null)
            {
                block.Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Hand);
                block.DoubleTapped += (_, _) => OpenUrl(firstUrl);
            }
        }

        private static void AddStyled(List<(string, string, FontStyle, FontWeight)> pieces, string segment)
        {
            int pos = 0;
            while (pos < segment.Length)
            {
                Match bold = BoldPattern.Match(segment, pos);
                Match italic = ItalicPattern.Match(segment, pos);
                Match first = null;
                bool isBold = false;
                if (bold.Success && (!italic.Success || bold.Index <= italic.Index)) { first = bold; isBold = true; }
                else if (italic.Success) { first = italic; }

                if (first == null)
                {
                    pieces.Add((segment.Substring(pos), null, FontStyle.Normal, FontWeight.Normal));
                    return;
                }
                if (first.Index > pos)
                    pieces.Add((segment.Substring(pos, first.Index - pos), null, FontStyle.Normal, FontWeight.Normal));
                pieces.Add((first.Groups[1].Value, null,
                    isBold ? FontStyle.Normal : FontStyle.Italic,
                    isBold ? FontWeight.Bold : FontWeight.Normal));
                pos = first.Index + first.Length;
            }
        }

        private static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { AppLogger.Warn("Couldn't open " + url + ": " + ex.Message); }
        }
    }
}
