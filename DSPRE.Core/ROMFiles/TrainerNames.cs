using System;
using System.Collections.Generic;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;

namespace DSPRE
{
    /// <summary>Trainer "[id] Class Name" list builder (extracted from the WinForms <c>Helpers</c>; core).</summary>
    public static class TrainerNames
    {
        private const string TrainerClassHeader = "include/constants/trainerclass.h";

        public static string[] GetAll()
        {
            // hg-engine builds the trainer files and names from Trainers.c, which is newer than the ROM copy.
            if (HgEngineProject.IsActive)
            {
                string[] classNames = RomInfo.GetTrainerClassNames();
                var blocks = HgEngineTrainerSource.LoadAll();
                var entries = new string[blocks.Count];
                for (int i = 0; i < blocks.Count; i++) entries[i] = HgEngineEntry(i, blocks[i], classNames);
                return entries;
            }

            List<string> trainerList = new List<string>();

            /* Store all trainer names and classes */
            TextArchive trainerClasses = new TextArchive(RomInfo.trainerClassMessageNumber);
            TextArchive trainerNames = new TextArchive(RomInfo.trainerNamesMessageNumber);

            List<string> simpleNames = trainerNames.GetSimpleTrainerNames();
            int trainerCount = Filesystem.GetTrainerPropertiesCount();
            for (int i = 0; i < trainerCount; i++)
            {
                string path = Filesystem.GetTrainerPropertiesPath(i);
                int classMessageID = BitConverter.ToUInt16(DSUtils.ReadFromFile(path, startOffset: 1, 2), 0);
                string currentTrainerName = i < simpleNames.Count ? simpleNames[i] : TrainerFile.NAME_NOT_FOUND;
                // hg-engine allows class ids with no name in the class text.
                string className = classMessageID < trainerClasses.messages.Count ? trainerClasses.messages[classMessageID] : $"Class {classMessageID}";

                trainerList.Add("[" + i.ToString("D2") + "] " + className + " " + currentTrainerName);
            }

            return trainerList.ToArray();
        }

        /// <summary>One list row for a Trainers.c entry.</summary>
        public static string HgEngineEntry(int id, HgEngineSourceBlock block, string[] classNames)
        {
            string name = block.TryGetString(new[] { FieldPathSegment.Field("name") }, out string n) ? n : "";
            string className = block.TryGetSymbol(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("trainerClass") }, TrainerClassHeader, out int c)
                ? (c >= 0 && c < classNames.Length ? classNames[c] : $"Class {c}") : "";
            return "[" + id.ToString("D2") + "] " + className + " " + name;
        }

        /// <summary>Just the names from Trainers.c, by trainer id.</summary>
        public static string[] HgEngineSimpleNames()
        {
            var blocks = HgEngineTrainerSource.LoadAll();
            var names = new string[blocks.Count];
            for (int i = 0; i < blocks.Count; i++)
                names[i] = blocks[i].TryGetString(new[] { FieldPathSegment.Field("name") }, out string n) ? n : "";
            return names;
        }
    }
}
