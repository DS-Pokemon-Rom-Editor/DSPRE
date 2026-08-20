using System;
using System.IO;
using System.Windows.Forms;
using DSPRE.ROMFiles;

namespace DSPRE.Editors {
    public partial class BattleTowerEditor : Form, IEditorWithUnsavedChanges {
        private BattleTowerTrainerFile trainerFile;
        private BattleTowerPokemonSetFile setFile;
        private bool isDirty = false;

        #region IEditorWithUnsavedChanges Implementation
        public bool HasUnsavedChanges => isDirty;
        public string UnsavedChangesDescription => "Battle Tower Editor";
        public void SaveChanges() { SaveTrainers(); SaveSets(); }
        public void DiscardChanges() { isDirty = false; }
        #endregion

        private void SetDirty() { isDirty = true; }

        public BattleTowerEditor() {
            InitializeComponent();

            if (!BattleTowerTrainerFile.IsAvailable() || !BattleTowerPokemonSetFile.IsAvailable()) {
                MessageBox.Show("Battle Tower data was not found for this game.", "Not Available",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Load += (s, e) => Close();
                return;
            }

            DSUtils.TryUnpackNarcs(new System.Collections.Generic.List<RomInfo.DirNames> {
                RomInfo.DirNames.battleTowerTrainers, RomInfo.DirNames.battleTowerPokemon
            });
            DSUtils.TryUnpackNarcs(new System.Collections.Generic.List<RomInfo.DirNames> { RomInfo.DirNames.monIcons });
            RomInfo.SetMonIconsPalTableAddress();

            setFile = new BattleTowerPokemonSetFile(true);
            trainerFile = new BattleTowerTrainerFile(true);

            if (setFile.Sets.Count > 1) {
                addSetNumeric.Maximum = setFile.Sets.Count - 1;
            }

            PopulatePickers();
            RefreshTrainerList();
            RefreshSetList();
            UpdateAddSetPreview();
        }

        private void PopulatePickers() {
            Helpers.DisableHandlers();
            try {
                trainerClassCombo.Items.Clear();
                trainerClassCombo.Items.AddRange(RomInfo.GetTrainerClassNames());

                speciesCombo.Items.Clear();
                speciesCombo.Items.AddRange(RomInfo.GetPokemonNames());

                string[] moveNames = RomInfo.GetAttackNames();
                foreach (var box in new[] { move1Combo, move2Combo, move3Combo, move4Combo }) {
                    box.Items.Clear();
                    box.Items.AddRange(moveNames);
                }

                itemCombo.Items.Clear();
                itemCombo.Items.AddRange(RomInfo.GetItemNames());

                natureCombo.Items.Clear();
                natureCombo.Items.AddRange(BattleTowerPokemonSet.NatureNames);
            } finally {
                Helpers.EnableHandlers();
            }
        }

        #region Trainers tab
        private void RefreshTrainerList(int selectIndex = 0) {
            Helpers.DisableHandlers();
            try {
                trainerListBox.Items.Clear();
                for (int i = 0; i < trainerFile.Trainers.Count; i++) {
                    trainerListBox.Items.Add($"[{i:D3}] {trainerFile.Trainers[i]}");
                }
            } finally {
                Helpers.EnableHandlers();
            }
            if (trainerListBox.Items.Count > 0) {
                trainerListBox.SelectedIndex = Math.Min(selectIndex, trainerListBox.Items.Count - 1);
            }
        }

        private void buttonNewTrainer_Click(object sender, EventArgs e) {
            trainerFile.Trainers.Add(new BattleTowerTrainer());
            RefreshTrainerList(trainerFile.Trainers.Count - 1);
            SetDirty();
        }

        private BattleTowerTrainer SelectedTrainer =>
            (trainerListBox.SelectedIndex >= 0 && trainerListBox.SelectedIndex < trainerFile.Trainers.Count)
                ? trainerFile.Trainers[trainerListBox.SelectedIndex] : null;

        private void trainerListBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) return;
            BattleTowerTrainer trainer = SelectedTrainer;
            if (trainer == null) return;

            Helpers.DisableHandlers();
            try {
                trainerClassCombo.SelectedIndex = trainer.TrainerType < trainerClassCombo.Items.Count ? trainer.TrainerType : -1;
                trainerNameTextBox.Text = trainer.Name;
                message1TextBox.Text = trainer.Messages.Length > 0 ? trainer.Messages[0] : "";
                message2TextBox.Text = trainer.Messages.Length > 1 ? trainer.Messages[1] : "";
                message3TextBox.Text = trainer.Messages.Length > 2 ? trainer.Messages[2] : "";
                RefreshSetIdsList(trainer);
            } finally {
                Helpers.EnableHandlers();
            }
        }

        private void RefreshSetIdsList(BattleTowerTrainer trainer) {
            setIdsListBox.Items.Clear();
            foreach (ushort id in trainer.SetIDs) {
                setIdsListBox.Items.Add($"Set {id:D3}: {SetLabel(id)}");
            }
        }

