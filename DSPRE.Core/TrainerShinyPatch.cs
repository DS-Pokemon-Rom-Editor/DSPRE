using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace DSPRE
{
    /// <summary>
    /// Recognises the external trainer shiny patch (an armips patch shared with PR #271) in US HeartGold and SoulSilver
    /// and Italian HeartGold. As its author describes it, the patched game makes a party member with
    /// <see cref="ROMFiles.PartyPokemon.ForceShiny"/> set shiny by changing the secret half of its OT ID, leaving its
    /// PID, nature, ability and IVs as they would be without the flag.
    /// </summary>
    public static class TrainerShinyPatch
    {
        private const int PayloadLength = 156;

        // Patch v0.1.0: SHA-256 of the payload after zeroing the six checked BLs and the table pointer.
        private const string PayloadFingerprint = "a5c8e302fb7a3fd14e2ad437c3701013fd3fe3bb079c1f8ca2d6ead95df89966";
        private static readonly int[] CallOffsets = { 0x2E, 0x4A, 0x54, 0x60, 0x7C, 0x8C };

        public static bool DetectCurrentProject()
        {
            // The routine sits in the synthetic overlay, so the ARM9 expansion must be in place too.
            if (!SupportsCurrentRom || !RomInfo.IsDsRomProject || !PatchToolboxLogic.CheckFilesArm9ExpansionApplied()) return false;
            try
            {
                byte[] arm9 = File.ReadAllBytes(RomInfo.arm9Path);
                byte[] synthetic = File.Exists(Filesystem.expArmPath) ? File.ReadAllBytes(Filesystem.expArmPath) : null;
                return Inspect(RomLocator.TrainerShinySites, arm9, synthetic);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>The synthetic overlay bytes the routine takes when the ARM9 is hooked to it, whatever is there now.</summary>
        public static (long Start, long End)? InstalledRange(byte[] arm9)
        {
            RomInfo.TrainerShinySites sites = RomLocator.TrainerShinySites;
            if (sites == null || !Matches(arm9, sites.Mask, PatchedMask)) return null;
            if (!TryReadBl(arm9, sites.Hook, ARM9.address + (uint)sites.Hook, out uint target)) return null;
            long offset = (long)target - RomInfo.synthOverlayLoadAddress;
            if (offset < 0 || offset + PayloadLength > RomInfo.synthOverlaySize) return null;
            return (offset, offset + PayloadLength);
        }

        private static bool Inspect(RomInfo.TrainerShinySites sites, byte[] arm9, byte[] synthetic)
        {
            if (!Matches(arm9, sites.Mask, PatchedMask)) return false;
            if (!TryReadBl(arm9, sites.Hook, ARM9.address + (uint)sites.Hook, out uint target)) return false;

            long offset = (long)target - RomInfo.synthOverlayLoadAddress;
            if ((target & 3) != 0 || offset < 0 || offset + PayloadLength > RomInfo.synthOverlaySize ||
                synthetic == null || offset + PayloadLength > synthetic.Length)
                return false;

            byte[] payload = new byte[PayloadLength];
            Array.Copy(synthetic, (int)offset, payload, 0, payload.Length);
            for (int i = 0; i < CallOffsets.Length; i++)
            {
                if (!TryReadBl(payload, CallOffsets[i], target + (uint)CallOffsets[i], out uint callee) || callee != sites.CallTargets[i])
                    return false;
                Array.Clear(payload, CallOffsets[i], 4);
            }
            if (BitConverter.ToUInt32(payload, 0x98) != target + 0x94) return false;
            Array.Clear(payload, 0x98, 4);

            string hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
            return hash == PayloadFingerprint;
        }

        // The patch's routine as armips builds it, with its six BLs and its pointer to the record strides zeroed;
        // they are filled in for wherever the routine is placed. Hashes to PayloadFingerprint.
        private static readonly byte[] PayloadTemplate =
        {
            0xF0, 0xB5, 0x87, 0xB0, 0x00, 0x90, 0x10, 0x99, 0x62, 0x19, 0x28, 0x32, 0x13, 0x78, 0x03, 0x2B,
            0x3B, 0xD8, 0xD7, 0x78, 0x00, 0x2F, 0x38, 0xD0, 0x06, 0x2F, 0x00, 0xD9, 0x06, 0x27, 0x89, 0x00,
            0x61, 0x18, 0x4C, 0x68, 0x05, 0x1C, 0x1C, 0x4A, 0xD2, 0x5C, 0x01, 0x92, 0x20, 0x1C, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x28, 0x29, 0xDD, 0xB8, 0x42, 0x00, 0xDA, 0x07, 0x1C, 0x00, 0x26, 0x68, 0x78,
            0x40, 0x21, 0x08, 0x42, 0x1C, 0xD0, 0x20, 0x1C, 0x31, 0x1C, 0x00, 0x00, 0x00, 0x00, 0x03, 0x90,
            0x00, 0x21, 0x00, 0x22, 0x00, 0x00, 0x00, 0x00, 0x04, 0x90, 0x03, 0x98, 0x07, 0x21, 0x00, 0x22,
            0x00, 0x00, 0x00, 0x00, 0x04, 0x99, 0x0A, 0x0C, 0x51, 0x40, 0x02, 0x04, 0x12, 0x0C, 0x51, 0x40,
            0x09, 0x04, 0x11, 0x43, 0x05, 0x91, 0x03, 0x98, 0x07, 0x21, 0x05, 0xAA, 0x00, 0x00, 0x00, 0x00,
            0x01, 0x98, 0x2D, 0x18, 0x76, 0x1C, 0xBE, 0x42, 0xD9, 0xDB, 0x00, 0x98, 0x00, 0x00, 0x00, 0x00,
            0x07, 0xB0, 0xF0, 0xBD, 0x08, 0x10, 0x0A, 0x12, 0x00, 0x00, 0x00, 0x00,
        };

        // ARM9 before the patch: the call that frees the raw party records (the last routine the payload calls), and the
        // ability mask (mov r3, #0xF0).
        private static readonly byte[] OriginalMask = { 0xF0, 0x23 };
        private static readonly byte[] PatchedMask = { 0xB0, 0x23 };


        public static bool SupportsCurrentRom => RomLocator.TrainerShinySites != null && PatchToolboxLogic.Arm9ExpansionWhyNot() == null;

        /// <summary>The routine for a given runtime address.</summary>
        public static byte[] BuildPayload(uint address)
        {
            byte[] payload = (byte[])PayloadTemplate.Clone();
            for (int i = 0; i < CallOffsets.Length; i++)
                PatchToolboxLogic.BuildThumbBl(address + (uint)CallOffsets[i], RomLocator.TrainerShinySites.CallTargets[i]).CopyTo(payload, CallOffsets[i]);
            BitConverter.GetBytes(address + 0x94).CopyTo(payload, 0x98);
            return payload;
        }

        /// <summary>Why the ARM9 can't take the patch, or null when its two hook sites are as the game ships them.</summary>
        public static string WhyNotApplicable(byte[] arm9)
        {
            RomInfo.TrainerShinySites sites = RomLocator.TrainerShinySites;
            bool freesRecords = TryReadBl(arm9, sites.Hook, ARM9.address + (uint)sites.Hook, out uint callee) && callee == sites.CallTargets[^1];
            if (!freesRecords || !Matches(arm9, sites.Mask, OriginalMask))
                return "The ARM9 code this patch hooks has already been changed.";
            return null;
        }

        /// <summary>Writes the routine at <paramref name="payloadOffset"/> in the synthetic overlay and hooks the ARM9 to it.</summary>
        public static void Apply(uint payloadOffset)
        {
            RomInfo.TrainerShinySites sites = RomLocator.TrainerShinySites;
            uint address = RomInfo.synthOverlayLoadAddress + payloadOffset;
            DSUtils.WriteToFile(Filesystem.expArmPath, BuildPayload(address), payloadOffset);
            ARM9.WriteBytes(PatchToolboxLogic.BuildThumbBl(ARM9.address + (uint)sites.Hook, address), (uint)sites.Hook);
            ARM9.WriteBytes(PatchedMask, (uint)sites.Mask);
        }

        /// <summary>The routine with its relocated parts zeroed, for the fingerprint and the range status.</summary>
        public static byte[] Template => (byte[])PayloadTemplate.Clone();

        /// <summary>
        /// The ability half of a party member's flag byte. With the patch the shiny bit is not part of it, matching
        /// the game's PID; without it the whole high nibble is, as the unpatched game reads it.
        /// </summary>
        public static int AbilityOverride(byte flags, bool patched) => (flags & (patched ? 0xB0 : 0xF0)) >> 4;

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
