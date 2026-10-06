using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// hg-engine's starters, src/starters.c's sStarterChoices[3]. Its hooks replace the game's own species and
    /// cries at run time, so the ROM's copies no longer matter. A choice may carry a form with MON_WITH_FORM.
    /// </summary>
    public static class HgEngineStarters
    {
        public const string SourceRelPath = "src/starters.c";
        private const string SpeciesHeaderRelPath = "include/constants/species.h";
        private const string Table = "sStarterChoices";

        private static string FilePath => Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));

        public static bool Available => HgEngineProject.IsActive && File.Exists(FilePath);

        private static string Parse(string text, out List<CInitItem> entries, out int[] species, out int[] forms)
        {
            entries = null; species = forms = null;
            CDeclaration decl = CSourceFile.For(text).Find(Table);
            if (decl == null) return $"{SourceRelPath} has no {Table}.";
            if (decl.Init.Items.Any(i => i.IsConditional)) return $"{Table} has a starter under #if, which DSPRE doesn't edit.";
            if (decl.Init.Items.Count != 3) return $"{Table} lists {decl.Init.Items.Count} starters, not 3.";
            entries = decl.Init.Items;
            species = new int[3];
            forms = new int[3];
            for (int i = 0; i < 3; i++)
                if (!HgEngineTrainerSource.TryParseSpecies(entries[i].ValueText(text).Trim(), SpeciesHeaderRelPath, out species[i], out forms[i]))
                    return $"{SourceRelPath}: starter {i + 1} \"{entries[i].ValueText(text).Trim()}\" couldn't be read.";
            return null;
        }

        private static bool TryLoad(out string text, out List<CInitItem> entries, out int[] species, out int[] forms, out string error)
        {
            text = null; entries = null; species = forms = null;
            if (!Available) { error = $"{SourceRelPath} is missing from the checkout."; return false; }
            text = HgEngineFileCache.GetText(FilePath).Replace("\r\n", "\n");
            error = Parse(text, out entries, out species, out forms);
            return error == null;
        }

        public static bool TryRead(out int[] species, out int[] forms, out string error) => TryLoad(out _, out _, out species, out forms, out error);

        /// <summary>Writes the three choices. A choice whose species is unchanged keeps its form and spelling.</summary>
        public static bool TryWrite(int[] species, out string error)
        {
            if (!TryLoad(out string text, out List<CInitItem> entries, out int[] oldSpecies, out int[] oldForms, out error)) return false;
            HgEngineSymbolTable names = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            string updated = text;
            for (int i = 2; i >= 0; i--)
            {
                if (species[i] == oldSpecies[i]) continue;
                if (names == null || !names.TryGetNameWithPrefix(species[i], "SPECIES_", out string name)) { error = $"No SPECIES_ name for species {species[i]}."; return false; }
                updated = updated.Substring(0, entries[i].ValueStart) + name + updated.Substring(entries[i].ValueEnd);
            }
            if (updated == text) return true;
            try
            {
                return HgEngineVerifiedWrite.TryWrite(FilePath, SourceRelPath, updated, written =>
                    Parse(written, out _, out int[] back, out _) ?? (back.SequenceEqual(species) ? null : "the starters differ"), out error);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { error = ex.Message; return false; }
        }
    }
}
