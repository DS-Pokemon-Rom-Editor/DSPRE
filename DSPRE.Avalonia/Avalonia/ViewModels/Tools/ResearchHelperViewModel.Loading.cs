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
        public async Task LoadAllDataAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            DataLoaded = false;
            StatusText = "Loading data";

            ScriptStats.Clear();
            LevelScriptStats.Clear();
            _cachedScriptFiles.Clear();
            _cachedLevelScriptFiles.Clear();
            _cachedEventFiles.Clear();
            _scriptFileById.Clear();

            try
            {
                (List<ScriptFile> scripts, List<LevelScriptFile> levelScripts, List<EventFile> events) = await Task.Run(() => ReadEverything());

                _cachedScriptFiles = scripts;
                _cachedLevelScriptFiles = levelScripts;
                _cachedEventFiles = events;
                FillScriptStats();
                FillLevelScriptStats();
                FillPickers();

                DataLoaded = true;
                StatusText = $"{_cachedScriptFiles.Count} script files, {_cachedLevelScriptFiles.Count} level scripts, {_cachedEventFiles.Count} event files";
            }
            catch (Exception ex)
            {
                StatusText = $"Couldn't load data: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static (List<ScriptFile>, List<LevelScriptFile>, List<EventFile>) ReadEverything()
        {
            List<ScriptFile> sc = new List<ScriptFile>();
            List<LevelScriptFile> ls = new List<LevelScriptFile>();
            List<EventFile> ev = new List<EventFile>();

            int scriptCount = Filesystem.GetScriptCount();
            for (int i = 0; i < scriptCount; i++)
            {
                try
                {
                    ScriptFile sf = new ScriptFile(i, readFunctions: true, readActions: true);
                    if (sf.IsLevelScript)
                    {
                        try { ls.Add(new LevelScriptFile(i)); } catch (Exception caught) when (AppLogger.Noted("ResearchHelperViewModel.LoadAllDataAsync", caught)) { }   // a level script that can't be read is left out
                    }
                    else
                    {
                        sc.Add(sf);
                    }
                }
                catch (Exception caught) when (AppLogger.Noted("ResearchHelperViewModel.LoadAllDataAsync.2", caught)) { }   // a script file that can't be read is left out
            }

            int eventCount = Filesystem.GetEventFileCount();
            for (int i = 0; i < eventCount; i++)
            {
                try { ev.Add(new EventFile(i)); } catch (Exception caught) when (AppLogger.Noted("ResearchHelperViewModel.LoadAllDataAsync.3", caught)) { }   // an event file that can't be read is left out
            }

            return (sc, ls, ev);
        }

        private void FillScriptStats()
        {
            foreach (ScriptFile sf in _cachedScriptFiles)
            {
                int total = (sf.AllScripts?.Count ?? 0) + (sf.AllFunctions?.Count ?? 0) + (sf.AllActions?.Count ?? 0);
                ScriptStats.Add(new ScriptFileStats
                {
                    ID = sf.FileID,
                    Total = total,
                    Scripts = sf.AllScripts?.Count ?? 0,
                    Functions = sf.AllFunctions?.Count ?? 0,
                    Actions = sf.AllActions?.Count ?? 0,
                    Source = HgEngineOwnedFiles
                        .Get(HgEngineOwnedFiles.ArchiveOf(DirNames.Scripts), sf.FileID)?.RelPath ?? "",
                });
                _scriptFileById[sf.FileID] = sf;
            }
        }

        private void FillLevelScriptStats()
        {
            foreach (LevelScriptFile ls in _cachedLevelScriptFiles)
            {
                int mapChange = 0, screenReset = 0, loadGame = 0, varValue = 0;
                if (ls.BufferSet != null)
                {
                    foreach (LevelScriptTrigger t in ls.BufferSet)
                    {
                        switch (t.TriggerType)
                        {
                            case LevelScriptTrigger.MAPCHANGE:   mapChange++;  break;
                            case LevelScriptTrigger.SCREENRESET: screenReset++; break;
                            case LevelScriptTrigger.LOADGAME:    loadGame++;   break;
                            case LevelScriptTrigger.VARIABLEVALUE: varValue++; break;
                        }
                    }
                }
                LevelScriptStats.Add(new LevelScriptFileStats
                {
                    ID = ls.ID,
                    Total = ls.BufferSet?.Count ?? 0,
                    MapChange = mapChange,
                    ScreenReset = screenReset,
                    LoadGame = loadGame,
                    VariableValue = varValue
                });
            }
        }

        private void FillPickers()
        {
            ScriptFileEntries.Clear();
            foreach (ScriptFile sf in _cachedScriptFiles)
                ScriptFileEntries.Add($"Script file {sf.FileID}");

            if (ScriptFileEntries.Count > 0)
                SelectedScriptFileIndex = 0;

            TrainerNamesList.Clear();
            foreach (string name in DSPRE.TrainerNames.GetAll()) TrainerNamesList.Add(name);
            if (TrainerNamesList.Count > 0) SelectedTrainerIndex = 0;

            ClassNamesList.Clear();
            string[] classNames = RomInfo.GetTrainerClassNames();
            for (int i = 0; i < classNames.Length; i++) ClassNamesList.Add($"[{i:D3}] {classNames[i]}");
            if (ClassNamesList.Count > 0) SelectedClassIndex = 0;
        }
    }
}
