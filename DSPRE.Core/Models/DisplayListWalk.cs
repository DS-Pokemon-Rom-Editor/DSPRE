using System;
using System.Collections.Generic;

namespace DSPRE.Models
{
    /// <summary>Walks a shape's display list, recording where each vertex command sits.</summary>
    public sealed class DisplayListWalk
    {
        public enum Put
        {
            Sixteen = 0x23,
            Ten = 0x24,
            Xy = 0x25,
            Xz = 0x26,
            Yz = 0x27,
            Diff = 0x28,
        }

        public struct Corner
        {
            public int Index;

            public int RawX, RawY, RawZ;

            public float X => RawX / 4096f;
            public float Y => RawY / 4096f;
            public float Z => RawZ / 4096f;

            public float S, T;

            public Put How;

            public int PutAt;

            public int TexCoordAt;

            public int Colour;

            public int Normal;

            public bool ColourLast;
        }

        public sealed class Run
        {
            public int Kind;
            public int BeginAt;
            public List<Corner> Corners = new List<Corner>();
        }

        public List<Run> Runs { get; } = new List<Run>();

        public int CornerCount
        {
            get { int n = 0; foreach (var r in Runs) n += r.Corners.Count; return n; }
        }

        public static DisplayListWalk Read(byte[] dl, out string whynot)
        {
            whynot = null;
            var walk = new DisplayListWalk();
            if (dl == null || dl.Length == 0) return walk;

            // Integer state so VTX_DIFF chains don't drift.
            int ix = 0, iy = 0, iz = 0;
            float s = 0, t = 0;
            int texAt = -1;
            int colour = -1, normal = -1;
            bool colourLast = false;
            Run run = null;
            int at = 0;
            int corner = 0;

            while (at < dl.Length)
            {
                var ops = new byte[4];
                for (int k = 0; k < 4; k++) ops[k] = at + k < dl.Length ? dl[at + k] : (byte)0;
                at += 4;

                for (int k = 0; k < 4; k++)
                {
                    byte op = ops[k];
                    int words = GxDisplayList.TryParamWords(op);
                    if (words < 0) { whynot = $"Unknown GX command 0x{op:X2}."; return null; }
                    if (at + words * 4 > dl.Length && words > 0)
                    {
                        return walk;
                    }

                    switch (op)
                    {
                        case 0x40:
                            run = new Run { Kind = Word(dl, at) & 3, BeginAt = at };
                            walk.Runs.Add(run);
                            break;

                        case 0x41:
                            run = null;
                            break;

                        case 0x20:
                            colour = Word(dl, at) & 0x7fff;
                            colourLast = true;
                            break;

                        case 0x21:
                            normal = Word(dl, at) & 0x3fffffff;
                            colourLast = false;
                            break;

                        case 0x22:
                            {
                                int p = Word(dl, at);
                                s = Signed(p & 0xffff, 16) / 16f;
                                t = Signed((p >> 16) & 0xffff, 16) / 16f;
                                texAt = at;
                                break;
                            }

                        case 0x23:
                            {
                                int a = Word(dl, at), b = Word(dl, at + 4);
                                ix = Signed(a & 0xffff, 16);
                                iy = Signed((a >> 16) & 0xffff, 16);
                                iz = Signed(b & 0xffff, 16);
                                Add(run, corner++, Put.Sixteen, at, ix, iy, iz, s, t, texAt, colour, normal, colourLast);
                                break;
                            }

                        case 0x24:
                            {
                                int p = Word(dl, at);
                                ix = Signed(p & 0x3ff, 10) * 64;
                                iy = Signed((p >> 10) & 0x3ff, 10) * 64;
                                iz = Signed((p >> 20) & 0x3ff, 10) * 64;
                                Add(run, corner++, Put.Ten, at, ix, iy, iz, s, t, texAt, colour, normal, colourLast);
                                break;
                            }

                        case 0x25:
                            {
                                int p = Word(dl, at);
                                ix = Signed(p & 0xffff, 16);
                                iy = Signed((p >> 16) & 0xffff, 16);
                                Add(run, corner++, Put.Xy, at, ix, iy, iz, s, t, texAt, colour, normal, colourLast);
                                break;
                            }

                        case 0x26:
                            {
                                int p = Word(dl, at);
                                ix = Signed(p & 0xffff, 16);
                                iz = Signed((p >> 16) & 0xffff, 16);
                                Add(run, corner++, Put.Xz, at, ix, iy, iz, s, t, texAt, colour, normal, colourLast);
                                break;
                            }

                        case 0x27:
                            {
                                int p = Word(dl, at);
                                iy = Signed(p & 0xffff, 16);
                                iz = Signed((p >> 16) & 0xffff, 16);
                                Add(run, corner++, Put.Yz, at, ix, iy, iz, s, t, texAt, colour, normal, colourLast);
                                break;
                            }

                        case 0x28:
                            {
                                int p = Word(dl, at);
                                ix += Signed(p & 0x3ff, 10);
                                iy += Signed((p >> 10) & 0x3ff, 10);
                                iz += Signed((p >> 20) & 0x3ff, 10);
                                Add(run, corner++, Put.Diff, at, ix, iy, iz, s, t, texAt, colour, normal, colourLast);
                                break;
                            }
                    }

                    at += words * 4;
                }
            }

            return walk;
        }

