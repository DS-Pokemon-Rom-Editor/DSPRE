using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.HgEngine;
using AvaloniaEdit.Document;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using global::Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using static MKDS_Course_Editor.NSBTP.NSBTP.NSBTP_File;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Text
{
    public partial class ScriptEditorViewModel
    {
        private string ReadSourceForPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            if (_currentPath != null && ProjectPaths.Same(path, _currentPath)) return ScriptText ?? "";
            try { return PlainFiles.Exists(path) ? RotomTool.ReadSource(path) : ""; }
            catch (Exception ex) { AppLogger.WarnOnce("ScriptEditorViewModel.ReadSourceForPath", ex); return ""; }
        }

        private static string ResolveProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            return Path.IsPathRooted(path) ? path : Path.Combine(RotomTool.ProjectRoot, path);
        }

        private static void TryReadSpan(JsonElement details, out int start, out int end)
        {
            start = 0;
            end = 0;

            if (details.ValueKind == JsonValueKind.Object
                && details.TryGetProperty("span", out JsonElement span)
                && span.ValueKind == JsonValueKind.Object
                && span.TryGetProperty("start", out JsonElement startElement)
                && startElement.TryGetInt32(out int startValue))
            {
                start = startValue;
                if (span.TryGetProperty("end", out JsonElement endElement) && endElement.TryGetInt32(out int endValue))
                    end = endValue;
            }
        }

        private static string WarningMessage(string kind, JsonElement details)
        {
            string name = details.ReadString("name");
            string command = details.ReadString("command");
            string condition = details.ReadString("condition");

            return kind switch
            {
                "UnusedAlias" => "Alias '" + name + "' is never used",
                "ShadowedAlias" => "Alias '" + name + "' shadows a previous alias definition",
                "MissingSlot" => "Script slot #" + details.ReadString("slot") + " is empty, so the next free script pointer is reused",
                "MessageLineTooLong" => "Message line " + details.ReadString("line_index") + " exceeds the maximum dialog width",
                "VariantConditionUnresolvable" => "Couldn't evaluate variant condition '" + condition + "' for command '" + command + "'",
                _ => kind
            };
        }

        private static void LineColumnForByteOffset(string text, int byteOffset, out int line, out int column)
        {
            line = 1;
            column = 1;
            int charIndex = CharIndexFromUtf8Offset(text, Math.Max(0, byteOffset));

            for (int i = 0; i < charIndex && i < text.Length; i++)
            {
                if (text[i] == '\r')
                {
                    line++;
                    column = 1;
                    if (i + 1 < charIndex && i + 1 < text.Length && text[i + 1] == '\n') i++;
                }
                else if (text[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }
        }

        private static int CharIndexFromUtf8Offset(string text, int byteOffset)
        {
            int bytes = 0;
            for (int i = 0; i < text.Length;)
            {
                int charCount = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
                int charBytes = Encoding.UTF8.GetByteCount(text.AsSpan(i, charCount));
                if (bytes + charBytes > byteOffset) return i;
                bytes += charBytes;
                i += charCount;
            }
            return text.Length;
        }

        private static string LineText(string text, int line)
        {
            if (string.IsNullOrEmpty(text)) return "";

            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (line < 1 || line > lines.Length) return "";
            return lines[line - 1].TrimEnd('\r');
        }

        private static bool TryFuzzyMatch(string text, string query, out int column)
        {
            column = 1;
            int queryIndex = 0;
            int firstMatch = -1;

            for (int i = 0; i < text.Length && queryIndex < query.Length; i++)
            {
                if (char.ToUpperInvariant(text[i]) != char.ToUpperInvariant(query[queryIndex])) continue;
                if (firstMatch < 0) firstMatch = i;
                queryIndex++;
            }

            if (queryIndex != query.Length) return false;
            column = firstMatch + 1;
            return true;
        }

        private static string LanguageIdForPath(string path)
            => Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase) ? "json" : "rotom";

        private static string DisplayPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "(project)";

            string root = RotomTool.ProjectRoot;
            if (!string.IsNullOrWhiteSpace(root))
            {
                try { path = Path.GetRelativePath(root, path); }
                catch (ArgumentException) { /* a path GetRelativePath rejects is shown unchanged */ }
            }
            return path.Replace('\\', '/');
        }
    }
}
