using System;
using System.Collections.Generic;
using System.IO;
using DSPRE.ROMFiles;

namespace DSPRE
{
    /// <summary>
    /// Shared, UI-agnostic logic for managing the ground-item script list (the entries an Overworld
    /// Item event's script number, 7000+index, can point at). Both the WinForms and Avalonia shells
    /// can call this directly, no UI-toolkit dependency.
    /// </summary>
    public static class GroundItemScriptsLogic
    {
        public const int ItemScrMin = 7000;
        public const int ItemScrMax = 8000;

        public sealed class Entry
        {
            public int ScriptIndex;
            public int ItemId;
            public int Quantity;
            public bool InUse;
        }

        public static List<Entry> GetEntries()
        {
            var itemScript = new ScriptFile(RomInfo.itemScriptFileNumber);
            var used = GetUsedScriptNumbers();
            var result = new List<Entry>();

            foreach (var e in DSUtils.GetGroundItemScriptEntries(itemScript))
            {
                result.Add(new Entry
                {
                    ScriptIndex = e.scriptIndex,
                    ItemId = e.itemId,
                    Quantity = e.quantity,
                    InUse = used.Contains(ItemScrMin + e.scriptIndex)
                });
            }

            return result;
        }

        public static HashSet<int> GetUsedScriptNumbers()
        {
            var used = new HashSet<int>();
            int fileCount = Filesystem.GetEventFileCount();

            for (int i = 0; i < fileCount; i++)
            {
                EventFile ev = new EventFile(i);
                foreach (Overworld ow in ev.overworlds)
                {
                    bool isItem = ow.type == (ushort)Overworld.OwType.ITEM || (ow.scriptNumber >= ItemScrMin && ow.scriptNumber <= ItemScrMax);
                    if (isItem)
                    {
                        used.Add(ow.scriptNumber);
                    }
                }
            }

            int? hardcoded = ReadDistortionWorldItemScript();
            if (hardcoded != null) used.Add(hardcoded.Value);

            return used;
        }

        private static bool IsItemScriptNumber(int n) => n >= ItemScrMin && n <= ItemScrMax;

        /// <summary>Platinum's Distortion World Griseous Orb, which overlay 9 names by script number rather than through an event file.</summary>
        private static int? ReadDistortionWorldItemScript()
        {
            uint offset = RomInfo.distortionWorldItemScriptOffset;
            if (offset == 0) return null;
            if (OverlayUtils.IsCompressed(9)) OverlayUtils.Decompress(9);
            string path = OverlayUtils.GetPath(9);
            if (!File.Exists(path) || new FileInfo(path).Length < offset + 2) return null;
            ushort value;
            using (DSUtils.EasyReader reader = new DSUtils.EasyReader(path, offset))
                value = reader.ReadUInt16();
            return IsItemScriptNumber(value) ? value : null;
        }

        private static void WriteDistortionWorldItemScript(int value)
        {
            using (DSUtils.EasyWriter writer = new DSUtils.EasyWriter(OverlayUtils.GetPath(9), RomInfo.distortionWorldItemScriptOffset))
                writer.Write((ushort)value);
        }

        public static void AddEntry(int itemId, int quantity)
        {
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts });

