using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// Full, round-trippable model of an hg-engine <c>NNN_anim.json</c> (NANR). Unlike
    /// <see cref="HgEngineTrainerGraphicsSource"/>'s private, read-only POCOs (built only for the preview
    /// renderer), this keeps every sequence and field so a structured editor can safely round-trip the
    /// whole file.
    /// </summary>
    public sealed class AnimJsonRoot
    {
        [JsonPropertyName("labelEnabled")] public bool LabelEnabled { get; set; } = true;
        [JsonPropertyName("uaatEnabled")] public bool UaatEnabled { get; set; }
        [JsonPropertyName("sequenceCount")] public int SequenceCount { get; set; }
        [JsonPropertyName("frameCount")] public int FrameCount { get; set; }
        [JsonPropertyName("sequences")] public List<AnimSequenceJson> Sequences { get; set; } = new();
        [JsonPropertyName("animationResults")] public List<AnimResultJson> AnimationResults { get; set; } = new();
        [JsonPropertyName("resultCount")] public int ResultCount { get; set; }
        [JsonPropertyName("labels")] public List<string> Labels { get; set; } = new();
        [JsonPropertyName("labelCount")] public int LabelCount { get; set; }

        /// <summary>Recomputes every derived count field and rebuilds animationResults/resultId from the
        /// sequences' own frame lists, so callers only ever edit Sequences/frames directly.</summary>
        public void Normalize()
        {
            SequenceCount = Sequences.Count;
            AnimationResults.Clear();
            int totalFrames = 0;
            foreach (var seq in Sequences)
            {
                seq.FrameCount = seq.FrameData.Count;
                totalFrames += seq.FrameCount;
                foreach (var frame in seq.FrameData)
                {
                    frame.ResultId = AnimationResults.Count;
                    AnimationResults.Add(new AnimResultJson { ResultType = 0, Index = frame.CellIndex });
                }
            }
            FrameCount = totalFrames;
            ResultCount = AnimationResults.Count;

            while (Labels.Count < Sequences.Count) Labels.Add("");
            if (Labels.Count > Sequences.Count) Labels.RemoveRange(Sequences.Count, Labels.Count - Sequences.Count);
            LabelCount = Labels.Count;
        }

        /// <summary>Deserializes and resolves each frame's file-format resultId indirection into the
        /// editor-facing CellIndex (see <see cref="AnimFrameDataJson.CellIndex"/>).</summary>
        public static AnimJsonRoot Parse(string text)
        {
            var root = JsonSerializer.Deserialize<AnimJsonRoot>(text, new JsonSerializerOptions { AllowTrailingCommas = true });
            if (root == null) return null;

            root.Sequences ??= new List<AnimSequenceJson>();
            root.AnimationResults ??= new List<AnimResultJson>();
            root.Labels ??= new List<string>();
            foreach (var seq in root.Sequences)
            {
                seq.FrameData ??= new List<AnimFrameDataJson>();
                foreach (var frame in seq.FrameData)
                {
                    frame.CellIndex = frame.ResultId >= 0 && frame.ResultId < root.AnimationResults.Count
                        ? root.AnimationResults[frame.ResultId].Index
                        : 0;
                }
            }
            return root;
        }

        public string Serialize()
        {
            Normalize();
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public sealed class AnimSequenceJson
    {
        [JsonPropertyName("frameCount")] public int FrameCount { get; set; }
        [JsonPropertyName("loopStartFrame")] public int LoopStartFrame { get; set; }
        [JsonPropertyName("animationElement")] public int AnimationElement { get; set; }
        [JsonPropertyName("animationType")] public int AnimationType { get; set; } = 1;
        [JsonPropertyName("playbackMode")] public int PlaybackMode { get; set; } = 1;
        [JsonPropertyName("frameData")] public List<AnimFrameDataJson> FrameData { get; set; } = new();
    }

    /// <summary>One played frame. <see cref="CellIndex"/> is the editor-facing pose; <see cref="ResultId"/>
    /// is the file's own indirection into animationResults[], recomputed by
    /// <see cref="AnimJsonRoot.Normalize"/> and not meant to be hand-edited.</summary>
    public sealed class AnimFrameDataJson
    {
        [JsonPropertyName("frameDelay")] public int FrameDelay { get; set; } = 4;
        [JsonPropertyName("resultId")] public int ResultId { get; set; }

        [JsonIgnore] public int CellIndex { get; set; }
    }

    public sealed class AnimResultJson
    {
        [JsonPropertyName("resultType")] public int ResultType { get; set; }
        [JsonPropertyName("index")] public int Index { get; set; }
    }
}
