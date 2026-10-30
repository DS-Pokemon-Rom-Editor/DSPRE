using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DSPRE.Tracking
{
    /// <summary>Writes subjects as a Markdown changelog. Plain words only: no symbols or emoji.</summary>
    public static class ChangelogWriter
    {
        /// <summary>The changelog, and the pictures it links to by their file name inside <paramref name="pictureFolder"/>.</summary>
        public static string Write(IReadOnlyList<ChangelogSubject> subjects, ChangelogOptions o, string pictureFolder, out List<(string File, byte[] Png)> pictures)
        {
            pictures = new List<(string, byte[])>();
            Builder b = new Builder();
            b.Heading(1, string.IsNullOrWhiteSpace(o.Title) ? "Changelog" : o.Title.Trim());
            b.Paragraph(o.Since switch
            {
                ChangelogSince.Release when !string.IsNullOrEmpty(o.Release) => "Changes since " + o.Release + ".",
                ChangelogSince.Uncommitted => "Changes since the last commit.",
                _ => "Changes since the original game.",
            });
            List<ChangelogSubject> shown = subjects.Where(s => !o.Off.Contains(s.Section)).ToList();
            if (shown.Count == 0)
            {
                b.Paragraph("Nothing changed.");
                return b.ToString();
            }
            if (o.GroupBy == ChangelogGrouping.Editor) ByEditor(b, shown, o, pictureFolder, pictures);
            else BySubject(b, shown, o, pictureFolder, pictures);
            return b.ToString();
        }

        private static void BySubject(Builder b, List<ChangelogSubject> subjects, ChangelogOptions o, string folder, List<(string, byte[])> pictures)
        {
            foreach (IGrouping<ChangelogSection, ChangelogSubject> section in subjects.GroupBy(s => s.Section))
            {
                ChangelogDetail detail = o.DetailOf(section.Key);
                foreach (IGrouping<ChangelogStatus, ChangelogSubject> status in section.GroupBy(s => s.Status).OrderBy(g => g.Key))
                {
                    b.Heading(2, Heading(section.Key, status.Key));
                    if (detail == ChangelogDetail.Names)
                    {
                        b.Paragraph(string.Join(", ", status.Select(s => s.Name)));
                        continue;
                    }
                    foreach (ChangelogSubject s in status) Subject(b, s, s.Areas, detail, o, folder, pictures);
                }
            }
        }

        private static void ByEditor(Builder b, List<ChangelogSubject> subjects, ChangelogOptions o, string folder, List<(string, byte[])> pictures)
        {
            IEnumerable<IGrouping<string, (ChangelogSubject Subject, ChangelogArea Area)>> editors = subjects
                .SelectMany(s => s.Areas.Select(a => (Subject: s, Area: a)))
                .GroupBy(x => x.Area.Editor)
                .OrderBy(g => g.Min(x => x.Subject.Section));
            foreach (IGrouping<string, (ChangelogSubject Subject, ChangelogArea Area)> editor in editors)
            {
                b.Heading(2, editor.Key);
                List<IGrouping<ChangelogSubject, ChangelogArea>> bySubject = editor.GroupBy(x => x.Subject, x => x.Area).ToList();
                if (o.CountsOnly || o.Detail == ChangelogDetail.Names)
                {
                    string names = string.Join(", ", bySubject.Select(g => g.Key.Name));
                    b.Paragraph(o.CountsOnly ? bySubject.Count + " changed: " + names : names);
                    continue;
                }
                foreach (IGrouping<ChangelogSubject, ChangelogArea> s in bySubject)
                    Subject(b, s.Key, s.ToList(), o.DetailOf(s.Key.Section), o, folder, pictures);
            }
        }

        private static void Subject(Builder b, ChangelogSubject s, IReadOnlyList<ChangelogArea> areas, ChangelogDetail detail, ChangelogOptions o,
            string folder, List<(string, byte[])> pictures)
        {
            List<ChangelogArea> shown = areas.Where(a => a.Lines.Count > 0 || a.Count > 0).ToList();
            // A subject that is only a count, like script files, gets one line.
            if (shown.Count == 1 && shown[0].Count > 0)
            {
                b.Heading(3, s.Name);
                b.Item(Counted(shown[0]));
                return;
            }
            b.Heading(3, s.Name);
            if (detail == ChangelogDetail.Areas || shown.Count == 0)
            {
                if (shown.Count > 0) b.Paragraph(string.Join(", ", shown.Select(a => a.Name)));
                return;
            }
            foreach (ChangelogArea a in shown)
            {
                b.Label(a.Name);
                if (a.Count > 0)
                {
                    b.Item(Counted(a));
                    continue;
                }
                if (a.IsText && !o.TextLines)
                {
                    b.Item(a.Lines.Count + (a.Lines.Count == 1 ? " line changed" : " lines changed"));
                    continue;
                }
                foreach (ChangelogLine line in a.Lines) Line(b, line, o, folder, pictures, a.IsText);
            }
        }

        private static string Counted(ChangelogArea a)
        {
            List<string> parts = a.Counts.Select(c => c.Value + " " + (c.Value == 1 ? c.Key : Plural(c.Key))).ToList();
            string text = (parts.Count > 1 ? string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1] : parts.FirstOrDefault() ?? "") + " changed";
            return a.Places.Count > 0 ? text + ": " + string.Join(", ", a.Places) : text;
        }

        private static string Plural(string noun) =>
            noun.EndsWith("ix", StringComparison.Ordinal) ? noun.Substring(0, noun.Length - 2) + "ices"
            : noun.EndsWith("s", StringComparison.Ordinal) ? noun + "es"
            : noun.EndsWith("y", StringComparison.Ordinal) ? noun.Substring(0, noun.Length - 1) + "ies"
            : noun + "s";

        private static void Line(Builder b, ChangelogLine line, ChangelogOptions o, string folder, List<(string, byte[])> pictures, bool quoted)
        {
            if (line.Picture != null && o.Images != ChangelogImages.None)
            {
                if (line.Text != null) b.Item(line.Text);
                List<(string File, string Alt)> files = new List<(string, string)>();
                if (o.Images == ChangelogImages.Both && line.Picture.Before != null) files.Add(Save(line.Picture.Name + "-before.png", line.Picture.Before, pictures));
                if (line.Picture.After != null) files.Add(Save(line.Picture.Name + (o.Images == ChangelogImages.Both ? "-after.png" : ".png"), line.Picture.After, pictures));
                b.Pictures(files.Select(f => (folder + "/" + f.File, f.Alt)).ToList());
                return;
            }
            if (line.Text != null)
            {
                b.Item(line.Text);
                return;
            }
            // Game text is quoted, so a "to" inside it can't be misread.
            string was = quoted ? "\"" + line.Was + "\"" : line.Was, now = quoted ? "\"" + line.Now + "\"" : line.Now;
            b.Item(o.ShowBefore ? line.Label + ": " + was + " to " + now : line.Label + ": " + now);
        }

        private static (string File, string Alt) Save(string file, byte[] png, List<(string, byte[])> pictures)
        {
            string name = file;
            for (int n = 2; pictures.Any(p => p.Item1 == name); n++) name = file.Insert(file.Length - 4, "-" + n);
            pictures.Add((name, png));
            string alt = name.EndsWith("-before.png", StringComparison.Ordinal) ? "Before" : name.EndsWith("-after.png", StringComparison.Ordinal) ? "After" : "Picture";
            return (name, alt);
        }

        private static string Heading(ChangelogSection section, ChangelogStatus status)
        {
            string title = Changelog.SectionTitle(section);
            bool records = section is ChangelogSection.Pokemon or ChangelogSection.Moves or ChangelogSection.Items or ChangelogSection.Trainers
                or ChangelogSection.Trades or ChangelogSection.Maps;
            string lower = section == ChangelogSection.Pokemon ? title : char.ToLowerInvariant(title[0]) + title.Substring(1);
            if (!records) return status == ChangelogStatus.Modified ? title : (status == ChangelogStatus.New ? "New " : "Removed ") + lower;
            return (status == ChangelogStatus.New ? "New " : status == ChangelogStatus.Removed ? "Removed " : "Modified ") + lower;
        }

        /// <summary>The changelog as Markdown: headings, labels, list items, pictures.</summary>
        private sealed class Builder
        {
            private readonly StringBuilder _sb = new StringBuilder();

            public void Heading(int level, string text)
            {
                Blank();
                _sb.Append('#', level).Append(' ').Append(text).Append("\n\n");
            }

            public void Paragraph(string text) => _sb.Append(text).Append("\n\n");

            public void Label(string text)
            {
                // Markdown would read a line right after a list item as part of that item.
                Blank();
                _sb.Append(text).Append(":\n");
            }

            public void Item(string text) => _sb.Append("- ").Append(text).Append('\n');

            public void Pictures(List<(string Path, string Alt)> files)
            {
                if (files.Count == 0) return;
                _sb.Append(string.Join(" ", files.Select(f => "![" + f.Alt + "](" + Uri.EscapeDataString(f.Path).Replace("%2F", "/") + ")"))).Append("\n\n");
            }

            private void Blank()
            {
                if (_sb.Length > 0 && !_sb.ToString().EndsWith("\n\n", StringComparison.Ordinal)) _sb.Append('\n');
            }

            public override string ToString()
            {
                string text = _sb.ToString();
                while (text.Contains("\n\n\n", StringComparison.Ordinal)) text = text.Replace("\n\n\n", "\n\n");
                return text.Trim() + "\n";
            }
        }
    }
}
