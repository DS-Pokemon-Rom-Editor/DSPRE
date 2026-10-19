using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    public class ScriptCommandContainer
    {
        public List<ScriptCommand> Commands;
        public uint ManualUserID;
        public int UsedScriptID; //useScript ID referenced by this Script/Function
        public ScriptFile.ContainerTypes ContainerType;
        internal static readonly string FunctionStart;

        public ScriptCommandContainer(uint scriptNumber, ScriptFile.ContainerTypes containerType, int usedScriptID = -1, List<ScriptCommand> commandList = null)
        {
            ManualUserID = scriptNumber;
            this.UsedScriptID = usedScriptID;
            this.ContainerType = containerType;
            Commands = commandList;
        }

        public ScriptCommandContainer(uint newID, ScriptCommandContainer toCopy)
        {
            ManualUserID = newID;
            UsedScriptID = toCopy.UsedScriptID;
            ContainerType = toCopy.ContainerType;
            Commands = new List<ScriptCommand>(toCopy.Commands); //command parameters need to be copied recursively
        }
    }
}