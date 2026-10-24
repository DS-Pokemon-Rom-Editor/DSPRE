using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_Burn, for the burned status: six flames flicker up in a row along the Pokemon's
    /// feet, two passes apart.</summary>
    public static class Burn
    {
        private const int FlameCount = 6, OffsetXStep = -12, BaseOffsetX = 40, BaseOffsetY = 40, DelayStep = 2, VisibleFrames = 16;

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            int attacker = sys.Attacker;
            int dir = sys.TransformDirectionX(attacker);
            (int x, int y) = sys.BattlerPos(attacker);
            CellActor[] flames = new CellActor[FlameCount];
            int[] visible = new int[FlameCount], delays = new int[FlameCount];
            // The game makes them from the last one back.
            for (int i = FlameCount - 1; i >= 0; i--)
            {
                CellActor f = flames[i] = i == FlameCount - 1 ? sprite : sys.NewSprite(sprite);
                f.Visible = false;
                f.SetSeq(i / 2);
                delays[i] = i * DelayStep;
                visible[i] = VisibleFrames;
                f.SetPosition(x + (BaseOffsetX + OffsetXStep * i) * dir, y + BaseOffsetY);
            }
            int state = 0;

            bool Update(int i)
            {
                if (delays[i] > 0)
                {
                    if (--delays[i] == 0) flames[i].Visible = true;
                }
                else if (visible[i] > 0) visible[i]--;
                else { flames[i].Visible = false; return true; }
                return false;
            }

            return () =>
            {
                if (state != 0)
                {
                    foreach (CellActor f in flames) sys.DeleteSprite(f);
                    return false;
                }
                // The game keeps only the last flame's result.
                bool done = false;
                for (int i = 0; i < FlameCount; i++) done = Update(i);
                if (done) state++;
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
