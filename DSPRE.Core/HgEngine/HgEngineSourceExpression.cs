using System;
using System.Collections.Generic;
using System.Globalization;

namespace DSPRE.HgEngine
{
    /// <summary>Evaluates the integer constant expressions hg-engine's data files use as field values: names,
    /// literals, OR-ed flags, shifts, arithmetic, comparisons and config switches such as
    /// <c>((CHAMPIONS_POWER_CHANGES) ? (90) : (80))</c>. A name the lookup can't resolve fails the whole value.</summary>
    public static class HgEngineSourceExpression
    {
        public static bool TryEvaluate(string text, Func<string, int?> lookup, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text) || !TryTokenize(text, out var tokens)) return false;
            var parser = new Parser(tokens, lookup);
            if (!parser.TryTernary(out long result) || parser.Position != tokens.Count) return false;
            if (result < int.MinValue || result > uint.MaxValue) return false;
            value = unchecked((int)result);
            return true;
        }

        private static readonly string[] Operators =
            { "||", "&&", "==", "!=", "<=", ">=", "<<", ">>", "?", ":", "|", "^", "&", "<", ">", "+", "-", "*", "/", "%", "!", "~", "(", ")" };

        private static bool TryTokenize(string s, out List<string> tokens)
        {
            tokens = new List<string>();
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                if (c == '/' && i + 1 < s.Length && (s[i + 1] == '/' || s[i + 1] == '*'))
                {
                    BraceScanner.SkipNonCode(s, ref i);
                    continue;
                }
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    int start = i;
                    while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                    tokens.Add(s.Substring(start, i - start));
                    continue;
                }
                string op = null;
                foreach (string candidate in Operators)
                    if (string.CompareOrdinal(s, i, candidate, 0, candidate.Length) == 0) { op = candidate; break; }
                if (op == null) return false;
                tokens.Add(op);
                i += op.Length;
            }
            return tokens.Count > 0;
        }

        private sealed class Parser
        {
            private readonly List<string> _tokens;
            private readonly Func<string, int?> _lookup;
            public int Position { get; private set; }

            public Parser(List<string> tokens, Func<string, int?> lookup) { _tokens = tokens; _lookup = lookup; }

            private string Peek => Position < _tokens.Count ? _tokens[Position] : null;
            private bool Accept(string op)
            {
                if (Peek != op) return false;
                Position++;
                return true;
            }

            public bool TryTernary(out long value)
            {
                if (!TryBinary(0, out value)) return false;
                if (!Accept("?")) return true;
                // C would still reject an unknown name in the branch it doesn't take, so both must resolve.
                if (!TryTernary(out long whenTrue) || !Accept(":") || !TryTernary(out long whenFalse)) return false;
                value = value != 0 ? whenTrue : whenFalse;
                return true;
            }

            // Lowest precedence first, as in C.
            private static readonly string[][] Levels =
            {
                new[] { "||" }, new[] { "&&" }, new[] { "|" }, new[] { "^" }, new[] { "&" },
                new[] { "==", "!=" }, new[] { "<", "<=", ">", ">=" }, new[] { "<<", ">>" }, new[] { "+", "-" }, new[] { "*", "/", "%" },
            };

            private bool TryBinary(int level, out long value)
            {
                if (level == Levels.Length) return TryUnary(out value);
                if (!TryBinary(level + 1, out value)) return false;
                while (Peek != null && Array.IndexOf(Levels[level], Peek) >= 0)
                {
                    string op = _tokens[Position++];
                    if (!TryBinary(level + 1, out long right)) return false;
                    switch (op)
                    {
                        case "||": value = (value != 0 || right != 0) ? 1 : 0; break;
                        case "&&": value = (value != 0 && right != 0) ? 1 : 0; break;
                        case "|": value |= right; break;
                        case "^": value ^= right; break;
                        case "&": value &= right; break;
                        case "==": value = value == right ? 1 : 0; break;
                        case "!=": value = value != right ? 1 : 0; break;
                        case "<": value = value < right ? 1 : 0; break;
                        case "<=": value = value <= right ? 1 : 0; break;
                        case ">": value = value > right ? 1 : 0; break;
                        case ">=": value = value >= right ? 1 : 0; break;
                        case "<<": if (right < 0 || right > 62) return false; value <<= (int)right; break;
                        case ">>": if (right < 0 || right > 62) return false; value >>= (int)right; break;
                        case "+": value += right; break;
                        case "-": value -= right; break;
                        case "*": value *= right; break;
                        case "/": if (right == 0) return false; value /= right; break;
                        case "%": if (right == 0) return false; value %= right; break;
                    }
                }
                return true;
            }

            private bool TryUnary(out long value)
            {
                value = 0;
                if (Accept("-")) { if (!TryUnary(out value)) return false; value = -value; return true; }
                if (Accept("+")) return TryUnary(out value);
                if (Accept("!")) { if (!TryUnary(out value)) return false; value = value == 0 ? 1 : 0; return true; }
                if (Accept("~")) { if (!TryUnary(out value)) return false; value = ~value; return true; }
                if (Accept("("))
                    return TryTernary(out value) && Accept(")");
                return TryPrimary(out value);
            }

            private bool TryPrimary(out long value)
            {
                value = 0;
                string token = Peek;
                if (token == null) return false;
                if (char.IsDigit(token[0]))
                {
                    Position++;
                    return TryParseNumber(token, out value);
                }
                if (!char.IsLetter(token[0]) && token[0] != '_') return false;
                Position++;
                int? resolved = _lookup?.Invoke(token);
                if (resolved == null) return false;
                value = resolved.Value;
                return true;
            }

            private static bool TryParseNumber(string token, out long value)
            {
                string digits = token.TrimEnd('u', 'U', 'l', 'L');
                if (digits.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    return long.TryParse(digits[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
                return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value);
            }
        }
    }
}
