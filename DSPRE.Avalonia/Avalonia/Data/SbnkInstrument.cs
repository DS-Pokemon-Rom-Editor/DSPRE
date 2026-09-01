using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>One playable region of an instrument: which of the bank's 4 linked wave archives to use (an
    /// index into <see cref="SdatBankInfo.WaveArcNo"/>, not a global wave-archive number), which wave inside it
    /// to play, the MIDI-style base note that sample was recorded at (so other notes can be pitch-shifted
    /// relative to it), and the key range this region covers (a single-sample instrument covers the whole
    /// 0-127 range; a multi-region instrument splits it into several regions, each its own sample).</summary>
    public sealed class SbnkRegion
    {
        public int LowKey, HighKey = 127;
        public int WaveArcSlot;
        public int WaveIndex;
        public int BaseNote = 60;   // middle C, the common default when a record doesn't specify one

        // Raw SNDInstParam envelope bytes (0-127, Nitro SDK's own rate/level encoding, NOT plain milliseconds
        // or a linear 0-1 level; see NitroEnvelope for the conversion. 127 means "fast/instant" for
        // attack/decay/release, "full level" for sustain). All-127 is a flat, unshaped envelope, matching a
        // record that (for whatever reason) didn't carry real envelope bytes rather than silently applying a
        // slow ramp or an inaudible decay.
        public int Attack = 127, Decay = 127, Sustain = 127, Release = 127;

        /// <summary>Which of the DS's own tone generators plays this region, when no sample does.</summary>
        public PsgKind Psg = PsgKind.None;

        /// <summary>How much of each cycle the square wave spends high, 0 to 7. Only read for a square.</summary>
        public int PsgDuty;
    }

    /// <summary>
    /// The DS can make a note without any recorded sound, either as a square wave or as noise.
    /// </summary>
    public enum PsgKind { None = 0, Square = 1, Noise = 2 }

    /// <summary>An instrument resolved from an SBNK record: one or more <see cref="SbnkRegion"/>s covering the
    /// playable key range between them.</summary>
    public sealed class SbnkInstrument
    {
        public List<SbnkRegion> Regions { get; } = new List<SbnkRegion>();

        /// <summary>The region that plays for MIDI note <paramref name="note"/>, or null if none covers it.</summary>
        public SbnkRegion Resolve(int note)
        {
            foreach (var r in Regions) if (note >= r.LowKey && note <= r.HighKey) return r;
            return null;
        }
    }

    /// <summary>
    /// Parses an SBNK instrument bank (public Nitro sound format: file header, one DATA block, then a
    /// per-program-slot offset table of record-type byte + 24-bit offset relative to the SBNK sub-file's
    /// own byte 0) into per-program instrument info. Matches <c>SNDBankData</c>'s on-disk layout: a
    /// 32-byte reserved area (the in-memory struct's wave-archive link pointers, zero-filled on disk)
    /// sits between the block header and the real instrument count.
    ///
    /// Decodes the PCM-backed record types: single-region (one-shot SFX), key-split (several regions,
    /// each its own note range) and drum set (one region per key, for percussion), and the two that make
    /// their sound without a recording: square wave and noise.
    /// </summary>
    public static class SbnkBank
    {
        public static List<SbnkInstrument> ParseBank(byte[] d)
        {
            var list = new List<SbnkInstrument>();
            if (d == null || d.Length < 16 + 8 + 4) return list;

            int U16(int o) => d[o] | (d[o + 1] << 8);
            uint U32(int o) => (uint)(d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24));
            string Sig4(int o) => System.Text.Encoding.ASCII.GetString(d, o, 4);

            if (Sig4(0) != "SBNK") return list;
            const int blockStart = 16;
            if (Sig4(blockStart) != "DATA") return list;
            const int reservedSize = 32;   // SNDBankData.waveArcLink[SND_BANK_TO_WAVEARC_MAX], zero on disk
            int count = (int)U32(blockStart + 8 + reservedSize);

            for (int i = 0; i < count; i++)
            {
                int entryAt = blockStart + 8 + reservedSize + 4 + i * 4;
                if (entryAt + 4 > d.Length) break;
                uint packed = U32(entryAt);
                int recordType = (int)(packed & 0xFF);
                int relOff = (int)(packed >> 8);
                if (recordType == 0 || relOff == 0) { list.Add(null); continue; }

                // The offset is relative to this SBNK sub-file's own byte 0, NOT the DATA block's start (same
                // base the format's own instCount lookup uses).
                int at = relOff;
                var inst = new SbnkInstrument();

                switch (recordType)
                {
                    // Single-region PCM instrument (10 bytes, the real SNDInstParam layout: sampleIndex u16,
                    // wave-archive-slot index u16 (a plain 0-3 index into the bank's 4 linked wave archives, no
                    // bit or mask games), unityKey u8, then attack/decay/sustain/release/pan, one byte each).
                    // Covers the whole key range.
                    case 1 when at + 5 <= d.Length:
                    {
                        var rgn = new SbnkRegion { LowKey = 0, HighKey = 127, WaveIndex = U16(at), WaveArcSlot = U16(at + 2), BaseNote = d[at + 4] };
                        if (at + 9 <= d.Length) { rgn.Attack = d[at + 5]; rgn.Decay = d[at + 6]; rgn.Sustain = d[at + 7]; rgn.Release = d[at + 8]; }
                        inst.Regions.Add(rgn);
                        break;
                    }

                    // Drum set (one region per individual MIDI key from lowKey to highKey; each region is the
                    // same 12-byte shape as a key-split region below).
                    case 0x10 when at + 2 <= d.Length:
                    {
                        int lowKey = d[at], highKey = d[at + 1];
                        int nRgns = highKey >= lowKey ? highKey - lowKey + 1 : 0;
                        for (int r = 0; r < nRgns; r++)
                        {
                            int rgnAt = at + 2 + r * 12;
                            if (rgnAt + 7 > d.Length) break;
                            var rgn = new SbnkRegion
                            {
                                LowKey = lowKey + r, HighKey = lowKey + r,
                                WaveIndex = U16(rgnAt + 2), WaveArcSlot = U16(rgnAt + 4), BaseNote = d[rgnAt + 6],
                            };
                            if (rgnAt + 11 <= d.Length) { rgn.Attack = d[rgnAt + 7]; rgn.Decay = d[rgnAt + 8]; rgn.Sustain = d[rgnAt + 9]; rgn.Release = d[rgnAt + 10]; }
                            inst.Regions.Add(rgn);
                        }
                        break;
                    }

                    // Key-split (multi-region) instrument: up to 8 ascending key-range boundary bytes, stopping
                    // at the first 0, then one 12-byte region per boundary (region i covers keyRanges[i-1]+1
                    // through keyRanges[i], the first region starting at key 0).
                    case 0x11 when at + 8 <= d.Length:
                    {
                        var keyRanges = new int[8];
                        int nRgns = 0;
                        for (int k = 0; k < 8; k++)
                        {
                            int v = d[at + k];
                            if (v == 0) break;
                            keyRanges[k] = v;
                            nRgns++;
                        }
                        for (int r = 0; r < nRgns; r++)
                        {
                            int rgnAt = at + 8 + r * 12;
                            if (rgnAt + 7 > d.Length) break;
                            var rgn = new SbnkRegion
                            {
                                LowKey = r == 0 ? 0 : keyRanges[r - 1] + 1, HighKey = keyRanges[r],
                                WaveIndex = U16(rgnAt + 2), WaveArcSlot = U16(rgnAt + 4), BaseNote = d[rgnAt + 6],
                            };
                            if (rgnAt + 11 <= d.Length) { rgn.Attack = d[rgnAt + 7]; rgn.Decay = d[rgnAt + 8]; rgn.Sustain = d[rgnAt + 9]; rgn.Release = d[rgnAt + 10]; }
                            inst.Regions.Add(rgn);
                        }
                        break;
                    }

                    // Square wave (2) and noise (3).
                    case 2 when at + 5 <= d.Length:
                    case 3 when at + 5 <= d.Length:
                    {
                        var rgn = new SbnkRegion
                        {
                            LowKey = 0, HighKey = 127, BaseNote = d[at + 4],
                            Psg = recordType == 2 ? PsgKind.Square : PsgKind.Noise,
                            PsgDuty = U16(at) & 7,
                        };
                        if (at + 9 <= d.Length) { rgn.Attack = d[at + 5]; rgn.Decay = d[at + 6]; rgn.Sustain = d[at + 7]; rgn.Release = d[at + 8]; }
                        inst.Regions.Add(rgn);
                        break;
                    }

                    default:
                        inst = null;   // nothing else is a playable record
                        break;
                }
                list.Add(inst);
            }
            return list;
        }
    }
}
