using System;
using System.Collections.Generic;
using System.Linq;
using Spots = DSPRE.ROMFiles.TouchListButtons.Spots;

namespace DSPRE.ROMFiles
{
    /// <summary>The text spots of a touch page as rows that move on their own. Spots sharing a name are copies, or the x and y of one window.</summary>
    public static class ScreenTextSpots
    {
        public sealed record Part(string Name, int Button, List<(Spots Spots, int At)> Places)
        {
            public ScreenPart Row => new ScreenPart(Name, Button, Places.Max(p => p.Spots.Unit),
                                                    Places.Any(p => !p.Spots.YOnly), Places.Any(p => !p.Spots.XOnly));
        }

        public static List<Part> Of(Spots[] all, IReadOnlyList<int> starts, string[] buttons, Func<Spots, int> count)
        {
            List<Part> parts = new List<Part>();
            for (int t = 0; t < (all?.Length ?? 0); t++)
            {
                Spots s = all[t];
                if (s.Text == null) continue;
                for (int i = 0; i < count(s); i++)
                {
                    string name = s.Text.Length > 0 ? s.Text : buttons[s.FirstButton + i];
                    Part part = parts.FirstOrDefault(p => p.Name == name);
                    if (part == null) parts.Add(part = new Part(name, s.FirstButton + i, new List<(Spots, int)>()));
                    part.Places.Add((s, starts[t] + i * s.Stride + s.X));
                }
            }
            return parts;
        }

        /// <summary>In pixels.</summary>
        public static (int X, int Y) At(byte[] data, Part part)
        {
            (Spots S, int At) x = part.Places.FirstOrDefault(p => !p.Spots.YOnly), y = part.Places.FirstOrDefault(p => !p.Spots.XOnly);
            return (x.S == null ? 0 : ScreenSpots.Entry(data, x.S, x.At).X * x.S.Unit, y.S == null ? 0 : ScreenSpots.Entry(data, y.S, y.At).Y * y.S.Unit);
        }

        public static void Move(byte[] data, Part part, int dx, int dy)
        {
            foreach ((Spots s, int at) in part.Places) ScreenSpots.ShiftEntry(data, s, at, dx, dy);
        }
    }
}
