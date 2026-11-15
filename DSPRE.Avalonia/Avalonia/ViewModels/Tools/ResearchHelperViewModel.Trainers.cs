using Avalonia.Controls;
using DSPRE.HgEngine;
using DSPRE.Avalonia;
using DSPRE.Editors.Utils;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Tools
{
    public partial class ResearchHelperViewModel
    {
        public void SearchOverworldEntryUsage()
        {
            if (!DataLoaded) { StatusText = "Data not loaded yet."; return; }

            int owEntryId = OwEntryIdSearch;
            OwWatcherResults.Clear();
            StatusText = $"Searching for overworld {owEntryId}…";

            foreach (EventFile eventFile in _cachedEventFiles)
            {
                if (eventFile.Overworlds == null) continue;

                for (int i = 0; i < eventFile.Overworlds.Count; i++)
                {
                    Overworld ow = eventFile.Overworlds[i];
                    if (ow.OverlayTableEntry != owEntryId) continue;

                    OwWatcherResults.Add(new OwEntryUsageResult
                    {
                        EventFileID = eventFile.ID,
                        OwIndex = i,
                        OwID = ow.OwID,
                        Position = $"Map ({ow.XMapPosition}, {ow.YMapPosition}) / Matrix ({ow.XMatrixPosition}, {ow.YMatrixPosition})",
                        Movement = ow.Movement,
                        ScriptNumber = ow.ScriptNumber
                    });
                }
            }

            StatusText = $"Overworld {owEntryId}: {OwWatcherResults.Count} uses";
        }

        public void ClearOwResults() { OwWatcherResults.Clear(); OwEntryIdSearch = 0; StatusText = "Overworld watcher search cleared"; }

        public void NavigateToOwResult(OwEntryUsageResult result)
        {
            if (result == null) return;
            AvaloniaEditorLauncher.OpenEventEditorWithOverworld(result.EventFileID, result.OwIndex);
        }

        public void SearchTrainerUsage()
        {
            if (!DataLoaded) { StatusText = "Data not loaded yet."; return; }
            if (SelectedTrainerIndex < 0) { StatusText = "Select a trainer."; return; }

            int trainerId = SelectedTrainerIndex;
            TrainerWatcherResults.Clear();
            StatusText = $"Searching for uses of trainer {trainerId}…";

            Dictionary<ushort, ScriptCommandInfo> commandInfoDict = RomInfo.GetScriptCommandInfoDict();
            foreach (ScriptFile scriptFile in _cachedScriptFiles)
            {
                ScanContainersForTrainerParameter(scriptFile.AllScripts, commandInfoDict, trainerId, scriptFile.FileID, "Script command");
                ScanContainersForTrainerParameter(scriptFile.AllFunctions, commandInfoDict, trainerId, scriptFile.FileID, "Function command");
            }

            AddTrainerOverworlds(trainerId);
            AddVsSeekerRematches(trainerId);
            AddPokegearRematches(trainerId);

            // The battle-message table and the phone book, from the same scanner trainer removal uses.
            if (TrainerReferenceScanner.TryFindTableReferences(trainerId, out List<TrainerReference> tables, out string tableError))
                foreach (TrainerReference reference in tables) TrainerWatcherResults.Add(TrainerUsageResult.From(reference));
            else { StatusText = tableError; return; }

            StatusText = $"Found {Plural.Of(TrainerWatcherResults.Count, "use")} of trainer {trainerId}";
        }

        private void AddTrainerOverworlds(int trainerId)
        {
            foreach (EventFile eventFile in _cachedEventFiles)
            {
                if (eventFile.Overworlds == null) continue;

                for (int i = 0; i < eventFile.Overworlds.Count; i++)
                {
                    Overworld ow = eventFile.Overworlds[i];
                    if (OverworldEventTypes.Find(RomInfo.GameFamily, ow.Type)?.IsTrainer != true) continue;

                    if (TrainerScripts.TrainerIdFor(ow.ScriptNumber) != trainerId) continue;
                    bool isPartner = TrainerScripts.IsDouble(ow.ScriptNumber);

                    TrainerWatcherResults.Add(new TrainerUsageResult
                    {
                        SourceId = eventFile.ID,
                        Type = isPartner ? "Overworld (partner)" : "Overworld",
                        Index = i,
                        Details = ow.ToString()
                    });
                }
            }
        }

        private void AddVsSeekerRematches(int trainerId)
        {
            if (VsSeekerRematchTable.IsSupported)
            {
                List<RematchTable.Row> rows = VsSeekerRematchTable.ReadAll();
                for (int r = 0; r < rows.Count; r++)
                {
                    RematchTable.Row row = rows[r];
                    if (row.BaseTrainerId == trainerId)
                    {
                        TrainerWatcherResults.Add(new TrainerUsageResult
                        {
                            SourceId = -1,
                            Type = "Vs. Seeker encounter",
                            Index = r,
                            Details = $"Row {r}: owns this rematch chain"
                        });
                    }
                    for (int s = 0; s < RematchTable.RematchLevelCount; s++)
                    {
                        if (row.Rematch(s) != trainerId) continue;

                        TrainerWatcherResults.Add(new TrainerUsageResult
                        {
                            SourceId = -1,
                            Type = $"Vs. Seeker rematch {(char)('A' + s)}",
                            Index = r,
                            Details = $"Row {r}: rematch {(char)('A' + s)} for encounter trainer {row.BaseTrainerId}"
                        });
                    }
                }
            }
        }

        private void AddPokegearRematches(int trainerId)
        {
            if (PokegearRematchTable.IsSupported)
            {
                List<RematchTable.Row> rows = PokegearRematchTable.ReadAll();
                for (int r = 0; r < rows.Count; r++)
                {
                    RematchTable.Row row = rows[r];
                    if (row.BaseTrainerId == trainerId)
                    {
                        TrainerWatcherResults.Add(new TrainerUsageResult
                        {
                            SourceId = -1,
                            Type = "Pokégear rematch base",
                            Index = r,
                            Details = $"Row {r}: owns this rematch chain"
                        });
                    }
                    for (int s = 0; s < RematchTable.RematchLevelCount; s++)
                    {
                        if (row.Rematch(s) != trainerId) continue;

                        TrainerWatcherResults.Add(new TrainerUsageResult
                        {
                            SourceId = -1,
                            Type = $"Pokégear rematch {s + 1}",
                            Index = r,
                            Details = $"Row {r}: rematch {s + 1} for base trainer {row.BaseTrainerId}"
                        });
                    }
                }
            }
        }

        public void SearchClassUsage()
        {
            if (!DataLoaded) { StatusText = "Data not loaded yet."; return; }
            if (SelectedClassIndex < 0) { StatusText = "Select a class."; return; }

            int classId = SelectedClassIndex;
            ClassWatcherResults.Clear();
            if (!TrainerClassDatasetManager.TryFindClassUses(classId, out List<TrainerReference> uses, out string error))
            {
                StatusText = error;
                return;
            }
            foreach (TrainerReference reference in uses) ClassWatcherResults.Add(TrainerUsageResult.From(reference));
            StatusText = $"Found {Plural.Of(ClassWatcherResults.Count, "use")} of class {classId}";
        }

        public void ClearClassResults() { ClassWatcherResults.Clear(); StatusText = "Class watcher search cleared"; }

        public void ClearTrainerResults() { TrainerWatcherResults.Clear(); StatusText = "Trainer watcher search cleared"; }

        private void ScanContainersForTrainerParameter(List<ScriptCommandContainer> containers,
            Dictionary<ushort, ScriptCommandInfo> commandInfoDict, int trainerId, int sourceFileId, string typeLabel)
        {
            if (containers == null) return;

            foreach (ScriptCommandContainer container in containers)
            {
                if (container.Commands == null) continue;

                foreach (ScriptCommand cmd in container.Commands)
                {
                    if (cmd.Id == null || cmd.CmdParams == null) continue;
                    if (!commandInfoDict.TryGetValue(cmd.Id.Value, out ScriptCommandInfo cmdInfo) || cmdInfo.ParameterTypes == null) continue;

                    for (int i = 0; i < cmd.CmdParams.Count && i < cmdInfo.ParameterTypes.Count; i++)
                    {
                        if (cmdInfo.ParameterTypes[i] != ScriptParameter.ParameterType.Trainer) continue;
                        if (ScriptCommand.ParamValue(cmd.CmdParams[i]) != trainerId) continue;

                        TrainerWatcherResults.Add(new TrainerUsageResult
                        {
                            SourceId = sourceFileId,
                            Type = typeLabel,
                            Index = (int)container.ManualUserID,
                            Details = $"{cmdInfo.Name} (param {i})"
                        });
                    }
                }
            }
        }

        public void NavigateToTrainerResult(TrainerUsageResult result)
        {
            if (result == null) return;

            if (result.Type.StartsWith("Vs. Seeker"))
            {
                AvaloniaEditorLauncher.OpenVsSeekerRematchEditor(result.Index);
                return;
            }

            if (result.Type == "Trainer")
            {
                AvaloniaEditorLauncher.OpenTrainerEditor(result.Index);
                return;
            }

            if (result.Type == "Pokégear phone book")
            {
                AvaloniaEditorLauncher.OpenPokegearPhoneBook(result.Index);
                return;
            }

            if (result.Type == "Battle Tower")
            {
                AvaloniaEditorLauncher.OpenBattleTowerEditor();
                return;
            }

            if (result.Type == "Event")
            {
                AvaloniaEditorLauncher.OpenEventEditorWithOverworld(result.SourceId, result.Index);
                return;
            }

            if (result.Type.StartsWith("Script"))
            {
                AvaloniaEditorLauncher.OpenScriptEditor(result.SourceId, result.InFunction ? 0 : result.Index);
                return;
            }

            if (result.Type.StartsWith("Pokégear Rematch", StringComparison.OrdinalIgnoreCase))
            {
                AvaloniaEditorLauncher.OpenPokegearRematchEditor(result.Index);
                return;
            }

            if (result.Type.StartsWith("Overworld"))
            {
                AvaloniaEditorLauncher.OpenEventEditorWithOverworld(result.SourceId, result.Index);
            }
            else if (result.Type == "Script command" || result.Type == "Function command")
            {
                // A function has no jump-table slot to land on, so only the file is opened.
                int scriptNumber = result.Type == "Script command" ? result.Index : 0;
                AvaloniaEditorLauncher.OpenScriptEditor(result.SourceId, scriptNumber);
            }
        }
    }
}