            var itemScript = new ScriptFile(RomInfo.itemScriptFileNumber);
            int insertAt = itemScript.allScripts.FindLastIndex(DSUtils.IsGroundItemScriptEntry) + 1;
            var cmdList = new List<ScriptCommand>
            {
                new ScriptCommand("SetVar 0x8008 " + itemId),
                new ScriptCommand("SetVar 0x8009 " + quantity),
                new ScriptCommand("Jump Function_#1")
            };
            var newEntry = new ScriptCommandContainer(uint.MaxValue, ScriptFile.ContainerTypes.Script, commandList: cmdList);
            itemScript.allScripts.Insert(insertAt, newEntry);
            itemScript.RenumberContainers();
            itemScript.SaveToFileDefaultDir(RomInfo.itemScriptFileNumber, showSuccessMessage: false);

            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.eventFiles });
            RemapItemReferences(n => n - ItemScrMin >= insertAt ? n + 1 : n);
        }

        /// <returns>null on success, or a user-facing error message if the entry is still in use.</returns>
        public static string RemoveEntry(int scriptIndex)
        {
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts });
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.eventFiles });

            int scriptNumber = ItemScrMin + scriptIndex;
            if (GetUsedScriptNumbers().Contains(scriptNumber))
            {
                return "This entry is currently used by an Overworld Item event and can't be removed.\nChange or delete that event first.";
            }

            var itemScript = new ScriptFile(RomInfo.itemScriptFileNumber);
            itemScript.allScripts.RemoveAt(scriptIndex);
            itemScript.RenumberContainers();
            itemScript.SaveToFileDefaultDir(RomInfo.itemScriptFileNumber, showSuccessMessage: false);

            ShiftOverworldReferencesAfterRemoval(scriptNumber);
            return null;
        }

        // Entries after the removed one shifted down a slot, so references past it must shift too.
        private static void ShiftOverworldReferencesAfterRemoval(int removedScriptNumber) =>
            RemapItemReferences(n => n > removedScriptNumber ? n - 1 : n);

        /// <summary>Moves item events and Platinum's overlay 9 reference to their entries' new script numbers.</summary>
        private static void RemapItemReferences(Func<int, int> remap)
        {
            int Shifted(int n) => remap(n);

            int fileCount = Filesystem.GetEventFileCount();
            for (int i = 0; i < fileCount; i++)
            {
                EventFile ev = new EventFile(i);
                bool dirty = false;

                foreach (Overworld ow in ev.overworlds)
                {
                    bool isItem = ow.type == (ushort)Overworld.OwType.ITEM || (ow.scriptNumber >= ItemScrMin && ow.scriptNumber <= ItemScrMax);
                    if (!isItem) continue;
                    int moved = Shifted(ow.scriptNumber);
                    if (moved != ow.scriptNumber) { ow.scriptNumber = (ushort)moved; dirty = true; }
                }

                if (dirty)
                {
                    ev.SaveToFileDefaultDir(i, showSuccessMessage: false);
                }
            }

            int? hardcoded = ReadDistortionWorldItemScript();
            if (hardcoded != null)
            {
                int moved = Shifted(hardcoded.Value);
                if (moved != hardcoded.Value) WriteDistortionWorldItemScript(moved);
            }
        }

        /// <summary>Holds ground-item adds and removes until <see cref="Save"/> writes the script and renumbers events together.</summary>
        public sealed class Session
        {
            private readonly ScriptFile _script;
            private readonly List<int?> _origin = new List<int?>();   // each container's original index, null when added here
            private readonly HashSet<int> _used;
            private readonly List<int> _removed = new List<int>();

            public Session()
            {
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts, RomInfo.DirNames.eventFiles });
                _script = new ScriptFile(RomInfo.itemScriptFileNumber);
                for (int i = 0; i < _script.allScripts.Count; i++) _origin.Add(i);
                _used = GetUsedScriptNumbers();
            }

            public bool HasChanges { get; private set; }

            public List<Entry> Entries()
            {
                var result = new List<Entry>();
                foreach (var e in DSUtils.GetGroundItemScriptEntries(_script))
                {
                    int? origin = _origin[e.scriptIndex];
                    result.Add(new Entry
                    {
                        ScriptIndex = e.scriptIndex, ItemId = e.itemId, Quantity = e.quantity,
                        InUse = origin != null && _used.Contains(ItemScrMin + origin.Value),
                    });
                }
                return result;
            }

            public void Add(int itemId, int quantity)
            {
                int insertAt = _script.allScripts.FindLastIndex(DSUtils.IsGroundItemScriptEntry) + 1;
                var cmdList = new List<ScriptCommand>
                {
                    new ScriptCommand("SetVar 0x8008 " + itemId),
                    new ScriptCommand("SetVar 0x8009 " + quantity),
                    new ScriptCommand("Jump Function_#1")
                };
                _script.allScripts.Insert(insertAt, new ScriptCommandContainer(uint.MaxValue, ScriptFile.ContainerTypes.Script, commandList: cmdList));
                _origin.Insert(insertAt, null);
                HasChanges = true;
            }

            /// <summary>Changes an entry's item and quantity in place, so events using it keep their script number.</summary>
            public void Update(int scriptIndex, int itemId, int quantity)
            {
                if (scriptIndex < 0 || scriptIndex >= _script.allScripts.Count) return;
                ScriptCommandContainer container = _script.allScripts[scriptIndex];
                if (!DSUtils.IsGroundItemScriptEntry(container)) return;
                container.commands[0] = SetVarLike(container.commands[0], itemId);
                container.commands[1] = SetVarLike(container.commands[1], quantity);
                HasChanges = true;
            }

            // Rebuilt from text rather than patching parameter bytes, so the command's text form stays in step.
            private static ScriptCommand SetVarLike(ScriptCommand original, int value)
            {
                ushort variable = BitConverter.ToUInt16(original.cmdParams[0], 0);
                return new ScriptCommand($"SetVar 0x{variable:X4} {value}");
            }

            /// <returns>null on success, or why the entry can't go.</returns>
            public string Remove(int scriptIndex)
            {
                if (scriptIndex < 0 || scriptIndex >= _script.allScripts.Count) return null;
                int? origin = _origin[scriptIndex];
                if (origin != null && _used.Contains(ItemScrMin + origin.Value))
                    return "An item event on the map still uses this entry. Change or delete that event first.";
                _script.allScripts.RemoveAt(scriptIndex);
                _origin.RemoveAt(scriptIndex);
                if (origin != null) _removed.Add(ItemScrMin + origin.Value);
                HasChanges = true;
                return null;
            }

            public void Save()
            {
                if (!HasChanges) return;
                // Every surviving entry's original script number to its new one. An add lands before any
                // later scripts in the file, so moves go both ways.
                var moved = new Dictionary<int, int>();
                for (int i = 0; i < _origin.Count; i++)
                    if (_origin[i] != null) moved[ItemScrMin + _origin[i].Value] = ItemScrMin + i;
                var removed = new List<int>(_removed);
                int Remap(int n)
                {
                    if (moved.TryGetValue(n, out int m)) return m;
                    // Removed or past the end: slide down past removed entries only, as before.
                    int below = 0;
                    foreach (int r in removed) if (r < n) below++;
                    return n - below;
                }

                _script.RenumberContainers();
                _script.SaveToFileDefaultDir(RomInfo.itemScriptFileNumber, showSuccessMessage: false);
                RemapItemReferences(Remap);
                _removed.Clear();
                for (int i = 0; i < _origin.Count; i++) _origin[i] = i;
                _used.Clear();
                foreach (int n in GetUsedScriptNumbers()) _used.Add(n);
                HasChanges = false;
            }
        }
    }
}
