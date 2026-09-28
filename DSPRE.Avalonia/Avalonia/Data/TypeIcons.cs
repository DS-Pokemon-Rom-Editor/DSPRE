using System.Collections.Generic;
using System.Linq;
using Avalonia.Media.Imaging;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>The ROM's own type icons (the 32x16 labels from the summary screen), by type number.</summary>
    internal static class TypeIcons
    {
        private static readonly Dictionary<int, Bitmap> Cache = new();
        private static string _cachedFor;

        /// <summary>
        /// The icon for a type, or null when this ROM has none for it. The game's icon tables run on past the
        /// 18 types into the five contest conditions, so types 18 to 22 draw those pictures, as the game does.
        /// </summary>
        public static Bitmap For(int type)
        {
            string rom = RomInfo.workDir;
            if (_cachedFor != rom) { Cache.Clear(); _cachedFor = rom; }
            if (Cache.TryGetValue(type, out var known)) return known;

            Bitmap icon = null;
            if (type >= 0 && type < BattleObjects.IconOrder.Length)
            {
                try
                {
                    int index = BattleObjects.Find("P_ST_TYPE_" + BattleObjects.IconOrder[type], "Drawing");
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
