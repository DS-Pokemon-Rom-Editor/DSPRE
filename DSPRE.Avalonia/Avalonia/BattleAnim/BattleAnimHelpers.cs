using System;
using DSPRE.Avalonia.BattleAnim.Particles;

namespace DSPRE.Avalonia.BattleAnim
{
    /// <summary>The movement contexts of the decomp's battle_anim_helpers.c, in its fixed point.</summary>
    public static class BattleAnimMath
    {
        public static ushort DegToIdx(double degrees) => (ushort)(int)(degrees * 0xFFFF / 360);

        public static int StepSize(int start, int end, int steps) => SplFx.Div(end - start, steps << 12);

        // BattleAnimUtil_GetRequiredSteps: a part step counts as a step, toward zero for a negative count.
        public static int RequiredSteps(int a, int b, int stepSize)
        {
            int steps = SplFx.Div(b - a, stepSize);
            int frac = Modf(steps, out steps);
            if (frac != 0) steps += SplFx.One;
            return Math.Abs(steps) >> 12;
        }

        // FX_Modf.
        public static int Modf(int x, out int whole)
        {
            if (x >= 0) { whole = x & 0x7FFFF000; return x & 0xFFF; }
            whole = -(-x & 0x7FFFF000);
            return -(-x & 0xFFF);
        }

        /// <summary>BattleAnimUtil_StepValue: moves the value toward the target by the step; true once it is there.</summary>
        public static bool StepValue(ref int value, int target, int step)
        {
            if (step < 0 ? value + step > target : value + step < target) { value += step; return false; }
            value = target;
            return true;
        }

        /// <summary>MON_AFFINE_SCALE(1): full size in a Pokemon sprite's scale attribute.</summary>
        public const int MonScaleOne = 0x100;

        private const int MonSpriteFrameHeight = 80;

        /// <summary>BattleAnimUtil_SetPokemonSpriteAnchoredPosition: the Y centre that keeps a Pokemon scaled by
        /// <paramref name="scaleY"/> (fixed point, MonScaleOne is full size) on the ground, or at the top.</summary>
        public static int AnchoredMonY(int y, int height, int scaleY, bool top = false)
        {
            int available = (MonSpriteFrameHeight - height * 2) * SplFx.One;
            int scaled = SplFx.Mul(available, scaleY);
            int relative = SplFx.Div(scaled, MonScaleOne * SplFx.One);
            int diff = available - relative;
            // Any fraction gets a half added, so the offset rounds.
            int frac = Modf(diff, out int whole);
            if (frac != 0) frac += SplFx.One / 2;
            int offset = (short)(((whole + frac) >> 12) / 2);
            if (top) { offset = -offset; y -= height; }
            return y + offset;
        }
    }

    /// <summary>PosLerpContext: a straight move from one point to another.</summary>
    public sealed class PosLerp
    {
        public int X, Y;
        private int _steps, _stepX, _stepY, _curX, _curY;

        public PosLerp(int sx, int ex, int sy, int ey, int steps)
        {
            X = (short)sx; Y = (short)sy; _steps = (ushort)steps;
            _stepX = BattleAnimMath.StepSize((short)sx * SplFx.One, (short)ex * SplFx.One, _steps);
            _stepY = BattleAnimMath.StepSize((short)sy * SplFx.One, (short)ey * SplFx.One, _steps);
            _curX = (short)sx * SplFx.One; _curY = (short)sy * SplFx.One;
        }

        public bool Update()
        {
            if (_steps == 0) return false;
            _curX += _stepX; _curY += _stepY;
            X = (short)(_curX >> 12); Y = (short)(_curY >> 12);
            _steps--;
            return true;
        }
    }

    /// <summary>RevolutionContext: a point going round an ellipse, angles as 16-bit indexes. Effects read and change
    /// its state between steps, as the game's do.</summary>
    public sealed class Revolution
    {
        public int X, Y;
        public int Steps, CurX, CurY, RadiusX, RadiusY, StepX, StepY;

        private Revolution() { }

        public Revolution(int sx, int ex, int sy, int ey, int rx, int ry, int steps)
        {
            Steps = steps; CurX = (ushort)sx; CurY = (ushort)sy; RadiusX = rx; RadiusY = ry;
            StepX = steps == 0 ? 0 : ((ushort)ex - (ushort)sx) / steps;
            StepY = steps == 0 ? 0 : ((ushort)ey - (ushort)sy) / steps;
        }

        /// <summary>RevolutionContext_InitWithStepSize: as many steps of the x step size as reach the end.</summary>
        public static Revolution WithStepSize(int sx, int ex, int sy, int ey, int rx, int ry, int stepSizeX)
        {
            sx = (ushort)sx; ex = (ushort)ex; sy = (ushort)sy; ey = (ushort)ey;
            int step = (short)(ushort)(sx > ex ? -stepSizeX : stepSizeX);
            Revolution r = new Revolution { CurX = sx, CurY = sy, RadiusX = rx, RadiusY = ry, StepX = step };
            r.Steps = BattleAnimMath.RequiredSteps(sx * SplFx.One, ex * SplFx.One, step * SplFx.One);
            r.StepY = r.Steps == 0 ? 0 : (ey - sy) / r.Steps;
            return r;
        }

        public Revolution Clone() => (Revolution)MemberwiseClone();

        public bool Update()
        {
            if (Steps == 0) return false;
            CurX = (CurX + StepX) & 0xFFFF; CurY = (CurY + StepY) & 0xFFFF;
            Steps--;
            X = (short)(SplFx.Mul(SplFx.Sin(CurX), RadiusX) >> 12);
            Y = (short)(SplFx.Mul(SplFx.Cos(CurY), RadiusY) >> 12);
            return true;
        }
    }

