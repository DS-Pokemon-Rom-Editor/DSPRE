using DSPRE.Resources;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using static DSPRE.MoveData;
using MessageBox = System.Windows.Forms.MessageBox;

namespace DSPRE {
    public partial class MoveDataEditor : Form {
        private bool disableHandlers = false;

        private readonly string[] fileNames;
        private readonly string[] moveDescriptions;
        private readonly string[] typeNames;

        // Lookup dictionaries for import validation (built from ROM data)
        private Dictionary<string, int> typeNameToId;
        private Dictionary<string, MoveData.MoveSplit> splitNameToEnum;
        private Dictionary<string, ushort> rangeNameToValue;

        private int currentLoadedId = 0;
        private MoveData currentLoadedFile = null;

        private static bool dirty = false;
        private static readonly string formName = "Move Data Editor";

        public MoveDataEditor(string[] fileNames, string[] moveDescriptions) {
        this.fileNames = fileNames.ToArray();
        this.moveDescriptions = moveDescriptions;
        this.typeNames = RomInfo.GetTypeNames();

        // Build lookup dictionaries for import validation
        BuildLookupDictionaries();

        InitializeComponent();

            disableHandlers = true;

            moveNumberNumericUpDown.Maximum = fileNames.Length - 1;
            moveNameInputComboBox.Items.AddRange(this.fileNames);
            string[] battleSequenceFiles = RomInfo.GetBattleEffectSequenceFiles();

            for (int i = 0; i < battleSequenceFiles.Length; i++) {
                string[] db = PokeDatabase.MoveData.battleSequenceDescriptions;
                
                if (i >= db.Length || db[i] is null) {
                    battleSeqComboBox.Items.Add($"{i:D3} - Undocumented");
                } else {
                    battleSeqComboBox.Items.Add($"{i:D3} - {db[i]}");
                }
            }

            moveSplitComboBox.Items.AddRange(Enum.GetNames(typeof(MoveData.MoveSplit)));

            // Setup range ComboBox with user-friendly descriptions
            rangeComboBox.Items.Clear();
            foreach (var option in MoveData.AttackRangeDescriptions) {
                rangeComboBox.Items.Add($"{option.name}: {option.description}");
            }

            string[] names = Enum.GetNames(typeof(MoveData.MoveFlags));
            System.Collections.IList list = flagsTableLayoutPanel.Controls;
            for (int i = 0; i < list.Count; i++) {
                CheckBox cb = list[i] as CheckBox;
                cb.Text = names[i + 1];
                cb.CheckedChanged += FlagsCheckBox_CheckedChanged;
            }

            contestConditionComboBox.Items.AddRange(Enum.GetNames(typeof(MoveData.ContestCondition)));

            moveTypeComboBox.Items.AddRange(typeNames);

            disableHandlers = false;

            moveNameInputComboBox.SelectedIndex = 1;

            // Add Import/Export menu strip
            var menuStrip = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("Import & Export");
            
            var exportMenuItem = new ToolStripMenuItem("Export All to CSV...");
            exportMenuItem.Click += (s, args) => ExportToCSV();
            
            var importMenuItem = new ToolStripMenuItem("Import from CSV...");
            importMenuItem.Click += (s, args) => ImportFromCSV();

            fileMenu.DropDownItems.Add(exportMenuItem);
            fileMenu.DropDownItems.Add(importMenuItem);
            menuStrip.Items.Add(fileMenu);
            
            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);

            // Focus the move name input when the form is shown
            this.Shown += MoveDataEditor_Shown;
        }

        private void MoveDataEditor_Shown(object sender, EventArgs e) {
            // Give focus to the move name combobox so user can immediately start typing
            moveNameInputComboBox.Focus();
            moveNameInputComboBox.SelectAll();
        }
        private void setDirty(bool status) {
            if (status) {
                dirty = true;
                this.Text = formName + "*";
            } else {
                dirty = false;
                this.Text = formName;
            }
        }
        private bool CheckDiscardChanges() {
            if (!dirty) {
                return true;
            }
            
            DialogResult res = MessageBox.Show(this, "There are unsaved changes to the current Move data.\nDiscard and proceed?", "Unsaved changes", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res.Equals(DialogResult.Yes)) {
                return true;
            }

            moveNumberNumericUpDown.Value = currentLoadedId;
            moveNameInputComboBox.SelectedIndex = currentLoadedId;


            return false;
        }
        private void ChangeLoadedFile(int toLoad) {
            currentLoadedId = toLoad;
            currentLoadedFile = new MoveData(toLoad);
            PopulateAllFromCurrentFile();
            setDirty(false);
        }
        private void PopulateAllFromCurrentFile() {
            moveTypeComboBox.SelectedIndex = (int)currentLoadedFile.movetype;

            // Find the matching range option
            int rangeIndex = 0;
            for (int i = 0; i < MoveData.AttackRangeDescriptions.Length; i++) {
                if (MoveData.AttackRangeDescriptions[i].value == currentLoadedFile.target) {
                    rangeIndex = i;
                    break;
                }
            }
            rangeComboBox.SelectedIndex = rangeIndex;

            System.Collections.IList list = flagsTableLayoutPanel.Controls;
            for (int i = 0; i < list.Count; i++) {
                (list[i] as CheckBox).Checked = ((currentLoadedFile.flagField & (1 << i)) != 0);
            }

            textBox1.Text = moveDescriptions[currentLoadedId];

            battleSeqComboBox.SelectedIndex = (int)currentLoadedFile.battleeffect;
            moveSplitComboBox.SelectedIndex = (int)currentLoadedFile.split;
            sideEffectProbabilityUpDown.Value = currentLoadedFile.sideEffectProbability;
            contestConditionComboBox.SelectedIndex = (int)currentLoadedFile.contestConditionType;
            contestAppealNumericUpDown.Value = currentLoadedFile.contestAppeal;
            priorityNumericUpDown.Value = currentLoadedFile.priority;
            
            powerNumericUpDown.Value = currentLoadedFile.damage;
            accuracyNumericUpDown.Value = currentLoadedFile.accuracy;

            ppUpDown.Value = currentLoadedFile.pp;
        }

