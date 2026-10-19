namespace DSPRE.ROMFiles
{
  //This class is in case a MapHeader uses the same MapFile more than once
  //ToString is the matrix x,y and mapID
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
      // If the passed object is null
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
