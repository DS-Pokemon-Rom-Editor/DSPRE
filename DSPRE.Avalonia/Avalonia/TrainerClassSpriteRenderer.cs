using System;
using System.IO;
using System.Linq;
using DSPRE.Avalonia.Data;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using AvaBitmap = global::Avalonia.Media.Imaging.Bitmap;
using static DSPRE.RomInfo;
using System.Collections.Generic;

namespace DSPRE.Avalonia
{
    /// <summary>Renders trainer class sprite frames for the Trainer editor and the Music & battle tables.</summary>
    public sealed class TrainerClassSpriteRenderer
    {
        private ushort[][] _rows;
        private DrawingFile _tile;

        // The built archive's cell file, or on hg-engine the cells read straight from *_cell.json (see
        // HgEngineTrainerGraphicsSource) instead of the compiled narc, bypassing a real nitrogfx compiler bug in
        // this checkout that corrupts the OAM size/flip bits for any OAM with a negative X coordinate.
        private NcerFile _cells;

        // The real animation frame order/repeats come from the NANR (###_anim.json's compiled form),
        // not from the NCER bank list directly: a bank can be a deliberate blank first frame (not
        // garbage), and the same bank can repeat several times in a row for a visual "hold".
        private int[] _frameBankIndices = Array.Empty<int>();
        private int[] _frameDurations = Array.Empty<int>();

        // Diamond and Pearl: the drawing is the finished picture, with no cells.
        private bool _flat;

        // The file holds square frame slots side by side; class sprites draw in one, back sprites in both.
        private int[] _flatSlots = Array.Empty<int>();

        public int FrameCount => _flat ? _flatSlots.Length : _frameBankIndices.Length;
        public bool HasSprite => _cells != null || _flat;

        // Every NANR sequence as (bank, duration) frames, for callers that play a particular one.
        private (int Bank, int Duration)[][] _sequences = Array.Empty<(int, int)[]>();

        /// <summary>How many animation sequences the class has (0 when they could not be read).</summary>
        public int SequenceCount => _sequences.Length;

        /// <summary>A sequence's frames as (cell bank, duration in 60 fps units).</summary>
        public (int Bank, int Duration)[] Sequence(int seq) =>
            seq >= 0 && seq < _sequences.Length ? _sequences[seq] : Array.Empty<(int, int)>();

        private ushort[] Colours => _rows.SelectMany(r => r).ToArray();

        /// <summary>Renders one cell bank, the way <see cref="Render"/> renders a frame.</summary>
        public AvaBitmap RenderBank(int bankIndex, int width, int height)
        {
            if (_flat) return Render(bankIndex, width, height);
            int frame = Array.IndexOf(_frameBankIndices, bankIndex);
            if (frame >= 0) return Render(frame, width, height);
            if (_cells == null || bankIndex < 0 || bankIndex >= _cells.Cells.Count) return null;
            return ImageConverter.ToAvaloniaBitmap(CellRenderer.Draw(_cells, bankIndex, _tile.ToBytes(), Colours, width, height));
        }

        /// <summary>Duration of the given frame in 60fps game ticks (the NANR frame header's own delay
        /// field), or 4 (a reasonable default) if unavailable.</summary>
        public int GetFrameDuration(int frame) =>
            frame >= 0 && frame < _frameDurations.Length && _frameDurations[frame] > 0 ? _frameDurations[frame] : 4;

        /// <summary>Index into the played sequence for the class's default/idle pose (the bank hg-engine
        /// itself names "CellAnime0"), not "whichever cell the animation happens to end on".</summary>
        public int DefaultFrame { get; private set; }

        /// <summary>Loads the graphics for a trainer class. Returns the max frame index (FrameCount-1)
        /// for a scrubber's upper bound; see <see cref="DefaultFrame"/> for which frame to show initially.</summary>
        public int Load(int trClassID) => Load(trClassID, DirNames.TrainerGraphics);

