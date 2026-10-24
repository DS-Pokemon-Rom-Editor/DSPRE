using System;
using DSPRE.Avalonia.BattleAnim.Particles;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_ConfusionStatus, for the confused status: six orbs fade in circling the Pokemon,
    /// passing behind it on the far side, and fade out.</summary>
    public static class ConfusionStatus
    {
        private const int Count = 6, DefaultAlpha = 16, StartAlpha = 1, EndAlpha = 16, FadeFrames = 10, RevFrames = 48;

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            int attacker = sys.Attacker;
            int monX = (short)sys.MonX(attacker), monY = (short)sys.MonY(attacker);
            CellActor[] orbs = new CellActor[Count];
            for (int i = 0; i < Count; i++)
            {
                orbs[i] = i == 0 ? sprite : sys.NewSprite(sprite);
                orbs[i].Xlu = true;
                orbs[i].Animate = true;
            }
            int dir = sys.TransformDirectionX(attacker);
            Revolution[] revs = new Revolution[Count];
            int behindMin = BattleAnimMath.DegToIdx(90), behindMax = BattleAnimMath.DegToIdx(269);
            int state = 0, delay = 0;
            AlphaFade alpha = null;

            void Revolve()
            {
                for (int i = 0; i < Count; i++)
                {
                    revs[i].Update();
                    orbs[i].SetPosition(monX + revs[i].X, monY);
                    // The Pokemon sit at priority 1; one more puts the orb behind them.
                    orbs[i].Priority = revs[i].CurX >= behindMin && revs[i].CurX <= behindMax ? 1 : 2;
                }
            }

            return () =>
            {
                switch (state)
                {
                    case 0:
                    {
                        sys.SetBlendAlpha(StartAlpha, DefaultAlpha - StartAlpha);
                        alpha = sys.StartAlphaFade(StartAlpha, EndAlpha, DefaultAlpha - StartAlpha, DefaultAlpha - EndAlpha, FadeFrames);
                        int step = BattleAnimMath.DegToIdx(360) / Count;
                        for (int i = 0; i < Count; i++)
                        {
                            revs[i] = new Revolution(0, BattleAnimMath.DegToIdx(180), 0, 0, 50 * SplFx.One, 0, RevFrames);
                            revs[i].CurX += step * i;
                            revs[i].StepX *= dir;
                        }
                        Revolve();
                        state++;
                        break;
                    }
                    case 1:
                        Revolve();
                        if (alpha.Done) { state++; delay = RevFrames - FadeFrames * 2; }
                        break;
                    case 2:
                        Revolve();
                        if (--delay < 0)
                        {
                            state++;
                            alpha = sys.StartAlphaFade(EndAlpha, StartAlpha, DefaultAlpha - EndAlpha, DefaultAlpha - StartAlpha, FadeFrames);
                        }
                        break;
                    case 3:
                        Revolve();
                        if (alpha.Done) state++;
                        break;
                    default:
                        foreach (CellActor o in orbs) sys.DeleteSprite(o);
                        return false;
                }
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
