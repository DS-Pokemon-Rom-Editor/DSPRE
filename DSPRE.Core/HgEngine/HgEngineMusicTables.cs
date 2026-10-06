using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DSPRE.ROMFiles;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// src/music_tables.c: battle music combos, the class and species tables, and eye-contact music. With
    /// EXPAND_MUSIC_TABLES the ROM's pointers lead into hg-engine's code, so only the source is readable.
    /// </summary>
    public static class HgEngineMusicTables
    {
        public const string SourceRelPath = "src/music_tables.c";
        private const string ConfigRelPath = "include/config.h";

        private const string ComboTable = "MainMusicComboTable";
        private const string ClassTable = "TrainerClassToMusicCombo";
        private const string SpeciesTable = "PokemonBattleMusic";
        private const string EncounterTable = "sTrainerEncounterMusicParam";

        private const string SeqPrefix = "SEQ_", ComboPrefix = "ANIM_MUSIC_COMBO_", ClassPrefix = "TRAINERCLASS_";

        /// <summary>True when the linked checkout builds its music tables from source.</summary>
        public static bool TablesInSource
        {
            get
            {
                if (!HgEngineProject.IsActive) return false;
                string config = Read(ConfigRelPath, out _);
                return config != null && Read(SourceRelPath, out _) != null
                    && Regex.IsMatch(config, @"^[ \t]*#define[ \t]+EXPAND_MUSIC_TABLES\b", RegexOptions.Multiline);
            }
        }

        private static HgEngineSymbolTable Sounds => HgEngineSymbolTable.Load("include/constants/sndseq.h");
        private static HgEngineSymbolTable Classes => HgEngineSymbolTable.Load("include/constants/trainerclass.h");
        private static HgEngineSymbolTable Species => HgEngineSymbolTable.Load("include/constants/species.h");

        // ── Reading ──────────────────────────────────────────────────────────────────────────────

        public static BattleMusicTables ReadBattle()
        {
            string text = Read(SourceRelPath, out _);
            return text == null ? null : ParseBattle(text, Sounds, Classes, Species);
        }

        /// <summary>Eye-contact music by trainer class: the Johto and Kanto sequences.</summary>
        public static Dictionary<int, (int Johto, int Kanto)> ReadEncounterMusic()
        {
            string text = Read(SourceRelPath, out _);
            return text == null ? new Dictionary<int, (int, int)>() : ParseEncounterMusic(text, Sounds, Classes);
        }

        internal static BattleMusicTables ParseBattle(string source, HgEngineSymbolTable sounds, HgEngineSymbolTable classes, HgEngineSymbolTable species)
        {
            string masked = MaskComments(source);
            IReadOnlyList<Match> combos = Rows(masked, ComboTable, ComboRow);
            if (combos == null) return null;
            BattleMusicTables t = new BattleMusicTables(fromHgEngineSource: true);

            SortedDictionary<int, (ushort, ushort)> byIndex = new SortedDictionary<int, (ushort, ushort)>();
            int next = 0;
            foreach (Match m in combos)
            {
                int index = m.Groups[1].Success ? Value(m.Groups[1].Value, sounds) : next;
                if (index < 0) continue;
                byIndex[index] = ((ushort)Value(m.Groups[2].Value, sounds), (ushort)Value(m.Groups[3].Value, sounds));
                next = index + 1;
            }
            foreach (KeyValuePair<int, (ushort, ushort)> kv in byIndex)
            {
                while (t.Combos.Rows.Count < kv.Key) t.Combos.Rows.Add((0xFFFF, 0));
                t.Combos.Rows.Add(kv.Value);
            }

            foreach (Match m in Rows(masked, ClassTable, ClassRow) ?? (IEnumerable<Match>)Array.Empty<Match>())
                t.Classes.Rows.Add((Value(m.Groups[1].Value, classes), ClassCombo(m, sounds)));

            foreach (Match m in Rows(masked, SpeciesTable, SpeciesRow) ?? (IEnumerable<Match>)Array.Empty<Match>())
                t.Species.Rows.Add((Value(m.Groups[1].Value, species), Value(m.Groups[2].Value, sounds)));
            return t;
        }

        internal static Dictionary<int, (int, int)> ParseEncounterMusic(string source, HgEngineSymbolTable sounds, HgEngineSymbolTable classes)
        {
            Dictionary<int, (int, int)> music = new Dictionary<int, (int, int)>();
            foreach (Match m in Rows(MaskComments(source), EncounterTable, EncounterRow) ?? (IEnumerable<Match>)Array.Empty<Match>())
            {
                int cls = Value(m.Groups[1].Value, classes);
                if (cls >= 0) music.TryAdd(cls, (Value(m.Groups[2].Value, sounds), Value(m.Groups[3].Value, sounds)));
            }
            return music;
        }

        // ── Writing ──────────────────────────────────────────────────────────────────────────────

        public static bool TrySetCombo(int index, ushort transition, ushort sequence, out string error)
            => Edit(out error, text => SetCombo(text, index, transition, sequence, Sounds));

        /// <summary>Rewrites row <paramref name="row"/> of the class table, in file order.</summary>
        public static bool TrySetClassCombo(int row, int trainerClass, int combo, out string error)
            => Edit(out error, text => SetClassCombo(text, row, trainerClass, combo, Sounds, Classes));

        /// <summary>Rewrites row <paramref name="row"/> of the Pokemon music table, in file order.</summary>
        public static bool TrySetSpeciesCombo(int row, int species, int combo, out string error)
        {
            error = SpeciesRowProblem(species, combo);
            return error == null && Edit(out error, text => SetSpeciesCombo(text, row, species, combo, Sounds, Species));
        }

        // struct MonBattleMusic packs the species in 10 bits and the combo in 6.
        private static string SpeciesRowProblem(int species, int combo) =>
            species < 0 || species > 0x3FF ? $"{SpeciesTable} stores species in 10 bits, so species {species} can't have a row."
            : combo < 0 || combo > 0x3F ? $"{SpeciesTable} stores combos in 6 bits, so combo {combo} doesn't fit."
            : null;

        /// <summary>
        /// Adds a Pokemon music row. The game only searches as many rows as bytereplacement's count byte for the
        /// table says, so that count is raised with it.
        /// </summary>
        public static bool TryAddSpeciesRow(int species, int combo, out string error)
        {
            error = SpeciesRowProblem(species, combo);
            if (error != null) return false;
            if (!TryTableCount(SpeciesTable, SpeciesRow, +1, out string count, out error)) return false;
            return Edit(out error, text => AddSpeciesRow(text, species, combo, Sounds, Species)) && WriteCount(count, out error);
        }

        /// <summary>Takes a Pokemon music row out, lowering the table's count byte with it.</summary>
        public static bool TryRemoveSpeciesRow(int row, out string error)
        {
            if (!TryTableCount(SpeciesTable, SpeciesRow, -1, out string count, out error)) return false;
            return Edit(out error, text => RemoveSpeciesRow(text, row)) && WriteCount(count, out error);
        }

        private const string ByteReplacementRelPath = "bytereplacement";

        // bytereplacement's EXPAND_MUSIC_TABLES branch: "# <table> table range" then "arm9 <address> <count>".
        private static Regex CountLine(string table) => new(@"#\s*" + table + @"\s+table range[^\n]*\n\s*arm9\s+\w+\s+([0-9A-Fa-f]{2})\b");

        private static bool TryTableCount(string table, string rowPattern, int delta, out string updated, out string error)
        {
            updated = null;
            error = null;
            string text = Read(ByteReplacementRelPath, out _), source = Read(SourceRelPath, out _);
            if (text == null || source == null) { error = $"{ByteReplacementRelPath} or {SourceRelPath} is missing from the checkout."; return false; }
            Match m = CountLine(table).Match(text);
            if (!m.Success) { error = $"{ByteReplacementRelPath} has no {table} table range, so a row can't be added or removed."; return false; }
            int count = int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            int rows = Rows(MaskComments(source), table, rowPattern)?.Count ?? -1;
            if (rows != count) { error = $"{table} has {rows} rows but the game is told to search {count}. Make them agree first."; return false; }
            int next = count + delta;
            if (next < 1 || next > 255) { error = $"{table} can have 1 to 255 rows."; return false; }
            updated = text.Substring(0, m.Groups[1].Index) + next.ToString("X2", CultureInfo.InvariantCulture) + text.Substring(m.Groups[1].Index + 2);
            return true;
        }

        private static bool WriteCount(string text, out string error)
        {
            error = null;
            Read(ByteReplacementRelPath, out string path);
            HgEngineFileCache.WriteText(path, text);
            return true;
        }

        internal static (string Text, string Error) SetSpeciesCombo(string text, int row, int species, int combo,
            HgEngineSymbolTable sounds, HgEngineSymbolTable speciesTable)
        {
            IReadOnlyList<Match> rows = Rows(MaskComments(text), SpeciesTable, SpeciesRow);
            if (rows == null || row < 0 || row >= rows.Count) return (null, $"{SpeciesTable} has no row {row}.");
            Match m = rows[row];
            return (Replace(text, (m.Groups[2], Token(combo, m.Groups[2].Value, sounds, ComboPrefix)),
                                  (m.Groups[1], Token(species, m.Groups[1].Value, speciesTable, "SPECIES_"))), null);
        }

        internal static (string Text, string Error) AddSpeciesRow(string text, int species, int combo,
            HgEngineSymbolTable sounds, HgEngineSymbolTable speciesTable)
        {
            string masked = MaskComments(text);
            IReadOnlyList<Match> rows = Rows(masked, SpeciesTable, SpeciesRow);
            int end = BlockEnd(masked, SpeciesTable);
            if (rows == null || end < 0) return (null, $"{SourceRelPath} has no {SpeciesTable}.");
            string newline = text.Contains("\r\n") ? "\r\n" : "\n";
            string indent = "    ";
            if (rows.Count > 0)
            {
                int lineStart = text.LastIndexOf('\n', rows[^1].Index) + 1;
                indent = text.Substring(lineStart, rows[^1].Index - lineStart);
                indent = indent.Substring(0, indent.Length - indent.TrimStart().Length);
            }
            string line = $"{indent}{{ .species = {Token(species, null, speciesTable, "SPECIES_")}, .combo = {Token(combo, null, sounds, ComboPrefix)} }},{newline}";
            return (text.Insert(text.LastIndexOf('\n', end) + 1, line), null);
        }

        internal static (string Text, string Error) RemoveSpeciesRow(string text, int row)
        {
            IReadOnlyList<Match> rows = Rows(MaskComments(text), SpeciesTable, SpeciesRow);
            if (rows == null || row < 0 || row >= rows.Count) return (null, $"{SpeciesTable} has no row {row}.");
            int start = text.LastIndexOf('\n', rows[row].Index) + 1;
            int end = text.IndexOf('\n', rows[row].Index + rows[row].Length);
            end = end < 0 ? text.Length : end + 1;
            // A line holding only this row goes whole; otherwise just the row and its comma.
            string lineText = text.Substring(start, end - start);
            if (lineText.Trim().TrimEnd(',').Trim() == rows[row].Value.Trim()) return (text.Remove(start, end - start), null);
            int cut = rows[row].Index + rows[row].Length;
            while (cut < text.Length && (text[cut] == ' ' || text[cut] == ',')) cut++;
            return (text.Remove(rows[row].Index, cut - rows[row].Index), null);
        }

        /// <summary>Sets a class's eye-contact music, adding its row when it has none.</summary>
        /// <summary>Sets a class's eye-contact music. A new row raises the count the game searches, as hg-engine's
        /// guide to adding classes says it must.</summary>
        public static bool TrySetEncounterMusic(int trainerClass, ushort johto, ushort kanto, out string error)
        {
            error = null;
            string source = Read(SourceRelPath, out _);
            bool adding = source != null && !(Rows(MaskComments(source), EncounterTable, EncounterRow) ?? Array.Empty<Match>())
                .Any(m => Value(m.Groups[1].Value, Classes) == trainerClass);
            string count = null;
            if (adding && !TryTableCount(EncounterTable, EncounterRow, +1, out count, out error)) return false;
            return Edit(out error, text => SetEncounterMusic(text, trainerClass, johto, kanto, Sounds, Classes))
                && (!adding || WriteCount(count, out error));
        }

        internal static (string Text, string Error) SetCombo(string text, int index, ushort transition, ushort sequence, HgEngineSymbolTable sounds)
        {
            Match row = null;
            int next = 0;
            foreach (Match m in Rows(MaskComments(text), ComboTable, ComboRow) ?? (IEnumerable<Match>)Array.Empty<Match>())
            {
                int at = m.Groups[1].Success ? Value(m.Groups[1].Value, sounds) : next;
                next = at + 1;
                if (at == index) { row = m; break; }
            }
            if (row == null) return (null, $"{ComboTable} has no combo {index}.");
            return (Replace(text, (row.Groups[3], Seq(sequence, row.Groups[3].Value, sounds)),
                                  (row.Groups[2], Token(transition, row.Groups[2].Value, sounds, null, hex: true))), null);
        }

        internal static (string Text, string Error) SetClassCombo(string text, int row, int trainerClass, int combo,
            HgEngineSymbolTable sounds, HgEngineSymbolTable classes)
        {
            IReadOnlyList<Match> rows = Rows(MaskComments(text), ClassTable, ClassRow);
            if (rows == null || row < 0 || row >= rows.Count) return (null, $"{ClassTable} has no row {row}.");
            Match m = rows[row];
            string cls = Token(trainerClass, m.Groups[1].Value, classes, ClassPrefix);
            string comboText = m.Groups[3].Success
                ? (Value(m.Groups[2].Value, sounds) == combo ? m.Groups[2].Value : Token(combo, null, sounds, ComboPrefix)) + " * 4"
                : Token(combo * 4, m.Groups[2].Value, null, null);
            return (Replace(text, (m, $"{{ {cls}, {comboText} }}")), null);
        }

        internal static (string Text, string Error) SetEncounterMusic(string text, int trainerClass, ushort johto, ushort kanto,
            HgEngineSymbolTable sounds, HgEngineSymbolTable classes)
        {
            string masked = MaskComments(text);
            IReadOnlyList<Match> rows = Rows(masked, EncounterTable, EncounterRow);
            if (rows == null) return (null, $"{SourceRelPath} has no {EncounterTable}.");
            foreach (Match m in rows)
                if (Value(m.Groups[1].Value, classes) == trainerClass)
                    return (Replace(text, (m.Groups[3], Seq(kanto, m.Groups[3].Value, sounds)),
                                          (m.Groups[2], Seq(johto, m.Groups[2].Value, sounds))), null);

            string designator = Token(trainerClass, null, classes, ClassPrefix);
            int lineStart = text.LastIndexOf('\n', BlockEnd(masked, EncounterTable)) + 1;
            string line = $"        {{ .class = {designator}, .music1 = {Seq(johto, null, sounds)}, .music2 = {Seq(kanto, null, sounds)} }},{(text.Contains("\r\n") ? "\r\n" : "\n")}";
            return (text.Insert(lineStart, line), null);
        }

        // ── Source text ──────────────────────────────────────────────────────────────────────────

        private const string ComboRow = @"(?:\[\s*(\w+)\s*\]\s*=\s*)?\{\s*(\w+)\s*,\s*(\w+)\s*\}";
        private const string ClassRow = @"\{\s*(\w+)\s*,\s*(\w+)\s*(\*\s*4)?\s*\}";
        private const string SpeciesRow = @"\{\s*\.species\s*=\s*(\w+)\s*,\s*\.combo\s*=\s*(\w+)\s*\}";
        private const string EncounterRow = @"\.class\s*=\s*(\w+)\s*,\s*\.music1\s*=\s*(\w+)\s*,\s*\.music2\s*=\s*(\w+)";

        // The second byte of a class row is the combo's offset into the 4-byte-row combo table.
        private static int ClassCombo(Match m, HgEngineSymbolTable sounds)
        {
            int v = Value(m.Groups[2].Value, sounds);
            return v < 0 || m.Groups[3].Success ? v : v / 4;
        }

        private static string Read(string relPath, out string path)
        {
            path = null;
            string root = HgEngineProject.RepoPathUnc;
            if (string.IsNullOrEmpty(root)) return null;
            path = Path.Combine(root, relPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }

        private static bool Edit(out string error, Func<string, (string Text, string Error)> change)
        {
            error = null;
            if (!TablesInSource) { error = $"This hg-engine checkout does not build its music tables from {SourceRelPath}."; return false; }
            string text = Read(SourceRelPath, out string path);
            (string edited, string err) = change(text);
            if (edited == null) { error = err; return false; }
            if (edited != text) HgEngineFileCache.WriteText(path, edited);
            return true;
        }

        // Comments blanked to spaces, so matches in the masked text sit at the same offsets as in the source.
        internal static string MaskComments(string text)
        {
            StringBuilder sb = new StringBuilder(text);
            foreach (Match m in Regex.Matches(text, @"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline))
                for (int i = m.Index; i < m.Index + m.Length; i++)
                    if (sb[i] != '\n') sb[i] = ' ';
            return sb.ToString();
        }

        // The table's rows as the C reader splits them, each matched against the row's own span; null when the table is
        // missing, a row sits under #if, or a row isn't the shape the pattern describes.
        private static IReadOnlyList<Match> Rows(string masked, string table, string rowPattern)
        {
            CDeclaration decl = CSourceFile.For(masked).Find(table);
            if (decl == null) return null;
            Regex pattern = new Regex(rowPattern);
            List<Match> rows = new List<Match>();
            foreach (CInitItem item in decl.Init.Items)
            {
                if (item.IsConditional) return null;
                Match m = pattern.Match(masked, item.Start, item.End - item.Start);
                if (!m.Success) return null;
                rows.Add(m);
            }
            return rows;
        }

        // Where the table's closing brace is, for inserting rows before it.
        private static int BlockEnd(string masked, string table) => CSourceFile.For(masked).Find(table)?.Init.Close ?? -1;

        // Replaces spans right to left so earlier offsets stay valid.
        private static string Replace(string text, params (Capture At, string With)[] edits)
        {
            Array.Sort(edits, (a, b) => b.At.Index.CompareTo(a.At.Index));
            foreach ((Capture at, string with) in edits) text = text.Remove(at.Index, at.Length).Insert(at.Index, with);
            return text;
        }

        private static string Seq(int value, string current, HgEngineSymbolTable sounds) => Token(value, current, sounds, SeqPrefix);

        // Keeps the token as written when its value is unchanged, so aliases and hex survive a save.
        private static string Token(int value, string current, HgEngineSymbolTable symbols, string prefix, bool hex = false)
        {
            if (current != null && Value(current, symbols) == value) return current;
            if (prefix != null && symbols != null && symbols.TryGetNameWithPrefix(value, prefix, out string name)) return name;
            return hex ? "0x" + value.ToString("X", CultureInfo.InvariantCulture) : value.ToString(CultureInfo.InvariantCulture);
        }

        internal static int Value(string token, HgEngineSymbolTable symbols)
        {
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(token.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex)) return hex;
            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int dec)) return dec;
            return symbols != null && symbols.TryGetValue(token, out int v) ? v : -1;
        }
    }
}
