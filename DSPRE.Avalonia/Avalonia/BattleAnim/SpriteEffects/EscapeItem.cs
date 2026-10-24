using System;
using DSPRE.Avalonia.BattleAnim.Particles;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_EscapeItem: the item arcs in from the bottom left, plays its animation and fades
    /// out, behind the Pokemon.</summary>
    public static class EscapeItem
    {
        private const int StartX = -30, StartY = 160, DestX = 112, DestY = 112, MoveFrames = 21, Radius = 64 * SplFx.One;
        private const int MinAlpha = 0, MaxAlpha = 16, EffectBgPriority = 3;

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            sprite.SetPosition(StartX, StartY);
            sprite.Priority = EffectBgPriority;
            int state = 0, spriteAlpha = 0, bgAlpha = 0;
            Parabolic arc = null;

            return () =>
            {
                switch (state)
                {
                    case 0:
                        arc = new Parabolic(StartX, DestX, StartY, DestY, MoveFrames, Radius);
                        state++;
                        break;
                    case 1:
                        if (arc.Update()) sprite.SetPosition(arc.X, arc.Y);
                        else state++;
                        break;
                    case 2:
                        if (!sprite.IsAnimated)
                        {
                            sprite.Xlu = true;
                            spriteAlpha = MaxAlpha; bgAlpha = MinAlpha;
                            sys.SetBlendAlpha(spriteAlpha, bgAlpha);
                            state++;
                        }
                        break;
                    case 3:
                        if (spriteAlpha > MinAlpha) spriteAlpha--;
                        if (bgAlpha < MaxAlpha) bgAlpha++;
                        sys.SetBlendAlpha(spriteAlpha, bgAlpha);
                        if (spriteAlpha == MinAlpha) state++;
                        break;
                    default:
                        sys.DeleteSprite(sprite);
                        return false;
                }
                sprite.TickFrame();
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
