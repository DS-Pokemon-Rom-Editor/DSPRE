using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// HGSS Pokéathlon performance (a/1/6/9): one 20-byte record per species form.
    /// Stars are stored 0-4 for 1-5: base values at 0x00, minimum/maximum pairs from 0x09, in <see cref="Stats"/> order.
    /// </summary>
    public class PokeathlonPerformance
    {
        public const int RecordSize = 20, StatCount = 5, HighestStar = 4;
        public const int LastSpecies = 493, RecordCount = 554;

        public static readonly string[] Stats = { "Power", "Speed", "Jump", "Stamina", "Skill" };

        // Species with more than one record in retail, used when the game's own table can't be read.
        private static readonly Dictionary<int, int> FormRecords = new Dictionary<int, int>
        {
            [172] = 2, [201] = 28, [386] = 4, [412] = 3, [413] = 3, [422] = 2,
            [423] = 2, [479] = 6, [487] = 2, [492] = 2, [493] = 18,
        };

        public static int FormsOf(int species)
        {
            if (species < 1 || species > LastSpecies) return 0;
            var table = RomTable();
            if (table != null) return (species < LastSpecies ? table[species + 1] : RecordCount) - table[species];
            return FormRecords.TryGetValue(species, out int n) ? n : 1;
        }

        public static bool UsesRomTable() => RomTable() != null;

        private static ushort[] _table;
        private static string _tableKey;

        // The game's sPokeathlonPerformanceArcIdxs, or null when it can't be found or no longer looks like a member table.
        private static ushort[] RomTable()
        {
            int offset = PokeathlonMemberTableOffset;
            if (offset < 0 || string.IsNullOrEmpty(arm9Path) || !File.Exists(arm9Path)) return null;
            string key = arm9Path + "|" + File.GetLastWriteTimeUtc(arm9Path).Ticks;
            if (key == _tableKey) return _table;
            _tableKey = key;
            _table = null;
            if (ARM9.CheckCompressionMark()) return null;
            byte[] bytes = DSUtils.ReadFromFile(arm9Path, offset, (LastSpecies + 1) * 2);
            if (bytes == null || bytes.Length < (LastSpecies + 1) * 2) return null;
            var table = new ushort[LastSpecies + 1];
            for (int i = 0; i <= LastSpecies; i++) table[i] = BitConverter.ToUInt16(bytes, i * 2);
            if (table[1] != 0) return null;
            for (int s = 1; s < LastSpecies; s++)
                if (table[s + 1] <= table[s]) return null;
            if (table[LastSpecies] >= RecordCount) return null;
            return _table = table;
        }

        private static readonly Dictionary<int, string[]> FormLabels = new Dictionary<int, string[]>
        {
            [172] = new[] { "Normal", "Spiky-eared" },
            [201] = new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S",
                            "T", "U", "V", "W", "X", "Y", "Z", "!", "?" },
            [386] = new[] { "Normal", "Attack", "Defense", "Speed" },
            [412] = new[] { "Plant", "Sandy", "Trash" },
            [413] = new[] { "Plant", "Sandy", "Trash" },
            [422] = new[] { "West Sea", "East Sea" },
            [423] = new[] { "West Sea", "East Sea" },
            [479] = new[] { "Normal", "Heat", "Wash", "Frost", "Fan", "Mow" },
            [487] = new[] { "Altered", "Origin" },
            [492] = new[] { "Land", "Sky" },
            [493] = new[] { "Normal", "Fighting", "Flying", "Poison", "Ground", "Rock", "Bug", "Ghost", "Steel", "???",
                            "Fire", "Water", "Grass", "Electric", "Psychic", "Ice", "Dragon", "Dark" },
        };

        public static string[] FormNamesOf(int species)
        {
            int forms = FormsOf(species);
            if (FormLabels.TryGetValue(species, out var names) && names.Length == forms) return names;
            return forms == 1 ? new[] { "Normal" } : Enumerable.Range(0, forms).Select(f => f == 0 ? "Normal" : $"Form {f}").ToArray();
        }

        /// <summary>The archive member for a species form, or -1.</summary>
        public static int MemberOf(int species, int form)
        {
            if (form < 0 || form >= FormsOf(species)) return -1;
            var table = RomTable();
            if (table != null) return table[species] + form;
            int member = species - 1;
            foreach (var kv in FormRecords)
                if (kv.Key < species) member += kv.Value - 1;
            return member + form;
        }

        public int Member { get; }
        private readonly byte[] _data;

        public PokeathlonPerformance(int member, byte[] data)
        {
            if (data == null || data.Length < RecordSize)
                throw new InvalidDataException($"Pokéathlon record {member} is {data?.Length ?? 0} bytes; it needs {RecordSize}.");
            Member = member;
            _data = (byte[])data.Clone();
        }

        public byte Base(int stat) => _data[stat];
        public byte Min(int stat) => _data[9 + stat * 2];
        public byte Max(int stat) => _data[10 + stat * 2];
        public void SetBase(int stat, byte value) => _data[stat] = value;
        public void SetMin(int stat, byte value) => _data[9 + stat * 2] = value;
        public void SetMax(int stat, byte value) => _data[10 + stat * 2] = value;

        // Read only by the Pokéathlon overlay (96).
        // Raised: nonzero puts a sprite above the Pokémon 24 px up instead of 16.
        // Hitbox: 1-3 picks entry 0-2 of a per-event collision circle member in a/1/7/0.
        // Lift: 1-3 raises a sprite 8, 8 or 16 px.
        // Offset: 1-3 is 3, 4 or 5 px between the Pokémon's bottom edge and the two sprites drawn with it;
        // one event raises a sprite 20, 32 or 40 px instead.
        public byte Raised { get => _data[5]; set => _data[5] = value; }
        public byte Hitbox { get => _data[6]; set => _data[6] = value; }
        public byte Lift { get => _data[7]; set => _data[7] = value; }
        public byte Offset { get => _data[8]; set => _data[8] = value; }

        public byte[] ToBytes() => (byte[])_data.Clone();

        /// <summary>Why a record would misbehave in game, or null.</summary>
        public string Problem()
        {
            for (int s = 0; s < StatCount; s++)
                if (!(Min(s) <= Base(s) && Base(s) <= Max(s) && Max(s) <= HighestStar))
                    return $"{Stats[s]} needs minimum ≤ base ≤ maximum, all from 1 to 5 stars.";
            // The event code asserts on anything but its three types.
            if (Hitbox is < 1 or > 3 || Lift is < 1 or > 3 || Offset is < 1 or > 3)
                return "Hitbox, Lift and Offset must each be one of their three choices.";
            return null;
        }

        public static string WhyNot() =>
            gameFamily != GameFamilies.HGSS ? "Pokéathlon stats exist only in HeartGold and SoulSilver."
            : !gameDirs.ContainsKey(DirNames.pokeathlonPerformance) ? "This ROM has no Pokéathlon archive."
            : null;

        private static string PathOf(int member)
        {
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.pokeathlonPerformance });
            return Path.Combine(gameDirs[DirNames.pokeathlonPerformance].unpackedDir, member.ToString("D4"));
        }

        public static PokeathlonPerformance Read(int member)
        {
            string path = PathOf(member);
            return File.Exists(path) ? new PokeathlonPerformance(member, File.ReadAllBytes(path)) : null;
        }

        public void Write()
        {
            string problem = Problem();
            if (problem != null) throw new InvalidDataException(problem);
            string path = PathOf(Member);
            // Keep anything past the 20 bytes the game reads.
            byte[] onDisk = File.Exists(path) ? File.ReadAllBytes(path) : new byte[RecordSize];
            if (onDisk.Length < RecordSize) onDisk = new byte[RecordSize];
            _data.AsSpan(0, RecordSize).CopyTo(onDisk);
            File.WriteAllBytes(path, onDisk);
        }
    }
}
