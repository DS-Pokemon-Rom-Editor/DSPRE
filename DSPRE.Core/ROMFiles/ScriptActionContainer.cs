using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    public class ScriptActionContainer
    {
        public List<ScriptAction> Commands;
        public uint ManualUserID;

        public ScriptActionContainer(uint actionNumber, List<ScriptAction> commands = null)
        {
            ManualUserID = actionNumber;
            this.Commands = commands;
        }
    }
}