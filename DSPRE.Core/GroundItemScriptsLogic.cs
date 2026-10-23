using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using DSPRE.ROMFiles;

namespace DSPRE
{
    /// <summary>The ground item scripts. An item event picks one with script number 7000 plus its index.</summary>
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
            ScriptFile itemScript = new ScriptFile(RomInfo.ItemScriptFileNumber);
            HashSet<int> used = GetUsedScriptNumbers();
            List<Entry> result = new List<Entry>();

            foreach ((int scriptIndex, int itemId, int quantity) e in DSUtils.GetGroundItemScriptEntries(itemScript))
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
            HashSet<int> used = new HashSet<int>();
            int fileCount = Filesystem.GetEventFileCount();

            for (int i = 0; i < fileCount; i++)
            {
                EventFile ev = new EventFile(i);
                foreach (Overworld ow in ev.Overworlds)
                {
                    bool isItem = ow.Type == (ushort)Overworld.OwType.ITEM || (ow.ScriptNumber >= ItemScrMin && ow.ScriptNumber <= ItemScrMax);
                    if (isItem)
                    {
                        used.Add(ow.ScriptNumber);
                    }
                }
            }

            int? hardcoded = ReadDistortionWorldItemScript();
            if (hardcoded != null) used.Add(hardcoded.Value);

