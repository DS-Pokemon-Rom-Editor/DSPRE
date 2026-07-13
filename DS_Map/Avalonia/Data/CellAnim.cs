using System;
using System.Collections.Generic;
using Images;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// One resolved CATS cell-animation frame: which cell to draw, how long to hold it, and the per-frame SRT the
    /// NANR carries (translate px/py, rotation, scale). Mirrors NNS_G2dAnimFrameData content for animType 0/1/2.
    /// </summary>
    public readonly struct CFrame
    {
        public readonly int Cell, Dur;                 // cell index + hold (1/60 s ticks)
        public readonly double Px, Py, RotDeg, Sx, Sy; // NANR frame transform (translate px, rotation°, scale)
        public CFrame(int cell, int dur, double px, double py, double rotDeg, double sx, double sy)
        { Cell = cell; Dur = Math.Max(1, dur); Px = px; Py = py; RotDeg = rotDeg; Sx = sx; Sy = sy; }
    }

    /// <summary>One NANR animation sequence (a bank): its frame list and whether it loops.</summary>
    public sealed class CellSequence
    {
        public CFrame[] Frames;
        public bool Loop;          // NNS_G2dAnimPlayMode 2 (FORWARD_LOOP) / 4 (REVERSE_LOOP)
        public int TotalDur()
        { int t = 0; if (Frames != null) foreach (var f in Frames) t += f.Dur; return t; }
    }

    /// <summary>
    /// A live CATS cell actor — the faithful playback model behind the actor-add and set-animation-sequence calls.
    /// It advances the current sequence frame-by-frame honouring each frame's hold duration and the sequence
    /// play-mode (loop vs once), and carries the actor-level transform (position/scale/flip/palette/visibility)
    /// the per-move callbacks set each tick. Rendering (NCER cell → OAM) is applied on top of <see cref="CellIndex"/> + the
    /// frame SRT + this transform by the scene compositor; this class owns only the timeline + state, so it is
    /// fully unit-testable without graphics.
    /// </summary>
    public sealed class CellActor
    {
        private readonly CellSequence[] _seqs;
        private int _frame, _timer;

        // Actor transform — set by callbacks (set-position / set-scale / set-flip / palette).
        public double X, Y;
        public double ScaleX = 1, ScaleY = 1;
        public bool FlipH, FlipV;
        public int PalShift;                 // palette-bank offset (a draw-priority-style palette change)
        public bool Visible = true;
        public double ExtraRotDeg;           // actor-level spin some callbacks add on top of the frame SRT
        public double Alpha = 1.0;

        public int Seq { get; private set; }
        public bool Finished { get; private set; }
        public int SeqCount => _seqs?.Length ?? 0;
        public int CapId = -1;        // WEST_CATS_ACT_ADD_EZ slot (so a later FUNC_CALL can find this actor); -1 = anonymous
        public bool Alive = true;     // cleared by RES_FREE
        public int FuncId = -1;       // WEST_CATS_ACT_ADD callback id (the opcode dispatch table) driving this actor
        public int Age;               // frames since spawn (the callback timeline clock)
        public int[] Gp = System.Array.Empty<int>();   // the ACT_ADD gp_wk args the callback reads
        public double BaseX, BaseY;   // spawn position (callbacks animate X,Y relative to this)

        public CellActor(CellSequence[] sequences, int seq = 0)
        { _seqs = sequences ?? Array.Empty<CellSequence>(); SetSeq(seq); }

        /// <summary>Switch to sequence <paramref name="seq"/> and restart it.</summary>
        public void SetSeq(int seq)
        { Seq = (_seqs.Length == 0) ? 0 : Math.Clamp(seq, 0, _seqs.Length - 1); _frame = 0; _timer = 0; Finished = false; }

        private CellSequence Cur => (_seqs.Length == 0 || Seq >= _seqs.Length) ? null : _seqs[Seq];
        private CFrame CurFrame
        {
            get { var s = Cur; return (s?.Frames != null && s.Frames.Length > 0) ? s.Frames[Math.Min(_frame, s.Frames.Length - 1)] : default; }
        }

        public int CellIndex => CurFrame.Cell;
        public double FrameX => CurFrame.Px;
        public double FrameY => CurFrame.Py;
        public double FrameRotDeg => CurFrame.RotDeg + ExtraRotDeg;
        public double FrameScaleX => CurFrame.Sx;
        public double FrameScaleY => CurFrame.Sy;
        public int FrameIndex => _frame;

        /// <summary>Advance one 1/60 s tick (NNS_G2dTickCellAnimation by FX32_ONE): hold the current frame for its
        /// duration, then step to the next; at the end either wrap (loop) or clamp to the last frame (once).</summary>
        public void Tick()
        {
            var s = Cur;
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

        /// <summary>Build the sequence table from a parsed NANR (fx32 scale → double, rotation u16 → degrees).</summary>
        public static CellSequence[] FromNanr(NANR nanr)
        {
            var anis = nanr?.Struct.abnk.anis;
            if (anis == null) return Array.Empty<CellSequence>();
            var outp = new CellSequence[anis.Length];
            for (int i = 0; i < anis.Length; i++)
            {
                var a = anis[i];
                var frames = new CFrame[a.frames?.Length ?? 0];
                for (int j = 0; j < frames.Length; j++)
                {
                    var d = a.frames[j].data;
                    double sx = a.dataType == 1 ? GetFrameInt(d, "scaleX", 4096) / 4096.0 : 1.0;   // fx32 → 1.0
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
            var field = typeof(NANR.sNANR.Frame_Data).GetField(fieldName);
            return field == null ? defaultValue : Convert.ToInt32(field.GetValue(data));
        }

        private static ushort GetFrameUShort(NANR.sNANR.Frame_Data data, string fieldName, ushort defaultValue)
        {
            var field = typeof(NANR.sNANR.Frame_Data).GetField(fieldName);
            return field == null ? defaultValue : Convert.ToUInt16(field.GetValue(data));
        }

        private static uint GetAnimationUInt(NANR.sNANR.Animation animation, string fieldName, uint defaultValue)
        {
            var field = typeof(NANR.sNANR.Animation).GetField(fieldName);
            return field == null ? defaultValue : Convert.ToUInt32(field.GetValue(animation));
        }
    }
}
