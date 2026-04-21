namespace DSPRE.Editors
{
    partial class TrainerSearch
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
            this.operatorComboBox = new System.Windows.Forms.ComboBox();
            this.searchTextBox = new System.Windows.Forms.TextBox();
            this.searchButton = new System.Windows.Forms.Button();
            this.resetButton = new System.Windows.Forms.Button();
            this.goToButton = new System.Windows.Forms.Button();
            this.autoSearchCheckBox = new System.Windows.Forms.CheckBox();
            this.caseSensitiveCheckBox = new System.Windows.Forms.CheckBox();
            this.resultsListBox = new System.Windows.Forms.ListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // operatorComboBox
            // 
            this.operatorComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.operatorComboBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.operatorComboBox.FormattingEnabled = true;
            this.operatorComboBox.Location = new System.Drawing.Point(12, 82);
            this.operatorComboBox.Name = "operatorComboBox";
            this.operatorComboBox.Size = new System.Drawing.Size(190, 24);
            this.operatorComboBox.TabIndex = 1;
            this.operatorComboBox.SelectedIndexChanged += new System.EventHandler(this.operatorComboBox_SelectedIndexChanged);
            // 
            // searchTextBox
            // 
            this.searchTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.searchTextBox.Location = new System.Drawing.Point(12, 27);
            this.searchTextBox.Name = "searchTextBox";
            this.searchTextBox.Size = new System.Drawing.Size(283, 22);
            this.searchTextBox.TabIndex = 0;
            this.searchTextBox.KeyUp += new System.Windows.Forms.KeyEventHandler(this.searchTextBox_KeyUp);
            // 
            // searchButton
            // 
            this.searchButton.Image = global::DSPRE.Properties.Resources.wideLensImage;
            this.searchButton.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.searchButton.Location = new System.Drawing.Point(209, 75);
            this.searchButton.Name = "searchButton";
            this.searchButton.Size = new System.Drawing.Size(86, 37);
            this.searchButton.TabIndex = 2;
            this.searchButton.Text = "Search";
            this.searchButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.searchButton.UseVisualStyleBackColor = true;
            this.searchButton.Click += new System.EventHandler(this.searchButton_Click);
            // 
            // resetButton
            // 
            this.resetButton.Image = global::DSPRE.Properties.Resources.wideLensImageTransp;
            this.resetButton.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.resetButton.Location = new System.Drawing.Point(301, 11);
            this.resetButton.Name = "resetButton";
            this.resetButton.Size = new System.Drawing.Size(86, 37);
            this.resetButton.TabIndex = 4;
            this.resetButton.Text = "Reset";
            this.resetButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.resetButton.UseVisualStyleBackColor = true;
            this.resetButton.Click += new System.EventHandler(this.resetButton_Click);
            // 
            // goToButton
            // 
            this.goToButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.goToButton.Location = new System.Drawing.Point(301, 75);
            this.goToButton.Name = "goToButton";
            this.goToButton.Size = new System.Drawing.Size(86, 37);
            this.goToButton.TabIndex = 5;
            this.goToButton.Text = "Go To";
            this.goToButton.UseVisualStyleBackColor = true;
            this.goToButton.Click += new System.EventHandler(this.goToButton_Click);
            // 
            // autoSearchCheckBox
            // 
            this.autoSearchCheckBox.AutoSize = true;
            this.autoSearchCheckBox.Checked = true;
            this.autoSearchCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.autoSearchCheckBox.Location = new System.Drawing.Point(12, 112);
            this.autoSearchCheckBox.Name = "autoSearchCheckBox";
            this.autoSearchCheckBox.Size = new System.Drawing.Size(82, 17);
            this.autoSearchCheckBox.TabIndex = 3;
            this.autoSearchCheckBox.Text = "AutoSearch";
            this.autoSearchCheckBox.UseVisualStyleBackColor = true;
            // 
            // caseSensitiveCheckBox
            // 
            this.caseSensitiveCheckBox.AutoSize = true;
            this.caseSensitiveCheckBox.Location = new System.Drawing.Point(12, 132);
            this.caseSensitiveCheckBox.Name = "caseSensitiveCheckBox";
            this.caseSensitiveCheckBox.Size = new System.Drawing.Size(96, 17);
            this.caseSensitiveCheckBox.TabIndex = 9;
            this.caseSensitiveCheckBox.Text = "Case Sensitive";
            this.caseSensitiveCheckBox.UseVisualStyleBackColor = true;
            this.caseSensitiveCheckBox.CheckedChanged += new System.EventHandler(this.caseSensitiveCheckBox_CheckedChanged);
            // 
            // resultsListBox
            // 
            this.resultsListBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.resultsListBox.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.resultsListBox.FormattingEnabled = true;
            this.resultsListBox.ItemHeight = 14;
            this.resultsListBox.Location = new System.Drawing.Point(12, 154);
            this.resultsListBox.Name = "resultsListBox";
            this.resultsListBox.Size = new System.Drawing.Size(375, 214);
            this.resultsListBox.TabIndex = 6;
            this.resultsListBox.DoubleClick += new System.EventHandler(this.resultsListBox_DoubleClick);
            this.resultsListBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.resultsListBox_KeyDown);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(9, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(94, 15);
            this.label1.TabIndex = 7;
            this.label1.Text = "Trainer Name";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(9, 64);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(54, 15);
            this.label2.TabIndex = 8;
            this.label2.Text = "Criteria";
            // 
            // TrainerSearch
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(399, 381);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.resultsListBox);
            this.Controls.Add(this.caseSensitiveCheckBox);
            this.Controls.Add(this.autoSearchCheckBox);
            this.Controls.Add(this.goToButton);
            this.Controls.Add(this.resetButton);
            this.Controls.Add(this.searchButton);
            this.Controls.Add(this.searchTextBox);
            this.Controls.Add(this.operatorComboBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TrainerSearch";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Trainer Search";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ComboBox operatorComboBox;
        private System.Windows.Forms.TextBox searchTextBox;
        private System.Windows.Forms.Button searchButton;
        private System.Windows.Forms.Button resetButton;
        private System.Windows.Forms.Button goToButton;
        private System.Windows.Forms.CheckBox autoSearchCheckBox;
        private System.Windows.Forms.CheckBox caseSensitiveCheckBox;
        private System.Windows.Forms.ListBox resultsListBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
    }
}
