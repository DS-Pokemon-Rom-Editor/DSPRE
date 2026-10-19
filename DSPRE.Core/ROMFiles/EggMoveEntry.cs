using System.Collections.Generic;

namespace DSPRE
{
    public struct EggMoveEntry
    {
        public int SpeciesID;
        public List<ushort> MoveIDs;

        public EggMoveEntry(int speciesID, List<ushort> moveIDs)
        {
            this.SpeciesID = speciesID;
            this.MoveIDs = moveIDs;
        }

        public int GetSizeInBytes()
        {
            // speciesID + moveIDs (2 bytes each)
            return 2 + (2 * MoveIDs.Count);
        }
    }
}
