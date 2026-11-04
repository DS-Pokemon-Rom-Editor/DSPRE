using System.Collections.Generic;

// The root namespace: a DSPRE.Json namespace would hide every class called Json in code under DSPRE.
namespace DSPRE
{
    /// <summary>
    /// Reads JSON text by character offsets, which System.Text.Json doesn't report, so an edit can replace one value or
    /// add one member and leave the rest of the file as written. Read values with JsonDocument.
    /// </summary>
    public static class JsonText
    {
        /// <summary>One object member: where its key starts, and its value's span.</summary>
        public sealed class Member
        {
            public string Key;
            public int KeyStart;
            public int ValueStart;
            /// <summary>Exclusive.</summary>
            public int ValueEnd;
        }

        /// <summary>The members of the object at <paramref name="objStart"/>, or null when it isn't one.</summary>
        public static List<Member> Members(string s, int objStart)
        {
            int i = SkipWs(s, objStart);
            if (i >= s.Length || s[i] != '{') return null;
            List<Member> result = new List<Member>();
            i = SkipWs(s, i + 1);
            if (i < s.Length && s[i] == '}') return result;
            while (i < s.Length)
            {
                if (s[i] != '"') return null;
                int keyStart = i;
                int keyEnd = SkipString(s, i);
                if (keyEnd < 0) return null;
                string key = s.Substring(keyStart + 1, keyEnd - keyStart - 2);
                i = SkipWs(s, keyEnd);
                if (i >= s.Length || s[i] != ':') return null;
                int valueStart = SkipWs(s, i + 1);
                int valueEnd = SkipValue(s, valueStart);
                if (valueEnd < 0) return null;
                result.Add(new Member { Key = key, KeyStart = keyStart, ValueStart = valueStart, ValueEnd = valueEnd });
                i = SkipWs(s, valueEnd);
                if (i < s.Length && s[i] == ',') { i = SkipWs(s, i + 1); continue; }
                if (i < s.Length && s[i] == '}') return result;
                return null;
            }
            return null;
        }

        /// <summary>Each non-empty string literal between two positions, in order, as written (escapes kept).</summary>
        public static IEnumerable<string> Strings(string json, int start, int end)
        {
            for (int at = start; at < end; at++)
            {
                if (json[at] != '"') continue;
                int close = SkipString(json, at);
                if (close < 0 || close > end) yield break;
                if (close - at > 2) yield return json.Substring(at + 1, close - at - 2);
                at = close - 1;
            }
        }

        public static int SkipWs(string s, int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            return i;
        }

        /// <summary>Just past the value at <paramref name="i"/>, or -1 when it doesn't end.</summary>
        public static int SkipValue(string s, int i)
        {
            if (i >= s.Length) return -1;
            if (s[i] == '"') return SkipString(s, i);
            if (s[i] == '{' || s[i] == '[')
            {
                int depth = 0;
                for (; i < s.Length; i++)
                {
                    char c = s[i];
                    if (c == '"') { i = SkipString(s, i); if (i < 0) return -1; i--; continue; }
                    if (c == '{' || c == '[') depth++;
                    else if ((c == '}' || c == ']') && --depth == 0) return i + 1;
                }
                return -1;
            }
            while (i < s.Length && s[i] != ',' && s[i] != '}' && s[i] != ']' && !char.IsWhiteSpace(s[i])) i++;
            return i;
        }

        // Just past the string literal at i, or -1.
        private static int SkipString(string s, int i)
        {
            for (i++; i < s.Length; i++)
            {
                if (s[i] == '\\') { i++; continue; }
                if (s[i] == '"') return i + 1;
            }
            return -1;
        }

        /// <summary>
        /// Adds <paramref name="member"/> as the object's last member: after the last one with a comma, or on its own line
        /// in an empty object.
        /// </summary>
        public static string InsertMember(string json, int objStart, int objEnd, List<Member> members,
            string member, string memberIndent, string closeIndent, string newline)
        {
            if (members == null || members.Count == 0)
                return json.Substring(0, objStart + 1) + newline + memberIndent + member + newline + closeIndent + json.Substring(objEnd - 1);
            int after = members[^1].ValueEnd;
            return json.Substring(0, after) + "," + newline + memberIndent + member + json.Substring(after);
        }

        /// <summary>The indent one level adds, from the first member that starts its own line; two spaces when none does.</summary>
        public static string IndentUnit(string json)
        {
            for (int open = json.IndexOf('{'); open >= 0; open = json.IndexOf('{', open + 1))
            {
                int at = open + 1;
                if (at < json.Length && json[at] == '\r') at++;
                if (at >= json.Length || json[at] != '\n') continue;
                int start = ++at;
                while (at < json.Length && json[at] is ' ' or '\t') at++;
                if (at > start && at < json.Length && json[at] == '"') return json.Substring(start, at - start);
            }
            return "  ";
        }
    }
}
