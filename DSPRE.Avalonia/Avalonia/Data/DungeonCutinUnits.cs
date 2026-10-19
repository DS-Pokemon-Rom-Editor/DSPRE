using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>The splash screens as the Graphics window lists them, from <see cref="DungeonCutinTable"/>.</summary>
    public static class DungeonCutinUnits
    {
        private static readonly string[] WhenNames = { "Morning", "Noon", "Evening", "Night" };

        /// <summary>
        /// Rows for the Graphics window: one splash screen, with the files each time of day uses.
        ///
        /// Most locations reuse their noon art for the other three, so the same file turns up more than
        /// once and is only listed the first time. Two pairs of locations go further and share the whole
        /// screen: in HeartGold rows 4 and 5 both name files 27 to 35, and rows 17 and 18 both name 150
        /// to 158. Those become one row naming both places, because listing them twice would say the
        /// files exist twice.
        /// </summary>
        public static List<GraphicAssets.Unit> UnitsFor(GraphicAssets.Archive archive, int fileCount)
        {
            List<GraphicAssets.Unit> units = new List<GraphicAssets.Unit>();
            HashSet<int> spokenFor = new HashSet<int>();

            List<string> headerNames = null;
            try { headerNames = HeaderLists.GetHeaderListBoxNames(); } catch (Exception ex) { AppLogger.Warn("Dungeon cut-ins: header names: " + ex.Message); }

            // The list names read "323 -   D24R0211" with the internal name padded out with zero bytes.
            // Only the name is wanted here, without the number or the padding.
            string Where(int header)
            {
                if (headerNames == null || header < 0 || header >= headerNames.Count) return null;
                string name = headerNames[header];
                if (string.IsNullOrWhiteSpace(name)) return null;
                int dash = name.IndexOf('-');
                if (dash >= 0) name = name.Substring(dash + 1);
                name = name.Replace("\0", "").Trim();
                return string.IsNullOrWhiteSpace(name) ? null : name;
            }

            // Rows naming the same files are the same screen shown in more than one place.
            Dictionary<string, GraphicAssets.Unit> byFiles = new Dictionary<string, GraphicAssets.Unit>();

            foreach (DungeonCutinTable.Row r in DungeonCutinTable.Read())
            {
                string key = string.Join(",", r.Art.SelectMany(x => new[] { x.Palette, x.Tiles, x.Screen }));
                if (byFiles.TryGetValue(key, out GraphicAssets.Unit already))
                {
                    string also = Where(r.HeaderIndex);
                    already.Name += also != null ? ", " + also : $", screen {r.Number}";
                    continue;
                }

                string place = Where(r.HeaderIndex);
                GraphicAssets.Unit u = new GraphicAssets.Unit
                {
                    Archive = archive,
                    Name = place != null ? $"Splash screen {r.Number} for {place}" : $"Splash screen {r.Number}",
                };
                byFiles[key] = u;

                for (int t = 0; t < 4; t++)
                {
                    (int pal, int tiles, int screen) = r.Art[t];
                    void Add(int index, string what)
                    {
                        if (index < 0 || index >= fileCount) return;
                        if (u.Parts.Exists(x => x.Index == index)) return;
                        u.Parts.Add(new GraphicAssets.UnitPart
                        {
                            Archive = archive, Index = index, Name = $"{WhenNames[t]} {what.ToLowerInvariant()}",
                        });
                        spokenFor.Add(index);
                    }
                    Add(tiles, "drawing");
                    Add(pal, "colours");
                    Add(screen, "arrangement");
                }

                if (u.Parts.Count > 0) units.Add(u);
            }

            for (int i = 0; i < fileCount; i++)
            {
                if (spokenFor.Contains(i)) continue;
                GraphicAssets.Unit lone = new GraphicAssets.Unit { Archive = archive, Name = archive.Title };
                lone.Parts.Add(new GraphicAssets.UnitPart { Archive = archive, Index = i, Name = "File " + i });
                units.Add(lone);
            }

            units.Sort((x, y) => x.First.CompareTo(y.First));
            return units;
        }
    }
}
