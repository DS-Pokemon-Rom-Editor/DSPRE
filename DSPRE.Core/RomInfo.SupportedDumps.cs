using System.Collections.Generic;
using System.Linq;

namespace DSPRE
{
    public partial class RomInfo
    {
        /// <summary>
        /// The European English revisions and the US revisions their arm9 and overlays are byte-identical to: Diamond and
        /// Pearl (13), Platinum, HeartGold and SoulSilver (10). Used to list and name supported dumps.
        /// </summary>
        private static readonly Dictionary<(string id, int rev), (string id, int rev)> CodeTwins = new()
        {
            [("ADAE", 13)] = ("ADAE", 5), [("APAE", 13)] = ("APAE", 5), [("CPUE", 10)] = ("CPUE", 1),
            [("IPKE", 10)] = ("IPKE", 0), [("IPGE", 10)] = ("IPGE", 0),
        };

        public static GameFamilies FamilyOfCode(string id) =>
            id.StartsWith("ADA") || id.StartsWith("APA") ? GameFamilies.DP : id.StartsWith("CPU") ? GameFamilies.Plat : id.StartsWith("IP") ? GameFamilies.HGSS : GameFamilies.NULL;

        public static GameLanguages LanguageOfCode(string id) => id[3] switch
        {
            'J' => GameLanguages.Japanese, 'I' => GameLanguages.Italian, 'S' => GameLanguages.Spanish, 'F' => GameLanguages.French,
            'D' => GameLanguages.German, 'K' => GameLanguages.Korean, _ => GameLanguages.English,
        };

        /// <summary>Every dump DSPRE's offsets were measured on, European English included.</summary>
        public static IReadOnlyList<(string id, int rev)> KnownDumps { get; } = new List<(string id, int rev)>
        {
            ("ADAD", 5), ("ADAE", 5), ("ADAE", 13), ("ADAF", 5), ("ADAI", 5), ("ADAJ", 0), ("ADAK", 0), ("ADAS", 5),
            ("APAD", 5), ("APAE", 5), ("APAE", 13), ("APAF", 5), ("APAI", 5), ("APAJ", 0), ("APAJ", 5), ("APAJ", 6), ("APAK", 0), ("APAS", 5),
            ("CPUD", 0), ("CPUE", 1), ("CPUE", 10), ("CPUF", 0), ("CPUI", 0), ("CPUJ", 0), ("CPUK", 0), ("CPUS", 0),
            ("IPGD", 0), ("IPGE", 0), ("IPGE", 10), ("IPGF", 0), ("IPGI", 0), ("IPGJ", 0), ("IPGK", 0), ("IPGS", 0),
            ("IPKD", 0), ("IPKE", 0), ("IPKE", 10), ("IPKF", 0), ("IPKI", 0), ("IPKJ", 0), ("IPKK", 0), ("IPKS", 0),
        };

        /// <summary>The dumps plus every revision that is byte-identical to one of them.</summary>
        public static List<(string id, int rev)> WithTwins(IEnumerable<(string id, int rev)> dumps)
        {
            List<(string id, int rev)> all = dumps.Distinct().ToList();
            all.AddRange(CodeTwins.Where(t => all.Contains(t.Value) && !all.Contains(t.Key)).Select(t => t.Key));
            return all;
        }

        private static readonly (string Prefix, string Name)[] DumpGames =
            { ("ADA", "Diamond"), ("APA", "Pearl"), ("CPU", "Platinum"), ("IPK", "HeartGold"), ("IPG", "SoulSilver") };

        private static readonly (char Region, string Name)[] DumpLanguages =
            { ('E', "US"), ('P', "European English"), ('D', "German"), ('F', "French"), ('I', "Italian"), ('S', "Spanish"), ('J', "Japanese"), ('K', "Korean") };

        /// <summary>
        /// Names dumps by game and language, games with the same languages together:
        /// "Diamond and Pearl (US, European English); Platinum (US)". European English shares the US game code and is
        /// told apart by its revision.
        /// </summary>
        public static string DescribeDumps(IEnumerable<(string id, int rev)> dumps)
        {
            List<(string id, int rev)> list = dumps.Where(d => d.id != null && d.id.Length == 4).ToList();
            char RegionOf((string id, int rev) d) => d.id[3] == 'E' && CodeTwins.ContainsKey(d) ? 'P' : d.id[3];
            List<(string Game, string Languages)> games = new();
            foreach ((string prefix, string name) in DumpGames)
            {
                List<char> regions = list.Where(d => d.id.StartsWith(prefix)).Select(RegionOf).Distinct().ToList();
                if (regions.Count == 0) continue;
                games.Add((name, string.Join(", ", DumpLanguages.Where(l => regions.Contains(l.Region)).Select(l => l.Name))));
            }
            List<string> parts = new();
            for (int i = 0; i < games.Count; i++)
            {
                List<string> same = new() { games[i].Game };
                while (i + 1 < games.Count && games[i + 1].Languages == games[i].Languages) same.Add(games[++i].Game);
                string names = same.Count == 1 ? same[0] : string.Join(", ", same.Take(same.Count - 1)) + " and " + same[^1];
                parts.Add($"{names} ({games[i].Languages})");
            }
            return string.Join("; ", parts);
        }
    }
}
