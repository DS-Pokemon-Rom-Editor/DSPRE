using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_FollowMe: the finger sways round in front of and behind the defender, then tilts
    /// back and forth.</summary>
    public static class FollowMe
    {
        private const int SwitchPrio = 0, End = 255, TiltFrames = 4, MonPriority = 1, EffectBgPriority = 3;

        // sFollowMeSpriteOffsets: x steps, with 0 swapping the sprite to the other side of the Pokemon.
        private static readonly short[] Offsets =
        {
            5, 5, 5, 5, 4, 4, 4, 3, 3, 3, SwitchPrio, -3, -3, -3, -4, -4, -4, -5, -5, -5, -5,
            -5, -5, -5, -5, -5, -4, -4, -4, -3, -3, -3, SwitchPrio, 3, 3, 3, 4, 4, 4, 5, 5, 5, 5, End,
        };

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            (int x, int y) = sys.BattlerPos(sys.Defender);
            sprite.SetPosition(x, y);
            sprite.SetSeq(sys.Attacker == 1 ? 1 : 0);
            sprite.Priority = MonPriority;
            sprite.Offset(0, 12);
            int tilt = BattleAnimMath.DegToIdx(20);
            int state = 0, step = 0;
            ValueLerp angle = null;

            return () =>
            {
                switch (state)
                {
                    case 0:
                    {
                        short offsetX = Offsets[step++];
                        if (offsetX != End)
                        {
                            if (offsetX == SwitchPrio) sprite.Priority = sprite.Priority == MonPriority ? EffectBgPriority : MonPriority;
                            sprite.Offset(offsetX, 0);
                        }
                        else
                        {
                            angle = ValueLerp.Whole(0, tilt, TiltFrames);
                            sprite.PivotX = -8; sprite.PivotY = 16;
                            step = 0;
                            state++;
                        }
                        break;
                    }
                    case 1:
                        if (angle.Update()) sprite.SetRotationIdx(angle.Value);
                        else if (step > 5) state++;
                        else
                        {
                            step++;
                            angle = step switch
                            {
                                1 or 3 => ValueLerp.Whole(tilt, -tilt, TiltFrames),
                                2 or 4 => ValueLerp.Whole(-tilt, tilt, TiltFrames),
                                5 => ValueLerp.Whole(tilt, 0, TiltFrames / 2),
                                _ => angle,
                            };
                        }
                        break;
                    default:
                        sys.DeleteSprite(sprite);
                        return false;
                }
                sprite.TickFrame();
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
