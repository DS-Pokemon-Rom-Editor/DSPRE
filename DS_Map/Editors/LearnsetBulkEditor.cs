using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using System.Text;

namespace DSPRE.Editors
{
    public partial class LearnsetBulkEditor : Form
    {
        private DataGridView dataGridView;
        private BindingList<LearnsetEntry> learnsetData;
        private string[] pokemonNames;
        private string[] moveNames;
        private ToolStrip toolStrip;
        private StatusStrip statusStrip;
        private ContextMenuStrip contextMenu;
        private bool isDirty = false;
        private bool changesSaved = false;
        private string currentFilterText = "";

        // Lookup dictionaries for import validation (built from ROM data)
        private Dictionary<string, int> pokemonNameToId;
        private Dictionary<string, int> moveNameToId;

        public LearnsetBulkEditor(BindingList<LearnsetEntry> learnsetData, string[] pokemonNames, string[] moveNames)
        {
            //InitializeComponent(); // we set up controls manually
            this.learnsetData = learnsetData;
            this.pokemonNames = pokemonNames;
            this.moveNames = moveNames;
            
            // Build lookup dictionaries from ROM data for strict validation
            BuildLookupDictionaries();
            
            SetupControls();
        }

        private void BuildLookupDictionaries()
        {
            // Build pokemon name -> ID lookup (case-insensitive)
            pokemonNameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < pokemonNames.Length; i++)
            {
                string name = pokemonNames[i];
                if (!string.IsNullOrEmpty(name) && !pokemonNameToId.ContainsKey(name))
                {
                    pokemonNameToId[name] = i;
                }
            }

            // Build move name -> ID lookup (case-insensitive)
            moveNameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < moveNames.Length; i++)
            {
                string name = moveNames[i];
                if (!string.IsNullOrEmpty(name) && !moveNameToId.ContainsKey(name))
                {
                    moveNameToId[name] = i;
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (isDirty && !changesSaved)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Are you sure you want to exit?",
                    "Unsaved Changes",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                );

                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            this.DialogResult = changesSaved ? DialogResult.OK : DialogResult.Cancel;
            base.OnFormClosed(e);
        }

        private void SetupControls()
        {
            this.Size = new Size(1000, 700);
            this.Text = "Bulk Learnset Editor";
            UpdateWindowTitle();

            dataGridView = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };

            var idColumn = new DataGridViewTextBoxColumn
            {
                DataPropertyName = "PokemonID",
                HeaderText = "ID",
                ReadOnly = true,
                Width = 50
            };

            var nameColumn = new DataGridViewTextBoxColumn
            {
                DataPropertyName = "PokemonName",
                HeaderText = "Pokemon",
                ReadOnly = true,
                Width = 150
            };

