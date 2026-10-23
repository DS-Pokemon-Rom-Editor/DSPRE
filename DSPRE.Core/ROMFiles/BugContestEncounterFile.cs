using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Represents a Bug Contest encounter entry (BUGMON structure).
    /// File location: data/mushi/mushi_encount.bin (HGSS only)
    /// Structure: 8 bytes per entry
    /// </summary>
    public class BugContestEncounter
    {
        public ushort Species { get; set; }
        public byte MinLevel { get; set; }
        public byte MaxLevel { get; set; }
        public byte Rate { get; set; }
        public byte Score { get; set; }
        /// <summary>Padding, 0 in every retail record; kept as read.</summary>
        public ushort Dummy { get; set; }

        public BugContestEncounter()
        {
            Species = 0;
            MinLevel = 1;
            MaxLevel = 1;
            Rate = 0;
            Score = 0;
            Dummy = 0;
        }

        public BugContestEncounter(BinaryReader br)
        {
            Species = br.ReadUInt16();
            MinLevel = br.ReadByte();
            MaxLevel = br.ReadByte();
            Rate = br.ReadByte();
            Score = br.ReadByte();
            Dummy = br.ReadUInt16();
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(Species);
            bw.Write(MinLevel);
            bw.Write(MaxLevel);
            bw.Write(Rate);
            bw.Write(Score);
            bw.Write(Dummy);
        }

        public override string ToString()
        {
            string[] pokemonNames = RomInfo.GetPokemonNames();
            string name = Species < pokemonNames.Length ? pokemonNames[Species] : $"Pokémon {Species}";
            return $"{name} (Lv.{MinLevel}-{MaxLevel}, Rate:{Rate}, Score:{Score})";
        }
    }

    /// <summary>
    /// Represents a set of Bug Contest encounters.
    /// Each set contains ENCOUNTERS_PER_SET (10) encounters.
    /// </summary>
    public class BugContestEncounterSet
    {
        public string Name { get; }
        public string Description { get; }
        public BindingList<BugContestEncounter> Encounters { get; }

        public BugContestEncounterSet(string name, string description)
        {
            Name = name;
            Description = description;
            Encounters = new BindingList<BugContestEncounter>();
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// data/mushi/mushi_encount.bin (HGSS): four sets of 10. Set 0 before the National Dex, then day_of_week / 2, so
    /// Tuesday, Thursday and Saturday use sets 1 to 3.
    /// </summary>
    public class BugContestEncounterFile : RomFile
    {
        public const int EntrySize = 8;
        public const int EncountersPerSet = 10;
        public const int SetCount = 4;
        public const int TotalEntries = SetCount * EncountersPerSet; // 40

        public List<BugContestEncounterSet> Sets { get; private set; }

        /// <summary>Every encounter in one list, for callers that still want one.</summary>
        public BindingList<BugContestEncounter> Encounters
        {
            get
            {
                BindingList<BugContestEncounter> all = new BindingList<BugContestEncounter>();
                foreach (BugContestEncounterSet set in Sets)
                {
                    foreach (BugContestEncounter enc in set.Encounters)
                    {
                        all.Add(enc);
                    }
                }
                return all;
            }
        }


        public BugContestEncounterFile()
        {
            Sets = new List<BugContestEncounterSet> {
                new BugContestEncounterSet("Before National Dex", "Every contest before the National Dex."),
                new BugContestEncounterSet("Tuesdays after the National Dex", "Tuesday contests after the National Dex."),
                new BugContestEncounterSet("Thursdays after the National Dex", "Thursday contests after the National Dex."),
                new BugContestEncounterSet("Saturdays after the National Dex", "Saturday contests after the National Dex.")
            };
        }


        public BugContestEncounterFile(bool load) : this()
        {
            if (load)
            {
                string path = Filesystem.GetBugContestEncounterPath();
                if (File.Exists(path))
                {
                    ParseFile(path);
                }
            }
        }


        public BugContestEncounterFile(string path) : this()
        {
            long size = new FileInfo(path).Length;
            if (size != TotalEntries * EntrySize)
                throw new InvalidDataException($"That file is {size} bytes; a Bug Contest file is {TotalEntries * EntrySize}.");
            ParseFile(path);
        }

        private void ParseFile(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {

                foreach (BugContestEncounterSet set in Sets)
                {
                    set.Encounters.Clear();
                }

                int totalEntries = (int)(fs.Length / EntrySize);
                int entryIndex = 0;


                while (fs.Position + EntrySize <= fs.Length && entryIndex < TotalEntries)
                {
                    int setIndex = entryIndex / EncountersPerSet;
                    if (setIndex < Sets.Count)
                    {
                        Sets[setIndex].Encounters.Add(new BugContestEncounter(br));
                    }
                    entryIndex++;
                }

                // Every set gets exactly 10 entries, filled with empty ones if short.
                foreach (BugContestEncounterSet set in Sets)
                {
                    while (set.Encounters.Count < EncountersPerSet)
                    {
                        set.Encounters.Add(new BugContestEncounter());
                    }
                }
            }
        }

        /// <summary>What would crash or misread in game, or null.</summary>
        public string Problem()
        {
            foreach (BugContestEncounterSet set in Sets)
            {
                BindingList<BugContestEncounter> list = set.Encounters;
                for (int i = 0; i < list.Count; i++)
                {
                    BugContestEncounter e = list[i];
                    if (e.MaxLevel == 0) return $"{set.Name}, entry {i + 1}: the maximum level can't be 0; the score divides by it.";
                    if (e.MaxLevel < e.MinLevel) return $"{set.Name}, entry {i + 1}: the maximum level is below the minimum.";
                }
                if (list.Count > 0 && list[list.Count - 1].Rate != 0)
                    return $"{set.Name}: the last entry's rate must be 0, or the game reads past the end of the list.";
            }
            return null;
        }

        public override byte[] ToByteArray()
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {

                foreach (BugContestEncounterSet set in Sets)
                {
                    foreach (BugContestEncounter encounter in set.Encounters)
                    {
                        encounter.Write(bw);
                    }
                }
                return ms.ToArray();
            }
        }


        public bool SaveToFile(bool showSuccessMessage = true)
        {
            string path = Filesystem.GetBugContestEncounterPath();
            return SaveToFile(path, showSuccessMessage);
        }


        public new bool SaveToFile(string path, bool showSuccessMessage = true)
        {
            try
            {
                byte[] data = ToByteArray();
                ArchiveMembers.Write(path, data);

                if (showSuccessMessage)
                {
                    AppMessages.Notice("Bug Contest encounters saved.");
                }
                return true;
            }
            catch (Exception ex)
            {
                AppMessages.Error($"Couldn't save Bug Contest encounters: {ex.Message}", "Special encounters editor");
                return false;
            }
        }

        /// <summary>Whether the game has Bug Contest encounters (HGSS only).</summary>
        public static bool IsAvailable()
        {
            return RomInfo.GameFamily == RomInfo.GameFamilies.HGSS;
        }
    }
}