        private static void Add(Run run, int index, Put how, int at,
                                int ix, int iy, int iz, float s, float t, int texAt,
                                int colour, int normal, bool colourLast)
        {
            run?.Corners.Add(new Corner
            {
                Index = index,
                RawX = ix, RawY = iy, RawZ = iz,
                S = s, T = t,
                How = how, PutAt = at, TexCoordAt = texAt,
                Colour = colour, Normal = normal, ColourLast = colourLast,
            });
        }

        public static IEnumerable<(int a, int b, int c)> Triangles(Run run)
        {
            int n = run.Corners.Count;
            switch (run.Kind)
            {
                case 0:
                    for (int i = 0; i + 2 < n; i += 3) yield return (i, i + 1, i + 2);
                    break;
                case 1:
                    for (int i = 0; i + 3 < n; i += 4)
                    {
                        yield return (i, i + 1, i + 2);
                        yield return (i, i + 2, i + 3);
                    }
                    break;
                case 2:
                    for (int i = 0; i + 2 < n; i++)
                        yield return (i & 1) == 0 ? (i, i + 1, i + 2) : (i + 1, i, i + 2);
                    break;
                case 3:
                    for (int i = 0; i + 3 < n; i += 2)
                    {
                        yield return (i, i + 1, i + 3);
                        yield return (i, i + 3, i + 2);
                    }
                    break;
            }
        }

        public static IEnumerable<int[]> Faces(Run run)
        {
            int n = run.Corners.Count;
            switch (run.Kind)
            {
                case 0:
                    for (int i = 0; i + 2 < n; i += 3) yield return new[] { i, i + 1, i + 2 };
                    break;
                case 1:
                    for (int i = 0; i + 3 < n; i += 4) yield return new[] { i, i + 1, i + 2, i + 3 };
                    break;
                case 2:
                    for (int i = 0; i + 2 < n; i++)
                        yield return (i & 1) == 0 ? new[] { i, i + 1, i + 2 } : new[] { i + 1, i, i + 2 };
                    break;
                case 3:
                    for (int i = 0; i + 3 < n; i += 2) yield return new[] { i, i + 1, i + 3, i + 2 };
                    break;
            }
        }

        private static int Word(byte[] b, int at) => BitConverter.ToInt32(b, at);

        private static int Signed(int value, int bits)
        {
            int sign = 1 << (bits - 1);
            return (value & sign) != 0 ? value - (1 << bits) : value;
        }
    }
}
