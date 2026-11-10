using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
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
                    && CSourceFile.For(config).FindDefine("EXPAND_MUSIC_TABLES") != null;
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
            IReadOnlyList<Row> combos = Rows(source, ComboTable, ComboRow);
            if (combos == null) return null;
            BattleMusicTables t = new BattleMusicTables(fromHgEngineSource: true);

            SortedDictionary<int, (ushort, ushort)> byIndex = new SortedDictionary<int, (ushort, ushort)>();
            int next = 0;
            foreach (Row m in combos)
            {
                int index = m[1] != null ? Value(m[1].Value, sounds) : next;
                if (index < 0) continue;
                byIndex[index] = ((ushort)Value(m[2].Value, sounds), (ushort)Value(m[3].Value, sounds));
                next = index + 1;
            }
            foreach (KeyValuePair<int, (ushort, ushort)> kv in byIndex)
            {
                while (t.Combos.Rows.Count < kv.Key) t.Combos.Rows.Add((0xFFFF, 0));
                t.Combos.Rows.Add(kv.Value);
            }

            foreach (Row m in Rows(source, ClassTable, ClassRow) ?? (IEnumerable<Row>)Array.Empty<Row>())
                t.Classes.Rows.Add((Value(m[1].Value, classes), ClassCombo(m, sounds)));

            foreach (Row m in Rows(source, SpeciesTable, SpeciesRow) ?? (IEnumerable<Row>)Array.Empty<Row>())
                t.Species.Rows.Add((Value(m[1].Value, species), Value(m[2].Value, sounds)));
            return t;
        }

        internal static Dictionary<int, (int, int)> ParseEncounterMusic(string source, HgEngineSymbolTable sounds, HgEngineSymbolTable classes)
        {
            Dictionary<int, (int, int)> music = new Dictionary<int, (int, int)>();
            foreach (Row m in Rows(source, EncounterTable, EncounterRow) ?? (IEnumerable<Row>)Array.Empty<Row>())
            {
                int cls = Value(m[1].Value, classes);
                if (cls >= 0) music.TryAdd(cls, (Value(m[2].Value, sounds), Value(m[3].Value, sounds)));
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
            if (!TryTableCount(SpeciesTable, SpeciesRow, +1, out HgEngineHookList count, out error)) return false;
            return Edit(out error, text => AddSpeciesRow(text, species, combo, Sounds, Species)) && count.Save(out error);
        }

        /// <summary>Takes a Pokemon music row out, lowering the table's count byte with it.</summary>
        public static bool TryRemoveSpeciesRow(int row, out string error)
        {
            if (!TryTableCount(SpeciesTable, SpeciesRow, -1, out HgEngineHookList count, out error)) return false;
            return Edit(out error, text => RemoveSpeciesRow(text, row)) && count.Save(out error);
        }

        private const string ByteReplacementRelPath = "bytereplacement";

        // Changes the count under "# <table> table range" in bytereplacement by delta. The caller saves the list after
        // the table is written.
        private static bool TryTableCount(string table, RowShape shape, int delta, out HgEngineHookList updated, out string error)
        {
            updated = HgEngineHookList.Read(HgEngineHookKind.ByteReplacement);
            error = null;
            string source = Read(SourceRelPath, out _);
            if (updated == null || source == null) { error = $"{ByteReplacementRelPath} or {SourceRelPath} is missing from the checkout."; return false; }
            HgEngineHookEntry entry = updated.Entries.FirstOrDefault(e => e.Parsed && e.Heading == table + " table range");
            if (entry == null || entry.OverlayNumber >= 0 || entry.Bytes.Count != 1)
            { error = $"{ByteReplacementRelPath} has no {table} table range, so a row can't be added or removed."; return false; }
            int count = entry.Bytes[0];
            int rows = Rows(source, table, shape)?.Count ?? -1;
            if (rows != count) { error = $"{table} has {rows} rows but {ByteReplacementRelPath} counts {count}. Fix the count before adding or removing rows."; return false; }
            int next = count + delta;
            if (next < 1 || next > 255) { error = $"{table} can have 1 to 255 rows."; return false; }
            HgEngineHookList.ChangeBytes(entry, new[] { (byte)next });
            return true;
        }

        internal static (string Text, string Error) SetSpeciesCombo(string text, int row, int species, int combo,
            HgEngineSymbolTable sounds, HgEngineSymbolTable speciesTable)
        {
            IReadOnlyList<Row> rows = Rows(text, SpeciesTable, SpeciesRow);
            if (rows == null || row < 0 || row >= rows.Count) return (null, $"{SpeciesTable} has no row {row}.");
            Row m = rows[row];
            return (Replace(text, (m[2], Token(combo, m[2].Value, sounds, ComboPrefix)),
                                  (m[1], Token(species, m[1].Value, speciesTable, "SPECIES_"))), null);
        }

        internal static (string Text, string Error) AddSpeciesRow(string text, int species, int combo,
            HgEngineSymbolTable sounds, HgEngineSymbolTable speciesTable)
        {
            IReadOnlyList<Row> rows = Rows(text, SpeciesTable, SpeciesRow);
            int end = BlockEnd(text, SpeciesTable);
            if (rows == null || end < 0) return (null, $"{SourceRelPath} has no {SpeciesTable}.");
            string newline = text.Contains("\r\n") ? "\r\n" : "\n";
            string indent = rows.Count > 0 ? HgEngineSourcePatcher.IndentOfLine(text, rows[^1].Index) : "    ";
            string line = $"{indent}{{ .species = {Token(species, null, speciesTable, "SPECIES_")}, .combo = {Token(combo, null, sounds, ComboPrefix)} }},{newline}";
            return (text.Insert(text.LastIndexOf('\n', end) + 1, line), null);
        }

        internal static (string Text, string Error) RemoveSpeciesRow(string text, int row)
        {
            IReadOnlyList<Row> rows = Rows(text, SpeciesTable, SpeciesRow);
            if (rows == null || row < 0 || row >= rows.Count) return (null, $"{SpeciesTable} has no row {row}.");
            return (HgEngineSourcePatcher.RemoveElement(text, rows[row].Index, rows[row].Index + rows[row].Length), null);
        }

        /// <summary>Sets a class's eye-contact music. A new row raises the count the game searches, as hg-engine's
        /// guide to adding classes says it must.</summary>
        public static bool TrySetEncounterMusic(int trainerClass, ushort johto, ushort kanto, out string error)
        {
            error = null;
            string source = Read(SourceRelPath, out _);
            bool adding = source != null && !(Rows(source, EncounterTable, EncounterRow) ?? Array.Empty<Row>())
                .Any(m => Value(m[1].Value, Classes) == trainerClass);
            HgEngineHookList count = null;
            if (adding && !TryTableCount(EncounterTable, EncounterRow, +1, out count, out error)) return false;
            return Edit(out error, text => SetEncounterMusic(text, trainerClass, johto, kanto, Sounds, Classes))
                && (!adding || count.Save(out error));
        }

        internal static (string Text, string Error) SetCombo(string text, int index, ushort transition, ushort sequence, HgEngineSymbolTable sounds)
        {
            Row row = null;
            int next = 0;
            foreach (Row m in Rows(text, ComboTable, ComboRow) ?? (IEnumerable<Row>)Array.Empty<Row>())
            {
                int at = m[1] != null ? Value(m[1].Value, sounds) : next;
                next = at + 1;
                if (at == index) { row = m; break; }
            }
            if (row == null) return (null, $"{ComboTable} has no combo {index}.");
            return (Replace(text, (row[3], Seq(sequence, row[3].Value, sounds)),
                                  (row[2], Token(transition, row[2].Value, sounds, null, hex: true))), null);
        }

        internal static (string Text, string Error) SetClassCombo(string text, int row, int trainerClass, int combo,
            HgEngineSymbolTable sounds, HgEngineSymbolTable classes)
        {
            IReadOnlyList<Row> rows = Rows(text, ClassTable, ClassRow);
            if (rows == null || row < 0 || row >= rows.Count) return (null, $"{ClassTable} has no row {row}.");
            Row m = rows[row];
            string cls = Token(trainerClass, m[1].Value, classes, ClassPrefix);
            string comboText = m[3] != null
                ? (Value(m[2].Value, sounds) == combo ? m[2].Value : Token(combo, null, sounds, ComboPrefix)) + " * 4"
                : Token(combo * 4, m[2].Value, null, null);
            return (Replace(text, (m, $"{{ {cls}, {comboText} }}")), null);
        }

        internal static (string Text, string Error) SetEncounterMusic(string text, int trainerClass, ushort johto, ushort kanto,
            HgEngineSymbolTable sounds, HgEngineSymbolTable classes)
        {
            IReadOnlyList<Row> rows = Rows(text, EncounterTable, EncounterRow);
            if (rows == null) return (null, $"{SourceRelPath} has no {EncounterTable}.");
            foreach (Row m in rows)
                if (Value(m[1].Value, classes) == trainerClass)
                    return (Replace(text, (m[3], Seq(kanto, m[3].Value, sounds)),
                                          (m[2], Seq(johto, m[2].Value, sounds))), null);

            string designator = Token(trainerClass, null, classes, ClassPrefix);
            int lineStart = HgEngineSourcePatcher.LineStart(text, BlockEnd(text, EncounterTable));
            string indent = rows.Count > 0 ? HgEngineSourcePatcher.IndentOfLine(text, rows[^1].Index) : "        ";
            string line = $"{indent}{{ .class = {designator}, .music1 = {Seq(johto, null, sounds)}, .music2 = {Seq(kanto, null, sounds)} }},{(text.Contains("\r\n") ? "\r\n" : "\n")}";
            return (text.Insert(lineStart, line), null);
        }

        // ── Source text ──────────────────────────────────────────────────────────────────────────

        /// <summary>A value in a row, or the whole row: its text and where it sits.</summary>
        private record Cell(string Value, int Index, int Length);

        /// <summary>One table row. Cells count from 1. An optional cell the row doesn't have is null.</summary>
        private sealed record Row(string Value, int Index, int Length, Cell[] Cells) : Cell(Value, Index, Length)
        {
            public Cell this[int cell] => Cells[cell - 1];
        }

        /// <summary>A row's cells, or null when it isn't the shape the table needs.</summary>
        private delegate Cell[] RowShape(CInitItem item, string text);

        private static Cell Of(CInitItem value, string text)
        {
            if (value?.List != null) return null;
            string written = value?.ValueText(text);
            if (string.IsNullOrWhiteSpace(written)) return null;
            int start = value.ValueStart + written.Length - written.TrimStart().Length;
            return new Cell(written.Trim(), start, written.Trim().Length);
        }

        private static Cell[] Filled(params Cell[] cells) => cells.All(c => c != null) ? cells : null;

        // [SEQ_A] = { transition, sequence }, or the same with no index.
        private static Cell[] ComboRow(CInitItem item, string text)
        {
            if (item.List?.Items.Count != 2) return null;
            Cell[] pair = Filled(Of(item.List.Items[0], text), Of(item.List.Items[1], text));
            if (pair == null) return null;
            string index = item.IndexText?.Trim();
            return new[] { index == null ? null : new Cell(index, 0, 0), pair[0], pair[1] };
        }

        // { class, combo } or { class, COMBO * 4 }. The last cell is set in the second form.
        private static Cell[] ClassRow(CInitItem item, string text)
        {
            if (item.List?.Items.Count != 2) return null;
            Cell cls = Of(item.List.Items[0], text), combo = Of(item.List.Items[1], text);
            if (cls == null || combo == null) return null;
            List<CToken> tokens = CLexer.Tokenize(text, combo.Index, combo.Index + combo.Length);
            if (tokens.Count == 1) return new[] { cls, combo, null };
            if (tokens.Count != 3 || !tokens[1].Is(text, "*") || !tokens[2].Is(text, "4")) return null;
            return new[] { cls, new Cell(tokens[0].Text(text), tokens[0].Start, tokens[0].Length), new Cell("* 4", tokens[1].Start, tokens[2].End - tokens[1].Start) };
        }

        private static Cell[] SpeciesRow(CInitItem item, string text) =>
            Filled(Of(item.List?.Field("species"), text), Of(item.List?.Field("combo"), text));

        private static Cell[] EncounterRow(CInitItem item, string text) =>
            Filled(Of(item.List?.Field("class"), text), Of(item.List?.Field("music1"), text), Of(item.List?.Field("music2"), text));

        // The second byte of a class row is the combo's offset into the 4-byte-row combo table.
        private static int ClassCombo(Row m, HgEngineSymbolTable sounds)
        {
            int v = Value(m[2].Value, sounds);
            return v < 0 || m[3] != null ? v : v / 4;
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
            if (!TablesInSource) { error = $"This hg-engine checkout doesn't build its music tables from {SourceRelPath}."; return false; }
            string text = Read(SourceRelPath, out string path);
            (string edited, string err) = change(text);
            if (edited == null) { error = err; return false; }
            if (edited != text) HgEngineFileCache.WriteText(path, edited);
            return true;
        }

        // Null when the table is missing, a row sits under #if, or a row isn't the shape the table needs.
        private static IReadOnlyList<Row> Rows(string text, string table, RowShape shape)
        {
            CDeclaration decl = CSourceFile.For(text).Find(table);
            if (decl == null) return null;
            List<Row> rows = new List<Row>();
            foreach (CInitItem item in decl.Init.Items)
            {
                if (item.IsConditional) return null;
                Cell[] cells = shape(item, text);
                if (cells == null) return null;
                rows.Add(new Row(text.Substring(item.Start, item.End - item.Start), item.Start, item.End - item.Start, cells));
            }
            return rows;
        }

        // Where the table's closing brace is, for inserting rows before it.
        private static int BlockEnd(string text, string table) => CSourceFile.For(text).Find(table)?.Init.Close ?? -1;

        // Replaces spans right to left so earlier offsets stay valid.
        private static string Replace(string text, params (Cell At, string With)[] edits)
        {
            Array.Sort(edits, (a, b) => b.At.Index.CompareTo(a.At.Index));
            foreach ((Cell at, string with) in edits) text = text.Remove(at.Index, at.Length).Insert(at.Index, with);
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

        internal static int Value(string token, HgEngineSymbolTable symbols) => HgEngineSymbolTable.TryValue(token, out int v, symbols) ? v : -1;
    }
}
