using System;
using System.IO;

namespace DSPRE
{
    /// <summary>
    /// A deferred, guarded write that makes one overworld appearance use another local appearance's
    /// animation/profile metadata. The source is always read from the currently open ROM, so
    /// Platinum code pointers are never imported from another ROM or language.
    /// </summary>
    public sealed class OverworldSpriteProfileMetadataPatch
    {
        private readonly string _path;
        private readonly long _offset;
        private readonly byte[] _expected;
        private readonly byte[] _replacement;

        internal OverworldSpriteProfileMetadataPatch(string path, long offset, byte[] expected, byte[] replacement)
        {
            _path = path;
            _offset = offset;
            _expected = expected;
            _replacement = replacement;
        }

        public bool TryApply(out string error) => TryReplace(_expected, _replacement, out error);

        public bool TryRollback(out string error) => TryReplace(_replacement, _expected, out error);

        private bool TryReplace(byte[] expected, byte[] replacement, out string error)
        {
            error = null;
            try
            {
                byte[] current = File.ReadAllBytes(_path);
                if (_offset < 0 || _offset > current.Length || replacement.Length > current.Length - _offset)
                {
                    error = "The overworld metadata table is no longer large enough for this change.";
                    return false;
                }

                for (int i = 0; i < expected.Length; i++)
                {
                    if (current[_offset + i] != expected[i])
                    {
                        error = "The overworld metadata changed after the profile was selected. Re-import the image to refresh the staged change.";
                        return false;
                    }
                }

                Array.Copy(replacement, 0, current, _offset, replacement.Length);
                File.WriteAllBytes(_path, current);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    public static class OverworldSpriteProfileMetadata
    {
        private const int MaximumRows = 4096;

        public static bool TryCreatePatch(
            uint targetAppearanceId,
            uint sourceAppearanceId,
            out OverworldSpriteProfileMetadataPatch patch,
            out string error)
        {
            patch = null;
            error = null;
            if (targetAppearanceId == sourceAppearanceId)
            {
                error = "Choose a different overworld appearance as the profile source.";
                return false;
            }

            try
            {
                if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS)
                    return TryCreateHgssPatch(RomInfo.OWtablePath, RomInfo.OWTableOffset, targetAppearanceId, sourceAppearanceId, out patch, out error);

                if (RomInfo.gameFamily == RomInfo.GameFamilies.Plat || RomInfo.gameFamily == RomInfo.GameFamilies.DP)
                {
                    if (OverworldSpriteTableExpansion.IsApplied)
                        return TryCreateExpandedDppPatch(targetAppearanceId, sourceAppearanceId, out patch, out error);
                    return TryCreateDppPatch(RomInfo.OWtablePath, RomInfo.OWTableOffset, targetAppearanceId, sourceAppearanceId, out patch, out error);
                }

                error = "Overworld graphics profiles are not supported for this game.";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryCreateExpandedDppPatch(
            uint targetAppearanceId,
            uint sourceAppearanceId,
            out OverworldSpriteProfileMetadataPatch patch,
            out string error)
        {
            patch = null;
            error = null;
            if (!OverworldSpriteTableExpansion.TryGetRowLocation(3, targetAppearanceId, out string targetPath, out long targetRow, out int targetSize) ||
                !OverworldSpriteTableExpansion.TryGetRowLocation(3, sourceAppearanceId, out string sourcePath, out long sourceRow, out int sourceSize) ||
                targetPath != sourcePath || targetSize != 16 || sourceSize != 16)
            {
                error = "The selected profile is missing from the expanded animation table.";
                return false;
            }

            byte[] data = File.ReadAllBytes(targetPath);
            return CreatePatch(data, targetPath, targetRow + 4, sourceRow + 4, 12, out patch, out error);
        }

        internal static bool TryCreateDppPatch(
            string path,
            long textureTableOffset,
            uint targetAppearanceId,
            uint sourceAppearanceId,
            out OverworldSpriteProfileMetadataPatch patch,
            out string error)
        {
            patch = null;
            error = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                error = "The overworld table file was not found.";
                return false;
            }

            byte[] data = File.ReadAllBytes(path);
            long sentinel = FindU32Row(data, textureTableOffset, 8, 0xFFFF);
            if (sentinel < 0)
            {
                error = "The end of the overworld texture table could not be found.";
                return false;
            }

            long animationTable = sentinel + 8;
            long target = FindU32Row(data, animationTable, 16, targetAppearanceId);
            long source = FindU32Row(data, animationTable, 16, sourceAppearanceId);
            if (target < 0 || source < 0)
            {
                error = target < 0
                    ? $"Target appearance 0x{targetAppearanceId:X} is missing from the animation table."
                    : $"Profile source 0x{sourceAppearanceId:X} is missing from the animation table.";
                return false;
            }

            return CreatePatch(data, path, target + 4, source + 4, 12, out patch, out error);
        }

        internal static bool TryCreateHgssPatch(
            string path,
            long tableOffset,
            uint targetAppearanceId,
            uint sourceAppearanceId,
            out OverworldSpriteProfileMetadataPatch patch,
            out string error)
        {
            patch = null;
            error = null;
            if (targetAppearanceId > ushort.MaxValue || sourceAppearanceId > ushort.MaxValue)
            {
                error = "HGSS overworld appearance IDs must fit in 16 bits.";
                return false;
            }
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                error = "The overworld table file was not found.";
                return false;
            }

            byte[] data = File.ReadAllBytes(path);
            long target = FindU16Row(data, tableOffset, 6, (ushort)targetAppearanceId);
            long source = FindU16Row(data, tableOffset, 6, (ushort)sourceAppearanceId);
            if (target < 0 || source < 0)
            {
                error = target < 0
                    ? $"Target appearance 0x{targetAppearanceId:X} is missing from the overworld table."
                    : $"Profile source 0x{sourceAppearanceId:X} is missing from the overworld table.";
                return false;
            }

            return CreatePatch(data, path, target + 4, source + 4, 2, out patch, out error);
        }

        private static bool CreatePatch(
            byte[] data,
            string path,
            long targetOffset,
            long sourceOffset,
            int length,
            out OverworldSpriteProfileMetadataPatch patch,
            out string error)
        {
            patch = null;
            error = null;
            if (!Fits(data, targetOffset, length) || !Fits(data, sourceOffset, length))
            {
                error = "The overworld metadata table is truncated.";
                return false;
            }

            var expected = new byte[length];
            var replacement = new byte[length];
            Array.Copy(data, targetOffset, expected, 0, length);
            Array.Copy(data, sourceOffset, replacement, 0, length);
            patch = new OverworldSpriteProfileMetadataPatch(path, targetOffset, expected, replacement);
            return true;
        }

        private static long FindU32Row(byte[] data, long start, int rowSize, uint key)
        {
            for (int row = 0; row < MaximumRows; row++)
            {
                long offset = start + (long)row * rowSize;
                if (!Fits(data, offset, 4)) return -1;
                uint current = BitConverter.ToUInt32(data, (int)offset);
                if (current == key) return offset;
                if (current == 0xFFFF) return -1;
            }
            return -1;
        }

        private static long FindU16Row(byte[] data, long start, int rowSize, ushort key)
        {
            for (int row = 0; row < MaximumRows; row++)
            {
                long offset = start + (long)row * rowSize;
                if (!Fits(data, offset, 2)) return -1;
                ushort current = BitConverter.ToUInt16(data, (int)offset);
                if (current == key) return offset;
                if (current == 0xFFFF) return -1;
            }
            return -1;
        }

        private static bool Fits(byte[] data, long offset, int length) =>
            offset >= 0 && offset <= data.Length && length >= 0 && length <= data.Length - offset;
    }
}
