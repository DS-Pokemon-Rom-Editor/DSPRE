using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;

namespace DSPRE
{
    /// <summary>Trainer "[id] Class Name" list builder (extracted from the WinForms <c>Helpers</c>; core).</summary>
    public static class TrainerNames
    {
        public static string[] GetAll()
        {
            List<string> trainerList = new List<string>();

            /* Store all trainer names and classes */
            TextArchive trainerClasses = new TextArchive(RomInfo.trainerClassMessageNumber);
            TextArchive trainerNames = new TextArchive(RomInfo.trainerNamesMessageNumber);

            int trainerCount = Filesystem.GetTrainerPropertiesCount();
            for (int i = 0; i < trainerCount; i++)
            {
                string path = Filesystem.GetTrainerPropertiesPath(i);
                int classMessageID = BitConverter.ToUInt16(DSUtils.ReadFromFile(path, startOffset: 1, 2), 0);
                string currentTrainerName;

                if (i < trainerNames.GetSimpleTrainerNames().Count)
                {
                    currentTrainerName = trainerNames.GetSimpleTrainerNames()[i];
                }
                else
                {
                    currentTrainerName = TrainerFile.NAME_NOT_FOUND;
                }

                trainerList.Add("[" + i.ToString("D2") + "] " + trainerClasses.messages[classMessageID] + " " + currentTrainerName);
            }

            return trainerList.ToArray();
        }
    }
}
