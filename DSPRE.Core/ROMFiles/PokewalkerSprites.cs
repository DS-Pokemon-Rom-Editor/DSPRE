using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DSPRE.HgEngine;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Which Pokéwalker picture (a/2/5/6) each Pokémon sends to the Pokéwalker. Vanilla HGSS looks the species and
    /// sex up in a table in overlay 112, except for species picked by form, which take a first picture plus the form.
    /// hg-engine replaces that lookup with src/field/pokewalker.c: the old species keep the vanilla layout, and its
    /// new species follow from POKEWALKER_SPRITE_BASE_NEW_MONS in the order of their battle sprites.
    /// </summary>
    public sealed class PokewalkerSprites
    {
        public const int VanillaSpeciesCount = 494;

        /// <summary>What one picture shows. Form is -1 for a species that isn't picked by form.</summary>
        public readonly record struct Picture(int Species, bool Female, int Form);

        private readonly Dictionary<int, Picture> _pictures = new();
        private readonly Dictionary<(int Species, bool Female), int> _bySex = new();
        private readonly Dictionary<int, (int First, int Count)> _forms = new();

        public IReadOnlyDictionary<int, Picture> Pictures => _pictures;

        /// <summary>The index for the open ROM or checkout, or null with a reason.</summary>
        public static PokewalkerSprites Load(int pictureCount, out string error)
        {
            error = null;
            if (RomInfo.gameFamily != RomInfo.GameFamilies.HGSS) { error = "The Pokéwalker is in HeartGold and SoulSilver."; return null; }
            try
            {
                return HgEngineProject.IsActive ? FromHgEngine(pictureCount, out error) : FromRom(pictureCount, out error);
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is UnauthorizedAccessException)
            {
                error = ex.Message;
                return null;
            }
        }

        /// <summary>The picture the game sends for this species, sex and form, or -1.</summary>
        public int PictureFor(int species, bool female, int form)
        {
            if (_forms.TryGetValue(species, out (int First, int Count) f)) return form >= 0 && form < f.Count ? f.First + form : -1;
            if (female && _bySex.TryGetValue((species, true), out int pf)) return pf;
            return _bySex.TryGetValue((species, false), out int pm) ? pm : -1;
        }

        /// <summary>How many form pictures a species has, or 0 when its pictures go by sex.</summary>
        public int FormCount(int species) => _forms.TryGetValue(species, out (int First, int Count) f) ? f.Count : 0;

        private static PokewalkerSprites FromRom(int pictureCount, out string error)
        {
            error = GameTableFile.WhyNot(RomInfo.GameTable.PokewalkerSprites, VanillaSpeciesCount * 4);
            if (error != null) return null;
            byte[] table = GameTableFile.Read(RomInfo.GameTable.PokewalkerSprites, VanillaSpeciesCount * 4);
            PokewalkerSprites index = new PokewalkerSprites();
            for (int s = 1; s < VanillaSpeciesCount; s++)
            {
                int male = BitConverter.ToUInt16(table, s * 4), female = BitConverter.ToUInt16(table, s * 4 + 2);
                if (male >= pictureCount || female >= pictureCount)
                {
                    error = "The Pokéwalker table in overlay 112 doesn't match this ROM's pictures.";
                    return null;
                }
                index.AddSex(s, male, female);
            }
            index.AddForms(RomInfo.PokewalkerFormPictures, pictureCount);
            return index;
        }

        private static readonly Regex Define = new(@"^\s*#define\s+(POKEWALKER_SPRITE_BASE_\w+)\s+(\d+)", RegexOptions.Multiline);

        private static PokewalkerSprites FromHgEngine(int pictureCount, out string error)
        {
            error = null;
            string path = Path.Combine(HgEngineProject.RepoRootWindows, "src", "field", "pokewalker.c");
            if (!File.Exists(path)) { error = "src/field/pokewalker.c isn't in the checkout."; return null; }
            string text = File.ReadAllText(path);
            HgEngineSymbolTable species = HgEngineSymbolTable.Load("include/constants/species.h");
            if (species == null) { error = "include/constants/species.h couldn't be read."; return null; }

            Dictionary<string, int> bases = Define.Matches(text).Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => int.Parse(m.Groups[2].Value));
            int Value(string token) => int.TryParse(token, out int n) ? n
                : bases.TryGetValue(token, out n) ? n
                : species.ByName.TryGetValue(token, out n) ? n : throw new InvalidOperationException($"pokewalker.c uses {token}, which isn't defined.");

            if (!bases.TryGetValue("POKEWALKER_SPRITE_BASE_GENDER_DIFFERENCES", out int femaleBase)
                || !bases.TryGetValue("POKEWALKER_SPRITE_BASE_NEW_MONS", out int newBase)
                || !species.ByName.TryGetValue("SPECIES_VICTINI", out int firstNew))
            {
                error = "pokewalker.c doesn't define the picture bases DSPRE reads.";
                return null;
            }

            List<int> females = ArrayBody(text, "sSpeciesWithGenderDifferences").Select(Value).ToList();
            List<(int, int)> formRows = ArrayBody(text, "sMapOldSpeciesToBaseFormIndex", rows: true)
                .Select(row => row.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .Where(cells => cells.Length == 2)
                .Select(cells => (Value(cells[0]), Value(cells[1])))
                .ToList();

            PokewalkerSprites index = new PokewalkerSprites();
            for (int s = 1; s < firstNew; s++)
            {
                int female = females.IndexOf(s);
                index.AddSex(s, s - 1, female >= 0 ? femaleBase + female : s - 1);
            }
            index.AddForms(formRows, newBase);
            for (int picture = newBase; picture < pictureCount; picture++)
                index._pictures[picture] = new Picture(picture - newBase + firstNew, false, -1);
            for (int s = firstNew; s < firstNew + Math.Max(0, pictureCount - newBase); s++)
                index._bySex[(s, false)] = s - firstNew + newBase;
            return index;
        }

        // The entries of `name[] = { ... };`, or each `{ ... }` row's inside when rows is set.
        private static IEnumerable<string> ArrayBody(string text, string name, bool rows = false)
        {
            Match m = Regex.Match(text, Regex.Escape(name) + @"\s*(\[[^\]]*\]\s*)+=\s*\{");
            if (!m.Success) return Enumerable.Empty<string>();
            int open = m.Index + m.Length - 1;
            if (!BraceScanner.TryFindMatchingBrace(text, open, out int close)) return Enumerable.Empty<string>();
            string body = Regex.Replace(text.Substring(open + 1, close - open - 1), @"//[^\n]*", "");
            return rows
                ? Regex.Matches(body, @"\{([^{}]*)\}").Cast<Match>().Select(r => r.Groups[1].Value)
                : body.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0);
        }

        private void AddSex(int species, int male, int female)
        {
            _bySex[(species, false)] = male;
            _pictures.TryAdd(male, new Picture(species, false, -1));
            if (female != male)
            {
                _bySex[(species, true)] = female;
                _pictures.TryAdd(female, new Picture(species, true, -1));
            }
        }

        // Each species' forms run up to the next one's first picture; the last runs up to end.
        private void AddForms(IEnumerable<(int Species, int First)> rows, int end)
        {
            List<(int Species, int First)> sorted = rows.OrderBy(r => r.First).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                int next = i + 1 < sorted.Count ? sorted[i + 1].First : end;
                (int species, int first) = sorted[i];
                _forms[species] = (first, next - first);
                for (int form = 0; form < next - first; form++) _pictures[first + form] = new Picture(species, false, form);
            }
        }
    }
}
