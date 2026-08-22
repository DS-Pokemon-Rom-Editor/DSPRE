namespace DSPRE.Editors
{
    partial class RockSmashEditor
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.listBoxHeaders = new System.Windows.Forms.ListBox();
            this.labelOddsCaption = new System.Windows.Forms.Label();
            this.numericOdds = new System.Windows.Forms.NumericUpDown();
            this.labelTypeCaption = new System.Windows.Forms.Label();
            this.comboBoxType = new System.Windows.Forms.ComboBox();
            this.labelStatus = new System.Windows.Forms.Label();
            this.buttonSave = new System.Windows.Forms.Button();
            this.groupBoxRuinsOfAlph = new System.Windows.Forms.GroupBox();
            this.groupBoxDefault = new System.Windows.Forms.GroupBox();
            this.groupBoxCliffCave = new System.Windows.Forms.GroupBox();
            this.labelItemTablesUnavailable = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.numericOdds)).BeginInit();
            this.SuspendLayout();
            // 
            // listBoxHeaders
            // 
            this.listBoxHeaders.FormattingEnabled = true;
            this.listBoxHeaders.Location = new System.Drawing.Point(9, 9);
            this.listBoxHeaders.Name = "listBoxHeaders";
            this.listBoxHeaders.Size = new System.Drawing.Size(215, 498);
            this.listBoxHeaders.TabIndex = 0;
            this.listBoxHeaders.SelectedIndexChanged += new System.EventHandler(this.listBoxHeaders_SelectedIndexChanged);
            // 
            // labelOddsCaption
            // 
            this.labelOddsCaption.AutoSize = true;
            this.labelOddsCaption.Location = new System.Drawing.Point(240, 11);
            this.labelOddsCaption.Name = "labelOddsCaption";
            this.labelOddsCaption.Size = new System.Drawing.Size(84, 13);
            this.labelOddsCaption.TabIndex = 1;
            this.labelOddsCaption.Text = "Item Drop Odds:";
            // 
            // numericOdds
            // 
            this.numericOdds.Location = new System.Drawing.Point(240, 28);
            this.numericOdds.Name = "numericOdds";
            this.numericOdds.Size = new System.Drawing.Size(77, 20);
            this.numericOdds.TabIndex = 2;
            this.numericOdds.ValueChanged += new System.EventHandler(this.numericOdds_ValueChanged);
            // 
            // labelTypeCaption
            // 
            this.labelTypeCaption.AutoSize = true;
            this.labelTypeCaption.Location = new System.Drawing.Point(334, 11);
            this.labelTypeCaption.Name = "labelTypeCaption";
            this.labelTypeCaption.Size = new System.Drawing.Size(60, 13);
            this.labelTypeCaption.TabIndex = 3;
            this.labelTypeCaption.Text = "Item Table:";
            // 
            // comboBoxType
            // 
            this.comboBoxType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxType.FormattingEnabled = true;
            this.comboBoxType.Location = new System.Drawing.Point(334, 28);
            this.comboBoxType.Name = "comboBoxType";
            this.comboBoxType.Size = new System.Drawing.Size(138, 21);
            this.comboBoxType.TabIndex = 4;
            this.comboBoxType.SelectedIndexChanged += new System.EventHandler(this.comboBoxType_SelectedIndexChanged);
            // 
            // labelStatus
            // 
            this.labelStatus.AutoSize = true;
            this.labelStatus.ForeColor = System.Drawing.Color.DarkOrange;
            this.labelStatus.Location = new System.Drawing.Point(240, 54);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(0, 13);
            this.labelStatus.TabIndex = 5;
            // 
            // buttonSave
            // 
            this.buttonSave.Location = new System.Drawing.Point(240, 472);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(120, 26);
            this.buttonSave.TabIndex = 6;
            this.buttonSave.Text = "Save Changes";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // groupBoxRuinsOfAlph
            // 
            this.groupBoxRuinsOfAlph.Location = new System.Drawing.Point(240, 82);
            this.groupBoxRuinsOfAlph.Name = "groupBoxRuinsOfAlph";
            this.groupBoxRuinsOfAlph.Size = new System.Drawing.Size(743, 95);
            this.groupBoxRuinsOfAlph.TabIndex = 7;
            this.groupBoxRuinsOfAlph.TabStop = false;
            this.groupBoxRuinsOfAlph.Text = "Ruins of Alph Table";
            // 
            // groupBoxDefault
            // 
            this.groupBoxDefault.Location = new System.Drawing.Point(240, 186);
            this.groupBoxDefault.Name = "groupBoxDefault";
            this.groupBoxDefault.Size = new System.Drawing.Size(743, 95);
            this.groupBoxDefault.TabIndex = 8;
            this.groupBoxDefault.TabStop = false;
            this.groupBoxDefault.Text = "Default Table";
            // 
            // groupBoxCliffCave
            // 
            this.groupBoxCliffCave.Location = new System.Drawing.Point(240, 290);
            this.groupBoxCliffCave.Name = "groupBoxCliffCave";
            this.groupBoxCliffCave.Size = new System.Drawing.Size(743, 95);
            this.groupBoxCliffCave.TabIndex = 9;
            this.groupBoxCliffCave.TabStop = false;
            this.groupBoxCliffCave.Text = "Cliff Cave Table";
            // 
            // labelItemTablesUnavailable
            // 
            this.labelItemTablesUnavailable.AutoSize = true;
            this.labelItemTablesUnavailable.Location = new System.Drawing.Point(240, 82);
            this.labelItemTablesUnavailable.Name = "labelItemTablesUnavailable";
            this.labelItemTablesUnavailable.Size = new System.Drawing.Size(384, 13);
            this.labelItemTablesUnavailable.TabIndex = 10;
            this.labelItemTablesUnavailable.Text = "The item-drop tables are only editable on the English HeartGold/SoulSilver build." +
    "";
            this.labelItemTablesUnavailable.Visible = false;
            // 
            // RockSmashEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.labelItemTablesUnavailable);
            this.Controls.Add(this.groupBoxCliffCave);
            this.Controls.Add(this.groupBoxDefault);
            this.Controls.Add(this.groupBoxRuinsOfAlph);
            this.Controls.Add(this.buttonSave);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.comboBoxType);
            this.Controls.Add(this.labelTypeCaption);
            this.Controls.Add(this.numericOdds);
            this.Controls.Add(this.labelOddsCaption);
            this.Controls.Add(this.listBoxHeaders);
            this.Name = "RockSmashEditor";
            this.Size = new System.Drawing.Size(1079, 536);
            ((System.ComponentModel.ISupportInitialize)(this.numericOdds)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListBox listBoxHeaders;
        private System.Windows.Forms.Label labelOddsCaption;
        private System.Windows.Forms.NumericUpDown numericOdds;
        private System.Windows.Forms.Label labelTypeCaption;
        private System.Windows.Forms.ComboBox comboBoxType;
        private System.Windows.Forms.Label labelStatus;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.GroupBox groupBoxRuinsOfAlph;
        private System.Windows.Forms.GroupBox groupBoxDefault;
        private System.Windows.Forms.GroupBox groupBoxCliffCave;
        private System.Windows.Forms.Label labelItemTablesUnavailable;
    }
}
