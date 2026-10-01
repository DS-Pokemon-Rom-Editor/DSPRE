namespace DSPRE.ROMFiles {
  public class VariableValueTrigger : LevelScriptTrigger {
    public int variableToWatch { get; set; }
    public int expectedValue { get; set; }

    public VariableValueTrigger(int scriptIDtoTrigger, int variableToWatch, int expectedValue) : base(VARIABLEVALUE, scriptIDtoTrigger) {
      this.variableToWatch = variableToWatch;
      this.expectedValue = expectedValue;
    }

    // The game reads both halves through its variable lookup: from 0x4000 a number is a save variable,
    // and from 0x8000 it is script memory that does not exist while no script runs, which freezes the map.
    public const int FirstVariable = 0x4000;
    public const int FirstScriptLocal = 0x8000;

    public override string ToString() {
      string expected = expectedValue >= FirstVariable ? "Var " + FormatValue(expectedValue) : FormatValue(expectedValue);
      return base.ToString() + " when Var " + FormatValue(variableToWatch) + " == " + expected;
    }

    /// <summary>The last save variable this game has.</summary>
    public static int LastVariable => FirstVariable + RomInfo.SaveVariableCount - 1;

    /// <summary>Why the game cannot run this trigger, or null.</summary>
    public string Problem() {
      if (variableToWatch < FirstVariable || variableToWatch > LastVariable)
        return $"watches {FormatValue(variableToWatch)}, which is not a save variable ({FormatValue(FirstVariable)} to {FormatValue(LastVariable)})";
      if (expectedValue >= FirstScriptLocal)
        return $"expects {FormatValue(expectedValue)}, which the game reads as script memory and freezes the map";
      if (expectedValue > LastVariable)
        return $"expects {FormatValue(expectedValue)}, which the game reads as a variable it does not have";
      return null;
    }

    public override bool Equals(object obj) {
      // If the passed object is null
      if (obj == null) {
        return false;
      }

      if (!(obj is VariableValueTrigger other)) {
        return false;
      }

      return this.triggerType == other.triggerType 
          && this.scriptTriggered == other.scriptTriggered
          && this.variableToWatch == other.variableToWatch
          && this.expectedValue == other.expectedValue;
    }

    public override int GetHashCode() {
      return this.triggerType.GetHashCode() ^ variableToWatch.GetHashCode() ^ expectedValue.GetHashCode();
    }
  }
}
