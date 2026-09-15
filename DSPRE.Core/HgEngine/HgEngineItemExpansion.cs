using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DSPRE.ROMFiles;

namespace DSPRE.HgEngine
{
    /// <summary>Mints a new item: a #define in item.h, a complete entry in itemdata.c, and its name and
    /// custom-item messages in the checkout's text sources. New ids append after the current max;
    /// MAX_BASE_ITEM_NUM stays untouched as the vanilla boundary marker.</summary>
    public static class HgEngineItemExpansion
    {
        private const string HeaderRelPath = "include/constants/item.h";
        private const string SourceRelPath = "data/itemdata/itemdata.c";
        private const string FileIdsHeaderRelPath = "include/constants/file.h";
        private const string Prefix = "ITEM_";
        private const string CustomPlaceholderName = "Custom Item";

        /// <summary>The last item id that shipped with hg-engine itself.</summary>
        public static bool TryGetVanillaBoundary(out int lastVanillaItemId)
        {
            lastVanillaItemId = -1;
            var items = HgEngineSymbolTable.Load(HeaderRelPath);
            return items != null && items.TryGetValue("MAX_BASE_ITEM_NUM", out lastVanillaItemId);
        }

        /// <summary>A new item that Add has numbered and shaped but not written. Save commits it.</summary>
        public sealed class PendingItem
        {
            public int Id { get; init; }
            public string Designator { get; init; }
            public string DisplayName { get; init; }
            /// <summary>The new itemdata.c entry as it is inserted, before any edits.</summary>
            public HgEngineSourceBlock Entry { get; init; }
        }

        private const string ConfigRelPath = "include/config.h";

        /// <summary>Numbers and shapes a new item without writing anything, so item.h readers keep seeing the
        /// current ids until <see cref="TryCommitItem"/> runs. Text the build generates refuses here, before
        /// the item is edited.</summary>
        public static bool TryPrepareItem(string displayName, out PendingItem pending, out string error)
        {
            pending = null;
            if (!TryReadSources(out _, out string headerText, out _, out string sourceText, out error)) return false;
            var config = HgEngineSymbolTable.Load(ConfigRelPath)?.ByName;
            if (!TryPrepare(headerText, sourceText, displayName, config, out var prepared, out _, out _, out error)) return false;
            if (!TryPlanTextWrites(headerText, HgEngineSymbolTable.Parse(headerText, config), prepared.Id, displayName, out _, out error)) return false;
            pending = prepared;
            return true;
        }

        /// <summary>Fills <paramref name="item"/> from the pending item's new entry, price included.</summary>
        public static bool TryReadTemplate(PendingItem pending, ItemData item, out string error) =>
            HgEngineItemSource.TryRead(pending.Entry, item, HgEngineSourceFields.NameLookup(HgEngineItemSource.Headers), out error);

        /// <summary>Writes the pending item's define, its itemdata.c entry with <paramref name="item"/>'s fields, and
        /// its text lines as one change. Run it inside a save session and call <see cref="CompleteAdd"/> once that commits.</summary>
        public static bool TryCommitItem(PendingItem pending, ItemData item, out string error)
        {
            if (!TryReadSources(out string headerPath, out string headerOriginal, out string sourcePath, out string sourceOriginal, out error)) return false;
            var config = HgEngineSymbolTable.Load(ConfigRelPath)?.ByName;
            if (!TryBuildCommit(headerOriginal, sourceOriginal, pending, item, config, w => HgEngineSourceFields.NameLookup(w.Headers),
                    HgEngineSourceFields.NameLookup(HgEngineItemSource.Headers), out string headerText, out string sourceText, out error))
                return false;
            if (!TryPlanTextWrites(headerOriginal, HgEngineSymbolTable.Parse(headerOriginal, config), pending.Id, pending.DisplayName, out var textWrites, out error))
                return false;

            var undo = new List<Action>();
            try
            {
                HgEngineFileCache.WriteText(headerPath, headerText);
                undo.Add(() => HgEngineFileCache.WriteText(headerPath, headerOriginal));
                HgEngineFileCache.WriteText(sourcePath, sourceText);
                undo.Add(() => HgEngineFileCache.WriteText(sourcePath, sourceOriginal));
                foreach (var w in textWrites)
                {
                    if (!HgEngineOwnedFiles.TryWriteLines(w.File, w.Updated, out string writeError))
                        throw new IOException(writeError);
                    var captured = w;
                    undo.Add(() => HgEngineOwnedFiles.TryWriteLines(captured.File, captured.Original, out _));
                }
            }
            catch (Exception ex)
            {
                for (int i = undo.Count - 1; i >= 0; i--)
                {
                    try { undo[i](); } catch (Exception undoEx) { AppLogger.Error("HgEngineItemExpansion rollback: " + undoEx.Message); }
                }
                error = $"The new item couldn't be written, so nothing was added: {ex.Message}";
                return false;
            }
            return true;
        }

