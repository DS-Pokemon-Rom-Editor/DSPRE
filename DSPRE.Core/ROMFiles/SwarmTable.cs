using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Where a swarm can happen; HGSS rows add an encounter method. The row count lives only in the two
    /// `movs r1, #count` sites that pick the day's swarm, and a table that outgrows its space moves to the ARM9 expansion.
    /// </summary>
    public sealed class SwarmTable
    {
        public sealed class Row
        {
            public ushort Header { get; set; }
            public ushort Method { get; set; }
        }

        public const string Marker = "SWARMTABLEX1";
        public static readonly string[] MethodNames = { "Walking", "Surfing", "Fishing" };

        public List<Row> Rows { get; } = new List<Row>();
        public bool HasMethod => gameFamily == GameFamilies.HGSS;
        public string Where { get; private set; } = "";
        /// <summary>hg-engine: the rows are src/swarms.c's sSwarmMapLUT.</summary>
        public bool FromSource { get; private set; }
        public bool InExpansion => _path == Filesystem.expArmPath;
        /// <summary>Rows that fit where the table is now without moving it.</summary>
        public int Capacity { get; private set; }

        private readonly SwarmSites _sites;
        private string _path;
        private int _offset;
        private int _blockStart = -1, _blockLength;

        private SwarmTable(SwarmSites sites) { _sites = sites; }

        private string CodePath => _sites.Overlay < 0 ? arm9Path : OverlayUtils.GetPath(_sites.Overlay);
        private uint CodeBase => _sites.Overlay < 0 ? ARM9.address : OverlayUtils.OverlayTable.GetRAMAddress(_sites.Overlay);

        public static string WhyNot()
        {
            if (HgEngine.HgEngineProject.IsActive)
                return HgEngine.HgEngineSwarms.WhyNot() ?? (HgEngine.HgEngineSwarms.TryRead(out _, out string error) ? null : error);
            var sites = SwarmCodeSites;
            if (sites == null) return "Only US HeartGold, Platinum (Rev 1) and Diamond are supported.";
            if (sites.Overlay < 0 && !IsDsRomProject && ARM9.CheckCompressionMark()) return "arm9 is still compressed. Convert this project to ds-rom format first.";
            try { Load(); }
            catch (Exception e) when (e is InvalidDataException || e is IOException || e is ArgumentException) { return e.Message; }
            return null;
        }

        private byte[] ReadCode()
        {
            if (_sites.Overlay >= 0 && OverlayUtils.IsCompressed(_sites.Overlay)) OverlayUtils.Decompress(_sites.Overlay);
            return File.ReadAllBytes(CodePath);
        }

        public static SwarmTable Load()
        {
            if (HgEngine.HgEngineProject.IsActive)
            {
                if (!HgEngine.HgEngineSwarms.TryRead(out var rows, out string error)) throw new InvalidDataException(error);
                var source = new SwarmTable(null) { FromSource = true, Capacity = 255, Where = "in " + HgEngine.HgEngineSwarms.RelPath };
                source.Rows.AddRange(rows.Select(r => new Row { Header = r.Header, Method = r.Method }));
                return source;
            }
            var sites = SwarmCodeSites ?? throw new InvalidOperationException("This game version isn't supported yet.");
            var table = new SwarmTable(sites);
            byte[] code = table.ReadCode();

            var targets = sites.Literals.Select(o => BitConverter.ToUInt32(code, o)).Distinct().ToList();
            if (targets.Count != 1) throw new InvalidDataException("The swarm code points at more than one table; DSPRE can't tell which is used.");
            uint ram = targets[0];
            if (sites.LiteralsPlus2.Any(o => BitConverter.ToUInt32(code, o) != ram + 2))
                throw new InvalidDataException("The swarm code's table pointers don't agree with each other.");
            var counts = sites.CountSites.Select(o => (code[o], code[o + 1])).Distinct().ToList();
            if (counts.Count != 1 || counts[0].Item2 != 0x21) throw new InvalidDataException("The swarm code's row count doesn't look like the game's; it may have been patched.");
            int count = counts[0].Item1;

            uint codeBase = table.CodeBase;
            if (ram >= codeBase && (ulong)(ram - codeBase) + (ulong)(count * sites.RowSize) <= (ulong)code.Length)
            {
                table._path = table.CodePath; table._offset = (int)(ram - codeBase);
                bool vanilla = table._offset == sites.Table;
                table.Capacity = vanilla ? sites.Rows : count;
                table.Where = vanilla ? "where the game keeps it" : "moved by a patch";
            }
            else if (ram >= synthOverlayLoadAddress && File.Exists(Filesystem.expArmPath)
                     && ram - synthOverlayLoadAddress + (ulong)(count * sites.RowSize) <= (ulong)new FileInfo(Filesystem.expArmPath).Length)
            {
                byte[] synth = File.ReadAllBytes(Filesystem.expArmPath);
                table._path = Filesystem.expArmPath; table._offset = (int)(ram - synthOverlayLoadAddress);
                var block = SyntheticOverlaySpace.Blocks(synth, Marker).FirstOrDefault(b => table._offset == b.Start + SyntheticOverlaySpace.HeaderSize);
                if (block.End > 0)
                {
                    table._blockStart = (int)block.Start; table._blockLength = (int)(block.End - block.Start);
                    table.Capacity = (table._blockLength - SyntheticOverlaySpace.HeaderSize) / sites.RowSize;
                }
                else table.Capacity = count;
                table.Where = "in the expanded ARM9 area";
            }
            else throw new InvalidDataException($"The swarm table was moved to 0x{ram:X8}, which DSPRE can't follow.");

            byte[] data = File.ReadAllBytes(table._path);
            if (table._offset + count * sites.RowSize > data.Length) throw new InvalidDataException("The swarm table runs past the end of its file.");
            for (int i = 0; i < count; i++)
            {
                int o = table._offset + i * sites.RowSize;
                table.Rows.Add(table.HasMethod
                    ? new Row { Header = BitConverter.ToUInt16(data, o), Method = BitConverter.ToUInt16(data, o + 2) }
                    : new Row { Header = (ushort)BitConverter.ToUInt32(data, o) });
            }
            return table;
        }

        private byte[] RowBytes()
        {
            var bytes = new byte[Rows.Count * (_sites?.RowSize ?? 4)];
            for (int i = 0; i < Rows.Count; i++)
            {
                if (HasMethod)
                {
                    BitConverter.GetBytes(Rows[i].Header).CopyTo(bytes, i * 4);
                    BitConverter.GetBytes(Rows[i].Method).CopyTo(bytes, i * 4 + 2);
                }
                else BitConverter.GetBytes((uint)Rows[i].Header).CopyTo(bytes, i * 4);
            }
            return bytes;
        }

        /// <summary>The table outgrew its room in the game and the ARM9 expansion isn't there to take it.</summary>
        public bool NeedsExpansion => !FromSource && !FitsWhereItIs && !InExpansion && !SyntheticOverlaySpace.Available();

        /// <summary>Why the table can't be saved, or null.</summary>
        /// <param name="hasSwarmSpecies">HGSS: whether the row's method has a swarm species in its header's encounter file.</param>
        public string Problem(int headerCount, Func<ushort, bool> hasEncounters, Func<Row, bool> hasSwarmSpecies = null)
        {
            if (Rows.Count == 0) return "The swarm table needs at least one row.";
            if (Rows.Count > 255) return "The game can pick from up to 255 swarm rows.";
            if (NeedsExpansion)
                return $"The table holds {Capacity} rows until the ARM9 expansion is applied in the ROM Patch Toolbox.";
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].Header >= headerCount) return $"Row {i + 1} points at a header that doesn't exist.";
                if (hasEncounters != null && !hasEncounters(Rows[i].Header)) return $"Row {i + 1}: header {Rows[i].Header} has no wild encounters, so its swarm would have no Pokémon.";
                if (HasMethod && Rows[i].Method > 2) return $"Row {i + 1} has an encounter method the game doesn't know.";
                // HGSS asserts the picked swarm species isn't 0.
                if (HasMethod && hasSwarmSpecies != null && !hasSwarmSpecies(Rows[i]))
                    return $"Row {i + 1}: header {Rows[i].Header} has no {MethodNames[Rows[i].Method].ToLowerInvariant()} swarm Pokémon.";
            }
            return null;
        }

        public bool FitsWhereItIs => Rows.Count <= Capacity;

        public void Save(int headerCount, Func<ushort, bool> hasEncounters, Func<Row, bool> hasSwarmSpecies = null)
        {
            if (Problem(headerCount, hasEncounters, hasSwarmSpecies) is string p) throw new InvalidOperationException(p);
            if (FromSource)
            {
                if (!HgEngine.HgEngineSwarms.TryWrite(Rows.Select(r => (r.Header, r.Method)).ToList(), out string error))
                    throw new InvalidOperationException(error);
                return;
            }
            byte[] code = ReadCode(), codeBefore = (byte[])code.Clone();
            byte[] rows = RowBytes();

            if (FitsWhereItIs)
            {
                if (_path == CodePath)
                {
                    Array.Clear(code, _offset, Capacity * _sites.RowSize);
                    rows.CopyTo(code, _offset);
                }
                else
                {
                    byte[] synth = File.ReadAllBytes(Filesystem.expArmPath), synthOld = (byte[])synth.Clone();
                    Array.Clear(synth, _offset, Capacity * _sites.RowSize);
                    rows.CopyTo(synth, _offset);
                    if (_blockStart >= 0) BitConverter.GetBytes((uint)Rows.Count).CopyTo(synth, _blockStart + 0x14);
                    foreach (int o in _sites.CountSites) code[o] = (byte)Rows.Count;
                    try
                    {
                        File.WriteAllBytes(Filesystem.expArmPath, synth);
                        File.WriteAllBytes(CodePath, code);
                    }
                    catch
                    {
                        File.WriteAllBytes(Filesystem.expArmPath, synthOld);
                        File.WriteAllBytes(CodePath, codeBefore);
                        throw;
                    }
                    return;
                }
                foreach (int o in _sites.CountSites) code[o] = (byte)Rows.Count;
                File.WriteAllBytes(CodePath, code);
                return;
            }

            if (!SyntheticOverlaySpace.Available())
                throw new InvalidOperationException($"The swarm table holds {Capacity} rows where it is. Apply the ARM9 expansion in the ROM Patch Toolbox to go past that.");
            byte[] synthNow = File.ReadAllBytes(Filesystem.expArmPath), synthBefore = (byte[])synthNow.Clone();
            byte[] block = new byte[SyntheticOverlaySpace.HeaderSize + rows.Length];
            Encoding.ASCII.GetBytes(Marker).CopyTo(block, 0);
            BitConverter.GetBytes(1u).CopyTo(block, 0x0C);
            BitConverter.GetBytes((uint)block.Length).CopyTo(block, 0x10);
            BitConverter.GetBytes((uint)Rows.Count).CopyTo(block, 0x14);
            rows.CopyTo(block, SyntheticOverlaySpace.HeaderSize);

            if (_blockStart >= 0) Array.Clear(synthNow, _blockStart, _blockLength);
            int at = SyntheticOverlaySpace.FindFree(synthNow, block.Length, 4, SyntheticOverlaySpace.Reserved(synthNow));
            if (at < 0) throw new InvalidOperationException("No free space was found in the expanded ARM9 area for the swarm table.");
            block.CopyTo(synthNow, at);
            uint ram = synthOverlayLoadAddress + (uint)(at + SyntheticOverlaySpace.HeaderSize);
            foreach (int o in _sites.Literals) BitConverter.GetBytes(ram).CopyTo(code, o);
            foreach (int o in _sites.LiteralsPlus2) BitConverter.GetBytes(ram + 2).CopyTo(code, o);
            foreach (int o in _sites.CountSites) code[o] = (byte)Rows.Count;
            try
            {
                File.WriteAllBytes(Filesystem.expArmPath, synthNow);
                File.WriteAllBytes(CodePath, code);
            }
            catch
            {
                File.WriteAllBytes(Filesystem.expArmPath, synthBefore);
                File.WriteAllBytes(CodePath, codeBefore);
                throw;
            }
            _path = Filesystem.expArmPath; _offset = at + SyntheticOverlaySpace.HeaderSize;
            _blockStart = at; _blockLength = block.Length;
            Capacity = Rows.Count;
            Where = "in the expanded ARM9 area";
        }

        public byte[] Snapshot() => RowBytes().Concat(BitConverter.GetBytes(Rows.Count)).ToArray();
    }
}
