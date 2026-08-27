namespace DSPRE.ROMFiles {
  public class VariableValueTrigger : LevelScriptTrigger {
    public int variableToWatch { get; set; }
    public int expectedValue { get; set; }

    public VariableValueTrigger(int scriptIDtoTrigger, int variableToWatch, int expectedValue) : base(VARIABLEVALUE, scriptIDtoTrigger) {
      this.variableToWatch = variableToWatch;
      this.expectedValue = expectedValue;
    }

    public override string ToString() {
      return base.ToString() + " when Var " + FormatValue(variableToWatch) + " == " + FormatValue(expectedValue);
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