            var levelColumn = new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Level",
                HeaderText = "Level",
                Width = 60
            };

            var moveColumn = new DataGridViewComboBoxColumn
            {
                DataPropertyName = "MoveID",
                HeaderText = "Move",
                DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
                Width = 200,
                ValueType = typeof(int)
            };

            dataGridView.Columns.AddRange(new DataGridViewColumn[] { idColumn, nameColumn, levelColumn, moveColumn });

            dataGridView.DataSource = learnsetData;

            var moveItems = moveNames.Select((name, index) => new { Index = index, Name = name })
                        .ToArray();
            moveColumn.DataSource = moveItems;
            moveColumn.DisplayMember = "Name";
            moveColumn.ValueMember = "Index";

            contextMenu = new ContextMenuStrip();
            var ctxCopyLearnset = new ToolStripMenuItem("Copy Learnset from this Pokemon");
            ctxCopyLearnset.Click += (s, e) => CopyLearnsetFromContext();
            contextMenu.Items.Add(ctxCopyLearnset);
            dataGridView.ContextMenuStrip = contextMenu;

            toolStrip = new ToolStrip { Dock = DockStyle.Top };

            var btnSave = new ToolStripButton("Save All");
            btnSave.Click += (s, e) => SaveAllChanges();

            var btnAddMove = new ToolStripButton("Add Move");
            btnAddMove.Click += (s, e) => AddMoveToSelectedPokemon();

            var btnDelete = new ToolStripButton("Delete Selected");
            btnDelete.Click += (s, e) => DeleteSelectedMoves();

            var btnSort = new ToolStripButton("Sort Learnsets");
            btnSort.Click += (s, e) => SortAllLearnsets();

            var btnBulkOps = new ToolStripDropDownButton("Bulk Operations");

            var btnCopyLearnset = new ToolStripMenuItem("Copy Learnset to Other Pokemon...");
            btnCopyLearnset.Click += (s, e) => CopyLearnsetToOthers();

            var btnRemoveMoveGlobally = new ToolStripMenuItem("Remove Move from All Learnsets...");
            btnRemoveMoveGlobally.Click += (s, e) => RemoveMoveFromAllLearnsets();

            var btnLevelAdjust = new ToolStripMenuItem("Adjust Levels for Selected...");
            btnLevelAdjust.Click += (s, e) => AdjustLevelsForSelected();

            var btnReplaceMove = new ToolStripMenuItem("Replace Move in All Learnsets...");
            btnReplaceMove.Click += (s, e) => ReplaceMoveGlobally();

            btnBulkOps.DropDownItems.AddRange(new ToolStripItem[] {
                btnCopyLearnset,
                btnRemoveMoveGlobally,
                btnLevelAdjust,
                btnReplaceMove
            });

            var btnImportExport = new ToolStripDropDownButton("Import/Export");

            var btnExportCSV = new ToolStripMenuItem("Export to CSV...");
            btnExportCSV.Click += (s, e) => ExportToCSV();

            var btnImportCSV = new ToolStripMenuItem("Import from CSV...");
            btnImportCSV.Click += (s, e) => ImportFromCSV();

            btnImportExport.DropDownItems.AddRange(new ToolStripItem[] {
                btnExportCSV,
                btnImportCSV
            });

            var sep = new ToolStripSeparator();

            var lblFilter = new ToolStripLabel("Filter:");
            var txtFilter = new ToolStripTextBox();
            txtFilter.TextChanged += (s, e) => FilterData(txtFilter.Text);

            toolStrip.Items.AddRange(new ToolStripItem[] {
                btnSave, btnAddMove, btnDelete, btnSort, btnBulkOps, btnImportExport, sep, lblFilter, txtFilter
            });

            statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
            var statusLabel = new ToolStripStatusLabel();
            statusStrip.Items.Add(statusLabel);

            this.Controls.AddRange(new Control[] { dataGridView, toolStrip, statusStrip });

            dataGridView.CellValueChanged += DataGridView_CellValueChanged;
            dataGridView.DataError += DataGridView_DataError;
            dataGridView.SelectionChanged += DataGridView_SelectionChanged;
            dataGridView.MouseClick += DataGridView_MouseClick;
            dataGridView.UserAddedRow += DataGridView_UserAddedRow;
            dataGridView.UserDeletedRow += DataGridView_UserDeletedRow;

            UpdateStatus();

            // Probably should move all this to winforms designer later
        }

        #region Dirty Tracking Methods
        private void SetDirty()
        {
            if (!isDirty)
            {
                isDirty = true;
                changesSaved = false;
                UpdateWindowTitle();
            }
        }

        private void SetClean()
        {
            if (isDirty)
            {
                isDirty = false;
                changesSaved = true;
                UpdateWindowTitle();
            }
        }

        private void UpdateWindowTitle()
        {
            string baseTitle = "Bulk Learnset Editor";
            this.Text = isDirty ? $"{baseTitle} *" : baseTitle;
        }
        #endregion

        #region Event Handlers
        private void DataGridView_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var hitTest = dataGridView.HitTest(e.X, e.Y);
                if (hitTest.RowIndex >= 0 && hitTest.RowIndex < dataGridView.Rows.Count)
                {
                    var row = dataGridView.Rows[hitTest.RowIndex];
                    if (!row.IsNewRow)
                    {
                        dataGridView.ClearSelection();
                        row.Selected = true;
                    }
                }
            }
        }

        private void DataGridView_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            // Get the actual entry from the row's DataBoundItem
            var row = dataGridView.Rows[e.RowIndex];
            if (row.IsNewRow || !(row.DataBoundItem is LearnsetEntry entry)) return;

            // Validate level since there is no level 0 thing in gen4 afaik
            if (entry.Level < 1) entry.Level = 1;
            if (entry.Level > 100) entry.Level = 100;

            if (e.ColumnIndex == 3) // MoveID column, maybe can be set as a constant
            {
                entry.MoveName = moveNames[entry.MoveID];
            }

            dataGridView.InvalidateRow(e.RowIndex);
            UpdateStatus();
            SetDirty();
        }

        private void DataGridView_UserAddedRow(object sender, DataGridViewRowEventArgs e)
        {
            SetDirty();
        }

        private void DataGridView_UserDeletedRow(object sender, DataGridViewRowEventArgs e)
        {
            SetDirty();
        }

        private void DataGridView_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            MessageBox.Show($"Invalid value: {e.Exception.Message}", "Data Error",
                           MessageBoxButtons.OK, MessageBoxIcon.Warning);
            e.ThrowException = false;
        }

        private void DataGridView_SelectionChanged(object sender, EventArgs e)
        {
            UpdateStatus();
        }
        #endregion

        #region Bulk Operations
        private void AddMoveToSelectedPokemon()
        {
            var selectedPokemon = GetSelectedPokemonIds();
            if (selectedPokemon.Count == 0)
            {
                MessageBox.Show("Please select at least one Pokemon row.", "No Selection",
                               MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var addForm = new AddMoveForm(moveNames))
            {
                if (addForm.ShowDialog() == DialogResult.OK)
                {
                    foreach (var pokemonId in selectedPokemon)
                    {
                        learnsetData.Add(new LearnsetEntry
                        {
                            PokemonID = pokemonId,
                            PokemonName = pokemonNames[pokemonId],
                            Level = addForm.SelectedLevel,
                            MoveID = addForm.SelectedMoveId,
                            MoveName = moveNames[addForm.SelectedMoveId]
                        });
                    }
                    UpdateStatus();
                    SortAllLearnsets();
                    SetDirty();
                }
            }
        }

        private void DeleteSelectedMoves()
        {
            if (dataGridView.SelectedRows.Count == 0) return;

            var result = MessageBox.Show($"Delete {dataGridView.SelectedRows.Count} selected moves?",
                                       "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                // Get the actual LearnsetEntry objects from the selected rows
                var entriesToRemove = dataGridView.SelectedRows
                    .OfType<DataGridViewRow>()
                    .Where(row => !row.IsNewRow && row.DataBoundItem is LearnsetEntry)
                    .Select(row => (LearnsetEntry)row.DataBoundItem)
                    .ToList();

                foreach (var entry in entriesToRemove)
                {
                    learnsetData.Remove(entry);
                }

                // Refresh the filter if one is active
                RefreshCurrentFilter();
                UpdateStatus();
                SetDirty();
            }
        }

        private void SortAllLearnsets()
        {
            // Group by Pokemon and sort each learnset by level, maintaining Pokemon order
            var grouped = learnsetData
                .GroupBy(x => x.PokemonID)
                .OrderBy(g => g.Key) // Sort by Pokemon ID to maintain order
                .ToList();

            learnsetData.Clear();

            foreach (var group in grouped)
            {
                var sorted = group.OrderBy(x => x.Level);
                foreach (var entry in sorted)
                {
                    learnsetData.Add(entry);
                }
            }

            UpdateStatus();
            SetDirty();
        }

        private void FilterData(string filterText)
        {
            currentFilterText = filterText ?? "";

            if (string.IsNullOrWhiteSpace(filterText))
            {
                dataGridView.DataSource = learnsetData;
            }
            else
            {
                var filtered = new BindingList<LearnsetEntry>(
                    learnsetData.Where(x =>
                        x.PokemonName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        x.MoveName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0
                    ).ToList()
                );
                dataGridView.DataSource = filtered;
            }
            UpdateStatus();
        }

        private void RefreshCurrentFilter()
        {
            FilterData(currentFilterText);
        }

        private void SaveAllChanges()
        {
            try
            {
                // Group by Pokemon ID and save each learnset
                var groupedData = learnsetData.GroupBy(x => x.PokemonID);

                foreach (var group in groupedData)
                {
                    var learnset = new LearnsetData(group.Key);
                    learnset.list.Clear();

                    foreach (var entry in group.OrderBy(x => x.Level))
                    {
                        learnset.list.Add(((byte level, ushort move))(entry.Level, entry.MoveID));
                    }
                    learnset.SaveToFileDefaultDir(group.Key, false);
                }

                SetClean();
                UpdateStatus("All changes saved successfully!");
                MessageBox.Show("All learnset changes have been saved.", "Save Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving changes: {ex.Message}", "Save Error",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CopyLearnsetToOthers()
        {
            var sourcePokemon = GetSingleSelectedPokemonId();
            if (sourcePokemon == -1)
            {
                MessageBox.Show("Please select exactly one Pokemon row to copy FROM.", "Selection Error",
                              MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var form = new SelectPokemonForm(pokemonNames, "Select Pokemon to copy learnset TO:"))
            {
                if (form.ShowDialog() == DialogResult.OK && form.SelectedPokemonIds.Any())
                {
                    var sourceMoves = learnsetData.Where(x => x.PokemonID == sourcePokemon).ToList();

                    // Remove existing moves from target Pokemon
                    foreach (var targetId in form.SelectedPokemonIds)
                    {
                        var existingMoves = learnsetData.Where(x => x.PokemonID == targetId).ToList();
                        foreach (var move in existingMoves)
                        {
                            learnsetData.Remove(move);
                        }

                        // Add source moves to target Pokemon
                        foreach (var sourceMove in sourceMoves)
                        {
                            learnsetData.Add(new LearnsetEntry
                            {
                                PokemonID = targetId,
                                PokemonName = pokemonNames[targetId],
                                Level = sourceMove.Level,
                                MoveID = sourceMove.MoveID,
                                MoveName = sourceMove.MoveName
                            });
                        }
                    }

                    // Re-sort the entire list to maintain Pokemon ID order
                    var sortedEntries = learnsetData
                        .OrderBy(x => x.PokemonID)
                        .ThenBy(x => x.Level)
                        .ToList();

                    learnsetData.Clear();
                    foreach (var entry in sortedEntries)
                    {
                        learnsetData.Add(entry);
                    }

                    UpdateStatus($"Copied learnset from {pokemonNames[sourcePokemon]} to {form.SelectedPokemonIds.Count} Pokemon.");
                    SetDirty();
                }
            }
        }

        private void CopyLearnsetFromContext()
        {
            CopyLearnsetToOthers();
        }

        private void RemoveMoveFromAllLearnsets()
        {
            using (var form = new SelectMoveForm(moveNames, "Select move to remove from ALL learnsets:"))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    var moveId = form.SelectedMoveId;
                    var moveName = moveNames[moveId];

                    var result = MessageBox.Show($"This will remove {moveName} from EVERY Pokemon's learnset. Continue?",
                                               "Confirm Global Removal", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                    if (result == DialogResult.Yes)
                    {
                        var movesToRemove = learnsetData.Where(x => x.MoveID == moveId).ToList();
                        foreach (var move in movesToRemove)
                        {
                            learnsetData.Remove(move);
                        }

                        UpdateStatus($"Removed {moveName} from {movesToRemove.Count} learnsets.");
                        SetDirty();
                    }
                }
            }
        }

        private void AdjustLevelsForSelected()
        {
            if (dataGridView.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select some moves to adjust levels.", "No Selection",
                              MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var form = new AdjustLevelsForm())
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    var adjustment = form.LevelAdjustment;
                    var operation = form.AdjustmentOperation;

                    // Get the actual LearnsetEntry objects from the selected rows
                    var selectedEntries = dataGridView.SelectedRows
                        .OfType<DataGridViewRow>()
                        .Where(row => !row.IsNewRow && row.DataBoundItem is LearnsetEntry)
                        .Select(row => (LearnsetEntry)row.DataBoundItem)
                        .ToList();

                    foreach (var entry in selectedEntries)
                    {
                        switch (operation)
                        {
                            case LevelOperation.Add:
                                entry.Level = Math.Max(1, Math.Min(100, entry.Level + adjustment));
                                break;
                            case LevelOperation.Subtract:
                                entry.Level = Math.Max(1, Math.Min(100, entry.Level - adjustment));
                                break;
                            case LevelOperation.Set:
                                entry.Level = Math.Max(1, Math.Min(100, adjustment));
                                break;
                        }
                    }

                    dataGridView.Refresh();
                    UpdateStatus($"Adjusted levels for {selectedEntries.Count} moves.");
                    SetDirty();
                }
            }
        }

        private void ReplaceMoveGlobally()
        {
            using (var form = new ReplaceMoveForm(moveNames))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    var oldMoveId = form.OldMoveId;
                    var newMoveId = form.NewMoveId;
                    var oldMoveName = moveNames[oldMoveId];
                    var newMoveName = moveNames[newMoveId];

                    var affectedMoves = learnsetData.Where(x => x.MoveID == oldMoveId).ToList();

                    var result = MessageBox.Show($"Replace {oldMoveName} with {newMoveName} in {affectedMoves.Count} learnsets?",
                                               "Confirm Replacement", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        foreach (var move in affectedMoves)
                        {
                            move.MoveID = newMoveId;
                            move.MoveName = newMoveName;
                        }

                        dataGridView.Refresh();
                        UpdateStatus($"Replaced {oldMoveName} with {newMoveName} in {affectedMoves.Count} learnsets.");
                        SetDirty();
                    }
                }
            }
        }
        #endregion

        #region Import/Export Operations
        private void ExportToCSV()
        {
            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                saveDialog.DefaultExt = "csv";
                saveDialog.FileName = "LearnsetData.csv";
                saveDialog.Title = "Export Learnset Data to CSV";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        using (var writer = new StreamWriter(saveDialog.FileName))
                        {
                            // Write header
                            writer.WriteLine("ID,Name,Level,Move");

                            // Group by Pokemon ID and write entries
                            var grouped = learnsetData
                                .GroupBy(x => x.PokemonID)
                                .OrderBy(g => g.Key);

                            foreach (var group in grouped)
                            {
                                foreach (var entry in group.OrderBy(x => x.Level))
                                {
                                    writer.WriteLine($"{entry.PokemonID},{entry.PokemonName},{entry.Level},{entry.MoveName}");
                                }
                            }
                        }

                        MessageBox.Show($"Learnset data exported successfully to:\n{saveDialog.FileName}",
                            "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error exporting data: {ex.Message}", "Export Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ImportFromCSV()
        {
            using (var openDialog = new OpenFileDialog())
            {
                openDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                openDialog.DefaultExt = "csv";
                openDialog.Title = "Import Learnset Data from CSV";

                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    var importResult = ValidateAndParseCSV(openDialog.FileName);

                    // Show the import preview dialog
                    using (var previewForm = new LearnsetImportPreviewForm(importResult, pokemonNames, moveNames, learnsetData))
                    {
                        if (previewForm.ShowDialog() == DialogResult.OK)
                        {
                            // Apply the changes
                            ApplyImportedData(importResult.ValidEntries);
                        }
                    }
                }
            }
        }

        private LearnsetImportResult ValidateAndParseCSV(string filePath)
        {
            var result = new LearnsetImportResult();

            try
            {
                var lines = File.ReadAllLines(filePath);
                
                if (lines.Length == 0)
                {
                    result.Errors.Add(new ImportError(0, "File is empty."));
                    return result;
                }

                // Validate header
                var header = lines[0].Split(',');
                if (header.Length < 4 || 
                    !header[0].Trim().Equals("ID", StringComparison.OrdinalIgnoreCase) ||
                    !header[1].Trim().Equals("Name", StringComparison.OrdinalIgnoreCase) ||
                    !header[2].Trim().Equals("Level", StringComparison.OrdinalIgnoreCase) ||
                    !header[3].Trim().Equals("Move", StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add(new ImportError(1, $"Invalid header. Expected: 'ID,Name,Level,Move'. Got: '{lines[0]}'"));
                    return result;
                }

                result.TotalRowsRead = lines.Length - 1; // Exclude header

                // Parse each data row
                for (int i = 1; i < lines.Length; i++)
                {
                    int lineNumber = i + 1; // 1-based line number for user display
                    var line = lines[i];

                    // Skip empty lines
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var parts = ParseCSVLine(line);

                    if (parts.Length < 4)
                    {
                        result.Errors.Add(new ImportError(lineNumber, $"Invalid number of columns. Expected 4, got {parts.Length}. Line: '{line}'"));
                        continue;
                    }

                    var rowResult = ValidateRow(lineNumber, parts[0], parts[1], parts[2], parts[3]);
                    
                        if (rowResult.IsEmptyRow)
                        {
                            // Skip empty rows but don't count as errors
                            continue;
                        }
                    
                        // Collect warnings even from valid rows
                        result.Warnings.AddRange(rowResult.Warnings);
                        
                        // Collect name mismatches
                        result.NameMismatches.AddRange(rowResult.NameMismatches);
                    
                        if (rowResult.IsValid)
                        {
                            result.ValidEntries.Add(rowResult.Entry);
                        }
                        else
                        {
                            result.Errors.AddRange(rowResult.Errors);
                        }
                    }
            }
            catch (Exception ex)
            {
                result.Errors.Add(new ImportError(0, $"Failed to read file: {ex.Message}"));
            }

            return result;
        }

        private string[] ParseCSVLine(string line)
        {
            // Simple CSV parsing that handles quoted values
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString().Trim());

            return result.ToArray();
        }

        private RowValidationResult ValidateRow(int lineNumber, string idStr, string nameStr, string levelStr, string moveStr)
        {
            var result = new RowValidationResult { LineNumber = lineNumber };

            // Validate Pokemon ID
            int pokemonId;
            if (!int.TryParse(idStr.Trim(), out pokemonId))
            {
                result.Errors.Add(new ImportError(lineNumber, $"Invalid Pokemon ID '{idStr}'. Must be a number."));
            }
            else if (pokemonId < 0 || pokemonId >= pokemonNames.Length)
            {
                result.Errors.Add(new ImportError(lineNumber, $"Pokemon ID {pokemonId} is out of range. Valid range: 0-{pokemonNames.Length - 1}"));
            }
            else
            {
                result.Entry.PokemonID = pokemonId;
            }

            // Validate Pokemon Name (cross-reference with ID)
            string pokemonName = nameStr.Trim();
            if (result.Entry.PokemonID >= 0 && result.Entry.PokemonID < pokemonNames.Length)
            {
                string expectedName = pokemonNames[result.Entry.PokemonID];
                if (!pokemonName.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    // Name doesn't match ID at all - this is a warning but we'll use the ID
                    result.Warnings.Add(new ImportWarning(lineNumber, 
                        $"Pokemon name '{pokemonName}' doesn't match ID {result.Entry.PokemonID} (expected '{expectedName}'). Using ID."));
                    
                    // Track as potential name rename (user might want to rename the Pokemon in ROM)
                    result.NameMismatches.Add(new NameMismatch(
                        NameMismatch.MismatchType.Pokemon,
                        result.Entry.PokemonID,
                        expectedName,
                        pokemonName,
                        lineNumber));
                }
                else if (!pokemonName.Equals(expectedName, StringComparison.Ordinal))
                {
                    // Names match case-insensitively but differ in case - not a true mismatch, just case difference
                    // Don't add to NameMismatches since it's just a case difference
                }
                result.Entry.PokemonName = expectedName;
            }
            else if (pokemonNameToId.TryGetValue(pokemonName, out int resolvedId))
            {
                // ID was invalid but name is valid - use the name
                result.Entry.PokemonID = resolvedId;
                result.Entry.PokemonName = pokemonNames[resolvedId];
                result.Warnings.Add(new ImportWarning(lineNumber,
                    $"Invalid ID '{idStr}' but name '{pokemonName}' resolved to ID {resolvedId}."));
            }
            else
            {
                result.Errors.Add(new ImportError(lineNumber, 
                    $"Cannot resolve Pokemon. ID '{idStr}' is invalid and name '{pokemonName}' not found in ROM data."));
            }

            // Validate Level
            int level;
            if (string.IsNullOrWhiteSpace(levelStr))
            {
                // Empty level - this row might be intentionally empty (Pokemon with no moves)
                result.IsEmptyRow = true;
                return result;
            }
            
            if (!int.TryParse(levelStr.Trim(), out level))
            {
                result.Errors.Add(new ImportError(lineNumber, $"Invalid level '{levelStr}'. Must be a number."));
            }
            else if (level < 1 || level > 100)
            {
                result.Errors.Add(new ImportError(lineNumber, $"Level {level} is out of range. Valid range: 1-100"));
            }
            else
            {
                result.Entry.Level = level;
            }

            // Validate Move
            string moveName = moveStr.Trim();
            if (string.IsNullOrWhiteSpace(moveName))
            {
                result.IsEmptyRow = true;
                return result;
            }

            if (moveNameToId.TryGetValue(moveName, out int moveId))
            {
                result.Entry.MoveID = moveId;
                string romMoveName = moveNames[moveId];
                result.Entry.MoveName = romMoveName;
                
                // Check if the CSV name differs from ROM name (beyond just case)
                if (!moveName.Equals(romMoveName, StringComparison.Ordinal) && 
                    !moveName.Equals(romMoveName, StringComparison.OrdinalIgnoreCase))
                {
                    // This shouldn't happen since we matched case-insensitively, but just in case
                }
                else if (!moveName.Equals(romMoveName, StringComparison.Ordinal))
                {
                    // Matched case-insensitively but text differs (e.g., "POUND" vs "Pound" - just case)
                    // Only track if it's a real text difference, not just case
                    // Actually, we want to detect if user typed something like "pound them" vs "Pound"
                    // The case-insensitive match means they're the same text, different case only
                }
            }
            else
            {
                // Try to find a close match for better error message
                var closestMatch = FindClosestMatch(moveName, moveNames);
                string suggestion = closestMatch != null ? $" Did you mean '{closestMatch}'?" : "";
                result.Errors.Add(new ImportError(lineNumber, 
                    $"Move '{moveName}' not found in ROM data.{suggestion}"));
            }

            result.IsValid = result.Errors.Count == 0 && !result.IsEmptyRow;
            return result;
        }

        private string FindClosestMatch(string input, string[] candidates)
        {
            if (string.IsNullOrEmpty(input)) return null;

            string inputLower = input.ToLowerInvariant();
            string bestMatch = null;
            int bestScore = int.MaxValue;

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate)) continue;

                string candidateLower = candidate.ToLowerInvariant();

                // Simple Levenshtein-like scoring
                if (candidateLower.Contains(inputLower) || inputLower.Contains(candidateLower))
                {
                    int score = Math.Abs(candidate.Length - input.Length);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestMatch = candidate;
                    }
                }
            }

            return bestScore <= 5 ? bestMatch : null;
        }

        private void ApplyImportedData(List<LearnsetEntry> importedEntries)
        {
            // Clear existing data and add imported entries
            learnsetData.Clear();

            foreach (var entry in importedEntries)
            {
                learnsetData.Add(entry);
            }

            // Sort the data
            SortAllLearnsets();
            
            RefreshCurrentFilter();
            UpdateStatus($"Imported {importedEntries.Count} entries successfully.");
            SetDirty();
        }
        #endregion

        #region Helper Methods
        private List<int> GetSelectedPokemonIds()
        {
            return dataGridView.SelectedRows
                .OfType<DataGridViewRow>()
                .Where(row => !row.IsNewRow && row.DataBoundItem is LearnsetEntry)
                .Select(row => ((LearnsetEntry)row.DataBoundItem).PokemonID)
                .Distinct()
                .ToList();
        }

        private int GetSingleSelectedPokemonId()
        {
            var selectedIds = GetSelectedPokemonIds();
            return selectedIds.Count == 1 ? selectedIds[0] : -1;
        }

        private void UpdateStatus(string message = null)
        {
            if (statusStrip.Items.Count == 0) return;

            if (message != null)
            {
                statusStrip.Items[0].Text = message;
                return;
            }

            var selectedCount = dataGridView.SelectedRows.Count;
            var totalCount = learnsetData.Count;
            var pokemonCount = learnsetData.Select(x => x.PokemonID).Distinct().Count();

            statusStrip.Items[0].Text =
                $"{totalCount} moves across {pokemonCount} Pokemon. " +
                $"{(selectedCount > 0 ? $"{selectedCount} selected." : "")}" +
                $"{(isDirty ? " [Unsaved Changes]" : "")}";
        }
        #endregion
    }

    #region Supporting Enums and Classes
    public class LearnsetEntry
    {
        public int PokemonID { get; set; }
        public string PokemonName { get; set; }
        public int Level { get; set; }
        public int MoveID { get; set; }
        public string MoveName { get; set; }
    }

    public class AddMoveForm : Form
    {
        private NumericUpDown numLevel;
        private ComboBox cmbMove;
        private Button btnOK;
        private Button btnCancel;
        private string[] moveNames;

        public int SelectedLevel => (int)numLevel.Value;
        public int SelectedMoveId => cmbMove.SelectedIndex;

        public AddMoveForm(string[] moves)
        {
            moveNames = moves;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(300, 150);
            this.Text = "Add Move";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;

            var tableLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3
            };
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Level
            tableLayout.Controls.Add(new Label { Text = "Level:", TextAlign = ContentAlignment.MiddleRight }, 0, 0);
            numLevel = new NumericUpDown { Minimum = 1, Maximum = 100, Value = 1, Dock = DockStyle.Fill };
            tableLayout.Controls.Add(numLevel, 1, 0);

            // Move
            tableLayout.Controls.Add(new Label { Text = "Move:", TextAlign = ContentAlignment.MiddleRight }, 0, 1);
            cmbMove = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMove.Items.AddRange(moveNames.Select((name, idx) => $"{idx:000} - {name}").ToArray());
            if (cmbMove.Items.Count > 0) cmbMove.SelectedIndex = 0;
            tableLayout.Controls.Add(cmbMove, 1, 1);

            // Buttons
            var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            btnOK = new Button { Text = "OK", DialogResult = DialogResult.OK };
            buttonPanel.Controls.AddRange(new Control[] { btnOK, btnCancel });
            tableLayout.Controls.Add(buttonPanel, 0, 2);
            tableLayout.SetColumnSpan(buttonPanel, 2);

            this.Controls.Add(tableLayout);
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }
    }

    public class SelectPokemonForm : Form
    {
        private CheckedListBox checkedListBox;
        private Button btnOK;
        private Button btnCancel;

        public List<int> SelectedPokemonIds =>
            checkedListBox.CheckedIndices.Cast<int>().ToList();

        public SelectPokemonForm(string[] pokemonNames, string title)
        {
            InitializeComponent(pokemonNames, title);
        }

        private void InitializeComponent(string[] pokemonNames, string title)
        {
            this.Size = new Size(300, 500);
            this.Text = title;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;

            var tableLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            checkedListBox = new CheckedListBox
            {
                Dock = DockStyle.Fill
            };

            foreach (var item in pokemonNames.Select((name, idx) => $"{idx:000} - {name}"))
            {
                checkedListBox.Items.Add(item);
            }

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            btnOK = new Button { Text = "OK", DialogResult = DialogResult.OK };
            buttonPanel.Controls.AddRange(new Control[] { btnOK, btnCancel });

            tableLayout.Controls.Add(checkedListBox, 0, 0);
            tableLayout.Controls.Add(buttonPanel, 0, 1);

            this.Controls.Add(tableLayout);
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }
    }

    public class SelectMoveForm : Form
    {
        private ComboBox cmbMove;
        private Button btnOK;
        private Button btnCancel;

        public int SelectedMoveId => cmbMove.SelectedIndex;

        public SelectMoveForm(string[] moveNames, string title)
        {
            InitializeComponent(moveNames, title);
        }

        private void InitializeComponent(string[] moveNames, string title)
        {
            this.Size = new Size(300, 150);
            this.Text = title;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;

            var tableLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2
            };

            tableLayout.Controls.Add(new Label { Text = "Move:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 0);
            cmbMove = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMove.Items.AddRange(moveNames.Select((name, idx) => $"{idx:000} - {name}").ToArray());
            if (cmbMove.Items.Count > 0) cmbMove.SelectedIndex = 0;
            tableLayout.Controls.Add(cmbMove, 1, 0);

            var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            btnOK = new Button { Text = "OK", DialogResult = DialogResult.OK };
            buttonPanel.Controls.AddRange(new Control[] { btnOK, btnCancel });
            tableLayout.Controls.Add(buttonPanel, 0, 1);
            tableLayout.SetColumnSpan(buttonPanel, 2);

            this.Controls.Add(tableLayout);
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }
    }

    public class AdjustLevelsForm : Form
    {
        private NumericUpDown numAdjustment;
        private ComboBox cmbOperation;
        private Button btnOK;
        private Button btnCancel;

        public int LevelAdjustment => (int)numAdjustment.Value;
        public LevelOperation AdjustmentOperation => (LevelOperation)cmbOperation.SelectedIndex;

        public AdjustLevelsForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(300, 180);
            this.Text = "Adjust Levels";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;

            var tableLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3
            };

            tableLayout.Controls.Add(new Label { Text = "Operation:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 0);
            cmbOperation = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbOperation.Items.AddRange(new string[] { "Add to level", "Subtract from level", "Set level to" });
            cmbOperation.SelectedIndex = 0;
            tableLayout.Controls.Add(cmbOperation, 1, 0);

            tableLayout.Controls.Add(new Label { Text = "Value:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 1);
            numAdjustment = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 1, Maximum = 100, Value = 1 };
            tableLayout.Controls.Add(numAdjustment, 1, 1);

            var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            btnOK = new Button { Text = "OK", DialogResult = DialogResult.OK };
            buttonPanel.Controls.AddRange(new Control[] { btnOK, btnCancel });
            tableLayout.Controls.Add(buttonPanel, 0, 2);
            tableLayout.SetColumnSpan(buttonPanel, 2);

            this.Controls.Add(tableLayout);
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }
    }

    public class ReplaceMoveForm : Form
    {
        private ComboBox cmbOldMove;
        private ComboBox cmbNewMove;
        private Button btnOK;
        private Button btnCancel;

        public int OldMoveId => cmbOldMove.SelectedIndex;
        public int NewMoveId => cmbNewMove.SelectedIndex;

        public ReplaceMoveForm(string[] moveNames)
        {
            InitializeComponent(moveNames);
        }

        private void InitializeComponent(string[] moveNames)
        {
            this.Size = new Size(300, 180);
            this.Text = "Replace Move Globally";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;

            var tableLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3
            };

            tableLayout.Controls.Add(new Label { Text = "Replace:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 0);
            cmbOldMove = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbOldMove.Items.AddRange(moveNames.Select((name, idx) => $"{idx:000} - {name}").ToArray());
            if (cmbOldMove.Items.Count > 0) cmbOldMove.SelectedIndex = 0;
            tableLayout.Controls.Add(cmbOldMove, 1, 0);

            tableLayout.Controls.Add(new Label { Text = "With:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 1);
            cmbNewMove = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbNewMove.Items.AddRange(moveNames.Select((name, idx) => $"{idx:000} - {name}").ToArray());
            if (cmbNewMove.Items.Count > 0) cmbNewMove.SelectedIndex = 0;
            tableLayout.Controls.Add(cmbNewMove, 1, 1);

            var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            btnOK = new Button { Text = "OK", DialogResult = DialogResult.OK };
            buttonPanel.Controls.AddRange(new Control[] { btnOK, btnCancel });
            tableLayout.Controls.Add(buttonPanel, 0, 2);
            tableLayout.SetColumnSpan(buttonPanel, 2);

            this.Controls.Add(tableLayout);
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }
    }

    public enum LevelOperation
        {
            Add,
            Subtract,
            Set
        }

        #region Import Support Classes
        public class ImportError
        {
            public int LineNumber { get; }
            public string Message { get; }

            public ImportError(int lineNumber, string message)
            {
                LineNumber = lineNumber;
                Message = message;
            }

            public override string ToString() => LineNumber > 0 ? $"Line {LineNumber}: {Message}" : Message;
        }

        public class ImportWarning
        {
            public int LineNumber { get; }
            public string Message { get; }

            public ImportWarning(int lineNumber, string message)
            {
                LineNumber = lineNumber;
                Message = message;
            }

            public override string ToString() => LineNumber > 0 ? $"Line {LineNumber}: {Message}" : Message;
        }

        public class RowValidationResult
        {
            public int LineNumber { get; set; }
            public LearnsetEntry Entry { get; set; } = new LearnsetEntry();
            public List<ImportError> Errors { get; set; } = new List<ImportError>();
            public List<ImportWarning> Warnings { get; set; } = new List<ImportWarning>();
            public List<NameMismatch> NameMismatches { get; set; } = new List<NameMismatch>();
            public bool IsValid { get; set; }
            public bool IsEmptyRow { get; set; }
        }

        public class NameMismatch
        {
            public enum MismatchType { Pokemon, Move }
            
            public MismatchType Type { get; }
            public int Id { get; }
            public string RomName { get; }
            public string CsvName { get; }
            public int LineNumber { get; }

            public NameMismatch(MismatchType type, int id, string romName, string csvName, int lineNumber)
            {
                Type = type;
                Id = id;
                RomName = romName;
                CsvName = csvName;
                LineNumber = lineNumber;
            }

            public override string ToString() => 
                $"{Type} ID {Id}: ROM has '{RomName}', CSV has '{CsvName}' (Line {LineNumber})";
        }

        public class LearnsetImportResult
        {
            public List<LearnsetEntry> ValidEntries { get; set; } = new List<LearnsetEntry>();
            public List<ImportError> Errors { get; set; } = new List<ImportError>();
            public List<ImportWarning> Warnings { get; set; } = new List<ImportWarning>();
            public List<NameMismatch> NameMismatches { get; set; } = new List<NameMismatch>();
            public int TotalRowsRead { get; set; }

            public bool HasErrors => Errors.Count > 0;
            public bool HasWarnings => Warnings.Count > 0;
            public bool HasNameMismatches => NameMismatches.Count > 0;
            public int ValidCount => ValidEntries.Count;
            public int ErrorCount => Errors.Count;
            
            /// <summary>
            /// Gets unique move name mismatches (by move ID, taking first occurrence)
            /// </summary>
            public List<NameMismatch> UniqueMoveNameMismatches => 
                NameMismatches
                    .Where(m => m.Type == NameMismatch.MismatchType.Move)
                    .GroupBy(m => m.Id)
                    .Select(g => g.First())
                    .ToList();
            
            /// <summary>
            /// Gets unique Pokemon name mismatches (by Pokemon ID, taking first occurrence)
            /// </summary>
            public List<NameMismatch> UniquePokemonNameMismatches => 
                NameMismatches
                    .Where(m => m.Type == NameMismatch.MismatchType.Pokemon)
                    .GroupBy(m => m.Id)
                    .Select(g => g.First())
                    .ToList();
        }

        public class LearnsetImportPreviewForm : Form
        {
            private TabControl tabControl;
            private TextBox txtSummary;
            private TextBox txtErrors;
            private TextBox txtChanges;
            private TextBox txtValidValues;
            private TextBox txtNameMismatches;
            private CheckedListBox chkPokemonRenames;
            private CheckedListBox chkMoveRenames;
            private CheckBox chkUseTitleCase;
            private Button btnApply;
            private Button btnCancel;
            private LearnsetImportResult importResult;
            private BindingList<LearnsetEntry> currentData;
            private string[] pokemonNames;
            private string[] moveNames;
            private bool hasViewedErrorsTab = false;
            private int errorsTabIndex = 1; // Index of the Errors & Warnings tab

            public LearnsetImportPreviewForm(LearnsetImportResult result, string[] pokemonNames, string[] moveNames, BindingList<LearnsetEntry> currentData)
            {
                this.importResult = result;
                this.pokemonNames = pokemonNames;
                this.moveNames = moveNames;
                this.currentData = currentData;
                InitializeComponent();
                PopulateData();
            }

            private void InitializeComponent()
            {
                this.Size = new Size(800, 600);
                this.Text = "Import Preview";
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.StartPosition = FormStartPosition.CenterParent;
                this.MinimumSize = new Size(600, 400);

                var mainLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    RowCount = 2,
                    ColumnCount = 1,
                    Padding = new Padding(10)
                };
                mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

                // Tab control for different views
                tabControl = new TabControl { Dock = DockStyle.Fill };

                // Summary tab
                var summaryTab = new TabPage("Summary");
                txtSummary = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    Font = new Font("Consolas", 10f),
                    WordWrap = false
                };
                summaryTab.Controls.Add(txtSummary);

                // Errors tab
                var errorsTab = new TabPage("Errors & Warnings");
                txtErrors = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    Font = new Font("Consolas", 10f),
                    WordWrap = false,
                    ForeColor = Color.DarkRed
                };
                errorsTab.Controls.Add(txtErrors);

                // Changes preview tab
                var changesTab = new TabPage("Changes Preview");
                txtChanges = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    Font = new Font("Consolas", 9f),
                    WordWrap = false
                };
                changesTab.Controls.Add(txtChanges);

                // Name Mismatches tab (new)
                var nameMismatchesTab = new TabPage("Name Mismatches");
                var mismatchLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    RowCount = 4,
                    ColumnCount = 1
                };
                mismatchLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
                mismatchLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
                mismatchLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
                mismatchLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

                txtNameMismatches = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    Font = new Font("Consolas", 9f),
                    WordWrap = true
                };

                var pokemonGroup = new GroupBox { Text = "Pokemon Name Mismatches (check to rename in ROM)", Dock = DockStyle.Fill };
                chkPokemonRenames = new CheckedListBox { Dock = DockStyle.Fill };
                pokemonGroup.Controls.Add(chkPokemonRenames);

                var moveGroup = new GroupBox { Text = "Move Name Mismatches (check to rename in ROM)", Dock = DockStyle.Fill };
                chkMoveRenames = new CheckedListBox { Dock = DockStyle.Fill };
                moveGroup.Controls.Add(chkMoveRenames);

                var optionsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
                chkUseTitleCase = new CheckBox { Text = "Convert names to Title Case", Checked = true, AutoSize = true };
                optionsPanel.Controls.Add(chkUseTitleCase);

                mismatchLayout.Controls.Add(txtNameMismatches, 0, 0);
                mismatchLayout.Controls.Add(pokemonGroup, 0, 1);
                mismatchLayout.Controls.Add(moveGroup, 0, 2);
                mismatchLayout.Controls.Add(optionsPanel, 0, 3);
                nameMismatchesTab.Controls.Add(mismatchLayout);

                // Valid Values Reference tab
                var validValuesTab = new TabPage("Valid Values Reference");
                txtValidValues = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    Font = new Font("Consolas", 9f),
                    WordWrap = false
                };
                validValuesTab.Controls.Add(txtValidValues);

                tabControl.TabPages.AddRange(new TabPage[] { summaryTab, errorsTab, changesTab, nameMismatchesTab, validValuesTab });

                // Track when user views the errors tab
                tabControl.SelectedIndexChanged += (s, args) => {
                    if (tabControl.SelectedIndex == errorsTabIndex) {
                        hasViewedErrorsTab = true;
                    }
                };

                // Button panel
                var buttonPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.RightToLeft,
                    Padding = new Padding(0, 10, 0, 0)
                };

                btnCancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Size = new Size(100, 30)
                };

                btnApply = new Button
                {
                    Text = "Apply Changes",
                    Size = new Size(120, 30)
                };
                btnApply.Click += BtnApply_Click;

                buttonPanel.Controls.AddRange(new Control[] { btnCancel, btnApply });

                mainLayout.Controls.Add(tabControl, 0, 0);
                mainLayout.Controls.Add(buttonPanel, 0, 1);

                this.Controls.Add(mainLayout);
                this.AcceptButton = btnApply;
                this.CancelButton = btnCancel;
            }

            private string ToTitleCase(string input)
            {
                if (string.IsNullOrEmpty(input)) return input;
                return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower());
            }

            private void PopulateData()
            {
                var summary = new StringBuilder();
                var errors = new StringBuilder();
                var changes = new StringBuilder();
                var validValues = new StringBuilder();
                var nameMismatches = new StringBuilder();

                // Summary
                summary.AppendLine("═══════════════════════════════════════════════════════════════");
                summary.AppendLine("                     IMPORT VALIDATION SUMMARY");
                summary.AppendLine("═══════════════════════════════════════════════════════════════");
                summary.AppendLine();
                summary.AppendLine($"  Total rows read:     {importResult.TotalRowsRead}");
                summary.AppendLine($"  Valid entries:       {importResult.ValidCount}");
                summary.AppendLine($"  Errors found:        {importResult.ErrorCount}");
                summary.AppendLine($"  Warnings:            {importResult.Warnings.Count}");
                summary.AppendLine($"  Name mismatches:     {importResult.NameMismatches.Count}");
                summary.AppendLine();

                // Pokemon summary
                var pokemonInImport = importResult.ValidEntries
                    .GroupBy(e => e.PokemonID)
                    .OrderBy(g => g.Key)
                    .ToList();

                summary.AppendLine($"  Pokemon affected:    {pokemonInImport.Count}");
                summary.AppendLine($"  Current total moves: {currentData.Count}");
                summary.AppendLine($"  New total moves:     {importResult.ValidCount}");
                summary.AppendLine();

                if (importResult.HasErrors)
                {
                    summary.AppendLine("  ⚠️  ERRORS FOUND - Please review the 'Errors & Warnings' tab");
                    summary.AppendLine("      Some entries could not be imported due to validation errors.");
                    summary.AppendLine("      Check the 'Valid Values Reference' tab for accepted values.");
                    btnApply.Enabled = true; // Still allow import of valid entries
                }
                else
                {
                    summary.AppendLine("  ✓  No errors found. All entries validated successfully.");
                }

                if (importResult.Warnings.Count > 0)
                {
                    summary.AppendLine($"  ⚠️  {importResult.Warnings.Count} warning(s) - some data was auto-corrected.");
                }

                if (importResult.HasNameMismatches)
                {
                    summary.AppendLine($"  📝  {importResult.UniquePokemonNameMismatches.Count + importResult.UniqueMoveNameMismatches.Count} name mismatch(es) detected.");
                    summary.AppendLine("      Check the 'Name Mismatches' tab to optionally rename in ROM.");
                }

                summary.AppendLine();
                summary.AppendLine("═══════════════════════════════════════════════════════════════");

                // Errors and warnings
                if (importResult.HasErrors || importResult.Warnings.Count > 0)
                {
                    if (importResult.HasErrors)
                    {
                        errors.AppendLine("══════════════════════════════════════════════════════════════");
                        errors.AppendLine("                          ERRORS");
                        errors.AppendLine("══════════════════════════════════════════════════════════════");
                        errors.AppendLine();
                        foreach (var error in importResult.Errors)
                        {
                            errors.AppendLine($"  ✗ {error}");
                        }
                        errors.AppendLine();
                    }

                    if (importResult.Warnings.Count > 0)
                    {
                        errors.AppendLine("══════════════════════════════════════════════════════════════");
                        errors.AppendLine("                         WARNINGS");
                        errors.AppendLine("══════════════════════════════════════════════════════════════");
                        errors.AppendLine();
                        foreach (var warning in importResult.Warnings)
                        {
                            errors.AppendLine($"  ⚠ {warning}");
                        }
                    }
                }
                else
                {
                    errors.AppendLine("No errors or warnings found.");
                }

                // Name Mismatches tab
                if (importResult.HasNameMismatches)
                {
                    nameMismatches.AppendLine("Name mismatches detected between CSV and ROM data.");
                    nameMismatches.AppendLine("Check the items below to rename them in the ROM.");
                    nameMismatches.AppendLine("Note: Only mismatches with DIFFERENT text (not just case) are shown.");

                    // Populate Pokemon name mismatches checklist
                    foreach (var mismatch in importResult.UniquePokemonNameMismatches)
                    {
                        string display = $"ID {mismatch.Id}: '{mismatch.RomName}' → '{mismatch.CsvName}'";
                        chkPokemonRenames.Items.Add(mismatch, false);
                    }

                    // Populate Move name mismatches checklist
                    foreach (var mismatch in importResult.UniqueMoveNameMismatches)
                    {
                        string display = $"ID {mismatch.Id}: '{mismatch.RomName}' → '{mismatch.CsvName}'";
                        chkMoveRenames.Items.Add(mismatch, false);
                    }
                }
                else
                {
                    nameMismatches.AppendLine("No name mismatches detected.");
                    nameMismatches.AppendLine("All names in CSV match the ROM data (case-insensitive comparison).");
                }

                // Changes preview - show only what's ACTUALLY different
                changes.AppendLine("═══════════════════════════════════════════════════════════════");
                changes.AppendLine("                    CHANGES TO BE APPLIED");
                changes.AppendLine("═══════════════════════════════════════════════════════════════");
                changes.AppendLine();

                int changedPokemonCount = 0;
                int unchangedPokemonCount = 0;

                // Group by Pokemon for comparison
                foreach (var group in pokemonInImport)
                {
                    int pokemonId = group.Key;
                    string pokemonName = pokemonId < pokemonNames.Length ? pokemonNames[pokemonId] : $"Pokemon #{pokemonId}";
                    
                    // Get current moves for this Pokemon as a set of (level, moveId) tuples
                    var currentMoveSet = currentData
                        .Where(e => e.PokemonID == pokemonId)
                        .Select(e => (e.Level, e.MoveID))
                        .OrderBy(x => x.Level).ThenBy(x => x.MoveID)
                        .ToList();
                    
                    // Get new moves for this Pokemon
                    var newMoveSet = group
                        .Select(e => (e.Level, e.MoveID))
                        .OrderBy(x => x.Level).ThenBy(x => x.MoveID)
                        .ToList();
                    
                    // Compare if they're the same
                    bool isSame = currentMoveSet.Count == newMoveSet.Count &&
                                  currentMoveSet.SequenceEqual(newMoveSet);
                    
                    if (isSame)
                    {
                        unchangedPokemonCount++;
                        continue; // Skip Pokemon with no changes
                    }

                    changedPokemonCount++;

                    changes.AppendLine($"───────────────────────────────────────────────────────────────");
                    changes.AppendLine($"  [{pokemonId:D3}] {pokemonName}");
                    changes.AppendLine($"───────────────────────────────────────────────────────────────");
                
                    // Find added and removed moves
                    var currentSet = new HashSet<(int, int)>(currentMoveSet);
                    var newSet = new HashSet<(int, int)>(newMoveSet);
                    
                    var addedMoves = newMoveSet.Where(m => !currentSet.Contains(m)).ToList();
                    var removedMoves = currentMoveSet.Where(m => !newSet.Contains(m)).ToList();
                    
                    if (removedMoves.Any())
                    {
                        changes.AppendLine($"  REMOVED ({removedMoves.Count}):");
                        foreach (var (level, moveId) in removedMoves.OrderBy(x => x.Level))
                        {
                            string moveName = moveId < moveNames.Length ? moveNames[moveId] : $"Move #{moveId}";
                            changes.AppendLine($"    - Lv.{level,3}: {moveName}");
                        }
                    }
                    
                    if (addedMoves.Any())
                    {
                        changes.AppendLine($"  ADDED ({addedMoves.Count}):");
                        foreach (var (level, moveId) in addedMoves.OrderBy(x => x.Level))
                        {
                            string moveName = moveId < moveNames.Length ? moveNames[moveId] : $"Move #{moveId}";
                            changes.AppendLine($"    + Lv.{level,3}: {moveName}");
                        }
                    }
                    
                    changes.AppendLine();
                }

                // Summary of changes
                summary.AppendLine($"  Pokemon with changes: {changedPokemonCount}");
                summary.AppendLine($"  Pokemon unchanged:    {unchangedPokemonCount}");

                if (changedPokemonCount == 0)
                {
                    changes.AppendLine("  ✓  No changes detected - import data matches current data.");
                    changes.AppendLine();
                }

                // Show Pokemon that will lose their learnsets (present in current but not in import)
                var currentPokemonIds = currentData.Select(e => e.PokemonID).Distinct().ToHashSet();
                var importPokemonIds = importResult.ValidEntries.Select(e => e.PokemonID).Distinct().ToHashSet();
                var removedPokemon = currentPokemonIds.Except(importPokemonIds).ToList();

                if (removedPokemon.Any())
                {
                    changes.AppendLine("═══════════════════════════════════════════════════════════════");
                    changes.AppendLine("            POKEMON THAT WILL LOSE ALL MOVES");
                    changes.AppendLine("═══════════════════════════════════════════════════════════════");
                    changes.AppendLine();
                    changes.AppendLine("  ⚠️  The following Pokemon are in current data but NOT in the import.");
                    changes.AppendLine("      Their learnsets will be CLEARED if you proceed:");
                    changes.AppendLine();
                    foreach (var id in removedPokemon.OrderBy(x => x))
                    {
                        string name = id < pokemonNames.Length ? pokemonNames[id] : $"Pokemon #{id}";
                        int moveCount = currentData.Count(e => e.PokemonID == id);
                        changes.AppendLine($"    [{id:D3}] {name} - Currently has {moveCount} moves");
                    }
                }

                // Valid Values Reference
                validValues.AppendLine("═══════════════════════════════════════════════════════════════");
                validValues.AppendLine("                   VALID VALUES REFERENCE");
                validValues.AppendLine("═══════════════════════════════════════════════════════════════");
                validValues.AppendLine();
                validValues.AppendLine("  This tab shows all valid values that can be used in the CSV.");
                validValues.AppendLine("  Note: All text values are CASE-INSENSITIVE.");
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: ID (Pokemon ID)");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine($"  Range: 0 to {pokemonNames.Length - 1}");
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: Level");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  Range: 1 to 100");
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: Name (Pokemon Names)");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine($"  Total: {pokemonNames.Length} Pokemon (case-insensitive)");
                validValues.AppendLine();
                // Show a few random examples instead of full list
                var pokemonExamples = new[] { 1, 25, 150 }.Where(i => i < pokemonNames.Length).ToList();
                validValues.AppendLine("  Examples:");
                foreach (var i in pokemonExamples)
                {
                    validValues.AppendLine($"    {i,4}: {pokemonNames[i]}");
                }
                validValues.AppendLine();
                validValues.AppendLine($"  Any Pokemon name from ROM data is valid.");
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: Move (Move Names)");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine($"  Total: {moveNames.Length} Moves (case-insensitive)");
                validValues.AppendLine();
                // Show a few random examples instead of full list
                var moveExamples = new[] { 1, 10, 100 }.Where(i => i < moveNames.Length).ToList();
                validValues.AppendLine("  Examples:");
                foreach (var i in moveExamples)
                {
                    validValues.AppendLine($"    {i,4}: {moveNames[i]}");
                }
                validValues.AppendLine();
                validValues.AppendLine($"  Any move name from ROM data is valid.");

                txtSummary.Text = summary.ToString();
                txtErrors.Text = errors.ToString();
                txtChanges.Text = changes.ToString();
                txtValidValues.Text = validValues.ToString();
                txtNameMismatches.Text = nameMismatches.ToString();

                // Update tab colors based on content
                if (importResult.HasErrors)
                {
                    txtErrors.ForeColor = Color.DarkRed;
                }
                else if (importResult.Warnings.Count > 0)
                {
                    txtErrors.ForeColor = Color.DarkOrange;
                }
                else
                {
                    txtErrors.ForeColor = Color.DarkGreen;
                }
            }

            private void BtnApply_Click(object sender, EventArgs e)
            {
                if (importResult.ValidCount == 0)
                {
                    MessageBox.Show("No valid entries to import.", "Import Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // If there are errors and user hasn't viewed the errors tab, redirect them there first
                if (importResult.HasErrors && !hasViewedErrorsTab)
                {
                    MessageBox.Show("Let's at least open the Errors tab first, no?", "Review Errors",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    tabControl.SelectedIndex = errorsTabIndex;
                    hasViewedErrorsTab = true;
                    return;
                }

                // Check if any renames are selected
                int pokemonRenameCount = chkPokemonRenames.CheckedItems.Count;
                int moveRenameCount = chkMoveRenames.CheckedItems.Count;
                bool hasRenames = pokemonRenameCount > 0 || moveRenameCount > 0;

                var confirmMessage = $"This will replace all current learnset data with {importResult.ValidCount} imported entries.\n\n";
            
                if (importResult.HasErrors)
                {
                    confirmMessage += $"⚠️ Warning: {importResult.ErrorCount} rows had errors and will be skipped.\n\n";
                }

                if (hasRenames)
                {
                    confirmMessage += $"📝 The following names will be changed in ROM:\n";
                    if (pokemonRenameCount > 0) confirmMessage += $"   - {pokemonRenameCount} Pokemon name(s)\n";
                    if (moveRenameCount > 0) confirmMessage += $"   - {moveRenameCount} Move name(s)\n";
                    confirmMessage += $"   Title Case: {(chkUseTitleCase.Checked ? "Yes" : "No")}\n\n";
                }

                confirmMessage += "Are you sure you want to proceed?";

                var result = MessageBox.Show(confirmMessage, "Confirm Import",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    // Apply name renames if any are selected
                    if (hasRenames)
                    {
                        try
                        {
                            ApplyNameRenames();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error applying name renames: {ex.Message}\n\nThe import will continue without renaming.",
                                "Rename Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }

                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            }

            private void ApplyNameRenames()
            {
                bool useTitleCase = chkUseTitleCase.Checked;
                bool pokemonNamesChanged = false;
                bool moveNamesChanged = false;

                // Apply Pokemon name renames
                if (chkPokemonRenames.CheckedItems.Count > 0)
                {
                    var pokemonNameArchive = new DSPRE.ROMFiles.TextArchive(RomInfo.pokemonNamesTextNumbers[0]);

                    foreach (NameMismatch mismatch in chkPokemonRenames.CheckedItems)
                    {
                        if (mismatch.Id >= 0 && mismatch.Id < pokemonNameArchive.messages.Count)
                        {
                            string newName = useTitleCase ? ToTitleCase(mismatch.CsvName) : mismatch.CsvName;
                            pokemonNameArchive.messages[mismatch.Id] = newName;
                            pokemonNamesChanged = true;
                        }
                    }

                    if (pokemonNamesChanged)
                    {
                        pokemonNameArchive.SaveToExpandedDir(RomInfo.pokemonNamesTextNumbers[0], false);
                    }
                }

                // Apply Move name renames
                if (chkMoveRenames.CheckedItems.Count > 0)
                {
                    var moveNameArchive = new DSPRE.ROMFiles.TextArchive(RomInfo.attackNamesTextNumber);

                    foreach (NameMismatch mismatch in chkMoveRenames.CheckedItems)
                    {
                        if (mismatch.Id >= 0 && mismatch.Id < moveNameArchive.messages.Count)
                        {
                            string newName = useTitleCase ? ToTitleCase(mismatch.CsvName) : mismatch.CsvName;
                            moveNameArchive.messages[mismatch.Id] = newName;
                            moveNamesChanged = true;
                        }
                    }

                    if (moveNamesChanged)
                    {
                        moveNameArchive.SaveToExpandedDir(RomInfo.attackNamesTextNumber, false);
                    }
                }

                if (pokemonNamesChanged || moveNamesChanged)
                {
                    MessageBox.Show(
                        $"Name changes applied:\n" +
                        $"- Pokemon names: {chkPokemonRenames.CheckedItems.Count}\n" +
                        $"- Move names: {chkMoveRenames.CheckedItems.Count}",
                        "Names Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
        #endregion

        #endregion
    }