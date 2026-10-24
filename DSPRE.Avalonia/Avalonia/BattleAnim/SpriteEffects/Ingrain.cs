using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_Ingrain: four roots grow round the attacker's feet, flash white three times and
    /// fade out.</summary>
    public static class Ingrain
    {
        private const int VineCount = 4, UpperOffsetX = 40, UpperOffsetY = 4, LowerOffsetX = 32, LowerOffsetY = 0;
        private const int PriorityBehind = 10, PriorityInFront = 18, VineYPlayer = 140, VineYEnemy = 84, MaxDelay = 20;
        private const int MinAlpha = 0, MaxAlpha = 15, AlphaFadeDelay = 110, FadeStep = 2, FadeFrom = 0, FadeTo = 14, FadeTaskPriority = 1002;
        // Lower left, upper right, lower right, upper left.
        private const int LL = 0, UR = 1, LR = 2, UL = 3;

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            CellActor[] vines = new CellActor[VineCount];
            int[] counters = new int[VineCount];
            vines[0] = sprite;
            for (int i = 1; i < VineCount; i++)
            {
                counters[i] = sys.Random() % VineCount + i * (MaxDelay / VineCount);
                vines[i] = sys.NewSprite(sprite);
            }
            vines[UR].FlipH = vines[LR].FlipH = true;
            int attacker = sys.Attacker;
            bool player = attacker == 0;
            int vineX = (short)sys.MonX(attacker), vineY = player ? VineYPlayer : VineYEnemy;
            foreach (CellActor v in vines) v.SetPosition(vineX, vineY);
            vines[LL].OamPriority = vines[LR].OamPriority = player ? PriorityBehind : PriorityInFront;
            vines[UR].OamPriority = vines[UL].OamPriority = player ? PriorityInFront : PriorityBehind;
            vines[LL].Offset(-LowerOffsetX, LowerOffsetY);
            vines[UL].Offset(-UpperOffsetX, UpperOffsetY);
            vines[LR].Offset(LowerOffsetX, LowerOffsetY);
            vines[UR].Offset(UpperOffsetX, UpperOffsetY);
            int bgAlpha = 0, spriteAlpha = 0;
            bool ended = false;

            // PaletteFadeContext_New on the vines' shared palette: a step every run, toward white and back.
            void StartFade(int from, int to)
            {
                int fraction = from, step = from < to ? FadeStep : -FadeStep;
                bool active = true;
                sys.StartSysTask(() =>
                {
                    if (ended) return false;
                    if (!active) return true;
                    foreach (CellActor v in vines) { v.Tint = fraction; v.TintR = v.TintG = v.TintB = 255; }
                    if (fraction == to) active = false;
                    else fraction = step > 0 ? Math.Min(to, fraction + step) : Math.Max(to, fraction + step);
                    return true;
                }, FadeTaskPriority);
            }

            return () =>
            {
                bool done = false;
                for (int i = 0; i < VineCount; i++)
                {
                    if (++counters[i] < MaxDelay) continue;
                    SpriteVars.TickIfVisible(vines[i]);
                    if (counters[0] == AlphaFadeDelay) StartAlphaFade();
                }
                switch (counters[0])
                {
                    case 50: case 70: case 90: StartFade(FadeFrom, FadeTo); break;
                    case 60: case 80: case 100: StartFade(FadeTo, FadeFrom); break;
                }
                if (counters[0] == AlphaFadeDelay) StartAlphaFade();
                if (counters[0] >= AlphaFadeDelay)
                {
                    if (bgAlpha > MinAlpha) bgAlpha--;
                    if (spriteAlpha < MaxAlpha) spriteAlpha++;
                    sys.SetBlendAlpha(bgAlpha, spriteAlpha);
                    if (bgAlpha == MinAlpha && spriteAlpha == MaxAlpha) done = true;
                }
                if (done)
                {
                    foreach (CellActor v in vines) sys.DeleteSprite(v);
                    ended = true;
                    return false;
                }
                sys.DrawSprites(sprite.Manager);
                return true;
            };

            void StartAlphaFade()
            {
                sys.SetBlendAlpha(26, 5);
                foreach (CellActor v in vines) v.Xlu = true;
                bgAlpha = MaxAlpha;
                spriteAlpha = MinAlpha;
            }
        }
    }
}
