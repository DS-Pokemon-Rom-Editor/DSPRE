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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EggMoveEditor));
            this.mainTableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.editMonsGroupBox = new System.Windows.Forms.GroupBox();
            this.editMonTableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.monCountLabel = new System.Windows.Forms.Label();
            this.monStatusLabel = new System.Windows.Forms.Label();
            this.entryIDLabel = new System.Windows.Forms.Label();
            this.listSizeLabel = new System.Windows.Forms.Label();
            this.deleteMonButton = new System.Windows.Forms.Button();
            this.replaceMonButton = new System.Windows.Forms.Button();
            this.addMonButton = new System.Windows.Forms.Button();
            this.monLabel = new System.Windows.Forms.Label();
            this.monComboBox = new System.Windows.Forms.ComboBox();
            this.monSearchTextBox = new System.Windows.Forms.TextBox();
            this.searchMonButton = new System.Windows.Forms.Button();
            this.monSearchGroupBox = new System.Windows.Forms.GroupBox();
            this.monSearchListBox = new System.Windows.Forms.ListBox();
            this.moveListGroupBox = new System.Windows.Forms.GroupBox();
            this.eggMoveListBox = new System.Windows.Forms.ListBox();
            this.editMovesGroupBox = new System.Windows.Forms.GroupBox();
            this.editMoveTableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.moveCountLabel = new System.Windows.Forms.Label();
            this.moveStatusLabel = new System.Windows.Forms.Label();
            this.moveIDLabel = new System.Windows.Forms.Label();
            this.deleteMoveButton = new System.Windows.Forms.Button();
            this.saveDataButton = new System.Windows.Forms.Button();
            this.replaceMoveButton = new System.Windows.Forms.Button();
            this.addMoveButton = new System.Windows.Forms.Button();
            this.moveLabel = new System.Windows.Forms.Label();
            this.moveComboBox = new System.Windows.Forms.ComboBox();
            this.monListGroupBox = new System.Windows.Forms.GroupBox();
            this.monListBox = new System.Windows.Forms.ListBox();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.bulkEditGroupBox = new System.Windows.Forms.GroupBox();
            this.bulkEditTableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.replaceAllLabel = new System.Windows.Forms.Label();
            this.replaceeComboBox = new System.Windows.Forms.ComboBox();
            this.replacerComboBox = new System.Windows.Forms.ComboBox();
            this.withLabel = new System.Windows.Forms.Label();
            this.bulkReplaceButton = new System.Windows.Forms.Button();
            this.deleteAllLabel = new System.Windows.Forms.Label();
            this.deleteAllComboBox = new System.Windows.Forms.ComboBox();
            this.deleteAllButton = new System.Windows.Forms.Button();
            this.mainTableLayoutPanel.SuspendLayout();
            this.editMonsGroupBox.SuspendLayout();
            this.editMonTableLayoutPanel.SuspendLayout();
            this.monSearchGroupBox.SuspendLayout();
            this.moveListGroupBox.SuspendLayout();
            this.editMovesGroupBox.SuspendLayout();
            this.editMoveTableLayoutPanel.SuspendLayout();
            this.monListGroupBox.SuspendLayout();
            this.bulkEditGroupBox.SuspendLayout();
            this.bulkEditTableLayoutPanel.SuspendLayout();
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
            this.mainTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.mainTableLayoutPanel.Size = new System.Drawing.Size(885, 423);
            this.mainTableLayoutPanel.TabIndex = 0;
            // 
            // editMonsGroupBox
            // 
            this.editMonsGroupBox.Controls.Add(this.editMonTableLayoutPanel);
            this.editMonsGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.editMonsGroupBox.Location = new System.Drawing.Point(180, 3);
            this.editMonsGroupBox.Name = "editMonsGroupBox";
            this.editMonsGroupBox.Size = new System.Drawing.Size(259, 417);
            this.editMonsGroupBox.TabIndex = 3;
            this.editMonsGroupBox.TabStop = false;
            this.editMonsGroupBox.Text = "Edit Pokémon";
            // 
            // editMonTableLayoutPanel
            // 
            this.editMonTableLayoutPanel.ColumnCount = 3;
            this.editMonTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.editMonTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.editMonTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.editMonTableLayoutPanel.Controls.Add(this.monCountLabel, 0, 3);
            this.editMonTableLayoutPanel.Controls.Add(this.monStatusLabel, 1, 1);
            this.editMonTableLayoutPanel.Controls.Add(this.entryIDLabel, 0, 1);
            this.editMonTableLayoutPanel.Controls.Add(this.listSizeLabel, 0, 6);
            this.editMonTableLayoutPanel.Controls.Add(this.deleteMonButton, 2, 2);
            this.editMonTableLayoutPanel.Controls.Add(this.replaceMonButton, 1, 2);
            this.editMonTableLayoutPanel.Controls.Add(this.addMonButton, 0, 2);
            this.editMonTableLayoutPanel.Controls.Add(this.monLabel, 0, 0);
            this.editMonTableLayoutPanel.Controls.Add(this.monComboBox, 1, 0);
            this.editMonTableLayoutPanel.Controls.Add(this.monSearchTextBox, 0, 4);
            this.editMonTableLayoutPanel.Controls.Add(this.searchMonButton, 2, 4);
            this.editMonTableLayoutPanel.Controls.Add(this.monSearchGroupBox, 0, 5);
            this.editMonTableLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.editMonTableLayoutPanel.Location = new System.Drawing.Point(3, 16);
            this.editMonTableLayoutPanel.Name = "editMonTableLayoutPanel";
            this.editMonTableLayoutPanel.RowCount = 7;
            this.editMonTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.editMonTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.editMonTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            this.editMonTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.editMonTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.editMonTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.editMonTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.editMonTableLayoutPanel.Size = new System.Drawing.Size(253, 398);
            this.editMonTableLayoutPanel.TabIndex = 0;
            // 
            // monCountLabel
            // 
            this.monCountLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.monCountLabel.AutoSize = true;
            this.editMonTableLayoutPanel.SetColumnSpan(this.monCountLabel, 2);
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
            this.editMonTableLayoutPanel.SetColumnSpan(this.monStatusLabel, 2);
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
            this.entryIDLabel.Text = "Entry Index:";
            // 
            // listSizeLabel
            // 
            this.listSizeLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listSizeLabel.AutoSize = true;
            this.editMonTableLayoutPanel.SetColumnSpan(this.listSizeLabel, 2);
            this.listSizeLabel.Location = new System.Drawing.Point(3, 380);
            this.listSizeLabel.Margin = new System.Windows.Forms.Padding(3, 0, 3, 5);
            this.listSizeLabel.Name = "listSizeLabel";
            this.listSizeLabel.Size = new System.Drawing.Size(162, 13);
            this.listSizeLabel.TabIndex = 38;
            this.listSizeLabel.Text = "Total List Size:";
            this.toolTip.SetToolTip(this.listSizeLabel, "DPPt have a strictly limited list size. \r\nIn order to add new entries others have" +
        " to be replaced or removed.");
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
            this.monComboBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.editMonTableLayoutPanel.SetColumnSpan(this.monComboBox, 2);
            this.monComboBox.FormattingEnabled = true;
            this.monComboBox.Location = new System.Drawing.Point(87, 9);
            this.monComboBox.Name = "monComboBox";
            this.monComboBox.Size = new System.Drawing.Size(163, 21);
            this.monComboBox.TabIndex = 2;
            this.monComboBox.SelectedIndexChanged += new System.EventHandler(this.monComboBox_SelectedIndexChanged);
            // 
            // monSearchTextBox
            // 
            this.monSearchTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.editMonTableLayoutPanel.SetColumnSpan(this.monSearchTextBox, 2);
            this.monSearchTextBox.Location = new System.Drawing.Point(3, 160);
            this.monSearchTextBox.Name = "monSearchTextBox";
            this.monSearchTextBox.Size = new System.Drawing.Size(162, 20);
            this.monSearchTextBox.TabIndex = 42;
            this.monSearchTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.monSearchTextBox_KeyDown);
            // 
            // searchMonButton
            // 
            this.searchMonButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.searchMonButton.Image = global::DSPRE.Properties.Resources.SearchMiniIcon;
            this.searchMonButton.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.searchMonButton.Location = new System.Drawing.Point(170, 154);
            this.searchMonButton.Margin = new System.Windows.Forms.Padding(2);
            this.searchMonButton.Name = "searchMonButton";
            this.searchMonButton.Size = new System.Drawing.Size(81, 32);
            this.searchMonButton.TabIndex = 43;
            this.searchMonButton.Text = "Search";
            this.searchMonButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.searchMonButton.UseVisualStyleBackColor = true;
            this.searchMonButton.Click += new System.EventHandler(this.searchMonButton_Click);
            // 
            // monSearchGroupBox
            // 
            this.editMonTableLayoutPanel.SetColumnSpan(this.monSearchGroupBox, 3);
            this.monSearchGroupBox.Controls.Add(this.monSearchListBox);
            this.monSearchGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.monSearchGroupBox.Location = new System.Drawing.Point(3, 193);
            this.monSearchGroupBox.Name = "monSearchGroupBox";
            this.monSearchGroupBox.Size = new System.Drawing.Size(247, 172);
            this.monSearchGroupBox.TabIndex = 44;
            this.monSearchGroupBox.TabStop = false;
            this.monSearchGroupBox.Text = "Pokémon Search";
            // 
            // monSearchListBox
            // 
            this.monSearchListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.monSearchListBox.FormattingEnabled = true;
            this.monSearchListBox.Location = new System.Drawing.Point(3, 16);
            this.monSearchListBox.Name = "monSearchListBox";
            this.monSearchListBox.Size = new System.Drawing.Size(241, 153);
            this.monSearchListBox.TabIndex = 0;
            this.toolTip.SetToolTip(this.monSearchListBox, "Double-click to select entry in main list");
            this.monSearchListBox.DoubleClick += new System.EventHandler(this.monSearchListBox_DoubleClick);
            // 
            // moveListGroupBox
            // 
            this.moveListGroupBox.Controls.Add(this.eggMoveListBox);
            this.moveListGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.moveListGroupBox.Location = new System.Drawing.Point(445, 3);
            this.moveListGroupBox.Name = "moveListGroupBox";
            this.moveListGroupBox.Size = new System.Drawing.Size(171, 417);
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
            this.eggMoveListBox.Size = new System.Drawing.Size(165, 398);
            this.eggMoveListBox.TabIndex = 0;
            this.eggMoveListBox.SelectedIndexChanged += new System.EventHandler(this.eggMoveListBox_SelectedIndexChanged);
            // 
            // editMovesGroupBox
            // 
            this.editMovesGroupBox.Controls.Add(this.editMoveTableLayoutPanel);
            this.editMovesGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.editMovesGroupBox.Location = new System.Drawing.Point(622, 3);
            this.editMovesGroupBox.Name = "editMovesGroupBox";
            this.editMovesGroupBox.Size = new System.Drawing.Size(260, 417);
            this.editMovesGroupBox.TabIndex = 1;
            this.editMovesGroupBox.TabStop = false;
            this.editMovesGroupBox.Text = "Edit Moves";
            // 
            // editMoveTableLayoutPanel
            // 
            this.editMoveTableLayoutPanel.ColumnCount = 3;
            this.editMoveTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.editMoveTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.editMoveTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.editMoveTableLayoutPanel.Controls.Add(this.moveCountLabel, 0, 3);
            this.editMoveTableLayoutPanel.Controls.Add(this.moveStatusLabel, 1, 1);
            this.editMoveTableLayoutPanel.Controls.Add(this.moveIDLabel, 0, 1);
            this.editMoveTableLayoutPanel.Controls.Add(this.deleteMoveButton, 2, 2);
            this.editMoveTableLayoutPanel.Controls.Add(this.saveDataButton, 2, 5);
            this.editMoveTableLayoutPanel.Controls.Add(this.replaceMoveButton, 1, 2);
            this.editMoveTableLayoutPanel.Controls.Add(this.addMoveButton, 0, 2);
            this.editMoveTableLayoutPanel.Controls.Add(this.moveLabel, 0, 0);
            this.editMoveTableLayoutPanel.Controls.Add(this.moveComboBox, 1, 0);
            this.editMoveTableLayoutPanel.Controls.Add(this.bulkEditGroupBox, 0, 4);
            this.editMoveTableLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.editMoveTableLayoutPanel.Location = new System.Drawing.Point(3, 16);
            this.editMoveTableLayoutPanel.Name = "editMoveTableLayoutPanel";
            this.editMoveTableLayoutPanel.RowCount = 6;
            this.editMoveTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.editMoveTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.editMoveTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            this.editMoveTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.editMoveTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.editMoveTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            this.editMoveTableLayoutPanel.Size = new System.Drawing.Size(254, 398);
            this.editMoveTableLayoutPanel.TabIndex = 0;
            // 
            // moveCountLabel
            // 
            this.moveCountLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.moveCountLabel.AutoSize = true;
            this.editMoveTableLayoutPanel.SetColumnSpan(this.moveCountLabel, 2);
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
            this.editMoveTableLayoutPanel.SetColumnSpan(this.moveStatusLabel, 2);
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
            this.moveIDLabel.Text = "Move Index:";
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
            // saveDataButton
            // 
            this.saveDataButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.saveDataButton.Image = ((System.Drawing.Image)(resources.GetObject("saveDataButton.Image")));
            this.saveDataButton.Location = new System.Drawing.Point(207, 351);
            this.saveDataButton.Name = "saveDataButton";
            this.saveDataButton.Size = new System.Drawing.Size(44, 44);
            this.saveDataButton.TabIndex = 31;
            this.saveDataButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.saveDataButton.UseVisualStyleBackColor = true;
            this.saveDataButton.Click += new System.EventHandler(this.saveDataButton_Click);
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
            this.moveComboBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.editMoveTableLayoutPanel.SetColumnSpan(this.moveComboBox, 2);
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
            this.monListGroupBox.Size = new System.Drawing.Size(171, 417);
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
            this.monListBox.Size = new System.Drawing.Size(165, 398);
            this.monListBox.TabIndex = 1;
            this.monListBox.SelectedIndexChanged += new System.EventHandler(this.monListBox_SelectedIndexChanged);
            // 
            // bulkEditGroupBox
            // 
            this.editMoveTableLayoutPanel.SetColumnSpan(this.bulkEditGroupBox, 3);
            this.bulkEditGroupBox.Controls.Add(this.bulkEditTableLayoutPanel);
            this.bulkEditGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.bulkEditGroupBox.Location = new System.Drawing.Point(3, 153);
            this.bulkEditGroupBox.Name = "bulkEditGroupBox";
            this.bulkEditGroupBox.Size = new System.Drawing.Size(248, 192);
            this.bulkEditGroupBox.TabIndex = 42;
            this.bulkEditGroupBox.TabStop = false;
            this.bulkEditGroupBox.Text = "Bulk Edit";
            // 
            // bulkEditTableLayoutPanel
            // 
            this.bulkEditTableLayoutPanel.ColumnCount = 2;
            this.bulkEditTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.bulkEditTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.bulkEditTableLayoutPanel.Controls.Add(this.deleteAllComboBox, 1, 1);
            this.bulkEditTableLayoutPanel.Controls.Add(this.deleteAllLabel, 1, 0);
            this.bulkEditTableLayoutPanel.Controls.Add(this.replaceAllLabel, 0, 0);
            this.bulkEditTableLayoutPanel.Controls.Add(this.replaceeComboBox, 0, 1);
            this.bulkEditTableLayoutPanel.Controls.Add(this.replacerComboBox, 0, 3);
            this.bulkEditTableLayoutPanel.Controls.Add(this.withLabel, 0, 2);
            this.bulkEditTableLayoutPanel.Controls.Add(this.bulkReplaceButton, 0, 4);
            this.bulkEditTableLayoutPanel.Controls.Add(this.deleteAllButton, 1, 4);
            this.bulkEditTableLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.bulkEditTableLayoutPanel.Location = new System.Drawing.Point(3, 16);
            this.bulkEditTableLayoutPanel.Name = "bulkEditTableLayoutPanel";
            this.bulkEditTableLayoutPanel.RowCount = 6;
            this.bulkEditTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.bulkEditTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.bulkEditTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.bulkEditTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.bulkEditTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.bulkEditTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.bulkEditTableLayoutPanel.Size = new System.Drawing.Size(242, 173);
            this.bulkEditTableLayoutPanel.TabIndex = 0;
            // 
            // replaceAllLabel
            // 
            this.replaceAllLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.replaceAllLabel.AutoSize = true;
            this.replaceAllLabel.Location = new System.Drawing.Point(3, 0);
            this.replaceAllLabel.Name = "replaceAllLabel";
            this.replaceAllLabel.Size = new System.Drawing.Size(61, 25);
            this.replaceAllLabel.TabIndex = 0;
            this.replaceAllLabel.Text = "Replace All";
            this.replaceAllLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // replaceeComboBox
            // 
            this.replaceeComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.replaceeComboBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.replaceeComboBox.FormattingEnabled = true;
            this.replaceeComboBox.Location = new System.Drawing.Point(3, 29);
            this.replaceeComboBox.Name = "replaceeComboBox";
            this.replaceeComboBox.Size = new System.Drawing.Size(115, 21);
            this.replaceeComboBox.TabIndex = 3;
            // 
            // replacerComboBox
            // 
            this.replacerComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.replacerComboBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.replacerComboBox.FormattingEnabled = true;
            this.replacerComboBox.Location = new System.Drawing.Point(3, 84);
            this.replacerComboBox.Name = "replacerComboBox";
            this.replacerComboBox.Size = new System.Drawing.Size(115, 21);
            this.replacerComboBox.TabIndex = 4;
            // 
            // withLabel
            // 
            this.withLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.withLabel.AutoSize = true;
            this.withLabel.Location = new System.Drawing.Point(3, 55);
            this.withLabel.Name = "withLabel";
            this.withLabel.Size = new System.Drawing.Size(26, 25);
            this.withLabel.TabIndex = 5;
            this.withLabel.Text = "with";
            this.withLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // bulkReplaceButton
            // 
            this.bulkReplaceButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.bulkReplaceButton.Image = global::DSPRE.Properties.Resources.RenameIcon;
            this.bulkReplaceButton.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.bulkReplaceButton.Location = new System.Drawing.Point(2, 114);
            this.bulkReplaceButton.Margin = new System.Windows.Forms.Padding(2);
            this.bulkReplaceButton.Name = "bulkReplaceButton";
            this.bulkReplaceButton.Size = new System.Drawing.Size(117, 32);
            this.bulkReplaceButton.TabIndex = 37;
            this.bulkReplaceButton.Text = "Bulk Replace";
            this.bulkReplaceButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.bulkReplaceButton.UseVisualStyleBackColor = true;
            this.bulkReplaceButton.Click += new System.EventHandler(this.bulkReplaceButton_Click);
            // 
            // deleteAllLabel
            // 
            this.deleteAllLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.deleteAllLabel.AutoSize = true;
            this.deleteAllLabel.Location = new System.Drawing.Point(124, 0);
            this.deleteAllLabel.Name = "deleteAllLabel";
            this.deleteAllLabel.Size = new System.Drawing.Size(52, 25);
            this.deleteAllLabel.TabIndex = 38;
            this.deleteAllLabel.Text = "Delete All";
            this.deleteAllLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // deleteAllComboBox
            // 
            this.deleteAllComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.deleteAllComboBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.deleteAllComboBox.FormattingEnabled = true;
            this.deleteAllComboBox.Location = new System.Drawing.Point(124, 29);
            this.deleteAllComboBox.Name = "deleteAllComboBox";
            this.deleteAllComboBox.Size = new System.Drawing.Size(115, 21);
            this.deleteAllComboBox.TabIndex = 39;
            // 
            // deleteAllButton
            // 
            this.deleteAllButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.deleteAllButton.Image = global::DSPRE.Properties.Resources.deleteIcon;
            this.deleteAllButton.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.deleteAllButton.Location = new System.Drawing.Point(123, 114);
            this.deleteAllButton.Margin = new System.Windows.Forms.Padding(2);
            this.deleteAllButton.Name = "deleteAllButton";
            this.deleteAllButton.Size = new System.Drawing.Size(117, 32);
            this.deleteAllButton.TabIndex = 40;
            this.deleteAllButton.Text = "Bulk Delete";
            this.deleteAllButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.deleteAllButton.UseVisualStyleBackColor = true;
            this.deleteAllButton.Click += new System.EventHandler(this.deleteAllButton_Click);
            // 
            // EggMoveEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(909, 447);
            this.Controls.Add(this.mainTableLayoutPanel);
            this.Name = "EggMoveEditor";
            this.Text = "Egg Move Editor";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.EggMoveEditor_FormClosing);
            this.mainTableLayoutPanel.ResumeLayout(false);
            this.editMonsGroupBox.ResumeLayout(false);
            this.editMonTableLayoutPanel.ResumeLayout(false);
            this.editMonTableLayoutPanel.PerformLayout();
            this.monSearchGroupBox.ResumeLayout(false);
            this.moveListGroupBox.ResumeLayout(false);
            this.editMovesGroupBox.ResumeLayout(false);
            this.editMoveTableLayoutPanel.ResumeLayout(false);
            this.editMoveTableLayoutPanel.PerformLayout();
            this.monListGroupBox.ResumeLayout(false);
            this.bulkEditGroupBox.ResumeLayout(false);
            this.bulkEditTableLayoutPanel.ResumeLayout(false);
            this.bulkEditTableLayoutPanel.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel mainTableLayoutPanel;
        private System.Windows.Forms.GroupBox moveListGroupBox;
        private System.Windows.Forms.GroupBox editMovesGroupBox;
        private System.Windows.Forms.ListBox eggMoveListBox;
        private System.Windows.Forms.TableLayoutPanel editMoveTableLayoutPanel;
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
        private System.Windows.Forms.TableLayoutPanel editMonTableLayoutPanel;
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
        private System.Windows.Forms.Button saveDataButton;
        private System.Windows.Forms.TextBox monSearchTextBox;
        private System.Windows.Forms.Button searchMonButton;
        private System.Windows.Forms.GroupBox monSearchGroupBox;
        private System.Windows.Forms.ListBox monSearchListBox;
        private System.Windows.Forms.GroupBox bulkEditGroupBox;
        private System.Windows.Forms.TableLayoutPanel bulkEditTableLayoutPanel;
        private System.Windows.Forms.Label replaceAllLabel;
        private System.Windows.Forms.ComboBox replaceeComboBox;
        private System.Windows.Forms.ComboBox replacerComboBox;
        private System.Windows.Forms.Label withLabel;
        private System.Windows.Forms.Button bulkReplaceButton;
        private System.Windows.Forms.ComboBox deleteAllComboBox;
        private System.Windows.Forms.Label deleteAllLabel;
        private System.Windows.Forms.Button deleteAllButton;
    }
}