    /// <summary>ValueLerpContext: one value stepped in whole numbers, in fixed point, or along a cosine.</summary>
    public sealed class ValueLerp
    {
        private enum Kind { Whole, Fx, Cosine }
        private Kind _kind;
        public int Value;
        private int _steps, _step, _cur, _amplitude;

        private ValueLerp() { }

        public static ValueLerp Whole(int start, int end, int steps) => new ValueLerp
        { _kind = Kind.Whole, Value = start, _steps = steps, _step = BattleAnimMath.StepSize(start * SplFx.One, end * SplFx.One, steps) >> 12 };

        public static ValueLerp Fx(int start, int end, int steps) => new ValueLerp
        {
            _kind = Kind.Fx, Value = (short)start, _steps = steps, _cur = (short)start * SplFx.One,
            _step = BattleAnimMath.StepSize((short)start * SplFx.One, (short)end * SplFx.One, steps),
        };

        public static ValueLerp Sinusoidal(int start, int end, int amplitude, int steps) => new ValueLerp
        { _kind = Kind.Cosine, _steps = steps, _cur = (ushort)start, _amplitude = amplitude, _step = steps == 0 ? 0 : ((ushort)end - (ushort)start) / steps };

        public ValueLerp Clone() => (ValueLerp)MemberwiseClone();

        public bool Update()
        {
            if (_steps == 0) return false;
            switch (_kind)
            {
                case Kind.Whole: Value += _step; break;
                case Kind.Fx: _cur += _step; Value = _cur >> 12; break;
                default:
                    _cur = (_cur + _step) & 0xFFFF;
                    Value = SplFx.Mul(SplFx.Cos(_cur), _amplitude) >> 12;
                    break;
            }
            _steps--;
            return true;
        }
    }

    /// <summary>ScaleLerpContext: X and Y scales in MON_AFFINE_SCALE units, given as percentages of a reference.</summary>
    public sealed class ScaleLerp
    {
        public int X, Y;
        private int _steps, _stepX, _stepY, _curX, _curY;

        private static int Relative(int scale, int reference) => scale * BattleAnimMath.MonScaleOne / reference;

        public ScaleLerp(int start, int reference, int end, int steps) : this(start, end, start, end, reference, steps) { }

        public ScaleLerp(int sx, int ex, int sy, int ey, int reference, int steps)
        {
            _steps = steps;
            _stepX = BattleAnimMath.StepSize(Relative(sx, reference) * SplFx.One, Relative(ex, reference) * SplFx.One, steps);
            _stepY = BattleAnimMath.StepSize(Relative(sy, reference) * SplFx.One, Relative(ey, reference) * SplFx.One, steps);
            X = (short)Relative(sx, reference); Y = (short)Relative(sy, reference);
            _curX = X * SplFx.One; _curY = Y * SplFx.One;
        }

        public bool Update()
        {
            if (_steps == 0) return false;
            _steps--;
            _curX += _stepX; _curY += _stepY;
            X = (short)(_curX >> 12); Y = (short)(_curY >> 12);
            return true;
        }

        public int CurY => _curY;

        public ScaleLerp Clone() => (ScaleLerp)MemberwiseClone();

        /// <summary>ScaleLerpContext_GetAffineScale: the scale as a sprite's affine factor.</summary>
        public double AffineX => X / (double)BattleAnimMath.MonScaleOne;
        public double AffineY => Y / (double)BattleAnimMath.MonScaleOne;
    }

    /// <summary>ShakeContext: flips between the extent and its negative, through zero, every interval.</summary>
    public sealed class Shake
    {
        private const int MaxCyclesPerShake = 4;
        public int X, Y;
        private int _counter, _interval, _remaining, _cycle, _prevX, _prevY;

        public Shake(int extentX, int extentY, int interval, int amount)
        {
            _counter = interval; _interval = interval; _remaining = amount;
            _prevX = -(short)extentX; _prevY = -(short)extentY;
        }

        public bool Update()
        {
            if (_remaining == 0) return false;
            if (++_counter >= _interval)
            {
                _counter = 0;
                Flip(ref X, ref _prevX);
                Flip(ref Y, ref _prevY);
                if (++_cycle >= MaxCyclesPerShake) { _cycle = 0; _remaining--; }
            }
            return true;
        }

        private static void Flip(ref int cur, ref int prev)
        {
            int was = prev;
            prev = cur;
            cur = was == 0 ? 0 : (short)-was;
        }
    }

    /// <summary>XYTransformContext_InitParabolic: a straight move with a half-circle bump in y on top.</summary>
    public sealed class Parabolic
    {
        public readonly PosLerp Linear;
        private readonly Revolution _revs;

        public Parabolic(int sx, int ex, int sy, int ey, int frames, int arcRadius)
        {
            Linear = new PosLerp(sx, ex, sy, ey, frames);
            _revs = new Revolution(0, 0, BattleAnimMath.DegToIdx(90), BattleAnimMath.DegToIdx(270), 0, arcRadius, frames);
        }

        public int X => Linear.X;
        public int Y => Linear.Y;

        // The bump is added even on the pass that reports the end, as the game does.
        public bool Update()
        {
            bool a = Linear.Update(), b = _revs.Update();
            Linear.X += _revs.X; Linear.Y += _revs.Y;
            return a || b;
        }
    }
}
