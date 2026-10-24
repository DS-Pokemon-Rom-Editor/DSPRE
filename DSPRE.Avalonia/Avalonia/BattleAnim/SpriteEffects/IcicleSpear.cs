using System;
using DSPRE.Avalonia.BattleAnim.Particles;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_IcicleSpear: one icicle arcs from the attacker to the defender, turning as it
    /// flies. Values: target offset x and y, frames, arc radius.</summary>
    public static class IcicleSpear
    {
        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            int attacker = sys.Attacker, defender = sys.Defender;
            int offX = SpriteVars.Get(sprite, 0), offY = SpriteVars.Get(sprite, 1), frames = SpriteVars.Get(sprite, 2), radius = SpriteVars.Get(sprite, 3);
            int dir = sys.TransformDirectionX(attacker);
            Parabolic arc = new Parabolic(sys.MonX(attacker), sys.MonX(defender) + offX * dir, sys.MonY(attacker), sys.MonY(defender) + offY * dir,
                frames, radius * SplFx.One);
            ValueLerp angle = dir > 0
                ? ValueLerp.Whole(BattleAnimMath.DegToIdx(20) * dir, BattleAnimMath.DegToIdx(130) * dir, 10)
                : ValueLerp.Whole(BattleAnimMath.DegToIdx(90) * dir, BattleAnimMath.DegToIdx(130) * dir, 10);
            sprite.SetRotationIdx(angle.Value);
            if (arc.Update()) sprite.SetPosition(arc.X, arc.Y);
            sprite.TickFrame();

            return () =>
            {
                if (!arc.Update())
                {
                    sys.DeleteSprite(sprite);
                    return false;
                }
                sprite.SetPosition(arc.X, arc.Y);
                if (angle.Update()) sprite.SetRotationIdx(angle.Value);
                sprite.TickFrame();
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
