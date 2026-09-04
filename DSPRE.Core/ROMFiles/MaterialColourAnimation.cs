using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// A material animation (NSBMA): it fades a material in and out over time, which is what makes a shop
    /// sign glow and a waterfall's spray come and go.
    /// </summary>
    public sealed class MaterialColourAnimation
    {
        /// <summary>The value a fully solid material has, both here and in a model.</summary>
        public const int SolidAlpha = 31;

        private sealed class Track
        {
            public string Name;
            public byte[] Alpha;      // one value per frame, 0-31
            public ushort[] Diffuse;  // one packed colour per frame, five bits a channel
        }

        private readonly List<Track> _tracks = new List<Track>();
        private readonly Dictionary<string, int> _byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public int FrameCount { get; private set; }
        public IReadOnlyList<string> MaterialNames => _tracks.Select(t => t.Name).ToArray();

        /// <summary>True when at least one material actually fades rather than sitting at one value.</summary>
        public bool Fades => _tracks.Any(t => t.Alpha != null && t.Alpha.Distinct().Count() > 1);

        /// <summary>True when at least one material changes colour rather than only fading.</summary>
        public bool Colours => _tracks.Any(t => t.Diffuse != null && t.Diffuse.Distinct().Count() > 1);

        /// <summary>
        /// A material's colour on a frame, as three values from zero to one, or null when this animation
        /// leaves its colour alone. The file keeps five bits a channel, lowest bits red.
        /// </summary>
        public (float r, float g, float b)? ColourAt(int material, int frame)
        {
            if (material < 0 || material >= _tracks.Count) return null;
            var track = _tracks[material].Diffuse;
            if (track == null || track.Length == 0) return null;
            int c = track[Math.Clamp(frame, 0, track.Length - 1)];
            return ((c & 0x1F) / 31f, ((c >> 5) & 0x1F) / 31f, ((c >> 10) & 0x1F) / 31f);
        }

        public (float r, float g, float b)? ColourAt(string materialName, int frame) =>
            ColourAt(IndexOf(materialName), frame);

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

        /// <summary>Reads the materials one chunk fades. </summary>
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

                into._tracks.Add(new Track
                {
                    Name = name,
                    Alpha = ReadAlpha(d, chunk, rec, frames),
                    Diffuse = ReadDiffuse(d, chunk, rec, frames),
                });
            }
        }

        // The five channels are twenty bytes of the record, diffuse first. Each is a word: the value or
        // an offset in its low half, how many frames in the next byte, and flags in the top byte, where
        // 0x20 means the value is the colour itself rather than somewhere to read it from.
        private const int DiffuseOffset = 0;
        private const int ConstantFlag = 0x20;

        /// <summary>The colour track for one material, or null when it holds one colour throughout.</summary>
        private static ushort[] ReadDiffuse(byte[] d, int chunk, int record, int frames)
        {
            if (record + DiffuseOffset + 4 > d.Length) return null;
            uint channel = BitConverter.ToUInt32(d, record + DiffuseOffset);
            int value = (int)(channel & 0xFFFF);
            int flags = (int)((channel >> 24) & 0xFF);

            // One colour for the whole animation is not worth a track; it changes nothing over time.
            if ((flags & ConstantFlag) != 0) return null;

            int start = chunk + value;
            if (start < 0 || start + frames * 2 > d.Length) return null;

            var track = new ushort[frames];
            for (int f = 0; f < frames; f++) track[f] = BitConverter.ToUInt16(d, start + f * 2);
            return track;
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