        /// <summary>Runs after the add is on disk. The ROM copy shows the name before the next build; the source
        /// is what the build uses.</summary>
        public static void CompleteAdd(PendingItem pending)
        {
            HgEngineSymbolTable.ClearCache();
            var names = new ROMFiles.TextArchive(RomInfo.itemNamesTextNumber);
            while (names.messages.Count <= pending.Id) names.messages.Add("");
            names.messages[pending.Id] = pending.DisplayName;
            names.SaveToExpandedDir(RomInfo.itemNamesTextNumber, showSuccessMessage: false);
        }

        private static bool TryReadSources(out string headerPath, out string headerText, out string sourcePath, out string sourceText, out string error)
        {
            headerPath = headerText = sourcePath = sourceText = null;
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            headerPath = Path.Combine(HgEngineProject.RepoPathUnc, HeaderRelPath.Replace('/', '\\'));
            if (!File.Exists(headerPath)) { error = $"Source file not found: {headerPath}"; return false; }
            sourcePath = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            if (!File.Exists(sourcePath)) { error = $"Source file not found: {sourcePath}"; return false; }
            headerText = HgEngineFileCache.GetText(headerPath);
            sourceText = HgEngineFileCache.GetText(sourcePath);
            return true;
        }

        /// <summary>Pure half of <see cref="TryPrepareItem"/>: the id, the name and the rewritten texts.</summary>
        internal static bool TryPrepare(string headerText, string sourceText, string displayName, IReadOnlyDictionary<string, int> config,
            out PendingItem pending, out string newHeader, out string newSource, out string error)
        {
            pending = null;
            newHeader = newSource = null;
            error = null;
            var items = HgEngineSymbolTable.Parse(headerText, config);

            // Scan for the real max rather than using MAX_BASE_ITEM_NUM: that only tracks the vanilla boundary.
            int maxId = -1;
            foreach (var kv in items.ByName)
                if (kv.Key.StartsWith(Prefix, StringComparison.Ordinal) && kv.Value > maxId) maxId = kv.Value;
            if (maxId < 0) { error = "Could not find any existing ITEM_* constants."; return false; }
            int candidateId = maxId + 1;

            string designator = Prefix + HgEngineNameSlug.ToUniqueSlug(displayName, items, Prefix);
            if (!TryBuildSources(headerText, sourceText, designator, candidateId, out newHeader, out newSource, out error)) return false;
            if (!HgEngineSourcePatcher.TryFindEntry(newSource, designator, out int open, out int close))
            { error = $"The new {designator} entry could not be read back, so nothing was added."; return false; }

            pending = new PendingItem
            {
                Id = candidateId,
                Designator = designator,
                DisplayName = displayName,
                Entry = new HgEngineSourceBlock(newSource.Substring(open, close - open + 1)),
            };
            return true;
        }

        /// <summary>The define before MAX_TOTAL_ITEM_NUM, which then names it, and an entry shaped like ITEM_NONE
        /// before the source's final "};".</summary>
        internal static bool TryBuildSources(string headerText, string sourceText, string designator, int candidateId,
            out string newHeader, out string newSource, out string error)
        {
            newHeader = headerText;
            newSource = sourceText;
            error = null;
            if (!HgEngineHeaderEditor.TryInsertBeforeDefine(ref newHeader, "MAX_TOTAL_ITEM_NUM", $"#define {designator} {candidateId}\n\n"))
            { error = "Could not find MAX_TOTAL_ITEM_NUM in item.h to anchor the new item next to."; return false; }
            if (!HgEngineHeaderEditor.TryReplaceDefineValue(ref newHeader, "MAX_TOTAL_ITEM_NUM", designator))
            { error = "Could not update MAX_TOTAL_ITEM_NUM."; return false; }
            if (!HgEngineHeaderEditor.TryInsertBeforeFinalCloseBrace(ref newSource, BuildNewEntry(sourceText, designator)))
            { error = $"Could not find the end of {SourceRelPath} to insert the new item."; return false; }
            return true;
        }

