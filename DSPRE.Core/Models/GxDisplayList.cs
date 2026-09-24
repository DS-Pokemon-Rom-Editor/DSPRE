using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.Models
{
    /// <summary>Builds a GX display list.</summary>
    public sealed class GxDisplayList
    {
        public const byte Nop = 0x00;
        public const byte MtxRestore = 0x14;
        public const byte Color = 0x20;
        public const byte Normal = 0x21;
        public const byte TexCoord = 0x22;
        public const byte Vtx16 = 0x23;
        public const byte BeginVtxs = 0x40;
        public const byte EndVtxs = 0x41;

        public enum Shape { Triangles = 0, Quads = 1, TriangleStrip = 2, QuadStrip = 3 }

        private readonly List<byte> _ops = new();
        private readonly List<uint> _params = new();

        private void Add(byte op, params uint[] ps)
        {
            _ops.Add(op);
            foreach (uint p in ps) _params.Add(p);
        }

        public void Command(byte op, params uint[] ps) => Add(op, ps);

        public void Begin(Shape what) { Add(BeginVtxs, (uint)what); _last = null; }

        private (int x, int y, int z)? _last;

        public void AddVertexRaw(int x, int y, int z)
        {
            x = Math.Clamp(x, short.MinValue, short.MaxValue);
            y = Math.Clamp(y, short.MinValue, short.MaxValue);
            z = Math.Clamp(z, short.MinValue, short.MaxValue);
            uint Two(int a, int b) => (uint)((a & 0xFFFF) | ((b & 0xFFFF) << 16));
            static bool Ten(int v) => v >= -512 && v <= 511;

            if (_last is (int lx, int ly, int lz))
            {
                if (z == lz) { Add(0x25, Two(x, y)); _last = (x, y, z); return; }
                if (y == ly) { Add(0x26, Two(x, z)); _last = (x, y, z); return; }
                if (x == lx) { Add(0x27, Two(y, z)); _last = (x, y, z); return; }
                int dx = x - lx, dy = y - ly, dz = z - lz;
                if (Ten(dx) && Ten(dy) && Ten(dz))
                {
                    Add(0x28, (uint)((dx & 0x3FF) | ((dy & 0x3FF) << 10) | ((dz & 0x3FF) << 20)));
                    _last = (x, y, z);
                    return;
                }
            }
            if (x % 64 == 0 && y % 64 == 0 && z % 64 == 0 && Ten(x / 64) && Ten(y / 64) && Ten(z / 64))
                Add(0x24, (uint)(((x / 64) & 0x3FF) | (((y / 64) & 0x3FF) << 10) | (((z / 64) & 0x3FF) << 20)));
            else
                Add(Vtx16, Two(x, y), (uint)(z & 0xFFFF));
            _last = (x, y, z);
        }
        public void End() => Add(EndVtxs);

        public void RestoreMatrix(int stackId) => Add(MtxRestore, (uint)stackId);

        public void SetColour(int r, int g, int b) =>
            Add(Color, (uint)((r & 31) | ((g & 31) << 5) | ((b & 31) << 10)));

        public void SetNormal(float x, float y, float z) =>
            Add(Normal, (uint)((Ten(x) & 0x3FF) | ((Ten(y) & 0x3FF) << 10) | ((Ten(z) & 0x3FF) << 20)));

        public void SetTexCoord(float u, float v, int width, int height) =>
            Add(TexCoord, (uint)((Sixteenths(u * width) & 0xFFFF)
                               | ((Sixteenths(v * height) & 0xFFFF) << 16)));

        public void AddVertex(float x, float y, float z) =>
            Add(Vtx16, (uint)((Fixed(x) & 0xFFFF) | ((Fixed(y) & 0xFFFF) << 16)), (uint)(Fixed(z) & 0xFFFF));

        public byte[] ToBytes()
        {
            var o = new MemoryStream();
            int at = 0, taken = 0;
            var ops = new List<byte>(_ops);

            while (ops.Count % 4 != 0) ops.Add(Nop);
            for (int i = 0; i < 4; i++) ops.Add(Nop);

            while (at < ops.Count)
            {
                var four = new byte[4];
                int n = Math.Min(4, ops.Count - at);
                for (int i = 0; i < n; i++) four[i] = ops[at + i];
                o.Write(four, 0, 4);

                int wanted = 0;
                for (int i = 0; i < n; i++) wanted += ParamWords(ops[at + i]);
                for (int i = 0; i < wanted; i++)
                {
                    uint p = _params[taken++];
                    o.WriteByte((byte)p); o.WriteByte((byte)(p >> 8));
                    o.WriteByte((byte)(p >> 16)); o.WriteByte((byte)(p >> 24));
                }
                at += n;
            }
            return o.ToArray();
        }

        public int Flags()
        {
            int f = 0;
            if (_ops.Contains(Normal)) f |= 1;
            if (_ops.Contains(Color)) f |= 2;
            if (_ops.Contains(TexCoord)) f |= 4;
            if (_ops.Contains(MtxRestore)) f |= 8;
            return f;
        }

        public static int ParamWords(byte op)
        {
            int words = TryParamWords(op);
            if (words < 0) throw new ArgumentOutOfRangeException(nameof(op), $"no such graphics command 0x{op:X2}");
            return words;
        }

        public static int TryParamWords(byte op) => op switch
        {
            Nop => 0,
            0x10 => 1, 0x11 => 0, 0x12 => 1, 0x13 => 1, 0x14 => 1, 0x15 => 0,
            0x16 => 16, 0x17 => 12, 0x18 => 16, 0x19 => 12, 0x1a => 9, 0x1b => 3, 0x1c => 3,
            0x20 => 1, 0x21 => 1, 0x22 => 1, 0x23 => 2, 0x24 => 1, 0x25 => 1, 0x26 => 1,
            0x27 => 1, 0x28 => 1, 0x29 => 1, 0x2a => 1, 0x2b => 1,
            0x30 => 1, 0x31 => 1, 0x32 => 1, 0x33 => 1, 0x34 => 32,
            0x40 => 1, 0x41 => 0,
            0x50 => 1, 0x60 => 1, 0x70 => 3, 0x71 => 2, 0x72 => 1,
            _ => -1,
        };

        public static int Fixed(float v) => (int)Math.Round(Math.Clamp(v, -8f, 8f - 1f / 4096f) * 4096f);

        public static int Ten(float v) => (int)Math.Round(Math.Clamp(v, -1f, 0.998f) * 512f);

        public static int Sixteenths(float v) => (int)Math.Round(Math.Clamp(v, -2048f, 2047.9f) * 16f);
    }
}
