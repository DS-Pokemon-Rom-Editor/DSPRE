using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The chance of each encounter slot, compiled into the slot selectors as `cmp r0, #boundary` instructions on a
    /// roll of 0 to 99. Slot i covers [boundary i-1, boundary i); the last slot takes the rest. Walking's eleventh slot
    /// is an equality test (`cmp r0, #b9; bne`), so it is exactly one roll; any other width switches that test to
    /// `cmp r0, #b10; bcs`, which reads the same when the width is one.
    /// </summary>
    public sealed class EncounterSlotOdds
    {
        public sealed class Method
        {
            public SlotOddsMethod Sites { get; }
            public string Name => Sites.Name;
            public int[] Percents { get; }
            internal Method(SlotOddsMethod sites, int[] percents) { Sites = sites; Percents = percents; }
            internal bool HasEqualityTail => Sites.Slots - 1 > Sites.Boundaries.Length;
        }

        public List<Method> Methods { get; } = new List<Method>();

        private const byte CmpOpcodeMask = 0xF8, CmpOpcode = 0x28, Bne = 0xD1, Bcs = 0xD2;

        public static string WhyNot()
        {
            var methods = SlotOddsMethods;
            if (methods == null) return "Only US HeartGold, Platinum (Rev 1) and Diamond are supported.";
            foreach (int ov in methods.Select(m => m.Overlay).Distinct())
                if (!File.Exists(OverlayUtils.GetPath(ov))) return $"Overlay {ov} is missing from this project.";
            try { Load(); }
            catch (InvalidDataException e) { return e.Message; }
            return null;
        }

        private static byte[] ReadOverlay(int ov)
        {
            if (OverlayUtils.IsCompressed(ov)) OverlayUtils.Decompress(ov);
            return File.ReadAllBytes(OverlayUtils.GetPath(ov));
        }

        private static int Imm(byte[] d, int site, string method)
        {
            if (site + 1 >= d.Length || (d[site + 1] & CmpOpcodeMask) != CmpOpcode)
                throw new InvalidDataException($"The {method} slot code doesn't look like the game's; it may have been patched.");
            return d[site];
        }

        public static EncounterSlotOdds Load()
        {
            var odds = new EncounterSlotOdds();
            var files = new Dictionary<int, byte[]>();
            foreach (var m in SlotOddsMethods ?? throw new InvalidOperationException("This game version isn't supported yet."))
            {
                if (!files.TryGetValue(m.Overlay, out var d)) files[m.Overlay] = d = ReadOverlay(m.Overlay);
                var bounds = new List<int>();
                foreach (int[] sites in m.Boundaries)
                {
                    var values = sites.Select(s => Imm(d, s, m.Name)).Distinct().ToList();
                    if (values.Count != 1) throw new InvalidDataException($"The {m.Name} slot code compares one boundary against different values.");
                    bounds.Add(values[0]);
                }
                if (m.Slots - 1 > m.Boundaries.Length)
                {
                    int tail = m.Boundaries[0][0] + LandLastSlotSite;
                    int imm = Imm(d, tail, m.Name);
                    if (tail + 3 >= d.Length) throw new InvalidDataException($"The {m.Name} slot code runs past the end of its overlay.");
                    byte branch = d[tail + 3];
                    if (branch == Bne && imm == bounds[^1]) bounds.Add(imm + 1);
                    else if (branch == Bcs) bounds.Add(imm);
                    else throw new InvalidDataException($"The {m.Name} slot code's last test doesn't look like the game's; it may have been patched.");
                }
                var percents = new int[m.Slots];
                int previous = 0;
                for (int i = 0; i < bounds.Count; i++) { percents[i] = bounds[i] - previous; previous = bounds[i]; }
                percents[^1] = 100 - previous;
                odds.Methods.Add(new Method(m, percents));
            }
            return odds;
        }

        /// <summary>Why the odds can't be saved, or null.</summary>
        public string Problem()
        {
            foreach (var m in Methods)
            {
                if (m.Percents.Any(p => p < 0)) return $"{m.Name}: a slot has a negative chance.";
                int sum = m.Percents.Sum();
                if (sum != 100) return $"{m.Name}: the slots add up to {sum}%, not 100%.";
            }
            return null;
        }

        public byte[] Snapshot() => Methods.SelectMany(m => m.Percents).Select(p => (byte)Math.Clamp(p, 0, 255)).ToArray();

        public void Restore(byte[] snapshot)
        {
            int k = 0;
            foreach (var m in Methods) for (int i = 0; i < m.Percents.Length; i++) m.Percents[i] = snapshot[k++];
        }

        public void Save()
        {
            if (Problem() is string p) throw new InvalidOperationException(p);
            var files = new Dictionary<int, byte[]>();
            foreach (var m in Methods)
            {
                if (!files.TryGetValue(m.Sites.Overlay, out var d)) files[m.Sites.Overlay] = d = ReadOverlay(m.Sites.Overlay);
                int running = 0;
                var bounds = new List<int>();
                for (int i = 0; i < m.Percents.Length - 1; i++) { running += m.Percents[i]; bounds.Add(running); }
                for (int b = 0; b < m.Sites.Boundaries.Length; b++)
                    foreach (int site in m.Sites.Boundaries[b]) d[site] = (byte)bounds[b];
                if (m.HasEqualityTail)
                {
                    int tail = m.Sites.Boundaries[0][0] + LandLastSlotSite;
                    bool oneRoll = m.Percents[m.Percents.Length - 2] == 1;
                    d[tail] = (byte)(oneRoll ? bounds[^2] : bounds[^1]);
                    d[tail + 3] = oneRoll ? Bne : Bcs;
                }
            }
            foreach (var (ov, d) in files) File.WriteAllBytes(OverlayUtils.GetPath(ov), d);
        }

        private static (string key, EncounterSlotOdds odds) _cache;

        /// <summary>The live percentages for a method by name, or null when they can't be read.</summary>
        public static int[] CurrentPercents(string methodName)
        {
            try
            {
                var methods = SlotOddsMethods;
                if (methods == null) return null;
                // A label lookup never decompresses an overlay; that would write to the project.
                if (methods.Any(m => OverlayUtils.IsCompressed(m.Overlay))) return null;
                // Re-read only when a selector overlay changed on disk.
                string key = romID + ":" + string.Join(",", methods.Select(m => m.Overlay).Distinct()
                    .Select(ov => OverlayUtils.GetPath(ov)).Select(p => p + File.GetLastWriteTimeUtc(p).Ticks));
                if (_cache.key != key) _cache = (key, Load());
                return _cache.odds.Methods.FirstOrDefault(m => m.Name == methodName)?.Percents;
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException
                                      || e is UnauthorizedAccessException || e is IndexOutOfRangeException) { return null; }
        }

        /// <summary>A slot's label with its live chance: "20%" alone, or "Surf 1 · 60%" with a prefix.</summary>
        public static string SlotLabel(string methodName, int slot, string prefix = null)
        {
            int[] p = CurrentPercents(methodName);
            string pct = p != null && slot < p.Length ? $"{p[slot]}%" : null;
            if (prefix == null) return pct ?? $"Slot {slot + 1}";
            return pct == null ? prefix : $"{prefix} · {pct}";
        }
    }
}
