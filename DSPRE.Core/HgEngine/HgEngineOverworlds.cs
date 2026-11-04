using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// hg-engine's overworlds. The game reads the tag table in src/field/overworld_table.c (gOWTagToFileNum is
    /// repointed), and a/0/8/1 is built entirely from source: data/graphics/overworlds (1_), its custom/ folder for
    /// new NPCs (2_), and each species' sprites/&lt;mon&gt;/overworld.png from pokegra.mk (3_). Members are the build
    /// folder's files in ordinal name order, which is how narcpy packs them.
    /// </summary>
    public static class HgEngineOverworlds
    {
        private const string TableRelPath = "src/field/overworld_table.c";
        private const string TableName = "gOWTagToFileNum";
        private static readonly string[] Headers = { TableRelPath, "include/constants/species.h", "include/constants/pokemon.h" };
        private const string SizePrefix = "OVERWORLD_SIZE_";
        private const string CustomFolder = "custom";
        private const string NpcMacro = "NEW_NPC_ENTRY";

        // ── The tag table ────────────────────────────────────────────────────────────────────────

        /// <summary>One table row. <see cref="PropertiesAt"/> is where its callback_params is written, -1 when the
        /// row is a macro that doesn't take them, in which case a write spells the row out.</summary>
        public sealed record Entry(int Tag, int Gfx, int Properties, int ItemStart, int ItemLength, int PropertiesAt, int PropertiesLength, string Expanded,
            string MacroName = null);

        private sealed record Macro(string Name, string[] Params, string Body);

        // A directive token holds the whole #define, continuations included.
        private const string CallbackField = "callback_params";

        public static bool TryReadTable(out List<Entry> entries, out string error)
        {
            entries = new List<Entry>();
            if (!TryReadTableText(out string text, out string path, out error)) return false;
            return TryParseTable(text, out entries, out _, out _, out error);
        }

        private static bool TryParseTable(string text, out List<Entry> entries, out int bodyOpen, out int bodyClose, out string error)
        {
            entries = new List<Entry>();
            bodyOpen = bodyClose = -1;
            error = null;

            CSourceFile source = CSourceFile.For(text);
            CInitList table = source.Find(TableName)?.Init;
            if (table == null) { error = $"{TableRelPath} has no {TableName} table DSPRE can read."; return false; }
            bodyOpen = table.Open;
            bodyClose = table.Close;

            Dictionary<string, Macro> macros = new Dictionary<string, Macro>(StringComparer.Ordinal);
            foreach (CDirective define in source.Directives.Where(d => d.Parameters != null))
                if (define.Body.Contains(".tag")) macros[define.Name] = new Macro(define.Name, define.Parameters.ToArray(), define.Body);
            Func<string, int?> lookup = HgEngineSourceFields.NameLookup(Headers);

            foreach (CInitItem item in table.Items)
            {
                if (item.IsConditional && HgEngineConfigState.Compiles(item.Conditions) == false) continue;

                if (item.List != null)
                {
                    if (!TryRow(item.List, text, lookup, out int t, out int g, out int p, out CInitItem cbItem))
                    { error = $"{TableName} has a row DSPRE can't evaluate: {Line(text, item.Start)}"; return false; }
                    if (t == 0xFFFF) break;
                    entries.Add(new Entry(t, g, p, item.Start, item.End - item.Start,
                        cbItem?.ValueStart ?? -1, cbItem == null ? 0 : cbItem.ValueEnd - cbItem.ValueStart, text.Substring(item.Start, item.End - item.Start)));
                    continue;
                }

                // Rows written as macro calls; the macro supplies the comma, so several calls can share one element.
                List<CToken> tokens = CLexer.Tokenize(text, item.ValueStart, item.ValueEnd);
                for (int k = 0; k < tokens.Count;)
                {
                    // A brace row can follow the calls in the same element, as the 0xFFFF terminator does.
                    if (tokens[k].Is(text, "{") && source.ListAt(tokens[k].Start) is CInitList braced)
                    {
                        if (!TryRow(braced, text, lookup, out int bt, out int bg, out int bp, out CInitItem bcb))
                        { error = $"{TableName} has a row DSPRE can't evaluate: {Line(text, tokens[k].Start)}"; return false; }
                        if (bt == 0xFFFF) return true;
                        int bEnd = braced.Close + 1;
                        entries.Add(new Entry(bt, bg, bp, tokens[k].Start, bEnd - tokens[k].Start,
                            bcb?.ValueStart ?? -1, bcb == null ? 0 : bcb.ValueEnd - bcb.ValueStart, text.Substring(tokens[k].Start, bEnd - tokens[k].Start)));
                        while (k < tokens.Count && tokens[k].Start < bEnd) k++;
                        continue;
                    }

                    if (tokens[k].Kind != CTokenKind.Identifier || k + 1 >= tokens.Count || !tokens[k + 1].Is(text, "(")
                        || !macros.TryGetValue(tokens[k].Text(text), out Macro macro))
                    { error = $"{TableName} has a row DSPRE can't read: {Line(text, tokens[k].Start)}"; return false; }

                    CCall call = source.CallAt(tokens[k].Start);
                    if (call == null || call.End > item.ValueEnd) { error = $"{macro.Name} isn't closed: {Line(text, tokens[k].Start)}"; return false; }
                    List<(int Start, int End)> args = call.Args;
                    if (args.Count != macro.Params.Length) { error = $"{macro.Name} is called with {args.Count} arguments: {Line(text, tokens[k].Start)}"; return false; }

                    string expanded = Expand(macro, args.Select(a => text.Substring(a.Start, a.End - a.Start)).ToArray());
                    CInitList row = CSourceFile.For("int row[] = { " + expanded + " };").Find("row")?.Init?.Items.FirstOrDefault()?.List;
                    int callStart = call.Start, callEnd = call.End;
                    if (row == null || !TryRow(row, "int row[] = { " + expanded + " };", lookup, out int tag, out int gfx, out int props, out _))
                    { error = $"{TableName} has a row DSPRE can't evaluate: {Line(text, callStart)}"; return false; }
                    if (tag == 0xFFFF) return true;

                    // A row whose callback_params is a parameter is written there, in the call itself.
                    string bodySource = "int row[] = { " + macro.Body + " };";
                    CInitItem callback = CSourceFile.For(bodySource).Find("row")?.Init?.Items.FirstOrDefault()?.List?.Field(CallbackField);
                    int param = callback != null ? Array.IndexOf(macro.Params, callback.ValueText(bodySource).Trim()) : -1;
                    int at = param >= 0 ? args[param].Start : -1, len = param >= 0 ? args[param].End - args[param].Start : 0;
                    entries.Add(new Entry(tag, gfx, props, callStart, callEnd - callStart, at, len, expanded, macro.Name));
                    while (k < tokens.Count && tokens[k].Start < callEnd) k++;
                }
            }
            return true;
        }

        /// <summary>Writes one row's callback_params, by name when the table defines one for that value.</summary>
        public static bool TrySetProperties(int tag, int properties, out string error)
        {
            if (!TryReadTableText(out string text, out string path, out error)) return false;
            if (!TryParseTable(text, out List<Entry> entries, out _, out _, out error)) return false;
            Entry entry = entries.FirstOrDefault(e => e.Tag == tag);
            if (entry == null) { error = $"{TableName} has no row for tag {tag}."; return false; }
            if (entry.Properties == properties) return true;

            HgEngineSymbolTable table = HgEngineSymbolTable.Load(TableRelPath);
            string literal = table?.TryGetNameWithPrefix(properties, SizePrefix, out string named) == true ? named : $"0x{properties:X4}";
            if (entry.PropertiesAt >= 0)
                text = text.Substring(0, entry.PropertiesAt) + literal + text.Substring(entry.PropertiesAt + entry.PropertiesLength);
            else
            {
                const string wrapper = "int row[] = { ";
                string expandedSource = wrapper + entry.Expanded + " };";
                CInitItem callback = CSourceFile.For(expandedSource).Find("row")?.Init?.Items.FirstOrDefault()?.List?.Field(CallbackField);
                if (callback == null) return false;
                string row = entry.Expanded.Substring(0, callback.ValueStart - wrapper.Length) + literal + entry.Expanded.Substring(callback.ValueEnd - wrapper.Length);
                text = text.Substring(0, entry.ItemStart) + row + "," + text.Substring(CLexer.PastComma(text, entry.ItemStart + entry.ItemLength, text.Length));
            }
            HgEngineFileCache.WriteText(path, text);
            return true;
        }

        // ── New NPCs ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Adds an NPC overworld the way hg-engine documents it: a custom/ PNG, JSON and palettes copied from
        /// <paramref name="templateGfx"/>, a NEW_NPC_ENTRY row, and MON_OVERWORLD_GFX_START one higher, since the new
        /// member sits before every follower. The PNG is the template's until a picture is written into it.
        /// </summary>
        public static bool TryAddNpc(int templateGfx, out int tag, out int gfx, out Member added, out string error)
        {
            tag = gfx = -1;
            added = null;
            if (!TryReadTableText(out string text, out string tablePath, out error)) return false;
            HgEngineSymbolTable table = HgEngineSymbolTable.Load(TableRelPath);
            if (table == null || !table.TryGetValue("NEW_NPC_GFX_START", out int npcGfxStart) || !table.TryGetValue("NEW_NPC_TAG_START", out int npcTagStart)
                || !table.TryGetValue("MON_OVERWORLD_GFX_START", out int monGfxStart))
            { error = $"{TableRelPath} doesn't define NEW_NPC_GFX_START, NEW_NPC_TAG_START and MON_OVERWORLD_GFX_START."; return false; }

            List<Member> members = Members(out error);
            if (members == null) return false;
            List<Member> custom = members.Where(m => m.Name.StartsWith("2_", StringComparison.Ordinal)).ToList();
            if (!TryParseTable(text, out List<Entry> rows, out _, out _, out error)) return false;
            int calls = rows.Count(r => r.MacroName == NpcMacro);
            if (custom.Count != calls || monGfxStart != npcGfxStart + custom.Count)
            {
                error = $"The checkout's NPC overworlds don't line up: {custom.Count} custom files, {calls} {NpcMacro} rows, and MON_OVERWORLD_GFX_START is {monGfxStart} "
                      + $"where {npcGfxStart + custom.Count} is expected. Fix those first so followers keep their sprites.";
                return false;
            }

            if (templateGfx < 0 || templateGfx >= members.Count || members[templateGfx].Png == null)
            { error = "Pick an overworld drawn from a PNG to copy, not one stored as a raw file."; return false; }
            Member template = members[templateGfx];

            int number = custom.Count;
            string stem = number.ToString("D4");
            // Every new member has to come after the existing custom ones, or the earlier NPCs shift.
            if (custom.Any(m => string.CompareOrdinal(m.Name, "2_" + stem + ".btx0") >= 0))
            { error = $"A custom overworld already sorts after {stem}.png; rename it so new ones go last."; return false; }

            string dir = Path.Combine(SpritesDir(), CustomFolder);
            Directory.CreateDirectory(dir);
            string png = Path.Combine(dir, stem + ".png"), json = Path.Combine(dir, stem + ".json");
            List<string> palettes = new List<string>();
            CopyAsNew(template.Png, png);
            CopyAsNew(template.Json, json);
            foreach (string pal in template.Palettes)
            {
                string suffix = Path.GetFileName(pal).Substring(Path.GetFileNameWithoutExtension(template.Png).Length);
                string copy = Path.Combine(dir, stem + suffix);
                CopyAsNew(pal, copy);
                palettes.Add(copy);
            }

            int rowStart = TerminatorRow(text);
            if (rowStart < 0) { error = $"{TableName} has no 0xFFFF row to add before."; return false; }
            int lineStart = text.LastIndexOf('\n', rowStart) + 1;
            string indent = text.Substring(lineStart, rowStart - lineStart);
            string newline = text.Contains("\r\n") ? "\r\n" : "\n";
            text = text.Substring(0, lineStart) + indent + $"{NpcMacro}({number})" + newline + text.Substring(lineStart);
            CDirective start = CSourceFile.For(text).FindDefine("MON_OVERWORLD_GFX_START");
            foreach (CToken literal in CLexer.Tokenize(text, start?.BodyStart ?? 0, start?.BodyEnd ?? 0))
            {
                if (literal.Kind != CTokenKind.Number) continue;
                text = string.Concat(text.AsSpan(0, literal.Start), (monGfxStart + 1).ToString(), text.AsSpan(literal.End));
                break;
            }
            HgEngineFileCache.WriteText(tablePath, text);

            tag = npcTagStart + number;
            gfx = npcGfxStart + number;
            added = new Member("2_" + stem + ".btx0", png, json, palettes, null);
            return true;
        }

        // ── a/0/8/1 members and their sources ────────────────────────────────────────────────────

        /// <summary>One a/0/8/1 member: drawn from a PNG with its JSON layout and palettes, or a raw file.</summary>
        public sealed record Member(string Name, string Png, string Json, IReadOnlyList<string> Palettes, string Bin);

        /// <summary>Every member in a/0/8/1 order, or null with <paramref name="error"/>.</summary>
        public static List<Member> Members(out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return null; }
            string root = HgEngineProject.RepoPathUnc;
            string dir = SpritesDir();
            if (!Directory.Exists(dir)) { error = $"{Rel(dir)} is missing from the checkout."; return null; }

            List<Member> members = new List<Member>();
            foreach (string png in Directory.GetFiles(dir, "*.png"))
                members.Add(FromPng("1_" + Path.GetFileNameWithoutExtension(png) + ".btx0", png));
            foreach (string bin in Directory.GetFiles(dir, "*.bin"))
                members.Add(new Member("1_" + Path.GetFileNameWithoutExtension(bin) + ".bin", null, null, Array.Empty<string>(), bin));
            string custom = Path.Combine(dir, CustomFolder);
            if (Directory.Exists(custom))
                foreach (string png in Directory.GetFiles(custom, "*.png"))
                    members.Add(FromPng("2_" + Path.GetFileNameWithoutExtension(png) + ".btx0", png));

            string graphics = Path.Combine(root, "data", "graphics");
            foreach (string mk in Directory.Exists(graphics) ? Directory.GetFiles(graphics, "*.mk") : Array.Empty<string>())
                foreach (HgEngineMakeFile.Rule rule in HgEngineMakeFile.For(HgEngineFileCache.GetText(mk)).Rules)
                {
                    const string built = "build/pokemonow/";
                    if (rule.Target.StartsWith(built, StringComparison.Ordinal) && rule.Target.EndsWith(".btx0", StringComparison.Ordinal)
                        && rule.Prerequisites.EndsWith(".png", StringComparison.Ordinal) && !rule.Prerequisites.Any(char.IsWhiteSpace))
                        members.Add(FromPng(rule.Target.Substring(built.Length), Path.Combine(root, rule.Prerequisites.Replace('/', Path.DirectorySeparatorChar))));
                }

            members.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return members;
        }

        private static Member FromPng(string name, string png)
        {
            string json = Path.ChangeExtension(png, ".json");
            List<string> palettes = new List<string>();
            if (File.Exists(json))
            {
                try
                {
                    using JsonDocument doc = System.Text.Json.JsonDocument.Parse(HgEngineFileCache.GetText(json));
                    if (doc.RootElement.TryGetProperty("palettes", out JsonElement pals))
                        foreach (JsonProperty pal in pals.EnumerateObject())
                            if (pal.Value.TryGetProperty("fileName", out JsonElement file))
                                palettes.Add(Path.Combine(Path.GetDirectoryName(png), Path.GetFileNameWithoutExtension(png) + "-" + file.GetString()));
                }
                catch (System.Text.Json.JsonException ex) { AppLogger.Error($"HgEngineOverworlds: {json}: {ex.Message}"); }
            }
            return new Member(name, png, json, palettes, null);
        }

        // ── BTX0 textures ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A 16-colour overworld texture as the build makes it from a PNG: one palette index per pixel, rows of the
        /// texture's width, and every palette as 0xRRGGBB. False for any other kind of texture.
        /// </summary>
        public static bool TryDecodeBtx(byte[] btx, out int width, out int height, out byte[] indices, out List<int[]> palettes)
        {
            width = height = 0;
            indices = null;
            palettes = new List<int[]>();
            if (btx == null || btx.Length < 0x14 || BitConverter.ToUInt32(btx, 0) != 0x30585442) return false;
            int tex = (int)BitConverter.ToUInt32(btx, 16);
            if (tex + 60 > btx.Length || BitConverter.ToUInt32(btx, tex) != 0x30584554) return false;
            int info = tex + BitConverter.ToUInt16(btx, tex + 14);
            int image = tex + (int)BitConverter.ToUInt32(btx, tex + 20);
            int paletteBytes = (int)(BitConverter.ToUInt32(btx, tex + 48) << 3);
            int paletteInfo = tex + (int)BitConverter.ToUInt32(btx, tex + 52);
            int paletteData = tex + (int)BitConverter.ToUInt32(btx, tex + 56);
            int textures = btx[info + 1];
            int param = BitConverter.ToUInt16(btx, info + 12 + textures * 4 + 6);
            if (((param >> 10) & 7) != 3) return false;
            int count = btx[paletteInfo + 1];
            if (count == 0 || paletteData + count * 32 > btx.Length) return false;

            width = 8 << ((param >> 4) & 7);
            height = (paletteData - image) * 2 / width;
            indices = new byte[width * height];
            for (int i = 0; i < indices.Length / 2; i++)
            {
                indices[i * 2] = (byte)(btx[image + i] & 0xF);
                indices[i * 2 + 1] = (byte)(btx[image + i] >> 4);
            }
            int colours = paletteBytes / count / 2;
            if (colours < 16) colours = 16;
            for (int p = 0; p < count; p++)
            {
                int[] pal = new int[16];
                for (int c = 0; c < 16; c++)
                {
                    int at = paletteData + (p * colours + c) * 2;
                    if (at + 2 > btx.Length) break;
                    int bgr = BitConverter.ToUInt16(btx, at);
                    pal[c] = ((bgr & 0x1F) << 19) | (((bgr >> 5) & 0x1F) << 11) | (((bgr >> 10) & 0x1F) << 3);
                }
                palettes.Add(pal);
            }
            return true;
        }

        // ── JASC palettes ────────────────────────────────────────────────────────────────────────

        /// <summary>A JASC palette's colours as 0xRRGGBB, or null when it can't be read.</summary>
        public static int[] ReadJasc(string path)
        {
            try
            {
                string[] lines = File.ReadAllLines(path);
                if (lines.Length < 3 || lines[0].Trim() != "JASC-PAL" || !int.TryParse(lines[2].Trim(), out int n)) return null;
                int[] colours = new int[n];
                for (int i = 0; i < n && 3 + i < lines.Length; i++)
                {
                    string[] rgb = lines[3 + i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    colours[i] = (int.Parse(rgb[0]) << 16) | (int.Parse(rgb[1]) << 8) | int.Parse(rgb[2]);
                }
                return colours;
            }
            catch (Exception ex) when (ex is IOException || ex is FormatException || ex is IndexOutOfRangeException)
            {
                AppLogger.Error($"HgEngineOverworlds: {path}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Writes colours into a JASC palette. A colour the DS would store the same keeps the file's own value, so an
        /// unchanged save leaves the file as it was.
        /// </summary>
        public static void WriteJasc(string path, IReadOnlyList<int> colours)
        {
            int[] before = File.Exists(path) ? ReadJasc(path) : null;
            // The file's own line endings are kept; JASC's are CRLF.
            string nl = before != null && !File.ReadAllText(path).Contains("\r\n") ? "\n" : "\r\n";
            StringBuilder sb = new System.Text.StringBuilder("JASC-PAL").Append(nl).Append("0100").Append(nl).Append(colours.Count).Append(nl);
            for (int i = 0; i < colours.Count; i++)
            {
                int c = before != null && i < before.Length && (before[i] & 0xF8F8F8) == (colours[i] & 0xF8F8F8) ? before[i] : colours[i];
                sb.Append((c >> 16) & 0xFF).Append(' ').Append((c >> 8) & 0xFF).Append(' ').Append(c & 0xFF).Append(nl);
            }
            string text = sb.ToString();
            if (File.Exists(path) && File.ReadAllText(path) == text) return;
            PlainFiles.WriteText(path, text);
        }

        // ── helpers ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Copies a source file as a new one. A copy keeps the original's date, and make skips a missing object
        /// whose sources all look older than the archive, so the new member would never be built.
        /// </summary>
        public static void CopyAsNew(string from, string to)
        {
            PlainFiles.Copy(from, to, overwrite: false);
            File.SetLastWriteTimeUtc(to, DateTime.UtcNow);
        }

        private static string SpritesDir()
        {
            string root = HgEngineProject.RepoPathUnc;
            string narcs = Path.Combine(root, HgEngineOwnedFiles.MakeFragmentRelPath);
            string rel = "data/graphics/overworlds";
            if (File.Exists(narcs))
            {
                string value = HgEngineMakeFile.For(HgEngineFileCache.GetText(narcs)).Value("OVERWORLDS_DEPENDENCIES_DIR");
                if (value != null && !value.Contains("$(")) rel = value;
            }
            return Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string Rel(string path) => Path.GetRelativePath(HgEngineProject.RepoPathUnc, path).Replace('\\', '/');

        private static bool TryReadTableText(out string text, out string path, out string error)
        {
            text = null;
            error = null;
            path = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }
            path = Path.Combine(HgEngineProject.RepoPathUnc, TableRelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) { error = $"{TableRelPath} is missing from the checkout."; return false; }
            text = HgEngineFileCache.GetText(path);
            return true;
        }

        // Fields by name, or by position when the row names none.
        private static bool TryRow(CInitList row, string text, Func<string, int?> lookup, out int tag, out int gfx, out int props, out CInitItem callbackItem)
        {
            tag = gfx = props = 0;
            bool positional = row.Items.All(i => i.Designators.Count == 0);
            CInitItem Field(string name, int position) =>
                row.Field(name) ?? (positional && position < row.Items.Count ? row.Items[position] : null);
            CInitItem t = Field("tag", 0), g = Field("gfx", 1);
            callbackItem = Field("callback_params", 2);
            return t != null && g != null && callbackItem != null
                && HgEngineSourceExpression.TryEvaluate(t.ValueText(text), lookup, out tag)
                && HgEngineSourceExpression.TryEvaluate(g.ValueText(text), lookup, out gfx)
                && HgEngineSourceExpression.TryEvaluate(callbackItem.ValueText(text), lookup, out props);
        }

        private static string Expand(Macro macro, string[] args)
        {
            StringBuilder sb = new System.Text.StringBuilder();
            int last = 0;
            foreach (CToken token in CLexer.Tokenize(macro.Body))
            {
                int p = token.Kind == CTokenKind.Identifier ? Array.IndexOf(macro.Params, token.Text(macro.Body)) : -1;
                if (p < 0) continue;
                sb.Append(macro.Body, last, token.Start - last).Append(args[p].Trim());
                last = token.End;
            }
            sb.Append(macro.Body, last, macro.Body.Length - last);
            return sb.ToString().Trim().TrimEnd(',').Trim();
        }

        private static string Line(string text, int at)
        {
            int end = text.IndexOf('\n', at);
            return text.Substring(at, (end < 0 ? text.Length : end) - at).Trim();
        }

        /// <summary>Where the tag table's last 0xFFFF terminator row starts, or -1. A 0xFFFF in a comment or a macro's
        /// arguments isn't a row.</summary>
        internal static int TerminatorRow(string text)
        {
            CSourceFile source = CSourceFile.For(text);
            CInitList table = source.Find(TableName)?.Init;
            if (table == null) return -1;
            int found = -1;
            foreach (CToken token in CLexer.Tokenize(text, table.Open + 1, table.Close))
                if (token.Is(text, "{") && source.ListAt(token.Start) is CInitList row && row.Items.Count > 0
                    && CLexer.TryParseInt(row.Items[0].ValueText(text), out int tag) && tag == 0xFFFF)
                    found = token.Start;
            return found;
        }
    }
}
