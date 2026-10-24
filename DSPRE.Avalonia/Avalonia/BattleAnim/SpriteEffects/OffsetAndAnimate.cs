using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_OffsetAndAnimate: moves the sprite by the given x and y and plays its animation
    /// once.</summary>
    public static class OffsetAndAnimate
    {
        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            sprite.Offset((short)SpriteVars.Get(sprite, 0), (short)SpriteVars.Get(sprite, 1));
            return () => SpriteVars.PlayOnce(sys, sprite);
        }
    }
}
