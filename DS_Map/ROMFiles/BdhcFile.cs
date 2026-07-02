using System;
using System.Collections.Generic;
using System.IO;

namespace DSPRE.ROMFiles
{
    public sealed class BdhcFile
    {
        private const float Fx32One = 4096f;
        private const float UnitsPerRaw = 64f;
        private const float MapCenterUnits = 256f;
        private const int MaxCandidates = 10;

        private struct Point { public float X, Z; }
        private struct Normal { public float X, Y, Z; }
        private struct Plate { public ushort FirstPoint, SecondPoint, Normal, Constant; }
        private struct Strip { public float Scanline; public ushort Count, Start; }

        private Point[] _points;
        private Normal[] _normals;
        private float[] _constants;
        private Plate[] _plates;
        private Strip[] _strips;
        private ushort[] _accessList;

        public static bool TryParse(byte[] data, out BdhcFile bdhc)
        {
            bdhc = null;
            if (data == null || data.Length < 16) return false;

            try
            {
                using (var reader = new BinaryReader(new MemoryStream(data)))
                {
                    var magic = reader.ReadBytes(4);
                    if (magic.Length != 4 || magic[0] != 'B' || magic[1] != 'D' || magic[2] != 'H' || magic[3] != 'C')
                        return false;

                    int pointsCount = reader.ReadUInt16();
                    int normalsCount = reader.ReadUInt16();
                    int constantsCount = reader.ReadUInt16();
                    int platesCount = reader.ReadUInt16();
                    int stripsCount = reader.ReadUInt16();
                    int accessListCount = reader.ReadUInt16();

                    long needed = 16L +
                                  8L * pointsCount +
                                  12L * normalsCount +
                                  4L * constantsCount +
                                  8L * platesCount +
                                  8L * stripsCount +
                                  2L * accessListCount;
                    if (needed > data.Length) return false;

                    var parsed = new BdhcFile
                    {
                        _points = new Point[pointsCount],
                        _normals = new Normal[normalsCount],
                        _constants = new float[constantsCount],
                        _plates = new Plate[platesCount],
                        _strips = new Strip[stripsCount],
                        _accessList = new ushort[accessListCount],
                    };

                    for (int i = 0; i < pointsCount; i++)
                        parsed._points[i] = new Point { X = reader.ReadInt32() / Fx32One, Z = reader.ReadInt32() / Fx32One };

                    for (int i = 0; i < normalsCount; i++)
                        parsed._normals[i] = new Normal
                        {
                            X = reader.ReadInt32() / Fx32One,
                            Y = reader.ReadInt32() / Fx32One,
                            Z = reader.ReadInt32() / Fx32One,
                        };

                    for (int i = 0; i < constantsCount; i++)
                        parsed._constants[i] = reader.ReadInt32() / Fx32One;

                    for (int i = 0; i < platesCount; i++)
                        parsed._plates[i] = new Plate
                        {
                            FirstPoint = reader.ReadUInt16(),
                            SecondPoint = reader.ReadUInt16(),
                            Normal = reader.ReadUInt16(),
                            Constant = reader.ReadUInt16(),
                        };

                    for (int i = 0; i < stripsCount; i++)
                        parsed._strips[i] = new Strip
                        {
                            Scanline = reader.ReadInt32() / Fx32One,
                            Count = reader.ReadUInt16(),
                            Start = reader.ReadUInt16(),
                        };

                    for (int i = 0; i < accessListCount; i++)
                        parsed._accessList[i] = reader.ReadUInt16();

                    bdhc = parsed;
                    return true;
                }
            }
            catch
            {
                bdhc = null;
                return false;
            }
        }

        public bool TryGetHeight(float localRawX, float localRawZ, float preferredRawY, out float rawY)
        {
            rawY = 0f;
            if (_strips == null || _strips.Length == 0 || _plates == null || _plates.Length == 0) return false;

            float x = localRawX * UnitsPerRaw - MapCenterUnits;
            float z = localRawZ * UnitsPerRaw - MapCenterUnits;
            float preferredY = preferredRawY * UnitsPerRaw;

            int low = 0, high = _strips.Length - 1;
            while (low < high)
            {
                int mid = (low + high) / 2;
                if (_strips[mid].Scanline > z) high = mid;
                else low = mid + 1;
            }

            var strip = _strips[low];
            if (strip.Start >= _accessList.Length) return false;

            var candidates = new List<float>(MaxCandidates);
            int accessEnd = Math.Min(_accessList.Length, strip.Start + strip.Count);
            for (int i = strip.Start; i < accessEnd && candidates.Count < MaxCandidates; i++)
            {
                int plateIndex = _accessList[i];
                if (plateIndex >= _plates.Length) continue;
                var plate = _plates[plateIndex];
                if (plate.FirstPoint >= _points.Length || plate.SecondPoint >= _points.Length ||
                    plate.Normal >= _normals.Length || plate.Constant >= _constants.Length)
                    continue;

                var a = _points[plate.FirstPoint];
                var b = _points[plate.SecondPoint];
                float minX = Math.Min(a.X, b.X), maxX = Math.Max(a.X, b.X);
                float minZ = Math.Min(a.Z, b.Z), maxZ = Math.Max(a.Z, b.Z);
                if (x < minX || x > maxX || z < minZ || z > maxZ) continue;

                var n = _normals[plate.Normal];
                if (Math.Abs(n.Y) < 0.0001f) continue;
                float y = -((n.X * x) + (n.Z * z) + _constants[plate.Constant]) / n.Y;
                candidates.Add(y);
            }

            if (candidates.Count == 0) return false;

            float best = candidates[0];
            float bestDiff = Math.Abs(preferredY - best);
            for (int i = 1; i < candidates.Count; i++)
            {
                float diff = Math.Abs(preferredY - candidates[i]);
                if (diff < bestDiff)
                {
                    best = candidates[i];
                    bestDiff = diff;
                }
            }

            rawY = best / UnitsPerRaw;
            return true;
        }
    }
}
