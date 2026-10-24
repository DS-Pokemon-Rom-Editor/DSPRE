using System;
using DSPRE.Avalonia.BattleAnim.Particles;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_Torment: speech bubbles pop out round the attacker in pairs while it squashes and
    /// stretches, then all six play again.</summary>
    public static class Torment
    {
        private const int BubbleCount = 6, ScaleSteps = 2, BaseScale = 10, SmallX = 9, SmallY = 8, LargeX = 11, LargeY = 15;
        private const int AnimSpeed = 2, InbetweenDelay = 8;

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            int attacker = sys.Attacker;
            int attackerX = (short)sys.MonX(attacker), attackerY = (short)sys.MonY(attacker);
            int height = sys.MonYOffset(attacker);
            CellActor[] bubbles = new CellActor[BubbleCount];
            bool[] done = new bool[BubbleCount];
            int[] endDelays = new int[BubbleCount];
            for (int i = 0; i < BubbleCount; i++)
            {
                CellActor b = bubbles[i] = i == 0 ? sprite : sys.NewSprite(sprite);
                b.Visible = false;
                int angle = BattleAnimMath.DegToIdx(90) / (BubbleCount / 2) * (i / 2);
                short ox = (short)(SplFx.Mul(SplFx.Cos(angle), 48 * SplFx.One) >> 12);
                short oy = (short)(SplFx.Mul(SplFx.Sin(angle), 48 * SplFx.One) >> 12);
                if (i % 2 == 0) { ox = (short)(attackerX + ox); b.FlipH = true; }
                else ox = (short)(attackerX - ox);
                b.SetPosition(ox, attackerY - oy);
                endDelays[i] = i < 2 ? InbetweenDelay : 0;
            }
            ScaleLerp scale = null;
            int scaleState = 0, state = 0, cur = 0;

            void InitBubble(int i)
            {
                done[i] = false;
                CellActor b = bubbles[i];
                b.Visible = true;
                b.Animate = true;
                b.AnimSpeed = AnimSpeed;
                b.SetSeq(0);
                b.SetFrame(0);
                b.TickBy(55);
            }

            bool UpdateBubble(int i)
            {
                if (!done[i] && !bubbles[i].IsAnimated) done[i] = true;
                return done[i];
            }

            bool UpdateScale()
            {
                if (scaleState >= 3) return true;
                if (scale.Update())
                {
                    sys.SetMonScale(attacker, scale.X, scale.Y);
                    sys.SetMonY(attacker, BattleAnimMath.AnchoredMonY(attackerY, height, scale.CurY));
                    return false;
                }
                scaleState++;
                if (scaleState == 1) scale = new ScaleLerp(SmallX, LargeX, SmallY, LargeY, BaseScale, ScaleSteps);
                else if (scaleState == 2) scale = new ScaleLerp(LargeX, BaseScale, LargeY, BaseScale, BaseScale, ScaleSteps);
                else { sys.SetMonScale(attacker, BattleAnimMath.MonScaleOne, BattleAnimMath.MonScaleOne); return true; }
                return false;
            }

            return () =>
            {
                switch (state)
                {
                    case 0:
                        scaleState = 0;
                        scale = new ScaleLerp(BaseScale, SmallX, BaseScale, SmallY, BaseScale, ScaleSteps);
                        InitBubble(cur);
                        state++;
                        break;
                    case 1:
                    {
                        bool scaled = UpdateScale(), shown = UpdateBubble(cur);
                        if (scaled && shown) { bubbles[cur].SetSeq(1); state++; }
                        break;
                    }
                    case 2:
                        if (--endDelays[cur] < 0)
                        {
                            if (++cur < BubbleCount) state = 0;
                            else state++;
                        }
                        break;
                    case 3:
                        for (int i = 0; i < BubbleCount; i++) InitBubble(i);
                        state++;
                        break;
                    case 4:
                    {
                        // The game keeps only the last bubble's result.
                        bool last = false;
                        for (int i = 0; i < BubbleCount; i++) last = UpdateBubble(i);
                        if (last) state++;
                        break;
                    }
                    default:
                        foreach (CellActor b in bubbles) sys.DeleteSprite(b);
                        return false;
                }
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
