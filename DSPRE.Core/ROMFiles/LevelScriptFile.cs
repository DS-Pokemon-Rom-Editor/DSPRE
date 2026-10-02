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
        public BindingList<LevelScriptTrigger> bufferSet = new BindingList<LevelScriptTrigger>();

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
            string path1 = Filesystem.scripts;
            string path = Path.Combine(path1, this.ID.ToString("D4"));
            parse_file(path);
        }

        public void parse_file(string path)
        {
            _read = File.ReadAllBytes(path);
            _readTriggers = null;
            _tableEntryAt = 0;
            parse_triggers(path);
            _readTriggers = TriggerKeys();
        }

        /// <summary>The triggers in order, for undo; <see cref="RestoreTriggers"/> takes it back.</summary>
        public byte[] TriggerState()
        {
            var bytes = new List<byte>();
            foreach (LevelScriptTrigger t in bufferSet)
            {
                var v = t as VariableValueTrigger;
                foreach (int n in new[] { t.triggerType, t.scriptTriggered, v?.variableToWatch ?? 0, v?.expectedValue ?? 0 })
                    bytes.AddRange(BitConverter.GetBytes(n));
            }
            return bytes.ToArray();
        }

        public void RestoreTriggers(byte[] state)
        {
            bufferSet.Clear();
            for (int at = 0; at + 16 <= state.Length; at += 16)
            {
                int type = BitConverter.ToInt32(state, at), script = BitConverter.ToInt32(state, at + 4);
                bufferSet.Add(type == LevelScriptTrigger.VARIABLEVALUE
                    ? new VariableValueTrigger(script, BitConverter.ToInt32(state, at + 8), BitConverter.ToInt32(state, at + 12))
                    : new MapScreenLoadTrigger(type, script));
            }
        }

        private List<string> TriggerKeys()
        {
            var keys = new List<string>();
            foreach (LevelScriptTrigger t in bufferSet)
                keys.Add(t is VariableValueTrigger v
                    ? $"v{v.scriptTriggered}:{v.variableToWatch}:{v.expectedValue}"
                    : $"s{t.triggerType}:{t.scriptTriggered}");
            return keys;
        }

        private void parse_triggers(string path)
        {
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            // Basic implementation to check if the file is a level script or not from ScriptFile.cs
            /* Read script offsets from the header */
            bool isLevelScript = true; // Is Level Script as long as magic number FD13 doesn't exist

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
                        AppMessages.Error("Script File couldn't be read correctly.", "Unexpected EOF");
                    }
                }

                if (!isLevelScript)
                {
                    throw new InvalidDataException("The file you attempted to load is not a Level Script file.");
                }
                br.BaseStream.Position = 0;

                bool hasConditionalStructure = false;

                //conditionalStructureOffset is used to ensure the structure of the file is correct
                int conditionalStructureOffset = -1;

                int screenTriggers = 0;
                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    //first byte is the script type
                    //if not a valid script type, break loop
                    byte triggerType = br.ReadByte();
                    if (!LevelScriptTrigger.IsValidTriggerType(triggerType)) { break; }

                    //subtract triggerType length from conditionalStructureOffset
                    if (hasConditionalStructure) { conditionalStructureOffset -= sizeof(byte); }

                    //if trigger type is a variable value, that doesn't immediately mean we're processing that trigger
                    //the trigger data is processed last if it is there
                    if (triggerType == LevelScriptTrigger.VARIABLEVALUE)
                    {
                        _tableEntryAt = screenTriggers;
                        hasConditionalStructure = true;
                        conditionalStructureOffset = (int)br.ReadUInt32();
                        continue;
                    }

                    //map screen load trigger doesn't have a value or variable
                    uint scriptToTrigger = br.ReadUInt32();
                    bufferSet.Add(new MapScreenLoadTrigger(triggerType, (int)scriptToTrigger));
                    screenTriggers++;

                    //subtract scriptToTrigger length from conditionalStructureOffset
                    if (hasConditionalStructure) { conditionalStructureOffset -= sizeof(UInt32); }
                }

                //the earliest position a trigger can be
                const int SMALLEST_TRIGGER_SIZE = 5;

                //if triggerType is invalid
                //and next uint16 == 0
                //and the file stream length is shorter than the earliest position a trigger can be
                if (br.BaseStream.Position == 1 && fs.Length < SMALLEST_TRIGGER_SIZE
                    && (fs.Length < 3 || br.ReadUInt16() == 0))
                {
                    return;
                    throw new InvalidDataException("This level script does nothing."); // "Interesting..."
                }

                //br.BaseStream.Position == 3
                //triggerType is valid,
                //stream position is earlier than the first possible trigger, or
                //there is no start script condition specified
                if (br.BaseStream.Position < SMALLEST_TRIGGER_SIZE)
                {
                    throw new InvalidDataException("Parser failure: The input file you attempted to load is either malformed or not a Level Script file.");
                }

                //there are no instances of a variable value trigger
                if (!hasConditionalStructure)
                {
                    return;
                }

                //if there's a variable value trigger but the offset is incorrect, the file is corrupt
                if (conditionalStructureOffset != 1)
                {
                    throw new InvalidDataException($"Field error: The Level Script file you attempted to load is broken. {conditionalStructureOffset}");
                }

                //get the variable value trigger parts
                while (true)
                {
                    //there are no variables below 1
                    int variableID = br.ReadUInt16();
                    if (variableID <= 0) { break; }

                    int varExpectedValue = br.ReadUInt16();
                    int scriptToTrigger = br.ReadUInt16();
                    bufferSet.Add(new VariableValueTrigger(scriptToTrigger, variableID, varExpectedValue));
                }
            }
        }

        /// <summary>Why the game cannot run this level script as it stands, or null.</summary>
        public string Problem()
        {
            int n = 0;
            foreach (LevelScriptTrigger t in bufferSet)
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
            File.WriteAllBytes(path, bytes);
            _ = ScriptSourceSync.BinaryWritten(id);
            return bytes.Length;
        }

        public long write_file(string path, bool word_alignment_padding = false)
        {
            byte[] bytes = ToBytes(word_alignment_padding);
            // Create truncates: a shorter file written over a longer one must not keep its tail.
            File.WriteAllBytes(path, bytes);
            return bytes.Length;
        }

        /// <summary>
        /// The file as the game reads it: one entry per screen trigger, the variable table's entry where it was read
        /// (first in a new file), a 0 byte, then the table ended by a 0 word. Unchanged triggers give the bytes read.
        /// </summary>
        public byte[] ToBytes(bool word_alignment_padding = false)
        {
            if (_read != null && _readTriggers != null && TriggerKeys().SequenceEqual(_readTriggers)) return (byte[])_read.Clone();

            var screen = new List<MapScreenLoadTrigger>();
            var table = new List<VariableValueTrigger>();
            foreach (LevelScriptTrigger item in bufferSet)
            {
                if (item is VariableValueTrigger v) table.Add(v);
                else if (item is MapScreenLoadTrigger m) screen.Add(m);
            }

            using var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms))
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
                    bw.Write((byte)screen[i].triggerType);
                    bw.Write((UInt32)screen[i].scriptTriggered);
                }
                bw.Write((byte)0);

                if (table.Count > 0)
                {
                    foreach (VariableValueTrigger item in table)
                    {
                        bw.Write((UInt16)item.variableToWatch);
                        bw.Write((UInt16)item.expectedValue);
                        bw.Write((UInt16)item.scriptTriggered);
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
