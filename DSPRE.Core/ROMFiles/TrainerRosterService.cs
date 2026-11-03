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

    /// <summary>Checks everything before a trainer is added or removed, so nothing is written until every file passes.</summary>
    public static class TrainerRosterService
    {
        public static TrainerRosterAnalysis AnalyzeCurrentProject()
        {
            if (HgEngineProject.IsActive)
            {
                int trainers = HgEngineTrainerSource.Count();
                if (trainers < 0) return Refuse("data/Trainers.c is missing from the checkout.");
                if (!HgEngineCapacity(out TrainerRosterCapacity limits, out string why)) return Refuse(why);
                int left = limits.RemainingAdditions(trainers, HgEngineSpecialIndex(limits, trainers));
                // The trainer on the approach script's entry is a placeholder, so it takes one of the ids left.
                if (HgEngineTrainerSource.ApproachTrainer() >= trainers && left > 0) left--;
                return new TrainerRosterAnalysis(true, null, isConsistent: true, trainerRecordCount: trainers, remainingAdditions: left);
            }

            if (!TrainerScriptDescriptor.TryFor(GameFamily, out TrainerScriptDescriptor scriptDescriptor))
            {
                return Refuse(Unsafe($"no shared trainer script for {GameFamily}"));
            }

            if (!TrainerScriptExecutableDescriptor.TryFor(GameVersion, GameLanguage,
                out TrainerScriptExecutableDescriptor executableDescriptor, out string executableError))
            {
                return Refuse(executableError);
            }

            if (!TrainerRosterCapacity.TryFor(GameVersion, GameLanguage,
                out TrainerRosterCapacity capacity, out string capacityError))
            {
                return Refuse(capacityError);
            }

            string propertiesDir = GameDirs[DirNames.TrainerProperties].unpackedDir;
            string partyDir = GameDirs[DirNames.TrainerParty].unpackedDir;
            string partyNarcPath = GameDirs[DirNames.TrainerParty].packedDir;
            if (!Directory.Exists(propertiesDir) || !File.Exists(partyNarcPath))
            {
                return Refuse("The trainer files aren't unpacked yet.");
            }

            int trainerCount = NarcAPI.Narc.MemberCount(propertiesDir);
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
                return Refuse(Unsafe(narcError), trainerCount);
            }
            TextArchive names = new TextArchive(TrainerNamesMessageNumber);
            int nameCount = names.Messages?.Count ?? 0;
            if (trainerCount <= 0 || trainerCount != partyCount || trainerCount != nameCount)
            {
                return Refuse(
                    Unsafe($"counts differ: properties {trainerCount}, parties {partyCount}, names {nameCount}"),
                    trainerCount, partyCount, nameCount);
            }

            ScriptFile sharedScripts = new ScriptFile(scriptDescriptor.SharedScriptArchiveId,
                readFunctions: true, readActions: true);
            if (sharedScripts.ParseFailedDueToInvalidCommand)
            {
                return Refuse(Unsafe("the shared trainer script didn't parse completely"),
                    trainerCount, partyCount, nameCount);
            }

            byte[] scriptBytes = sharedScripts.ToByteArray();
            string layoutError = null;
            TrainerScriptLayout layout = null;
            if (scriptBytes == null || !TrainerScriptLayout.TryAnalyze(scriptBytes,
                out layout, out layoutError))
            {
                return Refuse(Unsafe(layoutError ?? "the shared trainer script couldn't be written out"),
                    trainerCount, partyCount, nameCount);
            }

            ushort specialScriptNumber = checked((ushort)(3000 + layout.SpecialIndex));
            foreach (TrainerScriptPatchTarget target in executableDescriptor.Targets)
            {
                string path = target.File == TrainerScriptExecutableFile.Arm9
                    ? Arm9Path
                    : OverlayUtils.GetPath(target.OverlayNumber);
                if (!File.Exists(path))
                {
                    return Refuse(Unsafe($"{target.File} is missing"),
                        trainerCount, partyCount, nameCount, layout.SpecialIndex);
                }

                byte[] executable = File.ReadAllBytes(path);
                if (!TrainerScriptExecutableDescriptor.TryLocateTarget(executable,
                    specialScriptNumber, target.ExpectedThumbReferences, out _, out string patchError))
                {
                    return Refuse(Unsafe($"{target.File} trainer script references: {patchError}"),
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

        // hg-engine keeps the game's trainer flags (TRAINER_FLAG_BASE 1360 in asm/include/flags.inc, and the flags after
        // the last trainer's have their own uses), so HeartGold's limits are kept, including the shared trainer script's.
        private static bool HgEngineCapacity(out TrainerRosterCapacity capacity, out string error) =>
            TrainerRosterCapacity.TryFor(GameVersion, GameLanguage, out capacity, out error);

        /// <summary>The name of the hg-engine trainer that sits on the approach script's entry.</summary>
        public const string UnusableTrainerName = "Unused";

        // The shared trainer script's last index grows with each trainer added.
        private static int HgEngineSpecialIndex(TrainerRosterCapacity capacity, int trainers) =>
            capacity.RetailSpecialIndex + trainers - capacity.RetailTrainerRecordCount;

        private static bool TryAddHgEngineTrainer(string trainerName, out int trainerId, out string error)
        {
            trainerId = -1;
            int trainers = HgEngineTrainerSource.Count();
            if (trainers < 0) { error = "data/Trainers.c is missing from the checkout."; return false; }
            if (!HgEngineCapacity(out TrainerRosterCapacity capacity, out error)) return false;
            if (!capacity.CanAdd(trainers, HgEngineSpecialIndex(capacity, trainers), out error)) return false;
            // hg-engine's trainer script note appends entries after the approach script, so the trainer on its
            // entry can't be used on the field. It is added as a placeholder and the new trainer comes after it.
            if (HgEngineTrainerSource.ApproachTrainer() == trainers)
            {
                if (!capacity.CanAdd(trainers + 1, HgEngineSpecialIndex(capacity, trainers + 1), out error)) return false;
                if (!HgEngineTrainerSource.TryAdd(UnusableTrainerName, out _, out error)) return false;
                trainers++;
            }
            if (!HgEngineTrainerSource.TryGiveScript(trainers, out error)) return false;
            return HgEngineTrainerSource.TryAdd(trainerName, out trainerId, out error);
        }

        public static bool TryAddTrainer(string name, out int trainerId, out string error)
        {
            trainerId = -1;
            error = null;
            string trainerName = string.IsNullOrWhiteSpace(name) ? "Trainer" : name.Trim();
            if (HgEngineProject.IsActive)
            {
                // Up to four writes over two files: held until all of them work, so a failure leaves neither file changed.
                using HgEngineWriteSession session = HgEngineWriteSession.Begin();
                if (!TryAddHgEngineTrainer(trainerName, out trainerId, out error)) return false;
                return session.Commit(keepLostComments: true, out error);
            }
            int trainerNameLimit = TrainerNameMaxChars;
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

            string partyDir = GameDirs[DirNames.TrainerParty].unpackedDir;
            if (!Directory.Exists(partyDir))
            {
                error = "The trainer parties aren't unpacked yet.";
                return false;
            }

            TrainerScriptDescriptor.TryFor(GameFamily, out TrainerScriptDescriptor scriptDescriptor);
            TrainerScriptExecutableDescriptor.TryFor(GameVersion, GameLanguage,
                out TrainerScriptExecutableDescriptor executableDescriptor, out _);

            ScriptFile sharedScripts = new ScriptFile(scriptDescriptor.SharedScriptArchiveId,
                readFunctions: true, readActions: true);
            if (sharedScripts.PlaintextParseFailed)
            {
                error = "The shared trainer script has edits in its .script file that don't compile. Fix them first.";
                return false;
            }
            if (sharedScripts.ParseFailedDueToInvalidCommand ||
                !TrainerScriptLayout.TryInsertGenericAliasBeforeSpecial(sharedScripts, out error))
            {
                error = Failed(error ?? "the shared trainer script didn't parse completely");
                return false;
            }

            byte[] expandedScript = sharedScripts.ToByteArray();
            if (expandedScript == null || !TrainerScriptLayout.TryAnalyze(expandedScript,
                out TrainerScriptLayout expandedLayout, out error) ||
                expandedLayout.SpecialIndex != analysis.SpecialScriptIndex + 1)
            {
                error = Failed(error ?? "the expanded script layout didn't move exactly one entry");
                return false;
            }

            ushort oldSpecialNumber = checked((ushort)(3000 + analysis.SpecialScriptIndex));
            ushort newSpecialNumber = checked((ushort)(3000 + expandedLayout.SpecialIndex));
            ArchiveMembers.Batch batch = new ArchiveMembers.Batch();
            foreach (TrainerScriptPatchTarget target in executableDescriptor.Targets)
            {
                string path = target.File == TrainerScriptExecutableFile.Arm9
                    ? Arm9Path
                    : OverlayUtils.GetPath(target.OverlayNumber);
                byte[] source = File.ReadAllBytes(path);
                if (!TrainerScriptExecutableDescriptor.TryPatchTarget(source, oldSpecialNumber,
                    newSpecialNumber, target.ExpectedThumbReferences, out byte[] patched,
                    out _, out error))
                {
                    error = Failed(error);
                    return false;
                }
                batch.PutCode(path, patched);
            }

            trainerId = analysis.TrainerRecordCount;
            batch.Put(DirNames.TrainerProperties, trainerId,
                new TrainerProperties((ushort)trainerId, 1) { TrainerClass = TrainerProperties.NewTrainerClass }.ToByteArray());
            // One Pokemon, so battling the new trainer never reads an empty party.
            batch.Put(DirNames.TrainerParty, trainerId, new PartyPokemon { PokeID = 1, Level = 5 }.ToByteArray());

            TextArchive trainerNames = new TextArchive(TrainerNamesMessageNumber);
            if (trainerNames.Messages.Count != trainerId ||
                !trainerNames.SetSimpleTrainerName(trainerId, trainerName))
            {
                error = Failed("the trainer name couldn't be added to the name archive");
                trainerId = -1;
                return false;
            }
            (string binPath, string jsonPath) namePaths = TextArchive.GetFilePaths(TrainerNamesMessageNumber);
            batch.PutFile(namePaths.jsonPath, trainerNames.ToExpandedJsonBytes(TrainerNamesMessageNumber));

            (string binPath, string txtPath) scriptPaths = ScriptFile.GetFilePaths(scriptDescriptor.SharedScriptArchiveId);
            batch.Put(DirNames.Scripts, scriptDescriptor.SharedScriptArchiveId, expandedScript);
            if (!TryAddPlaintextMutation(batch, sharedScripts, scriptPaths.txtPath, "expanded", out error))
            {
                error = Failed(error);
                trainerId = -1;
                return false;
            }

            if (!batch.TryCommit(out error))
            {
                trainerId = -1;
                return false;
            }

            AfterCommit(scriptDescriptor.SharedScriptArchiveId);
            return true;
        }

        // A Rotom project's expanded/scripts is Rotom's source root: its source is regenerated from the
        // binary instead, and a .script there would be converted over it.
        private static bool TryAddPlaintextMutation(ArchiveMembers.Batch batch,
            ScriptFile scripts, string txtPath, string what, out string error)
        {
            error = null;
            if (RomInfo.HasRotomProject) return true;
            string plaintext = scripts.ToPlainText(includeActions: true);
            if (string.IsNullOrWhiteSpace(plaintext))
            {
                error = $"the {what} trainer script couldn't be written as text";
                return false;
            }
            batch.PutFile(txtPath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(plaintext));
            return true;
        }

        private static void AfterCommit(int sharedScriptId)
        {
            ScriptFile.ClearPlaintextCache();
            _ = ScriptSourceSync.BinaryWrittenAsync(sharedScriptId);
            TextArchive.RaiseSaved(null, TrainerNamesMessageNumber);
        }

        public static bool TryRemoveLastAddedTrainer(out int trainerId, out string error)
        {
            trainerId = -1;
            error = null;

            if (HgEngineProject.IsActive)
            {
                int last = HgEngineTrainerSource.Count() - 1;
                if (!TrainerRosterCapacity.TryFor(GameVersion, GameLanguage, out TrainerRosterCapacity retail, out error)) return false;
                if (last < retail.RetailTrainerRecordCount) { error = "Only trainers DSPRE added can be removed."; return false; }
                if (!TrainerReferenceScanner.TryFindCurrentProjectReferences(last, out List<TrainerReference> uses, out error)) return false;
                if (uses.Count > 0)
                {
                    error = $"Trainer {last} is still in use and can't be removed:" + Environment.NewLine
                        + string.Join(Environment.NewLine, uses.Take(8).Select(u => "• " + u));
                    return false;
                }
                return HgEngineTrainerSource.TryRemoveLast(out trainerId, out error);
            }

            TrainerRosterAnalysis analysis = AnalyzeCurrentProject();
            if (!analysis.IsConsistent)
            {
                error = analysis.RefusalReason;
                return false;
            }
            if (!TrainerRosterCapacity.TryFor(GameVersion, GameLanguage,
                out TrainerRosterCapacity capacity, out error))
            {
                return false;
            }
            if (analysis.TrainerRecordCount <= capacity.RetailTrainerRecordCount)
            {
                error = "Only trainers DSPRE added can be removed.";
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
                error = $"Trainer {trainerId} is still in use and can't be removed:" +
                    Environment.NewLine + locations;
                trainerId = -1;
                return false;
            }

            TrainerScriptDescriptor.TryFor(GameFamily, out TrainerScriptDescriptor scriptDescriptor);
            TrainerScriptExecutableDescriptor.TryFor(GameVersion, GameLanguage,
                out TrainerScriptExecutableDescriptor executableDescriptor, out _);
            ScriptFile sharedScripts = new ScriptFile(scriptDescriptor.SharedScriptArchiveId,
                readFunctions: true, readActions: true);
            if (sharedScripts.PlaintextParseFailed)
            {
                error = "The shared trainer script has edits in its .script file that don't compile. Fix them first.";
                trainerId = -1;
                return false;
            }
            if (sharedScripts.ParseFailedDueToInvalidCommand ||
                !TrainerScriptLayout.TryRemoveGenericAliasBeforeSpecial(sharedScripts, out error))
            {
                error = Failed(error ?? "the shared trainer script didn't parse completely");
                trainerId = -1;
                return false;
            }

            byte[] contractedScript = sharedScripts.ToByteArray();
            if (contractedScript == null || !TrainerScriptLayout.TryAnalyze(contractedScript,
                out TrainerScriptLayout contractedLayout, out error) ||
                contractedLayout.SpecialIndex != analysis.SpecialScriptIndex - 1)
            {
                error = Failed(error ?? "the shrunk script layout didn't move exactly one entry");
                trainerId = -1;
                return false;
            }

            ushort oldSpecialNumber = checked((ushort)(3000 + analysis.SpecialScriptIndex));
            ushort newSpecialNumber = checked((ushort)(3000 + contractedLayout.SpecialIndex));
            ArchiveMembers.Batch batch = new ArchiveMembers.Batch();
            foreach (TrainerScriptPatchTarget target in executableDescriptor.Targets)
            {
                string path = target.File == TrainerScriptExecutableFile.Arm9
                    ? Arm9Path
                    : OverlayUtils.GetPath(target.OverlayNumber);
                byte[] source = File.ReadAllBytes(path);
                if (!TrainerScriptExecutableDescriptor.TryPatchTarget(source, oldSpecialNumber,
                    newSpecialNumber, target.ExpectedThumbReferences, out byte[] patched,
                    out _, out error))
                {
                    error = Failed(error);
                    trainerId = -1;
                    return false;
                }
                batch.PutCode(path, patched);
            }

            batch.Delete(DirNames.TrainerProperties, trainerId);
            batch.Delete(DirNames.TrainerParty, trainerId);

            TextArchive trainerNames = new TextArchive(TrainerNamesMessageNumber);
            if (trainerNames.Messages.Count != analysis.TrainerNameCount)
            {
                error = Failed("the trainer name archive changed while the removal was checked");
                trainerId = -1;
                return false;
            }
            trainerNames.Messages.RemoveAt(trainerId);
            (string binPath, string jsonPath) namePaths = TextArchive.GetFilePaths(TrainerNamesMessageNumber);
            batch.PutFile(namePaths.jsonPath, trainerNames.ToExpandedJsonBytes(TrainerNamesMessageNumber));

            (string binPath, string txtPath) scriptPaths = ScriptFile.GetFilePaths(scriptDescriptor.SharedScriptArchiveId);
            batch.Put(DirNames.Scripts, scriptDescriptor.SharedScriptArchiveId, contractedScript);
            if (!TryAddPlaintextMutation(batch, sharedScripts, scriptPaths.txtPath, "shrunk", out error))
            {
                error = Failed(error);
                trainerId = -1;
                return false;
            }

            if (!batch.TryCommit(out error))
            {
                trainerId = -1;
                return false;
            }

            AfterCommit(scriptDescriptor.SharedScriptArchiveId);
            return true;
        }

        // The detail goes to the log; the dialog only needs to say what happened.
        private static string Unsafe(string detail)
        {
            AppLogger.Warn("Trainer list: " + detail);
            return "DSPRE can't safely change the trainer list in this ROM. The log has the details.";
        }

        private static string Failed(string detail)
        {
            AppLogger.Warn("Trainer list: " + detail);
            return "DSPRE couldn't change the trainer list, so nothing changed. The log has the details.";
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
                    error = "the trainer party archive header is cut short";
                    return false;
                }
            }

            if (header[0] != (byte)'N' || header[1] != (byte)'A' ||
                header[2] != (byte)'R' || header[3] != (byte)'C')
            {
                error = "the trainer party archive isn't a NARC";
                return false;
            }

            uint value = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x18, sizeof(uint)));
            if (value > int.MaxValue)
            {
                error = "the trainer party archive member count is too large";
                return false;
            }

            count = (int)value;
            return true;
        }
    }
}
