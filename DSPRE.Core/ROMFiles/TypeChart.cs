using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The battle type chart (sTypeMatchupMultipliers / sTypeEffectiveness): 3-byte records of attacker, defender and
    /// multiplier in tenths. Pairs missing from it are neutral. Records after <c>FE FE 00</c> are the ones
    /// Foresight and Scrappy skip; <c>FF FF 00</c> ends it. The chart is followed through the battle overlay's
    /// pointers, so one a patch moved (for a Fairy type, say) is edited where it now lives. Diamond, Pearl and
    /// Platinum keep a Pokétch copy, an 18x18 grid of 1 (super effective), -1 (not very), -10 (none) and 0.
    /// </summary>
    public class TypeChart
    {
        public const int RecordSize = 3, VanillaTypes = 18, Neutral = 10;
        private const byte Boundary = 0xFE, End = 0xFF;

        public sealed class Matchup
        {
            public byte Attacker, Defender, Tenths;
            public bool ForesightRemovable;
        }

        /// <summary>Non-neutral pairs, in the order the chart lists them.</summary>
        public List<Matchup> Matchups { get; } = new List<Matchup>();

        /// <summary>Record slots the chart has room for, the two control records included.</summary>
        public int Capacity { get; private set; }
        public int MaxMatchups => Capacity - 2;

        /// <summary>Where the chart was found, for the window's status line.</summary>
        public string Where { get; private set; }

        private string _path;
        private int _offset;

        public static string WhyNot()
        {
            if (GameTableFile.WhyNot(GameTable.TypeChart, 112 * RecordSize) is string why) return why;
            return TypeChartPointerSites == null ? "This game version isn't supported yet." : null;
        }

        public static TypeChart Load()
        {
            var chart = new TypeChart();
            chart.Locate();
            byte[] data = File.ReadAllBytes(chart._path);
            int records = 0;
            bool foresight = false;
            for (int at = chart._offset; at + RecordSize <= data.Length; at += RecordSize, records++)
            {
                byte a = data[at], d = data[at + 1], m = data[at + 2];
                if (a == End && d == End) { records++; break; }
                if (a == Boundary && d == Boundary) { foresight = true; continue; }
                if (records > 4096) throw new InvalidDataException("The type chart has no end marker.");
                var existing = chart.Matchups.FirstOrDefault(x => x.Attacker == a && x.Defender == d);
                if (existing != null) throw new InvalidDataException($"The type chart lists types {a} and {d} twice; DSPRE can't edit it without changing damage.");
                chart.Matchups.Add(new Matchup { Attacker = a, Defender = d, Tenths = m, ForesightRemovable = foresight });
            }
            chart.Capacity = Math.Max(chart.Capacity, records);
            return chart;
        }

        /// <summary>Finds the chart through the overlay's pointers and reads its capacity from Conversion 2's bound.</summary>
        private void Locate()
        {
            if (WhyNot() is string why) throw new InvalidOperationException(why);
            var spot = SpotOf(GameTable.TypeChart).Value;
            var sites = TypeChartPointerSites.Value;
            string ovPath = GameTableFile.PathOf(spot);
            if (OverlayUtils.IsCompressed(spot.Overlay)) OverlayUtils.Decompress(spot.Overlay);
            byte[] ov = File.ReadAllBytes(ovPath);
            uint ovBase = OverlayUtils.OverlayTable.GetRAMAddress(spot.Overlay);

            var targets = sites.col0.Select(o => BitConverter.ToUInt32(ov, o)).Distinct().ToList();
            if (targets.Count != 1) throw new InvalidDataException("The battle code points at more than one type chart; DSPRE can't tell which is used.");
            uint ram = targets[0];
            if (sites.col1.Any(o => BitConverter.ToUInt32(ov, o) != ram + 1) || sites.col2.Any(o => BitConverter.ToUInt32(ov, o) != ram + 2))
                throw new InvalidDataException("The battle code's type chart pointers don't agree with each other.");

            if (ram >= ovBase && ram < ovBase + ov.Length)
            {
                _path = ovPath; _offset = (int)(ram - ovBase);
                Where = ram - ovBase == spot.Offset ? $"overlay {spot.Overlay}" : $"moved within overlay {spot.Overlay}";
            }
            else if (ram >= synthOverlayLoadAddress && File.Exists(Filesystem.expArmPath))
            {
                _path = Filesystem.expArmPath; _offset = (int)(ram - synthOverlayLoadAddress);
                Where = "moved to the expanded ARM9 area";
            }
            else if (ram >= ARM9.address && ram < ARM9.address + new FileInfo(arm9Path).Length)
            {
                _path = arm9Path; _offset = (int)(ram - ARM9.address);
                Where = "moved to arm9";
            }
            else throw new InvalidDataException($"The type chart was moved to 0x{ram:X8}, which DSPRE can't follow.");

            // Conversion 2 walks the chart by count, so its `cmp rN, #count` is the room the game allows.
            byte imm = ov[sites.countCompare], op = ov[sites.countCompare + 1];
            Capacity = (op & 0xF8) == 0x28 ? imm : 0;
        }

        public static byte[] PoketchGrid(IEnumerable<Matchup> matchups)
        {
            var grid = new byte[VanillaTypes * VanillaTypes];
            foreach (var m in matchups)
            {
                if (m.Attacker >= VanillaTypes || m.Defender >= VanillaTypes) continue;
                sbyte v = m.Tenths == 0 ? (sbyte)-10 : m.Tenths < Neutral ? (sbyte)-1 : m.Tenths > Neutral ? (sbyte)1 : (sbyte)0;
                grid[m.Attacker * VanillaTypes + m.Defender] = unchecked((byte)v);
            }
            return grid;
        }

        public byte[] ToBytes()
        {
            var data = new List<byte>();
            void Add(byte a, byte d, byte m) { data.Add(a); data.Add(d); data.Add(m); }
            foreach (var m in Matchups.Where(x => !x.ForesightRemovable)) Add(m.Attacker, m.Defender, m.Tenths);
            Add(Boundary, Boundary, 0);
            foreach (var m in Matchups.Where(x => x.ForesightRemovable)) Add(m.Attacker, m.Defender, m.Tenths);
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
            DSUtils.WriteToFile(_path, ToBytes(), (uint)_offset);
            if (SpotOf(GameTable.PoketchTypeChart) != null && GameTableFile.WhyNot(GameTable.PoketchTypeChart, VanillaTypes * VanillaTypes) == null)
                GameTableFile.Write(GameTable.PoketchTypeChart, PoketchGrid(Matchups));
        }

        public Matchup Find(int attacker, int defender) => Matchups.FirstOrDefault(m => m.Attacker == attacker && m.Defender == defender);

        /// <summary>Sets a pair's multiplier; neutral removes it. New pairs join the end of their part of the chart.</summary>
        public void Set(int attacker, int defender, int tenths, bool foresightRemovable)
        {
            var m = Find(attacker, defender);
            if (tenths == Neutral) { if (m != null) Matchups.Remove(m); return; }
            if (m == null) { m = new Matchup { Attacker = (byte)attacker, Defender = (byte)defender }; Matchups.Add(m); }
            m.Tenths = (byte)Math.Clamp(tenths, 0, 255);
            m.ForesightRemovable = foresightRemovable && tenths == 0;
        }
    }
}
