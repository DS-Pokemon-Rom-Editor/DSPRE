using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_MetalClaw, also Crush Claw and Dragon Claw: two claws slash from each side, the
    /// right pair ten passes after the left.</summary>
    public static class MetalClaw
    {
        private const int OffsetX = 32, OffsetY = 32, RightClawDelay = 10, VisibleFrames = 40;

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            CellActor[] claws = new CellActor[4];
            int[] counters = new int[4];
            claws[0] = sprite;
            for (int i = 1; i < 4; i++) claws[i] = sys.NewSprite(sprite);
            // Left one, left two, right one, right two.
            claws[0].FlipH = claws[1].FlipH = true;
            claws[0].Offset(-OffsetX, 0);
            claws[1].Offset(-OffsetX, OffsetY);
            claws[2].Offset(OffsetX, 0);
            claws[3].Offset(OffsetX, OffsetY);
            int rightDelay = RightClawDelay;

            return () =>
            {
                if (rightDelay <= 0) { SpriteVars.TickIfVisible(claws[2]); SpriteVars.TickIfVisible(claws[3]); }
                else rightDelay--;
                SpriteVars.TickIfVisible(claws[0]);
                SpriteVars.TickIfVisible(claws[1]);
                int done = 0;
                for (int i = 0; i < 4; i++)
                    if (++counters[i] >= VisibleFrames) { claws[i].Visible = false; done++; }
                if (done == 4)
                {
                    foreach (CellActor c in claws) sys.DeleteSprite(c);
                    return false;
                }
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
