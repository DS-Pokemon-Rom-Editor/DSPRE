using System;
using DSPRE.Avalonia.BattleAnim.Particles;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_Trick: the attacker squashes and springs up, two cups drop in and swap places five
    /// times in arcs, then fade out. Value 0 is the number of cups.</summary>
    public static class Trick
    {
        private const int Cup1X = 100, Cup1Y = 54, Cup2X = 180, Cup2Y = 39, ScaleIterations = 3, CupEndOffsetY = 50, CupStepY = 2;
        private const int Delay1 = 1, Delay2 = 0, Cup1OffsetDelay = 10, Cup2OffsetDelay = 12, CupTargetAlpha = 16, BgTargetAlpha = 0;
        private const int CupEndAlpha = 0, BgEndAlpha = 16, FadeOutStep = 2, SwitchCount = 5, SwitchFrames = 10, MonPriority = 1;

        // sTrickCupScales: x and y from and to, in percent, and frames.
        private static readonly int[][] Scales = { new[] { 100, 100, 100, 70, 8 }, new[] { 100, 100, 70, 120, 3 }, new[] { 100, 100, 120, 100, 3 } };
        private static readonly int[][] ArcDirections = { new[] { 1, -1 }, new[] { -1, 1 }, new[] { 1, -1 }, new[] { 1, -1 }, new[] { 1, -1 } };

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            int attacker = sys.Attacker;
            int cupAlpha = 0, bgAlpha = 16;
            int attackerY = (short)sys.MonY(attacker), height = sys.MonYOffset(attacker);
            sys.SetBlendAlpha(26, 5);
            sys.SetBlendAlpha(cupAlpha, bgAlpha);
            int count = Math.Min(2, SpriteVars.Get(sprite, 0));
            CellActor[] cups = new CellActor[2];
            cups[0] = sprite;
            if (count > 1) cups[1] = sys.NewSprite(sprite);
            for (int i = 0; i < count; i++) { cups[i].Xlu = true; cups[i].Priority = MonPriority + 1; }
            if (sys.Contest) { cups[0].SetPosition(Cup2X - 20, Cup1Y); cups[1]?.SetPosition(Cup1X - 20, Cup2Y); }
            else { cups[0].SetPosition(Cup1X, Cup1Y); cups[1]?.SetPosition(Cup2X, Cup2Y); }
            int state = 0, delay = 0, iteration = 0;
            ScaleLerp scale = null;
            Parabolic arc0 = null, arc1 = null;
            const int Radius = 40 * SplFx.One;

            bool Step()
            {
                switch (state)
                {
                    case 0:
                    {
                        int[] t = Scales[iteration];
                        scale = new ScaleLerp(t[0], t[1], t[2], t[3], 100, t[4]);
                        iteration++;
                        state++;
                        break;
                    }
                    case 1:
                        if (scale.Update())
                        {
                            sys.SetMonScale(attacker, scale.X, scale.Y);
                            sys.SetMonY(attacker, BattleAnimMath.AnchoredMonY(attackerY, height, scale.CurY));
                        }
                        else state = iteration >= ScaleIterations ? state + 1 : state - 1;
                        break;
                    case 2:
                        if (++delay >= Delay1) { delay = 0; iteration = 0; state++; }
                        break;
                    case 3:
                        if ((short)cups[0].Y <= Cup1Y + CupEndOffsetY) { cups[0].Offset(0, CupStepY); cups[1]?.Offset(0, CupStepY); }
                        else state++;
                        break;
                    case 4:
                        if ((short)cups[0].Y <= Cup1Y + CupEndOffsetY) { cups[0].Offset(0, CupStepY); cups[1]?.Offset(0, CupStepY); }
                        else { sys.SetBlendAlpha(CupTargetAlpha, BgTargetAlpha); state++; }
                        delay++;
                        BattleAnimMath.StepValue(ref cupAlpha, CupTargetAlpha, 16);
                        BattleAnimMath.StepValue(ref bgAlpha, BgTargetAlpha, -16);
                        sys.SetBlendAlpha(cupAlpha, bgAlpha);
                        if (delay == Cup1OffsetDelay) cups[0].Offset(0, 16);
                        if (delay == Cup2OffsetDelay) cups[1]?.Offset(0, 16);
                        break;
                    case 5:
                        if (++delay < Delay2 + 1) break;
                        delay = 0;
                        state++;
                        break;
                    case 6:
                    {
                        int x0 = (short)cups[0].X, y0 = (short)cups[0].Y, x1 = (short)(cups[1]?.X ?? 0), y1 = (short)(cups[1]?.Y ?? 0);
                        arc0 = new Parabolic(x0, x1, y0, y1, SwitchFrames, Radius * ArcDirections[iteration][0]);
                        arc1 = new Parabolic(x1, x0, y1, y0, SwitchFrames, Radius * ArcDirections[iteration][1]);
                        iteration++;
                        state++;
                        break;
                    }
                    case 7:
                    {
                        int done = 0;
                        if (arc0.Update()) cups[0].SetPosition(arc0.X, arc0.Y); else done++;
                        if (arc1.Update()) cups[1]?.SetPosition(arc1.X, arc1.Y); else done++;
                        if (done == 2) state = iteration == SwitchCount ? state + 1 : state - 1;
                        break;
                    }
                    case 8:
                    {
                        int done = 0;
                        if (BattleAnimMath.StepValue(ref cupAlpha, CupEndAlpha, -FadeOutStep)) done++;
                        if (BattleAnimMath.StepValue(ref bgAlpha, BgEndAlpha, FadeOutStep)) done++;
                        sys.SetBlendAlpha(cupAlpha, bgAlpha);
                        if (done == 2) state++;
                        break;
                    }
                    default:
                        for (int i = 0; i < count; i++) sys.DeleteSprite(cups[i]);
                        if (count == 0) sys.DeleteSprite(sprite);
                        return false;
                }
                return true;
            }

            return () =>
            {
                if (!Step()) return false;
                // Cups go behind the Pokemon only in the lower left of the screen.
                for (int i = 0; i < count; i++)
                    cups[i].Priority = (short)cups[i].Y < 80 || (short)cups[i].X > 128 ? MonPriority : MonPriority + 1;
                // Until the cups start to come down, the task returns before drawing.
                if (state < 4) return true;
                for (int i = 0; i < count; i++) cups[i].TickFrame();
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
