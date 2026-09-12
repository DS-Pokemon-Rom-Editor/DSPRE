using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// A cell file, kept as the bytes it came in as. Moving a piece patches the two halfwords that hold its
    /// position and nothing else, so a file that is opened and saved without an edit comes back identical.
    ///
    /// The reader in Images sorts pieces by priority when it loads and by number when it saves, which
    /// reorders them, and it writes without truncating. Neither is survivable for files the games index by
    /// position, hence this one.
    /// </summary>
    public sealed class NcerFile
    {
        /// <summary>How far a piece can sit from its cell's origin before the field runs out of bits.</summary>
        public const int MinX = -256, MaxX = 255, MinY = -128, MaxY = 127;

        public sealed class Piece
        {
            /// <summary>Where this piece's six bytes start, which is what an edit patches.</summary>
            public int At;
            public int X, Y, Width, Height, Tile, Palette;
            public bool FlipH, FlipV;

            public string Where => $"{Width}x{Height} at {X}, {Y}";
        }

        public sealed class Cell
        {
            public int Number;
            public string Name = "";
            public List<Piece> Pieces = new();
        }

        private byte[] _bytes;

        public List<Cell> Cells { get; } = new();

        /// <summary>Cells of the extended kind carry their own bounds, which are left as they were found.</summary>
        public bool Extended { get; private set; }

        public int PieceCount
        {
            get { int n = 0; foreach (var c in Cells) n += c.Pieces.Count; return n; }
        }

        private static readonly (int W, int H)[,] Sizes =
        {
            { (8, 8), (16, 16), (32, 32), (64, 64) },
            { (16, 8), (32, 8), (32, 16), (64, 32) },
            { (8, 16), (8, 32), (16, 32), (32, 64) },
            { (8, 8), (8, 8), (8, 8), (8, 8) },
        };

        /// <summary>Reads a cell file, or null when the bytes are not one.</summary>
        public static NcerFile Read(byte[] d)
        {
            if (d == null || d.Length < 0x20) return null;
            if (d[0] != 'R' || d[1] != 'E' || d[2] != 'C' || d[3] != 'N') return null;

            int kbec = NitroBgCodec.Find(d, "KBEC", 0);
            if (kbec < 0 || kbec + 16 > d.Length) return null;

            var f = new NcerFile { _bytes = (byte[])d.Clone() };
            int count = NitroBgCodec.U16(d, kbec + 8);
            int type = NitroBgCodec.U16(d, kbec + 10);
            f.Extended = type == 1;

            int entry = type == 1 ? 16 : 8;
            int table = kbec + 8 + (int)NitroBgCodec.U32(d, kbec + 12);
            if (table < 0 || table + count * entry > d.Length) return null;
            int area = table + count * entry;

            for (int i = 0; i < count; i++)
            {
                int at = table + i * entry;
                var cell = new Cell { Number = i };
                int n = NitroBgCodec.U16(d, at);
                int from = area + (int)NitroBgCodec.U32(d, at + 4);

                for (int k = 0; k < n; k++)
                {
                    int o = from + k * 6;
                    if (o < 0 || o + 6 > d.Length) break;
                    int a0 = NitroBgCodec.U16(d, o), a1 = NitroBgCodec.U16(d, o + 2), a2 = NitroBgCodec.U16(d, o + 4);
                    int y = a0 & 0xFF; if (y > 127) y -= 256;
                    int x = a1 & 0x1FF; if (x > 255) x -= 512;
                    var (w, h) = Sizes[(a0 >> 14) & 3, (a1 >> 14) & 3];
                    cell.Pieces.Add(new Piece
                    {
                        At = o, X = x, Y = y, Width = w, Height = h,
                        FlipH = ((a1 >> 12) & 1) != 0, FlipV = ((a1 >> 13) & 1) != 0,
                        Tile = a2 & 0x3FF, Palette = a2 >> 12,
                    });
                }
                f.Cells.Add(cell);
            }

            f.ReadNames(kbec);
            return f;
        }

        // Cells are reached by number in the games' own code, so a name is only ever shown, never relied on.
        private void ReadNames(int kbec)
        {
            int at = kbec + (int)NitroBgCodec.U32(_bytes, kbec + 4);
            if (at + 8 > _bytes.Length) return;
            if (_bytes[at] != 'L' || _bytes[at + 1] != 'B' || _bytes[at + 2] != 'A' || _bytes[at + 3] != 'L') return;

            long size = NitroBgCodec.U32(_bytes, at + 4);
            int list = at + 8;
            var starts = new List<int>();
            for (int i = 0; i < Cells.Count; i++)
            {
                int p = list + i * 4;
                if (p + 4 > _bytes.Length) break;
                long offset = NitroBgCodec.U32(_bytes, p);
                if (offset >= size - 8) break;
                starts.Add((int)offset);
            }

            int text = list + starts.Count * 4;
            for (int i = 0; i < starts.Count && i < Cells.Count; i++)
            {
                int p = text + starts[i];
                var sb = new System.Text.StringBuilder();
                while (p < _bytes.Length && _bytes[p] != 0) sb.Append((char)_bytes[p++]);
                Cells[i].Name = sb.ToString();
            }
        }

        public byte[] Write() => (byte[])_bytes.Clone();

        public Piece PieceAt(int cell, int piece)
        {
            if (cell < 0 || cell >= Cells.Count) return null;
            var pieces = Cells[cell].Pieces;
            return piece < 0 || piece >= pieces.Count ? null : pieces[piece];
        }

        /// <summary>
        /// Moves a piece within its cell. Returns why it could not be moved, or null when it was. The fields
        /// are nine bits across and eight down, so a position outside that is refused rather than wrapped.
        /// </summary>
        public string Move(int cell, int piece, int x, int y)
        {
            var p = PieceAt(cell, piece);
            if (p == null) return "There is no such piece.";
            if (x < MinX || x > MaxX) return $"Across has to be between {MinX} and {MaxX}.";
            if (y < MinY || y > MaxY) return $"Down has to be between {MinY} and {MaxY}.";

            int a0 = NitroBgCodec.U16(_bytes, p.At), a1 = NitroBgCodec.U16(_bytes, p.At + 2);
            a0 = (a0 & ~0xFF) | (y & 0xFF);
            a1 = (a1 & ~0x1FF) | (x & 0x1FF);
            _bytes[p.At] = (byte)(a0 & 0xFF);
            _bytes[p.At + 1] = (byte)(a0 >> 8);
            _bytes[p.At + 2] = (byte)(a1 & 0xFF);
            _bytes[p.At + 3] = (byte)(a1 >> 8);

            p.X = x; p.Y = y;
            return null;
        }
    }
}
