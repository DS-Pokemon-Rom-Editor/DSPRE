using DSPRE.Resources;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace DSPRE {
    public partial class PersonalDataEditor : Form {

        private readonly string[] fileNames;
        private readonly string[] pokenames;
        private readonly string[] machineMoveNames;
        private readonly string[] typeNames;
        private readonly string[] abilityNames;
        private readonly string[] itemNames;

        // Lookup dictionaries for import validation
        private Dictionary<string, int> typeNameToId;
        private Dictionary<string, int> abilityNameToId;
        private Dictionary<string, int> itemNameToId;
        private Dictionary<string, PokemonGrowthCurve> growthCurveNameToEnum;
        private Dictionary<string, PokemonDexColor> dexColorNameToEnum;
        private Dictionary<string, byte> eggGroupNameToId;

        private int currentLoadedId = 0;
        private PokemonPersonalData currentLoadedFile = null;

        public bool dirty = false;
        private bool modifiedAbilities = false;
        private static readonly string formName = "Personal Data Editor";

        PokemonEditor _parent;

        public PersonalDataEditor(string[] itemNames, string[] abilityNames, System.Windows.Forms.Control parent, PokemonEditor pokeEditor) {
            this.fileNames = RomInfo.GetPokemonNames().ToArray();;
            this.machineMoveNames = TMEditor.ReadMachineMoveNames().ToArray();
            this._parent = pokeEditor;
            this.typeNames = RomInfo.GetTypeNames();
            this.abilityNames = abilityNames;
            this.itemNames = itemNames;

            // Build lookup dictionaries for import validation
            BuildLookupDictionaries();

            InitializeComponent();
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Size = parent.Size;
            this.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
            Helpers.DisableHandlers();
            ScriptDatabase.InitializeMoveNamesIfNeeded();
            BindingList<string> listItemNames = new BindingList<string>(itemNames);
            item1InputComboBox.DataSource = new BindingSource(listItemNames, string.Empty);
            item2InputComboBox.DataSource = new BindingSource(listItemNames, string.Empty);

            BindingList<string> listTypeNames = new BindingList<string>(RomInfo.GetTypeNames());
            type1InputComboBox.DataSource = new BindingSource(listTypeNames, string.Empty);
            type2InputComboBox.DataSource = new BindingSource(listTypeNames, string.Empty);

            BindingList<string> listAbilityNames = new BindingList<string>(abilityNames);
            ability1InputComboBox.DataSource = new BindingSource(listAbilityNames, string.Empty);
            ability2InputComboBox.DataSource = new BindingSource(listAbilityNames, string.Empty);

            BindingList<string> listEggGroups = new BindingList<string>(Enum.GetNames(typeof(PokemonEggGroup)));
            eggGroup1InputCombobox.DataSource = new BindingSource(listEggGroups, string.Empty);
            eggGroup2InputCombobox.DataSource = new BindingSource(listEggGroups, string.Empty);

            growthCurveInputComboBox.Items.AddRange(Enum.GetNames(typeof(PokemonGrowthCurve)));
            
            dexColorInputComboBox.Items.AddRange(Enum.GetNames(typeof(PokemonDexColor)));


            /* ---------------- */
            int count = RomInfo.GetPersonalFilesCount();
            this.pokenames = RomInfo.GetPokemonNames();
            List<string> fileNames = new List<string>(count);
            fileNames.AddRange(pokenames);

            for (int i = 0; i < PokeDatabase.PersonalData.personalExtraFiles.Length; i++) {
                PokeDatabase.PersonalData.PersonalExtraFiles altFormEntry = PokeDatabase.PersonalData.personalExtraFiles[i];
                fileNames.Add(fileNames[altFormEntry.monId] + " - " + altFormEntry.description);
            }

            int extraEntries = fileNames.Count;
            for (int i = 0; i < count - extraEntries; i++) {
                fileNames.Add($"Extra entry {fileNames.Count}");
            }
            
            this.fileNames = fileNames.ToArray();
            monNumberNumericUpDown.Maximum = fileNames.Count - 1;
            pokemonNameInputComboBox.Items.AddRange(this.fileNames);
            hatchResultComboBox.DataSource = fileNames.ToArray();
            /* ---------------- */

            Helpers.EnableHandlers();

            pokemonNameInputComboBox.SelectedIndex = 1;
        }

        private void exportCsvButton_Click(object sender, EventArgs e) {
            ExportToCSV();
        }

        private void importCsvButton_Click(object sender, EventArgs e) {
            ImportFromCSV();
        }

        private void setDirty(bool status) {
            if (status) {
                dirty = true;
                this.Text = formName + "*";
            } else {
                dirty = false;
                this.Text = formName;
            }
            _parent.UpdateTabPageNames();
        }
        private void baseHpNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.baseHP = (byte)baseHpNumericUpDown.Value;
            setDirty(true);
        }

        private void baseAtkNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.baseAtk = (byte)baseAtkNumericUpDown.Value;
            setDirty(true);
        }
        private void baseDefNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.baseDef = (byte)baseDefNumericUpDown.Value;
            setDirty(true);
        }

        private void baseSpAtkNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.baseSpAtk = (byte)baseSpAtkNumericUpDown.Value;
            setDirty(true);
        }

        private void baseSpDefNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.baseSpDef = (byte)baseSpDefNumericUpDown.Value;
            setDirty(true);
        }

        private void baseSpeedNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.baseSpeed = (byte)baseSpeedNumericUpDown.Value;
            setDirty(true);
        }

        private void evHpNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.evHP = (byte)evHpNumericUpDown.Value;
            setDirty(true);
        }

        private void evAtkNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.evAtk = (byte)evAtkNumericUpDown.Value;
            setDirty(true);
        }

        private void evDefNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.evDef = (byte)evDefNumericUpDown.Value;
            setDirty(true);
        }

        private void evSpAtkNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.evSpAtk = (byte)evSpAtkNumericUpDown.Value;
            setDirty(true);
        }

        private void evSpDefNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.evSpDef = (byte)evSpDefNumericUpDown.Value;
            setDirty(true);
        }

        private void evSpeedNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.evSpeed = (byte)evSpeedNumericUpDown.Value;
            setDirty(true);
        }


        private void type1InputComboBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.type1 = (PokemonType)type1InputComboBox.SelectedIndex;
            setDirty(true);
        }

        private void type2InputComboBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.type2 = (PokemonType)type2InputComboBox.SelectedIndex;
            setDirty(true);
        }

        private void growthCurveInputComboBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.growthCurve = (PokemonGrowthCurve)growthCurveInputComboBox.SelectedIndex;
            setDirty(true);
        }

        private void baseExpYieldNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.givenExp = (byte)baseExpYieldNumericUpDown.Value;
            setDirty(true);
        }

        private void dexColorInputComboBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.color = (PokemonDexColor)dexColorInputComboBox.SelectedIndex;
            setDirty(true);
        }

        private void flipFlagCheckBox_CheckedChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.flip = flipFlagCheckBox.Checked;
            setDirty(true);
        }

        private void escapeRateNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.escapeRate = (byte)escapeRateNumericUpDown.Value;
            setDirty(true);
        }

        private void catchRateNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.catchRate = (byte)catchRateNumericUpDown.Value;
            setDirty(true);
        }

        private void genderProbabilityNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.genderVec = (byte)genderProbabilityNumericUpDown.Value;
            genderLabel.Text = GetGenderText(currentLoadedFile.genderVec);

            setDirty(true);
        }

        private string GetGenderText(int vec) {
            switch (vec) {
                case (byte)PokemonGender.Male:
                case (byte)PokemonGender.Female:
                    return $"100% {Enum.GetName(typeof(PokemonGender), vec)}";
                case (byte)PokemonGender.Unknown:
                    return "Gender Unknown";
                default: 
                    {
                        vec++;
                        float femaleProb = 100 * ((float)vec / 256);
                        return $"{100 - femaleProb}% Male\n\n{femaleProb}% Female";
                    }
            }
        }

        private void ability1InputComboBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.firstAbility = (byte)ability1InputComboBox.SelectedIndex;
            setDirty(true);
            modifiedAbilities = true;
        }
        private void ability2InputComboBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.secondAbility = (byte)ability2InputComboBox.SelectedIndex;
            setDirty(true);
            modifiedAbilities = true;
        }
        private void eggGroup1InputCombobox_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.eggGroup1 = (byte)eggGroup1InputCombobox.SelectedIndex;
            setDirty(true);
        }

        private void eggGroup2InputCombobox_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.eggGroup2 = (byte)eggGroup2InputCombobox.SelectedIndex;
            setDirty(true);
        }

        private void eggStepsNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.eggSteps = (byte)eggStepsNumericUpDown.Value;
            setDirty(true);
        }

        private void item1InputComboBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.item1 = (ushort)item1InputComboBox.SelectedIndex;
            setDirty(true);
        }

        private void item2InputComboBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.item2 = (ushort)item2InputComboBox.SelectedIndex;
            setDirty(true);
        }

        private void baseFriendshipNumericUpDown_ValueChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            currentLoadedFile.baseFriendship = (byte)baseFriendshipNumericUpDown.Value;
            setDirty(true);
        }


        private void addMachineButton_Click(object sender, EventArgs e) {
            int elemAdd = addableMachinesListBox.SelectedIndex;
            if (elemAdd < 0) {
                return;
            }
            int id = ZeroBasedIndexFromMachineName((string)addableMachinesListBox.SelectedItem);

            currentLoadedFile.machines.Add((byte)id);

            RebuildMachinesListBoxes(false, true);

            int count = addableMachinesListBox.Items.Count;
            if (count > 0) {
                addableMachinesListBox.SelectedIndex = Math.Min(count-1, elemAdd);
            }
            setDirty(true);
        }

        private void removeMachineButton_Click(object sender, EventArgs e) {
            int elemRemove = addedMachinesListBox.SelectedIndex;
            if (elemRemove < 0) {
                return;
            }
            int id = ZeroBasedIndexFromMachineName((string)addedMachinesListBox.SelectedItem);
            currentLoadedFile.machines.Remove((byte)id);

            RebuildMachinesListBoxes(true, false);

            int count = addedMachinesListBox.Items.Count;
            if (count > 0) {
                addedMachinesListBox.SelectedIndex = Math.Max(0, elemRemove - 1);
            }
            setDirty(true);
        }

        private void addAllMachinesButton_Click(object sender, EventArgs e) {
            int tot = PokemonPersonalData.tmsCount + PokemonPersonalData.hmsCount;
            if (currentLoadedFile.machines.Count == tot) {
                return;
            }

            currentLoadedFile.machines = new SortedSet<byte>();
            for (byte i = 0; i < tot; i++) {
                currentLoadedFile.machines.Add(i);
            }
            RebuildMachinesListBoxes();
            setDirty(true);
        }

        private void removeAllMachinesButton_Click(object sender, EventArgs e) {
            if (currentLoadedFile.machines.Count == 0) {
                return;
            }
            currentLoadedFile.machines.Clear();
            RebuildMachinesListBoxes();
            setDirty(true);
        }
        private void saveDataButton_Click(object sender, EventArgs e) {
            currentLoadedFile.SaveToFileDefaultDir(currentLoadedId, true);
            WriteHatchResult(currentLoadedId);
            //if (modifiedAbilities) {
            //    EditorPanels.MainProgram.RefreshAbilities(currentLoadedId);
            //    modifiedAbilities = false;
            //}
            setDirty(false);
        }
        //-------------------------------
        public bool CheckDiscardChanges() {
            if (!dirty) {
                return true;
            }

            DialogResult res = MessageBox.Show("Personal Editor\nThere are unsaved changes to the current Personal data.\nDiscard and proceed?", "Personal Editor - Unsaved changes", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res.Equals(DialogResult.Yes)) {
                return true;
            }

            monNumberNumericUpDown.Value = currentLoadedId;
            pokemonNameInputComboBox.SelectedIndex = currentLoadedId;


            return false;
        }

        private void pokemonNameInputComboBox_SelectedIndexChanged(object sender, EventArgs e) {
            Update();
            if (Helpers.HandlersDisabled) {
                return;
            }
            this._parent.TrySyncIndices((System.Windows.Forms.ComboBox)sender);
            Helpers.DisableHandlers();
            if (CheckDiscardChanges()) {
                int newNumber = pokemonNameInputComboBox.SelectedIndex;
                monNumberNumericUpDown.Value = newNumber;
                ChangeLoadedFile(newNumber);
            }
            Helpers.EnableHandlers();
        }

        private void monNumberNumericUpDown_ValueChanged(object sender, EventArgs e) {
            Update();
            if (Helpers.HandlersDisabled) {
                return;
            }
            this._parent.TrySyncIndices((NumericUpDown)sender);
            Helpers.DisableHandlers();
            if (CheckDiscardChanges()) {
                int newNumber = (int)monNumberNumericUpDown.Value;
                pokemonNameInputComboBox.SelectedIndex = newNumber;
                ChangeLoadedFile(newNumber);
            }
            Helpers.EnableHandlers();
        }


        private void hatchResultComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {

            if (Helpers.HandlersDisabled)
            {
                return;
            }

            setDirty(true);
        }

        public void ChangeLoadedFile(int toLoad) {
            currentLoadedId = toLoad;
            currentLoadedFile = new PokemonPersonalData(currentLoadedId);

            baseHpNumericUpDown.Value = currentLoadedFile.baseHP;
            baseAtkNumericUpDown.Value = currentLoadedFile.baseAtk;
            baseDefNumericUpDown.Value = currentLoadedFile.baseDef;
            baseSpeedNumericUpDown.Value = currentLoadedFile.baseSpeed;
            baseSpAtkNumericUpDown.Value = currentLoadedFile.baseSpAtk;
            baseSpDefNumericUpDown.Value = currentLoadedFile.baseSpDef;

            type1InputComboBox.SelectedIndex = (byte)currentLoadedFile.type1;
            type2InputComboBox.SelectedIndex = (byte)currentLoadedFile.type2;

            catchRateNumericUpDown.Value = currentLoadedFile.catchRate;
            baseExpYieldNumericUpDown.Value = currentLoadedFile.givenExp;

            evHpNumericUpDown.Value = currentLoadedFile.evHP;
            evAtkNumericUpDown.Value = currentLoadedFile.evAtk;
            evDefNumericUpDown.Value = currentLoadedFile.evDef;
            evSpeedNumericUpDown.Value = currentLoadedFile.evSpeed;
            evSpAtkNumericUpDown.Value = currentLoadedFile.evSpAtk;
            evSpDefNumericUpDown.Value = currentLoadedFile.evSpDef;

            item1InputComboBox.SelectedIndex = currentLoadedFile.item1;
            item2InputComboBox.SelectedIndex = currentLoadedFile.item2;

            genderProbabilityNumericUpDown.Value = currentLoadedFile.genderVec;
            eggStepsNumericUpDown.Value = currentLoadedFile.eggSteps;
            baseFriendshipNumericUpDown.Value = currentLoadedFile.baseFriendship;
            growthCurveInputComboBox.SelectedIndex = (byte)currentLoadedFile.growthCurve;

            eggGroup1InputCombobox.SelectedIndex = currentLoadedFile.eggGroup1;
            eggGroup2InputCombobox.SelectedIndex = currentLoadedFile.eggGroup2;
            hatchResultComboBox.SelectedIndex = GetHatchResult(currentLoadedId);

            ability1InputComboBox.SelectedIndex = currentLoadedFile.firstAbility;
            ability2InputComboBox.SelectedIndex = currentLoadedFile.secondAbility;
            escapeRateNumericUpDown.Value = currentLoadedFile.escapeRate;

            dexColorInputComboBox.SelectedIndex = (byte)currentLoadedFile.color;
            flipFlagCheckBox.Checked = currentLoadedFile.flip;

            genderLabel.Text = GetGenderText(currentLoadedFile.genderVec);
            RebuildMachinesListBoxes();

            int excess = toLoad - pokenames.Length;
            try {
                if (excess >= 0) {
                    toLoad = PokeDatabase.PersonalData.personalExtraFiles[excess].iconId;
                }
            } catch (IndexOutOfRangeException) {
                toLoad = 0;
            } finally {
                pokemonPictureBox.Image = DSUtils.GetPokePic(toLoad, pokemonPictureBox.Width, pokemonPictureBox.Height);
            }
            setDirty(false);
        }

        private void RebuildMachinesListBoxes(bool keepAddableSelection = true, bool keepAddedSelection = true) {
            addableMachinesListBox.BeginUpdate();
            addedMachinesListBox.BeginUpdate();

            string addableSel = null;
            if (keepAddableSelection) {
                addableSel = (string)addableMachinesListBox.SelectedItem;
            }
            string addedSel = null;
            if (keepAddedSelection) {
                addedSel = (string)addableMachinesListBox.SelectedItem;
            }

            addedMachinesListBox.Items.Clear();
            addableMachinesListBox.Items.Clear();

            int dataIndex = 0;
            byte tot = (byte)(PokemonPersonalData.tmsCount + PokemonPersonalData.hmsCount);
            for (byte i = 0; i < tot; i++) {
                
                string machineLabel = TMEditor.MachineLabelFromIndex(i);
                string machineMoveName = machineMoveNames.Length > i ? machineMoveNames[i] : $"UNK_{i}";
                string currentItem = $"{machineLabel} - {machineMoveName}";

                if (dataIndex < currentLoadedFile.machines.Count && currentLoadedFile.machines.Contains(i)) {
                    addedMachinesListBox.Items.Add(currentItem);
                    dataIndex++;
                } else {
                    addableMachinesListBox.Items.Add(currentItem);
                }
            }

            addableMachinesListBox.EndUpdate();
            addedMachinesListBox.EndUpdate();

            if (keepAddableSelection) { 
                int addableCount = addableMachinesListBox.Items.Count;
                if (addableCount > 0) {
                    addableMachinesListBox.SelectedItem = addableSel;
                }
            }

            int addedCount = addedMachinesListBox.Items.Count;
            if (addedCount > 0) {
                addedMachinesListBox.SelectedItem = addedSel;
            }
        }

        private int GetHatchResult(int monID)
        {
            if (monID < 0) {
                return 0;
            }

            // Open PMS file to find the hatch result
            // This isn't a narc despite the name, it's a binary file. It's also in the same location for all games and languages.
            FileStream stream = new FileStream(Path.Combine(RomInfo.dataPath, @"poketool/personal/pms.narc"), FileMode.Open);

            using (BinaryReader reader = new BinaryReader(stream)) 
            {
                // Each entry is 2 bytes long
                int offset = monID * 2;
                if (offset + 1 > stream.Length) {
                    return 0; // Out of bounds
                }
                stream.Seek(offset, SeekOrigin.Begin);
                ushort hatchResult = reader.ReadUInt16();
                stream.Close();
                return hatchResult;
            }
        }

        private void WriteHatchResult(int monID) {
            if (monID < 0) {
                return;
            }
            // Open PMS file to write the hatch result
            FileStream stream = new FileStream(Path.Combine(RomInfo.dataPath, @"poketool/personal/pms.narc"), FileMode.Open);
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                // Each entry is 2 bytes long
                int offset = monID * 2;
                if (offset + 1 > stream.Length)
                {
                    return; // Out of bounds
                }
                stream.Seek(offset, SeekOrigin.Begin);
                writer.Write((ushort)hatchResultComboBox.SelectedIndex);
                stream.Close();
            }
        }

        private static int ZeroBasedIndexFromMachineName(string machineName)
                {
                    // Split the machineName to get the prefix (TM or HM) and the number
                    // Format: "TM01 - Focus Punch" or "HM01 - Cut"
                    var parts = machineName.Split('-');
                    var machineLabel = parts[0].Trim(); // "TMXX" or "HMXX"

                    int machineIndex = -1;

                    if (machineLabel.StartsWith("TM"))
                    {
                        machineIndex = int.Parse(machineLabel.Substring(2)) - 1;
                    }
                    else if (machineLabel.StartsWith("HM"))
                    {
                        machineIndex = int.Parse(machineLabel.Substring(2)) + PokemonPersonalData.tmsCount - 1;
                    }

                    return machineIndex;

                }

                #region Import/Export Methods
                private void BuildLookupDictionaries() {
                    // Build type name -> ID lookup (case-insensitive)
                    typeNameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < typeNames.Length; i++) {
                        if (!string.IsNullOrEmpty(typeNames[i]) && !typeNameToId.ContainsKey(typeNames[i])) {
                            typeNameToId[typeNames[i]] = i;
                        }
                    }

                    // Build ability name -> ID lookup (case-insensitive)
                    // Include ALL ability names including index 0 which might be " -" or similar for "None"
                    abilityNameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < abilityNames.Length; i++) {
                        string abilityName = abilityNames[i];
                        // Add the ability name as-is (even if it's " -" for None)
                        if (!string.IsNullOrEmpty(abilityName) && !abilityNameToId.ContainsKey(abilityName)) {
                            abilityNameToId[abilityName] = i;
                        }
                        // Also add trimmed version as fallback for CSV parsing
                        string trimmedName = abilityName?.Trim();
                        if (!string.IsNullOrEmpty(trimmedName) && !abilityNameToId.ContainsKey(trimmedName)) {
                            abilityNameToId[trimmedName] = i;
                        }
                    }

                    // Build item name -> ID lookup (case-insensitive)
                    // Include ALL item names including index 0 which might be "----" or similar for "None"
                    itemNameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < itemNames.Length; i++) {
                        string itemName = itemNames[i];
                        if (!string.IsNullOrEmpty(itemName) && !itemNameToId.ContainsKey(itemName)) {
                            itemNameToId[itemName] = i;
                        }
                        // Also add trimmed version as fallback for CSV parsing
                        string trimmedName = itemName?.Trim();
                        if (!string.IsNullOrEmpty(trimmedName) && !itemNameToId.ContainsKey(trimmedName)) {
                            itemNameToId[trimmedName] = i;
                        }
                    }

                    // Build growth curve name -> enum lookup (case-insensitive)
                    growthCurveNameToEnum = new Dictionary<string, PokemonGrowthCurve>(StringComparer.OrdinalIgnoreCase);
                    foreach (PokemonGrowthCurve curve in Enum.GetValues(typeof(PokemonGrowthCurve))) {
                        growthCurveNameToEnum[curve.ToString()] = curve;
                    }

                    // Build dex color name -> enum lookup (case-insensitive)
                    dexColorNameToEnum = new Dictionary<string, PokemonDexColor>(StringComparer.OrdinalIgnoreCase);
                    foreach (PokemonDexColor color in Enum.GetValues(typeof(PokemonDexColor))) {
                        dexColorNameToEnum[color.ToString()] = color;
                    }

                    // Build egg group name -> ID lookup (case-insensitive)
                    eggGroupNameToId = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
                    foreach (PokemonEggGroup group in Enum.GetValues(typeof(PokemonEggGroup))) {
                        eggGroupNameToId[group.ToString()] = (byte)group;
                    }
                }

                public void ExportToCSV() {
                    using (var saveDialog = new SaveFileDialog()) {
                        saveDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                        saveDialog.DefaultExt = "csv";
                        saveDialog.FileName = "PersonalData.csv";
                        saveDialog.Title = "Export Personal Data to CSV";

                        if (saveDialog.ShowDialog() == DialogResult.OK) {
                            try {
                                using (var writer = new StreamWriter(saveDialog.FileName)) {
                                    // Write header
                                    writer.WriteLine("Pokemon ID,Pokemon Name,Type 1,Type 2,Base HP,Base Atk,Base Def,Base SpAtk,Base SpDef,Base Speed," +
                                        "EV HP,EV Atk,EV Def,EV SpAtk,EV SpDef,EV Speed," +
                                        "Ability 1,Ability 2,Item 1,Item 2," +
                                        "Catch Rate,Base Exp,Gender Ratio,Egg Steps,Base Friendship,Growth Curve," +
                                        "Egg Group 1,Egg Group 2,Escape Rate,Dex Color,Flip");

                                    for (int i = 0; i < RomInfo.GetPersonalFilesCount(); i++) {
                                        PokemonPersonalData data = new PokemonPersonalData(i);
                                        string pokeName = i < fileNames.Length ? fileNames[i] : $"Pokemon_{i}";

                                        string type1Str = (int)data.type1 < typeNames.Length ? typeNames[(int)data.type1] : data.type1.ToString();
                                        string type2Str = (int)data.type2 < typeNames.Length ? typeNames[(int)data.type2] : data.type2.ToString();
                                        string ability1Str = data.firstAbility < abilityNames.Length ? abilityNames[data.firstAbility] : $"Ability_{data.firstAbility}";
                                        string ability2Str = data.secondAbility < abilityNames.Length ? abilityNames[data.secondAbility] : $"Ability_{data.secondAbility}";
                                        string item1Str = data.item1 < itemNames.Length ? itemNames[data.item1] : $"Item_{data.item1}";
                                        string item2Str = data.item2 < itemNames.Length ? itemNames[data.item2] : $"Item_{data.item2}";

                                        writer.WriteLine($"{i},{pokeName},{type1Str},{type2Str}," +
                                            $"{data.baseHP},{data.baseAtk},{data.baseDef},{data.baseSpAtk},{data.baseSpDef},{data.baseSpeed}," +
                                            $"{data.evHP},{data.evAtk},{data.evDef},{data.evSpAtk},{data.evSpDef},{data.evSpeed}," +
                                            $"{ability1Str},{ability2Str},{item1Str},{item2Str}," +
                                            $"{data.catchRate},{data.givenExp},{data.genderVec},{data.eggSteps},{data.baseFriendship},{data.growthCurve}," +
                                            $"{(PokemonEggGroup)data.eggGroup1},{(PokemonEggGroup)data.eggGroup2},{data.escapeRate},{data.color},{data.flip}");
                                    }
                                }

                                MessageBox.Show($"Personal data exported successfully to:\n{saveDialog.FileName}",
                                    "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            } catch (Exception ex) {
                                MessageBox.Show($"Error exporting data: {ex.Message}", "Export Error",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                }

                public void ImportFromCSV() {
                    using (var openDialog = new OpenFileDialog()) {
                        openDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                        openDialog.DefaultExt = "csv";
                        openDialog.Title = "Import Personal Data from CSV";

                        if (openDialog.ShowDialog() == DialogResult.OK) {
                            var importResult = ValidateAndParseCSV(openDialog.FileName);

                            // Show the import preview dialog
                            using (var previewForm = new PersonalDataImportPreviewForm(importResult, fileNames, typeNames, abilityNames)) {
                                if (previewForm.ShowDialog() == DialogResult.OK) {
                                    // Apply the changes
                                    ApplyImportedData(importResult.ValidEntries);
                                }
                            }
                        }
                    }
                }

                private PersonalDataImportResult ValidateAndParseCSV(string filePath) {
                    var result = new PersonalDataImportResult();

                    try {
                        var lines = File.ReadAllLines(filePath);

                        if (lines.Length == 0) {
                            result.Errors.Add(new PersonalImportError(0, "File is empty."));
                            return result;
                        }

                        // Validate header
                        var header = lines[0].Split(',');
                        if (header.Length < 31 ||
                            !header[0].Trim().Equals("Pokemon ID", StringComparison.OrdinalIgnoreCase)) {
                            result.Errors.Add(new PersonalImportError(1, $"Invalid header. Expected 31 columns starting with 'Pokemon ID'. Got: '{lines[0]}'"));
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

                            if (parts.Length < 31) {
                                result.Errors.Add(new PersonalImportError(lineNumber, $"Invalid number of columns. Expected 31, got {parts.Length}."));
                                continue;
                            }

                            var rowResult = ValidateRow(lineNumber, parts);

                            result.Warnings.AddRange(rowResult.Warnings);

                            if (rowResult.IsValid) {
                                result.ValidEntries.Add(rowResult.Entry);
                            } else {
                                result.Errors.AddRange(rowResult.Errors);
                            }
                        }
                    } catch (Exception ex) {
                        result.Errors.Add(new PersonalImportError(0, $"Failed to read file: {ex.Message}"));
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

                private PersonalRowValidationResult ValidateRow(int lineNumber, string[] parts) {
                    var result = new PersonalRowValidationResult { LineNumber = lineNumber };
                    var entry = new PersonalDataImportEntry();
                    int maxPokemonId = RomInfo.GetPersonalFilesCount() - 1;

                    // Column 0: Pokemon ID
                    if (!int.TryParse(parts[0].Trim(), out int pokemonId)) {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Invalid Pokemon ID '{parts[0]}'. Must be a number."));
                    } else if (pokemonId < 0 || pokemonId > maxPokemonId) {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Pokemon ID {pokemonId} is out of range. Valid range: 0-{maxPokemonId}"));
                    } else {
                        entry.PokemonID = pokemonId;
                    }

                    // Column 1: Pokemon Name (for reference, not used)
                    entry.PokemonName = parts[1].Trim();

                    // Column 2: Type 1
                    if (typeNameToId.TryGetValue(parts[2].Trim(), out int type1Id)) {
                        entry.Type1 = (PokemonType)type1Id;
                    } else {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Unknown Type 1 '{parts[2]}'."));
                    }

                    // Column 3: Type 2
                    if (typeNameToId.TryGetValue(parts[3].Trim(), out int type2Id)) {
                        entry.Type2 = (PokemonType)type2Id;
                    } else {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Unknown Type 2 '{parts[3]}'."));
                    }

                    // Columns 4-9: Base Stats
                    if (!byte.TryParse(parts[4].Trim(), out byte baseHP)) {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Invalid Base HP '{parts[4]}'."));
                    } else { entry.BaseHP = baseHP; }

                    if (!byte.TryParse(parts[5].Trim(), out byte baseAtk)) {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Invalid Base Atk '{parts[5]}'."));
                    } else { entry.BaseAtk = baseAtk; }

                    if (!byte.TryParse(parts[6].Trim(), out byte baseDef)) {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Invalid Base Def '{parts[6]}'."));
                    } else { entry.BaseDef = baseDef; }

                    if (!byte.TryParse(parts[7].Trim(), out byte baseSpAtk)) {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Invalid Base SpAtk '{parts[7]}'."));
                    } else { entry.BaseSpAtk = baseSpAtk; }

                    if (!byte.TryParse(parts[8].Trim(), out byte baseSpDef)) {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Invalid Base SpDef '{parts[8]}'."));
                    } else { entry.BaseSpDef = baseSpDef; }

                    if (!byte.TryParse(parts[9].Trim(), out byte baseSpeed)) {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Invalid Base Speed '{parts[9]}'."));
                    } else { entry.BaseSpeed = baseSpeed; }

                    // Columns 10-15: EV Yields (0-3 each)
                    if (!byte.TryParse(parts[10].Trim(), out byte evHP) || evHP > 3) {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Invalid EV HP '{parts[10]}'. Must be 0-3."));
                    } else { entry.EvHP = evHP; }

                    if (!byte.TryParse(parts[11].Trim(), out byte evAtk) || evAtk > 3) {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Invalid EV Atk '{parts[11]}'. Must be 0-3."));
                    } else { entry.EvAtk = evAtk; }

                    if (!byte.TryParse(parts[12].Trim(), out byte evDef) || evDef > 3) {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Invalid EV Def '{parts[12]}'. Must be 0-3."));
                    } else { entry.EvDef = evDef; }

                    if (!byte.TryParse(parts[13].Trim(), out byte evSpAtk) || evSpAtk > 3) {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Invalid EV SpAtk '{parts[13]}'. Must be 0-3."));
                    } else { entry.EvSpAtk = evSpAtk; }

                    if (!byte.TryParse(parts[14].Trim(), out byte evSpDef) || evSpDef > 3) {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Invalid EV SpDef '{parts[14]}'. Must be 0-3."));
                    } else { entry.EvSpDef = evSpDef; }

                    if (!byte.TryParse(parts[15].Trim(), out byte evSpeed) || evSpeed > 3) {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Invalid EV Speed '{parts[15]}'. Must be 0-3."));
                    } else { entry.EvSpeed = evSpeed; }

                    // Columns 16-17: Abilities
                    if (abilityNameToId.TryGetValue(parts[16].Trim(), out int ability1Id)) {
                        entry.Ability1 = (byte)ability1Id;
                    } else {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Unknown Ability 1 '{parts[16]}'."));
                    }

                    if (abilityNameToId.TryGetValue(parts[17].Trim(), out int ability2Id)) {
                        entry.Ability2 = (byte)ability2Id;
                    } else {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Unknown Ability 2 '{parts[17]}'."));
                    }

                    // Columns 18-19: Held Items
                    if (itemNameToId.TryGetValue(parts[18].Trim(), out int item1Id)) {
                        entry.Item1 = (ushort)item1Id;
                    } else {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Unknown Item 1 '{parts[18]}'."));
                    }

                    if (itemNameToId.TryGetValue(parts[19].Trim(), out int item2Id)) {
                        entry.Item2 = (ushort)item2Id;
                    } else {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Unknown Item 2 '{parts[19]}'."));
                    }

                    // Columns 20-24: Misc numeric fields
                    if (!byte.TryParse(parts[20].Trim(), out byte catchRate)) {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Invalid Catch Rate '{parts[20]}'."));
                    } else { entry.CatchRate = catchRate; }

                    if (!byte.TryParse(parts[21].Trim(), out byte baseExp)) {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Invalid Base Exp '{parts[21]}'."));
                    } else { entry.BaseExp = baseExp; }

                    if (!byte.TryParse(parts[22].Trim(), out byte genderRatio)) {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Invalid Gender Ratio '{parts[22]}'."));
                    } else { entry.GenderRatio = genderRatio; }

                    if (!byte.TryParse(parts[23].Trim(), out byte eggSteps)) {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Invalid Egg Steps '{parts[23]}'."));
                    } else { entry.EggSteps = eggSteps; }

                    if (!byte.TryParse(parts[24].Trim(), out byte baseFriendship)) {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Invalid Base Friendship '{parts[24]}'."));
                    } else { entry.BaseFriendship = baseFriendship; }

                    // Column 25: Growth Curve
                    if (growthCurveNameToEnum.TryGetValue(parts[25].Trim(), out PokemonGrowthCurve growthCurve)) {
                        entry.GrowthCurve = growthCurve;
                    } else {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Unknown Growth Curve '{parts[25]}'."));
                    }

                    // Columns 26-27: Egg Groups
                    if (eggGroupNameToId.TryGetValue(parts[26].Trim(), out byte eggGroup1)) {
                        entry.EggGroup1 = eggGroup1;
                    } else {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Unknown Egg Group 1 '{parts[26]}'."));
                    }

                    if (eggGroupNameToId.TryGetValue(parts[27].Trim(), out byte eggGroup2)) {
                        entry.EggGroup2 = eggGroup2;
                    } else {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Unknown Egg Group 2 '{parts[27]}'."));
                    }

                    // Column 28: Escape Rate
                    if (!byte.TryParse(parts[28].Trim(), out byte escapeRate)) {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Invalid Escape Rate '{parts[28]}'."));
                    } else { entry.EscapeRate = escapeRate; }

                    // Column 29: Dex Color
                    if (dexColorNameToEnum.TryGetValue(parts[29].Trim(), out PokemonDexColor dexColor)) {
                        entry.DexColor = dexColor;
                    } else {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Unknown Dex Color '{parts[29]}'."));
                    }

                    // Column 30: Flip
                    if (bool.TryParse(parts[30].Trim(), out bool flip)) {
                        entry.Flip = flip;
                    } else {
                        result.Errors.Add(new PersonalImportError(lineNumber, $"Invalid Flip value '{parts[30]}'. Must be True or False."));
                    }

                    result.Entry = entry;
                    result.IsValid = result.Errors.Count == 0;
                    return result;
                }

                private void ApplyImportedData(List<PersonalDataImportEntry> importedEntries) {
                    int savedCount = 0;

                    foreach (var entry in importedEntries) {
                        try {
                            // Load the existing data to preserve TM/HM compatibility (not in CSV)
                            PokemonPersonalData data = new PokemonPersonalData(entry.PokemonID);

                            // Update fields from CSV
                            data.type1 = entry.Type1;
                            data.type2 = entry.Type2;
                            data.baseHP = entry.BaseHP;
                            data.baseAtk = entry.BaseAtk;
                            data.baseDef = entry.BaseDef;
                            data.baseSpAtk = entry.BaseSpAtk;
                            data.baseSpDef = entry.BaseSpDef;
                            data.baseSpeed = entry.BaseSpeed;
                            data.evHP = entry.EvHP;
                            data.evAtk = entry.EvAtk;
                            data.evDef = entry.EvDef;
                            data.evSpAtk = entry.EvSpAtk;
                            data.evSpDef = entry.EvSpDef;
                            data.evSpeed = entry.EvSpeed;
                            data.firstAbility = entry.Ability1;
                            data.secondAbility = entry.Ability2;
                            data.item1 = entry.Item1;
                            data.item2 = entry.Item2;
                            data.catchRate = entry.CatchRate;
                            data.givenExp = entry.BaseExp;
                            data.genderVec = entry.GenderRatio;
                            data.eggSteps = entry.EggSteps;
                            data.baseFriendship = entry.BaseFriendship;
                            data.growthCurve = entry.GrowthCurve;
                            data.eggGroup1 = entry.EggGroup1;
                            data.eggGroup2 = entry.EggGroup2;
                            data.escapeRate = entry.EscapeRate;
                            data.color = entry.DexColor;
                            data.flip = entry.Flip;

                            // Save to file
                            data.SaveToFileDefaultDir(entry.PokemonID, showSuccessMessage: false);
                            savedCount++;
                        } catch (Exception ex) {
                            AppLogger.Error($"Failed to save Pokemon {entry.PokemonID}: {ex.Message}");
                        }
                    }

                    // Reload the current Pokemon if it was modified
                    if (importedEntries.Any(e => e.PokemonID == currentLoadedId)) {
                        ChangeLoadedFile(currentLoadedId);
                    }

                    MessageBox.Show($"Successfully imported and saved {savedCount} Pokemon personal data entries.",
                        "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                #endregion
            }

            #region Personal Data Import Support Classes
            public class PersonalImportError {
                public int LineNumber { get; }
                public string Message { get; }

                public PersonalImportError(int lineNumber, string message) {
                    LineNumber = lineNumber;
                    Message = message;
                }

                public override string ToString() => LineNumber > 0 ? $"Line {LineNumber}: {Message}" : Message;
            }

            public class PersonalImportWarning {
                public int LineNumber { get; }
                public string Message { get; }

                public PersonalImportWarning(int lineNumber, string message) {
                    LineNumber = lineNumber;
                    Message = message;
                }

                public override string ToString() => LineNumber > 0 ? $"Line {LineNumber}: {Message}" : Message;
            }

            public class PersonalDataImportEntry {
                public int PokemonID { get; set; }
                public string PokemonName { get; set; }
                public PokemonType Type1 { get; set; }
                public PokemonType Type2 { get; set; }
                public byte BaseHP { get; set; }
                public byte BaseAtk { get; set; }
                public byte BaseDef { get; set; }
                public byte BaseSpAtk { get; set; }
                public byte BaseSpDef { get; set; }
                public byte BaseSpeed { get; set; }
                public byte EvHP { get; set; }
                public byte EvAtk { get; set; }
                public byte EvDef { get; set; }
                public byte EvSpAtk { get; set; }
                public byte EvSpDef { get; set; }
                public byte EvSpeed { get; set; }
                public byte Ability1 { get; set; }
                public byte Ability2 { get; set; }
                public ushort Item1 { get; set; }
                public ushort Item2 { get; set; }
                public byte CatchRate { get; set; }
                public byte BaseExp { get; set; }
                public byte GenderRatio { get; set; }
                public byte EggSteps { get; set; }
                public byte BaseFriendship { get; set; }
                public PokemonGrowthCurve GrowthCurve { get; set; }
                public byte EggGroup1 { get; set; }
                public byte EggGroup2 { get; set; }
                public byte EscapeRate { get; set; }
                public PokemonDexColor DexColor { get; set; }
                public bool Flip { get; set; }
            }

            public class PersonalRowValidationResult {
                public int LineNumber { get; set; }
                public PersonalDataImportEntry Entry { get; set; }
                public List<PersonalImportError> Errors { get; set; } = new List<PersonalImportError>();
                public List<PersonalImportWarning> Warnings { get; set; } = new List<PersonalImportWarning>();
                public bool IsValid { get; set; }
            }

            public class PersonalDataImportResult {
                public List<PersonalDataImportEntry> ValidEntries { get; set; } = new List<PersonalDataImportEntry>();
                public List<PersonalImportError> Errors { get; set; } = new List<PersonalImportError>();
                public List<PersonalImportWarning> Warnings { get; set; } = new List<PersonalImportWarning>();
                public int TotalRowsRead { get; set; }

                public bool HasErrors => Errors.Count > 0;
                public bool HasWarnings => Warnings.Count > 0;
                public int ValidCount => ValidEntries.Count;
                public int ErrorCount => Errors.Count;
            }

            public class PersonalDataImportPreviewForm : Form {
            private TabControl tabControl;
            private TextBox txtSummary;
            private TextBox txtErrors;
            private TextBox txtChanges;
            private TextBox txtValidValues;
            private Button btnApply;
            private Button btnCancel;
            private PersonalDataImportResult importResult;
            private string[] pokemonNames;
            private string[] typeNames;
            private string[] abilityNames;
            private bool hasViewedErrorsTab = false;
            private int errorsTabIndex = 1; // Index of the Errors & Warnings tab

            public PersonalDataImportPreviewForm(PersonalDataImportResult result, string[] pokemonNames, string[] typeNames, string[] abilityNames) {
                this.importResult = result;
                this.pokemonNames = pokemonNames;
                this.typeNames = typeNames;
                this.abilityNames = abilityNames;
                InitializeComponent();
                PopulateData();
            }

            private void InitializeComponent() {
                this.Size = new Size(900, 650);
                this.Text = "Personal Data Import Preview";
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.StartPosition = FormStartPosition.CenterParent;
                this.MinimumSize = new Size(700, 500);

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
                    Font = new Font("Consolas", 10f),
                    WordWrap = false
                };
                summaryTab.Controls.Add(txtSummary);

                var errorsTab = new TabPage("Errors & Warnings");
                txtErrors = new TextBox {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    Font = new Font("Consolas", 10f),
                    WordWrap = false,
                    ForeColor = Color.DarkRed
                };
                errorsTab.Controls.Add(txtErrors);

                var changesTab = new TabPage("Changes Preview");
                txtChanges = new TextBox {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    Font = new Font("Consolas", 9f),
                    WordWrap = false
                };
                changesTab.Controls.Add(txtChanges);

                var validValuesTab = new TabPage("Valid Values Reference");
                txtValidValues = new TextBox {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    Font = new Font("Consolas", 9f),
                    WordWrap = false
                };
                validValuesTab.Controls.Add(txtValidValues);

                tabControl.TabPages.AddRange(new TabPage[] { summaryTab, errorsTab, changesTab, validValuesTab });

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
                    Size = new Size(100, 30)
                };

                btnApply = new Button {
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

            private void PopulateData() {
                var summary = new StringBuilder();
                var errors = new StringBuilder();
                var changes = new StringBuilder();
                var validValues = new StringBuilder();

                // Load current data from ROM for comparison
                var changedPokemon = new List<(PersonalDataImportEntry imported, PokemonPersonalData current, List<string> changedFields)>();
                var unchangedCount = 0;

                foreach (var entry in importResult.ValidEntries) {
                    try {
                        PokemonPersonalData current = new PokemonPersonalData(entry.PokemonID);
                        var changedFields = new List<string>();

                        // Compare all fields
                        if (current.type1 != entry.Type1) changedFields.Add($"Type1: {typeNames[(int)current.type1]} → {typeNames[(int)entry.Type1]}");
                        if (current.type2 != entry.Type2) changedFields.Add($"Type2: {typeNames[(int)current.type2]} → {typeNames[(int)entry.Type2]}");
                        if (current.baseHP != entry.BaseHP) changedFields.Add($"BaseHP: {current.baseHP} → {entry.BaseHP}");
                        if (current.baseAtk != entry.BaseAtk) changedFields.Add($"BaseAtk: {current.baseAtk} → {entry.BaseAtk}");
                        if (current.baseDef != entry.BaseDef) changedFields.Add($"BaseDef: {current.baseDef} → {entry.BaseDef}");
                        if (current.baseSpAtk != entry.BaseSpAtk) changedFields.Add($"BaseSpAtk: {current.baseSpAtk} → {entry.BaseSpAtk}");
                        if (current.baseSpDef != entry.BaseSpDef) changedFields.Add($"BaseSpDef: {current.baseSpDef} → {entry.BaseSpDef}");
                        if (current.baseSpeed != entry.BaseSpeed) changedFields.Add($"BaseSpeed: {current.baseSpeed} → {entry.BaseSpeed}");
                        if (current.evHP != entry.EvHP) changedFields.Add($"EvHP: {current.evHP} → {entry.EvHP}");
                        if (current.evAtk != entry.EvAtk) changedFields.Add($"EvAtk: {current.evAtk} → {entry.EvAtk}");
                        if (current.evDef != entry.EvDef) changedFields.Add($"EvDef: {current.evDef} → {entry.EvDef}");
                        if (current.evSpAtk != entry.EvSpAtk) changedFields.Add($"EvSpAtk: {current.evSpAtk} → {entry.EvSpAtk}");
                        if (current.evSpDef != entry.EvSpDef) changedFields.Add($"EvSpDef: {current.evSpDef} → {entry.EvSpDef}");
                        if (current.evSpeed != entry.EvSpeed) changedFields.Add($"EvSpeed: {current.evSpeed} → {entry.EvSpeed}");
                        if (current.firstAbility != entry.Ability1) changedFields.Add($"Ability1: {abilityNames[current.firstAbility]} → {abilityNames[entry.Ability1]}");
                        if (current.secondAbility != entry.Ability2) changedFields.Add($"Ability2: {abilityNames[current.secondAbility]} → {abilityNames[entry.Ability2]}");
                        if (current.item1 != entry.Item1) changedFields.Add($"Item1: {current.item1} → {entry.Item1}");
                        if (current.item2 != entry.Item2) changedFields.Add($"Item2: {current.item2} → {entry.Item2}");
                        if (current.catchRate != entry.CatchRate) changedFields.Add($"CatchRate: {current.catchRate} → {entry.CatchRate}");
                        if (current.givenExp != entry.BaseExp) changedFields.Add($"BaseExp: {current.givenExp} → {entry.BaseExp}");
                        if (current.genderVec != entry.GenderRatio) changedFields.Add($"GenderRatio: {current.genderVec} → {entry.GenderRatio}");
                        if (current.eggSteps != entry.EggSteps) changedFields.Add($"EggSteps: {current.eggSteps} → {entry.EggSteps}");
                        if (current.baseFriendship != entry.BaseFriendship) changedFields.Add($"BaseFriendship: {current.baseFriendship} → {entry.BaseFriendship}");
                        if (current.growthCurve != entry.GrowthCurve) changedFields.Add($"GrowthCurve: {current.growthCurve} → {entry.GrowthCurve}");
                        if (current.eggGroup1 != entry.EggGroup1) changedFields.Add($"EggGroup1: {(PokemonEggGroup)current.eggGroup1} → {(PokemonEggGroup)entry.EggGroup1}");
                        if (current.eggGroup2 != entry.EggGroup2) changedFields.Add($"EggGroup2: {(PokemonEggGroup)current.eggGroup2} → {(PokemonEggGroup)entry.EggGroup2}");
                        if (current.escapeRate != entry.EscapeRate) changedFields.Add($"EscapeRate: {current.escapeRate} → {entry.EscapeRate}");
                        if (current.color != entry.DexColor) changedFields.Add($"DexColor: {current.color} → {entry.DexColor}");
                        if (current.flip != entry.Flip) changedFields.Add($"Flip: {current.flip} → {entry.Flip}");

                        if (changedFields.Count > 0) {
                            changedPokemon.Add((entry, current, changedFields));
                        } else {
                            unchangedCount++;
                        }
                    } catch {
                        changedPokemon.Add((entry, null, new List<string> { "Could not load current data" }));
                    }
                }

                // Summary
                summary.AppendLine("═══════════════════════════════════════════════════════════════");
                summary.AppendLine("               PERSONAL DATA IMPORT VALIDATION SUMMARY");
                summary.AppendLine("═══════════════════════════════════════════════════════════════");
                summary.AppendLine();
                summary.AppendLine($"  Total rows read:       {importResult.TotalRowsRead}");
                summary.AppendLine($"  Valid entries:         {importResult.ValidCount}");
                summary.AppendLine($"  Pokemon with changes:  {changedPokemon.Count}");
                summary.AppendLine($"  Pokemon unchanged:     {unchangedCount}");
                summary.AppendLine($"  Errors found:          {importResult.ErrorCount}");
                summary.AppendLine($"  Warnings:              {importResult.Warnings.Count}");
                summary.AppendLine();

                if (importResult.HasErrors) {
                    summary.AppendLine("  ⚠️  ERRORS FOUND - Please review the 'Errors & Warnings' tab");
                    summary.AppendLine("      Check the 'Valid Values Reference' tab for accepted values.");
                } else if (changedPokemon.Count == 0) {
                    summary.AppendLine("  ✓  No changes detected - import data matches current ROM data.");
                } else {
                    summary.AppendLine("  ✓  All entries validated successfully.");
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

                // Changes preview - only show actual changes
                changes.AppendLine("═══════════════════════════════════════════════════════════════");
                changes.AppendLine("                    POKEMON TO BE MODIFIED");
                changes.AppendLine("═══════════════════════════════════════════════════════════════");
                changes.AppendLine();

                if (changedPokemon.Count == 0) {
                    changes.AppendLine("  ✓  No changes detected - import data matches current ROM data.");
                } else {
                    changes.AppendLine($"The following {changedPokemon.Count} Pokemon will be updated:");
                    changes.AppendLine();

                    foreach (var (entry, current, changedFields) in changedPokemon.OrderBy(x => x.imported.PokemonID)) {
                        string pokeName = entry.PokemonID < pokemonNames.Length ? pokemonNames[entry.PokemonID] : $"Pokemon_{entry.PokemonID}";

                        changes.AppendLine($"───────────────────────────────────────────────────────────────");
                        changes.AppendLine($"  [{entry.PokemonID:D3}] {pokeName}");
                        changes.AppendLine($"───────────────────────────────────────────────────────────────");

                        foreach (var field in changedFields) {
                            changes.AppendLine($"    {field}");
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
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: Pokemon ID");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine($"  Range: 0 to {RomInfo.GetPersonalFilesCount() - 1}");
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: Type 1, Type 2");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                for (int i = 0; i < typeNames.Length; i++) {
                    if (!string.IsNullOrEmpty(typeNames[i])) {
                        validValues.AppendLine($"    {typeNames[i]}");
                    }
                }
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMNS: Base HP, Atk, Def, SpAtk, SpDef, Speed");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  Range: 0 to 255");
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMNS: EV HP, Atk, Def, SpAtk, SpDef, Speed");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  Range: 0 to 3");
                validValues.AppendLine("  Note: This is how many EVs the Pokemon gives when defeated");
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: Ability 1, Ability 2 (first 50 shown)");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                for (int i = 0; i < abilityNames.Length && i < 50; i++) {
                    if (!string.IsNullOrEmpty(abilityNames[i])) {
                        validValues.AppendLine($"    {abilityNames[i]}");
                    }
                }
                if (abilityNames.Length > 50) {
                    validValues.AppendLine($"    ... and {abilityNames.Length - 50} more");
                }
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMNS: Catch Rate, Base Exp, Escape Rate");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  Range: 0 to 255");
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: Gender Ratio");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  Range: 0 to 255");
                validValues.AppendLine("    0 = 100% Male");
                validValues.AppendLine("    254 = 100% Female");
                validValues.AppendLine("    255 = Genderless");
                validValues.AppendLine("    Other values = Female probability (value/256)");
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: Egg Steps, Base Friendship");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  Range: 0 to 255");
                validValues.AppendLine("  Note: Egg Steps is stored as a multiplier (actual steps = value * 256)");
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: Growth Curve");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                foreach (PokemonGrowthCurve curve in Enum.GetValues(typeof(PokemonGrowthCurve))) {
                    validValues.AppendLine($"    {curve}");
                }
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: Egg Group 1, Egg Group 2");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                foreach (PokemonEggGroup group in Enum.GetValues(typeof(PokemonEggGroup))) {
                    validValues.AppendLine($"    {group}");
                }
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: Dex Color");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                foreach (PokemonDexColor color in Enum.GetValues(typeof(PokemonDexColor))) {
                    validValues.AppendLine($"    {color}");
                }
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: Flip");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("    True");
                validValues.AppendLine("    False");
                validValues.AppendLine();
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                validValues.AppendLine("  COLUMN: Pokemon Name (first 50 shown)");
                validValues.AppendLine("───────────────────────────────────────────────────────────────");
                for (int i = 0; i < pokemonNames.Length && i < 50; i++) {
                    validValues.AppendLine($"    {i,4}: {pokemonNames[i]}");
                }
                if (pokemonNames.Length > 50) {
                    validValues.AppendLine($"    ... and {pokemonNames.Length - 50} more");
                }

                txtSummary.Text = summary.ToString();
                txtErrors.Text = errors.ToString();
                txtChanges.Text = changes.ToString();
                txtValidValues.Text = validValues.ToString();

                // Update tab colors based on content
                if (importResult.HasErrors) {
                    txtErrors.ForeColor = Color.DarkRed;
                } else if (importResult.Warnings.Count > 0) {
                    txtErrors.ForeColor = Color.DarkOrange;
                } else {
                    txtErrors.ForeColor = Color.DarkGreen;
                }
            }

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

                    var confirmMessage = $"This will modify {importResult.ValidCount} Pokemon personal data entries in the ROM.\n\n";

                    if (importResult.HasErrors) {
                        confirmMessage += $"⚠️ Warning: {importResult.ErrorCount} rows had errors and will be skipped.\n\n";
                    }

                    confirmMessage += "Note: TM/HM compatibility data will be preserved (not included in CSV).\n\n";
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
