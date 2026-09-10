using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Runs one Pokémon movement script the way the battle engine does, one game tick per <see cref="Step"/>.
    /// The outputs are the sprite's transform, which only changes when the script applies its working values.
    /// </summary>
    public sealed class PokeAnimPlayer
    {
        private const int TARGET_DX = 35, TARGET_DY = 36, TARGET_RX = 37, TARGET_RY = 38, TARGET_ROT = 39;
        private const int CURVE_SIN = 30, CURVE_COS = 31, CURVE_SIN_MINUS = 32, CURVE_COS_MINUS = 33;
        private const int APPLY_SET = 24, APPLY_ADD = 25, APPLY_SYNTHE = 26;
        private const int CORRECT_ON_MINUS = 27, CORRECT_OFF = 28, CORRECT_ON_NOT_EQ = 29;
        private const int PARAM_X = 8, PARAM_Y = 9, PARAM_DX = 10, PARAM_DY = 11, PARAM_RX = 12, PARAM_RY = 13, PARAM_ROT = 14;
        private const int COMP_MINUS = 15, COMP_PLUS = 16, COMP_EQUAL = 17;
        private const int CALC_WORK = 19, USE_WORK = 21, PARAM_SET = 22;
        private const int SPRITE_X = 0, SPRITE_Y = 1, SPRITE_ROT = 9, SPRITE_PIVOT_X = 10, SPRITE_SCALE_X = 12, SPRITE_SCALE_Y = 13;
        private const int MaxMoveFuncs = 4, MaxCommandsPerTick = 256;

        private enum Mk { Curve, CurveDiv, Line, LineDiv, LineDst }

        private sealed class Mf
        {
            public Mk Kind; public int Apply, Wait, Target, Local, Start; public bool Valid = true;
            public readonly int[] W = new int[8];
        }

        // FX_SinCosTable_: 4096 steps per turn in 20.12 fixed point, which is round(sin × 4096) at every step.
        private static readonly int[] Sin = BuildTable(Math.Sin), Cos = BuildTable(Math.Cos);
        private static int[] BuildTable(Func<double, double> f)
        {
            var t = new int[4096];
            for (int i = 0; i < t.Length; i++) t[i] = (int)Math.Round(f(i * 2.0 * Math.PI / 4096) * 4096);
            return t;
        }

        private readonly List<PastCommand> _cmds;
        private readonly int _startDelay;
        private readonly Mf[] _mfs = new Mf[MaxMoveFuncs];
        private int _pc, _wait;
        private bool _hold, _end, _request, _fadeWaiting;
        private int _dx, _dy, _rx, _ry, _rot, _transX, _transY, _correctDy;
        private int _spriteX, _spriteY, _spriteScaleX, _spriteScaleY, _spriteRot, _spritePivotX;
        private int _loopStart = -1, _loopCount, _loopMax;
        private readonly int[] _work = new int[8];
        private bool _fadeActive;
        private int _fadeEvy, _fadeTargetEvy, _fadeCounter, _fadeDelay, _fadeShownEvy;
        private byte _fadeR, _fadeG, _fadeB;

        /// <summary>X-mirror flag: when set, applied X translation is negated. Battle leaves it off.</summary>
        public bool Reverse { get; set; }

        /// <param name="startDelay">Ticks to wait before the first command, the sprite record's start delay.</param>
        public PokeAnimPlayer(IEnumerable<PastCommand> cmds, int startDelay = 0)
        {
            _cmds = new List<PastCommand>(cmds ?? Array.Empty<PastCommand>());
            _startDelay = Math.Max(0, startDelay);
            Reset();
        }

        public void Reset()
        {
            _pc = 0; _wait = _startDelay; _hold = _end = _request = _fadeWaiting = false;
            _dx = _dy = _rx = _ry = _rot = _transX = _transY = 0; _correctDy = CORRECT_OFF;
            SetDefault();
            _loopStart = -1; _loopCount = _loopMax = 0;
            Array.Clear(_mfs, 0, _mfs.Length);
            Array.Clear(_work, 0, _work.Length);
            _fadeActive = false; _fadeEvy = _fadeTargetEvy = _fadeCounter = _fadeDelay = _fadeShownEvy = 0;
        }

        public bool Finished => _end;

        /// <summary>A palette fade is stepped by the sprite, not the script, so it can outlive the script.</summary>
        public bool Active => !_end || _fadeActive;

        public double OffsetX => _spriteX;
        public double OffsetY => _spriteY;
        public double ScaleX => _spriteScaleX / 256.0;
        public double ScaleY => _spriteScaleY / 256.0;
        public double RotationDegrees => (_spriteRot & 0xFFFF) / 65536.0 * 360.0;
        /// <summary>Horizontal offset of the rotation centre from the sprite centre.</summary>
        public double PivotX => _spritePivotX;
        public double FadeStrength => Math.Clamp(_fadeShownEvy / 16.0, 0, 1);
        public byte FadeR => _fadeR; public byte FadeG => _fadeG; public byte FadeB => _fadeB;

        /// <summary>Advances one game tick.</summary>
        public void Step()
        {
            if (!_end)
            {
                if (_wait > 0) _wait--;
                else Execute();
            }
            StepFade();
        }

        private void Execute()
        {
            _request = false;

            int invalid = 0;
            foreach (var mf in _mfs)
            {
                if (mf == null || !mf.Valid) { invalid++; continue; }
                if (mf.Wait > 0) mf.Wait--;
                else StepMf(mf);
            }
            if (invalid == MaxMoveFuncs) _hold = false;

            if (_hold) { ApplyTrans(); ApplyAffine(); return; }
            if (_fadeWaiting) { if (_fadeActive) return; _fadeWaiting = false; }

            for (int count = 1; ; count++)
            {
                // A script edited without a closing End would otherwise run off its end.
                if (_pc < 0 || _pc >= _cmds.Count) { RunEnd(); break; }
                int next = _pc + 1;
                RunCmd(_cmds[_pc], ref next);
                if (_end) break;
                _pc = next;
                if (_request) break;
                if (_hold) { ApplyTrans(); ApplyAffine(); break; }
                if (count >= MaxCommandsPerTick) { _end = true; break; }
            }
        }

        private void RunCmd(PastCommand c, ref int next)
        {
            var a = c.Args;
            switch (c.Op)
            {
                case PastOp.End: RunEnd(); break;
                case PastOp.SetRequest: _request = true; break;
                case PastOp.SetDefault: SetDefault(); break;
                case PastOp.HoldCmd: _hold = true; break;
                case PastOp.SetWait: _wait = Arg(a, 0); _request = true; break;
                case PastOp.SetDyCorrect: _correctDy = Arg(a, 0) & 0xFF; break;

                case PastOp.StartLoop: _loopStart = next; _loopMax = Arg(a, 0); _loopCount = 0; break;
                case PastOp.EndLoop:
                    _loopCount++;
                    if (_loopCount < _loopMax && _loopStart >= 0) next = _loopStart;
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
                    if (_fadeActive) { _fadeWaiting = true; _request = true; }
                    break;

                case PastOp.SetWorkVal: SetW(a, 0, Arg(a, 1)); break;
                case PastOp.CopyWorkVal: SetW(a, 0, GetW(Arg(a, 1))); break;
                case PastOp.AddWorkVal: { (int v1, int v2) = AddMulOperands(a); SetW(a, 0, v1 + v2); break; }
                case PastOp.MulWorkVal: { (int v1, int v2) = AddMulOperands(a); SetW(a, 0, v1 * v2); break; }
                case PastOp.SubWorkVal: { (int v1, int v2) = SubDivOperands(a); SetW(a, 0, v1 - v2); break; }
                case PastOp.DivWorkVal: { (int v1, int v2) = SubDivOperands(a); SetW(a, 0, v2 == 0 ? 0 : v1 / v2); break; }
                case PastOp.ModWorkVal: { (int v1, int v2) = SubDivOperands(a); SetW(a, 0, v2 == 0 ? 0 : v1 % v2); break; }
                case PastOp.SetWorkValSin: SetW(a, 0, TrigWork(a, Sin)); break;
                case PastOp.SetWorkValCos: SetW(a, 0, TrigWork(a, Cos)); break;
                case PastOp.SetIfWorkVal: RunSetIf(a); break;

                case PastOp.SetVal: SpriteAttr(Arg(a, 0), GetW(Arg(a, 1)), set: true); break;
                case PastOp.AddVal: SpriteAttr(Arg(a, 0), GetW(Arg(a, 1)), set: false); break;
                case PastOp.SetAddVal:
                    if (a.Length >= 4) SpriteAttr(a[0], a[1] == USE_WORK ? GetW(a[2]) : a[2], set: a[3] == PARAM_SET);
                    break;

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
                    if (a.Length >= 4) AccSet(a[0], a[1] == USE_WORK ? GetW(a[2]) : a[2], a[3] == PARAM_SET);
                    break;
                case PastOp.ApplyTrans: ApplyTrans(); break;
                case PastOp.ApplyAffine: ApplyAffine(); break;
            }
        }

        // End puts the sprite back where it started before stopping, so each sprite settles on its own End.
        private void RunEnd() { SetDefault(); _request = true; _end = true; }

        private void SetDefault()
        {
            _spriteX = _spriteY = 0;
            _spriteRot = 0; _spritePivotX = 0;
            _spriteScaleX = _spriteScaleY = 0x100;
        }

        private void ApplyTrans()
        {
            _spriteX = Reverse ? -(_transX + _dx) : _transX + _dx;
            _spriteY = _transY + _dy;
        }

        // Adds to the current Y, so applying scale without translation drifts a little each tick, as in game.
        private void ApplyAffine()
        {
            _spriteScaleX = 0x100 + _rx;
            _spriteScaleY = 0x100 + _ry;
            _spriteRot = (ushort)_rot;
            if ((_correctDy == CORRECT_ON_MINUS && _ry < 0) || (_correctDy == CORRECT_ON_NOT_EQ && _ry != 0))
                _spriteY += (-_ry) / 8;
        }

        // The preview does not know the sprite's screen origin, so absolute position sets are left out.
        private void SpriteAttr(int attr, int v, bool set)
        {
            switch (attr)
            {
                case SPRITE_X: if (!set) _spriteX += v; break;
                case SPRITE_Y: if (!set) _spriteY += v; break;
                case SPRITE_ROT: _spriteRot = set ? v : _spriteRot + v; break;
                case SPRITE_PIVOT_X: _spritePivotX = set ? v : _spritePivotX + v; break;
                case SPRITE_SCALE_X: _spriteScaleX = set ? v : _spriteScaleX + v; break;
                case SPRITE_SCALE_Y: _spriteScaleY = set ? v : _spriteScaleY + v; break;
            }
        }

        // Registers a move-function: args = [apply, wait, <paramNum work words>]; the target enum is in one of them.
        private void AddMf(Mk kind, int[] a, int targetWork, int paramNum)
        {
            int slot = Array.FindIndex(_mfs, m => m == null || !m.Valid);
            if (slot < 0) return;   // the game has four slots and asserts past them

            var mf = new Mf { Kind = kind };
            mf.Apply = Arg(a, 0) & 0xFF;
            mf.Wait = Arg(a, 1) & 0xFF;
            for (int i = 0; i < paramNum && i + 2 < a.Length; i++) mf.W[i] = a[i + 2];
            mf.Target = mf.W[targetWork];
            mf.Start = AccGet(mf.Target);
            _mfs[slot] = mf;
            if (mf.Wait == 0) StepMf(mf);
            else mf.Wait--;
        }

        private void StepMf(Mf mf)
        {
            var w = mf.W;
            switch (mf.Kind)
            {
                case Mk.Curve:
                    mf.Local = CurveVal(w[0], (ushort)(w[3] * (w[6] + 1) + w[4]), w[2]);
                    ApplyMf(mf); if (++w[6] >= w[5]) mf.Valid = false;
                    break;
                case Mk.CurveDiv:
                    mf.Local = CurveVal(w[0], (ushort)(w[3] * (w[6] + 1) / (w[5] == 0 ? 1 : w[5]) + w[4]), w[2]);
                    ApplyMf(mf); if (++w[6] >= w[5]) mf.Valid = false;
                    break;
                case Mk.Line:
                    mf.Local += w[1] + w[2] * w[4];
                    ApplyMf(mf); if (++w[4] >= w[3]) mf.Valid = false;
                    break;
                case Mk.LineDiv:
                    mf.Local = (w[3] + 1) * w[1] / (w[2] == 0 ? 1 : w[2]);
                    ApplyMf(mf); if (++w[3] >= w[2]) mf.Valid = false;
                    break;
                case Mk.LineDst:
                {
                    int move = w[1] + w[2] * w[4];
                    mf.Local += move;
                    if (mf.Apply == APPLY_ADD)
                    {
                        // With ADD the bound applies to the start value plus the distance, not the distance.
                        int reached = mf.Start + mf.Local;
                        if (move < 0 ? reached <= w[3] : reached >= w[3]) { mf.Local += w[3] - reached; mf.Valid = false; }
                    }
                    else if (move < 0 ? mf.Local <= w[3] : mf.Local >= w[3]) { mf.Local = w[3]; mf.Valid = false; }
                    ApplyMf(mf); w[4]++;
                    break;
                }
            }
        }

        private static int Arg(int[] a, int i) => i < a.Length ? a[i] : 0;
        private int GetW(int idx) => _work[((idx % 8) + 8) % 8];
        private void SetW(int[] a, int dstArgIndex, int val) { if (dstArgIndex < a.Length) _work[((a[dstArgIndex] % 8) + 8) % 8] = val; }

        // ADD/MUL: [dst, calc, v1(work), v2(work-or-literal)].
        private (int, int) AddMulOperands(int[] a)
        {
            int v1 = GetW(Arg(a, 2));
            int v2 = Arg(a, 1) == CALC_WORK ? GetW(Arg(a, 3)) : Arg(a, 3);
            return (v1, v2);
        }
        // SUB/DIV/MOD: [dst, calc1, calc2, v1, v2], each operand work-or-literal.
        private (int, int) SubDivOperands(int[] a)
        {
            int v1 = Arg(a, 1) == CALC_WORK ? GetW(Arg(a, 3)) : Arg(a, 3);
            int v2 = Arg(a, 2) == CALC_WORK ? GetW(Arg(a, 4)) : Arg(a, 4);
            return (v1, v2);
        }
        // SET_WORK_VAL_SIN/COS: [dst, rad_idx, use1, l, use2, ofs].
        private int TrigWork(int[] a, int[] table)
        {
            int rad = GetW(Arg(a, 1));
            int l = Arg(a, 2) == USE_WORK ? GetW(Arg(a, 3)) : Arg(a, 3);
            int ofs = Arg(a, 4) == USE_WORK ? GetW(Arg(a, 5)) : Arg(a, 5);
            return (table[((rad + ofs) & 0xFFFF) >> 4] * l) >> 12;
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
                case PARAM_X: _transX = set ? v : _transX + v; break;
                case PARAM_Y: _transY = set ? v : _transY + v; break;
                case PARAM_DX: _dx = set ? v : _dx + v; break;
                case PARAM_DY: _dy = set ? v : _dy + v; break;
                case PARAM_RX: _rx = set ? v : _rx + v; break;
                case PARAM_RY: _ry = set ? v : _ry + v; break;
                case PARAM_ROT: _rot = set ? v : _rot + v; break;
            }
        }

        // ±sin/cos(angle) × L in the game's fixed point: the product is shifted down (floored) before the sign.
        private static int CurveVal(int type, int rad, int l)
        {
            int idx = (rad & 0xFFFF) >> 4;
            return type switch
            {
                CURVE_SIN => (Sin[idx] * l) >> 12,
                CURVE_COS => (Cos[idx] * l) >> 12,
                CURVE_SIN_MINUS => -((Sin[idx] * l) >> 12),
                CURVE_COS_MINUS => -((Cos[idx] * l) >> 12),
                _ => 0,
            };
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

        // PALETTE_FADE start_evy, end_evy, wait, rgb (15-bit BGR). The sprite blends at the current strength,
        // then moves one step toward the end every (wait + 1) ticks, and stops once it has shown the end.
        private void StartFade(int startEvy, int endEvy, int wait, int rgb)
        {
            _fadeActive = true;
            _fadeEvy = startEvy & 0xFF;
            _fadeTargetEvy = endEvy & 0xFF;
            _fadeCounter = 0;
            _fadeDelay = wait & 0xFF;
            _fadeR = (byte)((rgb & 0x1F) * 255 / 31);
            _fadeG = (byte)(((rgb >> 5) & 0x1F) * 255 / 31);
            _fadeB = (byte)(((rgb >> 10) & 0x1F) * 255 / 31);
        }

        private void StepFade()
        {
            if (!_fadeActive) return;
            if (_fadeCounter > 0) { _fadeCounter--; return; }
            _fadeCounter = _fadeDelay;
            _fadeShownEvy = _fadeEvy;
            if (_fadeEvy == _fadeTargetEvy) _fadeActive = false;
            else if (_fadeEvy > _fadeTargetEvy) _fadeEvy--;
            else _fadeEvy++;
        }
    }
}
