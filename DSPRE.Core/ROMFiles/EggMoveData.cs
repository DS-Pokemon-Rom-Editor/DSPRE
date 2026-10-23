using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;

namespace DSPRE
{
    /// <summary>
    /// The egg move tables: HGSS reads the eggMoves NARC; DP and Platinum read overlay 5, or one file per species when
    /// the table magic says so.
    /// </summary>
    public static class EggMoveData
    {
        public const int OverlayNumber = 5;
        public const int SpeciesConstant = 20000;

        /// <summary>Whether DP or Platinum use the expanded one-file-per-species layout, and its moves-per-species limit.</summary>
        /// <summary>hg-engine builds the egg moves from learnsets.json's EggMoves lists, which have no length limit.</summary>
        public static bool FromHgEngineSource => HgEngine.HgEngineProject.IsActive;

        public static (bool Expanded, int MaxMoves) Layout()
        {
            if (FromHgEngineSource) return (true, int.MaxValue);
            if (RomInfo.GameFamily == RomInfo.GameFamilies.HGSS) return (false, 0);
            using BinaryReader reader = new BinaryReader(File.OpenRead(OverlayUtils.GetPath(OverlayNumber)));
            reader.BaseStream.Seek(RomInfo.GetEggMoveTableOffset(), SeekOrigin.Begin);
            int magic = reader.ReadInt32(), maxMoves = reader.ReadInt32();
            return magic == ExpandedMagic ? (true, maxMoves) : (false, 0);
        }

        private const int ExpandedMagic = 4671301;

        /// <summary>
        /// Writes the table back where it was read from: HGSS's archive member, DP and Platinum's overlay table, or one
        /// file per species when the expanded layout is in use (species without moves get an empty list).
        /// </summary>
        public static void Write(IReadOnlyList<EggMoveEntry> entries, bool expandedLayout, int speciesCount)
        {
            if (FromHgEngineSource) { WriteSource(entries); return; }
            if (RomInfo.GameFamily == RomInfo.GameFamilies.HGSS)
            {
                ArchiveMembers.Put(RomInfo.DirNames.EggMoves, 0, TableBytes(entries));
            }
            else if (expandedLayout)
            {
                Directory.CreateDirectory(RomInfo.GameDirs[RomInfo.DirNames.EggMoves].unpackedDir);
                Dictionary<int, IReadOnlyList<ushort>> moves = new Dictionary<int, IReadOnlyList<ushort>>();
                foreach (EggMoveEntry e in entries) moves[e.SpeciesID] = e.MoveIDs;
                for (int i = 0; i < Math.Max(speciesCount, moves.Count == 0 ? 0 : moves.Keys.Max() + 1); i++)
                {
                    IReadOnlyList<ushort> list = moves.TryGetValue(i, out IReadOnlyList<ushort> m) ? m : Array.Empty<ushort>();
                    ArchiveMembers.Put(RomInfo.DirNames.EggMoves, i, list.Append((ushort)0xFFFF).SelectMany(BitConverter.GetBytes).ToArray());
                }
            }
            else
            {
                DSUtils.WriteToFile(OverlayUtils.GetPath(OverlayNumber), TableBytes(entries), (uint)RomInfo.GetEggMoveTableOffset());
            }
        }

        private static byte[] TableBytes(IReadOnlyList<EggMoveEntry> entries)
        {
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter w = new BinaryWriter(stream);
            WriteTable(w, entries);
            w.Flush();
            return stream.ToArray();
        }

        // Only species whose list changed are rewritten; one taken out of the table gets an empty list.
        private static void WriteSource(IReadOnlyList<EggMoveEntry> entries)
        {
            string field = HgEngine.HgEngineLearnsets.EggMovesField;
            if (!HgEngine.HgEngineLearnsets.TryGetAllMoveNames(field, out Dictionary<int, List<int>> before, out string error)) throw new IOException(error);
            Dictionary<int, List<int>> now = new Dictionary<int, List<int>>();
            foreach (EggMoveEntry e in entries) now[e.SpeciesID] = e.MoveIDs.Select(m => (int)m).ToList();
            Dictionary<int, IReadOnlyList<int>> changes = new Dictionary<int, IReadOnlyList<int>>();
            foreach ((int species, List<int> moves) in now)
                if (!before.TryGetValue(species, out List<int> was) ? moves.Count > 0 : !was.SequenceEqual(moves)) changes[species] = moves;
            foreach (int species in before.Keys)
                if (!now.ContainsKey(species)) changes[species] = new List<int>();
            if (!HgEngine.HgEngineLearnsets.TrySaveMoveNames(field, changes, out error)) throw new IOException(error);
        }

