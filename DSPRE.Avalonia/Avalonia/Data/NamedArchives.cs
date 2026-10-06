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
        /// <summary>HeartGold keeps field weather in its own archive; the other games in synthOverlay.</summary>
        public static DirNames WeatherDir => gameFamily == GameFamilies.HGSS ? DirNames.weatherGraphics : DirNames.synthOverlay;

        private static bool IsWeather(DirNames dir) => dir == DirNames.weatherGraphics || dir == DirNames.synthOverlay;

        /// <summary>The names for one archive in the game that is open, or empty when there are none.</summary>
        public static IReadOnlyList<string> Names(DirNames dir)
        {
            string packed = null;
            try
            {
                bool johto = gameFamily == GameFamilies.HGSS;
                if (dir == WeatherDir)
                    packed = johto ? ArchiveEntryNames.WeatherHeartGold : ArchiveEntryNames.WeatherPlatinum;
                else if (dir == DirNames.fonts)
                    packed = johto ? ArchiveEntryNames.FontHeartGold : ArchiveEntryNames.FontPlatinum;
            }
            catch { }

            if (packed == null) return Array.Empty<string>();
            List<string> names = packed.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                              .Select(n => n == "-" ? "" : n)
                              .ToList();
            // Diamond and Pearl lack the two three-file weather sets Platinum keeps at entries 55 to 60.
            if (dir == DirNames.synthOverlay && gameFamily == GameFamilies.DP && names.Count >= 61)
                names.RemoveRange(55, 6);
            return names;
        }

        /// <summary>What to call a thing, in the words somebody looking for it would use.</summary>
        public static string Friendly(DirNames dir, string thing)
        {
            if (string.IsNullOrEmpty(thing)) return null;

            if (IsWeather(dir))
            {
                foreach ((string name, string says) in Weather)
                    if (thing.Equals(name, StringComparison.Ordinal)) return says;
                return Pretty(thing);
            }

            if (dir == DirNames.fonts)
            {
                foreach ((string name, string says) in Fonts)
                    if (thing.Equals(name, StringComparison.Ordinal)) return says;
                return Pretty(thing);
            }

            return Pretty(thing);
        }

        // What each weather looks like in play.
        private static readonly (string Name, string Says)[] Weather =
        {
            ("Weather.Ash", "Falling ash"),
            ("Weather.Rain", "Rain"),
            ("Weather.HeavyRain", "Heavy rain"),
            ("Weather.Rainbow", "Rainbow"),
            ("Weather.Rainbow.Screen", "Rainbow arrangement"),
            ("Weather.Shimmer", "Mysterious shimmer"),
            ("Weather.Snow", "Snow"),
            ("Weather.DeepSnow", "Deep snow"),
            ("Weather.Blizzard", "Blizzard"),
            ("Weather.Sparks", "Sparks"),
            ("Weather.Sandstorm", "Sandstorm"),
            ("Weather.Sandstorm.Backdrop", "Sandstorm backdrop"),
            ("Weather.Sandstorm.Screen", "Sandstorm arrangement"),
            ("Weather.VolcanicAsh", "Volcanic ash"),
            ("Weather.VolcanicAsh.Backdrop", "Volcanic ash backdrop"),
            ("Weather.Overcast", "Overcast sky"),
            ("Weather.Haze", "Mystical haze"),
            ("Weather.Fog", "Fog colours"),
            ("Weather.Flash", "Lightning flash"),
            ("Weather.SunThroughTrees", "Sunlight through trees"),
            ("Weather.CaveDarkness", "Cave darkness"),
            ("Weather.SharedCells", "Shared layout data"),
            ("Weather.SharedAnimation", "Shared animation data"),
            ("Weather.SharedDrawing", "Shared drawing data"),
            ("Weather.SharedColours", "Shared colour data"),
        };

        private static readonly (string Name, string Says)[] Fonts =
        {
            ("Font.System", "System font"),
            ("Font.Message", "Dialogue font"),
            ("Font.TouchScreen", "Touch screen font"),
            ("Font.Unown", "Unown font"),
            ("Font.SpecialCharacters", "Special characters"),
            ("Font.ScreenIndicators", "Screen indicators"),
            ("Font.Extra", "Extra font colours"),
            ("Font.4", "Font 4"),
            ("Font.5", "Font 5"),
        };

        private static string Pretty(string name) =>
            string.IsNullOrEmpty(name) ? name : name.Split('.')[^1];

        /// <summary>One row per thing, with its pieces together, in the order the game lists them.</summary>
        public static List<GraphicAssets.Unit> Units(GraphicAssets.Archive a, int fileCount)
        {
            IReadOnlyList<string> names = Names(a.Dir);
            List<GraphicAssets.Unit> units = new List<GraphicAssets.Unit>();
            HashSet<int> spokenFor = new HashSet<int>();

            List<string> order = new List<string>();
            Dictionary<string, List<(int Index, string Part)>> pieces = new Dictionary<string, List<(int Index, string Part)>>();

            for (int i = 0; i < fileCount && i < names.Count; i++)
            {
                (string thing, string part) = BattleObjects.Split(names[i]);
                if (thing == null) continue;
                if (!pieces.TryGetValue(thing, out List<(int Index, string Part)> list))
                {
                    pieces[thing] = list = new List<(int, string)>();
                    order.Add(thing);
                }
                list.Add((i, part));
                spokenFor.Add(i);
            }

            foreach (string thing in order)
            {
                GraphicAssets.Unit u = new GraphicAssets.Unit { Archive = a, Name = Friendly(a.Dir, thing) };
                foreach ((int index, string part) in pieces[thing].OrderBy(p => Rank(p.Part)).ThenBy(p => p.Index))
                    u.Parts.Add(new GraphicAssets.UnitPart { Archive = a, Index = index, Name = part });
                units.Add(u);
            }

            for (int i = 0; i < fileCount; i++)
            {
                if (spokenFor.Contains(i)) continue;
                GraphicAssets.Unit lone = new GraphicAssets.Unit { Archive = a, Name = a.Title };
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
            IReadOnlyList<string> names = Names(dir);
            if (fileIndex < 0 || fileIndex >= names.Count) return -1;
            (string thing, string _) = BattleObjects.Split(names[fileIndex]);
            return thing == null ? -1 : IndexOf(names, thing, "Drawing");
        }

        /// <summary>The colours a drawing is meant to use, where the list says so.</summary>
        public static int ColoursFor(DirNames dir, int fileIndex)
        {
            IReadOnlyList<string> names = Names(dir);
            if (fileIndex < 0 || fileIndex >= names.Count) return -1;
            (string thing, string part) = BattleObjects.Split(names[fileIndex]);
            if (thing == null || part == "Colours") return -1;
            return IndexOf(names, thing, "Colours");
        }

        /// <summary>The arrangement a drawing is laid out by, where the list says so.</summary>
        public static int ArrangementFor(DirNames dir, int fileIndex)
        {
            IReadOnlyList<string> names = Names(dir);
            if (fileIndex < 0 || fileIndex >= names.Count) return -1;
            (string thing, string part) = BattleObjects.Split(names[fileIndex]);
            if (thing == null || part != "Drawing") return -1;
            return IndexOf(names, thing, "Arrangement");
        }

        private static int IndexOf(IReadOnlyList<string> names, string thing, string part)
        {
            for (int i = 0; i < names.Count; i++)
            {
                (string t, string p) = BattleObjects.Split(names[i]);
                if (t == thing && p == part) return i;
            }
            return -1;
        }

        /// <summary>What one file is, for the line above the picture.</summary>
        public static string NameOf(DirNames dir, int fileIndex)
        {
            IReadOnlyList<string> names = Names(dir);
            if (fileIndex < 0 || fileIndex >= names.Count) return null;
            (string thing, string part) = BattleObjects.Split(names[fileIndex]);
            if (thing == null) return null;
            string friendly = Friendly(dir, thing);
            return part == "File" ? friendly : $"{friendly}, {part.ToLowerInvariant()}";
        }
    }
}
