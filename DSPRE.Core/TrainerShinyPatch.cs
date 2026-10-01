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
        private const uint Arm9Base = 0x02000000;
        private const uint SyntheticBase = 0x023C8000;
        private const int LoadedCapacity = 0x16000;
        private const int HookOffset = 0x73A84;
        private const int PayloadLength = 156;

        // Patch v0.1.0: SHA-256 of the payload after zeroing the six checked BLs and the table pointer.
        private const string PayloadFingerprint = "a5c8e302fb7a3fd14e2ad437c3701013fd3fe3bb079c1f8ca2d6ead95df89966";
        private static readonly int[] CallOffsets = { 0x2E, 0x4A, 0x54, 0x60, 0x7C, 0x8C };
        private static readonly uint[] CallTargets = { 0x02074640, 0x02074644, 0x0206E540, 0x0206E540, 0x0206EC40, 0x0201AB0C };

        /// <summary>Per ROM: the ARM9 expansion's call into its loader and the loader itself, which the patch needs.</summary>
        private sealed record Expansion(byte[] Call, int LoaderAt, byte[] Loader);

        private static readonly byte[] UsLoader =
        {
            0xFC,0xB5,0x05,0x48,0xC0,0x46,0x1C,0x21,0x00,0x22,0x02,0x4D,0xA8,0x47,
            0x00,0x20,0x03,0x21,0xFC,0xBD,0x09,0x75,0x00,0x02,0x00,0x80,0x3C,0x02,
        };

        private static readonly Dictionary<string, Expansion> Expansions = new()
        {
            ["IPKE"] = new Expansion(new byte[] { 0x0F, 0xF1, 0x30, 0xFB }, 0x110334, UsLoader),
            ["IPGE"] = new Expansion(new byte[] { 0x0F, 0xF1, 0x30, 0xFB }, 0x110334, UsLoader),
            ["IPKI"] = new Expansion(new byte[] { 0x10, 0xF1, 0xCE, 0xF9 }, 0x111070, new byte[]
            {
                0xFC,0xB5,0x04,0x48,0x1C,0x21,0x00,0x22,0xF6,0xF6,0x46,0xFA,
                0x00,0x20,0x03,0x21,0xFC,0xBD,0x00,0x00,0x00,0x80,0x3C,0x02,0x00,
            }),
        };
        private const int ExpansionCallAt = 0xCD0;

        public static bool DetectCurrentProject()
        {
            if (RomInfo.romID == null || !Expansions.ContainsKey(RomInfo.romID) || !RomInfo.IsDsRomProject) return false;
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
            if (romId == null || !dsRomProject || !Expansions.TryGetValue(romId, out Expansion expansion)) return false;
            if (!Matches(arm9, ExpansionCallAt, expansion.Call) || !Matches(arm9, expansion.LoaderAt, expansion.Loader)) return false;
            if (!Matches(arm9, 0x73AA2, 0xB0, 0x23)) return false;
            if (!TryReadBl(arm9, HookOffset, Arm9Base + HookOffset, out uint target)) return false;

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

        // ARM9 before the patch: the call that frees the raw party records, and the ability mask (mov r3, #0xF0).
        private const int MaskOffset = 0x73AA2;
        private static readonly byte[] OriginalHook = { 0xA7, 0xF7, 0x42, 0xF8 };
        private static readonly byte[] OriginalMask = { 0xF0, 0x23 };
        private static readonly byte[] PatchedMask = { 0xB0, 0x23 };

        /// <summary>Where the toolbox offers to place the routine in the synthetic overlay.</summary>
        public const uint DefaultPayloadOffset = 0x12100;

        public static bool SupportsCurrentRom => RomInfo.romID != null && Expansions.ContainsKey(RomInfo.romID);

        /// <summary>The routine for a given runtime address.</summary>
        public static byte[] BuildPayload(uint address)
        {
            byte[] payload = (byte[])PayloadTemplate.Clone();
            for (int i = 0; i < CallOffsets.Length; i++)
                PatchToolboxLogic.BuildThumbBl(address + (uint)CallOffsets[i], CallTargets[i]).CopyTo(payload, CallOffsets[i]);
            BitConverter.GetBytes(address + 0x94).CopyTo(payload, 0x98);
            return payload;
        }

        /// <summary>Why the ARM9 can't take the patch, or null when its two hook sites are as the game ships them.</summary>
        public static string WhyNotApplicable(byte[] arm9)
        {
            if (!Matches(arm9, HookOffset, OriginalHook) || !Matches(arm9, MaskOffset, OriginalMask))
                return "The ARM9 code this patch hooks has already been changed.";
            return null;
        }

        /// <summary>Writes the routine at <paramref name="payloadOffset"/> in the synthetic overlay and hooks the ARM9 to it.</summary>
        public static void Apply(uint payloadOffset)
        {
            uint address = SyntheticBase + payloadOffset;
            DSUtils.WriteToFile(Filesystem.expArmPath, BuildPayload(address), payloadOffset);
            ARM9.WriteBytes(PatchToolboxLogic.BuildThumbBl(Arm9Base + HookOffset, address), HookOffset);
            ARM9.WriteBytes(PatchedMask, MaskOffset);
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
