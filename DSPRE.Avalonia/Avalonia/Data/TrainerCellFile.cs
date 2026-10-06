using System;
using System.Collections.Generic;
using System.IO;

namespace DSPRE.Avalonia.Data
{
    /// <summary>A Platinum or HeartGold trainer cell file taken apart so cells can be added and removed.</summary>
    public sealed class TrainerCellFile
    {
        public sealed class Cell
        {
            public ushort Attr;
            public short MaxX, MaxY, MinX, MinY;

            public List<ushort[]> Pieces = new();

            // A blank frame has size 0.
            public uint TransferOffset, TransferSize;
            public uint UserValue;

            public bool IsBlank => Pieces.Count == 0 || TransferSize == 0;

            public Cell Clone()
            {
                Cell c = (Cell)MemberwiseClone();
                c.Pieces = new List<ushort[]>();
                foreach (ushort[] p in Pieces) c.Pieces.Add((ushort[])p.Clone());
                return c;
            }
        }

        public List<Cell> Cells { get; } = new();

        public uint Mapping { get; private set; }
        public int CharUnit => 32 << (int)Mapping;

        public uint MaxTransferSize { get; set; }

        private ushort _version;
        private uint _stringBank;
        private ushort _userAttr;
        private byte[] _tail;
        private int _tailSections;

        public static TrainerCellFile Read(byte[] d)
        {
            try { return Parse(d); }
            catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException or EndOfStreamException) { return null; }
        }

        private static TrainerCellFile Parse(byte[] d)
        {
            if (d == null || d.Length < 0x30 || d[0] != 'R' || d[1] != 'E' || d[2] != 'C' || d[3] != 'N') return null;
            int headerSize = U16(d, 0xC), sections = U16(d, 0xE);
            int k = headerSize;
            if (Tag(d, k) != "KBEC") return null;
            int kbecSize = (int)U32(d, k + 4), body = k + 8;
            if (k + kbecSize > d.Length) return null;

            int count = U16(d, body);
            if (U16(d, body + 2) != 1) return null;
            int cellTable = body + (int)U32(d, body + 4);
            TrainerCellFile f = new TrainerCellFile
            {
                _version = U16(d, 6),
                Mapping = U32(d, body + 8),
                _stringBank = U32(d, body + 16),
            };
            uint vram = U32(d, body + 12), ext = U32(d, body + 20);
            if (vram == 0 || ext == 0) return null;

            int pieceArea = cellTable + count * 16;
            int expectAt = 0;
            for (int i = 0; i < count; i++)
            {
                int at = cellTable + i * 16;
                Cell c = new Cell
                {
                    Attr = U16(d, at + 2),
                    MaxX = (short)U16(d, at + 8), MaxY = (short)U16(d, at + 10),
                    MinX = (short)U16(d, at + 12), MinY = (short)U16(d, at + 14),
                };
                int n = U16(d, at);
                int from = (int)U32(d, at + 4);
                // The writer lays pieces out in cell order.
                if (from != expectAt) return null;
                for (int p = 0; p < n; p++)
                {
                    int o = pieceArea + from + p * 6;
                    c.Pieces.Add(new[] { U16(d, o), U16(d, o + 2), U16(d, o + 4) });
                }
                expectAt += n * 6;
                f.Cells.Add(c);
            }

            int v = body + (int)vram;
            f.MaxTransferSize = U32(d, v);
            int array = v + (int)U32(d, v + 4);
            for (int i = 0; i < count; i++)
            {
                f.Cells[i].TransferOffset = U32(d, array + i * 8);
                f.Cells[i].TransferSize = U32(d, array + i * 8 + 4);
            }
            if (Pad4(pieceArea + expectAt) != v) return null;
            for (int i = pieceArea + expectAt; i < v; i++) if (d[i] != 0) return null;
            if (U32(d, v + 4) != 8) return null;

            int u = body + (int)ext;
            if (Tag(d, u) != "TACU" || U16(d, u + 8) != count || U32(d, u + 12) != 8) return null;
            f._userAttr = U16(d, u + 10);
            if (f._userAttr != 1) return null;
            for (int i = 0; i < count; i++)
            {
                int offset = (int)U32(d, u + 16 + i * 4);
                f.Cells[i].UserValue = U32(d, u + 8 + offset);
            }
            if (u + (int)U32(d, u + 4) != k + kbecSize) return null;

            f._tailSections = sections - 1;
            f._tail = d.AsSpan(k + kbecSize).ToArray();

            byte[] check = f.Write();
            return check.AsSpan().SequenceEqual(d) ? f : null;
        }

        public byte[] Write()
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter w = new BinaryWriter(ms);
            w.Write("RECN"u8); w.Write((ushort)0xFEFF); w.Write(_version); w.Write(0u);
            w.Write((ushort)0x10); w.Write((ushort)(_tailSections + 1));

            long kbec = ms.Position;
            w.Write("KBEC"u8); w.Write(0u);
            long body = ms.Position;
            w.Write((ushort)Cells.Count); w.Write((ushort)1); w.Write(0x18u); w.Write(Mapping);
            w.Write(0u); w.Write(_stringBank); w.Write(0u);

