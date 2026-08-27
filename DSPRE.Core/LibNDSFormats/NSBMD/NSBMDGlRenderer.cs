// OpenGL Renderer for NSBMD models.
// Code adapted from kiwi.ds' NSBMD Model Viewer.
// All OpenGL rendering methods are no-op stubs until the OpenTK 4 rewrite.

using System.Collections.Generic;
using MKDS_Course_Editor.NSBTA;
using MKDS_Course_Editor.NSBTP;
using NSBCAFile = MKDS_Course_Editor.NSBCA.NSBCA.NSBCA_File;

namespace LibNDSFormats.NSBMD
{
    /// <summary>
    /// STUB - OpenGL renderer for NSBMD models.
    /// Rendering is disabled pending the OpenTK 4.x rewrite (migration step 33).
    /// Math utility methods are fully functional.
    /// </summary>
    public class NSBMDGlRenderer
    {
        // ----------------------------------------------------------------
        // Public static options (read/written by MapEditor, EventEditor)
        // ----------------------------------------------------------------
        public static bool gOptColoring  = true;
        public static bool gOptWireFrame = false;

        // ----------------------------------------------------------------
        // Public fields (read by OBJ exporter / 3D tools)
        // ----------------------------------------------------------------
        public List<float[]> vertex        = new List<float[]>();
        public List<float[]> normals       = new List<float[]>();
        public List<int>     vertex_normal = new List<int>();

        // ----------------------------------------------------------------
        // Model property
        // ----------------------------------------------------------------
        private NSBMDModel _model;
        public NSBMDModel Model
        {
            get => _model;
            set { _model = value; } // MakeTexture removed until OpenTK rewrite
        }

        // ----------------------------------------------------------------
        // Render mode enum (referenced by callers)
        // ----------------------------------------------------------------
        public enum RenderMode { Opaque = 1, Translucent, Picking }

        /// <summary>Converts the NSBMD 0-31 material alpha into a 0-1 GL alpha (31 = fully opaque).
        /// Ported from upstream main's PR #209 ("Make Map Editor texture preview respect 1-30 alpha
        /// values for materials") even though RenderModel here is currently a stub pending the OpenTK
        /// rewrite — kept in sync so a future merge with main doesn't conflict on this hunk, and so the
        /// eventual OpenTK renderer has it ready to call. The real fix now lives in the Avalonia
        /// renderer: see NsbmdGeometry.MaterialAlpha + NsbmdGlControl's per-part GL_BLEND.</summary>
        private static float MaterialAlpha(NSBMDMaterial mat) {
            return mat.Alpha >= 31 ? 1.0f : mat.Alpha / 31.0f;
        }

        // ----------------------------------------------------------------
        // Constructors
        // ----------------------------------------------------------------
        public NSBMDGlRenderer() { }
        public NSBMDGlRenderer(int matstart) { }

        // ----------------------------------------------------------------
        // RenderModel overloads — no-op stubs
        // ----------------------------------------------------------------
        public void RenderModel(
            string file2,
            NSBTA.NSBTA_File ani,
            int[] aniframeS, int[] aniframeT,
            int[] aniframeScaleS, int[] aniframeScaleT,
            int[] aniframeR,
            NSBCAFile ca,
            bool anim, int selectedani,
            float X, float Y,
            float dist, float elev, float ang,
            NSBTP.NSBTP_File p, NSBMD nsb)
        { /* stub — awaiting OpenTK 4 rewrite */ }

        public void RenderModel(
            string file2,
            NSBTA.NSBTA_File ani,
            int[] aniframeS, int[] aniframeT,
            int[] aniframeScaleS, int[] aniframeScaleT,
            int[] aniframeR,
            NSBCAFile ca,
            bool anim, int selectedani,
            float X, float Y,
            float dist, float elev, float ang,
            bool licht,
            NSBTP.NSBTP_File p, NSBMD nsb)
        { /* stub — awaiting OpenTK 4 rewrite */ }

        // ----------------------------------------------------------------
        // Pure math helpers — FULLY FUNCTIONAL (used by data-parsing layer)
        // ----------------------------------------------------------------

        public static int Sign(int data, int size)
        {
            if ((data & 1 << size - 1) != 0)
                data |= -1 << size;
            return data;
        }

        public static float[] loadIdentity()
        {
            float[] a = new float[16];
            a[0]  = 1.0F;
            a[5]  = 1.0F;
            a[10] = 1.0F;
            a[15] = 1.0F;
            return a;
        }

        public static float[] multMatrix(float[] a, float[] b)
        {
            float[] c = new float[16];
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                {
                    c[(i << 2) + j] = 0.0F;
                    for (int k = 0; k < 4; k++)
                        c[(i << 2) + j] += a[(k << 2) + j] * b[(i << 2) + k];
                }
            return c;
        }

        public static float[] Translate(float[] a, float x, float y, float z)
        {
            float[] b = loadIdentity();
            b[12] = x;
            b[13] = y;
            b[14] = z;
            return multMatrix(a, b);
        }

        public static float[] Rotate(float[] a, float x, float y, float z)
        {
            float[] b = loadIdentity();
            float cx = (float)System.Math.Cos(x);  float sx = (float)System.Math.Sin(x);
            float cy = (float)System.Math.Cos(y);  float sy = (float)System.Math.Sin(y);
            float cz = (float)System.Math.Cos(z);  float sz = (float)System.Math.Sin(z);
            b[0] = cy * cz;   b[1] = cy * sz;   b[2]  = -sy;
            b[4] = cz * sx * sy - sz * cx; b[5] = sx * sy + cx * cz; b[6] = sx * cy;
            b[8] = cx * cz * sy + sx * sz; b[9] = cx * sy * sz - sx * cz; b[10] = cx * cy;
            return multMatrix(a, b);
        }

        public static float[] scale(float[] a, float x, float y, float z)
        {
            float[] b = loadIdentity();
            b[0]  = x;
            b[5]  = y;
            b[10] = z;
            return multMatrix(a, b);
        }

        public static float[] loadMatrix(float[] fmatrix, int stack)
        {
            float[] a = loadIdentity();
            for (int i = 0; i < a.Length; i++)
                a[i] = fmatrix[stack * 16 + i];
            return a;
        }

        public float[] multVector(float[] cmatrix, float[] vtxState)
        {
            float[] vtxTrans = new float[3];
            for (int i = 0; i < 3; i++)
                vtxTrans[i] = vtxState[0] * cmatrix[0 + i]
                            + vtxState[1] * cmatrix[4 + i]
                            + vtxState[2] * cmatrix[8 + i]
                            + cmatrix[12 + i];
            return vtxTrans;
        }

        public float[] pullVector(float[] fmatrix, int offset)
        {
            float[] cmatrix = new float[16];
            for (int i = 0; i < cmatrix.Length; i++)
                cmatrix[i] = fmatrix[offset + i];
            return multVector(cmatrix, new float[] { 0.0F, 0.0F, 0.0F });
        }
    }
}
