using System;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.Json;
using Ekona.Images;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// Reads a trainer class's <c>NNN_cell.json</c> (NCER) and <c>NNN_anim.json</c> (NANR) straight from
    /// hg-engine source instead of the compiled narc. This checkout's nitrogfx build has a real bug
    /// compiling cell.json into NCER: negative-X OAMs get their whole Attr1 word overwritten with a raw
    /// sign-extended value, clobbering the size/flip bits. Reading the JSON sidesteps that bug entirely.
    /// </summary>
    internal static class HgEngineTrainerGraphicsSource
    {
        public static bool TryReadCellBanks(string cellJsonPath, out Bank[] banks, out uint blockSize, out string error)
        {
            banks = null; blockSize = 0; error = null;
            CellJson doc;
            try { doc = JsonSerializer.Deserialize<CellJson>(File.ReadAllText(cellJsonPath)); }
            catch (Exception ex) { error = "Failed to parse " + cellJsonPath + ": " + ex.Message; return false; }
            if (doc?.Cells == null || doc.Cells.Length == 0) { error = "No cells in " + cellJsonPath; return false; }

            blockSize = (uint)doc.MappingType;
            banks = new Bank[doc.Cells.Length];
            for (int i = 0; i < doc.Cells.Length; i++)
            {
                var c = doc.Cells[i];
                var oams = (c.OAM ?? Array.Empty<OamJson>())
                    .Select((o, idx) => BuildOam(o, (ushort)idx))
                    .ToList();
                oams.Sort(Actions.Comparision_OAM);

                var transfer = doc.TransferData != null && i < doc.TransferData.Length ? doc.TransferData[i] : null;
                banks[i] = new Bank
                {
                    oams = oams.ToArray(),
                    name = doc.Labels != null && i < doc.Labels.Length ? doc.Labels[i] : i.ToString(),
                    width = 0,
                    height = 0,
                    data_offset = (uint)(transfer?.Offset ?? 0),
                    data_size = (uint)(transfer?.Size ?? 0),
                };
            }
            return true;
        }

        public static bool TryReadAnimSequence(string animJsonPath, out int[] cells, out int[] durations, out string error)
        {
            cells = null; durations = null; error = null;
            AnimJson doc;
            try { doc = JsonSerializer.Deserialize<AnimJson>(File.ReadAllText(animJsonPath)); }
            catch (Exception ex) { error = "Failed to parse " + animJsonPath + ": " + ex.Message; return false; }
            if (doc?.Sequences == null || doc.Sequences.Length == 0 || doc.AnimationResults == null || doc.AnimationResults.Length == 0)
            {
                error = "No sequences/results in " + animJsonPath;
                return false;
            }

            // The real (possibly multi-frame) animation is whichever sequence has the most frames; the
            // other sequence(s) are single-frame idle/default poses. See TrainerClassSpriteRenderer.Load
            // for how DefaultFrame then picks the "CellAnime0"-named bank out of this played sequence.
            var longest = doc.Sequences.OrderByDescending(s => s.FrameData?.Length ?? 0).First();
            if (longest.FrameData == null || longest.FrameData.Length == 0) { error = "Empty sequence in " + animJsonPath; return false; }

            cells = longest.FrameData
                .Select(f => f.ResultId >= 0 && f.ResultId < doc.AnimationResults.Length ? doc.AnimationResults[f.ResultId].Index : 0)
                .ToArray();
            durations = longest.FrameData.Select(f => f.FrameDelay).ToArray();
            return true;
        }

        private static OAM BuildOam(OamJson o, ushort numCell)
        {
            var size = Actions.Get_OAMSize((byte)o.Attr0.Shape, (byte)o.Attr1.Size);
            return new OAM
            {
                num_cell = numCell,
                width = (ushort)size.Width,
                height = (ushort)size.Height,
                obj0 = new Obj0
                {
                    yOffset = o.Attr0.YCoordinate,
                    rs_flag = (byte)(o.Attr0.Rotation ? 1 : 0),
                    objDisable = (byte)(!o.Attr0.Rotation && o.Attr0.SizeDisable ? 1 : 0),
                    doubleSize = (byte)(o.Attr0.Rotation && o.Attr0.SizeDisable ? 1 : 0),
                    objMode = (byte)o.Attr0.Mode,
                    mosaic_flag = (byte)(o.Attr0.Mosaic ? 1 : 0),
                    depth = (byte)(o.Attr0.Colours == 256 ? 1 : 0),
                    shape = (byte)o.Attr0.Shape,
                },
                obj1 = new Obj1
                {
                    xOffset = o.Attr1.XCoordinate,
                    unused = 0,
                    // This JSON schema has no per-OAM flip, only a whole-cell hFlip/vFlip under cellAttrs
                    // (not modeled here yet).
                    flipX = 0,
                    flipY = 0,
                    select_param = (byte)o.Attr1.RotationScaling,
                    size = (byte)o.Attr1.Size,
                },
                obj2 = new Obj2
                {
                    tileOffset = (uint)o.Attr2.CharName,
                    priority = (byte)o.Attr2.Priority,
                    index_palette = (byte)o.Attr2.Palette,
                },
            };
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
