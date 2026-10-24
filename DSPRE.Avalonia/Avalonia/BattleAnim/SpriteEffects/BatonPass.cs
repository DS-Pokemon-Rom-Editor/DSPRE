using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_BatonPass: the ball opens, the attacker shrinks into it, and the ball closes and
    /// flies off the top.</summary>
    public static class BatonPass
    {
        private const int AnimFrames = 8, BaseScale = 10, EndScale = 0;

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            int attacker = sys.Attacker;
            sys.SetMonHidden(attacker, false);
            (int attackerX, int attackerY) = sys.BattlerPos(attacker);
            sprite.SetPosition(attackerX, attackerY);
            int state = 0;
            ScaleLerp scale = null;
            PosLerp pos = null, ballPos = null;

            return () =>
            {
                switch (state)
                {
                    case 0:
                        sprite.Animate = true;
                        state++;
                        break;
                    case 1:
                        if (!sprite.IsAnimated)
                        {
                            sprite.Animate = false;
                            state++;
                            scale = new ScaleLerp(BaseScale, BaseScale, EndScale, AnimFrames);
                            pos = new PosLerp(sys.MonX(attacker), attackerX, sys.MonY(attacker), attackerY, AnimFrames);
                        }
                        break;
                    case 2:
                        if (pos.Update()) { sys.SetMonX(attacker, pos.X); sys.SetMonY(attacker, pos.Y); }
                        if (scale.Update()) sys.SetMonScale(attacker, scale.X, scale.Y);
                        else
                        {
                            sys.SetMonHidden(attacker, true);
                            sys.SetMonScale(attacker, BattleAnimMath.MonScaleOne, BattleAnimMath.MonScaleOne);
                            sprite.SetSeq(1);
                            sprite.AnimSpeed = 1;
                            sprite.Animate = true;
                            state++;
                        }
                        break;
                    case 3:
                        if (!sprite.IsAnimated) { sprite.Animate = false; state++; ballPos = new PosLerp(0, 0, attackerY, 0, AnimFrames); }
                        break;
                    case 4:
                        if (ballPos.Update()) sprite.SetPosition(attackerX, ballPos.Y);
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
