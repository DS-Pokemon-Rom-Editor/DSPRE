using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.CharMaps;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>The empty files a new map header points at, added the way each file's own editor adds one.</summary>
    public static class NewProjectFiles
    {
        /// <summary>A level script with no triggers.</summary>
        public const string BlankLevelScriptJson = "{\n  \"header_entries\": [],\n  \"var_conditions\": []\n}\n";

        /// <summary>The one-line script a new script file starts with; rotom numbers script slots from #1.</summary>
        public const string BlankScriptRotom = "script Main #1:\n\tEnd\n";

        /// <summary>Adds a text archive holding one message and returns its number.</summary>
        public static int AddTextArchive(string firstMessage = "Your text here.", object sender = null)
        {
            int id = Filesystem.GetTextArchivesCount();
            new TextArchive(id, new List<string> { firstMessage }).SaveToExpandedDir(id, showSuccessMessage: false, sender: sender);
            (string binPath, string jsonPath) = TextArchive.GetFilePaths(id);
            TextConverter.JSONToBin(jsonPath, binPath, CharMapManager.GetCharMapPath());
            return id;
        }

        /// <summary>Adds an event file with no events and returns its number.</summary>
        public static int AddEventFile()
        {
            int id = Filesystem.GetEventFileCount();
            new EventFile().SaveToFileDefaultDir(id, showSuccessMessage: false);
            return id;
        }

        /// <summary>The number the next script file takes: one past every compiled file and every source.</summary>
        public static int NextScriptId()
        {
            int next = Filesystem.GetScriptCount();
            foreach (string root in RotomTool.ScriptSourceRoots())
            {
                if (!Directory.Exists(root)) continue;
                foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    if (int.TryParse(Path.GetFileNameWithoutExtension(file), out int id)) next = Math.Max(next, id + 1);
            }
            return next;
        }

        /// <summary>Writes a script source for the next script number; rotom builds it with the project.</summary>
        public static (int Id, string Path) AddScriptSource(string extension, string content)
        {
            string root = RotomTool.ScriptSourceRoots().FirstOrDefault() ?? Path.Combine(RotomTool.ProjectRoot, "expanded", "scripts");
            Directory.CreateDirectory(root);
            int id = NextScriptId();
            string path = Path.Combine(root, id.ToString("D4") + extension);
            while (File.Exists(path)) path = Path.Combine(root, (++id).ToString("D4") + extension);
            PlainFiles.WriteText(path, content);
            return (id, path);
        }

        public static (int Id, string Path) AddScript() => AddScriptSource(".rotom", BlankScriptRotom);

        /// <summary>Headers loading a script-archive file, as their scripts or as their level script.</summary>
        public static List<(ushort Header, string Name)> HeadersUsingScriptFile(int id) =>
            HeaderLinks.HeadersLinking(HeaderLinks.Kind.Script, id)
                .Concat(HeaderLinks.HeadersLinking(HeaderLinks.Kind.LevelScript, id))
                .GroupBy(u => u.Header).Select(g => g.First()).OrderBy(u => u.Header).ToList();

        /// <summary>Why the last script-archive file can't be removed, or null.</summary>
        public static string WhyKeepLastScriptFile(int id)
        {
            string archiveDir = gameDirs[DirNames.scripts].unpackedDir;
            int members = Directory.Exists(archiveDir) ? Directory.GetFiles(archiveDir).Count(f => int.TryParse(Path.GetFileName(f), out _)) : 0;
            if (members - 1 > id)
                return $"The script archive has {members} files but the sources stop at {id}; check the project's scripts folder.";
            List<(ushort Header, string Name)> uses = HeadersUsingScriptFile(id);
            if (uses.Count > 0)
                return $"Script file {id} stays: these headers still use it." + Environment.NewLine + HeaderLinks.Describe(uses)
                    + Environment.NewLine + "Point them at another file first.";
            return null;
        }

        /// <summary>Deletes a script-archive file's sources and its compiled copy.</summary>
        public static void DeleteScriptFile(int id)
        {
            foreach (string root in RotomTool.ScriptSourceRoots())
            {
                if (!Directory.Exists(root)) continue;
                foreach (string file in Directory.EnumerateFiles(root, id.ToString("D4") + ".*", SearchOption.AllDirectories))
                    File.Delete(file);
            }
            string member = Path.Combine(gameDirs[DirNames.scripts].unpackedDir, id.ToString("D4"));
            ArchiveMembers.Delete(member);
        }

        public static (int Id, string Path) AddBlankLevelScript() => AddScriptSource(".json", BlankLevelScriptJson);

        /// <summary>Gives every header up to this one an empty headbutt file (HGSS keys them by header). Returns how many.</summary>
        public static int FillHeadbuttFiles(int headerCount)
        {
            if (gameFamily != GameFamilies.HGSS || !gameDirs.ContainsKey(DirNames.headbutt)) return 0;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.headbutt });
            int made = 0;
            for (int id = Filesystem.GetHeadbuttCount(); id < headerCount; id++)
            {
                // Two zero tree-group counts: a map with no headbutt trees.
                ArchiveMembers.Put(DirNames.headbutt, id, new byte[4]);
                made++;
            }
            return made;
        }
    }
}
