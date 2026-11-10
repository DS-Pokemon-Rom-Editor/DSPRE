using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace DSPRE.ROMFiles
{
    /// <summary>A species (u16) and two bytes kept as read.</summary>
    public class SpeciesSlot
    {
        public ushort Species { get; set; }
        public ushort Padding { get; set; }

        public override string ToString()
        {
            string[] pokemonNames = RomInfo.GetPokemonNames();
            return Species < pokemonNames.Length ? pokemonNames[Species] : $"Pokémon {Species}";
        }
    }

    /// <summary>One encdata_ex member of species slots.</summary>
    public class SpeciesSlotGroup
    {
        public string Name { get; }
        public string Description { get; }
        public int Member { get; }
        public BindingList<SpeciesSlot> Encounters { get; } = new BindingList<SpeciesSlot>();

        public SpeciesSlotGroup(string name, string description, int member)
        {
            Name = name;
            Description = description;
            Member = member;
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Species-only slot lists in encdata_ex (DP and Pt), one member per group. Its bytes, every group in order, are
    /// also the export file.
    /// </summary>
    public abstract class SpeciesSlotArchive
    {
        public const int EntrySize = 4;

        public int SlotsPerGroup { get; }
        public List<SpeciesSlotGroup> Groups { get; }

        private int GroupSize => SlotsPerGroup * EntrySize;

        protected SpeciesSlotArchive(int slotsPerGroup, SpeciesSlotGroup[] groups, byte[] data)
        {
            SlotsPerGroup = slotsPerGroup;
            Groups = new List<SpeciesSlotGroup>(groups);
            foreach (SpeciesSlotGroup group in Groups)
                for (int slot = 0; slot < SlotsPerGroup; slot++) group.Encounters.Add(new SpeciesSlot());
            CopyFrom(data);
        }

        public static bool IsAvailable() =>
            RomInfo.GameFamily == RomInfo.GameFamilies.DP || RomInfo.GameFamily == RomInfo.GameFamilies.Plat;

        /// <summary>Sets every slot from <paramref name="data"/>; slots past its end are emptied.</summary>
        public void CopyFrom(byte[] data)
        {
            for (int g = 0; g < Groups.Count; g++)
                for (int slot = 0; slot < SlotsPerGroup; slot++)
                {
                    int at = g * GroupSize + slot * EntrySize;
                    bool present = at + EntrySize <= data.Length;
                    SpeciesSlot entry = Groups[g].Encounters[slot];
                    entry.Species = present ? BitConverter.ToUInt16(data, at) : (ushort)0;
                    entry.Padding = present ? BitConverter.ToUInt16(data, at + 2) : (ushort)0;
                }
        }

        public byte[] ToBytes()
        {
            byte[] data = new byte[Groups.Count * GroupSize];
            for (int g = 0; g < Groups.Count; g++)
                for (int slot = 0; slot < SlotsPerGroup; slot++)
                {
                    int at = g * GroupSize + slot * EntrySize;
                    SpeciesSlot entry = Groups[g].Encounters[slot];
                    BitConverter.GetBytes(entry.Species).CopyTo(data, at);
                    BitConverter.GetBytes(entry.Padding).CopyTo(data, at + 2);
                }
            return data;
        }

        /// <summary>The members in order as one block. Whole entries only; a missing member reads as empty slots.</summary>
        protected static byte[] ReadMembers(int slotsPerGroup, params int[] members)
        {
            int groupSize = slotsPerGroup * EntrySize;
            byte[] data = new byte[members.Length * groupSize];
            for (int g = 0; g < members.Length; g++)
            {
                string path = ArchiveMembers.PathOf(RomInfo.DirNames.EncounterExtended, members[g]);
                if (!File.Exists(path)) continue;
                byte[] member = File.ReadAllBytes(path);
                int whole = Math.Min(member.Length / EntrySize * EntrySize, groupSize);
                Array.Copy(member, 0, data, g * groupSize, whole);
            }
            return data;
        }

        /// <summary>Writes every group or none; a failure throws.</summary>
        public void Save()
        {
            string narcDir = Filesystem.EncounterExtended;
            if (string.IsNullOrEmpty(narcDir) || !Directory.Exists(narcDir))
                throw new DirectoryNotFoundException("arc/encdata_ex.narc isn't unpacked.");

            byte[] data = ToBytes();
            ArchiveMembers.Batch batch = new ArchiveMembers.Batch();
            for (int g = 0; g < Groups.Count; g++)
                batch.Put(RomInfo.DirNames.EncounterExtended, Groups[g].Member, data.AsSpan(g * GroupSize, GroupSize).ToArray());
            if (!batch.TryCommit(out string error)) throw new IOException(error);
        }
    }
}