        private string SetLabel(int id) => (id >= 0 && id < setFile.Sets.Count) ? setFile.Sets[id].ToString() : "?";

        private void trainerClassCombo_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) return;
            BattleTowerTrainer trainer = SelectedTrainer;
            if (trainer == null || trainerClassCombo.SelectedIndex < 0) return;
            trainer.TrainerType = (ushort)trainerClassCombo.SelectedIndex;
            SetDirty();
        }

        private void trainerNameTextBox_TextChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) return;
            BattleTowerTrainer trainer = SelectedTrainer;
            if (trainer == null) return;
            trainer.Name = trainerNameTextBox.Text;
            SetDirty();
        }

        private void messageTextBox_TextChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) return;
            BattleTowerTrainer trainer = SelectedTrainer;
            if (trainer == null) return;
            trainer.Messages[0] = message1TextBox.Text;
            trainer.Messages[1] = message2TextBox.Text;
            trainer.Messages[2] = message3TextBox.Text;
            SetDirty();
        }

        private void buttonAddSet_Click(object sender, EventArgs e) {
            BattleTowerTrainer trainer = SelectedTrainer;
            if (trainer == null) return;
            int setId = (int)addSetNumeric.Value;
            if (setId <= 0) return; // set 0 is the blank/unused placeholder entry
            trainer.SetIDs.Add((ushort)setId);
            RefreshSetIdsList(trainer);
            SetDirty();
        }

        private void addSetNumeric_ValueChanged(object sender, EventArgs e) => UpdateAddSetPreview();

        private void UpdateAddSetPreview() {
            addSetPreviewLabel.Text = $"({SetLabel((int)addSetNumeric.Value)})";
        }

        private void buttonRemoveSet_Click(object sender, EventArgs e) {
            BattleTowerTrainer trainer = SelectedTrainer;
            if (trainer == null || setIdsListBox.SelectedIndex < 0) return;
            trainer.SetIDs.RemoveAt(setIdsListBox.SelectedIndex);
            RefreshSetIdsList(trainer);
            SetDirty();
        }

        private void setIdsListBox_DoubleClick(object sender, EventArgs e) {
            BattleTowerTrainer trainer = SelectedTrainer;
            if (trainer == null || setIdsListBox.SelectedIndex < 0) return;
            int setId = trainer.SetIDs[setIdsListBox.SelectedIndex];
            if (setId >= 0 && setId < setListBox.Items.Count) {
                tabControl.SelectedTab = tabPageSets;
                setListBox.SelectedIndex = setId;
            }
        }

        private void SaveTrainers() {
            if (trainerFile == null) return;
            trainerFile.SaveToNarc();
        }

        private void buttonSaveTrainers_Click(object sender, EventArgs e) {
            SaveTrainers();
            isDirty = false;
        }

        private void buttonExportTrainers_Click(object sender, EventArgs e) {
            using (SaveFileDialog sfd = new SaveFileDialog {
                Filter = "Binary files (*.bin)|*.bin|All files (*.*)|*.*",
                DefaultExt = "bin",
                FileName = "battle_tower_trainers.bin"
            }) {
                if (sfd.ShowDialog() == DialogResult.OK) {
                    trainerFile.ExportToFile(sfd.FileName);
                }
            }
        }

        private void buttonImportTrainers_Click(object sender, EventArgs e) {
            using (OpenFileDialog ofd = new OpenFileDialog {
                Filter = "Binary files (*.bin)|*.bin|All files (*.*)|*.*", DefaultExt = "bin"
            }) {
                if (ofd.ShowDialog() == DialogResult.OK && trainerFile.ImportFromFile(ofd.FileName)) {
                    RefreshTrainerList();
                    SetDirty();
                }
            }
        }

        private void buttonLocateTrainers_Click(object sender, EventArgs e) {
            string path = Filesystem.battleTowerTrainers;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) {
                Helpers.ExplorerSelect(path);
            }
        }
        #endregion

        #region Pokemon Sets tab
        private void RefreshSetList(int selectIndex = 0) {
            Helpers.DisableHandlers();
            try {
                setListBox.Items.Clear();
                for (int i = 0; i < setFile.Sets.Count; i++) {
                    setListBox.Items.Add($"Set {i:D3}: {setFile.Sets[i]}");
                }
            } finally {
                Helpers.EnableHandlers();
            }
            if (setListBox.Items.Count > 0) {
                setListBox.SelectedIndex = Math.Min(selectIndex, setListBox.Items.Count - 1);
            }
        }

        private void buttonNewSet_Click(object sender, EventArgs e) {
            setFile.Sets.Add(new BattleTowerPokemonSet());
            addSetNumeric.Maximum = setFile.Sets.Count - 1;
            RefreshSetList(setFile.Sets.Count - 1);
            SetDirty();
        }

        private BattleTowerPokemonSet SelectedSet =>
            (setListBox.SelectedIndex >= 0 && setListBox.SelectedIndex < setFile.Sets.Count)
                ? setFile.Sets[setListBox.SelectedIndex] : null;

        private void setListBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) return;
            BattleTowerPokemonSet set = SelectedSet;
            if (set == null) return;

            Helpers.DisableHandlers();
            try {
                speciesCombo.SelectedIndex = set.Species < speciesCombo.Items.Count ? set.Species : -1;
                move1Combo.SelectedIndex = set.Moves[0] < move1Combo.Items.Count ? set.Moves[0] : -1;
                move2Combo.SelectedIndex = set.Moves[1] < move2Combo.Items.Count ? set.Moves[1] : -1;
                move3Combo.SelectedIndex = set.Moves[2] < move3Combo.Items.Count ? set.Moves[2] : -1;
                move4Combo.SelectedIndex = set.Moves[3] < move4Combo.Items.Count ? set.Moves[3] : -1;
                natureCombo.SelectedIndex = set.Nature < natureCombo.Items.Count ? set.Nature : -1;
                itemCombo.SelectedIndex = set.Item < itemCombo.Items.Count ? set.Item : -1;
                formNumeric.Value = set.Form;

                evHpCheck.Checked = (set.EvFlags & 0x01) != 0;
                evAtkCheck.Checked = (set.EvFlags & 0x02) != 0;
                evDefCheck.Checked = (set.EvFlags & 0x04) != 0;
                evSpeCheck.Checked = (set.EvFlags & 0x08) != 0;
                evSpaCheck.Checked = (set.EvFlags & 0x10) != 0;
                evSpdCheck.Checked = (set.EvFlags & 0x20) != 0;

                UpdateSpeciesIcon(set.Species);
            } finally {
                Helpers.EnableHandlers();
            }
        }

        private void UpdateSpeciesIcon(int species) {
            try {
                pictureBoxSpecies.Image = species > 0
                    ? (DSUtils.GetPokePic(species, pictureBoxSpecies.Width, pictureBoxSpecies.Height) ?? Properties.Resources.IconPokeball)
                    : Properties.Resources.IconPokeball;
            } catch {
                pictureBoxSpecies.Image = Properties.Resources.IconPokeball;
            }
        }

        private void SetField_Changed(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) return;
            BattleTowerPokemonSet set = SelectedSet;
            if (set == null) return;

            set.Species = (ushort)Math.Max(0, speciesCombo.SelectedIndex);
            set.Moves[0] = (ushort)Math.Max(0, move1Combo.SelectedIndex);
            set.Moves[1] = (ushort)Math.Max(0, move2Combo.SelectedIndex);
            set.Moves[2] = (ushort)Math.Max(0, move3Combo.SelectedIndex);
            set.Moves[3] = (ushort)Math.Max(0, move4Combo.SelectedIndex);
            set.Nature = (byte)Math.Max(0, natureCombo.SelectedIndex);
            set.Item = (ushort)Math.Max(0, itemCombo.SelectedIndex);
            set.Form = (ushort)formNumeric.Value;

            byte flags = 0;
            if (evHpCheck.Checked) flags |= 0x01;
            if (evAtkCheck.Checked) flags |= 0x02;
            if (evDefCheck.Checked) flags |= 0x04;
            if (evSpeCheck.Checked) flags |= 0x08;
            if (evSpaCheck.Checked) flags |= 0x10;
            if (evSpdCheck.Checked) flags |= 0x20;
            set.EvFlags = flags;

            SetDirty();

            if (sender == speciesCombo) {
                UpdateSpeciesIcon(set.Species);
            }

            int index = setListBox.SelectedIndex;
            if (index >= 0) {
                Helpers.DisableHandlers();
                try {
                    setListBox.Items[index] = $"Set {index:D3}: {set}";
                } finally {
                    Helpers.EnableHandlers();
                }
            }
        }

        private void SaveSets() {
            if (setFile == null) return;
            setFile.SaveToNarc();
        }

        private void buttonSaveSets_Click(object sender, EventArgs e) {
            SaveSets();
            isDirty = false;
        }

        private void buttonExportSets_Click(object sender, EventArgs e) {
            using (SaveFileDialog sfd = new SaveFileDialog {
                Filter = "Binary files (*.bin)|*.bin|All files (*.*)|*.*",
                DefaultExt = "bin",
                FileName = "battle_tower_sets.bin"
            }) {
                if (sfd.ShowDialog() == DialogResult.OK) {
                    setFile.ExportToFile(sfd.FileName);
                }
            }
        }

        private void buttonImportSets_Click(object sender, EventArgs e) {
            using (OpenFileDialog ofd = new OpenFileDialog {
                Filter = "Binary files (*.bin)|*.bin|All files (*.*)|*.*", DefaultExt = "bin"
            }) {
                if (ofd.ShowDialog() == DialogResult.OK && setFile.ImportFromFile(ofd.FileName)) {
                    RefreshSetList();
                    SetDirty();
                }
            }
        }

        private void buttonLocateSets_Click(object sender, EventArgs e) {
            string path = Filesystem.battleTowerPokemon;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) {
                Helpers.ExplorerSelect(path);
            }
        }
        #endregion
    }
}
