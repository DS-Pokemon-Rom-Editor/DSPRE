using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Frames as one indexed PNG of 128x128 cells, colour 0 the background; what each cell is goes in a JSON beside it.</summary>
    public static class TrainerSpriteSheet
    {
        public const int CellSize = TrainerSpriteFrames.Canvas;
        public static int CellX(int column) => column * CellSize;
        public static int CellY(int row) => row * CellSize;
        public static int Width(int columns) => columns * CellSize;
        public static int Height(int rows) => rows * CellSize;

        public sealed record Cell(int[] Canvas);

        public static (byte[] Indices, uint[] Palette, int Width, int Height) Compose(IReadOnlyList<IReadOnlyList<Cell>> rows, IReadOnlyList<uint[]> palettes)
        {
            int columns = rows.Max(r => r.Count);
            int w = Width(columns), h = Height(rows.Count);
            List<uint> colours = new List<uint>();
            foreach (uint[] p in palettes) for (int i = 0; i < 16; i++) colours.Add(0xFF000000 | (i < p.Length ? p[i] : 0));


            byte[] px = new byte[w * h];
            for (int r = 0; r < rows.Count; r++)
            {
                int y0 = CellY(r);
                for (int c = 0; c < rows[r].Count; c++)
                {
                    int x0 = CellX(c);
                    Cell cell = rows[r][c];
                    if (cell.Canvas == null) continue;
                    for (int y = 0; y < CellSize; y++)
                        for (int x = 0; x < CellSize; x++)
                        {
                            int v = cell.Canvas[y * CellSize + x];
                            if ((v & 0xF) != 0 && v < palettes.Count * 16) px[(y0 + y) * w + x0 + x] = (byte)v;
                        }
                }
            }
            return (px, colours.ToArray(), w, h);
        }

        public sealed class Read
        {
            public int Columns, Rows;
            public List<uint> Colours = new();
            public List<int[]> Cells = new();
            public bool Indexed;

            public int[] CellAt(int row, int column) => Cells[row * Columns + column];
            public static bool IsEmpty(int[] cell) => cell.All(v => v == 0);
        }

        public static Read Open(byte[] png, out string why)
        {
            why = null;
            int w, h;
            Read sheet = new Read();
            int[] ids;
            if (IndexedPng.TryRead(png, out byte[] indices, out uint[] plte, out w, out h))
            {
                sheet.Indexed = true;
                sheet.Colours.AddRange(plte.Select(c => c | 0xFF000000));
                ids = new int[w * h];
                for (int i = 0; i < ids.Length; i++)
                {
                    if (indices[i] >= plte.Length) { why = $"The picture uses colour {indices[i]}, which its own palette does not have."; return null; }
                    ids[i] = (plte[indices[i]] >> 24) < 128 ? 0 : indices[i];
                }
            }
            else if (AnyPng.TryReadRgba(png, out byte[] rgba, out w, out h, out why))
            {
                uint Colour(int i) => rgba[i * 4 + 3] < 128 ? 0 : 0xFF000000u | ((uint)rgba[i * 4] << 16) | ((uint)rgba[i * 4 + 1] << 8) | rgba[i * 4 + 2];
                Dictionary<uint, int> seen = new Dictionary<uint, int>();
                // Without a palette the background is the top left pixel.
                uint bg = Colour(0);
                seen[bg] = 0; seen[0] = 0;
                sheet.Colours.Add(bg == 0 ? 0xFF000000 : bg);
                ids = new int[w * h];
                if (!Fits(w, h, out int cols, out int rows, out why)) return null;
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                        for (int y = 0; y < CellSize; y++)
                            for (int x = 0; x < CellSize; x++)
                            {
                                int i = (CellY(r) + y) * w + CellX(c) + x;
                                uint col = Colour(i);
                                if (!seen.TryGetValue(col, out int id)) { id = sheet.Colours.Count; seen[col] = id; sheet.Colours.Add(col); }
                                ids[i] = id;
                            }
            }
            else return null;

            if (!Fits(w, h, out sheet.Columns, out sheet.Rows, out why)) return null;
            for (int r = 0; r < sheet.Rows; r++)
                for (int c = 0; c < sheet.Columns; c++)
                {
                    int[] cell = new int[CellSize * CellSize];
                    for (int y = 0; y < CellSize; y++)
                        for (int x = 0; x < CellSize; x++)
                            cell[y * CellSize + x] = ids[(CellY(r) + y) * w + CellX(c) + x];
                    sheet.Cells.Add(cell);
                }
            return sheet;
        }

        private static bool Fits(int w, int h, out int columns, out int rows, out string why)
        {
            columns = w / CellSize; rows = h / CellSize;
            why = null;
            if (columns < 1 || rows < 1 || Width(columns) != w || Height(rows) != h)
            {
                why = $"The picture is {w}x{h}, which is not a sheet of whole {CellSize}x{CellSize} cells. Export a sheet to start from.";
                return false;
            }
            return true;
        }

        // ── colours ────────────────────────────────────────────────────────────

        /// <summary>Sheet colour n is palette n / 16, colour n % 16.</summary>
        public static int[] WithSheetColours(int[] cell, int[] palettesUnder, int paletteCount, string where, out string why)
        {
            why = null;
            int[] canvas = new int[cell.Length];
            for (int i = 0; i < cell.Length; i++)
            {
                int id = cell[i];
                if (id == 0) continue;
                if (id % 16 == 0) { why = $"{where} uses colour {id}, which is a palette's see-through colour."; return null; }
                if (id >= paletteCount * 16) { why = $"{where} uses colour {id}; the sprite only has {paletteCount * 16}."; return null; }
                int under = palettesUnder[i];
                if (under != 0 && (under & (1 << (id / 16))) == 0) { why = $"{where} draws with palette {id / 16} where no piece uses it."; return null; }
                canvas[i] = id;
            }
            return canvas;
        }

        /// <summary>A colour with no exact match takes the nearest, counted in <paramref name="approximated"/>.</summary>
        public static int[] WithSpriteColours(int[] cell, IReadOnlyList<uint> sheetColours, int[] palettesUnder, IReadOnlyList<uint[]> palettes, ref int approximated)
        {
            int[] canvas = new int[cell.Length];
            Dictionary<(int, int), (int Value, bool Exact)> cache = new Dictionary<(int, int), (int Value, bool Exact)>();
            int all = (1 << palettes.Count) - 1;
            for (int i = 0; i < cell.Length; i++)
            {
                int id = cell[i];
                if (id == 0) continue;
                int banks = palettesUnder[i] & all;
                if (banks == 0) banks = 1;
                if (!cache.TryGetValue((id, banks), out (int Value, bool Exact) hit))
                {
                    (int Value, int Distance) best = (0, int.MaxValue);
                    for (int b = 0; b < palettes.Count; b++)
                    {
                        if ((banks & (1 << b)) == 0) continue;
                        (int index, int distance) = Nearest(sheetColours[id], palettes[b]);
                        if (distance < best.Distance) best = (b * 16 + index, distance);
                    }
                    cache[(id, banks)] = hit = (best.Value, best.Distance == 0);
                }
                canvas[i] = hit.Value;
                if (!hit.Exact) approximated++;
            }
            return canvas;
        }

        // Compared at five bits a channel, as stored; colour 0 is never a match.
        private static (int Index, int Distance) Nearest(uint colour, uint[] palette)
        {
            int best = 1, bestDist = int.MaxValue;
            for (int i = 1; i < Math.Min(16, palette.Length); i++)
            {
                int d = 0;
                for (int shift = 0; shift <= 16; shift += 8)
                {
                    int a = (int)((colour >> shift) & 0xFF) >> 3, b = (int)((palette[i] >> shift) & 0xFF) >> 3;
                    d += (a - b) * (a - b);
                }
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return (best, bestDist);
        }

        public static uint[][] SheetPalettes(Read sheet, IReadOnlyList<uint[]> current)
        {
            uint[][] result = current.Select(p => (uint[])p.Clone()).ToArray();
            for (int b = 0; b < result.Length; b++)
                for (int i = 0; i < 16 && i < result[b].Length; i++)
                {
                    int id = b * 16 + i;
                    if (id < sheet.Colours.Count) result[b][i] = 0xFF000000 | sheet.Colours[id];
                }
            return result;
        }

        // ── steps file ─────────────────────────────────────────────────────────

        public sealed class StepsFile
        {
            [JsonPropertyName("set"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string Set { get; set; }
            [JsonPropertyName("animation")] public int Animation { get; set; }
            [JsonPropertyName("steps")] public List<StepJson> Steps { get; set; } = new();
        }

        public sealed class StepJson
        {
            // For reading only; import matches drawings.
            [JsonPropertyName("frame"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Frame { get; set; }
            // In sixtieths of a second.
            [JsonPropertyName("hold")] public int Hold { get; set; }
            [JsonPropertyName("x"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? X { get; set; }
            [JsonPropertyName("y"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Y { get; set; }
        }

        // For reading only; import goes by position.
        public sealed class FramesFile
        {
            [JsonPropertyName("rows")] public List<FramesRow> Rows { get; set; } = new();
        }

        public sealed class FramesRow
        {
            [JsonPropertyName("set"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string Set { get; set; }
            [JsonPropertyName("frames")] public List<FrameJson> Frames { get; set; } = new();
        }

        public sealed class FrameJson
        {
            [JsonPropertyName("frame")] public int Frame { get; set; }
            [JsonPropertyName("blank"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Blank { get; set; }
        }

        public static string WriteFrames(FramesFile f) => JsonSerializer.Serialize(f, JsonOptions);

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public static string WriteSteps(StepsFile f) => JsonSerializer.Serialize(f, JsonOptions);

        public static StepsFile ReadSteps(string text, out string why)
        {
            why = null;
            try { return JsonSerializer.Deserialize<StepsFile>(text) ?? new StepsFile(); }
            catch (JsonException e) { why = e.Message; return null; }
        }
    }
}
