using System;
using System.IO;
using System.Security.Cryptography;

namespace DSPRE
{
    /// <summary>Recognises the external trainer shiny patch in USA HG/SS and Italian HG.</summary>
    public static class TrainerShinyPatch
    {
        private const uint Arm9Base = 0x02000000;
        private const uint SyntheticBase = 0x023C8000;
        private const int LoadedCapacity = 0x16000;
        private const int HookOffset = 0x73A84;
        private const int PayloadLength = 156;

        // v0.1.0: SHA-256 after zeroing the six validated BLs and table pointer.
        private const string PayloadFingerprint = "a5c8e302fb7a3fd14e2ad437c3701013fd3fe3bb079c1f8ca2d6ead95df89966";
        private static readonly int[] CallOffsets = { 0x2E, 0x4A, 0x54, 0x60, 0x7C, 0x8C };
        private static readonly uint[] CallTargets = { 0x02074640, 0x02074644, 0x0206E540, 0x0206E540, 0x0206EC40, 0x0201AB0C };
        private static readonly byte[] Loader = {
            0xFC,0xB5,0x05,0x48,0xC0,0x46,0x1C,0x21,0x00,0x22,0x02,0x4D,0xA8,0x47,
            0x00,0x20,0x03,0x21,0xFC,0xBD,0x09,0x75,0x00,0x02,0x00,0x80,0x3C,0x02
        };
        private static readonly byte[] ItalianLoader = {
            0xFC,0xB5,0x04,0x48,0x1C,0x21,0x00,0x22,0xF6,0xF6,0x46,0xFA,
            0x00,0x20,0x03,0x21,0xFC,0xBD,0x00,0x00,0x00,0x80,0x3C,0x02,0x00
        };

        public static bool DetectCurrentProject()
        {
            if ((RomInfo.romID != "IPKE" && RomInfo.romID != "IPGE" && RomInfo.romID != "IPKI") || !RomInfo.IsDsRomProject)
                return false;
            try
            {
                byte[] arm9 = File.ReadAllBytes(RomInfo.arm9Path);
                byte[] synthetic = File.Exists(Filesystem.expArmPath) ? File.ReadAllBytes(Filesystem.expArmPath) : null;
                return Inspect(RomInfo.romID, true, arm9, synthetic);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        public static bool Inspect(string romId, bool dsRomProject, byte[] arm9, byte[] synthetic)
        {
            if ((romId != "IPKE" && romId != "IPGE" && romId != "IPKI") || !dsRomProject)
                return false;
            bool expanded = romId == "IPKI"
                ? Matches(arm9, 0xCD0, 0x10,0xF1,0xCE,0xF9) && Matches(arm9, 0x111070, ItalianLoader)
                : Matches(arm9, 0xCD0, 0x0F,0xF1,0x30,0xFB) && Matches(arm9, 0x110334, Loader);
            if (!expanded)
                return false;
            if (!Matches(arm9, 0x73AA2, 0xB0,0x23))
                return false;
            if (!TryReadBl(arm9, HookOffset, Arm9Base + HookOffset, out uint target))
                return false;
            long offset = (long)target - SyntheticBase;
            if ((target & 3) != 0 || offset < 0 || offset + PayloadLength > LoadedCapacity ||
                synthetic == null || offset + PayloadLength > synthetic.Length)
                return false;

            byte[] payload = new byte[PayloadLength];
            Array.Copy(synthetic, (int)offset, payload, 0, payload.Length);
            for (int i = 0; i < CallOffsets.Length; i++)
            {
                if (!TryReadBl(payload, CallOffsets[i], target + (uint)CallOffsets[i], out uint callee) || callee != CallTargets[i])
                    return false;
                Array.Clear(payload, CallOffsets[i], 4);
            }
            if (BitConverter.ToUInt32(payload, 0x98) != target + 0x94)
                return false;
            Array.Clear(payload, 0x98, 4);
            using (var sha = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(payload)).Replace("-", "").ToLowerInvariant();
                if (hash != PayloadFingerprint)
                    return false;
            }
            return true;
        }

        private static bool Matches(byte[] data, int offset, params byte[] expected)
        {
            if (data == null || offset < 0 || offset > data.Length - expected.Length) return false;
            for (int i = 0; i < expected.Length; i++)
                if (data[offset + i] != expected[i]) return false;
            return true;
        }

        private static bool TryReadBl(byte[] data, int offset, uint address, out uint target)
        {
            target = 0;
            if (data == null || offset < 0 || offset > data.Length - 4 || (address & 1) != 0) return false;
            ushort high = BitConverter.ToUInt16(data, offset), low = BitConverter.ToUInt16(data, offset + 2);
            if ((high & 0xF800) != 0xF000 || (low & 0xF800) != 0xF800) return false;
            int displacement = ((high & 0x7FF) << 12) | ((low & 0x7FF) << 1);
            if ((displacement & 0x400000) != 0) displacement -= 0x800000;
            long destination = (long)address + 4 + displacement;
            if (destination < 0 || destination > uint.MaxValue) return false;
            target = (uint)destination;
            return true;
        }
    }
}
