using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_LockOn: the reticle closes in on the defender, locks, the screen flashes white
    /// and the reticle blinks out.</summary>
    public static class LockOn
    {
        private const int MoveExtent = 80, MoveFrames = 4, SegmentCount = 4, StartDelay = -16, MoveDelay = 1;
        private const int LockedAnim = 1, FlashDelay = 8, FadeWait = -8, BlinkCount = 4, BlinkInterval = 4;
        private const int FlashColor = 0x7FFF;

        private static PosLerp Segment(int segment) => segment switch
        {
            0 => new PosLerp(0, MoveExtent, 0, MoveExtent, MoveFrames),
            1 => new PosLerp(0, 0, 0, -MoveExtent, MoveFrames),
            2 => new PosLerp(0, -MoveExtent, 0, MoveExtent, MoveFrames),
            _ => new PosLerp(0, MoveExtent / 2, 0, -MoveExtent / 2, MoveFrames),
        };

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            int baseX = (short)sprite.X - MoveExtent / 2, baseY = (short)sprite.Y - MoveExtent / 2;
            sprite.SetPosition(baseX, baseY);
            sprite.Xlu = true;
            int state = 0, segment = 0, delay = StartDelay;
            bool blinkVisible = false;
            PosLerp pos = null;
            sys.SetBlendAlpha(31, 26);

            void FadeMons(int from, int to)
            {
                for (int b = 0; b < 2; b++) sys.StartMonFade(b, from, to, 0, FlashColor);
            }

            return () =>
            {
                switch (state)
                {
                    case 0:
                        if (++delay > MoveDelay) { pos = Segment(segment); state++; delay = 0; }
                        break;
                    case 1:
                        if (pos.Update()) sprite.SetPosition(baseX + pos.X, baseY + pos.Y);
                        else if (++segment < SegmentCount) { state = 0; baseX += pos.X; baseY += pos.Y; }
                        else
                        {
                            state++;
                            delay = FlashDelay;
                            sprite.SetSeq(LockedAnim);
                            sprite.Animate = true;
                        }
                        break;
                    case 2:
                        if (--delay < 0)
                        {
                            state++;
                            sys.StartBgFade(FadeWait, 0, 16, FlashColor);
                            FadeMons(14, 16);
                        }
                        break;
                    case 3:
                        if (!sys.BgFadeActive)
                        {
                            state++;
                            sys.StartBgFade(FadeWait, 16, 0, FlashColor);
                            FadeMons(2, 0);
                        }
                        break;
                    case 4:
                        if (!sys.BgFadeActive) { state++; delay = BlinkInterval; segment = BlinkCount; blinkVisible = false; }
                        break;
                    case 5:
                        if (--delay < 0)
                        {
                            delay = BlinkInterval;
                            if (--segment < 0) state++;
                            else { sprite.Visible = blinkVisible; blinkVisible = !blinkVisible; }
                        }
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
