namespace DSPRE.HgEngine
{
    /// <summary>Marks entries added past hg-engine's original vanilla content with a "[Custom] " prefix
    /// in name lists/dropdowns.</summary>
    public static class HgEngineCustomLabel
    {
        private const string Prefix = "[Custom] ";

        // Items have no reserved built-in range past the vanilla boundary, so an open-ended range is correct.
        public static void ApplyItemLabel(string[] names)
        {
            if (!HgEngineProject.IsActive || names == null) return;
            if (!HgEngineItemExpansion.TryGetVanillaBoundary(out int lastVanillaId)) return;
            ApplyRange(names, lastVanillaId + 1, names.Length - lastVanillaId - 1);
        }

        // Unlike items, moves/species have hg-engine's own built-in content (Gigantamax moves, form
        // species) past the custom block, so this uses a bounded range instead of open-ended.
        public static void ApplyMoveLabel(string[] names)
        {
            if (!HgEngineProject.IsActive || names == null) return;
            if (!HgEngineMoveExpansion.TryGetCustomRange(out int first, out int count)) return;
            ApplyRange(names, first, count);
        }

        public static void ApplySpeciesLabel(string[] names)
        {
            if (!HgEngineProject.IsActive || names == null) return;
            if (!HgEngineSpeciesExpansion.TryGetCustomRange(out int first, out int count)) return;
            ApplyRange(names, first, count);
        }

        private static void ApplyRange(string[] names, int firstIndex, int count)
        {
            int end = firstIndex + count;
            for (int i = firstIndex; i >= 0 && i < names.Length && i < end; i++)
            {
                if (string.IsNullOrEmpty(names[i]) || names[i].StartsWith(Prefix)) continue;
                names[i] = Prefix + names[i];
            }
        }
    }
}
