using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Frames a sheet can be read into: a sprite with cells, or Diamond and Pearl's fixed pair.</summary>
    public interface ISheetFrames
    {
        int FrameCount { get; }
        bool CanChangeFrameCount { get; }
        bool IsBlank(int frame);
        int[] Draw(int frame);
        int[] PalettesOf(int frame);
        string SetDrawing(int frame, int[] canvas);
    }

    /// <summary>A trainer sprite's drawing, cells and animations edited together, one cell and transfer block per frame.</summary>
    public sealed class TrainerSpriteFrames : ISheetFrames
    {
        // The cell's origin is the canvas centre, as in the sprite editor.
        public const int Canvas = 128;
        private const int Origin = Canvas / 2;

        public TrainerCellFile Cells { get; }
        public NanrFile Animations { get; }
        public byte[] Tiles { get; private set; }

        private readonly byte[] _ncgrHead, _ncgrTail;
        private readonly int _rahc;

        private TrainerSpriteFrames(TrainerCellFile cells, NanrFile anims, byte[] head, byte[] tiles, byte[] tail, int rahc)
        {
            Cells = cells; Animations = anims; _ncgrHead = head; Tiles = tiles; _ncgrTail = tail; _rahc = rahc;
        }

        public static TrainerSpriteFrames Read(byte[] ncgr, byte[] ncer, byte[] nanr, out string why)
        {
            why = null;
            TrainerCellFile cells = TrainerCellFile.Read(ncer);
            if (cells == null) { why = "The cell file is not laid out like a trainer sprite's."; return null; }
            NanrFile anims = NanrFile.Read(nanr);
            if (anims == null) { why = "The animation file could not be read."; return null; }

            int rahc = ncgr != null && ncgr.Length > 0x30 ? BitConverter.ToUInt16(ncgr, 0xC) : -1;
            if (rahc < 0 || System.Text.Encoding.ASCII.GetString(ncgr, rahc, 4) != "RAHC") { why = "The drawing has no pixel block."; return null; }
            if (BitConverter.ToUInt32(ncgr, rahc + 0xC) != 3 || (BitConverter.ToUInt32(ncgr, rahc + 0x14) & 0xFF) != 0)
            { why = "Only tiled 16-colour drawings can be rebuilt."; return null; }
            int size = BitConverter.ToInt32(ncgr, rahc + 0x18), data = rahc + 8 + BitConverter.ToInt32(ncgr, rahc + 0x1C);
            int end = rahc + BitConverter.ToInt32(ncgr, rahc + 4);
            if (data + size != end || end > ncgr.Length) { why = "The drawing's pixel block is not the usual shape."; return null; }

            foreach (TrainerCellFile.Cell c in cells.Cells)
                if (c.TransferOffset + c.TransferSize > size) { why = "A frame points past the end of the drawing."; return null; }

            return new TrainerSpriteFrames(cells, anims, ncgr[..data], ncgr[data..end], ncgr[end..], rahc);
        }

        public int FrameCount => Cells.Cells.Count;
        public bool CanChangeFrameCount => true;

        public void ReplaceTiles(byte[] tiles)
        {
            if (tiles == null || tiles.Length != Tiles.Length) throw new ArgumentException("The drawing in the editor is not the size of the one on disk.");
            Tiles = (byte[])tiles.Clone();
        }
        public bool IsBlank(int frame) => Cells.Cells[frame].IsBlank;

        public (byte[] Ncgr, byte[] Ncer, byte[] Nanr) Write()
        {
            byte[] ncgr = new byte[_ncgrHead.Length + Tiles.Length + _ncgrTail.Length];
            _ncgrHead.CopyTo(ncgr, 0);
            Tiles.CopyTo(ncgr, _ncgrHead.Length);
            _ncgrTail.CopyTo(ncgr, _ncgrHead.Length + Tiles.Length);
            BitConverter.TryWriteBytes(ncgr.AsSpan(8, 4), ncgr.Length);
            BitConverter.TryWriteBytes(ncgr.AsSpan(_rahc + 4, 4), _ncgrHead.Length - _rahc + Tiles.Length);
            BitConverter.TryWriteBytes(ncgr.AsSpan(_rahc + 0x18, 4), Tiles.Length);
            return (ncgr, Cells.Write(), Animations.Write());
        }

        // ── drawing ─────────────────────────────────────────────────────────────

        /// <summary>Pixels as palette * 16 + colour, 0 for none; a few sprites use a second palette.</summary>
        public int[] Draw(int frame)
        {
            int[] canvas = new int[Canvas * Canvas];
            TrainerCellFile.Cell c = Cells.Cells[frame];
            if (c.IsBlank) return canvas;
            // The first piece is in front.
            for (int i = c.Pieces.Count - 1; i >= 0; i--)
            {
                TrainerCellFile.Piece p = TrainerCellFile.Describe(c.Pieces[i]);
                ForEachPixel(c, p, (cx, cy, at, high) =>
                {
                    int v = high ? Tiles[at] >> 4 : Tiles[at] & 0xF;
                    if (v != 0) canvas[cy * Canvas + cx] = p.Palette * 16 + v;
                });
            }
            return canvas;
        }

        /// <summary>A bit per palette that can draw each pixel, since pieces can overlap.</summary>
        public int[] PalettesOf(int frame)
        {
            int[] canvas = new int[Canvas * Canvas];
            TrainerCellFile.Cell c = Cells.Cells[frame];
            if (c.IsBlank) c = Template();
            foreach (ushort[] piece in c.Pieces)
            {
                TrainerCellFile.Piece p = TrainerCellFile.Describe(piece);
                ForEachPixel(c, p, (cx, cy, _, _) => canvas[cy * Canvas + cx] |= 1 << p.Palette);
            }
            return canvas;
        }

        /// <summary>A drawing outside the frame's window moves the window; a blank frame drawn on becomes real.</summary>
        public string SetDrawing(int frame, int[] canvas)
        {
            TrainerCellFile.Cell c = Cells.Cells[frame];
            (int MinX, int MinY, int MaxX, int MaxY)? box = Bounds(canvas);
            if (box == null)
            {
                if (!c.IsBlank) Paint(c, canvas);
                return null;
            }
            if (c.IsBlank)
            {
                string why = MakeReal(frame);
                if (why != null) return why;
                c = Cells.Cells[frame];
            }

            (int minX, int minY, int maxX, int maxY) = box.Value;
            (int MinX, int MinY, int MaxX, int MaxY) win = Window(c);
            if (!Covers(c, canvas))
            {
                int dx = minX < win.MinX ? minX - win.MinX : maxX > win.MaxX ? maxX - win.MaxX : 0;
                int dy = minY < win.MinY ? minY - win.MinY : maxY > win.MaxY ? maxY - win.MaxY : 0;
                if (dx != 0 || dy != 0)
                {
                    string why = Cells.Shift(frame, dx, dy);
                    if (why != null) return why;
                }
                if (!Covers(c, canvas))
                    return maxX - minX > win.MaxX - win.MinX || maxY - minY > win.MaxY - win.MinY
                        ? $"Frame {frame}'s drawing is {maxX - minX + 1}x{maxY - minY + 1}, bigger than the {win.MaxX - win.MinX + 1}x{win.MaxY - win.MinY + 1} a frame holds."
                        : $"Frame {frame} draws with a palette where none of its pieces use it.";
            }
            Paint(c, canvas);
            return null;
        }

        public int AddFrame()
        {
            TrainerCellFile.Cell cell = Template().Clone();
            cell.TransferOffset = (uint)Tiles.Length;
            Tiles = Tiles.Concat(new byte[cell.TransferSize]).ToArray();
            Cells.Cells.Add(cell);
            return Cells.Cells.Count - 1;
        }

        /// <summary>Steps that showed a removed frame show the first kept one.</summary>
        public void RemoveFrames(ICollection<int> frames)
        {
            HashSet<int> gone = new HashSet<int>(frames.Where(f => f >= 0 && f < FrameCount));
            if (gone.Count == 0 || gone.Count >= FrameCount) return;

            int[] map = new int[FrameCount];
            int next = 0;
            for (int i = 0; i < FrameCount; i++) map[i] = gone.Contains(i) ? -1 : next++;
            int fallback = Array.FindIndex(map, m => m >= 0);

            // Collected first, since steps share results.
            List<(int S, int F, int To)> moves = new List<(int S, int F, int To)>();
            for (int s = 0; s < Animations.Sequences.Count; s++)
                for (int f = 0; f < Animations.Sequences[s].Frames.Count; f++)
                {
                    int cell = Animations.CellOf(s, f);
                    int to = cell < map.Length && map[cell] >= 0 ? map[cell] : map[fallback];
                    if (to != cell) moves.Add((s, f, to));
                }
            foreach ((int s, int f, int to) in moves) Animations.SetCell(s, f, to, everywhere: true);

            List<TrainerCellFile.Cell> kept = new List<TrainerCellFile.Cell>();
            for (int i = 0; i < FrameCount; i++) if (!gone.Contains(i)) kept.Add(Cells.Cells[i]);
            List<TrainerCellFile.Cell> blocks = kept.Where(k => !k.IsBlank).OrderBy(k => k.TransferOffset).ToList();
            List<byte> tiles = new List<byte>();
            foreach (TrainerCellFile.Cell k in blocks)
            {
                uint at = (uint)tiles.Count;
                tiles.AddRange(Tiles.AsSpan((int)k.TransferOffset, (int)k.TransferSize).ToArray());
                k.TransferOffset = at;
            }
            foreach (TrainerCellFile.Cell k in kept.Where(k => k.IsBlank)) k.TransferOffset = (uint)tiles.Count;
            Tiles = tiles.ToArray();
            Cells.Cells.Clear();
            Cells.Cells.AddRange(kept);
        }

        public IEnumerable<(int Sequence, int Step)> UsesOf(int frame)
        {
            for (int s = 0; s < Animations.Sequences.Count; s++)
                for (int f = 0; f < Animations.Sequences[s].Frames.Count; f++)
                    if (Animations.CellOf(s, f) == frame) yield return (s, f);
        }

        public sealed record Step(int Frame, int Hold, int X = 0, int Y = 0);

        /// <summary>An added sequence plays once, so the battle moves on after it.</summary>
        public string SetSequence(int sequence, IReadOnlyList<Step> steps)
        {
            if (steps == null || steps.Count == 0) return "An animation needs at least one step.";
            if (sequence > Animations.Sequences.Count) return $"There is no animation {sequence - 1} to follow.";
            foreach (Step st in steps)
                if (st.Frame < 0 || st.Frame >= FrameCount) return $"There is no frame {st.Frame}.";

            if (sequence == Animations.Sequences.Count)
            {
                string why = Animations.AddSequence();
                if (why != null) return why;
                Animations.SetPlayMode(sequence, 1);
                Animations.SetLoopStart(sequence, 0);
            }

            NanrFile.Sequence seq = Animations.Sequences[sequence];
            while (seq.Frames.Count < steps.Count) Animations.AddFrame(sequence, seq.Frames.Count - 1);
            while (seq.Frames.Count > steps.Count) Animations.RemoveFrame(sequence, seq.Frames.Count - 1);

            bool shifts = seq.ElementType == (ushort)NanrFile.Element.IndexTranslate;
            for (int i = 0; i < steps.Count; i++)
            {
                Animations.SetCell(sequence, i, steps[i].Frame);
                Animations.SetDelay(sequence, i, Math.Max(1, steps[i].Hold));
                if (shifts) Animations.SetShift(sequence, i, steps[i].X, steps[i].Y);
            }
            return null;
        }

        public bool SequenceShifts(int sequence) =>
            sequence >= 0 && sequence < Animations.Sequences.Count
            && Animations.Sequences[sequence].ElementType == (ushort)NanrFile.Element.IndexTranslate;

        public IReadOnlyList<Step> StepsOf(int sequence)
        {
            List<Step> list = new List<Step>();
            if (sequence < 0 || sequence >= Animations.Sequences.Count) return list;
            NanrFile.Sequence seq = Animations.Sequences[sequence];
            for (int i = 0; i < seq.Frames.Count; i++)
            {
                (int x, int y) = Animations.ShiftOf(sequence, i);
                list.Add(new Step(Animations.CellOf(sequence, i), seq.Frames[i].Delay, x, y));
            }
            return list;
        }

        // ── helpers ────────────────────────────────────────────────────────────

        // New frames copy the first drawn frame's layout.
        private TrainerCellFile.Cell Template()
        {
            TrainerCellFile.Cell real = Cells.Cells.FirstOrDefault(c => !c.IsBlank);
            if (real == null) throw new InvalidOperationException("The sprite has no drawn frame to copy the layout of.");
            return real;
        }

        private string MakeReal(int frame)
        {
            TrainerCellFile.Cell t = Template();
            TrainerCellFile.Cell cell = Cells.Cells[frame];
            cell.Pieces = t.Clone().Pieces;
            cell.Attr = t.Attr;
            cell.MinX = t.MinX; cell.MinY = t.MinY; cell.MaxX = t.MaxX; cell.MaxY = t.MaxY;
            cell.TransferSize = t.TransferSize;
            cell.TransferOffset = (uint)Tiles.Length;
            Tiles = Tiles.Concat(new byte[cell.TransferSize]).ToArray();
            return null;
        }

        private static (int MinX, int MinY, int MaxX, int MaxY)? Bounds(int[] canvas)
        {
            int minX = Canvas, minY = Canvas, maxX = -1, maxY = -1;
            for (int y = 0; y < Canvas; y++)
                for (int x = 0; x < Canvas; x++)
                    if (canvas[y * Canvas + x] != 0)
                    {
                        minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                        minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                    }
            return maxX < 0 ? null : (minX, minY, maxX, maxY);
        }

        private static (int MinX, int MinY, int MaxX, int MaxY) Window(TrainerCellFile.Cell c)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (ushort[] p in c.Pieces)
            {
                TrainerCellFile.Piece d = TrainerCellFile.Describe(p);
                minX = Math.Min(minX, Origin + d.X); minY = Math.Min(minY, Origin + d.Y);
                maxX = Math.Max(maxX, Origin + d.X + d.Width - 1); maxY = Math.Max(maxY, Origin + d.Y + d.Height - 1);
            }
            return (minX, minY, maxX, maxY);
        }

        // The pieces over each pixel, front first.
        private List<(int Palette, int At, bool High)>[] Layers(TrainerCellFile.Cell c)
        {
            List<(int, int, bool)>[] layers = new List<(int, int, bool)>[Canvas * Canvas];
            foreach (ushort[] piece in c.Pieces)
            {
                TrainerCellFile.Piece p = TrainerCellFile.Describe(piece);
                ForEachPixel(c, p, (cx, cy, at, high) => (layers[cy * Canvas + cx] ??= new()).Add((p.Palette, at, high)));
            }
            return layers;
        }

        private bool Covers(TrainerCellFile.Cell c, int[] canvas)
        {
            List<(int Palette, int At, bool High)>[] layers = Layers(c);
            for (int i = 0; i < canvas.Length; i++)
                if ((canvas[i] & 0xF) != 0 && (layers[i] == null || !layers[i].Exists(l => l.Palette == canvas[i] >> 4))) return false;
            return true;
        }

        // The frontmost piece with the pixel's palette takes it; pieces in front of it are cleared there.
        private void Paint(TrainerCellFile.Cell c, int[] canvas)
        {
            List<(int Palette, int At, bool High)>[] layers = Layers(c);
            for (int i = 0; i < canvas.Length; i++)
            {
                List<(int Palette, int At, bool High)> stack = layers[i];
                if (stack == null) continue;
                int v = canvas[i] & 0xF, bank = canvas[i] >> 4;
                int target = v == 0 ? stack.Count : stack.FindIndex(l => l.Palette == bank);
                for (int k = 0; k < stack.Count && k <= target; k++)
                {
                    (int _, int at, bool high) = stack[k];
                    int put = k == target ? v : 0;
                    Tiles[at] = high ? (byte)((Tiles[at] & 0x0F) | (put << 4)) : (byte)((Tiles[at] & 0xF0) | put);
                }
            }
        }

        // A piece's tiles run left to right, odd pixels in the high nibble.
        private void ForEachPixel(TrainerCellFile.Cell c, TrainerCellFile.Piece p, Action<int, int, int, bool> visit)
        {
            if (p.Colour256) return;
            int tilesWide = p.Width / 8;
            int start = (int)c.TransferOffset + p.Char * Cells.CharUnit;
            for (int ly = 0; ly < p.Height; ly++)
                for (int lx = 0; lx < p.Width; lx++)
                {
                    int tile = (ly / 8) * tilesWide + lx / 8;
                    int at = start + tile * 32 + (ly % 8) * 4 + (lx % 8) / 2;
                    if (at >= Tiles.Length || at >= c.TransferOffset + c.TransferSize) continue;
                    int cx = Origin + p.X + (p.FlipX ? p.Width - 1 - lx : lx);
                    int cy = Origin + p.Y + (p.FlipY ? p.Height - 1 - ly : ly);
                    if (cx < 0 || cy < 0 || cx >= Canvas || cy >= Canvas) continue;
                    visit(cx, cy, at, (lx & 1) != 0);
                }
        }
    }
}
