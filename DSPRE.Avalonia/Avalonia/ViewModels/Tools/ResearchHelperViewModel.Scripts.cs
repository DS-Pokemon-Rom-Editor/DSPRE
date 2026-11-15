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
        public void SearchVariableUsage()
        {
            if (!DataLoaded) { StatusText = "Data not loaded yet."; return; }

            string text = VarSearchText.Trim();
            if (string.IsNullOrEmpty(text)) { StatusText = "Enter a variable number."; return; }

            if (!TryParseNumber(text, VarHexMode, out int varNum))
            {
                StatusText = "Invalid number format.";
                return;
            }

            VariableResults.Clear();
            StatusText = $"Searching for variable 0x{varNum:X}...";

            Dictionary<ushort, ScriptCommandInfo> commandInfoDict = RomInfo.GetScriptCommandInfoDict();

            foreach (ScriptFile sf in _cachedScriptFiles)
            {
                int count = CountVariableInScriptFile(sf, varNum, commandInfoDict);
                if (count > 0) VariableResults.Add(new VariableUsageResult { FileType = "Script", FileID = sf.FileID, UsageCount = count });
            }

            foreach (LevelScriptFile ls in _cachedLevelScriptFiles)
            {
                int count = CountVariableInLevelScript(ls, varNum);
                if (count > 0) VariableResults.Add(new VariableUsageResult { FileType = "Level script", FileID = ls.ID, UsageCount = count });
            }

            foreach (EventFile ev in _cachedEventFiles)
            {
                int count = CountVariableInEventFile(ev, varNum);
                if (count > 0) VariableResults.Add(new VariableUsageResult { FileType = "Event", FileID = ev.ID, UsageCount = count });
            }

            StatusText = $"Found {VariableResults.Count} files using variable 0x{varNum:X}";
        }

        public void ClearVariableResults() { VariableResults.Clear(); VarSearchText = ""; StatusText = "Variable search cleared"; }

        public void SearchFlagUsage()
        {
            if (!DataLoaded) { StatusText = "Data not loaded yet."; return; }

            string text = FlagSearchText.Trim();
            if (string.IsNullOrEmpty(text)) { StatusText = "Enter a flag number."; return; }

            if (!TryParseNumber(text, FlagHexMode, out int flagNum))
            {
                StatusText = "Invalid number format.";
                return;
            }

            FlagResults.Clear();
            StatusText = $"Searching for flag 0x{flagNum:X}...";

            foreach (EventFile ev in _cachedEventFiles)
            {
                if (ev.Overworlds == null) continue;
                for (int i = 0; i < ev.Overworlds.Count; i++)
                {
                    Overworld ow = ev.Overworlds[i];
                    if (ow.Flag == flagNum)
                        FlagResults.Add(new FlagUsageResult
                        {
                            FileType = "Event",
                            FileID = ev.ID,
                            Details = $"Overworld {i}: {ow}",
                            UsageCount = 1,
                            EventIndex = i
                        });
                }
            }

            Dictionary<ushort, ScriptCommandInfo> commandInfoDict = RomInfo.GetScriptCommandInfoDict();
            foreach (ScriptFile sf in _cachedScriptFiles)
            {
                int count = CountFlagInScriptFile(sf, flagNum, commandInfoDict);
                if (count > 0)
                    FlagResults.Add(new FlagUsageResult
                    {
                        FileType = "Script",
                        FileID = sf.FileID,
                        Details = Plural.Of(count, "flag operation"),
                        UsageCount = count,
                        EventIndex = -1
                    });
            }

            StatusText = $"Found {FlagResults.Count} results for flag 0x{flagNum:X}";
        }

        public void ClearFlagResults() { FlagResults.Clear(); FlagSearchText = ""; StatusText = "Flag search cleared"; }

        public void SearchScriptFileReferences()
        {
            int id = FileWatcherScriptFileId;
            FileWatcherResults.Clear();
            StatusText = $"Searching for references to script file {id}...";

            int headerCount = RomInfo.GetHeaderCount();
            for (ushort i = 0; i < headerCount; i++)
            {
                try
                {
                    MapHeader header = MapHeader.GetMapHeader(i);
                    if (header == null) continue;
                    if (header.ScriptFileID == id)
                        FileWatcherResults.Add(new ScriptFileReferenceResult { ReferenceType = "Header", ReferenceID = i, Field = "scriptFileID" });
                    if (header.LevelScriptID == id)
                        FileWatcherResults.Add(new ScriptFileReferenceResult { ReferenceType = "Header", ReferenceID = i, Field = "levelScriptID" });
                }
                catch (Exception caught) when (AppLogger.Noted("ResearchHelperViewModel.SearchScriptFileReferences", caught)) { }   // a header that can't be read is skipped
            }

            StatusText = $"Found {FileWatcherResults.Count} references to script file {id}";
        }

        private void RefreshScriptIdEntries()
        {
            ScriptIdEntries.Clear();
            SelectedScriptIdIndex = -1;

            int idx = SelectedScriptFileIndex;
            if (idx < 0 || idx >= _cachedScriptFiles.Count) return;

            ScriptFile sf = _cachedScriptFiles[idx];
            int count = sf.AllScripts?.Count ?? 0;
            // Position is the script number, so 0 lists the events that run nothing.
            ScriptIdEntries.Add("No script");
            for (int i = 0; i < count; i++)
                ScriptIdEntries.Add($"Script {i + 1}");

            SelectedScriptIdIndex = count > 0 ? 1 : 0;
        }

        public void SearchScriptIdUsage()
        {
            if (SelectedScriptFileIndex < 0 || SelectedScriptIdIndex < 0)
            { StatusText = "Select a script file and script ID."; return; }

            ScriptFile sf = _cachedScriptFiles[SelectedScriptFileIndex];
            int scriptId = SelectedScriptIdIndex;
            string label = ScriptIdEntries[scriptId];

            ScriptIdResults.Clear();
            StatusText = $"Searching script file {sf.FileID} for {label}…";

            HashSet<int> assocEventIds = new HashSet<int>();
            int headerCount = RomInfo.GetHeaderCount();
            for (ushort i = 0; i < headerCount; i++)
            {
                try
                {
                    MapHeader h = MapHeader.GetMapHeader(i);
                    if (h != null && h.ScriptFileID == sf.FileID)
                        assocEventIds.Add(h.EventFileID);
                }
                catch (Exception caught) when (AppLogger.Noted("ResearchHelperViewModel.SearchScriptIdUsage", caught)) { }   // a header that can't be read is skipped
            }

            foreach (EventFile ev in _cachedEventFiles)
            {
                if (!assocEventIds.Contains(ev.ID)) continue;

                if (ev.Overworlds != null)
                    for (int i = 0; i < ev.Overworlds.Count; i++)
                    {
                        Overworld ow = ev.Overworlds[i];
                        if (ow.ScriptNumber == scriptId)
                            ScriptIdResults.Add(new ScriptIdUsageResult { EventFileID = ev.ID, EventType = "Overworld", EventIndex = i, Details = ow.ToString() });
                    }

                if (ev.Spawnables != null)
                    for (int i = 0; i < ev.Spawnables.Count; i++)
                    {
                        Spawnable sp = ev.Spawnables[i];
                        if (sp.ScriptNumber == scriptId)
                            ScriptIdResults.Add(new ScriptIdUsageResult { EventFileID = ev.ID, EventType = "Spawnable", EventIndex = i, Details = sp.ToString() });
                    }

                if (ev.Triggers != null)
                    for (int i = 0; i < ev.Triggers.Count; i++)
                    {
                        Trigger tr = ev.Triggers[i];
                        if (tr.ScriptNumber == scriptId)
                            ScriptIdResults.Add(new ScriptIdUsageResult { EventFileID = ev.ID, EventType = "Trigger", EventIndex = i, Details = tr.ToString() });
                    }
            }

            StatusText = $"Found {ScriptIdResults.Count} uses of {label}";
        }
    }
}
