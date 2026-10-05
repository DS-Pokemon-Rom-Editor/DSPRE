using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The roaming Pokémon, all in arm9: each roamer slot's species and level (pokeplatinum
    /// RoamingPokemon_ActivateSlot, pokeheartgold Save_CreateRoamerByID, DP's equivalent), the route table the
    /// roamers move between (map header ids) and its adjacency table (indexes into the routes, the ones a roamer can
    /// move to next). Everything is found through the code that reads it, not fixed offsets.
    ///
    /// The game sets species and level with immediates per slot, which can't hold every species. Saving replaces the
    /// slot switch with a lookup into a (species, level) table in the same bytes, and in Platinum and HGSS also the
    /// routine that maps a battled species back to its slot (SystemVars_SetRoamingSpeciesState, SpeciesToRoamerIdx),
    /// so a changed species keeps its after-battle state. Route and adjacency counts stay as the game has them.
    /// </summary>
    public sealed class RoamerData
    {
        public sealed class Slot
        {
            public int Species { get; set; }
            public int Level { get; set; }
        }

        public List<Slot> Slots { get; } = new();
        public List<int> Routes { get; } = new();
        public List<List<int>> Adjacency { get; } = new();
        /// <summary>Most route indexes an adjacency entry holds: 5 in DP and Platinum, 6 in HGSS.</summary>
        public int AdjacencyMax { get; private set; }
        /// <summary>HGSS: the first route index and count Raikou and Entei (slots 0-1) move within, then the rest.</summary>
        public (int Start, int Count)[] Regions { get; private set; }

        private byte[] _code;
        private int _routeTable, _adjTable, _switch, _reverse = -1;
        private Family _family;
        private int[] _setters;   // Platinum: the after-battle state setter per slot (0 = none)

        private enum Family { DP, Plat, HGSS }

        private sealed class Layout
        {
            public int SwitchLength, JoinOffset, DefaultOffset, TableOffset;
            public byte[] PatchedCode;    // the switch, without its bounds byte and table
            public int SpeciesReg, LevelReg;
        }

        // The create routine up to the slot switch; wildcards are its calls.
        private static readonly Dictionary<Family, Layout> Layouts = new()
        {
            [Family.Plat] = new Layout
            {
                SwitchLength = 0x42, JoinOffset = 0x4A, DefaultOffset = 0x42, TableOffset = 0x10,
                // cmp r0,#n-1; bhi default; lsls r0,#2; adr r1,table; adds r1,r0; ldrh r4,[r1]; ldrh r5,[r1,#2]; b join
                PatchedCode = Hex("05 28 1E D8 80 00 02 A1 09 18 0C 88 4D 88 1C E0"),
                SpeciesReg = 4, LevelReg = 5,
            },
            [Family.HGSS] = new Layout
            {
                SwitchLength = 0x32, JoinOffset = 0x3A, DefaultOffset = 0x32, TableOffset = 0x10,
                PatchedCode = Hex("03 28 16 D8 80 00 02 A1 09 18 0E 88 4D 88 14 E0"),
                SpeciesReg = 6, LevelReg = 5,
            },
            [Family.DP] = new Layout
            {
                // The switch starts two bytes past a word, so the table sits after a pad.
                SwitchLength = 0x22, JoinOffset = 0x2A, DefaultOffset = 0x22, TableOffset = 0x12,
                PatchedCode = Hex("02 2D 0E D8 A8 00 02 A1 09 18 0F 88 4E 88 0C E0 C0 46"),
                SpeciesReg = 7, LevelReg = 6,
            },
        };

        // Reverse mappings as the game has them, and as DSPRE writes them (a loop over the slot table).
        private static readonly byte[] PtReversePatched = Hex("30 B5 09 4B 00 24 A5 00 5D 5B 8D 42 03 D0 01 34 06 2C F8 D3 30 BD A4 00 04 4B 1B 59 00 2B 01 D0 11 1C 98 47 30 BD C0 46");
        private const int PtReverseLength = 0x54;
        private const string HgReverseOriginal = "08 B5 5F 21 89 00 88 42 08 DC 11 DA F4 28 11 DC F3 28 0F DB";
        private static readonly byte[] HgReversePatched = Hex("05 4B 00 21 8A 00 9A 5A 82 42 02 D0 01 31 04 29 F8 D3 08 1C 70 47 C0 46");
        private const int HgReverseLength = 0x3C;

        private static byte[] Hex(string s) => s.Split(' ').Select(b => Convert.ToByte(b, 16)).ToArray();
        private static string Hex(byte[] b) => string.Join(" ", b.Select(x => x.ToString("X2")));
        private static (byte[] Bytes, bool[] Any) S(string s) =>
            (s.Split(' ').Select(b => b == "??" ? (byte)0 : Convert.ToByte(b, 16)).ToArray(), s.Split(' ').Select(b => b == "??").ToArray());

        private static int FindOne(byte[] code, string signature, int from = 0, int to = -1)
        {
            var (bytes, any) = S(signature);
            if (to < 0) to = code.Length;
            int found = -1;
            for (int i = from; i + bytes.Length <= to; i += 2)
            {
                bool ok = true;
                for (int j = 0; j < bytes.Length && ok; j++) ok = any[j] || code[i + j] == bytes[j];
                if (!ok) continue;
                if (found >= 0) return -2;
                found = i;
            }
            return found;
        }

        private static bool At(byte[] code, int at, string signature)
        {
            var (bytes, any) = S(signature);
            if (at < 0 || at + bytes.Length > code.Length) return false;
            for (int j = 0; j < bytes.Length; j++) if (!any[j] && code[at + j] != bytes[j]) return false;
            return true;
        }

        private static uint Word(byte[] code, int at) => BitConverter.ToUInt32(code, at);
        private static int ToFile(uint ram) => (int)(ram - ARM9.address);
        private static int LiteralOf(int ldrAt, byte[] code) => ((ldrAt + 4) & ~3) + code[ldrAt] * 4;

        /// <summary>Why the roamers can't be edited, or null.</summary>
        public static string WhyNot() => TryLoad(out _, out string error) ? null : error;

        public static bool TryLoad(out RoamerData data, out string error)
        {
            data = null;
            if (HgEngine.HgEngineProject.IsActive) { error = "hg-engine keeps its roamers in src/field_roamer.c."; return false; }
            if (!IsDsRomProject && ARM9.CheckCompressionMark()) { error = "arm9 is still compressed. Convert this project to ds-rom format first."; return false; }
            var family = gameFamily switch { GameFamilies.DP => Family.DP, GameFamilies.Plat => Family.Plat, GameFamilies.HGSS => Family.HGSS, _ => (Family?)null };
            if (family == null) { error = "This game has no roamers DSPRE knows."; return false; }
            byte[] code;
            try { code = File.ReadAllBytes(arm9Path); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { error = e.Message; return false; }

            var d = new RoamerData { _code = code, _family = family.Value };
            error = d.Read();
            if (error != null) return false;
            data = d;
            return true;
        }

        private string Read()
        {
            var layout = Layouts[_family];
            string fail = "the roamer code isn't the game's own, so it may have been patched";

            // Routes: the lookup's cmp gives the count, the literal after it the table.
            // Other lookups share this shape; the game's route count (29 in DP and Platinum, 41 in HGSS) picks it out.
            string routeCountByte = _family == Family.HGSS ? "29" : "1D";
            int lookup = FindOne(_code, $"10 B5 04 1C {routeCountByte} 2C 01 D3 ?? ?? ?? ?? 01 48 A1 00 40 58 10 BD");
            if (lookup < 0) return $"The roamer route lookup wasn't found; {fail}.";
            int routeCount = _code[lookup + 4];
            _routeTable = ToFile(Word(_code, lookup + 20));
            if (_routeTable < 0 || _routeTable + routeCount * 4 > _code.Length) return "The roamer route table is outside arm9.";
            for (int i = 0; i < routeCount; i++) Routes.Add((int)Word(_code, _routeTable + i * 4));

            int adjUse = FindOne(_code, "?? 21 ?? 4A 41 43 50 5A 54 18 01 28");
            if (adjUse < 0) return $"The roamer adjacency table wasn't found; {fail}.";
            int entry = _code[adjUse];
            if (entry != 12 && entry != 14) return "The roamer adjacency table has an entry size DSPRE doesn't know.";
            AdjacencyMax = (entry - 2) / 2;
            _adjTable = ToFile(Word(_code, LiteralOf(adjUse + 2, _code)));
            if (_adjTable < 0 || _adjTable + routeCount * entry > _code.Length) return "The roamer adjacency table is outside arm9.";
            for (int i = 0; i < routeCount; i++)
            {
                int at = _adjTable + i * entry;
                int n = BitConverter.ToUInt16(_code, at);
                if (n < 1 || n > AdjacencyMax) return $"Route {i}'s adjacency count {n} isn't one DSPRE understands.";
                Adjacency.Add(Enumerable.Range(0, n).Select(k => (int)BitConverter.ToUInt16(_code, at + 2 + k * 2)).ToList());
            }

            if (_family == Family.HGSS)
            {
                int r = FindOne(_code, "01 98 01 28 02 D8 ?? 24 ?? 25 01 E0 ?? 24 ?? 25");
                if (r < 0) return $"The HGSS roamer region split wasn't found; {fail}.";
                Regions = new[] { (_code[r + 8], _code[r + 6]), (_code[r + 14], _code[r + 12]) }.Select(x => ((int)x.Item1, (int)x.Item2)).ToArray();
            }

            // Slots.
            string prologueSig = PrologueText(_family);
            int prologue = FindOne(_code, prologueSig);
            if (prologue < 0) return $"The roamer creation routine wasn't found; {fail}.";
            _switch = prologue + prologueSig.Split(' ').Length;
            if (!At(_code, _switch + layout.JoinOffset, JoinText(_family))) return $"The roamer creation routine doesn't hand species and level on as the game does; {fail}.";

            if (IsPatched(layout))
            {
                int count = _code[_switch] + 1;
                for (int i = 0; i < count; i++)
                {
                    int at = _switch + layout.TableOffset + i * 4;
                    Slots.Add(new Slot { Species = BitConverter.ToUInt16(_code, at), Level = BitConverter.ToUInt16(_code, at + 2) });
                }
            }
            else
            {
                var cases = CaseAddresses(layout);
                if (cases == null) return $"The roamer slot switch isn't the game's own; {fail}.";
                foreach (int c in cases)
                {
                    var slot = DecodeCase(c, layout);
                    if (slot == null) return $"A roamer slot at 0x{ARM9.address + c:X8} isn't coded the way DSPRE reads; {fail}.";
                    Slots.Add(slot);
                }
            }

            // After-battle reverse mapping.
            if (_family == Family.Plat)
            {
                int orig = FindOne(_code, PtReverseText());
                int patched = FindOne(_code, Hex(PtReversePatched));
                if (orig >= 0)
                {
                    _reverse = orig;
                    // Its five calls, in order: Mesprit (slot 0), Cresselia (1), Moltres (3), Zapdos (4), Articuno (5).
                    int[] calls = { 0x28, 0x30, 0x38, 0x40, 0x48 };
                    int[] slots = { 0, 1, 3, 4, 5 };
                    _setters = new int[6];
                    for (int k = 0; k < calls.Length; k++) _setters[slots[k]] = (int)(ARM9.address + BlTarget(orig + calls[k])) | 1;
                }
                else if (patched >= 0)
                {
                    _reverse = patched;
                    int setters = ToFile(Word(_code, patched + 0x2C));
                    _setters = Enumerable.Range(0, 6).Select(i => (int)Word(_code, setters + i * 4)).ToArray();
                }
                else return $"The routine that records a battled roamer wasn't found; {fail}.";
            }
            else if (_family == Family.HGSS)
            {
                int orig = FindOne(_code, HgReverseOriginal);
                int patched = FindOne(_code, Hex(HgReversePatched));
                _reverse = orig >= 0 ? orig : patched;
                if (_reverse < 0) return $"The routine that records a battled roamer wasn't found; {fail}.";
            }
            return null;
        }

        private static string PrologueText(Family f) => f switch
        {
            Family.Plat => "F0 B5 87 B0 04 91 07 1C ?? ?? ?? ?? 04 99 05 90 ?? ?? ?? ?? 06 1C 04 98",
            Family.HGSS => "F0 B5 87 B0 04 91 07 1C ?? ?? ?? ?? 04 99 05 90 ?? ?? ?? ?? 04 1C 04 98",
            _ => "F8 B5 88 B0 0D 1C 04 90 ?? ?? ?? ?? 29 1C 05 90 ?? ?? ?? ?? 04 1C",
        };

        private static string JoinText(Family f) => f switch
        {
            Family.Plat => "30 1C 04 21 22 1C ?? ?? ?? ?? 30 1C 06 21 2A 1C",
            Family.HGSS => "20 1C 04 21 32 1C ?? ?? ?? ?? 20 1C 06 21 2A 1C",
            _ => "20 1C 04 21 3A 1C ?? ?? ?? ?? 20 1C 06 21 32 1C",
        };

        private static string PtReverseText() =>
            "08 B5 13 4B 99 42 0A DC 0D DA 92 29 1E DC 90 29 1C DB 18 D0 91 29 12 D0 92 29 0C D0 08 BD DB 1D 99 42 04 D0 08 BD "
            + "11 1C ?? ?? ?? ?? 08 BD 11 1C ?? ?? ?? ?? 08 BD 11 1C ?? ?? ?? ?? 08 BD 11 1C ?? ?? ?? ?? 08 BD 11 1C ?? ?? ?? ?? 08 BD";

        private bool IsPatched(Layout layout)
        {
            for (int j = 1; j < layout.PatchedCode.Length; j++)
                if (_code[_switch + j] != layout.PatchedCode[j]) return false;
            return true;
        }

        // Thumb bl: two halfwords holding a 22-bit halfword offset from the call + 4.
        private int BlTarget(int at)
        {
            int hi = BitConverter.ToUInt16(_code, at), lo = BitConverter.ToUInt16(_code, at + 2);
            int offset = ((hi & 0x7FF) << 12) | ((lo & 0x7FF) << 1);
            if ((offset & 0x400000) != 0) offset -= 0x800000;
            return at + 4 + offset;
        }

        private List<int> CaseAddresses(Layout layout)
        {
            var cases = new List<int>();
            if (_family == Family.DP)
            {
                // cmp r5,#k; beq case, for k = 0, 1, 2, then b default.
                for (int k = 0; ; k++)
                {
                    int at = _switch + k * 4;
                    if (_code[at + 1] == 0xE0) break;
                    if (_code[at] != k || _code[at + 1] != 0x2D || _code[at + 3] != 0xD0) return null;
                    cases.Add(at + 2 + 4 + (sbyte)_code[at + 2] * 2);
                }
                return cases.Count > 0 ? cases : null;
            }
            // cmp r0,#n-1; bhi; adds; add r0,pc; ldrh; lsls; asrs; add pc,r0; then n s16 offsets from the add pc + 4.
            if (!At(_code, _switch + 1, "28 ?? D8 00 18 78 44 C0 88 00 04 00 14 87 44")) return null;
            int n = _code[_switch] + 1;
            int addPc = _switch + 0xE;
            for (int i = 0; i < n; i++) cases.Add(addPc + 4 + BitConverter.ToInt16(_code, _switch + 0x10 + i * 2));
            return cases;
        }

        // ldr Rs,[pc,#] or movs Rs,#imm [lsls Rs,Rs,#n]; movs Rl,#level; b join.
        private Slot DecodeCase(int at, Layout layout)
        {
            int species;
            int i = at;
            if (_code[i + 1] == (0x48 | layout.SpeciesReg)) species = (int)Word(_code, LiteralOf(i, _code));
            else if (_code[i + 1] == (0x20 | layout.SpeciesReg)) species = _code[i];
            else return null;
            i += 2;
            int lsl = BitConverter.ToUInt16(_code, i);
            if ((lsl & 0xF800) == 0 && (lsl & 7) == layout.SpeciesReg && ((lsl >> 3) & 7) == layout.SpeciesReg)
            {
                species <<= (lsl >> 6) & 31;
                i += 2;
            }
            if (_code[i + 1] != (0x20 | layout.LevelReg)) return null;
            int level = _code[i];
            if ((_code[i + 3] & 0xF8) != 0xE0) return null;
            return new Slot { Species = species, Level = level };
        }

        /// <summary>Why the values can't be saved, or null.</summary>
        public string Problem(int mapCount, int speciesCount)
        {
            for (int s = 0; s < Slots.Count; s++)
            {
                if (Slots[s].Species <= 0 || Slots[s].Species >= speciesCount) return $"Roamer {s + 1} needs a species.";
                if (Slots[s].Level < 1 || Slots[s].Level > 100) return $"Roamer {s + 1}'s level must be 1 to 100.";
            }
            // The battled roamer is found by its species.
            var dup = Slots.GroupBy(s => s.Species).FirstOrDefault(g => g.Count() > 1);
            if (dup != null) return "Two roamers can't be the same species: the game finds the one you battled by species.";
            for (int r = 0; r < Routes.Count; r++)
            {
                if (Routes[r] < 0 || Routes[r] >= mapCount) return $"Route {r + 1} points at a header that doesn't exist.";
                var adj = Adjacency[r];
                if (adj.Count < 1 || adj.Count > AdjacencyMax) return $"Route {r + 1} needs 1 to {AdjacencyMax} next routes.";
                if (adj.Any(a => a < 0 || a >= Routes.Count)) return $"Route {r + 1} has a next route that isn't in the list.";
                // The game rerolls until the map differs from the last, so a choice of one map would never end.
                if (adj.Count > 1 && adj.Select(a => Routes[a]).Distinct().Count() < 2) return $"Route {r + 1}'s next routes need at least two different maps.";
            }
            var regions = Regions ?? new[] { (0, Routes.Count) };
            // HGSS keeps each roamer in its region, so a next route has to be in the same one.
            int RegionOf(int r) => Array.FindIndex(regions, g => r >= g.Item1 && r < g.Item1 + g.Item2);
            for (int r = 0; r < Routes.Count; r++)
                if (Adjacency[r].Any(a => RegionOf(a) != RegionOf(r))) return $"Route {r + 1}'s next routes have to be in its own region.";
            foreach (var (start, count) in regions)
                if (Routes.Skip(start).Take(count).Distinct().Count() < 3) return "Each roaming area needs at least three different maps, or the game can't move a roamer.";
            return null;
        }

        /// <summary>Writes routes, adjacency and slots into arm9. Throws with the reason if the values can't be saved.</summary>
        public void Save(int mapCount, int speciesCount)
        {
            if (Problem(mapCount, speciesCount) is string p) throw new InvalidOperationException(p);
            var layout = Layouts[_family];
            byte[] code = (byte[])_code.Clone();

            for (int r = 0; r < Routes.Count; r++) BitConverter.GetBytes((uint)Routes[r]).CopyTo(code, _routeTable + r * 4);
            int entry = 2 + AdjacencyMax * 2;
            for (int r = 0; r < Routes.Count; r++)
            {
                int at = _adjTable + r * entry;
                int was = BitConverter.ToUInt16(code, at);
                BitConverter.GetBytes((ushort)Adjacency[r].Count).CopyTo(code, at);
                // Slots past the count keep whatever the game had there, unless an edit just emptied them.
                for (int k = 0; k < AdjacencyMax; k++)
                    if (k < Adjacency[r].Count) BitConverter.GetBytes((ushort)Adjacency[r][k]).CopyTo(code, at + 2 + k * 2);
                    else if (k < was) BitConverter.GetBytes((ushort)0xFFFF).CopyTo(code, at + 2 + k * 2);
            }

            // The slot switch becomes a table lookup in the same bytes; the default path after it stays put.
            var region = new byte[layout.SwitchLength];
            layout.PatchedCode.CopyTo(region, 0);
            region[0] = (byte)(Slots.Count - 1);
            for (int s = 0; s < Slots.Count; s++)
            {
                int at = layout.TableOffset + s * 4;
                if (at + 4 > region.Length) throw new InvalidOperationException("The roamer slots don't fit where the game keeps them.");
                BitConverter.GetBytes((ushort)Slots[s].Species).CopyTo(region, at);
                BitConverter.GetBytes((ushort)Slots[s].Level).CopyTo(region, at + 2);
            }
            region.CopyTo(code, _switch);
            uint tableRam = ARM9.address + (uint)(_switch + layout.TableOffset);

            if (_family == Family.Plat)
            {
                var fn = new byte[PtReverseLength];
                PtReversePatched.CopyTo(fn, 0);
                BitConverter.GetBytes(tableRam).CopyTo(fn, 0x28);
                BitConverter.GetBytes(ARM9.address + (uint)_reverse + 0x30).CopyTo(fn, 0x2C);
                for (int s = 0; s < 6; s++) BitConverter.GetBytes((uint)(s < _setters.Length ? _setters[s] : 0)).CopyTo(fn, 0x30 + s * 4);
                fn.CopyTo(code, _reverse);
            }
            else if (_family == Family.HGSS)
            {
                var fn = new byte[HgReverseLength];
                HgReversePatched.CopyTo(fn, 0);
                BitConverter.GetBytes(tableRam).CopyTo(fn, 0x18);
                fn.CopyTo(code, _reverse);
            }

            File.WriteAllBytes(arm9Path, code);
            _code = code;
        }
    }
}
