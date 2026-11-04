using System;
using System.Collections.Generic;
using System.IO;

namespace DSPRE.HgEngine
{
    /// <summary>Source-text read/write for data/IconPaletteTable.c's <c>gIconPalTable[]</c> (which of the
    /// 3 party-icon palettes each species uses). A flat `[SPECIES_X] = N,` array, not a `{ ... }` block,
    /// so this reads/writes directly instead of through HgEngineSourcePatcher. Some rows (Unown and the
    /// other vanilla forms) are designated by number, e.g. `[499] = 0,`.</summary>
    public static class HgEngineIconPalette
    {
        /// <summary>The file this reads, for anything that needs to name it on screen.</summary>
        public const string SourceFile = "data/IconPaletteTable.c";

        private const string SourceRelPath = SourceFile;
        private const string TableName = "gIconPalTable";

        public static bool TryGetPaletteId(int speciesId, out int paletteId)
        {
            paletteId = 0;
            if (!HgEngineProject.IsActive) return false;
            string text = TryReadSource(out _);
            if (text == null) return false;

            CInitItem row = FindRow(text, speciesId, DesignatorFor(speciesId));
            return row != null && CLexer.TryParseInt(row.ValueText(text), out paletteId);
        }

        public static bool TrySetPaletteId(int speciesId, int paletteId, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"{path} is missing from the checkout."; return false; }

            if (!TryReplaceValue(ref text, speciesId, DesignatorFor(speciesId), paletteId))
            { error = $"IconPaletteTable.c has no entry for species {speciesId}."; return false; }
            HgEngineFileCache.WriteText(path, text);
            return true;
        }

        /// <summary>The row named <paramref name="designator"/>, or failing that the row designated by the number
        /// <paramref name="speciesId"/>.</summary>
        private static CInitItem FindRow(string text, int speciesId, string designator)
        {
            CDeclaration table = CSourceFile.For(text).Find(TableName);
            if (table == null) return null;
            CInitItem numeric = null;
            foreach (CInitItem row in table.Init.Items)
            {
                string d = row.IndexText?.Trim();
                if (designator != null && d == designator) return row;
                if (numeric == null && CLexer.TryParseInt(d, out int n) && n == speciesId) numeric = row;
            }
            return numeric;
        }

        private static bool TryReplaceValue(ref string text, int speciesId, string designator, int paletteId)
        {
            CInitItem row = FindRow(text, speciesId, designator);
            if (row == null) return false;
            text = string.Concat(text.AsSpan(0, row.ValueStart), paletteId.ToString(), text.AsSpan(row.ValueEnd));
            return true;
        }

        private static string DesignatorFor(int speciesId)
        {
            HgEngineSymbolTable species = HgEngineSymbolTable.Load("include/constants/species.h");
            return species != null && species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator) ? designator : null;
        }

        private static string TryReadSource(out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
