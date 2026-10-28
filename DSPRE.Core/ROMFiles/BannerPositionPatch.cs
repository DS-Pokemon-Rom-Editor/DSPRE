using System;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Lets the place-name banner sit at any column and row. PlaceNameBanner sets them.
    /// Source: Resources/ROMToolboxDB/BannerPosition/banner_position.s.
    /// </summary>
    public static class BannerPositionPatch
    {
        public static bool SupportsCurrentRom => PlaceNameBanner.SupportsCurrentRom;

        public static bool IsApplied => PlaceNameBanner.Read()?.Patched == true;

        public static string WhyNotApplicable()
        {
            if (!SupportsCurrentRom) return "Platinum, HeartGold and SoulSilver only";
            return PlaceNameBanner.Read() == null ? "Something else has already changed the banner code, so DSPRE leaves it alone." : null;
        }

        public static void Apply()
        {
            PlaceNameBanner banner = PlaceNameBanner.Read() ?? throw new InvalidOperationException("The place-name banner's code wasn't found.");
            if (!banner.Patched) banner.WritePositionCode();
        }
    }
}
