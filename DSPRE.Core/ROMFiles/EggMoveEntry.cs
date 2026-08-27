using System.Collections.Generic;

namespace DSPRE
{
    public struct EggMoveEntry
    {
        public int speciesID;
        public List<ushort> moveIDs;

        public EggMoveEntry(int speciesID, List<ushort> moveIDs)
        {
            this.speciesID = speciesID;
            this.moveIDs = moveIDs;
        }

        public int GetSizeInBytes()
        {
            // speciesID + moveIDs (2 bytes each)
            return 2 + (2 * moveIDs.Count);
        }
    }
}
