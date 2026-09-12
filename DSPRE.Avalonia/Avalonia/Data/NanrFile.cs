using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// A cell animation file, read and written whole. The old reader in Images takes a path and drops
    /// several fields, so this is a second, byte-oriented model that can put a file back exactly as it
    /// found it.
    ///
    /// Nearly everything is kept verbatim on purpose. The parts a person edits, a frame's hold and which
    /// drawing it shows, are patched in place; everything else is handed back unchanged. That is what makes
    /// an untouched file come out byte for byte identical, which is the only way to know nothing was lost.
    ///
    /// Layout from the NitroSystem g2d animation headers in the public pokeplatinum decomp.
    /// </summary>
    public sealed class NanrFile
    {
        public const ushort Beef = 0xBEEF;
        private const byte PadByte = 0xCC;

        /// <summary>How a frame's content is stored, which decides how wide each result is.</summary>
        public enum Element { Index = 0, IndexScaleRotate = 1, IndexTranslate = 2 }

        public static int ResultSize(int element) => element switch
        {
            1 => 16,
            2 => 8,
            _ => 2,   // an index on its own is two bytes, not four
        };

        /// <summary>One entry in the result area, and how many frames point at it.</summary>
        public sealed class Result
        {
            public int Offset;          // where it sits in the result area, kept so bytes land back
            public int Element;
            public int Referrers;
            public ushort Cell;

            /// <summary>How far the sprite is shifted on this frame, in pixels. Zero unless it carries one.</summary>
            public short ShiftX, ShiftY;

            /// <summary>A turn, as the sixteen-bit angle the hardware uses, and a stretch in 4096ths.</summary>
            public ushort Rotation;
            public int ScaleX = 4096, ScaleY = 4096;
        }

        public sealed class Frame
        {
            public int ResultAt;        // the result's offset, which is how the file names it
            public ushort Delay;        // the hold, in sixtieths of a second
            public ushort Pad = Beef;
        }

        public sealed class Sequence
        {
            public ushort LoopStartFrame;
            public ushort AnimationType = 1;    // 1 cell, 2 multi-cell location
            public ushort ElementType;
            public uint PlayMode = 1;
            public uint FrameArrayAt;           // kept so the rebuilt table matches
            public List<Frame> Frames = new();
            public string Name = "";
        }

        // Everything below is handed back exactly as it arrived.
        private ushort _bom, _version, _headerSize, _sectionCount;
        private uint _stringBank;
        private byte[] _results = Array.Empty<byte>();
        private byte[] _padding = Array.Empty<byte>();
        private byte[] _uaat;               // the extended block, opaque; null when absent
        private byte[] _labl;               // the whole LBAL payload after its tag and size
        private byte[] _txeu;               // the whole TXEU payload after its tag and size
        private uint _frameArrayHead, _animContents;

        public List<Sequence> Sequences { get; } = new();
        public IReadOnlyList<Result> Results => _resultList;
        private readonly List<Result> _resultList = new();

        /// <summary>True when this file carries the extended block the trainer sprites use.</summary>
        public bool HasExtendedData => _uaat != null;

        /// <summary>Reads a file, or null when it is not one.</summary>
        public static NanrFile Read(byte[] d)
        {
            if (d == null || d.Length < 0x30) return null;
            if (d[0] != 'R' || d[1] != 'N' || d[2] != 'A' || d[3] != 'N') return null;

            var f = new NanrFile();
            f._bom = U16(d, 0x04);
            f._version = U16(d, 0x06);
            f._headerSize = U16(d, 0x0C);
            f._sectionCount = U16(d, 0x0E);

            if (d[0x10] != 'K' || d[0x11] != 'N' || d[0x12] != 'B' || d[0x13] != 'A') return null;
            uint blockSize = U32(d, 0x14);

            int bank = 0x18;                       // every pointer below counts from here
            int sequences = U16(d, bank + 0x00);
            uint sequenceHead = U32(d, bank + 0x04);
            f._frameArrayHead = U32(d, bank + 0x08);
            f._animContents = U32(d, bank + 0x0C);
            f._stringBank = U32(d, bank + 0x10);
            uint extended = U32(d, bank + 0x14);

            // Sequences, then their frames, then the results the frames point at.
            var seen = new Dictionary<int, Result>();
            for (int i = 0; i < sequences; i++)
            {
                int at = bank + (int)sequenceHead + i * 0x10;
                if (at + 0x10 > d.Length) return null;

                uint animType = U32(d, at + 0x04);
                var s = new Sequence
                {
                    LoopStartFrame = U16(d, at + 0x02),
                    AnimationType = (ushort)(animType >> 16),
                    ElementType = (ushort)(animType & 0xFF),
                    PlayMode = U32(d, at + 0x08),
                    FrameArrayAt = U32(d, at + 0x0C),
                };

                int frames = U16(d, at + 0x00);
                for (int j = 0; j < frames; j++)
                {
                    int fa = bank + (int)f._frameArrayHead + (int)s.FrameArrayAt + j * 8;
                    if (fa + 8 > d.Length) return null;

                    int resultAt = (int)U32(d, fa + 0x00);
                    s.Frames.Add(new Frame
                    {
                        ResultAt = resultAt,
                        Delay = U16(d, fa + 0x04),
                        Pad = U16(d, fa + 0x06),
                    });

                    // Several frames often share one result. Keeping them as one thing is what stops an
                    // edit to a frame quietly changing another.
                    if (!seen.TryGetValue(resultAt, out var r))
                    {
                        int ra = bank + (int)f._animContents + resultAt;
                        r = new Result
                        {
                            Offset = resultAt,
                            Element = s.ElementType,
                            Cell = ra + 2 <= d.Length ? U16(d, ra) : (ushort)0,
                        };
                        // A shift or a turn, for the two kinds that carry one. Several applications move a
                        // sprite about while showing the same drawing on every frame, so without these the
                        // animation looks frozen.
                        if (s.ElementType == 1)
                        {
                            r.Rotation = U16(d, ra + 2);
                            r.ScaleX = (int)U32(d, ra + 4);
                            r.ScaleY = (int)U32(d, ra + 8);
                            r.ShiftX = (short)U16(d, ra + 12);
                            r.ShiftY = (short)U16(d, ra + 14);
                        }
                        else if (s.ElementType == 2)
                        {
                            r.ShiftX = (short)U16(d, ra + 4);
                            r.ShiftY = (short)U16(d, ra + 6);
                        }
                        seen[resultAt] = r;
                        f._resultList.Add(r);
                    }
                    r.Referrers++;
                }
                f.Sequences.Add(s);
            }

            // The result area, kept whole so gaps and ordering survive untouched.
            int resultsStart = bank + (int)f._animContents;
            int resultsEnd = resultsStart;
            foreach (var r in f._resultList)
                resultsEnd = Math.Max(resultsEnd, resultsStart + r.Offset + ResultSize(r.Element));

            int blockEnd = 0x10 + (int)blockSize;
            if (extended != 0)
            {
                int uaatAt = bank + (int)extended;
                if (uaatAt < 0 || uaatAt > d.Length) return null;
                f._padding = Slice(d, resultsEnd, uaatAt - resultsEnd);
                f._uaat = Slice(d, uaatAt, blockEnd - uaatAt);
            }
            else
            {
                f._padding = Slice(d, resultsEnd, blockEnd - resultsEnd);
            }
            f._results = Slice(d, resultsStart, resultsEnd - resultsStart);

            // The two trailing sections, payloads kept verbatim.
            int p = blockEnd;
            while (p + 8 <= d.Length)
            {
                string tag = Encoding.ASCII.GetString(d, p, 4);
                int size = (int)U32(d, p + 4);
                if (size < 8 || p + size > d.Length) break;
                byte[] payload = Slice(d, p + 8, size - 8);
                if (tag == "LBAL") f._labl = payload;
                else if (tag == "TXEU") f._txeu = payload;
                p += size;
            }

            f.ReadNames(sequences);
            return f;
        }

        // The names sit in LBAL as a table of offsets then the strings themselves.
        private void ReadNames(int count)
        {
            if (_labl == null) return;
            int table = count * 4;
            for (int i = 0; i < count && i < Sequences.Count; i++)
            {
                if (i * 4 + 4 > _labl.Length) break;
                int at = table + (int)U32(_labl, i * 4);
                if (at < 0 || at >= _labl.Length) continue;
                int end = at;
                while (end < _labl.Length && _labl[end] != 0) end++;
                Sequences[i].Name = Encoding.ASCII.GetString(_labl, at, end - at);
            }
        }

        /// <summary>Puts the file back together. An untouched file comes out byte for byte identical.</summary>
        public byte[] Write()
        {
            int sequences = Sequences.Count;
            int totalFrames = Sequences.Sum(s => s.Frames.Count);

            const int bank = 0x18;
            const uint sequenceHead = 24;
            uint frameArrayHead = sequenceHead + (uint)(sequences * 0x10);
            uint animContents = frameArrayHead + (uint)(totalFrames * 8);

            int blockSize = 8 + 24 + sequences * 0x10 + totalFrames * 8
                          + _results.Length + _padding.Length + (_uaat?.Length ?? 0);

            int lablSize = _labl == null ? 0 : 8 + _labl.Length;
            int txeuSize = _txeu == null ? 0 : 8 + _txeu.Length;
            int fileSize = 0x10 + blockSize + lablSize + txeuSize;

            var d = new byte[fileSize];
            Encoding.ASCII.GetBytes("RNAN").CopyTo(d, 0);
            PutU16(d, 0x04, _bom);
            PutU16(d, 0x06, _version);
            PutU32(d, 0x08, (uint)fileSize);
            PutU16(d, 0x0C, _headerSize);
            PutU16(d, 0x0E, _sectionCount);

            Encoding.ASCII.GetBytes("KNBA").CopyTo(d, 0x10);
            PutU32(d, 0x14, (uint)blockSize);
            PutU16(d, bank + 0x00, (ushort)sequences);
            PutU16(d, bank + 0x02, (ushort)totalFrames);
            PutU32(d, bank + 0x04, sequenceHead);
            PutU32(d, bank + 0x08, frameArrayHead);
            PutU32(d, bank + 0x0C, animContents);
            PutU32(d, bank + 0x10, _stringBank);

            int frameAt = bank + (int)frameArrayHead;
            for (int i = 0; i < sequences; i++)
            {
                var s = Sequences[i];
                int at = bank + (int)sequenceHead + i * 0x10;
                PutU16(d, at + 0x00, (ushort)s.Frames.Count);
                PutU16(d, at + 0x02, s.LoopStartFrame);
                PutU32(d, at + 0x04, (uint)((s.AnimationType << 16) | s.ElementType));
                PutU32(d, at + 0x08, s.PlayMode);
                PutU32(d, at + 0x0C, s.FrameArrayAt);

                for (int j = 0; j < s.Frames.Count; j++)
                {
                    var fr = s.Frames[j];
                    int fa = frameAt + (int)s.FrameArrayAt + j * 8;
                    PutU32(d, fa + 0x00, (uint)fr.ResultAt);
                    PutU16(d, fa + 0x04, fr.Delay);
                    PutU16(d, fa + 0x06, fr.Pad);
                }
            }

            int resultsAt = bank + (int)animContents;
            _results.CopyTo(d, resultsAt);
            int after = resultsAt + _results.Length;
            _padding.CopyTo(d, after);
            after += _padding.Length;
            if (_uaat != null)
            {
                PutU32(d, bank + 0x14, (uint)(after - bank));
                _uaat.CopyTo(d, after);
                after += _uaat.Length;
            }

            if (_labl != null)
            {
                Encoding.ASCII.GetBytes("LBAL").CopyTo(d, after);
                PutU32(d, after + 4, (uint)(8 + _labl.Length));
                _labl.CopyTo(d, after + 8);
                after += 8 + _labl.Length;
            }
            if (_txeu != null)
            {
                Encoding.ASCII.GetBytes("TXEU").CopyTo(d, after);
                PutU32(d, after + 4, (uint)(8 + _txeu.Length));
                _txeu.CopyTo(d, after + 8);
            }
            return d;
        }

        // ── editing ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Changes how long a frame is held. This lives in the frame itself, so it can never disturb any
        /// other frame however much they share.
        /// </summary>
        public void SetDelay(int sequence, int frame, int delay)
        {
            var s = At(sequence);
            if (s == null || frame < 0 || frame >= s.Frames.Count) return;
            s.Frames[frame].Delay = (ushort)Math.Clamp(delay, 0, ushort.MaxValue);
        }

        /// <summary>How many frames, besides this one, draw from the same result.</summary>
        public int SharedWith(int sequence, int frame)
        {
            var r = ResultFor(sequence, frame);
            return r == null ? 0 : Math.Max(0, r.Referrers - 1);
        }

        /// <summary>
        /// Changes which drawing a frame shows. The drawing lives in a result, and results are shared, so
        /// by default this frame is given a result of its own and every other frame is left alone.
        /// </summary>
        public void SetCell(int sequence, int frame, int cell, bool everywhere = false)
        {
            var s = At(sequence);
            if (s == null || frame < 0 || frame >= s.Frames.Count) return;
            var fr = s.Frames[frame];
            var r = _resultList.FirstOrDefault(x => x.Offset == fr.ResultAt);
            if (r == null) return;

            if (everywhere || r.Referrers <= 1)
            {
                PutU16(_results, r.Offset, (ushort)cell);
                r.Cell = (ushort)cell;
                return;
            }

            // Give this frame its own copy on the end, and leave the shared one as it was.
            int size = ResultSize(r.Element);
            var grown = new byte[_results.Length + size];
            _results.CopyTo(grown, 0);
            Array.Copy(_results, r.Offset, grown, _results.Length, size);
            var fresh = new Result
            {
                Offset = _results.Length,
                Element = r.Element,
                Referrers = 1,
                Cell = (ushort)cell,
            };
            _results = grown;
            PutU16(_results, fresh.Offset, (ushort)cell);
            _resultList.Add(fresh);
            r.Referrers--;
            fr.ResultAt = fresh.Offset;
        }

        /// <summary>Which drawing a frame shows.</summary>
        public int CellOf(int sequence, int frame) => ResultFor(sequence, frame)?.Cell ?? 0;

        /// <summary>How far this frame shifts the sprite, in pixels.</summary>
        public (int X, int Y) ShiftOf(int sequence, int frame)
        {
            var r = ResultFor(sequence, frame);
            return r == null ? (0, 0) : (r.ShiftX, r.ShiftY);
        }

        /// <summary>This frame's turn in degrees and its stretch, for the kind that carries them.</summary>
        public (double Degrees, double ScaleX, double ScaleY) TurnOf(int sequence, int frame)
        {
            var r = ResultFor(sequence, frame);
            if (r == null || r.Element != 1) return (0, 1, 1);
            return (r.Rotation * 360.0 / 65536.0, r.ScaleX / 4096.0, r.ScaleY / 4096.0);
        }

        private Sequence At(int i) => i >= 0 && i < Sequences.Count ? Sequences[i] : null;

        private Result ResultFor(int sequence, int frame)
        {
            var s = At(sequence);
            if (s == null || frame < 0 || frame >= s.Frames.Count) return null;
            int at = s.Frames[frame].ResultAt;
            return _resultList.FirstOrDefault(x => x.Offset == at);
        }

        // ── bytes ────────────────────────────────────────────────────────────────────

        private static byte[] Slice(byte[] d, int at, int len)
        {
            if (len <= 0 || at < 0 || at >= d.Length) return Array.Empty<byte>();
            len = Math.Min(len, d.Length - at);
            var s = new byte[len];
            Array.Copy(d, at, s, 0, len);
            return s;
        }

        private static ushort U16(byte[] d, int at) =>
            at + 2 <= d.Length ? (ushort)(d[at] | (d[at + 1] << 8)) : (ushort)0;

        private static uint U32(byte[] d, int at) =>
            at + 4 <= d.Length
                ? (uint)(d[at] | (d[at + 1] << 8) | (d[at + 2] << 16) | (d[at + 3] << 24))
                : 0u;

        private static void PutU16(byte[] d, int at, ushort v)
        {
            if (at + 2 > d.Length) return;
            d[at] = (byte)v; d[at + 1] = (byte)(v >> 8);
        }

        private static void PutU32(byte[] d, int at, uint v)
        {
            if (at + 4 > d.Length) return;
            d[at] = (byte)v; d[at + 1] = (byte)(v >> 8);
            d[at + 2] = (byte)(v >> 16); d[at + 3] = (byte)(v >> 24);
        }
    }
}
