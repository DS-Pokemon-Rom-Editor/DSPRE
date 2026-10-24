using System;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.SpriteEffects
{
    /// <summary>BattleAnimSpriteFunc_Assist: twelve paw prints pop up at random over the screen and then fly off to the
    /// corners and edges, placed with the game's general random numbers.</summary>
    public static class Assist
    {
        private const int PawCount = 12, ConfigCount = 6, ScaleSteps = 3, BaseDelay = 10, RandomDelay = 10, MoveDelay = 30;
        private const int MoveBaseFrames = 10, MoveRandomFrames = 10, BasePriority = 1100;
        private const double ScaleStep = 0.1;

        // sAssistPawOffsets: x, y, then the random x and y ranges for each sixth of the screen.
        private static readonly int[][] Offsets =
        {
            new[] { 40, 30, 53, 50 }, new[] { 93, 30, 53, 50 }, new[] { 146, 30, 53, 50 },
            new[] { 40, 80, 53, 50 }, new[] { 93, 80, 53, 50 }, new[] { 146, 80, 53, 50 },
        };
        private static readonly (int X, int Y)[] Destinations = { (0, 0), (128, 0), (256, 0), (0, 170), (128, 170), (256, 170) };

        // The paw's outer state the main task waits on.
        private enum Outer { Init, ScaleDone, Move, Done }

        public static Func<bool> Start(IBattleAnimSystem sys, CellActor sprite)
        {
            sys.SetBlendAlpha(26, 5);
            CellActor[] paws = new CellActor[PawCount];
            Outer[] outer = new Outer[PawCount];
            int offsetIdx = 0;
            for (int i = 0; i < PawCount; i++)
            {
                CellActor paw = paws[i] = i == 0 ? sprite : sys.NewSprite(sprite);
                int index = i;
                byte delay = (byte)(BaseDelay + sys.Random() % RandomDelay + i);
                int dest = sys.Random() % ConfigCount;
                double scale = 1.0;
                int x = (short)(Offsets[offsetIdx][0] + sys.Random() % Offsets[offsetIdx][2]);
                int y = (short)(Offsets[offsetIdx][1] + sys.Random() % Offsets[offsetIdx][3]);
                paw.SetPosition(x, y);
                paw.ScaleX = paw.ScaleY = scale;
                paw.Visible = false;
                int state = 0, step = 0;
                PosLerp pos = null;
                sys.StartTask(() =>
                {
                    bool running = true;
                    switch (state)
                    {
                        case 0:
                            if (--delay <= BaseDelay) state++;
                            break;
                        case 1:
                            if (++step >= 3) { paw.Visible = true; step = 0; state++; }
                            break;
                        case 2:
                            if (++step <= ScaleSteps) { scale += ScaleStep; paw.ScaleX = paw.ScaleY = scale; }
                            else { step = 0; state++; }
                            break;
                        case 3:
                            if (++step <= ScaleSteps) { scale -= ScaleStep; paw.ScaleX = paw.ScaleY = scale; }
                            else { step = 0; state++; }
                            break;
                        case 4:
                            if (--delay <= BaseDelay) { outer[index] = Outer.ScaleDone; step = 0; state++; }
                            break;
                        case 5:
                            if (++step > MoveDelay) { step = 0; state++; }
                            else if (outer[index] == Outer.Move) state++;
                            break;
                        case 6:
                        {
                            int frames = MoveBaseFrames + sys.Random() % MoveRandomFrames;
                            pos = new PosLerp((short)paw.X, Destinations[dest].X, (short)paw.Y, Destinations[dest].Y, frames);
                            state++;
                            break;
                        }
                        case 7:
                            if (pos.Update()) { paw.SetPosition(pos.X, pos.Y); break; }
                            paw.Visible = false;
                            state++;
                            outer[index] = Outer.Done;
                            running = false;
                            break;
                        default:
                            outer[index] = Outer.Done;
                            running = false;
                            break;
                    }
                    paw.TickFrame();
                    return running;
                }, BasePriority - 1);
                offsetIdx = (offsetIdx + 1) % ConfigCount;
            }

            int mainState = 0;
            sys.StartTask(() =>
            {
                switch (mainState)
                {
                    case 0:
                        if (Array.TrueForAll(outer, o => o == Outer.ScaleDone)) mainState++;
                        break;
                    case 1:
                        for (int i = 0; i < PawCount; i++) outer[i] = Outer.Move;
                        mainState++;
                        break;
                    case 2:
                        if (Array.TrueForAll(outer, o => o == Outer.Done)) mainState++;
                        break;
                    case 3:
                        foreach (CellActor p in paws) sys.DeleteSprite(p);
                        mainState++;
                        break;
                    default:
                        return false;
                }
                sys.DrawSprites(sprite.Manager);
                return true;
            }, BasePriority + 1);
            return null;
        }
    }
}
