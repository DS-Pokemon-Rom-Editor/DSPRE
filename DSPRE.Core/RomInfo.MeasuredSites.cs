using System;
using System.Collections.Generic;

namespace DSPRE
{
    public partial class RomInfo
    {
        // The code sites behind the US-only lists and tables, found in other dumps: every site of a record was
        // found exactly once by the bytes around it in the US dump of the same game, or the record is left out.
        // Entries marked as needing verification were measured on dumps that are not known good dumps.

        private static readonly Dictionary<(string id, int rev, GameTable table), MoveListSites> MeasuredMoveListSites = new()
        {
            [("ADAD", 5, GameTable.PunchingMoves)] = new(0x1E86C, 0x1E584, 15),
            [("ADAD", 5, GameTable.SoundMoves)] = new(0x1A044, 0x19FB8, 14),
            [("ADAF", 5, GameTable.PunchingMoves)] = new(0x1E958, 0x1E670, 15),
            [("ADAF", 5, GameTable.SoundMoves)] = new(0x1A130, 0x1A0A4, 14),
            [("ADAI", 5, GameTable.PunchingMoves)] = new(0x1E86C, 0x1E584, 15),
            [("ADAI", 5, GameTable.SoundMoves)] = new(0x1A044, 0x19FB8, 14),
            [("ADAJ", 0, GameTable.PunchingMoves)] = new(0x17D6C, 0x17C78, 15),
            [("ADAJ", 0, GameTable.SoundMoves)] = new(0x1C340, 0x1C2AE, 14),
            [("ADAK", 0, GameTable.PunchingMoves)] = new(0x1E82C, 0x1E544, 15),
            [("ADAK", 0, GameTable.SoundMoves)] = new(0x1A044, 0x19FB8, 14),
            [("ADAS", 5, GameTable.PunchingMoves)] = new(0x1E958, 0x1E670, 15),
            [("ADAS", 5, GameTable.SoundMoves)] = new(0x1A130, 0x1A0A4, 14),
            [("APAD", 5, GameTable.PunchingMoves)] = new(0x1E86C, 0x1E584, 15),
            [("APAD", 5, GameTable.SoundMoves)] = new(0x1A044, 0x19FB8, 14),
            [("APAE", 5, GameTable.PunchingMoves)] = new(0x1E81C, 0x1E534, 15),
            [("APAE", 5, GameTable.SoundMoves)] = new(0x19FF4, 0x19F68, 14),
            [("APAF", 5, GameTable.PunchingMoves)] = new(0x1E958, 0x1E670, 15),
            [("APAF", 5, GameTable.SoundMoves)] = new(0x1A130, 0x1A0A4, 14),
            [("APAI", 5, GameTable.PunchingMoves)] = new(0x1E86C, 0x1E584, 15),
            [("APAI", 5, GameTable.SoundMoves)] = new(0x1A044, 0x19FB8, 14),
            [("APAJ", 0, GameTable.PunchingMoves)] = new(0x17D6C, 0x17C78, 15),
            [("APAJ", 0, GameTable.SoundMoves)] = new(0x1C340, 0x1C2AE, 14),
            [("APAJ", 5, GameTable.PunchingMoves)] = new(0x17D6C, 0x17C78, 15),
            [("APAJ", 5, GameTable.SoundMoves)] = new(0x1C340, 0x1C2AE, 14),
            [("APAJ", 6, GameTable.PunchingMoves)] = new(0x17D6C, 0x17C78, 15),
            [("APAJ", 6, GameTable.SoundMoves)] = new(0x1C340, 0x1C2AE, 14),
            [("APAK", 0, GameTable.PunchingMoves)] = new(0x1E82C, 0x1E544, 15),
            [("APAK", 0, GameTable.SoundMoves)] = new(0x1A044, 0x19FB8, 14),
            [("APAS", 5, GameTable.PunchingMoves)] = new(0x1E958, 0x1E670, 15),
            [("APAS", 5, GameTable.SoundMoves)] = new(0x1A130, 0x1A0A4, 14),
            [("CPUD", 0, GameTable.PunchingMoves)] = new(0x1FC48, 0x1F97C, 15),
            [("CPUD", 0, GameTable.SoundMoves)] = new(0x1B1C0, 0x1B134, 26),
            [("CPUF", 0, GameTable.PunchingMoves)] = new(0x1FD34, 0x1FA68, 15),
            [("CPUF", 0, GameTable.SoundMoves)] = new(0x1B2AC, 0x1B220, 26),
            [("CPUI", 0, GameTable.PunchingMoves)] = new(0x1FC48, 0x1F97C, 15),
            [("CPUI", 0, GameTable.SoundMoves)] = new(0x1B1C0, 0x1B134, 26),
            [("CPUJ", 0, GameTable.PunchingMoves)] = new(0x1FC20, 0x1F954, 15),
            [("CPUJ", 0, GameTable.SoundMoves)] = new(0x1B198, 0x1B10C, 26),
            [("CPUK", 0, GameTable.PunchingMoves)] = new(0x1FC44, 0x1F978, 15),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUK", 0, GameTable.SoundMoves)] = new(0x1B1BC, 0x1B130, 26),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0, GameTable.PunchingMoves)] = new(0x1FD34, 0x1FA68, 15),
            [("CPUS", 0, GameTable.SoundMoves)] = new(0x1B2AC, 0x1B220, 26),
            [("IPGD", 0, GameTable.PunchingMoves)] = new(0x20224, 0x1FF58, 15),
            [("IPGD", 0, GameTable.SoundMoves)] = new(0x1B7A4, 0x1B718, 16),
            [("IPGF", 0, GameTable.PunchingMoves)] = new(0x20224, 0x1FF58, 15),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGF", 0, GameTable.SoundMoves)] = new(0x1B7A4, 0x1B718, 16),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0, GameTable.PunchingMoves)] = new(0x20224, 0x1FF58, 15),
            [("IPGI", 0, GameTable.SoundMoves)] = new(0x1B7A4, 0x1B718, 16),
            [("IPGJ", 0, GameTable.PunchingMoves)] = new(0x2022C, 0x1FF60, 15),
            [("IPGJ", 0, GameTable.SoundMoves)] = new(0x1B7AC, 0x1B720, 16),
            [("IPGK", 0, GameTable.PunchingMoves)] = new(0x20228, 0x1FF5C, 15),
            [("IPGK", 0, GameTable.SoundMoves)] = new(0x1B7A8, 0x1B71C, 16),
            [("IPGS", 0, GameTable.PunchingMoves)] = new(0x20224, 0x1FF58, 15),
            [("IPGS", 0, GameTable.SoundMoves)] = new(0x1B7A4, 0x1B718, 16),
            [("IPKD", 0, GameTable.PunchingMoves)] = new(0x20224, 0x1FF58, 15),
            [("IPKD", 0, GameTable.SoundMoves)] = new(0x1B7A4, 0x1B718, 16),
            [("IPKF", 0, GameTable.PunchingMoves)] = new(0x20224, 0x1FF58, 15),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKF", 0, GameTable.SoundMoves)] = new(0x1B7A4, 0x1B718, 16),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0, GameTable.PunchingMoves)] = new(0x20224, 0x1FF58, 15),
            [("IPKI", 0, GameTable.SoundMoves)] = new(0x1B7A4, 0x1B718, 16),
            [("IPKJ", 0, GameTable.PunchingMoves)] = new(0x2022C, 0x1FF60, 15),
            [("IPKJ", 0, GameTable.SoundMoves)] = new(0x1B7AC, 0x1B720, 16),
            [("IPKK", 0, GameTable.PunchingMoves)] = new(0x20228, 0x1FF5C, 15),
            [("IPKK", 0, GameTable.SoundMoves)] = new(0x1B7A8, 0x1B71C, 16),
            [("IPKS", 0, GameTable.PunchingMoves)] = new(0x20224, 0x1FF58, 15),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKS", 0, GameTable.SoundMoves)] = new(0x1B7A4, 0x1B718, 16),   // needs verifying: measured on a dump that is not a known good dump
        };

