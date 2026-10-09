using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The Pokédex's species lists in the Pokédex data archives, from member 11 on: national and regional order,
    /// alphabetical, heaviest to shortest, the search's letter, type and body shape lists. The regional order is also
    /// kept as each species' regional number and, in DP and Platinum, as the species at each regional number; those
    /// files are written with it. Lists change only when edited here or when <see cref="Rebuild"/> is asked for,
    /// which builds every list but the regional one the way the game's data was built (pokeplatinum
    /// tools/dataproc/src/speciesproc.c pack_dexdata, pokeheartgold zukan_data.json).
    /// </summary>
    public sealed class PokedexLists
    {
        public enum Group { Order, Letter, LetterGroup, Type, BodyShape, Unused }

        public sealed class ListInfo
        {
            public int Member { get; init; }
            public string Name { get; init; }
            public Group Group { get; init; }
            /// <summary>The type or body shape number, or the letter, the list is for.</summary>
            public int Key { get; init; }
        }

        public const int National = 11, Regional = 12, Alphabetical = 13, Heaviest = 14, Lightest = 15, Tallest = 16, Shortest = 17;
        private const int Mystery = 9, Dark = 17, BodyShapes = 14;
        private static readonly string[] LetterGroups = { "ABC", "DEF", "GHI", "JKL", "MNO", "PQR", "STU", "VWX", "YZ" };

        public static string[] BodyShapeNames { get; } =
        {
            "Quadruped", "Bipedal, tailless", "Bipedal, tailed", "Serpentine", "Two pairs of wings", "One pair of wings",
            "Insectoid", "Head and body", "Head and arms", "Head and legs", "Tentacles", "Fins", "Head only", "Multiple bodies",
        };

        private int MemberCount => gameFamily == GameFamilies.HGSS ? 102 : 58;
        // HGSS adds a list per letter (and 18 empty ones) before its types and puts the letter groups last.
        private int TypesFrom => gameFamily == GameFamilies.HGSS ? 62 : 27;
        private int ShapesFrom => gameFamily == GameFamilies.HGSS ? 79 : 44;
        private int GroupsFrom => gameFamily == GameFamilies.HGSS ? 93 : 18;

        private sealed class Copy
        {
            public DirNames Dir;
            public string Folder;
            public byte[][] Members;
            public List<ushort>[] Lists;
        }

        private readonly List<Copy> _copies = new();
        public int SpeciesCount { get; private set; }

        /// <summary>Whether the copy with Altered Giratina is a separate archive; its lists can differ from the main one.</summary>
        public bool HasAlteredCopy => _copies.Count > 1;

        public IReadOnlyList<ListInfo> Lists { get; private set; }

        /// <summary>Why the lists can't be edited in this project, or null.</summary>
        public static string WhyNot()
        {
            if (gameDirs == null || !gameDirs.ContainsKey(DirNames.pokedexData)) return "This game's Pokédex data isn't known to DSPRE.";
            if (HgEngine.HgEngineProject.IsActive) return "hg-engine builds these lists from data/PokedexSort.c, which the Pokédex data tab keeps up to date.";
            return null;
        }

        public static bool TryLoad(out PokedexLists lists, out string error)
        {
            lists = null;
            error = WhyNot();
            if (error != null) return false;
            PokedexLists l = new PokedexLists();
            List<DirNames> dirs = new List<DirNames> { DirNames.pokedexData };
            if (gameDirs.ContainsKey(DirNames.pokedexDataAltered)) dirs.Add(DirNames.pokedexDataAltered);
            DSUtils.TryUnpackNarcs(dirs);
            foreach (DirNames dir in dirs)
            {
                string folder = gameDirs[dir].unpackedDir;
                byte[][] members = new byte[l.MemberCount][];
                for (int i = 0; i < members.Length; i++)
                {
                    string path = Path.Combine(folder, i.ToString("D4"));
                    if (!File.Exists(path)) { error = $"The Pokédex data is missing member {i}."; return false; }
                    members[i] = File.ReadAllBytes(path);
                }
                if (File.Exists(Path.Combine(folder, l.MemberCount.ToString("D4"))))
                { error = $"The Pokédex data has more than the game's {l.MemberCount} members, so its layout isn't the one DSPRE knows."; return false; }
                Copy c = new Copy { Dir = dir, Folder = folder, Members = members, Lists = new List<ushort>[members.Length] };
                for (int i = National; i < members.Length; i++) c.Lists[i] = ToList(members[i]);
                l._copies.Add(c);
            }
            l.SpeciesCount = l._copies[0].Members[0].Length / 4;
            l.Lists = l.BuildLayout();
            lists = l;
            return true;
        }

        private static List<ushort> ToList(byte[] b) => Enumerable.Range(0, b.Length / 2).Select(i => BitConverter.ToUInt16(b, i * 2)).ToList();
        private static byte[] ToBytes(IEnumerable<ushort> l) => l.SelectMany(BitConverter.GetBytes).ToArray();

        private List<ListInfo> BuildLayout()
        {
            string[] typeNames;
            try { typeNames = GetTypeNames(); } catch (Exception ex) when (ex is IOException || ex is InvalidDataException) { typeNames = new string[0]; }
            List<ListInfo> l = new List<ListInfo>
            {
                new ListInfo { Member = National, Name = "National order", Group = Group.Order },
                new ListInfo { Member = Regional, Name = "Regional order", Group = Group.Order },
                new ListInfo { Member = Alphabetical, Name = "Alphabetical", Group = Group.Order },
                new ListInfo { Member = Heaviest, Name = "Heaviest first", Group = Group.Order },
                new ListInfo { Member = Lightest, Name = "Lightest first", Group = Group.Order },
                new ListInfo { Member = Tallest, Name = "Tallest first", Group = Group.Order },
                new ListInfo { Member = Shortest, Name = "Shortest first", Group = Group.Order },
            };
            if (gameFamily == GameFamilies.HGSS)
            {
                for (int i = 0; i < 26; i++)
                    l.Add(new ListInfo { Member = 18 + i, Name = "Letter " + (char)('A' + i), Group = Group.Letter, Key = 'A' + i });
                for (int i = 26; i < 44; i++)
                    l.Add(new ListInfo { Member = 18 + i, Name = "Unused " + (i - 25), Group = Group.Unused });
            }
            for (int i = 0; i < LetterGroups.Length; i++)
                l.Add(new ListInfo { Member = GroupsFrom + i, Name = "Letters " + LetterGroups[i], Group = Group.LetterGroup, Key = LetterGroups[i][^1] });
            for (int type = 0, n = 0; type <= Dark; type++)
            {
                if (type == Mystery) continue;
                string name = type < typeNames.Length && !string.IsNullOrWhiteSpace(typeNames[type]) ? typeNames[type] : "Type " + type;
                l.Add(new ListInfo { Member = TypesFrom + n++, Name = name, Group = Group.Type, Key = type });
            }
            for (int s = 0; s < BodyShapes; s++)
                l.Add(new ListInfo { Member = ShapesFrom + s, Name = BodyShapeNames[s], Group = Group.BodyShape, Key = s });
            return l.OrderBy(i => i.Member).ToList();
        }

        /// <summary>A list as the chosen copy holds it.</summary>
        public List<ushort> Get(int member, bool altered = false) => CopyOf(altered).Lists[member];

        /// <summary>Whether the two copies hold the list differently, so each is edited on its own.</summary>
        public bool DiffersByCopy(int member) => HasAlteredCopy && !_copies[0].Lists[member].SequenceEqual(_copies[1].Lists[member]);

        private Copy CopyOf(bool altered) => altered && HasAlteredCopy ? _copies[1] : _copies[0];

        /// <summary>Replaces a list; in both copies unless they already held it differently.</summary>
        public void Set(int member, IEnumerable<ushort> list, bool altered = false)
        {
            if (member < National || member >= MemberCount) throw new ArgumentOutOfRangeException(nameof(member));
            List<ushort> values = list.ToList();
            if (DiffersByCopy(member)) CopyOf(altered).Lists[member] = values;
            else foreach (Copy c in _copies) c.Lists[member] = values.ToList();
        }

        /// <summary>The regional dex, first entry first.</summary>
        public List<ushort> RegionalOrder => _copies[0].Lists[Regional];

        public void SetRegionalOrder(IEnumerable<ushort> order) => Set(Regional, order);

        /// <summary>
        /// Builds every list but the regional order from the species' names and types and each copy's heights,
        /// weights and body shapes, as the game's own data was built.
        /// </summary>
        public void Rebuild(string[] names, Func<int, (int, int)> types)
        {
            int[] species = Enumerable.Range(1, SpeciesCount - 1).ToArray();
            string Name(int s) => s < names.Length ? (names[s] ?? "").ToUpperInvariant() : "";
            // Byte order, as the data tools sort: this reproduces the retail lists of all three games.
            int[] byName = species.OrderBy(Name, StringComparer.Ordinal).ToArray();
            foreach (Copy c in _copies)
            {
                byte[][] m = c.Members;
                uint H(int s) => BitConverter.ToUInt32(m[0], s * 4);
                uint W(int s) => BitConverter.ToUInt32(m[1], s * 4);
                List<ushort> L(IEnumerable<int> e) => e.Select(s => (ushort)s).ToList();

                c.Lists[National] = L(species);
                c.Lists[Alphabetical] = L(byName);
                c.Lists[Heaviest] = L(species.OrderByDescending(W).ThenBy(s => s));
                c.Lists[Lightest] = L(species.OrderBy(W).ThenBy(s => s));
                c.Lists[Tallest] = L(species.OrderByDescending(H).ThenBy(s => s));
                c.Lists[Shortest] = L(species.OrderBy(H).ThenBy(s => s));
                foreach (ListInfo info in Lists)
                {
                    switch (info.Group)
                    {
                        case Group.Letter:
                            // HGSS's letter lists put Nidoran♀ before Nidoran♂, the other way round from its alphabetical
                            // list (pokeheartgold zukan_data.json agrees), so the two gender signs swap places here.
                            c.Lists[info.Member] = L(species.Where(s => Name(s).Length > 0 && Name(s)[0] == info.Key)
                                                            .OrderBy(s => SwapGenderSigns(Name(s)), StringComparer.Ordinal));
                            break;
                        case Group.Unused:
                            c.Lists[info.Member] = new List<ushort>();
                            break;
                        case Group.LetterGroup:
                            char first = (char)(info.Key - LetterGroups[info.Member - GroupsFrom].Length + 1);
                            c.Lists[info.Member] = L(byName.Where(s => Name(s).Length > 0 && Name(s)[0] >= first && Name(s)[0] <= info.Key));
                            break;
                        case Group.Type:
                            c.Lists[info.Member] = L(species.Where(s => { (int a, int b) = types(s); return a == info.Key || b == info.Key; }));
                            break;
                        case Group.BodyShape:
                            c.Lists[info.Member] = L(species.Where(s => m[2][s] == info.Key));
                            break;
                    }
                }
            }
        }

        /// <summary>Every list of every copy as bytes, for undo and for telling whether anything changed.</summary>
        public byte[] Snapshot()
        {
            using MemoryStream ms = new MemoryStream();
            using BinaryWriter w = new BinaryWriter(ms);
            foreach (Copy c in _copies)
                for (int i = National; i < c.Lists.Length; i++)
                {
                    w.Write(c.Lists[i].Count);
                    foreach (ushort s in c.Lists[i]) w.Write(s);
                }
            w.Flush();
            return ms.ToArray();
        }

        public void Restore(byte[] snapshot)
        {
            using BinaryReader r = new BinaryReader(new MemoryStream(snapshot));
            foreach (Copy c in _copies)
                for (int i = National; i < c.Lists.Length; i++)
                {
                    int n = r.ReadInt32();
                    List<ushort> l = new List<ushort>(n);
                    for (int k = 0; k < n; k++) l.Add(r.ReadUInt16());
                    c.Lists[i] = l;
                }
        }

        private static string SwapGenderSigns(string s)
        {
            char[] c = s.ToCharArray();
            for (int i = 0; i < c.Length; i++) c[i] = c[i] == '♂' ? '♀' : c[i] == '♀' ? '♂' : c[i];
            return new string(c);
        }

        /// <summary>Why the lists can't be saved as they are, or null.</summary>
        public string Problem()
        {
            foreach (Copy c in _copies)
                foreach (ListInfo info in Lists)
                {
                    List<ushort> l = c.Lists[info.Member];
                    if (l.Any(s => s == 0 || s >= SpeciesCount)) return $"{info.Name} holds a species number this game doesn't have.";
                    if (l.Distinct().Count() != l.Count) return $"{info.Name} holds a species twice.";
                }
            // The GTS and Battle Video pickers index the alphabetical list at fixed places up to the last species,
            // so a shorter list would be read past its end.
            foreach (Copy c in _copies)
                if (c.Lists[Alphabetical].Count != SpeciesCount - 1)
                    return $"Alphabetical must list every species ({SpeciesCount - 1}); the GTS and Battle Video species pickers read all of it.";
            return null;
        }

        // Where the GTS and Battle Video species pickers split the alphabetical list (ABC, DEF ... YZ), in all three
        // games (pokeplatinum ov94 sAlphabeticalSpeciesCharpadIndices and its ov62 copies).
        private static readonly int[] PickerSplits = { 87, 136, 191, 235, 303, 360, 457, 487, 493 };

        /// <summary>
        /// A note when the letter groups no longer line up with the pickers' fixed split, so some species would show
        /// under a neighbouring group there; null when they line up.
        /// </summary>
        public string Warning()
        {
            if (SpeciesCount - 1 != PickerSplits[^1]) return null;
            int total = 0;
            List<string> off = new List<string>();
            for (int g = 0; g < LetterGroups.Length; g++)
            {
                total += _copies[0].Lists[GroupsFrom + g].Count;
                if (total != PickerSplits[g]) off.Add(LetterGroups[g]);
            }
            return off.Count == 0 ? null
                : "The GTS and Battle Video species pickers split the alphabetical list at the game's own places, and the "
                  + "letter groups no longer end there (" + string.Join(", ", off) + "), so some species show under a "
                  + "neighbouring group in those pickers.";
        }

        /// <summary>Writes every list that changed, and the regional number files, into the unpacked archives.</summary>
        public void Save()
        {
            if (Problem() is string p) throw new InvalidOperationException(p);
            foreach (Copy c in _copies)
                for (int i = National; i < c.Members.Length; i++)
                {
                    byte[] bytes = ToBytes(c.Lists[i]);
                    if (bytes.AsSpan().SequenceEqual(c.Members[i])) continue;
                    File.WriteAllBytes(Path.Combine(c.Folder, i.ToString("D4")), bytes);
                    c.Members[i] = bytes;
                }
            SaveRegionalNumbers();
        }

        private void SaveRegionalNumbers()
        {
            List<ushort> order = RegionalOrder;
            List<DirNames> dirs = new[] { DirNames.regionalDexNumbers, DirNames.regionalDexSpecies }.Where(gameDirs.ContainsKey).ToList();
            DSUtils.TryUnpackNarcs(dirs);

            if (gameDirs.ContainsKey(DirNames.regionalDexNumbers))
            {
                string path = Path.Combine(gameDirs[DirNames.regionalDexNumbers].unpackedDir, "0000");
                if (File.Exists(path))
                {
                    // One entry per species; species past the end keep whatever the file holds.
                    ushort[] numbers = new ushort[Math.Max(new FileInfo(path).Length / 2, SpeciesCount)];
                    for (int i = 0; i < order.Count; i++) numbers[order[i]] = (ushort)(i + 1);
                    WriteIfChanged(path, ToBytes(numbers));
                }
            }
            if (gameDirs.ContainsKey(DirNames.regionalDexSpecies))
            {
                string path = Path.Combine(gameDirs[DirNames.regionalDexSpecies].unpackedDir, "0000");
                if (File.Exists(path))
                {
                    // The game reads up to the regional count its code holds, which can be more than a shortened
                    // order until the count patch is applied, so the file never shrinks; spare entries read as none.
                    int length = Math.Max(order.Count + 1, (int)(new FileInfo(path).Length / 2));
                    ushort[] species = new ushort[length];
                    for (int i = 0; i < order.Count; i++) species[i + 1] = order[i];
                    WriteIfChanged(path, ToBytes(species));
                }
            }
        }

        private static void WriteIfChanged(string path, byte[] bytes)
        {
            if (File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) return;
            File.WriteAllBytes(path, bytes);
        }

        /// <summary>The regional files' own view of the order, for checking they agree with the list.</summary>
        public (ushort[] Numbers, ushort[] Species) ReadRegionalFiles()
        {
            ushort[] Read(DirNames d)
            {
                if (!gameDirs.ContainsKey(d)) return null;
                DSUtils.TryUnpackNarcs(new List<DirNames> { d });
                string path = Path.Combine(gameDirs[d].unpackedDir, "0000");
                return File.Exists(path) ? ToList(File.ReadAllBytes(path)).ToArray() : null;
            }
            return (Read(DirNames.regionalDexNumbers), Read(DirNames.regionalDexSpecies));
        }
    }
}
