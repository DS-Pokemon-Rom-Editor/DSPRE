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
                        details = "The action above may be missing its End.";
                    }
                    else
                    {
                        details = "It doesn't look like an action command.";
                    }
                    AppMessages.Error("Line " + lineNumber + " can't be read: \"" + wholeLine + "\".\n\n" + details, "Script not saved");
                    Id = null;
                    return;
                }
            }

            if (Id == 0x00FE && nameParts.Length != 1 || Id != 0x00FE && nameParts.Length != 2)
            { //E.g.: End 0x2 0x40    OR     LookUp
                AppMessages.Error("Action " + nameParts[0] + " on line " + lineNumber + " takes 1 parameter but has " + (nameParts.Length - 1) + ".", "Script not saved");
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
