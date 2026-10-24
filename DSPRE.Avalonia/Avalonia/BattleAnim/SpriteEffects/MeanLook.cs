using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_MeanLook: the eye pulses in, each round smaller and quicker, then opens while the
    /// screen flashes white, and fades out.</summary>
    public static class MeanLook
    {
        private const int RepeatCount = 3, PulseCount = 2, Decay = 3, Precision = 100, StartScale = 15, RefScale = 10, EndScale = 10;
        private const int ScaleFrames = 4, InitAnimFrame = 4, BlendMin = 0, BlendMax = 16, PulseAlphaStart = 16, PulseAlphaEnd = 10;
        private const int PulseFadeScale = 2, FlashDelay = 30, FlashFadeFrames = 4, FadeOutFrames = 8, BrightnessFrames = 8;

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            sprite.Xlu = true;
            sprite.SetFrame(InitAnimFrame);
            int repeat = RepeatCount, frames = ScaleFrames * Precision, start = StartScale * Precision, end = EndScale * Precision;
            int state = 0, seqState = 0, pulseState = 0, pulseCount = 0, timer = 0;
            ScaleLerp scale = null;
            AlphaFade alpha = null;

            void InitPulse()
            {
                scale = new ScaleLerp(start / Precision, RefScale, end / Precision, frames / Precision);
                sys.SetBlendAlpha(BlendMax, BlendMin);
                alpha = sys.StartAlphaFade(PulseAlphaStart, PulseAlphaEnd, BlendMax - PulseAlphaStart, BlendMax - PulseAlphaEnd,
                    frames / Precision * PulseFadeScale);
                pulseState = 0;
                pulseCount = 0;
            }

            bool ScaleStep()
            {
                if (!scale.Update()) return false;
                sprite.ScaleX = scale.AffineX; sprite.ScaleY = scale.AffineY;
                return true;
            }

            bool UpdatePulse()
            {
                switch (pulseState)
                {
                    case 0:
                        if (!ScaleStep()) { scale = new ScaleLerp(end / Precision, RefScale, start / Precision, frames / Precision); pulseState++; }
                        return false;
                    case 1:
                        if (!ScaleStep())
                        {
                            if (++pulseCount < PulseCount) { pulseState = 0; scale = new ScaleLerp(start / Precision, RefScale, end / Precision, frames / Precision); }
                            else pulseState++;
                        }
                        return false;
                    case 2:
                        if (alpha.Done) { pulseState++; return true; }
                        return false;
                    default:
                        return true;
                }
            }

            bool UpdatePulses()
            {
                switch (seqState)
                {
                    case 0:
                        InitPulse();
                        UpdatePulse();
                        seqState++;
                        return false;
                    case 1:
                        if (!UpdatePulse()) return false;
                        if (--repeat >= 0)
                        {
                            frames -= frames / Decay;
                            start -= start / Decay;
                            end -= end / Decay;
                            seqState = 0;
                            return false;
                        }
                        seqState++;
                        return true;
                    default:
                        return true;
                }
            }

            return () =>
            {
                switch (state)
                {
                    case 0:
                        if (UpdatePulses())
                        {
                            state++;
                            timer = FlashDelay;
                            sprite.SetSeq(0);
                            sprite.Animate = true;
                            sprite.ScaleX = sprite.ScaleY = 1;
                            alpha = sys.StartAlphaFade(BlendMin, BlendMax, BlendMax, BlendMin, FlashFadeFrames);
                        }
                        break;
                    case 1:
                        // A timer of -1 is the flash going up, -2 coming back.
                        if (timer == 0) { sys.StartBrightness(BrightnessFrames, 16, 0); timer = -1; }
                        else if (timer > 0) timer--;
                        if (timer == -1 && sys.BrightnessDone) { sys.StartBrightness(BrightnessFrames, 0, 16); timer = -2; }
                        if (!sprite.IsAnimated) state++;
                        break;
                    case 2:
                        if (sys.BrightnessDone)
                        {
                            state++;
                            sys.SetBlendAlpha(BlendMax, BlendMin);
                            alpha = sys.StartAlphaFade(BlendMax, BlendMin, BlendMin, BlendMax, FadeOutFrames);
                        }
                        break;
                    case 3:
                        if (alpha.Done) { sprite.Visible = false; state++; }
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
