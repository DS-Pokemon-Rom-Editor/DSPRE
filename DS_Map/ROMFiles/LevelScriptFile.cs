using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;

namespace DSPRE.ROMFiles
{
    public class LevelScriptFile
    {
        public int ID;
        public BindingList<LevelScriptTrigger> bufferSet = new BindingList<LevelScriptTrigger>();

        // The game loads the level script into a 0x100-byte buffer and asserts the file is smaller.
        public const int MaxGameFileSize = 0x100 - 1;

        public int GetBinarySize()
        {
            int mapTriggers = 0, varTriggers = 0;
            foreach (LevelScriptTrigger t in new HashSet<LevelScriptTrigger>(bufferSet))
            {
                if (t is VariableValueTrigger) varTriggers++;
                else mapTriggers++;
            }
            int size = mapTriggers * 5 + 2;
            if (varTriggers > 0) size += 5 + 1 + varTriggers * 6;
            return size;
        }

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
            FileStream fs = new FileStream(path, FileMode.Open);
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
                        MessageBox.Show("Script File couldn't be read correctly.", "Unexpected EOF", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                if (!isLevelScript)
                {
                    throw new InvalidDataException("The file you attempted to load is not a Level Script file.");
                }
                br.BaseStream.Position = 0;

                // The game stops reading triggers at the first 0x00 type byte, so a file starting
                // with 0x00 is an empty level script whatever follows (padding, or dead triggers
                // like "00 00000000 02 04000000" seen in some HGE files).
                if (fs.Length == 0 || br.ReadByte() == 0)
                {
                    if (!IsAllZero(fs))
                    {
                        AppLogger.Warn($"Level script {ID}: file starts with 0x00, so the game treats it as empty. The bytes after it are ignored and will be dropped on save.");
                    }
                    return;
                }
                br.BaseStream.Position = 0;

                bool hasConditionalStructure = false;

                //conditionalStructureOffset is used to ensure the structure of the file is correct
                int conditionalStructureOffset = -1;

                while (true)
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
                        hasConditionalStructure = true;
                        conditionalStructureOffset = (int)br.ReadUInt32();
                        continue;
                    }

                    //map screen load trigger doesn't have a value or variable
                    uint scriptToTrigger = br.ReadUInt32();
                    bufferSet.Add(new MapScreenLoadTrigger(triggerType, (int)scriptToTrigger));

                    //subtract scriptToTrigger length from conditionalStructureOffset
                    if (hasConditionalStructure) { conditionalStructureOffset -= sizeof(UInt32); }
                }

                //the earliest position a trigger can be
                const int SMALLEST_TRIGGER_SIZE = 5;

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

        private static bool IsAllZero(Stream stream)
        {
            long start = stream.Position;
            int b;
            while ((b = stream.ReadByte()) != -1)
            {
                if (b != 0)
                {
                    stream.Position = start;
                    return false;
                }
            }
            stream.Position = start;
            return true;
        }

        public long write_file(string path, bool word_alignment_padding = false)
        {
            // FileMode.Create truncates: OpenOrCreate would leave the old file's tail behind
            // when the new level script is shorter, corrupting it.
            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                HashSet<MapScreenLoadTrigger> mapScreenLoadTriggers = new HashSet<MapScreenLoadTrigger>();
                HashSet<VariableValueTrigger> variableValueTriggers = new HashSet<VariableValueTrigger>();

                foreach (LevelScriptTrigger item in bufferSet)
                {
                    if (item is VariableValueTrigger variableValueTrigger)
                    {
                        variableValueTriggers.Add(variableValueTrigger);
                    }
                    else if (item is MapScreenLoadTrigger mapScreenLoadTrigger)
                    {
                        mapScreenLoadTriggers.Add(mapScreenLoadTrigger);
                    }
                }

                foreach (MapScreenLoadTrigger item in mapScreenLoadTriggers)
                {
                    bw.Write((byte)item.triggerType);
                    bw.Write((UInt32)item.scriptTriggered);
                }

                if (variableValueTriggers.Count > 0)
                {
                    bw.Write((byte)LevelScriptTrigger.VARIABLEVALUE);
                    bw.Write((UInt32)1);
                    bw.Write((byte)0);
                    foreach (VariableValueTrigger item in variableValueTriggers)
                    {
                        bw.Write((UInt16)item.variableToWatch);
                        bw.Write((UInt16)item.expectedValue);
                        bw.Write((UInt16)item.scriptTriggered);
                    }
                }

                bw.Write((UInt16)0);

                if (word_alignment_padding)
                {
                    long missing_bytes = bw.BaseStream.Position % 4;
                    for (int i = 0; i < 4 - missing_bytes; i++)
                    {
                        bw.Write((byte)0);
                    }
                }

                return bw.BaseStream.Position;
            }
        }
    }
}