        /// <summary>Pure half of <see cref="TryCommitItem"/>'s source texts. The add is prepared again from the
        /// current texts, so a header changed since Add refuses instead of reusing a stale id.</summary>
        internal static bool TryBuildCommit(string headerText, string sourceText, PendingItem pending, ItemData item, IReadOnlyDictionary<string, int> config,
            Func<HgEngineValueSpelling.Write, Func<string, int?>> lookupFor, Func<string, int?> priceLookup,
            out string newHeader, out string newSource, out string error)
        {
            newHeader = newSource = null;
            if (!TryPrepare(headerText, sourceText, pending.DisplayName, config, out var fresh, out string header, out string source, out error)) return false;
            if (fresh.Id != pending.Id || fresh.Designator != pending.Designator)
            { error = "item.h changed after the item was added, so it was not saved. Discard it and add it again."; return false; }
            if (!TrySpliceItem(ref source, pending.Designator, item, lookupFor, priceLookup, out error)) return false;
            newHeader = header;
            newSource = source;
            return true;
        }

        /// <summary>Everything the new item's text files get, planned without writing. Null lines mean the
        /// checkout doesn't build that archive.</summary>
        private static bool TryPlanTextWrites(string headerText, HgEngineSymbolTable items, int candidateId, string displayName,
            out List<(HgEngineOwnedFile File, List<string> Original, List<string> Updated)> textWrites, out string error)
        {
            textWrites = new List<(HgEngineOwnedFile File, List<string> Original, List<string> Updated)>();
            string textArchive = HgEngineOwnedFiles.ArchiveOf(RomInfo.DirNames.textArchives);
            var nameFile = HgEngineOwnedFiles.Get(textArchive, RomInfo.itemNamesTextNumber);
            if (!TryPlanLine(nameFile, "item names", out var nameLines, out error)) return false;
            if (nameLines != null)
                textWrites.Add((nameFile, nameLines, SetLine(nameLines, candidateId, displayName, "")));

            int customOffset = CustomMessageOffset(headerText, items, candidateId);
            if (customOffset < 0) return true;

            var fileIds = HgEngineSymbolTable.Load(FileIdsHeaderRelPath);
            foreach (string define in new[] { "MSG_DATA_ITEM_DESCRIPTION_CUSTOM", "MSG_DATA_ITEM_NAME_ARTICLE_CUSTOM",
                                              "MSG_DATA_ITEM_NAME_PLURAL_CUSTOM", "MSG_DATA_ITEM_GIVE_ITEM_CUSTOM" })
            {
                if (fileIds == null || !fileIds.TryGetValue(define, out int archiveId)) continue;
                var file = HgEngineOwnedFiles.Get(textArchive, archiveId);
                if (!TryPlanLine(file, define, out var lines, out error)) return false;
                if (lines == null) continue;

                int firstCustomId = candidateId - customOffset;
                string firstCustomName = customOffset > 0 && nameLines != null && firstCustomId < nameLines.Count
                    ? nameLines[firstCustomId] : null;
                bool isDescription = define == "MSG_DATA_ITEM_DESCRIPTION_CUSTOM";
                var updated = isDescription
                    ? PadCustomLines(lines, customOffset)
                    : WithCustomNameLine(lines, customOffset, displayName, firstCustomName);
                textWrites.Add((file, lines, updated));
            }
            return true;
        }

        /// <summary>Writes one item's fields and its price in a single all-or-nothing pass over itemdata.c.</summary>
        public static bool TryWriteItem(int itemId, ItemData item, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            string sourcePath = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            if (!File.Exists(sourcePath)) { error = $"Source file not found: {sourcePath}"; return false; }
            if (!HgEngineDesignators.TryResolve(HgEngineDomain.Items, itemId, out string designator))
            { error = $"Could not find item {itemId} in {HeaderRelPath}."; return false; }

            string text = HgEngineFileCache.GetText(sourcePath);
            string updated = text;
            if (!TrySpliceItem(ref updated, designator, item, w => HgEngineSourceFields.NameLookup(w.Headers),
                    HgEngineSourceFields.NameLookup(HgEngineItemSource.Headers), out error))
                return false;
            if (updated == text) return true;
            try { HgEngineFileCache.WriteText(sourcePath, updated); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                error = $"{Path.GetFileName(sourcePath)} couldn't be written: {ex.Message}";
                return false;
            }
            return true;
        }

