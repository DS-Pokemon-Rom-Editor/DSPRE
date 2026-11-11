using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.Graphics
{
    internal static class TypeIconPixels
    {
        public static GraphicAssets.Preview For(int type) => Pixels(BattleObjects.TypeIconMember(type));

        /// <summary>A move category's icon (0 physical, 1 special, 2 status), or null.</summary>
        public static GraphicAssets.Preview Category(int category) =>
            category >= 0 && category < BattleObjects.KindOrder.Length ? Pixels(BattleObjects.Find(BattleObjects.KindOrder[category], "Drawing")) : null;

        private static GraphicAssets.Preview Pixels(int member)
        {
            try
            {
                GraphicAssets.Archive archive = GraphicAssets.All.FirstOrDefault(a => a.Dir == DirNames.BattleObj);
                return member < 0 || archive == null ? null : GraphicAssets.Render(archive, member);
            }
            catch (System.Exception ex) { AppLogger.WarnOnce("TypeIcons.Render", ex); return null; }
        }
    }
}
