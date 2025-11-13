namespace DSPRE
{
    partial class TMEditor
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
            this.components = new System.ComponentModel.Container();
            this.machineListBox = new System.Windows.Forms.ListBox();
            this.TMListLabel = new System.Windows.Forms.Label();
            this.moveComboBox = new System.Windows.Forms.ComboBox();
            this.saveButton = new System.Windows.Forms.Button();
            this.moveLabel = new System.Windows.Forms.Label();
            this.paletteLabel = new System.Windows.Forms.Label();
            this.paletteComboBox = new System.Windows.Forms.ComboBox();
            this.autoPaletteButton = new System.Windows.Forms.Button();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.exportButton = new System.Windows.Forms.Button();
            this.importButton = new System.Windows.Forms.Button();
            this.autoPaletteAllButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // machineListBox
            // 
            this.machineListBox.FormattingEnabled = true;
            this.machineListBox.Location = new System.Drawing.Point(13, 34);
            this.machineListBox.Name = "machineListBox";
            this.machineListBox.Size = new System.Drawing.Size(225, 394);
            this.machineListBox.TabIndex = 0;
            this.machineListBox.SelectedIndexChanged += new System.EventHandler(this.machineListBox_SelectedIndexChanged);
            // 
            // TMListLabel
            // 
            this.TMListLabel.AutoSize = true;
            this.TMListLabel.Location = new System.Drawing.Point(10, 18);
            this.TMListLabel.Name = "TMListLabel";
            this.TMListLabel.Size = new System.Drawing.Size(87, 13);
            this.TMListLabel.TabIndex = 1;
            this.TMListLabel.Text = "Choose Machine";
            // 
            // moveComboBox
            // 
            this.moveComboBox.FormattingEnabled = true;
            this.moveComboBox.Location = new System.Drawing.Point(244, 50);
            this.moveComboBox.Name = "moveComboBox";
            this.moveComboBox.Size = new System.Drawing.Size(121, 21);
            this.moveComboBox.TabIndex = 2;
            this.moveComboBox.SelectedIndexChanged += new System.EventHandler(this.moveComboBox_SelectedIndexChanged);
            // 
            // saveButton
            // 
            this.saveButton.Image = global::DSPRE.Properties.Resources.saveButton;
            this.saveButton.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.saveButton.Location = new System.Drawing.Point(244, 398);
            this.saveButton.Name = "saveButton";
            this.saveButton.Size = new System.Drawing.Size(121, 30);
            this.saveButton.TabIndex = 3;
            this.saveButton.Text = "Save";
            this.saveButton.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.saveButton.UseVisualStyleBackColor = true;
            this.saveButton.Click += new System.EventHandler(this.saveButton_Click);
            // 
            // moveLabel
            // 
            this.moveLabel.AutoSize = true;
            this.moveLabel.Location = new System.Drawing.Point(244, 34);
            this.moveLabel.Name = "moveLabel";
            this.moveLabel.Size = new System.Drawing.Size(34, 13);
            this.moveLabel.TabIndex = 4;
            this.moveLabel.Text = "Move";
            // 
            // paletteLabel
            // 
            this.paletteLabel.AutoSize = true;
            this.paletteLabel.Location = new System.Drawing.Point(244, 79);
            this.paletteLabel.Name = "paletteLabel";
            this.paletteLabel.Size = new System.Drawing.Size(40, 13);
            this.paletteLabel.TabIndex = 5;
            this.paletteLabel.Text = "Palette";
            // 
            // paletteComboBox
            // 
            this.paletteComboBox.FormattingEnabled = true;
            this.paletteComboBox.Location = new System.Drawing.Point(244, 95);
            this.paletteComboBox.Name = "paletteComboBox";
            this.paletteComboBox.Size = new System.Drawing.Size(121, 21);
            this.paletteComboBox.TabIndex = 6;
            this.toolTip.SetToolTip(this.paletteComboBox, "Select the type that will be used for the palette.\r\n??? type does not have a pale" +
        "tte and will use Normal instead.");
            this.paletteComboBox.SelectedIndexChanged += new System.EventHandler(this.paletteComboBox_SelectedIndexChanged);
            // 
            // autoPaletteButton
            // 
            this.autoPaletteButton.Image = global::DSPRE.Properties.Resources.resetColorTable;
            this.autoPaletteButton.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.autoPaletteButton.Location = new System.Drawing.Point(244, 122);
            this.autoPaletteButton.Name = "autoPaletteButton";
            this.autoPaletteButton.Size = new System.Drawing.Size(121, 30);
            this.autoPaletteButton.TabIndex = 7;
            this.autoPaletteButton.Text = "Auto Palette";
            this.autoPaletteButton.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.toolTip.SetToolTip(this.autoPaletteButton, "Determine palette from move type");
            this.autoPaletteButton.UseVisualStyleBackColor = true;
            this.autoPaletteButton.Click += new System.EventHandler(this.autoPaletteButton_Click);
            // 
            // exportButton
            // 
            this.exportButton.Image = global::DSPRE.Properties.Resources.exportArrow;
            this.exportButton.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.exportButton.Location = new System.Drawing.Point(244, 362);
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(121, 30);
            this.exportButton.TabIndex = 8;
            this.exportButton.Text = "Export";
            this.exportButton.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.exportButton.UseVisualStyleBackColor = true;
            this.exportButton.Click += new System.EventHandler(this.exportButton_Click);
            // 
            // importButton
            // 
            this.importButton.Image = global::DSPRE.Properties.Resources.importArrow;
            this.importButton.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.importButton.Location = new System.Drawing.Point(244, 326);
            this.importButton.Name = "importButton";
            this.importButton.Size = new System.Drawing.Size(121, 30);
            this.importButton.TabIndex = 9;
            this.importButton.Text = "Import";
            this.importButton.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.importButton.UseVisualStyleBackColor = true;
            this.importButton.Click += new System.EventHandler(this.importButton_Click);
            // 
            // autoPaletteAllButton
            // 
            this.autoPaletteAllButton.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.autoPaletteAllButton.Location = new System.Drawing.Point(244, 158);
            this.autoPaletteAllButton.Name = "autoPaletteAllButton";
            this.autoPaletteAllButton.Size = new System.Drawing.Size(121, 30);
            this.autoPaletteAllButton.TabIndex = 7;
            this.autoPaletteAllButton.Text = "Auto Palette All";
            this.autoPaletteAllButton.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.toolTip.SetToolTip(this.autoPaletteAllButton, "Apply automatic palette based on move type to all moves.\r\nCaution: TMs/HMs of mov" +
        "es with unknown types (e.g. Fairy) will be assigned the Normal type palette.\r\n");
            this.autoPaletteAllButton.UseVisualStyleBackColor = true;
            this.autoPaletteAllButton.Click += new System.EventHandler(this.autoPaletteAllButton_Click);
            // 
            // TMEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(379, 458);
            this.Controls.Add(this.importButton);
            this.Controls.Add(this.exportButton);
            this.Controls.Add(this.autoPaletteAllButton);
            this.Controls.Add(this.autoPaletteButton);
            this.Controls.Add(this.paletteComboBox);
            this.Controls.Add(this.paletteLabel);
            this.Controls.Add(this.moveLabel);
            this.Controls.Add(this.saveButton);
            this.Controls.Add(this.moveComboBox);
            this.Controls.Add(this.TMListLabel);
            this.Controls.Add(this.machineListBox);
            this.Name = "TMEditor";
            this.Text = "TM/HM Editor";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.TMEditor_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListBox machineListBox;
        private System.Windows.Forms.Label TMListLabel;
        private System.Windows.Forms.ComboBox moveComboBox;
        private System.Windows.Forms.Button saveButton;
        private System.Windows.Forms.Label moveLabel;
        private System.Windows.Forms.Label paletteLabel;
        private System.Windows.Forms.ComboBox paletteComboBox;
        private System.Windows.Forms.Button autoPaletteButton;
        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.Button exportButton;
        private System.Windows.Forms.Button importButton;
        private System.Windows.Forms.Button autoPaletteAllButton;
    }
}