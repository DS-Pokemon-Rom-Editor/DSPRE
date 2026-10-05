using System;
using System.Collections.Generic;
using System.IO;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Works out a species' habitat for the DP/Pt AREA page the way pokeplatinum's pokedex_enc_data.c and
    /// pokedex_field_map.c do: each place in the species' list sets the cells of its field record, empty cells next
    /// to set ones become edge pieces, and caves get a marker each.
    /// </summary>
    public static class DexHabitatBuilder
    {
        public const int MapSide = 30;
        private const int FieldRecord = 36;   // four bytes of placement, then a 32-cell matrix
        // Spring Path's cave stays hidden until the path opens; it is the one place the game hides by default.
        private static readonly int[] HiddenDungeons = { 10 };

        /// <param name="time">0 morning, 1 day, 2 night.</param>
        /// <param name="national">Whether the special list is the one used once the National Pokédex is obtained.</param>
        public static DexHabitatData Build(int species, int time, bool national)
        {
            if (gameFamily == GameFamilies.HGSS || !PokedexAreaData.TryLoad(out PokedexAreaData areas, out _)) return null;
            if (species < 0 || species >= areas.SpeciesCount) return null;
            int[][] lists = areas.Get(species);
            string folder = gameDirs[DirNames.pokedexAreas].unpackedDir;
            byte[] dungeons = File.ReadAllBytes(Path.Combine(folder, "0000"));
            byte[] fields = File.ReadAllBytes(Path.Combine(folder, "0002"));

            int t = Math.Clamp(time, 0, 2), special = national ? 4 : 3;
            DexHabitatData data = new();
            int[] ownFields = lists[areas.List(1, t)], specialFields = lists[areas.List(1, special)];
            Locate(data.Normal, fields, ownFields, Array.Empty<int>());
            // A place already shown for the time is not drawn again underneath.
            Locate(data.Special, fields, specialFields, ownFields);
            Smooth(data.Normal);
            Smooth(data.Special);

            int[] ownDungeons = lists[areas.List(0, t)], specialDungeons = lists[areas.List(0, special)];
            AddDungeons(data, dungeons, ownDungeons, HiddenDungeons, 2, 0);
            List<int> skip = new(HiddenDungeons);
            skip.AddRange(ownDungeons);
            AddDungeons(data, dungeons, specialDungeons, skip.ToArray(), 3, 1);
            return data;
        }

        // ── HGSS: one translucent block per place over the region map ─────────────────────────────────

        private const int KantoGrid = 22, MapLeft = 64, MapTop = 40, CaveLeft = MapLeft + 4, CaveTop = MapTop + 4;
        // Block sizes in 8-pixel cells and the animation that shows each: 1x1, 2x1 .. 6x1, 1x2 .. 1x5, 2x2, 3x2.
        private const int Block1x1 = 0, Block1x2 = 6, Block2x2 = 10, Block3x2 = 11;

        // Zones the page treats on their own (pokeheartgold's MAP_ constants).
        private const int Route2 = 10, Route2East = 414, Route10 = 18, Route16 = 24, Route16East = 422;
        // The editor's sample player stands at home.
        private const int NewBarkTown = 60;
        private static readonly int[] VictoryRoad = { 124, 178, 179 }, NationalPark = { 96, 487 };
        private static readonly int[] RuinsOfAlph = { 113, 315, 490, 491, 492 };
        private static readonly int[] DiglettsCave = { 106 }, IcePath = { 120, 237, 238, 239 }, DarkCave = { 123, 176 };

        /// <summary>
        /// The places on the HGSS AREA page for a time and region: each route or town as a block sized to its area,
        /// each cave as a small block, and markers on the far exits of caves that cross the map.
        /// </summary>
        /// <param name="kanto">Whether the Kanto half of the map is the one shown.</param>
        public static DexHabitatData BuildHgss(int species, int time, bool kanto)
        {
            if (gameFamily != GameFamilies.HGSS || !PokedexAreaData.TryLoad(out PokedexAreaData areas, out _)) return null;
            if (species < 0 || species >= areas.SpeciesCount) return null;
            int[][] lists = areas.Get(species);
            string folder = gameDirs[DirNames.pokedexAreas].unpackedDir;
            byte[] caveGrid = File.ReadAllBytes(Path.Combine(folder, "0000"));
            byte[] fieldGrid = File.ReadAllBytes(Path.Combine(folder, "0001"));
            int[] fieldZones = ZoneTable(pokedexFieldZoneTableOffset), caveZones = ZoneTable(pokedexDungeonZoneTableOffset);
            int region = kanto ? 1 : 0, t = Math.Clamp(time, 0, 2);
            DexHabitatData data = new() { Player = PlayerAt(fieldGrid, fieldZones, kanto) };
            int Zone(int[] table, int id) => table != null && id >= 0 && id < table.Length ? table[id] : -1;
            bool InRegion(byte[] grid, int size, int id) => id * size < grid.Length && (grid[id * size] >= KantoGrid) == kanto;

            // Routes and towns first, then caves, each once (pokeheartgold ov18_021E8528).
            List<int> fields = new(), caves = new();
            foreach (int list in new[] { areas.List(1, t), areas.List(1, 3) })
                foreach (int raw in lists[list])
                {
                    if (!InRegion(fieldGrid, FieldRecord, raw)) continue;
                    int id = raw;
                    // Route 2 and Route 16 are drawn as one place each on the Kanto map.
                    if (kanto && Zone(fieldZones, id) is Route2 or Route2East) id = Array.IndexOf(fieldZones, Route2);
                    else if (kanto && Zone(fieldZones, id) is Route16 or Route16East) id = Array.IndexOf(fieldZones, Route16);
                    if (id >= 0 && !fields.Contains(id)) fields.Add(id);
                }
            foreach (int list in new[] { areas.List(0, t), areas.List(0, 3) })
                foreach (int id in lists[list])
                    if (InRegion(caveGrid, 4, id) && !caves.Contains(id)) caves.Add(id);

            foreach (int id in fields)
            {
                int at = id * FieldRecord;
                int gx = fieldGrid[at], gy = fieldGrid[at + 1], sx = fieldGrid[at + 2], sy = fieldGrid[at + 3];
                int zone = Zone(fieldZones, id);
                if (zone is Route2 or Route2East) (gx, gy, sx, sy) = (32, 4, 1, 3);
                else if (zone == Route10) (sx, sy) = (1, 2);
                else if (zone is Route16 or Route16East) (gx, gy, sx, sy) = (35, 8, 2, 1);
                data.Dungeons.Add(((gx - region * KantoGrid) * 8 + sx * 4 + MapLeft, gy * 8 + sy * 4 + MapTop, BlockFor(sx, sy)));
            }
            int exits = 0;
            foreach (int id in caves)
            {
                int x = (caveGrid[id * 4] - region * KantoGrid) * 8 + CaveLeft, y = caveGrid[id * 4 + 1] * 8 + CaveTop, block = Block1x1;
                int zone = Zone(caveZones, id);
                if (Array.IndexOf(VictoryRoad, zone) >= 0 || Array.IndexOf(RuinsOfAlph, zone) >= 0) { block = Block1x2; y += 4; }
                else if (Array.IndexOf(NationalPark, zone) >= 0) { x += 4; y += 4; }
                if (Array.IndexOf(DiglettsCave, zone) >= 0) exits |= 1;
                else if (Array.IndexOf(IcePath, zone) >= 0) exits |= 2;
                else if (Array.IndexOf(DarkCave, zone) >= 0) exits |= 4;
                data.Dungeons.Add((x, y, block));
            }
            if ((exits & 1) != 0) data.Dungeons.Add(((32 - KantoGrid) * 8 + CaveLeft, 4 * 8 + CaveTop, Block1x1));
            if ((exits & 2) != 0) data.Dungeons.Add((21 * 8 + CaveLeft, 4 * 8 + CaveTop, Block1x1));
            if ((exits & 4) != 0)
            {
                data.Dungeons.Add((20 * 8 + CaveLeft, 6 * 8 + CaveTop, Block1x1));
                data.Dungeons.Add((19 * 8 + CaveLeft, 10 * 8 + CaveTop, Block1x1));
            }

            // The list names each place's zone, sorted the way the page sorts it (pokeheartgold's ov18_021E8A00).
            List<int> zones = new();
            foreach (int id in fields) zones.Add(Zone(fieldZones, id));
            foreach (int id in caves) zones.Add(Zone(caveZones, id));
            int[] order = SortTable();
            int Rank(int zone) { int at = order == null ? -1 : Array.IndexOf(order, zone); return at < 0 ? 0 : at; }
            for (int i = 0; i < zones.Count - 1; i++)
            {
                int first = zones[i];
                for (int j = i + 1; j < zones.Count; j++)
                {
                    int other = zones[j];
                    if (Rank(first) <= Rank(other)) continue;
                    (zones[i], zones[j]) = (zones[j], zones[i]);
                    first = other;
                }
            }
            foreach (int zone in zones)
            {
                string name = null;
                try { name = HeaderLabels.LocationNameOf(MapHeader.GetMapHeader((ushort)zone)); } catch { }
                data.Places.Add(string.IsNullOrWhiteSpace(name) ? "Map " + zone : name);
            }
            return data;
        }

        /// <summary>The HGSS AREA page with no places: the map and the player's face only.</summary>
        public static DexHabitatData HgssPlayerOnly(bool kanto)
        {
            if (gameFamily != GameFamilies.HGSS) return null;
            string folder = gameDirs[DirNames.pokedexAreas].unpackedDir;
            byte[] fieldGrid = File.ReadAllBytes(Path.Combine(folder, "0001"));
            return new DexHabitatData { Player = PlayerAt(fieldGrid, ZoneTable(pokedexFieldZoneTableOffset), kanto) };
        }

        // The face sits on the player's map block, four pixels in, and only on its own region's map.
        private static (int X, int Y)? PlayerAt(byte[] fieldGrid, int[] fieldZones, bool kanto)
        {
            int id = fieldZones == null ? -1 : Array.IndexOf(fieldZones, NewBarkTown);
            if (id < 0 || (id + 1) * FieldRecord > fieldGrid.Length) return null;
            int gx = fieldGrid[id * FieldRecord], gy = fieldGrid[id * FieldRecord + 1];
            if ((gx >= KantoGrid + 1) != kanto) return null;
            return ((gx - (kanto ? KantoGrid : 0)) * 8 + CaveLeft, gy * 8 + CaveTop);
        }

        private static int[] SortTable()
        {
            if (pokedexAreaSortTableOffset < 0 || pokedexAreaSortTableCount <= 0) return null;
            try
            {
                byte[] overlay = File.ReadAllBytes(OverlayUtils.GetPath(pokedexAreaZoneOverlay));
                int[] zones = new int[pokedexAreaSortTableCount];
                for (int i = 0; i < zones.Length; i++) zones[i] = BitConverter.ToInt32(overlay, pokedexAreaSortTableOffset + i * 4);
                return zones;
            }
            catch { return null; }
        }

        private static int BlockFor(int sx, int sy) => sx switch
        {
            1 => sy switch { 1 => Block1x1, >= 2 and <= 5 => Block1x2 + sy - 2, _ => Block1x1 },
            2 => sy == 1 ? 1 : Block2x2,
            3 => sy == 1 ? 2 : Block3x2,
            >= 4 and <= 6 => sx - 1,
            _ => Block1x1,
        };

        // A zone table in overlay 18, read up to its 0xFFFF end (the first entry is 0xFFFF too).
        private static int[] ZoneTable(int offset)
        {
            if (offset < 0) return null;
            try
            {
                if (OverlayUtils.IsCompressed(pokedexAreaZoneOverlay)) OverlayUtils.Decompress(pokedexAreaZoneOverlay);
                byte[] overlay = File.ReadAllBytes(OverlayUtils.GetPath(pokedexAreaZoneOverlay));
                List<int> zones = new() { -1 };
                for (int at = offset + 2; at + 2 <= overlay.Length; at += 2)
                {
                    int zone = BitConverter.ToUInt16(overlay, at);
                    if (zone == 0xFFFF) break;
                    zones.Add(zone);
                }
                return zones.ToArray();
            }
            catch { return null; }
        }

        private static void Locate(byte[] map, byte[] fields, int[] places, int[] skip)
        {
            foreach (int place in places)
            {
                if (Array.IndexOf(skip, place) >= 0) continue;
                int at = place * FieldRecord;
                if (at + FieldRecord > fields.Length) continue;
                // The record is y, x, height, width (FieldCoordinates), x being the grid's slow index.
                int y0 = fields[at], x0 = fields[at + 1], h = fields[at + 2], w = fields[at + 3];
                for (int x = x0; x < x0 + w; x++)
                    for (int y = y0; y < y0 + h; y++)
                    {
                        int cell = (x - x0) * h + (y - y0);
                        if (x >= MapSide || y >= MapSide || cell >= 32) continue;
                        map[x * MapSide + y] |= fields[at + 4 + cell];
                    }
            }
        }

        private static void AddDungeons(DexHabitatData data, byte[] coordinates, int[] places, int[] skip, int seq, int coronetSeq)
        {
            foreach (int place in places)
            {
                if (Array.IndexOf(skip, place) >= 0) continue;
                int at = place * 4;
                if (at + 4 > coordinates.Length) continue;
                data.Dungeons.Add((coordinates[at], coordinates[at + 1], coordinates[at + 2] != 0 ? coronetSeq : seq));
            }
        }

        // ── edge pieces, as pokedex_field_map.c picks them ────────────────────────────────────────────

        private const int West = 1, East = 2, South = 4, North = 8, SouthWest = 16, NorthWest = 32, SouthEast = 64, NorthEast = 128;

        private static readonly Dictionary<int, int> Pieces = new()
        {
            [0] = 0, [West] = 2, [East] = 3, [South] = 4, [North] = 5, [West | East] = 6, [West | South] = 7,
            [West | North] = 8, [East | South] = 9, [East | North] = 10, [South | North] = 11, [West | East | South] = 12,
            [West | East | North] = 13, [West | South | North] = 14, [East | South | North] = 15,
            [West | East | South | North] = 16, [SouthWest] = 17, [NorthWest] = 18, [SouthEast] = 19, [NorthEast] = 20,
            [SouthWest | NorthWest] = 21, [SouthWest | SouthEast] = 22, [SouthWest | NorthEast] = 23,
            [NorthWest | SouthEast] = 24, [NorthWest | NorthEast] = 25, [SouthEast | NorthEast] = 26,
            [SouthWest | NorthWest | SouthEast] = 27, [SouthWest | NorthWest | NorthEast] = 28,
            [SouthWest | SouthEast | NorthEast] = 29, [NorthWest | SouthEast | NorthEast] = 30,
            [SouthWest | NorthWest | SouthEast | NorthEast] = 31, [West | SouthEast | NorthEast] = 32,
            [East | SouthWest | NorthWest] = 33, [South | NorthWest | NorthEast] = 34, [North | SouthWest | SouthEast] = 35,
            [West | South | NorthEast] = 36, [West | North | SouthEast] = 37, [East | South | NorthWest] = 38,
            [East | North | SouthWest] = 39, [West | SouthEast] = 40, [West | NorthEast] = 41, [East | SouthWest] = 42,
            [East | NorthWest] = 43, [South | NorthWest] = 44, [South | NorthEast] = 45, [North | SouthWest] = 46,
            [North | SouthEast] = 47,
        };

        private static int At(byte[] map, int y, int x)
            => y < 0 || y >= MapSide || x < 0 || x >= MapSide ? -1 : map[x * MapSide + y];

        // Cells are smoothed in place, in the game's order, so a cell already given an edge piece no longer counts
        // as set for the ones after it (only value 1 is a neighbour).
        private static void Smooth(byte[] map)
        {
            for (int x = 0; x < MapSide; x++)
                for (int y = 0; y < MapSide; y++)
                {
                    if (map[x * MapSide + y] != 0) continue;
                    int n = 0;
                    if (At(map, y, x - 1) == 1) n |= West;
                    if (At(map, y, x + 1) == 1) n |= East;
                    if (At(map, y - 1, x) == 1) n |= South;
                    if (At(map, y + 1, x) == 1) n |= North;
                    if ((n & (West | South)) == 0 && At(map, y - 1, x - 1) == 1) n |= SouthWest;
                    if ((n & (West | North)) == 0 && At(map, y + 1, x - 1) == 1) n |= NorthWest;
                    if ((n & (East | South)) == 0 && At(map, y - 1, x + 1) == 1) n |= SouthEast;
                    if ((n & (East | North)) == 0 && At(map, y + 1, x + 1) == 1) n |= NorthEast;
                    map[x * MapSide + y] = (byte)(Pieces.TryGetValue(n, out int piece) ? piece : 0);
                }
        }
    }
}
