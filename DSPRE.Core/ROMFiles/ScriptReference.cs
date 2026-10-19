namespace DSPRE.ROMFiles
{
  internal class ScriptReference
  {
    public ScriptFile.ContainerTypes TypeOfCaller { get; private set; }
    public uint CallerID { get; private set; }
    public ScriptFile.ContainerTypes TypeOfInvoked { get; private set; }
    public uint InvokedID { get; private set; }
    public int InvokedAt { get; private set; }

    public ScriptReference(ScriptFile.ContainerTypes typeOfCaller, uint callerID, ScriptFile.ContainerTypes invokedType, uint invokedID, int invokedAt)
    {
      this.TypeOfCaller = typeOfCaller;
      this.CallerID = callerID;
      this.TypeOfInvoked = invokedType;
      this.InvokedID = invokedID;

      this.InvokedAt = invokedAt;
    }

    public override string ToString()
    {
      return TypeOfCaller + " " + CallerID + " invokes " + TypeOfInvoked + " " + InvokedID + " at " + InvokedAt;
    }
  }
}
