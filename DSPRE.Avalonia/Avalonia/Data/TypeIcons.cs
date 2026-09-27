using System.Collections.Generic;
using System.Linq;
using Avalonia.Media.Imaging;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>The ROM's own type icons (the 32x16 labels from the summary screen), by type number.</summary>
    internal static class TypeIcons
    {
        // Battle-object file tags in type order. Types past these have no icon of their own.
        private static readonly string[] Tags =
        {
            "NORMAL", "FIGHT", "FLIGHT", "POISON", "GROUND", "ROCK", "INSECT", "GHOST", "STEEL",
            "QUES", "FIRE", "WATER", "GRASS", "ELE", "ESP", "ICE", "DRAGON", "EVIL",
        };

        private static readonly Dictionary<int, Bitmap> Cache = new();
        private static string _cachedFor;

        /// <summary>The icon for a type, or null when this ROM has none for it.</summary>
        public static Bitmap For(int type)
        {
            string rom = RomInfo.workDir;
            if (_cachedFor != rom) { Cache.Clear(); _cachedFor = rom; }
            if (Cache.TryGetValue(type, out var known)) return known;

            Bitmap icon = null;
            if (type >= 0 && type < Tags.Length)
            {
                try
                {
                    int index = BattleObjects.Find("P_ST_TYPE_" + Tags[type], "Drawing");
                    var archive = GraphicAssets.All.FirstOrDefault(a => a.Dir == DirNames.battleObj);
                    if (index >= 0 && archive != null)
                    {
                        var p = GraphicAssets.Render(archive, index);
                        if (p.Rgba != null) icon = ImageConverter.FromRgba(p.Rgba, p.Width, p.Height);
                    }
                }
                catch { icon = null; }
            }
            Cache[type] = icon;
            return icon;
        }
    }
}
