using System.Collections.Generic;
using System.Linq;
using Avalonia.Media.Imaging;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>The ROM's own type icons (the 32x16 labels from the summary screen), by type number, and its
    /// physical, special and status icons.</summary>
    internal static class TypeIcons
    {
        private static readonly Dictionary<int, Bitmap> Cache = new();
        private static readonly Dictionary<int, Bitmap> CategoryCache = new();
        private static string _cachedFor;

        /// <summary>The icon for a move category (0 physical, 1 special, 2 status), or null.</summary>
        public static Bitmap Category(int category)
        {
            string rom = RomInfo.WorkDir;
            if (_cachedFor != rom) { Cache.Clear(); CategoryCache.Clear(); _cachedFor = rom; }
            if (CategoryCache.TryGetValue(category, out Bitmap known)) return known;
            Bitmap icon = category >= 0 && category < BattleObjects.KindOrder.Length ? Render(BattleObjects.KindOrder[category]) : null;
            CategoryCache[category] = icon;
            return icon;
        }

        private static Bitmap Render(string thing) => ToBitmap(Pixels(BattleObjects.Find(thing, "Drawing")));

        private static Bitmap ToBitmap(GraphicAssets.Preview p) => p?.Rgba != null ? ImageConverter.FromRgba(p.Rgba, p.Width, p.Height) : null;

        private static GraphicAssets.Preview Pixels(int member)
        {
            try
            {
                GraphicAssets.Archive archive = GraphicAssets.All.FirstOrDefault(a => a.Dir == DirNames.BattleObj);
                return member < 0 || archive == null ? null : GraphicAssets.Render(archive, member);
            }
            catch (System.Exception ex) { AppLogger.WarnOnce("TypeIcons.Render", ex); return null; }
        }

        /// <summary>A type's icon as raw pixels for composed pictures, or null if there's none.</summary>
        public static GraphicAssets.Preview PixelsFor(int type) => Pixels(BattleObjects.TypeIconMember(type));

        /// <summary>A move category's icon as pixels.</summary>
        public static GraphicAssets.Preview CategoryPixels(int category) =>
            category >= 0 && category < BattleObjects.KindOrder.Length ? Pixels(BattleObjects.Find(BattleObjects.KindOrder[category], "Drawing")) : null;

        /// <summary>
        /// The icon for a type, or null when this ROM has none for it. The game's icon tables run on past the
        /// types into the five contest conditions, so a type number past them draws those pictures, as the game does.
        /// </summary>
        public static Bitmap For(int type)
        {
            string rom = RomInfo.WorkDir;
            if (_cachedFor != rom) { Cache.Clear(); CategoryCache.Clear(); _cachedFor = rom; }
            if (Cache.TryGetValue(type, out Bitmap known)) return known;

            Bitmap icon = ToBitmap(PixelsFor(type));
            Cache[type] = icon;
            return icon;
        }
    }
}
