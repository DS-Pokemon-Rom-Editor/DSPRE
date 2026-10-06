using System.Collections.Generic;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    public struct BattleOffsetRecord
    {
        public int FrontY, ShadowX, ShadowSize;
        public bool HasHeights;
        public int BackF, BackM, FrontF, FrontM;   // height.narc, unsigned
    }

    public interface IBattleOffsetSource
    {
        bool TryLoad(int id, out BattleOffsetRecord rec);
        void Save(int id, in BattleOffsetRecord rec);
        void Invalidate();
    }

    /// <summary>Reads/writes per-mon records from a NARC that unpacks to either a single blob (record at
    /// id*recLen) or one file per mon (file "NNNN" = the record). Caches in memory; writes back to disk.</summary>
    public sealed class OffsetNarc
    {
        private readonly DirNames _dir;
        private readonly int _recLen;
        private bool _ready, _multi;
        private byte[] _blob;
        private string _path;

        public OffsetNarc(DirNames dir, int recLen) { _dir = dir; _recLen = recLen; }

        public void Invalidate() { _ready = false; _blob = null; }

        private void Ensure()
        {
            if (_ready) return;
            _ready = true;
            DSPRE.DSUtils.TryUnpackNarcs(new List<DirNames> { _dir });
            _path = gameDirs[_dir].unpackedDir;
            string[] files = System.IO.Directory.Exists(_path) ? System.IO.Directory.GetFiles(_path) : System.Array.Empty<string>();
            _multi = files.Length > 1;
            _blob = (!_multi && files.Length == 1) ? System.IO.File.ReadAllBytes(files[0]) : null;
        }

        private string FilePath(int id) => System.IO.Path.Combine(_path, id.ToString("D4"));

        public byte[] GetRecord(int id)
        {
            Ensure();
            if (_multi)
            {
                string f = FilePath(id);
                return System.IO.File.Exists(f) ? System.IO.File.ReadAllBytes(f) : null;
            }
            if (_blob == null) return null;
            int off = id * _recLen;
            if (off < 0 || off + _recLen > _blob.Length) return null;
            byte[] r = new byte[_recLen];
            System.Array.Copy(_blob, off, r, 0, _recLen);
            return r;
        }

        public void PutRecord(int id, byte[] rec)
        {
            Ensure();
            if (_multi) { System.IO.File.WriteAllBytes(FilePath(id), rec); return; }
            if (_blob == null) return;
            int off = id * _recLen;
            if (off < 0 || off + rec.Length > _blob.Length) return;
            System.Array.Copy(rec, 0, _blob, off, rec.Length);
            System.IO.File.WriteAllBytes(FilePath(0), _blob);   // single-file blob is "0000"
        }
    }

    /// <summary>height.narc (DP + Platinum): 4 unsigned 1-byte values per mon, file order F-back, M-back,
    /// F-front, M-front, so mon N's slot s is the (N*4 + s)th file (or byte, if it unpacks to one blob).</summary>
    public sealed class HeightNarc
    {
        private const int FB = 0, MB = 1, FF = 2, MF = 3;
        private readonly OffsetNarc _n = new OffsetNarc(DirNames.pokeHeight, 1);
        public void Invalidate() => _n.Invalidate();

        // Single-gender species leave the unused slots empty; don't fail the whole load over that.
        public bool TryLoad(int id, out int backF, out int backM, out int frontF, out int frontM)
        {
            byte[] a = _n.GetRecord(id * 4 + FB); byte[] b = _n.GetRecord(id * 4 + MB);
            byte[] c = _n.GetRecord(id * 4 + FF); byte[] d = _n.GetRecord(id * 4 + MF);
            if (a == null && b == null && c == null && d == null) { backF = backM = frontF = frontM = 0; return false; }
            backF = (a != null && a.Length >= 1) ? a[0] : 0;
            backM = (b != null && b.Length >= 1) ? b[0] : 0;
            frontF = (c != null && c.Length >= 1) ? c[0] : 0;
            frontM = (d != null && d.Length >= 1) ? d[0] : 0;
            return true;
        }

        public void Save(int id, in BattleOffsetRecord rec)
        {
            Put(id * 4 + FB, rec.BackF); Put(id * 4 + MB, rec.BackM); Put(id * 4 + FF, rec.FrontF); Put(id * 4 + MF, rec.FrontM);
        }
        private void Put(int idx, int v) => BattleOffsetBytes.Put(_n, idx, (byte)v);
    }

    /// <summary>Platinum and HGSS sprite record, plus Platinum's per-gender heights.</summary>
    public sealed class CombinedTailSource : IBattleOffsetSource
    {
        private readonly OffsetNarc _narc;
        private readonly HeightNarc _heights;   // null when withHeights:false
        public CombinedTailSource(OffsetNarc records, bool withHeights)
        { _narc = records; _heights = withHeights ? new HeightNarc() : null; }

        public void Invalidate() { _narc.Invalidate(); _heights?.Invalidate(); }

        public bool TryLoad(int id, out BattleOffsetRecord rec)
        {
            rec = default;
            byte[] r = _narc.GetRecord(id);
            if (r == null || r.Length < 3) return false;
            int n = r.Length;
            rec.FrontY = (sbyte)r[n - 3]; rec.ShadowX = (sbyte)r[n - 2]; rec.ShadowSize = r[n - 1];
            if (_heights != null && _heights.TryLoad(id, out int bf, out int bm, out int ff, out int fm))
            { rec.BackF = bf; rec.BackM = bm; rec.FrontF = ff; rec.FrontM = fm; rec.HasHeights = true; }
            return true;
        }

        public void Save(int id, in BattleOffsetRecord rec)
        {
            byte[] r = _narc.GetRecord(id);
            if (r == null || r.Length < 3) return;
            int n = r.Length;
            r[n - 3] = (byte)(sbyte)rec.FrontY; r[n - 2] = (byte)(sbyte)rec.ShadowX; r[n - 1] = (byte)rec.ShadowSize;
            _narc.PutRecord(id, r);
            if (_heights != null && rec.HasHeights) _heights.Save(id, in rec);
        }
    }

    /// <summary>Diamond / Pearl: front Y, shadow X and shadow size each live in their own single-byte-per-mon
    /// NARC, plus per-gender heights (height.narc). (pokeanm is handled separately, like form heights.)</summary>
    public sealed class SeparateByteSource : IBattleOffsetSource
    {
        private readonly OffsetNarc _y, _sx, _sz;
        private readonly HeightNarc _heights = new HeightNarc();
        public SeparateByteSource(DirNames yDir, DirNames sxDir, DirNames szDir)
        { _y = new OffsetNarc(yDir, 1); _sx = new OffsetNarc(sxDir, 1); _sz = new OffsetNarc(szDir, 1); }

        public void Invalidate() { _y.Invalidate(); _sx.Invalidate(); _sz.Invalidate(); _heights.Invalidate(); }

        public bool TryLoad(int id, out BattleOffsetRecord rec)
        {
            rec = default;
            byte[] ry = _y.GetRecord(id); byte[] rx = _sx.GetRecord(id); byte[] rz = _sz.GetRecord(id);
            if (ry == null || rx == null || rz == null || ry.Length < 1 || rx.Length < 1 || rz.Length < 1) return false;
            rec.FrontY = (sbyte)ry[0]; rec.ShadowX = (sbyte)rx[0]; rec.ShadowSize = rz[0];
            if (_heights.TryLoad(id, out int bf, out int bm, out int ff, out int fm))
            { rec.BackF = bf; rec.BackM = bm; rec.FrontF = ff; rec.FrontM = fm; rec.HasHeights = true; }
            return true;
        }

        public void Save(int id, in BattleOffsetRecord rec)
        {
            WriteByte(_y, id, (byte)(sbyte)rec.FrontY);
            WriteByte(_sx, id, (byte)(sbyte)rec.ShadowX);
            WriteByte(_sz, id, (byte)rec.ShadowSize);
            if (rec.HasHeights) _heights.Save(id, in rec);
        }
        private static void WriteByte(OffsetNarc narc, int id, byte v) => BattleOffsetBytes.Put(narc, id, v);
    }

    public static class BattleOffsetBytes
    {
        /// <summary>
        /// Writes a one-byte record. A slot the game leaves unused is an empty file, read as 0: it stays empty while
        /// the value is 0 and grows to one byte only when something else is written.
        /// </summary>
        public static void Put(OffsetNarc narc, int idx, byte v)
        {
            byte[] r = narc.GetRecord(idx);
            if (r == null) return;
            if (r.Length < 1)
            {
                if (v == 0) return;
                r = new byte[1];
            }
            else if (r[0] == v) return;
            r[0] = v;
            narc.PutRecord(idx, r);
        }
    }
}
