using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DSPRE
{
    public partial class ResearchHelper : Form
    {
        private List<ScriptFileStats> allScriptStats = new List<ScriptFileStats>();
        private BindingList<ScriptFileStats> displayedStats;

        public ResearchHelper()
        {
            InitializeComponent();
        }

        private void ResearchHelper_Load(object sender, EventArgs e)
        {
            LoadScriptData();
        }

        private async void LoadScriptData()
        {
            statusLabel.Text = "Loading script data...";
            scriptsDataGridView.Rows.Clear();
            allScriptStats.Clear();

            // Ensure scripts NARC is unpacked
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts });

            int scriptCount = Filesystem.GetScriptCount();

            // Run the loading in a background task to keep UI responsive
            await Task.Run(() =>
            {
                for (int i = 0; i < scriptCount; i++)
                {
                    try
                    {
                        ScriptFile scriptFile = new ScriptFile(i, readFunctions: true, readActions: true);

                        // Skip level scripts
                        if (scriptFile.isLevelScript)
                        {
                            continue;
                        }

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
                    catch (Exception ex)
                    {
                        AppLogger.Warn($"Failed to load script file {i}: {ex.Message}");
                    }

                    // Update progress on UI thread
                    int currentIndex = i;
                    this.BeginInvoke((Action)(() =>
                    {
                        statusLabel.Text = $"Loading script data... ({currentIndex + 1}/{scriptCount})";
                    }));
                }
            });

            // Populate the DataGridView
            displayedStats = new BindingList<ScriptFileStats>(allScriptStats);
            PopulateDataGridView(allScriptStats);

            statusLabel.Text = $"Ready - {allScriptStats.Count} script files loaded (excluding level scripts)";
        }

        private void PopulateDataGridView(IList<ScriptFileStats> stats)
        {
            scriptsDataGridView.Rows.Clear();

            foreach (var stat in stats)
            {
                scriptsDataGridView.Rows.Add(stat.ID, stat.Total, stat.Scripts, stat.Functions, stat.Actions);
            }
        }

        private void searchButton_Click(object sender, EventArgs e)
        {
            ApplyFilter();
        }

        private void clearSearchButton_Click(object sender, EventArgs e)
        {
            searchValueNumericUpDown.Value = 0;
            PopulateDataGridView(allScriptStats);
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

            PopulateDataGridView(filteredStats);
            statusLabel.Text = $"Filter applied: {columnName} {comparisonName} {searchValue} - {filteredStats.Count} results";
        }

        private void refreshButton_Click(object sender, EventArgs e)
        {
            LoadScriptData();
        }

        private void scriptsDataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            // Get the script ID from the selected row
            int scriptId = (int)scriptsDataGridView.Rows[e.RowIndex].Cells[0].Value;

            // Copy to clipboard for easy reference
            Clipboard.SetText(scriptId.ToString());
            statusLabel.Text = $"Script ID {scriptId} copied to clipboard";
        }

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
    }
}
