using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_Constrict: four vines appear up the defender one after another, it shakes, and
    /// the vines squeeze in and out.</summary>
    public static class Constrict
    {
        private const int VineCount = 4, BaseOffsetY = 16, StepOffsetY = 10;
        private const int StartScale = 10, RefScale = 10, EndScale = 8, ScaleFrames = 8, StaggerFrames = 4, ScaleCount = 3;

        private sealed class Vine
        {
            public CellActor Sprite;
            public ScaleLerp Scale = new ScaleLerp(StartScale, RefScale, EndScale, ScaleFrames);
            public bool Flip;
            public int Counter = ScaleCount, NextStart = EndScale, NextEnd = StartScale;

            public bool Update()
            {
                if (Scale.Update()) { Sprite.ScaleX = Flip ? -Scale.AffineX : Scale.AffineX; Sprite.ScaleY = 1; return true; }
                if (Counter == 0) return false;
                Counter--;
                Scale = new ScaleLerp(NextStart, RefScale, NextEnd, ScaleFrames);
                (NextStart, NextEnd) = (NextEnd, NextStart);
                return true;
            }
        }

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            int defender = sys.Defender;
            int defenderX = (short)sys.MonX(defender), defenderY = (short)sys.MonY(defender);
            Shake shake = new Shake(4, 0, 1, 1);
            int vineY = defenderY + BaseOffsetY;
            bool flip = false;
            Vine[] vines = new Vine[VineCount];
            for (int i = 0; i < VineCount; i++)
            {
                CellActor s = i == 0 ? sprite : sys.NewSprite(sprite);
                vines[i] = new Vine { Sprite = s, Flip = flip };
                s.SetPosition(defenderX, vineY);
                if (flip) { s.ScaleX = -1; s.ScaleY = 1; }
                s.Visible = false;
                vineY -= StepOffsetY;
                flip = !flip;
            }
            int state = 0, counter = 0, vineIdx = 0;

            return () =>
            {
                switch (state)
                {
                    case 0:
                        if (counter >= StaggerFrames)
                        {
                            counter = 0;
                            vines[vineIdx].Sprite.Visible = true;
                            vines[vineIdx].Sprite.Animate = true;
                            if (++vineIdx >= VineCount) state++;
                        }
                        counter++;
                        break;
                    case 1:
                        if (shake.Update()) { sys.SetMonX(defender, defenderX + shake.X); sys.SetMonY(defender, defenderY + shake.Y); }
                        else state++;
                        break;
                    case 2:
                    {
                        // The game keeps only the last vine's result, so it stops when that one does.
                        bool active = false;
                        foreach (Vine v in vines) active = v.Update();
                        if (!active) state++;
                        break;
                    }
                    default:
                        sys.SetMonX(defender, defenderX);
                        sys.SetMonY(defender, defenderY);
                        foreach (Vine v in vines) sys.DeleteSprite(v.Sprite);
                        return false;
                }
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
