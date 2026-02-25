namespace DSPRE.Editors {
    partial class BugContestEncounterEditor {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent() {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BugContestEncounterEditor));
            this.labelNotAvailable = new System.Windows.Forms.Label();
            this.panelMain = new System.Windows.Forms.Panel();
            this.groupBoxInfo = new System.Windows.Forms.GroupBox();
            this.labelInfo = new System.Windows.Forms.Label();
            this.groupBoxSet = new System.Windows.Forms.GroupBox();
            this.comboBoxSet = new System.Windows.Forms.ComboBox();
            this.labelSetDescription = new System.Windows.Forms.Label();
            this.groupBoxEncounters = new System.Windows.Forms.GroupBox();
            this.listBoxEncounters = new System.Windows.Forms.ListBox();
            this.buttonAdd = new System.Windows.Forms.Button();
            this.buttonRemove = new System.Windows.Forms.Button();
            this.groupBoxDetails = new System.Windows.Forms.GroupBox();
            this.pictureBoxPokemon = new System.Windows.Forms.PictureBox();
            this.labelSpecies = new System.Windows.Forms.Label();
            this.comboBoxSpecies = new System.Windows.Forms.ComboBox();
            this.labelMinLevel = new System.Windows.Forms.Label();
            this.numericUpDownMinLevel = new System.Windows.Forms.NumericUpDown();
            this.labelMaxLevel = new System.Windows.Forms.Label();
            this.numericUpDownMaxLevel = new System.Windows.Forms.NumericUpDown();
            this.labelRate = new System.Windows.Forms.Label();
            this.numericUpDownRate = new System.Windows.Forms.NumericUpDown();
            this.labelScore = new System.Windows.Forms.Label();
            this.numericUpDownScore = new System.Windows.Forms.NumericUpDown();
            this.labelDummy = new System.Windows.Forms.Label();
            this.numericUpDownDummy = new System.Windows.Forms.NumericUpDown();
            this.labelDummyInfo = new System.Windows.Forms.Label();
            this.buttonSave = new System.Windows.Forms.Button();
            this.buttonExport = new System.Windows.Forms.Button();
            this.buttonImport = new System.Windows.Forms.Button();
            this.buttonLocate = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.panelMain.SuspendLayout();
            this.groupBoxInfo.SuspendLayout();
            this.groupBoxSet.SuspendLayout();
            this.groupBoxEncounters.SuspendLayout();
            this.groupBoxDetails.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxPokemon)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownMinLevel)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownMaxLevel)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownRate)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownScore)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownDummy)).BeginInit();
            this.SuspendLayout();
            // 
            // labelNotAvailable
            // 
            this.labelNotAvailable.AutoSize = true;
            this.labelNotAvailable.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            this.labelNotAvailable.Location = new System.Drawing.Point(20, 20);
            this.labelNotAvailable.Name = "labelNotAvailable";
            this.labelNotAvailable.Size = new System.Drawing.Size(334, 20);
            this.labelNotAvailable.TabIndex = 0;
            this.labelNotAvailable.Text = "Bug Contest Editor is only available for HGSS.";
            this.labelNotAvailable.Visible = false;
            // 
            // panelMain
            // 
            this.panelMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelMain.Controls.Add(this.groupBoxInfo);
            this.panelMain.Controls.Add(this.groupBoxSet);
            this.panelMain.Controls.Add(this.groupBoxEncounters);
            this.panelMain.Controls.Add(this.groupBoxDetails);
            this.panelMain.Controls.Add(this.buttonSave);
            this.panelMain.Controls.Add(this.buttonExport);
            this.panelMain.Controls.Add(this.buttonImport);
            this.panelMain.Controls.Add(this.buttonLocate);
            this.panelMain.Location = new System.Drawing.Point(3, 3);
            this.panelMain.Name = "panelMain";
            this.panelMain.Size = new System.Drawing.Size(847, 558);
            this.panelMain.TabIndex = 1;
            // 
            // groupBoxInfo
            // 
            this.groupBoxInfo.Controls.Add(this.labelInfo);
            this.groupBoxInfo.Location = new System.Drawing.Point(312, 282);
            this.groupBoxInfo.Name = "groupBoxInfo";
            this.groupBoxInfo.Size = new System.Drawing.Size(376, 257);
            this.groupBoxInfo.TabIndex = 7;
            this.groupBoxInfo.TabStop = false;
            this.groupBoxInfo.Text = "Information";
            // 
            // labelInfo
            // 
            this.labelInfo.Location = new System.Drawing.Point(6, 16);
            this.labelInfo.Name = "labelInfo";
            this.labelInfo.Size = new System.Drawing.Size(364, 182);
            this.labelInfo.TabIndex = 0;
            this.labelInfo.Text = resources.GetString("labelInfo.Text");
            // 
            // groupBoxSet
            // 
            this.groupBoxSet.Controls.Add(this.comboBoxSet);
            this.groupBoxSet.Controls.Add(this.labelSetDescription);
            this.groupBoxSet.Location = new System.Drawing.Point(6, 6);
            this.groupBoxSet.Name = "groupBoxSet";
            this.groupBoxSet.Size = new System.Drawing.Size(300, 90);
            this.groupBoxSet.TabIndex = 0;
            this.groupBoxSet.TabStop = false;
            this.groupBoxSet.Text = "Encounter Set";
            // 
            // comboBoxSet
            // 
            this.comboBoxSet.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxSet.FormattingEnabled = true;
            this.comboBoxSet.Location = new System.Drawing.Point(6, 19);
            this.comboBoxSet.Name = "comboBoxSet";
            this.comboBoxSet.Size = new System.Drawing.Size(288, 21);
            this.comboBoxSet.TabIndex = 0;
            this.comboBoxSet.SelectedIndexChanged += new System.EventHandler(this.comboBoxSet_SelectedIndexChanged);
            // 
            // labelSetDescription
            // 
            this.labelSetDescription.ForeColor = System.Drawing.SystemColors.GrayText;
            this.labelSetDescription.Location = new System.Drawing.Point(6, 45);
            this.labelSetDescription.Name = "labelSetDescription";
            this.labelSetDescription.Size = new System.Drawing.Size(288, 40);
            this.labelSetDescription.TabIndex = 1;
            this.labelSetDescription.Text = "Select a set to view encounters";
            // 
            // groupBoxEncounters
            // 
            this.groupBoxEncounters.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.groupBoxEncounters.Controls.Add(this.listBoxEncounters);
            this.groupBoxEncounters.Controls.Add(this.buttonAdd);
            this.groupBoxEncounters.Controls.Add(this.buttonRemove);
            this.groupBoxEncounters.Location = new System.Drawing.Point(6, 102);
            this.groupBoxEncounters.Name = "groupBoxEncounters";
            this.groupBoxEncounters.Size = new System.Drawing.Size(300, 443);
            this.groupBoxEncounters.TabIndex = 1;
            this.groupBoxEncounters.TabStop = false;
            this.groupBoxEncounters.Text = "Encounters (10 per set)";
            // 
            // listBoxEncounters
            // 
            this.listBoxEncounters.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBoxEncounters.FormattingEnabled = true;
            this.listBoxEncounters.Location = new System.Drawing.Point(6, 19);
            this.listBoxEncounters.Name = "listBoxEncounters";
            this.listBoxEncounters.Size = new System.Drawing.Size(288, 368);
            this.listBoxEncounters.TabIndex = 0;
            this.listBoxEncounters.SelectedIndexChanged += new System.EventHandler(this.listBoxEncounters_SelectedIndexChanged);
            // 
            // buttonAdd
            // 
            this.buttonAdd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonAdd.Enabled = false;
            this.buttonAdd.Image = global::DSPRE.Properties.Resources.addIcon;
            this.buttonAdd.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.buttonAdd.Location = new System.Drawing.Point(6, 405);
            this.buttonAdd.Name = "buttonAdd";
            this.buttonAdd.Size = new System.Drawing.Size(138, 32);
            this.buttonAdd.TabIndex = 1;
            this.buttonAdd.Text = "Add";
            this.buttonAdd.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonAdd.UseVisualStyleBackColor = true;
            // 
            // buttonRemove
            // 
            this.buttonRemove.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonRemove.Enabled = false;
            this.buttonRemove.Image = global::DSPRE.Properties.Resources.deleteIcon;
            this.buttonRemove.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.buttonRemove.Location = new System.Drawing.Point(156, 405);
            this.buttonRemove.Name = "buttonRemove";
            this.buttonRemove.Size = new System.Drawing.Size(138, 32);
            this.buttonRemove.TabIndex = 2;
            this.buttonRemove.Text = "Remove";
            this.buttonRemove.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonRemove.UseVisualStyleBackColor = true;
            // 
            // groupBoxDetails
            // 
            this.groupBoxDetails.Controls.Add(this.pictureBoxPokemon);
            this.groupBoxDetails.Controls.Add(this.labelSpecies);
            this.groupBoxDetails.Controls.Add(this.comboBoxSpecies);
            this.groupBoxDetails.Controls.Add(this.labelMinLevel);
            this.groupBoxDetails.Controls.Add(this.numericUpDownMinLevel);
            this.groupBoxDetails.Controls.Add(this.labelMaxLevel);
            this.groupBoxDetails.Controls.Add(this.numericUpDownMaxLevel);
            this.groupBoxDetails.Controls.Add(this.labelRate);
            this.groupBoxDetails.Controls.Add(this.numericUpDownRate);
            this.groupBoxDetails.Controls.Add(this.labelScore);
            this.groupBoxDetails.Controls.Add(this.numericUpDownScore);
            this.groupBoxDetails.Controls.Add(this.labelDummy);
            this.groupBoxDetails.Controls.Add(this.numericUpDownDummy);
            this.groupBoxDetails.Controls.Add(this.labelDummyInfo);
            this.groupBoxDetails.Location = new System.Drawing.Point(312, 6);
            this.groupBoxDetails.Name = "groupBoxDetails";
            this.groupBoxDetails.Size = new System.Drawing.Size(280, 270);
            this.groupBoxDetails.TabIndex = 2;
            this.groupBoxDetails.TabStop = false;
            this.groupBoxDetails.Text = "Encounter Details";
            // 
            // pictureBoxPokemon
            // 
            this.pictureBoxPokemon.BackColor = System.Drawing.Color.White;
            this.pictureBoxPokemon.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureBoxPokemon.Location = new System.Drawing.Point(200, 19);
            this.pictureBoxPokemon.Name = "pictureBoxPokemon";
            this.pictureBoxPokemon.Size = new System.Drawing.Size(68, 56);
            this.pictureBoxPokemon.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.pictureBoxPokemon.TabIndex = 14;
            this.pictureBoxPokemon.TabStop = false;
            // 
            // labelSpecies
            // 
            this.labelSpecies.AutoSize = true;
            this.labelSpecies.Location = new System.Drawing.Point(6, 22);
            this.labelSpecies.Name = "labelSpecies";
            this.labelSpecies.Size = new System.Drawing.Size(48, 13);
            this.labelSpecies.TabIndex = 0;
            this.labelSpecies.Text = "Species:";
            // 
            // comboBoxSpecies
            // 
            this.comboBoxSpecies.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxSpecies.FormattingEnabled = true;
            this.comboBoxSpecies.Location = new System.Drawing.Point(75, 19);
            this.comboBoxSpecies.Name = "comboBoxSpecies";
            this.comboBoxSpecies.Size = new System.Drawing.Size(119, 21);
            this.comboBoxSpecies.TabIndex = 1;
            this.comboBoxSpecies.SelectedIndexChanged += new System.EventHandler(this.comboBoxSpecies_SelectedIndexChanged);
            // 
            // labelMinLevel
            // 
            this.labelMinLevel.AutoSize = true;
            this.labelMinLevel.Location = new System.Drawing.Point(6, 50);
            this.labelMinLevel.Name = "labelMinLevel";
            this.labelMinLevel.Size = new System.Drawing.Size(56, 13);
            this.labelMinLevel.TabIndex = 2;
            this.labelMinLevel.Text = "Min Level:";
            // 
            // numericUpDownMinLevel
            // 
            this.numericUpDownMinLevel.Location = new System.Drawing.Point(75, 48);
            this.numericUpDownMinLevel.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDownMinLevel.Name = "numericUpDownMinLevel";
            this.numericUpDownMinLevel.Size = new System.Drawing.Size(60, 20);
            this.numericUpDownMinLevel.TabIndex = 3;
            this.numericUpDownMinLevel.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDownMinLevel.ValueChanged += new System.EventHandler(this.numericUpDownMinLevel_ValueChanged);
            // 
            // labelMaxLevel
            // 
            this.labelMaxLevel.AutoSize = true;
            this.labelMaxLevel.Location = new System.Drawing.Point(6, 78);
            this.labelMaxLevel.Name = "labelMaxLevel";
            this.labelMaxLevel.Size = new System.Drawing.Size(59, 13);
            this.labelMaxLevel.TabIndex = 4;
            this.labelMaxLevel.Text = "Max Level:";
            // 
            // numericUpDownMaxLevel
            // 
            this.numericUpDownMaxLevel.Location = new System.Drawing.Point(75, 76);
            this.numericUpDownMaxLevel.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDownMaxLevel.Name = "numericUpDownMaxLevel";
            this.numericUpDownMaxLevel.Size = new System.Drawing.Size(60, 20);
            this.numericUpDownMaxLevel.TabIndex = 5;
            this.numericUpDownMaxLevel.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDownMaxLevel.ValueChanged += new System.EventHandler(this.numericUpDownMaxLevel_ValueChanged);
            // 
            // labelRate
            // 
            this.labelRate.AutoSize = true;
            this.labelRate.Location = new System.Drawing.Point(6, 106);
            this.labelRate.Name = "labelRate";
            this.labelRate.Size = new System.Drawing.Size(33, 13);
            this.labelRate.TabIndex = 6;
            this.labelRate.Text = "Rate:";
            // 
            // numericUpDownRate
            // 
            this.numericUpDownRate.Location = new System.Drawing.Point(75, 104);
            this.numericUpDownRate.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.numericUpDownRate.Name = "numericUpDownRate";
            this.numericUpDownRate.Size = new System.Drawing.Size(60, 20);
            this.numericUpDownRate.TabIndex = 7;
            this.numericUpDownRate.ValueChanged += new System.EventHandler(this.numericUpDownRate_ValueChanged);
            // 
            // labelScore
            // 
            this.labelScore.AutoSize = true;
            this.labelScore.Location = new System.Drawing.Point(6, 134);
            this.labelScore.Name = "labelScore";
            this.labelScore.Size = new System.Drawing.Size(38, 13);
            this.labelScore.TabIndex = 8;
            this.labelScore.Text = "Score:";
            // 
            // numericUpDownScore
            // 
            this.numericUpDownScore.Location = new System.Drawing.Point(75, 132);
            this.numericUpDownScore.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.numericUpDownScore.Name = "numericUpDownScore";
            this.numericUpDownScore.Size = new System.Drawing.Size(60, 20);
            this.numericUpDownScore.TabIndex = 9;
            this.numericUpDownScore.ValueChanged += new System.EventHandler(this.numericUpDownScore_ValueChanged);
            // 
            // labelDummy
            // 
            this.labelDummy.AutoSize = true;
            this.labelDummy.Location = new System.Drawing.Point(6, 168);
            this.labelDummy.Name = "labelDummy";
            this.labelDummy.Size = new System.Drawing.Size(64, 13);
            this.labelDummy.TabIndex = 10;
            this.labelDummy.Text = "Terminator*:";
            // 
            // numericUpDownDummy
            // 
            this.numericUpDownDummy.Enabled = false;
            this.numericUpDownDummy.Hexadecimal = true;
            this.numericUpDownDummy.Location = new System.Drawing.Point(75, 166);
            this.numericUpDownDummy.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.numericUpDownDummy.Name = "numericUpDownDummy";
            this.numericUpDownDummy.Size = new System.Drawing.Size(80, 20);
            this.numericUpDownDummy.TabIndex = 11;
            // 
            // labelDummyInfo
            // 
            this.labelDummyInfo.ForeColor = System.Drawing.SystemColors.GrayText;
            this.labelDummyInfo.Location = new System.Drawing.Point(6, 195);
            this.labelDummyInfo.Name = "labelDummyInfo";
            this.labelDummyInfo.Size = new System.Drawing.Size(268, 70);
            this.labelDummyInfo.TabIndex = 13;
            this.labelDummyInfo.Text = "* We believe this to be simply the \'end of encounter data\' terminator. Its exact " +
    "purpose is not fully researched, but it may be relevant in future discoveries.";
            // 
            // buttonSave
            // 
            this.buttonSave.Image = global::DSPRE.Properties.Resources.saveButton;
            this.buttonSave.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.buttonSave.Location = new System.Drawing.Point(598, 19);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(90, 32);
            this.buttonSave.TabIndex = 3;
            this.buttonSave.Text = "Save";
            this.buttonSave.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // buttonExport
            // 
            this.buttonExport.Image = global::DSPRE.Properties.Resources.exportArrow;
            this.buttonExport.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.buttonExport.Location = new System.Drawing.Point(598, 57);
            this.buttonExport.Name = "buttonExport";
            this.buttonExport.Size = new System.Drawing.Size(90, 32);
            this.buttonExport.TabIndex = 4;
            this.buttonExport.Text = "Export";
            this.buttonExport.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonExport.UseVisualStyleBackColor = true;
            this.buttonExport.Click += new System.EventHandler(this.buttonExport_Click);
            // 
            // buttonImport
            // 
            this.buttonImport.Image = global::DSPRE.Properties.Resources.importArrow;
            this.buttonImport.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.buttonImport.Location = new System.Drawing.Point(598, 95);
            this.buttonImport.Name = "buttonImport";
            this.buttonImport.Size = new System.Drawing.Size(90, 32);
            this.buttonImport.TabIndex = 5;
            this.buttonImport.Text = "Import";
            this.buttonImport.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonImport.UseVisualStyleBackColor = true;
            this.buttonImport.Click += new System.EventHandler(this.buttonImport_Click);
            // 
            // buttonLocate
            // 
            this.buttonLocate.Image = global::DSPRE.Properties.Resources.lens;
            this.buttonLocate.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.buttonLocate.Location = new System.Drawing.Point(598, 133);
            this.buttonLocate.Name = "buttonLocate";
            this.buttonLocate.Size = new System.Drawing.Size(90, 32);
            this.buttonLocate.TabIndex = 6;
            this.buttonLocate.Text = "Locate";
            this.buttonLocate.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonLocate.UseVisualStyleBackColor = true;
            this.buttonLocate.Click += new System.EventHandler(this.buttonLocate_Click);
            // 
            // BugContestEncounterEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.labelNotAvailable);
            this.Controls.Add(this.panelMain);
            this.Name = "BugContestEncounterEditor";
            this.Size = new System.Drawing.Size(850, 564);
            this.panelMain.ResumeLayout(false);
            this.groupBoxInfo.ResumeLayout(false);
            this.groupBoxSet.ResumeLayout(false);
            this.groupBoxEncounters.ResumeLayout(false);
            this.groupBoxDetails.ResumeLayout(false);
            this.groupBoxDetails.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxPokemon)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownMinLevel)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownMaxLevel)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownRate)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownScore)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownDummy)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelNotAvailable;
        private System.Windows.Forms.Panel panelMain;
        private System.Windows.Forms.GroupBox groupBoxSet;
        private System.Windows.Forms.ComboBox comboBoxSet;
        private System.Windows.Forms.Label labelSetDescription;
        private System.Windows.Forms.GroupBox groupBoxEncounters;
        private System.Windows.Forms.ListBox listBoxEncounters;
        private System.Windows.Forms.Button buttonAdd;
        private System.Windows.Forms.Button buttonRemove;
        private System.Windows.Forms.GroupBox groupBoxDetails;
        private System.Windows.Forms.PictureBox pictureBoxPokemon;
        private System.Windows.Forms.Label labelSpecies;
        private System.Windows.Forms.ComboBox comboBoxSpecies;
        private System.Windows.Forms.Label labelMinLevel;
        private System.Windows.Forms.NumericUpDown numericUpDownMinLevel;
        private System.Windows.Forms.Label labelMaxLevel;
        private System.Windows.Forms.NumericUpDown numericUpDownMaxLevel;
        private System.Windows.Forms.Label labelRate;
        private System.Windows.Forms.NumericUpDown numericUpDownRate;
        private System.Windows.Forms.Label labelScore;
        private System.Windows.Forms.NumericUpDown numericUpDownScore;
        private System.Windows.Forms.Label labelDummy;
        private System.Windows.Forms.NumericUpDown numericUpDownDummy;
        private System.Windows.Forms.Label labelDummyInfo;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.Button buttonExport;
        private System.Windows.Forms.Button buttonImport;
        private System.Windows.Forms.Button buttonLocate;
        private System.Windows.Forms.GroupBox groupBoxInfo;
        private System.Windows.Forms.Label labelInfo;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