        //-------------------------------
        private void saveDataButton_Click(object sender, EventArgs e) {
            currentLoadedFile.SaveToFileDefaultDir(currentLoadedId, true);
            setDirty(false);
        }

        private void FlagsCheckBox_CheckedChanged(object sender, EventArgs e) {
            if (disableHandlers) {
                return;
            }

            disableHandlers = true;

            System.Collections.IList list = flagsTableLayoutPanel.Controls;
            
            currentLoadedFile.flagField = 0; 
            for (int i = 0; i < list.Count; i++) {
                int en = (list[i] as CheckBox).Checked ? 1 : 0;
                currentLoadedFile.flagField |= (byte)(en << i);
            }

            setDirty(true);
            disableHandlers = false;
        }

        private void rangeComboBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (disableHandlers) {
                return;
            }

            int selectedIndex = rangeComboBox.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < MoveData.AttackRangeDescriptions.Length) {
                currentLoadedFile.target = MoveData.AttackRangeDescriptions[selectedIndex].value;
                setDirty(true);
            }
        }
        private void moveNameInputComboBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (disableHandlers) {
                return;
            }

            disableHandlers = true;

            if (CheckDiscardChanges()) {
                int newNumber = moveNameInputComboBox.SelectedIndex;
                moveNumberNumericUpDown.Value = newNumber;
                ChangeLoadedFile(newNumber);
            }

            disableHandlers = false;
        }

        private void moveNumberNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (disableHandlers) { 
                return; 
            }

            disableHandlers = true;

            if (CheckDiscardChanges()) {
                int newNumber = (int)moveNumberNumericUpDown.Value;
                moveNameInputComboBox.SelectedIndex = newNumber;
                ChangeLoadedFile(newNumber);
            }
            
            disableHandlers = false;
        }

        private void saveToFileToolStripMenuItem_Click(object sender, EventArgs e) {
            string suggestedFilename = this.fileNames[currentLoadedId];
            currentLoadedFile.SaveToFileExplorePath(suggestedFilename, true);
        }

        private void ppUpDown_ValueChanged(object sender, EventArgs e) {
            if (disableHandlers) {
                return;
            }

            currentLoadedFile.pp = (byte)ppUpDown.Value;
            setDirty(true);
        }

        private void moveSplitComboBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (disableHandlers) {
                return;
            }

            currentLoadedFile.split = (MoveSplit)moveSplitComboBox.SelectedIndex;
            setDirty(true);
        }

        private void moveTypeComboBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (disableHandlers) {
                return;
            }

            currentLoadedFile.movetype = (PokemonType)moveTypeComboBox.SelectedIndex;
            setDirty(true);
        }

        private void battleSeqComboBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (disableHandlers) {
                return;
            }

            currentLoadedFile.battleeffect = (ushort)battleSeqComboBox.SelectedIndex;
            setDirty(true);
        }

        private void contestConditionComboBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (disableHandlers) {
                return;
            }

            currentLoadedFile.contestConditionType = (ContestCondition)contestConditionComboBox.SelectedIndex;
            setDirty(true);
        }
        private void contestAppealNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (disableHandlers) {
                return;
            }

            currentLoadedFile.contestAppeal = (byte)contestAppealNumericUpDown.Value;
            setDirty(true);
        }

        private void powerNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (disableHandlers) {
                return;
            }

            currentLoadedFile.damage = (byte)powerNumericUpDown.Value;
            setDirty(true);
        }

        private void accuracyNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (disableHandlers) {
                return;
            }

            currentLoadedFile.accuracy = (byte)accuracyNumericUpDown.Value;
            setDirty(true);
        }

        private void priorityNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (currentLoadedFile.priority == 0) {
                prioPictureBox.Image = null;
            } else if (currentLoadedFile.priority > 0) {
                prioPictureBox.Image = Properties.Resources.addIcon;
            } else {
                prioPictureBox.Image = Properties.Resources.deleteIcon;
            }

            if (disableHandlers) {
                return;
            }
            currentLoadedFile.priority = (sbyte)priorityNumericUpDown.Value;

            setDirty(true);
        }

        private void sideEffectUpDown_ValueChanged(object sender, EventArgs e) {
                    if (disableHandlers) {
                        return;
                    }
                    currentLoadedFile.sideEffectProbability = (byte)sideEffectProbabilityUpDown.Value;

                    setDirty(true);
                }

                #region Import/Export Methods
                private void BuildLookupDictionaries() {
                    // Build type name -> ID lookup (case-insensitive)
                    typeNameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    string[] types = RomInfo.GetTypeNames();
                    for (int i = 0; i < types.Length; i++) {
                        if (!string.IsNullOrEmpty(types[i]) && !typeNameToId.ContainsKey(types[i])) {
                            typeNameToId[types[i]] = i;
                        }
                    }

                    // Build split name -> enum lookup (case-insensitive)
                    splitNameToEnum = new Dictionary<string, MoveData.MoveSplit>(StringComparer.OrdinalIgnoreCase);
                    foreach (MoveData.MoveSplit split in Enum.GetValues(typeof(MoveData.MoveSplit))) {
                        splitNameToEnum[split.ToString()] = split;
                    }

                    // Build range name -> value lookup (case-insensitive)
                    rangeNameToValue = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
                    foreach (var range in MoveData.AttackRangeDescriptions) {
                        rangeNameToValue[range.name] = range.value;
                    }
                }

                private void ExportToCSV() {
                    using (var saveDialog = new SaveFileDialog()) {
                        saveDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                        saveDialog.DefaultExt = "csv";
                        saveDialog.FileName = "MoveData.csv";
                        saveDialog.Title = "Export Move Data to CSV";

                        if (saveDialog.ShowDialog() == DialogResult.OK) {
                            try {
                                using (var writer = new StreamWriter(saveDialog.FileName)) {
                                    // Write header - matching DocTool format but simplified for import
                                    writer.WriteLine("Move ID,Move Name,Move Type,Move Split,Power,Accuracy,Priority,Side Effect Probability,PP,Range");

                                    for (int i = 0; i < fileNames.Length; i++) {
                                        MoveData move = new MoveData(i);
                                
                                        string typeString = (int)move.movetype < typeNames.Length 
                                            ? typeNames[(int)move.movetype] 
                                            : $"UnknownType_{(int)move.movetype}";
                                
                                        string rangeString = MoveData.GetAttackRangeName(move.target);

                                        writer.WriteLine($"{i},{fileNames[i]},{typeString},{move.split}," +
                                            $"{move.damage},{move.accuracy},{move.priority}," +
                                            $"{move.sideEffectProbability},{move.pp},{rangeString}");
                                    }
                                }

                                MessageBox.Show($"Move data exported successfully to:\n{saveDialog.FileName}",
                                    "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            } catch (Exception ex) {
                                MessageBox.Show($"Error exporting data: {ex.Message}", "Export Error",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                }

                private void ImportFromCSV() {
                    using (var openDialog = new OpenFileDialog()) {
                        openDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                        openDialog.DefaultExt = "csv";
                        openDialog.Title = "Import Move Data from CSV";

                        if (openDialog.ShowDialog() == DialogResult.OK) {
                            var importResult = ValidateAndParseCSV(openDialog.FileName);

                            // Show the import preview dialog
                            using (var previewForm = new MoveDataImportPreviewForm(importResult, fileNames, typeNames)) {
                                if (previewForm.ShowDialog() == DialogResult.OK) {
                                    // Apply the changes
                                    ApplyImportedData(importResult.ValidEntries);

                                    // Apply any selected name renames
                                    if (previewForm.SelectedMoveRenames.Count > 0) {
                                        ApplyMoveRenames(previewForm.SelectedMoveRenames, previewForm.UseTitleCase);
                                    }
                                }
                            }
                        }
                    }
                }

                private MoveDataImportResult ValidateAndParseCSV(string filePath) {
                    var result = new MoveDataImportResult();

                    try {
                        var lines = File.ReadAllLines(filePath);

                        if (lines.Length == 0) {
                            result.Errors.Add(new MoveImportError(0, "File is empty."));
                            return result;
                        }

                        // Validate header
                        var header = lines[0].Split(',');
                        if (header.Length < 10 ||
                            !header[0].Trim().Equals("Move ID", StringComparison.OrdinalIgnoreCase) ||
                            !header[1].Trim().Equals("Move Name", StringComparison.OrdinalIgnoreCase)) {
                            result.Errors.Add(new MoveImportError(1, $"Invalid header. Expected: 'Move ID,Move Name,Move Type,Move Split,Power,Accuracy,Priority,Side Effect Probability,PP,Range'. Got: '{lines[0]}'"));
                            return result;
                        }

                        result.TotalRowsRead = lines.Length - 1;

                        // Parse each data row
                        for (int i = 1; i < lines.Length; i++) {
                            int lineNumber = i + 1;
                            var line = lines[i];

                            if (string.IsNullOrWhiteSpace(line)) {
                                continue;
                            }

                            var parts = ParseCSVLine(line);

                            if (parts.Length < 10) {
                                result.Errors.Add(new MoveImportError(lineNumber, $"Invalid number of columns. Expected 10, got {parts.Length}. Line: '{line}'"));
                                continue;
                            }

                            var rowResult = ValidateRow(lineNumber, parts);

                            result.Warnings.AddRange(rowResult.Warnings);
                            
                            // Collect name mismatches
                            result.NameMismatches.AddRange(rowResult.NameMismatches);

                            if (rowResult.IsValid) {
                                result.ValidEntries.Add(rowResult.Entry);
                            } else {
                                result.Errors.AddRange(rowResult.Errors);
                            }
                        }
                    } catch (Exception ex) {
                        result.Errors.Add(new MoveImportError(0, $"Failed to read file: {ex.Message}"));
                    }

                    return result;
                }

                private string[] ParseCSVLine(string line) {
                    var result = new List<string>();
                    var current = new StringBuilder();
                    bool inQuotes = false;

                    foreach (char c in line) {
                        if (c == '"') {
                            inQuotes = !inQuotes;
                        } else if (c == ',' && !inQuotes) {
                            result.Add(current.ToString().Trim());
                            current.Clear();
                        } else {
                            current.Append(c);
                        }
                    }
                    result.Add(current.ToString().Trim());

                    return result.ToArray();
                }

                private MoveRowValidationResult ValidateRow(int lineNumber, string[] parts) {
                    var result = new MoveRowValidationResult { LineNumber = lineNumber };
                    var entry = new MoveDataImportEntry();

                    // Validate Move ID (column 0)
                    if (!int.TryParse(parts[0].Trim(), out int moveId)) {
                        result.Errors.Add(new MoveImportError(lineNumber, $"Invalid Move ID '{parts[0]}'. Must be a number."));
                    } else if (moveId < 0 || moveId >= fileNames.Length) {
                        result.Errors.Add(new MoveImportError(lineNumber, $"Move ID {moveId} is out of range. Valid range: 0-{fileNames.Length - 1}"));
                    } else {
                        entry.MoveID = moveId;
                    }

                    // Validate Move Name (column 1) - cross-reference with ID
                    string moveName = parts[1].Trim();
                    if (entry.MoveID >= 0 && entry.MoveID < fileNames.Length) {
                        string expectedName = fileNames[entry.MoveID];
                        if (!moveName.Equals(expectedName, StringComparison.OrdinalIgnoreCase)) {
                            // Name doesn't match ID at all - this is a warning but we'll use the ID
                            result.Warnings.Add(new MoveImportWarning(lineNumber,
                                $"Move name '{moveName}' doesn't match ID {entry.MoveID} (expected '{expectedName}'). Using ID."));
                            
                            // Track as potential name rename (user might want to rename the Move in ROM)
                            result.NameMismatches.Add(new MoveNameMismatch(
                                entry.MoveID,
                                expectedName,
                                moveName,
                                lineNumber));
                        }
                        else if (!moveName.Equals(expectedName, StringComparison.Ordinal)) {
                            // Names match case-insensitively but differ in case - not a true mismatch
                            // Don't add to NameMismatches since it's just a case difference
                        }
                        entry.MoveName = expectedName;
                    }

                    // Validate Type (column 2)
                    string typeStr = parts[2].Trim();
                    if (typeNameToId.TryGetValue(typeStr, out int typeId)) {
                        entry.MoveType = (PokemonType)typeId;
                    } else {
                        result.Errors.Add(new MoveImportError(lineNumber, $"Unknown type '{typeStr}'. Valid types: {string.Join(", ", typeNames.Where(t => !string.IsNullOrEmpty(t)))}"));
                    }

                    // Validate Split (column 3)
                    string splitStr = parts[3].Trim();
                    if (splitNameToEnum.TryGetValue(splitStr, out MoveData.MoveSplit split)) {
                        entry.Split = split;
                    } else {
                        result.Errors.Add(new MoveImportError(lineNumber, $"Unknown split '{splitStr}'. Valid values: PHYSICAL, SPECIAL, STATUS"));
                    }

                    // Validate Power (column 4)
                    if (!byte.TryParse(parts[4].Trim(), out byte power)) {
                        result.Errors.Add(new MoveImportError(lineNumber, $"Invalid power '{parts[4]}'. Must be 0-255."));
                    } else {
                        entry.Power = power;
                    }

                    // Validate Accuracy (column 5)
                    if (!byte.TryParse(parts[5].Trim(), out byte accuracy)) {
                        result.Errors.Add(new MoveImportError(lineNumber, $"Invalid accuracy '{parts[5]}'. Must be 0-255."));
                    } else {
                        entry.Accuracy = accuracy;
                    }

                    // Validate Priority (column 6)
                    if (!sbyte.TryParse(parts[6].Trim(), out sbyte priority)) {
                        result.Errors.Add(new MoveImportError(lineNumber, $"Invalid priority '{parts[6]}'. Must be -128 to 127."));
                    } else {
                        entry.Priority = priority;
                    }

                    // Validate Side Effect Probability (column 7)
                    if (!byte.TryParse(parts[7].Trim(), out byte sideEffect)) {
                        result.Errors.Add(new MoveImportError(lineNumber, $"Invalid side effect probability '{parts[7]}'. Must be 0-255."));
                    } else {
                        entry.SideEffectProbability = sideEffect;
                    }

                    // Validate PP (column 8)
                    if (!byte.TryParse(parts[8].Trim(), out byte pp)) {
                        result.Errors.Add(new MoveImportError(lineNumber, $"Invalid PP '{parts[8]}'. Must be 0-255."));
                    } else {
                        entry.PP = pp;
                    }

                    // Validate Range (column 9)
                    string rangeStr = parts[9].Trim();
                    if (rangeNameToValue.TryGetValue(rangeStr, out ushort rangeValue)) {
                        entry.Range = rangeValue;
                    } else {
                        result.Errors.Add(new MoveImportError(lineNumber, 
                            $"Unknown range '{rangeStr}'. Valid values: {string.Join(", ", rangeNameToValue.Keys)}"));
                    }

                    result.Entry = entry;
                    result.IsValid = result.Errors.Count == 0;
                    return result;
                }

                private void ApplyImportedData(List<MoveDataImportEntry> importedEntries) {
                    int savedCount = 0;

                    foreach (var entry in importedEntries) {
                        try {
                            // Load the existing move data to preserve fields not in the CSV
                            MoveData move = new MoveData(entry.MoveID);

                            // Update only the fields from the CSV
                            move.movetype = entry.MoveType;
                            move.split = entry.Split;
                            move.damage = entry.Power;
                            move.accuracy = entry.Accuracy;
                            move.priority = entry.Priority;
                            move.sideEffectProbability = entry.SideEffectProbability;
                            move.pp = entry.PP;
                            move.target = entry.Range;

                            // Save to file
                            move.SaveToFileDefaultDir(entry.MoveID, showSuccessMessage: false);
                            savedCount++;
                        } catch (Exception ex) {
                            AppLogger.Error($"Failed to save move {entry.MoveID}: {ex.Message}");
                        }
                    }

                    // Reload the current move if it was modified
                    if (importedEntries.Any(e => e.MoveID == currentLoadedId)) {
                        ChangeLoadedFile(currentLoadedId);
                    }

                    MessageBox.Show($"Successfully imported and saved {savedCount} move(s).",
                        "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                private string ToTitleCase(string input) {
                    if (string.IsNullOrEmpty(input)) return input;
                    return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower());
                }

                private void ApplyMoveRenames(List<MoveNameMismatch> renames, bool useTitleCase) {
                    if (renames == null || renames.Count == 0) return;

                    try {
                        // Get the text archive ID for Move names from RomInfo
                        int moveNamesTextArchiveId = RomInfo.attackNamesTextNumber;

                        // Load the text archive containing Move names
                        var textArchive = new ROMFiles.TextArchive(moveNamesTextArchiveId);

                        int renamedCount = 0;
                        foreach (var rename in renames) {
                            if (rename.MoveId >= 0 && rename.MoveId < textArchive.messages.Count) {
                                string newName = useTitleCase ? ToTitleCase(rename.CsvName) : rename.CsvName;
                                textArchive.messages[rename.MoveId] = newName;

                                // Also update local array for immediate UI refresh
                                if (rename.MoveId < fileNames.Length) {
                                    fileNames[rename.MoveId] = newName;
                                }
                                renamedCount++;
                                AppLogger.Info($"Renamed move {rename.MoveId}: '{rename.RomName}' -> '{newName}'");
                            }
                        }

                        // Save the text archive
                        textArchive.SaveToExpandedDir(moveNamesTextArchiveId, showSuccessMessage: false);

                        // Refresh the combo box
                        disableHandlers = true;
                        int currentIndex = moveNameInputComboBox.SelectedIndex;
                        moveNameInputComboBox.Items.Clear();
                        moveNameInputComboBox.Items.AddRange(fileNames);
                        moveNameInputComboBox.SelectedIndex = currentIndex;
                        disableHandlers = false;

                        MessageBox.Show($"Successfully renamed {renamedCount} move(s) in ROM.",
                            "Rename Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    } catch (Exception ex) {
                        AppLogger.Error($"Failed to apply move renames: {ex.Message}");
                        MessageBox.Show($"Failed to apply move renames: {ex.Message}",
                            "Rename Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                #endregion
            }

            #region Move Import Support Classes
            public class MoveImportError {
                public int LineNumber { get; }
                public string Message { get; }

                public MoveImportError(int lineNumber, string message) {
                    LineNumber = lineNumber;
                    Message = message;
                }

                public override string ToString() => LineNumber > 0 ? $"Line {LineNumber}: {Message}" : Message;
            }

            public class MoveImportWarning {
                public int LineNumber { get; }
                public string Message { get; }

                public MoveImportWarning(int lineNumber, string message) {
                    LineNumber = lineNumber;
                    Message = message;
                }

                public override string ToString() => LineNumber > 0 ? $"Line {LineNumber}: {Message}" : Message;
            }

            public class MoveNameMismatch {
                public int MoveId { get; }
                public string RomName { get; }
                public string CsvName { get; }
                public int LineNumber { get; }

                public MoveNameMismatch(int moveId, string romName, string csvName, int lineNumber) {
                    MoveId = moveId;
                    RomName = romName;
                    CsvName = csvName;
                    LineNumber = lineNumber;
                }

                public override string ToString() => 
                    $"Move ID {MoveId}: ROM has '{RomName}', CSV has '{CsvName}'";
            }

            public class MoveDataImportEntry {
                public int MoveID { get; set; }
                public string MoveName { get; set; }
                public PokemonType MoveType { get; set; }
                public MoveData.MoveSplit Split { get; set; }
                public byte Power { get; set; }
                public byte Accuracy { get; set; }
                public sbyte Priority { get; set; }
                public byte SideEffectProbability { get; set; }
                public byte PP { get; set; }
                public ushort Range { get; set; }
            }

            public class MoveRowValidationResult {
                public int LineNumber { get; set; }
                public MoveDataImportEntry Entry { get; set; }
                public List<MoveImportError> Errors { get; set; } = new List<MoveImportError>();
                public List<MoveImportWarning> Warnings { get; set; } = new List<MoveImportWarning>();
                public List<MoveNameMismatch> NameMismatches { get; set; } = new List<MoveNameMismatch>();
                public bool IsValid { get; set; }
            }

            public class MoveDataImportResult {
                public List<MoveDataImportEntry> ValidEntries { get; set; } = new List<MoveDataImportEntry>();
                public List<MoveImportError> Errors { get; set; } = new List<MoveImportError>();
                public List<MoveImportWarning> Warnings { get; set; } = new List<MoveImportWarning>();
                public List<MoveNameMismatch> NameMismatches { get; set; } = new List<MoveNameMismatch>();
                public int TotalRowsRead { get; set; }

                public bool HasErrors => Errors.Count > 0;
                public bool HasWarnings => Warnings.Count > 0;
                public bool HasNameMismatches => NameMismatches.Count > 0;
                public int ValidCount => ValidEntries.Count;
                public int ErrorCount => Errors.Count;

                /// <summary>
                /// Gets unique move name mismatches (by move ID, taking first occurrence)
                /// </summary>
                public List<MoveNameMismatch> UniqueNameMismatches => 
                    NameMismatches
                        .GroupBy(m => m.MoveId)
                        .Select(g => g.First())
                        .ToList();
            }

            public class MoveDataImportPreviewForm : Form {
            private TabControl tabControl;
            private TextBox txtSummary;
            private TextBox txtErrors;
            private TextBox txtChanges;
            private TextBox txtValidValues;
            private TextBox txtNameMismatches;
            private CheckedListBox chkMoveRenames;
            private CheckBox chkUseTitleCase;
            private Button btnApply;
            private Button btnCancel;
            private MoveDataImportResult importResult;
            private string[] moveNames;
            private string[] typeNames;
            private bool hasViewedErrorsTab = false;
            private int errorsTabIndex = 1; // Index of the Errors & Warnings tab

            public MoveDataImportPreviewForm(MoveDataImportResult result, string[] moveNames, string[] typeNames) {
                this.importResult = result;
                this.moveNames = moveNames;
                this.typeNames = typeNames;
                InitializeComponent();
                PopulateData();
            }

            private void InitializeComponent() {
                this.Size = new System.Drawing.Size(800, 600);
                this.Text = "Move Data Import Preview";
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.StartPosition = FormStartPosition.CenterParent;
                this.MinimumSize = new System.Drawing.Size(600, 400);

                var mainLayout = new TableLayoutPanel {
                    Dock = DockStyle.Fill,
                    RowCount = 2,
                    ColumnCount = 1,
                    Padding = new Padding(10)
                };
                mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

                tabControl = new TabControl { Dock = DockStyle.Fill };

                var summaryTab = new TabPage("Summary");
                txtSummary = new TextBox {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    Font = new System.Drawing.Font("Consolas", 10f),
                    WordWrap = false
                };
                summaryTab.Controls.Add(txtSummary);

                var errorsTab = new TabPage("Errors & Warnings");
                txtErrors = new TextBox {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    Font = new System.Drawing.Font("Consolas", 10f),
                    WordWrap = false,
                    ForeColor = System.Drawing.Color.DarkRed
                };
                errorsTab.Controls.Add(txtErrors);

                var changesTab = new TabPage("Changes Preview");
                txtChanges = new TextBox {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    Font = new System.Drawing.Font("Consolas", 9f),
                    WordWrap = false
                };
                changesTab.Controls.Add(txtChanges);

                // Name Mismatches tab (new)
                var nameMismatchesTab = new TabPage("Name Mismatches");
                var mismatchLayout = new TableLayoutPanel {
                    Dock = DockStyle.Fill,
                    RowCount = 3,
                    ColumnCount = 1
                };
                mismatchLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
                mismatchLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                mismatchLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

                txtNameMismatches = new TextBox {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    Font = new System.Drawing.Font("Consolas", 9f),
                    WordWrap = true
                };

                var moveGroup = new GroupBox { Text = "Move Name Mismatches (check to rename in ROM)", Dock = DockStyle.Fill };
                chkMoveRenames = new CheckedListBox { Dock = DockStyle.Fill };
                moveGroup.Controls.Add(chkMoveRenames);

                var optionsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
                chkUseTitleCase = new CheckBox { Text = "Convert names to Title Case", Checked = true, AutoSize = true };
                optionsPanel.Controls.Add(chkUseTitleCase);

                mismatchLayout.Controls.Add(txtNameMismatches, 0, 0);
                mismatchLayout.Controls.Add(moveGroup, 0, 1);
                mismatchLayout.Controls.Add(optionsPanel, 0, 2);
                nameMismatchesTab.Controls.Add(mismatchLayout);

                var validValuesTab = new TabPage("Valid Values Reference");
                txtValidValues = new TextBox {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    Font = new System.Drawing.Font("Consolas", 9f),
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

                var buttonPanel = new FlowLayoutPanel {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.RightToLeft,
                    Padding = new Padding(0, 10, 0, 0)
                };

                btnCancel = new Button {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Size = new System.Drawing.Size(100, 30)
                };

                btnApply = new Button {
                    Text = "Apply Changes",
                    Size = new System.Drawing.Size(120, 30)
                };
                btnApply.Click += BtnApply_Click;

                buttonPanel.Controls.AddRange(new Control[] { btnCancel, btnApply });

                mainLayout.Controls.Add(tabControl, 0, 0);
                mainLayout.Controls.Add(buttonPanel, 0, 1);

                this.Controls.Add(mainLayout);
                this.AcceptButton = btnApply;
                this.CancelButton = btnCancel;
            }

            private string ToTitleCase(string input) {
                if (string.IsNullOrEmpty(input)) return input;
                return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower());
            }

            private void PopulateData() {
                var summary = new StringBuilder();
                var errors = new StringBuilder();
                var changes = new StringBuilder();
                var validValues = new StringBuilder();
                var nameMismatches = new StringBuilder();

                // Load current move data from ROM for comparison
                var changedMoves = new List<(MoveDataImportEntry imported, MoveData current)>();
                var unchangedCount = 0;

                foreach (var entry in importResult.ValidEntries) {
                    try {
                        MoveData currentMove = new MoveData(entry.MoveID);
                            
                        // Compare all fields
                        bool hasChanges = 
                            currentMove.movetype != entry.MoveType ||
                            currentMove.split != entry.Split ||
                            currentMove.damage != entry.Power ||
                            currentMove.accuracy != entry.Accuracy ||
                            currentMove.priority != entry.Priority ||
                            currentMove.sideEffectProbability != entry.SideEffectProbability ||
                            currentMove.pp != entry.PP ||
                            currentMove.target != entry.Range;
                            
                        if (hasChanges) {
                            changedMoves.Add((entry, currentMove));
                        } else {
                            unchangedCount++;
                        }
                    } catch {
                        // If we can't load current data, assume it's changed
                        changedMoves.Add((entry, null));
                    }
                }

                // Summary
                summary.AppendLine("═══════════════════════════════════════════════════════════════");
                summary.AppendLine("                   MOVE DATA IMPORT VALIDATION SUMMARY");
                summary.AppendLine("═══════════════════════════════════════════════════════════════");
                summary.AppendLine();
                summary.AppendLine($"  Total rows read:     {importResult.TotalRowsRead}");
                summary.AppendLine($"  Valid entries:       {importResult.ValidCount}");
                summary.AppendLine($"  Moves with changes:  {changedMoves.Count}");
                summary.AppendLine($"  Moves unchanged:     {unchangedCount}");
                summary.AppendLine($"  Errors found:        {importResult.ErrorCount}");
                summary.AppendLine($"  Warnings:            {importResult.Warnings.Count}");
                summary.AppendLine($"  Name mismatches:     {importResult.UniqueNameMismatches.Count}");
                summary.AppendLine();

                if (importResult.HasErrors) {
                    summary.AppendLine("  ⚠️  ERRORS FOUND - Please review the 'Errors & Warnings' tab");
                    summary.AppendLine("      Some entries could not be imported due to validation errors.");
                    summary.AppendLine("      Check the 'Valid Values Reference' tab for accepted values.");
                } else if (changedMoves.Count == 0) {
                    summary.AppendLine("  ✓  No changes detected - import data matches current ROM data.");
                } else {
                    summary.AppendLine("  ✓  All entries validated successfully.");
                }

                if (importResult.Warnings.Count > 0) {
                    summary.AppendLine($"  ⚠️  {importResult.Warnings.Count} warning(s) - some data was auto-corrected.");
                }

                if (importResult.HasNameMismatches) {
                    summary.AppendLine($"  📝  {importResult.UniqueNameMismatches.Count} name mismatch(es) detected.");
                    summary.AppendLine("      Check the 'Name Mismatches' tab to optionally rename in ROM.");
                }

                summary.AppendLine();
                summary.AppendLine("═══════════════════════════════════════════════════════════════");

                // Errors and warnings
                if (importResult.HasErrors || importResult.Warnings.Count > 0) {
                    if (importResult.HasErrors) {
                        errors.AppendLine("══════════════════════════════════════════════════════════════");
                        errors.AppendLine("                          ERRORS");
                        errors.AppendLine("══════════════════════════════════════════════════════════════");
                        errors.AppendLine();
                        foreach (var error in importResult.Errors) {
                            errors.AppendLine($"  ✗ {error}");
                        }
                        errors.AppendLine();
                    }

                    if (importResult.Warnings.Count > 0) {
                        errors.AppendLine("══════════════════════════════════════════════════════════════");
                        errors.AppendLine("                         WARNINGS");
                        errors.AppendLine("══════════════════════════════════════════════════════════════");
                        errors.AppendLine();
                        foreach (var warning in importResult.Warnings) {
                            errors.AppendLine($"  ⚠ {warning}");
                        }
                    }
                } else {
                    errors.AppendLine("No errors or warnings found.");
                }

                // Name Mismatches tab
                if (importResult.HasNameMismatches) {
                    nameMismatches.AppendLine("Name mismatches detected between CSV and ROM data.");
                    nameMismatches.AppendLine("Check the items below to rename them in the ROM.");
                    nameMismatches.AppendLine("Note: Only mismatches with DIFFERENT text (not just case) are shown.");

                    // Populate Move name mismatches checklist
                    foreach (var mismatch in importResult.UniqueNameMismatches) {
                        chkMoveRenames.Items.Add(mismatch, false);
                    }
                } else {
                    nameMismatches.AppendLine("No name mismatches detected.");
                    nameMismatches.AppendLine("All names in CSV match the ROM data (case-insensitive comparison).");
                }

                // Changes preview - only show actual changes
                changes.AppendLine("═══════════════════════════════════════════════════════════════");
                changes.AppendLine("                    MOVES TO BE MODIFIED");
                changes.AppendLine("═══════════════════════════════════════════════════════════════");
                changes.AppendLine();

                if (changedMoves.Count == 0) {
                    changes.AppendLine("  ✓  No changes detected - import data matches current ROM data.");
                } else {
                    changes.AppendLine($"The following {changedMoves.Count} move(s) will be updated:");
                    changes.AppendLine();

                    foreach (var (entry, current) in changedMoves.OrderBy(x => x.imported.MoveID)) {
                        string typeName = (int)entry.MoveType < typeNames.Length ? typeNames[(int)entry.MoveType] : "???";
                        string rangeName = MoveData.GetAttackRangeName(entry.Range);
                            
                        changes.AppendLine($"───────────────────────────────────────────────────────────────");
                        changes.AppendLine($"  [{entry.MoveID:D3}] {entry.MoveName}");
                        changes.AppendLine($"───────────────────────────────────────────────────────────────");

                        if (current != null) {
                            // Show field-by-field changes
                            if (current.movetype != entry.MoveType) {
                                string oldType = (int)current.movetype < typeNames.Length ? typeNames[(int)current.movetype] : "???";
                                changes.AppendLine($"    Type:     {oldType} → {typeName}");
                            }
                            if (current.split != entry.Split) {
                                changes.AppendLine($"    Split:    {current.split} → {entry.Split}");
                            }
                            if (current.damage != entry.Power) {
                                changes.AppendLine($"    Power:    {current.damage} → {entry.Power}");
                            }
                            if (current.accuracy != entry.Accuracy) {
                                changes.AppendLine($"    Accuracy: {current.accuracy} → {entry.Accuracy}");
                            }
                            if (current.priority != entry.Priority) {
                                changes.AppendLine($"    Priority: {current.priority} → {entry.Priority}");
                            }
                            if (current.sideEffectProbability != entry.SideEffectProbability) {
                                changes.AppendLine($"    Effect %: {current.sideEffectProbability} → {entry.SideEffectProbability}");
                            }
                            if (current.pp != entry.PP) {
                                changes.AppendLine($"    PP:       {current.pp} → {entry.PP}");
                            }
                            if (current.target != entry.Range) {
                                string oldRange = MoveData.GetAttackRangeName(current.target);
                                changes.AppendLine($"    Range:    {oldRange} → {rangeName}");
                            }
                        } else {
                            // No current data available, show all new values
                            changes.AppendLine($"    Type: {typeName}, Split: {entry.Split}, Power: {entry.Power}");
                            changes.AppendLine($"    Accuracy: {entry.Accuracy}, Priority: {entry.Priority}, PP: {entry.PP}");
                            changes.AppendLine($"    Effect %: {entry.SideEffectProbability}, Range: {rangeName}");
                        }
                        changes.AppendLine();
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
                validValues.AppendLine("  COLUMN: Move ID");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine($"  Range: 0 to {moveNames.Length - 1}");
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: Move Name");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine($"  Total: {moveNames.Length} Moves (case-insensitive)");
                validValues.AppendLine();
                // Show a few random examples instead of full list
                var moveExamples = new[] { 1, 10, 100 }.Where(i => i < moveNames.Length).ToList();
                validValues.AppendLine("  Examples:");
                foreach (var i in moveExamples) {
                    validValues.AppendLine($"    {i,4}: {moveNames[i]}");
                }
                validValues.AppendLine();
                validValues.AppendLine($"  Any move name from ROM data is valid.");
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: Move Type (case-insensitive)");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                for (int i = 0; i < typeNames.Length; i++) {
                    if (!string.IsNullOrEmpty(typeNames[i])) {
                        validValues.AppendLine($"    {typeNames[i]}");
                    }
                }
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: Move Split (case-insensitive)");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("    PHYSICAL");
                validValues.AppendLine("    SPECIAL");
                validValues.AppendLine("    STATUS");
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: Power");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  Range: 0 to 255");
                validValues.AppendLine("  Note: 0 = No damage (usually for status moves)");
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: Accuracy");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  Range: 0 to 255");
                validValues.AppendLine("  Note: 0 = Never misses (e.g., Swift)");
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: Priority");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  Range: -128 to 127");
                validValues.AppendLine("  Note: Higher = moves first. Most moves are 0.");
                validValues.AppendLine("        Quick Attack = +1, Protect = +4, etc.");
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: Side Effect Probability");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  Range: 0 to 255");
                validValues.AppendLine("  Note: Percentage chance for secondary effect to trigger");
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: PP");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  Range: 0 to 255");
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: Range (Attack Target) (case-insensitive)");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                foreach (var range in MoveData.AttackRangeDescriptions) {
                    validValues.AppendLine($"    {range.name,-25} - {range.description}");
                }

                txtSummary.Text = summary.ToString();
                txtErrors.Text = errors.ToString();
                txtChanges.Text = changes.ToString();
                txtValidValues.Text = validValues.ToString();
                txtNameMismatches.Text = nameMismatches.ToString();

                // Update tab colors based on content
                if (importResult.HasErrors) {
                    txtErrors.ForeColor = System.Drawing.Color.DarkRed;
                } else if (importResult.Warnings.Count > 0) {
                    txtErrors.ForeColor = System.Drawing.Color.DarkOrange;
                } else {
                    txtErrors.ForeColor = System.Drawing.Color.DarkGreen;
                }
            }

                /// <summary>
                /// Gets the list of move names to be renamed after import
                /// </summary>
                public List<MoveNameMismatch> SelectedMoveRenames { get; private set; } = new List<MoveNameMismatch>();

                /// <summary>
                /// Gets whether the user wants to use title case for renamed names
                /// </summary>
                public bool UseTitleCase { get; private set; }

                private void BtnApply_Click(object sender, EventArgs e) {
                    if (importResult.ValidCount == 0) {
                        MessageBox.Show("No valid entries to import.", "Import Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // If there are errors and user hasn't viewed the errors tab, redirect them there first
                    if (importResult.HasErrors && !hasViewedErrorsTab) {
                        MessageBox.Show("Let's at least open the Errors tab first, no?", "Review Errors",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        tabControl.SelectedIndex = errorsTabIndex;
                        hasViewedErrorsTab = true;
                        return;
                    }

                    // Collect selected move renames
                    SelectedMoveRenames.Clear();
                    foreach (var item in chkMoveRenames.CheckedItems) {
                        if (item is MoveNameMismatch mismatch) {
                            SelectedMoveRenames.Add(mismatch);
                        }
                    }
                    UseTitleCase = chkUseTitleCase.Checked;

                    var confirmMessage = $"This will modify {importResult.ValidCount} move(s) in the ROM.\n\n";

                    if (SelectedMoveRenames.Count > 0) {
                        confirmMessage += $"📝  Additionally, {SelectedMoveRenames.Count} move name(s) will be renamed in the ROM.\n\n";
                    }

                    if (importResult.HasErrors) {
                        confirmMessage += $"⚠️ Warning: {importResult.ErrorCount} rows had errors and will be skipped.\n\n";
                    }

                    confirmMessage += "Are you sure you want to proceed?";

                    var result = MessageBox.Show(confirmMessage, "Confirm Import",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (result == DialogResult.Yes) {
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                }
            }
            #endregion
        }
