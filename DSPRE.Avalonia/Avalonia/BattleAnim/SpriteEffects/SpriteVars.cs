using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    internal static class SpriteVars
    {
        /// <summary>BattleAnimSystem_GetScriptVar: the values AddSpriteWithFunc passed, 0 past the end.</summary>
        public static int Get(CellActor sprite, int i) => i < sprite.Gp.Length ? sprite.Gp[i] : 0;

        // BattleAnimUtil_TickSpriteIfVisible.
        public static void TickIfVisible(CellActor sprite)
        {
            if (sprite.Visible) sprite.TickFrame();
        }

        // The task both OffsetAndAnimate and Fissure run: one step of the animation a pass until it ends.
        public static bool PlayOnce(IBattleAnimSystem sys, CellActor sprite)
        {
            if (!sprite.IsAnimated)
            {
                sys.DeleteSprite(sprite);
                return false;
            }
            sprite.TickFrame();
            sys.DrawSprites(sprite.Manager);
            return true;
        }
    }
}
