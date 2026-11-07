using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.HgEngine
{
    /// <summary>Read-only, layout-agnostic typed access to one C designated-initializer block's fields.
    /// Built entirely on source text; a field not present in the block's current source just returns
    /// false, never guessed at.</summary>
    public readonly struct HgEngineSourceBlock
    {
        public string Raw { get; }
        public HgEngineSourceBlock(string raw) { Raw = raw ?? ""; }

        public bool TryGetRaw(IReadOnlyList<FieldPathSegment> path, out string raw) =>
            HgEngineSourcePatcher.TryGetFieldValueInBlock(Raw, path, out raw);

        /// <summary>Resolves a field to an int: a plain literal, or a symbolic constant looked up in
        /// <paramref name="headerRelPath"/>'s table if given.</summary>
        public bool TryGetSymbol(IReadOnlyList<FieldPathSegment> path, string headerRelPath, out int value)
        {
            value = 0;
            if (!TryGetRaw(path, out string raw)) return false;
            return TryResolveToken(raw, headerRelPath, out value);
        }

        /// <summary>Resolves a field against the first of several headers that defines its name.</summary>
        public bool TryGetSymbolIn(IReadOnlyList<FieldPathSegment> path, IReadOnlyList<string> headers, out int value)
        {
            value = 0;
            return TryGetRaw(path, out string raw) && TryResolveTokenIn(raw, headers, out value);
        }

        /// <summary>An OR of names, each resolved against several headers. Fails on any unknown term.</summary>
        public bool TryGetFlagsValueIn(IReadOnlyList<FieldPathSegment> path, IReadOnlyList<string> headers, out int value)
        {
            value = 0;
            if (!TryGetRaw(path, out string raw)) return false;
            foreach (string token in CLexer.Split(raw, "|"))
            {
                if (!TryResolveTokenIn(token, headers, out int term)) { value = 0; return false; }
                value |= term;
            }
            return true;
        }

        public static bool TryResolveTokenIn(string token, IReadOnlyList<string> headers, out int value)
        {
            if (TryResolveToken(token, (string)null, out value)) return true;
            if (headers != null)
                foreach (string header in headers)
                    if (TryResolveToken(token, header, out value)) return true;
            value = 0;
            return false;
        }

        /// <summary>Shorthand for <see cref="TryGetSymbol"/> with no header, for fields that are always plain literals.</summary>
        public bool TryGetInt(IReadOnlyList<FieldPathSegment> path, out int value) => TryGetSymbol(path, null, out value);

        /// <summary>Reads a quoted C string field, unescaping `\\` and `\"`. Other backslash escapes
        /// (the game's own `\n`/`\r` markup) are left as literal characters.</summary>
        public bool TryGetString(IReadOnlyList<FieldPathSegment> path, out string value)
        {
            value = null;
            if (!TryGetRaw(path, out string raw)) return false;
            value = UnquoteCString(raw);
            return value != null;
        }

        /// <summary>Resolves a flags-style field written as an OR-expression of names to its combined
        /// int value. Fails if any term can't be resolved.</summary>
        public bool TryGetFlagsValue(IReadOnlyList<FieldPathSegment> path, string headerRelPath, out int value)
        {
            value = 0;
            if (!TryGetRaw(path, out string raw)) return false;

            int result = 0;
            foreach (string token in CLexer.Split(raw, "|"))
            {
                if (!TryResolveToken(token, headerRelPath, out int termValue)) return false;
                result |= termValue;
            }
            value = result;
            return true;
        }

        /// <summary>Returns however many elements are actually written in an array field's source text.</summary>
        public IReadOnlyList<HgEngineSourceBlock> GetArrayElements(IReadOnlyList<FieldPathSegment> path)
        {
            if (!TryGetRaw(path, out string raw)) return System.Array.Empty<HgEngineSourceBlock>();
            return HgEngineSourcePatcher.SplitArrayValue(raw).Select(e => new HgEngineSourceBlock(e)).ToList();
        }

        /// <summary>Resolves a raw token (a literal, or a symbolic constant if a header is given) to an
        /// int. Public so bare-scalar array elements (no `{ }` wrapper) can resolve directly.</summary>
        public static bool TryResolveToken(string token, string headerRelPath, out int value)
        {
            token = token.Trim();
            if (CLexer.TryParseInt(token, out value)) return true;
            if (headerRelPath == null) return false;
            HgEngineSymbolTable table = HgEngineSymbolTable.Load(headerRelPath);
            return table != null && table.TryGetValue(token, out value);
        }

        private static string UnquoteCString(string raw)
        {
            raw = raw.Trim();
            if (raw.Length < 2 || raw[0] != '"' || raw[^1] != '"') return null;
            StringBuilder sb = new StringBuilder(raw.Length - 2);
            for (int i = 1; i < raw.Length - 1; i++)
            {
                if (raw[i] == '\\' && i + 1 < raw.Length - 1 && (raw[i + 1] == '\\' || raw[i + 1] == '"'))
                {
                    sb.Append(raw[i + 1]);
                    i++;
                }
                else
                {
                    sb.Append(raw[i]);
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>Loads one trainer's whole `[id] = { ... }` entry out of data/Trainers.c, and formats
    /// plain strings as escaped C-string literals for `.name`/`.text` fields.</summary>
    public static class HgEngineTrainerSource
    {
        private const string SourceRelPath = "data/Trainers.c";

        public static bool TryLoad(int trainerId, out HgEngineSourceBlock block, out string error) => TryLoad(trainerId, false, out block, out error);

        /// <summary>Same as <see cref="TryLoad(int, out HgEngineSourceBlock, out string)"/>, for an editor that will save
        /// this trainer. The save compares against what this loads.</summary>
        public static bool TryLoadForEditing(int trainerId, out HgEngineSourceBlock block, out string error) => TryLoad(trainerId, true, out block, out error);

        private static bool TryLoad(int trainerId, bool forEditing, out HgEngineSourceBlock block, out string error)
        {
            block = default;
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }

            string path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) { error = $"{path} is missing from the checkout."; return false; }

            string text = HgEngineFileCache.GetText(path);
            if (!Entries(text).TryGetValue(trainerId, out TrainerEntry entry))
            { error = $"Trainers.c has no trainer {trainerId}."; return false; }

            block = new HgEngineSourceBlock(text.Substring(entry.Open, entry.Close - entry.Open + 1));
            if (forEditing) HgEngineLoadedEntries.Store(path, entry.Token, block.Raw);
            return true;
        }

        private readonly record struct TrainerEntry(string Token, int Open, int Close);

        private static string _indexedText;
        private static Dictionary<int, TrainerEntry> _indexed;

        /// <summary>Every trainer entry by id, however the file numbers or orders them.</summary>
        private static Dictionary<int, TrainerEntry> Entries(string text)
        {
            if (ReferenceEquals(text, _indexedText)) return _indexed;

            Dictionary<int, TrainerEntry> entries = new Dictionary<int, TrainerEntry>();
            string[] headers = CSourceFile.For(text).Includes.Select(p => p.Replace("../", "")).Where(p => p.StartsWith("include/", StringComparison.Ordinal)).ToArray();
            Func<string, int?> lookup = HgEngineSourceFields.NameLookup(headers);
            foreach ((string token, (int open, int close)) in HgEngineEntryIndex.Build(text))
                if (open >= 0 && HgEngineSourceExpression.TryEvaluate(token, lookup, out int id) && id >= 0) entries.TryAdd(id, new TrainerEntry(token, open, close));

            _indexedText = text;
            _indexed = entries;
            return entries;
        }

        /// <summary>The text between a trainer's brackets in Trainers.c: its number, or the constant the file uses.</summary>
        internal static string TokenFor(int trainerId)
        {
            if (!HgEngineProject.IsActive) return trainerId.ToString();
            string path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return trainerId.ToString();
            return Entries(HgEngineFileCache.GetText(path)).TryGetValue(trainerId, out TrainerEntry entry) ? entry.Token : trainerId.ToString();
        }

        /// <summary>The trainers trainerdatagen builds messages for: only those listed in sTrainerTextOrder.</summary>
        public static bool TryReadTextOrder(out HashSet<int> listed, out string error)
        {
            listed = new HashSet<int>();
            if (!TryFindTextOrder(out _, out string text, out int open, out int close, out error)) return false;
            foreach (string token in ElementScanner.SplitElementValues(text, open, close))
            {
                if (!HgEngineSourceBlock.TryResolveToken(token, (string)null, out int id))
                { error = $"sTrainerTextOrder lists \"{token.Trim()}\", which isn't a trainer number."; return false; }
                listed.Add(id);
            }
            return true;
        }

        private const string TableName = "sTrainerData";

        private static string TrainersPath => Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));

        /// <summary>How many trainers the file holds, counting any gap, or -1.</summary>
        public static int Count()
        {
            if (!File.Exists(TrainersPath)) return -1;
            Dictionary<int, TrainerEntry> entries = Entries(HgEngineFileCache.GetText(TrainersPath));
            return entries.Count == 0 ? 0 : entries.Keys.Max() + 1;
        }

        private const string TrainerScriptsRelPath = "data/scr_seq/scr_seq_00953_trainerscript.s";

        private const string NormalTrainerScript = "scr_seq_0953_normalTrainer";
        private static string TrainerScriptsPath => System.IO.Path.Combine(HgEngineProject.RepoPathUnc, TrainerScriptsRelPath.Replace('/', System.IO.Path.DirectorySeparatorChar));

        // Each scrdef line's label and where the line ends, in table order.
        private static List<(string Label, int LineEnd)> ScriptEntries(string text) =>
            AsmSourceFile.For(text).Lines.Where(l => l.Is(text, 0, "scrdef") && l.Words.Count == 2)
                .Select(l => (l.Words[1].Text(text), l.End)).ToList();

        /// <summary>
        /// The trainer whose eye-contact entry is the approach script, so it can't be used on the field: trainer N runs
        /// entry N - 1 of the trainer script table. -1 when the table can't be read.
        /// </summary>
        public static int ApproachTrainer()
        {
            if (!File.Exists(TrainerScriptsPath)) return -1;
            int entry = ScriptEntries(HgEngineFileCache.GetText(TrainerScriptsPath)).FindIndex(e => e.Label != NormalTrainerScript);
            return entry < 0 ? -1 : entry + 1;
        }

        /// <summary>
        /// Gives a trainer its normal eye-contact entry, appending entries after the last one as the table's own note
        /// asks. False when that entry is a different script.
        /// </summary>
        public static bool TryGiveScript(int trainerId, out string error)
        {
            error = null;
            if (!File.Exists(TrainerScriptsPath)) { error = $"{TrainerScriptsRelPath} is missing from the checkout."; return false; }
            string text = HgEngineFileCache.GetText(TrainerScriptsPath);
            List<(string Label, int LineEnd)> entries = ScriptEntries(text);
            if (entries.Count == 0) { error = $"{TrainerScriptsRelPath} has no trainer script entries to add after."; return false; }
            int entry = trainerId - 1;
            if (entry < entries.Count)
            {
                if (entries[entry].Label == NormalTrainerScript) return true;
                error = $"Trainer {trainerId}'s entry in {TrainerScriptsRelPath} runs {entries[entry].Label}, not the normal trainer script.";
                return false;
            }
            string nl = text.Contains("\r\n") ? "\r\n" : "\n";
            string added = string.Concat(Enumerable.Range(entries.Count, entry - entries.Count + 1).Select(i => $"{nl}/* Trainer {i,3} */ scrdef {NormalTrainerScript}"));
            int after = entries[^1].LineEnd;
            if (after > 0 && text[after - 1] == '\r') after--;
            return HgEngineVerifiedWrite.TryWrite(TrainerScriptsPath, TrainerScriptsRelPath, text.Insert(after, added), written =>
                ScriptEntries(written) is List<(string Label, int LineEnd)> back && back.Count > entry && back[entry].Label == NormalTrainerScript
                    ? null : $"trainer {trainerId} has no entry", out error);
        }

        /// <summary>Appends a trainer shaped like the last one, so it has every field that one declares.</summary>
        public static bool TryAdd(string name, out int trainerId, out string error)
        {
            trainerId = -1;
            string text = File.Exists(TrainersPath) ? HgEngineFileCache.GetText(TrainersPath) : null;
            CDeclaration table = text == null ? null : CSourceFile.For(text).Find(TableName);
            if (table == null || table.Init.Items.Count == 0) { error = $"{SourceRelPath} has no {TableName} to add to."; return false; }

            CInitItem last = table.Init.Items[^1];
            if (last.List == null || last.Designators.Count == 0) { error = $"{SourceRelPath}'s last trainer isn't a [N] = {{ ... }} entry to copy."; return false; }
            Dictionary<int, TrainerEntry> known = Entries(text);
            if (known.Count == 0) { error = $"{SourceRelPath} has no trainer DSPRE can read."; return false; }
            trainerId = known.Keys.Max() + 1;
            string entry = text.Substring(last.Start, last.End - last.Start);
            // Positions in the copied entry, applied from the end so earlier positions stay valid.
            List<(int At, int Length, string With)> edits = new List<(int, int, string)>();
            if (last.List.Field("text") is CInitItem lines)
                edits.Add((lines.ValueStart - last.Start, lines.ValueEnd - lines.ValueStart, PlaceholderText(HgEngineSourcePatcher.Indent(text, lines.Start))));
            else
            {
                string fieldIndent = last.List.Items.FirstOrDefault() is CInitItem first ? HgEngineSourcePatcher.Indent(text, first.Start) : "        ";
                int lineStart = HgEngineSourcePatcher.LineStart(entry, last.List.Close - last.Start);
                edits.Add((lineStart, 0, $"{fieldIndent}.text = {PlaceholderText(fieldIndent)},\n"));
            }
            if (last.List.Field("name") is CInitItem named)
                edits.Add((named.ValueStart - last.Start, named.ValueEnd - named.ValueStart, ToCStringLiteral(name)));
            // The designator goes last: every other edit sits after it.
            edits.Add((0, last.Designators[0].End - last.Start, $"[{trainerId}]"));
            foreach ((int at, int length, string with) in edits.OrderByDescending(e => e.At))
                entry = entry.Remove(at, length).Insert(at, with);

            string indent = HgEngineSourcePatcher.Indent(text, last.Start);
            int added = trainerId;
            if (!HgEngineVerifiedWrite.TryWrite(TrainersPath, SourceRelPath, text.Insert(table.Init.Close, $"{indent}{entry},\n\n"),
                    written => Entries(written).ContainsKey(added) ? null : $"trainer {added} is missing", out error))
                return false;
            return TryAddToTextOrder(trainerId, out error);
        }

        // Without lines the game shows an empty box before and after the battle.
        private static string PlaceholderText(string indent)
        {
            (string Type, string Text)[] lines =
            {
                ("TRMSG_INTRO", "Let’s battle!\\\\r"),
                ("TRMSG_LOSE", "I lost!\\\\n"),
                ("TRMSG_AFTER", "That was a good battle.\\\\n"),
            };
            StringBuilder sb = new StringBuilder("{");
            foreach ((string type, string line) in lines)
                sb.Append($"\n{indent}    {{\n{indent}        .type = {type},\n{indent}        .text = \"{line}\",\n{indent}    }},");
            return sb.Append($"\n{indent}}}").ToString();
        }

        /// <summary>Removes the last trainer from the table and from sTrainerTextOrder.</summary>
        public static bool TryRemoveLast(out int trainerId, out string error)
        {
            trainerId = -1;
            error = null;
            string text = File.Exists(TrainersPath) ? HgEngineFileCache.GetText(TrainersPath) : null;
            CDeclaration table = text == null ? null : CSourceFile.For(text).Find(TableName);
            if (table == null || table.Init.Items.Count == 0) { error = $"{SourceRelPath} has no {TableName}."; return false; }

            CInitItem last = table.Init.Items[^1];
            Dictionary<int, TrainerEntry> entries = Entries(text);
            if (entries.Count == 0) { error = $"{SourceRelPath} has no trainer DSPRE can read."; return false; }
            int id = entries.Keys.Max();
            // The caller checked the highest number for uses, so that trainer has to be the block that goes.
            if (!(entries[id].Open >= last.Start && entries[id].Open < last.End))
            { error = $"Trainer {id} isn't the last entry in {SourceRelPath}, so it can't be removed as the last trainer."; return false; }
            trainerId = id;
            int start = HgEngineSourcePatcher.LineStart(text, last.Start), end = table.Init.Close;
            text = text.Remove(start, HgEngineSourcePatcher.LineStart(text, end) - start);

            CDeclaration order = CSourceFile.For(text).Find("sTrainerTextOrder");
            string edited = text;
            CInitItem listed = order?.Init.Items.FirstOrDefault(i => i.ValueText(edited).Trim() == id.ToString());
            if (listed != null) text = HgEngineSourcePatcher.RemoveElement(text, listed.Start, listed.End);
            return HgEngineVerifiedWrite.TryWrite(TrainersPath, SourceRelPath, text, written =>
                Entries(written) is Dictionary<int, TrainerEntry> back && !back.ContainsKey(id) && back.Count == entries.Count - 1
                    ? null : $"trainer {id} is still there", out error);
        }

        /// <summary>Appends a trainer to sTrainerTextOrder when it is missing. Appending keeps every listed
        /// trainer's message numbers where they were.</summary>
        public static bool TryAddToTextOrder(int trainerId, out string error)
        {
            if (!TryReadTextOrder(out HashSet<int> listed, out error)) return false;
            if (listed.Contains(trainerId)) return true;
            TryFindTextOrder(out string path, out string text, out int open, out int close, out _);

            List<(int Start, int End)> spans = ElementScanner.ElementSpans(text, open, close);
            if (spans.Count > 0)
            {
                string line = "\n" + HgEngineSourcePatcher.IndentOfLine(text, spans[0].Start) + trainerId + ",";
                HgEngineSourcePatcher.InsertAfterLastElement(ref text, spans[^1].End, close, line);
            }
            else text = text.Insert(open + 1, "\n    " + trainerId + ",\n");

            return HgEngineVerifiedWrite.TryWrite(path, SourceRelPath, text, written =>
                CSourceFile.For(written).Find("sTrainerTextOrder")?.Init?.Items.Any(i => i.ValueText(written).Trim() == trainerId.ToString()) == true
                    ? null : $"trainer {trainerId} isn't in sTrainerTextOrder", out error);
        }

        private static bool TryFindTextOrder(out string path, out string text, out int open, out int close, out string error)
        {
            text = null;
            open = close = -1;
            error = null;
            path = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }

            path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) { error = $"{path} is missing from the checkout."; return false; }

            text = HgEngineFileCache.GetText(path);
            CDeclaration order = CSourceFile.For(text).Find("sTrainerTextOrder");
            if (order == null) { error = "sTrainerTextOrder wasn't found in Trainers.c."; return false; }
            open = order.Init.Open;
            close = order.Init.Close;
            return true;
        }

        /// <summary>Every trainer by id, in one pass over the file. A number the file skips is an empty block, because
        /// the list position is the trainer id and C fills the gap with an empty trainer.</summary>
        public static List<HgEngineSourceBlock> LoadAll()
        {
            List<HgEngineSourceBlock> blocks = new List<HgEngineSourceBlock>();
            if (!HgEngineProject.IsActive) return blocks;
            string path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return blocks;

            string text = HgEngineFileCache.GetText(path);
            Dictionary<int, TrainerEntry> entries = Entries(text);
            if (entries.Count == 0) return blocks;
            for (int id = 0; id <= entries.Keys.Max(); id++)
                blocks.Add(new HgEngineSourceBlock(entries.TryGetValue(id, out TrainerEntry entry) ? text.Substring(entry.Open, entry.Close - entry.Open + 1) : ""));
            return blocks;
        }

        /// <summary>
        /// hg-engine's species words carry a form above the species, as include/constants/species.h spells it:
        /// <c>MON_WITH_FORM(species, form) ((species) | ((form) &lt;&lt; 11))</c>. Trainers, wild and Safari
        /// encounters and evolutions all use it.
        /// </summary>
        public const int FormShift = 11;
        public const int SpeciesMask = (1 << FormShift) - 1;

        /// <summary>A species word as source text: the species' name, wrapped in MON_WITH_FORM when it has a form.</summary>
        public static string FormatPackedSpecies(int packed, string speciesHeader) =>
            FormatSpecies(packed & SpeciesMask, packed >> FormShift, HgEngineSymbolTable.Load(speciesHeader));

        /// <summary>Writes a form species such as SPECIES_RATTATA_ALOLAN as MON_WITH_FORM(base, n).</summary>
        public static string FormatSpecies(int speciesId, int form, HgEngineSymbolTable speciesTable)
        {
            if (HgEngineFormRegistry.TryGetBaseAndForm(speciesId, out int baseId, out int baseForm)) { speciesId = baseId; form = baseForm; }
            string name = speciesTable?.TryGetNameWithPrefix(speciesId, "SPECIES_", out string n) == true ? n : speciesId.ToString();
            return FormatSpecies(name, form);
        }

        /// <summary>Reads a party species value: a name or number, <c>MON_WITH_FORM(species, form)</c>, or
        /// <c>species | (form &lt;&lt; 11)</c>. The form sits in the top five bits of the species word.</summary>
        public static bool TryParseSpecies(string raw, string speciesHeader, out int species, out int form)
        {
            species = form = 0;
            if (raw == null) return false;
            int? Lookup(string name) => HgEngineSourceBlock.TryResolveToken(name, speciesHeader, out int v) ? v : null;
            if (!HgEngineSourceExpression.TryEvaluate(raw, Lookup, out int value)) return false;
            form = value >> FormShift;
            species = value & SpeciesMask;
            return true;
        }

        /// <summary>The same split with species names from a table already loaded.</summary>
        public static bool TryParseSpecies(string raw, HgEngineSymbolTable speciesTable, out int species, out int form)
        {
            species = form = 0;
            if (raw == null || !HgEngineSourceExpression.TryEvaluate(raw, n => speciesTable != null && speciesTable.TryGetValue(n, out int v) ? v : null, out int value)) return false;
            form = value >> FormShift;
            species = value & SpeciesMask;
            return true;
        }

        /// <summary>A species word with its form packed in, or 0, noting the first token that couldn't be read.</summary>
        internal static int SpeciesWord(string token, HgEngineSymbolTable speciesTable, ref string unreadable)
        {
            if (TryParseSpecies(token.Trim(), speciesTable, out int id, out int form)) return id | (form << FormShift);
            unreadable ??= token.Trim();
            return 0;
        }

        /// <summary>The species source value, wrapped in hg-engine's own macro when a form is set.</summary>
        public static string FormatSpecies(string speciesLiteral, int form) =>
            form > 0 ? $"MON_WITH_FORM({speciesLiteral}, {form})" : speciesLiteral;

        /// <summary>The build encodes only 0-9, A-Z and a-z and stops at 10 bytes.</summary>
        public static string ToEncodableNickname(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            StringBuilder sb = new StringBuilder(10);
            foreach (char c in value)
            {
                if (sb.Length == 10) break;
                if (c is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z') sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Reads 0/1 style fields that the source may also spell TRUE or FALSE.</summary>
        public static bool TryParseBool(string raw, out bool value)
        {
            value = false;
            if (raw == null) return false;
            raw = raw.Trim();
            if (raw == "TRUE") { value = true; return true; }
            if (raw == "FALSE") return true;
            if (!HgEngineSourceBlock.TryResolveToken(raw, null, out int n)) return false;
            value = n != 0;
            return true;
        }

        /// <summary>Formats a plain string as a quoted C string literal, escaping `\` and `"` only.</summary>
        public static string ToCStringLiteral(string value)
        {
            StringBuilder sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char c in value)
            {
                // A typed line break becomes the game's \n markup; a raw newline would break the C literal.
                if (c == '\r') continue;
                if (c == '\n') { sb.Append(@"\\n"); continue; }
                if (c == '\\' || c == '"') sb.Append('\\');
                sb.Append(c);
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
