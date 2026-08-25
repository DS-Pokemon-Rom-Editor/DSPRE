using System;
using System.Collections.Generic;
using System.IO;
using Ekona.Images;
using Images;
using AvaBitmap = global::Avalonia.Media.Imaging.Bitmap;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Renders a move visual-effect CELL animation (the CATS layer of the WEST scripts) into Avalonia bitmap frames,
    /// reusing the existing Nitro image stack (NCGR char + NCLR palette + NCER cells + NANR animation). The four
    /// resources live in the wazaeffect/effectclact NARCs (wechar/wepltt/wecell/wecellanm); a WEST script's
    /// <c>ACT_ADD res,arc</c> commands pick the file indices to load. Mirrors TrainerClassSpriteRenderer +
    /// the Images AnimationControl reference. Covers the ~32 cell-animation moves; particle moves are unaffected.
    /// </summary>
    public sealed class WeCellAnimRenderer
    {
        private NCGR _char;
        private NCLR _pltt;
        private NCER _cell;
        private NANR _anm;

        /// <summary>One rendered animation frame: the composed sprite plus how many 1/60 s ticks to hold it.</summary>
        public readonly struct Frame
        {
            public readonly AvaBitmap Bitmap;
            public readonly int Duration;
            public Frame(AvaBitmap bitmap, int duration) { Bitmap = bitmap; Duration = duration; }
        }

        public bool Loaded => _cell != null && _char != null && _pltt != null && _anm != null;

        /// <summary>Drops whatever resources are currently loaded. This instance is shared across every move
        /// preview in the editor session (persisted so switching moves doesn't re-decode graphics unnecessarily),
        /// so a move whose own WEST script has no CATS cell-anim resource must call this, otherwise the
        /// PREVIOUS move's graphics (e.g. Surf's wave sprite) stay loaded and get reused by any later move
        /// whose script still fires a generic ACT_ADD-family opcode.</summary>
        public void Unload() { _char = null; _pltt = null; _cell = null; _anm = null; _cellRgbaCache.Clear(); }

        // Centre of the rendered sprite's non-transparent content in the 256×192 frame (for WE_057 scale/anchor).
        public int ContentCx { get; private set; } = 128;
        public int ContentCy { get; private set; } = 96;
        public int AnimationCount => _anm?.Struct.abnk.nBanks ?? 0;

        /// <summary>Loads the four cell-graphics resources at the given file indices (from the effect's CATS loads).
        /// Returns false if any archive is unmapped/missing or a file index is out of range.</summary>
        public bool Load(int charIdx, int plttIdx, int cellIdx, int anmIdx)
        {
            _char = null; _pltt = null; _cell = null; _anm = null;
            // RenderCellRgba's cache is keyed by a bare cell INDEX, with no idea which resource set it came
            // from; without clearing it here, a later move whose actor requests the same index (e.g. cell 0)
            // would get back a bitmap rendered from the PREVIOUSLY loaded char/pltt/cell archives instead of
            // this move's own.
            _cellRgbaCache.Clear();
            try
            {
                string charPath = EntryPath(DirNames.wazaEffectChar, charIdx);
                string plttPath = EntryPath(DirNames.wazaEffectPltt, plttIdx);
                string cellPath = EntryPath(DirNames.wazaEffectCell, cellIdx);
                string anmPath  = EntryPath(DirNames.wazaEffectCellAnm, anmIdx);
                if (charPath == null || plttPath == null || cellPath == null || anmPath == null)
                {
                    AppLogger.Warn($"WeCellAnim: missing resource path (char={charPath != null} pltt={plttPath != null} " +
                        $"cell={cellPath != null} anm={anmPath != null})");
                    return false;
                }

                // The effectclact entries are LZ10-compressed (0x10 header) in the NARC; decompress before parsing.
                var temps = new List<string>();
                try
                {
                    plttPath = Inflate(plttPath, temps);
                    charPath = Inflate(charPath, temps);
                    cellPath = Inflate(cellPath, temps);
                    anmPath  = Inflate(anmPath, temps);

                    _pltt = Try("NCLR", () => new NCLR(plttPath, plttIdx, Path.GetFileName(plttPath)));
                    _char = Try("NCGR", () => new NCGR(charPath, charIdx, Path.GetFileName(charPath)));
                    _cell = Try("NCER", () => new NCER(cellPath, cellIdx, Path.GetFileName(cellPath)));
                    _anm  = Try("NANR", () => new NANR(null, anmPath, anmIdx));   // NANR.Read doesn't use the plugin host
                }
                finally
                {
                    foreach (var t in temps) { try { File.Delete(t); } catch { } }
                }
                return Loaded;
            }
            catch (Exception ex)
            {
                AppLogger.Error("WeCellAnimRenderer.Load failed: " + ex.Message);
                _char = null; _pltt = null; _cell = null; _anm = null;
                return false;
            }
        }

        /// <summary>The parsed NANR sequences as the CATS playback model, drives live <see cref="DSPRE.Avalonia.Data.CellActor"/>s.</summary>
        public DSPRE.Avalonia.Data.CellSequence[] BuildSequences() => DSPRE.Avalonia.Data.CellActor.FromNanr(_anm);

        /// <summary>Renders a SINGLE cell bank (the actor's current frame) to a 256×192 bitmap, composited at the
        /// cell's own OAM positions. The scene then places it at the actor position with the frame SRT + transform.</summary>
        public AvaBitmap RenderCell(int cellIdx, int width = 256, int height = 256)
        {
            if (!Loaded || cellIdx < 0) return null;
            try
            {
                var raw = _cell.Get_RawImage(_char, _pltt, cellIdx, width, height, trans: true, currOAM: -1, draw_index: null);
                return ImageConverter.ToAvaloniaBitmap(raw);
            }
            catch (Exception ex) { AppLogger.Error("WeCellAnimRenderer.RenderCell failed: " + ex.Message); return null; }
        }

        /// <summary>One rendered cell as a straight (non-premultiplied) RGBA buffer, with the actor origin (0,0) at the
        /// canvas centre (<see cref="Size"/>/2): Ekona Get_Image draws each OAM at <c>size/2 + oam.xy</c>. Cached.</summary>
        public readonly struct CellPixels
        {
            public readonly byte[] Rgba; public readonly int Size;
            public CellPixels(byte[] rgba, int size) { Rgba = rgba; Size = size; }
        }

        private readonly Dictionary<int, CellPixels> _cellRgbaCache = new Dictionary<int, CellPixels>();

        /// <summary>Renders cell bank <paramref name="cellIdx"/> to a 256×256 RGBA buffer (origin = centre), cached by
        /// cell index so an actor re-uses it across frames. Returns an empty buffer if not loaded / out of range.</summary>
        public CellPixels RenderCellRgba(int cellIdx)
        {
            if (_cellRgbaCache.TryGetValue(cellIdx, out var c)) return c;
            const int S = 256;
            byte[] rgba = null;
            if (Loaded && cellIdx >= 0)
            {
                try
                {
                    var raw = _cell.Get_RawImage(_char, _pltt, cellIdx, S, S, trans: true, currOAM: -1, draw_index: null);
                    if (raw != null) rgba = ToRgba(raw, S);
                }
                catch (Exception ex) { AppLogger.Error("WeCellAnimRenderer.RenderCellRgba failed: " + ex.Message); }
            }
            var res = new CellPixels(rgba, S);
            _cellRgbaCache[cellIdx] = res;
            return res;
        }

        // RawImage BGRA → straight RGBA byte[S*S*4].
        private static byte[] ToRgba(DSPRE.RawImage raw, int s)
        {
            byte[] outp = new byte[s * s * 4];
            if (raw == null || raw.IsEmpty) return outp;
            int bw = Math.Min(s, raw.Width), bh = Math.Min(s, raw.Height);
            for (int y = 0; y < bh; y++)
            {
                for (int x = 0; x < bw; x++)
                {
                    int si = (y * raw.Width + x) * 4, di = (y * s + x) * 4;   // BGRA → RGBA
                    outp[di + 0] = raw.Bgra[si + 2]; outp[di + 1] = raw.Bgra[si + 1];
                    outp[di + 2] = raw.Bgra[si + 0]; outp[di + 3] = raw.Bgra[si + 3];
                }
            }
            return outp;
        }

        /// <summary>Renders every frame of animation <paramref name="animId"/> to bitmaps with their hold durations.</summary>
        public IReadOnlyList<Frame> RenderAnimation(int animId, int width = 256, int height = 192)
        {
            var frames = new List<Frame>();
            if (!Loaded || animId < 0 || animId >= AnimationCount) return frames;
            try
            {
                var anis = _anm.Struct.abnk.anis[animId];
                for (int i = 0; i < anis.nFrames; i++)
                {
                    int nCell = anis.frames[i].data.nCell;
                    int duration = anis.frames[i].unknown1;   // NANR per-frame hold (1/60 s units)
                    if (duration <= 0) duration = 1;
                    var raw = _cell.Get_RawImage(_char, _pltt, nCell, width, height, trans: true, currOAM: -1, draw_index: null);
                    if (i == 0) ComputeContentCenter(raw, width, height);
                    frames.Add(new Frame(ImageConverter.ToAvaloniaBitmap(raw), duration));
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("WeCellAnimRenderer.RenderAnimation failed: " + ex.Message);
            }
            return frames;
        }

        // Scan the rendered frame for the bounding box of non-transparent pixels and store its centre, so WE_057
        // can scale/anchor the wave about its ACTUAL position instead of assuming the frame centre (which made it
        // jump). Falls back to the frame centre if empty.
        private void ComputeContentCenter(DSPRE.RawImage raw, int w, int h)
        {
            if (raw == null || raw.IsEmpty) return;
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y += 2)
                for (int x = 0; x < w; x += 2)
                {
                    if (x >= raw.Width || y >= raw.Height) continue;
                    if (raw.Bgra[(y * raw.Width + x) * 4 + 3] <= 8) continue;   // alpha
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
            if (maxX >= minX && maxY >= minY) { ContentCx = (minX + maxX) / 2; ContentCy = (minY + maxY) / 2; }
            else { ContentCx = w / 2; ContentCy = h / 2; }
        }

        // If the file is LZ10-compressed (NDS 0x10 header), decompress it to a temp file and return that path
        // (tracked for later deletion); otherwise return the original path. The clact resource readers take a file
        // path, so we materialise the inflated bytes to a temp file rather than threading a byte[] through them.
        private static string Inflate(string path, List<string> temps)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 4 || bytes[0] != 0x10) return path;   // not LZ10 (raw RLCN/RGCN etc.)
            byte[] raw = NSMBe4.ROM.LZ77_Decompress(bytes);
            string tmp = Path.Combine(Path.GetTempPath(), "dspre_we_" + Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllBytes(tmp, raw);
            temps.Add(tmp);
            return tmp;
        }

        // Constructs one resource, logging (instead of aborting all four) if its reader throws, so we see exactly
        // which format/file is the culprit rather than a blanket "read beyond end of stream".
        private static T Try<T>(string what, Func<T> make) where T : class
        {
            try { return make(); }
            catch (Exception ex) { AppLogger.Error($"WeCellAnim {what} read failed: {ex.Message}"); return null; }
        }

        // Unpacks the archive (lazily) and returns the path to entry file "NNNN", or null if unavailable.
        private static string EntryPath(DirNames dir, int index)
        {
            if (!gameDirs.ContainsKey(dir)) return null;
            DSUtils.TryUnpackNarcs(new List<DirNames> { dir });
            string baseDir = gameDirs[dir].unpackedDir;
            if (baseDir == null || !Directory.Exists(baseDir)) return null;
            string f = Path.Combine(baseDir, index.ToString("D4"));
            return File.Exists(f) ? f : null;
        }
    }
}