            return used;
        }

        private static bool IsItemScriptNumber(int n) => n >= ItemScrMin && n <= ItemScrMax;

        /// <summary>Platinum's Distortion World Griseous Orb, which overlay 9 names by script number, not through an event file.</summary>
        private static int? ReadDistortionWorldItemScript()
        {
            uint offset = RomInfo.DistortionWorldItemScriptOffset;
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
            DSUtils.WriteToFile(OverlayUtils.GetPath(9), BitConverter.GetBytes((ushort)value), RomInfo.DistortionWorldItemScriptOffset);
        }

        public static void AddEntry(int itemId, int quantity)
        {
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.Scripts });

            ScriptFile itemScript = new ScriptFile(RomInfo.ItemScriptFileNumber);
            int insertAt = itemScript.AllScripts.FindLastIndex(DSUtils.IsGroundItemScriptEntry) + 1;
            List<ScriptCommand> cmdList = new List<ScriptCommand>
            {
                new ScriptCommand("SetVar 0x8008 " + itemId),
                new ScriptCommand("SetVar 0x8009 " + quantity),
                new ScriptCommand("Jump Function_#1")
            };
            ScriptCommandContainer newEntry = new ScriptCommandContainer(uint.MaxValue, ScriptFile.ContainerTypes.Script, commandList: cmdList);
            itemScript.AllScripts.Insert(insertAt, newEntry);
            itemScript.RenumberContainers();
            itemScript.SaveToFileDefaultDir(RomInfo.ItemScriptFileNumber, showSuccessMessage: false);

            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.EventFiles });
            RemapItemReferences(n => n - ItemScrMin >= insertAt ? n + 1 : n);
        }

        /// <returns>null on success, or a user-facing error message if the entry is still in use.</returns>
        public static string RemoveEntry(int scriptIndex)
        {
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.Scripts });
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.EventFiles });

            int scriptNumber = ItemScrMin + scriptIndex;
            if (GetUsedScriptNumbers().Contains(scriptNumber))
            {
                return "An item event on the map still uses this entry. Change or delete that event first.";
            }

            ScriptFile itemScript = new ScriptFile(RomInfo.ItemScriptFileNumber);
            itemScript.AllScripts.RemoveAt(scriptIndex);
            itemScript.RenumberContainers();
            itemScript.SaveToFileDefaultDir(RomInfo.ItemScriptFileNumber, showSuccessMessage: false);

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

                foreach (Overworld ow in ev.Overworlds)
                {
                    bool isItem = ow.Type == (ushort)Overworld.OwType.ITEM || (ow.ScriptNumber >= ItemScrMin && ow.ScriptNumber <= ItemScrMax);
                    if (!isItem) continue;
                    int moved = Shifted(ow.ScriptNumber);
                    if (moved != ow.ScriptNumber) { ow.ScriptNumber = (ushort)moved; dirty = true; }
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
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.Scripts, RomInfo.DirNames.EventFiles });
                _script = new ScriptFile(RomInfo.ItemScriptFileNumber);
                for (int i = 0; i < _script.AllScripts.Count; i++) _origin.Add(i);
                _used = GetUsedScriptNumbers();
            }

            public bool HasChanges { get; private set; }

            /// <summary>The pending list at one moment, for an undo step. Only valid until the next Save.</summary>
            public sealed class Snapshot
            {
                internal List<(ScriptCommandContainer Container, List<ScriptCommand> Commands)> Scripts;
                internal List<int?> Origin;
                internal List<int> Removed;
                internal bool Changed;
            }

            public Snapshot Take() => new Snapshot
            {
                Scripts = _script.AllScripts.Select(c => (c, new List<ScriptCommand>(c.Commands))).ToList(),
                Origin = new List<int?>(_origin),
                Removed = new List<int>(_removed),
                Changed = HasChanges,
            };

            public void Restore(Snapshot snapshot)
            {
                _script.AllScripts.Clear();
                foreach ((ScriptCommandContainer container, List<ScriptCommand> commands) in snapshot.Scripts)
                {
                    container.Commands = new List<ScriptCommand>(commands);
                    _script.AllScripts.Add(container);
                }
                _origin.Clear();
                _origin.AddRange(snapshot.Origin);
                _removed.Clear();
                _removed.AddRange(snapshot.Removed);
                HasChanges = snapshot.Changed;
            }

            public List<Entry> Entries()
            {
                List<Entry> result = new List<Entry>();
                foreach ((int scriptIndex, int itemId, int quantity) e in DSUtils.GetGroundItemScriptEntries(_script))
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
                int insertAt = _script.AllScripts.FindLastIndex(DSUtils.IsGroundItemScriptEntry) + 1;
                List<ScriptCommand> cmdList = new List<ScriptCommand>
                {
                    new ScriptCommand("SetVar 0x8008 " + itemId),
                    new ScriptCommand("SetVar 0x8009 " + quantity),
                    new ScriptCommand("Jump Function_#1")
                };
                _script.AllScripts.Insert(insertAt, new ScriptCommandContainer(uint.MaxValue, ScriptFile.ContainerTypes.Script, commandList: cmdList));
                _origin.Insert(insertAt, null);
                HasChanges = true;
            }

            /// <summary>Changes an entry's item and quantity in place, so events using it keep their script number.</summary>
            public void Update(int scriptIndex, int itemId, int quantity)
            {
                if (scriptIndex < 0 || scriptIndex >= _script.AllScripts.Count) return;
                ScriptCommandContainer container = _script.AllScripts[scriptIndex];
                if (!DSUtils.IsGroundItemScriptEntry(container)) return;
                container.Commands[0] = SetVarLike(container.Commands[0], itemId);
                container.Commands[1] = SetVarLike(container.Commands[1], quantity);
                HasChanges = true;
            }

            // Rebuilt from text so the command's text form stays in step.
            private static ScriptCommand SetVarLike(ScriptCommand original, int value)
            {
                ushort variable = BitConverter.ToUInt16(original.CmdParams[0], 0);
                return new ScriptCommand($"SetVar 0x{variable:X4} {value}");
            }

            /// <returns>null on success, or why the entry can't go.</returns>
            public string Remove(int scriptIndex)
            {
                if (scriptIndex < 0 || scriptIndex >= _script.AllScripts.Count) return null;
                int? origin = _origin[scriptIndex];
                if (origin != null && _used.Contains(ItemScrMin + origin.Value))
                    return "An item event on the map still uses this entry. Change or delete that event first.";
                _script.AllScripts.RemoveAt(scriptIndex);
                _origin.RemoveAt(scriptIndex);
                if (origin != null) _removed.Add(ItemScrMin + origin.Value);
                HasChanges = true;
                return null;
            }

            /// <summary>False when the script file could not be written; the item events are then left alone.</summary>
            public bool Save()
            {
                if (!HasChanges) return true;
                // Every surviving entry's original script number to its new one. An add lands before any
                // later scripts in the file, so moves go both ways.
                Dictionary<int, int> moved = new Dictionary<int, int>();
                for (int i = 0; i < _origin.Count; i++)
                    if (_origin[i] != null) moved[ItemScrMin + _origin[i].Value] = ItemScrMin + i;
                List<int> removed = new List<int>(_removed);
                int Remap(int n)
                {
                    if (moved.TryGetValue(n, out int m)) return m;
                    // Removed or past the end: slide down past removed entries only.
                    int below = 0;
                    foreach (int r in removed) if (r < n) below++;
                    return n - below;
                }

                _script.RenumberContainers();
                if (!_script.SaveToFileDefaultDir(RomInfo.ItemScriptFileNumber, showSuccessMessage: false)) return false;
                RemapItemReferences(Remap);
                _removed.Clear();
                for (int i = 0; i < _origin.Count; i++) _origin[i] = i;
                _used.Clear();
                foreach (int n in GetUsedScriptNumbers()) _used.Add(n);
                HasChanges = false;
                return true;
            }
        }
    }
}
