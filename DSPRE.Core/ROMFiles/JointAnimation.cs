using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LibNDSFormats.NSBMD;
using MKDS_Course_Editor.NSBCA;

namespace DSPRE.ROMFiles
{
    /// <summary>A joint animation (NSBCA): it moves, turns and resizes model parts, like windmill sails or a lift platform.</summary>
    public sealed class JointAnimation
    {
        /// <summary>A turn is stored in six bytes: which way it turns, then its sine and cosine.</summary>
        public const int PivotEntrySize = 6;

        private readonly NSBCA.NSBCA_File _file;
        private readonly int _animation;

        public int FrameCount { get; }

        /// <summary>The model parts this animation drives. Anything else keeps the pose the model gives it.</summary>
        public IReadOnlyList<int> AnimatedObjects { get; }

        /// <summary>The animation's 16-character name in the JNT0 block.</summary>
        public string Name { get; }

        /// <summary>How many animations the file holds. One file can carry several.</summary>
        public int Count { get; }

        private JointAnimation(NSBCA.NSBCA_File file, int animation)
        {
            _file = file;
            _animation = animation;
            NSBCA.NSBCA_File.J_AC jac = file.JAC[animation];
            FrameCount = Math.Max(1, (int)jac.NrFrames);
            Count = file.JAC.Length;
            Name = file.JNT0.Names != null && animation < file.JNT0.Names.Length
                ? (file.JNT0.Names[animation] ?? "").Trim() : null;
            AnimatedObjects = (jac.ObjInfo ?? Array.Empty<NSBCA.NSBCA_File.J_AC.objInfo>())
                .Where(Drives).Select(o => (int)o.ID).Distinct().ToArray();
        }

        /// <summary>The names of every animation in a file, or an empty list when there are none.</summary>
        public static IReadOnlyList<string> NamesIn(byte[] data)
        {
            if (data == null || data.Length < 4) return Array.Empty<string>();
            if (Encoding.ASCII.GetString(data, 0, 4) != "BCA0") return Array.Empty<string>();
            try
            {
                NSBCA.NSBCA_File file = NSBCA.Read(data);
                return file.JNT0.Names?.Select(n => (n ?? "").Trim()).ToArray() ?? Array.Empty<string>();
            }
            catch (Exception ex) { AppLogger.WarnOnce("JointAnimation.NamesIn", ex); return Array.Empty<string>(); }
        }

        /// <summary>Reads one, or null when the file is some other kind of animation.</summary>
        public static JointAnimation Load(byte[] data, int animation = 0)
        {
            if (data == null || data.Length < 4) return null;
            if (Encoding.ASCII.GetString(data, 0, 4) != "BCA0") return null;

            NSBCA.NSBCA_File file;
            try { file = NSBCA.Read(data); } catch (Exception ex) { AppLogger.WarnOnce("JointAnimation.Load", ex); return null; }
            if (file.JAC == null || animation < 0 || animation >= file.JAC.Length) return null;
            if (file.JAC[animation].ObjInfo == null) return null;
            return new JointAnimation(file, animation);
        }

        /// <summary>True when any part of this animation moves.</summary>
        public bool Moves => AnimatedObjects.Count > 0;

        private static bool Drives(NSBCA.NSBCA_File.J_AC.objInfo o) =>
            HasTranslation(o) || HasRotation(o) || HasScale(o);

        private static bool HasTranslation(NSBCA.NSBCA_File.J_AC.objInfo o) =>
            (o.Translate?.Any(l => l != null && l.Count > 0) ?? false)
            || (o.TranslateKeyframes?.Any(l => l != null && l.Count > 0) ?? false);

        private static bool HasRotation(NSBCA.NSBCA_File.J_AC.objInfo o) =>
            (o.RotateKeyframes?[0]?.Count ?? 0) > 0;

        private static bool HasScale(NSBCA.NSBCA_File.J_AC.objInfo o) =>
            (o.Scale?.Any(x => x != null && x[0] != null && x[0].Count > 0) ?? false)
            || (o.ScaleKeyframes?.Any(x => x != null && x[0] != null && x[0].Count > 0) ?? false);

