using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_FakeOut: the clapping hands fade in on the defender, play once and fade out.</summary>
    public static class FakeOut
    {
        private const int StartAlpha = 0, EndAlpha = 16, MaxAlpha = 16, FadeFrames = 8;

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            sprite.Animate = true;
            sprite.Xlu = true;
            int state = 0;
            AlphaFade alpha = null;

            return () =>
            {
                switch (state)
                {
                    case 0:
                        sys.SetBlendAlpha(StartAlpha, MaxAlpha - StartAlpha);
                        alpha = sys.StartAlphaFade(StartAlpha, EndAlpha, MaxAlpha - StartAlpha, MaxAlpha - EndAlpha, FadeFrames);
                        state++;
                        break;
                    case 1:
                        if (alpha.Done) state++;
                        break;
                    case 2:
                        if (!sprite.IsAnimated)
                        {
                            state++;
                            alpha = sys.StartAlphaFade(EndAlpha, StartAlpha, MaxAlpha - EndAlpha, MaxAlpha - StartAlpha, FadeFrames);
                        }
                        break;
                    case 3:
                        if (alpha.Done) state++;
                        break;
                    default:
                        sys.DeleteSprite(sprite);
                        return false;
                }
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
