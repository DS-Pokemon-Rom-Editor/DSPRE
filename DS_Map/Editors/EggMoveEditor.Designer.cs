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
            this.components = new System.ComponentModel.Container();
            this.mainTableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.editMonsGroupBox = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.listSizeLabel = new System.Windows.Forms.Label();
            this.monCountLabel = new System.Windows.Forms.Label();
            this.monStatusLabel = new System.Windows.Forms.Label();
            this.entryIDLabel = new System.Windows.Forms.Label();
            this.deleteMonButton = new System.Windows.Forms.Button();
            this.replaceMonButton = new System.Windows.Forms.Button();
            this.addMonButton = new System.Windows.Forms.Button();
            this.monLabel = new System.Windows.Forms.Label();
            this.monComboBox = new System.Windows.Forms.ComboBox();
            this.moveListGroupBox = new System.Windows.Forms.GroupBox();
            this.eggMoveListBox = new System.Windows.Forms.ListBox();
            this.editMovesGroupBox = new System.Windows.Forms.GroupBox();
            this.editTableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.moveCountLabel = new System.Windows.Forms.Label();
            this.moveStatusLabel = new System.Windows.Forms.Label();
            this.moveIDLabel = new System.Windows.Forms.Label();
            this.deleteMoveButton = new System.Windows.Forms.Button();
            this.replaceMoveButton = new System.Windows.Forms.Button();
            this.addMoveButton = new System.Windows.Forms.Button();
            this.moveLabel = new System.Windows.Forms.Label();
            this.moveComboBox = new System.Windows.Forms.ComboBox();
            this.monListGroupBox = new System.Windows.Forms.GroupBox();
            this.monListBox = new System.Windows.Forms.ListBox();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.mainTableLayoutPanel.SuspendLayout();
            this.editMonsGroupBox.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.moveListGroupBox.SuspendLayout();
            this.editMovesGroupBox.SuspendLayout();
            this.editTableLayoutPanel.SuspendLayout();
            this.monListGroupBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // mainTableLayoutPanel
            // 
            this.mainTableLayoutPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.mainTableLayoutPanel.ColumnCount = 4;
            this.mainTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.mainTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
            this.mainTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.mainTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
            this.mainTableLayoutPanel.Controls.Add(this.editMonsGroupBox, 1, 0);
            this.mainTableLayoutPanel.Controls.Add(this.moveListGroupBox, 2, 0);
            this.mainTableLayoutPanel.Controls.Add(this.editMovesGroupBox, 3, 0);
            this.mainTableLayoutPanel.Controls.Add(this.monListGroupBox, 0, 0);
            this.mainTableLayoutPanel.Location = new System.Drawing.Point(12, 12);
            this.mainTableLayoutPanel.Name = "mainTableLayoutPanel";
            this.mainTableLayoutPanel.RowCount = 1;
            this.mainTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.mainTableLayoutPanel.Size = new System.Drawing.Size(885, 420);
            this.mainTableLayoutPanel.TabIndex = 0;
            // 
            // editMonsGroupBox
            // 
            this.editMonsGroupBox.Controls.Add(this.tableLayoutPanel1);
            this.editMonsGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.editMonsGroupBox.Location = new System.Drawing.Point(180, 3);
            this.editMonsGroupBox.Name = "editMonsGroupBox";
            this.editMonsGroupBox.Size = new System.Drawing.Size(259, 414);
            this.editMonsGroupBox.TabIndex = 3;
            this.editMonsGroupBox.TabStop = false;
            this.editMonsGroupBox.Text = "Edit Pokémon";
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 3;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel1.Controls.Add(this.monCountLabel, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.monStatusLabel, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.entryIDLabel, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.listSizeLabel, 0, 4);
            this.tableLayoutPanel1.Controls.Add(this.deleteMonButton, 2, 2);
            this.tableLayoutPanel1.Controls.Add(this.replaceMonButton, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.addMonButton, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.monLabel, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.monComboBox, 1, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(3, 16);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 5;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(253, 395);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // listSizeLabel
            // 
            this.listSizeLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listSizeLabel.AutoSize = true;
            this.tableLayoutPanel1.SetColumnSpan(this.listSizeLabel, 2);
            this.listSizeLabel.Location = new System.Drawing.Point(3, 382);
            this.listSizeLabel.Name = "listSizeLabel";
            this.listSizeLabel.Size = new System.Drawing.Size(162, 13);
            this.listSizeLabel.TabIndex = 38;
            this.listSizeLabel.Text = "Total List Size:";
            this.toolTip.SetToolTip(this.listSizeLabel, "DPPt have a strictly limited list size. \r\nIn order to add new entries others have" +
        " to be replaced or removed.");
            // 
            // monCountLabel
            // 
            this.monCountLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.monCountLabel.AutoSize = true;
            this.tableLayoutPanel1.SetColumnSpan(this.monCountLabel, 2);
            this.monCountLabel.Location = new System.Drawing.Point(3, 128);
            this.monCountLabel.Name = "monCountLabel";
            this.monCountLabel.Size = new System.Drawing.Size(162, 13);
            this.monCountLabel.TabIndex = 41;
            this.monCountLabel.Text = "Pokémon Count:";
            this.toolTip.SetToolTip(this.monCountLabel, "Amount of Pokémon that have Egg Moves.\r\nEach Pokémon needs at least 2 Bytes of sp" +
        "ace in the list.");
            // 
            // monStatusLabel
            // 
            this.monStatusLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.monStatusLabel.AutoSize = true;
            this.tableLayoutPanel1.SetColumnSpan(this.monStatusLabel, 2);
            this.monStatusLabel.Location = new System.Drawing.Point(87, 48);
            this.monStatusLabel.Name = "monStatusLabel";
            this.monStatusLabel.Size = new System.Drawing.Size(163, 13);
            this.monStatusLabel.TabIndex = 40;
            this.monStatusLabel.Text = "Current Status Mon";
            // 
            // entryIDLabel
            // 
            this.entryIDLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.entryIDLabel.AutoSize = true;
            this.entryIDLabel.Location = new System.Drawing.Point(3, 48);
            this.entryIDLabel.Name = "entryIDLabel";
            this.entryIDLabel.Size = new System.Drawing.Size(78, 13);
            this.entryIDLabel.TabIndex = 39;
            this.entryIDLabel.Text = "Entry ID:";
            // 
            // deleteMonButton
            // 
            this.deleteMonButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.deleteMonButton.Enabled = false;
            this.deleteMonButton.Image = global::DSPRE.Properties.Resources.deleteIcon;
            this.deleteMonButton.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.deleteMonButton.Location = new System.Drawing.Point(170, 79);
            this.deleteMonButton.Margin = new System.Windows.Forms.Padding(2);
            this.deleteMonButton.Name = "deleteMonButton";
            this.deleteMonButton.Size = new System.Drawing.Size(81, 32);
            this.deleteMonButton.TabIndex = 37;
            this.deleteMonButton.Text = "Delete";
            this.deleteMonButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.deleteMonButton.UseVisualStyleBackColor = true;
            this.deleteMonButton.Click += new System.EventHandler(this.deleteMonButton_Click);
            // 
            // replaceMonButton
            // 
            this.replaceMonButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.replaceMonButton.Enabled = false;
            this.replaceMonButton.Image = global::DSPRE.Properties.Resources.RenameIcon;
            this.replaceMonButton.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.replaceMonButton.Location = new System.Drawing.Point(86, 79);
            this.replaceMonButton.Margin = new System.Windows.Forms.Padding(2);
            this.replaceMonButton.Name = "replaceMonButton";
            this.replaceMonButton.Size = new System.Drawing.Size(80, 32);
            this.replaceMonButton.TabIndex = 36;
            this.replaceMonButton.Text = "Replace";
            this.replaceMonButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.replaceMonButton.UseVisualStyleBackColor = true;
            this.replaceMonButton.Click += new System.EventHandler(this.replaceMonButton_Click);
            // 
            // addMonButton
            // 
            this.addMonButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.addMonButton.Enabled = false;
            this.addMonButton.Image = global::DSPRE.Properties.Resources.addIcon;
            this.addMonButton.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.addMonButton.Location = new System.Drawing.Point(2, 79);
            this.addMonButton.Margin = new System.Windows.Forms.Padding(2);
            this.addMonButton.Name = "addMonButton";
            this.addMonButton.Size = new System.Drawing.Size(80, 32);
            this.addMonButton.TabIndex = 35;
            this.addMonButton.Text = "Add";
            this.addMonButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.addMonButton.UseVisualStyleBackColor = true;
            this.addMonButton.Click += new System.EventHandler(this.addMonButton_Click);
            // 
            // monLabel
            // 
            this.monLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.monLabel.AutoSize = true;
            this.monLabel.Location = new System.Drawing.Point(3, 13);
            this.monLabel.Name = "monLabel";
            this.monLabel.Size = new System.Drawing.Size(78, 13);
            this.monLabel.TabIndex = 0;
            this.monLabel.Text = "Pokémon";
            // 
            // monComboBox
            // 
            this.monComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel1.SetColumnSpan(this.monComboBox, 2);
            this.monComboBox.FormattingEnabled = true;
            this.monComboBox.Location = new System.Drawing.Point(87, 9);
            this.monComboBox.Name = "monComboBox";
            this.monComboBox.Size = new System.Drawing.Size(163, 21);
            this.monComboBox.TabIndex = 2;
            this.monComboBox.SelectedIndexChanged += new System.EventHandler(this.monComboBox_SelectedIndexChanged);
            // 
            // moveListGroupBox
            // 
            this.moveListGroupBox.Controls.Add(this.eggMoveListBox);
            this.moveListGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.moveListGroupBox.Location = new System.Drawing.Point(445, 3);
            this.moveListGroupBox.Name = "moveListGroupBox";
            this.moveListGroupBox.Size = new System.Drawing.Size(171, 414);
            this.moveListGroupBox.TabIndex = 0;
            this.moveListGroupBox.TabStop = false;
            this.moveListGroupBox.Text = "Egg Move List";
            // 
            // eggMoveListBox
            // 
            this.eggMoveListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.eggMoveListBox.FormattingEnabled = true;
            this.eggMoveListBox.Location = new System.Drawing.Point(3, 16);
            this.eggMoveListBox.Name = "eggMoveListBox";
            this.eggMoveListBox.Size = new System.Drawing.Size(165, 395);
            this.eggMoveListBox.TabIndex = 0;
            this.eggMoveListBox.SelectedIndexChanged += new System.EventHandler(this.eggMoveListBox_SelectedIndexChanged);
            // 
            // editMovesGroupBox
            // 
            this.editMovesGroupBox.Controls.Add(this.editTableLayoutPanel);
            this.editMovesGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.editMovesGroupBox.Location = new System.Drawing.Point(622, 3);
            this.editMovesGroupBox.Name = "editMovesGroupBox";
            this.editMovesGroupBox.Size = new System.Drawing.Size(260, 414);
            this.editMovesGroupBox.TabIndex = 1;
            this.editMovesGroupBox.TabStop = false;
            this.editMovesGroupBox.Text = "Edit Moves";
            // 
            // editTableLayoutPanel
            // 
            this.editTableLayoutPanel.ColumnCount = 3;
            this.editTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.editTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.editTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.editTableLayoutPanel.Controls.Add(this.moveCountLabel, 0, 3);
            this.editTableLayoutPanel.Controls.Add(this.moveStatusLabel, 1, 1);
            this.editTableLayoutPanel.Controls.Add(this.moveIDLabel, 0, 1);
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
            this.editTableLayoutPanel.Size = new System.Drawing.Size(254, 395);
            this.editTableLayoutPanel.TabIndex = 0;
            // 
            // moveCountLabel
            // 
            this.moveCountLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.moveCountLabel.AutoSize = true;
            this.editTableLayoutPanel.SetColumnSpan(this.moveCountLabel, 2);
            this.moveCountLabel.Location = new System.Drawing.Point(3, 128);
            this.moveCountLabel.Name = "moveCountLabel";
            this.moveCountLabel.Size = new System.Drawing.Size(162, 13);
            this.moveCountLabel.TabIndex = 41;
            this.moveCountLabel.Text = "Move Count:";
            this.toolTip.SetToolTip(this.moveCountLabel, "Amount of Egg Moves the currently selected Pokémon has.\r\nEach Egg Move needs at l" +
        "east 2 Bytes of space in the list.\r\n");
            // 
            // moveStatusLabel
            // 
            this.moveStatusLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.moveStatusLabel.AutoSize = true;
            this.editTableLayoutPanel.SetColumnSpan(this.moveStatusLabel, 2);
            this.moveStatusLabel.Location = new System.Drawing.Point(87, 48);
            this.moveStatusLabel.Name = "moveStatusLabel";
            this.moveStatusLabel.Size = new System.Drawing.Size(164, 13);
            this.moveStatusLabel.TabIndex = 40;
            this.moveStatusLabel.Text = "Current Status";
            // 
            // moveIDLabel
            // 
            this.moveIDLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.moveIDLabel.AutoSize = true;
            this.moveIDLabel.Location = new System.Drawing.Point(3, 48);
            this.moveIDLabel.Name = "moveIDLabel";
            this.moveIDLabel.Size = new System.Drawing.Size(78, 13);
            this.moveIDLabel.TabIndex = 39;
            this.moveIDLabel.Text = "Move ID:";
            // 
            // deleteMoveButton
            // 
            this.deleteMoveButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.deleteMoveButton.Enabled = false;
            this.deleteMoveButton.Image = global::DSPRE.Properties.Resources.deleteIcon;
            this.deleteMoveButton.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.deleteMoveButton.Location = new System.Drawing.Point(170, 79);
            this.deleteMoveButton.Margin = new System.Windows.Forms.Padding(2);
            this.deleteMoveButton.Name = "deleteMoveButton";
            this.deleteMoveButton.Size = new System.Drawing.Size(82, 32);
            this.deleteMoveButton.TabIndex = 37;
            this.deleteMoveButton.Text = "Delete";
            this.deleteMoveButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.deleteMoveButton.UseVisualStyleBackColor = true;
            this.deleteMoveButton.Click += new System.EventHandler(this.deleteMoveButton_Click);
            // 
            // replaceMoveButton
            // 
            this.replaceMoveButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.replaceMoveButton.Enabled = false;
            this.replaceMoveButton.Image = global::DSPRE.Properties.Resources.RenameIcon;
            this.replaceMoveButton.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.replaceMoveButton.Location = new System.Drawing.Point(86, 79);
            this.replaceMoveButton.Margin = new System.Windows.Forms.Padding(2);
            this.replaceMoveButton.Name = "replaceMoveButton";
            this.replaceMoveButton.Size = new System.Drawing.Size(80, 32);
            this.replaceMoveButton.TabIndex = 36;
            this.replaceMoveButton.Text = "Replace";
            this.replaceMoveButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.replaceMoveButton.UseVisualStyleBackColor = true;
            this.replaceMoveButton.Click += new System.EventHandler(this.replaceMoveButton_Click);
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
            this.addMoveButton.Size = new System.Drawing.Size(80, 32);
            this.addMoveButton.TabIndex = 35;
            this.addMoveButton.Text = "Add";
            this.addMoveButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.addMoveButton.UseVisualStyleBackColor = true;
            this.addMoveButton.Click += new System.EventHandler(this.addMoveButton_Click);
            // 
            // moveLabel
            // 
            this.moveLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.moveLabel.AutoSize = true;
            this.moveLabel.Location = new System.Drawing.Point(3, 13);
            this.moveLabel.Name = "moveLabel";
            this.moveLabel.Size = new System.Drawing.Size(78, 13);
            this.moveLabel.TabIndex = 0;
            this.moveLabel.Text = "Move";
            // 
            // moveComboBox
            // 
            this.moveComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.editTableLayoutPanel.SetColumnSpan(this.moveComboBox, 2);
            this.moveComboBox.FormattingEnabled = true;
            this.moveComboBox.Location = new System.Drawing.Point(87, 9);
            this.moveComboBox.Name = "moveComboBox";
            this.moveComboBox.Size = new System.Drawing.Size(164, 21);
            this.moveComboBox.TabIndex = 2;
            this.moveComboBox.SelectedIndexChanged += new System.EventHandler(this.moveComboBox_SelectedIndexChanged);
            // 
            // monListGroupBox
            // 
            this.monListGroupBox.Controls.Add(this.monListBox);
            this.monListGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.monListGroupBox.Location = new System.Drawing.Point(3, 3);
            this.monListGroupBox.Name = "monListGroupBox";
            this.monListGroupBox.Size = new System.Drawing.Size(171, 414);
            this.monListGroupBox.TabIndex = 2;
            this.monListGroupBox.TabStop = false;
            this.monListGroupBox.Text = "Pokémon List";
            // 
            // monListBox
            // 
            this.monListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.monListBox.FormattingEnabled = true;
            this.monListBox.Location = new System.Drawing.Point(3, 16);
            this.monListBox.Name = "monListBox";
            this.monListBox.Size = new System.Drawing.Size(165, 395);
            this.monListBox.TabIndex = 1;
            this.monListBox.SelectedIndexChanged += new System.EventHandler(this.monListBox_SelectedIndexChanged);
            // 
            // EggMoveEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(909, 444);
            this.Controls.Add(this.mainTableLayoutPanel);
            this.Name = "EggMoveEditor";
            this.Text = "Egg Move Editor";
            this.mainTableLayoutPanel.ResumeLayout(false);
            this.editMonsGroupBox.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.moveListGroupBox.ResumeLayout(false);
            this.editMovesGroupBox.ResumeLayout(false);
            this.editTableLayoutPanel.ResumeLayout(false);
            this.editTableLayoutPanel.PerformLayout();
            this.monListGroupBox.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel mainTableLayoutPanel;
        private System.Windows.Forms.GroupBox moveListGroupBox;
        private System.Windows.Forms.GroupBox editMovesGroupBox;
        private System.Windows.Forms.ListBox eggMoveListBox;
        private System.Windows.Forms.TableLayoutPanel editTableLayoutPanel;
        private System.Windows.Forms.Label moveLabel;
        private System.Windows.Forms.ComboBox moveComboBox;
        private System.Windows.Forms.Button addMoveButton;
        private System.Windows.Forms.Button replaceMoveButton;
        private System.Windows.Forms.Button deleteMoveButton;
        private System.Windows.Forms.Label listSizeLabel;
        private System.Windows.Forms.Label moveIDLabel;
        private System.Windows.Forms.Label moveStatusLabel;
        private System.Windows.Forms.Label moveCountLabel;
        private System.Windows.Forms.GroupBox monListGroupBox;
        private System.Windows.Forms.GroupBox editMonsGroupBox;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label monCountLabel;
        private System.Windows.Forms.Label monStatusLabel;
        private System.Windows.Forms.Label entryIDLabel;
        private System.Windows.Forms.Button deleteMonButton;
        private System.Windows.Forms.Button replaceMonButton;
        private System.Windows.Forms.Button addMonButton;
        private System.Windows.Forms.Label monLabel;
        private System.Windows.Forms.ComboBox monComboBox;
        private System.Windows.Forms.ListBox monListBox;
        private System.Windows.Forms.ToolTip toolTip;
    }
}