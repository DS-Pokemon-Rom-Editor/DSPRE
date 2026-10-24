using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_Sleep, for the asleep status: two Zs rise from the Pokemon and grow.</summary>
    public static class Sleep
    {
        private const int MoveX = 16, MoveY = -32, AnimFrames = 32, BaseScale = 10, StartScale = 2, EndScale = 10, SecondDelay = 8;

        private sealed class Z
        {
            public CellActor Sprite;
            public PosLerp Pos;
            public ScaleLerp Scale;

            public void Init(int dir)
            {
                Sprite.Visible = true;
                int x = (short)Sprite.X, y = (short)Sprite.Y;
                Pos = new PosLerp(x, x + MoveX * dir, y, y + MoveY, AnimFrames);
                Scale = new ScaleLerp(StartScale, BaseScale, EndScale, AnimFrames);
            }

            public bool Update()
            {
                if (Pos.Update()) Sprite.SetPosition(Pos.X, Pos.Y);
                if (Scale.Update()) { Sprite.ScaleX = Scale.AffineX; Sprite.ScaleY = Scale.AffineY; return false; }
                Sprite.Visible = false;
                return true;
            }
        }

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            int attacker = sys.Attacker;
            int dir = sys.TransformDirectionX(attacker);
            (int x, int y) = sys.BattlerPos(attacker);
            Z[] zs = { new Z { Sprite = sprite }, new Z { Sprite = sys.NewSprite(sprite) } };
            foreach (Z z in zs) { z.Sprite.SetPosition(x, y); z.Sprite.Visible = false; }
            int state = 0, delay = 0;

            return () =>
            {
                switch (state)
                {
                    case 0:
                        zs[0].Init(dir); zs[0].Update();
                        state++;
                        delay = SecondDelay;
                        break;
                    case 1:
                        delay--;
                        zs[0].Update();
                        if (delay < 0) state++;
                        break;
                    case 2:
                        zs[1].Init(dir); zs[1].Update(); zs[0].Update();
                        state++;
                        break;
                    case 3:
                        zs[0].Update();
                        if (zs[1].Update()) state++;
                        break;
                    default:
                        foreach (Z z in zs) sys.DeleteSprite(z.Sprite);
                        return false;
                }
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
