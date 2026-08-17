using System;
using System.IO;

namespace DSPRE.HgEngine
{
    /// <summary>Mints a new item: a #define in item.h, a minimal entry in itemdata.c, and the display
    /// name in the item-names text archive (the one piece of item data outside source). New ids append
    /// after the current max; MAX_BASE_ITEM_NUM stays untouched as the vanilla boundary marker.</summary>
    public static class HgEngineItemExpansion
    {
        private const string HeaderRelPath = "include/constants/item.h";
        private const string SourceRelPath = "data/itemdata/itemdata.c";
        private const string Prefix = "ITEM_";

        /// <summary>The last item id that shipped with hg-engine itself.</summary>
        public static bool TryGetVanillaBoundary(out int lastVanillaItemId)
        {
            lastVanillaItemId = -1;
            var items = HgEngineSymbolTable.Load(HeaderRelPath);
            return items != null && items.TryGetValue("MAX_BASE_ITEM_NUM", out lastVanillaItemId);
        }

        public static bool TryAddItem(string displayName, out int newItemId, out string error)
        {
            newItemId = -1;
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }

            var items = HgEngineSymbolTable.Load(HeaderRelPath);
            if (items == null) { error = $"Could not load {HeaderRelPath}."; return false; }

            // Scan for the real max rather than using MAX_BASE_ITEM_NUM: that only tracks the vanilla boundary.
            int maxId = -1;
            foreach (var kv in items.ByName)
                if (kv.Key.StartsWith(Prefix, StringComparison.Ordinal) && kv.Value > maxId) maxId = kv.Value;
            if (maxId < 0) { error = "Could not find any existing ITEM_* constants."; return false; }
            int candidateId = maxId + 1;

            string slug = HgEngineNameSlug.ToUniqueSlug(displayName, items, Prefix);
            string designator = Prefix + slug;

            string headerPath = Path.Combine(HgEngineProject.RepoPathUnc, HeaderRelPath.Replace('/', '\\'));
            if (!File.Exists(headerPath)) { error = $"Source file not found: {headerPath}"; return false; }
            string headerText = File.ReadAllText(headerPath);

            if (!HgEngineHeaderEditor.TryInsertBeforeDefine(ref headerText, "MAX_TOTAL_ITEM_NUM", $"#define {designator} {candidateId}\n\n"))
            { error = "Could not find MAX_TOTAL_ITEM_NUM in item.h to anchor the new item next to."; return false; }
            if (!HgEngineHeaderEditor.TryReplaceDefineValue(ref headerText, "MAX_TOTAL_ITEM_NUM", designator))
            { error = "Could not update MAX_TOTAL_ITEM_NUM."; return false; }

            string sourcePath = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            if (!File.Exists(sourcePath)) { error = $"Source file not found: {sourcePath}"; return false; }
            string sourceText = File.ReadAllText(sourcePath);

            string newEntry =
                $"\n[{designator}] =\n{{\n" +
                "    ITEM_PRICE(0),\n" +
                "    .selectable = TRUE,\n" +
                "    .fieldPocket = POCKET_ITEMS,\n" +
                "},\n";
            if (!HgEngineHeaderEditor.TryInsertBeforeFinalCloseBrace(ref sourceText, newEntry))
            { error = $"Could not find the end of {SourceRelPath} to insert the new item."; return false; }

            File.WriteAllText(headerPath, headerText);
            File.WriteAllText(sourcePath, sourceText);
            HgEngineSymbolTable.ClearCache();

            var names = new ROMFiles.TextArchive(RomInfo.itemNamesTextNumber);
            while (names.messages.Count <= candidateId) names.messages.Add("");
            names.messages[candidateId] = displayName;
            names.SaveToExpandedDir(RomInfo.itemNamesTextNumber, showSuccessMessage: false);

            newItemId = candidateId;
            return true;
        }
    }
}
