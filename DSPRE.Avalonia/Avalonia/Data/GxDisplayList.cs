using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Builds a display list: the run of graphics-engine commands a DS model's shape is actually made
    /// of. Commands are packed four to a word, each followed by its parameters, which is how the
    /// hardware reads them and how every model in the games is written.
    ///
    /// The command numbers and how many words each takes were checked by reading all 340 of
    /// HeartGold's building models back: every one decodes to exactly the triangle and quad count its
    /// own header states.
    /// </summary>
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

        /// <summary>What a run of corners makes.</summary>
        public enum Shape { Triangles = 0, Quads = 1, TriangleStrip = 2, QuadStrip = 3 }

        private readonly List<byte> _ops = new();
        private readonly List<uint> _params = new();

        private void Add(byte op, params uint[] ps)
        {
            _ops.Add(op);
            foreach (uint p in ps) _params.Add(p);
        }

        public void Begin(Shape what) => Add(BeginVtxs, (uint)what);
        public void End() => Add(EndVtxs);

        /// <summary>Which matrix on the stack this shape is drawn against.</summary>
        public void RestoreMatrix(int stackId) => Add(MtxRestore, (uint)stackId);

        /// <summary>A flat colour, five bits a channel.</summary>
        public void SetColour(int r, int g, int b) =>
            Add(Color, (uint)((r & 31) | ((g & 31) << 5) | ((b & 31) << 10)));

        /// <summary>Which way a corner faces. Each part runs from minus one to just under one.</summary>
        public void SetNormal(float x, float y, float z) =>
            Add(Normal, (uint)((Ten(x) & 0x3FF) | ((Ten(y) & 0x3FF) << 10) | ((Ten(z) & 0x3FF) << 20)));

        /// <summary>Where a corner lands on its picture, in whole pixels of it.</summary>
        public void SetTexCoord(float u, float v, int width, int height) =>
            Add(TexCoord, (uint)((Sixteenths(u * width) & 0xFFFF)
                               | ((Sixteenths(v * height) & 0xFFFF) << 16)));

        /// <summary>Where a corner sits, at the finest the hardware keeps.</summary>
        public void AddVertex(float x, float y, float z) =>
            Add(Vtx16, (uint)((Fixed(x) & 0xFFFF) | ((Fixed(y) & 0xFFFF) << 16)), (uint)(Fixed(z) & 0xFFFF));

        /// <summary>The finished list, padded so it ends on a whole word.</summary>
        public byte[] ToBytes()
        {
            var o = new MemoryStream();
            int at = 0, taken = 0;
            var ops = new List<byte>(_ops);

            // End on a word of do-nothing commands, which is how the games end theirs. A list that
            // stops flush on its last parameter leaves a reader nothing after it, and readers that
            // stop when the parameters run out then drop whatever came last.
            while (ops.Count % 4 != 0) ops.Add(Nop);
            for (int i = 0; i < 4; i++) ops.Add(Nop);

            // Commands go four to a word with their parameters after, so the last word is filled out
            // with the do-nothing command rather than left short.
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

        /// <summary>Whether this list ever says which way a corner faces, what colour it is, or where
        /// it lands on a picture. The shape has to declare which of those it uses.</summary>
        public int Flags()
        {
            int f = 0;
            if (_ops.Contains(Normal)) f |= 1;
            if (_ops.Contains(Color)) f |= 2;
            if (_ops.Contains(TexCoord)) f |= 4;
            if (_ops.Contains(MtxRestore)) f |= 8;
            return f;
        }

        public static int ParamWords(byte op) => op switch
        {
            Nop => 0,
            0x10 => 1, 0x11 => 0, 0x12 => 1, 0x13 => 1, 0x14 => 1, 0x15 => 0, 0x1b => 3,
            0x20 => 1, 0x21 => 1, 0x22 => 1, 0x23 => 2, 0x24 => 1, 0x25 => 1, 0x26 => 1,
            0x27 => 1, 0x28 => 1, 0x29 => 1, 0x2a => 1, 0x2b => 1,
            0x30 => 1, 0x31 => 1, 0x32 => 1, 0x33 => 1, 0x34 => 32,
            0x40 => 1, 0x41 => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(op), $"no such graphics command 0x{op:X2}"),
        };

        // ── the number formats the hardware uses ──────────────────────────────────────────────────

        /// <summary>Twelve bits after the point, which is how the hardware keeps a distance.</summary>
        public static int Fixed(float v) => (int)Math.Round(Math.Clamp(v, -8f, 8f - 1f / 4096f) * 4096f);

        /// <summary>Nine bits after the point, for a direction.</summary>
        public static int Ten(float v) => (int)Math.Round(Math.Clamp(v, -1f, 0.998f) * 512f);

        /// <summary>Four bits after the point, for a place on a picture.</summary>
        public static int Sixteenths(float v) => (int)Math.Round(Math.Clamp(v, -2048f, 2047.9f) * 16f);
    }
}
