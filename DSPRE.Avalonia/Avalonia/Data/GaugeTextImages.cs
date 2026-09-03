using Avalonia.Media.Imaging;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Gauge pictures ready to show, so the three previews that draw a battle all ask the ROM the
    /// same question rather than writing the name in a desktop font.
    /// </summary>
    public static class GaugeTextImages
    {
        /// <summary>Whether the real pictures can be drawn, and why not when they cannot.</summary>
        public static bool Available => BattleGaugeTextRenderer.IsAvailable;
        public static string Unavailable => BattleGaugeTextRenderer.Unavailable;

        public static Bitmap Name(string name) => Show(BattleGaugeTextRenderer.Name(name));

        public static Bitmap Level(int level, BattleGaugeText.Gender gender = BattleGaugeText.Gender.Genderless)
            => Show(BattleGaugeTextRenderer.LevelWithGender(level, gender));

        public static Bitmap Health(int now, int most) => Show(BattleGaugeTextRenderer.HealthNumbers(now, most));

        public static Bitmap Status(BattleGaugeText.Status status) => Show(BattleGaugeTextRenderer.StatusWord(status));

        private static Bitmap Show(BattleGaugeTextRenderer.Drawn drawn)
        {
            if (drawn?.Rgba == null) return null;
            try { return ImageConverter.FromRgba(drawn.Rgba, drawn.Width, drawn.Height); }
            catch { return null; }
        }
    }
}
