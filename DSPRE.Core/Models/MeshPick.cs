using System;
using System.Collections.Generic;

namespace DSPRE.Models
{
    /// <summary>Ray picking against a map mesh.</summary>
    public static class MeshPick
    {
        public struct Hit
        {
            public int Face;

            public float Distance;

            public float X, Y, Z;

            public int NearestCorner;
        }

        public static Hit? Nearest(MapMesh mesh, float[] toScene,
                                   float ox, float oy, float oz, float dx, float dy, float dz,
                                   Func<int, bool> allowed = null)
        {
            if (mesh == null || mesh.Faces.Count == 0) return null;

            Hit? best = null;

            for (int f = 0; f < mesh.Faces.Count; f++)
            {
                if (allowed != null && !allowed(f)) continue;
                var face = mesh.Faces[f];
                if (face.Corners == null || face.Corners.Length < 3) continue;

                for (int i = 1; i + 1 < face.Corners.Length; i++)
                {
                    var a = Place(mesh, toScene, face.Corners[0]);
                    var b = Place(mesh, toScene, face.Corners[i]);
                    var c = Place(mesh, toScene, face.Corners[i + 1]);

                    if (!Through(ox, oy, oz, dx, dy, dz, a, b, c, out float t)) continue;
                    if (best != null && t >= best.Value.Distance) continue;

                    float hx = ox + dx * t, hy = oy + dy * t, hz = oz + dz * t;
                    best = new Hit
                    {
                        Face = f,
                        Distance = t,
                        X = hx, Y = hy, Z = hz,
                        NearestCorner = Closest(mesh, toScene, face, hx, hy, hz),
                    };
                }
            }

            return best;
        }

        private static int Closest(MapMesh mesh, float[] toScene, MapMesh.Face face,
                                   float x, float y, float z)
        {
            int best = face.Corners[0];
            float closest = float.MaxValue;
            foreach (int corner in face.Corners)
            {
                var (px, py, pz) = Place(mesh, toScene, corner);
                float d = (px - x) * (px - x) + (py - y) * (py - y) + (pz - z) * (pz - z);
                if (d < closest) { closest = d; best = corner; }
            }
            return best;
        }

        public static (float x, float y, float z) Place(MapMesh mesh, float[] m, int vertex)
        {
            var v = mesh.Vertices[vertex];
            return Place(m, v.X, v.Y, v.Z);
        }

        public static (float x, float y, float z) Place(float[] m, float x, float y, float z)
        {
            if (m == null || m.Length != 16) return (x, y, z);
            return (m[0] * x + m[4] * y + m[8] * z + m[12],
                    m[1] * x + m[5] * y + m[9] * z + m[13],
                    m[2] * x + m[6] * y + m[10] * z + m[14]);
        }

        public static bool Crosses(float ox, float oy, float oz, float dx, float dy, float dz,
                                   (float x, float y, float z) a, (float x, float y, float z) b,
                                   (float x, float y, float z) c, out float t)
            => Through(ox, oy, oz, dx, dy, dz, a, b, c, out t);

        private static bool Through(float ox, float oy, float oz, float dx, float dy, float dz,
                                    (float x, float y, float z) a,
                                    (float x, float y, float z) b,
                                    (float x, float y, float z) c,
                                    out float t)
        {
            t = 0f;
            float e1x = b.x - a.x, e1y = b.y - a.y, e1z = b.z - a.z;
            float e2x = c.x - a.x, e2y = c.y - a.y, e2z = c.z - a.z;

            float px = dy * e2z - dz * e2y;
            float py = dz * e2x - dx * e2z;
            float pz = dx * e2y - dy * e2x;

            float det = e1x * px + e1y * py + e1z * pz;
            if (Math.Abs(det) < 1e-9f) return false;

            float inv = 1f / det;
            float tx = ox - a.x, ty = oy - a.y, tz = oz - a.z;

            float u = (tx * px + ty * py + tz * pz) * inv;
            if (u < -1e-5f || u > 1f + 1e-5f) return false;

            float qx = ty * e1z - tz * e1y;
            float qy = tz * e1x - tx * e1z;
            float qz = tx * e1y - ty * e1x;

            float v = (dx * qx + dy * qy + dz * qz) * inv;
            if (v < -1e-5f || u + v > 1f + 1e-5f) return false;

            t = (e2x * qx + e2y * qy + e2z * qz) * inv;
            return t > 1e-5f;
        }
    }
}
