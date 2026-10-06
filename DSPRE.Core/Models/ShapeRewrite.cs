using System;
using System.Collections.Generic;

namespace DSPRE.Models
{
    /// <summary>Rewrites a display list with some vertices moved.</summary>
    public static class ShapeRewrite
    {
        private const int One = 4096;

        private const int Nearest = -8 * One;
        private const int Furthest = 8 * One - 1;

        public static byte[] WithCornersAt(byte[] dl,
            IReadOnlyDictionary<int, (float x, float y, float z)> moved, out string whynot)
        {
            whynot = null;
            if (dl == null) { whynot = "Empty display list."; return null; }
            if (moved == null || moved.Count == 0) return (byte[])dl.Clone();

            DisplayListWalk walk = DisplayListWalk.Read(dl, out whynot);
            if (walk == null) return null;

            Dictionary<int, (int x, int y, int z)> want = new Dictionary<int, (int x, int y, int z)>();
            foreach (DisplayListWalk.Run run in walk.Runs)
                foreach (DisplayListWalk.Corner corner in run.Corners)
                    want[corner.Index] = moved.TryGetValue(corner.Index, out (float x, float y, float z) to)
                        ? (Round(to.x), Round(to.y), Round(to.z))
                        : (corner.RawX, corner.RawY, corner.RawZ);

            foreach (KeyValuePair<int, (float x, float y, float z)> kv in moved)
                if (!want.ContainsKey(kv.Key)) { whynot = $"No vertex {kv.Key} in this shape."; return null; }

            GxDisplayList built = new GxDisplayList();
            int cx = 0, cy = 0, cz = 0;
            int index = 0;
            int at2 = 0;

            while (at2 < dl.Length)
            {
                byte[] ops = new byte[4];
                for (int k = 0; k < 4; k++) ops[k] = at2 + k < dl.Length ? dl[at2 + k] : (byte)0;
                at2 += 4;

                for (int k = 0; k < 4; k++)
                {
                    byte op = ops[k];
                    int words = GxDisplayList.TryParamWords(op);
                    if (words < 0) { whynot = $"Unknown GX command 0x{op:X2}."; return null; }
                    if (words > 0 && at2 + words * 4 > dl.Length) return built.ToBytes();

                    if (op >= 0x23 && op <= 0x28)
                    {
                        if (!want.TryGetValue(index, out (int x, int y, int z) to))
                        {
                            Copy(built, dl, op, at2, words);
                            at2 += words * 4;
                            continue;
                        }

                        (int x, int y, int z) asWas = WouldPut(dl, op, at2, cx, cy, cz);
                        if (asWas == to)
                        {
                            Copy(built, dl, op, at2, words);
                        }
                        else
                        {
                            if (!Fits(to.x) || !Fits(to.y) || !Fits(to.z))
                            {
                                whynot = $"Vertex {index} is out of the 4.12 fixed point range.";
                                return null;
                            }

                            built.Command(0x23,
                                (uint)((to.x & 0xFFFF) | ((to.y & 0xFFFF) << 16)),
                                (uint)(to.z & 0xFFFF));
                        }

                        cx = to.x; cy = to.y; cz = to.z;
                        index++;
                        at2 += words * 4;
                        continue;
                    }

                    Copy(built, dl, op, at2, words);
                    at2 += words * 4;
                }
            }

            return built.ToBytes();
        }

        private static (int x, int y, int z) WouldPut(byte[] dl, byte op, int at, int cx, int cy, int cz)
        {
            switch (op)
            {
                case 0x23:
                    {
                        int a = Word(dl, at), b = Word(dl, at + 4);
                        return (Signed(a & 0xffff, 16), Signed((a >> 16) & 0xffff, 16), Signed(b & 0xffff, 16));
                    }
                case 0x24:
                    {
                        int p = Word(dl, at);
                        return (Signed(p & 0x3ff, 10) * 64,
                                Signed((p >> 10) & 0x3ff, 10) * 64,
                                Signed((p >> 20) & 0x3ff, 10) * 64);
                    }
                case 0x25:
                    {
                        int p = Word(dl, at);
                        return (Signed(p & 0xffff, 16), Signed((p >> 16) & 0xffff, 16), cz);
                    }
                case 0x26:
                    {
                        int p = Word(dl, at);
                        return (Signed(p & 0xffff, 16), cy, Signed((p >> 16) & 0xffff, 16));
                    }
                case 0x27:
                    {
                        int p = Word(dl, at);
                        return (cx, Signed(p & 0xffff, 16), Signed((p >> 16) & 0xffff, 16));
                    }
                default:
                    {
                        int p = Word(dl, at);
                        return (cx + Signed(p & 0x3ff, 10),
                                cy + Signed((p >> 10) & 0x3ff, 10),
                                cz + Signed((p >> 20) & 0x3ff, 10));
                    }
            }
        }

        private static void Copy(GxDisplayList to, byte[] dl, byte op, int at, int words)
        {
            uint[] ps = new uint[words];
            for (int i = 0; i < words; i++) ps[i] = (uint)Word(dl, at + i * 4);
            to.Command(op, ps);
        }

        private static bool Fits(int v) => v >= Nearest && v <= Furthest;

        private static int Round(float v) => (int)Math.Round(v * One);
        private static int Word(byte[] b, int at) => BitConverter.ToInt32(b, at);

        private static int Signed(int value, int bits)
        {
            int sign = 1 << (bits - 1);
            return (value & sign) != 0 ? value - (1 << bits) : value;
        }
    }
}
