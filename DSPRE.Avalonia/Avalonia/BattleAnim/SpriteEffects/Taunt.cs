using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_Taunt: the beckoning hand fades in mid-screen, waves and fades out. Value 0 is the
    /// number of hands.</summary>
    public static class Taunt
    {
        private const int Hand0X = 128, Hand0Y = 80, MinAlpha = 0, MaxAlpha = 15, FadeInFrames = 45;

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            sys.SetBlendAlpha(26, 5);
            int count = SpriteVars.Get(sprite, 0);
            int spriteAlpha = MinAlpha, bgAlpha = MaxAlpha;
            sys.SetBlendAlpha(spriteAlpha, bgAlpha);
            CellActor[] hands = new CellActor[Math.Max(1, count)];
            hands[0] = sprite;
            for (int i = 1; i < count; i++) hands[i] = sys.NewSprite(sprite);
            bool sameSide = (sys.Defender == 0) == (sys.Attacker == 0);
            // Facing: towards the target, or away when a Pokemon taunts its own side.
            hands[0].SetSeq(sys.Defender == 0 ? (sameSide ? 0 : 1) : (sameSide ? 1 : 0));
            hands[0].SetPosition(Hand0X, Hand0Y);
            for (int i = 0; i < count; i++)
            {
                hands[i].Xlu = true;
                if (sys.Contest) { hands[i].ScaleX = -1; hands[i].ScaleY = 1; }
            }
            int state = 0, frames = 0;

            return () =>
            {
                switch (state)
                {
                    case 0:
                        if (spriteAlpha < MaxAlpha) spriteAlpha++;
                        if (bgAlpha > MinAlpha) bgAlpha--;
                        sys.SetBlendAlpha(spriteAlpha, bgAlpha);
                        if (++frames >= FadeInFrames) state++;
                        break;
                    case 1:
                        if (spriteAlpha > MinAlpha) spriteAlpha--;
                        if (bgAlpha < MaxAlpha) bgAlpha++;
                        if (spriteAlpha == MinAlpha && bgAlpha == MaxAlpha) state++;
                        sys.SetBlendAlpha(spriteAlpha, bgAlpha);
                        break;
                    default:
                        for (int i = 0; i < count; i++) sys.DeleteSprite(hands[i]);
                        if (count == 0) sys.DeleteSprite(sprite);
                        return false;
                }
                for (int i = 0; i < count; i++) hands[i].TickFrame();
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
