using System;
using System.Collections.Generic;
using System.IO;
using Ekona.Images;
using Images;
using AvaBitmap = global::Avalonia.Media.Imaging.Bitmap;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia
{
    public sealed class WeCellAnimRenderer
    {
        private NCGR _char;
        private NCLR _pltt;
        private NCER _cell;
        private NANR _anm;

        public readonly struct Frame
        {
            public readonly AvaBitmap Bitmap;
            public readonly int Duration;
            public Frame(AvaBitmap bitmap, int duration) { Bitmap = bitmap; Duration = duration; }
        }

        public bool Loaded => _cell != null && _char != null && _pltt != null && _anm != null;

        public void Unload() { _char = null; _pltt = null; _cell = null; _anm = null; _cellRgbaCache.Clear(); }

        public int ContentCx { get; private set; } = 128;
        public int ContentCy { get; private set; } = 96;
        public int AnimationCount => _anm?.Struct.abnk.nBanks ?? 0;

        public bool Load(int charIdx, int plttIdx, int cellIdx, int anmIdx)
            => Load(DirNames.wazaEffectChar, charIdx, DirNames.wazaEffectPltt, plttIdx,
                    DirNames.wazaEffectCell, cellIdx, DirNames.wazaEffectCellAnm, anmIdx);

        public bool Load(DirNames charDir, int charIdx, DirNames plttDir, int plttIdx, DirNames cellDir, int cellIdx, DirNames anmDir, int anmIdx)
        {
            _char = null; _pltt = null; _cell = null; _anm = null;
            _cellRgbaCache.Clear();
            try
            {
                string charPath = EntryPath(charDir, charIdx);
                string plttPath = EntryPath(plttDir, plttIdx);
                string cellPath = EntryPath(cellDir, cellIdx);
                string anmPath  = EntryPath(anmDir, anmIdx);
                if (charPath == null || plttPath == null || cellPath == null || anmPath == null)
                {
                    AppLogger.Warn($"WeCellAnim: missing resource path (char={charPath != null} pltt={plttPath != null} " +
                        $"cell={cellPath != null} anm={anmPath != null})");
                    return false;
                }

                List<string> temps = new List<string>();
                try
                {
                    plttPath = Inflate(plttPath, temps);
                    charPath = Inflate(charPath, temps);
                    cellPath = Inflate(cellPath, temps);
                    anmPath  = Inflate(anmPath, temps);

                    _pltt = Try("NCLR", () => new NCLR(plttPath, plttIdx, Path.GetFileName(plttPath)));
                    _char = Try("NCGR", () => new NCGR(charPath, charIdx, Path.GetFileName(charPath)));
                    _cell = Try("NCER", () => new NCER(cellPath, cellIdx, Path.GetFileName(cellPath)));
                    _anm  = Try("NANR", () => new NANR(null, anmPath, anmIdx));
                }
                finally
                {
                    foreach (string t in temps) { try { File.Delete(t); } catch { } }
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

        public DSPRE.Avalonia.Data.CellSequence[] BuildSequences() => DSPRE.Avalonia.Data.CellActor.FromNanr(_anm);

        public AvaBitmap RenderCell(int cellIdx, int width = 256, int height = 256)
        {
            if (!Loaded || cellIdx < 0) return null;
            try
            {
                RawImage raw = _cell.Get_RawImage(_char, _pltt, cellIdx, width, height, trans: true, currOAM: -1, draw_index: null);
                return ImageConverter.ToAvaloniaBitmap(raw);
            }
            catch (Exception ex) { AppLogger.Error("WeCellAnimRenderer.RenderCell failed: " + ex.Message); return null; }
        }

        public readonly struct CellPixels
        {
            public readonly byte[] Rgba; public readonly int Size;
            public CellPixels(byte[] rgba, int size) { Rgba = rgba; Size = size; }
        }

        private readonly Dictionary<int, CellPixels> _cellRgbaCache = new Dictionary<int, CellPixels>();

        public CellPixels RenderCellRgba(int cellIdx)
        {
            if (_cellRgbaCache.TryGetValue(cellIdx, out CellPixels c)) return c;
            const int S = 256;
            byte[] rgba = null;
            if (Loaded && cellIdx >= 0)
            {
                try
                {
                    RawImage raw = _cell.Get_RawImage(_char, _pltt, cellIdx, S, S, trans: true, currOAM: -1, draw_index: null);
                    if (raw != null) rgba = ToRgba(raw, S);
                }
                catch (Exception ex) { AppLogger.Error("WeCellAnimRenderer.RenderCellRgba failed: " + ex.Message); }
            }
            CellPixels res = new CellPixels(rgba, S);
            _cellRgbaCache[cellIdx] = res;
            return res;
        }

        private static byte[] ToRgba(DSPRE.RawImage raw, int s)
        {
            byte[] outp = new byte[s * s * 4];
            if (raw == null || raw.IsEmpty) return outp;
            int bw = Math.Min(s, raw.Width), bh = Math.Min(s, raw.Height);
            for (int y = 0; y < bh; y++)
            {
                for (int x = 0; x < bw; x++)
                {
                    int si = (y * raw.Width + x) * 4, di = (y * s + x) * 4;
                    outp[di + 0] = raw.Bgra[si + 2]; outp[di + 1] = raw.Bgra[si + 1];
                    outp[di + 2] = raw.Bgra[si + 0]; outp[di + 3] = raw.Bgra[si + 3];
                }
            }
            return outp;
        }

        public IReadOnlyList<Frame> RenderAnimation(int animId, int width = 256, int height = 192)
        {
            List<Frame> frames = new List<Frame>();
            if (!Loaded || animId < 0 || animId >= AnimationCount) return frames;
            try
            {
                NANR.sNANR.Animation anis = _anm.Struct.abnk.anis[animId];
                for (int i = 0; i < anis.nFrames; i++)
                {
                    int nCell = anis.frames[i].data.nCell;
                    int duration = anis.frames[i].unknown1;
                    if (duration <= 0) duration = 1;
                    RawImage raw = _cell.Get_RawImage(_char, _pltt, nCell, width, height, trans: true, currOAM: -1, draw_index: null);
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

        private void ComputeContentCenter(DSPRE.RawImage raw, int w, int h)
        {
            if (raw == null || raw.IsEmpty) return;
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y += 2)
                for (int x = 0; x < w; x += 2)
                {
                    if (x >= raw.Width || y >= raw.Height) continue;
                    if (raw.Bgra[(y * raw.Width + x) * 4 + 3] <= 8) continue;
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
            if (maxX >= minX && maxY >= minY) { ContentCx = (minX + maxX) / 2; ContentCy = (minY + maxY) / 2; }
            else { ContentCx = w / 2; ContentCy = h / 2; }
        }

        private static string Inflate(string path, List<string> temps)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 4 || bytes[0] != 0x10) return path;
            byte[] raw = NSMBe4.ROM.LZ77_Decompress(bytes);
            string tmp = Path.Combine(Path.GetTempPath(), "dspre_we_" + Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllBytes(tmp, raw);
            temps.Add(tmp);
            return tmp;
        }

        private static T Try<T>(string what, Func<T> make) where T : class
        {
            try { return make(); }
            catch (Exception ex) { AppLogger.Error($"WeCellAnim {what} read failed: {ex.Message}"); return null; }
        }

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
