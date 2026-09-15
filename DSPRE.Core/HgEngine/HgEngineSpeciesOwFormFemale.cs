using System;
using System.Collections.Generic;
using System.IO;

namespace DSPRE.HgEngine
{
    /// <summary>Source-text read/write for data/SpeciesToOWFormFemale.c's <c>SpeciesToOWFormFemale[]</c>.
    /// Values are <c>FALSE</c>, <c>TRUE</c>, <c>SPECIES_X_OVERWORLD_Y</c> or
    /// <c>OW_FEMALE_MASK | SPECIES_X_OVERWORLD_FEMALE</c>, so the right-hand side is edited as raw text,
    /// validated before it is written.</summary>
    public static class HgEngineSpeciesOwFormFemale
    {
        private const string SourceRelPath = "data/SpeciesToOWFormFemale.c";
        private const string SpeciesHeaderRelPath = "include/constants/species.h";
        // The headers SpeciesToOWFormFemale.c itself includes.
        private static readonly string[] NameHeaders = { "include/types.h", "include/constants/pokemon.h", SpeciesHeaderRelPath };

        public static bool TryGetRawExpression(int speciesId, out string expression)
        {
            expression = null;
            if (!HgEngineProject.IsActive) return false;
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator)) return false;

            string text = TryReadSource(out _);
            if (text == null) return false;
            // Absent = FALSE (no female overworld form), same as the game's own default for this table.
            if (!HgEngineFlatArrayField.TryGetRawValue(text, designator, out expression)) expression = "FALSE";
            return true;
        }

        public static bool TrySetRawExpression(int speciesId, string expression, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            if (!TryValidateRawExpression(expression, out error)) return false;
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Could not resolve a species designator for id {speciesId}."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }
            if (!HgEngineFlatArrayField.TrySetRawValue(ref text, designator, expression.Trim()))
            { error = $"Could not locate or insert species {speciesId} in SpeciesToOWFormFemale.c."; return false; }

            HgEngineFileCache.WriteText(path, text);
            return true;
        }

        /// <summary>Checks a value against the names the checkout defines, without writing it.</summary>
        public static bool TryValidateRawExpression(string expression, out string error)
        {
            var nameTables = new List<HgEngineSymbolTable>();
            foreach (string header in NameHeaders)
                if (HgEngineSymbolTable.Load(header) is { } table) nameTables.Add(table);
            bool IsKnownName(string name) => nameTables.Exists(t => t.TryGetValue(name, out _));
            // Without the headers a name can't be checked, only the expression's shape.
            return TryValidateExpression(expression, nameTables.Count == NameHeaders.Length ? (Func<string, bool>)IsKnownName : null, out error);
        }

        /// <summary>Accepts what one table value can be: names and numbers joined by <c>| &lt;&lt; &gt;&gt; + -</c>,
        /// with parentheses. A comma, semicolon or brace would break the table or its reader, and an
        /// incomplete expression breaks the build.</summary>
        internal static bool TryValidateExpression(string expression, Func<string, bool> isKnownName, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(expression)) { error = "Enter a value."; return false; }

            var tokens = new List<string>();
            string s = expression.Trim();
            for (int i = 0; i < s.Length;)
            {
                char c = s[i];
                if (c == ' ' || c == '\t') { i++; continue; }
                int start = i;
                if (char.IsLetter(c) || c == '_')
                {
                    while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                }
                else if (char.IsDigit(c))
                {
                    bool hex = c == '0' && i + 1 < s.Length && (s[i + 1] == 'x' || s[i + 1] == 'X');
                    i += hex ? 2 : 0;
                    int digits = i;
                    while (i < s.Length && (hex ? Uri.IsHexDigit(s[i]) : char.IsDigit(s[i]))) i++;
                    if (i == digits || (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')))
                    { error = $"\"{s[start..Math.Min(i + 1, s.Length)]}\" is not a number."; return false; }
                }
                else if ((c == '<' || c == '>') && i + 1 < s.Length && s[i + 1] == c) i += 2;
                else if (c is '|' or '+' or '-' or '(' or ')') i++;
                else { error = $"\"{c}\" is not allowed here."; return false; }
                tokens.Add(s[start..i]);
            }

            int pos = 0;
            string problem = null;   // local functions can't assign the out parameter
            bool ParseExpr()
            {
                if (!ParseUnary()) return false;
                while (pos < tokens.Count && tokens[pos] is "|" or "<<" or ">>" or "+" or "-")
                {
                    pos++;
                    if (!ParseUnary()) return false;
                }
                return true;
            }
            bool ParseUnary()
            {
                while (pos < tokens.Count && tokens[pos] is "+" or "-") pos++;
                if (pos >= tokens.Count) { problem = "The expression is incomplete."; return false; }
                string t = tokens[pos++];
                if (t == "(")
                {
                    if (!ParseExpr()) return false;
                    if (pos >= tokens.Count || tokens[pos] != ")") { problem = "A \"(\" is not closed."; return false; }
                    pos++;
                    return true;
                }
                if (char.IsDigit(t[0])) return true;
                if (char.IsLetter(t[0]) || t[0] == '_')
                {
                    if (isKnownName != null && !isKnownName(t)) { problem = $"{t} is not defined."; return false; }
                    return true;
                }
                problem = $"\"{t}\" is out of place.";
                return false;
            }

            if (!ParseExpr()) { error = problem; return false; }
            if (pos < tokens.Count)
            {
                error = tokens[pos] == ")" ? "A \")\" has no matching \"(\"." : $"\"{tokens[pos]}\" is out of place.";
                return false;
            }
            return true;
        }

        private static string TryReadSource(out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
