using System;

namespace DSPRE.Avalonia.Gl
{
    /// <summary>Minimal column-major 4x4 matrix helper for the GL renderer (no external math dep).</summary>
    public static class Mat4
    {
        public static float[] Identity() => new float[]
        { 1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1 };

        public static float[] Multiply(float[] a, float[] b)
        {
            var r = new float[16];
            for (int col = 0; col < 4; col++)
                for (int row = 0; row < 4; row++)
                {
                    float sum = 0;
                    for (int k = 0; k < 4; k++)
                        sum += a[k * 4 + row] * b[col * 4 + k];
                    r[col * 4 + row] = sum;
                }
            return r;
        }

        public static float[] Perspective(float fovYRadians, float aspect, float near, float far)
        {
            float f = 1f / (float)Math.Tan(fovYRadians / 2.0);
            var m = new float[16];
            m[0] = f / aspect;
            m[5] = f;
            m[10] = (far + near) / (near - far);
            m[11] = -1f;
            m[14] = (2f * far * near) / (near - far);
            return m;
        }

        /// <summary>Orthographic projection, for the flat 2D map view. </summary>
        public static float[] Ortho(float halfHeight, float aspect, float near, float far)
        {
            float halfWidth = halfHeight * aspect;
            var m = Identity();
            m[0] = 1f / halfWidth;
            m[5] = 1f / halfHeight;
            m[10] = -2f / (far - near);
            m[14] = -(far + near) / (far - near);
            return m;
        }

        public static float[] Translate(float x, float y, float z)
        {
            var m = Identity();
            m[12] = x; m[13] = y; m[14] = z;
            return m;
        }

        public static float[] Scale(float x, float y, float z)
        {
            var m = Identity();
            m[0] = x; m[5] = y; m[10] = z;
            return m;
        }

        public static float[] RotateX(float a)
        {
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            var m = Identity();
            m[5] = c; m[6] = s; m[9] = -s; m[10] = c;
            return m;
        }

        public static float[] RotateY(float a)
        {
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            var m = Identity();
            m[0] = c; m[2] = -s; m[8] = s; m[10] = c;
            return m;
        }

        public static float[] RotateZ(float a)
        {
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            var m = Identity();
            m[0] = c; m[1] = s; m[4] = -s; m[5] = c;
            return m;
        }

        /// <summary>Transforms a point (w=1) by a column-major 4x4 matrix.</summary>
        public static void TransformPoint(float[] m, ref float x, ref float y, ref float z)
        {
            float nx = m[0] * x + m[4] * y + m[8] * z + m[12];
            float ny = m[1] * x + m[5] * y + m[9] * z + m[13];
            float nz = m[2] * x + m[6] * y + m[10] * z + m[14];
            x = nx; y = ny; z = nz;
        }

        /// <summary>Builds a view matrix orbiting the origin: distance + yaw (deg) + pitch (deg).</summary>
        public static float[] OrbitView(float distance, float yawDeg, float pitchDeg)
        {
            float yaw = yawDeg * (float)Math.PI / 180f;
            float pitch = pitchDeg * (float)Math.PI / 180f;
            var t = Translate(0, 0, -distance);
            var rx = RotateX(pitch);
            var ry = RotateY(yaw);
            // view = T * Rx * Ry  (rotate model into view space, then push back)
            return Multiply(t, Multiply(rx, ry));
        }
    }
}
