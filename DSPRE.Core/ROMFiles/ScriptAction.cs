using DSPRE.Resources;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DSPRE.ROMFiles
{
    public class ActionContainer
    {
        public List<ScriptAction> ActionCommandsList;
        public uint ManualUserID;

        #region Constructors (2)
        public ActionContainer(uint actionNumber, List<ScriptAction> actionCommandsList = null)
        {
            ManualUserID = actionNumber;
            this.ActionCommandsList = actionCommandsList;
        }
        #endregion
    }
    public class ScriptAction
    {

        #region Fields (4)
        public ushort? Id;
        public ushort? RepetitionCount;
        public string Name;
        #endregion

        /// <summary>An action built by hand, for callers that already know its name.</summary>
        public ScriptAction() { }

        public ScriptAction(ushort id, ushort? repetitionCount = null)
        {
            this.Id = id;
            this.RepetitionCount = repetitionCount;

            if (!RomInfo.ScriptActionNamesDict.TryGetValue(id, out Name))
            {
                Name = id.ToString("X4");
            }

            if (repetitionCount != null && id != 0x00FE)
            {
                Name += " " + "0x" + ((ushort)repetitionCount).ToString("X");
            }
        }
        public ScriptAction(string wholeLine, int lineNumber)
        {
            Name = wholeLine;

            string[] nameParts = wholeLine.Replace("\t", "").Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); // Separate command code from parameters
            /* Get command id, which is always first in the description */

            if (RomInfo.ScriptActionNamesReverseDict.TryGetValue(nameParts[0].ToLower(), out ushort cmdID))
            {
                this.Id = cmdID;
            }
            else
            {
                if (ushort.TryParse(nameParts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort buf))
                {
                    Id = buf;
                }
                else
                {
                    string details;
                    if (wholeLine.Contains(':') && wholeLine.ContainsNumber())
                    {
                        details = "This probably means you forgot to \"End\" the Action above it.";
                    }
                    else
                    {
                        details = "Are you sure it's a proper Action Command?";
                    }
                    AppMessages.Error("This Script file could not be saved." +
                        Environment.NewLine + "Parser failed to interpret line " + lineNumber + ": \"" + wholeLine + "\"." +
                        Environment.NewLine + "\n" + details, "Parser error");
                    Id = null;
                    return;
                }
            }

            if (Id == 0x00FE && nameParts.Length != 1 || Id != 0x00FE && nameParts.Length != 2)
            { //E.g.: End 0x2 0x40    OR     LookUp
                AppMessages.Error("Wrong number of parameters for action " + nameParts[0] + " at line " + lineNumber + "." + Environment.NewLine +
                    "Received: " + (nameParts.Length - 1) + Environment.NewLine + "Expected: 1"
                    + Environment.NewLine + "\nThis Script File can not be saved.", "Error");
                Id = null;
            }
            else
            {
                if (Id == 0x00FE)
                {
                    RepetitionCount = 0;
                }
                else
                {
                    NumberStyles style;
                    if (nameParts[1].StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
                    {
                        style = NumberStyles.HexNumber;
                        nameParts[1] = nameParts[1].Substring(2);
                    }
                    else
                    {
                        style = NumberStyles.Integer;
                    }
                    RepetitionCount = ushort.Parse(nameParts[1], style);
                }
            }
        }
    }
}
