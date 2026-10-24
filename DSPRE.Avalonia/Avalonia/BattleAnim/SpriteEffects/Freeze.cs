using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_Freeze, for the frozen status: the ice fades in, glints by cycling its three
    /// palettes with the animation, and fades out.</summary>
    public static class Freeze
    {
        private const int DefaultAlpha = 20, StartAlpha = 0, EndAlpha = 16, FadeInFrames = 10, FadeOutFrames = 8;

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            sprite.Visible = false;
            sprite.Xlu = true;
            int basePalette = sprite.PalShift;
            int state = 0;
            AlphaFade alpha = null;

            return () =>
            {
                switch (state)
                {
                    case 0:
                        alpha = sys.StartAlphaFade(StartAlpha, EndAlpha, DefaultAlpha - StartAlpha, DefaultAlpha - EndAlpha, FadeInFrames);
                        sprite.Visible = true;
                        sys.SetBlendAlpha(StartAlpha, DefaultAlpha - StartAlpha);
                        state++;
                        break;
                    case 1:
                        if (alpha.Done) { state++; sprite.Animate = true; sprite.AnimSpeed = 1; }
                        break;
                    case 2:
                        sprite.PalShift = basePalette + sprite.FrameIndex % 3;
                        if (!sprite.IsAnimated)
                        {
                            state++;
                            alpha = sys.StartAlphaFade(EndAlpha, StartAlpha, DefaultAlpha - EndAlpha, DefaultAlpha - StartAlpha, FadeOutFrames);
                        }
                        break;
                    case 3:
                        if (alpha.Done) { state++; sprite.Visible = false; }
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
