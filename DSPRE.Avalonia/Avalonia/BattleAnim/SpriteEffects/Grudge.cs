using System;
using DSPRE.Avalonia.BattleAnim.Particles;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_Grudge: six flames fade in circling the attacker, passing behind it on the far
    /// side, and fade out.</summary>
    public static class Grudge
    {
        private const int Count = 6, RevFrames = 48, StartAlpha = 1, EndAlpha = 16, MaxAlpha = 16, FadeFrames = 10, RevXScale = 5;

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            int attacker = sys.Attacker;
            int attackerX = (short)sys.MonX(attacker), attackerY = (short)sys.MonY(attacker);
            CellActor[] flames = new CellActor[Count];
            for (int i = 0; i < Count; i++)
            {
                flames[i] = i == 0 ? sprite : sys.NewSprite(sprite);
                flames[i].Xlu = true;
                flames[i].Animate = true;
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
                    ushort angle = (ushort)(revs[i].CurX * RevXScale);
                    short offsetY = (short)(SplFx.Mul(SplFx.Sin(angle), 10 * SplFx.One * dir) >> 12);
                    flames[i].SetPosition(attackerX + revs[i].X, attackerY + offsetY);
                    // The Pokemon sit at priority 1; one more puts the flame behind them.
                    flames[i].Priority = revs[i].CurX >= behindMin && revs[i].CurX <= behindMax ? 1 : 2;
                }
            }

            return () =>
            {
                switch (state)
                {
                    case 0:
                    {
                        sys.SetBlendAlpha(StartAlpha, MaxAlpha - StartAlpha);
                        alpha = sys.StartAlphaFade(StartAlpha, EndAlpha, MaxAlpha - StartAlpha, MaxAlpha - EndAlpha, FadeFrames);
                        int angleStep = BattleAnimMath.DegToIdx(360) / Count;
                        for (int i = 0; i < Count; i++)
                        {
                            revs[i] = new Revolution(0, BattleAnimMath.DegToIdx(180), 0, 0, 50 * SplFx.One, 0, RevFrames);
                            revs[i].CurX += angleStep * i;
                            revs[i].StepX *= dir;
                        }
                        Revolve();
                        state++;
                        break;
                    }
                    case 1:
                        Revolve();
                        if (alpha.Done) { state++; delay = RevFrames - 2 * FadeFrames; }
                        break;
                    case 2:
                        Revolve();
                        if (--delay < 0)
                        {
                            state++;
                            alpha = sys.StartAlphaFade(EndAlpha, StartAlpha, MaxAlpha - EndAlpha, MaxAlpha - StartAlpha, FadeFrames);
                        }
                        break;
                    case 3:
                        Revolve();
                        if (alpha.Done) state++;
                        break;
                    default:
                        foreach (CellActor f in flames) sys.DeleteSprite(f);
                        return false;
                }
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
