using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_FrenzyPlant: eight roots burst up in a line from the attacker to the defender, the
    /// near ones behind the Pokemon.</summary>
    public static class FrenzyPlant
    {
        private const int RootCount = 8, MoveDelay = 32, Lifetime = 160, BaseBgPriority = 1, EffectBgPriority = 3;

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            CellActor[] roots = new CellActor[RootCount];
            roots[0] = sprite;
            for (int i = 1; i < RootCount; i++)
            {
                roots[i] = sys.NewSprite(sprite);
                if (i % 2 != 0) roots[i].FlipH = true;
            }
            int attacker = sys.Attacker, defender = sys.Defender;
            // The roots spread evenly between the two, every other step of the line.
            PosLerp pos = new PosLerp(sys.MonX(attacker), sys.MonX(defender), sys.MonY(attacker), sys.MonY(defender), (RootCount + 2) * 2);
            bool skip = attacker == 1, active;
            int frame = 0, idx = 0;
            do
            {
                active = pos.Update();
                if (frame % 2 != 0)
                {
                    if (!skip) { if (idx < RootCount) roots[idx++].SetPosition(pos.X, pos.Y); }
                    else skip = false;
                }
                frame++;
            }
            while (active);
            int[] lifetime = new int[RootCount];
            bool playerSide = attacker == 0;
            for (int i = 0; i < RootCount; i++)
            {
                lifetime[i] = (RootCount - i) * 4;
                if (sys.Contest) { roots[i].Priority = EffectBgPriority; roots[i].OamPriority = i; continue; }
                roots[i].Priority = (playerSide ? i < 4 : i >= 4) ? EffectBgPriority : BaseBgPriority;
                roots[i].OamPriority = RootCount - i;
            }

            return () =>
            {
                int hidden = 0;
                for (int i = 0; i < RootCount; i++)
                {
                    if (++lifetime[i] < MoveDelay) continue;
                    SpriteVars.TickIfVisible(roots[i]);
                    if (lifetime[i] >= Lifetime) { roots[i].Visible = false; hidden++; }
                }
                if (hidden == RootCount)
                {
                    foreach (CellActor r in roots) sys.DeleteSprite(r);
                    return false;
                }
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
