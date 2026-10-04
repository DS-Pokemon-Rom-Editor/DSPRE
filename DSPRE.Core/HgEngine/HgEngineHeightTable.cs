using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>Source-text read/write for data/HeightTable.c's <c>__data[]</c> (per-species front/back
    /// sprite height offsets, separate male/female, used by battle-scene sprite Y placement). A flat
    /// `[SPECIES_X] = { femaleBack, maleBack, femaleFront, maleFront },` positional 4-tuple, matched by the species
    /// id its name stands for.</summary>
    public static class HgEngineHeightTable
    {
        private const string SourceRelPath = "data/HeightTable.c";
        private const string SpeciesHeaderRelPath = "include/constants/species.h";

        private static CInitItem Entry(string text, int speciesId, out CDeclaration table)
        {
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            table = CSourceFile.For(text).Declarations.OrderByDescending(d => d.Init.Items.Count(i => i.IndexText != null)).FirstOrDefault();
            return table?.Init.Items.FirstOrDefault(i => i.IndexText != null && i.List != null
                && HgEngineSourceExpression.TryEvaluate(i.IndexText, n => species != null && species.TryGetValue(n, out int v) ? v : null, out int id) && id == speciesId);
        }

        private static int[] Values(string text, CInitItem entry)
        {
            if (entry?.List == null || entry.List.Items.Count < 4) return null;
            var v = new int[4];
            for (int k = 0; k < 4; k++)
                if (!HgEngineSourceExpression.TryEvaluate(entry.List.Items[k].ValueText(text), _ => null, out v[k])) return null;
            return v;
        }

        public static bool TryGet(int speciesId, out int femaleBack, out int maleBack, out int femaleFront, out int maleFront)
        {
            femaleBack = maleBack = femaleFront = maleFront = 0;
            if (!HgEngineProject.IsActive) return false;
            string text = TryReadSource(out _);
            if (text == null) return false;
            var v = Values(text, Entry(text, speciesId, out _));
            if (v == null) return false;
            (femaleBack, maleBack, femaleFront, maleFront) = (v[0], v[1], v[2], v[3]);
            return true;
        }

        public static bool TrySet(int speciesId, int femaleBack, int maleBack, int femaleFront, int maleFront, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Could not resolve a species designator for id {speciesId}."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }

            string valueLiteral = $"{{ {femaleBack}, {maleBack}, {femaleFront}, {maleFront} }}";
            var entry = Entry(text, speciesId, out var table);
            if (table == null) { error = $"{SourceRelPath} has no height table."; return false; }
            if (entry != null) text = text.Substring(0, entry.List.Open) + valueLiteral + text.Substring(entry.List.Close + 1);
            else text = text.Insert(HgEngineSwarms.LineStart(text, table.Init.Close), $"    [{designator}] = {valueLiteral},\n");

            int[] want = { femaleBack, maleBack, femaleFront, maleFront };
            return HgEngineVerifiedWrite.TryWrite(path, SourceRelPath, text, written =>
                Values(written, Entry(written, speciesId, out _))?.SequenceEqual(want) == true ? null : "the heights differ", out error);
        }

        private static string TryReadSource(out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? HgEngineFileCache.GetText(path).Replace("\r\n", "\n") : null;
        }
    }
}
