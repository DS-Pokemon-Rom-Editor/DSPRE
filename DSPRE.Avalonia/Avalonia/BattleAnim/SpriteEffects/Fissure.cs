using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_Fissure: the crack opens under the defender, behind the Pokemon.</summary>
    public static class Fissure
    {
        private const int PlayerY = 126, EnemyY = 32, ContestX = 72, ContestY = 32, EffectBgPriority = 3;

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            int defender = sys.Defender;
            (int x, int _) = sys.BattlerPos(defender);
            bool player = defender == 0;
            if (sys.Contest) { sprite.SetPosition(ContestX, ContestY); sprite.SetSeq(0); }
            else { sprite.SetPosition(x, player ? PlayerY : EnemyY); sprite.SetSeq(player ? 1 : 0); }
            sprite.Priority = EffectBgPriority;
            return () => SpriteVars.PlayOnce(sys, sprite);
        }
    }
}
