using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_StringShot: the strands flicker on over the defender, squeeze in and fade out.
    /// Value 0 is the number of strands.</summary>
    public static class StringShot
    {
        private const int InitSpriteAlpha = 15, InitBgAlpha = 0, EndSpriteAlpha = 0, EndBgAlpha = 15;
        private const int BaseY = 32, Spacing = 4, BlinkFrames = 45, WaitFrames = 45, ScaleFrom = 100, ScaleTo = 60, ScaleFrames = 10;

        // sStringShotSpriteTimings: passes before a strand starts flickering, and passes between its toggles.
        private static readonly int[][] Timings = { new[] { 8, 2 }, new[] { 13, 1 }, new[] { 18, 3 } };

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            int count = SpriteVars.Get(sprite, 0);
            // BattleAnimUtil_SetSpriteBgBlending with the defaults, then the strands' own weights.
            sys.SetBlendAlpha(26, 5);
            int spriteAlpha = InitSpriteAlpha, bgAlpha = InitBgAlpha;
            sys.SetBlendAlpha(spriteAlpha, bgAlpha);
            (int px, int py) = sys.BattlerPos(sys.Defender);
            CellActor[] strands = new CellActor[Math.Max(1, count)];
            strands[0] = sprite;
            for (int i = 1; i < count; i++) { strands[i] = sys.NewSprite(sprite); strands[i].SetPosition(px, py); }
            sprite.SetPosition(px, py);
            int[] counters = new int[strands.Length];
            for (int i = 0; i < count; i++) { strands[i].Offset(0, BaseY - i * Spacing); strands[i].Xlu = true; }
            int state = 0, delay = 0;
            ScaleLerp scale = null;

            return () =>
            {
                switch (state)
                {
                    case 0:
                        delay++;
                        for (int i = 0; i < count; i++)
                        {
                            if (delay >= Timings[Math.Min(i, 2)][0])
                            {
                                if (++counters[i] >= Timings[Math.Min(i, 2)][1]) { counters[i] = 0; strands[i].Visible = !strands[i].Visible; }
                            }
                            else strands[i].Visible = false;
                        }
                        if (delay >= BlinkFrames)
                        {
                            for (int i = 0; i < count; i++) strands[i].Visible = true;
                            delay = 0;
                            state++;
                        }
                        break;
                    case 1:
                        scale = new ScaleLerp(ScaleFrom, ScaleTo, 100, 100, 100, ScaleFrames);
                        state++;
                        break;
                    case 2:
                        if (scale.Update())
                            for (int i = 0; i < count; i++) { strands[i].ScaleX = scale.AffineX; strands[i].ScaleY = scale.AffineY; }
                        else if (++delay >= WaitFrames) state++;
                        break;
                    case 3:
                        if (spriteAlpha > EndSpriteAlpha) spriteAlpha--;
                        if (bgAlpha < EndBgAlpha) bgAlpha++;
                        if (spriteAlpha == EndSpriteAlpha && bgAlpha == EndBgAlpha) state++;
                        sys.SetBlendAlpha(spriteAlpha, bgAlpha);
                        break;
                    default:
                        for (int i = 0; i < count; i++) sys.DeleteSprite(strands[i]);
                        if (count == 0) sys.DeleteSprite(sprite);
                        return false;
                }
                for (int i = 0; i < count; i++) strands[i].TickFrame();
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
