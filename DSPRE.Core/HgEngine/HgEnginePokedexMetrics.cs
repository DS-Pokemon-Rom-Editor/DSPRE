using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DSPRE.ROMFiles;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// Pokédex measurements on hg-engine: each species' data/Species.c metricsData (speciesdatagen builds the
    /// per-species members of a/2/1/4 from it), and the size and body shape lists in data/PokedexSort.c, which
    /// hg-engine writes by hand and the build copies into the archive as they are.
    /// </summary>
    public static class HgEnginePokedexMetrics
    {
        private const string PokemonH = "include/constants/pokemon.h";
        public const string BodyShapePrefix = "DEX_SEARCH_BODYTYPE_";

        private static FieldPathSegment[] P(string field) => new[] { FieldPathSegment.Field("metricsData"), FieldPathSegment.Field(field) };

        private static HgEngineSourceField<PokedexMetrics> F(string field, int min, int max, Func<PokedexMetrics, int> get, Action<PokedexMetrics, int> set, string prefix = null) => new()
        {
            Path = P(field), Min = min, Max = max, Get = get, Set = set,
            Prefix = prefix, Headers = prefix != null ? new[] { PokemonH } : Array.Empty<string>(),
        };

        // Species.c order and include/species_data.h SpeciesMetrics widths.
        private static readonly HgEngineSourceField<PokedexMetrics>[] Fields =
        {
            F("heightDecimetres", 0, int.MaxValue, m => m.Height, (m, v) => m.Height = v),
            F("weightHectograms", 0, int.MaxValue, m => m.Weight, (m, v) => m.Weight = v),
            F("bodyType", 0, byte.MaxValue, m => m.BodyShape, (m, v) => m.BodyShape = v, BodyShapePrefix),
            F("femaleTrainerScale", 0, ushort.MaxValue, m => m.FemaleTrainerScale, (m, v) => m.FemaleTrainerScale = v),
            F("femalePokemonScale", 0, ushort.MaxValue, m => m.FemalePokemonScale, (m, v) => m.FemalePokemonScale = v),
            F("maleTrainerScale", 0, ushort.MaxValue, m => m.MaleTrainerScale, (m, v) => m.MaleTrainerScale = v),
            F("malePokemonScale", 0, ushort.MaxValue, m => m.MalePokemonScale, (m, v) => m.MalePokemonScale = v),
            F("femaleTrainerYOffset", short.MinValue, short.MaxValue, m => m.FemaleTrainerYOffset, (m, v) => m.FemaleTrainerYOffset = v),
            F("femalePokemonYOffset", short.MinValue, short.MaxValue, m => m.FemalePokemonYOffset, (m, v) => m.FemalePokemonYOffset = v),
            F("maleTrainerYOffset", short.MinValue, short.MaxValue, m => m.MaleTrainerYOffset, (m, v) => m.MaleTrainerYOffset = v),
            F("malePokemonYOffset", short.MinValue, short.MaxValue, m => m.MalePokemonYOffset, (m, v) => m.MalePokemonYOffset = v),
        };

        /// <summary>The checkout's body shape names in value order, without their prefix.</summary>
        public static List<string> BodyShapeNames()
        {
            HgEngineSymbolTable table = HgEngineSymbolTable.Load(PokemonH);
            List<string> names = new List<string>();
            if (table == null) return names;
            for (int v = 0; table.TryGetNameWithPrefix(v, BodyShapePrefix, out string name); v++)
                names.Add(name.Substring(BodyShapePrefix.Length));
            return names;
        }

        /// <summary>The species' metricsData. A field the entry leaves out reads as 0.</summary>
        public static bool TryLoad(int speciesId, out PokedexMetrics metrics, out string error)
        {
            metrics = new PokedexMetrics();
            if (!HgEngineEntrySource.TryLoad(HgEngineDomain.Species, speciesId, out HgEngineSourceBlock entry, out error)) return false;
            return HgEngineSourceFields.TryRead(entry, Fields, metrics, HgEngineSymbolTable.Load, out error);
        }

        /// <summary>Writes the species' metricsData, then moves it within PokedexSort.c's size lists and between
        /// its body shape lists if those values changed.</summary>
        public static bool TryWrite(int speciesId, PokedexMetrics metrics, out string error)
        {
            if (!HgEngineEntrySource.TryLoad(HgEngineDomain.Species, speciesId, out HgEngineSourceBlock entry, out error)) return false;
            PokedexMetrics before = new PokedexMetrics();
            if (!HgEngineSourceFields.TryRead(entry, Fields, before, HgEngineSymbolTable.Load, out error)) return false;
            if (before.SameAs(metrics)) return true;

            List<HgEngineFieldWrite> writes;
            if (entry.TryGetRaw(new[] { FieldPathSegment.Field("metricsData") }, out _))
            {
                writes = HgEngineValueSpelling.Preserve(entry, HgEngineSourceFields.Writes(Fields, metrics, HgEngineSymbolTable.Load),
                    w => HgEngineSourceFields.NameLookup(w.Headers, HgEngineSymbolTable.Load));
            }
            else
            {
                string body = string.Concat(Fields.Select(f => $"            .{f.Path[^1].Name} = {HgEngineSourceFields.Spell(f, f.Get(metrics), HgEngineSymbolTable.Load)},\n"));
                writes = new List<HgEngineFieldWrite> { new(new[] { FieldPathSegment.Field("metricsData") }, "{\n" + body + "        }") };
            }

            // The other species' values are read before this one's are written, so the lists use both.
            Dictionary<int, PokedexMetrics> all = null;
            if (before.Weight != metrics.Weight || before.Height != metrics.Height || before.BodyShape != metrics.BodyShape)
            {
                if (!TryLoadAll(out all, out error)) return false;
                all[speciesId] = metrics;
            }

            if (!HgEngineWriter.TryWriteFields(HgEngineDomain.Species, speciesId, writes, out _, out error, allowInsert: true, allOrNothing: true))
                return false;
            return all == null || TryUpdateSortLists(speciesId, before, metrics, all, out error);
        }

        private static readonly Regex SpeciesEntry = new(@"\[\s*(SPECIES_\w+)\s*\]\s*=\s*\{");

        /// <summary>Every Species.c entry's metricsData, by species id.</summary>
        public static bool TryLoadAll(out Dictionary<int, PokedexMetrics> all, out string error)
        {
            all = new Dictionary<int, PokedexMetrics>();
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }
            HgEngineDomainInfo info = HgEngineDomains.All.FirstOrDefault(d => d.Domain == HgEngineDomain.Species);
            HgEngineSymbolTable species = HgEngineSymbolTable.Load("include/constants/species.h");
            if (info == null || species == null) { error = "include/constants/species.h could not be read."; return false; }
            string path = Path.Combine(HgEngineProject.RepoPathUnc, info.SourceFileRelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) { error = $"Source file not found: {path}"; return false; }

            string text = HgEngineFileCache.GetText(path);
            int lastClose = -1;
            foreach (Match m in SpeciesEntry.Matches(text))
            {
                int open = m.Index + m.Length - 1;
                if (open < lastClose || !BraceScanner.TryFindMatchingBrace(text, open, out int close)) continue;
                lastClose = close;
                if (!species.TryGetValue(m.Groups[1].Value, out int id)) continue;
                PokedexMetrics metrics = new PokedexMetrics();
                if (!HgEngineSourceFields.TryRead(new HgEngineSourceBlock(text.Substring(open, close - open + 1)), Fields, metrics, HgEngineSymbolTable.Load, out error))
                { error = $"{m.Groups[1].Value}: {error}"; return false; }
                all[id] = metrics;
            }
            return true;
        }

        // A list entry is a species name, or any expression that comes to a species id.
        private static Func<string, int?> SpeciesIds(HgEngineSymbolTable species) => token =>
            species.TryGetValue(token, out int v) ? v
            : HgEngineSourceExpression.TryEvaluate(token, n => species.TryGetValue(n, out int w) ? w : null, out int e) ? e : null;

        /// <summary>
        /// The game shows a size sort in the list's order and filters by a body shape list's members, so the
        /// species moves to where its new value belongs among the others (after any it ties with) and from its old
        /// shape's list to the end of the new one. Everything else in the hand-written lists stays as it is, and a
        /// species a list leaves out stays out.
        /// </summary>
        private static bool TryUpdateSortLists(int speciesId, PokedexMetrics before, PokedexMetrics after, Dictionary<int, PokedexMetrics> all, out string error)
        {
            error = null;
            if (!HgEngineDexSortLists.Exists) return true;
            HgEngineSymbolTable species = HgEngineSymbolTable.Load("include/constants/species.h");
            if (species == null) { error = "include/constants/species.h could not be read."; return false; }
            if (!HgEngineDexSortLists.TryLoad(out HgEngineDexSortLists sort, out error)) return false;
            Dictionary<string, List<string>> tokens = sort.Lists;
            Func<string, int?> Id = SpeciesIds(species);
            HashSet<string> changed = new HashSet<string>();

            void Resort(string list, Func<PokedexMetrics, int> key, bool descending)
            {
                if (!tokens.TryGetValue(list, out List<string> order)) return;
                int at = order.FindIndex(t => Id(t) == speciesId);
                if (at < 0 || key(before) == key(after)) return;
                string token = order[at];
                order.RemoveAt(at);
                int value = key(after);
                int insert = order.FindIndex(t => Id(t) is int id && all.TryGetValue(id, out PokedexMetrics m)
                    && (descending ? key(m) < value : key(m) > value));
                order.Insert(insert < 0 ? order.Count : insert, token);
                changed.Add(list);
            }
            Resort("Heaviest", m => m.Weight, true);
            Resort("Lightest", m => m.Weight, false);
            Resort("Tallest", m => m.Height, true);
            Resort("Smallest", m => m.Height, false);

            if (before.BodyShape != after.BodyShape)
            {
                // The body shape lists follow the shape values in file order.
                List<string> shapeLists = sort.Names.Where(n => n.StartsWith("BodyType", StringComparison.Ordinal)).ToList();
                if (before.BodyShape < shapeLists.Count && after.BodyShape < shapeLists.Count)
                {
                    List<string> from = tokens[shapeLists[before.BodyShape]];
                    int at = from.FindIndex(t => Id(t) == speciesId);
                    if (at >= 0)
                    {
                        string token = from[at];
                        from.RemoveAt(at);
                        changed.Add(shapeLists[before.BodyShape]);
                        List<string> to = tokens[shapeLists[after.BodyShape]];
                        if (!to.Any(t => Id(t) == speciesId)) { to.Add(token); changed.Add(shapeLists[after.BodyShape]); }
                    }
                }
            }
            return sort.TryWrite(changed, out error);
        }

        /// <summary>
        /// Rebuilds every list the dex derives from species data, over every species sPokedexSort_NationalNum lists:
        /// the four size sorts (ties by number), A to Z and the letter lists by name, the type lists and the body shape
        /// lists in number order. Names sort as the existing lists do: letters and digits only, case aside, ♀ before ♂.
        /// The type search has no Fairy list, so Fairy species are found under Normal, as the existing lists keep them.
        /// The body shape lists come in DEX_SEARCH_BODYTYPE order in the file.
        /// </summary>
        public static bool TryRebuildSortLists(out int listed, out string error)
        {
            listed = 0;
            HgEngineSymbolTable species = HgEngineSymbolTable.Load("include/constants/species.h");
            if (species == null) { error = "include/constants/species.h could not be read."; return false; }
            if (!TryLoadAll(out Dictionary<int, PokedexMetrics> all, out error)) return false;
            if (!TryLoadAllTypes(out Dictionary<int, (int, int)> types, out error)) return false;
            if (!HgEngineDexSortLists.TryLoad(out HgEngineDexSortLists sort, out error)) return false;
            Dictionary<string, List<string>> tokens = sort.Lists;
            if (!tokens.TryGetValue("NationalNum", out List<string> national)) { error = $"{HgEngineDexSortLists.RelPath} has no sPokedexSort_NationalNum."; return false; }

            Func<string, int?> Id = SpeciesIds(species);
            List<(string Token, int Id)> known = national.Distinct().Where(t => Id(t) is int id && all.ContainsKey(id)).Select(t => (Token: t, Id: Id(t).Value)).ToList();
            listed = known.Count;

            HgEngineGeneratedText.Source nameSource = HgEngineGeneratedText.For(237);
            int count = known.Count == 0 ? 0 : known.Max(k => k.Id) + 1;
            if (nameSource == null || !HgEngineGeneratedText.TryReadLines(nameSource, count, out string[] names, out error))
            { error ??= "The species names could not be read."; return false; }

            HashSet<string> changed = new HashSet<string>();
            void Set(string list, IEnumerable<(string Token, int Id)> order)
            {
                if (!tokens.ContainsKey(list)) return;
                List<string> next = order.Select(o => o.Token).ToList();
                if (next.SequenceEqual(tokens[list])) return;
                tokens[list] = next;
                changed.Add(list);
            }

            Set("Heaviest", known.OrderByDescending(k => all[k.Id].Weight).ThenBy(k => k.Id));
            Set("Lightest", known.OrderBy(k => all[k.Id].Weight).ThenBy(k => k.Id));
            Set("Tallest", known.OrderByDescending(k => all[k.Id].Height).ThenBy(k => k.Id));
            Set("Smallest", known.OrderBy(k => all[k.Id].Height).ThenBy(k => k.Id));

            string Key(int id) => SortKey(id < names.Length ? names[id] : null);
            List<(string Token, int Id)> byName = known.OrderBy(k => Key(k.Id), StringComparer.Ordinal).ThenBy(k => k.Id).ToList();
            Set("NameAToZ", byName);
            for (char letter = 'A'; letter <= 'Z'; letter++)
            {
                char l = letter;
                Set("Name" + l, byName.Where(k => Key(k.Id).StartsWith(l)));
            }

            List<string> shapeLists = sort.Names.Where(n => n.StartsWith("BodyType", StringComparison.Ordinal)).ToList();
            for (int shape = 0; shape < shapeLists.Count; shape++)
            {
                int value = shape;
                Set(shapeLists[shape], known.Where(k => all[k.Id].BodyShape == value).OrderBy(k => k.Id));
            }

            HgEngineSymbolTable typeTable = HgEngineSymbolTable.Load(PokemonH);
            HgEngineSymbolTable battleTypes = HgEngineSymbolTable.Load("include/constants/battle_constants.h");
            int TypeValue(string name) => typeTable?.TryGetValue(name, out int v) == true ? v : battleTypes?.TryGetValue(name, out int w) == true ? w : -1;
            int normal = TypeValue("TYPE_NORMAL"), fairy = TypeValue("TYPE_FAIRY");
            bool fairyList = tokens.Keys.Any(k => k.Equals("TypeFairy", StringComparison.OrdinalIgnoreCase));
            foreach (string list in sort.Names.Where(n => n.StartsWith("Type", StringComparison.Ordinal)))
            {
                string symbol = "TYPE_" + System.Text.RegularExpressions.Regex.Replace(list.Substring(4), "(?<=[a-z])(?=[A-Z])", "_").ToUpperInvariant();
                int type = TypeValue(symbol);
                if (type < 0) continue;
                bool Has(int id)
                {
                    if (!types.TryGetValue(id, out (int, int) t)) return false;
                    if (t.Item1 == type || t.Item2 == type) return true;
                    return type == normal && !fairyList && fairy >= 0 && (t.Item1 == fairy || t.Item2 == fairy);
                }
                Set(list, known.Where(k => Has(k.Id)).OrderBy(k => k.Id));
            }

            return sort.TryWrite(changed, out error);
        }

        /// <summary>How the dex orders names: letters and digits only, ignoring case and accents, ♀ before ♂.</summary>
        public static string SortKey(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            StringBuilder b = new System.Text.StringBuilder();
            foreach (char c in name.Normalize(System.Text.NormalizationForm.FormKD))
            {
                if (c == '\u2640') b.Append('0');
                else if (c == '\u2642') b.Append('1');
                else if (c < 128 && char.IsLetterOrDigit(c)) b.Append(char.ToUpperInvariant(c));
            }
            return b.ToString();
        }

        private static readonly Regex TypesField = new(@"\.types\s*=\s*\{\s*(\w+)\s*,\s*(\w+)\s*\}");

        /// <summary>Each species' two types from Species.c, by id.</summary>
        public static bool TryLoadAllTypes(out Dictionary<int, (int, int)> types, out string error)
        {
            types = new Dictionary<int, (int, int)>();
            error = null;
            HgEngineDomainInfo info = HgEngineDomains.All.FirstOrDefault(d => d.Domain == HgEngineDomain.Species);
            HgEngineSymbolTable species = HgEngineSymbolTable.Load("include/constants/species.h");
            if (info == null || species == null) { error = "include/constants/species.h could not be read."; return false; }
            HgEngineSymbolTable typeTable = HgEngineSymbolTable.Load(PokemonH);
            HgEngineSymbolTable battleTypes = HgEngineSymbolTable.Load("include/constants/battle_constants.h");
            int Value(string token) => int.TryParse(token, out int n) ? n
                : typeTable?.TryGetValue(token, out int v) == true ? v : battleTypes?.TryGetValue(token, out int w) == true ? w : -1;
            string path = Path.Combine(HgEngineProject.RepoPathUnc, info.SourceFileRelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) { error = $"Source file not found: {path}"; return false; }
            string text = HgEngineFileCache.GetText(path);
            int lastClose = -1;
            foreach (Match m in SpeciesEntry.Matches(text))
            {
                int open = m.Index + m.Length - 1;
                if (open < lastClose || !BraceScanner.TryFindMatchingBrace(text, open, out int close)) continue;
                lastClose = close;
                if (!species.TryGetValue(m.Groups[1].Value, out int id)) continue;
                Match t = TypesField.Match(text, open, close - open);
                if (t.Success) types[id] = (Value(t.Groups[1].Value), Value(t.Groups[2].Value));
            }
            return true;
        }

        /// <summary>
        /// The type search filters by the sPokedexSort_Type* lists, so a species whose types change leaves the lists
        /// of types it lost and joins the end of the new ones, if those lists held it before.
        /// </summary>
        public static bool TryMoveTypes(int speciesId, (int, int) before, (int, int) after, out string error)
        {
            error = null;
            if (before == after || !HgEngineDexSortLists.Exists) return true;
            HgEngineSymbolTable species = HgEngineSymbolTable.Load("include/constants/species.h");
            if (species == null) { error = "include/constants/species.h could not be read."; return false; }
            if (!HgEngineDexSortLists.TryLoad(out HgEngineDexSortLists sort, out error)) return false;
            Dictionary<string, List<string>> tokens = sort.Lists;
            Func<string, int?> Id = SpeciesIds(species);

            // TYPE_FIGHTING's list is TypeFighting.
            string ListOf(int type)
            {
                foreach (string header in new[] { PokemonH, "include/constants/battle_constants.h" })
                    if (HgEngineSymbolTable.Load(header)?.TryGetNameWithPrefix(type, "TYPE_", out string name) == true)
                    {
                        string key = "type" + name.Substring(5).Replace("_", "").ToLowerInvariant();
                        return tokens.Keys.FirstOrDefault(k => k.ToLowerInvariant() == key);
                    }
                return null;
            }
            static bool Has((int, int) t, int type) => t.Item1 == type || t.Item2 == type;

            List<string> oldLists = new[] { before.Item1, before.Item2 }.Distinct().Select(ListOf).Where(l => l != null).ToList();
            string token = oldLists.SelectMany(l => tokens[l]).FirstOrDefault(t => Id(t) == speciesId);
            if (token == null) return true;

            HashSet<string> changed = new HashSet<string>();
            foreach (int type in new[] { before.Item1, before.Item2, after.Item1, after.Item2 }.Distinct())
            {
                if (Has(before, type) == Has(after, type) || ListOf(type) is not string list) continue;
                List<string> order = tokens[list];
                if (Has(after, type)) { if (!order.Any(t => Id(t) == speciesId)) order.Add(token); }
                else order.RemoveAll(t => Id(t) == speciesId);
                changed.Add(list);
            }
            return sort.TryWrite(changed, out error);
        }
    }
}
