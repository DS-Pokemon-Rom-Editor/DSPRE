using System.Collections.Generic;
using System.ComponentModel;

namespace DSPRE.ROMFiles
{
    /// <summary>Trophy Garden's daily Pokémon pool (DPPt): encdata_ex file 8, 16 slots.</summary>
    public sealed class TrophyGardenEncounterFile : SpeciesSlotArchive
    {
        public const int SlotCount = 16;
        public const int FileIndex = 8;

        public BindingList<SpeciesSlot> Encounters => Groups[0].Encounters;

        public TrophyGardenEncounterFile(byte[] data) : base(SlotCount, new[] { new SpeciesSlotGroup("Pool", "", FileIndex) }, data) { }

        public static TrophyGardenEncounterFile Load() => new TrophyGardenEncounterFile(ReadMembers(SlotCount, FileIndex));

        /// <summary>What would hang in game, or null.</summary>
        public string Problem()
        {
            // The daily pick rerolls until it differs from both active picks, comparing whole 4-byte entries.
            HashSet<uint> distinct = new HashSet<uint>();
            foreach (SpeciesSlot e in Encounters) distinct.Add(e.Species | ((uint)e.Padding << 16));
            return distinct.Count < 3
                ? "The pool needs at least 3 different species, or the game freezes picking the daily Pokémon."
                : null;
        }
    }
}
