using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;

namespace DSPRE
{
    /// <summary>
    /// Egg-move table reader (extracted from the Avalonia EggMoveEditorViewModel so core consumers
    /// like DocTool can use it). HGSS reads the eggMoves NARC; DP/Plat read overlay 5, with the
    /// "special" per-species file layout detected via the table magic.
    /// </summary>
    public static class EggMoveData
    {
        public const int OVERLAY_NUMBER = 5;
        public const int SPECIES_CONSTANT = 20000;

        /// <summary>Whether DP or Platinum use the expanded one-file-per-species layout, and its moves-per-species limit.</summary>
        /// <summary>hg-engine builds the egg moves from learnsets.json's EggMoves lists, which have no length limit.</summary>
        public static bool FromHgEngineSource => HgEngine.HgEngineProject.IsActive;

        public static (bool Expanded, int MaxMoves) Layout()
        {
            if (FromHgEngineSource) return (true, int.MaxValue);
            if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS) return (false, 0);
            using var reader = new BinaryReader(File.OpenRead(OverlayUtils.GetPath(OVERLAY_NUMBER)));
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
            if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS)
            {
                var path = Path.Combine(RomInfo.gameDirs[RomInfo.DirNames.eggMoves].unpackedDir, "0000");
                using var stream = File.OpenWrite(path);
                using var w = new BinaryWriter(stream);
                WriteTable(w, entries);
                if (stream.Position < stream.Length) stream.SetLength(stream.Position);
            }
            else if (expandedLayout)
            {
                string folder = RomInfo.gameDirs[RomInfo.DirNames.eggMoves].unpackedDir;
                Directory.CreateDirectory(folder);
                var hasFile = new HashSet<int>();
                foreach (var e in entries)
                {
                    using var w = new BinaryWriter(File.Create(Path.Combine(folder, e.speciesID.ToString("D4"))));
                    foreach (var m in e.moveIDs) w.Write(m);
                    w.Write((ushort)0xFFFF);
                    hasFile.Add(e.speciesID);
                }
                for (int i = 0; i < speciesCount; i++)
                {
                    if (hasFile.Contains(i)) continue;
                    using var w = new BinaryWriter(File.Create(Path.Combine(folder, i.ToString("D4"))));
                    w.Write((ushort)0xFFFF);
                }
            }
            else
            {
                using var stream = File.OpenWrite(OverlayUtils.GetPath(OVERLAY_NUMBER));
                using var w = new BinaryWriter(stream);
                stream.Seek(RomInfo.GetEggMoveTableOffset(), SeekOrigin.Begin);
                WriteTable(w, entries);
            }
        }

        // Only species whose list changed are rewritten; one taken out of the table gets an empty list.
        private static void WriteSource(IReadOnlyList<EggMoveEntry> entries)
        {
            string field = HgEngine.HgEngineLearnsets.EggMovesField;
            if (!HgEngine.HgEngineLearnsets.TryGetAllMoveNames(field, out var before, out string error)) throw new IOException(error);
            var now = new Dictionary<int, List<int>>();
            foreach (var e in entries) now[e.speciesID] = e.moveIDs.Select(m => (int)m).ToList();
            var changes = new Dictionary<int, IReadOnlyList<int>>();
            foreach (var (species, moves) in now)
                if (!before.TryGetValue(species, out var was) ? moves.Count > 0 : !was.SequenceEqual(moves)) changes[species] = moves;
            foreach (int species in before.Keys)
                if (!now.ContainsKey(species)) changes[species] = new List<int>();
            if (!HgEngine.HgEngineLearnsets.TrySaveMoveNames(field, changes, out error)) throw new IOException(error);
        }

        private static List<EggMoveEntry> ReadSource()
        {
            if (!HgEngine.HgEngineLearnsets.TryGetAllMoveNames(HgEngine.HgEngineLearnsets.EggMovesField, out var lists, out string error))
                throw new IOException(error);
            return lists.OrderBy(kv => kv.Key).Select(kv => new EggMoveEntry(kv.Key, kv.Value.Select(m => (ushort)m).ToList())).ToList();
        }

        private static void WriteTable(BinaryWriter w, IReadOnlyList<EggMoveEntry> entries)
        {
            foreach (var e in entries)
            {
                w.Write((ushort)(e.speciesID + SPECIES_CONSTANT));
                foreach (var m in e.moveIDs) w.Write(m);
            }
            w.Write((ushort)0xFFFF);
        }

        public static List<EggMoveEntry> ReadFromRom()
        {
            if (FromHgEngineSource) return ReadSource();
            const int overlayNum = OVERLAY_NUMBER;
            var result = new List<EggMoveEntry>();
            bool useSpecial = false;

            EndianBinaryReader reader = null;
            try
            {
                if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS)
                {
                    DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.eggMoves });
                    var path = Path.Combine(RomInfo.gameDirs[RomInfo.DirNames.eggMoves].unpackedDir, "0000");
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
                    DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.eggMoves });
                    string folder = RomInfo.gameDirs[RomInfo.DirNames.eggMoves].unpackedDir;
                    foreach (var file in Directory.GetFiles(folder))
                    {
                        if (!int.TryParse(Path.GetFileName(file), out int speciesID)) continue;
                        var moves = new List<ushort>();
                        using var r = new EndianBinaryReader(File.OpenRead(file), Endianness.LittleEndian);
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
                        if (read > SPECIES_CONSTANT)
                        {
                            result.Add(new EggMoveEntry(read - SPECIES_CONSTANT, new List<ushort>()));
                            idx++;
                        }
                        else if (idx >= 0)
                        {
                            var e = result[idx]; e.moveIDs.Add(read); result[idx] = e;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"EggMoveData.ReadFromRom failed: {ex.Message}");
            }
            finally
            {
                reader?.Close();
            }

            return result;
        }
    }
}
