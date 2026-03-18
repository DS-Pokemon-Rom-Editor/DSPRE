namespace DSPRE.Editors
{
    partial class BattleMessageEditor
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
            this.trainerClassPicBox = new System.Windows.Forms.PictureBox();
            this.mainLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.trainerLabel = new System.Windows.Forms.Label();
            this.trainerIDUpDown = new System.Windows.Forms.NumericUpDown();
            this.trainerIDlabel = new System.Windows.Forms.Label();
            this.trainerTextListBox = new System.Windows.Forms.ListBox();
            this.buttonGroupBox = new System.Windows.Forms.GroupBox();
            this.manageLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.addButton = new System.Windows.Forms.Button();
            this.deleteButton = new System.Windows.Forms.Button();
            this.messageScintilla = new ScintillaNET.Scintilla();
            this.trainerComboBox = new DSPRE.InputComboBox();
            this.triggerTypeComboBox = new DSPRE.InputComboBox();
            this.editButton = new System.Windows.Forms.Button();
            this.saveMessageButton = new System.Windows.Forms.Button();
            this.saveButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.trainerClassPicBox)).BeginInit();
            this.mainLayoutPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trainerIDUpDown)).BeginInit();
            this.buttonGroupBox.SuspendLayout();
            this.manageLayoutPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // trainerClassPicBox
            // 
            this.trainerClassPicBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.trainerClassPicBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.trainerClassPicBox.Location = new System.Drawing.Point(3, 3);
            this.trainerClassPicBox.Name = "trainerClassPicBox";
            this.mainLayoutPanel.SetRowSpan(this.trainerClassPicBox, 3);
            this.trainerClassPicBox.Size = new System.Drawing.Size(194, 194);
            this.trainerClassPicBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.trainerClassPicBox.TabIndex = 1;
            this.trainerClassPicBox.TabStop = false;
            // 
            // mainLayoutPanel
            // 
            this.mainLayoutPanel.ColumnCount = 4;
            this.mainLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 200F));
            this.mainLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 91F));
            this.mainLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.mainLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.mainLayoutPanel.Controls.Add(this.saveButton, 3, 4);
            this.mainLayoutPanel.Controls.Add(this.saveMessageButton, 3, 3);
            this.mainLayoutPanel.Controls.Add(this.trainerLabel, 2, 0);
            this.mainLayoutPanel.Controls.Add(this.trainerComboBox, 2, 1);
            this.mainLayoutPanel.Controls.Add(this.trainerIDUpDown, 1, 1);
            this.mainLayoutPanel.Controls.Add(this.trainerClassPicBox, 0, 0);
            this.mainLayoutPanel.Controls.Add(this.trainerIDlabel, 1, 0);
            this.mainLayoutPanel.Controls.Add(this.trainerTextListBox, 1, 2);
            this.mainLayoutPanel.Controls.Add(this.buttonGroupBox, 3, 1);
            this.mainLayoutPanel.Controls.Add(this.messageScintilla, 0, 3);
            this.mainLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainLayoutPanel.Location = new System.Drawing.Point(0, 0);
            this.mainLayoutPanel.Name = "mainLayoutPanel";
            this.mainLayoutPanel.RowCount = 5;
            this.mainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.mainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.mainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.mainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.mainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.mainLayoutPanel.Size = new System.Drawing.Size(695, 331);
            this.mainLayoutPanel.TabIndex = 2;
            // 
            // trainerLabel
            // 
            this.trainerLabel.AutoSize = true;
            this.trainerLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.trainerLabel.Location = new System.Drawing.Point(294, 0);
            this.trainerLabel.Name = "trainerLabel";
            this.trainerLabel.Size = new System.Drawing.Size(196, 40);
            this.trainerLabel.TabIndex = 51;
            this.trainerLabel.Text = "Trainer Name";
            this.trainerLabel.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // trainerIDUpDown
            // 
            this.trainerIDUpDown.Dock = System.Windows.Forms.DockStyle.Fill;
            this.trainerIDUpDown.Location = new System.Drawing.Point(203, 43);
            this.trainerIDUpDown.Name = "trainerIDUpDown";
            this.trainerIDUpDown.Size = new System.Drawing.Size(85, 20);
            this.trainerIDUpDown.TabIndex = 2;
            this.trainerIDUpDown.ValueChanged += new System.EventHandler(this.trainerIDUpDown_ValueChanged);
            // 
            // trainerIDlabel
            // 
            this.trainerIDlabel.AutoSize = true;
            this.trainerIDlabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.trainerIDlabel.Location = new System.Drawing.Point(203, 0);
            this.trainerIDlabel.Name = "trainerIDlabel";
            this.trainerIDlabel.Size = new System.Drawing.Size(85, 40);
            this.trainerIDlabel.TabIndex = 50;
            this.trainerIDlabel.Text = "Trainer ID";
            this.trainerIDlabel.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // trainerTextListBox
            // 
            this.mainLayoutPanel.SetColumnSpan(this.trainerTextListBox, 2);
            this.trainerTextListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.trainerTextListBox.FormattingEnabled = true;
            this.trainerTextListBox.Location = new System.Drawing.Point(203, 83);
            this.trainerTextListBox.Name = "trainerTextListBox";
            this.trainerTextListBox.Size = new System.Drawing.Size(287, 114);
            this.trainerTextListBox.TabIndex = 52;
            this.trainerTextListBox.SelectedIndexChanged += new System.EventHandler(this.trainerTextListBox_SelectedIndexChanged);
            // 
            // buttonGroupBox
            // 
            this.buttonGroupBox.Controls.Add(this.manageLayoutPanel);
            this.buttonGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonGroupBox.Location = new System.Drawing.Point(496, 43);
            this.buttonGroupBox.Name = "buttonGroupBox";
            this.mainLayoutPanel.SetRowSpan(this.buttonGroupBox, 2);
            this.buttonGroupBox.Size = new System.Drawing.Size(196, 154);
            this.buttonGroupBox.TabIndex = 53;
            this.buttonGroupBox.TabStop = false;
            this.buttonGroupBox.Text = "Manage Messages";
            // 
            // manageLayoutPanel
            // 
            this.manageLayoutPanel.ColumnCount = 1;
            this.manageLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.manageLayoutPanel.Controls.Add(this.editButton, 0, 3);
            this.manageLayoutPanel.Controls.Add(this.triggerTypeComboBox, 0, 2);
            this.manageLayoutPanel.Controls.Add(this.deleteButton, 0, 1);
            this.manageLayoutPanel.Controls.Add(this.addButton, 0, 0);
            this.manageLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.manageLayoutPanel.Location = new System.Drawing.Point(3, 16);
            this.manageLayoutPanel.Name = "manageLayoutPanel";
            this.manageLayoutPanel.RowCount = 4;
            this.manageLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25.00062F));
            this.manageLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25.00063F));
            this.manageLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25.00063F));
            this.manageLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 24.99813F));
            this.manageLayoutPanel.Size = new System.Drawing.Size(190, 135);
            this.manageLayoutPanel.TabIndex = 0;
            this.manageLayoutPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.manageLayoutPanel_Paint);
            // 
            // addButton
            // 
            this.addButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.addButton.Image = global::DSPRE.Properties.Resources.addIcon;
            this.addButton.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.addButton.Location = new System.Drawing.Point(3, 3);
            this.addButton.Name = "addButton";
            this.addButton.Size = new System.Drawing.Size(184, 27);
            this.addButton.TabIndex = 0;
            this.addButton.Text = "Add";
            this.addButton.UseVisualStyleBackColor = true;
            // 
            // deleteButton
            // 
            this.deleteButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.deleteButton.Image = global::DSPRE.Properties.Resources.deleteIcon;
            this.deleteButton.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.deleteButton.Location = new System.Drawing.Point(3, 36);
            this.deleteButton.Name = "deleteButton";
            this.deleteButton.Size = new System.Drawing.Size(184, 27);
            this.deleteButton.TabIndex = 1;
            this.deleteButton.Text = "Remove";
            this.deleteButton.UseVisualStyleBackColor = true;
            // 
            // messageScintilla
            // 
            this.mainLayoutPanel.SetColumnSpan(this.messageScintilla, 3);
            this.messageScintilla.Dock = System.Windows.Forms.DockStyle.Fill;
            this.messageScintilla.Enabled = false;
            this.messageScintilla.Location = new System.Drawing.Point(3, 203);
            this.messageScintilla.Name = "messageScintilla";
            this.mainLayoutPanel.SetRowSpan(this.messageScintilla, 2);
            this.messageScintilla.Size = new System.Drawing.Size(487, 125);
            this.messageScintilla.TabIndex = 54;
            this.messageScintilla.Text = "Message Here";
            this.messageScintilla.Zoom = 3;
            // 
            // trainerComboBox
            // 
            this.trainerComboBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.trainerComboBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.trainerComboBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.trainerComboBox.FormattingEnabled = true;
            this.trainerComboBox.Location = new System.Drawing.Point(294, 43);
            this.trainerComboBox.Name = "trainerComboBox";
            this.trainerComboBox.Size = new System.Drawing.Size(196, 21);
            this.trainerComboBox.TabIndex = 49;
            this.trainerComboBox.SelectedIndexChanged += new System.EventHandler(this.trainerComboBox_SelectedIndexChanged);
            // 
            // triggerTypeComboBox
            // 
            this.triggerTypeComboBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.triggerTypeComboBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.triggerTypeComboBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.triggerTypeComboBox.FormattingEnabled = true;
            this.triggerTypeComboBox.Location = new System.Drawing.Point(3, 69);
            this.triggerTypeComboBox.Name = "triggerTypeComboBox";
            this.triggerTypeComboBox.Size = new System.Drawing.Size(184, 21);
            this.triggerTypeComboBox.TabIndex = 50;
            // 
            // editButton
            // 
            this.editButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.editButton.Image = global::DSPRE.Properties.Resources.RenameIcon;
            this.editButton.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.editButton.Location = new System.Drawing.Point(3, 102);
            this.editButton.Name = "editButton";
            this.editButton.Size = new System.Drawing.Size(184, 30);
            this.editButton.TabIndex = 51;
            this.editButton.Text = "Change Trigger";
            this.editButton.UseVisualStyleBackColor = true;
            // 
            // saveMessageButton
            // 
            this.saveMessageButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.saveMessageButton.Image = global::DSPRE.Properties.Resources.saveButton;
            this.saveMessageButton.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.saveMessageButton.Location = new System.Drawing.Point(496, 203);
            this.saveMessageButton.Name = "saveMessageButton";
            this.saveMessageButton.Size = new System.Drawing.Size(196, 34);
            this.saveMessageButton.TabIndex = 55;
            this.saveMessageButton.Text = "Save Message";
            this.saveMessageButton.UseVisualStyleBackColor = true;
            // 
            // saveButton
            // 
            this.saveButton.Dock = System.Windows.Forms.DockStyle.Right;
            this.saveButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.saveButton.Image = global::DSPRE.Properties.Resources.save_rom;
            this.saveButton.ImageAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.saveButton.Location = new System.Drawing.Point(606, 243);
            this.saveButton.Name = "saveButton";
            this.saveButton.Size = new System.Drawing.Size(86, 85);
            this.saveButton.TabIndex = 56;
            this.saveButton.Text = "Save \r\nand \r\nSort Table\r\n";
            this.saveButton.TextImageRelation = System.Windows.Forms.TextImageRelation.TextAboveImage;
            this.saveButton.UseVisualStyleBackColor = true;
            // 
            // BattleMessageEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(695, 331);
            this.Controls.Add(this.mainLayoutPanel);
            this.Name = "BattleMessageEditor";
            this.Text = "BattleMessageEditor";
            ((System.ComponentModel.ISupportInitialize)(this.trainerClassPicBox)).EndInit();
            this.mainLayoutPanel.ResumeLayout(false);
            this.mainLayoutPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trainerIDUpDown)).EndInit();
            this.buttonGroupBox.ResumeLayout(false);
            this.manageLayoutPanel.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        public System.Windows.Forms.PictureBox trainerClassPicBox;
        private System.Windows.Forms.TableLayoutPanel mainLayoutPanel;
        private System.Windows.Forms.NumericUpDown trainerIDUpDown;
        public InputComboBox trainerComboBox;
        private System.Windows.Forms.Label trainerIDlabel;
        private System.Windows.Forms.Label trainerLabel;
        private System.Windows.Forms.ListBox trainerTextListBox;
        private System.Windows.Forms.GroupBox buttonGroupBox;
        private System.Windows.Forms.TableLayoutPanel manageLayoutPanel;
        private System.Windows.Forms.Button deleteButton;
        private System.Windows.Forms.Button addButton;
        public InputComboBox triggerTypeComboBox;
        private ScintillaNET.Scintilla messageScintilla;
        private System.Windows.Forms.Button editButton;
        private System.Windows.Forms.Button saveButton;
        private System.Windows.Forms.Button saveMessageButton;
    }
}