        private static List<EggMoveEntry> ReadSource()
        {
            if (!HgEngine.HgEngineLearnsets.TryGetAllMoveNames(HgEngine.HgEngineLearnsets.EggMovesField, out Dictionary<int, List<int>> lists, out string error))
                throw new IOException(error);
            return lists.OrderBy(kv => kv.Key).Select(kv => new EggMoveEntry(kv.Key, kv.Value.Select(m => (ushort)m).ToList())).ToList();
        }

        private static void WriteTable(BinaryWriter w, IReadOnlyList<EggMoveEntry> entries)
        {
            foreach (EggMoveEntry e in entries)
            {
                w.Write((ushort)(e.SpeciesID + SpeciesConstant));
                foreach (ushort m in e.MoveIDs) w.Write(m);
            }
            w.Write((ushort)0xFFFF);
        }

        public static List<EggMoveEntry> ReadFromRom()
        {
            if (FromHgEngineSource) return ReadSource();
            const int overlayNum = OverlayNumber;
            List<EggMoveEntry> result = new List<EggMoveEntry>();
            bool useSpecial = false;

            EndianBinaryReader reader = null;
            try
            {
                if (RomInfo.GameFamily == RomInfo.GameFamilies.HGSS)
                {
                    DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.EggMoves });
                    string path = Path.Combine(RomInfo.GameDirs[RomInfo.DirNames.EggMoves].unpackedDir, "0000");
                    reader = new EndianBinaryReader(File.OpenRead(path), Endianness.LittleEndian);
                }
                else
                {
                    int offset = RomInfo.GetEggMoveTableOffset();
                    reader = new EndianBinaryReader(File.OpenRead(OverlayUtils.GetPath(overlayNum)), Endianness.LittleEndian);
                    reader.BaseStream.Seek(offset, SeekOrigin.Begin);
                    int magic    = reader.ReadInt32();
                    int maxMoves = reader.ReadInt32();
                    reader.BaseStream.Seek(-8, SeekOrigin.Current);
                    if (magic == ExpandedMagic) useSpecial = true;
                }

                if (useSpecial)
                {
                    reader?.Close();
                    DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.EggMoves });
                    string folder = RomInfo.GameDirs[RomInfo.DirNames.EggMoves].unpackedDir;
                    foreach (string file in Directory.GetFiles(folder))
                    {
                        if (!int.TryParse(Path.GetFileName(file), out int speciesID)) continue;
                        List<ushort> moves = new List<ushort>();
                        using EndianBinaryReader r = new EndianBinaryReader(File.OpenRead(file), Endianness.LittleEndian);
                        while (r.BaseStream.Position < r.BaseStream.Length)
                        {
                            ushort id = r.ReadUInt16();
                            if (id == 0xFFFF) break;
                            moves.Add(id);
                        }
                        result.Add(new EggMoveEntry(speciesID, moves));
                    }
                }
                else
                {
                    int idx = -1;
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        ushort read = reader.ReadUInt16();
                        if (read == 0xFFFF) break;
                        if (read > SpeciesConstant)
                        {
                            result.Add(new EggMoveEntry(read - SpeciesConstant, new List<ushort>()));
                            idx++;
                        }
                        else if (idx >= 0)
                        {
                            EggMoveEntry e = result[idx]; e.MoveIDs.Add(read); result[idx] = e;
                        }
                    }
                }
            }
            // A failed read throws: callers saving back an empty table would wipe every list.
            finally
            {
                reader?.Close();
            }

            return result;
        }
    }
}
