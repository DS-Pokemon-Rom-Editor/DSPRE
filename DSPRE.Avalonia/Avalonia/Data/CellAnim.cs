using System;
using System.Collections.Generic;
using System.Reflection;
using Images;

namespace DSPRE.Avalonia.Data
{
    public readonly struct CFrame
    {
        public readonly int Cell, Dur;
        public readonly double Px, Py, RotDeg, Sx, Sy;
        public CFrame(int cell, int dur, double px, double py, double rotDeg, double sx, double sy)
        { Cell = cell; Dur = Math.Max(1, dur); Px = px; Py = py; RotDeg = rotDeg; Sx = sx; Sy = sy; }
    }

    public sealed class CellSequence
    {
        public CFrame[] Frames;
        public bool Loop;
        public int TotalDur()
        { int t = 0; if (Frames != null) foreach (CFrame f in Frames) t += f.Dur; return t; }
    }

    public sealed class CellActor
    {
        private readonly CellSequence[] _seqs;
        private int _frame, _timer;

        public double X, Y;
        public double ScaleX = 1, ScaleY = 1;
        public bool FlipH, FlipV;
        public int PalShift;
        public bool Visible = true;
        public double ExtraRotDeg;
        public double Alpha = 1.0;

        public int Seq { get; private set; }
        public bool Finished { get; private set; }
        public int SeqCount => _seqs?.Length ?? 0;
        public int CapId = -1;
        public bool Alive = true;
        public int FuncId = -1;
        public int Age;
        public int[] Gp = System.Array.Empty<int>();
        public double BaseX, BaseY;

        public CellActor(CellSequence[] sequences, int seq = 0)
        { _seqs = sequences ?? Array.Empty<CellSequence>(); SetSeq(seq); }

        public void SetSeq(int seq)
        { Seq = (_seqs.Length == 0) ? 0 : Math.Clamp(seq, 0, _seqs.Length - 1); _frame = 0; _timer = 0; Finished = false; }

        private CellSequence Cur => (_seqs.Length == 0 || Seq >= _seqs.Length) ? null : _seqs[Seq];
        private CFrame CurFrame
        {
            get { CellSequence s = Cur; return (s?.Frames != null && s.Frames.Length > 0) ? s.Frames[Math.Min(_frame, s.Frames.Length - 1)] : default; }
        }

        public int CellIndex => CurFrame.Cell;
        public double FrameX => CurFrame.Px;
        public double FrameY => CurFrame.Py;
        public double FrameRotDeg => CurFrame.RotDeg + ExtraRotDeg;
        public double FrameScaleX => CurFrame.Sx;
        public double FrameScaleY => CurFrame.Sy;
        public int FrameIndex => _frame;

        public void Tick()
        {
            CellSequence s = Cur;
            if (s?.Frames == null || s.Frames.Length == 0) { Finished = true; return; }
            if (Finished && !s.Loop) return;
            _timer++;
            if (_timer >= s.Frames[Math.Min(_frame, s.Frames.Length - 1)].Dur)
            {
                _timer = 0;
                _frame++;
                if (_frame >= s.Frames.Length)
                {
                    if (s.Loop) _frame = 0;
                    else { _frame = s.Frames.Length - 1; Finished = true; }
                }
            }
        }

        public static CellSequence[] FromNanr(NANR nanr)
        {
            NANR.sNANR.Animation[] anis = nanr?.Struct.abnk.anis;
            if (anis == null) return Array.Empty<CellSequence>();
            CellSequence[] outp = new CellSequence[anis.Length];
            for (int i = 0; i < anis.Length; i++)
            {
                NANR.sNANR.Animation a = anis[i];
                CFrame[] frames = new CFrame[a.frames?.Length ?? 0];
                for (int j = 0; j < frames.Length; j++)
                {
                    NANR.sNANR.Frame_Data d = a.frames[j].data;
                    double sx = a.dataType == 1 ? GetFrameInt(d, "scaleX", 4096) / 4096.0 : 1.0;
                    double sy = a.dataType == 1 ? GetFrameInt(d, "scaleY", 4096) / 4096.0 : 1.0;
                    double rot = a.dataType == 1 ? GetFrameUShort(d, "rotation", 0) / 65536.0 * 360.0 : 0.0;
                    frames[j] = new CFrame(d.nCell, a.frames[j].unknown1, d.xDisplacement, d.yDisplacement, rot, sx, sy);
                }
                uint playMode = GetAnimationUInt(a, "playMode", GetAnimationUInt(a, "unknown2", 0) | (GetAnimationUInt(a, "unknown3", 0) << 16));
                outp[i] = new CellSequence { Frames = frames, Loop = playMode == 2 || playMode == 4 };
            }
            return outp;
        }

        private static int GetFrameInt(NANR.sNANR.Frame_Data data, string fieldName, int defaultValue)
        {
            FieldInfo field = typeof(NANR.sNANR.Frame_Data).GetField(fieldName);
            return field == null ? defaultValue : Convert.ToInt32(field.GetValue(data));
        }

        private static ushort GetFrameUShort(NANR.sNANR.Frame_Data data, string fieldName, ushort defaultValue)
        {
            FieldInfo field = typeof(NANR.sNANR.Frame_Data).GetField(fieldName);
            return field == null ? defaultValue : Convert.ToUInt16(field.GetValue(data));
        }

        private static uint GetAnimationUInt(NANR.sNANR.Animation animation, string fieldName, uint defaultValue)
        {
            FieldInfo field = typeof(NANR.sNANR.Animation).GetField(fieldName);
            return field == null ? defaultValue : Convert.ToUInt32(field.GetValue(animation));
        }
    }
}
