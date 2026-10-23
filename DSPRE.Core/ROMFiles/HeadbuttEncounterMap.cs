namespace DSPRE.ROMFiles
{
    // A header can use one map file more than once, so each use has its own entry, shown as matrix x,y and map.
  class HeadbuttEncounterMap
  {
    public readonly int MapID;
    public readonly int X;
    public readonly int Y;

    public HeadbuttEncounterMap(int mapID, int x, int y)
    {
      this.MapID = mapID;
      this.X = x;
      this.Y = y;
    }

    public override string ToString()
    {
      return $"{MapID} - {X},{Y}";
    }

    public override bool Equals(object obj)
    {
      if (obj == null)
      {
        return false;
      }

      if (obj is HeadbuttEncounterMap)
      {
        return this.ToString() == ((HeadbuttEncounterMap)obj).ToString();
      }

      return false;
    }

    public override int GetHashCode()
    {
      return this.X.GetHashCode() ^ Y.GetHashCode() ^ MapID.GetHashCode();
    }
  }
}
