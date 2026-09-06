using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DSPRE.HgEngine;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    public sealed class TrainerRosterAnalysis
    {
        internal TrainerRosterAnalysis(bool canAdd, string refusalReason, bool isConsistent = false,
            int trainerRecordCount = 0,
            int partyRecordCount = 0, int trainerNameCount = 0, int specialScriptIndex = -1,
            int remainingAdditions = 0)
        {
            CanAdd = canAdd;
            RefusalReason = refusalReason;
            IsConsistent = isConsistent;
            TrainerRecordCount = trainerRecordCount;
            PartyRecordCount = partyRecordCount;
            TrainerNameCount = trainerNameCount;
            SpecialScriptIndex = specialScriptIndex;
            RemainingAdditions = remainingAdditions;
        }

        public bool CanAdd { get; }
        public string RefusalReason { get; }
        public bool IsConsistent { get; }
        public int TrainerRecordCount { get; }
        public int PartyRecordCount { get; }
        public int TrainerNameCount { get; }
        public int SpecialScriptIndex { get; }
        public int RemainingAdditions { get; }
    }

    /// <summary>
    /// Owns preflight validation for trainer roster expansion. Mutation remains separate so callers
    /// cannot write one roster resource before every dependency has passed analysis.
    /// </summary>
    public static class TrainerRosterService
    {
        public static TrainerRosterAnalysis AnalyzeCurrentProject()
        {
            if (HgEngineProject.IsActive)
            {
                return Refuse("Adding trainers to hg-engine source-backed projects is not implemented.");
            }

            if (!TrainerScriptDescriptor.TryFor(gameFamily, out TrainerScriptDescriptor scriptDescriptor))
            {
                return Refuse($"The {gameFamily} shared trainer-script bank is not supported.");
            }

            if (!TrainerScriptExecutableDescriptor.TryFor(gameVersion, gameLanguage,
                out TrainerScriptExecutableDescriptor executableDescriptor, out string executableError))
            {
                return Refuse(executableError);
            }

            if (!TrainerRosterCapacity.TryFor(gameVersion, gameLanguage,
                out TrainerRosterCapacity capacity, out string capacityError))
            {
                return Refuse(capacityError);
            }

            string propertiesDir = gameDirs[DirNames.trainerProperties].unpackedDir;
            string partyDir = gameDirs[DirNames.trainerParty].unpackedDir;
            string partyNarcPath = gameDirs[DirNames.trainerParty].packedDir;
            if (!Directory.Exists(propertiesDir) || !File.Exists(partyNarcPath))
            {
                return Refuse("The trainer properties and packed party archive must be available first.");
            }

            int trainerCount = Directory.GetFiles(propertiesDir).Length;
            int partyCount;
            string[] unpackedPartyFiles = Directory.Exists(partyDir)
                ? Directory.GetFiles(partyDir)
                : Array.Empty<string>();
            if (unpackedPartyFiles.Length > 0)
            {
                partyCount = unpackedPartyFiles.Length;
            }
            else if (!TryReadNarcElementCount(partyNarcPath, out partyCount, out string narcError))
            {
                return Refuse(narcError, trainerCount);
            }
            var names = new TextArchive(trainerNamesMessageNumber);
            int nameCount = names.messages?.Count ?? 0;
            if (trainerCount <= 0 || trainerCount != partyCount || trainerCount != nameCount)
            {
                return Refuse(
                    $"Trainer resource counts differ: properties {trainerCount}, parties {partyCount}, names {nameCount}.",
                    trainerCount, partyCount, nameCount);
            }

            var sharedScripts = new ScriptFile(scriptDescriptor.SharedScriptArchiveId,
                readFunctions: true, readActions: true);
            if (sharedScripts.parseFailedDueToInvalidCommand)
            {
                return Refuse("The shared trainer-script file did not parse completely.",
                    trainerCount, partyCount, nameCount);
            }

            byte[] scriptBytes = sharedScripts.ToByteArray();
            string layoutError = null;
            TrainerScriptLayout layout = null;
            if (scriptBytes == null || !TrainerScriptLayout.TryAnalyze(scriptBytes,
                out layout, out layoutError))
            {
                return Refuse(layoutError ?? "The shared trainer-script file could not be serialized.",
                    trainerCount, partyCount, nameCount);
            }

            ushort specialScriptNumber = checked((ushort)(3000 + layout.SpecialIndex));
            foreach (TrainerScriptPatchTarget target in executableDescriptor.Targets)
            {
                string path = target.File == TrainerScriptExecutableFile.Arm9
                    ? arm9Path
                    : OverlayUtils.GetPath(target.OverlayNumber);
                if (!File.Exists(path))
                {
                    return Refuse($"The {target.File} executable patch target is missing.",
                        trainerCount, partyCount, nameCount, layout.SpecialIndex);
                }

                byte[] executable = File.ReadAllBytes(path);
                if (!TrainerScriptExecutableDescriptor.TryLocateTarget(executable,
                    specialScriptNumber, target.ExpectedThumbReferences, out _, out string patchError))
                {
                    return Refuse($"The {target.File} trainer-script signature is unsupported: {patchError}",
                        trainerCount, partyCount, nameCount, layout.SpecialIndex);
                }
            }

            if (!capacity.CanAdd(trainerCount, layout.SpecialIndex, out string refusalReason))
            {
                return new TrainerRosterAnalysis(false, refusalReason, isConsistent: true,
                    trainerCount, partyCount, nameCount, layout.SpecialIndex);
            }

            return new TrainerRosterAnalysis(true, null, isConsistent: true,
                trainerCount, partyCount, nameCount,
                layout.SpecialIndex, capacity.RemainingAdditions(trainerCount, layout.SpecialIndex));
        }

        public static bool TryAddTrainer(string name, out int trainerId, out string error)
        {
            trainerId = -1;
            error = null;
            string trainerName = string.IsNullOrWhiteSpace(name) ? "Trainer" : name.Trim();
            int trainerNameLimit = trainerNameMaxLen;
            if (trainerName.Length > trainerNameLimit)
            {
                error = $"Trainer names are limited to {trainerNameLimit} characters in this ROM.";
                return false;
            }

            TrainerRosterAnalysis analysis = AnalyzeCurrentProject();
            if (!analysis.CanAdd)
            {
                error = analysis.RefusalReason;
                return false;
            }

            string partyDir = gameDirs[DirNames.trainerParty].unpackedDir;
            if (!Directory.Exists(partyDir))
            {
                error = "The trainer-party archive must be unpacked before adding a trainer.";
                return false;
            }

            TrainerScriptDescriptor.TryFor(gameFamily, out TrainerScriptDescriptor scriptDescriptor);
            TrainerScriptExecutableDescriptor.TryFor(gameVersion, gameLanguage,
                out TrainerScriptExecutableDescriptor executableDescriptor, out _);

            var sharedScripts = new ScriptFile(scriptDescriptor.SharedScriptArchiveId,
                readFunctions: true, readActions: true);
            if (sharedScripts.parseFailedDueToInvalidCommand ||
                !TrainerScriptLayout.TryInsertGenericAliasBeforeSpecial(sharedScripts, out error))
            {
                error ??= "The shared trainer-script file did not parse completely.";
                return false;
            }

            byte[] expandedScript = sharedScripts.ToByteArray();
            if (expandedScript == null || !TrainerScriptLayout.TryAnalyze(expandedScript,
                out TrainerScriptLayout expandedLayout, out error) ||
                expandedLayout.SpecialIndex != analysis.SpecialScriptIndex + 1)
            {
                error ??= "The expanded trainer-script layout did not move exactly one entry.";
                return false;
            }

            ushort oldSpecialNumber = checked((ushort)(3000 + analysis.SpecialScriptIndex));
            ushort newSpecialNumber = checked((ushort)(3000 + expandedLayout.SpecialIndex));
            var mutations = new List<TrainerRosterFileMutation>();
            foreach (TrainerScriptPatchTarget target in executableDescriptor.Targets)
            {
                string path = target.File == TrainerScriptExecutableFile.Arm9
                    ? arm9Path
                    : OverlayUtils.GetPath(target.OverlayNumber);
                byte[] source = File.ReadAllBytes(path);
                if (!TrainerScriptExecutableDescriptor.TryPatchTarget(source, oldSpecialNumber,
                    newSpecialNumber, target.ExpectedThumbReferences, out byte[] patched,
                    out _, out error))
                {
                    return false;
                }
                mutations.Add(new TrainerRosterFileMutation(path, patched));
            }

            trainerId = analysis.TrainerRecordCount;
            string index = trainerId.ToString("D4");
            mutations.Add(new TrainerRosterFileMutation(
                Path.Combine(gameDirs[DirNames.trainerProperties].unpackedDir, index),
                new TrainerProperties((ushort)trainerId).ToByteArray()));
            mutations.Add(new TrainerRosterFileMutation(Path.Combine(partyDir, index),
                new PartyPokemon().ToByteArray()));

            var trainerNames = new TextArchive(trainerNamesMessageNumber);
            if (trainerNames.messages.Count != trainerId ||
                !trainerNames.SetSimpleTrainerName(trainerId, trainerName))
            {
                error = "The trainer-name archive could not append the new trainer name.";
                trainerId = -1;
                return false;
            }
            var namePaths = TextArchive.GetFilePaths(trainerNamesMessageNumber);
            mutations.Add(new TrainerRosterFileMutation(namePaths.jsonPath,
                trainerNames.ToExpandedJsonBytes(trainerNamesMessageNumber)));

            var scriptPaths = ScriptFile.GetFilePaths(scriptDescriptor.SharedScriptArchiveId);
            mutations.Add(new TrainerRosterFileMutation(scriptPaths.binPath, expandedScript));
            string plaintext = sharedScripts.ToPlainText(includeActions: true);
            if (string.IsNullOrWhiteSpace(plaintext))
            {
                error = "The expanded trainer-script plaintext could not be serialized.";
                trainerId = -1;
                return false;
            }
            mutations.Add(new TrainerRosterFileMutation(scriptPaths.txtPath,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(plaintext)));

            if (!TrainerRosterFileTransaction.TryCommit(mutations, out error))
            {
                trainerId = -1;
                return false;
            }

            ScriptFile.ClearPlaintextCache();
            return true;
        }

        public static bool TryRemoveLastAddedTrainer(out int trainerId, out string error)
        {
            trainerId = -1;
            error = null;

            TrainerRosterAnalysis analysis = AnalyzeCurrentProject();
            if (!analysis.IsConsistent)
            {
                error = analysis.RefusalReason;
                return false;
            }
            if (!TrainerRosterCapacity.TryFor(gameVersion, gameLanguage,
                out TrainerRosterCapacity capacity, out error))
            {
                return false;
            }
            if (analysis.TrainerRecordCount <= capacity.RetailTrainerRecordCount)
            {
                error = "Retail trainers cannot be removed. Only trainers appended by DSPRE are eligible.";
                return false;
            }

            trainerId = analysis.TrainerRecordCount - 1;
            if (!TrainerReferenceScanner.TryFindCurrentProjectReferences(trainerId,
                out List<TrainerReference> references, out error))
            {
                trainerId = -1;
                return false;
            }
            if (references.Count > 0)
            {
                string locations = string.Join(Environment.NewLine,
                    references.Take(8).Select(reference => "• " + reference));
                if (references.Count > 8)
                {
                    locations += Environment.NewLine + $"• and {references.Count - 8} more";
                }
                error = $"Trainer {trainerId} is still in use and cannot be removed:" +
                    Environment.NewLine + locations;
                trainerId = -1;
                return false;
            }

            TrainerScriptDescriptor.TryFor(gameFamily, out TrainerScriptDescriptor scriptDescriptor);
            TrainerScriptExecutableDescriptor.TryFor(gameVersion, gameLanguage,
                out TrainerScriptExecutableDescriptor executableDescriptor, out _);
            var sharedScripts = new ScriptFile(scriptDescriptor.SharedScriptArchiveId,
                readFunctions: true, readActions: true);
            if (sharedScripts.parseFailedDueToInvalidCommand ||
                !TrainerScriptLayout.TryRemoveGenericAliasBeforeSpecial(sharedScripts, out error))
            {
                error ??= "The shared trainer-script file did not parse completely.";
                trainerId = -1;
                return false;
            }

            byte[] contractedScript = sharedScripts.ToByteArray();
            if (contractedScript == null || !TrainerScriptLayout.TryAnalyze(contractedScript,
                out TrainerScriptLayout contractedLayout, out error) ||
                contractedLayout.SpecialIndex != analysis.SpecialScriptIndex - 1)
            {
                error ??= "The contracted trainer-script layout did not move exactly one entry.";
                trainerId = -1;
                return false;
            }

            ushort oldSpecialNumber = checked((ushort)(3000 + analysis.SpecialScriptIndex));
            ushort newSpecialNumber = checked((ushort)(3000 + contractedLayout.SpecialIndex));
            var mutations = new List<TrainerRosterFileMutation>();
            foreach (TrainerScriptPatchTarget target in executableDescriptor.Targets)
            {
                string path = target.File == TrainerScriptExecutableFile.Arm9
                    ? arm9Path
                    : OverlayUtils.GetPath(target.OverlayNumber);
                byte[] source = File.ReadAllBytes(path);
                if (!TrainerScriptExecutableDescriptor.TryPatchTarget(source, oldSpecialNumber,
                    newSpecialNumber, target.ExpectedThumbReferences, out byte[] patched,
                    out _, out error))
                {
                    trainerId = -1;
                    return false;
                }
                mutations.Add(new TrainerRosterFileMutation(path, patched));
            }

            string index = trainerId.ToString("D4");
            mutations.Add(TrainerRosterFileMutation.Delete(
                Path.Combine(gameDirs[DirNames.trainerProperties].unpackedDir, index)));
            mutations.Add(TrainerRosterFileMutation.Delete(
                Path.Combine(gameDirs[DirNames.trainerParty].unpackedDir, index)));

            var trainerNames = new TextArchive(trainerNamesMessageNumber);
            if (trainerNames.messages.Count != analysis.TrainerNameCount)
            {
                error = "The trainer-name archive changed during removal analysis.";
                trainerId = -1;
                return false;
            }
            trainerNames.messages.RemoveAt(trainerId);
            var namePaths = TextArchive.GetFilePaths(trainerNamesMessageNumber);
            mutations.Add(new TrainerRosterFileMutation(namePaths.jsonPath,
                trainerNames.ToExpandedJsonBytes(trainerNamesMessageNumber)));

            var scriptPaths = ScriptFile.GetFilePaths(scriptDescriptor.SharedScriptArchiveId);
            mutations.Add(new TrainerRosterFileMutation(scriptPaths.binPath, contractedScript));
            string plaintext = sharedScripts.ToPlainText(includeActions: true);
            if (string.IsNullOrWhiteSpace(plaintext))
            {
                error = "The contracted trainer-script plaintext could not be serialized.";
                trainerId = -1;
                return false;
            }
            mutations.Add(new TrainerRosterFileMutation(scriptPaths.txtPath,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(plaintext)));

            if (!TrainerRosterFileTransaction.TryCommit(mutations, out error))
            {
                trainerId = -1;
                return false;
            }

            ScriptFile.ClearPlaintextCache();
            return true;
        }

        private static TrainerRosterAnalysis Refuse(string reason, int trainerCount = 0,
            int partyCount = 0, int nameCount = 0, int specialIndex = -1)
        {
            return new TrainerRosterAnalysis(false, reason, isConsistent: false, trainerCount,
                partyCount, nameCount, specialIndex);
        }

        private static bool TryReadNarcElementCount(string path, out int count, out string error)
        {
            count = 0;
            error = null;
            byte[] header = new byte[0x1C];
            using (FileStream input = File.OpenRead(path))
            {
                if (input.Length < header.Length || input.Read(header, 0, header.Length) != header.Length)
                {
                    error = "The trainer-party NARC header is truncated.";
                    return false;
                }
            }

            if (header[0] != (byte)'N' || header[1] != (byte)'A' ||
                header[2] != (byte)'R' || header[3] != (byte)'C')
            {
                error = "The trainer-party archive is not a NARC file.";
                return false;
            }

            uint value = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x18, sizeof(uint)));
            if (value > int.MaxValue)
            {
                error = "The trainer-party NARC element count is too large.";
                return false;
            }

            count = (int)value;
            return true;
        }
    }

    internal sealed class TrainerRosterFileMutation
    {
        public TrainerRosterFileMutation(string path, byte[] bytes)
        {
            Path = path;
            Bytes = bytes;
        }

        private TrainerRosterFileMutation(string path)
        {
            Path = path;
            IsDeletion = true;
        }

        public string Path { get; }
        public byte[] Bytes { get; }
        public bool IsDeletion { get; }

        public static TrainerRosterFileMutation Delete(string path) => new(path);
    }

    internal static class TrainerRosterFileTransaction
    {
        public static bool TryCommit(IReadOnlyList<TrainerRosterFileMutation> mutations,
            out string error)
        {
            error = null;
            if (mutations == null || mutations.Count == 0)
            {
                error = "The trainer roster transaction has no outputs.";
                return false;
            }

            if (mutations.Any(item => item == null || string.IsNullOrWhiteSpace(item.Path) ||
                (!item.IsDeletion && item.Bytes == null)) ||
                mutations.Select(item => System.IO.Path.GetFullPath(item.Path))
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != mutations.Count)
            {
                error = "The trainer roster transaction contains invalid or duplicate outputs.";
                return false;
            }

            string token = Guid.NewGuid().ToString("N");
            var staged = new List<(TrainerRosterFileMutation mutation, string stage,
                bool existed, byte[] original, DateTime timestamp)>();
            var createdDirectories = new List<string>();
            try
            {
                foreach (TrainerRosterFileMutation mutation in mutations)
                {
                    string fullPath = System.IO.Path.GetFullPath(mutation.Path);
                    string directory = System.IO.Path.GetDirectoryName(fullPath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                        createdDirectories.Add(directory);
                    }

                    bool existed = File.Exists(fullPath);
                    byte[] original = existed ? File.ReadAllBytes(fullPath) : null;
                    DateTime timestamp = existed ? File.GetLastWriteTimeUtc(fullPath) : default;
                    if (mutation.IsDeletion && !existed)
                    {
                        throw new FileNotFoundException("A trainer roster deletion target is missing.", fullPath);
                    }
                    string stage = mutation.IsDeletion ? null : fullPath + ".dspre-stage-" + token;
                    staged.Add((mutation, stage, existed, original, timestamp));
                    if (!mutation.IsDeletion) File.WriteAllBytes(stage, mutation.Bytes);
                }

                foreach (var item in staged)
                {
                    string target = System.IO.Path.GetFullPath(item.mutation.Path);
                    if (item.mutation.IsDeletion) File.Delete(target);
                    else File.Move(item.stage, target, overwrite: true);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = $"Trainer roster transaction failed and was rolled back: {ex.Message}";
                foreach (var item in staged)
                {
                    string target = System.IO.Path.GetFullPath(item.mutation.Path);
                    try
                    {
                        if (item.existed)
                        {
                            File.WriteAllBytes(target, item.original);
                            File.SetLastWriteTimeUtc(target, item.timestamp);
                        }
                        else if (File.Exists(target))
                        {
                            File.Delete(target);
                        }
                    }
                    catch (Exception rollbackError)
                    {
                        error += $" Rollback also failed for {System.IO.Path.GetFileName(target)}: " +
                            rollbackError.Message;
                    }
                }
                return false;
            }
            finally
            {
                foreach (var item in staged)
                {
                    if (!string.IsNullOrEmpty(item.stage) && File.Exists(item.stage)) File.Delete(item.stage);
                }
                for (int i = createdDirectories.Count - 1; i >= 0; i--)
                {
                    if (Directory.Exists(createdDirectories[i]) &&
                        !Directory.EnumerateFileSystemEntries(createdDirectories[i]).Any())
                    {
                        Directory.Delete(createdDirectories[i]);
                    }
                }
            }
        }
    }
}
