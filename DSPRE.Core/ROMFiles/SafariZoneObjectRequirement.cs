using System.Collections.Generic;
using System.IO;

namespace DSPRE.ROMFiles
{
  public class SafariZoneObjectRequirement
  {
    public static Dictionary<int, string> ObjectTypes = new Dictionary<int, string>() {
      { 0, "No requirement" },
      { 1, "Plains" },
      { 2, "Forest" },
      { 3, "Peak" },
      { 4, "Waterside" },
    };

    public byte TypeID;
    // Points of this object type, not a count of objects; each object earns 1 to 7 by how long its area has been placed.
    public byte Quantity;

    public SafariZoneObjectRequirement(byte typeID = 0, byte quantity = 0)
    {
      this.TypeID = typeID;
      this.Quantity = quantity;
    }

    public SafariZoneObjectRequirement(BinaryReader br)
    {
      ReadRequirement(br);
    }

    public void ReadRequirement(BinaryReader br)
    {
      TypeID = br.ReadByte();
      Quantity = br.ReadByte();
    }

    public void WriteRequirement(BinaryWriter bw)
    {
      bw.Write((byte)TypeID);
      bw.Write((byte)Quantity);
    }

    public override string ToString()
    {
      return $"{TypeID} {ObjectTypes[TypeID]}: {Quantity}";
    }
  }
}
