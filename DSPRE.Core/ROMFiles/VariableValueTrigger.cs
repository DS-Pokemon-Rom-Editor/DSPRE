namespace DSPRE.ROMFiles
{
  public class VariableValueTrigger : LevelScriptTrigger
  {
    public int VariableToWatch { get; set; }
    public int ExpectedValue { get; set; }

    public VariableValueTrigger(int scriptIDtoTrigger, int variableToWatch, int expectedValue) : base(VARIABLEVALUE, scriptIDtoTrigger)
    {
      this.VariableToWatch = variableToWatch;
      this.ExpectedValue = expectedValue;
    }

    // The game reads both halves through its variable lookup: from 0x4000 a number is a save variable,
    // and from 0x8000 it is script memory that does not exist while no script runs, which freezes the map.
    public const int FirstVariable = 0x4000;
    public const int FirstScriptLocal = 0x8000;

    public override string ToString()
    {
      string expected = ExpectedValue >= FirstVariable ? $"variable 0x{ExpectedValue:X4}" : FormatValue(ExpectedValue);
      return base.ToString() + $" when variable 0x{VariableToWatch:X4} is " + expected;
    }

    /// <summary>The last save variable this game has.</summary>
    public static int LastVariable => FirstVariable + RomInfo.SaveVariableCount - 1;

    /// <summary>Why the game cannot run this trigger, or null.</summary>
    public string Problem()
    {
      if (VariableToWatch < FirstVariable || VariableToWatch > LastVariable)
        return $"watches {FormatValue(VariableToWatch)}, which is not a save variable ({FormatValue(FirstVariable)} to {FormatValue(LastVariable)})";
      if (ExpectedValue >= FirstScriptLocal)
        return $"expects {FormatValue(ExpectedValue)}, which the game reads as script memory and freezes the map";
      if (ExpectedValue > LastVariable)
        return $"expects {FormatValue(ExpectedValue)}, which the game reads as a variable it does not have";
      return null;
    }

    public override bool Equals(object obj)
    {
      // If the passed object is null
      if (obj == null)
      {
        return false;
      }

      if (!(obj is VariableValueTrigger other))
      {
        return false;
      }

      return this.TriggerType == other.TriggerType 
          && this.ScriptTriggered == other.ScriptTriggered
          && this.VariableToWatch == other.VariableToWatch
          && this.ExpectedValue == other.ExpectedValue;
    }

    public override int GetHashCode()
    {
      return this.TriggerType.GetHashCode() ^ VariableToWatch.GetHashCode() ^ ExpectedValue.GetHashCode();
    }
  }
}
