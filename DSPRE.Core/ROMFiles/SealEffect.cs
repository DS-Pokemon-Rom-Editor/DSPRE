using System;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// When and where a placed seal bursts; only the first emitter of its particle file starts, with an orthographic camera.
    /// </summary>
    public static class SealEffect
    {
        // Ticks to wait by ring out from the board's centre; letter seals always take the second.
        private static readonly int[] RingDelays = { 0, 8, 14, 20 };

        public static int DelayTicks(BallSeal seal, int x, int y)
        {
            if (seal == null) return 0;
            if (seal.IsLetter) return RingDelays[1];
            int dx = x - BallCapsule.BoardCentreX, dy = y - BallCapsule.BoardCentreY;
            int distance = (int)Math.Sqrt(dx * dx + dy * dy);
            int ring = distance >= BallCapsule.BoardRadius - 4 ? 3 : Math.Min(3, (distance + 1) / 20);
            return RingDelays[ring];
        }

        // Particle world units to screen pixels (FX32_ONE / PT_LCD_DOT).
        private const double PixelsPerUnit = 4096.0 / 172.0;

        /// <summary>Where a seal's emitter sits on the 256 by 192 top screen.</summary>
        public static (double X, double Y) ScreenPosition(int x, int y, bool enemySide)
        {
            // Orthographic world position of the single player and enemy battler.
            double worldX = enemySide ? 2.5 : -2.5, worldY = enemySide ? 0.75 : -1.5625;
            // Lift in pixels: (10, 32) plus (0, -16) for the player or (-15, -25) for the enemy.
            double liftX = enemySide ? 10 - 15 : 10, liftY = enemySide ? 32 - 25 : 32 - 16;

            double px = worldX * PixelsPerUnit - liftX + (x - BallCapsule.BoardCentreX);
            double py = worldY * PixelsPerUnit - liftY + (BallCapsule.BoardCentreY - y + 30);
            return (128 + px, 96 - py);
        }
    }
}
