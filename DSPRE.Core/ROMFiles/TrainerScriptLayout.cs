using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Describes the shared trainer-script pointer table. Ordinary entries alias one generic trainer
    /// script and the final, unique entry is the eye-meets script used while a trainer approaches.
    /// </summary>
    public sealed class TrainerScriptLayout
    {
        private TrainerScriptLayout(int entryCount, int genericTarget, int specialTarget)
        {
            EntryCount = entryCount;
            GenericTarget = genericTarget;
            SpecialTarget = specialTarget;
        }

        public int EntryCount { get; }
        public int GenericEntryCount => EntryCount - 1;
        public int SpecialIndex => EntryCount - 1;
        public int GenericTarget { get; }
        public int SpecialTarget { get; }

        public static bool TryAnalyze(ReadOnlySpan<byte> data, out TrainerScriptLayout layout,
            out string error)
        {
            layout = null;
            error = null;

            if (!TryReadPointerTargets(data, out List<int> targets, out int headerLength, out error))
            {
                return false;
            }

            if (targets.Count < 2)
            {
                error = "The shared trainer-script table must contain a generic entry and a special entry.";
                return false;
            }

            int genericTarget = targets[0];
            int specialTarget = targets[^1];
            if (genericTarget < headerLength || specialTarget < headerLength)
            {
                error = "A shared trainer-script pointer targets the pointer table instead of a script body.";
                return false;
            }

            if (genericTarget == specialTarget)
            {
                error = "The final shared trainer-script entry does not have a unique eye-meets body.";
                return false;
            }

            for (int i = 0; i < targets.Count - 1; i++)
            {
                if (targets[i] != genericTarget)
                {
                    error = $"Shared trainer-script entry {i} does not alias the generic trainer body.";
                    return false;
                }
            }

            layout = new TrainerScriptLayout(targets.Count, genericTarget, specialTarget);
            return true;
        }

        /// <summary>
        /// Inserts one generic alias immediately before the special entry. The caller serializes and
        /// reparses the file before committing any output.
        /// </summary>
        public static bool TryInsertGenericAliasBeforeSpecial(ScriptFile scriptFile, out string error)
        {
            error = null;
            if (scriptFile?.allScripts == null || scriptFile.allScripts.Count < 2)
            {
                error = "The shared trainer-script file has no usable script table.";
                return false;
            }

            List<ScriptCommandContainer> scripts = scriptFile.allScripts;
            for (int i = 0; i < scripts.Count; i++)
            {
                if (scripts[i].manualUserID != (uint)(i + 1))
                {
                    error = "The shared trainer-script IDs are not sequential.";
                    return false;
                }
            }

            ScriptCommandContainer generic = scripts[0];
            ScriptCommandContainer special = scripts[^1];
            if (generic.usedScriptID != -1 || generic.commands == null ||
                special.usedScriptID != -1 || special.commands == null)
            {
                error = "The generic and eye-meets entries must both own script bodies.";
                return false;
            }

            if (scripts.Count(script => script.usedScriptID == -1) != 2)
            {
                error = "The shared trainer-script file does not contain exactly two script bodies.";
                return false;
            }

            int genericId = checked((int)generic.manualUserID);
            for (int i = 1; i < scripts.Count - 1; i++)
            {
                if (scripts[i].usedScriptID != genericId)
                {
                    error = $"Shared trainer-script entry {i} does not reference the generic entry.";
                    return false;
                }
            }

            if (special.manualUserID >= int.MaxValue)
            {
                error = "The shared trainer-script ID range is exhausted.";
                return false;
            }

            uint oldSpecialId = special.manualUserID;
            special.manualUserID++;

            // Functions can also use a script body by ID. Preserve any reference that deliberately
            // targeted the special body when its ID moves.
            if (scriptFile.allFunctions != null)
            {
                foreach (ScriptCommandContainer function in scriptFile.allFunctions)
                {
                    if (function.usedScriptID == checked((int)oldSpecialId))
                    {
                        function.usedScriptID = checked((int)special.manualUserID);
                    }
                }
            }

            scripts.Insert(scripts.Count - 1,
                new ScriptCommandContainer(oldSpecialId, ScriptFile.ContainerTypes.Script,
                    usedScriptID: genericId));
            return true;
        }

        /// <summary>Removes the final generic alias and moves the eye-meets entry back one slot.</summary>
        public static bool TryRemoveGenericAliasBeforeSpecial(ScriptFile scriptFile, out string error)
        {
            error = null;
            if (scriptFile?.allScripts == null || scriptFile.allScripts.Count < 3)
            {
                error = "The shared trainer-script file has no removable generic entry.";
                return false;
            }

            List<ScriptCommandContainer> scripts = scriptFile.allScripts;
            for (int i = 0; i < scripts.Count; i++)
            {
                if (scripts[i].manualUserID != (uint)(i + 1))
                {
                    error = "The shared trainer-script IDs are not sequential.";
                    return false;
                }
            }

            ScriptCommandContainer generic = scripts[0];
            ScriptCommandContainer removable = scripts[^2];
            ScriptCommandContainer special = scripts[^1];
            int genericId = checked((int)generic.manualUserID);
            if (generic.usedScriptID != -1 || generic.commands == null ||
                removable.usedScriptID != genericId || special.usedScriptID != -1 ||
                special.commands == null)
            {
                error = "The final shared trainer-script entries do not match the verified expansion layout.";
                return false;
            }
            if (removable.manualUserID + 1 != special.manualUserID)
            {
                error = "The removable trainer-script entry is not immediately before the eye-meets entry.";
                return false;
            }

            uint oldSpecialId = special.manualUserID;
            uint newSpecialId = oldSpecialId - 1;
            scripts.RemoveAt(scripts.Count - 2);
            special.manualUserID = newSpecialId;

            if (scriptFile.allFunctions != null)
            {
                foreach (ScriptCommandContainer function in scriptFile.allFunctions)
                {
                    if (function.usedScriptID == checked((int)oldSpecialId))
                    {
                        function.usedScriptID = checked((int)newSpecialId);
                    }
                }
            }
            return true;
        }

        public int GetScriptNumberForTrainer(int trainerId, int trainerRecordCount, bool doubleBattle)
        {
            if (trainerId <= 0 || trainerId >= trainerRecordCount)
            {
                throw new ArgumentOutOfRangeException(nameof(trainerId));
            }

            int localIndex = trainerId - 1;
            if (localIndex == SpecialIndex || localIndex >= EntryCount)
            {
                throw new InvalidOperationException("The trainer has no ordinary entry in the shared script table.");
            }

            return (doubleBattle ? 5000 : 3000) + localIndex;
        }

        public bool TryGetTrainerId(int scriptNumber, int trainerRecordCount, out int trainerId)
        {
            trainerId = 0;
            int localIndex;
            if (scriptNumber >= 3000 && scriptNumber < 4000)
            {
                localIndex = scriptNumber - 3000;
            }
            else if (scriptNumber >= 5000 && scriptNumber < 6000)
            {
                localIndex = scriptNumber - 5000;
            }
            else
            {
                return false;
            }

            if (localIndex < 0 || localIndex >= EntryCount || localIndex == SpecialIndex)
            {
                return false;
            }

            int candidate = localIndex + 1;
            if (candidate <= 0 || candidate >= trainerRecordCount)
            {
                return false;
            }

            trainerId = candidate;
            return true;
        }

        private static bool TryReadPointerTargets(ReadOnlySpan<byte> data, out List<int> targets,
            out int headerLength, out string error)
        {
            targets = new List<int>();
            headerLength = 0;
            error = null;

            for (int position = 0; ; position += sizeof(uint))
            {
                if (position + sizeof(ushort) > data.Length)
                {
                    error = "The shared trainer-script pointer table has no FD13 terminator.";
                    return false;
                }

                if (BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(position, sizeof(ushort))) == 0xFD13)
                {
                    headerLength = position + sizeof(ushort);
                    return true;
                }

                if (position + sizeof(uint) > data.Length)
                {
                    error = "The shared trainer-script pointer table is truncated.";
                    return false;
                }

                uint relativeOffset = BinaryPrimitives.ReadUInt32LittleEndian(
                    data.Slice(position, sizeof(uint)));
                long target = (long)position + sizeof(uint) + relativeOffset;
                if (target < 0 || target >= data.Length)
                {
                    error = $"Shared trainer-script pointer {targets.Count} targets outside the file.";
                    return false;
                }

                targets.Add(checked((int)target));
            }
        }
    }

    /// <summary>
    /// Retail table baseline only. Executable patch sites are revision-specific and are intentionally
    /// not represented here until their signatures have been independently verified.
    /// </summary>
    public sealed class TrainerScriptDescriptor
    {
        private TrainerScriptDescriptor(RomInfo.GameFamilies family, int sharedScriptArchiveId,
            int retailSpecialIndex)
        {
            Family = family;
            SharedScriptArchiveId = sharedScriptArchiveId;
            RetailSpecialIndex = retailSpecialIndex;
        }

        public RomInfo.GameFamilies Family { get; }
        public int SharedScriptArchiveId { get; }
        public int RetailSpecialIndex { get; }

        public static bool TryFor(RomInfo.GameFamilies family, out TrainerScriptDescriptor descriptor)
        {
            descriptor = family switch
            {
                RomInfo.GameFamilies.DP => new TrainerScriptDescriptor(family, 1040, 850),
                RomInfo.GameFamilies.Plat => new TrainerScriptDescriptor(family, 1114, 928),
                RomInfo.GameFamilies.HGSS => new TrainerScriptDescriptor(family, 953, 739),
                _ => null
            };
            return descriptor != null;
        }
    }
}
