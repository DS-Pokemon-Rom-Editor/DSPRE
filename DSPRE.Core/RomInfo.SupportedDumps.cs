using System.Collections.Generic;
using System.Linq;

namespace DSPRE
{
    public partial class RomInfo
    {
        // The dumps each site lookup answers for, from the tables the lookups read. Code-site tables go by SiteKey, where
        // SoulSilver US reads HeartGold US's entries.

        private static List<(string id, int rev)> SiteDumps(IEnumerable<(string id, int rev)> bySite, IEnumerable<(string id, int rev)> byCode)
        {
            List<(string id, int rev)> site = bySite.ToList();
            if (site.Contains(("IPKE", 0))) site.Add(("IPGE", 0));
            return WithTwins(site.Concat(byCode));
        }

        /// <summary>The dumps where every one of <paramref name="tables"/> has a known spot.</summary>
        public static List<(string id, int rev)> TableSpotDumps(params GameTable[] tables)
        {
            IEnumerable<(string id, int rev)> keys = TableSpots.Keys.Concat(MeasuredTableSpots.Keys).Select(k => (k.id, k.rev)).Distinct();
            return WithTwins(keys.Where(d => tables.All(t => TableSpots.ContainsKey((d.id, d.rev, t)) || MeasuredTableSpots.ContainsKey((d.id, d.rev, t)))));
        }

        public static GameFamilies FamilyOfCode(string id) =>
            id.StartsWith("ADA") || id.StartsWith("APA") ? GameFamilies.DP : id.StartsWith("CPU") ? GameFamilies.Plat : id.StartsWith("IP") ? GameFamilies.HGSS : GameFamilies.NULL;

        public static GameLanguages LanguageOfCode(string id) => id[3] switch
        {
            'J' => GameLanguages.Japanese, 'I' => GameLanguages.Italian, 'S' => GameLanguages.Spanish, 'F' => GameLanguages.French,
            'D' => GameLanguages.German, 'K' => GameLanguages.Korean, _ => GameLanguages.English,
        };

        /// <summary>Every dump DSPRE has measured, with the revisions that copy them.</summary>
        public static List<(string id, int rev)> KnownDumps => DumpsMeasured(_ => true);

        /// <summary>
        /// The dumps SetTrainerNameLenOffset finds a spot for: every one except Japanese and Korean Diamond and Pearl and
        /// Korean Platinum, unless a spot was measured on that dump.
        /// </summary>
        public static List<(string id, int rev)> TrainerNameLengthDumps => KnownDumps.Where(d =>
        {
            bool dp = d.id.StartsWith("ADA") || d.id.StartsWith("APA");
            bool unmapped = dp ? d.id[3] == 'J' || d.id[3] == 'K' : d.id.StartsWith("CPU") && d.id[3] == 'K';
            return !unmapped || Measured.TryGetValue(d, out Measure m) && m.TrainerNameLength != null;
        }).ToList();

        public static List<(string id, int rev)> BpShopDumps => SiteDumps(BpShopSiteTable.Keys, MeasuredBpShopSites.Keys);

        public static List<(string id, int rev)> SwarmDumps => SiteDumps(SwarmSiteTable.Keys, MeasuredSwarmSites.Keys);

        public static List<(string id, int rev)> TypeChartDumps =>
            SiteDumps(TypeChartSites.Keys, MeasuredTypeChartSites.Keys).Intersect(TableSpotDumps(GameTable.TypeChart)).ToList();

        public static List<(string id, int rev)> MoveListDumps(GameTable table) =>
            SiteDumps(MoveListSiteTable.Keys.Where(k => k.table == table).Select(k => (k.id, k.rev)),
                      MeasuredMoveListSites.Keys.Where(k => k.table == table).Select(k => (k.id, k.rev)))
                .Intersect(TableSpotDumps(table)).ToList();
    }
}