        /// <summary>Patches one entry of itemdata.c text with the editor's fields and price; the text is left
        /// alone unless all of them are placed.</summary>
        internal static bool TrySpliceItem(ref string text, string designator, ItemData item,
            Func<HgEngineValueSpelling.Write, Func<string, int?>> lookupFor, Func<string, int?> priceLookup, out string error)
        {
            error = null;
            if (item.FullPrice > ItemData.MaxHgEnginePrice)
            { error = $"A price of {item.FullPrice} is more than hg-engine can hold ({ItemData.MaxHgEnginePrice})."; return false; }
            if (!HgEngineSourcePatcher.TryFindEntry(text, designator, out int open, out int close))
            { error = $"{designator} is not in {SourceRelPath}, so nothing was written."; return false; }

            HgEngineEntryIndex.TryPatch(text.Substring(open, close - open + 1), HgEngineItemSource.Fields, item, HgEngineItemSource.Headers,
                lookupFor, out string block, out var unresolved);
            const string key = "[X] = ";
            string keyed = key + block;
            if (!TryReplacePrice(ref keyed, "X", item.FullPrice, priceLookup)) unresolved.Add("price");
            if (unresolved.Count > 0)
            {
                error = $"{designator} in {SourceRelPath} has no {string.Join(", ", unresolved)}, so nothing was written.";
                return false;
            }
            text = string.Concat(text.AsSpan(0, open), keyed.AsSpan(key.Length), text.AsSpan(close + 1));
            return true;
        }

        /// <summary>Sets an entry's full price in whichever spelling it uses: ITEM_PRICE(p), or .price with the low
        /// 16 bits and .price_high with bits 16-19, as include/item.h's macro expands. A part whose value is
        /// unchanged keeps its text.</summary>
        internal static bool TryReplacePrice(ref string text, string designator, int price, Func<string, int?> lookup = null)
        {
            if (price < 0 || price > ItemData.MaxHgEnginePrice) return false;
            if (!HgEngineSourcePatcher.TryFindEntry(text, designator, out int open, out int close)) return false;
            lookup ??= _ => null;
            bool Same(string raw, int value) => HgEngineSourceExpression.TryEvaluate(raw.Trim(), lookup, out int old) && old == value;

            switch (FindPriceMacro(text, open, close, out int argStart, out int argEnd))
            {
                case null: return false;
                case true:
                    if (!Same(text.Substring(argStart, argEnd - argStart), price))
                        text = string.Concat(text.AsSpan(0, argStart), price.ToString(), text.AsSpan(argEnd));
                    return true;
            }

            string updated = text;
            if (!HgEngineSourcePatcher.TryGetFieldValue(updated, designator, PricePath, out string rawLow)) return false;
            int low = price & 0xFFFF, high = price >> 16;
            if (!Same(rawLow, low) && !HgEngineSourcePatcher.TryReplaceField(ref updated, designator, PricePath, low.ToString())) return false;
            if (HgEngineSourcePatcher.TryGetFieldValue(updated, designator, PriceHighPath, out string rawHigh))
            {
                if (!Same(rawHigh, high) && !HgEngineSourcePatcher.TryReplaceField(ref updated, designator, PriceHighPath, high.ToString())) return false;
            }
            else if (high != 0 && !HgEngineSourcePatcher.TryUpsertField(ref updated, designator, PriceHighPath, high.ToString())) return false;
            text = updated;
            return true;
        }

        private static readonly FieldPathSegment[] PricePath = { FieldPathSegment.Field("price") };
        private static readonly FieldPathSegment[] PriceHighPath = { FieldPathSegment.Field("price_high") };

        /// <summary>Reads an entry's price from the same spellings <see cref="TryReplacePrice"/> writes. False with a
        /// null <paramref name="raw"/> when the entry has no price; false with the text when it can't be read.</summary>
        public static bool TryGetPrice(HgEngineSourceBlock entry, Func<string, int?> lookup, out int price, out string raw) =>
            TryGetPrice(entry, lookup, out price, out raw, out _);

