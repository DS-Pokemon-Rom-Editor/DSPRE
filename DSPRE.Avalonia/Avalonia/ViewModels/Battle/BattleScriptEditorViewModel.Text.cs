using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using DSPRE.Avalonia.BattleAnim;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Threading;
using DSPRE.Avalonia.Data;
using static DSPRE.RomInfo;
using Avalonia.Platform;
using System.Runtime.InteropServices;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    public sealed partial class BattleScriptEditorViewModel
    {
        // Cards and text stay in sync. Text only rebuilds the cards when it has no errors.
        private bool _pushingText;     // the text is being written from the cards, so it is not parsed
        private bool _rebuildingRows;  // the cards are being rebuilt from the text, so it is not rewritten

        private string _commandsText = "";
        public string CommandsText
        {
            get => _commandsText;
            set { if (Set(ref _commandsText, value) && !_pushingText) ParseTextIntoRows(); }
        }

        public readonly struct TextError
        {
            public TextError(int offset, int length, string message) { Offset = offset; Length = length; Message = message; }
            public int Offset { get; } public int Length { get; } public string Message { get; }
        }
        private IReadOnlyList<TextError> _textErrors = Array.Empty<TextError>();
        public IReadOnlyList<TextError> TextErrors { get => _textErrors; private set { _textErrors = value; OnPropertyChanged(nameof(TextErrors)); OnPropertyChanged(nameof(HasTextErrors)); OnPropertyChanged(nameof(TextErrorSummary)); } }
        public bool HasTextErrors => _textErrors.Count > 0;
        public string TextErrorSummary => _textErrors.Count == 0 ? "" : Plural.Of(_textErrors.Count, "error") + ": " + _textErrors[0].Message;

        private void SyncTextFromRows()
        {
            if (_rebuildingRows) return;
            _pushingText = true;
            CommandsText = RowsToText();
            _pushingText = false;
        }

        // A line is "CommandName label=value ...". Named args take their slot, bare ones the next free.
        private string RowsToText()
        {
            StringBuilder sb = new StringBuilder();
            foreach (ScriptCmdRow row in Rows)
            {
                string raw = OpNameOf(row.OpId);
                sb.Append(BattleAnimSchema.CommandName(raw, !IsAnimation));
                for (int i = 0; i < row.Args.Count; i++)
                {
                    sb.Append(' ');
                    int v = row.Args[i];
                    string label = BattleAnimSchema.ParamName(raw, i, !IsAnimation);
                    BattleAnimSchema.EnumOption[] opts = BattleAnimSchema.EnumFor(raw, i);
                    string valText = v.ToString(CultureInfo.InvariantCulture);
                    if (opts != null)
                        foreach (BattleAnimSchema.EnumOption o in opts) if (o.Value == v) { valText = BattleAnimSchema.Token(o.Label, true); break; }
                    if (label.StartsWith("Param ", StringComparison.Ordinal)) sb.Append(valText);
                    else sb.Append(BattleAnimSchema.ArgToken(raw, i, !IsAnimation)).Append('=').Append(valText);
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        private Dictionary<string, int> _nameToOp;
        // The raw opcode identifier and the command name both map to the opId, so a line takes either.
        private Dictionary<string, int> NameToOp()
        {
            Dictionary<string, int> d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < OpcodeNames.Count; i++)
            {
                d[OpcodeNames[i]] = i;
                string cmd = BattleAnimSchema.CommandName(OpcodeNames[i], !IsAnimation);
                if (!string.IsNullOrEmpty(cmd) && !d.ContainsKey(cmd)) d[cmd] = i;   // single-word name (first wins on collision)
            }
            return d;
        }

        private static bool TryParseWord(string t, out int v)
        {
            t = t.Trim();
            return t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? int.TryParse(t.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)
                : int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
        }

        private static bool TryResolveEnum(string rawOpName, int argIndex, string token, out int value)
        {
            value = 0;
            BattleAnimSchema.EnumOption[] opts = BattleAnimSchema.EnumFor(rawOpName, argIndex);
            if (opts == null) return false;
            foreach (BattleAnimSchema.EnumOption o in opts)
            {
                if (string.Equals(o.Label, token, StringComparison.OrdinalIgnoreCase)) { value = o.Value; return true; }   // the spaced label older text used
                if (string.Equals(BattleAnimSchema.Token(o.Label, true), token, StringComparison.OrdinalIgnoreCase)) { value = o.Value; return true; }
            }
            return false;
        }

        private static bool TryParseArgValue(string rawOpName, int argIndex, string token, out int v)
            => TryParseWord(token, out v) || TryResolveEnum(rawOpName, argIndex, token, out v);

        // Scans only the opcode's known labels, stopping at the first generic "Param N".
        private static int ResolveArgIndex(string rawOpName, string label, bool script)
        {
            string digits = label.Length > 5 && label.StartsWith("param", StringComparison.OrdinalIgnoreCase) ? label.Substring(5)
                           : label.Length > 3 && label.StartsWith("arg", StringComparison.OrdinalIgnoreCase) ? label.Substring(3)
                           : null;
            if (digits != null && int.TryParse(digits, out int n) && n >= 1) return n - 1;
            for (int i = 0; i < 32; i++)
            {
                string pn = BattleAnimSchema.ParamName(rawOpName, i, script);
                if (pn.StartsWith("Param ", StringComparison.Ordinal)) break;
                if (string.Equals(BattleAnimSchema.ArgToken(rawOpName, i, script), label, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        // Collects per-token errors for squiggles. Rebuilds the cards only when clean.
        private void ParseTextIntoRows()
        {
            List<TextError> errors = new List<TextError>();
            List<(int opId, int[] args)> parsed = new List<(int opId, int[] args)>();
            _nameToOp ??= NameToOp();

            int offset = 0;
            foreach (string rawLine in _commandsText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                int lineStart = offset;
                offset += rawLine.Length + 1;
                string line = rawLine.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("//") || line.StartsWith("#")) continue;
                ParseLine(rawLine, line, lineStart, errors, parsed);
            }

            TextErrors = errors;
            // The cards keep their last good state. The typed text still counts as an edit.
            if (errors.Count > 0) { Dirty = true; return; }

            _rebuildingRows = true;
            Rows.Clear();
            foreach ((int opId, int[] args) in parsed) AddRow(opId, args);
            _rebuildingRows = false;
            Dirty = DiffersFromLoaded();
            RefreshViewLines();
            RefreshStoryboard();
            OnPropertyChanged(nameof(HasRows));
            OnPropertyChanged(nameof(EntryHeader));
            Stepped();
        }

        private void ParseLine(string rawLine, string line, int lineStart, List<TextError> errors, List<(int opId, int[] args)> parsed)
        {
            string[] toks = line.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
            string cmdName = toks[0];

            if (!_nameToOp.TryGetValue(cmdName, out int opId))
            {
                int col = rawLine.IndexOf(cmdName, StringComparison.Ordinal);
                errors.Add(new TextError(lineStart + Math.Max(0, col), cmdName.Length, $"Unknown command '{cmdName}'"));
                return;
            }
            string raw = OpNameOf(opId);

            int searchFrom = Math.Max(0, rawLine.IndexOf(cmdName, StringComparison.Ordinal) + cmdName.Length);
            Dictionary<int, int> slotValue = new Dictionary<int, int>();
            List<(string tok, int col)> bareToks = new List<(string tok, int col)>();

            // Named label=value tokens claim their slots first. Bare ones then fill what is left, in order.
            bool argOk = ReadNamedArgs(raw, rawLine, toks, searchFrom, lineStart, slotValue, bareToks, errors);
            argOk &= FillBareArgs(raw, lineStart, bareToks, slotValue, errors);
            if (!argOk) return;

            int[] args = ArgsFromSlots(slotValue);
            // A wrong count shifts every word after it, so the game and the next load read other commands.
            int expected = ExpectedArgCount(opId, args);
            if (expected >= 0 && args.Length != expected)
            {
                int col = Math.Max(0, rawLine.IndexOf(cmdName, StringComparison.Ordinal));
                errors.Add(new TextError(lineStart + col, rawLine.TrimEnd().Length - col,
                    $"{cmdName} takes {Plural.Of(expected, "value")} but this line has {args.Length}"));
                return;
            }
            parsed.Add((opId, args));
        }

        private bool ReadNamedArgs(string raw, string rawLine, string[] toks, int searchFrom, int lineStart,
            Dictionary<int, int> slotValue, List<(string tok, int col)> bareToks, List<TextError> errors)
        {
            bool argOk = true;
            for (int i = 1; i < toks.Length; i++)
            {
                string tok = toks[i];
                int col = rawLine.IndexOf(tok, searchFrom, StringComparison.Ordinal);
                if (col >= 0) searchFrom = col + tok.Length;

                int eq = tok.IndexOf('=');
                if (eq <= 0) { bareToks.Add((tok, col)); continue; }

                string label = tok.Substring(0, eq);
                string valTok = tok.Substring(eq + 1);
                int idx = ResolveArgIndex(raw, label, !IsAnimation);
                if (idx < 0)
                {
                    errors.Add(new TextError(lineStart + Math.Max(0, col), tok.Length, $"Unknown argument '{label}'"));
                    argOk = false; continue;
                }
                if (!TryParseArgValue(raw, idx, valTok, out int v))
                {
                    errors.Add(new TextError(lineStart + Math.Max(0, col), tok.Length, $"'{valTok}' isn't a number or known value"));
                    argOk = false; continue;
                }
                slotValue[idx] = v;
            }
            return argOk;
        }

        private static bool FillBareArgs(string raw, int lineStart, List<(string tok, int col)> bareToks,
            Dictionary<int, int> slotValue, List<TextError> errors)
        {
            bool argOk = true;
            int cursor = 0;
            foreach ((string tok, int col) in bareToks)
            {
                while (slotValue.ContainsKey(cursor)) cursor++;
                if (!TryParseArgValue(raw, cursor, tok, out int v))
                {
                    errors.Add(new TextError(lineStart + Math.Max(0, col < 0 ? 0 : col), tok.Length, $"'{tok}' isn't a number or known value"));
                    argOk = false; cursor++; continue;
                }
                slotValue[cursor] = v;
                cursor++;
            }
            return argOk;
        }

        private static int[] ArgsFromSlots(Dictionary<int, int> slotValue)
        {
            int maxIdx = -1;
            foreach (int k in slotValue.Keys) if (k > maxIdx) maxIdx = k;
            int[] args = new int[maxIdx + 1];
            foreach (KeyValuePair<int, int> kv in slotValue) args[kv.Key] = kv.Value;
            return args;
        }

        private string _storyboard = "";
        public string Storyboard { get => _storyboard; private set => Set(ref _storyboard, value); }
        private IReadOnlyList<BattleAnimStoryboard.Line> _storyboardLines = Array.Empty<BattleAnimStoryboard.Line>();
        public IReadOnlyList<BattleAnimStoryboard.Line> StoryboardLines { get => _storyboardLines; private set => Set(ref _storyboardLines, value); }
        public bool ShowStoryboard => IsAvailable && HasRows;
        public string StoryboardTitle => IsAnimation ? "Animation storyboard" : "Effect summary";

        public global::Avalonia.Media.TextWrapping StoryboardWrap =>
            IsAnimation ? global::Avalonia.Media.TextWrapping.NoWrap : global::Avalonia.Media.TextWrapping.Wrap;
    }
}
