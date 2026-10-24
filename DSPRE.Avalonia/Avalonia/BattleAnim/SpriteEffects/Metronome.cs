using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_Metronome: a finger grows beside the attacker, wags five times and shrinks.</summary>
    public static class Metronome
    {
        private const int OffsetX = 40, OffsetY = 0, AffineOffsetX = -12, AffineOffsetY = 12;
        private const int StartScale = 1, EndScale = 10, RefScale = 10, ScaleFrames = 8, RotateFrames = 4, WaggleCount = 5;

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            int attacker = sys.Attacker;
            int dir = sys.TransformDirectionX(attacker);
            // ShouldBattlerSpriteBeFlipped is only true in a contest, for a species whose form is drawn symmetrical, which
            // the preview doesn't know.
            const int scaleDir = 1;
            int translationDir = dir;
            (int x, int y) = sys.BattlerPos(attacker);
            sprite.ScaleX = sprite.ScaleY = 1;
            sprite.SetPosition(x, y);
            sprite.Offset(OffsetX * dir, OffsetY);
            sprite.PivotX = AffineOffsetX * translationDir; sprite.PivotY = AffineOffsetY;
            sprite.SetSeq(attacker == 1 ? 1 : 0);
            ScaleLerp scale = new ScaleLerp(StartScale, RefScale, EndScale, ScaleFrames);
            ValueLerp angle;
            int nextEnd;
            if (translationDir >= 0)
            {
                angle = ValueLerp.Whole(BattleAnimMath.DegToIdx(359), BattleAnimMath.DegToIdx(320), RotateFrames);
                nextEnd = BattleAnimMath.DegToIdx(359);
            }
            else
            {
                angle = ValueLerp.Whole(BattleAnimMath.DegToIdx(0), BattleAnimMath.DegToIdx(40), RotateFrames);
                nextEnd = BattleAnimMath.DegToIdx(0);
            }
            int state = 0, counter = 0;

            return () =>
            {
                switch (state)
                {
                    case 0:
                        if (scale.Update()) { sprite.ScaleX = scale.AffineX * scaleDir; sprite.ScaleY = scale.AffineY; }
                        else { state++; counter = WaggleCount; scale = new ScaleLerp(EndScale, RefScale, StartScale, ScaleFrames); }
                        break;
                    case 1:
                    {
                        bool active = angle.Update();
                        sprite.SetRotationIdx(angle.Value);
                        if (!active)
                        {
                            if (counter > 0)
                            {
                                counter--;
                                int start = angle.Value, end = nextEnd;
                                nextEnd = angle.Value;
                                angle = ValueLerp.Whole(start, end, RotateFrames);
                            }
                            else state++;
                        }
                        break;
                    }
                    case 2:
                        if (scale.Update()) { sprite.ScaleX = scale.AffineX * scaleDir; sprite.ScaleY = scale.AffineY; }
                        else state++;
                        break;
                    default:
                        sys.DeleteSprite(sprite);
                        return false;
                }
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
