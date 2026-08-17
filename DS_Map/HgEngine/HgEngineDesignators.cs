using System.Collections.Generic;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// Resolves the numeric ID DSPRE tracks (species/item/move index, encounter-table index) to the
    /// exact designator token the write-path's <see cref="HgEngineSourcePatcher"/> needs to find
    /// `[TOKEN] = { ... }` in a data/*.c file. Trainers.c uses plain numeric designators directly; every
    /// other domain uses a symbolic constant, resolved via that domain's own header.
    /// </summary>
    public static class HgEngineDesignators
    {
        private static readonly Dictionary<HgEngineDomain, string> HeaderByDomain = new()
        {
            [HgEngineDomain.Species]       = "include/constants/species.h",
            [HgEngineDomain.Items]         = "include/constants/item.h",
            [HgEngineDomain.Moves]         = "include/constants/moves.h",
            [HgEngineDomain.Encounters]    = "include/constants/encounter_tables.h",
            [HgEngineDomain.SpriteOffsets] = "include/constants/species.h",
        };

        // item.h packs ITEM_*/POCKET_*/BATTLE_POCKET_* into one flat namespace, so resolve by prefix everywhere.
        private static readonly Dictionary<HgEngineDomain, string> DesignatorPrefixByDomain = new()
        {
            [HgEngineDomain.Species]       = "SPECIES_",
            [HgEngineDomain.Items]         = "ITEM_",
            [HgEngineDomain.Moves]         = "MOVE_",
            [HgEngineDomain.Encounters]    = "ENCDATA_",
            [HgEngineDomain.SpriteOffsets] = "SPECIES_",
        };

        public static bool TryResolve(HgEngineDomain domain, int id, out string designator)
        {
            designator = null;
            if (domain == HgEngineDomain.Trainers)
            {
                designator = id.ToString();
                return true;
            }

            if (!HeaderByDomain.TryGetValue(domain, out string header)) return false;
            var table = HgEngineSymbolTable.Load(header);
            if (table == null) return false;
            return table.TryGetNameWithPrefix(id, DesignatorPrefixByDomain[domain], out designator);
        }
    }
}
