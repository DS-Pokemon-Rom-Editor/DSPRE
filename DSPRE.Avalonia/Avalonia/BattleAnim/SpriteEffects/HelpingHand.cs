using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_HelpingHand: pairs of hands slide in from the corners and meet in the middle, the
    /// last pair twice. Value 0 is the number of hands, six in the game's script.</summary>
    public static class HelpingHand
    {
        private const int MinAlpha = 0, MaxAlpha = 15;

        private static readonly (int X, int Y)[] Positions = { (80, 50), (180, 50), (80, 100), (180, 100), (80, 75), (180, 75) };
        private static readonly (int X, int Y)[][] Dests =
        {
            new[] { (0x78, 0x50), (0x88, 0x50) }, new[] { (0x78, 0x50), (0x88, 0x50) },
            new[] { (0x78, 0x50), (0x88, 0x50) }, new[] { (0x58, 0x50), (0xA8, 0x50) },
        };
        private static readonly int[] MoveFrames = { 5, 5, 5, 3 };
        private static readonly int[] Anims = { 0, 0, 1, 1, 2, 3 };

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            sys.SetBlendAlpha(26, 5);
            int spriteAlpha = MinAlpha, bgAlpha = MaxAlpha;
            sys.SetBlendAlpha(spriteAlpha, bgAlpha);
            int count = Math.Min(6, SpriteVars.Get(sprite, 0));
            CellActor[] hands = new CellActor[6];
            hands[0] = sprite;
            for (int i = 1; i < 6; i++) hands[i] = i < count ? sys.NewSprite(sprite) : null;
            for (int i = 0; i < count; i++)
            {
                hands[i].SetSeq(Anims[i]);
                hands[i].Animate = true;
                hands[i].SetPosition(Positions[i].X, Positions[i].Y);
                hands[i].Xlu = true;
                hands[i].Visible = i == 0 || i == 3;
            }
            hands[0].FlipH = true;
            if (hands[3] != null) hands[3].FlipH = true;
            int state = 0, pair = 0;
            CellActor hand1 = null, hand2 = null;
            PosLerp pos1 = null, pos2 = null;

            return () =>
            {
                bool animsDone = false;
                switch (state)
                {
                    case 0:
                        if (spriteAlpha < MaxAlpha) spriteAlpha++;
                        if (bgAlpha > MinAlpha) bgAlpha--;
                        if (spriteAlpha == MaxAlpha && bgAlpha == MinAlpha) state++;
                        sys.SetBlendAlpha(spriteAlpha, bgAlpha);
                        break;
                    case 1:
                        (int a, int b) = pair switch { 0 => (0, 3), 1 => (1, 2), _ => (4, 5) };
                        hand1 = hands[a] ?? sprite; hand2 = hands[b] ?? sprite;
                        hand1.Visible = hand2.Visible = true;
                        pos1 = new PosLerp((short)hand1.X, Dests[pair][0].X, (short)hand1.Y, Dests[pair][0].Y, MoveFrames[pair]);
                        pos2 = new PosLerp((short)hand2.X, Dests[pair][1].X, (short)hand2.Y, Dests[pair][1].Y, MoveFrames[pair]);
                        pair++;
                        state++;
                        break;
                    case 2:
                    {
                        int done = 0;
                        if (pos1.Update()) hand1.SetPosition(pos1.X, pos1.Y); else done++;
                        if (pos2.Update()) hand2.SetPosition(pos2.X, pos2.Y); else done++;
                        if (done >= 2)
                        {
                            if (pair <= 3) { hand1.Visible = hand2.Visible = false; state--; }
                            else state++;
                        }
                        break;
                    }
                    case 3:
                        if (spriteAlpha > MinAlpha) spriteAlpha--;
                        if (bgAlpha < MaxAlpha) bgAlpha++;
                        if (bgAlpha >= MaxAlpha / 2) animsDone = true;
                        if (spriteAlpha == MinAlpha && bgAlpha == MaxAlpha) state++;
                        sys.SetBlendAlpha(spriteAlpha, bgAlpha);
                        break;
                    default:
                        for (int i = 0; i < 6; i++) if (hands[i] != null) sys.DeleteSprite(hands[i]);
                        return false;
                }
                if (state < 3 && !animsDone)
                    for (int i = 0; i < count; i++) hands[i].TickFrame();
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