        /// <summary>Same, from another archive laid out the same way, such as the back sprites.</summary>
        public int Load(int trClassID, DirNames archive)
        {
            Clear();
            try
            {
                DSUtils.TryUnpackNarcs(new System.Collections.Generic.List<DirNames> { archive });
                string dir = GameDirs[archive].unpackedDir;

                bool back = archive == DirNames.TrainerBackGraphics;
                // A class added on hg-engine has its source files before a build gives it archive members; class
                // 0's members serve as same-sized containers for the source PNG laid over them below.
                int memberClass = trClassID;
                if ((back || archive == DirNames.TrainerGraphics) && HgEngineProject.IsActive
                    && !File.Exists(Path.Combine(dir, TrainerGraphicsLayout.ColoursEntry(trClassID).ToString("D4")))
                    && File.Exists(HgEngineTrainerGraphicsSource.Stem(back, trClassID) + "_cell.json"))
                    memberClass = 0;

                _rows = NitroBgCodec.ReadColourRows(File.ReadAllBytes(Path.Combine(dir, TrainerGraphicsLayout.ColoursEntry(memberClass).ToString("D4"))));
                _tile = DrawingFile.Read(File.ReadAllBytes(Path.Combine(dir, TrainerGraphicsLayout.DrawingEntry(memberClass).ToString("D4"))));
                if (_tile == null) throw new InvalidDataException("The drawing has no pixel section.");

                if (TrainerGraphicsLayout.PixelsAreScrambled)
                {
                    byte[] pixels = (byte[])_tile.Tiles.Clone();
                    SpriteScrambling.Unscramble(pixels, 0, pixels.Length);
                    _tile.SetTiles(pixels);
                }

                if (!TrainerGraphicsLayout.HasCells)
                {
                    _flat = true;
                    _flatSlots = DrawnSlots();
                    return Math.Max(0, FrameCount - 1);
                }

                if ((back || archive == DirNames.TrainerGraphics) && HgEngineProject.IsActive && TryLoadFromSource(trClassID, back))
                    return FrameCount - 1;
                if (memberClass != trClassID) { _rows = null; _tile = null; return 0; }

                _cells = NcerFile.Read(File.ReadAllBytes(Path.Combine(dir, TrainerGraphicsLayout.CellsEntry(trClassID).ToString("D4"))));
                if (_cells == null) throw new InvalidDataException("The cell file could not be read.");

                NanrFile anim = ReadNanr(dir, trClassID);
                _sequences = anim == null ? Array.Empty<(int, int)[]>()
                    : Enumerable.Range(0, anim.Sequences.Count)
                        .Select(s => Enumerable.Range(0, anim.Sequences[s].Frames.Count)
                            .Select(f => ((int)(anim.ResultFor(s, f)?.Cell ?? 0), (int)anim.Sequences[s].Frames[f].Delay)).ToArray()).ToArray();

                // The NCER cell sequence to play back is the NANR sequence with the most frames; the other
                // sequences are single-frame idle/default poses, not the real animation.
                (int Bank, int Duration)[] longest = _sequences.OrderByDescending(s => s.Length).FirstOrDefault();
                _frameBankIndices = longest != null && longest.Length > 0 ? longest.Select(f => f.Bank).ToArray() : Enumerable.Range(0, _cells.Cells.Count).ToArray();
                _frameDurations = longest != null && longest.Length > 0 ? longest.Select(f => f.Duration).ToArray() : Array.Empty<int>();

                DefaultFrame = IdleFrame();
                return FrameCount - 1;
            }
            catch (Exception ex)
            {
                AppLogger.Error("TrainerClassSpriteRenderer.Load failed: " + ex.Message);
                Clear();
                return 0;
            }
        }

        private void Clear()
        {
            _rows = null; _tile = null; _cells = null; _flat = false; _flatSlots = Array.Empty<int>();
            _frameBankIndices = Array.Empty<int>(); _frameDurations = Array.Empty<int>(); DefaultFrame = 0;
            _sequences = Array.Empty<(int, int)[]>();
        }

        private int IdleFrame()
        {
            int idleBank = _cells.Cells.FindIndex(c => c.Name == "CellAnime0");
            int idleFrame = idleBank >= 0 ? Array.IndexOf(_frameBankIndices, idleBank) : -1;
            return idleFrame >= 0 ? idleFrame : 0;
        }

        private static NanrFile ReadNanr(string dir, int trClassID)
        {
            string path = Path.Combine(dir, TrainerGraphicsLayout.AnimationEntry(trClassID).ToString("D4"));
            try { return File.Exists(path) ? NanrFile.Read(File.ReadAllBytes(path)) : null; }
            catch (Exception ex) { AppLogger.WarnOnce("TrainerClassSpriteRenderer.ReadNanr", ex); return null; }
        }

        /// <summary>Reads the sprite from the linked checkout's PNG, cell.json and anim.json. False when the
        /// cells can't be read, so the caller falls back to the built archive.</summary>
        private bool TryLoadFromSource(int trClassID, bool back)
        {
            string stem = HgEngineTrainerGraphicsSource.Stem(back, trClassID);
            string cellPath = stem + "_cell.json";
            string animPath = stem + "_anim.json";
            if (!File.Exists(cellPath)) return false;
            if (_rows.Length > 0)
            {
                System.Drawing.Color[] first = _rows[0].Select(c => { (byte r, byte g, byte b) = NitroBgCodec.Colour(c); return System.Drawing.Color.FromArgb(r, g, b); }).ToArray();
                if (TrainerSpriteSourcePng.TryApply(stem + ".png", _tile, first))
                    _rows[0] = first.Select(c => NitroBgCodec.ToBgr555(c.R, c.G, c.B)).ToArray();
            }

            if (!HgEngineTrainerGraphicsSource.TryReadCells(cellPath, out NcerFile cells, out string cellError))
            {
                AppLogger.Error($"TrainerClassSpriteRenderer: {cellError}");
                return false;
            }
            _cells = cells;

            (int[] cells, int[] durations)? anim = null;
            if (File.Exists(animPath) &&
                HgEngineTrainerGraphicsSource.TryReadAnimSequence(animPath, out int[] frameCells, out int[] durations, out string animError))
            {
                anim = (frameCells, durations);
            }

            _frameBankIndices = anim?.cells ?? Enumerable.Range(0, _cells.Cells.Count).ToArray();
            _frameDurations = anim?.durations ?? Array.Empty<int>();
            _sequences = File.Exists(animPath) ? HgEngineTrainerGraphicsSource.ReadAnimSequences(animPath) ?? Array.Empty<(int, int)[]>() : Array.Empty<(int, int)[]>();
            DefaultFrame = IdleFrame();
            return true;
        }

