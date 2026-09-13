using System;
using System.IO;
using System.Linq;
using DSPRE.Avalonia.Data;
using DSPRE.HgEngine;
using Ekona.Images;
using Images;
using AvaBitmap = global::Avalonia.Media.Imaging.Bitmap;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia
{
    /// <summary>Renders trainer class sprite frames for the Trainer Editor and the Table Editor.</summary>
    public sealed class TrainerClassSpriteRenderer
    {
        private PaletteBase _pal;
        private ImageBase _tile;

        // Vanilla path: the compiled binary NCER, read via the shared Ekona SpriteBase pipeline.
        private SpriteBase _sprite;

        // hg-engine path: OAM/cell data read straight from *_cell.json/*_anim.json (see
        // HgEngineTrainerGraphicsSource) instead of the compiled narc, bypassing a real nitrogfx
        // compiler bug in this checkout that corrupts the OAM size/flip bits for any OAM with a negative
        // X coordinate. Composited via Actions.Get_RawImage directly since there's no SpriteBase-derived
        // class to hold externally-built banks.
        private Bank[] _jsonBanks;
        private uint _jsonBlockSize;

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
        public bool HasSprite => _sprite != null || _jsonBanks != null || _flat;

        // Every NANR sequence as (bank, duration) frames, for callers that play a particular one.
        private (int Bank, int Duration)[][] _sequences = Array.Empty<(int, int)[]>();

        /// <summary>How many animation sequences the class has (0 when they could not be read).</summary>
        public int SequenceCount => _sequences.Length;

        /// <summary>A sequence's frames as (cell bank, duration in 60 fps units).</summary>
        public (int Bank, int Duration)[] Sequence(int seq) =>
            seq >= 0 && seq < _sequences.Length ? _sequences[seq] : Array.Empty<(int, int)>();

        /// <summary>Renders one cell bank, the way <see cref="Render"/> renders a frame.</summary>
        public AvaBitmap RenderBank(int bankIndex, int width, int height)
        {
            if (_flat) return Render(bankIndex, width, height);
            int frame = Array.IndexOf(_frameBankIndices, bankIndex);
            if (frame >= 0) return Render(frame, width, height);
            if (_sprite == null || bankIndex < 0 || bankIndex >= _sprite.Banks.Length) return null;
            try
            {
                int[] oams = Enumerable.Range(0, _sprite.Banks[bankIndex].oams.Length).ToArray();
                return ImageConverter.ToAvaloniaBitmap(_sprite.Get_RawImage(_tile, _pal, bankIndex, width, height, trans: true, currOAM: -1, draw_index: oams));
            }
            catch (Exception ex)
            {
                AppLogger.Error("TrainerClassSpriteRenderer.RenderBank failed: " + ex.Message);
                return null;
            }
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
        public int Load(int trClassID) => Load(trClassID, DirNames.trainerGraphics);

        /// <summary>Same, from another archive laid out the same way, such as the back sprites.</summary>
        public int Load(int trClassID, DirNames archive)
        {
            _pal = null; _tile = null; _sprite = null; _jsonBanks = null; _flat = false; _flatSlots = Array.Empty<int>();
            _frameBankIndices = Array.Empty<int>(); _frameDurations = Array.Empty<int>(); DefaultFrame = 0;
            _sequences = Array.Empty<(int, int)[]>();
            try
            {
                DSUtils.TryUnpackNarcs(new System.Collections.Generic.List<DirNames> { archive });
                string dir = gameDirs[archive].unpackedDir;

                int paletteFileID = TrainerGraphicsLayout.ColoursEntry(trClassID);
                string paletteFilename = paletteFileID.ToString("D4");
                _pal = new NCLR(Path.Combine(dir, paletteFilename), paletteFileID, paletteFilename);

                int tilesFileID = TrainerGraphicsLayout.DrawingEntry(trClassID);
                string tilesFilename = tilesFileID.ToString("D4");
                _tile = new NCGR(Path.Combine(dir, tilesFilename), tilesFileID, tilesFilename);

                if (TrainerGraphicsLayout.PixelsAreScrambled && _tile.Tiles != null)
                {
                    var pixels = (byte[])_tile.Tiles.Clone();
                    SpriteScrambling.Unscramble(pixels, 0, pixels.Length);
                    _tile.Set_Tiles(pixels);
                }

                if (!TrainerGraphicsLayout.HasCells)
                {
                    _flat = true;
                    _flatSlots = DrawnSlots();
                    return Math.Max(0, FrameCount - 1);
                }

                bool back = archive == DirNames.trainerBackGraphics;
                if ((back || archive == DirNames.trainerGraphics) && HgEngineProject.IsActive && TryLoadFromSource(trClassID, back))
                    return FrameCount - 1;

                int spriteFileID = TrainerGraphicsLayout.CellsEntry(trClassID);
                string spriteFilename = spriteFileID.ToString("D4");
                _sprite = new NCER(Path.Combine(dir, spriteFilename), spriteFileID, spriteFilename);

                _sequences = TryReadNanrSequences(dir, trClassID);
                var nanrSequence = TryReadNanrFrameSequence(dir, trClassID);
                _frameBankIndices = nanrSequence?.cells ?? Enumerable.Range(0, _sprite.Banks.Length).ToArray();
                _frameDurations = nanrSequence?.durations ?? Array.Empty<int>();

                int idleBank = Array.FindIndex(_sprite.Banks, b => b.name == "CellAnime0");
                int idleFrame = idleBank >= 0 ? Array.IndexOf(_frameBankIndices, idleBank) : -1;
                DefaultFrame = idleFrame >= 0 ? idleFrame : 0;

                return FrameCount - 1;
            }
            catch (Exception ex)
            {
                AppLogger.Error("TrainerClassSpriteRenderer.Load failed: " + ex.Message);
                _pal = null; _tile = null; _sprite = null; _jsonBanks = null; _flat = false; _flatSlots = Array.Empty<int>(); _frameBankIndices = Array.Empty<int>();
                return 0;
            }
        }

        /// <summary>Reads the sprite from the linked checkout's PNG, cell.json and anim.json. False when the
        /// cells can't be read, so the caller falls back to the built archive.</summary>
        private bool TryLoadFromSource(int trClassID, bool back)
        {
            string stem = HgEngineTrainerGraphicsSource.Stem(back, trClassID);
            string cellPath = stem + "_cell.json";
            string animPath = stem + "_anim.json";
            if (!File.Exists(cellPath)) return false;
            TrainerSpriteSourcePng.TryApply(stem + ".png", _tile, _pal);

            if (!HgEngineTrainerGraphicsSource.TryReadCellBanks(cellPath, out var banks, out var blockSize, out string cellError))
            {
                AppLogger.Error($"TrainerClassSpriteRenderer: {cellError}");
                return false;
            }

            _jsonBanks = banks;
            _jsonBlockSize = blockSize;

            (int[] cells, int[] durations)? anim = null;
            if (File.Exists(animPath) &&
                HgEngineTrainerGraphicsSource.TryReadAnimSequence(animPath, out var cells, out var durations, out string animError))
            {
                anim = (cells, durations);
            }

            _frameBankIndices = anim?.cells ?? Enumerable.Range(0, _jsonBanks.Length).ToArray();
            _frameDurations = anim?.durations ?? Array.Empty<int>();
            _sequences = File.Exists(animPath) ? HgEngineTrainerGraphicsSource.ReadAnimSequences(animPath) ?? Array.Empty<(int, int)[]>() : Array.Empty<(int, int)[]>();

            int idleBank = Array.FindIndex(_jsonBanks, b => b.name == "CellAnime0");
            int idleFrame = idleBank >= 0 ? Array.IndexOf(_frameBankIndices, idleBank) : -1;
            DefaultFrame = idleFrame >= 0 ? idleFrame : 0;
            return true;
        }

        // The NCER cell sequence to play back is the NANR sequence with the most frames; the other
        // sequences are single-frame idle/default poses, not the real animation. Each frame's own
        // "unknown1" field is its duration in 60fps ticks.
        private static (int[] cells, int[] durations)? TryReadNanrFrameSequence(string dir, int trClassID)
        {
            try
            {
                int nanrFileID = TrainerGraphicsLayout.AnimationEntry(trClassID);
                string path = Path.Combine(dir, nanrFileID.ToString("D4"));
                if (!File.Exists(path)) return null;

                var nanr = new NANR(null, path, nanrFileID);
                var anis = nanr.Struct.abnk.anis;
                if (anis == null || anis.Length == 0) return null;

                var longest = anis.OrderByDescending(a => a.nFrames).First();
                return (longest.frames.Select(f => (int)f.data.nCell).ToArray(),
                        longest.frames.Select(f => (int)f.unknown1).ToArray());
            }
            catch { return null; }
        }

        private static (int Bank, int Duration)[][] TryReadNanrSequences(string dir, int trClassID)
        {
            try
            {
                int nanrFileID = TrainerGraphicsLayout.AnimationEntry(trClassID);
                string path = Path.Combine(dir, nanrFileID.ToString("D4"));
                if (!File.Exists(path)) return Array.Empty<(int, int)[]>();
                var anis = new NANR(null, path, nanrFileID).Struct.abnk.anis;
                if (anis == null) return Array.Empty<(int, int)[]>();
                return anis.Select(a => a.frames == null ? Array.Empty<(int, int)>()
                    : a.frames.Select(f => ((int)f.data.nCell, (int)f.unknown1)).ToArray()).ToArray();
            }
            catch { return Array.Empty<(int, int)[]>(); }
        }

        /// <summary>One square frame slot cut out of the sheet.</summary>
        private static DSPRE.RawImage Slot(DSPRE.RawImage whole, int slot)
        {
            int size = whole.Height;
            if (size <= 0 || (slot + 1) * size > whole.Width) return whole;

            var frame = new DSPRE.RawImage(size, size);
            for (int y = 0; y < size; y++)
                Array.Copy(whole.Bgra, y * whole.Stride + slot * size * 4, frame.Bgra, y * frame.Stride, size * 4);
            return frame;
        }

        /// <summary>The slots with anything drawn in them.</summary>
        private int[] DrawnSlots()
        {
            byte[] px = _tile?.Tiles;
            int size = _tile?.Height ?? 0;
            if (px == null || size <= 0 || _tile.BPP != 4) return new[] { 0 };

            int w = _tile.Width, slots = Math.Max(1, w / size);
            var drawn = new System.Collections.Generic.List<int>();
            for (int s = 0; s < slots; s++)
            {
                bool any = false;
                for (int y = 0; y < size && !any; y++)
                    for (int x = s * size; x < (s + 1) * size && !any; x++)
                    {
                        int i = x + y * w;
                        if (i / 2 < px.Length && (i % 2 == 0 ? px[i / 2] & 0x0F : px[i / 2] >> 4) != 0) any = true;
                    }
                if (any) drawn.Add(s);
            }
            return drawn.Count > 0 ? drawn.ToArray() : new[] { 0 };
        }

        /// <summary>Makes colour zero transparent, since a flat drawing sets every pixel.</summary>
        private void ClearColourZero(DSPRE.RawImage raw)
        {
            byte[] px = _tile?.Tiles;
            if (raw == null || px == null || _tile.BPP != 4) return;

            int w = _tile.Width, h = _tile.Height;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = x + y * w;
                    if (i / 2 >= px.Length) return;
                    int colour = i % 2 == 0 ? px[i / 2] & 0x0F : px[i / 2] >> 4;
                    if (colour == 0) raw.SetPixel(x, y, 0, 0, 0, 0);
                }
            }
        }

        /// <summary>Renders the given frame to an Avalonia bitmap, or null if there is no animated sprite.</summary>
        public AvaBitmap Render(int frame, int width, int height)
        {
            if (_flat)
            {
                try
                {
                    var flat = _tile.Get_RawImage(_pal);
                    ClearColourZero(flat);
                    int slot = _flatSlots.Length == 0 ? 0 : _flatSlots[Math.Clamp(frame, 0, _flatSlots.Length - 1)];
                    return ImageConverter.ToAvaloniaBitmap(Slot(flat, slot));
                }
                catch (Exception ex)
                {
                    AppLogger.Error("TrainerClassSpriteRenderer.Render failed: " + ex.Message);
                    return null;
                }
            }
            if (_frameBankIndices.Length == 0) return null;
            try
            {
                frame = Math.Max(0, Math.Min(_frameBankIndices.Length - 1, frame));
                int bankIndex = _frameBankIndices[frame];

                if (_jsonBanks != null)
                {
                    if (bankIndex < 0 || bankIndex >= _jsonBanks.Length) return null;
                    var bank = _jsonBanks[bankIndex];
                    int[] oamEnabled = Enumerable.Range(0, bank.oams.Length).ToArray();
                    var raw = Actions.Get_RawImage(bank, _jsonBlockSize, _tile, _pal, width, height, true, -1, 1, oamEnabled);
                    return ImageConverter.ToAvaloniaBitmap(raw);
                }

                if (_sprite == null) return null;
                int oamCount = _sprite.Banks[bankIndex].oams.Length;
                int[] oamEnabledVanilla = Enumerable.Range(0, oamCount).ToArray();
                var rawVanilla = _sprite.Get_RawImage(_tile, _pal, bankIndex, width, height, trans: true, currOAM: -1, draw_index: oamEnabledVanilla);
                return ImageConverter.ToAvaloniaBitmap(rawVanilla);
            }
            catch (Exception ex)
            {
                AppLogger.Error("TrainerClassSpriteRenderer.Render failed: " + ex.Message);
                return null;
            }
        }
    }
}
