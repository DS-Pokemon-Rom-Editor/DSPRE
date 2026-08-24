using DSPRE.Editors.Utils;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DSPRE
{
    public partial class ResearchHelper : Form
    {
        private List<ScriptFileStats> allScriptStats = new List<ScriptFileStats>();
        private List<LevelScriptFileStats> allLevelScriptStats = new List<LevelScriptFileStats>();
        private List<VariableUsageResult> allVariableUsageResults = new List<VariableUsageResult>();

        private List<ScriptFile> cachedScriptFiles = new List<ScriptFile>();
        private List<LevelScriptFile> cachedLevelScriptFiles = new List<LevelScriptFile>();
        private List<EventFile> cachedEventFiles = new List<EventFile>();
        private bool dataLoaded = false;

        public ResearchHelper()
        {
            InitializeComponent();
            mainTabControl.SelectedIndexChanged += MainTabControl_SelectedIndexChanged;
        }

        private void ResearchHelper_Load(object sender, EventArgs e)
        {
            LoadAllData();
        }

        private void MainTabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        #region Unified Data Loading

        private void LoadAllData()
        {
            statusLabel.Text = "Loading all data...";
            scriptsDataGridView.Rows.Clear();
            levelScriptsDataGridView.Rows.Clear();
            allScriptStats.Clear();
            allLevelScriptStats.Clear();
            cachedScriptFiles.Clear();
            cachedLevelScriptFiles.Clear();
            cachedEventFiles.Clear();
            dataLoaded = false;

            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { 
                RomInfo.DirNames.scripts, 
                RomInfo.DirNames.eventFiles 
            });

            int scriptCount = Filesystem.GetScriptCount();
            int eventCount = Filesystem.GetEventFileCount();
            int totalFiles = scriptCount + eventCount;

            using (LoadingForm loadingForm = new LoadingForm(totalFiles, "Loading Research Helper Data..."))
            {
                loadingForm.Show();
                Application.DoEvents();

                int progress = 0;

                for (int i = 0; i < scriptCount; i++)
                {
                    try
                    {
                        ScriptFile scriptFile = new ScriptFile(i, readFunctions: true, readActions: true);

                        if (scriptFile.isLevelScript)
                        {
                            try
                            {
                                LevelScriptFile levelScript = new LevelScriptFile(i);
                                cachedLevelScriptFiles.Add(levelScript);

                                int totalEntries = levelScript.bufferSet?.Count ?? 0;
                                int mapChangeCount = 0;
                                int screenResetCount = 0;
                                int loadGameCount = 0;
                                int variableValueCount = 0;

                                if (levelScript.bufferSet != null)
                                {
                                    foreach (var trigger in levelScript.bufferSet)
                                    {
                                        switch (trigger.triggerType)
                                        {
                                            case LevelScriptTrigger.MAPCHANGE:
                                                mapChangeCount++;
                                                break;
                                            case LevelScriptTrigger.SCREENRESET:
                                                screenResetCount++;
                                                break;
                                            case LevelScriptTrigger.LOADGAME:
                                                loadGameCount++;
                                                break;
                                            case LevelScriptTrigger.VARIABLEVALUE:
                                                variableValueCount++;
                                                break;
                                        }
                                    }
                                }

                                allLevelScriptStats.Add(new LevelScriptFileStats
                                {
                                    ID = i,
                                    Total = totalEntries,
                                    MapChange = mapChangeCount,
                                    ScreenReset = screenResetCount,
                                    LoadGame = loadGameCount,
                                    VariableValue = variableValueCount
                                });
                            }
                            catch (Exception ex)
                            {
                                AppLogger.Warn($"Failed to load level script file {i}: {ex.Message}");
                            }
                        }
                        else
                        {
                            cachedScriptFiles.Add(scriptFile);

                            int scriptsCount = scriptFile.allScripts?.Count ?? 0;
                            int functionsCount = scriptFile.allFunctions?.Count ?? 0;
                            int actionsCount = scriptFile.allActions?.Count ?? 0;
                            int total = scriptsCount + functionsCount + actionsCount;

                            allScriptStats.Add(new ScriptFileStats
                            {
                                ID = i,
                                Total = total,
                                Scripts = scriptsCount,
                                Functions = functionsCount,
                                Actions = actionsCount
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Warn($"Failed to load script file {i}: {ex.Message}");
                    }

                    progress++;
                    loadingForm.UpdateStatusAndProgress(progress, $"Loading scripts... ({i + 1}/{scriptCount})");
                    Application.DoEvents();
                }

                for (int i = 0; i < eventCount; i++)
                {
                    try
                    {
                        EventFile eventFile = new EventFile(i);
                        cachedEventFiles.Add(eventFile);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Warn($"Failed to load event file {i}: {ex.Message}");
                    }

                    progress++;
                    loadingForm.UpdateStatusAndProgress(progress, $"Loading events... ({i + 1}/{eventCount})");
                    Application.DoEvents();
                }

                loadingForm.Close();
            }

            dataLoaded = true;

            PopulateScriptsDataGridView(allScriptStats);
            PopulateLevelScriptsDataGridView(allLevelScriptStats);

            PopulateIdWatcherScriptFileComboBox();

            PopulateTrainerWatchComboBox();

            statusLabel.Text = $"Ready - {allScriptStats.Count} scripts, {allLevelScriptStats.Count} level scripts, {cachedEventFiles.Count} events loaded";
        }

        #endregion

        #region Scripts Tab

        private void PopulateScriptsDataGridView(IList<ScriptFileStats> stats)
        {
            scriptsDataGridView.Rows.Clear();

            foreach (var stat in stats)
            {
                scriptsDataGridView.Rows.Add(stat.ID, stat.Total, stat.Scripts, stat.Functions, stat.Actions);
            }
        }

        #region Level Scripts Tab

        private void PopulateLevelScriptsDataGridView(IList<LevelScriptFileStats> stats)
        {
            levelScriptsDataGridView.Rows.Clear();

            foreach (var stat in stats)
            {
                levelScriptsDataGridView.Rows.Add(stat.ID, stat.Total, stat.MapChange, stat.ScreenReset, stat.LoadGame, stat.VariableValue);
            }
        }

            private void lsSearchButton_Click(object sender, EventArgs e)
            {
                ApplyLevelScriptFilter();
            }

            private void lsClearSearchButton_Click(object sender, EventArgs e)
            {
                lsSearchValueNumericUpDown.Value = 0;
                PopulateLevelScriptsDataGridView(allLevelScriptStats);
                statusLabel.Text = $"Filter cleared - {allLevelScriptStats.Count} level script files shown";
            }

            private void ApplyLevelScriptFilter()
            {
                int searchValue = (int)lsSearchValueNumericUpDown.Value;
                List<LevelScriptFileStats> filteredStats = new List<LevelScriptFileStats>();

                Func<LevelScriptFileStats, int> columnSelector;
                string columnName;
                if (lsIdRadioButton.Checked)
                {
                    columnSelector = s => s.ID;
                    columnName = "ID";
                }
                else if (lsTotalRadioButton.Checked)
                {
                    columnSelector = s => s.Total;
                    columnName = "Total";
                }
                else if (lsMapChangeRadioButton.Checked)
                {
                    columnSelector = s => s.MapChange;
                    columnName = "Map Change";
                }
                else if (lsScreenResetRadioButton.Checked)
                {
                    columnSelector = s => s.ScreenReset;
                    columnName = "Screen Reset";
                }
                else if (lsLoadGameRadioButton.Checked)
                {
                    columnSelector = s => s.LoadGame;
                    columnName = "Load Game";
                }
                else // lsVariableValueRadioButton.Checked
                {
                    columnSelector = s => s.VariableValue;
                    columnName = "Variable Value";
                }

                Func<int, int, bool> comparison;
                string comparisonName;
                if (lsEqualsRadioButton.Checked)
                {
                    comparison = (value, target) => value == target;
                    comparisonName = "=";
                }
                else if (lsGreaterThanOrEqualRadioButton.Checked)
                {
                    comparison = (value, target) => value >= target;
                    comparisonName = ">=";
                }
                else // lsLessThanOrEqualRadioButton.Checked
                {
                    comparison = (value, target) => value <= target;
                    comparisonName = "<=";
                }

                foreach (var stat in allLevelScriptStats)
                {
                    int columnValue = columnSelector(stat);
                    if (comparison(columnValue, searchValue))
                    {
                        filteredStats.Add(stat);
                    }
                }

                PopulateLevelScriptsDataGridView(filteredStats);
                statusLabel.Text = $"Filter applied: {columnName} {comparisonName} {searchValue} - {filteredStats.Count} results";
            }

            private void lsRefreshButton_Click(object sender, EventArgs e)
            {
                LoadAllData();
            }

            private void levelScriptsDataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0)
                    return;

                int levelScriptId = (int)levelScriptsDataGridView.Rows[e.RowIndex].Cells[0].Value;

                if (Control.ModifierKeys.HasFlag(Keys.Shift))
                {
                    var mainProgram = Application.OpenForms["MainProgram"] as MainProgram;
                    if (mainProgram != null)
                    {
                        EditorPanels.levelScriptEditor.OpenLevelScriptEditor(mainProgram, levelScriptId);
                        statusLabel.Text = $"Opened Level Script {levelScriptId} in Level Script Editor";
                    }
                    else
                    {
                        statusLabel.Text = "Could not find main window to open Level Script Editor";
                    }
                }
                else
                {
                    Clipboard.SetText(levelScriptId.ToString());
                    statusLabel.Text = $"Level Script ID {levelScriptId} copied to clipboard (Shift+double-click to open in editor)";
                }
            }

            #endregion

        private void searchButton_Click(object sender, EventArgs e)
        {
            ApplyFilter();
        }

        private void clearSearchButton_Click(object sender, EventArgs e)
        {
            searchValueNumericUpDown.Value = 0;
            PopulateScriptsDataGridView(allScriptStats);
            statusLabel.Text = $"Filter cleared - {allScriptStats.Count} script files shown";
        }

        private void ApplyFilter()
        {
            int searchValue = (int)searchValueNumericUpDown.Value;
            List<ScriptFileStats> filteredStats = new List<ScriptFileStats>();

            Func<ScriptFileStats, int> columnSelector;
            string columnName;
            if (idRadioButton.Checked)
            {
                columnSelector = s => s.ID;
                columnName = "ID";
            }
            else if (totalRadioButton.Checked)
            {
                columnSelector = s => s.Total;
                columnName = "Total";
            }
            else if (scriptsRadioButton.Checked)
            {
                columnSelector = s => s.Scripts;
                columnName = "Scripts";
            }
            else if (functionsRadioButton.Checked)
            {
                columnSelector = s => s.Functions;
                columnName = "Functions";
            }
            else // actionsRadioButton.Checked
            {
                columnSelector = s => s.Actions;
                columnName = "Actions";
            }

            Func<int, int, bool> comparison;
            string comparisonName;
            if (equalsRadioButton.Checked)
            {
                comparison = (value, target) => value == target;
                comparisonName = "=";
            }
            else if (greaterThanOrEqualRadioButton.Checked)
            {
                comparison = (value, target) => value >= target;
                comparisonName = ">=";
            }
            else // lessThanOrEqualRadioButton.Checked
            {
                comparison = (value, target) => value <= target;
                comparisonName = "<=";
            }

            foreach (var stat in allScriptStats)
            {
                int columnValue = columnSelector(stat);
                if (comparison(columnValue, searchValue))
                {
                    filteredStats.Add(stat);
                }
            }

            PopulateScriptsDataGridView(filteredStats);
            statusLabel.Text = $"Filter applied: {columnName} {comparisonName} {searchValue} - {filteredStats.Count} results";
        }

        private void refreshButton_Click(object sender, EventArgs e)
        {
            LoadAllData();
        }

        private void scriptsDataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            int scriptId = (int)scriptsDataGridView.Rows[e.RowIndex].Cells[0].Value;

                        if (Control.ModifierKeys.HasFlag(Keys.Shift))
                        {
                            var mainProgram = Application.OpenForms["MainProgram"] as MainProgram;
                            if (mainProgram != null)
                            {
                                EditorPanels.scriptEditor.OpenScriptEditor(mainProgram, scriptId);
                                statusLabel.Text = $"Opened Script File {scriptId} in Script Editor";
                            }
                            else
                            {
                                statusLabel.Text = "Could not find main window to open Script Editor";
                            }
                        }
                        else
                        {
                            Clipboard.SetText(scriptId.ToString());
                            statusLabel.Text = $"Script ID {scriptId} copied to clipboard (Shift+double-click to open in editor)";
                        }
                    }

                    #endregion

                    #region Variables Tab

                    private void varSearchButton_Click(object sender, EventArgs e)
                    {
                        SearchVariableUsage();
                    }

                    private void varClearButton_Click(object sender, EventArgs e)
                    {
                        varSearchTextBox.Text = "";
                        variablesDataGridView.Rows.Clear();
                        allVariableUsageResults.Clear();
                        statusLabel.Text = "Variable search cleared";
                    }

                    private void SearchVariableUsage()
                    {
                        if (!dataLoaded)
                        {
                            MessageBox.Show("Please wait for data to finish loading.", "Data Not Ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        string searchText = varSearchTextBox.Text.Trim();
                        if (string.IsNullOrEmpty(searchText))
                        {
                            MessageBox.Show("Please enter a variable number to search.", "Empty Search", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        int variableNumber;
                        bool parseSuccess;

                        if (varHexRadioButton.Checked)
                        {
                            string hexValue = searchText;
                            if (hexValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                                hexValue.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
                            {
                                hexValue = hexValue.Substring(2);
                            }
                            parseSuccess = int.TryParse(hexValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out variableNumber);
                        }
                        else
                        {
                            parseSuccess = int.TryParse(searchText, out variableNumber);
                        }

                        if (!parseSuccess)
                        {
                            MessageBox.Show("Invalid variable number format.", "Parse Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        statusLabel.Text = $"Searching for variable {variableNumber} (0x{variableNumber:X})...";
                        Application.DoEvents();

                        allVariableUsageResults.Clear();
                        variablesDataGridView.Rows.Clear();

                        foreach (var scriptFile in cachedScriptFiles)
                        {
                            int usageCount = CountVariableInScriptFile(scriptFile, variableNumber);
                            if (usageCount > 0)
                            {
                                allVariableUsageResults.Add(new VariableUsageResult
                                {
                                    FileType = "Script",
                                    FileID = scriptFile.fileID,
                                    UsageCount = usageCount
                                });
                            }
                        }

                        foreach (var levelScript in cachedLevelScriptFiles)
                        {
                            int usageCount = CountVariableInLevelScript(levelScript, variableNumber);
                            if (usageCount > 0)
                            {
                                allVariableUsageResults.Add(new VariableUsageResult
                                {
                                    FileType = "Level Script",
                                    FileID = levelScript.ID,
                                    UsageCount = usageCount
                                });
                            }
                        }

                        foreach (var eventFile in cachedEventFiles)
                        {
                            int usageCount = CountVariableInEventFile(eventFile, variableNumber);
                            if (usageCount > 0)
                            {
                                allVariableUsageResults.Add(new VariableUsageResult
                                {
                                    FileType = "Event",
                                    FileID = eventFile.ID,
                                    UsageCount = usageCount
                                });
                            }
                        }

                        PopulateVariablesDataGridView(allVariableUsageResults);
                        statusLabel.Text = $"Found {allVariableUsageResults.Count} files using variable {variableNumber} (0x{variableNumber:X})";
                    }

                    private int CountVariableInScriptFile(ScriptFile scriptFile, int variableNumber)
                    {
                        int count = 0;

                        var commandInfoDict = RomInfo.GetScriptCommandInfoDict();

                        if (scriptFile.allScripts != null)
                        {
                            foreach (var script in scriptFile.allScripts)
                            {
                                count += CountVariableInCommands(script.commands, variableNumber, commandInfoDict);
                            }
                        }

                        if (scriptFile.allFunctions != null)
                        {
                            foreach (var func in scriptFile.allFunctions)
                            {
                                count += CountVariableInCommands(func.commands, variableNumber, commandInfoDict);
                            }
                        }

                        return count;
                    }

                    private int CountVariableInCommands(List<ScriptCommand> commands, int variableNumber, Dictionary<ushort, ScriptCommandInfo> commandInfoDict)
                    {
                        int count = 0;

                        if (commands == null) return 0;

                        foreach (var cmd in commands)
                        {
                            if (cmd.id == null || cmd.cmdParams == null) continue;

                            ScriptCommandInfo cmdInfo = null;
                            commandInfoDict?.TryGetValue(cmd.id.Value, out cmdInfo);

                            var paramTypes = cmdInfo?.ParameterTypes;

                            for (int i = 0; i < cmd.cmdParams.Count; i++)
                            {
                                byte[] paramData = cmd.cmdParams[i];

                                ScriptParameter.ParameterType paramType = ScriptParameter.ParameterType.Integer;
                                if (paramTypes != null && i < paramTypes.Count)
                                {
                                    paramType = paramTypes[i];
                                }

                                if (paramType == ScriptParameter.ParameterType.Variable ||
                                    paramType == ScriptParameter.ParameterType.Flex)
                                {
                                    int paramValue = GetParamValue(paramData);
                                    if (paramValue == variableNumber)
                                    {
                                        count++;
                                    }
                                }
                                else if (paramData.Length >= 2)
                                {
                                    int paramValue = GetParamValue(paramData);
                                    if (paramValue >= 0x4000 && paramValue == variableNumber)
                                    {
                                        count++;
                                    }
                                }
                            }
                        }

                        return count;
                    }

                    private int GetParamValue(byte[] data)
                    {
                        if (data == null || data.Length == 0) return 0;
                        if (data.Length == 1) return data[0];
                        if (data.Length >= 2) return BitConverter.ToUInt16(data, 0);
                        return 0;
                    }

                    private int CountVariableInLevelScript(LevelScriptFile levelScript, int variableNumber)
                    {
                        int count = 0;

                        if (levelScript.bufferSet == null) return 0;

                        foreach (var trigger in levelScript.bufferSet)
                        {
                            if (trigger is VariableValueTrigger varTrigger)
                            {
                                if (varTrigger.variableToWatch == variableNumber)
                                {
                                    count++;
                                }
                            }
                        }

                        return count;
                    }

                    private int CountVariableInEventFile(EventFile eventFile, int variableNumber)
                    {
                        int count = 0;

                        if (eventFile.triggers != null)
                        {
                            foreach (var trigger in eventFile.triggers)
                            {
                                if (trigger.variableWatched == variableNumber)
                                {
                                    count++;
                                }
                            }
                        }

                        return count;
                    }

                    private void PopulateVariablesDataGridView(IList<VariableUsageResult> results)
                    {
                        variablesDataGridView.Rows.Clear();

                        foreach (var result in results)
                        {
                            variablesDataGridView.Rows.Add(result.FileType, result.FileID, result.UsageCount);
                        }
                    }

                    private void variablesDataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
                    {
                        if (e.RowIndex < 0) return;

                        string fileType = (string)variablesDataGridView.Rows[e.RowIndex].Cells[0].Value;
                        int fileId = (int)variablesDataGridView.Rows[e.RowIndex].Cells[1].Value;

                        if (Control.ModifierKeys.HasFlag(Keys.Shift))
                        {
                            var mainProgram = Application.OpenForms["MainProgram"] as MainProgram;
                            if (mainProgram != null)
                            {
                                switch (fileType)
                                {
                                    case "Script":
                                        EditorPanels.scriptEditor.OpenScriptEditor(mainProgram, fileId);
                                        statusLabel.Text = $"Opened Script File {fileId} in Script Editor";
                                        break;
                                    case "Level Script":
                                        EditorPanels.levelScriptEditor.OpenLevelScriptEditor(mainProgram, fileId);
                                        statusLabel.Text = $"Opened Level Script {fileId} in Level Script Editor";
                                        break;
                                    case "Event":
                                        EditorPanels.eventEditor.OpenEventEditor(mainProgram, fileId);
                                        statusLabel.Text = $"Opened Event File {fileId} in Event Editor";
                                        break;
                                }
                            }
                            else
                            {
                                statusLabel.Text = "Could not find main window to open editor";
                            }
                        }
                        else
                        {
                            Clipboard.SetText($"{fileType} {fileId}");
                            statusLabel.Text = $"{fileType} {fileId} copied to clipboard (Shift+double-click to open in editor)";
                        }
                    }

                    #endregion

                    #region Data Classes

                    /// <summary>
                    /// Data class to hold script file statistics
                    /// </summary>
                    private class ScriptFileStats
                    {
                        public int ID { get; set; }
                        public int Total { get; set; }
                        public int Scripts { get; set; }
                        public int Functions { get; set; }
                        public int Actions { get; set; }
                    }

                    /// <summary>
                    /// Data class to hold level script file statistics
                    /// </summary>
                    private class LevelScriptFileStats
                    {
                        public int ID { get; set; }
                        public int Total { get; set; }
                        public int MapChange { get; set; }
                        public int ScreenReset { get; set; }
                        public int LoadGame { get; set; }
                        public int VariableValue { get; set; }
                    }

                    /// <summary>
                    /// Data class to hold variable usage search results
                    /// </summary>
                    private class VariableUsageResult
                    {
                        public string FileType { get; set; }
                        public int FileID { get; set; }
                        public int UsageCount { get; set; }
                    }

                    /// <summary>
                    /// Data class to hold flag usage search results
                    /// </summary>
                    private class FlagUsageResult
                    {
                        public string FileType { get; set; }
                        public int FileID { get; set; }
                        public string Details { get; set; }
                        public int UsageCount { get; set; }
                        public int EventIndex { get; set; }  // For navigation
                    }

                    /// <summary>
                    /// Data class to hold script file reference results
                    /// </summary>
                    private class ScriptFileReferenceResult
                    {
                        public string ReferenceType { get; set; }  // Header, Event
                        public int ReferenceID { get; set; }
                        public string Field { get; set; }  // scriptFileID, levelScriptID, etc.
                    }

                    /// <summary>
                    /// Data class to hold script ID usage results
                    /// </summary>
                    private class ScriptIdUsageResult
                    {
                        public int EventFileID { get; set; }
                        public string EventType { get; set; }  // Overworld, Spawnable, Trigger
                        public int EventIndex { get; set; }
                        public string Details { get; set; }
                    }

                    #endregion

                    #region Flag Watcher Tab

                    private List<FlagUsageResult> allFlagUsageResults = new List<FlagUsageResult>();

                    private void flagSearchButton_Click(object sender, EventArgs e)
                    {
                        SearchFlagUsage();
                    }

                    private void flagClearButton_Click(object sender, EventArgs e)
                    {
                        flagSearchTextBox.Text = "";
                        flagWatcherDataGridView.Rows.Clear();
                        allFlagUsageResults.Clear();
                        statusLabel.Text = "Flag search cleared";
                    }

                    private void SearchFlagUsage()
                    {
                        if (!dataLoaded)
                        {
                            MessageBox.Show("Please wait for data to finish loading.", "Data Not Ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        string searchText = flagSearchTextBox.Text.Trim();
                        if (string.IsNullOrEmpty(searchText))
                        {
                            MessageBox.Show("Please enter a flag number to search.", "Empty Search", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        int flagNumber;
                        bool parseSuccess;

                        if (flagHexRadioButton.Checked)
                        {
                            string hexValue = searchText;
                            if (hexValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                                hexValue.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
                            {
                                hexValue = hexValue.Substring(2);
                            }
                            parseSuccess = int.TryParse(hexValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flagNumber);
                        }
                        else
                        {
                            parseSuccess = int.TryParse(searchText, out flagNumber);
                        }

                        if (!parseSuccess)
                        {
                            MessageBox.Show("Invalid flag number format.", "Parse Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        statusLabel.Text = $"Searching for flag {flagNumber} (0x{flagNumber:X})...";
                        Application.DoEvents();

                        allFlagUsageResults.Clear();
                        flagWatcherDataGridView.Rows.Clear();

                        for (int eventIndex = 0; eventIndex < cachedEventFiles.Count; eventIndex++)
                        {
                            var eventFile = cachedEventFiles[eventIndex];
                            if (eventFile.overworlds != null)
                            {
                                for (int owIndex = 0; owIndex < eventFile.overworlds.Count; owIndex++)
                                {
                                    var ow = eventFile.overworlds[owIndex];
                                    if (ow.flag == flagNumber)
                                    {
                                        allFlagUsageResults.Add(new FlagUsageResult
                                        {
                                            FileType = "Event",
                                            FileID = eventFile.ID,
                                            Details = $"Overworld {owIndex}: {ow}",
                                            UsageCount = 1,
                                            EventIndex = owIndex
                                        });
                                    }
                                }
                            }
                        }

                        foreach (var scriptFile in cachedScriptFiles)
                        {
                            int usageCount = CountFlagInScriptFile(scriptFile, flagNumber);
                            if (usageCount > 0)
                            {
                                allFlagUsageResults.Add(new FlagUsageResult
                                {
                                    FileType = "Script",
                                    FileID = scriptFile.fileID,
                                    Details = $"{usageCount} flag operation(s)",
                                    UsageCount = usageCount,
                                    EventIndex = -1
                                });
                            }
                        }

                        PopulateFlagDataGridView(allFlagUsageResults);
                        statusLabel.Text = $"Found {allFlagUsageResults.Count} results for flag {flagNumber} (0x{flagNumber:X})";
                    }

                    private int CountFlagInScriptFile(ScriptFile scriptFile, int flagNumber)
                    {
                        int count = 0;

                        if (scriptFile.allScripts != null)
                        {
                            foreach (var script in scriptFile.allScripts)
                            {
                                count += CountFlagInCommands(script.commands, flagNumber);
                            }
                        }

                        if (scriptFile.allFunctions != null)
                        {
                            foreach (var func in scriptFile.allFunctions)
                            {
                                count += CountFlagInCommands(func.commands, flagNumber);
                            }
                        }

                        return count;
                    }

                    private int CountFlagInCommands(List<ScriptCommand> commands, int flagNumber)
                    {
                        int count = 0;
                        if (commands == null) return 0;

                        foreach (var cmd in commands)
                        {
                            if (cmd.cmdParams == null) continue;

                            foreach (var paramData in cmd.cmdParams)
                            {
                                if (paramData.Length >= 2)
                                {
                                    int paramValue = GetParamValue(paramData);
                                    if (paramValue == flagNumber && paramValue < 0x4000)
                                    {
                                        count++;
                                    }
                                }
                            }
                        }

                        return count;
                    }

                    private void PopulateFlagDataGridView(IList<FlagUsageResult> results)
                    {
                        flagWatcherDataGridView.Rows.Clear();

                        foreach (var result in results)
                        {
                            flagWatcherDataGridView.Rows.Add(result.FileType, result.FileID, result.Details, result.UsageCount);
                        }
                    }

                    private void flagWatcherDataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
                    {
                        if (e.RowIndex < 0) return;

                        string fileType = (string)flagWatcherDataGridView.Rows[e.RowIndex].Cells[0].Value;
                        int fileId = (int)flagWatcherDataGridView.Rows[e.RowIndex].Cells[1].Value;

                        var mainProgram = Application.OpenForms["MainProgram"] as MainProgram;
                        if (mainProgram != null)
                        {
                            switch (fileType)
                            {
                                case "Event":
                                    var result = allFlagUsageResults.FirstOrDefault(r => 
                                        r.FileType == "Event" && r.FileID == fileId);
                                    if (result != null && result.EventIndex >= 0)
                                    {
                                        EditorPanels.eventEditor.OpenEventEditorWithOverworld(mainProgram, fileId, result.EventIndex);
                                        statusLabel.Text = $"Opened Event File {fileId}, Overworld {result.EventIndex}";
                                    }
                                    else
                                    {
                                        EditorPanels.eventEditor.OpenEventEditor(mainProgram, fileId);
                                        statusLabel.Text = $"Opened Event File {fileId}";
                                    }
                                    break;
                                case "Script":
                                    EditorPanels.scriptEditor.OpenScriptEditor(mainProgram, fileId);
                                    statusLabel.Text = $"Opened Script File {fileId}";
                                    break;
                            }
                        }
                        else
                        {
                            statusLabel.Text = "Could not find main window to open editor";
                        }
                    }

                    #endregion

                    #region Overworld Watcher Tab

                    private List<OwEntryUsageResult> owEntryUsageResults = new List<OwEntryUsageResult>();

                    /// <summary>
                    /// Data class to hold OW Entry ID usage search results
                    /// </summary>
                    private class OwEntryUsageResult
                    {
                        public int EventFileID { get; set; }
                        public int OwIndex { get; set; }
                        public int OwID { get; set; }
                        public string Position { get; set; }
                        public int Movement { get; set; }
                        public int ScriptNumber { get; set; }
                    }

                    private void owSearchButton_Click(object sender, EventArgs e)
                    {
                        SearchOverworldEntryUsage();
                    }

                    private void owClearButton_Click(object sender, EventArgs e)
                    {
                        owEntryIdNumericUpDown.Value = 0;
                        owWatcherDataGridView.Rows.Clear();
                        owEntryUsageResults.Clear();
                        statusLabel.Text = "Overworld Watcher search cleared";
                    }

                    private void SearchOverworldEntryUsage()
                    {
                        if (!dataLoaded)
                        {
                            MessageBox.Show("Please wait for data to finish loading.", "Data Not Ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        int owEntryId = (int)owEntryIdNumericUpDown.Value;

                        statusLabel.Text = $"Searching for OW Entry ID {owEntryId}...";
                        Application.DoEvents();

                        owEntryUsageResults.Clear();
                        owWatcherDataGridView.Rows.Clear();

                        foreach (var eventFile in cachedEventFiles)
                        {
                            if (eventFile.overworlds == null) continue;

                            for (int i = 0; i < eventFile.overworlds.Count; i++)
                            {
                                var ow = eventFile.overworlds[i];
                                if (ow.overlayTableEntry == owEntryId)
                                {
                                    owEntryUsageResults.Add(new OwEntryUsageResult
                                    {
                                        EventFileID = eventFile.ID,
                                        OwIndex = i,
                                        OwID = ow.owID,
                                        Position = $"Map ({ow.xMapPosition}, {ow.yMapPosition}) / Matrix ({ow.xMatrixPosition}, {ow.yMatrixPosition})",
                                        Movement = ow.movement,
                                        ScriptNumber = ow.scriptNumber
                                    });
                                }
                            }
                        }

                        PopulateOwWatcherDataGridView(owEntryUsageResults);
                        statusLabel.Text = $"Found {owEntryUsageResults.Count} overworld event(s) using OW Entry ID {owEntryId}";
                    }

                    private void PopulateOwWatcherDataGridView(IList<OwEntryUsageResult> results)
                    {
                        owWatcherDataGridView.Rows.Clear();

                        foreach (var result in results)
                        {
                            owWatcherDataGridView.Rows.Add(result.EventFileID, result.OwIndex, result.OwID, result.Position, result.Movement, result.ScriptNumber);
                        }
                    }

                    private void owWatcherDataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
                    {
                        if (e.RowIndex < 0) return;

                        int eventFileId = (int)owWatcherDataGridView.Rows[e.RowIndex].Cells[0].Value;
                        int owIndex = (int)owWatcherDataGridView.Rows[e.RowIndex].Cells[1].Value;

                        var mainProgram = Application.OpenForms["MainProgram"] as MainProgram;
                        if (mainProgram != null)
                        {
                            EditorPanels.eventEditor.OpenEventEditorWithOverworld(mainProgram, eventFileId, owIndex);
                            statusLabel.Text = $"Opened Event File {eventFileId}, Overworld {owIndex}";
                        }
                        else
                        {
                            statusLabel.Text = "Could not find main window to open editor";
                        }
                    }

                    #endregion

                    #region Trainer Watcher Tab

                    private List<TrainerUsageResult> trainerUsageResults = new List<TrainerUsageResult>();

                    /// <summary>
                    /// Data class to hold trainer usage search results
                    /// </summary>
                    private class TrainerUsageResult
                    {
                        public int SourceId { get; set; } // event file ID, script file ID, or -1 for Vs. Seeker rows
                        public string Type { get; set; }
                        public int Index { get; set; }
                        public string Details { get; set; }
                    }

                    private void PopulateTrainerWatchComboBox()
                    {
                        trainerWatchComboBox.Items.Clear();
                        trainerWatchComboBox.Items.AddRange(Helpers.GetTrainerNames());
                        if (trainerWatchComboBox.Items.Count > 0)
                        {
                            trainerWatchComboBox.SelectedIndex = 0;
                        }
                    }

                    private void trainerWatchSearchButton_Click(object sender, EventArgs e)
                    {
                        SearchTrainerUsage();
                    }

                    private void trainerWatchClearButton_Click(object sender, EventArgs e)
                    {
                        trainerWatcherDataGridView.Rows.Clear();
                        trainerUsageResults.Clear();
                        statusLabel.Text = "Trainer Watcher search cleared";
                    }

                    private void SearchTrainerUsage()
                    {
                        if (!dataLoaded)
                        {
                            MessageBox.Show("Please wait for data to finish loading.", "Data Not Ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        if (trainerWatchComboBox.SelectedIndex < 0)
                        {
                            MessageBox.Show("Please select a trainer.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        int trainerId = trainerWatchComboBox.SelectedIndex;

                        statusLabel.Text = $"Searching for Trainer {trainerId} usage...";
                        Application.DoEvents();

                        trainerUsageResults.Clear();
                        trainerWatcherDataGridView.Rows.Clear();

                        var commandInfoDict = RomInfo.GetScriptCommandInfoDict();
                        foreach (var scriptFile in cachedScriptFiles)
                        {
                            ScanContainersForTrainerParameter(scriptFile.allScripts, commandInfoDict, trainerId, scriptFile.fileID, "Script Command");
                            ScanContainersForTrainerParameter(scriptFile.allFunctions, commandInfoDict, trainerId, scriptFile.fileID, "Function Command");
                        }

                        // Mirrors EventEditor.NavigateToOverworldTarget's decode formula.
                        foreach (var eventFile in cachedEventFiles)
                        {
                            if (eventFile.overworlds == null) continue;

                            for (int i = 0; i < eventFile.overworlds.Count; i++)
                            {
                                var ow = eventFile.overworlds[i];
                                if (ow.type != (ushort)Overworld.OwType.TRAINER) continue;

                                bool isPartner = ow.scriptNumber >= 4999;
                                int decodedId = ow.scriptNumber - (isPartner ? 4999 : 2999);
                                if (decodedId > RomInfo.trainerFunnyScriptNumber - 1) decodedId--;

                                if (decodedId != trainerId) continue;

                                trainerUsageResults.Add(new TrainerUsageResult
                                {
                                    SourceId = eventFile.ID,
                                    Type = isPartner ? "Overworld (Partner)" : "Overworld",
                                    Index = i,
                                    Details = ow.ToString()
                                });
                            }
                        }

                        if (VsSeekerRematchTable.IsSupported)
                        {
                            var rows = VsSeekerRematchTable.ReadAll();
                            for (int r = 0; r < rows.Count; r++)
                            {
                                var row = rows[r];
                                if (row.EncounterTrainerId == trainerId)
                                {
                                    trainerUsageResults.Add(new TrainerUsageResult
                                    {
                                        SourceId = -1,
                                        Type = "Vs. Seeker Encounter",
                                        Index = r,
                                        Details = $"Row {r}: owns this rematch chain"
                                    });
                                }
                                for (int s = 0; s < row.RematchTrainerIds.Length; s++)
                                {
                                    if (row.RematchTrainerIds[s] != trainerId) continue;

                                    trainerUsageResults.Add(new TrainerUsageResult
                                    {
                                        SourceId = -1,
                                        Type = $"Vs. Seeker Rematch {(char)('A' + s)}",
                                        Index = r,
                                        Details = $"Row {r}: rematch {(char)('A' + s)} for encounter trainer {row.EncounterTrainerId}"
                                    });
                                }
                            }
                        }

                        PopulateTrainerWatcherDataGridView(trainerUsageResults);
                        statusLabel.Text = $"Found {trainerUsageResults.Count} use(s) of Trainer {trainerId}";
                    }

                    private void ScanContainersForTrainerParameter(List<ScriptCommandContainer> containers, Dictionary<ushort, ScriptCommandInfo> commandInfoDict, int trainerId, int sourceFileId, string typeLabel)
                    {
                        if (containers == null) return;

                        foreach (var container in containers)
                        {
                            if (container.commands == null) continue;

                            foreach (var cmd in container.commands)
                            {
                                if (cmd.id == null || cmd.cmdParams == null) continue;

                                ScriptCommandInfo cmdInfo;
                                if (!commandInfoDict.TryGetValue(cmd.id.Value, out cmdInfo) || cmdInfo.ParameterTypes == null) continue;

                                for (int i = 0; i < cmd.cmdParams.Count && i < cmdInfo.ParameterTypes.Count; i++)
                                {
                                    if (cmdInfo.ParameterTypes[i] != ScriptParameter.ParameterType.Trainer) continue;
                                    if (GetParamValue(cmd.cmdParams[i]) != trainerId) continue;

                                    trainerUsageResults.Add(new TrainerUsageResult
                                    {
                                        SourceId = sourceFileId,
                                        Type = typeLabel,
                                        Index = (int)container.manualUserID,
                                        Details = $"{cmdInfo.Name} (param {i})"
                                    });
                                }
                            }
                        }
                    }

                    private void PopulateTrainerWatcherDataGridView(IList<TrainerUsageResult> results)
                    {
                        trainerWatcherDataGridView.Rows.Clear();

                        foreach (var result in results)
                        {
                            trainerWatcherDataGridView.Rows.Add(result.SourceId, result.Type, result.Index, result.Details);
                        }
                    }

                    private void trainerWatcherDataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
                    {
                        if (e.RowIndex < 0) return;

                        int sourceId = (int)trainerWatcherDataGridView.Rows[e.RowIndex].Cells[0].Value;
                        string type = (string)trainerWatcherDataGridView.Rows[e.RowIndex].Cells[1].Value;
                        int index = (int)trainerWatcherDataGridView.Rows[e.RowIndex].Cells[2].Value;

                        if (type.StartsWith("Vs. Seeker"))
                        {
                            using (var editor = new DSPRE.Editors.VsSeekerRematchEditor(index))
                            {
                                editor.ShowDialog();
                            }
                            statusLabel.Text = "Opened Vs. Seeker Rematch Editor";
                            return;
                        }

                        var mainProgram = Application.OpenForms["MainProgram"] as MainProgram;
                        if (mainProgram == null)
                        {
                            statusLabel.Text = "Could not find main window to open editor";
                            return;
                        }

                        if (type.StartsWith("Overworld"))
                        {
                            EditorPanels.eventEditor.OpenEventEditorWithOverworld(mainProgram, sourceId, index);
                            statusLabel.Text = $"Opened Event File {sourceId}, Overworld {index}";
                        }
                        else if (type == "Script Command")
                        {
                            EditorPanels.scriptEditor.OpenScriptEditorAndNavigate(mainProgram, sourceId, index);
                            statusLabel.Text = $"Opened Script File {sourceId}, Script {index}";
                        }
                        else if (type == "Function Command")
                        {
                            EditorPanels.scriptEditor.OpenScriptEditor(mainProgram, sourceId);
                            statusLabel.Text = $"Opened Script File {sourceId} (see Function {index})";
                        }
                    }

                    #endregion

                    #region Script Watcher Tab

                    private List<ScriptFileReferenceResult> scriptFileReferenceResults = new List<ScriptFileReferenceResult>();
                    private List<ScriptIdUsageResult> scriptIdUsageResults = new List<ScriptIdUsageResult>();
                    private Dictionary<int, ScriptFile> scriptFileIdToScript = new Dictionary<int, ScriptFile>();

                    private void PopulateIdWatcherScriptFileComboBox()
                    {
                        idWatcherScriptFileComboBox.Items.Clear();
                        scriptFileIdToScript.Clear();

                        foreach (var scriptFile in cachedScriptFiles)
                        {
                            idWatcherScriptFileComboBox.Items.Add($"{scriptFile.fileID}: Script File");
                            scriptFileIdToScript[scriptFile.fileID] = scriptFile;
                        }

                        if (idWatcherScriptFileComboBox.Items.Count > 0)
                        {
                            idWatcherScriptFileComboBox.SelectedIndex = 0;
                        }
                    }

                    private void idWatcherScriptFileComboBox_SelectedIndexChanged(object sender, EventArgs e)
                    {
                        idWatcherScriptIdComboBox.Items.Clear();

                        if (idWatcherScriptFileComboBox.SelectedIndex < 0) return;

                        string selectedText = idWatcherScriptFileComboBox.SelectedItem.ToString();
                        int colonIndex = selectedText.IndexOf(':');
                        if (colonIndex > 0 && int.TryParse(selectedText.Substring(0, colonIndex), out int scriptFileId))
                        {
                            if (scriptFileIdToScript.TryGetValue(scriptFileId, out ScriptFile scriptFile))
                            {
                                if (scriptFile.allScripts != null)
                                {
                                    for (int i = 0; i < scriptFile.allScripts.Count; i++)
                                    {
                                        idWatcherScriptIdComboBox.Items.Add($"Script {i + 1}");
                                    }
                                }

                                if (idWatcherScriptIdComboBox.Items.Count > 0)
                                {
                                    idWatcherScriptIdComboBox.SelectedIndex = 0;
                                }
                            }
                        }
                    }

                    private void fileWatcherSearchButton_Click(object sender, EventArgs e)
                    {
                        SearchScriptFileReferences();
                    }

                    private void SearchScriptFileReferences()
                    {
                        int searchScriptFileId = (int)fileWatcherScriptFileNumericUpDown.Value;

                        statusLabel.Text = $"Searching for references to script file {searchScriptFileId}...";
                        Application.DoEvents();

                        scriptFileReferenceResults.Clear();
                        fileWatcherDataGridView.Rows.Clear();

                        int headerCount = RomInfo.GetHeaderCount();
                        for (ushort i = 0; i < headerCount; i++)
                        {
                            try
                            {
                                MapHeader header = MapHeader.GetMapHeader(i);
                                if (header != null)
                                {
                                    if (header.scriptFileID == searchScriptFileId)
                                    {
                                        scriptFileReferenceResults.Add(new ScriptFileReferenceResult
                                        {
                                            ReferenceType = "Header",
                                            ReferenceID = i,
                                            Field = "scriptFileID"
                                        });
                                    }
                                    if (header.levelScriptID == searchScriptFileId)
                                    {
                                        scriptFileReferenceResults.Add(new ScriptFileReferenceResult
                                        {
                                            ReferenceType = "Header",
                                            ReferenceID = i,
                                            Field = "levelScriptID"
                                        });
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                AppLogger.Warn($"Failed to load header {i}: {ex.Message}");
                            }
                        }

                        PopulateFileWatcherDataGridView(scriptFileReferenceResults);
                        statusLabel.Text = $"Found {scriptFileReferenceResults.Count} references to script file {searchScriptFileId}";
                    }

                    private void PopulateFileWatcherDataGridView(IList<ScriptFileReferenceResult> results)
                    {
                        fileWatcherDataGridView.Rows.Clear();

                        foreach (var result in results)
                        {
                            fileWatcherDataGridView.Rows.Add(result.ReferenceType, result.ReferenceID, result.Field);
                        }
                    }

                    private void fileWatcherDataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
                    {
                        if (e.RowIndex < 0) return;

                        string refType = (string)fileWatcherDataGridView.Rows[e.RowIndex].Cells[0].Value;
                        int refId = (int)fileWatcherDataGridView.Rows[e.RowIndex].Cells[1].Value;

                        var mainProgram = Application.OpenForms["MainProgram"] as MainProgram;
                        if (mainProgram != null)
                        {
                            if (refType == "Header")
                            {
                                EditorPanels.headerEditor.OpenHeaderEditor(mainProgram, refId);
                                statusLabel.Text = $"Opened Header {refId}";
                            }
                        }
                        else
                        {
                            statusLabel.Text = "Could not find main window to open editor";
                        }
                    }

                    private void idWatcherSearchButton_Click(object sender, EventArgs e)
                    {
                        SearchScriptIdUsage();
                    }

                    private void SearchScriptIdUsage()
                    {
                        if (idWatcherScriptFileComboBox.SelectedIndex < 0 || idWatcherScriptIdComboBox.SelectedIndex < 0)
                        {
                            MessageBox.Show("Please select a script file and script ID.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        string selectedFileText = idWatcherScriptFileComboBox.SelectedItem.ToString();
                        int colonIndex = selectedFileText.IndexOf(':');
                        if (colonIndex <= 0 || !int.TryParse(selectedFileText.Substring(0, colonIndex), out int scriptFileId))
                        {
                            return;
                        }

                        // The script ID is 1-based in the dropdown (Script 1, Script 2, etc.)
                        int scriptId = idWatcherScriptIdComboBox.SelectedIndex + 1;

                        statusLabel.Text = $"Searching for Script {scriptId} usage in events linked to script file {scriptFileId}...";
                        Application.DoEvents();

                        scriptIdUsageResults.Clear();
                        idWatcherDataGridView.Rows.Clear();

                        HashSet<int> associatedEventFileIds = new HashSet<int>();
                        HashSet<int> associatedLevelScriptIds = new HashSet<int>();
                        int headerCount = RomInfo.GetHeaderCount();

                        for (ushort i = 0; i < headerCount; i++)
                        {
                            try
                            {
                                MapHeader header = MapHeader.GetMapHeader(i);
                                if (header != null && header.scriptFileID == scriptFileId)
                                {
                                    associatedEventFileIds.Add(header.eventFileID);
                                    associatedLevelScriptIds.Add(header.levelScriptID);
                                }
                            }
                            catch { }
                        }

                        foreach (var eventFile in cachedEventFiles)
                        {
                            if (!associatedEventFileIds.Contains(eventFile.ID))
                                continue;

                            if (eventFile.overworlds != null)
                            {
                                for (int i = 0; i < eventFile.overworlds.Count; i++)
                                {
                                    var ow = eventFile.overworlds[i];
                                    if (ow.scriptNumber == scriptId)
                                    {
                                        scriptIdUsageResults.Add(new ScriptIdUsageResult
                                        {
                                            EventFileID = eventFile.ID,
                                            EventType = "Overworld",
                                            EventIndex = i,
                                            Details = ow.ToString()
                                        });
                                    }
                                }
                            }

                            if (eventFile.spawnables != null)
                            {
                                for (int i = 0; i < eventFile.spawnables.Count; i++)
                                {
                                    var sp = eventFile.spawnables[i];
                                    if (sp.scriptNumber == scriptId)
                                    {
                                        scriptIdUsageResults.Add(new ScriptIdUsageResult
                                        {
                                            EventFileID = eventFile.ID,
                                            EventType = "Spawnable",
                                            EventIndex = i,
                                            Details = sp.ToString()
                                        });
                                    }
                                }
                            }

                            if (eventFile.triggers != null)
                            {
                                for (int i = 0; i < eventFile.triggers.Count; i++)
                                {
                                    var tr = eventFile.triggers[i];
                                    if (tr.scriptNumber == scriptId)
                                    {
                                        scriptIdUsageResults.Add(new ScriptIdUsageResult
                                        {
                                            EventFileID = eventFile.ID,
                                            EventType = "Trigger",
                                            EventIndex = i,
                                            Details = tr.ToString()
                                        });
                                    }
                                }
                            }
                        }

                        foreach (var levelScript in cachedLevelScriptFiles)
                        {
                            if (!associatedLevelScriptIds.Contains(levelScript.ID) || levelScript.bufferSet == null)
                                continue;

                            for (int i = 0; i < levelScript.bufferSet.Count; i++)
                            {
                                var trig = levelScript.bufferSet[i];
                                if (trig.scriptTriggered == scriptId)
                                {
                                    scriptIdUsageResults.Add(new ScriptIdUsageResult
                                    {
                                        EventFileID = levelScript.ID,
                                        EventType = "Level Script",
                                        EventIndex = i,
                                        Details = trig.ToString()
                                    });
                                }
                            }
                        }

                        int commonScriptHits = 0;
                        foreach (var eventFile in cachedEventFiles)
                        {
                            if (eventFile.overworlds != null)
                            {
                                for (int i = 0; i < eventFile.overworlds.Count; i++)
                                {
                                    var ow = eventFile.overworlds[i];
                                    var resolved = CommonScriptId.Resolve(RomInfo.gameFamily, ow.scriptNumber);
                                    if (resolved.Kind == CommonScriptId.Kind.Resolved && resolved.ScriptArchiveId == scriptFileId && resolved.ManualUserId == scriptId)
                                    {
                                        scriptIdUsageResults.Add(new ScriptIdUsageResult
                                        {
                                            EventFileID = eventFile.ID,
                                            EventType = "Overworld (Common Script)",
                                            EventIndex = i,
                                            Details = ow.ToString()
                                        });
                                        commonScriptHits++;
                                    }
                                }
                            }

                            if (eventFile.spawnables != null)
                            {
                                for (int i = 0; i < eventFile.spawnables.Count; i++)
                                {
                                    var sp = eventFile.spawnables[i];
                                    var resolved = CommonScriptId.Resolve(RomInfo.gameFamily, sp.scriptNumber);
                                    if (resolved.Kind == CommonScriptId.Kind.Resolved && resolved.ScriptArchiveId == scriptFileId && resolved.ManualUserId == scriptId)
                                    {
                                        scriptIdUsageResults.Add(new ScriptIdUsageResult
                                        {
                                            EventFileID = eventFile.ID,
                                            EventType = "Spawnable (Common Script)",
                                            EventIndex = i,
                                            Details = sp.ToString()
                                        });
                                        commonScriptHits++;
                                    }
                                }
                            }

                            if (eventFile.triggers != null)
                            {
                                for (int i = 0; i < eventFile.triggers.Count; i++)
                                {
                                    var tr = eventFile.triggers[i];
                                    var resolved = CommonScriptId.Resolve(RomInfo.gameFamily, tr.scriptNumber);
                                    if (resolved.Kind == CommonScriptId.Kind.Resolved && resolved.ScriptArchiveId == scriptFileId && resolved.ManualUserId == scriptId)
                                    {
                                        scriptIdUsageResults.Add(new ScriptIdUsageResult
                                        {
                                            EventFileID = eventFile.ID,
                                            EventType = "Trigger (Common Script)",
                                            EventIndex = i,
                                            Details = tr.ToString()
                                        });
                                        commonScriptHits++;
                                    }
                                }
                            }
                        }

                        foreach (var levelScript in cachedLevelScriptFiles)
                        {
                            if (levelScript.bufferSet == null) continue;

                            for (int i = 0; i < levelScript.bufferSet.Count; i++)
                            {
                                var trig = levelScript.bufferSet[i];
                                var resolved = CommonScriptId.Resolve(RomInfo.gameFamily, trig.scriptTriggered);
                                if (resolved.Kind == CommonScriptId.Kind.Resolved && resolved.ScriptArchiveId == scriptFileId && resolved.ManualUserId == scriptId)
                                {
                                    scriptIdUsageResults.Add(new ScriptIdUsageResult
                                    {
                                        EventFileID = levelScript.ID,
                                        EventType = "Level Script (Common Script)",
                                        EventIndex = i,
                                        Details = trig.ToString()
                                    });
                                    commonScriptHits++;
                                }
                            }
                        }

                        var commandInfoDict = RomInfo.GetScriptCommandInfoDict();
                        ushort? commonScriptCmdId = null;
                        foreach (var kvp in commandInfoDict)
                        {
                            if (kvp.Value.Name == "CommonScript")
                            {
                                commonScriptCmdId = kvp.Key;
                                break;
                            }
                        }

                        if (commonScriptCmdId.HasValue)
                        {
                            foreach (var scriptFile in cachedScriptFiles)
                            {
                                commonScriptHits += ScanContainersForCommonScriptCommand(scriptFile.allScripts, commonScriptCmdId.Value, scriptFileId, scriptId, scriptFile.fileID, "Script");
                                commonScriptHits += ScanContainersForCommonScriptCommand(scriptFile.allFunctions, commonScriptCmdId.Value, scriptFileId, scriptId, scriptFile.fileID, "Function");
                            }
                        }

                        PopulateIdWatcherDataGridView(scriptIdUsageResults);
                        statusLabel.Text = $"Found {scriptIdUsageResults.Count} uses of Script {scriptId} in Script File {scriptFileId}" +
                            (commonScriptHits > 0 ? $" ({commonScriptHits} via Common Script references)" : "");
                    }

                    /// <summary>
                    /// Scans a list of script/function containers for calls to the "CommonScript" command
                    /// (u16 Global Script ID parameter) that resolve to the given target archive+script.
                    /// </summary>
                    private int ScanContainersForCommonScriptCommand(List<ScriptCommandContainer> containers, ushort commonScriptCmdId, int targetScriptFileId, int targetScriptId, int sourceScriptFileId, string containerLabel)
                    {
                        int hits = 0;
                        if (containers == null) return 0;

                        foreach (var container in containers)
                        {
                            if (container.commands == null) continue;

                            foreach (var cmd in container.commands)
                            {
                                if (cmd.id != commonScriptCmdId || cmd.cmdParams == null || cmd.cmdParams.Count == 0)
                                    continue;

                                int globalScriptId = GetParamValue(cmd.cmdParams[0]);
                                var resolved = CommonScriptId.Resolve(RomInfo.gameFamily, globalScriptId);
                                if (resolved.Kind == CommonScriptId.Kind.Resolved && resolved.ScriptArchiveId == targetScriptFileId && resolved.ManualUserId == targetScriptId)
                                {
                                    scriptIdUsageResults.Add(new ScriptIdUsageResult
                                    {
                                        EventFileID = sourceScriptFileId,
                                        EventType = $"{containerLabel} Command (Common Script)",
                                        EventIndex = (int)container.manualUserID,
                                        Details = $"CommonScript call to Global Script ID {globalScriptId}"
                                    });
                                    hits++;
                                }
                            }
                        }

                        return hits;
                    }

                    private void PopulateIdWatcherDataGridView(IList<ScriptIdUsageResult> results)
                    {
                        idWatcherDataGridView.Rows.Clear();

                        foreach (var result in results)
                        {
                            idWatcherDataGridView.Rows.Add(result.EventFileID, result.EventType, result.EventIndex, result.Details);
                        }
                    }

                    private void idWatcherDataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
                    {
                        if (e.RowIndex < 0) return;

                        int eventFileId = (int)idWatcherDataGridView.Rows[e.RowIndex].Cells[0].Value;
                        string eventType = (string)idWatcherDataGridView.Rows[e.RowIndex].Cells[1].Value;
                        int eventIndex = (int)idWatcherDataGridView.Rows[e.RowIndex].Cells[2].Value;

                        var mainProgram = Application.OpenForms["MainProgram"] as MainProgram;
                        if (mainProgram != null)
                        {
                            if (eventType.StartsWith("Overworld"))
                            {
                                EditorPanels.eventEditor.OpenEventEditorWithOverworld(mainProgram, eventFileId, eventIndex);
                                statusLabel.Text = $"Opened Event File {eventFileId}, Overworld {eventIndex}";
                            }
                            else if (eventType.StartsWith("Spawnable"))
                            {
                                EditorPanels.eventEditor.OpenEventEditorWithSpawnable(mainProgram, eventFileId, eventIndex);
                                statusLabel.Text = $"Opened Event File {eventFileId}, Spawnable {eventIndex}";
                            }
                            else if (eventType.StartsWith("Trigger"))
                            {
                                EditorPanels.eventEditor.OpenEventEditorWithTrigger(mainProgram, eventFileId, eventIndex);
                                statusLabel.Text = $"Opened Event File {eventFileId}, Trigger {eventIndex}";
                            }
                            else if (eventType.StartsWith("Level Script"))
                            {
                                EditorPanels.levelScriptEditor.OpenLevelScriptEditor(mainProgram, eventFileId);
                                statusLabel.Text = $"Opened Level Script {eventFileId}";
                            }
                            else if (eventType.StartsWith("Script Command"))
                            {
                                EditorPanels.scriptEditor.OpenScriptEditorAndNavigate(mainProgram, eventFileId, eventIndex);
                                statusLabel.Text = $"Opened Script File {eventFileId}, Script {eventIndex}";
                            }
                            else if (eventType.StartsWith("Function Command"))
                            {
                                EditorPanels.scriptEditor.OpenScriptEditor(mainProgram, eventFileId);
                                statusLabel.Text = $"Opened Script File {eventFileId} (see Function {eventIndex})";
                            }
                            else
                            {
                                EditorPanels.eventEditor.OpenEventEditor(mainProgram, eventFileId);
                                statusLabel.Text = $"Opened Event File {eventFileId}";
                            }
                        }
                        else
                        {
                            statusLabel.Text = "Could not find main window to open editor";
                        }
                    }

                    #endregion

                    #region Header Watcher Tab

                    private MapHeader currentSearchedHeader = null;
                    private List<HeaderWarpResult> headerWarpResults = new List<HeaderWarpResult>();
                    private List<HeaderOutgoingWarpResult> headerOutgoingWarpResults = new List<HeaderOutgoingWarpResult>();

                    /// <summary>
                    /// Data class to hold incoming warp results pointing to a header
                    /// </summary>
                    private class HeaderWarpResult
                    {
                        public int EventFileID { get; set; }
                        public int WarpIndex { get; set; }
                        public string Position { get; set; }
                        public int Anchor { get; set; }
                    }

                    /// <summary>
                    /// Data class to hold outgoing warp results from a header's event file
                    /// </summary>
                    private class HeaderOutgoingWarpResult
                    {
                        public int WarpIndex { get; set; }
                        public string Position { get; set; }
                        public int DestHeader { get; set; }
                        public int DestAnchor { get; set; }
                    }

                    private void headerSearchButton_Click(object sender, EventArgs e)
                    {
                        SearchHeaderInfo();
                    }

                    private void SearchHeaderInfo()
                    {
                        int headerId = (int)headerIdNumericUpDown.Value;
                        int headerCount = RomInfo.GetHeaderCount();

                        if (headerId < 0 || headerId >= headerCount)
                        {
                            MessageBox.Show($"Header ID must be between 0 and {headerCount - 1}.", "Invalid Header ID", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        statusLabel.Text = $"Loading header {headerId}...";
                        Application.DoEvents();

                        try
                        {
                            currentSearchedHeader = MapHeader.GetMapHeader((ushort)headerId);
                            if (currentSearchedHeader == null)
                            {
                                MessageBox.Show($"Could not load header {headerId}.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }

                            PopulateHeaderInfoDataGridView(currentSearchedHeader);

                            SearchWarpsToHeader(headerId);

                            SearchWarpsFromHeader(currentSearchedHeader);

                            statusLabel.Text = $"Header {headerId} loaded - {headerWarpResults.Count} incoming, {headerOutgoingWarpResults.Count} outgoing warps";
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Error($"Error loading header {headerId}: {ex.Message}");
                            MessageBox.Show($"Error loading header: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }

                    private void PopulateHeaderInfoDataGridView(MapHeader header)
                    {
                        headerInfoDataGridView.Rows.Clear();

                        headerInfoDataGridView.Rows.Add("Header ID", header.ID);
                        headerInfoDataGridView.Rows.Add("Script File ID", header.scriptFileID);
                        headerInfoDataGridView.Rows.Add("Level Script ID", header.levelScriptID);
                        headerInfoDataGridView.Rows.Add("Event File ID", header.eventFileID);
                        headerInfoDataGridView.Rows.Add("Text Archive ID", header.textArchiveID);
                        headerInfoDataGridView.Rows.Add("Matrix ID", header.matrixID);
                        headerInfoDataGridView.Rows.Add("Area Data ID", header.areaDataID);
                        headerInfoDataGridView.Rows.Add("Camera Angle ID", header.cameraAngleID);
                        headerInfoDataGridView.Rows.Add("Music Day ID", header.musicDayID);
                        headerInfoDataGridView.Rows.Add("Music Night ID", header.musicNightID);
                        headerInfoDataGridView.Rows.Add("Weather ID", header.weatherID);
                        headerInfoDataGridView.Rows.Add("Wild Pok�mon", header.wildPokemon);
                        headerInfoDataGridView.Rows.Add("Location Specifier", header.locationSpecifier);
                        headerInfoDataGridView.Rows.Add("Flags", $"0x{header.flags:X2}");
                    }

                    private void SearchWarpsToHeader(int targetHeaderId)
                    {
                        headerWarpResults.Clear();
                        headerWarpsDataGridView.Rows.Clear();

                        foreach (var eventFile in cachedEventFiles)
                        {
                            if (eventFile.warps == null) continue;

                            for (int i = 0; i < eventFile.warps.Count; i++)
                            {
                                var warp = eventFile.warps[i];
                                if (warp.header == targetHeaderId)
                                {
                                    headerWarpResults.Add(new HeaderWarpResult
                                    {
                                        EventFileID = eventFile.ID,
                                        WarpIndex = i,
                                        Position = $"({warp.xMapPosition}, {warp.yMapPosition})",
                                        Anchor = warp.anchor
                                    });
                                }
                            }
                        }

                        foreach (var result in headerWarpResults)
                        {
                            headerWarpsDataGridView.Rows.Add(result.EventFileID, result.WarpIndex, result.Position, result.Anchor);
                        }
                    }

                    private void SearchWarpsFromHeader(MapHeader header)
                    {
                        headerOutgoingWarpResults.Clear();
                        headerOutgoingWarpsDataGridView.Rows.Clear();

                        var eventFile = cachedEventFiles.FirstOrDefault(e => e.ID == header.eventFileID);
                        if (eventFile == null || eventFile.warps == null)
                        {
                            return;
                        }

                        for (int i = 0; i < eventFile.warps.Count; i++)
                        {
                            var warp = eventFile.warps[i];
                            headerOutgoingWarpResults.Add(new HeaderOutgoingWarpResult
                            {
                                WarpIndex = i,
                                Position = $"({warp.xMapPosition}, {warp.yMapPosition})",
                                DestHeader = warp.header,
                                DestAnchor = warp.anchor
                            });
                        }

                        foreach (var result in headerOutgoingWarpResults)
                        {
                            headerOutgoingWarpsDataGridView.Rows.Add(result.WarpIndex, result.Position, result.DestHeader, result.DestAnchor);
                        }
                    }

                    private void headerInfoDataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
                    {
                        if (e.RowIndex < 0 || currentSearchedHeader == null) return;

                        string property = (string)headerInfoDataGridView.Rows[e.RowIndex].Cells[0].Value;
                        var mainProgram = Application.OpenForms["MainProgram"] as MainProgram;

                        if (mainProgram == null)
                        {
                            statusLabel.Text = "Could not find main window to open editor";
                            return;
                        }

                        switch (property)
                        {
                            case "Script File ID":
                                EditorPanels.scriptEditor.OpenScriptEditor(mainProgram, currentSearchedHeader.scriptFileID);
                                statusLabel.Text = $"Opened Script File {currentSearchedHeader.scriptFileID}";
                                break;
                            case "Level Script ID":
                                EditorPanels.levelScriptEditor.OpenLevelScriptEditor(mainProgram, currentSearchedHeader.levelScriptID);
                                statusLabel.Text = $"Opened Level Script {currentSearchedHeader.levelScriptID}";
                                break;
                            case "Event File ID":
                                EditorPanels.eventEditor.OpenEventEditor(mainProgram, currentSearchedHeader.eventFileID);
                                statusLabel.Text = $"Opened Event File {currentSearchedHeader.eventFileID}";
                                break;
                            case "Text Archive ID":
                                EditorPanels.textEditor.OpenTextEditor(mainProgram, currentSearchedHeader.textArchiveID, null);
                                statusLabel.Text = $"Opened Text Archive {currentSearchedHeader.textArchiveID}";
                                break;
                            case "Header ID":
                                EditorPanels.headerEditor.OpenHeaderEditor(mainProgram, currentSearchedHeader.ID);
                                statusLabel.Text = $"Opened Header {currentSearchedHeader.ID}";
                                break;
                            default:
                                Clipboard.SetText(headerInfoDataGridView.Rows[e.RowIndex].Cells[1].Value?.ToString() ?? "");
                                statusLabel.Text = $"{property} value copied to clipboard";
                                break;
                        }
                    }

                    private void headerWarpsDataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
                    {
                        if (e.RowIndex < 0) return;

                        int eventFileId = (int)headerWarpsDataGridView.Rows[e.RowIndex].Cells[0].Value;
                        int warpIndex = (int)headerWarpsDataGridView.Rows[e.RowIndex].Cells[1].Value;

                        bool shiftHeld = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;

                        if (shiftHeld)
                        {
                            var mainProgram = Application.OpenForms["MainProgram"] as MainProgram;
                            if (mainProgram != null)
                            {
                                EditorPanels.eventEditor.OpenEventEditorWithWarp(mainProgram, eventFileId, warpIndex);
                                statusLabel.Text = $"Opened Event File {eventFileId}, Warp {warpIndex}";
                            }
                            else
                            {
                                statusLabel.Text = "Could not find main window to open editor";
                            }
                        }
                        else
                        {
                            int? sourceHeader = FindHeaderByEventFileId(eventFileId);
                            if (sourceHeader.HasValue)
                            {
                                int previousHeader = (int)headerIdNumericUpDown.Value;
                                headerIdNumericUpDown.Value = sourceHeader.Value;
                                SearchHeaderInfo();
                                headerWatcherSubTabControl.SelectedTab = headerInfoSubTabPage;
                                statusLabel.Text = $"Jumped from Header {previousHeader} ? Header {sourceHeader.Value} (via Event File {eventFileId})";
                            }
                            else
                            {
                                statusLabel.Text = $"Could not find header using Event File {eventFileId}";
                            }
                        }
                    }

                    private void headerOutgoingWarpsDataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
                    {
                        if (e.RowIndex < 0 || currentSearchedHeader == null) return;

                        int warpIndex = (int)headerOutgoingWarpsDataGridView.Rows[e.RowIndex].Cells[0].Value;
                        int destHeader = (int)headerOutgoingWarpsDataGridView.Rows[e.RowIndex].Cells[2].Value;

                        bool shiftHeld = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;

                        if (shiftHeld)
                        {
                            var mainProgram = Application.OpenForms["MainProgram"] as MainProgram;
                            if (mainProgram != null)
                            {
                                EditorPanels.eventEditor.OpenEventEditorWithWarp(mainProgram, currentSearchedHeader.eventFileID, warpIndex);
                                statusLabel.Text = $"Opened Event File {currentSearchedHeader.eventFileID}, Warp {warpIndex}";
                            }
                            else
                            {
                                statusLabel.Text = "Could not find main window to open editor";
                            }
                        }
                        else
                        {
                            int previousHeader = currentSearchedHeader.ID;
                            headerIdNumericUpDown.Value = destHeader;
                            SearchHeaderInfo();
                            headerWatcherSubTabControl.SelectedTab = headerInfoSubTabPage;
                            statusLabel.Text = $"Jumped from Header {previousHeader} ? Header {destHeader}";
                        }
                    }

                    /// <summary>
                    /// Finds a header that uses the specified event file ID.
                    /// Returns the first matching header ID, or null if not found.
                    /// </summary>
                    private int? FindHeaderByEventFileId(int eventFileId)
                    {
                        int headerCount = RomInfo.GetHeaderCount();
                        for (int i = 0; i < headerCount; i++)
                        {
                            try
                            {
                                var header = MapHeader.GetMapHeader((ushort)i);
                                if (header != null && header.eventFileID == eventFileId)
                                {
                                    return i;
                                }
                            }
                            catch
                            {
                            }
                        }
                        return null;
                    }

                    #endregion
                }
            }
