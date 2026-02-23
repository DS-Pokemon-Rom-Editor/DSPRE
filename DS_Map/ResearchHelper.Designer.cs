namespace DSPRE
{
    partial class ResearchHelper
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
            this.mainTabControl = new System.Windows.Forms.TabControl();
            this.scriptsTabPage = new System.Windows.Forms.TabPage();
            this.scriptsDataGridView = new System.Windows.Forms.DataGridView();
            this.colID = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTotal = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colScripts = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colFunctions = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colActions = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.searchPanel = new System.Windows.Forms.Panel();
            this.searchGroupBox = new System.Windows.Forms.GroupBox();
            this.searchValueNumericUpDown = new System.Windows.Forms.NumericUpDown();
            this.searchButton = new System.Windows.Forms.Button();
            this.clearSearchButton = new System.Windows.Forms.Button();
            this.comparisonGroupBox = new System.Windows.Forms.GroupBox();
            this.equalsRadioButton = new System.Windows.Forms.RadioButton();
            this.greaterThanOrEqualRadioButton = new System.Windows.Forms.RadioButton();
            this.lessThanOrEqualRadioButton = new System.Windows.Forms.RadioButton();
            this.columnGroupBox = new System.Windows.Forms.GroupBox();
            this.idRadioButton = new System.Windows.Forms.RadioButton();
            this.totalRadioButton = new System.Windows.Forms.RadioButton();
            this.scriptsRadioButton = new System.Windows.Forms.RadioButton();
            this.functionsRadioButton = new System.Windows.Forms.RadioButton();
            this.actionsRadioButton = new System.Windows.Forms.RadioButton();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.refreshButton = new System.Windows.Forms.Button();
            this.mainTabControl.SuspendLayout();
            this.scriptsTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.scriptsDataGridView)).BeginInit();
            this.searchPanel.SuspendLayout();
            this.searchGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.searchValueNumericUpDown)).BeginInit();
            this.comparisonGroupBox.SuspendLayout();
            this.columnGroupBox.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // mainTabControl
            // 
            this.mainTabControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.mainTabControl.Controls.Add(this.scriptsTabPage);
            this.mainTabControl.Location = new System.Drawing.Point(12, 12);
            this.mainTabControl.Name = "mainTabControl";
            this.mainTabControl.SelectedIndex = 0;
            this.mainTabControl.Size = new System.Drawing.Size(760, 498);
            this.mainTabControl.TabIndex = 0;
            // 
            // scriptsTabPage
            // 
            this.scriptsTabPage.Controls.Add(this.scriptsDataGridView);
            this.scriptsTabPage.Controls.Add(this.searchPanel);
            this.scriptsTabPage.Location = new System.Drawing.Point(4, 22);
            this.scriptsTabPage.Name = "scriptsTabPage";
            this.scriptsTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.scriptsTabPage.Size = new System.Drawing.Size(752, 472);
            this.scriptsTabPage.TabIndex = 0;
            this.scriptsTabPage.Text = "Scripts";
            this.scriptsTabPage.UseVisualStyleBackColor = true;
            // 
            // scriptsDataGridView
            // 
            this.scriptsDataGridView.AllowUserToAddRows = false;
            this.scriptsDataGridView.AllowUserToDeleteRows = false;
            this.scriptsDataGridView.AllowUserToResizeRows = false;
            this.scriptsDataGridView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.scriptsDataGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.scriptsDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.scriptsDataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colID,
            this.colTotal,
            this.colScripts,
            this.colFunctions,
            this.colActions});
            this.scriptsDataGridView.Location = new System.Drawing.Point(6, 6);
            this.scriptsDataGridView.MultiSelect = false;
            this.scriptsDataGridView.Name = "scriptsDataGridView";
            this.scriptsDataGridView.ReadOnly = true;
            this.scriptsDataGridView.RowHeadersVisible = false;
            this.scriptsDataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.scriptsDataGridView.Size = new System.Drawing.Size(518, 460);
            this.scriptsDataGridView.TabIndex = 0;
            this.scriptsDataGridView.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.scriptsDataGridView_CellDoubleClick);
            // 
            // colID
            // 
            this.colID.HeaderText = "ID";
            this.colID.Name = "colID";
            this.colID.ReadOnly = true;
            this.colID.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // colTotal
            // 
            this.colTotal.HeaderText = "Total";
            this.colTotal.Name = "colTotal";
            this.colTotal.ReadOnly = true;
            // 
            // colScripts
            // 
            this.colScripts.HeaderText = "Scripts";
            this.colScripts.Name = "colScripts";
            this.colScripts.ReadOnly = true;
            // 
            // colFunctions
            // 
            this.colFunctions.HeaderText = "Functions";
            this.colFunctions.Name = "colFunctions";
            this.colFunctions.ReadOnly = true;
            // 
            // colActions
            // 
            this.colActions.HeaderText = "Actions";
            this.colActions.Name = "colActions";
            this.colActions.ReadOnly = true;
            // 
            // searchPanel
            // 
            this.searchPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.searchPanel.Controls.Add(this.refreshButton);
            this.searchPanel.Controls.Add(this.searchGroupBox);
            this.searchPanel.Location = new System.Drawing.Point(530, 6);
            this.searchPanel.Name = "searchPanel";
            this.searchPanel.Size = new System.Drawing.Size(216, 460);
            this.searchPanel.TabIndex = 1;
            // 
            // searchGroupBox
            // 
            this.searchGroupBox.Controls.Add(this.searchValueNumericUpDown);
            this.searchGroupBox.Controls.Add(this.searchButton);
            this.searchGroupBox.Controls.Add(this.clearSearchButton);
            this.searchGroupBox.Controls.Add(this.comparisonGroupBox);
            this.searchGroupBox.Controls.Add(this.columnGroupBox);
            this.searchGroupBox.Dock = System.Windows.Forms.DockStyle.Top;
            this.searchGroupBox.Location = new System.Drawing.Point(0, 0);
            this.searchGroupBox.Name = "searchGroupBox";
            this.searchGroupBox.Size = new System.Drawing.Size(216, 290);
            this.searchGroupBox.TabIndex = 0;
            this.searchGroupBox.TabStop = false;
            this.searchGroupBox.Text = "Search / Filter";
            // 
            // searchValueNumericUpDown
            // 
            this.searchValueNumericUpDown.Location = new System.Drawing.Point(6, 222);
            this.searchValueNumericUpDown.Maximum = new decimal(new int[] {
            99999,
            0,
            0,
            0});
            this.searchValueNumericUpDown.Name = "searchValueNumericUpDown";
            this.searchValueNumericUpDown.Size = new System.Drawing.Size(204, 20);
            this.searchValueNumericUpDown.TabIndex = 2;
            // 
            // searchButton
            // 
            this.searchButton.Location = new System.Drawing.Point(6, 248);
            this.searchButton.Name = "searchButton";
            this.searchButton.Size = new System.Drawing.Size(99, 30);
            this.searchButton.TabIndex = 3;
            this.searchButton.Text = "Search";
            this.searchButton.UseVisualStyleBackColor = true;
            this.searchButton.Click += new System.EventHandler(this.searchButton_Click);
            // 
            // clearSearchButton
            // 
            this.clearSearchButton.Location = new System.Drawing.Point(111, 248);
            this.clearSearchButton.Name = "clearSearchButton";
            this.clearSearchButton.Size = new System.Drawing.Size(99, 30);
            this.clearSearchButton.TabIndex = 4;
            this.clearSearchButton.Text = "Clear";
            this.clearSearchButton.UseVisualStyleBackColor = true;
            this.clearSearchButton.Click += new System.EventHandler(this.clearSearchButton_Click);
            // 
            // comparisonGroupBox
            // 
            this.comparisonGroupBox.Controls.Add(this.equalsRadioButton);
            this.comparisonGroupBox.Controls.Add(this.greaterThanOrEqualRadioButton);
            this.comparisonGroupBox.Controls.Add(this.lessThanOrEqualRadioButton);
            this.comparisonGroupBox.Location = new System.Drawing.Point(6, 134);
            this.comparisonGroupBox.Name = "comparisonGroupBox";
            this.comparisonGroupBox.Size = new System.Drawing.Size(204, 82);
            this.comparisonGroupBox.TabIndex = 1;
            this.comparisonGroupBox.TabStop = false;
            this.comparisonGroupBox.Text = "Comparison";
            // 
            // equalsRadioButton
            // 
            this.equalsRadioButton.AutoSize = true;
            this.equalsRadioButton.Checked = true;
            this.equalsRadioButton.Location = new System.Drawing.Point(6, 19);
            this.equalsRadioButton.Name = "equalsRadioButton";
            this.equalsRadioButton.Size = new System.Drawing.Size(58, 17);
            this.equalsRadioButton.TabIndex = 0;
            this.equalsRadioButton.TabStop = true;
            this.equalsRadioButton.Text = "Equals";
            this.equalsRadioButton.UseVisualStyleBackColor = true;
            // 
            // greaterThanOrEqualRadioButton
            // 
            this.greaterThanOrEqualRadioButton.AutoSize = true;
            this.greaterThanOrEqualRadioButton.Location = new System.Drawing.Point(6, 38);
            this.greaterThanOrEqualRadioButton.Name = "greaterThanOrEqualRadioButton";
            this.greaterThanOrEqualRadioButton.Size = new System.Drawing.Size(127, 17);
            this.greaterThanOrEqualRadioButton.TabIndex = 1;
            this.greaterThanOrEqualRadioButton.Text = "Greater Than or Equal";
            this.greaterThanOrEqualRadioButton.UseVisualStyleBackColor = true;
            // 
            // lessThanOrEqualRadioButton
            // 
            this.lessThanOrEqualRadioButton.AutoSize = true;
            this.lessThanOrEqualRadioButton.Location = new System.Drawing.Point(6, 57);
            this.lessThanOrEqualRadioButton.Name = "lessThanOrEqualRadioButton";
            this.lessThanOrEqualRadioButton.Size = new System.Drawing.Size(115, 17);
            this.lessThanOrEqualRadioButton.TabIndex = 2;
            this.lessThanOrEqualRadioButton.Text = "Less Than or Equal";
            this.lessThanOrEqualRadioButton.UseVisualStyleBackColor = true;
            // 
            // columnGroupBox
            // 
            this.columnGroupBox.Controls.Add(this.idRadioButton);
            this.columnGroupBox.Controls.Add(this.totalRadioButton);
            this.columnGroupBox.Controls.Add(this.scriptsRadioButton);
            this.columnGroupBox.Controls.Add(this.functionsRadioButton);
            this.columnGroupBox.Controls.Add(this.actionsRadioButton);
            this.columnGroupBox.Location = new System.Drawing.Point(6, 19);
            this.columnGroupBox.Name = "columnGroupBox";
            this.columnGroupBox.Size = new System.Drawing.Size(204, 109);
            this.columnGroupBox.TabIndex = 0;
            this.columnGroupBox.TabStop = false;
            this.columnGroupBox.Text = "Search Column";
            // 
            // idRadioButton
            // 
            this.idRadioButton.AutoSize = true;
            this.idRadioButton.Checked = true;
            this.idRadioButton.Location = new System.Drawing.Point(6, 19);
            this.idRadioButton.Name = "idRadioButton";
            this.idRadioButton.Size = new System.Drawing.Size(35, 17);
            this.idRadioButton.TabIndex = 0;
            this.idRadioButton.TabStop = true;
            this.idRadioButton.Text = "ID";
            this.idRadioButton.UseVisualStyleBackColor = true;
            // 
            // totalRadioButton
            // 
            this.totalRadioButton.AutoSize = true;
            this.totalRadioButton.Location = new System.Drawing.Point(6, 37);
            this.totalRadioButton.Name = "totalRadioButton";
            this.totalRadioButton.Size = new System.Drawing.Size(48, 17);
            this.totalRadioButton.TabIndex = 1;
            this.totalRadioButton.Text = "Total";
            this.totalRadioButton.UseVisualStyleBackColor = true;
            // 
            // scriptsRadioButton
            // 
            this.scriptsRadioButton.AutoSize = true;
            this.scriptsRadioButton.Location = new System.Drawing.Point(6, 55);
            this.scriptsRadioButton.Name = "scriptsRadioButton";
            this.scriptsRadioButton.Size = new System.Drawing.Size(56, 17);
            this.scriptsRadioButton.TabIndex = 2;
            this.scriptsRadioButton.Text = "Scripts";
            this.scriptsRadioButton.UseVisualStyleBackColor = true;
            // 
            // functionsRadioButton
            // 
            this.functionsRadioButton.AutoSize = true;
            this.functionsRadioButton.Location = new System.Drawing.Point(6, 73);
            this.functionsRadioButton.Name = "functionsRadioButton";
            this.functionsRadioButton.Size = new System.Drawing.Size(71, 17);
            this.functionsRadioButton.TabIndex = 3;
            this.functionsRadioButton.Text = "Functions";
            this.functionsRadioButton.UseVisualStyleBackColor = true;
            // 
            // actionsRadioButton
            // 
            this.actionsRadioButton.AutoSize = true;
            this.actionsRadioButton.Location = new System.Drawing.Point(6, 91);
            this.actionsRadioButton.Name = "actionsRadioButton";
            this.actionsRadioButton.Size = new System.Drawing.Size(60, 17);
            this.actionsRadioButton.TabIndex = 4;
            this.actionsRadioButton.Text = "Actions";
            this.actionsRadioButton.UseVisualStyleBackColor = true;
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 513);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(784, 22);
            this.statusStrip.TabIndex = 1;
            this.statusStrip.Text = "statusStrip1";
            // 
            // statusLabel
            // 
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(39, 17);
            this.statusLabel.Text = "Ready";
            // 
            // refreshButton
            // 
            this.refreshButton.Location = new System.Drawing.Point(6, 296);
            this.refreshButton.Name = "refreshButton";
            this.refreshButton.Size = new System.Drawing.Size(204, 30);
            this.refreshButton.TabIndex = 5;
            this.refreshButton.Text = "Refresh Data";
            this.refreshButton.UseVisualStyleBackColor = true;
            this.refreshButton.Click += new System.EventHandler(this.refreshButton_Click);
            // 
            // ResearchHelper
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 535);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.mainTabControl);
            this.MinimumSize = new System.Drawing.Size(600, 400);
            this.Name = "ResearchHelper";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Research Helper";
            this.Load += new System.EventHandler(this.ResearchHelper_Load);
            this.mainTabControl.ResumeLayout(false);
            this.scriptsTabPage.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.scriptsDataGridView)).EndInit();
            this.searchPanel.ResumeLayout(false);
            this.searchGroupBox.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.searchValueNumericUpDown)).EndInit();
            this.comparisonGroupBox.ResumeLayout(false);
            this.comparisonGroupBox.PerformLayout();
            this.columnGroupBox.ResumeLayout(false);
            this.columnGroupBox.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TabControl mainTabControl;
        private System.Windows.Forms.TabPage scriptsTabPage;
        private System.Windows.Forms.DataGridView scriptsDataGridView;
        private System.Windows.Forms.DataGridViewTextBoxColumn colID;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTotal;
        private System.Windows.Forms.DataGridViewTextBoxColumn colScripts;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFunctions;
        private System.Windows.Forms.DataGridViewTextBoxColumn colActions;
        private System.Windows.Forms.Panel searchPanel;
        private System.Windows.Forms.GroupBox searchGroupBox;
        private System.Windows.Forms.NumericUpDown searchValueNumericUpDown;
        private System.Windows.Forms.Button searchButton;
        private System.Windows.Forms.Button clearSearchButton;
        private System.Windows.Forms.GroupBox comparisonGroupBox;
        private System.Windows.Forms.RadioButton equalsRadioButton;
        private System.Windows.Forms.RadioButton greaterThanOrEqualRadioButton;
        private System.Windows.Forms.RadioButton lessThanOrEqualRadioButton;
        private System.Windows.Forms.GroupBox columnGroupBox;
        private System.Windows.Forms.RadioButton idRadioButton;
        private System.Windows.Forms.RadioButton totalRadioButton;
        private System.Windows.Forms.RadioButton scriptsRadioButton;
        private System.Windows.Forms.RadioButton functionsRadioButton;
        private System.Windows.Forms.RadioButton actionsRadioButton;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
        private System.Windows.Forms.Button refreshButton;
    }
}
