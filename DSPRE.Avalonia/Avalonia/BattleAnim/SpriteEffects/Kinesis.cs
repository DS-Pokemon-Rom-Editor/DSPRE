using System;
using DSPRE.Avalonia.BattleAnim.Particles;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_Kinesis: a spoon fades in by the attacker and swings round it, two more follow,
    /// then it bends and fades out.</summary>
    public static class Kinesis
    {
        private const int ExtraSpoons = 2, AlphaScale = 10, SpoonEndAlpha = 31, BgStartAlpha = 31, StaggerFrames = 8;

        private sealed class Spoon
        {
            public CellActor Sprite;
            public Revolution Revs;
            public ValueLerp CosWave;
            public int RevStepX, PosX, PosY, State;
            public bool Active;

            // KinesisSpoon_Update: true while it still moves or hasn't started.
            public bool Update()
            {
                if (!Active) return true;
                if (State >= 2) return false;
                if (Revs.Update())
                {
                    CosWave.Update();
                    Revs.StepX = RevStepX + CosWave.Value;
                    Sprite.SetPosition(PosX + Revs.X, PosY + Revs.Y);
                }
                else
                {
                    if (State == 0)
                    {
                        int deg180 = BattleAnimMath.DegToIdx(180);
                        Revs = Revolution.WithStepSize(Revs.CurX, deg180, Revs.CurX, deg180, Revs.RadiusX, Revs.RadiusY, BattleAnimMath.DegToIdx(10));
                        CosWave = ValueLerp.Sinusoidal(0, BattleAnimMath.DegToIdx(360), -SplFx.One, Revs.Steps);
                    }
                    State++;
                }
                return true;
            }
        }

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            int attacker = sys.Attacker;
            int deg90 = BattleAnimMath.DegToIdx(90), deg270 = BattleAnimMath.DegToIdx(270);
            Spoon spoon = new Spoon { Sprite = sprite, Active = true };
            spoon.Revs = Revolution.WithStepSize(deg90, deg270, deg90, deg270, sys.TransformDirectionX(attacker) * -32 * SplFx.One, -8 * SplFx.One,
                BattleAnimMath.DegToIdx(10));
            spoon.CosWave = ValueLerp.Sinusoidal(0, BattleAnimMath.DegToIdx(360), -SplFx.One, spoon.Revs.Steps);
            spoon.RevStepX = spoon.Revs.StepX;
            (spoon.PosX, spoon.PosY) = sys.BattlerPos(attacker);
            sprite.SetPosition(spoon.PosX, spoon.PosY);

            Spoon[] extra = new Spoon[ExtraSpoons];
            for (int i = 0; i < ExtraSpoons; i++)
            {
                CellActor s = sys.NewSprite(sprite);
                s.SetPosition(spoon.PosX, spoon.PosY);
                s.Visible = false;
                extra[i] = new Spoon { Sprite = s, Revs = spoon.Revs.Clone(), CosWave = spoon.CosWave.Clone(), RevStepX = spoon.RevStepX,
                    PosX = spoon.PosX, PosY = spoon.PosY, State = spoon.State };
            }

            int spoonAlpha = 0, state = 0, delay = StaggerFrames, active = 0;
            sys.SetBlendAlpha(0, BgStartAlpha);
            sprite.Xlu = true;
            foreach (Spoon s in extra) s.Sprite.Xlu = true;

            return () =>
            {
                switch (state)
                {
                    case 0:
                        spoon.Update();
                        state++;
                        break;
                    case 1:
                        spoonAlpha += AlphaScale;
                        sys.SetBlendAlpha(spoonAlpha / AlphaScale, BgStartAlpha - spoonAlpha / AlphaScale);
                        if (spoonAlpha >= SpoonEndAlpha * AlphaScale) { sprite.Xlu = false; state++; }
                        break;
                    case 2:
                        // BATTLE_ANIM_DEFAULT_ALPHA is -1, which the register reads as 31 for both.
                        sys.SetBlendAlpha(-1, -1);
                        state++;
                        break;
                    case 3:
                    {
                        if (delay <= 0)
                        {
                            extra[active].Active = true;
                            extra[active].Sprite.Visible = true;
                            active++;
                            delay = StaggerFrames;
                        }
                        if (active < ExtraSpoons) delay--;
                        bool any = spoon.Update();
                        foreach (Spoon s in extra)
                        {
                            bool on = s.Update();
                            if (!on) s.Sprite.Visible = false;
                            any |= on;
                        }
                        if (!any) state++;
                        break;
                    }
                    case 4:
                        sprite.TickFrame();
                        if (!sprite.IsAnimated) { sprite.Xlu = true; state++; }
                        break;
                    case 5:
                        spoonAlpha -= AlphaScale;
                        sys.SetBlendAlpha(spoonAlpha / AlphaScale, BgStartAlpha - spoonAlpha / AlphaScale);
                        if (spoonAlpha <= 0) state++;
                        break;
                    default:
                        foreach (Spoon s in extra) sys.DeleteSprite(s.Sprite);
                        sys.DeleteSprite(sprite);
                        return false;
                }
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