        /// <summary>The colour number at one pixel of the whole drawing, or -1 past its data.</summary>
        private int IndexAt(int x, int y)
        {
            int pixel = _tile.Tiled
                ? ((y / 8) * (_tile.Width / 8) + x / 8) * 64 + (y % 8) * 8 + x % 8
                : y * _tile.Width + x;
            byte[] px = _tile.Tiles;
            if (_tile.EightBit) return pixel < px.Length ? px[pixel] : -1;
            if (pixel / 2 >= px.Length) return -1;
            return (pixel & 1) == 0 ? px[pixel / 2] & 0x0F : px[pixel / 2] >> 4;
        }

        /// <summary>The whole drawing in its first row of colours, colour zero see-through on a 16-colour sheet.</summary>
        private RawImage Sheet()
        {
            RawImage raw = new RawImage(_tile.Width, _tile.Height);
            ushort[] row = _rows.Length > 0 ? _rows[0] : Array.Empty<ushort>();
            for (int y = 0; y < _tile.Height; y++)
                for (int x = 0; x < _tile.Width; x++)
                {
                    int index = IndexAt(x, y);
                    if (index < 0 || index >= row.Length || index == 0 && !_tile.EightBit) continue;
                    (byte r, byte g, byte b) = NitroBgCodec.Colour(row[index]);
                    raw.SetPixel(x, y, r, g, b, 255);
                }
            return raw;
        }

        /// <summary>One square frame slot cut out of the sheet.</summary>
        private static DSPRE.RawImage Slot(DSPRE.RawImage whole, int slot)
        {
            int size = whole.Height;
            if (size <= 0 || (slot + 1) * size > whole.Width) return whole;

            RawImage frame = new DSPRE.RawImage(size, size);
            for (int y = 0; y < size; y++)
                Array.Copy(whole.Bgra, y * whole.Stride + slot * size * 4, frame.Bgra, y * frame.Stride, size * 4);
            return frame;
        }

        /// <summary>The slots with anything drawn in them.</summary>
        private int[] DrawnSlots()
        {
            int size = _tile.Height;
            if (size <= 0 || _tile.EightBit) return new[] { 0 };

            int w = _tile.Width, slots = Math.Max(1, w / size);
            List<int> drawn = new System.Collections.Generic.List<int>();
            for (int s = 0; s < slots; s++)
            {
                bool any = false;
                for (int y = 0; y < size && !any; y++)
                    for (int x = s * size; x < (s + 1) * size && !any; x++)
                        if (IndexAt(x, y) > 0) any = true;
                if (any) drawn.Add(s);
            }
            return drawn.Count > 0 ? drawn.ToArray() : new[] { 0 };
        }

        /// <summary>
        /// A square block of a Diamond and Pearl drawing as RGBA, taken from the top of the whole sheet starting
        /// <paramref name="left"/> pixels in, the way the gym and Elite Four intros cut their faces. Null if there is no drawing.
        /// </summary>
        public byte[] FlatCut(int left, int size)
        {
            if (!_flat) return null;
            RawImage sheet = Sheet();
            byte[] rgba = new byte[size * size * 4];
            for (int y = 0; y < size && y < sheet.Height; y++)
                for (int x = 0; x < size && left + x < sheet.Width; x++)
                {
                    int from = y * sheet.Stride + (left + x) * 4, to = (y * size + x) * 4;
                    rgba[to] = sheet.Bgra[from + 2]; rgba[to + 1] = sheet.Bgra[from + 1]; rgba[to + 2] = sheet.Bgra[from]; rgba[to + 3] = sheet.Bgra[from + 3];
                }
            return rgba;
        }

        /// <summary>Renders the given frame to an Avalonia bitmap, or null if there is no animated sprite.</summary>
        public AvaBitmap Render(int frame, int width, int height)
        {
            if (_flat)
            {
                int slot = _flatSlots.Length == 0 ? 0 : _flatSlots[Math.Clamp(frame, 0, _flatSlots.Length - 1)];
                return ImageConverter.ToAvaloniaBitmap(Slot(Sheet(), slot));
            }
            if (_frameBankIndices.Length == 0 || _cells == null) return null;
            frame = Math.Max(0, Math.Min(_frameBankIndices.Length - 1, frame));
            int bankIndex = _frameBankIndices[frame];
            if (bankIndex < 0 || bankIndex >= _cells.Cells.Count) return null;
            return ImageConverter.ToAvaloniaBitmap(CellRenderer.Draw(_cells, bankIndex, _tile.ToBytes(), Colours, width, height));
        }
    }
}
