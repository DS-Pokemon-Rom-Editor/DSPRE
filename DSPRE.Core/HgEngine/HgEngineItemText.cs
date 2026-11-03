using System.Collections.Generic;
using System.IO;
using System.Linq;

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
            CDirective macro = header == null ? null : CSourceFile.For(header).FindDefine("ITEM_GENERATION");
            if (macro == null || items == null || files == null) return false;

            // ((id) <= LAST_A ? GEN_A : (id) <= LAST_B ? GEN_B : GEN_C)
            List<string> body = CLexer.Tokenize(macro.Body).Select(t => t.Text(macro.Body)).ToList();
            List<(int Last, string Gen)> steps = new List<(int Last, string Gen)>();
            string gen = null;
            for (int t = 0; t + 2 < body.Count; t++)
            {
                if (body[t] == "<=" && body[t + 2] == "?" && t + 3 < body.Count)
                {
                    if (!items.TryGetValue(body[t + 1], out int last)) return false;
                    steps.Add((last, body[t + 3]));
                }
                else if (body[t] == ":" && body[t + 1] != "(") gen = body[t + 1];
            }
            if (steps.Count == 0 || gen == null) return false;

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
