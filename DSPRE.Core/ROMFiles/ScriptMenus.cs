using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The multichoice menus and lists field scripts put up, found by reading every script file once per ROM, so a text
    /// line can be shown in the menus that use it.
    /// </summary>
    public static class ScriptMenus
    {
        public sealed class Menu
        {
            public int ScriptFile { get; init; }
            /// <summary>The window's writing area, in tiles, as the script gives it.</summary>
            public int X { get; init; }
            public int Y { get; init; }
            public bool IsList { get; init; }
            public bool OnTouchScreen { get; init; }
            /// <summary>The cursor's first entry.</summary>
            public int Cursor { get; init; }
            /// <summary>The archive the entries come from: the shared menu archive, or the map's own.</summary>
            public IReadOnlyList<int> Banks { get; init; }
            public IReadOnlyList<int> Entries { get; init; }
        }

        private static readonly object Gate = new object();
        private static string _rom;
        private static List<Menu> _menus;

        /// <summary>The menus that show <paramref name="line"/> of <paramref name="bank"/>.</summary>
        public static List<Menu> Showing(int bank, int line)
        {
            List<Menu> found = new List<Menu>();
            foreach (Menu menu in All())
                if (menu.Entries.Contains(line) && menu.Banks.Contains(bank)) found.Add(menu);
            return found;
        }

        private static List<Menu> All()
        {
            lock (Gate)
            {
                if (_menus != null && _rom == RomInfo.WorkDir) return _menus;
                _rom = RomInfo.WorkDir;
                _menus = Scan();
                return _menus;
            }
        }

        private static List<Menu> Scan()
        {
            List<Menu> menus = new List<Menu>();
            if (string.IsNullOrWhiteSpace(Filesystem.Scripts) || !Directory.Exists(Filesystem.Scripts)) return menus;

            // A script's own menus read the text archive of the map running it.
            Dictionary<int, List<int>> mapText = new Dictionary<int, List<int>>();
            int headers = MapHeader.GetHeaderCount();
            for (ushort h = 0; h < headers; h++)
            {
                MapHeader header;
                try { header = MapHeader.GetMapHeader(h); }
                catch (Exception ex) { AppLogger.WarnOnce("ScriptMenus.Header", ex); continue; }
                if (header == null) continue;
                if (!mapText.TryGetValue(header.ScriptFileID, out List<int> banks)) mapText[header.ScriptFileID] = banks = new List<int>();
                if (!banks.Contains(header.TextArchiveID)) banks.Add(header.TextArchiveID);
            }
            List<int> shared = new List<int> { RomInfo.FieldMenuEntriesArchive };

            int count = Filesystem.GetScriptCount();
            for (int file = 0; file < count; file++)
            {
                string path = Filesystem.GetScriptPath(file);
                if (!File.Exists(path)) continue;
                ScriptFile script;
                // The binary as the ROM holds it; the plaintext reader keeps a cache the editors share.
                try { using (MemoryStream data = new MemoryStream(File.ReadAllBytes(path))) script = new ScriptFile(data, true, false, file); }
                catch (Exception ex) { AppLogger.WarnOnce("ScriptMenus.Script", ex); continue; }
                if (script.IsLevelScript) continue;

                List<int> local = mapText.TryGetValue(file, out List<int> own) ? own : new List<int>();
                foreach (List<ScriptCommandContainer> group in new[] { script.AllScripts, script.AllFunctions })
                    foreach (ScriptCommandContainer container in group)
                        Read(container.Commands, file, shared, local, menus);
            }
            return menus;
        }

        private static void Read(List<ScriptCommand> commands, int file, List<int> shared, List<int> local, List<Menu> menus)
        {
            if (commands == null) return;
            ScriptCommand start = null;
            string startName = null;
            List<int> entries = null;
            foreach (ScriptCommand cmd in commands)
            {
                string name = Name(cmd);
                switch (name)
                {
                    case "MultiStandardText":
                    case "MultiLocalText":
                    case "ListStandardText":
                    case "ListLocalText":
                    case "MultiTouchStandardText":
                    case "MultiTouchLocalText":
                        start = cmd;
                        startName = name;
                        entries = new List<int>();
                        break;
                    case "AddMultiOption":
                    case "AddListOption":
                    case "CreateMultiTouchBox":
                        entries?.Add((int)Value(cmd, 0));
                        break;
                    case "ShowMulti":
                    case "ShowList":
                    case "MultiColumn":
                    case "CloseMultiTouch":
                        if (start == null || entries == null || entries.Count == 0) break;
                        bool touch = startName.StartsWith("MultiTouch", StringComparison.Ordinal);
                        menus.Add(new Menu
                        {
                            ScriptFile = file,
                            X = (int)Value(start, 0),
                            Y = (int)Value(start, 1),
                            Cursor = touch ? 0 : (int)Value(start, 2),
                            IsList = startName.StartsWith("List", StringComparison.Ordinal),
                            OnTouchScreen = touch,
                            Banks = startName.Contains("Standard") ? shared : local,
                            Entries = entries,
                        });
                        start = null;
                        entries = null;
                        break;
                }
            }
        }

        private static string Name(ScriptCommand cmd)
        {
            if (cmd.Id != null)
            {
                string legacy = ScriptWalker.DefaultLegacyName(cmd.Id.Value);
                if (!string.IsNullOrEmpty(legacy)) return legacy;
            }
            string full = cmd.Name ?? "";
            int space = full.IndexOf(' ');
            return space < 0 ? full : full.Substring(0, space);
        }

        private static long Value(ScriptCommand cmd, int param)
        {
            List<byte[]> data = cmd?.CmdParams;
            if (data == null || param < 0 || param >= data.Count || data[param] == null) return 0;
            byte[] b = data[param];
            return b.Length switch { 1 => b[0], 2 => BitConverter.ToUInt16(b, 0), 4 => BitConverter.ToUInt32(b, 0), _ => 0 };
        }
    }
}
