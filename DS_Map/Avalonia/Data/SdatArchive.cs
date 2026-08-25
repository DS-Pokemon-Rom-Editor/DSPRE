using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>One sequence (SSEQ) entry in an SDAT: which sub-file holds its bytecode, which bank plays it, and
    /// the player settings the game applies when it starts the sequence.</summary>
    public sealed class SdatSeqInfo
    {
        public int FileId;
        public int BankNo;
        public int Volume, ChannelPrio, PlayerPrio, PlayerNo;
    }

    /// <summary>One instrument bank (SBNK) entry: which sub-file holds it and up to 4 wave archives it draws
    /// samples from.</summary>
    public sealed class SdatBankInfo
    {
        public int FileId;
        public int[] WaveArcNo = new int[4];   // NNS_SND_ARC_INVALID_WAVEARC_NO (0xffff) = unused slot
    }

    /// <summary>One wave archive (SWAR) entry: just which sub-file holds it.</summary>
    public sealed class SdatWaveArcInfo
    {
        public int FileId;
        public int Flags;
    }

    /// <summary>
    /// Parses an SDAT sound archive, the Nitro sound engine's container for every sequence (SSEQ), instrument
    /// bank (SBNK) and wave archive (SWAR) in the game. Layout: a 48-byte file header giving the byte offset and
    /// size of an optional SYMB (name) block, a mandatory INFO block and a mandatory FAT block, followed by the
    /// raw sub-file bytes themselves. INFO holds one "offset table" per category (sequences/banks/wave archives/
    /// ...), each entry pointing (relative to the INFO block's own start) at a small fixed record; FAT holds one
    /// {byte offset, size} pair per sub-file, offsets absolute from the start of the .sdat file. SYMB mirrors
    /// INFO's per-category offset tables but each entry points at a null-terminated name string instead.
    /// This is the standard Nitro sound-archive format used by essentially every DS game, not something specific
    /// to this project; only the exact file paths and the fact a move script's "sound" argument is a raw sequence
    /// number are project-specific findings (see WestPlayer.cs's WEST_SE handling).
    /// </summary>
    public sealed class SdatArchive
    {
        private byte[] _d;
        private int _fatOffset;

        public List<SdatSeqInfo> Sequences { get; } = new List<SdatSeqInfo>();
        public List<SdatBankInfo> Banks { get; } = new List<SdatBankInfo>();
        public List<SdatWaveArcInfo> WaveArcs { get; } = new List<SdatWaveArcInfo>();

        // seq number -> name (from SYMB), only entries that had a name are present.
        public Dictionary<int, string> SeqNames { get; } = new Dictionary<int, string>();

        public static SdatArchive Parse(byte[] d)
        {
            var a = new SdatArchive();
            const int HdrSize = 48;
            if (d == null || d.Length < HdrSize) return a;
            a._d = d;

            int U16(int o) => d[o] | (d[o + 1] << 8);
            uint U32(int o) => (uint)(d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24));
            string Sig4(int o) => System.Text.Encoding.ASCII.GetString(d, o, 4);

            if (Sig4(0) != "SDAT") return a;

            int symbOffset = (int)U32(16), symbSize = (int)U32(20);
            int infoOffset = (int)U32(24), infoSize = (int)U32(28);
            int fatOffset = (int)U32(32), fatSize = (int)U32(36);
            if (infoOffset <= 0 || infoOffset + infoSize > d.Length) return a;
            a._fatOffset = fatOffset;

            // ── INFO block: one offset-table per category, each entry relative to infoOffset ──
            int seqTableOff = (int)U32(infoOffset + 8);
            int bankTableOff = (int)U32(infoOffset + 16);
            int waveArcTableOff = (int)U32(infoOffset + 20);

            List<int> ReadOffsetTable(int relOff)
            {
                var list = new List<int>();
                if (relOff == 0) return list;
                int tableAt = infoOffset + relOff;
                if (tableAt + 4 > d.Length) return list;
                int count = (int)U32(tableAt);
                for (int i = 0; i < count; i++)
                {
                    int entryAt = tableAt + 4 + i * 4;
                    if (entryAt + 4 > d.Length) break;
                    list.Add((int)U32(entryAt));   // 0 = no entry at this index
                }
                return list;
            }

            var seqOffs = ReadOffsetTable(seqTableOff);
            for (int i = 0; i < seqOffs.Count; i++)
            {
                if (seqOffs[i] == 0) { a.Sequences.Add(null); continue; }
                int at = infoOffset + seqOffs[i];
                if (at + 12 > d.Length) { a.Sequences.Add(null); continue; }
                a.Sequences.Add(new SdatSeqInfo
                {
                    FileId = (int)U32(at),
                    BankNo = U16(at + 4),
                    Volume = d[at + 6],
                    ChannelPrio = d[at + 7],
                    PlayerPrio = d[at + 8],
                    PlayerNo = d[at + 9],
                });
            }

            var bankOffs = ReadOffsetTable(bankTableOff);
            for (int i = 0; i < bankOffs.Count; i++)
            {
                if (bankOffs[i] == 0) { a.Banks.Add(null); continue; }
                int at = infoOffset + bankOffs[i];
                if (at + 12 > d.Length) { a.Banks.Add(null); continue; }
                var b = new SdatBankInfo { FileId = (int)U32(at) };
                for (int w = 0; w < 4; w++) b.WaveArcNo[w] = U16(at + 4 + w * 2);
                a.Banks.Add(b);
            }

            var waveArcOffs = ReadOffsetTable(waveArcTableOff);
            for (int i = 0; i < waveArcOffs.Count; i++)
            {
                if (waveArcOffs[i] == 0) { a.WaveArcs.Add(null); continue; }
                int at = infoOffset + waveArcOffs[i];
                if (at + 4 > d.Length) { a.WaveArcs.Add(null); continue; }
                uint packed = U32(at);   // fileId:24, flags:8 (LSB-first bitfield)
                a.WaveArcs.Add(new SdatWaveArcInfo { FileId = (int)(packed & 0xFFFFFF), Flags = (int)(packed >> 24) });
            }

            // ── SYMB block (optional): mirrors INFO's per-category offset tables, entries point at C strings ──
            if (symbOffset > 0 && symbOffset + symbSize <= d.Length && symbOffset + 12 <= d.Length)
            {
                int symbSeqTableOff = (int)U32(symbOffset + 8);
                if (symbSeqTableOff != 0)
                {
                    int tableAt = symbOffset + symbSeqTableOff;
                    if (tableAt + 4 <= d.Length)
                    {
                        int count = (int)U32(tableAt);
                        for (int i = 0; i < count; i++)
                        {
                            int entryAt = tableAt + 4 + i * 4;
                            if (entryAt + 4 > d.Length) break;
                            int strRel = (int)U32(entryAt);
                            if (strRel == 0) continue;
                            int strAt = symbOffset + strRel;
                            if (strAt >= d.Length) continue;
                            int end = strAt;
                            while (end < d.Length && d[end] != 0) end++;
                            a.SeqNames[i] = System.Text.Encoding.ASCII.GetString(d, strAt, end - strAt);
                        }
                    }
                }
            }

            return a;
        }

        /// <summary>Raw bytes of sub-file <paramref name="fileId"/> (an SSEQ/SBNK/SWAR), or null if out of range.
        /// FAT offsets are absolute from the start of the .sdat file, not relative to any block.</summary>
        public byte[] GetFileBytes(int fileId)
        {
            if (_d == null || _fatOffset <= 0) return null;
            int countAt = _fatOffset + 8;
            if (countAt + 4 > _d.Length) return null;
            int count = _d[countAt] | (_d[countAt + 1] << 8) | (_d[countAt + 2] << 16) | (_d[countAt + 3] << 24);
            if (fileId < 0 || fileId >= count) return null;

            int entryAt = _fatOffset + 12 + fileId * 16;
            if (entryAt + 8 > _d.Length) return null;
            int off = _d[entryAt] | (_d[entryAt + 1] << 8) | (_d[entryAt + 2] << 16) | (_d[entryAt + 3] << 24);
            int size = _d[entryAt + 4] | (_d[entryAt + 5] << 8) | (_d[entryAt + 6] << 16) | (_d[entryAt + 7] << 24);
            if (off < 0 || size < 0 || off + size > _d.Length) return null;

            var bytes = new byte[size];
            System.Array.Copy(_d, off, bytes, 0, size);
            return bytes;
        }

        // Decoding a bank/wave archive is expensive (SWAR decode walks every sample, each an ADPCM unpack
        // loop), so cache per sub-file rather than re-decoding on every preview or animation frame.
        //
        // ConcurrentDictionary, not Dictionary: animation playback renders each triggered sound on its own
        // background thread, and one move can fire several sounds sharing a bank on the same frame (e.g.
        // Thunder Punch). A concurrent first-write race on a plain Dictionary can corrupt it and throw,
        // which the animation path's best-effort exception handling swallows silently, so a sound just
        // never plays with no visible cause.
        private readonly ConcurrentDictionary<int, List<SbnkInstrument>> _bankCache = new ConcurrentDictionary<int, List<SbnkInstrument>>();
        private readonly ConcurrentDictionary<int, List<SwavSample>> _waveArcCache = new ConcurrentDictionary<int, List<SwavSample>>();

        /// <summary>Decoded instruments for bank <paramref name="bankNo"/> (an index into <see cref="Banks"/>),
        /// decoded once and cached thereafter.</summary>
        public List<SbnkInstrument> GetBankInstruments(int bankNo)
        {
            if (bankNo < 0 || bankNo >= Banks.Count || Banks[bankNo] == null) return null;
            return _bankCache.GetOrAdd(bankNo, no =>
            {
                var bytes = GetFileBytes(Banks[no].FileId);
                return bytes != null ? SbnkBank.ParseBank(bytes) : new List<SbnkInstrument>();
            });
        }

        /// <summary>Decoded waves for wave archive <paramref name="waveArcNo"/> (an index into
        /// <see cref="WaveArcs"/>), decoded once and cached thereafter.</summary>
        public List<SwavSample> GetWaveArchive(int waveArcNo)
        {
            if (waveArcNo == 0xFFFF || waveArcNo < 0 || waveArcNo >= WaveArcs.Count || WaveArcs[waveArcNo] == null) return null;
            return _waveArcCache.GetOrAdd(waveArcNo, no =>
            {
                var bytes = GetFileBytes(WaveArcs[no].FileId);
                return bytes != null ? SwavSample.ParseArchive(bytes) : new List<SwavSample>();
            });
        }
    }
}
