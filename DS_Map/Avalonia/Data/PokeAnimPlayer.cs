using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Interprets a parsed PAST program-animation script (see <see cref="PokeAnimScript"/>) and produces the
    /// per-frame sprite transform, faithfully reproducing the runtime in the leaked source (src/pokeanime/
    /// p_anm_sys.c). Pure/UI-free so it can be unit-tested and driven by a 60 fps timer in the preview.
    ///
    /// Model: each 60 fps tick = one <see cref="Step"/>. A frame either decrements an outstanding wait, or runs
    /// the move-functions then executes commands until one yields the frame (SET_WAIT / HOLD_CMD while move-funcs
    /// are still running / palette-fade wait / END). Targets DX/DY = pixel translate, RX/RY = scale offset
    /// (base 0x100 = 1×), ROT = rotation (0x10000 = 360°). Apply modes SET / ADD(start+val) / SYNTHE(+=val).
    /// </summary>
    public sealed class PokeAnimPlayer
    {
        // PAST enums (past_def.h)
        private const int TARGET_DX = 35, TARGET_DY = 36, TARGET_RX = 37, TARGET_RY = 38, TARGET_ROT = 39;
        private const int CURVE_SIN = 30, CURVE_COS = 31, CURVE_SIN_MINUS = 32, CURVE_COS_MINUS = 33;
        private const int APPLY_SET = 24, APPLY_ADD = 25, APPLY_SYNTHE = 26;
        private const int CORRECT_ON_MINUS = 27, CORRECT_OFF = 28, CORRECT_ON_NOT_EQ = 29;
        private const int PARAM_X = 8, PARAM_Y = 9, PARAM_DX = 10, PARAM_DY = 11, PARAM_RX = 12, PARAM_RY = 13, PARAM_ROT = 14;
        private const int COMP_MINUS = 15, COMP_PLUS = 16, COMP_EQUAL = 17;
        private const int CALC_WORK = 19, USE_WORK = 21, PARAM_SET = 22;

        private enum Mk { Curve, CurveDiv, Line, LineDiv, LineDst }

        private sealed class Mf
        {
            public Mk Kind; public int Apply, Wait, Target, Local, Start; public bool Valid = true;
            public readonly int[] W = new int[8];
        }

        private readonly List<PastCommand> _cmds;
        private int _pc, _wait, _guard;
        private bool _hold, _end, _request;
        private int _dx, _dy, _rx, _ry, _rot, _transX, _transY, _correctDy;
        private int _loopStart = -1, _loopCount, _loopMax;
        private readonly int[] _work = new int[8];
        private readonly List<Mf> _mfs = new List<Mf>();
        // palette fade (approximate visual): strength ramps to target over the fade's wait frames.
        private double _fade, _fadeTarget; private int _fadeFrames, _fadeWaitFrames; private bool _fadeWaiting;
        private byte _fadeR, _fadeG, _fadeB;

        /// <summary>X-mirror flag (PokeReverse in the source): when set, the X translation is negated so motion that
        /// pushes the mon "forward" goes the other way. The own-mon back sprite and some species are mirrored.</summary>
        public bool Reverse { get; set; }

        public PokeAnimPlayer(IEnumerable<PastCommand> cmds) { _cmds = new List<PastCommand>(cmds ?? Array.Empty<PastCommand>()); Reset(); }

        public void Reset()
        {
            _pc = 0; _wait = 0; _guard = 0; _hold = _end = _request = false;
            _dx = _dy = _rx = _ry = _rot = _transX = _transY = 0; _correctDy = 0;
            _loopStart = -1; _loopCount = _loopMax = 0;
            _mfs.Clear(); _fade = _fadeTarget = 0; _fadeWaiting = false;
            Array.Clear(_work, 0, _work.Length);
        }

        public bool Finished => _end;
        // Output transform for the current frame (mirrors ApplyTrans/ApplyAffine in p_anm_sys.c).
        // X: OrgX ± (TransX+dx) per the PokeReverse flag.  Y: OrgY + TransY + dy, plus the scale-anchor DY correction.
        public double OffsetX => Reverse ? -(_transX + _dx) : (_transX + _dx);
        public double OffsetY => _transY + _dy + DyCorrection();
        public double ScaleX => (256.0 + _rx) / 256.0;
        public double ScaleY => (256.0 + _ry) / 256.0;
        public double RotationDegrees => (((_rot % 65536) + 65536) % 65536) / 65536.0 * 360.0;
        public double FadeStrength => _fade < 0 ? 0 : (_fade > 1 ? 1 : _fade);
        public byte FadeR => _fadeR; public byte FadeG => _fadeG; public byte FadeB => _fadeB;

        /// <summary>Advances one 60 fps frame.</summary>
        public void Step()
        {
            if (_end) return;
            // The palette fade is driven by the soft-sprite system independently of the command interpreter, so it
            // keeps ramping even while the script is waiting.
            if (_fadeFrames > 0) { _fade += (_fadeTarget - _fade) / _fadeFrames; _fadeFrames--; }
            if (_wait > 0) { _wait--; return; }
            Execute();
        }

        private void Execute()
        {
            _request = false;

            // Tick active move-functions; when they've all finished, release any command hold.
            int invalid = 0;
            foreach (var mf in _mfs)
            {
                if (!mf.Valid) { invalid++; continue; }
                if (mf.Wait > 0) mf.Wait--;
                else StepMf(mf);
            }
            if (_mfs.Count == 0 || invalid == _mfs.Count) _hold = false;

            if (_hold) return;
            if (_fadeWaiting) { if (_fadeWaitFrames > 0) { _fadeWaitFrames--; return; } _fadeWaiting = false; }

            while (true)
            {
                if (_pc < 0 || _pc >= _cmds.Count) { _end = true; break; }
                int next = _pc + 1;
                RunCmd(_cmds[_pc], ref next);
                if (_end) break;
                _pc = next;
                if (_request || _hold) break;
                if (++_guard > 200000) { _end = true; break; }   // runaway guard
            }
        }

        private void RunCmd(PastCommand c, ref int next)
        {
            var a = c.Args;
            switch (c.Op)
            {
                case PastOp.End: _end = true; break;
                case PastOp.SetRequest: _request = true; break;
                case PastOp.SetDefault: _dx = _dy = _rx = _ry = _rot = _transX = _transY = 0; break;
                case PastOp.HoldCmd: _hold = true; break;
                case PastOp.SetWait: _wait = a.Length > 0 ? a[0] : 0; _request = true; break;
                case PastOp.SetDyCorrect: _correctDy = a.Length > 0 ? a[0] : 0; break;

                case PastOp.StartLoop: _loopStart = next; _loopMax = a.Length > 0 ? a[0] : 0; _loopCount = 0; break;
                case PastOp.EndLoop:
                    _loopCount++;
                    if (_loopMax > 0 && _loopCount < _loopMax && _loopStart >= 0) next = _loopStart;
                    else { _loopStart = -1; _loopCount = _loopMax = 0; }
                    break;

                case PastOp.CallMfCurve:        AddMf(Mk.Curve, a, targetWork: 1, paramNum: 6); break;
                case PastOp.CallMfCurveDivTime: AddMf(Mk.CurveDiv, a, targetWork: 1, paramNum: 6); break;
                case PastOp.CallMfLine:         AddMf(Mk.Line, a, targetWork: 0, paramNum: 4); break;
                case PastOp.CallMfLineDivTime:  AddMf(Mk.LineDiv, a, targetWork: 0, paramNum: 3); break;
                case PastOp.CallMfLineDst:      AddMf(Mk.LineDst, a, targetWork: 0, paramNum: 4); break;

                case PastOp.PaletteFade:
                    if (a.Length >= 4) StartFade(a[0], a[1], a[2], a[3]);
                    break;
                case PastOp.WaitPaletteFade:
                    if (_fadeFrames > 0) { _fadeWaiting = true; _fadeWaitFrames = _fadeFrames; _request = true; }
                    break;

                // ── Work-register math (used heavily by the back animations) ─────────────────────
                case PastOp.SetWorkVal: SetW(a, 0, a.Length > 1 ? a[1] : 0); break;
                case PastOp.CopyWorkVal: SetW(a, 0, GetW(a.Length > 1 ? a[1] : 0)); break;
                case PastOp.AddWorkVal: { (int v1, int v2) = AddMulOperands(a); SetW(a, 0, v1 + v2); break; }
                case PastOp.MulWorkVal: { (int v1, int v2) = AddMulOperands(a); SetW(a, 0, v1 * v2); break; }
                case PastOp.SubWorkVal: { (int v1, int v2) = SubDivOperands(a); SetW(a, 0, v1 - v2); break; }
                case PastOp.DivWorkVal: { (int v1, int v2) = SubDivOperands(a); SetW(a, 0, v2 == 0 ? 0 : v1 / v2); break; }
                case PastOp.ModWorkVal: { (int v1, int v2) = SubDivOperands(a); SetW(a, 0, v2 == 0 ? 0 : v1 % v2); break; }
                case PastOp.SetWorkValSin: SetW(a, 0, TrigWork(a, CURVE_SIN)); break;
                case PastOp.SetWorkValCos: SetW(a, 0, TrigWork(a, CURVE_COS)); break;
                case PastOp.SetIfWorkVal: RunSetIf(a); break;

                // ── Direct-set ops (feed the transform accumulators) ─────────────────────────────
                case PastOp.SetD:
                    if (a.Length >= 2) { int t = a[1], w = GetW(a[0]); if (t == PARAM_X || t == PARAM_DX) _dx = w; else if (t == PARAM_Y || t == PARAM_DY) _dy = w; }
                    break;
                case PastOp.SetTrans:
                    if (a.Length >= 2) { if (a[1] == PARAM_X) _transX = GetW(a[0]); else if (a[1] == PARAM_Y) _transY = GetW(a[0]); }
                    break;
                case PastOp.AddTrans:
                    if (a.Length >= 2) { if (a[1] == PARAM_X) _transX += GetW(a[0]); else if (a[1] == PARAM_Y) _transY += GetW(a[0]); }
                    break;
                case PastOp.SetAddParam:
                {
                    if (a.Length >= 4)
                    {
                        int v = a[1] == USE_WORK ? GetW(a[2]) : a[2];
                        bool set = a[3] == PARAM_SET;
                        AccSet(a[0], v, set);
                    }
                    break;
                }

                // ApplyTrans/ApplyAffine are implicit — the output reads the accumulators live. SET_VAL / ADD_VAL /
                // SET_ADD_VAL write less-common sprite params and are consumed (no transform effect for now).
                default: break;
            }
        }

        // Registers a move-function: args = [apply, wait, <paramNum work words>]; the target enum is in one of them.
        private void AddMf(Mk kind, int[] a, int targetWork, int paramNum)
        {
            var mf = new Mf { Kind = kind };
            mf.Apply = a.Length > 0 ? a[0] : APPLY_SET;
            mf.Wait = a.Length > 1 ? a[1] : 0;
            for (int i = 0; i < paramNum && i + 2 < a.Length; i++) mf.W[i] = a[i + 2];
            mf.Target = mf.W[targetWork];
            mf.Start = AccGet(mf.Target);
            _mfs.Add(mf);
            if (mf.Wait == 0) StepMf(mf);   // runs once on the registering frame (matches CallMoveFuc)
            else mf.Wait--;
        }

        private void StepMf(Mf mf)
        {
            switch (mf.Kind)
            {
                case Mk.Curve:
                {
                    int rad = mf.W[3] * (mf.W[6] + 1) + mf.W[4];
                    mf.Local = CurveVal(mf.W[0], rad, mf.W[2]);
                    ApplyMf(mf); if (++mf.W[6] >= mf.W[5]) mf.Valid = false;
                    break;
                }
                case Mk.CurveDiv:
                {
                    int div = mf.W[5] == 0 ? 1 : mf.W[5];
                    int rad = mf.W[3] * (mf.W[6] + 1) / div + mf.W[4];
                    mf.Local = CurveVal(mf.W[0], rad, mf.W[2]);
                    ApplyMf(mf); if (++mf.W[6] >= mf.W[5]) mf.Valid = false;
                    break;
                }
                case Mk.Line:
                {
                    mf.Local += mf.W[1] + mf.W[2] * mf.W[4];
                    ApplyMf(mf); if (++mf.W[4] >= mf.W[3]) mf.Valid = false;
                    break;
                }
                case Mk.LineDiv:
                {
                    int div = mf.W[2] == 0 ? 1 : mf.W[2];
                    mf.Local = (mf.W[3] + 1) * mf.W[1] / div;
                    ApplyMf(mf); if (++mf.W[3] >= mf.W[2]) mf.Valid = false;
                    break;
                }
                case Mk.LineDst:
                {
                    int move = mf.W[1] + mf.W[2] * mf.W[4];
                    mf.Local += move;
                    if (move < 0 ? mf.Local <= mf.W[3] : mf.Local >= mf.W[3]) { mf.Local = mf.W[3]; mf.Valid = false; }
                    ApplyMf(mf); mf.W[4]++;
                    break;
                }
            }
        }

        // ── Work-register helpers ───────────────────────────────────────────────────────────
        private int GetW(int idx) => _work[((idx % 8) + 8) % 8];
        private void SetW(int[] a, int dstArgIndex, int val) { if (dstArgIndex < a.Length) _work[((a[dstArgIndex] % 8) + 8) % 8] = val; }

        // ADD/MUL: [dst, calc, v1(work), v2(work-or-literal)].
        private (int, int) AddMulOperands(int[] a)
        {
            int v1 = GetW(a.Length > 2 ? a[2] : 0);
            int v2 = (a.Length > 1 && a[1] == CALC_WORK) ? GetW(a.Length > 3 ? a[3] : 0) : (a.Length > 3 ? a[3] : 0);
            return (v1, v2);
        }
        // SUB/DIV/MOD: [dst, calc1, calc2, v1, v2] — each operand work-or-literal.
        private (int, int) SubDivOperands(int[] a)
        {
            int v1 = (a.Length > 1 && a[1] == CALC_WORK) ? GetW(a.Length > 3 ? a[3] : 0) : (a.Length > 3 ? a[3] : 0);
            int v2 = (a.Length > 2 && a[2] == CALC_WORK) ? GetW(a.Length > 4 ? a[4] : 0) : (a.Length > 4 ? a[4] : 0);
            return (v1, v2);
        }
        // SET_WORK_VAL_SIN/COS: [dst, rad_idx, use1, l, use2, ofs].
        private int TrigWork(int[] a, int type)
        {
            int rad = GetW(a.Length > 1 ? a[1] : 0);
            int l = (a.Length > 2 && a[2] == USE_WORK) ? GetW(a.Length > 3 ? a[3] : 0) : (a.Length > 3 ? a[3] : 0);
            int ofs = (a.Length > 4 && a[4] == USE_WORK) ? GetW(a.Length > 5 ? a[5] : 0) : (a.Length > 5 ? a[5] : 0);
            return CurveVal(type, ((rad + ofs) % 65536 + 65536) % 65536, l);
        }
        // SET_IF_WORK_VAL: [use1, v1, v2, comp, use2, v3(dst), v4].
        private void RunSetIf(int[] a)
        {
            if (a.Length < 7) return;
            int a1 = GetW(a[1]);
            int a2 = a[0] == USE_WORK ? GetW(a[2]) : a[2];
            int result = a1 < a2 ? COMP_MINUS : a1 > a2 ? COMP_PLUS : COMP_EQUAL;
            if (a[3] != result) return;
            int val = a[4] == USE_WORK ? GetW(a[6]) : a[6];
            _work[((a[5] % 8) + 8) % 8] = val;
        }
        private void AccSet(int param, int v, bool set)
        {
            switch (param)
            {
                case PARAM_DX: _dx = set ? v : _dx + v; break;
                case PARAM_DY: _dy = set ? v : _dy + v; break;
                case PARAM_RX: _rx = set ? v : _rx + v; break;
                case PARAM_RY: _ry = set ? v : _ry + v; break;
                case PARAM_ROT: _rot = set ? v : _rot + v; break;
            }
        }

        // value = ±sin/cos(angle) × L, angle in NDS units (0x10000 = 360°).
        private static int CurveVal(int type, int rad, int l)
        {
            double ang = ((rad % 65536) + 65536) % 65536 / 65536.0 * 2.0 * Math.PI;
            double v = type == CURVE_SIN ? Math.Sin(ang)
                     : type == CURVE_COS ? Math.Cos(ang)
                     : type == CURVE_SIN_MINUS ? -Math.Sin(ang)
                     : type == CURVE_COS_MINUS ? -Math.Cos(ang) : 0.0;
            return (int)Math.Round(v * l);
        }

        // CorrectDy (ApplyAffine): when scaling, nudge POS_Y by -ry/8 so the sprite stays anchored (doesn't drift
        // off its platform). CORRECT_ON_MINUS only when shrinking (ry<0); CORRECT_ON_NOT_EQ whenever scaled.
        private int DyCorrection()
        {
            if (_correctDy == CORRECT_ON_MINUS) return _ry < 0 ? (-_ry) / 8 : 0;
            if (_correctDy == CORRECT_ON_NOT_EQ) return _ry != 0 ? (-_ry) / 8 : 0;
            return 0;
        }

        private int AccGet(int target) => target switch
        {
            TARGET_DX => _dx, TARGET_DY => _dy, TARGET_RX => _rx, TARGET_RY => _ry, TARGET_ROT => _rot, _ => 0
        };

        private void ApplyMf(Mf mf)
        {
            int cur = AccGet(mf.Target);
            int v = mf.Apply == APPLY_SET ? mf.Local : mf.Apply == APPLY_ADD ? mf.Start + mf.Local : cur + mf.Local;
            switch (mf.Target)
            {
                case TARGET_DX: _dx = v; break;
                case TARGET_DY: _dy = v; break;
                case TARGET_RX: _rx = v; break;
                case TARGET_RY: _ry = v; break;
                case TARGET_ROT: _rot = v; break;
            }
        }

        // PALETTE_FADE start_evy, end_evy, wait, rgb (NDS 15-bit BGR). Approximated as a colour overlay whose
        // strength ramps end_evy/16 over `wait` frames.
        private void StartFade(int startEvy, int endEvy, int wait, int rgb)
        {
            _fade = startEvy / 16.0;
            _fadeTarget = endEvy / 16.0;
            // The soft-sprite fade steps EVY by 1 every (wait+1) frames until it reaches end, so the visible
            // duration scales with both the EVY delta and the wait — not the wait alone.
            _fadeFrames = Math.Max(1, Math.Abs(endEvy - startEvy) * (wait + 1));
            _fadeR = (byte)((rgb & 0x1F) * 255 / 31);
            _fadeG = (byte)(((rgb >> 5) & 0x1F) * 255 / 31);
            _fadeB = (byte)(((rgb >> 10) & 0x1F) * 255 / 31);
        }
    }
}
