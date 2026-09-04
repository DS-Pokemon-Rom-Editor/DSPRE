using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Which parts of a model are showing on which frame, out of an NSBVA. One bit per part per frame,
    /// which is how a shop sign lights up or a door panel disappears without the shape itself moving.
    /// </summary>
    public sealed class VisibilityAnimation
    {
        private sealed class Track
        {
            public string Name;
            public bool[][] Visible;    // [part][frame]
        }

        private readonly List<Track> _tracks = new();
        private readonly Dictionary<string, int> _byName = new(StringComparer.OrdinalIgnoreCase);

        public int FrameCount { get; private set; }
        public IReadOnlyList<string> AnimationNames => _tracks.Select(t => t.Name).ToArray();
        public int PartCount(int animation) =>
            animation >= 0 && animation < _tracks.Count ? _tracks[animation].Visible.Length : 0;

        /// <summary>Whether a part is showing. Anything out of range is showing, which is the resting state.</summary>
        public bool Visible(int animation, int part, int frame)
        {
            if (animation < 0 || animation >= _tracks.Count) return true;
            var v = _tracks[animation].Visible;
            if (part < 0 || part >= v.Length || v[part].Length == 0) return true;
            return v[part][Math.Clamp(frame, 0, v[part].Length - 1)];
        }

        /// <summary>The parts that are hidden at some point. The rest never change and are not worth showing.</summary>
        public IReadOnlyList<int> PartsThatChange(int animation)
        {
            if (animation < 0 || animation >= _tracks.Count) return Array.Empty<int>();
            var v = _tracks[animation].Visible;
            return Enumerable.Range(0, v.Length).Where(p => v[p].Any(x => !x)).ToArray();
        }

        public bool Hides => _tracks.Any(t => t.Visible.Any(p => p.Any(x => !x)));

        public int IndexOf(string name) => _byName.TryGetValue(name ?? "", out int i) ? i : -1;

        public static VisibilityAnimation Load(byte[] d)
        {
            if (d == null || d.Length < 24) return null;
            if (Encoding.ASCII.GetString(d, 0, 4) != "BVA0") return null;

            try { return Parse(d); }
            catch (Exception ex) { AppLogger.Error("Visibility animation failed to read: " + ex.Message); return null; }
        }

        private static VisibilityAnimation Parse(byte[] d)
        {
            int section = BitConverter.ToInt32(d, 16);
            if (section + 8 > d.Length || Encoding.ASCII.GetString(d, section, 4) != "VIS0") return null;

            var result = new VisibilityAnimation();
            var names = NamesIn(d, section).ToList();
            int at = 0;
            foreach (int chunk in ChunkOffsets(d, section))
            {
                string name = at < names.Count ? names[at] : "";
                at++;
                if (chunk + 12 > d.Length) continue;

                int frames = BitConverter.ToUInt16(d, chunk + 4);
                int parts = BitConverter.ToUInt16(d, chunk + 6);
                if (frames <= 0 || parts <= 0) continue;

                var track = new bool[parts][];
                for (int p = 0; p < parts; p++) track[p] = new bool[frames];

                int q = chunk + 12;
                if (q + 4 > d.Length) continue;
                uint word = BitConverter.ToUInt32(d, q);
                q += 4;
                int bit = 0, total = frames * parts;
                for (int f = 0; f < frames; f++)
                    for (int p = 0; p < parts; p++)
                    {
                        track[p][f] = (word & 1) != 0;
                        word >>= 1;
                        bit++;
                        // Only refill while bits remain: the stream is padded to a whole word, so a last
                        // animation whose bits divide exactly by 32 would otherwise read past the file.
                        if (bit % 32 == 0 && bit < total && q + 4 <= d.Length)
                        {
                            word = BitConverter.ToUInt32(d, q);
                            q += 4;
                        }
                    }

                result.FrameCount = Math.Max(result.FrameCount, frames);
                result._tracks.Add(new Track { Name = name, Visible = track });
            }

            if (result._tracks.Count == 0) return null;
            for (int i = 0; i < result._tracks.Count; i++)
                result._byName[result._tracks[i].Name] = i;
            return result;
        }

        /// <summary>Where each animation's chunk starts, from the outer list.</summary>
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

        /// <summary>The sixteen character name each animation carries, after the offsets.</summary>
        private static IEnumerable<string> NamesIn(byte[] d, int section)
        {
            int p = section + 8;
            int count = d[p + 1];
            int namesAt = p + 4 + 8 + 4 * count + 4 + 4 * count;
            for (int i = 0; i < count; i++)
            {
                int n = namesAt + i * 16;
                if (n + 16 > d.Length) yield break;
                yield return Encoding.ASCII.GetString(d, n, 16).Split('\0')[0].Trim();
            }
        }
    }
}