        /// <summary>A value the build would truncate is refused, with <paramref name="outOfRange"/> set.</summary>
        internal static bool TryGetPrice(HgEngineSourceBlock entry, Func<string, int?> lookup, out int price, out string raw, out bool outOfRange)
        {
            price = 0;
            raw = null;
            outOfRange = false;
            string text = entry.Raw;
            if (text.Length == 0 || text[0] != '{' || !BraceScanner.TryFindMatchingBrace(text, 0, out int close)) return false;

            switch (FindPriceMacro(text, 0, close, out int argStart, out int argEnd))
            {
                case null: raw = "ITEM_PRICE(...)"; return false;
                case true:
                    raw = text.Substring(argStart, argEnd - argStart).Trim();
                    if (!HgEngineSourceExpression.TryEvaluate(raw, lookup, out price)) return false;
                    break;
                default:
                    if (!entry.TryGetRaw(PricePath, out raw)) return false;
                    if (!HgEngineSourceExpression.TryEvaluate(raw, lookup, out int low)) return false;
                    if (low < 0 || low > 0xFFFF) { outOfRange = true; return false; }
                    int high = 0;
                    if (entry.TryGetRaw(PriceHighPath, out string rawHigh))
                    {
                        if (!HgEngineSourceExpression.TryEvaluate(rawHigh, lookup, out high)) { raw = rawHigh; return false; }
                        if (high < 0 || high > 0xF) { raw = rawHigh; outOfRange = true; return false; }
                    }
                    price = low | (high << 16);
                    break;
            }
            if (price >= 0 && price <= ItemData.MaxHgEnginePrice) return true;
            outOfRange = true;
            return false;
        }

        /// <summary>True with the argument span of an ITEM_PRICE(...) element, false when there is none, null when
        /// one is malformed.</summary>
        private static bool? FindPriceMacro(string text, int open, int close, out int argStart, out int argEnd)
        {
            argStart = argEnd = -1;
            var macro = new Regex(@"\GITEM_PRICE\s*\(");
            foreach (var (start, end) in ElementScanner.ElementSpans(text, open, close))
            {
                Match m = macro.Match(text, start);
                if (!m.Success) continue;
                argStart = m.Index + m.Length;
                argEnd = text.LastIndexOf(')', end - 1, end - argStart);
                return argEnd < argStart ? null : true;
            }
            return false;
        }

        /// <summary>A new entry shaped like the checkout's own ITEM_NONE, so it declares every field the
        /// editor writes. Falls back to a copy of the current hg-engine layout when there is none.</summary>
        internal static string BuildNewEntry(string sourceText, string designator)
        {
            string body = null;
            if (HgEngineSourcePatcher.TryFindEntry(sourceText, "ITEM_NONE", out int open, out int close))
            {
                body = sourceText.Substring(open, close - open + 1).Replace("\r\n", "\n");
                const string key = "[X] = ";
                string probe = key + body;
                if (HgEngineSourcePatcher.TryReplaceField(ref probe, "X", new[] { FieldPathSegment.Field("selectable") }, "TRUE"))
                    body = probe.Substring(key.Length);
            }
            body ??= DefaultEntryBody;
            return $"\n[{designator}] =\n{body},\n";
        }

        internal const string DefaultEntryBody =
            "{\n" +
            "    ITEM_PRICE(0),\n" +
            "    .holdEffect = 0,\n" +
            "    .holdEffectParam = 0,\n" +
            "    .pluckEffect = 0,\n" +
            "    .flingEffect = 0,\n" +
            "    .flingPower = 0,\n" +
            "    .naturalGiftPower = 0,\n" +
            "    .naturalGiftType = TYPE_NORMAL,\n" +
            "    .prevent_toss = FALSE,\n" +
            "    .selectable = TRUE,\n" +
            "    .fieldPocket = POCKET_ITEMS,\n" +
            "    .battlePocket = BATTLE_POCKET_NONE,\n" +
            "    .fieldUseFunc = 0,\n" +
            "    .battleUseFunc = 0,\n" +
            "    .partyUse = 0,\n" +
            "    .partyUseParam = {\n" +
            "        .slp_heal = FALSE,\n" +
            "        .psn_heal = FALSE,\n" +
            "        .brn_heal = FALSE,\n" +
            "        .frz_heal = FALSE,\n" +
            "        .prz_heal = FALSE,\n" +
            "        .cfs_heal = FALSE,\n" +
            "        .inf_heal = FALSE,\n" +
            "        .guard_spec = FALSE,\n" +
            "        .revive = FALSE,\n" +
            "        .revive_all = FALSE,\n" +
            "        .level_up = FALSE,\n" +
            "        .evolve = FALSE,\n" +
            "        .atk_stages = 0,\n" +
            "        .def_stages = 0,\n" +
            "        .spatk_stages = 0,\n" +
            "        .spdef_stages = 0,\n" +
            "        .speed_stages = 0,\n" +
            "        .accuracy_stages = 0,\n" +
            "        .critrate_stages = 0,\n" +
            "        .pp_up = FALSE,\n" +
            "        .pp_max = FALSE,\n" +
            "        .pp_restore = FALSE,\n" +
            "        .pp_restore_all = FALSE,\n" +
            "        .hp_restore = FALSE,\n" +
            "        .hp_ev_up = FALSE,\n" +
            "        .atk_ev_up = FALSE,\n" +
            "        .def_ev_up = FALSE,\n" +
            "        .speed_ev_up = FALSE,\n" +
            "        .spatk_ev_up = FALSE,\n" +
            "        .spdef_ev_up = FALSE,\n" +
            "        .friendship_mod_lo = FALSE,\n" +
            "        .friendship_mod_med = FALSE,\n" +
            "        .friendship_mod_hi = FALSE,\n" +
            "        .hp_ev_up_param = 0,\n" +
            "        .atk_ev_up_param = 0,\n" +
            "        .def_ev_up_param = 0,\n" +
            "        .speed_ev_up_param = 0,\n" +
            "        .spatk_ev_up_param = 0,\n" +
            "        .spdef_ev_up_param = 0,\n" +
            "        .hp_restore_param = 0,\n" +
            "        .pp_restore_param = 0,\n" +
            "        .friendship_mod_lo_param = 0,\n" +
            "        .friendship_mod_med_param = 0,\n" +
            "        .friendship_mod_hi_param = 0,\n" +
            "    },\n" +
            "}";

