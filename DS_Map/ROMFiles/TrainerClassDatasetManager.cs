using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    public sealed class TrainerClassDatasetState
    {
        public int MetadataCount { get; internal set; }
        public int NameCount { get; internal set; }
        public int DescriptionCount { get; internal set; }
        public int GraphicsMemberCount { get; internal set; }
        public int GraphicsClassCount => GraphicsMemberCount / TrainerClassDatasetManager.GraphicsMembersPerClass;
        public bool GraphicsCountIsDivisible => GraphicsMemberCount % TrainerClassDatasetManager.GraphicsMembersPerClass == 0;
        public IReadOnlyList<string> ClassNames { get; internal set; }

        public string CountSummary =>
            "a155 records: " + MetadataCount +
            "; class names: " + NameCount +
            "; class descriptions: " + DescriptionCount +
            "; trainerGraphics members: " + GraphicsMemberCount +
            (GraphicsCountIsDivisible ? " (" + GraphicsClassCount + " class sets)" : " (not divisible by 5)");
    }

    public static class TrainerClassDatasetManager
    {
        public const int GraphicsMembersPerClass = 5;

        private static int DescriptionArchiveId => RomInfo.trainerClassMessageNumber + 1;

        public static bool TryInspect(out TrainerClassDatasetState state, out string error)
        {
            state = null;
            error = null;

            if (TrainerClassMetadataStore.DetectCurrentRom(out string detail) != TrainerClassMetadataDetectionState.SchemaV1)
            {
                error = detail;
                return false;
            }
            if (!TrainerClassMetadataStore.EnsureUnpacked(out error)) return false;

            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames>
                {
                    DirNames.textArchives,
                    DirNames.trainerGraphics,
                    DirNames.trainerProperties
                });

                string graphicsDir = RomInfo.gameDirs[DirNames.trainerGraphics].unpackedDir;
                if (!TryCountContiguousMembers(graphicsDir, null, out int graphicsCount, out error)) return false;

                var names = new TextArchive(RomInfo.trainerClassMessageNumber);
                var descriptions = new TextArchive(DescriptionArchiveId);
                state = new TrainerClassDatasetState
                {
                    MetadataCount = TrainerClassMetadataStore.RecordCount,
                    NameCount = names.messages.Count,
                    DescriptionCount = descriptions.messages.Count,
                    GraphicsMemberCount = graphicsCount,
                    ClassNames = names.messages.ToArray()
                };

                bool countsAgree = state.MetadataCount >= TrainerClassMetadataStore.MinimumRecordCount &&
                    state.NameCount == state.MetadataCount &&
                    state.DescriptionCount == state.MetadataCount &&
                    state.GraphicsCountIsDivisible &&
                    state.GraphicsClassCount == state.MetadataCount;
                if (!countsAgree)
                {
                    error = "Trainer-class datasets do not have matching quantities. " + state.CountSummary +
                        ". Add and Remove are disabled; no automatic repair was attempted.";
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

        public static bool TryCopyClass(int sourceClassId, out int newClassId, out string error)
        {
            newClassId = -1;
            error = null;
            if (!TryInspect(out TrainerClassDatasetState state, out error)) return false;
            if (sourceClassId < 0 || sourceClassId >= state.MetadataCount)
            {
                error = "The source trainer class does not exist.";
                return false;
            }
            if (state.MetadataCount > byte.MaxValue)
            {
                error = "DSPRE's current Trainer Editor cannot add another assignable class after class ID 255.";
                return false;
            }

            newClassId = state.MetadataCount;
            string metadataSource = MetadataPath(sourceClassId);
            string metadataDestination = MetadataPath(newClassId);
            string[] graphicsSources = GraphicsPaths(sourceClassId);
            string[] graphicsDestinations = GraphicsPaths(newClassId);
            var addedFiles = new List<string> { metadataDestination };
            addedFiles.AddRange(graphicsDestinations);
            if (addedFiles.Any(path => File.Exists(path) || Directory.Exists(path)))
            {
                error = "A destination file for trainer class " + newClassId + " already exists.";
                newClassId = -1;
                return false;
            }

            FileSnapshot nameJson = null;
            FileSnapshot descriptionJson = null;
            bool mutationStarted = false;
            try
            {
                var names = new TextArchive(RomInfo.trainerClassMessageNumber);
                var descriptions = new TextArchive(DescriptionArchiveId);
                byte[] metadata = File.ReadAllBytes(metadataSource);
                byte[][] graphics = graphicsSources.Select(File.ReadAllBytes).ToArray();
                nameJson = FileSnapshot.Capture(TextArchive.GetFilePaths(RomInfo.trainerClassMessageNumber).jsonPath);
                descriptionJson = FileSnapshot.Capture(TextArchive.GetFilePaths(DescriptionArchiveId).jsonPath);
                names.messages.Add(names.messages[sourceClassId]);
                descriptions.messages.Add(descriptions.messages[sourceClassId]);

                mutationStarted = true;
                File.WriteAllBytes(metadataDestination, metadata);
                for (int i = 0; i < graphics.Length; i++)
                    File.WriteAllBytes(graphicsDestinations[i], graphics[i]);
                names.SaveToExpandedDir(RomInfo.trainerClassMessageNumber, showSuccessMessage: false);
                descriptions.SaveToExpandedDir(DescriptionArchiveId, showSuccessMessage: false);

                TrainerClassMetadataStore.SetManagedRecordCount(newClassId + 1);
                return true;
            }
            catch (Exception ex)
            {
                var rollbackErrors = new List<string>();
                if (mutationStarted)
                {
                    foreach (string path in addedFiles)
                        TryDelete(path, rollbackErrors);
                    nameJson?.Restore(rollbackErrors);
                    descriptionJson?.Restore(rollbackErrors);
                }
                newClassId = -1;
                error = "Trainer class was not added: " + ex.Message +
                    (mutationStarted ? RollbackSummary(rollbackErrors) : " No files were changed.");
                return false;
            }
        }

        public static bool TryFindTrainerUses(int classId, out List<int> trainerIds, out string error)
        {
            trainerIds = new List<int>();
            error = null;
            if (classId < 0)
            {
                error = "Trainer-class index cannot be negative.";
                return false;
            }
            if (classId > byte.MaxValue) return true;

            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerProperties });
                string directory = RomInfo.gameDirs[DirNames.trainerProperties].unpackedDir;
                if (!Directory.Exists(directory))
                {
                    error = "The trainer-properties archive could not be unpacked.";
                    return false;
                }

                foreach (string path in Directory.GetFiles(directory).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    byte[] data = File.ReadAllBytes(path);
                    if (data.Length < 2)
                    {
                        error = "Trainer-properties member " + Path.GetFileName(path) + " is too short to audit.";
                        trainerIds.Clear();
                        return false;
                    }
                    if (data[1] != classId) continue;

                    if (int.TryParse(Path.GetFileName(path), out int trainerId)) trainerIds.Add(trainerId);
                    else
                    {
                        error = "Trainer-properties member has an unexpected filename: " + Path.GetFileName(path) + ".";
                        trainerIds.Clear();
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                trainerIds.Clear();
                return false;
            }
        }

        public static bool TryRemoveLastClass(out int removedClassId, out string error)
        {
            removedClassId = -1;
            error = null;
            if (!TryInspect(out TrainerClassDatasetState state, out error)) return false;
            if (state.MetadataCount <= 1)
            {
                error = "At least one trainer class must be retained.";
                return false;
            }

            removedClassId = state.MetadataCount - 1;
            if (!TryFindTrainerUses(removedClassId, out List<int> trainerIds, out error))
            {
                removedClassId = -1;
                return false;
            }
            if (trainerIds.Count > 0)
            {
                error = "Trainer class " + removedClassId + " is used by trainer" +
                    (trainerIds.Count == 1 ? " " : "s ") + string.Join(", ", trainerIds) + ".";
                removedClassId = -1;
                return false;
            }

            string metadataPath = MetadataPath(removedClassId);
            string[] graphicsPaths = GraphicsPaths(removedClassId);
            byte[] metadata = null;
            byte[][] graphics = null;
            FileSnapshot nameJson = null;
            FileSnapshot descriptionJson = null;
            bool mutationStarted = false;

            try
            {
                var names = new TextArchive(RomInfo.trainerClassMessageNumber);
                var descriptions = new TextArchive(DescriptionArchiveId);
                metadata = File.ReadAllBytes(metadataPath);
                graphics = graphicsPaths.Select(File.ReadAllBytes).ToArray();
                nameJson = FileSnapshot.Capture(TextArchive.GetFilePaths(RomInfo.trainerClassMessageNumber).jsonPath);
                descriptionJson = FileSnapshot.Capture(TextArchive.GetFilePaths(DescriptionArchiveId).jsonPath);

                mutationStarted = true;
                File.Delete(metadataPath);
                foreach (string path in graphicsPaths) File.Delete(path);
                names.messages.RemoveAt(removedClassId);
                descriptions.messages.RemoveAt(removedClassId);
                names.SaveToExpandedDir(RomInfo.trainerClassMessageNumber, showSuccessMessage: false);
                descriptions.SaveToExpandedDir(DescriptionArchiveId, showSuccessMessage: false);

                TrainerClassMetadataStore.SetManagedRecordCount(removedClassId);
                return true;
            }
            catch (Exception ex)
            {
                var rollbackErrors = new List<string>();
                if (mutationStarted)
                {
                    TryRestoreDeletedFile(metadataPath, metadata, rollbackErrors);
                    for (int i = 0; i < graphicsPaths.Length; i++)
                        TryRestoreDeletedFile(graphicsPaths[i], graphics[i], rollbackErrors);
                    nameJson?.Restore(rollbackErrors);
                    descriptionJson?.Restore(rollbackErrors);
                }
                removedClassId = -1;
                error = "Trainer class was not removed: " + ex.Message +
                    (mutationStarted ? RollbackSummary(rollbackErrors) : " No files were changed.");
                return false;
            }
        }

        private static bool TryCountContiguousMembers(string directory, int? requiredLength,
            out int count, out string error)
        {
            count = 0;
            error = null;
            if (!Directory.Exists(directory))
            {
                error = "Required unpacked directory is missing: " + directory;
                return false;
            }

            string[] files = Directory.GetFiles(directory);
            count = files.Length;
            var actual = new HashSet<string>(files.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < count; i++)
            {
                string expected = Path.GetFullPath(Path.Combine(directory, i.ToString("D4")));
                if (!actual.Contains(expected))
                {
                    error = "Unpacked member sequence is not contiguous at " + expected + ".";
                    return false;
                }
                if (requiredLength.HasValue && new FileInfo(expected).Length != requiredLength.Value)
                {
                    error = "Unpacked member " + i + " is " + new FileInfo(expected).Length +
                        " bytes; expected " + requiredLength.Value + ".";
                    return false;
                }
            }
            return true;
        }

        private static string MetadataPath(int classId) =>
            Path.Combine(RomInfo.gameDirs[DirNames.trainerClassMetadata].unpackedDir, classId.ToString("D4"));

        private static string[] GraphicsPaths(int classId) =>
            Enumerable.Range(classId * GraphicsMembersPerClass, GraphicsMembersPerClass)
                .Select(id => Path.Combine(RomInfo.gameDirs[DirNames.trainerGraphics].unpackedDir, id.ToString("D4")))
                .ToArray();

        private static void TryDelete(string path, List<string> errors)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { errors.Add(Path.GetFileName(path) + ": " + ex.Message); }
        }

        private static void TryRestoreDeletedFile(string path, byte[] data, List<string> errors)
        {
            try
            {
                if (!File.Exists(path)) File.WriteAllBytes(path, data);
            }
            catch (Exception ex) { errors.Add(Path.GetFileName(path) + ": " + ex.Message); }
        }

        private static string RollbackSummary(List<string> errors) => errors.Count == 0
            ? " Previous data was restored."
            : " Rollback also failed for " + string.Join("; ", errors) + ". Restore the affected files from backup.";

        private sealed class FileSnapshot
        {
            private readonly string path;
            private readonly bool existed;
            private readonly byte[] data;

            private FileSnapshot(string path)
            {
                this.path = path;
                existed = File.Exists(path);
                data = existed ? File.ReadAllBytes(path) : null;
            }

            public static FileSnapshot Capture(string path) => new FileSnapshot(path);

            public void Restore(List<string> errors)
            {
                try
                {
                    if (existed)
                    {
                        if (File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(data)) return;
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        File.WriteAllBytes(path, data);
                    }
                    else if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception ex) { errors.Add(Path.GetFileName(path) + ": " + ex.Message); }
            }
        }
    }
}
