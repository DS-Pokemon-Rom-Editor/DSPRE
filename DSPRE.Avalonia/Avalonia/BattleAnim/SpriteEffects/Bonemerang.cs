using System;
using DSPRE.Avalonia.BattleAnim.Particles;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_Bonemerang: the attacker leans back and the bone flies to the defender and back
    /// in two arcs.</summary>
    public static class Bonemerang
    {
        private const int ThrowMoveX = -16, CatchMoveX = -32;
        private const int ThrowFramesA = 4, ThrowFramesB = 2, CatchFramesA = 4, CatchFramesB = 4;
        private const int ArcRadius = 32 * SplFx.One, ArcFrames = 10;

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            int attacker = sys.Attacker, defender = sys.Defender;
            int dirX = sys.TransformDirectionX(attacker);
            int ax = sys.MonX(attacker), ay = sys.MonY(attacker);
            int dx = sys.MonX(defender), dy = sys.MonY(defender);
            PosLerp lean = new PosLerp(0, ThrowMoveX * dirX, 0, 0, ThrowFramesA);
            Parabolic arc = new Parabolic(ax, dx, ay, dy, ArcFrames, ArcRadius);
            sprite.Animate = true;
            int state = 0;

            bool Lean()
            {
                if (!lean.Update()) return false;
                sys.SetMonX(attacker, ax + lean.X);
                sys.SetMonY(attacker, ay + lean.Y);
                return true;
            }

            void Restore()
            {
                sys.SetMonX(attacker, ax);
                sys.SetMonY(attacker, ay);
            }

            return () =>
            {
                switch (state)
                {
                    // The throw states return before drawing, so the bone isn't shown until it flies.
                    case 0:
                        if (!Lean()) { lean = new PosLerp(lean.X, 0, 0, 0, ThrowFramesB); state++; }
                        return true;
                    case 1:
                        if (!Lean()) { Restore(); lean = new PosLerp(0, CatchMoveX * dirX, 0, 0, CatchFramesA); state++; }
                        return true;
                    case 2:
                        if (arc.Update()) sprite.SetPosition(arc.X, arc.Y);
                        else { arc = new Parabolic(dx, ax, dy, ay, ArcFrames, -ArcRadius); state++; }
                        break;
                    case 3:
                        if (arc.Update()) sprite.SetPosition(arc.X, arc.Y);
                        else { sprite.Visible = false; state++; }
                        break;
                    case 4:
                        if (!Lean()) { lean = new PosLerp(lean.X, 0, 0, 0, CatchFramesB); state++; }
                        break;
                    case 5:
                        if (!Lean()) { Restore(); state++; }
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
