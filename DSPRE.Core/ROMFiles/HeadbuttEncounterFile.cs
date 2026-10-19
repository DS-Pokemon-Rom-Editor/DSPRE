using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    //https://hirotdk.neocities.org/FileSpecs.html#Headbutt
    public class HeadbuttEncounterFile
    {
        public ushort ID;

        //get encounter tables, 12 normal pokemon definitions, 6 special pokemon definitions, 4 bytes per definition
        const int NormalEncountersCount = 12;
        const int SpecialEncountersCount = 6;

        public ushort NormalTreeGroupsCount;
        public ushort SpecialTreeGroupsCount;
        public List<HeadbuttEncounter> NormalEncounters;
        public List<HeadbuttEncounter> SpecialEncounters;
        public BindingList<HeadbuttTreeGroup> NormalTreeGroups;
        public BindingList<HeadbuttTreeGroup> SpecialTreeGroups;

        /// <summary>Builds an empty, in-memory file with no backing path (e.g. for a source-backed
        /// hg-engine read that populates the lists itself instead of parsing a NARC file).</summary>
        public HeadbuttEncounterFile()
        {
            NormalEncounters = new List<HeadbuttEncounter>();
            SpecialEncounters = new List<HeadbuttEncounter>();
            NormalTreeGroups = new BindingList<HeadbuttTreeGroup>();
            SpecialTreeGroups = new BindingList<HeadbuttTreeGroup>();
        }

        public HeadbuttEncounterFile(ushort id)
        {
            this.ID = id;
            string path = Filesystem.GetHeadbuttPath(id);
            ParseFile(path);
        }

        public HeadbuttEncounterFile(string path)
        {
            ParseFile(path);
        }

        /// <summary>A file read from bytes already in memory, laid out as on disk.</summary>
        public HeadbuttEncounterFile(ushort id, byte[] data)
        {
            this.ID = id;
            Parse(new MemoryStream(data));
        }

        public void ParseFile(string path) => Parse(new FileStream(path, FileMode.Open));

        private void Parse(Stream fs)
        {
            using (BinaryReader br = new BinaryReader(fs))
            {
                //get the number of tree group definitions
                NormalTreeGroupsCount = br.ReadUInt16();
                SpecialTreeGroupsCount = br.ReadUInt16();

                NormalEncounters = new List<HeadbuttEncounter>();
                SpecialEncounters = new List<HeadbuttEncounter>();

                NormalTreeGroups = new BindingList<HeadbuttTreeGroup>();
                SpecialTreeGroups = new BindingList<HeadbuttTreeGroup>();

                // A tree-less map is only the counts unless its slots were filled in, which the writer keeps.
                bool hasTrees = NormalTreeGroupsCount > 0 || SpecialTreeGroupsCount > 0;
                bool hasSlots = hasTrees || br.BaseStream.Length >= 4 + (NormalEncountersCount + SpecialEncountersCount) * 4;
                if (!hasSlots)
                {
                    for (int i = 0; i < NormalEncountersCount; i++)
                    {
                        NormalEncounters.Add(new HeadbuttEncounter());
                    }

                    for (int i = 0; i < SpecialEncountersCount; i++)
                    {
                        SpecialEncounters.Add(new HeadbuttEncounter());
                    }

                    return;
                }

                for (int i = 0; i < NormalEncountersCount; i++)
                {
                    NormalEncounters.Add(new HeadbuttEncounter(br));
                }

                for (int i = 0; i < SpecialEncountersCount; i++)
                {
                    SpecialEncounters.Add(new HeadbuttEncounter(br));
                }

                //tree groups have 6 sets of xy global coordinates x treeGroupsCount
                //coordinates need to be converted to matrix and local coordinates to be useful
                for (int i = 0; i < NormalTreeGroupsCount; i++)
                {
                    NormalTreeGroups.Add(new HeadbuttTreeGroup(br));
                }

                for (int i = 0; i < SpecialTreeGroupsCount; i++)
                {
                    SpecialTreeGroups.Add(new HeadbuttTreeGroup(br));
                }
            }
        }

        public byte[] ToByteArray()
        {
            MemoryStream newData = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(newData))
            {
                writer.Write((ushort)NormalTreeGroups.Count);
                writer.Write((ushort)SpecialTreeGroups.Count);

                // A map with no trees is only the counts; slots are kept once any are filled in.
                bool empty = NormalTreeGroups.Count == 0 && SpecialTreeGroups.Count == 0
                    && NormalEncounters.Concat(SpecialEncounters).All(e => e.PokemonID == 0 && e.MinLevel == 0 && e.MaxLevel == 0);
                if (empty) { writer.Flush(); return newData.ToArray(); }

                foreach (HeadbuttEncounter encounter in NormalEncounters)
                {
                    writer.Write((ushort)encounter.PokemonID);
                    writer.Write((byte)encounter.MinLevel);
                    writer.Write((byte)encounter.MaxLevel);
                }

                foreach (HeadbuttEncounter encounter in SpecialEncounters)
                {
                    writer.Write((UInt16)encounter.PokemonID);
                    writer.Write((byte)encounter.MinLevel);
                    writer.Write((byte)encounter.MaxLevel);
                }

                foreach (HeadbuttTreeGroup treeGroup in NormalTreeGroups)
                {
                    foreach (HeadbuttTree tree in treeGroup.Trees)
                    {
                        writer.Write((UInt16)tree.GlobalX);
                        writer.Write((UInt16)tree.GlobalY);
                    }
                }

                foreach (HeadbuttTreeGroup treeGroup in SpecialTreeGroups)
                {
                    foreach (HeadbuttTree tree in treeGroup.Trees)
                    {
                        writer.Write((UInt16)tree.GlobalX);
                        writer.Write((UInt16)tree.GlobalY);
                    }
                }
            }

            return newData.ToArray();
        }

        public bool SaveToFile() => SaveToFile(ID);

        public bool SaveToFile(int id)
        {
            ArchiveMembers.Put(RomInfo.DirNames.Headbutt, id, ToByteArray());
            return true;
        }


        public bool SaveToFile(string path, bool showSuccessMessage = true)
        {
            byte[] romFileToByteArray = ToByteArray();
            ArchiveMembers.Write(path, romFileToByteArray);
            return true;
        }
    }
}
