using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// A material animation (NSBMA): it fades a material in and out over time, which is what makes a
    /// shop sign glow and a waterfall's spray come and go.
    ///
    /// The file names its materials the same way the other animations do, and each one carries four
    /// channels. The last is see-through-ness, stored as one byte per frame from 31 (solid) down to 0
    /// (gone), which is the same 0-31 range a model's own materials use.
    /// </summary>
    public sealed class MaterialColourAnimation
    {
        /// <summary>The value a fully solid material has, both here and in a model.</summary>
        public const int SolidAlpha = 31;

        private sealed class Track
        {
            public string Name;
            public byte[] Alpha;      // one value per frame, 0-31
        }

        private readonly List<Track> _tracks = new List<Track>();
        private readonly Dictionary<string, int> _byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public int FrameCount { get; private set; }
        public IReadOnlyList<string> MaterialNames => _tracks.Select(t => t.Name).ToArray();

        /// <summary>True when at least one material actually fades rather than sitting at one value.</summary>
        public bool Fades => _tracks.Any(t => t.Alpha != null && t.Alpha.Distinct().Count() > 1);

        /// <summary>Reads one, or null when the file is some other kind of animation.</summary>
        public static MaterialColourAnimation Load(byte[] d)
        {
            if (d == null || d.Length < 24) return null;
            if (Encoding.ASCII.GetString(d, 0, 4) != "BMA0") return null;

            try { return Parse(d); }
            catch (Exception ex) { AppLogger.Error("Material animation failed to read: " + ex.Message); return null; }
        }

        private static MaterialColourAnimation Parse(byte[] d)
        {
            int section = BitConverter.ToInt32(d, 16);
            if (section + 8 > d.Length || Encoding.ASCII.GetString(d, section, 4) != "MAT0") return null;

            // The outer list names the MODEL the animation belongs to and points at one chunk per model.
            // The materials it actually fades are named inside that chunk.
            var result = new MaterialColourAnimation();
            foreach (int chunk in ChunkOffsets(d, section))
            {
                if (chunk + 12 > d.Length) continue;
                if (Encoding.ASCII.GetString(d, chunk, 4) != "M\0AM") continue;

                int frames = BitConverter.ToUInt16(d, chunk + 4);
                if (frames <= 0) continue;
                result.FrameCount = Math.Max(result.FrameCount, frames);
                ReadMaterials(d, chunk, frames, result);
            }

            if (result._tracks.Count == 0) return null;
            for (int i = 0; i < result._tracks.Count; i++) result._byName[result._tracks[i].Name] = i;
            return result;
        }

        /// <summary>Where each model's chunk starts, from the outer list.</summary>
        private static IEnumerable<int> ChunkOffsets(byte[] d, int section)
        {
            int p = section + 8;
            int count = d[p + 1];
            int q = p + 4 + 8 + 4 * count + 4;
            for (int i = 0; i < count; i++)
            {
                if (q + i * 4 + 4 > d.Length) yield break;
                yield return section + BitConverter.ToInt32(d, q + i * 4);
            }
        }

        private const int AlphaFramesOffset = 14;     // the see-through channel's frame count
        private const int AlphaValueOffset = 16;      // and its value, which points at the track

        /// <summary>
        /// Reads the materials one chunk fades. They are listed the usual way, with one 20-byte record
        /// each followed by the names. A record is ten 16-bit values: a flag, four channels of a frame
        /// count and a value, then the frame count again. The last channel is see-through-ness, and its
        /// value says where in the chunk the one-byte-per-frame track lives.
        /// </summary>
        private static void ReadMaterials(byte[] d, int chunk, int frames, MaterialColourAnimation into)
        {
            int p = chunk + 8;
            int count = d[p + 1];
            if (count <= 0) return;

            int q = p + 4 + 8 + 4 * count;            // past the block nothing here needs
            if (q + 4 > d.Length) return;
            int recordSize = BitConverter.ToUInt16(d, q);
            q += 4;
            if (recordSize < AlphaValueOffset + 2) return;

            int namesAt = q + recordSize * count;
            for (int i = 0; i < count; i++)
            {
                int rec = q + i * recordSize;
                if (rec + recordSize > d.Length || namesAt + i * 16 + 16 > d.Length) return;

                // A name field is 16 bytes but ends at its first zero.
                string name = Encoding.ASCII.GetString(d, namesAt + i * 16, 16).Split('\0')[0].Trim();
                if (name.Length == 0) continue;

                into._tracks.Add(new Track { Name = name, Alpha = ReadAlpha(d, chunk, rec, frames) });
            }
        }

        /// <summary>The see-through track for one material, or null when it never changes.</summary>
        private static byte[] ReadAlpha(byte[] d, int chunk, int record, int frames)
        {
            int trackFrames = BitConverter.ToUInt16(d, record + AlphaFramesOffset) & 0x0FFF;
            int value = BitConverter.ToUInt16(d, record + AlphaValueOffset);
            if (trackFrames != frames || value == 0) return null;

            int start = chunk + value;
            if (start + frames > d.Length) return null;

            var track = new byte[frames];
            for (int f = 0; f < frames; f++)
            {
                byte v = d[start + f];
                if (v > SolidAlpha) return null;      // not a see-through track after all
                track[f] = v;
            }
            return track;
        }

        /// <summary>Index of the material this animation fades, or -1 when it doesn't touch it.</summary>
        public int IndexOf(string materialName) =>
            materialName != null && _byName.TryGetValue(materialName, out int i) ? i : -1;

        /// <summary>How see-through a material is on a frame, 0 (gone) to 1 (solid), or null when it never changes.</summary>
        public float? Evaluate(int material, int frame)
        {
            if (material < 0 || material >= _tracks.Count) return null;
            var track = _tracks[material].Alpha;
            if (track == null || track.Length == 0) return null;
            if (frame < 0) frame = 0;
            return track[frame % track.Length] / (float)SolidAlpha;
        }

        public float? Evaluate(string materialName, int frame) => Evaluate(IndexOf(materialName), frame);

        /// <summary>True when a material never actually fades, so the renderer can skip it.</summary>
        public bool IsStatic(int material)
        {
            if (material < 0 || material >= _tracks.Count) return true;
            var track = _tracks[material].Alpha;
            return track == null || track.Distinct().Count() <= 1;
        }
    }
}