        private static readonly Dictionary<(string id, int rev), (int[] col0, int[] col1, int[] col2, int countCompare, int countModulus)> MeasuredTypeChartSites = new()
        {
            [("ADAD", 5)] = (new[] { 0x19028, 0x19150, 0x19620, 0x19674 }, new[] { 0x18CFC, 0x1919C, 0x19624 }, new[] { 0x18D00, 0x191A0, 0x19628 }, 0x195F4, 0x195FC),
            [("ADAF", 5)] = (new[] { 0x19114, 0x1923C, 0x1970C, 0x19760 }, new[] { 0x18DE8, 0x19288, 0x19710 }, new[] { 0x18DEC, 0x1928C, 0x19714 }, 0x196E0, 0x196E8),
            [("ADAI", 5)] = (new[] { 0x19028, 0x19150, 0x19620, 0x19674 }, new[] { 0x18CFC, 0x1919C, 0x19624 }, new[] { 0x18D00, 0x191A0, 0x19628 }, 0x195F4, 0x195FC),
            [("ADAJ", 0)] = (new[] { 0x1CC4C, 0x1CC8C, 0x1D2B8, 0x1D614 }, new[] { 0x1CC90, 0x1D174, 0x1D6E0 }, new[] { 0x1CC94, 0x1D178, 0x1D6E4 }, 0x1CC5E, 0x1CC66),
            [("ADAK", 0)] = (new[] { 0x19028, 0x19150, 0x19620, 0x19674 }, new[] { 0x18CFC, 0x1919C, 0x19624 }, new[] { 0x18D00, 0x191A0, 0x19628 }, 0x195F4, 0x195FC),
            [("ADAS", 5)] = (new[] { 0x19114, 0x1923C, 0x1970C, 0x19760 }, new[] { 0x18DE8, 0x19288, 0x19710 }, new[] { 0x18DEC, 0x1928C, 0x19714 }, 0x196E0, 0x196E8),
            [("APAD", 5)] = (new[] { 0x19028, 0x19150, 0x19620, 0x19674 }, new[] { 0x18CFC, 0x1919C, 0x19624 }, new[] { 0x18D00, 0x191A0, 0x19628 }, 0x195F4, 0x195FC),
            [("APAE", 5)] = (new[] { 0x18FD8, 0x19100, 0x195D0, 0x19624 }, new[] { 0x18CAC, 0x1914C, 0x195D4 }, new[] { 0x18CB0, 0x19150, 0x195D8 }, 0x195A4, 0x195AC),
            [("APAF", 5)] = (new[] { 0x19114, 0x1923C, 0x1970C, 0x19760 }, new[] { 0x18DE8, 0x19288, 0x19710 }, new[] { 0x18DEC, 0x1928C, 0x19714 }, 0x196E0, 0x196E8),
            [("APAI", 5)] = (new[] { 0x19028, 0x19150, 0x19620, 0x19674 }, new[] { 0x18CFC, 0x1919C, 0x19624 }, new[] { 0x18D00, 0x191A0, 0x19628 }, 0x195F4, 0x195FC),
            [("APAJ", 0)] = (new[] { 0x1CC4C, 0x1CC8C, 0x1D2B8, 0x1D614 }, new[] { 0x1CC90, 0x1D174, 0x1D6E0 }, new[] { 0x1CC94, 0x1D178, 0x1D6E4 }, 0x1CC5E, 0x1CC66),
            [("APAJ", 5)] = (new[] { 0x1CC4C, 0x1CC8C, 0x1D2B8, 0x1D614 }, new[] { 0x1CC90, 0x1D174, 0x1D6E0 }, new[] { 0x1CC94, 0x1D178, 0x1D6E4 }, 0x1CC5E, 0x1CC66),
            [("APAJ", 6)] = (new[] { 0x1CC4C, 0x1CC8C, 0x1D2B8, 0x1D614 }, new[] { 0x1CC90, 0x1D174, 0x1D6E0 }, new[] { 0x1CC94, 0x1D178, 0x1D6E4 }, 0x1CC5E, 0x1CC66),
            [("APAK", 0)] = (new[] { 0x19028, 0x19150, 0x19620, 0x19674 }, new[] { 0x18CFC, 0x1919C, 0x19624 }, new[] { 0x18D00, 0x191A0, 0x19628 }, 0x195F4, 0x195FC),
            [("APAS", 5)] = (new[] { 0x19114, 0x1923C, 0x1970C, 0x19760 }, new[] { 0x18DE8, 0x19288, 0x19710 }, new[] { 0x18DEC, 0x1928C, 0x19714 }, 0x196E0, 0x196E8),
            [("CPUD", 0)] = (new[] { 0x1A1A8, 0x1A2D0, 0x1A79C, 0x1A7F0 }, new[] { 0x19E7C, 0x1A31C, 0x1A7A0 }, new[] { 0x19E80, 0x1A320, 0x1A7A4 }, 0x1A770, 0x1A778),
            [("CPUF", 0)] = (new[] { 0x1A294, 0x1A3BC, 0x1A888, 0x1A8DC }, new[] { 0x19F68, 0x1A408, 0x1A88C }, new[] { 0x19F6C, 0x1A40C, 0x1A890 }, 0x1A85C, 0x1A864),
            [("CPUI", 0)] = (new[] { 0x1A1A8, 0x1A2D0, 0x1A79C, 0x1A7F0 }, new[] { 0x19E7C, 0x1A31C, 0x1A7A0 }, new[] { 0x19E80, 0x1A320, 0x1A7A4 }, 0x1A770, 0x1A778),
            [("CPUJ", 0)] = (new[] { 0x1A180, 0x1A2A8, 0x1A774, 0x1A7C8 }, new[] { 0x19E54, 0x1A2F4, 0x1A778 }, new[] { 0x19E58, 0x1A2F8, 0x1A77C }, 0x1A748, 0x1A750),
            [("CPUK", 0)] = (new[] { 0x1A1A4, 0x1A2CC, 0x1A798, 0x1A7EC }, new[] { 0x19E78, 0x1A318, 0x1A79C }, new[] { 0x19E7C, 0x1A31C, 0x1A7A0 }, 0x1A76C, 0x1A774),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0)] = (new[] { 0x1A294, 0x1A3BC, 0x1A888, 0x1A8DC }, new[] { 0x19F68, 0x1A408, 0x1A88C }, new[] { 0x19F6C, 0x1A40C, 0x1A890 }, 0x1A85C, 0x1A864),
            [("IPGD", 0)] = (new[] { 0x1A78C, 0x1A8B4, 0x1AD80, 0x1ADD4 }, new[] { 0x1A460, 0x1A900, 0x1AD84 }, new[] { 0x1A464, 0x1A904, 0x1AD88 }, 0x1AD54, 0x1AD5C),
            [("IPGF", 0)] = (new[] { 0x1A78C, 0x1A8B4, 0x1AD80, 0x1ADD4 }, new[] { 0x1A460, 0x1A900, 0x1AD84 }, new[] { 0x1A464, 0x1A904, 0x1AD88 }, 0x1AD54, 0x1AD5C),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0)] = (new[] { 0x1A78C, 0x1A8B4, 0x1AD80, 0x1ADD4 }, new[] { 0x1A460, 0x1A900, 0x1AD84 }, new[] { 0x1A464, 0x1A904, 0x1AD88 }, 0x1AD54, 0x1AD5C),
            [("IPGJ", 0)] = (new[] { 0x1A794, 0x1A8BC, 0x1AD88, 0x1ADDC }, new[] { 0x1A468, 0x1A908, 0x1AD8C }, new[] { 0x1A46C, 0x1A90C, 0x1AD90 }, 0x1AD5C, 0x1AD64),
            [("IPGK", 0)] = (new[] { 0x1A790, 0x1A8B8, 0x1AD84, 0x1ADD8 }, new[] { 0x1A464, 0x1A904, 0x1AD88 }, new[] { 0x1A468, 0x1A908, 0x1AD8C }, 0x1AD58, 0x1AD60),
            [("IPGS", 0)] = (new[] { 0x1A78C, 0x1A8B4, 0x1AD80, 0x1ADD4 }, new[] { 0x1A460, 0x1A900, 0x1AD84 }, new[] { 0x1A464, 0x1A904, 0x1AD88 }, 0x1AD54, 0x1AD5C),
            [("IPKD", 0)] = (new[] { 0x1A78C, 0x1A8B4, 0x1AD80, 0x1ADD4 }, new[] { 0x1A460, 0x1A900, 0x1AD84 }, new[] { 0x1A464, 0x1A904, 0x1AD88 }, 0x1AD54, 0x1AD5C),
            [("IPKF", 0)] = (new[] { 0x1A78C, 0x1A8B4, 0x1AD80, 0x1ADD4 }, new[] { 0x1A460, 0x1A900, 0x1AD84 }, new[] { 0x1A464, 0x1A904, 0x1AD88 }, 0x1AD54, 0x1AD5C),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0)] = (new[] { 0x1A78C, 0x1A8B4, 0x1AD80, 0x1ADD4 }, new[] { 0x1A460, 0x1A900, 0x1AD84 }, new[] { 0x1A464, 0x1A904, 0x1AD88 }, 0x1AD54, 0x1AD5C),
            [("IPKJ", 0)] = (new[] { 0x1A794, 0x1A8BC, 0x1AD88, 0x1ADDC }, new[] { 0x1A468, 0x1A908, 0x1AD8C }, new[] { 0x1A46C, 0x1A90C, 0x1AD90 }, 0x1AD5C, 0x1AD64),
            [("IPKK", 0)] = (new[] { 0x1A790, 0x1A8B8, 0x1AD84, 0x1ADD8 }, new[] { 0x1A464, 0x1A904, 0x1AD88 }, new[] { 0x1A468, 0x1A908, 0x1AD8C }, 0x1AD58, 0x1AD60),
            [("IPKS", 0)] = (new[] { 0x1A78C, 0x1A8B4, 0x1AD80, 0x1ADD4 }, new[] { 0x1A460, 0x1A900, 0x1AD84 }, new[] { 0x1A464, 0x1A904, 0x1AD88 }, 0x1AD54, 0x1AD5C),   // needs verifying: measured on a dump that is not a known good dump
        };

        private static readonly Dictionary<(string id, int rev), SwarmSites> MeasuredSwarmSites = new()
        {
            [("ADAD", 5)] = new(6, 0x17C48, 28, 4, new[] { 0xC214, 0xC240 }, Array.Empty<int>(), new[] { 0xC206, 0xC21E }),
            [("ADAF", 5)] = new(6, 0x17C48, 28, 4, new[] { 0xC214, 0xC240 }, Array.Empty<int>(), new[] { 0xC206, 0xC21E }),
            [("ADAI", 5)] = new(6, 0x17C48, 28, 4, new[] { 0xC214, 0xC240 }, Array.Empty<int>(), new[] { 0xC206, 0xC21E }),
            [("ADAJ", 0)] = new(6, 0x18778, 28, 4, new[] { 0xC7F8, 0xC810 }, Array.Empty<int>(), new[] { 0xC7D8, 0xC800 }),
            [("ADAK", 0)] = new(6, 0x17C4C, 28, 4, new[] { 0xC214, 0xC240 }, Array.Empty<int>(), new[] { 0xC206, 0xC21E }),
            [("ADAS", 5)] = new(6, 0x17C48, 28, 4, new[] { 0xC214, 0xC240 }, Array.Empty<int>(), new[] { 0xC206, 0xC21E }),
            [("APAD", 5)] = new(6, 0x17C54, 28, 4, new[] { 0xC220, 0xC24C }, Array.Empty<int>(), new[] { 0xC212, 0xC22A }),
            [("APAE", 5)] = new(6, 0x17CAC, 28, 4, new[] { 0xC278, 0xC2A4 }, Array.Empty<int>(), new[] { 0xC26A, 0xC282 }),
            [("APAF", 5)] = new(6, 0x17C54, 28, 4, new[] { 0xC220, 0xC24C }, Array.Empty<int>(), new[] { 0xC212, 0xC22A }),
            [("APAI", 5)] = new(6, 0x17C54, 28, 4, new[] { 0xC220, 0xC24C }, Array.Empty<int>(), new[] { 0xC212, 0xC22A }),
            [("APAJ", 0)] = new(6, 0x18778, 28, 4, new[] { 0xC7F8, 0xC810 }, Array.Empty<int>(), new[] { 0xC7D8, 0xC800 }),
            [("APAJ", 5)] = new(6, 0x18778, 28, 4, new[] { 0xC7F8, 0xC810 }, Array.Empty<int>(), new[] { 0xC7D8, 0xC800 }),
            [("APAJ", 6)] = new(6, 0x18778, 28, 4, new[] { 0xC7F8, 0xC810 }, Array.Empty<int>(), new[] { 0xC7D8, 0xC800 }),
            [("APAK", 0)] = new(6, 0x17C58, 28, 4, new[] { 0xC220, 0xC24C }, Array.Empty<int>(), new[] { 0xC212, 0xC22A }),
            [("APAS", 5)] = new(6, 0x17C54, 28, 4, new[] { 0xC220, 0xC24C }, Array.Empty<int>(), new[] { 0xC212, 0xC22A }),
            [("CPUD", 0)] = new(6, 0xAF50, 22, 4, new[] { 0x50E8, 0x5114 }, Array.Empty<int>(), new[] { 0x50DA, 0x50F2 }),
            [("CPUF", 0)] = new(6, 0xAF50, 22, 4, new[] { 0x50E8, 0x5114 }, Array.Empty<int>(), new[] { 0x50DA, 0x50F2 }),
            [("CPUI", 0)] = new(6, 0xAF50, 22, 4, new[] { 0x50E8, 0x5114 }, Array.Empty<int>(), new[] { 0x50DA, 0x50F2 }),
            [("CPUJ", 0)] = new(6, 0xAF50, 22, 4, new[] { 0x50E8, 0x5114 }, Array.Empty<int>(), new[] { 0x50DA, 0x50F2 }),
            [("CPUK", 0)] = new(6, 0xAF50, 22, 4, new[] { 0x50E8, 0x5114 }, Array.Empty<int>(), new[] { 0x50DA, 0x50F2 }),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0)] = new(6, 0xAF50, 22, 4, new[] { 0x50E8, 0x5114 }, Array.Empty<int>(), new[] { 0x50DA, 0x50F2 }),
            [("IPGD", 0)] = new(-1, 0x108F34, 20, 4, new[] { 0x97F94, 0x97FF0 }, new[] { 0x97F98 }, new[] { 0x97F70, 0x97FA2 }),
            [("IPGF", 0)] = new(-1, 0x108F70, 20, 4, new[] { 0x97F94, 0x97FF0 }, new[] { 0x97F98 }, new[] { 0x97F70, 0x97FA2 }),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0)] = new(-1, 0x108EE0, 20, 4, new[] { 0x97F94, 0x97FF0 }, new[] { 0x97F98 }, new[] { 0x97F70, 0x97FA2 }),
            [("IPGJ", 0)] = new(-1, 0x108498, 20, 4, new[] { 0x97794, 0x977F0 }, new[] { 0x97798 }, new[] { 0x97770, 0x977A2 }),
            [("IPGK", 0)] = new(-1, 0x1096C0, 20, 4, new[] { 0x98680, 0x986DC }, new[] { 0x98684 }, new[] { 0x9865C, 0x9868E }),
            [("IPGS", 0)] = new(-1, 0x108F80, 20, 4, new[] { 0x97F94, 0x97FF0 }, new[] { 0x97F98 }, new[] { 0x97F70, 0x97FA2 }),
            [("IPKD", 0)] = new(-1, 0x108F34, 20, 4, new[] { 0x97F94, 0x97FF0 }, new[] { 0x97F98 }, new[] { 0x97F70, 0x97FA2 }),
            [("IPKF", 0)] = new(-1, 0x108F70, 20, 4, new[] { 0x97F94, 0x97FF0 }, new[] { 0x97F98 }, new[] { 0x97F70, 0x97FA2 }),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0)] = new(-1, 0x108EE0, 20, 4, new[] { 0x97F94, 0x97FF0 }, new[] { 0x97F98 }, new[] { 0x97F70, 0x97FA2 }),
            [("IPKJ", 0)] = new(-1, 0x108498, 20, 4, new[] { 0x97794, 0x977F0 }, new[] { 0x97798 }, new[] { 0x97770, 0x977A2 }),
            [("IPKK", 0)] = new(-1, 0x1096C8, 20, 4, new[] { 0x98688, 0x986E4 }, new[] { 0x9868C }, new[] { 0x98664, 0x98696 }),
            [("IPKS", 0)] = new(-1, 0x108F78, 20, 4, new[] { 0x97F8C, 0x97FE8 }, new[] { 0x97F90 }, new[] { 0x97F68, 0x97F9A }),   // needs verifying: measured on a dump that is not a known good dump
        };

        /// <summary>The first compare of each encounter method's selector, in the order of that game's US methods.</summary>
        private static readonly Dictionary<(string id, int rev), int[]> MeasuredSlotOddsBases = new()
        {
            [("ADAD", 5)] = new[] { 0x2F2A, 0x2FC6, 0x3024, 0x3048, 0x306C },
            [("ADAF", 5)] = new[] { 0x2F2A, 0x2FC6, 0x3024, 0x3048, 0x306C },
            [("ADAI", 5)] = new[] { 0x2F2A, 0x2FC6, 0x3024, 0x3048, 0x306C },
            [("ADAK", 0)] = new[] { 0x2F2A, 0x2FC6, 0x3024, 0x3048, 0x306C },
            [("ADAS", 5)] = new[] { 0x2F2A, 0x2FC6, 0x3024, 0x3048, 0x306C },
            [("APAD", 5)] = new[] { 0x2F36, 0x2FD2, 0x3030, 0x3054, 0x3078 },
            [("APAE", 5)] = new[] { 0x2F36, 0x2FD2, 0x3030, 0x3054, 0x3078 },
            [("APAF", 5)] = new[] { 0x2F36, 0x2FD2, 0x3030, 0x3054, 0x3078 },
            [("APAI", 5)] = new[] { 0x2F36, 0x2FD2, 0x3030, 0x3054, 0x3078 },
            [("APAK", 0)] = new[] { 0x2F36, 0x2FD2, 0x3030, 0x3054, 0x3078 },
            [("APAS", 5)] = new[] { 0x2F36, 0x2FD2, 0x3030, 0x3054, 0x3078 },
            [("CPUD", 0)] = new[] { 0x37E2, 0x387E, 0x38DC, 0x3900, 0x3924 },
            [("CPUF", 0)] = new[] { 0x37E2, 0x387E, 0x38DC, 0x3900, 0x3924 },
            [("CPUI", 0)] = new[] { 0x37E2, 0x387E, 0x38DC, 0x3900, 0x3924 },
            [("CPUJ", 0)] = new[] { 0x37E2, 0x387E, 0x38DC, 0x3900, 0x3924 },
            [("CPUK", 0)] = new[] { 0x37E2, 0x387E, 0x38DC, 0x3900, 0x3924 },   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0)] = new[] { 0x37E2, 0x387E, 0x38DC, 0x3900, 0x3924 },
            [("IPGD", 0)] = new[] { 0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54 },
            [("IPGF", 0)] = new[] { 0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54 },   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0)] = new[] { 0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54 },
            [("IPGJ", 0)] = new[] { 0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54 },
            [("IPGK", 0)] = new[] { 0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54 },
            [("IPGS", 0)] = new[] { 0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54 },
            [("IPKD", 0)] = new[] { 0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54 },
            [("IPKF", 0)] = new[] { 0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54 },   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0)] = new[] { 0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54 },
            [("IPKJ", 0)] = new[] { 0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54 },
            [("IPKK", 0)] = new[] { 0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54 },
            [("IPKS", 0)] = new[] { 0x1B20, 0x1BB4, 0x1BF8, 0x1C30, 0x1C54 },   // needs verifying: measured on a dump that is not a known good dump
        };

        /// <summary>Whole selector layouts where the code differs from the US one (Japanese Diamond and Pearl).</summary>
        private static readonly Dictionary<(string id, int rev), SlotOddsMethod[]> MeasuredSlotOddsMethods = new()
        {
            [("ADAJ", 0)] = new[]
            {
                new SlotOddsMethod("Walking", 6, 12, new[] { new[] { 0x31E0, 0x31E8 }, new[] { 0x31EC, 0x31F4 }, new[] { 0x31F8, 0x3200 }, new[] { 0x3204, 0x320C }, new[] { 0x3210, 0x3218 }, new[] { 0x321C, 0x3224 }, new[] { 0x3228, 0x3230 }, new[] { 0x3234, 0x323C }, new[] { 0x3240, 0x3248 }, new[] { 0x324C } }, 0x74),
                new SlotOddsMethod("Surfing", 6, 5, new[] { new[] { 0x3190, 0x3198 }, new[] { 0x319C, 0x31A4 }, new[] { 0x31A8, 0x31B0 }, new[] { 0x31B4 } }),
                new SlotOddsMethod("Old Rod", 6, 5, new[] { new[] { 0x30FA }, new[] { 0x3102 }, new[] { 0x310A }, new[] { 0x3112 } }),
                new SlotOddsMethod("Good Rod", 6, 5, new[] { new[] { 0x311E }, new[] { 0x3126 }, new[] { 0x312E }, new[] { 0x3136 } }),
                new SlotOddsMethod("Super Rod", 6, 5, new[] { new[] { 0x3142 }, new[] { 0x314A }, new[] { 0x3152 }, new[] { 0x315A } }),
            },
            [("APAJ", 0)] = new[]
            {
                new SlotOddsMethod("Walking", 6, 12, new[] { new[] { 0x31E0, 0x31E8 }, new[] { 0x31EC, 0x31F4 }, new[] { 0x31F8, 0x3200 }, new[] { 0x3204, 0x320C }, new[] { 0x3210, 0x3218 }, new[] { 0x321C, 0x3224 }, new[] { 0x3228, 0x3230 }, new[] { 0x3234, 0x323C }, new[] { 0x3240, 0x3248 }, new[] { 0x324C } }, 0x74),
                new SlotOddsMethod("Surfing", 6, 5, new[] { new[] { 0x3190, 0x3198 }, new[] { 0x319C, 0x31A4 }, new[] { 0x31A8, 0x31B0 }, new[] { 0x31B4 } }),
                new SlotOddsMethod("Old Rod", 6, 5, new[] { new[] { 0x30FA }, new[] { 0x3102 }, new[] { 0x310A }, new[] { 0x3112 } }),
                new SlotOddsMethod("Good Rod", 6, 5, new[] { new[] { 0x311E }, new[] { 0x3126 }, new[] { 0x312E }, new[] { 0x3136 } }),
                new SlotOddsMethod("Super Rod", 6, 5, new[] { new[] { 0x3142 }, new[] { 0x314A }, new[] { 0x3152 }, new[] { 0x315A } }),
            },
            [("APAJ", 5)] = new[]
            {
                new SlotOddsMethod("Walking", 6, 12, new[] { new[] { 0x31E0, 0x31E8 }, new[] { 0x31EC, 0x31F4 }, new[] { 0x31F8, 0x3200 }, new[] { 0x3204, 0x320C }, new[] { 0x3210, 0x3218 }, new[] { 0x321C, 0x3224 }, new[] { 0x3228, 0x3230 }, new[] { 0x3234, 0x323C }, new[] { 0x3240, 0x3248 }, new[] { 0x324C } }, 0x74),
                new SlotOddsMethod("Surfing", 6, 5, new[] { new[] { 0x3190, 0x3198 }, new[] { 0x319C, 0x31A4 }, new[] { 0x31A8, 0x31B0 }, new[] { 0x31B4 } }),
                new SlotOddsMethod("Old Rod", 6, 5, new[] { new[] { 0x30FA }, new[] { 0x3102 }, new[] { 0x310A }, new[] { 0x3112 } }),
                new SlotOddsMethod("Good Rod", 6, 5, new[] { new[] { 0x311E }, new[] { 0x3126 }, new[] { 0x312E }, new[] { 0x3136 } }),
                new SlotOddsMethod("Super Rod", 6, 5, new[] { new[] { 0x3142 }, new[] { 0x314A }, new[] { 0x3152 }, new[] { 0x315A } }),
            },
            [("APAJ", 6)] = new[]
            {
                new SlotOddsMethod("Walking", 6, 12, new[] { new[] { 0x31E0, 0x31E8 }, new[] { 0x31EC, 0x31F4 }, new[] { 0x31F8, 0x3200 }, new[] { 0x3204, 0x320C }, new[] { 0x3210, 0x3218 }, new[] { 0x321C, 0x3224 }, new[] { 0x3228, 0x3230 }, new[] { 0x3234, 0x323C }, new[] { 0x3240, 0x3248 }, new[] { 0x324C } }, 0x74),
                new SlotOddsMethod("Surfing", 6, 5, new[] { new[] { 0x3190, 0x3198 }, new[] { 0x319C, 0x31A4 }, new[] { 0x31A8, 0x31B0 }, new[] { 0x31B4 } }),
                new SlotOddsMethod("Old Rod", 6, 5, new[] { new[] { 0x30FA }, new[] { 0x3102 }, new[] { 0x310A }, new[] { 0x3112 } }),
                new SlotOddsMethod("Good Rod", 6, 5, new[] { new[] { 0x311E }, new[] { 0x3126 }, new[] { 0x312E }, new[] { 0x3136 } }),
                new SlotOddsMethod("Super Rod", 6, 5, new[] { new[] { 0x3142 }, new[] { 0x314A }, new[] { 0x3152 }, new[] { 0x315A } }),
            },
        };

        private static readonly Dictionary<(string id, int rev), BpShopSites> MeasuredBpShopSites = new()
        {
            [("ADAD", 5)] = new(-1, -1, -1, 0xF4352, 0x42B5A),
            [("ADAF", 5)] = new(-1, -1, -1, 0xF4382, 0x42B5A),
            [("ADAI", 5)] = new(-1, -1, -1, 0xF42F6, 0x42B5A),
            [("ADAJ", 0)] = new(-1, -1, -1, 0xF6164, 0x44B04),
            [("ADAK", 0)] = new(-1, -1, -1, 0xEF98E, 0x42F7E),
            [("ADAS", 5)] = new(-1, -1, -1, 0xF438E, 0x42B5A),
            [("APAD", 5)] = new(-1, -1, -1, 0xF4352, 0x42B5A),
            [("APAE", 5)] = new(-1, -1, -1, 0xF433E, 0x42AEA),
            [("APAF", 5)] = new(-1, -1, -1, 0xF4382, 0x42B5A),
            [("APAI", 5)] = new(-1, -1, -1, 0xF42F6, 0x42B5A),
            [("APAJ", 0)] = new(-1, -1, -1, 0xF6168, 0x44B04),
            [("APAJ", 5)] = new(-1, -1, -1, 0xF6168, 0x44B04),
            [("APAJ", 6)] = new(-1, -1, -1, 0xF62A8, 0x44BA8),
            [("APAK", 0)] = new(-1, -1, -1, 0xEF98E, 0x42F7E),
            [("APAS", 5)] = new(-1, -1, -1, 0xF438E, 0x42B5A),
            [("CPUD", 0)] = new(0x100C8C, 0x4F44, 0x4F3A, 0xEBCDA, 0x49D8A),
            [("CPUF", 0)] = new(0x100CCC, 0x4F44, 0x4F3A, 0xEBD0A, 0x49D8A),
            [("CPUI", 0)] = new(0x100C4C, 0x4F44, 0x4F3A, 0xEBC9E, 0x49D8A),
            [("CPUJ", 0)] = new(0xFFEE4, 0x4DA8, 0x4D9E, 0xEB312, 0x495C2),
            [("CPUK", 0)] = new(0x1019CC, 0x4F44, 0x4F3A, 0xEC70A, 0x4A1DA),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0)] = new(0x100CEC, 0x4F44, 0x4F3A, 0xEBD16, 0x49D8A),
        };

        private static readonly Dictionary<(string id, int rev), VsIntroSites> MeasuredVsIntroSites = new()
        {
            [("ADAD", 5)] = new(0x47644, 36, 5, 0x1FFC0, 31, 5, 0x20410, 8, 8, -1, 0x203E8, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
            [("ADAF", 5)] = new(0x47644, 36, 5, 0x1FFC0, 31, 5, 0x20410, 8, 8, -1, 0x203E8, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
            [("ADAI", 5)] = new(0x47644, 36, 5, 0x1FFC0, 31, 5, 0x20410, 8, 8, -1, 0x203E8, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
            [("ADAJ", 0)] = new(0x4A010, 36, 5, 0x21014, 31, 5, 0x21434, 8, 8, -1, 0x2140C, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
            [("ADAK", 0)] = new(0x47AA0, 36, 5, 0x1FFBC, 31, 5, 0x2040C, 8, 8, -1, 0x203E4, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
            [("ADAS", 5)] = new(0x47644, 36, 5, 0x1FFC0, 31, 5, 0x20410, 8, 8, -1, 0x203E8, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
            [("APAD", 5)] = new(0x47644, 36, 5, 0x1FFC0, 31, 5, 0x20410, 8, 8, -1, 0x203E8, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
            [("APAE", 5)] = new(0x475D4, 36, 5, 0x20008, 31, 5, 0x20458, 8, 8, -1, 0x20430, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
            [("APAF", 5)] = new(0x47644, 36, 5, 0x1FFC0, 31, 5, 0x20410, 8, 8, -1, 0x203E8, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
            [("APAI", 5)] = new(0x47644, 36, 5, 0x1FFC0, 31, 5, 0x20410, 8, 8, -1, 0x203E8, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
            [("APAJ", 0)] = new(0x4A010, 36, 5, 0x21014, 31, 5, 0x21434, 8, 8, -1, 0x2140C, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
            [("APAJ", 5)] = new(0x4A010, 36, 5, 0x21014, 31, 5, 0x21434, 8, 8, -1, 0x2140C, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
            [("APAJ", 6)] = new(0x4A0C8, 36, 5, 0x21020, 31, 5, 0x21440, 8, 8, -1, 0x21418, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
            [("APAK", 0)] = new(0x47AA0, 36, 5, 0x1FFBC, 31, 5, 0x2040C, 8, 8, -1, 0x203E4, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
            [("APAS", 5)] = new(0x47644, 36, 5, 0x1FFC0, 31, 5, 0x20410, 8, 8, -1, 0x203E8, -1, -1, -1, -1, Array.Empty<int>(), Array.Empty<int>(), -1, 15, -1, -1, -1, 11, -1),
            [("CPUD", 0)] = new(0x51CD8, 41, 5, 0x28D10, 31, 5, 0x28FD0, 8, 20, -1, 0x28FA8, -1, -1, -1, -1, new[] { 0x14DF8, 0x150FC }, new[] { 0x14F8C, 0x15118 }, 51, 40, 147, 151, 11, -1, -1),
            [("CPUF", 0)] = new(0x51CD8, 41, 5, 0x28CFC, 31, 5, 0x28FBC, 8, 20, -1, 0x28F94, -1, -1, -1, -1, new[] { 0x14DE4, 0x150E8 }, new[] { 0x14F78, 0x15104 }, 51, 40, 147, 151, 11, -1, -1),
            [("CPUI", 0)] = new(0x51CD8, 41, 5, 0x28D04, 31, 5, 0x28FC4, 8, 20, -1, 0x28F9C, -1, -1, -1, -1, new[] { 0x14DEC, 0x150F0 }, new[] { 0x14F80, 0x1510C }, 51, 40, 147, 151, 11, -1, -1),
            [("CPUJ", 0)] = new(0x51514, 41, 5, 0x28AF4, 31, 5, 0x28DA4, 8, 20, -1, 0x28D7C, -1, -1, -1, -1, new[] { 0x14CB0, 0x14FB4 }, new[] { 0x14E44, 0x14FD0 }, 51, 40, 147, 151, 11, -1, -1),
            [("CPUK", 0)] = new(0x52128, 41, 5, 0x28CEC, 31, 5, 0x28FAC, 8, 20, -1, 0x28F84, -1, -1, -1, -1, new[] { 0x14DD8, 0x150DC }, new[] { 0x14F6C, 0x150F8 }, 51, 40, 147, 151, 11, -1, -1),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0)] = new(0x51CD8, 41, 5, 0x28CFC, 31, 5, 0x28FBC, 8, 20, -1, 0x28F94, -1, -1, -1, -1, new[] { 0x14DE4, 0x150E8 }, new[] { 0x14F78, 0x15104 }, 51, 40, 147, 151, 11, -1, -1),
            [("IPGD", 0)] = new(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x517E4, 0x517D4, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),
            [("IPGF", 0)] = new(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x517E4, 0x517D4, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0)] = new(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x517E4, 0x517D4, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),
            [("IPGJ", 0)] = new(-1, 0, 1, 0x20E48, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x5127C, 0x5126C, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),
            [("IPGK", 0)] = new(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x518D8, 0x518C8, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),
            [("IPGS", 0)] = new(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x517E4, 0x517D4, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),
            [("IPKD", 0)] = new(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x517E4, 0x517D4, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),
            [("IPKF", 0)] = new(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x517E4, 0x517D4, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0)] = new(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x517E4, 0x517D4, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),
            [("IPKJ", 0)] = new(-1, 0, 1, 0x20E48, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x5127C, 0x5126C, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),
            [("IPKK", 0)] = new(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x518E0, 0x518D0, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),
            [("IPKS", 0)] = new(-1, 0, 1, 0x20FC4, 47, 115, 0x1390, 16, 20, 0x1354, 0x1368, 117, 0xAAC, 0x517DC, 0x517CC, new[] { 0xBD0, 0xED4 }, Array.Empty<int>(), 59, 48, 207, 211, 16, -1, 215),   // needs verifying: measured on a dump that is not a known good dump
        };

        private static readonly Dictionary<(string id, int rev), VsIntroMotionSites> MeasuredVsIntroMotion = new()
        {
            [("ADAD", 5)] = new(5, 0x20510, 5, 0x1FF34, new[] { 0, 2, 4, 3 }, null),
            [("ADAF", 5)] = new(5, 0x20510, 5, 0x1FF34, new[] { 0, 2, 4, 3 }, null),
            [("ADAI", 5)] = new(5, 0x20510, 5, 0x1FF34, new[] { 0, 2, 4, 3 }, null),
            [("ADAJ", 0)] = new(5, 0x21534, 5, 0x20F88, new[] { 0, 2, 4, 3 }, null),
            [("ADAK", 0)] = new(5, 0x2050C, 5, 0x1FF30, new[] { 0, 2, 4, 3 }, null),
            [("ADAS", 5)] = new(5, 0x20510, 5, 0x1FF34, new[] { 0, 2, 4, 3 }, null),
            [("APAD", 5)] = new(5, 0x20510, 5, 0x1FF34, new[] { 0, 2, 4, 3 }, null),
            [("APAE", 5)] = new(5, 0x20558, 5, 0x1FF7C, new[] { 0, 2, 4, 3 }, null),
            [("APAF", 5)] = new(5, 0x20510, 5, 0x1FF34, new[] { 0, 2, 4, 3 }, null),
            [("APAI", 5)] = new(5, 0x20510, 5, 0x1FF34, new[] { 0, 2, 4, 3 }, null),
            [("APAJ", 0)] = new(5, 0x21534, 5, 0x20F88, new[] { 0, 2, 4, 3 }, null),
            [("APAJ", 5)] = new(5, 0x21534, 5, 0x20F88, new[] { 0, 2, 4, 3 }, null),
            [("APAJ", 6)] = new(5, 0x21540, 5, 0x20F94, new[] { 0, 2, 4, 3 }, null),
            [("APAK", 0)] = new(5, 0x2050C, 5, 0x1FF30, new[] { 0, 2, 4, 3 }, null),
            [("APAS", 5)] = new(5, 0x20510, 5, 0x1FF34, new[] { 0, 2, 4, 3 }, null),
            [("CPUD", 0)] = new(5, 0x29130, 5, 0x28C24, new[] { 0, 2, 4, 3 }, new[] { 107, 108 }),
            [("CPUF", 0)] = new(5, 0x2911C, 5, 0x28C10, new[] { 0, 2, 4, 3 }, new[] { 107, 108 }),
            [("CPUI", 0)] = new(5, 0x29124, 5, 0x28C18, new[] { 0, 2, 4, 3 }, new[] { 107, 108 }),
            [("CPUJ", 0)] = new(5, 0x28F04, 5, 0x28A08, new[] { 0, 2, 4, 3 }, new[] { 107, 108 }),
            [("CPUK", 0)] = new(5, 0x2910C, 5, 0x28C00, new[] { 0, 2, 4, 3 }, new[] { 107, 108 }),   // needs verifying: measured on a dump that is not a known good dump
            [("CPUS", 0)] = new(5, 0x2911C, 5, 0x28C10, new[] { 0, 2, 4, 3 }, new[] { 107, 108 }),
            [("IPGD", 0)] = new(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),
            [("IPGF", 0)] = new(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),   // needs verifying: measured on a dump that is not a known good dump
            [("IPGI", 0)] = new(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),
            [("IPGJ", 0)] = new(117, 0xAD4, 1, 0x20F04, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),
            [("IPGK", 0)] = new(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),
            [("IPGS", 0)] = new(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),
            [("IPKD", 0)] = new(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),
            [("IPKF", 0)] = new(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),   // needs verifying: measured on a dump that is not a known good dump
            [("IPKI", 0)] = new(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),
            [("IPKJ", 0)] = new(117, 0xAD4, 1, 0x20F04, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),
            [("IPKK", 0)] = new(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),
            [("IPKS", 0)] = new(117, 0xAD4, 1, 0x21080, new[] { 3, 156, 158, 157 }, new[] { 151, 152 }),   // needs verifying: measured on a dump that is not a known good dump
        };
    }
}
