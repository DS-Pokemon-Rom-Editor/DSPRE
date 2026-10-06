using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MKDS_Course_Editor.NSBTA;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// A terrain animation (NSBTA) ready to play: it scrolls, scales and rotates a material's texture
    /// coordinates over time.
    /// </summary>
    public sealed class TextureSrtAnimation
    {
        public struct Srt
        {
            public float ScaleS, ScaleT;
            // Rotation is kept as its sine and cosine, which is how the file stores it.
            public float SinRotation, CosRotation;
            public float TranslateS, TranslateT;

            public static Srt Identity => new Srt { ScaleS = 1f, ScaleT = 1f, CosRotation = 1f };

            /// <summary>
            /// The transform as a 3x3 matrix in OpenGL's column-major order, ready to multiply a texture
            /// coordinate by.
            /// </summary>
            /// <param name="mode">The model's texture matrix mode (NNSG3dResMdlInfo.texMtxMode): 0 Maya, 1 SoftImage 3D,
            /// 2 3ds Max, 3 XSI. Each tool builds the matrix its own way; the games' field models are Maya, where the S
            /// translation is subtracted and T is measured from the far edge. In texture-size units, so the texture's
            /// pixel size only enters through <paramref name="heightOverWidth"/> where a rotation mixes the axes.</param>
            public float[] ToMatrix3(int mode = 0, float heightOverWidth = 1f)
            {
                float sS = ScaleS, sT = ScaleT, sin = SinRotation, cos = CosRotation, tS = TranslateS, tT = TranslateT;
                float a, b, c, d, tx, ty;   // u' = a u + c v + tx, v' = b u + d v + ty
                switch (mode)
                {
                    case 1:   // SoftImage 3D: no rotation
                        a = sS; b = 0f; c = 0f; d = sT;
                        tx = -sS * tS; ty = -sT * tT;
                        break;
                    case 2:   // 3ds Max
                        a = sS * cos; c = -sS * sin; b = sT * sin; d = sT * cos;
                        tx = sS * cos * (-0.5f - tS) - sS * sin * heightOverWidth * (-0.5f + tT) + 0.5f;
                        ty = sT * sin / heightOverWidth * (-0.5f - tS) + sT * cos * (-0.5f + tT) + 0.5f;
                        break;
                    case 3:   // XSI
                        a = sS * cos; c = -sS * sin; b = sT * sin; d = sT * cos;
                        tx = sS * sin - sS * (tS * cos + tT * sin);
                        ty = 1f - sT * cos - sT * (tS * sin - tT * cos);
                        break;
                    default:  // Maya
                        a = sS * cos; c = sS * sin; b = -sT * sin; d = sT * cos;
                        tx = sS * (1f - sin - cos) / 2f - sS * tS;
                        ty = (sT * sin - sT * cos - sT + 2f) / 2f + sT * tT;
                        break;
                }
                return new[] { a, b, 0f, c, d, 0f, tx, ty, 1f };
            }
        }

        private readonly NSBTA.NSBTA_File _file;
        private readonly Dictionary<string, int> _byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<string> MaterialNames { get; }

        /// <summary>Longest track in the animation: how many frames a full loop takes.</summary>
        public int FrameCount { get; }

        private TextureSrtAnimation(NSBTA.NSBTA_File file)
        {
            _file = file;
            MaterialNames = file.MAT.names ?? Array.Empty<string>();
            for (int i = 0; i < MaterialNames.Count; i++)
                _byName[MaterialNames[i]] = i;

            int longest = 1;
            for (int i = 0; i < MaterialNames.Count; i++)
                foreach (decimal[] track in Tracks(i))
                    if (track != null && track.Length > longest) longest = track.Length;
            FrameCount = longest;
        }

        /// <summary>Reads one, or null when the file is some other kind of animation. Callers hand this
        /// whole archives to sort through, so a file that isn't a texture animation is expected.</summary>
        public static TextureSrtAnimation Load(byte[] data)
        {
            if (data == null || data.Length < 4) return null;
            if (Encoding.ASCII.GetString(data, 0, 4) != "BTA0") return null;
            NSBTA.NSBTA_File file = NSBTA.Read(data);
            return file.Header.ID == "BTA0" && file.SRTData != null ? new TextureSrtAnimation(file) : null;
        }

        private IEnumerable<decimal[]> Tracks(int material)
        {
            NSBTA.NSBTA_File.srtData d = _file.SRTData[material];
            yield return d.scaleS; yield return d.scaleT;
            yield return d.translateS; yield return d.translateT;
        }

        /// <summary>Index of the material this animation drives, or -1 when it doesn't touch it.</summary>
        public int IndexOf(string materialName) =>
            materialName != null && _byName.TryGetValue(materialName, out int i) ? i : -1;

        private static float At(decimal[] track, int frame, float fallback)
        {
            if (track == null || track.Length == 0) return fallback;
            return (float)track[track.Length == 1 ? 0 : frame % track.Length];
        }

        /// <summary>The texture transform for one material on one frame. Frames wrap, as the games loop forever.</summary>
        public Srt Evaluate(int material, int frame)
        {
            if (material < 0 || material >= MaterialNames.Count) return Srt.Identity;
            if (frame < 0) frame = 0;

            NSBTA.NSBTA_File.srtData d = _file.SRTData[material];
            Srt srt = new Srt
            {
                ScaleS = At(d.scaleS, frame, 1f),
                ScaleT = At(d.scaleT, frame, 1f),
                SinRotation = 0f,
                CosRotation = 1f,
                TranslateS = At(d.translateS, frame, 0f),
                TranslateT = At(d.translateT, frame, 0f),
            };

            // A rotating material stores a sine and a cosine per frame, side by side.
            if (d.rotate != null && d.rotate.Length > 2)
            {
                int pairs = d.rotate.Length / 2;
                int i = (frame % pairs) * 2;
                srt.SinRotation = (float)d.rotate[i];
                srt.CosRotation = (float)d.rotate[i + 1];
            }
            return srt;
        }

        public Srt Evaluate(string materialName, int frame) => Evaluate(IndexOf(materialName), frame);

        /// <summary>True when this material never moves, so the renderer can skip it entirely.</summary>
        public bool IsStatic(int material)
        {
            if (material < 0 || material >= MaterialNames.Count) return true;
            decimal[] rot = _file.SRTData[material].rotate;
            if (rot != null && rot.Length > 2) return false;
            return Tracks(material).All(t => t == null || t.Length <= 1);
        }
    }
}
