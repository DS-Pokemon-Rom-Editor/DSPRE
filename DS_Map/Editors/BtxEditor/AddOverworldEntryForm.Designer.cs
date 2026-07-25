namespace DSPRE.Editors.BtxEditor
{
    partial class AddOverworldEntryForm
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
            this.hintLabel = new System.Windows.Forms.Label();
            this.appearanceIdLabel = new System.Windows.Forms.Label();
            this.appearanceIdTextBox = new System.Windows.Forms.TextBox();
            this.imageLabel = new System.Windows.Forms.Label();
            this.choosePngButton = new System.Windows.Forms.Button();
            this.chooseRawBtxButton = new System.Windows.Forms.Button();
            this.clearImageButton = new System.Windows.Forms.Button();
            this.imagePreview = new System.Windows.Forms.PictureBox();
            this.imageInfoLabel = new System.Windows.Forms.Label();
            this.slotLabel = new System.Windows.Forms.Label();
            this.slotCombo = new System.Windows.Forms.ComboBox();
            this.cloneLabel = new System.Windows.Forms.Label();
            this.cloneCombo = new System.Windows.Forms.ComboBox();
            this.statusLabel = new System.Windows.Forms.Label();
            this.addButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.imagePreview)).BeginInit();
            this.SuspendLayout();
            //
            // hintLabel
            //
            this.hintLabel.ForeColor = System.Drawing.SystemColors.GrayText;
            this.hintLabel.Location = new System.Drawing.Point(12, 12);
            this.hintLabel.Name = "hintLabel";
            this.hintLabel.Size = new System.Drawing.Size(396, 30);
            this.hintLabel.TabIndex = 0;
            this.hintLabel.Text = "Requires the hzla PlatPatches overworld-sprites expansion patch and a free custo" +
    "m slot.";
            //
            // appearanceIdLabel
            //
            this.appearanceIdLabel.AutoSize = true;
            this.appearanceIdLabel.ForeColor = System.Drawing.SystemColors.GrayText;
            this.appearanceIdLabel.Location = new System.Drawing.Point(12, 50);
            this.appearanceIdLabel.Name = "appearanceIdLabel";
            this.appearanceIdLabel.Size = new System.Drawing.Size(155, 13);
            this.appearanceIdLabel.TabIndex = 1;
            this.appearanceIdLabel.Text = "Appearance ID (decimal or 0x hex)";
            //
            // appearanceIdTextBox
            //
            this.appearanceIdTextBox.Location = new System.Drawing.Point(12, 66);
            this.appearanceIdTextBox.Name = "appearanceIdTextBox";
            this.appearanceIdTextBox.Size = new System.Drawing.Size(396, 20);
            this.appearanceIdTextBox.TabIndex = 2;
            //
            // imageLabel
            //
            this.imageLabel.ForeColor = System.Drawing.SystemColors.GrayText;
            this.imageLabel.Location = new System.Drawing.Point(12, 96);
            this.imageLabel.Name = "imageLabel";
            this.imageLabel.Size = new System.Drawing.Size(396, 30);
            this.imageLabel.TabIndex = 3;
            this.imageLabel.Text = "Image (optional - pick this first, it decides which texture slot below will fit" +
    ")";
            //
            // choosePngButton
            //
            this.choosePngButton.Location = new System.Drawing.Point(12, 130);
            this.choosePngButton.Name = "choosePngButton";
            this.choosePngButton.Size = new System.Drawing.Size(110, 23);
            this.choosePngButton.TabIndex = 4;
            this.choosePngButton.Text = "Choose PNG...";
            this.choosePngButton.UseVisualStyleBackColor = true;
            this.choosePngButton.Click += new System.EventHandler(this.ChoosePngButton_Click);
            //
            // chooseRawBtxButton
            //
            this.chooseRawBtxButton.Location = new System.Drawing.Point(128, 130);
            this.chooseRawBtxButton.Name = "chooseRawBtxButton";
            this.chooseRawBtxButton.Size = new System.Drawing.Size(150, 23);
            this.chooseRawBtxButton.TabIndex = 5;
            this.chooseRawBtxButton.Text = "Choose Raw Texture...";
            this.chooseRawBtxButton.UseVisualStyleBackColor = true;
            this.chooseRawBtxButton.Click += new System.EventHandler(this.ChooseRawBtxButton_Click);
            //
            // clearImageButton
            //
            this.clearImageButton.Enabled = false;
            this.clearImageButton.Location = new System.Drawing.Point(284, 130);
            this.clearImageButton.Name = "clearImageButton";
            this.clearImageButton.Size = new System.Drawing.Size(70, 23);
            this.clearImageButton.TabIndex = 6;
            this.clearImageButton.Text = "Clear";
            this.clearImageButton.UseVisualStyleBackColor = true;
            this.clearImageButton.Click += new System.EventHandler(this.ClearImageButton_Click);
            //
            // imagePreview
            //
            this.imagePreview.BackColor = System.Drawing.Color.WhiteSmoke;
            this.imagePreview.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.imagePreview.Location = new System.Drawing.Point(12, 160);
            this.imagePreview.Name = "imagePreview";
            this.imagePreview.Size = new System.Drawing.Size(396, 120);
            this.imagePreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.imagePreview.TabIndex = 7;
            this.imagePreview.TabStop = false;
            //
            // imageInfoLabel
            //
            this.imageInfoLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold);
            this.imageInfoLabel.Location = new System.Drawing.Point(12, 284);
            this.imageInfoLabel.Name = "imageInfoLabel";
            this.imageInfoLabel.Size = new System.Drawing.Size(396, 32);
            this.imageInfoLabel.TabIndex = 8;
            //
            // slotLabel
            //
            this.slotLabel.AutoSize = true;
            this.slotLabel.ForeColor = System.Drawing.SystemColors.GrayText;
            this.slotLabel.Location = new System.Drawing.Point(12, 320);
            this.slotLabel.Name = "slotLabel";
            this.slotLabel.Size = new System.Drawing.Size(59, 13);
            this.slotLabel.TabIndex = 9;
            this.slotLabel.Text = "Texture slot";
            //
            // slotCombo
            //
            this.slotCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.slotCombo.FormattingEnabled = true;
            this.slotCombo.Location = new System.Drawing.Point(12, 336);
            this.slotCombo.Name = "slotCombo";
            this.slotCombo.Size = new System.Drawing.Size(396, 21);
            this.slotCombo.TabIndex = 10;
            //
            // cloneLabel
            //
            this.cloneLabel.AutoSize = true;
            this.cloneLabel.ForeColor = System.Drawing.SystemColors.GrayText;
            this.cloneLabel.Location = new System.Drawing.Point(12, 361);
            this.cloneLabel.Name = "cloneLabel";
            this.cloneLabel.Size = new System.Drawing.Size(163, 13);
            this.cloneLabel.TabIndex = 11;
            this.cloneLabel.Text = "Clone render/animation profile from";
            //
            // cloneCombo
            //
            this.cloneCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cloneCombo.FormattingEnabled = true;
            this.cloneCombo.Location = new System.Drawing.Point(12, 377);
            this.cloneCombo.Name = "cloneCombo";
            this.cloneCombo.Size = new System.Drawing.Size(396, 21);
            this.cloneCombo.TabIndex = 12;
            //
            // statusLabel
            //
            this.statusLabel.ForeColor = System.Drawing.Color.Firebrick;
            this.statusLabel.Location = new System.Drawing.Point(12, 404);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(396, 40);
            this.statusLabel.TabIndex = 13;
            //
            // addButton
            //
            this.addButton.Location = new System.Drawing.Point(226, 450);
            this.addButton.Name = "addButton";
            this.addButton.Size = new System.Drawing.Size(90, 23);
            this.addButton.TabIndex = 14;
            this.addButton.Text = "Add";
            this.addButton.UseVisualStyleBackColor = true;
            this.addButton.Click += new System.EventHandler(this.AddButton_Click);
            //
            // cancelButton
            //
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(322, 450);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(90, 23);
            this.cancelButton.TabIndex = 15;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            //
            // AddOverworldEntryForm
            //
            this.AcceptButton = this.addButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(420, 490);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.addButton);
            this.Controls.Add(this.statusLabel);
            this.Controls.Add(this.cloneCombo);
            this.Controls.Add(this.cloneLabel);
            this.Controls.Add(this.slotCombo);
            this.Controls.Add(this.slotLabel);
            this.Controls.Add(this.imageInfoLabel);
            this.Controls.Add(this.imagePreview);
            this.Controls.Add(this.clearImageButton);
            this.Controls.Add(this.chooseRawBtxButton);
            this.Controls.Add(this.choosePngButton);
            this.Controls.Add(this.imageLabel);
            this.Controls.Add(this.appearanceIdTextBox);
            this.Controls.Add(this.appearanceIdLabel);
            this.Controls.Add(this.hintLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AddOverworldEntryForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Add Custom Overworld Entry";
            ((System.ComponentModel.ISupportInitialize)(this.imagePreview)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label hintLabel;
        private System.Windows.Forms.Label appearanceIdLabel;
        private System.Windows.Forms.TextBox appearanceIdTextBox;
        private System.Windows.Forms.Label imageLabel;
        private System.Windows.Forms.Button choosePngButton;
        private System.Windows.Forms.Button chooseRawBtxButton;
        private System.Windows.Forms.Button clearImageButton;
        private System.Windows.Forms.PictureBox imagePreview;
        private System.Windows.Forms.Label imageInfoLabel;
        private System.Windows.Forms.Label slotLabel;
        private System.Windows.Forms.ComboBox slotCombo;
        private System.Windows.Forms.Label cloneLabel;
        private System.Windows.Forms.ComboBox cloneCombo;
        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.Button addButton;
        private System.Windows.Forms.Button cancelButton;
    }
}
