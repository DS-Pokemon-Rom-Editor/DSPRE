﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE {
    internal class LearnsetData : RomFile {
        public static readonly int bitsMove = 9;
        public static readonly int bitsLevel = 7;
        public static readonly int VanillaLimit = 20;

        /// <summary>hg-engine-linked projects sync learnsets as u32 (level &lt;&lt; 16) | move entries.</summary>
        public static bool UsesWideEntries => HgEngineDomains.IsOwned(DirNames.learnsets);

        public readonly UniqueList<(byte level, ushort move)> list;
        public bool IsWide { get; }

        public ushort[] GetLearnsetAtLevel(int atLevel)
        {
            List<ushort> moves = new List<ushort>();
            
            foreach ((byte level, ushort move) elem in list) {

                if (elem.level > atLevel) {
                    continue; // the moves should be sorted by level so we can probably break here but better safe than sorry
                }

                if (elem.move == 0) {
                    continue; // skip empty moves
                }

                if (!moves.Contains(elem.move)) {
                    
                    if (moves.Count >= 4)
                    {
                        moves.RemoveAt(0);
                    }                    
                    moves.Add(elem.move); // add the new move to the end of the list
                }
            }

            ushort[] learnset = moves.ToArray();

            // Ensure we have exactly 4 moves, filling with 0 if necessary
            if (learnset.Length < 4) {
                Array.Resize(ref learnset, 4);
            }

            return learnset;
        }

        public LearnsetData(Stream stream) : this(stream, wide: false) { }

        public LearnsetData(Stream stream, bool wide) {
            IsWide = wide;

            if (wide) {
                int count = (int)(stream.Length / sizeof(uint));
                list = new UniqueList<(byte level, ushort move)>(Math.Max(0, count - 1));
                using (BinaryReader reader = new BinaryReader(stream)) {
                    for (int i = 0; i < count; i++) {
                        uint raw = reader.ReadUInt32();
                        if ((raw & 0xFFFF) == 0xFFFF) {
                            return;
                        }
                        // Gen 4 caps levels at 100, so the byte level field only loses unreachable values.
                        int lvWide = Math.Min((int)(raw >> 16), byte.MaxValue);
                        list.Add(((byte)lvWide, (ushort)(raw & 0xFFFF)));
                    }
                }
                return;
            }

            int numEntries = (int)(stream.Length / sizeof(ushort));
            list = new UniqueList<(byte level, ushort move)>(numEntries - 1);

            using (BinaryReader reader = new BinaryReader(stream)) {
                for (int i = 0; i < numEntries; i++) {
                    ushort entry = reader.ReadUInt16();
                    if (entry == 0xFFFF) {
                        return;
                    }

                    int maskMove = (1 << (bitsMove)) - 1;
                    int move = entry & maskMove;
                    entry >>= bitsMove;
                    
                    int maskLevel = (1 << (bitsLevel)) - 1;
                    int lv = entry & maskLevel;
                    
                    list.Add(((byte)lv, (ushort)move));
                }
            }
        }


        public LearnsetData(int ID) : this(ReadFile(ID, out bool wide), wide) { }

        // A folder synced before entries became u32 still holds u16 files until the next sync. A wide file
        // always ends in the u32 terminator FF FF 00 00; a u16 file ends in FF FF after a real entry.
        private static Stream ReadFile(int id, out bool wide)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(RomInfo.gameDirs[DirNames.learnsets].unpackedDir, id.ToString("D4")));
            int n = bytes.Length;
            wide = UsesWideEntries && n >= 4 && n % 4 == 0
                && bytes[n - 4] == 0xFF && bytes[n - 3] == 0xFF && bytes[n - 2] == 0 && bytes[n - 1] == 0;
            return new MemoryStream(bytes);
        }

        public override byte[] ToByteArray() {
            using (MemoryStream memoryStream = new MemoryStream()) {
                using (BinaryWriter writer = new BinaryWriter(memoryStream)) {
                    if (IsWide) {
                        foreach ((byte level, ushort move) elem in list) {
                            writer.Write(((uint)elem.level << 16) | elem.move);
                        }
                        writer.Write(HgEngineLearnsets.Terminator);
                        return memoryStream.ToArray();
                    }

                    foreach ((byte level, ushort move) elem in list) {
                        ushort move = (ushort)(elem.move & ((1 << bitsMove) - 1));
                        byte level = (byte)(elem.level & ((1 << bitsLevel) - 1));

                        ushort entry = (ushort)(move | (level << bitsMove));
                        writer.Write(entry);
                    }
                    // Add the termination entry
                    writer.Write((ushort)0xFFFF);
                    writer.Write((ushort)0x0000);
                }
                return memoryStream.ToArray();
            }
        }

        public void SaveToFileDefaultDir(int IDtoReplace, bool showSuccessMessage = true) {
            SaveToFileDefaultDir(DirNames.learnsets, IDtoReplace, showSuccessMessage);
        }
        public void SaveToFileExplorePath(string suggestedFileName, bool showSuccessMessage = true) {
            SaveToFileExplorePath("Gen IV Pokémon Learnset data", "bin", suggestedFileName, showSuccessMessage);
        }
    }
}