using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Tracking.Kinds
{
    /// <summary>A map file: permission grids as rows, buildings as a list, the 3D model and terrain as files next to the JSON.</summary>
    public sealed class MapFileKind : FieldKind
    {
        public override string Name => "map-file";
        public override string Folder => "maps/files";
        public override string Title(string id, ChangeContext ctx) => "Map " + id;

        public override bool Owns(TrackedFile file, out string id)
        {
            id = file.TryMember(out DirNames dir, out int member) && dir == DirNames.Maps ? Id(member) : null;
            return id != null;
        }

        public override IReadOnlyList<TrackedFile> Files(string id, ChangeContext ctx) => new[] { TrackedFile.Member(DirNames.Maps, Number(id)) };

        protected override string BlobExtension(string key) => key switch
        {
            "model" => ".nsbmd",
            "terrain" => ".bdhc",
            "sounds" => ".bgs",
            _ => ".bin",
        };

        protected override JsonObject Decode(string id, Func<TrackedFile, byte[]> read, ChangeContext ctx)
        {
            byte[] bytes = read(Files(id, ctx)[0]);
            if (bytes == null) return null;
            MapFile map = new MapFile(new MemoryStream(bytes), GameFamily, showMessages: false);
            return Fields.Write(f => Bind(f, map));
        }

        protected override Dictionary<TrackedFile, byte[]> Encode(string id, JsonObject record, Func<TrackedFile, byte[]> start, ChangeContext ctx)
        {
            TrackedFile file = Files(id, ctx)[0];
            byte[] bytes = start(file) ?? throw new InvalidOperationException("A new map needs a file to build from.");
            MapFile map = new MapFile(new MemoryStream(bytes), GameFamily, showMessages: false);
            Fields.Read(record, f => Bind(f, map));
            return new Dictionary<TrackedFile, byte[]> { [file] = map.ToByteArray() };
        }

        private static void Buildings(Fields f, MapFile map)
        {
            if (f.Reading)
            {
                map.Buildings = ((JsonArray)f.Json["buildings"]).Select(n => BuildingRecords.Read((JsonObject)n)).ToList();
                return;
            }
            f.Json["buildings"] = new JsonArray(map.Buildings.Select(b => (JsonNode)BuildingRecords.Write(b)).ToArray());
        }

        private void Bind(Fields f, MapFile map)
        {
            f.Grid("collision", MapFile.MapSize, MapFile.MapSize, (x, y) => map.Collisions[y, x], (x, y, v) => map.Collisions[y, x] = v);
            f.Grid("types", MapFile.MapSize, MapFile.MapSize, (x, y) => map.Types[y, x], (x, y, v) => map.Types[y, x] = v);
            Buildings(f, map);
            f.Bytes("buildings_tail", () => map.BuildingsTail, v => map.BuildingsTail = v);
            f.Text("model", () => Blob(map.MapModelData), v => map.MapModelData = BlobBytes(v));
            f.Text("terrain", () => Blob(map.Bdhc), v => map.Bdhc = BlobBytes(v));
            if (GameFamily == GameFamilies.HGSS) f.Text("sounds", () => Blob(map.Bgs), v => map.Bgs = BlobBytes(v));
        }
    }

    internal static class BuildingRecords
    {
        // Record bytes the Building class keeps but doesn't name: the scales' low bytes and the tail.
        private static readonly int[] Unnamed = { 0x1C, 0x1F, 0x20, 0x23, 0x24, 0x27, 0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E, 0x2F };

        public static JsonObject Write(Building building)
        {
            JsonObject json = ReflectedFields.Write(building, null);
            byte[] record = building.ToRecord();
            json["unknown"] = ChangesJson.Hex(Unnamed.Select(i => record[i]).ToArray());
            return json;
        }

        public static Building Read(JsonObject json)
        {
            byte[] record = new byte[MapFile.BuildingHeaderSize];
            byte[] unnamed = ChangesJson.FromHex(json["unknown"]?.GetValue<string>() ?? "");
            for (int i = 0; i < Unnamed.Length && i < unnamed.Length; i++) record[Unnamed[i]] = unnamed[i];
            Building building = new Building(new MemoryStream(record));
            ReflectedFields.Read(json, building, null);
            return building;
        }
    }

    /// <summary>A matrix: its size, name and the maps, headers and heights of each square, one row per line.</summary>
    public sealed class MatrixKind : FieldKind
    {
        public override string Name => "matrix";
        public override string Folder => "maps/matrices";
        public override string Title(string id, ChangeContext ctx) => "Matrix " + id;

        public override bool Owns(TrackedFile file, out string id)
        {
            id = file.TryMember(out DirNames dir, out int member) && dir == DirNames.Matrices ? Id(member) : null;
            return id != null;
        }

        public override IReadOnlyList<TrackedFile> Files(string id, ChangeContext ctx) => new[] { TrackedFile.Member(DirNames.Matrices, Number(id)) };

        protected override string SlugName(string id, ChangeContext ctx)
        {
            byte[] bytes = ctx.Project.Read(Files(id, ctx)[0]) ?? ctx.Base.Read(Files(id, ctx)[0]);
            return bytes == null ? null : new GameMatrix(new MemoryStream(bytes)).Name;
        }

        protected override JsonObject Decode(string id, Func<TrackedFile, byte[]> read, ChangeContext ctx)
        {
            byte[] bytes = read(Files(id, ctx)[0]);
            if (bytes == null) return null;
            GameMatrix matrix = new GameMatrix(new MemoryStream(bytes));
            return Fields.Write(f => Bind(f, matrix));
        }

        protected override Dictionary<TrackedFile, byte[]> Encode(string id, JsonObject record, Func<TrackedFile, byte[]> start, ChangeContext ctx)
        {
            TrackedFile file = Files(id, ctx)[0];
            byte[] bytes = start(file);
            GameMatrix matrix = bytes == null ? new GameMatrix() : new GameMatrix(new MemoryStream(bytes));
            Fields.Read(record, f => Bind(f, matrix));
            return new Dictionary<TrackedFile, byte[]> { [file] = matrix.ToByteArray() };
        }

        private static void Bind(Fields f, GameMatrix m)
        {
            f.Text("name", () => m.Name, v => m.Name = v);
            f.Int("width", () => m.Width, v => m.Width = (byte)v);
            f.Int("height", () => m.Height, v => m.Height = (byte)v);
            f.Bool("has_headers", () => m.HasHeadersSection, v => m.HasHeadersSection = v);
            f.Bool("has_heights", () => m.HasHeightsSection, v => m.HasHeightsSection = v);
            Rows(f, "maps", m.Height, m.Width, () => m.Maps, v => m.Maps = v, true);
            if (m.HasHeadersSection || f.Reading && f.Json.ContainsKey("headers")) Rows(f, "headers", m.Height, m.Width, () => m.Headers, v => m.Headers = v, true);
            if (m.HasHeightsSection || f.Reading && f.Json.ContainsKey("heights"))
                Rows(f, "heights", m.Height, m.Width, () => Widen(m.Altitudes), v => m.Altitudes = Narrow(v), false);
        }

        private static void Rows(Fields f, string key, int height, int width, Func<ushort[,]> get, Action<ushort[,]> set, bool wide)
        {
            if (f.Reading)
            {
                JsonObject rows = (JsonObject)f.Json[key];
                ushort[,] grid = new ushort[height, width];
                for (int y = 0; y < height; y++)
                {
                    JsonArray row = (JsonArray)rows[y.ToString(System.Globalization.CultureInfo.InvariantCulture)];
                    for (int x = 0; x < width; x++) grid[y, x] = (ushort)row[x].GetValue<int>();
                }
                set(grid);
                return;
            }
            ushort[,] values = get();
            JsonObject result = new JsonObject();
            for (int y = 0; y < height; y++)
                result[y.ToString(System.Globalization.CultureInfo.InvariantCulture)] = new JsonArray(Enumerable.Range(0, width).Select(x => (JsonNode)(int)values[y, x]).ToArray());
            f.Json[key] = result;
        }

        private static ushort[,] Widen(byte[,] grid)
        {
            if (grid == null) return new ushort[0, 0];
            ushort[,] wide = new ushort[grid.GetLength(0), grid.GetLength(1)];
            for (int y = 0; y < grid.GetLength(0); y++)
                for (int x = 0; x < grid.GetLength(1); x++) wide[y, x] = grid[y, x];
            return wide;
        }

        private static byte[,] Narrow(ushort[,] grid)
        {
            byte[,] narrow = new byte[grid.GetLength(0), grid.GetLength(1)];
            for (int y = 0; y < grid.GetLength(0); y++)
                for (int x = 0; x < grid.GetLength(1); x++) narrow[y, x] = (byte)grid[y, x];
            return narrow;
        }
    }
}
