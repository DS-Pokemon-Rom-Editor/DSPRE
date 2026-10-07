using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.HgEngine;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The battle type chart: 3-byte attacker, defender, tenths records; missing pairs are neutral, records after
    /// <c>FE FE 00</c> are skipped by Foresight and Scrappy, and <c>FF FF 00</c> ends it. The game tests only the
    /// attacker byte for both markers.
    /// </summary>
    public class TypeChart
    {
        public const int RecordSize = 3, VanillaTypes = 18, Neutral = 10;
        private const byte Boundary = 0xFE, End = 0xFF;

        public sealed class Matchup
        {
            public byte Attacker, Defender, Tenths;
            public bool ForesightRemovable;
            /// <summary>hg-engine: an immunity listed after TYPE_RING_TARGET, which Ring Target lifts.</summary>
            public bool RingTargetRemovable;
        }

        /// <summary>True when the chart is hg-engine's TypeEffectivenessTable in its source.</summary>
        public bool FromSource { get; private set; }

        private static bool UsesSource => HgEngine.HgEngineProject.IsActive;

        /// <summary>Non-neutral pairs, in the order the chart lists them.</summary>
        public List<Matchup> Matchups { get; } = new List<Matchup>();

        /// <summary>Record slots the chart has room for, the two control records included.</summary>
        public int Capacity { get; private set; }
        public int MaxMatchups => Capacity - 2;

        public string Where { get; private set; }

        private string _path;
        private int _offset;
        private string _ovPath;
        private bool _countIsCompare;
        private bool _modulusIsMovs;

        public const string Marker = "TYPECHARTXP1";
        /// <summary>Capped at 255 because Conversion 2's bound is a byte immediate.</summary>
        public const int ExpandedCapacity = 255;

        /// <summary>Whether the chart already lives in a block DSPRE placed in the expanded ARM9 area.</summary>
        public bool InExpansion { get; private set; }

        public static string WhyNot()
        {
            if (UsesSource) return null;
            if (GameTableFile.WhyNot(GameTable.TypeChart, 112 * RecordSize) is string why) return why;
            return TypeChartPointerSites == null ? "This game version isn't supported yet." : null;
        }

        public static TypeChart Load()
        {
            if (UsesSource) return LoadSource();
            TypeChart chart = new TypeChart();
            chart.Locate();
            byte[] data = File.ReadAllBytes(chart._path);
            int records = 0;
            bool foresight = false;
            for (int at = chart._offset; at + RecordSize <= data.Length; at += RecordSize, records++)
            {
                byte a = data[at], d = data[at + 1], m = data[at + 2];
                if (a == End) { records++; break; }
                if (a == Boundary) { foresight = true; continue; }
                if (records > 4096) throw new InvalidDataException("The type chart has no end marker.");
                Matchup existing = chart.Matchups.FirstOrDefault(x => x.Attacker == a && x.Defender == d);
                if (existing != null) throw new InvalidDataException($"The type chart lists types {a} and {d} twice; DSPRE can't edit it without changing damage.");
                chart.Matchups.Add(new Matchup { Attacker = a, Defender = d, Tenths = m, ForesightRemovable = foresight });
            }
            chart.Capacity = Math.Max(chart.Capacity, records);
            return chart;
        }

        // hg-engine repoints the battle code to its own table, sized by the compiler, so there is no fixed room.
        private static TypeChart LoadSource()
        {
            if (!HgEngine.HgEngineTypeChart.TryRead(out List<HgEngineTypeChart.Row> rows, out string error)) throw new InvalidDataException(error);
            TypeChart chart = new TypeChart { FromSource = true, InExpansion = true, Capacity = 4096, Where = "in " + HgEngine.HgEngineTypeChart.SourceRelPath };
            foreach (HgEngineTypeChart.Row r in rows)
            {
                if (chart.Find(r.Attacker, r.Defender) != null)
                    throw new InvalidDataException($"The type chart lists types {r.Attacker} and {r.Defender} twice; DSPRE can't edit it without changing damage.");
                chart.Matchups.Add(new Matchup
                {
                    Attacker = (byte)r.Attacker, Defender = (byte)r.Defender, Tenths = (byte)r.Tenths,
                    ForesightRemovable = r.Section == HgEngine.HgEngineTypeChart.Section.Foresight,
                    RingTargetRemovable = r.Section == HgEngine.HgEngineTypeChart.Section.RingTarget,
                });
            }
            return chart;
        }

        /// <summary>Follows the battle overlay's pointers, so a chart a patch moved is edited where it now lives.</summary>
        private void Locate()
        {
            if (WhyNot() is string why) throw new InvalidOperationException(why);
            TableSpot spot = SpotOf(GameTable.TypeChart).Value;
            (int[] col0, int[] col1, int[] col2, int countCompare, int countModulus) sites = TypeChartPointerSites.Value;
            string ovPath = GameTableFile.PathOf(spot);
            _ovPath = ovPath;
            if (OverlayUtils.IsCompressed(spot.Overlay)) OverlayUtils.Decompress(spot.Overlay);
            byte[] ov = File.ReadAllBytes(ovPath);
            uint ovBase = OverlayUtils.OverlayTable.GetRAMAddress(spot.Overlay);

            List<uint> targets = sites.col0.Select(o => BitConverter.ToUInt32(ov, o)).Distinct().ToList();
            if (targets.Count != 1) throw new InvalidDataException("The battle code points at more than one type chart; DSPRE can't tell which is used.");
            uint ram = targets[0];
            if (sites.col1.Any(o => BitConverter.ToUInt32(ov, o) != ram + 1) || sites.col2.Any(o => BitConverter.ToUInt32(ov, o) != ram + 2))
                throw new InvalidDataException("The battle code's type chart pointers don't agree with each other.");

            if (ram >= ovBase && ram < ovBase + ov.Length)
            {
                _path = ovPath; _offset = (int)(ram - ovBase);
                Where = ram - ovBase == spot.Offset ? $"in overlay {spot.Overlay}" : $"moved within overlay {spot.Overlay}";
            }
            else if (ram >= synthOverlayLoadAddress && File.Exists(Filesystem.expArmPath)
                     && ram - synthOverlayLoadAddress + (ulong)RecordSize < (ulong)new FileInfo(Filesystem.expArmPath).Length)
            {
                _path = Filesystem.expArmPath; _offset = (int)(ram - synthOverlayLoadAddress);
                Where = "in the expanded ARM9 area";
                byte[] synth = File.ReadAllBytes(_path);
                InExpansion = SyntheticOverlaySpace.Blocks(synth, Marker).Any(b => _offset == b.Start + SyntheticOverlaySpace.HeaderSize);
            }
            else if (ram >= ARM9.address && ram < ARM9.address + new FileInfo(arm9Path).Length)
            {
                _path = arm9Path; _offset = (int)(ram - ARM9.address);
                Where = "moved into arm9";
            }
            else throw new InvalidDataException($"The type chart was moved to 0x{ram:X8}, which DSPRE can't follow.");

            // Conversion 2 walks the chart by count, so its `cmp rN, #count` is the capacity.
            byte imm = ov[sites.countCompare], op = ov[sites.countCompare + 1];
            _countIsCompare = (op & 0xF8) == 0x28;
            Capacity = _countIsCompare ? imm : 0;
            _modulusIsMovs = ov[sites.countModulus + 1] == 0x21;
        }

        /// <summary>
        /// Charts moved by older DSPRE builds left Conversion 2's random pick at the vanilla count, so it never
        /// reached the added records. Sets it to the count compare's value.
        /// </summary>
        private void RepairModulus()
        {
            if (!InExpansion || !_countIsCompare || !_modulusIsMovs) return;
            (int[] col0, int[] col1, int[] col2, int countCompare, int countModulus) sites = TypeChartPointerSites.Value;
            byte[] ov = File.ReadAllBytes(_ovPath);
            if (ov[sites.countModulus] == ov[sites.countCompare]) return;
            DSUtils.WriteToFile(_ovPath, new[] { ov[sites.countCompare] }, (uint)sites.countModulus);
        }

        /// <summary>DP and Pt's Pokétch copy: an 18x18 grid of 1 (super effective), -1 (not very), -10 (none) and 0.</summary>
        public static byte[] PoketchGrid(IEnumerable<Matchup> matchups)
        {
            byte[] grid = new byte[VanillaTypes * VanillaTypes];
            foreach (Matchup m in matchups)
            {
                if (m.Attacker >= VanillaTypes || m.Defender >= VanillaTypes) continue;
                sbyte v = m.Tenths == 0 ? (sbyte)-10 : m.Tenths < Neutral ? (sbyte)-1 : m.Tenths > Neutral ? (sbyte)1 : (sbyte)0;
                grid[m.Attacker * VanillaTypes + m.Defender] = unchecked((byte)v);
            }
            return grid;
        }

        public byte[] ToBytes()
        {
            List<byte> data = new List<byte>();
            void Add(byte a, byte d, byte m) { data.Add(a); data.Add(d); data.Add(m); }
            foreach (Matchup m in Matchups.Where(x => !x.ForesightRemovable)) Add(m.Attacker, m.Defender, m.Tenths);
            Add(Boundary, Boundary, 0);
            foreach (Matchup m in Matchups.Where(x => x.ForesightRemovable)) Add(m.Attacker, m.Defender, m.Tenths);
            while (data.Count < Capacity * RecordSize) Add(End, End, 0);
            return data.ToArray();
        }

        /// <summary>Why the chart can't be saved, or null.</summary>
        public string Problem()
        {
            if (Matchups.Count > MaxMatchups) return $"The chart has room for {MaxMatchups} matchups; {Matchups.Count} are set.";
            if (Matchups.Any(m => m.Tenths == Neutral)) return "A neutral matchup is listed; remove it instead.";
            return null;
        }

        /// <summary>Multipliers the game's messages and Conversion 2 don't treat as the usual four.</summary>
        public IEnumerable<Matchup> Unusual => Matchups.Where(m => m.Tenths != 0 && m.Tenths != 5 && m.Tenths != 20);

        public void Save()
        {
            if (Problem() is string p) throw new InvalidOperationException(p);
            if (FromSource)
            {
                List<HgEngineTypeChart.Row> rows = Matchups.Select(m => new HgEngine.HgEngineTypeChart.Row(m.Attacker, m.Defender, m.Tenths,
                    m.ForesightRemovable ? HgEngine.HgEngineTypeChart.Section.Foresight
                    : m.RingTargetRemovable ? HgEngine.HgEngineTypeChart.Section.RingTarget
                    : HgEngine.HgEngineTypeChart.Section.Main)).ToList();
                if (!HgEngine.HgEngineTypeChart.TryWrite(rows, out string error)) throw new IOException(error);
                return;
            }
            byte[] chart = ToBytes();
            // A failed Pokétch write puts the chart and modulus back, so neither overlay changes.
            byte[] chartBefore = DSUtils.ReadFromFile(_path, _offset, chart.Length);
            int modulusSite = TypeChartPointerSites.Value.countModulus;
            byte[] modulusBefore = DSUtils.ReadFromFile(_ovPath, modulusSite, 1);
            DSUtils.WriteToFile(_path, chart, (uint)_offset);
            RepairModulus();
            try
            {
                if (SpotOf(GameTable.PoketchTypeChart) != null && GameTableFile.WhyNot(GameTable.PoketchTypeChart, VanillaTypes * VanillaTypes) == null)
                    GameTableFile.Write(GameTable.PoketchTypeChart, PoketchGrid(Matchups));
            }
            catch
            {
                DSUtils.WriteToFile(_path, chartBefore, (uint)_offset);
                DSUtils.WriteToFile(_ovPath, modulusBefore, (uint)modulusSite);
                throw;
            }
        }

        /// <summary>Moves the chart into its own expanded ARM9 block and repoints the battle code and Conversion 2's bound.</summary>
        public void MoveToExpansion()
        {
            if (InExpansion) return;
            if (!SyntheticOverlaySpace.Available())
                throw new InvalidOperationException("Apply the ARM9 expansion in the ROM Patch Toolbox first.");
            (int[] col0, int[] col1, int[] col2, int countCompare, int countModulus) sites = TypeChartPointerSites ?? throw new InvalidOperationException("This game version isn't supported yet.");
            if (!_countIsCompare || !_modulusIsMovs) throw new InvalidOperationException("Conversion 2's count check doesn't look like the game's, so DSPRE won't move the chart.");
            int oldCapacity = Capacity;
            Capacity = ExpandedCapacity;
            if (Problem() is string p) { Capacity = oldCapacity; throw new InvalidOperationException(p); }

            byte[] chart = ToBytes();
            byte[] block = new byte[(SyntheticOverlaySpace.HeaderSize + chart.Length + 3) & ~3];
            System.Text.Encoding.ASCII.GetBytes(Marker).CopyTo(block, 0);
            BitConverter.GetBytes(1u).CopyTo(block, 0x0C);
            BitConverter.GetBytes((uint)block.Length).CopyTo(block, 0x10);
            BitConverter.GetBytes((uint)ExpandedCapacity).CopyTo(block, 0x14);
            chart.CopyTo(block, SyntheticOverlaySpace.HeaderSize);

            byte[] synth = File.ReadAllBytes(Filesystem.expArmPath);
            int at = SyntheticOverlaySpace.FindFree(synth, block.Length, 4, SyntheticOverlaySpace.Reserved(synth));
            if (at < 0) { Capacity = oldCapacity; throw new InvalidOperationException("No free space was found in the expanded ARM9 area for the type chart."); }
            byte[] ov = File.ReadAllBytes(_ovPath);
            byte[] ovBefore = (byte[])ov.Clone(), synthBefore = (byte[])synth.Clone();
            block.CopyTo(synth, at);
            uint ram = synthOverlayLoadAddress + (uint)(at + SyntheticOverlaySpace.HeaderSize);
            foreach (int o in sites.col0) BitConverter.GetBytes(ram).CopyTo(ov, o);
            foreach (int o in sites.col1) BitConverter.GetBytes(ram + 1).CopyTo(ov, o);
            foreach (int o in sites.col2) BitConverter.GetBytes(ram + 2).CopyTo(ov, o);
            ov[sites.countCompare] = ExpandedCapacity;
            ov[sites.countModulus] = ExpandedCapacity;
            try
            {
                File.WriteAllBytes(Filesystem.expArmPath, synth);
                File.WriteAllBytes(_ovPath, ov);
            }
            catch
            {
                File.WriteAllBytes(Filesystem.expArmPath, synthBefore);
                File.WriteAllBytes(_ovPath, ovBefore);
                Capacity = oldCapacity;
                throw;
            }
            _path = Filesystem.expArmPath;
            _offset = at + SyntheticOverlaySpace.HeaderSize;
            InExpansion = true;
            Where = "in the expanded ARM9 area";
            if (SpotOf(GameTable.PoketchTypeChart) != null && GameTableFile.WhyNot(GameTable.PoketchTypeChart, VanillaTypes * VanillaTypes) == null)
                GameTableFile.Write(GameTable.PoketchTypeChart, PoketchGrid(Matchups));
        }

        /// <summary>A chart in the expanded ARM9 area without a DSPRE block, so allocators skip it.</summary>
        internal static (long Start, long End)? UnmarkedRangeInExpansion()
        {
            try
            {
                if (WhyNot() != null) return null;
                TypeChart chart = Load();
                if (chart._path != Filesystem.expArmPath || chart.InExpansion) return null;
                return (chart._offset, chart._offset + (long)chart.Capacity * RecordSize);
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException) { return null; }
        }

        public Matchup Find(int attacker, int defender) => Matchups.FirstOrDefault(m => m.Attacker == attacker && m.Defender == defender);

        /// <summary>Sets a pair's multiplier; neutral removes it. New pairs join the end of their part of the chart.</summary>
        public void Set(int attacker, int defender, int tenths, bool foresightRemovable)
        {
            Matchup m = Find(attacker, defender);
            if (tenths == Neutral) { if (m != null) Matchups.Remove(m); return; }
            // hg-engine lists every immunity after its Ring Target row.
            if (m == null) { m = new Matchup { Attacker = (byte)attacker, Defender = (byte)defender, RingTargetRemovable = FromSource }; Matchups.Add(m); }
            m.Tenths = (byte)Math.Clamp(tenths, 0, 255);
            m.ForesightRemovable = foresightRemovable && tenths == 0;
            m.RingTargetRemovable = FromSource && tenths == 0 && !m.ForesightRemovable;
        }
    }
}
