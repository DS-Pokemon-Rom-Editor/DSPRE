using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    public class LevelScriptFile
    {
        public int ID;
        public BindingList<LevelScriptTrigger> BufferSet = new BindingList<LevelScriptTrigger>();

        // The bytes parse_file read and the triggers they held: an unchanged file is written back as the game has it,
        // since retail files differ in padding the triggers alone don't describe.
        private byte[] _read;
        private List<string> _readTriggers;

        // How many screen triggers came before the variable table's entry; the games put it first.
        private int _tableEntryAt;

        public LevelScriptFile() { }

        public LevelScriptFile(int id)
        {
            this.ID = id;
            string path1 = Filesystem.Scripts;
            string path = Path.Combine(path1, this.ID.ToString("D4"));
            ParseFile(path);
        }

        public void ParseFile(string path)
        {
            _read = File.ReadAllBytes(path);
            _readTriggers = null;
            _tableEntryAt = 0;
            ParseTriggers(path);
            _readTriggers = TriggerKeys();
        }

        /// <summary>The triggers in order, for undo; <see cref="RestoreTriggers"/> takes it back.</summary>
        public byte[] TriggerState()
        {
            List<byte> bytes = new List<byte>();
            foreach (LevelScriptTrigger t in BufferSet)
            {
                VariableValueTrigger v = t as VariableValueTrigger;
                foreach (int n in new[] { t.TriggerType, t.ScriptTriggered, v?.VariableToWatch ?? 0, v?.ExpectedValue ?? 0 })
                    bytes.AddRange(BitConverter.GetBytes(n));
            }
            return bytes.ToArray();
        }

        public void RestoreTriggers(byte[] state)
        {
            BufferSet.Clear();
            for (int at = 0; at + 16 <= state.Length; at += 16)
            {
                int type = BitConverter.ToInt32(state, at), script = BitConverter.ToInt32(state, at + 4);
                BufferSet.Add(type == LevelScriptTrigger.VARIABLEVALUE
                    ? new VariableValueTrigger(script, BitConverter.ToInt32(state, at + 8), BitConverter.ToInt32(state, at + 12))
                    : new MapScreenLoadTrigger(type, script));
            }
        }

        private List<string> TriggerKeys()
        {
            List<string> keys = new List<string>();
            foreach (LevelScriptTrigger t in BufferSet)
                keys.Add(t is VariableValueTrigger v
                    ? $"v{v.ScriptTriggered}:{v.VariableToWatch}:{v.ExpectedValue}"
                    : $"s{t.TriggerType}:{t.ScriptTriggered}");
            return keys;
        }

        private void ParseTriggers(string path)
        {
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            /* Read script offsets from the header */
            bool isLevelScript = true; // until the script magic FD13 turns up

            using (BinaryReader br = new BinaryReader(fs))
            {
                List<int> scriptOffsets = new List<int>();
                try
                {
                    while (true)
                    {
                        long headerPos = br.BaseStream.Position;
                        uint checker = br.ReadUInt16();
                        br.BaseStream.Position -= 0x2;
                        uint value = br.ReadUInt32();

                        if (value == 0 && scriptOffsets.Count == 0)
                        {
                            isLevelScript = true;
                            break;
                        }

                        if (checker == 0xFD13)
                        {
                            br.BaseStream.Position -= 0x4;
                            isLevelScript = false;
                            break;
                        }

                        int offsetFromStart = (int)(value + br.BaseStream.Position); // Don't change order of addition
                        scriptOffsets.Add(offsetFromStart);
                    }
                }
                catch (EndOfStreamException)
                {
                    if (!isLevelScript)
                    {
                        AppMessages.Error("The level script couldn't be read.", "Level script error");
                    }
                }

                if (!isLevelScript)
                {
                    throw new InvalidDataException("This file isn't a level script.");
                }
                br.BaseStream.Position = 0;

                bool hasConditionalStructure = false;

                // Counts down to where the variable table must start, to catch damaged files.
                int conditionalStructureOffset = -1;

                int screenTriggers = 0;
                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    byte triggerType = br.ReadByte();
                    if (!LevelScriptTrigger.IsValidTriggerType(triggerType)) { break; }

                    if (hasConditionalStructure) { conditionalStructureOffset -= sizeof(byte); }

                    if (triggerType == LevelScriptTrigger.VARIABLEVALUE)
                    {
                        _tableEntryAt = screenTriggers;
                        hasConditionalStructure = true;
                        conditionalStructureOffset = (int)br.ReadUInt32();
                        continue;
                    }

                    uint scriptToTrigger = br.ReadUInt32();
                    BufferSet.Add(new MapScreenLoadTrigger(triggerType, (int)scriptToTrigger));
                    screenTriggers++;

                    if (hasConditionalStructure) { conditionalStructureOffset -= sizeof(UInt32); }
                }

                const int SMALLEST_TRIGGER_SIZE = 5;

                if (br.BaseStream.Position == 1 && fs.Length < SMALLEST_TRIGGER_SIZE
                    && (fs.Length < 3 || br.ReadUInt16() == 0))
                {
                    return;
                    throw new InvalidDataException("This level script does nothing.");
                }

                if (br.BaseStream.Position < SMALLEST_TRIGGER_SIZE)
                {
                    throw new InvalidDataException("This file is damaged or isn't a level script.");
                }

                if (!hasConditionalStructure)
                {
                    return;
                }

                if (conditionalStructureOffset != 1)
                {
                    AppLogger.Error($"LevelScriptFile: conditionalStructureOffset is {conditionalStructureOffset}, expected 1.");
                    throw new InvalidDataException("This level script's variable triggers are damaged.");
                }

                while (true)
                {
                    int variableID = br.ReadUInt16();
                    if (variableID <= 0) { break; }

                    int varExpectedValue = br.ReadUInt16();
                    int scriptToTrigger = br.ReadUInt16();
                    BufferSet.Add(new VariableValueTrigger(scriptToTrigger, variableID, varExpectedValue));
                }
            }
        }

        /// <summary>Why the game cannot run this level script as it stands, or null.</summary>
        public string Problem()
        {
            int n = 0;
            foreach (LevelScriptTrigger t in BufferSet)
            {
                n++;
                if (t is VariableValueTrigger v && v.Problem() is string why)
                    return $"Trigger {n} {why}.";
            }
            return null;
        }

        /// <summary>Writes script file <paramref name="id"/> and keeps its Rotom source in step.</summary>
        public long SaveToFileDefaultDir(int id, bool word_alignment_padding = false)
        {
            string path = Filesystem.GetScriptPath(id);
            byte[] bytes = ToBytes(word_alignment_padding);
            // An identical file is left alone, so its Rotom source isn't regenerated for nothing.
            if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) return bytes.Length;
            ArchiveMembers.Put(RomInfo.DirNames.Scripts, id, bytes);
            _ = ScriptSourceSync.BinaryWrittenAsync(id);
            return bytes.Length;
        }

        public long WriteFile(string path, bool word_alignment_padding = false)
        {
            byte[] bytes = ToBytes(word_alignment_padding);
            // Create truncates: a shorter file written over a longer one must not keep its tail.
            ArchiveMembers.Write(path, bytes);
            return bytes.Length;
        }

        /// <summary>
        /// The file as the game reads it: one entry per screen trigger, the variable table's entry where it was read
        /// (first in a new file), a 0 byte, then the table ended by a 0 word. Unchanged triggers give the bytes read.
        /// </summary>
        public byte[] ToBytes(bool word_alignment_padding = false)
        {
            if (_read != null && _readTriggers != null && TriggerKeys().SequenceEqual(_readTriggers)) return (byte[])_read.Clone();

            List<MapScreenLoadTrigger> screen = new List<MapScreenLoadTrigger>();
            List<VariableValueTrigger> table = new List<VariableValueTrigger>();
            foreach (LevelScriptTrigger item in BufferSet)
            {
                if (item is VariableValueTrigger v) table.Add(v);
                else if (item is MapScreenLoadTrigger m) screen.Add(m);
            }

            using MemoryStream ms = new MemoryStream();
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                int tableAt = Math.Clamp(_tableEntryAt, 0, screen.Count);
                for (int i = 0; i <= screen.Count; i++)
                {
                    if (i == tableAt && table.Count > 0)
                    {
                        bw.Write((byte)LevelScriptTrigger.VARIABLEVALUE);
                        // Counted from the end of this word: the entries after it, then the 0 byte.
                        bw.Write((UInt32)((screen.Count - tableAt) * 5 + 1));
                    }
                    if (i == screen.Count) break;
                    bw.Write((byte)screen[i].TriggerType);
                    bw.Write((UInt32)screen[i].ScriptTriggered);
                }
                bw.Write((byte)0);

                if (table.Count > 0)
                {
                    foreach (VariableValueTrigger item in table)
                    {
                        bw.Write((UInt16)item.VariableToWatch);
                        bw.Write((UInt16)item.ExpectedValue);
                        bw.Write((UInt16)item.ScriptTriggered);
                    }
                    bw.Write((UInt16)0);
                }

                if (word_alignment_padding)
                {
                    while (bw.BaseStream.Position % 4 != 0) bw.Write((byte)0);
                }
            }
            return ms.ToArray();
        }
    }
}
