using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The Pokédex habitat data: per species, lists of the areas it is found in, each ending in 0. List k of species s
    /// is member (header members) + k * (species count) + s.
    /// HGSS (a/1/3/3): two members place the areas on the dex map, then eight lists: special areas by morning, day and
    /// night, routes and towns by morning, day and night, then the special areas and the routes and towns where it
    /// appears by other means.
    /// DP and Pt (zukan_enc_*.narc): four members (dungeon positions, the map header of each dungeon, field shapes, the
    /// map header of each field), then ten lists: dungeons by morning, day, night, other means and other means after
    /// the National Dex, then the same five for fields. Area 0 is unused in both groups.
    /// </summary>
    public sealed class PokedexAreaData
    {
        /// <summary>HGSS's list count, which hg-engine's PokedexArea.c shares.</summary>
        public const int ListCount = 8;

        /// <summary>The HGSS list index for an area group (0 special areas, 1 routes and towns) and time (0 morning,
        /// 1 day, 2 night, 3 other).</summary>
        public static int ListOf(int group, int time) => time == 3 ? 6 + group : group * 3 + time;

        /// <summary>Times per group: morning, day, night, other, plus other after the National Dex on DP and Pt.</summary>
        public int Times => _dppt ? 5 : 4;

        public int Lists => _dppt ? 10 : ListCount;

        /// <summary>The list index for an area group (0 special areas or dungeons, 1 routes, towns or fields) and time.</summary>
        public int List(int group, int time) => _dppt ? group * 5 + time : ListOf(group, time);

        // The dex map's area ids, special areas and routes and towns numbered separately from 1.
        public static readonly string[] SpecialAreaNames =
        {
            "Sprout Tower", "Ruins of Alph", "Union Cave", "Slowpoke Well", "Ilex Forest", "National Park", "Burned Tower",
            "Bell Tower", "Whirl Islands", "Mt. Mortar", "Ice Path", "Dragon's Den", "Dark Cave", "Mt. Moon",
            "Seafoam Islands", "Mt. Silver Cave", "Cliff Edge Gate", "Cliff Cave", "Olivine City (special)", "Rock Tunnel",
            "Victory Road", "Tohjo Falls", "Diglett's Cave", "Viridian Forest", "Cerulean Cave",
        };

        public static readonly string[] RouteAreaNames =
        {
            "New Bark Town", "Route 29", "Cherrygrove City", "Route 30", "Route 31", "Violet City", "Route 32", "Route 33",
            "Route 34", "Route 35", "Route 36", "Route 37", "Ecruteak City", "Route 38", "Route 39", "Olivine City",
            "Route 40", "Route 41", "Cianwood City", "Route 42", "Route 43", "Lake of Rage", "Route 44", "Blackthorn City",
            "Route 45", "Route 46", "Route 47", "Mt. Silver", "Route 12", "Route 19", "Route 20", "Pallet Town",
            "Viridian City", "Cerulean City", "Vermilion City", "Celadon City", "Fuchsia City", "Cinnabar Island",
            "Route 48", "Route 26", "Route 27", "Route 28", "Route 1", "Route 2", "Route 3", "Route 4", "Route 5",
            "Route 6", "Route 7", "Route 8", "Route 9", "Route 10", "Route 11", "Route 13", "Route 14", "Route 15",
            "Route 16", "Route 17", "Route 18", "Route 21", "Route 22", "Route 24", "Route 25", "Route 2 (second part)",
            "Pewter City", "Azalea Town", "Safari Zone Gate", "Route 16 (second part)",
        };

        private readonly string _folder;
        private readonly bool _dppt;
        private int HeaderMembers => _dppt ? 4 : 2;

        /// <summary>Species 0 to <see cref="SpeciesCount"/> - 1 have lists.</summary>
        public int SpeciesCount { get; }

        private PokedexAreaData(string folder, bool dppt, int species) { _folder = folder; _dppt = dppt; SpeciesCount = species; }

        public static bool TryLoad(out PokedexAreaData data, out string error)
        {
            data = null;
            error = null;
            if (GameDirs == null || !GameDirs.ContainsKey(DirNames.PokedexAreas))
            { error = "This game has no Pokédex area archive."; return false; }
            bool dppt = GameFamily != GameFamilies.HGSS;
            int header = dppt ? 4 : 2, lists = dppt ? 10 : ListCount;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.PokedexAreas });
            string folder = GameDirs[DirNames.PokedexAreas].unpackedDir;
            int members = NarcAPI.Narc.MemberCount(folder);
            if (members < header + lists || (members - header) % lists != 0)
            { error = $"The Pokédex area archive has {members} members, which isn't {header} plus {lists} lists per species."; return false; }
            data = new PokedexAreaData(folder, dppt, (members - header) / lists);
            return true;
        }

        private string MemberPath(int list, int species) => Path.Combine(_folder, (HeaderMembers + list * SpeciesCount + species).ToString("D4"));

        /// <summary>
        /// Each group's areas as (id, name). HGSS names come from the dex map's fixed order; DP and Pt name each area
        /// after the location of the map header the archive gives it, with the header's own name added where two
        /// areas share a place.
        /// </summary>
        public List<(int Id, string Name)>[] Areas()
        {
            List<(int Id, string Name)>[] groups = new[] { new List<(int Id, string Name)>(), new List<(int Id, string Name)>() };
            if (!_dppt)
            {
                groups[0].AddRange(SpecialAreaNames.Select((n, i) => (i + 1, n)));
                groups[1].AddRange(RouteAreaNames.Select((n, i) => (i + 1, n)));
                return groups;
            }
            List<string> names = HeaderLists.GetHeaderListBoxNames();
            for (int g = 0; g < 2; g++)
            {
                byte[] b = File.ReadAllBytes(Path.Combine(_folder, (1 + g * 2).ToString("D4")));
                List<(int Id, string Place, string Label)> named = new List<(int Id, string Place, string Label)>();
                for (int id = 1; id * 4 + 4 <= b.Length; id++)
                {
                    int header = (int)BitConverter.ToUInt32(b, id * 4);
                    string place = "", label = $"Map {header}";
                    try { place = HeaderLabels.LocationNameOf(MapHeader.GetMapHeader((ushort)header)); } catch { }   // a header that can't be read is listed by its number
                    if (names != null && header < names.Count)
                    {
                        string n = names[header].Replace("\0", "").Trim();
                        int sep = n.IndexOf(MapHeader.NameSeparator, StringComparison.Ordinal);
                        label = sep >= 0 ? n.Substring(sep + MapHeader.NameSeparator.Length).Trim() : n;
                    }
                    named.Add((id, place, label));
                }
                foreach ((int id, string place, string label) in named)
                {
                    bool shared = string.IsNullOrWhiteSpace(place) || named.Count(n => n.Place == place) > 1;
                    groups[g].Add((id, string.IsNullOrWhiteSpace(place) ? label : shared ? $"{place} ({label})" : place));
                }
            }
            return groups;
        }

        /// <summary>The species' area lists, without their ends.</summary>
        public int[][] Get(int species)
        {
            int[][] lists = new int[Lists][];
            for (int k = 0; k < Lists; k++)
            {
                byte[] b = File.ReadAllBytes(MemberPath(k, species));
                List<int> ids = new List<int>();
                for (int i = 0; i + 4 <= b.Length; i += 4)
                {
                    int id = (int)BitConverter.ToUInt32(b, i);
                    if (id == 0) break;
                    ids.Add(id);
                }
                lists[k] = ids.ToArray();
            }
            return lists;
        }

        /// <summary>Writes the lists that differ from what the archive holds.</summary>
        public void Set(int species, int[][] lists)
        {
            if (species <= 0 || species >= SpeciesCount) throw new ArgumentOutOfRangeException(nameof(species));
            if (lists.Length != Lists) throw new ArgumentException($"Expected {Lists} lists.", nameof(lists));
            int[][] now = Get(species);
            for (int k = 0; k < Lists; k++)
            {
                if (now[k].SequenceEqual(lists[k])) continue;
                // DP and Pt keep the lists sorted, which is how the game's own build writes them.
                IEnumerable<int> ids = _dppt ? lists[k].OrderBy(i => i) : (IEnumerable<int>)lists[k];
                ArchiveMembers.Write(MemberPath(k, species), ids.Append(0).SelectMany(id => BitConverter.GetBytes((uint)id)).ToArray());
            }
        }
    }
}
