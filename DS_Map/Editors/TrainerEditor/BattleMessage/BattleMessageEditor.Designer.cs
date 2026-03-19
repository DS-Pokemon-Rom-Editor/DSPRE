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
            this.infoLabel = new System.Windows.Forms.Label();
            this.saveButton = new System.Windows.Forms.Button();
            this.saveMessageButton = new System.Windows.Forms.Button();
            this.trainerLabel = new System.Windows.Forms.Label();
            this.trainerIDUpDown = new System.Windows.Forms.NumericUpDown();
            this.trainerIDlabel = new System.Windows.Forms.Label();
            this.trainerTextListBox = new System.Windows.Forms.ListBox();
            this.buttonGroupBox = new System.Windows.Forms.GroupBox();
            this.manageLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.editButton = new System.Windows.Forms.Button();
            this.deleteButton = new System.Windows.Forms.Button();
            this.addButton = new System.Windows.Forms.Button();
            this.trainerComboBox = new DSPRE.InputComboBox();
            this.triggerTypeComboBox = new DSPRE.InputComboBox();
            this.dsTextBox = new DSPRE.Editors.DSTextBox();
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
            this.mainLayoutPanel.SetRowSpan(this.trainerClassPicBox, 2);
            this.trainerClassPicBox.Size = new System.Drawing.Size(84, 84);
            this.trainerClassPicBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.trainerClassPicBox.TabIndex = 1;
            this.trainerClassPicBox.TabStop = false;
            // 
            // mainLayoutPanel
            // 
            this.mainLayoutPanel.ColumnCount = 4;
            this.mainLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 90F));
            this.mainLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 90F));
            this.mainLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 342F));
            this.mainLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.mainLayoutPanel.Controls.Add(this.infoLabel, 3, 0);
            this.mainLayoutPanel.Controls.Add(this.saveButton, 3, 4);
            this.mainLayoutPanel.Controls.Add(this.saveMessageButton, 3, 3);
            this.mainLayoutPanel.Controls.Add(this.trainerLabel, 2, 0);
            this.mainLayoutPanel.Controls.Add(this.trainerComboBox, 2, 1);
            this.mainLayoutPanel.Controls.Add(this.trainerIDUpDown, 1, 1);
            this.mainLayoutPanel.Controls.Add(this.trainerClassPicBox, 0, 0);
            this.mainLayoutPanel.Controls.Add(this.trainerIDlabel, 1, 0);
            this.mainLayoutPanel.Controls.Add(this.trainerTextListBox, 0, 2);
            this.mainLayoutPanel.Controls.Add(this.buttonGroupBox, 3, 1);
            this.mainLayoutPanel.Controls.Add(this.dsTextBox, 0, 3);
            this.mainLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainLayoutPanel.Location = new System.Drawing.Point(0, 0);
            this.mainLayoutPanel.Name = "mainLayoutPanel";
            this.mainLayoutPanel.RowCount = 6;
            this.mainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 45F));
            this.mainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 45F));
            this.mainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.mainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.mainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 60F));
            this.mainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 10F));
            this.mainLayoutPanel.Size = new System.Drawing.Size(737, 331);
            this.mainLayoutPanel.TabIndex = 2;
            // 
            // infoLabel
            // 
            this.infoLabel.AutoSize = true;
            this.infoLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.infoLabel.Location = new System.Drawing.Point(525, 0);
            this.infoLabel.Name = "infoLabel";
            this.infoLabel.Size = new System.Drawing.Size(209, 45);
            this.infoLabel.TabIndex = 57;
            this.infoLabel.Text = "Info";
            this.infoLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // saveButton
            // 
            this.saveButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.saveButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.saveButton.Image = global::DSPRE.Properties.Resources.save_rom;
            this.saveButton.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.saveButton.Location = new System.Drawing.Point(525, 253);
            this.saveButton.Name = "saveButton";
            this.saveButton.Size = new System.Drawing.Size(209, 54);
            this.saveButton.TabIndex = 56;
            this.saveButton.Text = "Save \r\nand \r\nSort Table\r\n";
            this.saveButton.UseVisualStyleBackColor = true;
            this.saveButton.Click += new System.EventHandler(this.saveButton_Click);
            // 
            // saveMessageButton
            // 
            this.saveMessageButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.saveMessageButton.Image = global::DSPRE.Properties.Resources.saveButton;
            this.saveMessageButton.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.saveMessageButton.Location = new System.Drawing.Point(525, 213);
            this.saveMessageButton.Name = "saveMessageButton";
            this.saveMessageButton.Size = new System.Drawing.Size(209, 34);
            this.saveMessageButton.TabIndex = 55;
            this.saveMessageButton.Text = "Save Message";
            this.saveMessageButton.UseVisualStyleBackColor = true;
            this.saveMessageButton.Click += new System.EventHandler(this.saveMessageButton_Click);
            // 
            // trainerLabel
            // 
            this.trainerLabel.AutoSize = true;
            this.trainerLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.trainerLabel.Location = new System.Drawing.Point(183, 0);
            this.trainerLabel.Name = "trainerLabel";
            this.trainerLabel.Size = new System.Drawing.Size(336, 45);
            this.trainerLabel.TabIndex = 51;
            this.trainerLabel.Text = "Trainer Name";
            this.trainerLabel.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // trainerIDUpDown
            // 
            this.trainerIDUpDown.Dock = System.Windows.Forms.DockStyle.Fill;
            this.trainerIDUpDown.Location = new System.Drawing.Point(93, 48);
            this.trainerIDUpDown.Name = "trainerIDUpDown";
            this.trainerIDUpDown.Size = new System.Drawing.Size(84, 20);
            this.trainerIDUpDown.TabIndex = 2;
            this.trainerIDUpDown.ValueChanged += new System.EventHandler(this.trainerIDUpDown_ValueChanged);
            // 
            // trainerIDlabel
            // 
            this.trainerIDlabel.AutoSize = true;
            this.trainerIDlabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.trainerIDlabel.Location = new System.Drawing.Point(93, 0);
            this.trainerIDlabel.Name = "trainerIDlabel";
            this.trainerIDlabel.Size = new System.Drawing.Size(84, 45);
            this.trainerIDlabel.TabIndex = 50;
            this.trainerIDlabel.Text = "Trainer ID";
            this.trainerIDlabel.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // trainerTextListBox
            // 
            this.mainLayoutPanel.SetColumnSpan(this.trainerTextListBox, 3);
            this.trainerTextListBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.trainerTextListBox.FormattingEnabled = true;
            this.trainerTextListBox.Location = new System.Drawing.Point(3, 93);
            this.trainerTextListBox.Name = "trainerTextListBox";
            this.trainerTextListBox.Size = new System.Drawing.Size(516, 114);
            this.trainerTextListBox.TabIndex = 52;
            this.trainerTextListBox.SelectedIndexChanged += new System.EventHandler(this.trainerTextListBox_SelectedIndexChanged);
            // 
            // buttonGroupBox
            // 
            this.buttonGroupBox.Controls.Add(this.manageLayoutPanel);
            this.buttonGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonGroupBox.Location = new System.Drawing.Point(525, 48);
            this.buttonGroupBox.Name = "buttonGroupBox";
            this.mainLayoutPanel.SetRowSpan(this.buttonGroupBox, 2);
            this.buttonGroupBox.Size = new System.Drawing.Size(209, 159);
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
            this.manageLayoutPanel.Size = new System.Drawing.Size(203, 140);
            this.manageLayoutPanel.TabIndex = 0;
            // 
            // editButton
            // 
            this.editButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.editButton.Image = global::DSPRE.Properties.Resources.RenameIcon;
            this.editButton.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.editButton.Location = new System.Drawing.Point(3, 108);
            this.editButton.Name = "editButton";
            this.editButton.Size = new System.Drawing.Size(197, 29);
            this.editButton.TabIndex = 51;
            this.editButton.Text = "Change Trigger";
            this.editButton.UseVisualStyleBackColor = true;
            this.editButton.Click += new System.EventHandler(this.editButton_Click);
            // 
            // deleteButton
            // 
            this.deleteButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.deleteButton.Image = global::DSPRE.Properties.Resources.deleteIcon;
            this.deleteButton.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.deleteButton.Location = new System.Drawing.Point(3, 38);
            this.deleteButton.Name = "deleteButton";
            this.deleteButton.Size = new System.Drawing.Size(197, 29);
            this.deleteButton.TabIndex = 1;
            this.deleteButton.Text = "Remove";
            this.deleteButton.UseVisualStyleBackColor = true;
            this.deleteButton.Click += new System.EventHandler(this.deleteButton_Click);
            // 
            // addButton
            // 
            this.addButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.addButton.Image = global::DSPRE.Properties.Resources.addIcon;
            this.addButton.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.addButton.Location = new System.Drawing.Point(3, 3);
            this.addButton.Name = "addButton";
            this.addButton.Size = new System.Drawing.Size(197, 29);
            this.addButton.TabIndex = 0;
            this.addButton.Text = "Add";
            this.addButton.UseVisualStyleBackColor = true;
            this.addButton.Click += new System.EventHandler(this.addButton_Click);
            // 
            // trainerComboBox
            // 
            this.trainerComboBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.trainerComboBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.trainerComboBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.trainerComboBox.FormattingEnabled = true;
            this.trainerComboBox.Location = new System.Drawing.Point(183, 48);
            this.trainerComboBox.Name = "trainerComboBox";
            this.trainerComboBox.Size = new System.Drawing.Size(336, 21);
            this.trainerComboBox.TabIndex = 49;
            this.trainerComboBox.SelectedIndexChanged += new System.EventHandler(this.trainerComboBox_SelectedIndexChanged);
            // 
            // triggerTypeComboBox
            // 
            this.triggerTypeComboBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.triggerTypeComboBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.triggerTypeComboBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.triggerTypeComboBox.FormattingEnabled = true;
            this.triggerTypeComboBox.Location = new System.Drawing.Point(3, 73);
            this.triggerTypeComboBox.Name = "triggerTypeComboBox";
            this.triggerTypeComboBox.Size = new System.Drawing.Size(197, 21);
            this.triggerTypeComboBox.TabIndex = 50;
            // 
            // dsTextBox
            // 
            this.mainLayoutPanel.SetColumnSpan(this.dsTextBox, 3);
            this.dsTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dsTextBox.Location = new System.Drawing.Point(3, 213);
            this.dsTextBox.Name = "dsTextBox";
            this.mainLayoutPanel.SetRowSpan(this.dsTextBox, 2);
            this.dsTextBox.Size = new System.Drawing.Size(516, 94);
            this.dsTextBox.TabIndex = 58;
            // 
            // BattleMessageEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(737, 331);
            this.Controls.Add(this.mainLayoutPanel);
            this.Name = "BattleMessageEditor";
            this.ShowIcon = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Trainer Message Editor";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.BattleMessageEditor_FormClosing);
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
        private System.Windows.Forms.Button editButton;
        private System.Windows.Forms.Button saveButton;
        private System.Windows.Forms.Button saveMessageButton;
        private System.Windows.Forms.Label infoLabel;
        private DSTextBox dsTextBox;
    }
}