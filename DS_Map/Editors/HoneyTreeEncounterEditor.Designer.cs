namespace DSPRE.Editors {
    partial class HoneyTreeEncounterEditor {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HoneyTreeEncounterEditor));
            this.labelNotAvailable = new System.Windows.Forms.Label();
            this.panelMain = new System.Windows.Forms.Panel();
            this.groupBoxInfo = new System.Windows.Forms.GroupBox();
            this.labelInfo = new System.Windows.Forms.Label();
            this.groupBoxGroup = new System.Windows.Forms.GroupBox();
            this.comboBoxGroup = new System.Windows.Forms.ComboBox();
            this.labelGroupDescription = new System.Windows.Forms.Label();
            this.groupBoxEncounters = new System.Windows.Forms.GroupBox();
            this.listBoxEncounters = new System.Windows.Forms.ListBox();
            this.groupBoxDetails = new System.Windows.Forms.GroupBox();
            this.pictureBoxPokemon = new System.Windows.Forms.PictureBox();
            this.labelSpecies = new System.Windows.Forms.Label();
            this.comboBoxSpecies = new System.Windows.Forms.ComboBox();
            this.labelEncounterRate = new System.Windows.Forms.Label();
            this.buttonRateHelp = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.buttonExport = new System.Windows.Forms.Button();
            this.buttonImport = new System.Windows.Forms.Button();
            this.buttonLocate = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.panelMain.SuspendLayout();
            this.groupBoxInfo.SuspendLayout();
            this.groupBoxGroup.SuspendLayout();
            this.groupBoxEncounters.SuspendLayout();
            this.groupBoxDetails.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxPokemon)).BeginInit();
            this.SuspendLayout();
            // 
            // labelNotAvailable
            // 
            this.labelNotAvailable.AutoSize = true;
            this.labelNotAvailable.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            this.labelNotAvailable.Location = new System.Drawing.Point(20, 20);
            this.labelNotAvailable.Name = "labelNotAvailable";
            this.labelNotAvailable.Size = new System.Drawing.Size(488, 20);
            this.labelNotAvailable.TabIndex = 0;
            this.labelNotAvailable.Text = "Honey Tree Editor is only available for Diamond, Pearl, and Platinum.";
            this.labelNotAvailable.Visible = false;
            // 
            // panelMain
            // 
            this.panelMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelMain.Controls.Add(this.groupBoxInfo);
            this.panelMain.Controls.Add(this.groupBoxGroup);
            this.panelMain.Controls.Add(this.groupBoxEncounters);
            this.panelMain.Controls.Add(this.groupBoxDetails);
            this.panelMain.Controls.Add(this.buttonSave);
            this.panelMain.Controls.Add(this.buttonExport);
            this.panelMain.Controls.Add(this.buttonImport);
            this.panelMain.Controls.Add(this.buttonLocate);
            this.panelMain.Location = new System.Drawing.Point(3, 3);
            this.panelMain.Name = "panelMain";
            this.panelMain.Size = new System.Drawing.Size(694, 394);
            this.panelMain.TabIndex = 1;
            // 
            // groupBoxInfo
            // 
            this.groupBoxInfo.Controls.Add(this.labelInfo);
            this.groupBoxInfo.Location = new System.Drawing.Point(350, 200);
            this.groupBoxInfo.Name = "groupBoxInfo";
            this.groupBoxInfo.Size = new System.Drawing.Size(330, 150);
            this.groupBoxInfo.TabIndex = 7;
            this.groupBoxInfo.TabStop = false;
            this.groupBoxInfo.Text = "Information";
            // 
            // labelInfo
            // 
            this.labelInfo.Location = new System.Drawing.Point(10, 20);
            this.labelInfo.Name = "labelInfo";
            this.labelInfo.Size = new System.Drawing.Size(310, 120);
            this.labelInfo.TabIndex = 0;
            this.labelInfo.Text = resources.GetString("labelInfo.Text");
            // 
            // groupBoxGroup
            // 
            this.groupBoxGroup.Controls.Add(this.comboBoxGroup);
            this.groupBoxGroup.Controls.Add(this.labelGroupDescription);
            this.groupBoxGroup.Location = new System.Drawing.Point(10, 10);
            this.groupBoxGroup.Name = "groupBoxGroup";
            this.groupBoxGroup.Size = new System.Drawing.Size(330, 80);
            this.groupBoxGroup.TabIndex = 1;
            this.groupBoxGroup.TabStop = false;
            this.groupBoxGroup.Text = "Encounter Group";
            // 
            // comboBoxGroup
            // 
            this.comboBoxGroup.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxGroup.FormattingEnabled = true;
            this.comboBoxGroup.Location = new System.Drawing.Point(10, 22);
            this.comboBoxGroup.Name = "comboBoxGroup";
            this.comboBoxGroup.Size = new System.Drawing.Size(200, 21);
            this.comboBoxGroup.TabIndex = 0;
            this.comboBoxGroup.SelectedIndexChanged += new System.EventHandler(this.comboBoxGroup_SelectedIndexChanged);
            // 
            // labelGroupDescription
            // 
            this.labelGroupDescription.Location = new System.Drawing.Point(10, 50);
            this.labelGroupDescription.Name = "labelGroupDescription";
            this.labelGroupDescription.Size = new System.Drawing.Size(310, 26);
            this.labelGroupDescription.TabIndex = 1;
            this.labelGroupDescription.Text = "Select a group to view its encounters.";
            // 
            // groupBoxEncounters
            // 
            this.groupBoxEncounters.Controls.Add(this.listBoxEncounters);
            this.groupBoxEncounters.Location = new System.Drawing.Point(10, 100);
            this.groupBoxEncounters.Name = "groupBoxEncounters";
            this.groupBoxEncounters.Size = new System.Drawing.Size(330, 180);
            this.groupBoxEncounters.TabIndex = 2;
            this.groupBoxEncounters.TabStop = false;
            this.groupBoxEncounters.Text = "Encounter Slots";
            // 
            // listBoxEncounters
            // 
            this.listBoxEncounters.FormattingEnabled = true;
            this.listBoxEncounters.Location = new System.Drawing.Point(10, 20);
            this.listBoxEncounters.Name = "listBoxEncounters";
            this.listBoxEncounters.Size = new System.Drawing.Size(310, 147);
            this.listBoxEncounters.TabIndex = 0;
            this.listBoxEncounters.SelectedIndexChanged += new System.EventHandler(this.listBoxEncounters_SelectedIndexChanged);
            // 
            // groupBoxDetails
            // 
            this.groupBoxDetails.Controls.Add(this.pictureBoxPokemon);
            this.groupBoxDetails.Controls.Add(this.labelSpecies);
            this.groupBoxDetails.Controls.Add(this.comboBoxSpecies);
            this.groupBoxDetails.Controls.Add(this.labelEncounterRate);
            this.groupBoxDetails.Controls.Add(this.buttonRateHelp);
            this.groupBoxDetails.Location = new System.Drawing.Point(350, 10);
            this.groupBoxDetails.Name = "groupBoxDetails";
            this.groupBoxDetails.Size = new System.Drawing.Size(330, 180);
            this.groupBoxDetails.TabIndex = 3;
            this.groupBoxDetails.TabStop = false;
            this.groupBoxDetails.Text = "Slot Details";
            // 
            // pictureBoxPokemon
            // 
            this.pictureBoxPokemon.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureBoxPokemon.Location = new System.Drawing.Point(220, 20);
            this.pictureBoxPokemon.Name = "pictureBoxPokemon";
            this.pictureBoxPokemon.Size = new System.Drawing.Size(96, 96);
            this.pictureBoxPokemon.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.pictureBoxPokemon.TabIndex = 0;
            this.pictureBoxPokemon.TabStop = false;
            // 
            // labelSpecies
            // 
            this.labelSpecies.AutoSize = true;
            this.labelSpecies.Location = new System.Drawing.Point(10, 25);
            this.labelSpecies.Name = "labelSpecies";
            this.labelSpecies.Size = new System.Drawing.Size(48, 13);
            this.labelSpecies.TabIndex = 1;
            this.labelSpecies.Text = "Species:";
            // 
            // comboBoxSpecies
            // 
            this.comboBoxSpecies.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxSpecies.FormattingEnabled = true;
            this.comboBoxSpecies.Location = new System.Drawing.Point(10, 45);
            this.comboBoxSpecies.Name = "comboBoxSpecies";
            this.comboBoxSpecies.Size = new System.Drawing.Size(190, 21);
            this.comboBoxSpecies.TabIndex = 2;
            this.comboBoxSpecies.SelectedIndexChanged += new System.EventHandler(this.comboBoxSpecies_SelectedIndexChanged);
            // 
            // labelEncounterRate
            // 
            this.labelEncounterRate.AutoSize = true;
            this.labelEncounterRate.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold);
            this.labelEncounterRate.Location = new System.Drawing.Point(10, 85);
            this.labelEncounterRate.Name = "labelEncounterRate";
            this.labelEncounterRate.Size = new System.Drawing.Size(147, 16);
            this.labelEncounterRate.TabIndex = 3;
            this.labelEncounterRate.Text = "Encounter Rate: N/A";
            // 
            // buttonRateHelp
            // 
            this.buttonRateHelp.Location = new System.Drawing.Point(10, 120);
            this.buttonRateHelp.Name = "buttonRateHelp";
            this.buttonRateHelp.Size = new System.Drawing.Size(120, 30);
            this.buttonRateHelp.TabIndex = 4;
            this.buttonRateHelp.Text = "Rate System Help";
            this.buttonRateHelp.UseVisualStyleBackColor = true;
            this.buttonRateHelp.Click += new System.EventHandler(this.buttonRateHelp_Click);
            // 
            // buttonSave
            // 
            this.buttonSave.Image = global::DSPRE.Properties.Resources.saveButton;
            this.buttonSave.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.buttonSave.Location = new System.Drawing.Point(10, 355);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(75, 30);
            this.buttonSave.TabIndex = 4;
            this.buttonSave.Text = "Save";
            this.buttonSave.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // buttonExport
            // 
            this.buttonExport.Image = global::DSPRE.Properties.Resources.exportArrow;
            this.buttonExport.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.buttonExport.Location = new System.Drawing.Point(95, 355);
            this.buttonExport.Name = "buttonExport";
            this.buttonExport.Size = new System.Drawing.Size(75, 30);
            this.buttonExport.TabIndex = 5;
            this.buttonExport.Text = "Export";
            this.buttonExport.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonExport.UseVisualStyleBackColor = true;
            this.buttonExport.Click += new System.EventHandler(this.buttonExport_Click);
            // 
            // buttonImport
            // 
            this.buttonImport.Image = global::DSPRE.Properties.Resources.importArrow;
            this.buttonImport.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.buttonImport.Location = new System.Drawing.Point(180, 355);
            this.buttonImport.Name = "buttonImport";
            this.buttonImport.Size = new System.Drawing.Size(75, 30);
            this.buttonImport.TabIndex = 6;
            this.buttonImport.Text = "Import";
            this.buttonImport.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonImport.UseVisualStyleBackColor = true;
            this.buttonImport.Click += new System.EventHandler(this.buttonImport_Click);
            // 
            // buttonLocate
            // 
            this.buttonLocate.Image = global::DSPRE.Properties.Resources.SearchMiniIcon;
            this.buttonLocate.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.buttonLocate.Location = new System.Drawing.Point(265, 355);
            this.buttonLocate.Name = "buttonLocate";
            this.buttonLocate.Size = new System.Drawing.Size(75, 30);
            this.buttonLocate.TabIndex = 7;
            this.buttonLocate.Text = "Locate";
            this.buttonLocate.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonLocate.UseVisualStyleBackColor = true;
            this.buttonLocate.Click += new System.EventHandler(this.buttonLocate_Click);
            // 
            // HoneyTreeEncounterEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.labelNotAvailable);
            this.Controls.Add(this.panelMain);
            this.Name = "HoneyTreeEncounterEditor";
            this.Size = new System.Drawing.Size(700, 400);
            this.panelMain.ResumeLayout(false);
            this.groupBoxInfo.ResumeLayout(false);
            this.groupBoxGroup.ResumeLayout(false);
            this.groupBoxEncounters.ResumeLayout(false);
            this.groupBoxDetails.ResumeLayout(false);
            this.groupBoxDetails.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxPokemon)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelNotAvailable;
        private System.Windows.Forms.Panel panelMain;
        private System.Windows.Forms.GroupBox groupBoxInfo;
        private System.Windows.Forms.Label labelInfo;
        private System.Windows.Forms.GroupBox groupBoxGroup;
        private System.Windows.Forms.ComboBox comboBoxGroup;
        private System.Windows.Forms.Label labelGroupDescription;
        private System.Windows.Forms.GroupBox groupBoxEncounters;
        private System.Windows.Forms.ListBox listBoxEncounters;
        private System.Windows.Forms.GroupBox groupBoxDetails;
        private System.Windows.Forms.PictureBox pictureBoxPokemon;
        private System.Windows.Forms.Label labelSpecies;
        private System.Windows.Forms.ComboBox comboBoxSpecies;
        private System.Windows.Forms.Label labelEncounterRate;
        private System.Windows.Forms.Button buttonRateHelp;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.Button buttonExport;
        private System.Windows.Forms.Button buttonImport;
        private System.Windows.Forms.Button buttonLocate;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
