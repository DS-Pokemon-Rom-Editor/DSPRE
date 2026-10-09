using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// The Pokémon a Pokédex page is shown with, read from the loaded ROM: its name, number, entry, category,
    /// height and weight, its battle picture, footprint and types.
    /// </summary>
    public sealed class DexSample
    {
        /// <summary>Shuckle: plain shapes and two types, which makes misplaced pieces easy to spot.</summary>
        public const int DefaultSpecies = 213;

        /// <summary>Heracross, beside Shuckle and one of the few DP keeps foreign entries for.</summary>
        public const int DiamondPearlSpecies = 214;

        public int Species { get; }
        private readonly Dictionary<int, List<string>> _banks = new();

        public DexSample(int species = 0) =>
            Species = species > 0 ? species : gameFamily == GameFamilies.DP ? DiamondPearlSpecies : DefaultSpecies;

        public string Text(DexText t, string variant)
        {
            int species = Species + t.Offset;
            switch (t.Kind)
            {
                case DexTextKind.Message: return Fill(Line(pokedexMessagesTextNumber, t.Line), t.Args);
                case DexTextKind.SpeciesName:
                    string[] names = null;
                    try { names = GetPokemonNames(); } catch { }
                    return names != null && species >= 0 && species < names.Length ? names[species] : null;
                case DexTextKind.Number: return species.ToString("D3");
                case DexTextKind.Category: return Line(pokedexCategoryTextNumber, species);
                case DexTextKind.Height: return Line(pokedexHeightTextNumber, species);
                case DexTextKind.Weight: return Line(pokedexWeightTextNumber, species);
                case DexTextKind.Entry: return Line(pokedexEntryTextNumber, species);
                case DexTextKind.Count: return t.Sample.ToString("D3");
                case DexTextKind.ForeignEntry: return Line(Pick(pokedexForeignEntryTextNumbers, t.Line), ForeignLine(species, t.Line));
                case DexTextKind.ForeignName: return Line(Pick(pokedexForeignNameTextNumbers, t.Line), ForeignLine(species, t.Line));
                case DexTextKind.ForeignCategory: return Line(Pick(pokedexForeignCategoryTextNumbers, t.Line), ForeignLine(species, t.Line));
                case DexTextKind.PlayerName: return Line(playerCharacterNamesTextNumber, Female(variant) ? 1 : 0);
                case DexTextKind.SearchHeight: return SearchStepText(t.Sample, height: true);
                case DexTextKind.SearchWeight: return SearchStepText(t.Sample, height: false);
                case DexTextKind.Place:
                {
                    // The list's cursor sits on its first entry, "show all", which is the third row.
                    int entry = t.Line - 2;
                    if (entry < 0) return null;
                    if (entry == 0) return Line(pokedexMessagesTextNumber, variant == "Kanto" ? 135 : 134);
                    List<string> places = Habitat(variant)?.Places;
                    return places != null && entry - 1 < places.Count ? places[entry - 1] : null;
                }
                default: return null;
            }
        }

        // Placeholders like {STRVAR_1, 52, 0, 0} take the sample values in order; any left over show nothing.
        private static readonly System.Text.RegularExpressions.Regex Placeholder = new(@"\{STRVAR[^}]*\}");

        private static string Fill(string line, string[] args)
        {
            if (line == null) return null;
            int next = 0;
            return Placeholder.Replace(line, _ => args != null && next < args.Length ? args[next++] : "");
        }

        private static int Pick(int[] banks, int language) =>
            banks != null && language >= 0 && language < banks.Length ? banks[language] : -1;

        // DP's foreign banks list Japanese, English, French, German, Italian, Spanish for each species in its table.
        private static readonly int[] DiamondPearlLanguages = { 2, 3, 4, 5, 0 };
        private int[] _foreignSpecies;

        private int ForeignLine(int species, int language)
        {
            if (gameFamily != GameFamilies.DP) return species;
            if (pokedexForeignSpeciesTableOffset < 0 || language < 0 || language >= DiamondPearlLanguages.Length) return -1;
            if (_foreignSpecies == null)
            {
                try
                {
                    byte[] table = ARM9.ReadBytes((uint)pokedexForeignSpeciesTableOffset, pokedexForeignSpeciesCount * 2);
                    _foreignSpecies = Enumerable.Range(0, pokedexForeignSpeciesCount)
                        .Select(i => (int)BitConverter.ToUInt16(table, i * 2)).ToArray();
                }
                catch (Exception ex) { AppLogger.Warn("Pokédex foreign species: " + ex.Message); _foreignSpecies = Array.Empty<int>(); }
            }
            int slot = Array.IndexOf(_foreignSpecies, species);
            return slot < 0 ? -1 : slot * 6 + DiamondPearlLanguages[language];
        }

        private string Line(int bank, int line)
        {
            if (bank < 0 || line < 0) return null;
            if (!_banks.TryGetValue(bank, out List<string> lines))
            {
                try { lines = new TextArchive(bank).messages; } catch { lines = null; }
                _banks[bank] = lines;
            }
            if (lines == null || line >= lines.Count) return null;
            // Lines carry escapes for the text engine's own codes; show the words.
            return lines[line].Replace("\\n", "\n").Replace("\\r", "\n").Replace("\\f", "\n");
        }

        public DexPicture Picture(DexMon mon)
        {
            int species = Species + mon.Offset;
            if (species <= 0) return null;
            try
            {
                switch (mon.Kind)
                {
                    case DexMonKind.Front:
                    {
                        GraphicAssets.Archive sprites = GraphicAssets.All.First(a => a.Dir == DirNames.pokemonBattleSprites);
                        // Six files a species: back female, back male, front female, front male, colours, shiny.
                        GraphicAssets.Preview p = GraphicAssets.Render(sprites, species * 6 + 3);
                        return Frame(p, 80, 80);
                    }
                    case DexMonKind.Back:
                    {
                        GraphicAssets.Archive sprites = GraphicAssets.All.First(a => a.Dir == DirNames.pokemonBattleSprites);
                        GraphicAssets.Preview p = GraphicAssets.Render(sprites, species * 6 + 1);
                        return Frame(p, 80, 80);
                    }
                    case DexMonKind.Icon:
                    {
                        // Party icons follow seven shared files, one per species, two frames tall.
                        GraphicAssets.Archive icons = GraphicAssets.All.First(a => a.Dir == DirNames.monIcons);
                        GraphicAssets.Preview p = GraphicAssets.Render(icons, species + 7);
                        return Frame(p, 32, 32);
                    }
                    case DexMonKind.Footprint:
                    {
                        int entry = GraphicAssets.FootprintEntry(species);
                        if (entry < 0) return null;
                        GraphicAssets.Archive feet = GraphicAssets.All.First(a => a.Dir == DirNames.footprintGraphics);
                        GraphicAssets.Preview p = GraphicAssets.Render(feet, entry);
                        return p?.Rgba == null ? null : new DexPicture { Rgba = p.Rgba, Width = p.Width, Height = p.Height };
                    }
                }
            }
            catch (Exception ex) { AppLogger.Warn("Pokédex sample picture: " + ex.Message); }
            return null;
        }

        private static DexPicture Frame(GraphicAssets.Preview p, int w, int h)
        {
            if (p?.Rgba == null) return null;
            w = Math.Min(w, p.Width);
            h = Math.Min(h, p.Height);
            byte[] rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++) Array.Copy(p.Rgba, y * p.Width * 4, rgba, y * w * 4, w * 4);
            return new DexPicture { Rgba = rgba, Width = w, Height = h };
        }

        /// <summary>The habitat for an AREA page state, named after a time of day; none for "Area unknown".</summary>
        private readonly Dictionary<string, DexHabitatData> _habitats = new();

        public DexHabitatData Habitat(string variant)
        {
            if (variant == "Area unknown" && gameFamily != GameFamilies.HGSS) return null;
            if (_habitats.TryGetValue(variant ?? "", out DexHabitatData known)) return known;
            DexHabitatData built = BuildHabitat(variant);
            _habitats[variant ?? ""] = built;
            return built;
        }

        /// <summary>Forgets the habitats worked out so far, for when the area files change.</summary>
        public void Forget() => _habitats.Clear();

        private DexHabitatData BuildHabitat(string variant)
        {
            int time = variant == "Morning" ? 0 : variant == "Night" ? 2 : 1;
            // The sample needs the National Pokédex, so the special list is the one used after it.
            try
            {
                if (variant == "Area unknown") return DexHabitatBuilder.HgssPlayerOnly(kanto: false);
                return gameFamily == GameFamilies.HGSS
                    ? DexHabitatBuilder.BuildHgss(Species, time, kanto: variant == "Kanto")
                    : DexHabitatBuilder.Build(Species, time, national: true);
            }
            catch (Exception ex) { AppLogger.Warn("Pokédex habitat: " + ex.Message); return null; }
        }

        /// <summary>
        /// The sample's size-page data for a page state: offsets and scales from the Pokédex data for the player's
        /// gender (states naming Dawn or the female player), and the scale's tilt from the weights.
        /// </summary>
        /// <summary>Values to show in place of the saved ones, for a preview of edits not yet saved.</summary>
        public Func<PokedexMetrics> MetricsOverride { get; set; }

        public DexSizeInfo SizeInfo(string variant)
        {
            bool female = Female(variant);
            PokedexMetrics m = MetricsOverride?.Invoke();
            try { if (m == null && PokedexDataArchive.TryLoad(out PokedexDataArchive archive, out _)) m = archive.Get(Species); }
            catch (Exception ex) { AppLogger.Warn("Pokédex size data: " + ex.Message); }
            if (m == null) return null;
            return new DexSizeInfo
            {
                PokemonOffset = female ? m.FemalePokemonYOffset : m.MalePokemonYOffset,
                PokemonScale = female ? m.FemalePokemonScale : m.MalePokemonScale,
                TrainerOffset = female ? m.FemaleTrainerYOffset : m.MaleTrainerYOffset,
                TrainerScale = female ? m.FemaleTrainerScale : m.MaleTrainerScale,
                Tilt = gameFamily == GameFamilies.HGSS ? HgssTilt(m.Weight, female) : DpptTilt(m.Weight, female),
            };
        }

        private static bool Female(string variant) => variant != null && (variant.Contains("Dawn") || variant.Contains("Female"));

        // pokeplatinum ov21_021E737C.c: the first row whose difference reaches the weights' gives the angle; the
        // player's side goes down when the Pokémon is lighter.
        private static readonly (int Difference, int Angle)[] DpptTilts =
        {
            (0x0, 0x0), (0xA, 0x2D8), (0x14, 0x222), (0x1E, 0x2D8), (0x28, 0x38E), (0x32, 0x444), (0x3C, 0x4FA),
            (0x46, 0x5B0), (0x96, 0xAAA), (0x12C, 0xAAA), (0x1F4, 0xAAA), (0x2EE, 0xAAA), (0x41A, 0xAAA), (0x60E, 0xAAA),
            (0x92E, 0xAAA), (0xDAC, 0xAAA), (0xFFFF, 0xAAA),
        };

        private static int DpptTilt(int weight, bool female)
        {
            int player = female ? 340 : 380;
            int difference = Math.Abs(weight - player);
            foreach ((int limit, int angle) in DpptTilts)
                if (difference <= limit) return weight >= player ? -angle : angle;
            return 0;
        }

        // The difference in kilograms picks a pattern; patterns 1-7 swing and settle where their steps add up to, the
        // rest stop at the dial's limit, 0xA00. The players weigh 40.0 and 41.0 kg.
        private static readonly int[] HgssPatternTop = { 0, 1, 2, 3, 4, 5, 6, 7, 15, 30, 50, 75, 105, 155, 235, 350, 65535 };
        private static readonly (int Start, int Step, int Stop)[] HgssSwings =
        {
            (0, 0, 0), (0x100, -0x20, -0xE0), (0x120, -0x20, -0x100), (0x140, -0x20, -0x120), (0x160, -0x20, -0x140),
            (0x180, -0x20, -0x160), (0x1C0, -0x40, -0x140), (0x200, -0x40, -0x180),
        };

        private static int HgssTilt(int weight, bool female)
        {
            int player = female ? 410 : 400;
            int kilograms = Math.Abs(weight - player) / 10;
            int pattern = Array.FindIndex(HgssPatternTop, top => kilograms <= top);
            int tilt = 0;
            if (pattern >= 8) tilt = 0xA00;
            else if (pattern > 0)
            {
                (int move, int step, int stop) = HgssSwings[pattern];
                for (int guard = 0; guard < 256 && move != stop; guard++)
                {
                    tilt += move;
                    move += step;
                }
            }
            return weight >= player ? -tilt : tilt;
        }

        // ── HGSS's height and weight search sliders ─────────────────────────────────────────────────

        private ushort[] _searchSteps;

        /// <summary>A slider step's height (decimetres) or weight (tenths of a kilogram), or -1.</summary>
        private int SearchStep(int step, bool height)
        {
            if (_searchSteps == null)
            {
                try
                {
                    byte[] raw = System.IO.File.ReadAllBytes(System.IO.Path.Combine(gameDirs[DirNames.pokedexSearchSteps].unpackedDir, "0000"));
                    _searchSteps = new ushort[raw.Length / 2];
                    Buffer.BlockCopy(raw, 0, _searchSteps, 0, _searchSteps.Length * 2);
                }
                catch { _searchSteps = Array.Empty<ushort>(); }
            }
            int at = step * 2 + (height ? 0 : 1);
            return step >= 0 && at < _searchSteps.Length ? _searchSteps[at] : -1;
        }

        // English games show feet and inches or pounds (pokeheartgold ov18_021EFD00, ov18_021EFDB4), the others the
        // stored metres and kilograms.
        private static bool Imperial => gameLanguage == GameLanguages.English;

        private static int Inches(int decimetres) => decimetres == 999 ? 1188 : (decimetres * 10000 / 254 + 5) / 10;

        private static int TenthsOfPounds(int hectograms) => hectograms == 9999 ? 99990 : (hectograms * 220462 + 50000) / 100000;

        private string SearchStepText(int step, bool height)
        {
            int value = SearchStep(step, height);
            if (value < 0) return null;
            if (height && Imperial)
            {
                int inches = Inches(value);
                return Fill(Line(pokedexMessagesTextNumber, 175), new[] { (inches / 12).ToString(), (inches % 12).ToString("D2") });
            }
            int tenths = !height && Imperial ? TenthsOfPounds(value) : value;
            return Fill(Line(pokedexMessagesTextNumber, 38), new[] { (tenths / 10).ToString(), (tenths % 10).ToString() });
        }

        // The knobs' digit sprites (pokeheartgold ov18_021F38F0 and ov18_021F36D4 for height, ov18_021F39C4 and
        // ov18_021F37D4 for weight). The metric games' four places are unconfirmed against a decomp.
        private const int FirstDigitSeq = 0x2B;
        private static readonly int[] FeetDigitX = { -20, -12, 4, 12 }, PoundDigitX = { -20, -12, -4, 4, 20 };
        private static readonly int[] MetricDigitX = { -16, -8, 0, 16 };

        public (int Seq, int Dx)? SearchDigit(DexReadout readout, int step, int place)
        {
            int value = SearchStep(step, readout == DexReadout.SearchHeight);
            if (value < 0) return null;
            int digit;
            if (!Imperial)
            {
                // Four places, the last after the point; leading zeros are hidden until the units.
                if (place >= MetricDigitX.Length) return null;
                int power = (int)Math.Pow(10, 3 - place);
                digit = value / power % 10;
                if (place < 2 && value / power == 0) return null;
                return (FirstDigitSeq + digit, MetricDigitX[place]);
            }
            if (readout == DexReadout.SearchHeight)
            {
                // Feet in two places, the tens only from ten feet, then inches in two.
                int inches = Inches(value), feet = inches / 12;
                if (place == 4 || (place == 0 && feet < 10)) return null;
                digit = place switch { 0 => feet / 10 % 10, 1 => feet % 10, 2 => inches % 12 / 10, _ => inches % 12 % 10 };
                return (FirstDigitSeq + digit, FeetDigitX[place]);
            }
            // Pounds in tenths over five places; leading zeros are hidden until the units.
            int tenths = TenthsOfPounds(value), scale = (int)Math.Pow(10, 4 - place);
            if (place < 3 && tenths / scale == 0) return null;
            return (FirstDigitSeq + tenths / scale % 10, PoundDigitX[place]);
        }

        // The Pokédex lists types in its own order, which is not the order of the type numbers.
        private static readonly int[] DexTypeOrder =
        {
            // Normal Fighting Flying Poison Ground Rock Bug Ghost Steel ??? Fire Water Grass Electric Psychic Ice Dragon Dark
            0, 6, 14, 10, 8, 5, 11, 7, 9, 7, 1, 3, 2, 4, 15, 13, 16, 12,
        };

        /// <summary>The sample's first or second type in the Pokédex's order, or -1 (no second type).</summary>
        public int TypeSlot(int slot)
        {
            try
            {
                PokemonPersonalData data = new PokemonPersonalData(Species);
                int type = slot == 1 ? (int)data.type1 : (int)data.type2;
                if (slot == 2 && data.type2 == data.type1) return -1;
                return type >= 0 && type < DexTypeOrder.Length ? DexTypeOrder[type] : -1;
            }
            catch { return -1; }
        }
    }
}
