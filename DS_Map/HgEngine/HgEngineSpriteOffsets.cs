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
    }
}