            int pieceAt = 0;
            foreach (Cell c in Cells)
            {
                w.Write((ushort)c.Pieces.Count); w.Write(c.Attr); w.Write((uint)pieceAt);
                w.Write(c.MaxX); w.Write(c.MaxY); w.Write(c.MinX); w.Write(c.MinY);
                pieceAt += c.Pieces.Count * 6;
            }
            foreach (Cell c in Cells)
                foreach (ushort[] p in c.Pieces) { w.Write(p[0]); w.Write(p[1]); w.Write(p[2]); }
            while (ms.Position % 4 != 0) w.Write((byte)0);

            long vram = ms.Position;
            w.Write(MaxTransferSize); w.Write(8u);
            foreach (Cell c in Cells) { w.Write(c.TransferOffset); w.Write(c.TransferSize); }

            long ext = ms.Position;
            int userSize = 16 + Cells.Count * 8;
            w.Write("TACU"u8); w.Write((uint)userSize);
            w.Write((ushort)Cells.Count); w.Write(_userAttr); w.Write(8u);
            for (int i = 0; i < Cells.Count; i++) w.Write((uint)(8 + Cells.Count * 4 + i * 4));
            foreach (Cell c in Cells) w.Write(c.UserValue);

            long end = ms.Position;
            w.Write(_tail);
            w.Flush();

            byte[] d = ms.ToArray();
            Put32(d, 8, (uint)d.Length);
            Put32(d, (int)kbec + 4, (uint)(end - kbec));
            Put32(d, (int)body + 12, (uint)(vram - body));
            Put32(d, (int)body + 20, (uint)(ext - body));
            return d;
        }

        // ── piece geometry ──────────────────────────────────────────────────────

        private static readonly (int W, int H)[,] Sizes =
        {
            { (8, 8), (16, 16), (32, 32), (64, 64) },
            { (16, 8), (32, 8), (32, 16), (64, 32) },
            { (8, 16), (8, 32), (16, 32), (32, 64) },
            { (8, 8), (8, 8), (8, 8), (8, 8) },
        };

        public readonly record struct Piece(int X, int Y, int Width, int Height, bool FlipX, bool FlipY, int Char, int Palette, bool Colour256);

        public static Piece Describe(ushort[] p)
        {
            int y = p[0] & 0xFF; if (y > 127) y -= 256;
            int x = p[1] & 0x1FF; if (x > 255) x -= 512;
            (int w, int h) = Sizes[(p[0] >> 14) & 3, (p[1] >> 14) & 3];
            return new Piece(x, y, w, h, ((p[1] >> 12) & 1) != 0, ((p[1] >> 13) & 1) != 0, p[2] & 0x3FF, p[2] >> 12, ((p[0] >> 13) & 1) != 0);
        }

        public string Shift(int cell, int dx, int dy)
        {
            Cell c = Cells[cell];
            foreach (ushort[] p in c.Pieces)
            {
                Piece d = Describe(p);
                int x = d.X + dx, y = d.Y + dy;
                if (x < -256 || x > 255 || y < -128 || y > 127) return $"Frame {cell} cannot move that far.";
            }
            foreach (ushort[] p in c.Pieces)
            {
                Piece d = Describe(p);
                p[0] = (ushort)((p[0] & ~0xFF) | ((d.Y + dy) & 0xFF));
                p[1] = (ushort)((p[1] & ~0x1FF) | ((d.X + dx) & 0x1FF));
            }
            FitBounds(cell);
            return null;
        }

        // The bounding radius is in four-pixel units, rounded up, as retail cells store it.
        public void FitBounds(int cell)
        {
            Cell c = Cells[cell];
            if (c.Pieces.Count == 0) { c.MinX = c.MinY = c.MaxX = c.MaxY = 0; c.Attr = 0x800; return; }
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (ushort[] p in c.Pieces)
            {
                Piece d = Describe(p);
                minX = Math.Min(minX, d.X); minY = Math.Min(minY, d.Y);
                maxX = Math.Max(maxX, d.X + d.Width - 1); maxY = Math.Max(maxY, d.Y + d.Height - 1);
            }
            c.MinX = (short)minX; c.MinY = (short)minY; c.MaxX = (short)maxX; c.MaxY = (short)maxY;
            double rx = Math.Max(-minX, maxX + 1), ry = Math.Max(-minY, maxY + 1);
            int radius = ((int)Math.Sqrt(rx * rx + ry * ry) + 3) >> 2;
            c.Attr = (ushort)((c.Attr & ~0x3F) | 0x800 | Math.Min(radius, 0x3F));
        }

        // ── bytes ──────────────────────────────────────────────────────────────

        private static int Pad4(int v) => (v + 3) & ~3;
        private static string Tag(byte[] d, int at) => at + 4 <= d.Length ? System.Text.Encoding.ASCII.GetString(d, at, 4) : "";
        private static ushort U16(byte[] d, int at) => BitConverter.ToUInt16(d, at);
        private static uint U32(byte[] d, int at) => BitConverter.ToUInt32(d, at);
        private static void Put32(byte[] d, int at, uint v) => BitConverter.TryWriteBytes(d.AsSpan(at, 4), v);
    }
}