        /// <summary>Where a custom item's article, plural, give and description messages sit, or -1 when
        /// the id is not a custom one. item.h's ITEM_MSG_OFFSET names the last base item it counts from.</summary>
        internal static int CustomMessageOffset(string itemHeaderText, HgEngineSymbolTable items, int itemId)
        {
            Match m = Regex.Match(itemHeaderText ?? "",
                @"#define\s+ITEM_MSG_OFFSET\(id\)(?:[^\n]*\\\r?\n)*[^\n]*:\s*\(\(id\)\s*-\s*\((\w+)\s*\+\s*1\)\)\)");
            string lastBase = m.Success ? m.Groups[1].Value : "MAX_BASE_ITEM_NUM";
            if (items == null || !items.TryGetValue(lastBase, out int lastBaseId)) return -1;
            return itemId > lastBaseId ? itemId - (lastBaseId + 1) : -1;
        }

        /// <summary>Null lines with no error when the checkout doesn't build this archive.</summary>
        private static bool TryPlanLine(HgEngineOwnedFile file, string what, out List<string> lines, out string error)
        {
            lines = null;
            error = null;
            if (file == null) return true;
            if (file.Ownership != HgEngineOwnership.EditableSource)
            {
                error = $"hg-engine generates the {what} text during its build, so the new item can't be named there.";
                return false;
            }
            return HgEngineOwnedFiles.TryReadLines(file, out lines, out error);
        }

        internal static List<string> SetLine(List<string> lines, int index, string value, string pad)
        {
            var updated = new List<string>(lines);
            while (updated.Count <= index) updated.Add(pad);
            updated[index] = value;
            return updated;
        }

        /// <summary>Descriptions have no name in them; missing ones repeat the checkout's placeholder.</summary>
        internal static List<string> PadCustomLines(List<string> lines, int index)
        {
            var updated = new List<string>(lines);
            string pad = updated.Count > 0 ? updated[0] : "";
            while (updated.Count <= index) updated.Add(pad);
            return updated;
        }

        /// <summary>Article, plural and give-item lines are the checkout's placeholder with the name swapped
        /// in. The template is the first line, which names either the placeholder or the first custom item.</summary>
        internal static List<string> WithCustomNameLine(List<string> lines, int index, string displayName, string firstCustomName)
        {
            var updated = new List<string>(lines);
            string template = updated.Count > 0 ? updated[0] : "";
            string known = template.Contains(CustomPlaceholderName, StringComparison.Ordinal) ? CustomPlaceholderName
                : !string.IsNullOrEmpty(firstCustomName) && template.Contains(firstCustomName, StringComparison.Ordinal) ? firstCustomName
                : null;
            string Make(string name) => known != null ? template.Replace(known, name) : name;

            while (updated.Count < index) updated.Add(Make(CustomPlaceholderName));
            string line = Make(displayName);
            if (updated.Count == index) updated.Add(line);
            else if (updated[index].Contains(CustomPlaceholderName, StringComparison.Ordinal)) updated[index] = line;
            return updated;
        }
    }
}
