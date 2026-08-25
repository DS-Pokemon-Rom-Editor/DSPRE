using System.Collections.Generic;
using System.IO;
using static DSPRE.HgEngine.HgEngineSourcePatcher;

namespace DSPRE.HgEngine
{
    /// <summary>Source-text read for data/SpriteOffsets.c's per-species SpriteFrameData (idle-animation
    /// header, spriteYOffset, shadowXOffset, shadowSize). The compiled a180 narc lags behind once the
    /// checkout grows past however many species existed at its last build; source doesn't.</summary>
    public static class HgEngineSpriteOffsets
    {
        private const string SourceRelPath = "data/SpriteOffsets.c";

        public static bool TryLoad(int speciesId, out HgEngineSourceBlock block, out string error)
        {
            block = default;
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            if (!HgEngineDesignators.TryResolve(HgEngineDomain.SpriteOffsets, speciesId, out string designator))
            { error = $"Could not resolve a species designator for id {speciesId}."; return false; }

            string path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            if (!File.Exists(path)) { error = $"Source file not found: {path}"; return false; }

            string text = HgEngineFileCache.GetText(path);
            if (!TryFindEntry(text, designator, out int open, out int close))
            { error = $"Species {speciesId} not found in SpriteOffsets.c."; return false; }

            block = new HgEngineSourceBlock(text.Substring(open, close - open + 1));
            return true;
        }

        /// <summary>Reads a SpriteFrame[10] array (.frontFrames or .backFrames) into (frameNo, duration)
        /// steps, stopping at the first frameNo &lt; 0 terminator.</summary>
        public static List<(int Frame, int Duration)> ReadFrameSteps(HgEngineSourceBlock block, string frameArrayField)
        {
            var steps = new List<(int, int)>();
            var elements = block.GetArrayElements(new[] { FieldPathSegment.Field(frameArrayField) });
            foreach (var el in elements)
            {
                if (!el.TryGetInt(new[] { FieldPathSegment.Field("frameNo") }, out int frameNo) || frameNo < 0) break;
                el.TryGetInt(new[] { FieldPathSegment.Field("duration") }, out int duration);
                steps.Add((frameNo, duration));
            }
            return steps;
        }

        /// <summary>One SpriteFrame struct: which raw sprite frame to show, how long, and its per-frame
        /// pixel shift. A frameNo of -1 means "unused" (the file always writes all 10 slots explicitly).</summary>
        public readonly struct SpriteFrameSlot
        {
            public int FrameNo { get; }
            public int Duration { get; }
            public int HorizontalShift { get; }
            public int VerticalShift { get; }
            public SpriteFrameSlot(int frameNo, int duration, int horizontalShift, int verticalShift)
            {
                FrameNo = frameNo;
                Duration = duration;
                HorizontalShift = horizontalShift;
                VerticalShift = verticalShift;
            }
        }

        /// <summary>Reads all 10 elements of a SpriteFrame[10] array (.frontFrames or .backFrames) verbatim,
        /// for full-fidelity editing. Unlike <see cref="ReadFrameSteps"/>, this does not stop at the first
        /// frameNo &lt; 0 terminator, since an editor needs every slot including the trailing unused ones.</summary>
        public static List<SpriteFrameSlot> ReadFrameSlots(HgEngineSourceBlock block, string frameArrayField)
        {
            var slots = new List<SpriteFrameSlot>();
            foreach (var el in block.GetArrayElements(new[] { FieldPathSegment.Field(frameArrayField) }))
            {
                el.TryGetInt(new[] { FieldPathSegment.Field("frameNo") }, out int frameNo);
                el.TryGetInt(new[] { FieldPathSegment.Field("duration") }, out int duration);
                el.TryGetInt(new[] { FieldPathSegment.Field("horizontalShift") }, out int hShift);
                el.TryGetInt(new[] { FieldPathSegment.Field("verticalShift") }, out int vShift);
                slots.Add(new SpriteFrameSlot(frameNo, duration, hShift, vShift));
            }
            return slots;
        }

        /// <summary>Builds the field writes for one full frontFrames/backFrames array (up to 40 fields);
        /// merge with the other array's writes into a single <see cref="HgEngineWriter.TryWriteFields"/> call.</summary>
        public static IEnumerable<HgEngineFieldWrite> BuildFrameWrites(string frameArrayField, IReadOnlyList<SpriteFrameSlot> slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                FieldPathSegment[] Path(string field) => new[]
                {
                    FieldPathSegment.Field(frameArrayField), FieldPathSegment.At(i), FieldPathSegment.Field(field)
                };
                yield return new HgEngineFieldWrite(Path("frameNo"), s.FrameNo.ToString());
                yield return new HgEngineFieldWrite(Path("duration"), s.Duration.ToString());
                yield return new HgEngineFieldWrite(Path("horizontalShift"), s.HorizontalShift.ToString());
                yield return new HgEngineFieldWrite(Path("verticalShift"), s.VerticalShift.ToString());
            }
        }
    }
}
