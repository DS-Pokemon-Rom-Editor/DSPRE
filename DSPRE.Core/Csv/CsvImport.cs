using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.Csv
{
    public enum CsvIssueKind { Error, Warning }

    /// <summary>A suggested value for one cell.</summary>
    public sealed class CsvFix
    {
        public CsvFix(int column, string text, string label) { Column = column; Text = text; Label = label; }
        public int Column { get; }
        public string Text { get; }
        public string Label { get; }
    }

    public sealed class CsvIssue
    {
        public CsvIssue(int column, CsvIssueKind kind, string message, IReadOnlyList<CsvFix> fixes)
        { Column = column; Kind = kind; Message = message; Fixes = fixes ?? Array.Empty<CsvFix>(); }

        /// <summary>The cell the issue is about, or -1 for the whole row.</summary>
        public int Column { get; }
        public CsvIssueKind Kind { get; }
        public string Message { get; }
        public IReadOnlyList<CsvFix> Fixes { get; }
        internal string Key => Column + "\n" + Message;
    }

    /// <summary>One data row of an import, with what checking it found.</summary>
    public sealed class CsvRecord
    {
        private readonly HashSet<string> _kept = new HashSet<string>();

        internal CsvRecord(int index, int line, string[] cells) { Index = index; Line = line; Cells = cells; }

        /// <summary>The row's place among the file's rows, from 0.</summary>
        public int Index { get; }
        public int Line { get; }
        /// <summary>The cells in the importer's column order.</summary>
        public string[] Cells { get; }
        /// <summary>What the row is about, such as "#25 Pikachu".</summary>
        public string Label { get; set; } = "";
        public bool Skipped { get; set; }
        public List<CsvIssue> Issues { get; } = new List<CsvIssue>();
        /// <summary>The importer's parsed result. Only read from an accepted row.</summary>
        public object Value { get; set; }

        public bool HasErrors => Issues.Any(i => i.Kind == CsvIssueKind.Error);
        public bool IsKept(CsvIssue issue) => issue.Kind == CsvIssueKind.Warning && _kept.Contains(issue.Key);
        public void Keep(CsvIssue issue, bool keep)
        {
            if (issue.Kind != CsvIssueKind.Warning) return;
            if (keep) _kept.Add(issue.Key); else _kept.Remove(issue.Key);
        }
        public bool IsResolved => Skipped || Issues.All(IsKept);
        public bool IsAccepted => !Skipped && !HasErrors;
    }

    public sealed class CsvChange
    {
        public CsvChange(string record, string field, string old, string now) { Record = record; Field = field; Old = old; New = now; }
        public string Record { get; }
        public string Field { get; }
        public string Old { get; }
        public string New { get; }
    }

    /// <summary>Name lookup for a cell, such as the ROM's moves. Numbers also work.</summary>
    public sealed class CsvNames
    {
        private readonly Dictionary<string, List<int>> _byKey = new Dictionary<string, List<int>>();
        private readonly IReadOnlyList<string> _names;
        private readonly bool _numbers;

        /// <param name="what">The singular noun for messages, such as "move".</param>
        /// <param name="aliases">Other names an entry may go by, such as an older export's spelling.</param>
        /// <param name="numbers">Whether a bare number picks the entry at that place.</param>
        public CsvNames(IReadOnlyList<string> names, string what, IEnumerable<(int Index, string Name)> aliases = null, bool numbers = true)
        {
            _names = names;
            What = what;
            _numbers = numbers;
            for (int i = 0; i < names.Count; i++) Add(names[i], i);
            if (aliases != null) foreach ((int index, string name) in aliases) Add(name, index);
        }

        private void Add(string name, int index)
        {
            string key = Key(name);
            if (key.Length == 0) return;
            if (!_byKey.TryGetValue(key, out List<int> list)) _byKey[key] = list = new List<int>();
            if (!list.Contains(index)) list.Add(index);
        }

        public string What { get; }
        public int Count => _names.Count;
        public string this[int index] => index >= 0 && index < _names.Count ? _names[index] : $"{What} {index}";

        /// <summary>Finds the entry a cell names. When several share the name, <paramref name="prefer"/> wins if it
        /// is one of them, otherwise the first does and <paramref name="ambiguous"/> is set.</summary>
        public bool TryFind(string text, int prefer, out int index, out bool ambiguous)
        {
            ambiguous = false;
            index = -1;
            if (_byKey.TryGetValue(Key(text), out List<int> list))
            {
                index = list.Contains(prefer) ? prefer : list[0];
                ambiguous = list.Count > 1 && !list.Contains(prefer);
                return true;
            }
            string t = (text ?? "").Trim().TrimStart('#');
            if (_numbers && int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n >= 0 && n < _names.Count)
            {
                index = n;
                return true;
            }
            return false;
        }

        public IEnumerable<int> Closest(string text, int count = 3)
        {
            string key = Key(text);
            if (key.Length == 0) return Enumerable.Empty<int>();
            int limit = Math.Max(2, key.Length / 3);
            return _byKey
                .Select(kv => (kv.Value[0], Distance: CoreExtensions.Levenshtein(key, kv.Key)))
                .Where(x => x.Distance <= limit)
                .OrderBy(x => x.Distance).ThenBy(x => x.Item1)
                .Take(count)
                .Select(x => x.Item1);
        }

        // Ignore case, accents and spacing, so "poke ball" finds "Poké Ball".
        internal static string Key(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            string decomposed = name.Trim().Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder(decomposed.Length);
            bool space = false;
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsWhiteSpace(c) || c == '_') { space = sb.Length > 0; continue; }
                if (space) { sb.Append(' '); space = false; }
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }

    /// <summary>Reads the cells of one row for an importer, adding an issue for each cell that can't be used.</summary>
    public sealed class CsvCheck
    {
        private readonly CsvImporter _importer;

        internal CsvCheck(CsvRecord record, CsvImporter importer) { Record = record; _importer = importer; }

        public CsvRecord Record { get; }
        public string Text(int column) => Record.Cells[column] ?? "";
        public bool IsBlank(int column) => Text(column).Length == 0;
        public string ColumnName(int column) => column >= 0 ? _importer.Columns[column] : "Row";

        public void Error(int column, string message, params CsvFix[] fixes)
            => Record.Issues.Add(new CsvIssue(column, CsvIssueKind.Error, message, fixes));
        public void Warn(int column, string message, params CsvFix[] fixes)
            => Record.Issues.Add(new CsvIssue(column, CsvIssueKind.Warning, message, fixes));

        public bool Number(int column, int min, int max, out int value)
        {
            string text = Text(column);
            if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
            {
                Error(column, text.Length == 0 ? $"{ColumnName(column)} is empty." : $"{text} is not a whole number.");
                return false;
            }
            if (value < min || value > max)
            {
                int clamped = Math.Clamp(value, min, max);
                Error(column, $"{ColumnName(column)} must be between {min} and {max}.",
                    new CsvFix(column, clamped.ToString(CultureInfo.InvariantCulture), $"Use {clamped}"));
                return false;
            }
            return true;
        }

        public bool Flag(int column, out bool value)
        {
            string key = CsvNames.Key(Text(column));
            value = key == "true" || key == "yes" || key == "1";
            if (value || key == "false" || key == "no" || key == "0") return true;
            Error(column, $"{ColumnName(column)} must be True or False.",
                new CsvFix(column, "False", "Use False"), new CsvFix(column, "True", "Use True"));
            return false;
        }

        /// <summary>The entry a name cell gives. <paramref name="prefer"/> settles a name several entries share.</summary>
        public bool Name(int column, CsvNames names, out int index, int prefer = -1)
        {
            string text = Text(column);
            if (names.TryFind(text, prefer, out index, out bool ambiguous))
            {
                if (ambiguous)
                    Warn(column, $"More than one {names.What} is named {text}. Using number {index}.");
                return true;
            }
            if (text.Length == 0)
            {
                Error(column, $"{ColumnName(column)} is empty.");
                return false;
            }
            Error(column, $"No {names.What} is named {text}.",
                names.Closest(text).Select(i => new CsvFix(column, names[i], $"Use {names[i]}")).ToArray());
            return false;
        }

        /// <summary>A number column and a name column for the same entry. The number is used and a name that
        /// disagrees with it is a warning. When the number is blank, the name decides.</summary>
        public bool NumberAndName(int numberColumn, int nameColumn, CsvNames names, out int index)
        {
            index = -1;
            string numberText = Text(numberColumn), nameText = Text(nameColumn);
            if (numberText.Length == 0)
            {
                if (nameText.Length > 0) return Name(nameColumn, names, out index);
                Error(numberColumn, $"{ColumnName(numberColumn)} and {ColumnName(nameColumn)} are both empty.");
                return false;
            }
            bool named = names.TryFind(nameText, -1, out int byName, out _);
            if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out index) || index >= names.Count)
            {
                Error(numberColumn, $"There is no {names.What} {numberText}.",
                    named ? new[] { new CsvFix(numberColumn, byName.ToString(CultureInfo.InvariantCulture), $"Use {names[byName]} ({names.What} {byName})") } : Array.Empty<CsvFix>());
                index = -1;
                return false;
            }
            if (nameText.Length == 0 || CsvNames.Key(nameText) == CsvNames.Key(names[index])) return true;

            List<CsvFix> fixes = new List<CsvFix> { new CsvFix(nameColumn, names[index], $"Use {names[index]} ({names.What} {index})") };
            if (named && byName != index)
                fixes.Add(new CsvFix(numberColumn, byName.ToString(CultureInfo.InvariantCulture), $"Use {names[byName]} ({names.What} {byName})"));
            Warn(nameColumn, $"{Capital(names.What)} {index} is {names[index]} in this ROM, not {nameText}. Keep to use number {index}.", fixes.ToArray());
            return true;
        }

        private static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    /// <summary>What one kind of CSV holds and how its rows become edits.</summary>
    public abstract class CsvImporter
    {
        public abstract string Title { get; }
        /// <summary>Column names, as the matching export writes them.</summary>
        public abstract IReadOnlyList<string> Columns { get; }
        /// <summary>Columns a file may leave out; their cells are then empty.</summary>
        public virtual bool IsOptional(int column) => false;

        /// <summary>Sets the row's label, its issues and, when it can be used, its value.</summary>
        public abstract void Check(CsvCheck check);
        /// <summary>Issues between rows, such as the same Pokémon twice.</summary>
        public virtual void CheckTogether(IReadOnlyList<CsvRecord> records) { }
        /// <summary>What applying the accepted rows would change, old against new.</summary>
        public abstract IReadOnlyList<CsvChange> Changes(IReadOnlyList<CsvRecord> accepted);

        protected CsvCheck On(CsvRecord record) => new CsvCheck(record, this);
    }

    /// <summary>A CSV file read for one importer. Each fix to a cell checks every row again.</summary>
    public sealed class CsvImportSession
    {
        private CsvImportSession(CsvImporter importer, string fileName) { Importer = importer; FileName = fileName; }

        public CsvImporter Importer { get; }
        public string FileName { get; }
        public List<CsvRecord> Records { get; } = new List<CsvRecord>();
        /// <summary>Problems with the file as a whole. Nothing can be applied while there are any.</summary>
        public List<string> FileProblems { get; } = new List<string>();
        public List<string> Notes { get; } = new List<string>();

        public static CsvImportSession Open(CsvImporter importer, string path)
        {
            CsvImportSession session = new CsvImportSession(importer, Path.GetFileName(path));
            List<CsvText.Line> lines;
            try { lines = CsvText.Read(path); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                session.FileProblems.Add("The file could not be read: " + e.Message);
                return session;
            }
            session.Load(lines);
            return session;
        }

        public static CsvImportSession FromText(CsvImporter importer, string text, string fileName)
        {
            CsvImportSession session = new CsvImportSession(importer, fileName);
            session.Load(CsvText.Parse(text));
            return session;
        }

        private void Load(List<CsvText.Line> lines)
        {
            if (lines.Count == 0) { FileProblems.Add("The file is empty."); return; }

            // Columns are found by name, so a spreadsheet may reorder them or add its own.
            string[] header = lines[0].Fields;
            int[] source = new int[Importer.Columns.Count];
            List<string> missing = new List<string>();
            for (int c = 0; c < source.Length; c++)
            {
                string want = HeaderKey(Importer.Columns[c]);
                source[c] = Array.FindIndex(header, h => HeaderKey(h) == want);
                if (source[c] < 0 && !Importer.IsOptional(c)) missing.Add(Importer.Columns[c]);
            }
            if (missing.Count > 0)
            {
                FileProblems.Add($"Missing columns: {string.Join(", ", missing)}. The first line must have the column names.");
                FileProblems.Add($"Expected columns: {string.Join(", ", Importer.Columns)}.");
                return;
            }
            string[] unused = header.Where((h, i) => h.Length > 0 && !source.Contains(i)).ToArray();
            if (unused.Length > 0) Notes.Add($"Ignored columns: {string.Join(", ", unused)}.");

            foreach (CsvText.Line line in lines.Skip(1))
                Records.Add(new CsvRecord(Records.Count, line.Number, source.Select(s => s >= 0 && s < line.Fields.Length ? line.Fields[s] : "").ToArray()));
            if (Records.Count == 0) FileProblems.Add("The file has no data rows.");
            CheckAll();
        }

        private static string HeaderKey(string name) => new string((name ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

        public void CheckAll()
        {
            foreach (CsvRecord record in Records)
            {
                record.Issues.Clear();
                record.Value = null;
                record.Label = "";
                Importer.Check(new CsvCheck(record, Importer));
                // Rows that fail early are labelled by their first cells.
                if (record.Label.Length == 0) record.Label = string.Join(" ", record.Cells.Take(2).Where(t => t.Length > 0));
            }
            Importer.CheckTogether(Records.Where(r => !r.Skipped).ToList());
        }

        public void SetCell(CsvRecord record, int column, string text)
        {
            record.Cells[column] = (text ?? "").Trim();
            CheckAll();
        }

        public void SetSkipped(CsvRecord record, bool skipped)
        {
            record.Skipped = skipped;
            // A skipped row no longer clashes with the others.
            CheckAll();
        }

        public List<CsvRecord> Accepted => Records.Where(r => r.IsAccepted).ToList();
        public bool CanApply => FileProblems.Count == 0 && Records.All(r => r.IsResolved) && Records.Any(r => r.IsAccepted);
        public IReadOnlyList<CsvChange> Changes() => Importer.Changes(Accepted);
    }

    /// <summary>Checks several importers share.</summary>
    public static class CsvRules
    {
        /// <summary>A record named twice is an error on each later row.</summary>
        public static void OneRowPerRecord(IReadOnlyList<CsvRecord> records, Func<CsvRecord, int> key, string what, Func<CsvRecord, CsvCheck> on)
        {
            Dictionary<int, CsvRecord> first = new Dictionary<int, CsvRecord>();
            foreach (CsvRecord r in records)
            {
                int k = key(r);
                if (k < 0) continue;
                if (first.TryGetValue(k, out CsvRecord earlier))
                    on(r).Error(-1, $"Same {what} as line {earlier.Line}. Skip one of them.");
                else first[k] = r;
            }
        }

        /// <summary>Adds a change for each cell from <paramref name="from"/> on that differs.</summary>
        public static void Compare(List<CsvChange> changes, string record, IReadOnlyList<string> columns, int from, object[] was, object[] now)
        {
            for (int col = from; col < columns.Count; col++)
            {
                string a = Convert.ToString(was[col], System.Globalization.CultureInfo.InvariantCulture);
                string b = Convert.ToString(now[col], System.Globalization.CultureInfo.InvariantCulture);
                if (a != b) changes.Add(new CsvChange(record, columns[col], a, b));
            }
        }
    }
}
