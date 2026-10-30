using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DSPRE.Tracking
{
    /// <summary>
    /// How the changes folder writes its values. Output is the same bytes for the same content: keys in the order
    /// they were added, two-space indent, LF line ends and a final newline.
    /// </summary>
    public static class ChangesJson
    {
        private static readonly JsonSerializerOptions ValueOptions = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        /// <summary>Objects and lists of objects one entry per line; lists of plain values on one line.</summary>
        public static string Write(JsonNode node)
        {
            StringBuilder sb = new StringBuilder();
            Append(sb, node, 0);
            return sb.Append('\n').ToString();
        }

        private static void Append(StringBuilder sb, JsonNode node, int depth)
        {
            switch (node)
            {
                case null:
                    sb.Append("null");
                    break;
                case JsonObject o when o.Count == 0:
                    sb.Append("{}");
                    break;
                case JsonObject o:
                    sb.Append("{\n");
                    int i = 0;
                    foreach (KeyValuePair<string, JsonNode> e in o)
                    {
                        sb.Append(' ', (depth + 1) * 2).Append(JsonValue.Create(e.Key).ToJsonString(ValueOptions)).Append(": ");
                        Append(sb, e.Value, depth + 1);
                        if (++i < o.Count) sb.Append(',');
                        sb.Append('\n');
                    }
                    sb.Append(' ', depth * 2).Append('}');
                    break;
                case JsonArray a when a.All(e => e is null or JsonValue):
                    sb.Append('[');
                    for (int k = 0; k < a.Count; k++)
                    {
                        if (k > 0) sb.Append(", ");
                        Append(sb, a[k], depth + 1);
                    }
                    sb.Append(']');
                    break;
                case JsonArray a:
                    sb.Append("[\n");
                    for (int k = 0; k < a.Count; k++)
                    {
                        sb.Append(' ', (depth + 1) * 2);
                        Append(sb, a[k], depth + 1);
                        if (k + 1 < a.Count) sb.Append(',');
                        sb.Append('\n');
                    }
                    sb.Append(' ', depth * 2).Append(']');
                    break;
                default:
                    sb.Append(node.ToJsonString(ValueOptions));
                    break;
            }
        }

        public static byte[] WriteBytes(JsonNode node) => Encoding.UTF8.GetBytes(Write(node));

        public static JsonObject Read(string text) =>
            JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject
            ?? throw new InvalidDataException("The file doesn't hold a JSON object.");

        public static JsonObject ReadFile(string path) => Read(File.ReadAllText(path, Encoding.UTF8));

        /// <summary>A reference to a numbered record, written "33 Tackle", or "33" when it has no name.</summary>
        public static string Ref(int id, string name) =>
            string.IsNullOrWhiteSpace(name) ? id.ToString(CultureInfo.InvariantCulture) : id.ToString(CultureInfo.InvariantCulture) + " " + Clean(name);

        /// <summary>The number at the start of a reference; plain numbers are read too.</summary>
        public static int ParseRef(JsonNode node)
        {
            if (node is JsonValue value)
            {
                if (value.TryGetValue(out int number)) return number;
                if (value.TryGetValue(out string text)) return ParseRef(text);
            }
            throw new InvalidDataException($"Expected a number or \"number name\", found {node?.ToJsonString() ?? "null"}.");
        }

        public static int ParseRef(string text)
        {
            string trimmed = (text ?? "").TrimStart();
            int end = 0;
            if (end < trimmed.Length && trimmed[end] == '-') end++;
            while (end < trimmed.Length && char.IsDigit(trimmed[end])) end++;
            if (!int.TryParse(trimmed.AsSpan(0, end), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int id))
                throw new InvalidDataException($"\"{text}\" doesn't start with a number.");
            return id;
        }

        public static string Hex(ReadOnlySpan<byte> bytes)
        {
            StringBuilder sb = new StringBuilder(bytes.Length * 3);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        public static byte[] FromHex(string text)
        {
            string digits = (text ?? "").Replace(" ", "").Replace("\n", "").Replace("\t", "");
            if (digits.Length % 2 != 0) throw new InvalidDataException($"\"{text}\" isn't whole bytes.");
            return Convert.FromHexString(digits);
        }

        public static string Address(long offset) => "0x" + offset.ToString("X8", CultureInfo.InvariantCulture);

        public static long ParseAddress(JsonNode node)
        {
            string text = node?.GetValue<string>() ?? throw new InvalidDataException("Missing address.");
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text.Substring(2);
            return long.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        /// <summary>A DS colour (BGR555) as #rrggbb; each 5-bit channel is widened so it converts back exactly.</summary>
        public static string Colour(ushort bgr555)
        {
            int r = bgr555 & 0x1F, g = (bgr555 >> 5) & 0x1F, b = (bgr555 >> 10) & 0x1F;
            string text = "#" + Widen(r).ToString("x2") + Widen(g).ToString("x2") + Widen(b).ToString("x2");
            return (bgr555 & 0x8000) != 0 ? text + "!" : text;
        }

        public static ushort ParseColour(string text)
        {
            bool top = text.EndsWith("!", StringComparison.Ordinal);
            string hex = text.TrimStart('#').TrimEnd('!');
            if (hex.Length != 6) throw new InvalidDataException($"\"{text}\" isn't a #rrggbb colour.");
            int r = Convert.ToInt32(hex.Substring(0, 2), 16) >> 3;
            int g = Convert.ToInt32(hex.Substring(2, 2), 16) >> 3;
            int b = Convert.ToInt32(hex.Substring(4, 2), 16) >> 3;
            return (ushort)(r | (g << 5) | (b << 10) | (top ? 0x8000 : 0));
        }

        private static int Widen(int five) => (five << 3) | (five >> 2);

        public static string Sha1(ReadOnlySpan<byte> bytes) => "sha1:" + Convert.ToHexString(SHA1.HashData(bytes)).ToLowerInvariant();

        /// <summary>Lowercase, digits and hyphens, for file names.</summary>
        public static string Slug(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            string plain = name.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            bool hyphen = false;
            foreach (char c in plain)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (c < 128 && char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                    hyphen = false;
                }
                else if (!hyphen && sb.Length > 0)
                {
                    sb.Append('-');
                    hyphen = true;
                }
                if (sb.Length >= 40) break;
            }
            return sb.ToString().Trim('-');
        }

        private static string Clean(string name) => name.Replace('\n', ' ').Replace('\r', ' ').Trim();
    }
}
