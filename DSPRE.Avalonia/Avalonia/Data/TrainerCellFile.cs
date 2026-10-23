using System;
using System.Collections.Generic;
using System.IO;
using DSPRE.ROMFiles;

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

        // Everything comes from the one cell reader; the file is only taken when writing it back gives the same bytes, so
        // it's known to have the layout Write makes.
        private static TrainerCellFile Parse(byte[] d)
        {
            if (d == null || d.Length < 0x30) return null;
            NcerFile n = NcerFile.Read(d);
            if (n == null || !n.Extended || n.PartitionsAt == 0 || n.UserAt == 0 || n.UserAttr != 1 || n.UserCount != n.Cells.Count) return null;

            TrainerCellFile f = new TrainerCellFile
            {
                _version = n.Version,
                Mapping = n.Mapping,
                _stringBank = n.StringBank,
                MaxTransferSize = n.MaxTransferSize,
                _userAttr = n.UserAttr,
                _tailSections = n.SectionCount - 1,
            };
            foreach (NcerFile.Cell c in n.Cells)
            {
                Cell cell = new Cell
                {
                    Attr = c.Attr, MaxX = c.MaxX, MaxY = c.MaxY, MinX = c.MinX, MinY = c.MinY,
                    TransferOffset = c.TransferOffset, TransferSize = c.TransferSize, UserValue = c.UserValue,
                };
                foreach (NcerFile.Piece p in c.Pieces) cell.Pieces.Add(new[] { p.A0, p.A1, p.A2 });
                f.Cells.Add(cell);
            }
            int end = n.KbecAt + n.KbecSize;
            if (end < n.KbecAt || end > d.Length) return null;
            f._tail = d.AsSpan(end).ToArray();

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

        public readonly record struct Piece(int X, int Y, int Width, int Height, bool FlipX, bool FlipY, int Char, int Palette, bool Colour256);

        public static Piece Describe(ushort[] p)
        {
            NcerFile.Piece d = NcerFile.Decode(p[0], p[1], p[2]);
            return new Piece(d.X, d.Y, d.Width, d.Height, d.FlipH, d.FlipV, d.Tile, d.Palette, d.Colour256);
        }

        public string Shift(int cell, int dx, int dy)
        {
            Cell c = Cells[cell];
            foreach (ushort[] p in c.Pieces)
            {
                Piece d = Describe(p);
                int x = d.X + dx, y = d.Y + dy;
                if (x < -256 || x > 255 || y < -128 || y > 127) return $"Frame {cell} can't move that far.";
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

        private static void Put32(byte[] d, int at, uint v) => BitConverter.TryWriteBytes(d.AsSpan(at, 4), v);
    }
}
