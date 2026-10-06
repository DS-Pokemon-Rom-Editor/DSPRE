using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>How many records each per-class dataset holds under the trainer class metadata patch.</summary>
    public sealed class TrainerClassDatasetState
    {
        public int MetadataCount { get; internal set; }
        public int NameCount { get; internal set; }
        public int DescriptionCount { get; internal set; }
        public int GraphicsMemberCount { get; internal set; }
        public int GraphicsClassCount => GraphicsMemberCount / TrainerClassDatasetManager.GraphicsMembersPerClass;
        public bool GraphicsCountIsDivisible => GraphicsMemberCount % TrainerClassDatasetManager.GraphicsMembersPerClass == 0;

        public string CountSummary =>
            "metadata records: " + MetadataCount +
            "; class names: " + NameCount +
            "; class descriptions: " + DescriptionCount +
            "; trainer graphics members: " + GraphicsMemberCount +
            (GraphicsCountIsDivisible ? " (" + GraphicsClassCount + " class sets)" : " (not divisible by 5)");
    }

    /// <summary>
    /// Adds and removes whole trainer classes under the trainer class metadata patch (PR #272): the metadata record
    /// in a/1/5/5, the class name, its description, and its five trainer graphics members, which must stay in step.
    /// Classes are appended by copying an existing one, and only the last class can be removed.
    /// </summary>
    public static class TrainerClassDatasetManager
    {
        public const int GraphicsMembersPerClass = 5;

        private static int DescriptionArchiveId => trainerClassWithArticleMessageNumber;

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
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.textArchives, DirNames.trainerGraphics, DirNames.trainerProperties });
                string graphicsDir = gameDirs[DirNames.trainerGraphics].unpackedDir;
                if (!TryCountContiguousMembers(graphicsDir, out int graphicsCount, out error)) return false;

                TextArchive names = new TextArchive(trainerClassMessageNumber);
                TextArchive descriptions = new TextArchive(DescriptionArchiveId);
                state = new TrainerClassDatasetState
                {
                    MetadataCount = TrainerClassMetadataStore.RecordCount,
                    NameCount = names.messages.Count,
                    DescriptionCount = descriptions.messages.Count,
                    GraphicsMemberCount = graphicsCount,
                };

                bool countsAgree = state.MetadataCount >= TrainerClassMetadataStore.MinimumRecordCount &&
                    state.NameCount == state.MetadataCount &&
                    state.DescriptionCount == state.MetadataCount &&
                    state.GraphicsCountIsDivisible &&
                    state.GraphicsClassCount == state.MetadataCount;
                if (!countsAgree)
                {
                    error = "The trainer class data sets have different sizes (" + state.CountSummary +
                        "). Copying and removing classes is disabled; nothing was repaired.";
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

        /// <summary>Appends a copy of <paramref name="sourceClassId"/> as the next class.</summary>
        public static bool TryCopyClass(int sourceClassId, out int newClassId, out string error)
        {
            newClassId = -1;
            if (!TryInspect(out TrainerClassDatasetState state, out error)) return false;
            if (sourceClassId < 0 || sourceClassId >= state.MetadataCount)
            {
                error = "The source trainer class does not exist.";
                return false;
            }
            // Trainers store their class in one byte.
            if (state.MetadataCount > byte.MaxValue)
            {
                error = "Trainers can't use a class after 255.";
                return false;
            }

            newClassId = state.MetadataCount;
            string metadataDestination = MetadataPath(newClassId);
            string[] graphicsSources = GraphicsPaths(sourceClassId);
            string[] graphicsDestinations = GraphicsPaths(newClassId);
            List<string> addedFiles = new List<string> { metadataDestination };
            addedFiles.AddRange(graphicsDestinations);
            if (addedFiles.Any(path => File.Exists(path) || Directory.Exists(path)))
            {
                error = "A file for trainer class " + newClassId + " already exists.";
                newClassId = -1;
                return false;
            }

            FileSnapshot nameJson = null, descriptionJson = null;
            bool mutationStarted = false;
            try
            {
                TextArchive names = new TextArchive(trainerClassMessageNumber);
                TextArchive descriptions = new TextArchive(DescriptionArchiveId);
                byte[] metadata = File.ReadAllBytes(MetadataPath(sourceClassId));
                byte[][] graphics = graphicsSources.Select(File.ReadAllBytes).ToArray();
                nameJson = FileSnapshot.Capture(TextArchive.GetFilePaths(trainerClassMessageNumber).jsonPath);
                descriptionJson = FileSnapshot.Capture(TextArchive.GetFilePaths(DescriptionArchiveId).jsonPath);
                names.messages.Add(names.messages[sourceClassId]);
                descriptions.messages.Add(descriptions.messages[sourceClassId]);

                mutationStarted = true;
                File.WriteAllBytes(metadataDestination, metadata);
                for (int i = 0; i < graphics.Length; i++) File.WriteAllBytes(graphicsDestinations[i], graphics[i]);
                names.SaveToExpandedDir(trainerClassMessageNumber, showSuccessMessage: false);
                descriptions.SaveToExpandedDir(DescriptionArchiveId, showSuccessMessage: false);

                TrainerClassMetadataStore.SetManagedRecordCount(newClassId + 1);
                return true;
            }
            catch (Exception ex)
            {
                List<string> rollbackErrors = new List<string>();
                if (mutationStarted)
                {
                    foreach (string path in addedFiles) TryDelete(path, rollbackErrors);
                    nameJson?.Restore(rollbackErrors);
                    descriptionJson?.Restore(rollbackErrors);
                }
                newClassId = -1;
                error = "The trainer class was not added: " + ex.Message +
                    (mutationStarted ? RollbackSummary(rollbackErrors) : " No files were changed.");
                return false;
            }
        }

        /// <summary>The trainers whose class is <paramref name="classId"/>.</summary>
        public static bool TryFindTrainerUses(int classId, out List<int> trainerIds, out string error)
        {
            trainerIds = new List<int>();
            error = null;
            if (classId < 0)
            {
                error = "A trainer class can't be negative.";
                return false;
            }
            if (classId > byte.MaxValue) return true;

            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerProperties });
                string directory = gameDirs[DirNames.trainerProperties].unpackedDir;
                if (!Directory.Exists(directory))
                {
                    error = "The trainer properties archive could not be unpacked.";
                    return false;
                }

                foreach (string path in Directory.GetFiles(directory).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    byte[] data = File.ReadAllBytes(path);
                    if (data.Length < 2)
                    {
                        error = "Trainer properties file " + Path.GetFileName(path) + " is too short to check.";
                        trainerIds.Clear();
                        return false;
                    }
                    if (data[1] != classId) continue;
                    if (int.TryParse(Path.GetFileName(path), out int trainerId)) trainerIds.Add(trainerId);
                    else
                    {
                        error = "Unexpected trainer properties file name: " + Path.GetFileName(path) + ".";
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

        /// <summary>
        /// Everything that uses a class: its trainers, what uses those trainers (events, scripts, battle messages,
        /// rematch tables, the phone book), Battle Tower trainers of the class, and scripts that print its name.
        /// </summary>
        public static bool TryFindClassUses(int classId, out List<TrainerReference> uses, out string error)
        {
            uses = new List<TrainerReference>();
            if (!TryFindTrainerUses(classId, out List<int> trainerIds, out error)) return false;
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.scripts, DirNames.eventFiles, DirNames.trainerTextTable });
                string[] names = GetSimpleTrainerNames();
                foreach (int id in trainerIds)
                    uses.Add(new TrainerReference("Trainer", id < names.Length ? $"{id} {names[id]}" : id.ToString()) { TrainerId = id, Index = id });

                if (!TrainerReferenceScanner.TryFindCurrentProjectReferences(new HashSet<int>(trainerIds), classId,
                        out List<TrainerReference> found, out error))
                {
                    uses.Clear();
                    return false;
                }
                foreach (TrainerReference reference in found)
                    uses.Add(reference.TrainerId >= 0
                        ? new TrainerReference(reference.Kind, $"{reference.Location}, for trainer {reference.TrainerId}")
                        {
                            TrainerId = reference.TrainerId, SourceId = reference.SourceId, Index = reference.Index, InFunction = reference.InFunction,
                        }
                        : reference);

                if (BattleTowerTrainerFile.IsAvailable())
                {
                    DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.battleTowerTrainers });
                    BattleTowerTrainerFile tower = new BattleTowerTrainerFile(true);
                    for (int i = 0; i < tower.Trainers.Count; i++)
                        if (tower.Trainers[i].UnreadBytes == null && tower.Trainers[i].TrainerType == classId)
                            uses.Add(new TrainerReference("Battle Tower", string.IsNullOrEmpty(tower.Trainers[i].Name) ? $"trainer {i}" : $"trainer {i} {tower.Trainers[i].Name}") { Index = i });
                }
                return true;
            }
            catch (Exception ex)
            {
                uses.Clear();
                error = "The uses of class " + classId + " could not be checked: " + ex.Message;
                return false;
            }
        }

        /// <summary>The uses as a short list for a message, the first few and a count of the rest.</summary>
        public static string DescribeUses(IReadOnlyList<TrainerReference> uses, int shown = 12)
        {
            string list = string.Join(Environment.NewLine, uses.Take(shown).Select(u => "- " + u));
            return uses.Count > shown ? list + Environment.NewLine + $"- and {uses.Count - shown} more" : list;
        }

        /// <summary>Removes the last class, refusing while anything uses it.</summary>
        public static bool TryRemoveLastClass(out int removedClassId, out string error)
        {
            removedClassId = -1;
            if (!TryInspect(out TrainerClassDatasetState state, out error)) return false;
            if (state.MetadataCount <= 1)
            {
                error = "At least one trainer class must stay.";
                return false;
            }

            removedClassId = state.MetadataCount - 1;
            if (!TryFindClassUses(removedClassId, out List<TrainerReference> uses, out error))
            {
                removedClassId = -1;
                return false;
            }
            if (uses.Count > 0)
            {
                error = "Trainer class " + removedClassId + " is still in use:" + Environment.NewLine + DescribeUses(uses);
                removedClassId = -1;
                return false;
            }

            string metadataPath = MetadataPath(removedClassId);
            string[] graphicsPaths = GraphicsPaths(removedClassId);
            byte[] metadata = null;
            byte[][] graphics = null;
            FileSnapshot nameJson = null, descriptionJson = null;
            bool mutationStarted = false;
            try
            {
                TextArchive names = new TextArchive(trainerClassMessageNumber);
                TextArchive descriptions = new TextArchive(DescriptionArchiveId);
                metadata = File.ReadAllBytes(metadataPath);
                graphics = graphicsPaths.Select(File.ReadAllBytes).ToArray();
                nameJson = FileSnapshot.Capture(TextArchive.GetFilePaths(trainerClassMessageNumber).jsonPath);
                descriptionJson = FileSnapshot.Capture(TextArchive.GetFilePaths(DescriptionArchiveId).jsonPath);

                mutationStarted = true;
                File.Delete(metadataPath);
                foreach (string path in graphicsPaths) File.Delete(path);
                names.messages.RemoveAt(removedClassId);
                descriptions.messages.RemoveAt(removedClassId);
                names.SaveToExpandedDir(trainerClassMessageNumber, showSuccessMessage: false);
                descriptions.SaveToExpandedDir(DescriptionArchiveId, showSuccessMessage: false);

                TrainerClassMetadataStore.SetManagedRecordCount(removedClassId);
                return true;
            }
            catch (Exception ex)
            {
                List<string> rollbackErrors = new List<string>();
                if (mutationStarted)
                {
                    TryRestoreDeletedFile(metadataPath, metadata, rollbackErrors);
                    for (int i = 0; i < graphicsPaths.Length; i++) TryRestoreDeletedFile(graphicsPaths[i], graphics[i], rollbackErrors);
                    nameJson?.Restore(rollbackErrors);
                    descriptionJson?.Restore(rollbackErrors);
                }
                removedClassId = -1;
                error = "The trainer class was not removed: " + ex.Message +
                    (mutationStarted ? RollbackSummary(rollbackErrors) : " No files were changed.");
                return false;
            }
        }

        private static bool TryCountContiguousMembers(string directory, out int count, out string error)
        {
            count = 0;
            error = null;
            if (!Directory.Exists(directory))
            {
                error = "Missing unpacked folder: " + directory;
                return false;
            }
            string[] files = Directory.GetFiles(directory);
            count = files.Length;
            HashSet<string> actual = new HashSet<string>(files.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < count; i++)
            {
                string expected = Path.GetFullPath(Path.Combine(directory, i.ToString("D4")));
                if (actual.Contains(expected)) continue;
                error = "The unpacked files skip " + expected + ".";
                return false;
            }
            return true;
        }

        private static string MetadataPath(int classId) =>
            Path.Combine(gameDirs[DirNames.trainerClassMetadata].unpackedDir, classId.ToString("D4"));

        private static string[] GraphicsPaths(int classId) =>
            Enumerable.Range(classId * GraphicsMembersPerClass, GraphicsMembersPerClass)
                .Select(id => Path.Combine(gameDirs[DirNames.trainerGraphics].unpackedDir, id.ToString("D4")))
                .ToArray();

        private static void TryDelete(string path, List<string> errors)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { errors.Add(Path.GetFileName(path) + ": " + ex.Message); }
        }

        private static void TryRestoreDeletedFile(string path, byte[] data, List<string> errors)
        {
            try { if (data != null && !File.Exists(path)) File.WriteAllBytes(path, data); }
            catch (Exception ex) { errors.Add(Path.GetFileName(path) + ": " + ex.Message); }
        }

        private static string RollbackSummary(List<string> errors) => errors.Count == 0
            ? " The previous data was put back."
            : " Putting it back also failed for " + string.Join("; ", errors) + ". Restore those files from a backup.";

        private sealed class FileSnapshot
        {
            private readonly string _path;
            private readonly bool _existed;
            private readonly byte[] _data;

            private FileSnapshot(string path)
            {
                _path = path;
                _existed = File.Exists(path);
                _data = _existed ? File.ReadAllBytes(path) : null;
            }

            public static FileSnapshot Capture(string path) => new FileSnapshot(path);

            public void Restore(List<string> errors)
            {
                try
                {
                    if (_existed)
                    {
                        if (File.Exists(_path) && File.ReadAllBytes(_path).SequenceEqual(_data)) return;
                        Directory.CreateDirectory(Path.GetDirectoryName(_path));
                        File.WriteAllBytes(_path, _data);
                    }
                    else if (File.Exists(_path)) File.Delete(_path);
                }
                catch (Exception ex) { errors.Add(Path.GetFileName(_path) + ": " + ex.Message); }
            }
        }
    }
}
