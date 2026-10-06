using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// Where hg-engine keeps an item's messages. They are split by generation: item.h's ITEM_GENERATION picks the
    /// archive from file.h's MSG_DATA_ITEM_&lt;kind&gt;_GEN4..GEN9 or _CUSTOM, and ITEM_MSG_OFFSET the line in it, both
    /// counting from the last item of the generation before.
    /// </summary>
    public static class HgEngineItemText
    {
        private const string ItemHeaderRelPath = "include/constants/item.h";
        private const string FileHeaderRelPath = "include/constants/file.h";

        private static readonly Regex Generation = new(@"#define\s+ITEM_GENERATION\(id\)((?:[^\n]*\\\r?\n)*[^\n]*)");
        private static readonly Regex Step = new(@"\(id\)\s*<=\s*(\w+)\s*\?\s*(\w+)");
        private static readonly Regex Last = new(@":\s*(\w+)\s*\)\s*$");

        /// <summary>
        /// The text archive and line of item <paramref name="itemId"/>'s <paramref name="kind"/> message, kind being
        /// what file.h names after MSG_DATA_ITEM_ (DESCRIPTION, NAME_ARTICLE, NAME_PLURAL, GIVE_ITEM).
        /// </summary>
        public static bool TryLocate(int itemId, string kind, out int archive, out int line)
        {
            archive = line = -1;
            if (!HgEngineProject.IsActive || itemId < 0) return false;
            string header = Read(ItemHeaderRelPath);
            HgEngineSymbolTable items = HgEngineSymbolTable.Load(ItemHeaderRelPath);
            HgEngineSymbolTable files = HgEngineSymbolTable.Load(FileHeaderRelPath);
            Match macro = header == null ? Match.Empty : Generation.Match(header);
            if (!macro.Success || items == null || files == null) return false;

            string body = macro.Groups[1].Value.Replace("\\\r\n", " ").Replace("\\\n", " ");
            List<(int Last, string Gen)> steps = new List<(int Last, string Gen)>();
            foreach (Match m in Step.Matches(body))
            {
                if (!items.TryGetValue(m.Groups[1].Value, out int last)) return false;
                steps.Add((last, m.Groups[2].Value));
            }
            Match rest = Last.Match(body.Trim());
            if (steps.Count == 0 || !rest.Success) return false;

            string gen = rest.Groups[1].Value;
            int start = steps[^1].Last + 1;
            for (int i = 0; i < steps.Count; i++)
            {
                if (itemId > steps[i].Last) continue;
                gen = steps[i].Gen;
                start = i == 0 ? 0 : steps[i - 1].Last + 1;
                break;
            }
            if (!files.TryGetValue($"MSG_DATA_ITEM_{kind}_{gen}", out archive)) return false;
            line = itemId - start;
            return true;
        }

        private static string Read(string rel)
        {
            string path = Path.Combine(HgEngineProject.RepoPathUnc, rel.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
