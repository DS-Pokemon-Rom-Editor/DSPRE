namespace DSPRE.Editors {
    partial class BattleTowerEditor {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent() {
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabPageTrainers = new System.Windows.Forms.TabPage();
            this.trainerListBox = new System.Windows.Forms.ListBox();
            this.buttonNewTrainer = new System.Windows.Forms.Button();
            this.groupBoxTrainerInfo = new System.Windows.Forms.GroupBox();
            this.labelClass = new System.Windows.Forms.Label();
            this.trainerClassCombo = new DSPRE.InputComboBox();
            this.labelName = new System.Windows.Forms.Label();
            this.trainerNameTextBox = new System.Windows.Forms.TextBox();
            this.labelMessage1 = new System.Windows.Forms.Label();
            this.message1TextBox = new System.Windows.Forms.TextBox();
            this.labelMessage2 = new System.Windows.Forms.Label();
            this.message2TextBox = new System.Windows.Forms.TextBox();
            this.labelMessage3 = new System.Windows.Forms.Label();
            this.message3TextBox = new System.Windows.Forms.TextBox();
            this.groupBoxTrainerSets = new System.Windows.Forms.GroupBox();
            this.setIdsListBox = new System.Windows.Forms.ListBox();
            this.addSetNumeric = new System.Windows.Forms.NumericUpDown();
            this.addSetPreviewLabel = new System.Windows.Forms.Label();
            this.buttonAddSet = new System.Windows.Forms.Button();
            this.buttonRemoveSet = new System.Windows.Forms.Button();
            this.buttonSaveTrainers = new System.Windows.Forms.Button();
            this.buttonExportTrainers = new System.Windows.Forms.Button();
            this.buttonImportTrainers = new System.Windows.Forms.Button();
            this.buttonLocateTrainers = new System.Windows.Forms.Button();
            this.tabPageSets = new System.Windows.Forms.TabPage();
            this.pictureBoxSpecies = new System.Windows.Forms.PictureBox();
            this.setListBox = new System.Windows.Forms.ListBox();
            this.buttonNewSet = new System.Windows.Forms.Button();
            this.groupBoxSetDetails = new System.Windows.Forms.GroupBox();
            this.labelSpecies = new System.Windows.Forms.Label();
            this.speciesCombo = new DSPRE.InputComboBox();
            this.labelMove1 = new System.Windows.Forms.Label();
            this.move1Combo = new DSPRE.InputComboBox();
            this.labelMove2 = new System.Windows.Forms.Label();
            this.move2Combo = new DSPRE.InputComboBox();
            this.labelMove3 = new System.Windows.Forms.Label();
            this.move3Combo = new DSPRE.InputComboBox();
            this.labelMove4 = new System.Windows.Forms.Label();
            this.move4Combo = new DSPRE.InputComboBox();
            this.labelNature = new System.Windows.Forms.Label();
            this.natureCombo = new DSPRE.InputComboBox();
            this.labelItem = new System.Windows.Forms.Label();
            this.itemCombo = new DSPRE.InputComboBox();
            this.labelForm = new System.Windows.Forms.Label();
            this.formNumeric = new System.Windows.Forms.NumericUpDown();
            this.groupBoxEvs = new System.Windows.Forms.GroupBox();
            this.evHpCheck = new System.Windows.Forms.CheckBox();
            this.evAtkCheck = new System.Windows.Forms.CheckBox();
            this.evDefCheck = new System.Windows.Forms.CheckBox();
            this.evSpeCheck = new System.Windows.Forms.CheckBox();
            this.evSpaCheck = new System.Windows.Forms.CheckBox();
            this.evSpdCheck = new System.Windows.Forms.CheckBox();
            this.buttonSaveSets = new System.Windows.Forms.Button();
            this.buttonExportSets = new System.Windows.Forms.Button();
            this.buttonImportSets = new System.Windows.Forms.Button();
            this.buttonLocateSets = new System.Windows.Forms.Button();
            this.tabControl.SuspendLayout();
            this.tabPageTrainers.SuspendLayout();
            this.groupBoxTrainerInfo.SuspendLayout();
            this.groupBoxTrainerSets.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.addSetNumeric)).BeginInit();
            this.tabPageSets.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxSpecies)).BeginInit();
            this.groupBoxSetDetails.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.formNumeric)).BeginInit();
            this.groupBoxEvs.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl
            // 
            this.tabControl.Controls.Add(this.tabPageTrainers);
            this.tabControl.Controls.Add(this.tabPageSets);
            this.tabControl.Location = new System.Drawing.Point(10, 10);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(874, 660);
            this.tabControl.TabIndex = 0;
            // 
            // tabPageTrainers
            // 
            this.tabPageTrainers.Controls.Add(this.trainerListBox);
            this.tabPageTrainers.Controls.Add(this.buttonNewTrainer);
            this.tabPageTrainers.Controls.Add(this.groupBoxTrainerInfo);
            this.tabPageTrainers.Controls.Add(this.groupBoxTrainerSets);
            this.tabPageTrainers.Controls.Add(this.buttonSaveTrainers);
            this.tabPageTrainers.Controls.Add(this.buttonExportTrainers);
            this.tabPageTrainers.Controls.Add(this.buttonImportTrainers);
            this.tabPageTrainers.Controls.Add(this.buttonLocateTrainers);
            this.tabPageTrainers.Location = new System.Drawing.Point(4, 22);
            this.tabPageTrainers.Name = "tabPageTrainers";
            this.tabPageTrainers.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageTrainers.Size = new System.Drawing.Size(982, 634);
            this.tabPageTrainers.TabIndex = 0;
            this.tabPageTrainers.Text = "Trainers";
            this.tabPageTrainers.UseVisualStyleBackColor = true;
            // 
            // trainerListBox
            // 
            this.trainerListBox.FormattingEnabled = true;
            this.trainerListBox.Location = new System.Drawing.Point(10, 10);
            this.trainerListBox.Name = "trainerListBox";
            this.trainerListBox.Size = new System.Drawing.Size(280, 537);
            this.trainerListBox.TabIndex = 0;
            this.trainerListBox.SelectedIndexChanged += new System.EventHandler(this.trainerListBox_SelectedIndexChanged);
            // 
            // buttonNewTrainer
            // 
            this.buttonNewTrainer.Location = new System.Drawing.Point(10, 558);
            this.buttonNewTrainer.Name = "buttonNewTrainer";
            this.buttonNewTrainer.Size = new System.Drawing.Size(280, 22);
            this.buttonNewTrainer.TabIndex = 7;
            this.buttonNewTrainer.Text = "New Trainer";
            this.buttonNewTrainer.UseVisualStyleBackColor = true;
            this.buttonNewTrainer.Click += new System.EventHandler(this.buttonNewTrainer_Click);
            // 
            // groupBoxTrainerInfo
            // 
            this.groupBoxTrainerInfo.Controls.Add(this.labelClass);
            this.groupBoxTrainerInfo.Controls.Add(this.trainerClassCombo);
            this.groupBoxTrainerInfo.Controls.Add(this.labelName);
            this.groupBoxTrainerInfo.Controls.Add(this.trainerNameTextBox);
            this.groupBoxTrainerInfo.Controls.Add(this.labelMessage1);
            this.groupBoxTrainerInfo.Controls.Add(this.message1TextBox);
            this.groupBoxTrainerInfo.Controls.Add(this.labelMessage2);
            this.groupBoxTrainerInfo.Controls.Add(this.message2TextBox);
            this.groupBoxTrainerInfo.Controls.Add(this.labelMessage3);
            this.groupBoxTrainerInfo.Controls.Add(this.message3TextBox);
            this.groupBoxTrainerInfo.Location = new System.Drawing.Point(300, 10);
            this.groupBoxTrainerInfo.Name = "groupBoxTrainerInfo";
            this.groupBoxTrainerInfo.Size = new System.Drawing.Size(340, 270);
            this.groupBoxTrainerInfo.TabIndex = 1;
            this.groupBoxTrainerInfo.TabStop = false;
            this.groupBoxTrainerInfo.Text = "Trainer Info";
            // 
            // labelClass
            // 
            this.labelClass.AutoSize = true;
            this.labelClass.Location = new System.Drawing.Point(10, 20);
            this.labelClass.Name = "labelClass";
            this.labelClass.Size = new System.Drawing.Size(35, 13);
            this.labelClass.TabIndex = 0;
            this.labelClass.Text = "Class:";
            // 
            // trainerClassCombo
            // 
            this.trainerClassCombo.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.trainerClassCombo.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.trainerClassCombo.FormattingEnabled = true;
            this.trainerClassCombo.Location = new System.Drawing.Point(10, 40);
            this.trainerClassCombo.Name = "trainerClassCombo";
            this.trainerClassCombo.Size = new System.Drawing.Size(310, 21);
            this.trainerClassCombo.TabIndex = 1;
            this.trainerClassCombo.SelectedIndexChanged += new System.EventHandler(this.trainerClassCombo_SelectedIndexChanged);
            // 
            // labelName
            // 
            this.labelName.AutoSize = true;
            this.labelName.Location = new System.Drawing.Point(10, 70);
            this.labelName.Name = "labelName";
            this.labelName.Size = new System.Drawing.Size(38, 13);
            this.labelName.TabIndex = 2;
            this.labelName.Text = "Name:";
            // 
            // trainerNameTextBox
            // 
            this.trainerNameTextBox.Location = new System.Drawing.Point(10, 90);
            this.trainerNameTextBox.Name = "trainerNameTextBox";
            this.trainerNameTextBox.Size = new System.Drawing.Size(310, 20);
            this.trainerNameTextBox.TabIndex = 3;
            this.trainerNameTextBox.TextChanged += new System.EventHandler(this.trainerNameTextBox_TextChanged);
            // 
            // labelMessage1
            // 
            this.labelMessage1.AutoSize = true;
            this.labelMessage1.Location = new System.Drawing.Point(10, 120);
            this.labelMessage1.Name = "labelMessage1";
            this.labelMessage1.Size = new System.Drawing.Size(62, 13);
            this.labelMessage1.TabIndex = 4;
            this.labelMessage1.Text = "Message 1:";
            // 
            // message1TextBox
            // 
            this.message1TextBox.Location = new System.Drawing.Point(10, 140);
            this.message1TextBox.Name = "message1TextBox";
            this.message1TextBox.Size = new System.Drawing.Size(310, 20);
            this.message1TextBox.TabIndex = 5;
            this.message1TextBox.TextChanged += new System.EventHandler(this.messageTextBox_TextChanged);
            // 
            // labelMessage2
            // 
            this.labelMessage2.AutoSize = true;
            this.labelMessage2.Location = new System.Drawing.Point(10, 170);
            this.labelMessage2.Name = "labelMessage2";
            this.labelMessage2.Size = new System.Drawing.Size(62, 13);
            this.labelMessage2.TabIndex = 6;
            this.labelMessage2.Text = "Message 2:";
            // 
            // message2TextBox
            // 
            this.message2TextBox.Location = new System.Drawing.Point(10, 190);
            this.message2TextBox.Name = "message2TextBox";
            this.message2TextBox.Size = new System.Drawing.Size(310, 20);
            this.message2TextBox.TabIndex = 7;
            this.message2TextBox.TextChanged += new System.EventHandler(this.messageTextBox_TextChanged);
            // 
            // labelMessage3
            // 
            this.labelMessage3.AutoSize = true;
            this.labelMessage3.Location = new System.Drawing.Point(10, 220);
            this.labelMessage3.Name = "labelMessage3";
            this.labelMessage3.Size = new System.Drawing.Size(62, 13);
            this.labelMessage3.TabIndex = 8;
            this.labelMessage3.Text = "Message 3:";
            // 
            // message3TextBox
            // 
            this.message3TextBox.Location = new System.Drawing.Point(10, 240);
            this.message3TextBox.Name = "message3TextBox";
            this.message3TextBox.Size = new System.Drawing.Size(310, 20);
            this.message3TextBox.TabIndex = 9;
            this.message3TextBox.TextChanged += new System.EventHandler(this.messageTextBox_TextChanged);
            // 
            // groupBoxTrainerSets
            // 
            this.groupBoxTrainerSets.Controls.Add(this.setIdsListBox);
            this.groupBoxTrainerSets.Controls.Add(this.addSetNumeric);
            this.groupBoxTrainerSets.Controls.Add(this.addSetPreviewLabel);
            this.groupBoxTrainerSets.Controls.Add(this.buttonAddSet);
            this.groupBoxTrainerSets.Controls.Add(this.buttonRemoveSet);
            this.groupBoxTrainerSets.Location = new System.Drawing.Point(300, 290);
            this.groupBoxTrainerSets.Name = "groupBoxTrainerSets";
            this.groupBoxTrainerSets.Size = new System.Drawing.Size(340, 270);
            this.groupBoxTrainerSets.TabIndex = 2;
            this.groupBoxTrainerSets.TabStop = false;
            this.groupBoxTrainerSets.Text = "Available Sets (double-click to view)";
            // 
            // setIdsListBox
            // 
            this.setIdsListBox.FormattingEnabled = true;
            this.setIdsListBox.Location = new System.Drawing.Point(10, 20);
            this.setIdsListBox.Name = "setIdsListBox";
            this.setIdsListBox.Size = new System.Drawing.Size(320, 186);
            this.setIdsListBox.TabIndex = 0;
            this.setIdsListBox.DoubleClick += new System.EventHandler(this.setIdsListBox_DoubleClick);
            // 
            // addSetNumeric
            // 
            this.addSetNumeric.Location = new System.Drawing.Point(10, 215);
            this.addSetNumeric.Maximum = new decimal(new int[] {
            2000,
            0,
            0,
            0});
            this.addSetNumeric.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.addSetNumeric.Name = "addSetNumeric";
            this.addSetNumeric.Size = new System.Drawing.Size(80, 20);
            this.addSetNumeric.TabIndex = 1;
            this.addSetNumeric.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.addSetNumeric.ValueChanged += new System.EventHandler(this.addSetNumeric_ValueChanged);
            // 
            // addSetPreviewLabel
            // 
            this.addSetPreviewLabel.AutoSize = true;
            this.addSetPreviewLabel.Location = new System.Drawing.Point(10, 245);
            this.addSetPreviewLabel.Name = "addSetPreviewLabel";
            this.addSetPreviewLabel.Size = new System.Drawing.Size(0, 13);
            this.addSetPreviewLabel.TabIndex = 4;
            // 
            // buttonAddSet
            // 
            this.buttonAddSet.Location = new System.Drawing.Point(100, 213);
            this.buttonAddSet.Name = "buttonAddSet";
            this.buttonAddSet.Size = new System.Drawing.Size(105, 26);
            this.buttonAddSet.TabIndex = 2;
            this.buttonAddSet.Text = "Add Set";
            this.buttonAddSet.UseVisualStyleBackColor = true;
            this.buttonAddSet.Click += new System.EventHandler(this.buttonAddSet_Click);
            // 
            // buttonRemoveSet
            // 
            this.buttonRemoveSet.Location = new System.Drawing.Point(215, 213);
            this.buttonRemoveSet.Name = "buttonRemoveSet";
            this.buttonRemoveSet.Size = new System.Drawing.Size(115, 26);
            this.buttonRemoveSet.TabIndex = 3;
            this.buttonRemoveSet.Text = "Remove Selected";
            this.buttonRemoveSet.UseVisualStyleBackColor = true;
            this.buttonRemoveSet.Click += new System.EventHandler(this.buttonRemoveSet_Click);
            // 
            // buttonSaveTrainers
            // 
            this.buttonSaveTrainers.Image = global::DSPRE.Properties.Resources.saveButton;
            this.buttonSaveTrainers.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.buttonSaveTrainers.Location = new System.Drawing.Point(10, 585);
            this.buttonSaveTrainers.Name = "buttonSaveTrainers";
            this.buttonSaveTrainers.Size = new System.Drawing.Size(75, 30);
            this.buttonSaveTrainers.TabIndex = 3;
            this.buttonSaveTrainers.Text = "Save";
            this.buttonSaveTrainers.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonSaveTrainers.UseVisualStyleBackColor = true;
            this.buttonSaveTrainers.Click += new System.EventHandler(this.buttonSaveTrainers_Click);
            // 
            // buttonExportTrainers
            // 
            this.buttonExportTrainers.Image = global::DSPRE.Properties.Resources.exportArrow;
            this.buttonExportTrainers.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.buttonExportTrainers.Location = new System.Drawing.Point(95, 585);
            this.buttonExportTrainers.Name = "buttonExportTrainers";
            this.buttonExportTrainers.Size = new System.Drawing.Size(75, 30);
            this.buttonExportTrainers.TabIndex = 4;
            this.buttonExportTrainers.Text = "Export";
            this.buttonExportTrainers.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonExportTrainers.UseVisualStyleBackColor = true;
            this.buttonExportTrainers.Click += new System.EventHandler(this.buttonExportTrainers_Click);
            // 
            // buttonImportTrainers
            // 
            this.buttonImportTrainers.Image = global::DSPRE.Properties.Resources.importArrow;
            this.buttonImportTrainers.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.buttonImportTrainers.Location = new System.Drawing.Point(180, 585);
            this.buttonImportTrainers.Name = "buttonImportTrainers";
            this.buttonImportTrainers.Size = new System.Drawing.Size(75, 30);
            this.buttonImportTrainers.TabIndex = 5;
            this.buttonImportTrainers.Text = "Import";
            this.buttonImportTrainers.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonImportTrainers.UseVisualStyleBackColor = true;
            this.buttonImportTrainers.Click += new System.EventHandler(this.buttonImportTrainers_Click);
            // 
            // buttonLocateTrainers
            // 
            this.buttonLocateTrainers.Image = global::DSPRE.Properties.Resources.SearchMiniIcon;
            this.buttonLocateTrainers.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.buttonLocateTrainers.Location = new System.Drawing.Point(265, 585);
            this.buttonLocateTrainers.Name = "buttonLocateTrainers";
            this.buttonLocateTrainers.Size = new System.Drawing.Size(75, 30);
            this.buttonLocateTrainers.TabIndex = 6;
            this.buttonLocateTrainers.Text = "Locate";
            this.buttonLocateTrainers.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonLocateTrainers.UseVisualStyleBackColor = true;
            this.buttonLocateTrainers.Click += new System.EventHandler(this.buttonLocateTrainers_Click);
            // 
            // tabPageSets
            // 
            this.tabPageSets.Controls.Add(this.pictureBoxSpecies);
            this.tabPageSets.Controls.Add(this.setListBox);
            this.tabPageSets.Controls.Add(this.buttonNewSet);
            this.tabPageSets.Controls.Add(this.groupBoxSetDetails);
            this.tabPageSets.Controls.Add(this.buttonSaveSets);
            this.tabPageSets.Controls.Add(this.buttonExportSets);
            this.tabPageSets.Controls.Add(this.buttonImportSets);
            this.tabPageSets.Controls.Add(this.buttonLocateSets);
            this.tabPageSets.Location = new System.Drawing.Point(4, 22);
            this.tabPageSets.Name = "tabPageSets";
            this.tabPageSets.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageSets.Size = new System.Drawing.Size(866, 634);
            this.tabPageSets.TabIndex = 1;
            this.tabPageSets.Text = "Pokemon Sets";
            this.tabPageSets.UseVisualStyleBackColor = true;
            // 
            // pictureBoxSpecies
            // 
            this.pictureBoxSpecies.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureBoxSpecies.Location = new System.Drawing.Point(719, 25);
            this.pictureBoxSpecies.Name = "pictureBoxSpecies";
            this.pictureBoxSpecies.Size = new System.Drawing.Size(119, 118);
            this.pictureBoxSpecies.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.pictureBoxSpecies.TabIndex = 0;
            this.pictureBoxSpecies.TabStop = false;
            // 
            // setListBox
            // 
            this.setListBox.FormattingEnabled = true;
            this.setListBox.Location = new System.Drawing.Point(10, 10);
            this.setListBox.Name = "setListBox";
            this.setListBox.Size = new System.Drawing.Size(280, 537);
            this.setListBox.TabIndex = 0;
            this.setListBox.SelectedIndexChanged += new System.EventHandler(this.setListBox_SelectedIndexChanged);
            // 
            // buttonNewSet
            // 
            this.buttonNewSet.Location = new System.Drawing.Point(10, 558);
            this.buttonNewSet.Name = "buttonNewSet";
            this.buttonNewSet.Size = new System.Drawing.Size(280, 22);
            this.buttonNewSet.TabIndex = 6;
            this.buttonNewSet.Text = "New Set";
            this.buttonNewSet.UseVisualStyleBackColor = true;
            this.buttonNewSet.Click += new System.EventHandler(this.buttonNewSet_Click);
            // 
            // groupBoxSetDetails
            // 
            this.groupBoxSetDetails.Controls.Add(this.labelSpecies);
            this.groupBoxSetDetails.Controls.Add(this.speciesCombo);
            this.groupBoxSetDetails.Controls.Add(this.labelMove1);
            this.groupBoxSetDetails.Controls.Add(this.move1Combo);
            this.groupBoxSetDetails.Controls.Add(this.labelMove2);
            this.groupBoxSetDetails.Controls.Add(this.move2Combo);
            this.groupBoxSetDetails.Controls.Add(this.labelMove3);
            this.groupBoxSetDetails.Controls.Add(this.move3Combo);
            this.groupBoxSetDetails.Controls.Add(this.labelMove4);
            this.groupBoxSetDetails.Controls.Add(this.move4Combo);
            this.groupBoxSetDetails.Controls.Add(this.labelNature);
            this.groupBoxSetDetails.Controls.Add(this.natureCombo);
            this.groupBoxSetDetails.Controls.Add(this.labelItem);
            this.groupBoxSetDetails.Controls.Add(this.itemCombo);
            this.groupBoxSetDetails.Controls.Add(this.labelForm);
            this.groupBoxSetDetails.Controls.Add(this.formNumeric);
            this.groupBoxSetDetails.Controls.Add(this.groupBoxEvs);
            this.groupBoxSetDetails.Location = new System.Drawing.Point(300, 10);
            this.groupBoxSetDetails.Name = "groupBoxSetDetails";
            this.groupBoxSetDetails.Size = new System.Drawing.Size(400, 550);
            this.groupBoxSetDetails.TabIndex = 1;
            this.groupBoxSetDetails.TabStop = false;
            this.groupBoxSetDetails.Text = "Set Details";
            // 
            // labelSpecies
            // 
            this.labelSpecies.AutoSize = true;
            this.labelSpecies.Location = new System.Drawing.Point(10, 20);
            this.labelSpecies.Name = "labelSpecies";
            this.labelSpecies.Size = new System.Drawing.Size(48, 13);
            this.labelSpecies.TabIndex = 1;
            this.labelSpecies.Text = "Species:";
            // 
            // speciesCombo
            // 
            this.speciesCombo.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.speciesCombo.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.speciesCombo.FormattingEnabled = true;
            this.speciesCombo.Location = new System.Drawing.Point(10, 40);
            this.speciesCombo.Name = "speciesCombo";
            this.speciesCombo.Size = new System.Drawing.Size(260, 21);
            this.speciesCombo.TabIndex = 2;
            this.speciesCombo.SelectedIndexChanged += new System.EventHandler(this.SetField_Changed);
            // 
            // labelMove1
            // 
            this.labelMove1.AutoSize = true;
            this.labelMove1.Location = new System.Drawing.Point(10, 70);
            this.labelMove1.Name = "labelMove1";
            this.labelMove1.Size = new System.Drawing.Size(46, 13);
            this.labelMove1.TabIndex = 3;
            this.labelMove1.Text = "Move 1:";
            // 
            // move1Combo
            // 
            this.move1Combo.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.move1Combo.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.move1Combo.FormattingEnabled = true;
            this.move1Combo.Location = new System.Drawing.Point(10, 90);
            this.move1Combo.Name = "move1Combo";
            this.move1Combo.Size = new System.Drawing.Size(370, 21);
            this.move1Combo.TabIndex = 4;
            this.move1Combo.SelectedIndexChanged += new System.EventHandler(this.SetField_Changed);
            // 
            // labelMove2
            // 
            this.labelMove2.AutoSize = true;
            this.labelMove2.Location = new System.Drawing.Point(10, 120);
            this.labelMove2.Name = "labelMove2";
            this.labelMove2.Size = new System.Drawing.Size(46, 13);
            this.labelMove2.TabIndex = 5;
            this.labelMove2.Text = "Move 2:";
            // 
            // move2Combo
            // 
            this.move2Combo.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.move2Combo.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.move2Combo.FormattingEnabled = true;
            this.move2Combo.Location = new System.Drawing.Point(10, 140);
            this.move2Combo.Name = "move2Combo";
            this.move2Combo.Size = new System.Drawing.Size(370, 21);
            this.move2Combo.TabIndex = 6;
            this.move2Combo.SelectedIndexChanged += new System.EventHandler(this.SetField_Changed);
            // 
            // labelMove3
            // 
            this.labelMove3.AutoSize = true;
            this.labelMove3.Location = new System.Drawing.Point(10, 170);
            this.labelMove3.Name = "labelMove3";
            this.labelMove3.Size = new System.Drawing.Size(46, 13);
            this.labelMove3.TabIndex = 7;
            this.labelMove3.Text = "Move 3:";
            // 
            // move3Combo
            // 
            this.move3Combo.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.move3Combo.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.move3Combo.FormattingEnabled = true;
            this.move3Combo.Location = new System.Drawing.Point(10, 190);
            this.move3Combo.Name = "move3Combo";
            this.move3Combo.Size = new System.Drawing.Size(370, 21);
            this.move3Combo.TabIndex = 8;
            this.move3Combo.SelectedIndexChanged += new System.EventHandler(this.SetField_Changed);
            // 
            // labelMove4
            // 
            this.labelMove4.AutoSize = true;
            this.labelMove4.Location = new System.Drawing.Point(10, 220);
            this.labelMove4.Name = "labelMove4";
            this.labelMove4.Size = new System.Drawing.Size(46, 13);
            this.labelMove4.TabIndex = 9;
            this.labelMove4.Text = "Move 4:";
            // 
            // move4Combo
            // 
            this.move4Combo.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.move4Combo.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.move4Combo.FormattingEnabled = true;
            this.move4Combo.Location = new System.Drawing.Point(10, 240);
            this.move4Combo.Name = "move4Combo";
            this.move4Combo.Size = new System.Drawing.Size(370, 21);
            this.move4Combo.TabIndex = 10;
            this.move4Combo.SelectedIndexChanged += new System.EventHandler(this.SetField_Changed);
            // 
            // labelNature
            // 
            this.labelNature.AutoSize = true;
            this.labelNature.Location = new System.Drawing.Point(10, 270);
            this.labelNature.Name = "labelNature";
            this.labelNature.Size = new System.Drawing.Size(42, 13);
            this.labelNature.TabIndex = 11;
            this.labelNature.Text = "Nature:";
            // 
            // natureCombo
            // 
            this.natureCombo.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.natureCombo.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.natureCombo.FormattingEnabled = true;
            this.natureCombo.Location = new System.Drawing.Point(10, 290);
            this.natureCombo.Name = "natureCombo";
            this.natureCombo.Size = new System.Drawing.Size(200, 21);
            this.natureCombo.TabIndex = 12;
            this.natureCombo.SelectedIndexChanged += new System.EventHandler(this.SetField_Changed);
            // 
            // labelItem
            // 
            this.labelItem.AutoSize = true;
            this.labelItem.Location = new System.Drawing.Point(10, 320);
            this.labelItem.Name = "labelItem";
            this.labelItem.Size = new System.Drawing.Size(30, 13);
            this.labelItem.TabIndex = 13;
            this.labelItem.Text = "Item:";
            // 
            // itemCombo
            // 
            this.itemCombo.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.itemCombo.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.itemCombo.FormattingEnabled = true;
            this.itemCombo.Location = new System.Drawing.Point(10, 340);
            this.itemCombo.Name = "itemCombo";
            this.itemCombo.Size = new System.Drawing.Size(370, 21);
            this.itemCombo.TabIndex = 14;
            this.itemCombo.SelectedIndexChanged += new System.EventHandler(this.SetField_Changed);
            // 
            // labelForm
            // 
            this.labelForm.AutoSize = true;
            this.labelForm.Location = new System.Drawing.Point(10, 370);
            this.labelForm.Name = "labelForm";
            this.labelForm.Size = new System.Drawing.Size(33, 13);
            this.labelForm.TabIndex = 15;
            this.labelForm.Text = "Form:";
            // 
            // formNumeric
            // 
            this.formNumeric.Location = new System.Drawing.Point(10, 390);
            this.formNumeric.Maximum = new decimal(new int[] {
            50,
            0,
            0,
            0});
            this.formNumeric.Name = "formNumeric";
            this.formNumeric.Size = new System.Drawing.Size(80, 20);
            this.formNumeric.TabIndex = 16;
            this.formNumeric.ValueChanged += new System.EventHandler(this.SetField_Changed);
            // 
            // groupBoxEvs
            // 
            this.groupBoxEvs.Controls.Add(this.evHpCheck);
            this.groupBoxEvs.Controls.Add(this.evAtkCheck);
            this.groupBoxEvs.Controls.Add(this.evDefCheck);
            this.groupBoxEvs.Controls.Add(this.evSpeCheck);
            this.groupBoxEvs.Controls.Add(this.evSpaCheck);
            this.groupBoxEvs.Controls.Add(this.evSpdCheck);
            this.groupBoxEvs.Location = new System.Drawing.Point(10, 420);
            this.groupBoxEvs.Name = "groupBoxEvs";
            this.groupBoxEvs.Size = new System.Drawing.Size(370, 100);
            this.groupBoxEvs.TabIndex = 17;
            this.groupBoxEvs.TabStop = false;
            this.groupBoxEvs.Text = "EVs (252 each, checked = maxed)";
            // 
            // evHpCheck
            // 
            this.evHpCheck.AutoSize = true;
            this.evHpCheck.Location = new System.Drawing.Point(10, 25);
            this.evHpCheck.Name = "evHpCheck";
            this.evHpCheck.Size = new System.Drawing.Size(41, 17);
            this.evHpCheck.TabIndex = 0;
            this.evHpCheck.Text = "HP";
            this.evHpCheck.UseVisualStyleBackColor = true;
            this.evHpCheck.CheckedChanged += new System.EventHandler(this.SetField_Changed);
            // 
            // evAtkCheck
            // 
            this.evAtkCheck.AutoSize = true;
            this.evAtkCheck.Location = new System.Drawing.Point(130, 25);
            this.evAtkCheck.Name = "evAtkCheck";
            this.evAtkCheck.Size = new System.Drawing.Size(57, 17);
            this.evAtkCheck.TabIndex = 1;
            this.evAtkCheck.Text = "Attack";
            this.evAtkCheck.UseVisualStyleBackColor = true;
            this.evAtkCheck.CheckedChanged += new System.EventHandler(this.SetField_Changed);
            // 
            // evDefCheck
            // 
            this.evDefCheck.AutoSize = true;
            this.evDefCheck.Location = new System.Drawing.Point(250, 25);
            this.evDefCheck.Name = "evDefCheck";
            this.evDefCheck.Size = new System.Drawing.Size(66, 17);
            this.evDefCheck.TabIndex = 2;
            this.evDefCheck.Text = "Defense";
            this.evDefCheck.UseVisualStyleBackColor = true;
            this.evDefCheck.CheckedChanged += new System.EventHandler(this.SetField_Changed);
            // 
            // evSpeCheck
            // 
            this.evSpeCheck.AutoSize = true;
            this.evSpeCheck.Location = new System.Drawing.Point(10, 55);
            this.evSpeCheck.Name = "evSpeCheck";
            this.evSpeCheck.Size = new System.Drawing.Size(57, 17);
            this.evSpeCheck.TabIndex = 3;
            this.evSpeCheck.Text = "Speed";
            this.evSpeCheck.UseVisualStyleBackColor = true;
            this.evSpeCheck.CheckedChanged += new System.EventHandler(this.SetField_Changed);
            // 
            // evSpaCheck
            // 
            this.evSpaCheck.AutoSize = true;
            this.evSpaCheck.Location = new System.Drawing.Point(130, 55);
            this.evSpaCheck.Name = "evSpaCheck";
            this.evSpaCheck.Size = new System.Drawing.Size(61, 17);
            this.evSpaCheck.TabIndex = 4;
            this.evSpaCheck.Text = "Sp. Atk";
            this.evSpaCheck.UseVisualStyleBackColor = true;
            this.evSpaCheck.CheckedChanged += new System.EventHandler(this.SetField_Changed);
            // 
            // evSpdCheck
            // 
            this.evSpdCheck.AutoSize = true;
            this.evSpdCheck.Location = new System.Drawing.Point(250, 55);
            this.evSpdCheck.Name = "evSpdCheck";
            this.evSpdCheck.Size = new System.Drawing.Size(62, 17);
            this.evSpdCheck.TabIndex = 5;
            this.evSpdCheck.Text = "Sp. Def";
            this.evSpdCheck.UseVisualStyleBackColor = true;
            this.evSpdCheck.CheckedChanged += new System.EventHandler(this.SetField_Changed);
            // 
            // buttonSaveSets
            // 
            this.buttonSaveSets.Image = global::DSPRE.Properties.Resources.saveButton;
            this.buttonSaveSets.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.buttonSaveSets.Location = new System.Drawing.Point(10, 585);
            this.buttonSaveSets.Name = "buttonSaveSets";
            this.buttonSaveSets.Size = new System.Drawing.Size(75, 30);
            this.buttonSaveSets.TabIndex = 2;
            this.buttonSaveSets.Text = "Save";
            this.buttonSaveSets.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonSaveSets.UseVisualStyleBackColor = true;
            this.buttonSaveSets.Click += new System.EventHandler(this.buttonSaveSets_Click);
            // 
            // buttonExportSets
            // 
            this.buttonExportSets.Image = global::DSPRE.Properties.Resources.exportArrow;
            this.buttonExportSets.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.buttonExportSets.Location = new System.Drawing.Point(95, 585);
            this.buttonExportSets.Name = "buttonExportSets";
            this.buttonExportSets.Size = new System.Drawing.Size(75, 30);
            this.buttonExportSets.TabIndex = 3;
            this.buttonExportSets.Text = "Export";
            this.buttonExportSets.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonExportSets.UseVisualStyleBackColor = true;
            this.buttonExportSets.Click += new System.EventHandler(this.buttonExportSets_Click);
            // 
            // buttonImportSets
            // 
            this.buttonImportSets.Image = global::DSPRE.Properties.Resources.importArrow;
            this.buttonImportSets.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.buttonImportSets.Location = new System.Drawing.Point(180, 585);
            this.buttonImportSets.Name = "buttonImportSets";
            this.buttonImportSets.Size = new System.Drawing.Size(75, 30);
            this.buttonImportSets.TabIndex = 4;
            this.buttonImportSets.Text = "Import";
            this.buttonImportSets.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonImportSets.UseVisualStyleBackColor = true;
            this.buttonImportSets.Click += new System.EventHandler(this.buttonImportSets_Click);
            // 
            // buttonLocateSets
            // 
            this.buttonLocateSets.Image = global::DSPRE.Properties.Resources.SearchMiniIcon;
            this.buttonLocateSets.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.buttonLocateSets.Location = new System.Drawing.Point(265, 585);
            this.buttonLocateSets.Name = "buttonLocateSets";
            this.buttonLocateSets.Size = new System.Drawing.Size(75, 30);
            this.buttonLocateSets.TabIndex = 5;
            this.buttonLocateSets.Text = "Locate";
            this.buttonLocateSets.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonLocateSets.UseVisualStyleBackColor = true;
            this.buttonLocateSets.Click += new System.EventHandler(this.buttonLocateSets_Click);
            // 
            // BattleTowerEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(891, 680);
            this.Controls.Add(this.tabControl);
            this.Name = "BattleTowerEditor";
            this.Text = "Battle Tower Editor";
            this.tabControl.ResumeLayout(false);
            this.tabPageTrainers.ResumeLayout(false);
            this.groupBoxTrainerInfo.ResumeLayout(false);
            this.groupBoxTrainerInfo.PerformLayout();
            this.groupBoxTrainerSets.ResumeLayout(false);
            this.groupBoxTrainerSets.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.addSetNumeric)).EndInit();
            this.tabPageSets.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxSpecies)).EndInit();
            this.groupBoxSetDetails.ResumeLayout(false);
            this.groupBoxSetDetails.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.formNumeric)).EndInit();
            this.groupBoxEvs.ResumeLayout(false);
            this.groupBoxEvs.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabPageTrainers;
        private System.Windows.Forms.ListBox trainerListBox;
        private System.Windows.Forms.Button buttonNewTrainer;
        private System.Windows.Forms.GroupBox groupBoxTrainerInfo;
        private System.Windows.Forms.Label labelClass;
        private DSPRE.InputComboBox trainerClassCombo;
        private System.Windows.Forms.Label labelName;
        private System.Windows.Forms.TextBox trainerNameTextBox;
        private System.Windows.Forms.Label labelMessage1;
        private System.Windows.Forms.TextBox message1TextBox;
        private System.Windows.Forms.Label labelMessage2;
        private System.Windows.Forms.TextBox message2TextBox;
        private System.Windows.Forms.Label labelMessage3;
        private System.Windows.Forms.TextBox message3TextBox;
        private System.Windows.Forms.GroupBox groupBoxTrainerSets;
        private System.Windows.Forms.ListBox setIdsListBox;
        private System.Windows.Forms.NumericUpDown addSetNumeric;
        private System.Windows.Forms.Label addSetPreviewLabel;
        private System.Windows.Forms.Button buttonAddSet;
        private System.Windows.Forms.Button buttonRemoveSet;
        private System.Windows.Forms.Button buttonSaveTrainers;
        private System.Windows.Forms.Button buttonExportTrainers;
        private System.Windows.Forms.Button buttonImportTrainers;
        private System.Windows.Forms.Button buttonLocateTrainers;
        private System.Windows.Forms.TabPage tabPageSets;
        private System.Windows.Forms.ListBox setListBox;
        private System.Windows.Forms.Button buttonNewSet;
        private System.Windows.Forms.GroupBox groupBoxSetDetails;
        private System.Windows.Forms.PictureBox pictureBoxSpecies;
        private System.Windows.Forms.Label labelSpecies;
        private DSPRE.InputComboBox speciesCombo;
        private System.Windows.Forms.Label labelMove1;
        private DSPRE.InputComboBox move1Combo;
        private System.Windows.Forms.Label labelMove2;
        private DSPRE.InputComboBox move2Combo;
        private System.Windows.Forms.Label labelMove3;
        private DSPRE.InputComboBox move3Combo;
        private System.Windows.Forms.Label labelMove4;
        private DSPRE.InputComboBox move4Combo;
        private System.Windows.Forms.Label labelNature;
        private DSPRE.InputComboBox natureCombo;
        private System.Windows.Forms.Label labelItem;
        private DSPRE.InputComboBox itemCombo;
        private System.Windows.Forms.Label labelForm;
        private System.Windows.Forms.NumericUpDown formNumeric;
        private System.Windows.Forms.GroupBox groupBoxEvs;
        private System.Windows.Forms.CheckBox evHpCheck;
        private System.Windows.Forms.CheckBox evAtkCheck;
        private System.Windows.Forms.CheckBox evDefCheck;
        private System.Windows.Forms.CheckBox evSpeCheck;
        private System.Windows.Forms.CheckBox evSpaCheck;
        private System.Windows.Forms.CheckBox evSpdCheck;
        private System.Windows.Forms.Button buttonSaveSets;
        private System.Windows.Forms.Button buttonExportSets;
        private System.Windows.Forms.Button buttonImportSets;
        private System.Windows.Forms.Button buttonLocateSets;
    }
}
