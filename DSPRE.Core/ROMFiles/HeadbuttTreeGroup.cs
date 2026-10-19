using System.Collections.Generic;
using System.IO;

namespace DSPRE.ROMFiles
{
    public class HeadbuttTreeGroup
    {
        const int TreeCount = 6; //number of trees in each tree group
        public readonly List<HeadbuttTree> Trees = new List<HeadbuttTree>();

        public HeadbuttTreeGroup(BinaryReader br)
        {
            for (int i = 0; i < TreeCount; i++)
            {
                ushort x = br.ReadUInt16();
                ushort y = br.ReadUInt16();
                Trees.Add(new HeadbuttTree(x, y));
            }
        }

        public HeadbuttTreeGroup()
        {
            for (int i = 0; i < TreeCount; i++)
            {
                Trees.Add(new HeadbuttTree());
            }
        }

        public HeadbuttTreeGroup(HeadbuttTreeGroup original)
        {
            foreach (HeadbuttTree headbuttTree in original.Trees)
            {
                Trees.Add(new HeadbuttTree(headbuttTree));
            }
        }

        public override string ToString()
        {
            if (Trees.Count == 0)
            {
                return "Empty Tree Group";
            }
            return $"Tree Group at {Trees[0].GlobalX}, {Trees[0].GlobalY}";
        }
    }
}
