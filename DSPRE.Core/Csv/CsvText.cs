using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.Csv
{
    /// <summary>Comma-separated text as spreadsheets save it: a quoted field may hold commas, doubled quotes and
    /// line breaks.</summary>
    public static class CsvText
    {
        public sealed class Line
        {
            public Line(int number, string[] fields) { Number = number; Fields = fields; }
            /// <summary>The file line the record starts on, from 1.</summary>
            public int Number { get; }
            public string[] Fields { get; }
        }

        public static List<Line> Read(string path) => Parse(File.ReadAllText(path));

        public static List<Line> Parse(string text)
        {
            List<Line> lines = new List<Line>();
            List<string> fields = new List<string>();
            StringBuilder field = new StringBuilder();
            bool quoted = false;
            int lineNumber = 1, startLine = 1;
            int i = 0;
            if (text.Length > 0 && text[0] == '﻿') i = 1;

            void EndRecord()
            {
                fields.Add(field.ToString());
                field.Clear();
                // A blank line is not a record.
                if (fields.Count > 1 || fields[0].Trim().Length > 0)
                    lines.Add(new Line(startLine, fields.Select(f => f.Trim()).ToArray()));
                fields = new List<string>();
            }

            for (; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else quoted = false;
                    }
                    else
                    {
                        if (c == '\n') lineNumber++;
                        field.Append(c);
                    }
                    continue;
                }
                switch (c)
                {
                    // Only a quote that opens a field starts quoting; one inside a value is kept as text.
                    case '"' when field.Length == 0: quoted = true; break;
                    case ',': fields.Add(field.ToString()); field.Clear(); break;
                    case '\r': break;
                    case '\n':
                        EndRecord();
                        lineNumber++;
                        startLine = lineNumber;
                        break;
                    default: field.Append(c); break;
                }
            }
            if (field.Length > 0 || fields.Count > 0) EndRecord();
            return lines;
        }

        /// <summary>One field, quoted only when it has to be.</summary>
        public static string Field(object value)
        {
            string s = value?.ToString() ?? "";
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0 && s.Trim() == s) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        public static string Row(params object[] values) => string.Join(",", values.Select(Field));
    }
}
