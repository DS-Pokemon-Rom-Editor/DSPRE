using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>Sprite function 0, BattleAnimSpriteFunc_SpriteExample: three copies drift right, left and down-right.
    /// Its task never reaches its end state, so in the game it runs until the battle frees it.</summary>
    public static class SpriteExample
    {
        private const int Priority = 0x1000;

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            CellActor[] sprites = { sprite, sys.NewSprite(sprite), sys.NewSprite(sprite) };
            sys.StartTask(() =>
            {
                foreach (CellActor s in sprites) s.TickFrame();
                sprites[0].Offset(1, 0);
                sprites[1].Offset(-1, 0);
                sprites[2].Offset(1, 1);
                sys.DrawSprites(sprite.Manager);
                return true;
            }, Priority);
            return null;
        }
    }
}
