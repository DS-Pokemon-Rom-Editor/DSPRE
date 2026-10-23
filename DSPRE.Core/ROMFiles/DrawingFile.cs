using System;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// A drawing (NCGR) held for editing: its pixel bytes can be changed in place and the file comes back with only
    /// those bytes different.
    /// </summary>
    public sealed class DrawingFile
    {
        private readonly byte[] _file;
        private readonly NitroBgCodec.DrawingHeader _head;

        /// <summary>The pixel bytes as stored, tile after tile when <see cref="Tiled"/>. Edits go straight in here.</summary>
        public byte[] Tiles { get; private set; }

        public bool EightBit => _head.EightBit;
        public int BitsPerPixel => _head.EightBit ? 8 : 4;

        /// <summary>Stored as 8x8 tiles, not row by row.</summary>
        public bool Tiled => _head.Tiled;

        public int Width { get; }
        public int Height { get; }

        private DrawingFile(byte[] file, NitroBgCodec.DrawingHeader head)
        {
            _file = file;
            _head = head;
            Tiles = file.AsSpan(head.DataAt, head.DataSize).ToArray();
            (int width, int height) = NitroBgCodec.DrawingSize(head);
            // A tiled drawing is whole tiles, so an unrecorded size worked out from the byte count rounds up to them.
            Width = head.Tiled ? WholeTiles(width) : width;
            Height = head.Tiled ? WholeTiles(height) : height;
        }

        private static int WholeTiles(int pixels) => Math.Max(8, (pixels + 7) / 8 * 8);

        /// <summary>Reads a drawing, or null when the bytes are not one.</summary>
        public static DrawingFile Read(byte[] file)
            => NitroBgCodec.ReadDrawingHeader(file) is NitroBgCodec.DrawingHeader head ? new DrawingFile(file, head) : null;

        /// <summary>Replaces every pixel byte; the count must stay the same.</summary>
        public void SetTiles(byte[] tiles)
        {
            if (tiles == null || tiles.Length != _head.DataSize)
                throw new ArgumentException($"This drawing needs {_head.DataSize} bytes of pixels, not {tiles?.Length ?? 0}.");
            Tiles = tiles;
        }

        /// <summary>The whole file with the current pixel bytes.</summary>
        public byte[] ToBytes()
        {
            byte[] outp = (byte[])_file.Clone();
            Array.Copy(Tiles, 0, outp, _head.DataAt, Tiles.Length);
            return outp;
        }
    }
}
