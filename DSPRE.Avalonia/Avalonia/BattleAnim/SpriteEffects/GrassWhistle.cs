using System;
using DSPRE.Avalonia.BattleAnim.Particles;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_GrassWhistle: fifteen notes leave the attacker one after another and drift across
    /// the screen on a wave. No retail move script uses it.</summary>
    public static class GrassWhistle
    {
        private const int NoteCount = 15, NoteDelay = 6, MoveX = 256, MoveY = -128, MoveFrames = 59;
        private const int CurveAmplitude = 14 * SplFx.One, ScaleAmplitude = 696;

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            int attacker = sys.Attacker;
            int ax = (short)sys.MonX(attacker), ay = (short)sys.MonY(attacker);
            CellActor[] notes = new CellActor[NoteCount];
            PosLerp[] pos = new PosLerp[NoteCount];
            for (int i = 0; i < NoteCount; i++)
            {
                CellActor n = notes[i] = i == 0 ? sprite : sys.NewSprite(sprite);
                n.Visible = false;
                n.SetPosition(ax, ay);
                n.SetSeq(i % 3);
            }
            int dirX = sys.TransformDirectionX(attacker), dirY = sys.TransformDirectionY(attacker);
            int next = 0, delay = 0;
            int deg2 = BattleAnimMath.DegToIdx(2);

            bool UpdateNote(int i)
            {
                if (!pos[i].Update()) return true;
                CellActor n = notes[i];
                n.SetPosition(pos[i].X, pos[i].Y);
                ushort degX = (ushort)((short)n.X * deg2);
                short offsetY = (short)(SplFx.Mul(SplFx.Sin(degX), CurveAmplitude) >> 12);
                n.SetPosition((short)n.X, (short)n.Y + offsetY);
                double scale = 1.0 - SplFx.Mul(SplFx.Sin(degX), ScaleAmplitude) / 4096.0;
                n.ScaleX = n.ScaleY = scale;
                return false;
            }

            return () =>
            {
                if (next < NoteCount && ++delay > NoteDelay)
                {
                    delay = 0;
                    CellActor n = notes[next];
                    int sx = (short)n.X, sy = (short)n.Y;
                    pos[next] = new PosLerp(sx, sx + MoveX * dirX, sy, sy + MoveY * dirY, MoveFrames);
                    n.Visible = true;
                    next++;
                }
                // The game keeps only the last started note's result.
                bool done = false;
                for (int i = 0; i < next; i++) done = UpdateNote(i);
                if (next >= NoteCount && done)
                {
                    foreach (CellActor n in notes) sys.DeleteSprite(n);
                    return false;
                }
                sys.DrawSprites(sprite.Manager);
                return true;
            };
        }
    }
}
