using System;
using System.Collections.Generic;
using System.IO;

namespace DSPRE.ROMFiles
{
    public static class ScriptSources
    {
        // Rotom writes an ordinary script file as .rotom and a level script as .json.
        public static HashSet<int> OrdinaryIds()
        {
            HashSet<int> ids = new HashSet<int>();
            if (!RomInfo.HasRotomProject) return ids;
            foreach (string path in RotomTool.SourceFiles())
                if (Path.GetExtension(path).Equals(".rotom", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(Path.GetFileNameWithoutExtension(path), out int id)) ids.Add(id);
            return ids;
        }

        public static bool IsOrdinaryScript(int id) => OrdinaryIds().Contains(id);
    }
}
