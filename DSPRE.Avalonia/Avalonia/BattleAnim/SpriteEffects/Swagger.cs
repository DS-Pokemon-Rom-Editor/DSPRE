using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_Swagger: an anger mark pops up on one side of the defender, then on the other.</summary>
    public static class Swagger
    {
        private const int StartScale = 10, RefScale = 10, EndScale = 14, ScaleFrames = 4, SettleScale = 12, SettleFrames = 2;
        private const int PopOffsetX = 24, Pop1OffsetY = -16, Pop2OffsetY = -24, PopDelay = 4;

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            sprite.Visible = false;
            int delay = PopDelay;
            int dir = sys.TransformDirectionX(sys.Attacker);
            int defenderX = (short)sys.MonX(sys.Defender), defenderY = (short)sys.MonY(sys.Defender);
            ScaleLerp scale = null;
            bool settling = false;
            int state = 0, popState = 0;

            void StartPop(int x, int y)
            {
                sprite.Visible = true;
                sprite.SetPosition(x, y);
                settling = false;
                scale = new ScaleLerp(StartScale, RefScale, EndScale, ScaleFrames);
            }

            bool UpdatePop()
            {
                if (scale.Update()) { sprite.ScaleX = scale.AffineX; sprite.ScaleY = scale.AffineY; return false; }
                if (settling) { sprite.Visible = false; return true; }
                settling = true;
                scale = new ScaleLerp(EndScale, RefScale, SettleScale, SettleFrames);
                return false;
            }

            return () =>
            {
                if (state != 0)
                {
                    sys.DeleteSprite(sprite);
                    return false;
                }
                switch (popState)
                {
                    case 0: StartPop(defenderX + PopOffsetX * dir, defenderY + Pop1OffsetY); popState++; break;
                    case 1: if (UpdatePop()) popState++; break;
                    case 2:
                        if (--delay < 0) { popState++; StartPop(defenderX - PopOffsetX * dir, defenderY + Pop2OffsetY); }
                        break;
                    case 3: if (UpdatePop()) popState++; break;
                    default: state++; break;
                }
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
