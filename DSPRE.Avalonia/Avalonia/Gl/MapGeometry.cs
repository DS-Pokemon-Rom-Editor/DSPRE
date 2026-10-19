using System;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Gl
{
    /// <summary>Shared map-geometry helpers (building placement transform), used by the map
    /// and event editors and the matrix-scene builder so the maths lives in one place.</summary>
    public static class MapGeometry
    {
        /// <summary>Builds the scale/translate/rotate transform that places a building model into
        /// its map's local space (recovered from the original WinForms ScaleTranslateRotateBuilding).</summary>
        public static float[] BuildingTransform(Building b)
        {
            float fx = b.XPosition + b.XFraction / 65536f;
            float fy = b.YPosition + b.YFraction / 65536f;
            float fz = b.ZPosition + b.ZFraction / 65536f;

            float ms = b.NSBMDFile.Models[0].ModelScale;
            if (ms == 0) ms = 1f;
            float sf = ms / 1024f, tf = 256f / ms;
            float w = Math.Max(1, b.Width), h = Math.Max(1, b.Height), l = Math.Max(1, b.Length);
            const float d2r = (float)Math.PI / 180f;

            float[] scale = Mat4.Scale(sf * w, sf * h, sf * l);
            float[] trans = Mat4.Translate(fx * tf / w, fy * tf / h, fz * tf / l);
            float[] rx = Mat4.RotateX(Building.U16ToDeg(b.XRotation) * d2r);
            float[] ry = Mat4.RotateY(Building.U16ToDeg(b.YRotation) * d2r);
            float[] rz = Mat4.RotateZ(Building.U16ToDeg(b.ZRotation) * d2r);
            // The engine applies rotations to a building's local axes in X, then Y, then Z order,
            // so X must be innermost here.
            return Mat4.Multiply(scale, Mat4.Multiply(trans, Mat4.Multiply(rz, Mat4.Multiply(ry, rx))));
        }
    }
}
