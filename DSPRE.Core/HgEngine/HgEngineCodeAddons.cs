using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// hg-engine builds its data tables into the archive a/0/2/8, which its own code calls
    /// ARC_CODE_ADDONS, and reads them back by fixed member index, so one member left behind by an
    /// earlier build shifts every table the game reads. Indices are its own, from
    /// include/constants/file.h.
    ///
    /// These are tables, not code: hg-engine's code lives in overlay 129, which its armips hook loads
    /// as the ARM9 expansion in place of the synthetic overlay DSPRE builds for a vanilla ROM. DSPRE
    /// still files this archive under <see cref="RomInfo.DirNames.synthOverlay"/>, which is its own
    /// name for it from that arrangement, and means nothing about where hg-engine keeps code.
    /// </summary>
    public static class HgEngineCodeAddons
    {
        /// <summary>Members a vanilla HGSS a/0/2/8 holds; hg-engine's tables follow them.</summary>
        public const int VanillaMembers = 7;

        public const int HiddenAbilities = 7;
        public const int BaseExperience = 8;
        public const int IconPalettes = 9;
        public const int OverworldFormFemale = 10;
        public const int FormData = 11;
        public const int FormToSpecies = 12;
        public const int FormReversion = 13;
        public const int MachineLearnsets = 14;
        public const int TutorLearnsets = 15;
        public const int BattleTests = 16;
        public const int BackgroundGraphics = 17;
        public const int HiddenItemParams = 18;
        public const int AbilityFlags = 19;

        /// <summary>The tables in the order hg-engine packs them, starting at <see cref="HiddenAbilities"/>.</summary>
        public static readonly IReadOnlyList<string> TableNames = new[]
        {
            "Hidden abilities", "Base experience", "Icon palettes", "Overworld female forms",
            "Form data", "Form to species", "Form reversion", "TM/HM learnsets", "Tutor learnsets",
            "Battle tests", "Background graphics", "Hidden item parameters", "Ability flags",
        };

        public static int TableCount => TableNames.Count;
        public static int HealthyMemberCount => VanillaMembers + TableCount;

        /// <summary>Palette banks a party icon may use, so a stored id outside them is not a bank at all.</summary>
        public const int IconPaletteBanks = 3;

        public static string NameOfTable(int memberIndex)
        {
            int table = memberIndex - HiddenAbilities;
            return table >= 0 && table < TableNames.Count ? TableNames[table] : null;
        }

        /// <summary>One member of the archive as the game would find it.</summary>
        public static byte[] Member(int index)
        {
            if (!RomInfo.isHGE || index < 0) return null;
            try
            {
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.synthOverlay });
                string path = Filesystem.GetSynthOerlayPath(index);
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            catch (Exception ex)
            {
                AppLogger.Error("HgEngineCodeAddons.Member: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Species as the ROM itself counts them, from the packed personal archive. The unpacked copy is
        /// not the same thing once an hg-engine checkout is linked: it holds that checkout's build, and
        /// asking for it rebuilds from source.
        /// </summary>
        public static int SpeciesCountFromRom()
        {
            try
            {
                if (!RomInfo.gameDirs.TryGetValue(RomInfo.DirNames.personalPokeData, out var dirs)) return 0;
                return File.Exists(dirs.packedDir) ? new Editors.Utils.NarcReader(dirs.packedDir).Entrys : 0;
            }
            catch (Exception ex)
            {
                AppLogger.Error("HgEngineCodeAddons.SpeciesCountFromRom: " + ex.Message);
                return 0;
            }
        }

        public static int MemberCount()
        {
            if (!RomInfo.isHGE) return 0;
            try
            {
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.synthOverlay });
                string dir = Filesystem.synthOverlay;
                return Directory.Exists(dir) ? Directory.GetFiles(dir).Length : 0;
            }
            catch (Exception ex)
            {
                AppLogger.Error("HgEngineCodeAddons.MemberCount: " + ex.Message);
                return 0;
            }
        }

        /// <summary>Why a species has no palette bank to draw with.</summary>
        public enum PaletteStatus { Ok, NoTable, PastEndOfTable, NotABank }

        /// <summary>
        /// The palette bank the game itself reads for a species, taken from the member hg-engine's code
        /// reads rather than from the vanilla ARM9 table, which an hg-engine build does not keep.
        /// </summary>
        public static PaletteStatus ReadIconPaletteId(int species, out int paletteId)
        {
            paletteId = 0;
            byte[] table = Member(IconPalettes);
            if (table == null || table.Length == 0) return PaletteStatus.NoTable;
            if (species < 0 || species >= table.Length) return PaletteStatus.PastEndOfTable;

            paletteId = table[species];
            return paletteId < IconPaletteBanks ? PaletteStatus.Ok : PaletteStatus.NotABank;
        }

        public sealed class MemberInfo
        {
            public int Index;
            public int Length;
            /// <summary>What the game reads this member as, which is not what it holds when the archive is shifted.</summary>
            public string ReadAs;
            /// <summary>The table this member actually holds, when it could be identified.</summary>
            public string Holds;
            public bool IsStale;
        }

        public sealed class Layout
        {
            public IReadOnlyList<MemberInfo> Members = Array.Empty<MemberInfo>();
            /// <summary>Where the real icon palette table sits, or -1 when it could not be found.</summary>
            public int IconPaletteMemberIndex = -1;
            /// <summary>Where hg-engine's table block starts, or -1. It belongs at <see cref="HiddenAbilities"/>.</summary>
            public int TableBlockStart = -1;
            public bool IsHealthy;
            /// <summary>Members to drop so the remaining ones sit at the indices the game reads.</summary>
            public IReadOnlyList<int> StaleMembers = Array.Empty<int>();
            public string Summary = "";
        }

        /// <summary>
        /// Reads the archive and works out whether hg-engine's tables sit where its code reads them. The
        /// icon palette table is the anchor: it is one byte per species and every byte names a bank, which
        /// no other table looks like.
        /// </summary>
        /// <summary>The layout of the ROM's own archive, measured against the ROM's own species count.</summary>
        public static Layout Describe() => Describe(SpeciesCountFromRom());

        public static Layout Describe(int speciesCount)
        {
            int count = MemberCount();
            var members = new byte[count][];
            for (int i = 0; i < count; i++) members[i] = Member(i);
            return DescribeMembers(members, speciesCount);
        }

        /// <summary>The reading of a layout on its own, for callers that already hold the members.</summary>
        public static Layout DescribeMembers(IReadOnlyList<byte[]> memberBytes, int speciesCount)
        {
            var layout = new Layout();
            int count = memberBytes?.Count ?? 0;
            if (count == 0)
            {
                layout.Summary = "No a/0/2/8 members were found for this ROM.";
                return layout;
            }

            var members = new List<MemberInfo>();
            for (int i = 0; i < count; i++)
            {
                members.Add(new MemberInfo { Index = i, Length = memberBytes[i]?.Length ?? 0, ReadAs = NameOfTable(i) });
            }

            layout.IconPaletteMemberIndex = FindIconPaletteTable(memberBytes, speciesCount);
            if (layout.IconPaletteMemberIndex >= 0)
            {
                layout.TableBlockStart = layout.IconPaletteMemberIndex - (IconPalettes - HiddenAbilities);
            }

            if (layout.TableBlockStart >= VanillaMembers && layout.TableBlockStart + TableCount <= count)
            {
                for (int t = 0; t < TableCount; t++) members[layout.TableBlockStart + t].Holds = TableNames[t];

                var keep = new HashSet<int>(Enumerable.Range(0, VanillaMembers)
                    .Concat(Enumerable.Range(layout.TableBlockStart, TableCount)));
                var stale = Enumerable.Range(0, count).Where(i => !keep.Contains(i)).ToList();
                foreach (int i in stale) members[i].IsStale = true;
                layout.StaleMembers = stale;
            }

            layout.Members = members;
            layout.IsHealthy = layout.TableBlockStart == HiddenAbilities && count == HealthyMemberCount;
            layout.Summary = SummaryFor(layout, count);
            return layout;
        }

        private static int FindIconPaletteTable(IReadOnlyList<byte[]> memberBytes, int speciesCount)
        {
            if (speciesCount > 0)
            {
                for (int i = 0; i < memberBytes.Count; i++)
                {
                    byte[] bytes = memberBytes[i];
                    if (bytes == null || bytes.Length != speciesCount) continue;
                    if (bytes.All(b => b < IconPaletteBanks)) return i;
                }
            }

            // Failing that, the block names itself: hidden abilities and base experience are two bytes a
            // species and sit immediately before the icon palettes, which are one, so a run of equal,
            // equal, half where the last holds nothing but bank numbers is the table.
            for (int i = 0; i + 2 < memberBytes.Count; i++)
            {
                byte[] first = memberBytes[i], second = memberBytes[i + 1], third = memberBytes[i + 2];
                if (first == null || second == null || third == null) continue;
                if (third.Length == 0 || first.Length != second.Length || first.Length != third.Length * 2) continue;
                if (third.All(b => b < IconPaletteBanks)) return i + 2;
            }
            return -1;
        }

        private static string SummaryFor(Layout layout, int count)
        {
            if (layout.IsHealthy) return $"All {TableCount} hg-engine tables sit where the game reads them.";

            if (layout.TableBlockStart < 0)
            {
                return $"This archive holds {count} members and hg-engine's tables could not be identified in it. "
                     + $"A healthy build has {HealthyMemberCount}.";
            }

            int shift = layout.TableBlockStart - HiddenAbilities;
            string shifted = shift == 0
                ? "hg-engine's tables start where the game reads them"
                : $"hg-engine's tables start {shift} {(Math.Abs(shift) == 1 ? "member" : "members")} "
                  + (shift > 0 ? "later than" : "earlier than") + " the game reads them";

            return $"{shifted}, so the game reads the wrong member for every one of them. This archive holds "
                 + $"{count} members where a healthy build has {HealthyMemberCount}; {layout.StaleMembers.Count} "
                 + "left over from earlier builds can be dropped.";
        }
    }
}
