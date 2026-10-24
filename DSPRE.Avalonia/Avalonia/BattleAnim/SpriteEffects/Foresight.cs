using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_Foresight: the lens zigzags over the defender, then fades out while the defender
    /// flashes white.</summary>
    public static class Foresight
    {
        private const int MoveExtent = 80, MoveFrames = 8, SegmentCount = 6, StartDelay = 4;
        private const int FlashFrom = 0, FlashTo = 10, FlashDelay = 0, FlashColor = 0x7FFF;

        private static PosLerp Segment(int segment) => segment switch
        {
            0 => new PosLerp(0, MoveExtent / 2, 0, MoveExtent / 2, MoveFrames),
            1 => new PosLerp(0, 0, 0, -MoveExtent, MoveFrames),
            2 => new PosLerp(0, -MoveExtent, 0, MoveExtent, MoveFrames),
            3 => new PosLerp(0, 0, 0, -MoveExtent, MoveFrames),
            4 => new PosLerp(0, MoveExtent, 0, MoveExtent, MoveFrames),
            _ => new PosLerp(0, -MoveExtent / 2, 0, -MoveExtent / 2, MoveFrames),
        };

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            int defender = sys.Defender;
            int baseX = (short)sprite.X, baseY = (short)sprite.Y;
            int state = 0, segment = 0, delay = 0;
            PosLerp pos = null;
            AlphaFade alpha = null;

            return () =>
            {
                switch (state)
                {
                    case 0:
                        if (++delay > StartDelay) { pos = Segment(segment); state++; delay = 0; }
                        break;
                    case 1:
                        if (pos.Update()) sprite.SetPosition(baseX + pos.X, baseY + pos.Y);
                        else if (++segment < SegmentCount) { state = 0; baseX += pos.X; baseY += pos.Y; }
                        else
                        {
                            state++;
                            sprite.Xlu = true;
                            alpha = sys.StartAlphaFade(16, 0, 0, 16, 16);
                            sys.StartMonFade(defender, FlashFrom, FlashTo, FlashDelay, FlashColor);
                        }
                        break;
                    case 2:
                        if (alpha.Done) sprite.Visible = false;
                        if (!sys.MonFadeActive(defender)) { state++; sys.StartMonFade(defender, FlashTo, FlashFrom, FlashDelay, FlashColor); }
                        break;
                    case 3:
                        if (!sys.MonFadeActive(defender)) state++;
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
