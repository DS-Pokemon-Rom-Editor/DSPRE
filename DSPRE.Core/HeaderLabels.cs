using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;

namespace DSPRE
{
    /// <summary>What to call a header on screen, in one place so every editor calls it the same thing.</summary>
    public static class HeaderLabels
    {
        private static string _forRom;
        private static List<string> _friendly;

        /// <summary>
        /// One label per header: its number and the place it is, or its internal name where the game
        /// does not say which place it is.
        /// </summary>
        public static IReadOnlyList<string> Friendly()
        {
            string rom = RomInfo.WorkDir;
            if (rom == null || RomInfo.InternalNamesPath == null) return Array.Empty<string>();
            if (_friendly != null && _forRom == rom) return _friendly;

            List<string> built = new List<string>();
            bool trusted = false;
            try
            {
                List<string> internalNames = HeaderLists.GetInternalNames();
                if (internalNames == null) return _friendly = built;

                List<string> places = RomInfo.GetLocationNames();
                bool dynamic = DynamicHeaders;

                // The place lookup only answers properly in some games, so it is used only when nearly
                // every header gives an answer. Half a list of names reads worse than none.
                int answered = 0;
                int[] at = new int[internalNames.Count];
                for (int i = 0; i < internalNames.Count; i++)
                {
                    at[i] = -1;
                    if (MapHeader.TryReadLocationNameIndex((ushort)i, dynamic, out int idx)
                        && places != null && idx >= 0 && idx < places.Count)
                    {
                        at[i] = idx; answered++;
                    }
                }
                bool trust = places != null && internalNames.Count > 0
                             && answered * 10 >= internalNames.Count * 9;
                trusted = trust;

                for (int i = 0; i < internalNames.Count; i++)
                {
                    string place = trust && at[i] >= 0 ? places[at[i]] : null;
                    string name = string.IsNullOrWhiteSpace(place) ? internalNames[i].Trim() : place.Trim();
                    built.Add($"{i:D3} {name}");
                }
            }
            catch (Exception ex) { AppLogger.Warn("Header list names: " + ex.Message); }

            // Something asks for these while the ROM is still opening, before the place names can be
            // read. Keeping that answer would leave every editor showing bare codes for the session.
            if (!trusted) return built;
            _forRom = rom;
            return _friendly = built;
        }

        /// <summary>The place a header is, from the header itself.</summary>
        public static string LocationNameOf(MapHeader header)
        {
            if (header == null) return "";
            try
            {
                List<string> places = RomInfo.GetLocationNames();
                int at = header switch
                {
                    HeaderDP dp => dp.LocationName,
                    HeaderPt pt => pt.LocationName,
                    HeaderHGSS hg => hg.LocationName,
                    _ => -1,
                };
                return at >= 0 && places != null && at < places.Count ? places[at] : "";
            }
            catch (Exception ex) { AppLogger.WarnOnce("HeaderLabels.LocationNameOf", ex); return ""; }
        }

        /// <summary>Whether the dynamic headers patch is applied; the folder alone doesn't say so.</summary>
        public static bool DynamicHeaders =>
            ROMFiles.MapHeader.UsesDynamicHeaders;

        /// <summary>Drops the cache, for when a different ROM is opened.</summary>
        public static void Forget() { _friendly = null; _forRom = null; }
    }
}
