using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    public enum TrainerScriptExecutableFile
    {
        Arm9,
        Overlay
    }

    public sealed class TrainerScriptPatchTarget
    {
        public TrainerScriptPatchTarget(TrainerScriptExecutableFile file, int expectedThumbReferences,
            int overlayNumber = -1)
        {
            File = file;
            ExpectedThumbReferences = expectedThumbReferences;
            OverlayNumber = overlayNumber;
        }

        public TrainerScriptExecutableFile File { get; }
        public int OverlayNumber { get; }
        public int ExpectedThumbReferences { get; }
    }

    /// <summary>
    /// Revision-gated executable consumers of the special eye-meets script number. A target is
    /// accepted only when one aligned literal has the expected number of Thumb PC-relative loads.
    /// </summary>
    public sealed class TrainerScriptExecutableDescriptor
    {
        private TrainerScriptExecutableDescriptor(IReadOnlyList<TrainerScriptPatchTarget> targets)
        {
            Targets = targets;
        }

        public IReadOnlyList<TrainerScriptPatchTarget> Targets { get; }

        public static bool TryFor(RomInfo.GameVersions version, RomInfo.GameLanguages language,
            out TrainerScriptExecutableDescriptor descriptor, out string error)
        {
            descriptor = null;
            error = null;
            if (version == RomInfo.GameVersions.Platinum && language == RomInfo.GameLanguages.English)
            {
                descriptor = new TrainerScriptExecutableDescriptor(new[]
                {
                    new TrainerScriptPatchTarget(TrainerScriptExecutableFile.Arm9,
                        expectedThumbReferences: 2),
                    new TrainerScriptPatchTarget(TrainerScriptExecutableFile.Overlay,
                        expectedThumbReferences: 1, overlayNumber: 8)
                });
                return true;
            }

            if (version == RomInfo.GameVersions.HeartGold && language == RomInfo.GameLanguages.English)
            {
                descriptor = new TrainerScriptExecutableDescriptor(new[]
                {
                    new TrainerScriptPatchTarget(TrainerScriptExecutableFile.Arm9,
                        expectedThumbReferences: 2)
                });
                return true;
            }

            error = $"Trainer roster expansion is not verified for {version} {language}.";
            return false;
        }

        public static bool TryPatchTarget(ReadOnlySpan<byte> source, ushort oldScriptNumber,
            ushort newScriptNumber, int expectedThumbReferences, out byte[] patched,
            out TrainerScriptPatchSite site, out string error)
        {
            patched = null;
            site = null;
            if (!TryLocateTarget(source, oldScriptNumber, expectedThumbReferences, out site, out error))
            {
                return false;
            }

            patched = source.ToArray();
            BinaryPrimitives.WriteUInt32LittleEndian(
                patched.AsSpan(site.LiteralOffset, sizeof(uint)), newScriptNumber);
            return true;
        }

        public static bool TryLocateTarget(ReadOnlySpan<byte> source, ushort scriptNumber,
            int expectedThumbReferences, out TrainerScriptPatchSite site, out string error)
        {
            site = null;
            error = null;
            if (expectedThumbReferences <= 0)
            {
                error = "The executable patch descriptor has no expected references.";
                return false;
            }

            var literalOffsets = new List<int>();
            for (int offset = 0; offset + sizeof(uint) <= source.Length; offset += sizeof(uint))
            {
                if (BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, sizeof(uint))) ==
                    scriptNumber)
                {
                    literalOffsets.Add(offset);
                }
            }

            if (literalOffsets.Count != 1)
            {
                error = $"Expected one aligned literal for trainer script {scriptNumber}, found {literalOffsets.Count}.";
                return false;
            }

            int literalOffset = literalOffsets[0];
            var references = new List<int>();
            for (int offset = 0; offset + sizeof(ushort) <= source.Length; offset += sizeof(ushort))
            {
                ushort instruction = BinaryPrimitives.ReadUInt16LittleEndian(
                    source.Slice(offset, sizeof(ushort)));
                if ((instruction & 0xF800) != 0x4800)
                {
                    continue;
                }

                int alignedPc = (offset + 4) & ~3;
                int target = alignedPc + ((instruction & 0xFF) * 4);
                if (target == literalOffset)
                {
                    references.Add(offset);
                }
            }

            if (references.Count != expectedThumbReferences)
            {
                error = $"Expected {expectedThumbReferences} Thumb references to trainer script " +
                    $"{scriptNumber}, found {references.Count}.";
                return false;
            }

            site = new TrainerScriptPatchSite(literalOffset, references);
            return true;
        }
    }

    public sealed class TrainerScriptPatchSite
    {
        internal TrainerScriptPatchSite(int literalOffset, IReadOnlyList<int> referenceOffsets)
        {
            LiteralOffset = literalOffset;
            ReferenceOffsets = referenceOffsets;
        }

        public int LiteralOffset { get; }
        public IReadOnlyList<int> ReferenceOffsets { get; }
    }
}
