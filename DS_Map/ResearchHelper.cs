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

        // Cached data for variable search
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
            // Data is loaded once on form load, no need for lazy loading
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

            // Ensure NARCs are unpacked
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

                // Load script files
                for (int i = 0; i < scriptCount; i++)
                {
                    try
                    {
                        ScriptFile scriptFile = new ScriptFile(i, readFunctions: true, readActions: true);

                        if (scriptFile.isLevelScript)
                        {
                            // It's a level script - load as LevelScriptFile too
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
                            // It's a regular script
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

                // Load event files
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

            // Populate the DataGridViews
            PopulateScriptsDataGridView(allScriptStats);
            PopulateLevelScriptsDataGridView(allLevelScriptStats);

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

                // Determine which column to search
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

                // Determine comparison type
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

                // Apply filter
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

                // Get the level script ID from the selected row
                int levelScriptId = (int)levelScriptsDataGridView.Rows[e.RowIndex].Cells[0].Value;

                // If Shift is held, navigate to the Level Script Editor
                if (Control.ModifierKeys.HasFlag(Keys.Shift))
                {
                    // Get reference to MainProgram
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
                    // Copy to clipboard for easy reference
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

            // Determine which column to search
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

            // Determine comparison type
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

            // Apply filter
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

            // Get the script ID from the selected row
            int scriptId = (int)scriptsDataGridView.Rows[e.RowIndex].Cells[0].Value;

                        // If Shift is held, navigate to the Script Editor
                        if (Control.ModifierKeys.HasFlag(Keys.Shift))
                        {
                            // Get reference to MainProgram
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
                            // Copy to clipboard for easy reference
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

                        // Parse the variable number based on mode
                        int variableNumber;
                        bool parseSuccess;

                        if (varHexRadioButton.Checked)
                        {
                            // Hex mode - strip 0x prefix if present
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
                            // Decimal mode
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

                        // Search in Scripts
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

                        // Search in Level Scripts
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

                        // Search in Events
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

                        // Populate results
                        PopulateVariablesDataGridView(allVariableUsageResults);
                        statusLabel.Text = $"Found {allVariableUsageResults.Count} files using variable {variableNumber} (0x{variableNumber:X})";
                    }

                    private int CountVariableInScriptFile(ScriptFile scriptFile, int variableNumber)
                    {
                        int count = 0;

                        // Get command info dict for parameter type checking
                        var commandInfoDict = RomInfo.GetScriptCommandInfoDict();

                        // Check all scripts
                        if (scriptFile.allScripts != null)
                        {
                            foreach (var script in scriptFile.allScripts)
                            {
                                count += CountVariableInCommands(script.commands, variableNumber, commandInfoDict);
                            }
                        }

                        // Check all functions
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

                                // Check if this parameter is a Variable type
                                ScriptParameter.ParameterType paramType = ScriptParameter.ParameterType.Integer;
                                if (paramTypes != null && i < paramTypes.Count)
                                {
                                    paramType = paramTypes[i];
                                }

                                // For Variable or Flex types, check the value
                                if (paramType == ScriptParameter.ParameterType.Variable || 
                                    paramType == ScriptParameter.ParameterType.Flex)
                                {
                                    int paramValue = GetParamValue(paramData);
                                    if (paramValue == variableNumber)
                                    {
                                        count++;
                                    }
                                }
                                // Also check raw values that match variable range (variables are typically 0x4000+)
                                else if (paramData.Length >= 2)
                                {
                                    int paramValue = GetParamValue(paramData);
                                    // Check if it's in variable range and matches
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

                        // Check triggers for variableWatched
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
                }
            }
