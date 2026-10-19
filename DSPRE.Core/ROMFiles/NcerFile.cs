using System;
using System.Collections.Generic;
using System.Text;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The one reader of cell files (NCER), kept as the bytes it came in as. Moving a piece patches the two
    /// halfwords that hold its position and nothing else, so a file that is opened and saved without an edit
    /// comes back identical.
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
            /// <summary>Square, wide or tall; 3 is not a shape the hardware draws.</summary>
            public int Shape;
            public bool Colour256;
            /// <summary>The three halfwords as stored.</summary>
            public ushort A0, A1, A2;

            public string Where => $"{Width}x{Height} at {X}, {Y}";
        }

        public sealed class Cell
        {
            public int Number;
            public string Name = "";
            public List<Piece> Pieces = new();
            public ushort Attr;
            /// <summary>The cell's bounds, which only the extended kind stores.</summary>
            public short MaxX, MaxY, MinX, MinY;
            /// <summary>Where this cell's pieces start, counted from the end of the cell table.</summary>
            public uint PieceStart;
            /// <summary>This cell's slice of the drawing, in bytes, when the file keeps transfer partitions.</summary>
            public uint TransferOffset, TransferSize;
            public uint UserValue;
        }

        private byte[] _bytes;

        public List<Cell> Cells { get; } = new();

        /// <summary>Cells of the extended kind carry their own bounds, which are left as they were found.</summary>
        public bool Extended { get; private set; }

        public ushort Version { get; private set; }
        public int SectionCount { get; private set; }
        public int KbecAt { get; private set; }
        public int KbecSize { get; private set; }

        /// <summary>The sprite mapping as stored: a piece's tile number counts in units of 32 bytes shifted by it.</summary>
        public uint Mapping { get; private set; }

        /// <summary>Offsets from the cell bank's body to the label bank, the transfer partitions and the user data; 0 when absent.</summary>
        public uint StringBank { get; private set; }
        public uint PartitionsAt { get; private set; }
        public uint UserAt { get; private set; }

        public uint MaxTransferSize { get; private set; }
        public uint PartitionListOffset { get; private set; }

        /// <summary>The user data section (TACU) as stored: its attribute and entry count; values are on the cells.</summary>
        public ushort UserAttr { get; private set; }
        public int UserCount { get; private set; }
        public uint UserTableOffset { get; private set; }

        public int PieceCount
        {
            get { int n = 0; foreach (Cell c in Cells) n += c.Pieces.Count; return n; }
        }

        private static readonly (int W, int H)[,] Sizes =
        {
            { (8, 8), (16, 16), (32, 32), (64, 64) },
            { (16, 8), (32, 8), (32, 16), (64, 32) },
            { (8, 16), (8, 32), (16, 32), (32, 64) },
            { (8, 8), (8, 8), (8, 8), (8, 8) },
        };

        /// <summary>One piece's fields from its three halfwords.</summary>
        public static Piece Decode(int a0, int a1, int a2, int at = -1)
        {
            int y = a0 & 0xFF; if (y > 127) y -= 256;
            int x = a1 & 0x1FF; if (x > 255) x -= 512;
            int shape = (a0 >> 14) & 3;
            (int w, int h) = Sizes[shape, (a1 >> 14) & 3];
            return new Piece
            {
                At = at, X = x, Y = y, Width = w, Height = h, Shape = shape,
                FlipH = ((a1 >> 12) & 1) != 0, FlipV = ((a1 >> 13) & 1) != 0, Colour256 = ((a0 >> 13) & 1) != 0,
                Tile = a2 & 0x3FF, Palette = a2 >> 12,
                A0 = (ushort)a0, A1 = (ushort)a1, A2 = (ushort)a2,
            };
        }

        /// <summary>
        /// Where a piece's pixels start in the drawing, in 32-byte characters: its tile number counts in units of
        /// the sprite mapping's boundary, and a file with transfer partitions counts from its cell's own slice.
        /// Trainer layouts record mapping 1, so their numbers step in twos.
        /// </summary>
        public int CharacterOf(Cell cell, Piece piece)
            => (piece.Tile << Math.Clamp((int)(Mapping & 0xFF), 0, 3)) + (int)(cell.TransferOffset / 32);

        /// <summary>Cells built in memory, for a layout kept as source rather than as a file. Not for writing.</summary>
        public static NcerFile FromCells(uint mapping, IEnumerable<Cell> cells)
        {
            NcerFile f = new NcerFile { Mapping = mapping };
            f.Cells.AddRange(cells);
            return f;
        }

        /// <summary>Reads a cell file, or null when the bytes are not one.</summary>
        public static NcerFile Read(byte[] d)
        {
            if (d == null || d.Length < 0x20) return null;
            if (d[0] != 'R' || d[1] != 'E' || d[2] != 'C' || d[3] != 'N') return null;

            int kbec = NitroBgCodec.Find(d, "KBEC", 0);
            if (kbec < 0 || kbec + 16 > d.Length) return null;

            NcerFile f = new NcerFile
            {
                _bytes = (byte[])d.Clone(),
                Version = (ushort)NitroBgCodec.U16(d, 6),
                SectionCount = NitroBgCodec.U16(d, 0x0E),
                KbecAt = kbec,
                KbecSize = NitroBgCodec.U32(d, kbec + 4),
            };
            int body = kbec + 8;
            int count = NitroBgCodec.U16(d, body);
            int type = NitroBgCodec.U16(d, body + 2);
            f.Extended = type == 1;
            if (body + 24 <= d.Length)
            {
                f.Mapping = (uint)NitroBgCodec.U32(d, body + 8);
                f.PartitionsAt = (uint)NitroBgCodec.U32(d, body + 12);
                f.StringBank = (uint)NitroBgCodec.U32(d, body + 16);
                f.UserAt = (uint)NitroBgCodec.U32(d, body + 20);
            }

            int entry = type == 1 ? 16 : 8;
            int table = body + NitroBgCodec.U32(d, body + 4);
            if (table < 0 || table + count * entry > d.Length) return null;
            int area = table + count * entry;

            for (int i = 0; i < count; i++)
            {
                int at = table + i * entry;
                Cell cell = new Cell
                {
                    Number = i,
                    Attr = (ushort)NitroBgCodec.U16(d, at + 2),
                    PieceStart = (uint)NitroBgCodec.U32(d, at + 4),
                };
                if (f.Extended)
                {
                    cell.MaxX = (short)NitroBgCodec.U16(d, at + 8); cell.MaxY = (short)NitroBgCodec.U16(d, at + 10);
                    cell.MinX = (short)NitroBgCodec.U16(d, at + 12); cell.MinY = (short)NitroBgCodec.U16(d, at + 14);
                }
                int n = NitroBgCodec.U16(d, at);
                int from = area + (int)cell.PieceStart;

                for (int k = 0; k < n; k++)
                {
                    int o = from + k * 6;
                    if (o < 0 || o + 6 > d.Length) break;
                    cell.Pieces.Add(Decode(NitroBgCodec.U16(d, o), NitroBgCodec.U16(d, o + 2), NitroBgCodec.U16(d, o + 4), o));
                }
                f.Cells.Add(cell);
            }

            f.ReadPartitions(body);
            f.ReadUserData(body);
            f.ReadNames(kbec);
            return f;
        }

        // Where each cell's slice of the drawing begins and how long it is. The Pokétch and the battle objects
        // keep no partitions, so their cells all count from the start.
        private void ReadPartitions(int body)
        {
            if (PartitionsAt == 0) return;
            int head = body + (int)PartitionsAt;
            if (head + 8 > _bytes.Length) return;
            MaxTransferSize = (uint)NitroBgCodec.U32(_bytes, head);
            PartitionListOffset = (uint)NitroBgCodec.U32(_bytes, head + 4);
            int list = head + (int)PartitionListOffset;
            for (int i = 0; i < Cells.Count; i++)
            {
                int at = list + i * 8;
                if (at + 8 > _bytes.Length) break;
                Cells[i].TransferOffset = (uint)NitroBgCodec.U32(_bytes, at);
                Cells[i].TransferSize = (uint)NitroBgCodec.U32(_bytes, at + 4);
            }
        }

        // The user data some cell files carry, one value a cell after a table of offsets to them.
        private void ReadUserData(int body)
        {
            if (UserAt == 0) return;
            int u = body + (int)UserAt;
            if (u + 16 > _bytes.Length || _bytes[u] != 'T' || _bytes[u + 1] != 'A' || _bytes[u + 2] != 'C' || _bytes[u + 3] != 'U') return;
            UserCount = NitroBgCodec.U16(_bytes, u + 8);
            UserAttr = (ushort)NitroBgCodec.U16(_bytes, u + 10);
            UserTableOffset = (uint)NitroBgCodec.U32(_bytes, u + 12);
            for (int i = 0; i < Cells.Count && i < UserCount; i++)
            {
                int p = u + 16 + i * 4;
                if (p + 4 > _bytes.Length) break;
                int value = u + 8 + NitroBgCodec.U32(_bytes, p);
                if (value < 0 || value + 4 > _bytes.Length) break;
                Cells[i].UserValue = (uint)NitroBgCodec.U32(_bytes, value);
            }
        }

        // Cells are reached by number in the games' own code, so a name is only ever shown, never relied on.
        private void ReadNames(int kbec)
        {
            int at = kbec + NitroBgCodec.U32(_bytes, kbec + 4);
            if (at < 0 || at + 8 > _bytes.Length) return;
            if (_bytes[at] != 'L' || _bytes[at + 1] != 'B' || _bytes[at + 2] != 'A' || _bytes[at + 3] != 'L') return;

            long size = (uint)NitroBgCodec.U32(_bytes, at + 4);
            int list = at + 8;
            List<int> starts = new List<int>();
            for (int i = 0; i < Cells.Count; i++)
            {
                int p = list + i * 4;
                if (p + 4 > _bytes.Length) break;
                long offset = (uint)NitroBgCodec.U32(_bytes, p);
                if (offset >= size - 8) break;
                starts.Add((int)offset);
            }

            int text = list + starts.Count * 4;
            for (int i = 0; i < starts.Count && i < Cells.Count; i++)
            {
                int p = text + starts[i];
                StringBuilder sb = new StringBuilder();
                while (p < _bytes.Length && _bytes[p] != 0) sb.Append((char)_bytes[p++]);
                Cells[i].Name = sb.ToString();
            }
        }

        public byte[] Write() => (byte[])_bytes.Clone();

        public Piece PieceAt(int cell, int piece)
        {
            if (cell < 0 || cell >= Cells.Count) return null;
            List<Piece> pieces = Cells[cell].Pieces;
            return piece < 0 || piece >= pieces.Count ? null : pieces[piece];
        }

        /// <summary>
        /// Moves a piece within its cell. Returns why it could not be moved, or null when it was. The fields
        /// are nine bits across and eight down, so a position outside that is refused rather than wrapped.
        /// </summary>
        public string Move(int cell, int piece, int x, int y)
        {
            Piece p = PieceAt(cell, piece);
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

            p.X = x; p.Y = y; p.A0 = (ushort)a0; p.A1 = (ushort)a1;
            return null;
        }
    }
}
