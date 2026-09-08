using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>One span of a binary that hg-engine's build writes over.</summary>
    public sealed class HgEngineClaim
    {
        /// <summary>-1 for arm9, otherwise the overlay number.</summary>
        public int OverlayNumber { get; init; }

        public long Offset { get; init; }
        public int Length { get; init; }

        /// <summary>The list it came from, and the symbol or bytes it names.</summary>
        public string Source { get; init; }
        public string Detail { get; init; }

        public bool Overlaps(long offset, int length) =>
            offset < Offset + Length && Offset < offset + Math.Max(length, 1);

        public override string ToString() =>
            $"{Source} {Detail} at 0x{Offset:X} ({Length} bytes)";
    }

    /// <summary>
    /// The parts of arm9 and the overlays hg-engine's build patches, read from the checkout's own
    /// hooks, armhooks, bytereplacement and repoints lists. A DSPRE write outside these is untouched by
    /// a build; one inside them is overwritten on the next compile.
    ///
    /// Lines inside a false #ifdef are counted too, so the map claims a little more than a given build
    /// configuration will actually write. Over-claiming is the safe direction.
    /// </summary>
    public static class HgEngineClaimedRanges
    {
        private static Dictionary<int, List<HgEngineClaim>> _byOverlay;
        private static string _cachedFor;

        public static void ClearCache()
        {
            _byOverlay = null;
            _cachedFor = null;
        }

        /// <summary>Everything claimed in one binary. -1 is arm9.</summary>
        public static IReadOnlyList<HgEngineClaim> For(int overlayNumber)
        {
            var map = Load();
            return map.TryGetValue(overlayNumber, out var claims) ? claims : Array.Empty<HgEngineClaim>();
        }

        /// <summary>The first claim a write would land in, or null when nothing claims it.</summary>
        public static HgEngineClaim Claiming(int overlayNumber, long offset, int length)
            => For(overlayNumber).FirstOrDefault(c => c.Overlaps(offset, length));

        private static Dictionary<int, List<HgEngineClaim>> Load()
        {
            string root = HgEngineProject.IsActive ? HgEngineProject.RepoRootWindows : null;
            if (_byOverlay != null && _cachedFor == root) return _byOverlay;

            _cachedFor = root;
            _byOverlay = root == null
                ? new Dictionary<int, List<HgEngineClaim>>()
                : ReadAll(root, OverlayRamAddress);
            return _byOverlay;
        }

        private static long OverlayRamAddress(int overlayNumber)
        {
            try { return OverlayUtils.OverlayTable.GetRAMAddress(overlayNumber); }
            catch (Exception ex)
            {
                AppLogger.Error("HgEngineClaimedRanges.OverlayRamAddress: " + ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Built from the checkout's own patch lists, so there is one parser and one piece of offset
        /// arithmetic rather than a second copy that can drift from it.
        /// </summary>
        internal static Dictionary<int, List<HgEngineClaim>> ReadAll(string root, Func<int, long> overlayRam)
        {
            var map = new Dictionary<int, List<HgEngineClaim>>();

            foreach (HgEnginePatchList list in HgEnginePatchList.ReadAllAt(root))
            {
                foreach (HgEnginePatchEntry entry in list.Entries.Where(e => e.Parsed))
                {
                    long offset = entry.FileOffset(overlayRam);
                    if (offset < 0) continue;

                    if (!map.TryGetValue(entry.OverlayNumber, out var claims))
                    {
                        claims = new List<HgEngineClaim>();
                        map[entry.OverlayNumber] = claims;
                    }
                    claims.Add(new HgEngineClaim
                    {
                        OverlayNumber = entry.OverlayNumber,
                        Offset = offset,
                        Length = entry.Length,
                        Source = list.FileName,
                        Detail = entry.Kind == HgEnginePatchKind.ByteReplacement
                            ? $"{entry.Bytes.Count} byte(s)" : entry.Symbol,
                    });
                }
            }

            return map;
        }

        internal static bool TryBinary(string field, out int overlayNumber)
        {
            if (string.Equals(field, "arm9", StringComparison.OrdinalIgnoreCase))
            {
                overlayNumber = -1;
                return true;
            }
            return int.TryParse(field, NumberStyles.Integer, CultureInfo.InvariantCulture, out overlayNumber);
        }

    }
}
