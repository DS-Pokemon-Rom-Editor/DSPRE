using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Grouping and naming for the archives whose contents the games name themselves.</summary>
    public static class NamedArchives
    {
        /// <summary>The names for one archive in the game that is open, or empty when there are none.</summary>
        public static IReadOnlyList<string> Names(DirNames dir)
        {
            string packed = null;
            try
            {
                bool johto = gameFamily == GameFamilies.HGSS;
                if (dir == DirNames.synthOverlay)
                    packed = johto ? ArchiveEntryNames.WeatherHeartGold : ArchiveEntryNames.WeatherPlatinum;
                else if (dir == DirNames.fonts)
                    packed = johto ? ArchiveEntryNames.FontHeartGold : ArchiveEntryNames.FontPlatinum;
            }
            catch { }

            if (packed == null) return Array.Empty<string>();
            return packed.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                         .Select(n => n == "-" ? "" : n)
                         .ToList();
        }

        /// <summary>What to call a thing, in the words somebody looking for it would use.</summary>
        public static string Friendly(DirNames dir, string thing)
        {
            if (string.IsNullOrEmpty(thing)) return null;

            if (dir == DirNames.synthOverlay)
            {
                foreach (var (name, says) in Weather)
                    if (thing.Equals(name, StringComparison.Ordinal)) return says;
                return Pretty(thing);
            }

            if (dir == DirNames.fonts)
            {
                foreach (var (name, says) in Fonts)
                    if (thing.Equals(name, StringComparison.Ordinal)) return says;
                return Pretty(thing);
            }

            return Pretty(thing);
        }

        // The weather the field can put on the screen. The names are the game's own; these say what they
        // look like in play.
        private static readonly (string Name, string Says)[] Weather =
        {
            ("BLOCK", "Falling ash"),
            ("RAIN", "Rain"),
            ("RAIN_ST", "Heavy rain"),
            ("RAINBOW", "Rainbow"),
            ("SHINPI", "Mysterious shimmer"),
            ("SNOW", "Snow"),
            ("SNOW_D", "Deep snow"),
            ("SNOW_S", "Blizzard"),
            ("SPARK", "Sparks"),
            ("STORM", "Sandstorm"),
            ("STORM_BG", "Sandstorm backdrop"),
            ("STORM_SC", "Sandstorm arrangement"),
            ("VOLCANO", "Volcanic ash"),
            ("VOLCANO_BG", "Volcanic ash backdrop"),
            ("CLOUDINESS", "Overcast sky"),
            ("MYSTIC", "Mystical haze"),
            ("FOG_BG", "Fog colours"),
            ("FLASH", "Lightning flash"),
            ("WEATHER_CELL_RESDAT", "Shared layout data"),
            ("WEATHER_CELLANM_RESDAT", "Shared animation data"),
            ("WEATHER_CHAR_RESDAT", "Shared drawing data"),
            ("WEATHER_PLTT_RESDAT", "Shared colour data"),
        };

        private static readonly (string Name, string Says)[] Fonts =
        {
            ("system", "System font"),
            ("talk", "Dialogue font"),
            ("button", "Button font"),
            ("touch", "Touch screen font"),
            ("unknown", "Spare font"),
            ("num_lz", "Numbers"),
            ("dis_change", "Font size change marks"),
            ("system_ncrl", "System font widths"),
            ("talk_ncrl", "Dialogue font widths"),
            ("touch_ncrl", "Touch screen font widths"),
        };

        private static string Pretty(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var words = name.Split('_', StringSplitOptions.RemoveEmptyEntries)
                            .Select(w => w.Length <= 1 ? w
                                        : char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant());
            return string.Join(" ", words);
        }

        /// <summary>One row per thing, with its pieces together, in the order the game lists them.</summary>
        public static List<GraphicAssets.Unit> Units(GraphicAssets.Archive a, int fileCount)
        {
            var names = Names(a.Dir);
            var units = new List<GraphicAssets.Unit>();
            var spokenFor = new HashSet<int>();

            var order = new List<string>();
            var pieces = new Dictionary<string, List<(int Index, string Part)>>();

            for (int i = 0; i < fileCount && i < names.Count; i++)
            {
                var (thing, part) = BattleObjects.Split(names[i]);
                if (thing == null) continue;
                if (!pieces.TryGetValue(thing, out var list))
                {
                    pieces[thing] = list = new List<(int, string)>();
                    order.Add(thing);
                }
                list.Add((i, part));
                spokenFor.Add(i);
            }

            foreach (string thing in order)
            {
                var u = new GraphicAssets.Unit { Archive = a, Name = Friendly(a.Dir, thing) };
                foreach (var (index, part) in pieces[thing].OrderBy(p => Rank(p.Part)).ThenBy(p => p.Index))
                    u.Parts.Add(new GraphicAssets.UnitPart { Archive = a, Index = index, Name = part });
                units.Add(u);
            }

            for (int i = 0; i < fileCount; i++)
            {
                if (spokenFor.Contains(i)) continue;
                var lone = new GraphicAssets.Unit { Archive = a, Name = a.Title };
                lone.Parts.Add(new GraphicAssets.UnitPart { Archive = a, Index = i, Name = "File " + i });
                units.Add(lone);
            }

            units.Sort((x, y) => x.First.CompareTo(y.First));
            return units;
        }

        private static int Rank(string part) => GraphicAssets.PartRank(part);

        /// <summary>The drawing a layout or arrangement belongs with, which is the one of the same thing.</summary>
        public static int DrawingFor(DirNames dir, int fileIndex)
        {
            var names = Names(dir);
            if (fileIndex < 0 || fileIndex >= names.Count) return -1;
            var (thing, _) = BattleObjects.Split(names[fileIndex]);
            return thing == null ? -1 : IndexOf(names, thing, "Drawing");
        }

        /// <summary>The colours a drawing is meant to use, where the list says so.</summary>
        public static int ColoursFor(DirNames dir, int fileIndex)
        {
            var names = Names(dir);
            if (fileIndex < 0 || fileIndex >= names.Count) return -1;
            var (thing, part) = BattleObjects.Split(names[fileIndex]);
            if (thing == null || part == "Colours") return -1;
            return IndexOf(names, thing, "Colours");
        }

        /// <summary>The arrangement a drawing is laid out by, where the list says so.</summary>
        public static int ArrangementFor(DirNames dir, int fileIndex)
        {
            var names = Names(dir);
            if (fileIndex < 0 || fileIndex >= names.Count) return -1;
            var (thing, part) = BattleObjects.Split(names[fileIndex]);
            if (thing == null || part != "Drawing") return -1;
            return IndexOf(names, thing, "Arrangement");
        }

        private static int IndexOf(IReadOnlyList<string> names, string thing, string part)
        {
            for (int i = 0; i < names.Count; i++)
            {
                var (t, p) = BattleObjects.Split(names[i]);
                if (t == thing && p == part) return i;
            }
            return -1;
        }

        /// <summary>What one file is, for the line above the picture.</summary>
        public static string NameOf(DirNames dir, int fileIndex)
        {
            var names = Names(dir);
            if (fileIndex < 0 || fileIndex >= names.Count) return null;
            var (thing, part) = BattleObjects.Split(names[fileIndex]);
            if (thing == null) return null;
            string friendly = Friendly(dir, thing);
            return part == "File" ? friendly : $"{friendly}, {part.ToLowerInvariant()}";
        }
    }
}
