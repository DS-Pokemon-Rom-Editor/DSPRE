using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MKDS_Course_Editor.NSBTP;

namespace DSPRE.ROMFiles
{
    /// <summary>A texture-swapping animation (NSBTP) ready to play. </summary>
    public sealed class TexturePatternAnimation
    {
        public struct Swap
        {
            public string TextureName;
            public string PaletteName;
            public bool IsSet => !string.IsNullOrEmpty(TextureName);
        }

        private readonly NSBTP.NSBTP_File _file;
        private readonly Dictionary<string, int> _byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<string> MaterialNames { get; }

        /// <summary>How many frames a full loop takes: one past the last key frame of any material.</summary>
        public int FrameCount { get; }

        private TexturePatternAnimation(NSBTP.NSBTP_File file)
        {
            _file = file;
            MaterialNames = file.PAT0.names ?? Array.Empty<string>();
            for (int i = 0; i < MaterialNames.Count; i++)
                _byName[MaterialNames[i]] = i;

            int last = 1;
            for (int i = 0; i < MaterialNames.Count && i < Frames.Length; i++)
                foreach (var k in Frames[i])
                    if (k.Start + 1 > last) last = k.Start + 1;
            FrameCount = last;
        }

        private NSBTP.NSBTP_File.animData.keyFrame[][] Frames =>
            _file.AnimData?.Select(a => a.KeyFrames ?? Array.Empty<NSBTP.NSBTP_File.animData.keyFrame>()).ToArray()
            ?? Array.Empty<NSBTP.NSBTP_File.animData.keyFrame[]>();

        /// <summary>Reads one, or null when the file is some other kind of animation.</summary>
        public static TexturePatternAnimation Load(byte[] data)
        {
            if (data == null || data.Length < 4) return null;
            if (Encoding.ASCII.GetString(data, 0, 4) != "BTP0") return null;
            var file = NSBTP.Read(data);
            return file.Header.ID == "BTP0" && file.AnimData != null ? new TexturePatternAnimation(file) : null;
        }

        /// <summary>Index of the material this animation swaps, or -1 when it doesn't touch it.</summary>
        public int IndexOf(string materialName) =>
            materialName != null && _byName.TryGetValue(materialName, out int i) ? i : -1;

        /// <summary>Which texture a material shows on a frame. Frames wrap, as the games loop forever.</summary>
        public Swap Evaluate(int material, int frame)
        {
            var frames = Frames;
            if (material < 0 || material >= frames.Length) return default;
            var keys = frames[material];
            if (keys.Length == 0) return default;

            if (frame < 0) frame = 0;
            frame %= Math.Max(1, FrameCount);

            // Show the texture named by the last key frame at or before now.
            var chosen = keys[0];
            foreach (var k in keys)
            {
                if (k.Start > frame) break;
                chosen = k;
            }
            return new Swap { TextureName = chosen.texName, PaletteName = chosen.palName };
        }

        public Swap Evaluate(string materialName, int frame) => Evaluate(IndexOf(materialName), frame);

        /// <summary>Every texture a material can end up showing, so they can all be made ready up front.</summary>
        public IEnumerable<Swap> AllSwaps(int material)
        {
            var frames = Frames;
            if (material < 0 || material >= frames.Length) yield break;
            foreach (var k in frames[material])
                yield return new Swap { TextureName = k.texName, PaletteName = k.palName };
        }

        /// <summary>True when a material never actually changes texture, so it can be skipped.</summary>
        public bool IsStatic(int material)
        {
            var swaps = AllSwaps(material).Select(s => s.TextureName + "/" + s.PaletteName).Distinct().Take(2).ToArray();
            return swaps.Length < 2;
        }
    }
}
