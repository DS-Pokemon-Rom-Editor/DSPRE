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
        /// One label per header: its number, its internal name, and the place it is, where the game
        /// answers for it.
        /// </summary>
        public static IReadOnlyList<string> Friendly()
        {
            string rom = RomInfo.workDir ?? "";
            if (_friendly != null && _forRom == rom) return _friendly;

            var built = new List<string>();
            bool trusted = false;
            try
            {
                var internalNames = HeaderLists.GetHeaderListBoxNames();
                if (internalNames == null) return _friendly = built;

                var places = RomInfo.GetLocationNames();
                bool dynamic = DynamicHeaders;

                // The place lookup only answers properly in some games, so it is used only when nearly
                // every header gives an answer. Half a list of names reads worse than none.
                int answered = 0;
                var at = new int[internalNames.Count];
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
                    string label = internalNames[i].TrimEnd('\0').TrimEnd();
                    string place = trust && at[i] >= 0 ? places[at[i]] : null;
                    built.Add(string.IsNullOrWhiteSpace(place) ? label : label + "   " + place);
                }
            }
            catch { }

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
                var places = RomInfo.GetLocationNames();
                int at = header switch
                {
                    HeaderDP dp => dp.locationName,
                    HeaderPt pt => pt.locationName,
                    HeaderHGSS hg => hg.locationName,
                    _ => -1,
                };
                return at >= 0 && places != null && at < places.Count ? places[at] : "";
            }
            catch { return ""; }
        }

        /// <summary>Whether headers are read from their own files rather than out of arm9. Having a
        /// folder for them is not the same as the patch being applied.</summary>
        public static bool DynamicHeaders =>
            RomPatchState.flag_DynamicHeadersPatchApplied || PatchToolboxLogic.CheckFilesDynamicHeadersPatchApplied();

        /// <summary>Drops the cache, for when a different ROM is opened.</summary>
        public static void Forget() { _friendly = null; _forRom = null; }
    }
}
