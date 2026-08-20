namespace DSPRE.Editors.BtxEditor
{
    partial class BtxEditor
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.overworldList = new DSPRE.InputComboBox();
            this.overworldPictureBox = new System.Windows.Forms.PictureBox();
            this.showBtxFileButton = new System.Windows.Forms.Button();
            this.exportImagePng = new System.Windows.Forms.Button();
            this.importImagePng = new System.Windows.Forms.Button();
            this.shinyCheckbox = new System.Windows.Forms.CheckBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.saveSelected_Button = new System.Windows.Forms.Button();
            this.SaveAll_Button = new System.Windows.Forms.Button();
            this.overworldPropertiesGroupBox = new System.Windows.Forms.GroupBox();
            this.expansionStatusLabel = new System.Windows.Forms.Label();
            this.drawTypeLabel = new System.Windows.Forms.Label();
            this.drawTypeCombo = new System.Windows.Forms.ComboBox();
            this.shadowTypeLabel = new System.Windows.Forms.Label();
            this.shadowTypeCombo = new System.Windows.Forms.ComboBox();
            this.footmarkTypeLabel = new System.Windows.Forms.Label();
            this.footmarkTypeCombo = new System.Windows.Forms.ComboBox();
            this.reflectTypeLabel = new System.Windows.Forms.Label();
            this.reflectTypeCombo = new System.Windows.Forms.ComboBox();
            this.rendererInfoLabel = new System.Windows.Forms.Label();
            this.animationInfoLabel = new System.Windows.Forms.Label();
            this.addEntryButton = new System.Windows.Forms.Button();
            this.deleteEntryButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.overworldPictureBox)).BeginInit();
            this.panel1.SuspendLayout();
            this.overworldPropertiesGroupBox.SuspendLayout();
            this.SuspendLayout();
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(55, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Overworld";
            //
            // overworldList
            //
            this.overworldList.FormattingEnabled = true;
            this.overworldList.Location = new System.Drawing.Point(12, 29);
            this.overworldList.Name = "overworldList";
            this.overworldList.Size = new System.Drawing.Size(125, 21);
            this.overworldList.TabIndex = 1;
            this.overworldList.SelectedIndexChanged += new System.EventHandler(this.overworldList_SelectedIndexChanged);
            //
            // overworldPictureBox
            //
            this.overworldPictureBox.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.overworldPictureBox.Location = new System.Drawing.Point(3, 0);
            this.overworldPictureBox.Name = "overworldPictureBox";
            this.overworldPictureBox.Size = new System.Drawing.Size(117, 209);
            this.overworldPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.overworldPictureBox.TabIndex = 2;
            this.overworldPictureBox.TabStop = false;
            //
            // showBtxFileButton
            //
            this.showBtxFileButton.Enabled = false;
            this.showBtxFileButton.Image = global::DSPRE.Properties.Resources.lens;
            this.showBtxFileButton.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.showBtxFileButton.Location = new System.Drawing.Point(148, 27);
            this.showBtxFileButton.Name = "showBtxFileButton";
            this.showBtxFileButton.Size = new System.Drawing.Size(121, 23);
            this.showBtxFileButton.TabIndex = 3;
            this.showBtxFileButton.Text = "Show BTX File";
            this.showBtxFileButton.TextImageRelation = System.Windows.Forms.TextImageRelation.TextBeforeImage;
            this.showBtxFileButton.UseVisualStyleBackColor = true;
            this.showBtxFileButton.Click += new System.EventHandler(this.showBtxFileButton_Click);
            //
            // exportImagePng
            //
            this.exportImagePng.Enabled = false;
            this.exportImagePng.Image = global::DSPRE.Properties.Resources.exportArrow;
            this.exportImagePng.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.exportImagePng.Location = new System.Drawing.Point(148, 145);
            this.exportImagePng.Name = "exportImagePng";
            this.exportImagePng.Size = new System.Drawing.Size(121, 23);
            this.exportImagePng.TabIndex = 4;
            this.exportImagePng.Text = "Export PNG";
            this.exportImagePng.TextImageRelation = System.Windows.Forms.TextImageRelation.TextBeforeImage;
            this.exportImagePng.UseVisualStyleBackColor = true;
            this.exportImagePng.Click += new System.EventHandler(this.exportImagePng_Click);
            //
            // importImagePng
            //
            this.importImagePng.Enabled = false;
            this.importImagePng.Image = global::DSPRE.Properties.Resources.importArrow;
            this.importImagePng.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.importImagePng.Location = new System.Drawing.Point(16, 145);
            this.importImagePng.Name = "importImagePng";
            this.importImagePng.Size = new System.Drawing.Size(121, 23);
            this.importImagePng.TabIndex = 5;
            this.importImagePng.Text = "Import PNG";
            this.importImagePng.TextImageRelation = System.Windows.Forms.TextImageRelation.TextBeforeImage;
            this.importImagePng.UseVisualStyleBackColor = true;
            this.importImagePng.Click += new System.EventHandler(this.importImagePng_Click);
            //
            // shinyCheckbox
            //
            this.shinyCheckbox.AutoSize = true;
            this.shinyCheckbox.Enabled = false;
            this.shinyCheckbox.Location = new System.Drawing.Point(13, 57);
            this.shinyCheckbox.Name = "shinyCheckbox";
            this.shinyCheckbox.Size = new System.Drawing.Size(52, 17);
            this.shinyCheckbox.TabIndex = 6;
            this.shinyCheckbox.Text = "Shiny";
            this.shinyCheckbox.UseVisualStyleBackColor = true;
            this.shinyCheckbox.CheckedChanged += new System.EventHandler(this.shinyCheckbox_CheckedChanged);
            //
            // panel1
            //
            this.panel1.AutoScroll = true;
            this.panel1.Controls.Add(this.overworldPictureBox);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Right;
            this.panel1.Location = new System.Drawing.Point(283, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(123, 350);
            this.panel1.TabIndex = 7;
            //
            // saveSelected_Button
            //
            this.saveSelected_Button.Image = global::DSPRE.Properties.Resources.saveButton;
            this.saveSelected_Button.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.saveSelected_Button.Location = new System.Drawing.Point(16, 174);
            this.saveSelected_Button.Name = "saveSelected_Button";
            this.saveSelected_Button.Size = new System.Drawing.Size(121, 23);
            this.saveSelected_Button.TabIndex = 8;
            this.saveSelected_Button.Text = "Save Selected";
            this.saveSelected_Button.TextImageRelation = System.Windows.Forms.TextImageRelation.TextBeforeImage;
            this.saveSelected_Button.UseVisualStyleBackColor = true;
            this.saveSelected_Button.Click += new System.EventHandler(this.saveSelected_Button_Click);
            //
            // SaveAll_Button
            //
            this.SaveAll_Button.Image = global::DSPRE.Properties.Resources.saveButton;
            this.SaveAll_Button.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.SaveAll_Button.Location = new System.Drawing.Point(148, 174);
            this.SaveAll_Button.Name = "SaveAll_Button";
            this.SaveAll_Button.Size = new System.Drawing.Size(121, 23);
            this.SaveAll_Button.TabIndex = 9;
            this.SaveAll_Button.Text = "Save All";
            this.SaveAll_Button.TextImageRelation = System.Windows.Forms.TextImageRelation.TextBeforeImage;
            this.SaveAll_Button.UseVisualStyleBackColor = true;
            this.SaveAll_Button.Click += new System.EventHandler(this.SaveAll_Button_Click);
            //
            // overworldPropertiesGroupBox
            //
            this.overworldPropertiesGroupBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)));
            this.overworldPropertiesGroupBox.Controls.Add(this.expansionStatusLabel);
            this.overworldPropertiesGroupBox.Controls.Add(this.drawTypeLabel);
            this.overworldPropertiesGroupBox.Controls.Add(this.drawTypeCombo);
            this.overworldPropertiesGroupBox.Controls.Add(this.shadowTypeLabel);
            this.overworldPropertiesGroupBox.Controls.Add(this.shadowTypeCombo);
            this.overworldPropertiesGroupBox.Controls.Add(this.footmarkTypeLabel);
            this.overworldPropertiesGroupBox.Controls.Add(this.footmarkTypeCombo);
            this.overworldPropertiesGroupBox.Controls.Add(this.reflectTypeLabel);
            this.overworldPropertiesGroupBox.Controls.Add(this.reflectTypeCombo);
            this.overworldPropertiesGroupBox.Controls.Add(this.rendererInfoLabel);
            this.overworldPropertiesGroupBox.Controls.Add(this.animationInfoLabel);
            this.overworldPropertiesGroupBox.Controls.Add(this.addEntryButton);
            this.overworldPropertiesGroupBox.Controls.Add(this.deleteEntryButton);
            this.overworldPropertiesGroupBox.Location = new System.Drawing.Point(280, 8);
            this.overworldPropertiesGroupBox.Name = "overworldPropertiesGroupBox";
            this.overworldPropertiesGroupBox.Size = new System.Drawing.Size(270, 334);
            this.overworldPropertiesGroupBox.TabIndex = 10;
            this.overworldPropertiesGroupBox.TabStop = false;
            this.overworldPropertiesGroupBox.Text = "Overworld properties";
            //
            // expansionStatusLabel
            //
            this.expansionStatusLabel.Location = new System.Drawing.Point(8, 20);
            this.expansionStatusLabel.Name = "expansionStatusLabel";
            this.expansionStatusLabel.Size = new System.Drawing.Size(254, 32);
            this.expansionStatusLabel.TabIndex = 0;
            //
            // drawTypeLabel
            //
            this.drawTypeLabel.AutoSize = true;
            this.drawTypeLabel.Location = new System.Drawing.Point(8, 56);
            this.drawTypeLabel.Name = "drawTypeLabel";
            this.drawTypeLabel.Size = new System.Drawing.Size(52, 13);
            this.drawTypeLabel.TabIndex = 1;
            this.drawTypeLabel.Text = "Draw type";
            //
            // drawTypeCombo
            //
            this.drawTypeCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.drawTypeCombo.FormattingEnabled = true;
            this.drawTypeCombo.Items.AddRange(new object[] {
            "None",
            "Billboard",
            "3D model"});
            this.drawTypeCombo.Location = new System.Drawing.Point(8, 71);
            this.drawTypeCombo.Name = "drawTypeCombo";
            this.drawTypeCombo.Size = new System.Drawing.Size(254, 21);
            this.drawTypeCombo.TabIndex = 2;
            this.drawTypeCombo.SelectedIndexChanged += new System.EventHandler(this.RenderStateCombo_SelectedIndexChanged);
            //
            // shadowTypeLabel
            //
            this.shadowTypeLabel.AutoSize = true;
            this.shadowTypeLabel.Location = new System.Drawing.Point(8, 98);
            this.shadowTypeLabel.Name = "shadowTypeLabel";
            this.shadowTypeLabel.Size = new System.Drawing.Size(43, 13);
            this.shadowTypeLabel.TabIndex = 3;
            this.shadowTypeLabel.Text = "Shadow";
            //
            // shadowTypeCombo
            //
            this.shadowTypeCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.shadowTypeCombo.FormattingEnabled = true;
            this.shadowTypeCombo.Items.AddRange(new object[] {
            "None",
            "On"});
            this.shadowTypeCombo.Location = new System.Drawing.Point(8, 113);
            this.shadowTypeCombo.Name = "shadowTypeCombo";
            this.shadowTypeCombo.Size = new System.Drawing.Size(254, 21);
            this.shadowTypeCombo.TabIndex = 4;
            this.shadowTypeCombo.SelectedIndexChanged += new System.EventHandler(this.RenderStateCombo_SelectedIndexChanged);
            //
            // footmarkTypeLabel
            //
            this.footmarkTypeLabel.AutoSize = true;
            this.footmarkTypeLabel.Location = new System.Drawing.Point(8, 140);
            this.footmarkTypeLabel.Name = "footmarkTypeLabel";
            this.footmarkTypeLabel.Size = new System.Drawing.Size(50, 13);
            this.footmarkTypeLabel.TabIndex = 5;
            this.footmarkTypeLabel.Text = "Footmark";
            //
            // footmarkTypeCombo
            //
            this.footmarkTypeCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.footmarkTypeCombo.FormattingEnabled = true;
            this.footmarkTypeCombo.Items.AddRange(new object[] {
            "None",
            "Normal (2-leg)",
            "Cycle (bike)"});
            this.footmarkTypeCombo.Location = new System.Drawing.Point(8, 155);
            this.footmarkTypeCombo.Name = "footmarkTypeCombo";
            this.footmarkTypeCombo.Size = new System.Drawing.Size(254, 21);
            this.footmarkTypeCombo.TabIndex = 6;
            this.footmarkTypeCombo.SelectedIndexChanged += new System.EventHandler(this.RenderStateCombo_SelectedIndexChanged);
            //
            // reflectTypeLabel
            //
            this.reflectTypeLabel.AutoSize = true;
            this.reflectTypeLabel.Location = new System.Drawing.Point(8, 182);
            this.reflectTypeLabel.Name = "reflectTypeLabel";
            this.reflectTypeLabel.Size = new System.Drawing.Size(53, 13);
            this.reflectTypeLabel.TabIndex = 7;
            this.reflectTypeLabel.Text = "Reflection";
            //
            // reflectTypeCombo
            //
            this.reflectTypeCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.reflectTypeCombo.FormattingEnabled = true;
            this.reflectTypeCombo.Items.AddRange(new object[] {
            "None",
            "On (billboard reflection)"});
            this.reflectTypeCombo.Location = new System.Drawing.Point(8, 197);
            this.reflectTypeCombo.Name = "reflectTypeCombo";
            this.reflectTypeCombo.Size = new System.Drawing.Size(254, 21);
            this.reflectTypeCombo.TabIndex = 8;
            this.reflectTypeCombo.SelectedIndexChanged += new System.EventHandler(this.RenderStateCombo_SelectedIndexChanged);
            //
            // rendererInfoLabel
            //
            this.rendererInfoLabel.AutoEllipsis = true;
            this.rendererInfoLabel.Font = new System.Drawing.Font("Consolas", 7.5F);
            this.rendererInfoLabel.Location = new System.Drawing.Point(8, 224);
            this.rendererInfoLabel.Name = "rendererInfoLabel";
            this.rendererInfoLabel.Size = new System.Drawing.Size(254, 16);
            this.rendererInfoLabel.TabIndex = 9;
            //
            // animationInfoLabel
            //
            this.animationInfoLabel.AutoEllipsis = true;
            this.animationInfoLabel.Font = new System.Drawing.Font("Consolas", 7.5F);
            this.animationInfoLabel.Location = new System.Drawing.Point(8, 240);
            this.animationInfoLabel.Name = "animationInfoLabel";
            this.animationInfoLabel.Size = new System.Drawing.Size(254, 16);
            this.animationInfoLabel.TabIndex = 10;
            //
            // addEntryButton
            //
            this.addEntryButton.Location = new System.Drawing.Point(8, 262);
            this.addEntryButton.Name = "addEntryButton";
            this.addEntryButton.Size = new System.Drawing.Size(254, 23);
            this.addEntryButton.TabIndex = 11;
            this.addEntryButton.Text = "Add Custom Entry...";
            this.addEntryButton.UseVisualStyleBackColor = true;
            this.addEntryButton.Click += new System.EventHandler(this.AddEntryButton_Click);
            //
            // deleteEntryButton
            //
            this.deleteEntryButton.Enabled = false;
            this.deleteEntryButton.Location = new System.Drawing.Point(8, 291);
            this.deleteEntryButton.Name = "deleteEntryButton";
            this.deleteEntryButton.Size = new System.Drawing.Size(254, 23);
            this.deleteEntryButton.TabIndex = 12;
            this.deleteEntryButton.Text = "Delete Selected Entry";
            this.deleteEntryButton.UseVisualStyleBackColor = true;
            this.deleteEntryButton.Click += new System.EventHandler(this.DeleteEntryButton_Click);
            //
            // BtxEditor
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(700, 350);
            this.Controls.Add(this.overworldPropertiesGroupBox);
            this.Controls.Add(this.SaveAll_Button);
            this.Controls.Add(this.saveSelected_Button);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.shinyCheckbox);
            this.Controls.Add(this.importImagePng);
            this.Controls.Add(this.exportImagePng);
            this.Controls.Add(this.showBtxFileButton);
            this.Controls.Add(this.overworldList);
            this.Controls.Add(this.label1);
            this.MinimumSize = new System.Drawing.Size(716, 389);
            this.Name = "BtxEditor";
            this.ShowIcon = false;
            this.Text = "Overworld Editor";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.BtxEditor_FormClosing);
            ((System.ComponentModel.ISupportInitialize)(this.overworldPictureBox)).EndInit();
            this.panel1.ResumeLayout(false);
            this.overworldPropertiesGroupBox.ResumeLayout(false);
            this.overworldPropertiesGroupBox.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private DSPRE.InputComboBox overworldList;
        private System.Windows.Forms.PictureBox overworldPictureBox;
        private System.Windows.Forms.Button showBtxFileButton;
        private System.Windows.Forms.Button exportImagePng;
        private System.Windows.Forms.Button importImagePng;
        private System.Windows.Forms.CheckBox shinyCheckbox;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button saveSelected_Button;
        private System.Windows.Forms.Button SaveAll_Button;
        private System.Windows.Forms.GroupBox overworldPropertiesGroupBox;
        private System.Windows.Forms.Label expansionStatusLabel;
        private System.Windows.Forms.Label drawTypeLabel;
        private System.Windows.Forms.ComboBox drawTypeCombo;
        private System.Windows.Forms.Label shadowTypeLabel;
        private System.Windows.Forms.ComboBox shadowTypeCombo;
        private System.Windows.Forms.Label footmarkTypeLabel;
        private System.Windows.Forms.ComboBox footmarkTypeCombo;
        private System.Windows.Forms.Label reflectTypeLabel;
        private System.Windows.Forms.ComboBox reflectTypeCombo;
        private System.Windows.Forms.Label rendererInfoLabel;
        private System.Windows.Forms.Label animationInfoLabel;
        private System.Windows.Forms.Button addEntryButton;
        private System.Windows.Forms.Button deleteEntryButton;
    }
}
