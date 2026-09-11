using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;

namespace DSPRE
{
    /// <summary>
    /// Which message archive each Pokégear contact speaks from. Message 0 of that archive is the contact's
    /// name. The table sits in ARM9 and is found through the pointer to it, so no address is assumed.
    /// </summary>
    public static class PokegearContactArchives
    {
        private static string _forRom;
        private static int[] _cached;

        /// <summary>Archive IDs by contact, or null when the table can't be found.</summary>
        public static int[] Find(int contactCount)
        {
            string rom = RomInfo.workDir ?? "";
            if (_forRom == rom && _cached != null && _cached.Length == contactCount) return _cached;

            _cached = null;
            _forRom = rom;
            if (!PokegearPhoneBook.IsSupported || contactCount <= 0) return null;

            try
            {
                if (ARM9.CheckCompressionMark() || !File.Exists(RomInfo.arm9Path)) return null;
                byte[] arm9 = File.ReadAllBytes(RomInfo.arm9Path);
                _cached = Locate(arm9, ARM9.address, contactCount, Filesystem.GetTextArchivesCount());
            }
            catch (Exception ex)
            {
                AppLogger.Error("PokegearContactArchives: " + ex.Message);
            }
            return _cached;
        }

        /// <summary>
        /// Every word pointing inside ARM9 is tried as the table: one archive per contact, all different,
        /// all real archives and close together, as the game lays them out.
        /// </summary>
        internal static int[] Locate(byte[] arm9, uint loadAddress, int contactCount, int archiveCount)
        {
            int size = contactCount * 2;
            var found = new HashSet<int>();

            for (int at = 0; at + 4 <= arm9.Length; at += 4)
            {
                uint word = BitConverter.ToUInt32(arm9, at);
                if (word < loadAddress || (word & 1) != 0) continue;
                long offset = word - loadAddress;
                if (offset + size > arm9.Length || found.Contains((int)offset)) continue;
                if (LooksLikeTable(arm9, (int)offset, contactCount, archiveCount)) found.Add((int)offset);
            }

            if (found.Count != 1) return null;
            int start = found.First();
            return Enumerable.Range(0, contactCount).Select(i => (int)BitConverter.ToUInt16(arm9, start + i * 2)).ToArray();
        }

        private static bool LooksLikeTable(byte[] data, int offset, int count, int archiveCount)
        {
            var seen = new HashSet<ushort>();
            ushort min = ushort.MaxValue, max = 0;
            for (int i = 0; i < count; i++)
            {
                ushort id = BitConverter.ToUInt16(data, offset + i * 2);
                if (id == 0 || id >= archiveCount || !seen.Add(id)) return false;
                min = Math.Min(min, id);
                max = Math.Max(max, id);
            }
            return max - min < count * 2;
        }

        /// <summary>Each contact's name from its archive, or "Contact N" where it can't be read.</summary>
        public static string[] Names(int contactCount)
        {
            var names = new string[contactCount];
            int[] archives = Find(contactCount);
            for (int i = 0; i < contactCount; i++)
            {
                string name = null;
                if (archives != null)
                {
                    try { name = new TextArchive(archives[i]).messages.FirstOrDefault(); }
                    catch (Exception ex) { AppLogger.Error($"PokegearContactArchives: archive {archives[i]}: {ex.Message}"); }
                }
                names[i] = string.IsNullOrWhiteSpace(name) ? $"Contact {i}" : name.Trim();
            }
            return names;
        }

        public static void Forget() { _cached = null; _forRom = null; }
    }
}
