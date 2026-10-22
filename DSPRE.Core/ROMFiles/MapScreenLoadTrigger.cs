namespace DSPRE.ROMFiles
{
  public class MapScreenLoadTrigger : LevelScriptTrigger
  {
    public MapScreenLoadTrigger(int type, int scriptTriggered) : base(type, scriptTriggered) { }

    public override string ToString()
    {
      switch (TriggerType)
      {
        case LevelScriptTrigger.MAPCHANGE:
          return base.ToString() + " when the player enters the map";
        case LevelScriptTrigger.SCREENRESET:
          return base.ToString() + " when the screen fades back in on the map";
        case LevelScriptTrigger.LOADGAME:
          return base.ToString() + " when a saved game continues on the map";
        default:
          return base.ToString() + " on an unknown trigger";
      }
    }

    public override bool Equals(object obj)
    {
      // If the passed object is null
      if (obj == null)
      {
        return false;
      }

      if (obj is MapScreenLoadTrigger)
      {
        return this.ToString() == ((MapScreenLoadTrigger)obj).ToString();
      }

      return false;
    }

    public override int GetHashCode()
    {
      return this.TriggerType.GetHashCode() ^ ScriptTriggered.GetHashCode();
    }
  }
}