        /// <summary>
        /// The matrix to use for one model part on one frame, or null when this animation leaves that part
        /// alone.
        /// </summary>
        /// <param name="modelObject">The part as the model has it, for whatever the animation leaves alone.</param>
        /// <param name="modelScale">
        /// The model's scale; animation distances are divided by it too, or parts fly out of place.
        /// </param>
        public float[] MatrixFor(int objectId, int frame, NSBMDObject modelObject = null, float modelScale = 1f)
        {
            NSBCA.NSBCA_File.J_AC jac = _file.JAC[_animation];
            NSBCA.NSBCA_File.J_AC.objInfo obj = jac.ObjInfo.FirstOrDefault(o => o.ID == objectId);
            if (obj.ID != objectId || !Drives(obj)) return null;

            if (frame < 0) frame = 0;
            frame %= FrameCount;

            float[] t = Translation(obj, frame);
            float[] r = Rotation(jac, obj, frame);
            float[] s = Scale(obj, frame);

            // Fall back to the model's own value for anything this animation does not drive.
            if (t == null && modelObject?.TransVect != null && modelObject.Trans)
                t = new[] { modelObject.TransVect[0] * modelScale,      // undone again below
                            modelObject.TransVect[1] * modelScale,
                            modelObject.TransVect[2] * modelScale };
            if (r == null && modelObject != null && modelObject.IsRotated)
                r = modelObject.RotateMtx;
            if (s == null && modelObject != null && modelObject.IsScaled && modelObject.Scale != null)
                s = new[] { modelObject.Scale[0], modelObject.Scale[1], modelObject.Scale[2] };

            if (modelScale <= 0f) modelScale = 1f;

            float[] m = NSBMDGlRenderer.LoadIdentity();
            if (t != null)
                m = NSBMDGlRenderer.MultMatrix(m, Translate(t[0] / modelScale, t[1] / modelScale, t[2] / modelScale));
            if (r != null) m = NSBMDGlRenderer.MultMatrix(m, r);
            if (s != null)
                m = NSBMDGlRenderer.MultMatrix(m, NSBMDGlRenderer.Scale(NSBMDGlRenderer.LoadIdentity(), s[0], s[1], s[2]));
            return m;
        }

        private static float[] Translate(float x, float y, float z)
        {
            float[] m = NSBMDGlRenderer.LoadIdentity();
            m[12] = x; m[13] = y; m[14] = z;
            return m;
        }

        private static float[] Translation(NSBCA.NSBCA_File.J_AC.objInfo o, int frame)
        {
            if (!HasTranslation(o)) return null;
            float[] v = new float[3];
            for (int k = 0; k < 3; k++) v[k] = Track(o.Translate?[k], o.TranslateKeyframes?[k], frame, 0f);
            return v;
        }

        private static float[] Scale(NSBCA.NSBCA_File.J_AC.objInfo o, int frame)
        {
            if (!HasScale(o)) return null;
            float[] v = new float[3];
            for (int k = 0; k < 3; k++)
                v[k] = Track(o.Scale?[k]?[0], o.ScaleKeyframes?[k]?[0], frame, 1f);

            // A scale of zero would squash the part out of existence, which is not something the games
            // do; where one turns up it means that axis is not really being scaled, so leave it alone.
            for (int k = 0; k < 3; k++) if (v[k] == 0f) v[k] = 1f;
            return v;
        }

        /// <summary>A channel is either one value that never changes or one value per frame.</summary>
        private static float Track(List<float> constant, List<float> perFrame, int frame, float fallback)
        {
            if (constant != null && constant.Count > 0) return constant[0];
            if (perFrame != null && perFrame.Count > 0) return perFrame[frame % perFrame.Count];
            return fallback;
        }

        /// <summary>The turn for one frame, read out of the animation's pivot pool. </summary>
        private static float[] Rotation(NSBCA.NSBCA_File.J_AC jac, NSBCA.NSBCA_File.J_AC.objInfo o, int frame)
        {
            List<float> indices = o.RotateKeyframes?[0];
            List<float> modes = o.RotateKeyframes?[1];
            if (indices == null || indices.Count == 0) return null;

            int at = frame % indices.Count;
            int index = (int)indices[at];
            int mode = modes != null && at < modes.Count ? (int)modes[at] : 1;
            if (mode != 1) return null;               // a whole stored matrix, which these games never use

            byte[] pool = jac.JointData;
            int off = index * PivotEntrySize;
            if (pool == null || off + PivotEntrySize > pool.Length) return null;

            ushort packed = BitConverter.ToUInt16(pool, off);
            short a = BitConverter.ToInt16(pool, off + 2);
            short b = BitConverter.ToInt16(pool, off + 4);

            return NSBMD.MtxPivot(new[] { a / 4096f, b / 4096f }, packed & 0xF, (packed >> 4) & 0xF);
        }
    }
}
