using System.Collections.Generic;
using System.IO;

namespace DSPRE.ROMFiles
{
  public class SafariZoneEncounterFile : RomFile
  {
    public static Dictionary<int, string> Names = new Dictionary<int, string>() {
      {0, "Plains"},
      {1, "Meadow"},
      {2, "Savannah"},
      {3, "Peak"},
      {4, "Rocky Beach"},
      {5, "Wetland"},
      {6, "Forest"},
      {7, "Swamp"},
      {8, "Marshland"},
      {9, "Wasteland"},
      {10, "Mountain"},
      {11, "Desert"},
    };

    public int ID;
    
    public SafariZoneEncounterGroup GrassEncounterGroup = new SafariZoneEncounterGroup();
    public SafariZoneEncounterGroup SurfEncounterGroup = new SafariZoneEncounterGroup();
    public SafariZoneEncounterGroup OldRodEncounterGroup = new SafariZoneEncounterGroup();
    public SafariZoneEncounterGroup GoodRodEncounterGroup = new SafariZoneEncounterGroup();
    public SafariZoneEncounterGroup SuperRodEncounterGroup = new SafariZoneEncounterGroup();

    public SafariZoneEncounterFile(int id)
    {
      this.ID = id;
      string path = Filesystem.GetSafariZonePath(id);
      ParseFile(path);
    }

    public SafariZoneEncounterFile(string path)
    {
      ParseFile(path);
    }

    /// <summary>A file read from bytes already in memory, laid out as on disk.</summary>
    public SafariZoneEncounterFile(int id, byte[] data)
    {
      this.ID = id;
      Parse(new MemoryStream(data));
    }

    public void ParseFile(string path) => Parse(new FileStream(path, FileMode.Open, FileAccess.Read));

    private void Parse(Stream fs)
    {
      using (BinaryReader br = new BinaryReader(fs))
      {
        if (br.BaseStream.Length < 5){ return; }
        //#1 Section - Object Arrangement Allocation
        GrassEncounterGroup.ReadObjectSlots(br);
        SurfEncounterGroup.ReadObjectSlots(br);
        OldRodEncounterGroup.ReadObjectSlots(br);
        GoodRodEncounterGroup.ReadObjectSlots(br);
        SuperRodEncounterGroup.ReadObjectSlots(br);

        br.ReadByte();
        br.ReadByte();
        br.ReadByte();

        GrassEncounterGroup.ReadGroup(br);
        SurfEncounterGroup.ReadGroup(br);
        OldRodEncounterGroup.ReadGroup(br);
        GoodRodEncounterGroup.ReadGroup(br);
        SuperRodEncounterGroup.ReadGroup(br);
      }
    }

    public override byte[] ToByteArray()
    {
      MemoryStream newData = new MemoryStream();
      using (BinaryWriter bw = new BinaryWriter(newData))
      {
        GrassEncounterGroup.WriteObjectSlots(bw);
        SurfEncounterGroup.WriteObjectSlots(bw);
        OldRodEncounterGroup.WriteObjectSlots(bw);
        GoodRodEncounterGroup.WriteObjectSlots(bw);
        SuperRodEncounterGroup.WriteObjectSlots(bw);

        bw.Write((byte)0);
        bw.Write((byte)0);
        bw.Write((byte)0);

        GrassEncounterGroup.WriteGroup(bw);
        SurfEncounterGroup.WriteGroup(bw);
        OldRodEncounterGroup.WriteGroup(bw);
        GoodRodEncounterGroup.WriteGroup(bw);
        SuperRodEncounterGroup.WriteGroup(bw);
      }
      return newData.ToArray();
    }

    // The editor says nothing of its own when a save works, so don't pop the base class's message.
    public bool SaveToFile()
    {
      string path = Filesystem.GetSafariZonePath(ID);
      return SaveToFile(path, showSuccessMessage: false);
    }

    public bool SaveToFile(int id)
    {
      string path = Filesystem.GetSafariZonePath(id);
      return SaveToFile(path, showSuccessMessage: false);
    }
  }
}
