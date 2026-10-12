using System;
using System.Text;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Converts game message text between its stored form (\n, \r and \f markup) and an editable form where
    /// each markup is followed by a line break, so a message reads as the game lays it out.
    /// </summary>
    public static class MessageMarkup
    {
        public static string ToDisplay(string raw)
        {
            raw ??= "";
            foreach (string b in new[] { "\\n", "\\r", "\\f" }) raw = raw.Replace(b, b + Environment.NewLine);
            return raw;
        }

        /// <summary>
        /// As <see cref="ToDisplay(string)"/>, but a \r closing the message is left out of the text and handed
        /// back in <paramref name="end"/>, for <see cref="ToRaw(string, string)"/> to put back on save.
        /// </summary>
        public static string ToDisplay(string raw, out string end)
        {
            raw ??= "";
            end = raw.EndsWith("\\r", StringComparison.Ordinal) ? "\\r" : "";
            return ToDisplay(raw.Substring(0, raw.Length - end.Length));
        }

        public static string ToRaw(string display, string end) => ToRaw((display ?? "").TrimEnd('\r', '\n')) + (end ?? "");

        // A line break shown after \n, \r or \f markup is only for display; one typed anywhere else becomes \n.
        public static string ToRaw(string display)
        {
            string s = (display ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c != '\n') { sb.Append(c); continue; }
                bool afterMarkup = sb.Length >= 2 && sb[^2] == '\\' && (sb[^1] == 'n' || sb[^1] == 'r' || sb[^1] == 'f');
                if (!afterMarkup) sb.Append("\\n");
            }
            return sb.ToString();
        }
    }
}
