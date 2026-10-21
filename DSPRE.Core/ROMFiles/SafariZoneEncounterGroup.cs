using System.ComponentModel;
using System.IO;

namespace DSPRE.ROMFiles
{
  public class SafariZoneEncounterGroup
  {
    private const int EncounterSlots = 10;

    public byte ObjectSlots;
    public BindingList<SafariZoneEncounter> MorningEncounters = new BindingList<SafariZoneEncounter>();
    public BindingList<SafariZoneEncounter> DayEncounters = new BindingList<SafariZoneEncounter>();
    public BindingList<SafariZoneEncounter> NightEncounters = new BindingList<SafariZoneEncounter>();
    public BindingList<SafariZoneEncounter> MorningEncountersObject = new BindingList<SafariZoneEncounter>();
    public BindingList<SafariZoneEncounter> DayEncountersObject = new BindingList<SafariZoneEncounter>();
    public BindingList<SafariZoneEncounter> NightEncountersObject = new BindingList<SafariZoneEncounter>();
    public BindingList<SafariZoneObjectRequirement> ObjectRequirements = new BindingList<SafariZoneObjectRequirement>();
    public BindingList<SafariZoneObjectRequirement> OptionalObjectRequirements = new BindingList<SafariZoneObjectRequirement>();

    /// <summary>Ten empty slots for each time of day and no object slots, the smallest group that writes.</summary>
    public static SafariZoneEncounterGroup Blank()
    {
      SafariZoneEncounterGroup group = new SafariZoneEncounterGroup();
      for (int i = 0; i < EncounterSlots; i++)
      {
        group.MorningEncounters.Add(new SafariZoneEncounter());
        group.DayEncounters.Add(new SafariZoneEncounter());
        group.NightEncounters.Add(new SafariZoneEncounter());
      }
      return group;
    }

    public void ReadObjectSlots(BinaryReader br)
    {
      ObjectSlots = br.ReadByte();
    }

    public void WriteObjectSlots(BinaryWriter bw)
    {
      bw.Write((byte)ObjectSlots);
    }

    public void ReadGroup(BinaryReader br)
    {
      //#2 Section - Tall Grass Encounters
      for (int i = 0; i < EncounterSlots; i++)
      {
        MorningEncounters.Add(new SafariZoneEncounter(br));
        br.ReadByte();
      }

      for (int i = 0; i < EncounterSlots; i++)
      {
        DayEncounters.Add(new SafariZoneEncounter(br));
        br.ReadByte();
      }

      for (int i = 0; i < EncounterSlots; i++)
      {
        NightEncounters.Add(new SafariZoneEncounter(br));
        br.ReadByte();
      }

      //#3 Section - Tall Grass Encounters (Object Arrangement)
      for (int i = 0; i < ObjectSlots; i++)
      {
        MorningEncountersObject.Add(new SafariZoneEncounter(br));
        br.ReadByte();
      }

      for (int i = 0; i < ObjectSlots; i++)
      {
        DayEncountersObject.Add(new SafariZoneEncounter(br));
        br.ReadByte();
      }

      for (int i = 0; i < ObjectSlots; i++)
      {
        NightEncountersObject.Add(new SafariZoneEncounter(br));
        br.ReadByte();
      }

      //#4 Section - Object Arrangement Requirements (Tall Grass)
      for (int i = 0; i < ObjectSlots; i++)
      {
        ObjectRequirements.Add(new SafariZoneObjectRequirement(br));
        OptionalObjectRequirements.Add(new SafariZoneObjectRequirement(br));
      }
    }

    public void WriteGroup(BinaryWriter bw)
    {
      //#2 Section - Tall Grass Encounters
      for (int i = 0; i < MorningEncounters.Count; i++)
      {
        MorningEncounters[i].WriteEncounter(bw);
        bw.Write((byte)0);
      }

      for (int i = 0; i < EncounterSlots; i++)
      {
        DayEncounters[i].WriteEncounter(bw);
        bw.Write((byte)0);
      }

      for (int i = 0; i < EncounterSlots; i++)
      {
        NightEncounters[i].WriteEncounter(bw);
        bw.Write((byte)0);
      }

      //#3 Section - Tall Grass Encounters (Object Arrangement)
      for (int i = 0; i < ObjectSlots; i++)
      {
        MorningEncountersObject[i].WriteEncounter(bw);
        bw.Write((byte)0);
      }

      for (int i = 0; i < ObjectSlots; i++)
      {
        DayEncountersObject[i].WriteEncounter(bw);
        bw.Write((byte)0);
      }

      for (int i = 0; i < ObjectSlots; i++)
      {
        NightEncountersObject[i].WriteEncounter(bw);
        bw.Write((byte)0);
      }

      //#4 Section - Object Arrangement Requirements (Tall Grass)
      for (int i = 0; i < ObjectSlots; i++)
      {
        ObjectRequirements[i].WriteRequirement(bw);
        OptionalObjectRequirements[i].WriteRequirement(bw);
      }
    }
  }
}
