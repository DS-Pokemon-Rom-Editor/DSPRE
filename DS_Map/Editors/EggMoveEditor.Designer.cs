namespace DSPRE
{
    partial class EggMoveEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EggMoveEditor));
            this.mainTableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.topTableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.pokemonPictureBox = new System.Windows.Forms.PictureBox();
            this.listGroupBox = new System.Windows.Forms.GroupBox();
            this.editGroupBox = new System.Windows.Forms.GroupBox();
            this.eggMoveListBox = new System.Windows.Forms.ListBox();
            this.editTableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.saveDataButton = new System.Windows.Forms.Button();
            this.monComboBox = new System.Windows.Forms.ComboBox();
            this.monNumericUpDown = new System.Windows.Forms.NumericUpDown();
            this.moveLabel = new System.Windows.Forms.Label();
            this.moveComboBox = new System.Windows.Forms.ComboBox();
            this.addMoveButton = new System.Windows.Forms.Button();
            this.replaceMoveButton = new System.Windows.Forms.Button();
            this.deleteMoveButton = new System.Windows.Forms.Button();
            this.listSizeLabel = new System.Windows.Forms.Label();
            this.moveIDLabel = new System.Windows.Forms.Label();
            this.statusLabel = new System.Windows.Forms.Label();
            this.moveCountLabel = new System.Windows.Forms.Label();
            this.mainTableLayoutPanel.SuspendLayout();
            this.topTableLayoutPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pokemonPictureBox)).BeginInit();
            this.listGroupBox.SuspendLayout();
            this.editGroupBox.SuspendLayout();
            this.editTableLayoutPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.monNumericUpDown)).BeginInit();
            this.SuspendLayout();
            // 
            // mainTableLayoutPanel
            // 
            this.mainTableLayoutPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.mainTableLayoutPanel.ColumnCount = 2;
            this.mainTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.mainTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.mainTableLayoutPanel.Controls.Add(this.listGroupBox, 0, 0);
            this.mainTableLayoutPanel.Controls.Add(this.editGroupBox, 1, 0);
            this.mainTableLayoutPanel.Location = new System.Drawing.Point(12, 65);
            this.mainTableLayoutPanel.Name = "mainTableLayoutPanel";
            this.mainTableLayoutPanel.RowCount = 1;
            this.mainTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.mainTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 444F));
            this.mainTableLayoutPanel.Size = new System.Drawing.Size(480, 384);
            this.mainTableLayoutPanel.TabIndex = 0;
            // 
            // topTableLayoutPanel
            // 
            this.topTableLayoutPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.topTableLayoutPanel.ColumnCount = 4;
            this.topTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            this.topTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 70F));
            this.topTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
            this.topTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            this.topTableLayoutPanel.Controls.Add(this.saveDataButton, 31, 0);
            this.topTableLayoutPanel.Controls.Add(this.pokemonPictureBox, 0, 0);
            this.topTableLayoutPanel.Controls.Add(this.monComboBox, 1, 0);
            this.topTableLayoutPanel.Controls.Add(this.monNumericUpDown, 2, 0);
            this.topTableLayoutPanel.Location = new System.Drawing.Point(12, 12);
            this.topTableLayoutPanel.Name = "topTableLayoutPanel";
            this.topTableLayoutPanel.RowCount = 1;
            this.topTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.topTableLayoutPanel.Size = new System.Drawing.Size(480, 50);
            this.topTableLayoutPanel.TabIndex = 1;
            // 
            // pokemonPictureBox
            // 
            this.pokemonPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pokemonPictureBox.Location = new System.Drawing.Point(3, 3);
            this.pokemonPictureBox.Name = "pokemonPictureBox";
            this.pokemonPictureBox.Size = new System.Drawing.Size(44, 44);
            this.pokemonPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.pokemonPictureBox.TabIndex = 13;
            this.pokemonPictureBox.TabStop = false;
            // 
            // listGroupBox
            // 
            this.listGroupBox.Controls.Add(this.eggMoveListBox);
            this.listGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listGroupBox.Location = new System.Drawing.Point(3, 3);
            this.listGroupBox.Name = "listGroupBox";
            this.listGroupBox.Size = new System.Drawing.Size(234, 378);
            this.listGroupBox.TabIndex = 0;
            this.listGroupBox.TabStop = false;
            this.listGroupBox.Text = "Egg Move List";
            // 
            // editGroupBox
            // 
            this.editGroupBox.Controls.Add(this.editTableLayoutPanel);
            this.editGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.editGroupBox.Location = new System.Drawing.Point(243, 3);
            this.editGroupBox.Name = "editGroupBox";
            this.editGroupBox.Size = new System.Drawing.Size(234, 378);
            this.editGroupBox.TabIndex = 1;
            this.editGroupBox.TabStop = false;
            this.editGroupBox.Text = "Edit";
            // 
            // eggMoveListBox
            // 
            this.eggMoveListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.eggMoveListBox.FormattingEnabled = true;
            this.eggMoveListBox.Location = new System.Drawing.Point(3, 16);
            this.eggMoveListBox.Name = "eggMoveListBox";
            this.eggMoveListBox.Size = new System.Drawing.Size(228, 359);
            this.eggMoveListBox.TabIndex = 0;
            // 
            // editTableLayoutPanel
            // 
            this.editTableLayoutPanel.ColumnCount = 3;
            this.editTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.editTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.editTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.editTableLayoutPanel.Controls.Add(this.moveCountLabel, 0, 3);
            this.editTableLayoutPanel.Controls.Add(this.statusLabel, 1, 1);
            this.editTableLayoutPanel.Controls.Add(this.moveIDLabel, 0, 1);
            this.editTableLayoutPanel.Controls.Add(this.listSizeLabel, 0, 4);
            this.editTableLayoutPanel.Controls.Add(this.deleteMoveButton, 2, 2);
            this.editTableLayoutPanel.Controls.Add(this.replaceMoveButton, 1, 2);
            this.editTableLayoutPanel.Controls.Add(this.addMoveButton, 0, 2);
            this.editTableLayoutPanel.Controls.Add(this.moveLabel, 0, 0);
            this.editTableLayoutPanel.Controls.Add(this.moveComboBox, 1, 0);
            this.editTableLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.editTableLayoutPanel.Location = new System.Drawing.Point(3, 16);
            this.editTableLayoutPanel.Name = "editTableLayoutPanel";
            this.editTableLayoutPanel.RowCount = 5;
            this.editTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.editTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.editTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            this.editTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.editTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.editTableLayoutPanel.Size = new System.Drawing.Size(228, 359);
            this.editTableLayoutPanel.TabIndex = 0;
            // 
            // saveDataButton
            // 
            this.saveDataButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.saveDataButton.Image = ((System.Drawing.Image)(resources.GetObject("saveDataButton.Image")));
            this.saveDataButton.Location = new System.Drawing.Point(433, 3);
            this.saveDataButton.Name = "saveDataButton";
            this.saveDataButton.Size = new System.Drawing.Size(44, 44);
            this.saveDataButton.TabIndex = 31;
            this.saveDataButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.saveDataButton.UseVisualStyleBackColor = true;
            this.saveDataButton.Click += new System.EventHandler(this.saveDataButton_Click);
            // 
            // monComboBox
            // 
            this.monComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.monComboBox.FormattingEnabled = true;
            this.monComboBox.Location = new System.Drawing.Point(53, 14);
            this.monComboBox.Name = "monComboBox";
            this.monComboBox.Size = new System.Drawing.Size(260, 21);
            this.monComboBox.TabIndex = 32;
            // 
            // monNumericUpDown
            // 
            this.monNumericUpDown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.monNumericUpDown.Location = new System.Drawing.Point(319, 15);
            this.monNumericUpDown.Name = "monNumericUpDown";
            this.monNumericUpDown.Size = new System.Drawing.Size(108, 20);
            this.monNumericUpDown.TabIndex = 33;
            // 
            // moveLabel
            // 
            this.moveLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.moveLabel.AutoSize = true;
            this.moveLabel.Location = new System.Drawing.Point(3, 13);
            this.moveLabel.Name = "moveLabel";
            this.moveLabel.Size = new System.Drawing.Size(69, 13);
            this.moveLabel.TabIndex = 0;
            this.moveLabel.Text = "Move";
            // 
            // moveComboBox
            // 
            this.moveComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.editTableLayoutPanel.SetColumnSpan(this.moveComboBox, 2);
            this.moveComboBox.FormattingEnabled = true;
            this.moveComboBox.Location = new System.Drawing.Point(78, 9);
            this.moveComboBox.Name = "moveComboBox";
            this.moveComboBox.Size = new System.Drawing.Size(147, 21);
            this.moveComboBox.TabIndex = 2;
            // 
            // addMoveButton
            // 
            this.addMoveButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.addMoveButton.Enabled = false;
            this.addMoveButton.Image = global::DSPRE.Properties.Resources.addIcon;
            this.addMoveButton.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.addMoveButton.Location = new System.Drawing.Point(2, 79);
            this.addMoveButton.Margin = new System.Windows.Forms.Padding(2);
            this.addMoveButton.Name = "addMoveButton";
            this.addMoveButton.Size = new System.Drawing.Size(71, 32);
            this.addMoveButton.TabIndex = 35;
            this.addMoveButton.Text = "Add";
            this.addMoveButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.addMoveButton.UseVisualStyleBackColor = true;
            // 
            // replaceMoveButton
            // 
            this.replaceMoveButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.replaceMoveButton.Enabled = false;
            this.replaceMoveButton.Image = global::DSPRE.Properties.Resources.RenameIcon;
            this.replaceMoveButton.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.replaceMoveButton.Location = new System.Drawing.Point(77, 79);
            this.replaceMoveButton.Margin = new System.Windows.Forms.Padding(2);
            this.replaceMoveButton.Name = "replaceMoveButton";
            this.replaceMoveButton.Size = new System.Drawing.Size(71, 32);
            this.replaceMoveButton.TabIndex = 36;
            this.replaceMoveButton.Text = "Replace";
            this.replaceMoveButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.replaceMoveButton.UseVisualStyleBackColor = true;
            // 
            // deleteMoveButton
            // 
            this.deleteMoveButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.deleteMoveButton.Enabled = false;
            this.deleteMoveButton.Image = global::DSPRE.Properties.Resources.deleteIcon;
            this.deleteMoveButton.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.deleteMoveButton.Location = new System.Drawing.Point(152, 79);
            this.deleteMoveButton.Margin = new System.Windows.Forms.Padding(2);
            this.deleteMoveButton.Name = "deleteMoveButton";
            this.deleteMoveButton.Size = new System.Drawing.Size(74, 32);
            this.deleteMoveButton.TabIndex = 37;
            this.deleteMoveButton.Text = "Delete";
            this.deleteMoveButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.deleteMoveButton.UseVisualStyleBackColor = true;
            // 
            // listSizeLabel
            // 
            this.listSizeLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listSizeLabel.AutoSize = true;
            this.editTableLayoutPanel.SetColumnSpan(this.listSizeLabel, 2);
            this.listSizeLabel.Location = new System.Drawing.Point(3, 346);
            this.listSizeLabel.Name = "listSizeLabel";
            this.listSizeLabel.Size = new System.Drawing.Size(144, 13);
            this.listSizeLabel.TabIndex = 38;
            this.listSizeLabel.Text = "Total List Size:";
            // 
            // moveIDLabel
            // 
            this.moveIDLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.moveIDLabel.AutoSize = true;
            this.moveIDLabel.Location = new System.Drawing.Point(3, 48);
            this.moveIDLabel.Name = "moveIDLabel";
            this.moveIDLabel.Size = new System.Drawing.Size(69, 13);
            this.moveIDLabel.TabIndex = 39;
            this.moveIDLabel.Text = "Move ID:";
            // 
            // statusLabel
            // 
            this.statusLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.statusLabel.AutoSize = true;
            this.editTableLayoutPanel.SetColumnSpan(this.statusLabel, 2);
            this.statusLabel.Location = new System.Drawing.Point(78, 48);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(147, 13);
            this.statusLabel.TabIndex = 40;
            this.statusLabel.Text = "Current Status";
            // 
            // moveCountLabel
            // 
            this.moveCountLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.moveCountLabel.AutoSize = true;
            this.editTableLayoutPanel.SetColumnSpan(this.moveCountLabel, 2);
            this.moveCountLabel.Location = new System.Drawing.Point(3, 128);
            this.moveCountLabel.Name = "moveCountLabel";
            this.moveCountLabel.Size = new System.Drawing.Size(144, 13);
            this.moveCountLabel.TabIndex = 41;
            this.moveCountLabel.Text = "Move Count:";
            // 
            // EggMoveEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(504, 461);
            this.Controls.Add(this.topTableLayoutPanel);
            this.Controls.Add(this.mainTableLayoutPanel);
            this.Name = "EggMoveEditor";
            this.Text = "EggMoveEditor";
            this.mainTableLayoutPanel.ResumeLayout(false);
            this.topTableLayoutPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pokemonPictureBox)).EndInit();
            this.listGroupBox.ResumeLayout(false);
            this.editGroupBox.ResumeLayout(false);
            this.editTableLayoutPanel.ResumeLayout(false);
            this.editTableLayoutPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.monNumericUpDown)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel mainTableLayoutPanel;
        private System.Windows.Forms.TableLayoutPanel topTableLayoutPanel;
        private System.Windows.Forms.PictureBox pokemonPictureBox;
        private System.Windows.Forms.GroupBox listGroupBox;
        private System.Windows.Forms.GroupBox editGroupBox;
        private System.Windows.Forms.ListBox eggMoveListBox;
        private System.Windows.Forms.TableLayoutPanel editTableLayoutPanel;
        private System.Windows.Forms.Button saveDataButton;
        private System.Windows.Forms.ComboBox monComboBox;
        private System.Windows.Forms.NumericUpDown monNumericUpDown;
        private System.Windows.Forms.Label moveLabel;
        private System.Windows.Forms.ComboBox moveComboBox;
        private System.Windows.Forms.Button addMoveButton;
        private System.Windows.Forms.Button replaceMoveButton;
        private System.Windows.Forms.Button deleteMoveButton;
        private System.Windows.Forms.Label listSizeLabel;
        private System.Windows.Forms.Label moveIDLabel;
        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.Label moveCountLabel;
    }
}