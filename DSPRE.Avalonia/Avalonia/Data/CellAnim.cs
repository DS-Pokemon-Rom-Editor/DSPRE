using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    public readonly struct CFrame
    {
        public readonly int Cell, Dur, RawDur;
        public readonly double Px, Py, RotDeg, Sx, Sy;
        public CFrame(int cell, int dur, double px, double py, double rotDeg, double sx, double sy)
        { Cell = cell; Dur = Math.Max(1, dur); RawDur = Math.Max(0, dur); Px = px; Py = py; RotDeg = rotDeg; Sx = sx; Sy = sy; }
    }

    public sealed class CellSequence
    {
        public CFrame[] Frames;
        public bool Loop;
        // NANR play mode (1 once, 2 loop, 3 there and back, 4 there and back looping) and the frame a loop restarts at.
        public int PlayMode = 1, LoopStart;
        public int TotalDur()
        { int t = 0; if (Frames != null) foreach (CFrame f in Frames) t += f.Dur; return t; }
    }

    public sealed class CellActor
    {
        private readonly CellSequence[] _seqs;
        private int _frame, _timer, _shown;
        private bool _reverse;

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
        public int TraceCmd = -1;
        public int[] Gp = System.Array.Empty<int>();
        public double BaseX, BaseY;

        // A sprite driven by an effect task only shows on passes where its task draws its manager, as in the game.
        public bool TaskDriven, Animate, Drawn;
        public int Manager, PendingTicks;
        public bool OnScreen => Visible && (!TaskDriven || Drawn);
        // Animation time each draw adds, in the cell animation's frame units (a ManagedSprite's animSpeed).
        public int AnimSpeed = 2;
        // The affine centre relative to the position; translucent sprites blend with the screen's alpha.
        public double PivotX, PivotY;
        public bool Xlu;
        // The sprite's BG priority (ManagedSprite_SetExplicitPriority); AddSprite's template uses 1.
        public int Priority = 1;
        // The order among sprites (ManagedSprite_SetPriority), lower on top; the template uses 100.
        public int OamPriority = 100;
        // A palette fade toward a colour, in sixteenths.
        public int Tint;
        public byte TintR, TintG, TintB;

        public void SetPosition(int x, int y) { X = (short)x; Y = (short)y; }
        public void Offset(int dx, int dy) { X = (short)(X + dx); Y = (short)(Y + dy); }
        public void SetRotationIdx(int idx) => ExtraRotDeg = (ushort)idx * 360.0 / 65536.0;
        public void TickFrame() => TickBy(1);
        public bool IsAnimated => !Finished;

        // NNS_G2dSetCellAnimationCurrentFrame.
        public void SetFrame(int frame)
        {
            CellSequence s = Cur;
            if (s?.Frames == null || s.Frames.Length == 0) return;
            _frame = _shown = Math.Clamp(frame, 0, s.Frames.Length - 1); _timer = 0;
        }

        public CellActor(CellSequence[] sequences, int seq = 0)
        { _seqs = sequences ?? Array.Empty<CellSequence>(); SetSeq(seq); }

        public void SetSeq(int seq)
        { Seq = (_seqs.Length == 0) ? 0 : Math.Clamp(seq, 0, _seqs.Length - 1); _frame = _shown = 0; _timer = 0; _reverse = false; Finished = false; }

        private CellSequence Cur => (_seqs.Length == 0 || Seq >= _seqs.Length) ? null : _seqs[Seq];
        private CFrame CurFrame
        {
            get { CellSequence s = Cur; return (s?.Frames != null && s.Frames.Length > 0) ? s.Frames[Math.Min(_shown, s.Frames.Length - 1)] : default; }
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
                _shown = _frame;
            }
        }

        // NNS_G2dTickAnimCtrl: time left over carries into the next frame, except across a loop. A frame of no length
        // is passed through without being shown.
        public void TickBy(int units)
        {
            CellSequence s = Cur;
            if (s?.Frames == null || s.Frames.Length == 0) { Finished = true; return; }
            if (Finished) return;
            int n = s.Frames.Length;
            bool pingPong = s.PlayMode == 3 || s.PlayMode == 4;
            _timer += units;
            // A sequence made only of empty frames would never stop moving on.
            for (int guard = 0; !Finished && _timer >= s.Frames[_frame].RawDur && guard < 4 * n + 4; guard++)
            {
                _timer -= s.Frames[_frame].RawDur;
                _frame += _reverse ? -1 : 1;
                bool edge = _reverse ? _frame <= s.LoopStart - 1 : _frame >= n;
                if (edge)
                {
                    bool common = true;
                    if (pingPong)
                    {
                        _reverse = !_reverse;
                        common = _frame <= s.LoopStart - 1;
                    }
                    if (common)
                    {
                        if (s.Loop) { _frame = _reverse ? n - 1 : s.LoopStart; _timer = 0; }
                        else Finished = true;
                    }
                    _frame = Math.Clamp(_frame, 0, n - 1);
                }
                if (s.Frames[_frame].RawDur != 0) _shown = _frame;
            }
        }

        public static CellSequence[] FromNanr(NanrFile nanr)
        {
            if (nanr == null) return Array.Empty<CellSequence>();
            CellSequence[] outp = new CellSequence[nanr.Sequences.Count];
            for (int i = 0; i < outp.Length; i++)
            {
                NanrFile.Sequence a = nanr.Sequences[i];
                CFrame[] frames = new CFrame[a.Frames.Count];
                for (int j = 0; j < frames.Length; j++)
                {
                    NanrFile.Result r = nanr.ResultFor(i, j) ?? new NanrFile.Result();
                    bool turns = a.ElementType == 1;
                    frames[j] = new CFrame(r.Cell, a.Frames[j].Delay, r.ShiftX, r.ShiftY,
                                           turns ? r.Rotation / 65536.0 * 360.0 : 0.0,
                                           turns ? r.ScaleX / 4096.0 : 1.0, turns ? r.ScaleY / 4096.0 : 1.0);
                }
                outp[i] = new CellSequence { Frames = frames, Loop = a.PlayMode == 2 || a.PlayMode == 4, PlayMode = (int)a.PlayMode,
                                             LoopStart = Math.Min(a.LoopStartFrame, Math.Max(0, frames.Length - 1)) };
            }
            return outp;
        }
    }
}
