using System;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.Json;
using DSPRE.ROMFiles;
using System.Collections.Generic;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// Reads a trainer class's cell.json and anim.json from source: nitrogfx overwrites Attr1 of negative-X OAMs
    /// when it builds the NCER, losing their size and flip bits.
    /// </summary>
    internal static class HgEngineTrainerGraphicsSource
    {
        /// <summary>A sprite's source path without extension: trainer_gfx/NNN, or trainer_back_gfx/NN for back sprites.</summary>
        public static string Stem(bool back, int id) =>
            Path.Combine(HgEngineProject.RepoPathUnc, "data", "graphics", back ? "trainer_back_gfx" : "trainer_gfx", id.ToString(back ? "D2" : "D3"));

        // The built drawing is the PNG cut into 8x8 tiles in reading order, each tile read row by row.
        public static byte[] RasterToTiles(byte[] raster, int width, int height)
        {
            byte[] tiles = new byte[raster.Length];
            int at = 0;
            for (int ty = 0; ty < height / 8; ty++)
                for (int tx = 0; tx < width / 8; tx++)
                    for (int y = 0; y < 8; y++)
                        for (int x = 0; x < 8; x++)
                            tiles[at++] = raster[(ty * 8 + y) * width + tx * 8 + x];
            return tiles;
        }

        public static byte[] TilesToRaster(byte[] tiles, int width, int height)
        {
            byte[] raster = new byte[width * height];
            int at = 0;
            for (int ty = 0; ty < height / 8; ty++)
                for (int tx = 0; tx < width / 8; tx++)
                    for (int y = 0; y < 8; y++)
                        for (int x = 0; x < 8; x++)
                            raster[(ty * 8 + y) * width + tx * 8 + x] = tiles[at++];
            return raster;
        }

        /// <summary>4bpp data to one index per pixel, low nibble first.</summary>
        public static byte[] Unpack4(byte[] data)
        {
            byte[] indices = new byte[data.Length * 2];
            for (int i = 0; i < data.Length; i++) { indices[i * 2] = (byte)(data[i] & 0xF); indices[i * 2 + 1] = (byte)(data[i] >> 4); }
            return indices;
        }

        public static byte[] Pack4(byte[] indices)
        {
            byte[] data = new byte[(indices.Length + 1) / 2];
            for (int i = 0; i < indices.Length; i++) data[i / 2] |= (byte)((indices[i] & 0xF) << (i % 2 == 0 ? 0 : 4));
            return data;
        }

        /// <summary>Every sequence in an anim.json as (cell, duration) frames, or null when it can't be read.</summary>
        public static (int Bank, int Duration)[][] ReadAnimSequences(string animJsonPath)
        {
            try
            {
                AnimJsonRoot root = AnimJsonRoot.Parse(File.ReadAllText(animJsonPath));
                return root?.Sequences.Select(seq => seq.FrameData.Select(f => (f.CellIndex, f.FrameDelay)).ToArray()).ToArray();
            }
            catch (Exception ex) { AppLogger.WarnOnce("HgEngineTrainerGraphicsSource.ReadAnimSequences", ex); return null; }
        }

        /// <summary>The cells of a cell.json, in file order, with the mapping it records.</summary>
        public static bool TryReadCells(string cellJsonPath, out NcerFile cells, out string error)
        {
            cells = null; error = null;
            CellJson doc;
            try { doc = JsonSerializer.Deserialize<CellJson>(File.ReadAllText(cellJsonPath)); }
            catch (Exception ex) { error = cellJsonPath + " couldn't be read: " + ex.Message; return false; }
            if (doc?.Cells == null || doc.Cells.Length == 0) { error = cellJsonPath + " has no cells."; return false; }

            List<NcerFile.Cell> list = new List<NcerFile.Cell>();
            for (int i = 0; i < doc.Cells.Length; i++)
            {
                TransferEntry transfer = doc.TransferData != null && i < doc.TransferData.Length ? doc.TransferData[i] : null;
                list.Add(new NcerFile.Cell
                {
                    Number = i,
                    Name = doc.Labels != null && i < doc.Labels.Length ? doc.Labels[i] : i.ToString(),
                    Pieces = (doc.Cells[i].OAM ?? Array.Empty<OamJson>()).Select(BuildPiece).ToList(),
                    TransferOffset = (uint)(transfer?.Offset ?? 0),
                    TransferSize = (uint)(transfer?.Size ?? 0),
                });
            }
            cells = NcerFile.FromCells((uint)doc.MappingType, list);
            return true;
        }

        public static bool TryReadAnimSequence(string animJsonPath, out int[] cells, out int[] durations, out string error)
        {
            cells = null; durations = null; error = null;
            AnimJson doc;
            try { doc = JsonSerializer.Deserialize<AnimJson>(File.ReadAllText(animJsonPath)); }
            catch (Exception ex) { error = animJsonPath + " couldn't be read: " + ex.Message; return false; }
            if (doc?.Sequences == null || doc.Sequences.Length == 0 || doc.AnimationResults == null || doc.AnimationResults.Length == 0)
            {
                error = animJsonPath + " has no sequences.";
                return false;
            }

            // The real (possibly multi-frame) animation is whichever sequence has the most frames; the
            // other sequence(s) are single-frame idle/default poses. See TrainerClassSpriteRenderer.Load
            // for how DefaultFrame then picks the "CellAnime0"-named bank out of this played sequence.
            SequenceJson longest = doc.Sequences.OrderByDescending(s => s.FrameData?.Length ?? 0).First();
            if (longest.FrameData == null || longest.FrameData.Length == 0) { error = animJsonPath + " has an empty sequence."; return false; }

            cells = longest.FrameData
                .Select(f => f.ResultId >= 0 && f.ResultId < doc.AnimationResults.Length ? doc.AnimationResults[f.ResultId].Index : 0)
                .ToArray();
            durations = longest.FrameData.Select(f => f.FrameDelay).ToArray();
            return true;
        }

        private static NcerFile.Piece BuildPiece(OamJson o)
        {
            int a0 = (o.Attr0.YCoordinate & 0xFF) | (o.Attr0.Rotation ? 1 << 8 : 0) | (o.Attr0.SizeDisable ? 1 << 9 : 0)
                     | ((o.Attr0.Mode & 3) << 10) | (o.Attr0.Mosaic ? 1 << 12 : 0) | (o.Attr0.Colours == 256 ? 1 << 13 : 0)
                     | ((o.Attr0.Shape & 3) << 14);
            int a1 = (o.Attr1.XCoordinate & 0x1FF) | (o.Attr0.Rotation ? (o.Attr1.RotationScaling & 0x1F) << 9 : 0)
                     | ((o.Attr1.Size & 3) << 14);
            int a2 = (o.Attr2.CharName & 0x3FF) | ((o.Attr2.Priority & 3) << 10) | ((o.Attr2.Palette & 0xF) << 12);
            NcerFile.Piece piece = NcerFile.Decode(a0, a1, a2);
            // This JSON schema has no per-OAM flip, only a whole-cell hFlip/vFlip under cellAttrs (not modeled here
            // yet); a rotating piece's flip bits are its rotation number.
            piece.FlipH = piece.FlipV = false;
            return piece;
        }

        // ── JSON schema: nitrogfx's cell.json / anim.json, per documentation/wiki/Adding-New-Trainer-Classes.md ──
        private sealed class CellJson
        {
            [JsonPropertyName("mappingType")] public int MappingType { get; set; }
            [JsonPropertyName("cells")] public CellEntry[] Cells { get; set; }
            [JsonPropertyName("labels")] public string[] Labels { get; set; }
            [JsonPropertyName("transferData")] public TransferEntry[] TransferData { get; set; }
        }
        private sealed class CellEntry
        {
            [JsonPropertyName("OAM")] public OamJson[] OAM { get; set; }
        }
        private sealed class OamJson
        {
            [JsonPropertyName("Attr0")] public Attr0Json Attr0 { get; set; }
            [JsonPropertyName("Attr1")] public Attr1Json Attr1 { get; set; }
            [JsonPropertyName("Attr2")] public Attr2Json Attr2 { get; set; }
        }
        private sealed class Attr0Json
        {
            [JsonPropertyName("YCoordinate")] public int YCoordinate { get; set; }
            [JsonPropertyName("Rotation")] public bool Rotation { get; set; }
            [JsonPropertyName("SizeDisable")] public bool SizeDisable { get; set; }
            [JsonPropertyName("Mode")] public int Mode { get; set; }
            [JsonPropertyName("Mosaic")] public bool Mosaic { get; set; }
            [JsonPropertyName("Colours")] public int Colours { get; set; }
            [JsonPropertyName("Shape")] public int Shape { get; set; }
        }
        private sealed class Attr1Json
        {
            [JsonPropertyName("XCoordinate")] public int XCoordinate { get; set; }
            [JsonPropertyName("RotationScaling")] public int RotationScaling { get; set; }
            [JsonPropertyName("Size")] public int Size { get; set; }
        }
        private sealed class Attr2Json
        {
            [JsonPropertyName("CharName")] public int CharName { get; set; }
            [JsonPropertyName("Priority")] public int Priority { get; set; }
            [JsonPropertyName("Palette")] public int Palette { get; set; }
        }
        private sealed class TransferEntry
        {
            [JsonPropertyName("offset")] public int Offset { get; set; }
            [JsonPropertyName("size")] public int Size { get; set; }
        }

        private sealed class AnimJson
        {
            [JsonPropertyName("sequences")] public SequenceJson[] Sequences { get; set; }
            [JsonPropertyName("animationResults")] public ResultJson[] AnimationResults { get; set; }
        }
        private sealed class SequenceJson
        {
            [JsonPropertyName("frameData")] public FrameDataJson[] FrameData { get; set; }
        }
        private sealed class FrameDataJson
        {
            [JsonPropertyName("frameDelay")] public int FrameDelay { get; set; }
            [JsonPropertyName("resultId")] public int ResultId { get; set; }
        }
        private sealed class ResultJson
        {
            [JsonPropertyName("resultType")] public int ResultType { get; set; }
            [JsonPropertyName("index")] public int Index { get; set; }
        }
    }
}
