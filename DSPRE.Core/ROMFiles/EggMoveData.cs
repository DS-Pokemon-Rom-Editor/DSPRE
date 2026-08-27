using System;
using System.Collections.Generic;
using System.IO;
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

        public static List<EggMoveEntry> ReadFromRom()
        {
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
                    if (magic == 4671301) useSpecial = true;
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
