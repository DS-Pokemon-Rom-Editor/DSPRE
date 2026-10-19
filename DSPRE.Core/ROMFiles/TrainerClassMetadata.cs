using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NarcAPI;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    public enum TrainerClassMetadataDetectionState
    {
        Stock,
        SchemaV1,
        Inconsistent
    }

    public sealed class TrainerClassMetadataCommonFields
    {
        public ushort Gender { get; set; }
        public ushort PrizeCoefficient { get; set; }
        public ushort MainEyeContactMusic { get; set; }
        public ushort AlternateEyeContactMusic { get; set; }
        public ushort BattleMusic { get; set; }
    }

    public enum TrainerClassMetadataAssetField
    {
        Group1Rlcn = 0x16,
        Group1Rgcn = 0x18,
        Group1Recn = 0x1A,
        Group1Rnan = 0x1C,
        Group1Rcsn1 = 0x1E,
        Group1Rcsn2 = 0x20,
        Group1Rcsn3 = 0x22,
        Group2Rlcn = 0x24,
        Group2Rgcn = 0x26,
        Group2Recn = 0x28,
        Group2Rnan = 0x2A,
        Group3Rlcn = 0x2C,
        Group3Rgcn = 0x2E,
        Group3Recn = 0x30,
        Group3Rnan = 0x32
    }

    public sealed class TrainerClassMetadataRecord
    {
        private readonly byte[] _data;

        private TrainerClassMetadataRecord(byte[] source)
        {
            _data = (byte[])source.Clone();
        }

        public ushort Gender { get => ReadUInt16(0x00); set => WriteUInt16(0x00, value); }
        public ushort PrizeCoefficient { get => ReadUInt16(0x02); set => WriteUInt16(0x02, value); }
        public ushort MainEyeContactMusic { get => ReadUInt16(0x04); set => WriteUInt16(0x04, value); }
        public ushort AlternateEyeContactMusic { get => ReadUInt16(0x06); set => WriteUInt16(0x06, value); }
        public ushort BattleMusic { get => ReadUInt16(0x08); set => WriteUInt16(0x08, value); }
        public ushort VsStyle { get => ReadUInt16(0x0A); set => WriteUInt16(0x0A, value); }
        public ushort TrainerNameId { get => ReadUInt16(0x0C); set => WriteUInt16(0x0C, value); }
        public byte UseSavedRivalName { get => _data[0x0E]; set => _data[0x0E] = value; }
        public byte Reserved { get => _data[0x0F]; set => _data[0x0F] = value; }
        public uint Style1Motion { get => ReadUInt32(0x10); set => WriteUInt32(0x10, value); }
        public ushort Style2Timing { get => ReadUInt16(0x14); set => WriteUInt16(0x14, value); }

        public static bool TryParse(byte[] source, out TrainerClassMetadataRecord record, out string error)
        {
            record = null;
            error = null;
            if (source == null || source.Length != TrainerClassMetadataStore.RecordLength)
            {
                error = "Trainer-class metadata records must be exactly " +
                    TrainerClassMetadataStore.RecordLength + " bytes.";
                return false;
            }

            record = new TrainerClassMetadataRecord(source);
            return true;
        }

        public ushort GetAsset(TrainerClassMetadataAssetField field)
        {
            EnsureKnownAssetField(field);
            return ReadUInt16((int)field);
        }

        public void SetAsset(TrainerClassMetadataAssetField field, ushort value)
        {
            EnsureKnownAssetField(field);
            WriteUInt16((int)field, value);
        }

        public byte[] ToByteArray()
        {
            return (byte[])_data.Clone();
        }

        private ushort ReadUInt16(int offset) => BitConverter.ToUInt16(_data, offset);
        private uint ReadUInt32(int offset) => BitConverter.ToUInt32(_data, offset);

        private void WriteUInt16(int offset, ushort value)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, _data, offset, 2);
        }

        private void WriteUInt32(int offset, uint value)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, _data, offset, 4);
        }

        private static void EnsureKnownAssetField(TrainerClassMetadataAssetField field)
        {
            if (!TrainerClassMetadataSchema.AssetFields.Contains(field))
                throw new ArgumentOutOfRangeException(nameof(field));
        }
    }

    public sealed class TrainerClassMetadataValidationResult
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public bool IsValid => Errors.Count == 0;
    }

    public static class TrainerClassMetadataSchema
    {
        public const ushort MaximumStyle = 13;

        public static readonly string[] StyleNames =
        {
            "0 - Dynamic terrain/time",
            "1 - Gym Leader / Rival",
            "2 - Elite Four / Champion",
            "3 - Rocket Admin",
            "4 - Kimono Girl",
            "5 - Red",
            "6 - Team Rocket",
            "7 - Static normal early",
            "8 - Static normal late",
            "9 - Static water early",
            "10 - Static water late",
            "11 - Static cave early",
            "12 - Static cave late",
            "13 - Frontier Brain"
        };

        public static readonly TrainerClassMetadataAssetField[] AssetFields =
        {
            TrainerClassMetadataAssetField.Group1Rlcn,
            TrainerClassMetadataAssetField.Group1Rgcn,
            TrainerClassMetadataAssetField.Group1Recn,
            TrainerClassMetadataAssetField.Group1Rnan,
            TrainerClassMetadataAssetField.Group1Rcsn1,
            TrainerClassMetadataAssetField.Group1Rcsn2,
            TrainerClassMetadataAssetField.Group1Rcsn3,
            TrainerClassMetadataAssetField.Group2Rlcn,
            TrainerClassMetadataAssetField.Group2Rgcn,
            TrainerClassMetadataAssetField.Group2Recn,
            TrainerClassMetadataAssetField.Group2Rnan,
            TrainerClassMetadataAssetField.Group3Rlcn,
            TrainerClassMetadataAssetField.Group3Rgcn,
            TrainerClassMetadataAssetField.Group3Recn,
            TrainerClassMetadataAssetField.Group3Rnan
        };

        public static bool UsesStaticName(TrainerClassMetadataRecord record)
        {
            if (record == null) return false;
            return record.VsStyle == 2 || record.VsStyle == 3 || record.VsStyle == 13 ||
                (record.VsStyle == 1 && record.UseSavedRivalName == 0);
        }

        public static bool IsAssetConsumed(ushort style, TrainerClassMetadataAssetField field)
        {
            if (style > MaximumStyle) return false;

            switch (field)
            {
                case TrainerClassMetadataAssetField.Group1Rlcn:
                case TrainerClassMetadataAssetField.Group1Rgcn:
                    return true;
                case TrainerClassMetadataAssetField.Group1Recn:
                case TrainerClassMetadataAssetField.Group1Rnan:
                    return style == 0 || style == 2 || (style >= 5 && style <= 12);
                case TrainerClassMetadataAssetField.Group1Rcsn1:
                    return style == 1 || style == 3 || style == 4 || style == 13;
                case TrainerClassMetadataAssetField.Group1Rcsn2:
                case TrainerClassMetadataAssetField.Group1Rcsn3:
                    return style == 3;
                case TrainerClassMetadataAssetField.Group2Rlcn:
                case TrainerClassMetadataAssetField.Group2Rgcn:
                case TrainerClassMetadataAssetField.Group2Recn:
                case TrainerClassMetadataAssetField.Group2Rnan:
                    return style <= 3 || style == 13;
                case TrainerClassMetadataAssetField.Group3Rlcn:
                case TrainerClassMetadataAssetField.Group3Rgcn:
                case TrainerClassMetadataAssetField.Group3Recn:
                case TrainerClassMetadataAssetField.Group3Rnan:
                    return style == 1 || style == 2 || style == 13;
                default:
                    return false;
            }
        }

        public static string GetExpectedMagic(TrainerClassMetadataAssetField field)
        {
            string name = field.ToString();
            if (name.EndsWith("Rlcn", StringComparison.Ordinal)) return "RLCN";
            if (name.EndsWith("Rgcn", StringComparison.Ordinal)) return "RGCN";
            if (name.EndsWith("Recn", StringComparison.Ordinal)) return "RECN";
            if (name.EndsWith("Rnan", StringComparison.Ordinal)) return "RNAN";
            return "RCSN";
        }

        /// <summary>The asset as the VS intro editor names it for this style, such as "Large Poké Ball palette".</summary>
        public static string GetAssetLabel(int style, TrainerClassMetadataAssetField field)
        {
            string name = field.ToString();
            string slot = name.StartsWith("Group2", StringComparison.Ordinal) ? (style == 0 ? "Large Poké Ball" : "Portrait")
                : name.StartsWith("Group3", StringComparison.Ordinal) ? "VS mark"
                : style == 4 ? "Background"
                : style == 1 || style == 13 ? "Banner"
                : style == 3 ? "Backdrop"
                : style == 2 ? "Frame"
                : style is 5 or 6 ? "Emblem"
                : "Small Poké Ball";
            string part = field switch
            {
                TrainerClassMetadataAssetField.Group1Rlcn or TrainerClassMetadataAssetField.Group2Rlcn or TrainerClassMetadataAssetField.Group3Rlcn => "palette",
                TrainerClassMetadataAssetField.Group1Rgcn or TrainerClassMetadataAssetField.Group2Rgcn or TrainerClassMetadataAssetField.Group3Rgcn => "tiles",
                TrainerClassMetadataAssetField.Group1Recn or TrainerClassMetadataAssetField.Group2Recn or TrainerClassMetadataAssetField.Group3Recn => "cells",
                TrainerClassMetadataAssetField.Group1Rcsn2 => "screen 2",
                TrainerClassMetadataAssetField.Group1Rcsn3 => "screen 3",
                TrainerClassMetadataAssetField.Group1Rcsn1 => "screen",
                _ => "animation",
            };
            return slot + " " + part;
        }
    }

    public static class TrainerClassMetadataStore
    {
        public const int RecordLength = 0x34;
        public const int MinimumRecordCount = 1;

        private static string _detectedWorkDir;
        private static TrainerClassMetadataDetectionState _detectionState;
        private static string _detectionDetail;
        private static int _recordCount;

        public static int RecordCount => _recordCount;

        internal static void SetManagedRecordCount(int count)
        {
            _recordCount = count;
        }

        public static void Reset()
        {
            _detectedWorkDir = null;
            _detectionState = TrainerClassMetadataDetectionState.Stock;
            _detectionDetail = null;
            _recordCount = 0;
        }

        public static TrainerClassMetadataDetectionState DetectCurrentRom(out string detail)
        {
            string currentWorkDir = RomInfo.WorkDir ?? string.Empty;
            if (_detectedWorkDir == currentWorkDir && _detectionDetail != null)
            {
                detail = _detectionDetail;
                return _detectionState;
            }

            _detectedWorkDir = currentWorkDir;
            _recordCount = 0;

            if (RomInfo.ClassMetadataHooks == null)
            {
                _detectionState = TrainerClassMetadataDetectionState.Stock;
                _detectionDetail = "TCM schema-v1 detection is limited to English HeartGold and SoulSilver.";
                detail = _detectionDetail;
                return _detectionState;
            }

            bool archiveMarker = TryInspectArchive(out _recordCount, out string archiveDetail);
            bool genderHookMarker = HasThumbVeneer(RomInfo.ClassMetadataHooks.Gender, 0x00, 0x49, 0x08, 0x47);
            bool eyeContactHookMarker = HasThumbVeneer(RomInfo.ClassMetadataHooks.EyeContact, 0x00, 0x4A, 0x10, 0x47);

            bool retiredTrainerTableMarker = TryReadByte(RomInfo.VsTrainerEntryTableOffsetToSizeLimiter, out byte trainerCount) &&
                TryReadUInt32(RomInfo.VsTrainerEntryTableOffsetToRAMAddress, out uint trainerPointer) &&
                trainerCount == 0 && trainerPointer == 0;
            bool speciesTableMarker = TryReadByte(RomInfo.VsPokemonEntryTableOffsetToSizeLimiter, out byte speciesCount) &&
                speciesCount >= RomInfo.ClassMetadataHooks.VanillaVsPokemonCount;

            bool[] markers =
            {
                archiveMarker,
                genderHookMarker,
                eyeContactHookMarker,
                retiredTrainerTableMarker,
                speciesTableMarker
            };

            bool anyMarker = false;
            bool allMarkers = true;
            foreach (bool marker in markers)
            {
                anyMarker |= marker;
                allMarkers &= marker;
            }

            if (allMarkers)
            {
                _detectionState = TrainerClassMetadataDetectionState.SchemaV1;
                _detectionDetail = "TCM schema v1-compatible data detected.";
            }
            else if (!anyMarker)
            {
                _detectionState = TrainerClassMetadataDetectionState.Stock;
                _detectionDetail = "No TCM schema-v1 markers detected.";
            }
            else
            {
                _detectionState = TrainerClassMetadataDetectionState.Inconsistent;
                List<string> failedMarkers = new List<string>();
                if (!archiveMarker) failedMarkers.Add("a155 structure (" + archiveDetail + ")");
                if (!genderHookMarker) failedMarkers.Add("gender hook");
                if (!eyeContactHookMarker) failedMarkers.Add("eye-contact hook");
                if (!retiredTrainerTableMarker) failedMarkers.Add("retired trainer-to-combo table");
                if (!speciesTableMarker) failedMarkers.Add("species-to-combo table with at least 43 rows");
                _detectionDetail = "Partial TCM installation: missing or invalid " + string.Join(", ", failedMarkers) + ".";
            }

            detail = _detectionDetail;
            return _detectionState;
        }

        public static bool EnsureUnpacked(out string error)
        {
            error = null;
            TrainerClassMetadataDetectionState state = DetectCurrentRom(out string detail);
            if (state != TrainerClassMetadataDetectionState.SchemaV1)
            {
                error = detail;
                return false;
            }

            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.TrainerClassMetadata });
                string unpackedDir = RomInfo.GameDirs[DirNames.TrainerClassMetadata].unpackedDir;
                if (!Directory.Exists(unpackedDir))
                {
                    error = "The trainer-class metadata archive could not be unpacked.";
                    return false;
                }

                if (!TryInspectUnpackedRecords(unpackedDir, out int unpackedRecordCount, out error))
                {
                    return false;
                }
                _recordCount = unpackedRecordCount;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryReadCommonFields(int classId, out TrainerClassMetadataCommonFields fields, out string error)
        {
            fields = null;
            if (!TryReadRecord(classId, out TrainerClassMetadataRecord record, out error))
            {
                return false;
            }

            fields = new TrainerClassMetadataCommonFields
            {
                Gender = record.Gender,
                PrizeCoefficient = record.PrizeCoefficient,
                MainEyeContactMusic = record.MainEyeContactMusic,
                AlternateEyeContactMusic = record.AlternateEyeContactMusic,
                BattleMusic = record.BattleMusic
            };
            return true;
        }

        public static bool TryWriteCommonFields(int classId, TrainerClassMetadataCommonFields fields, out string error)
        {
            error = null;
            if (fields == null)
            {
                error = "No trainer-class metadata values were supplied.";
                return false;
            }
            if (!TryReadRecord(classId, out TrainerClassMetadataRecord record, out error))
            {
                return false;
            }

            record.Gender = fields.Gender;
            record.PrizeCoefficient = fields.PrizeCoefficient;
            record.MainEyeContactMusic = fields.MainEyeContactMusic;
            record.AlternateEyeContactMusic = fields.AlternateEyeContactMusic;
            record.BattleMusic = fields.BattleMusic;

            return TryWriteRecordBytes(classId, record.ToByteArray(), out error);
        }

        public static bool TryReadRecord(int classId, out TrainerClassMetadataRecord record, out string error)
        {
            record = null;
            if (!TryReadRecordBytes(classId, out byte[] bytes, out error))
            {
                return false;
            }
            return TrainerClassMetadataRecord.TryParse(bytes, out record, out error);
        }

        public static bool TryWritePresentationFields(int classId, TrainerClassMetadataRecord editedRecord, out string error)
        {
            error = null;
            if (editedRecord == null)
            {
                error = "No trainer-class metadata record was supplied.";
                return false;
            }
            if (!TryReadRecordBytes(classId, out byte[] current, out error))
            {
                return false;
            }

            byte[] edited = editedRecord.ToByteArray();
            Buffer.BlockCopy(edited, 0x0A, current, 0x0A, RecordLength - 0x0A);
            return TryWriteRecordBytes(classId, current, out error);
        }

        public static TrainerClassMetadataValidationResult ValidatePresentation(
            TrainerClassMetadataRecord record, int staticNameCount)
        {
            TrainerClassMetadataValidationResult result = new TrainerClassMetadataValidationResult();
            if (record == null)
            {
                result.Errors.Add("No trainer-class metadata record was supplied.");
                return result;
            }

            if (record.VsStyle > TrainerClassMetadataSchema.MaximumStyle)
                result.Errors.Add("VS style must be in the range 0..13.");
            if (record.VsStyle == 1 && record.UseSavedRivalName > 1)
                result.Errors.Add("Style 1 saved-rival selection must be 0 or 1.");
            if (record.Reserved != 0)
                result.Errors.Add("Reserved byte 0x0F must be zero.");
            if (TrainerClassMetadataSchema.UsesStaticName(record) && record.TrainerNameId >= staticNameCount)
                result.Errors.Add("Trainer-name message ID " + record.TrainerNameId + " does not exist.");
            if (record.VsStyle == 2 && record.Style2Timing > byte.MaxValue)
                result.Errors.Add("Style 2 timing must be in the range 0..255.");

            if (record.VsStyle <= TrainerClassMetadataSchema.MaximumStyle)
            {
                ValidateConsumedAssets(record, result);
            }
            return result;
        }

        private static string UnpackedFolderName =>
            RomInfo.GameDirs.TryGetValue(DirNames.EncounterEffectGraphics, out (string packedDir, string unpackedDir) dirs)
                ? Path.GetFileName(dirs.unpackedDir) : "encounterEffectGraphics";

        private static void ValidateConsumedAssets(TrainerClassMetadataRecord record,
            TrainerClassMetadataValidationResult result)
        {
            if (!TryGetAssetArchivePath(out string archivePath) || !File.Exists(archivePath))
            {
                result.Errors.Add("The current ROM's /a/1/0/9 asset archive is missing.");
                return;
            }

            Narc archive = null;
            try
            {
                // The unpacked copy is what editors change and what Save ROM packs, so it is the one that counts.
                if (RomInfo.GameDirs.TryGetValue(DirNames.EncounterEffectGraphics, out (string packedDir, string unpackedDir) art)
                    && Directory.Exists(art.unpackedDir) && Directory.EnumerateFiles(art.unpackedDir).Any())
                    archive = Narc.FromFolder(art.unpackedDir);
                else
                    archive = Narc.Open(archivePath);
                if (archive == null)
                {
                    result.Errors.Add("The current ROM's /a/1/0/9 file is not a valid NARC archive.");
                    return;
                }

                int memberCount = archive.GetElementsLength();
                foreach (TrainerClassMetadataAssetField field in TrainerClassMetadataSchema.AssetFields)
                {
                    if (!TrainerClassMetadataSchema.IsAssetConsumed(record.VsStyle, field)) continue;

                    ushort memberId = record.GetAsset(field);
                    if (memberId >= memberCount)
                    {
                        // Say what would fix it: a number the archive has, or the file added where Save ROM packs from.
                        result.Errors.Add(TrainerClassMetadataSchema.GetAssetLabel(record.VsStyle, field) +
                            " " + memberId + " doesn't exist. The archive has files 0 to " + (memberCount - 1) +
                            ": pick one of those, or add the file to unpacked/" + UnpackedFolderName + " under the number it should have.");
                        continue;
                    }

                    Stream member = archive[memberId];
                    if (member.Length < 4)
                    {
                        result.Warnings.Add(TrainerClassMetadataSchema.GetAssetLabel(record.VsStyle, field) +
                            " " + memberId + " is too short to be a picture file.");
                        continue;
                    }

                    long originalPosition = member.Position;
                    member.Position = 0;
                    byte[] magicBytes = new byte[4];
                    member.Read(magicBytes, 0, magicBytes.Length);
                    member.Position = originalPosition;
                    string actualMagic = Encoding.ASCII.GetString(magicBytes);
                    string expectedMagic = TrainerClassMetadataSchema.GetExpectedMagic(field);
                    if (!string.Equals(actualMagic, expectedMagic, StringComparison.Ordinal))
                    {
                        result.Warnings.Add(TrainerClassMetadataSchema.GetAssetLabel(record.VsStyle, field) + " " +
                            memberId + " is the wrong kind of file.");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add("Could not inspect /a/1/0/9: " + ex.Message);
            }
            finally
            {
                archive?.Free();
            }
        }

        private static bool TryGetAssetArchivePath(out string path)
        {
            path = null;
            if (!RomInfo.GameDirs.TryGetValue(DirNames.TrainerClassMetadata,
                out (string packedDir, string unpackedDir) metadataPaths)) return false;

            string a1Directory = Path.GetDirectoryName(Path.GetDirectoryName(metadataPaths.packedDir));
            if (string.IsNullOrEmpty(a1Directory)) return false;
            path = Path.Combine(a1Directory, "0", "9");
            return true;
        }

        private static bool TryReadRecordBytes(int classId, out byte[] record, out string error)
        {
            record = null;
            error = null;
            if (classId < 0)
            {
                error = "Trainer-class index cannot be negative.";
                return false;
            }
            if (!EnsureUnpacked(out error))
            {
                return false;
            }
            if (classId >= _recordCount)
            {
                error = "Trainer-class metadata record " + classId + " does not exist.";
                return false;
            }

            string path = GetRecordPath(classId);
            if (!File.Exists(path))
            {
                error = "Trainer-class metadata record is missing: " + path;
                return false;
            }

            try
            {
                record = File.ReadAllBytes(path);
                if (record.Length != RecordLength)
                {
                    error = "Trainer-class metadata record " + classId + " is " + record.Length +
                        " bytes; expected " + RecordLength + ".";
                    record = null;
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryWriteRecordBytes(int classId, byte[] record, out string error)
        {
            error = null;
            try
            {
                ArchiveMembers.Put(DirNames.TrainerClassMetadata, classId, record);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string GetRecordPath(int classId)
        {
            return Path.Combine(RomInfo.GameDirs[DirNames.TrainerClassMetadata].unpackedDir, classId.ToString("D4"));
        }

        private static bool TryInspectArchive(out int memberCount, out string detail)
        {
            memberCount = 0;
            detail = "archive is missing";
            if (!RomInfo.GameDirs.TryGetValue(DirNames.TrainerClassMetadata, out (string packedDir, string unpackedDir) paths) ||
                !File.Exists(paths.packedDir))
            {
                return false;
            }

            Narc archive = null;
            try
            {
                using (BinaryReader reader = new BinaryReader(File.OpenRead(paths.packedDir)))
                {
                    if (reader.BaseStream.Length < 0x10)
                    {
                        detail = "truncated NARC header";
                        return false;
                    }
                    reader.BaseStream.Position = 0x08;
                    if (reader.ReadUInt32() != reader.BaseStream.Length)
                    {
                        detail = "NARC size does not match its header";
                        return false;
                    }
                }

                archive = Narc.Open(paths.packedDir);
                if (archive == null)
                {
                    detail = "invalid NARC archive";
                    return false;
                }

                memberCount = archive.GetElementsLength();
                if (memberCount < MinimumRecordCount)
                {
                    detail = "unexpected member count";
                    return false;
                }
                for (int i = 0; i < memberCount; i++)
                {
                    if (archive[i].Length != RecordLength)
                    {
                        detail = "member " + i + " is not a 0x34-byte record";
                        return false;
                    }
                }

                detail = memberCount + " valid records";
                return true;
            }
            catch (Exception ex)
            {
                detail = ex.Message;
                return false;
            }
            finally
            {
                archive?.Free();
            }
        }

        private static bool TryInspectUnpackedRecords(string directory, out int memberCount, out string error)
        {
            memberCount = 0;
            error = null;
            string[] files = Directory.GetFiles(directory);
            memberCount = files.Length;
            if (memberCount < MinimumRecordCount)
            {
                error = "The trainer-class metadata archive contains no records.";
                return false;
            }

            HashSet<string> actual = new HashSet<string>(files.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < memberCount; i++)
            {
                string expected = Path.GetFullPath(Path.Combine(directory, i.ToString("D4")));
                if (!actual.Contains(expected))
                {
                    error = "Trainer-class metadata member sequence is not contiguous at " + expected + ".";
                    return false;
                }
                long length = new FileInfo(expected).Length;
                if (length != RecordLength)
                {
                    error = "Trainer-class metadata member " + i + " is " + length +
                        " bytes; expected " + RecordLength + ".";
                    return false;
                }
            }
            return true;
        }

        private static bool HasThumbVeneer(uint offset, byte byte0, byte byte1, byte byte2, byte byte3)
        {
            try
            {
                byte[] bytes = ARM9.ReadBytes(offset, 8);
                return bytes.Length == 8 &&
                    bytes[0] == byte0 && bytes[1] == byte1 &&
                    bytes[2] == byte2 && bytes[3] == byte3 &&
                    (BitConverter.ToUInt32(bytes, 4) & 1) == 1;
            }
            catch (Exception ex)
            {
                AppLogger.WarnOnce("TrainerClassMetadataStore.HasThumbVeneer", ex);
                return false;
            }
        }

        private static bool TryReadByte(uint offset, out byte value)
        {
            value = 0;
            try
            {
                value = ARM9.ReadByte(offset);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.WarnOnce("TrainerClassMetadataStore.TryReadByte", ex);
                return false;
            }
        }

        private static bool TryReadUInt32(uint offset, out uint value)
        {
            value = 0;
            try
            {
                byte[] bytes = ARM9.ReadBytes(offset, 4);
                if (bytes.Length != 4) return false;
                value = BitConverter.ToUInt32(bytes, 0);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.WarnOnce("TrainerClassMetadataStore.TryReadUInt32", ex);
                return false;
            }
        }
    }
}
