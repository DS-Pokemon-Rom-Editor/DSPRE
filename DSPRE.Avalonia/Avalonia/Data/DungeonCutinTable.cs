using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// The table saying which files make up each location splash screen.
    ///
    /// HeartGold shows a picture of where you are when you walk in, and there are twenty five of them.
    /// Each names, for four times of day, the tiles it is drawn from, the colours and the arrangement.
    /// Read once here so the Dungeon Cutin Editor and the Graphics window agree about it rather than each
    /// having their own copy of the field order.
    /// </summary>
    public static class DungeonCutinTable
    {
        /// <summary>Rows in the retail table.</summary>
        public const int RowCount = 25;

        public enum TimeOfDay { Morning, Noon, Evening, Night }

        public sealed class Row
        {
            public int Number;                 // 1-based, for showing
            public int HeaderIndex;            // which zone this is for
            public int WipeType;
            public int NameMessageId;
            /// <summary>Colours, tiles and arrangement for each of the four times of day.</summary>
            public (int Palette, int Tiles, int Screen)[] Art = new (int, int, int)[4];

            public (int Palette, int Tiles, int Screen) At(TimeOfDay t) => Art[(int)t];
        }

        /// <summary>Reads the table, or an empty list when this game has none. Only HeartGold and
        /// SoulSilver do.</summary>
        public static List<Row> Read()
        {
            var rows = new List<Row>();
            try
            {
                RomInfo.SetDungeonCutinTableOffsetToRAMAddress();
                if (RomInfo.dungeonCutinTableOffsetToRAMAddress == 0) return rows;

                uint ram = BitConverter.ToUInt32(ARM9.ReadBytes(RomInfo.dungeonCutinTableOffsetToRAMAddress, 4), 0);
                using var reader = new ARM9.Reader(ram - ARM9.address);
                for (int i = 0; i < RowCount; i++)
                {
                    var row = new Row
                    {
                        Number = i + 1,
                        HeaderIndex = reader.ReadInt32(),
                        WipeType = reader.ReadInt32(),
                    };
                    for (int t = 0; t < 4; t++)
                        row.Art[t] = (reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                    row.NameMessageId = reader.ReadInt32();
                    rows.Add(row);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("DungeonCutinTable.Read: " + ex.Message);
                rows.Clear();
            }
            return rows;
        }

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
            var units = new List<GraphicAssets.Unit>();
            var spokenFor = new HashSet<int>();

            List<string> headerNames = null;
            try { headerNames = HeaderLists.GetHeaderListBoxNames(); } catch { }

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
            var byFiles = new Dictionary<string, GraphicAssets.Unit>();

            foreach (var r in Read())
            {
                var key = string.Join(",", r.Art.SelectMany(x => new[] { x.Palette, x.Tiles, x.Screen }));
                if (byFiles.TryGetValue(key, out var already))
                {
                    string also = Where(r.HeaderIndex);
                    already.Name += also != null ? ", " + also : $", screen {r.Number}";
                    continue;
                }

                string place = Where(r.HeaderIndex);
                var u = new GraphicAssets.Unit
                {
                    Archive = archive,
                    Name = place != null ? $"Splash screen {r.Number}, {place}" : $"Splash screen {r.Number}",
                };
                byFiles[key] = u;

                for (int t = 0; t < 4; t++)
                {
                    var (pal, tiles, screen) = r.Art[t];
                    void Add(int index, string what)
                    {
                        if (index < 0 || index >= fileCount) return;
                        if (u.Parts.Exists(x => x.Index == index)) return;
                        u.Parts.Add(new GraphicAssets.UnitPart
                        {
                            Archive = archive, Index = index, Name = $"{WhenNames[t]}, {what}",
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
                var lone = new GraphicAssets.Unit { Archive = archive, Name = archive.Title };
                lone.Parts.Add(new GraphicAssets.UnitPart { Archive = archive, Index = i, Name = "File " + i });
                units.Add(lone);
            }

            units.Sort((x, y) => x.First.CompareTo(y.First));
            return units;
        }

        /// <summary>The drawing one location uses at a given time of day, for an editor handing it over.</summary>
        public static int DrawingFor(int rowNumber, TimeOfDay when)
        {
            foreach (var r in Read())
                if (r.Number == rowNumber) return r.At(when).Tiles;
            return -1;
        }
    }
}
