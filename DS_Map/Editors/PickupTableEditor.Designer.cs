namespace DSPRE.Editors
{
    partial class PickupTableEditor
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PickupTableEditor));
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.groupBoxCommon = new System.Windows.Forms.GroupBox();
            this.dataGridViewCommon = new System.Windows.Forms.DataGridView();
            this.columnCommonLevel = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnCommonItem1 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.columnCommonItem2 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.columnCommonItem3 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.columnCommonItem4 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.columnCommonItem5 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.columnCommonItem6 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.columnCommonItem7 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.columnCommonItem8 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.columnCommonItem9 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.groupBoxRare = new System.Windows.Forms.GroupBox();
            this.dataGridViewRare = new System.Windows.Forms.DataGridView();
            this.columnRareLevel = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnRareItem1 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.columnRareItem2 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.panelTop = new System.Windows.Forms.Panel();
            this.buttonSave = new System.Windows.Forms.Button();
            this.labelInfo = new System.Windows.Forms.Label();
            this.groupBoxActivation = new System.Windows.Forms.GroupBox();
            this.dataGridViewActivation = new System.Windows.Forms.DataGridView();
            this.columnActivationSlot = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnActivationThreshold = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnActivationProbability = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnActivationRange = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel1.SuspendLayout();
            this.groupBoxCommon.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewCommon)).BeginInit();
            this.groupBoxRare.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewRare)).BeginInit();
            this.panelTop.SuspendLayout();
            this.groupBoxActivation.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewActivation)).BeginInit();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.groupBoxCommon, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.groupBoxRare, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.panelTop, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.groupBoxActivation, 0, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 4;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 200F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 55F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 45F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1000, 700);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // groupBoxCommon
            // 
            this.groupBoxCommon.Controls.Add(this.dataGridViewCommon);
            this.groupBoxCommon.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxCommon.Location = new System.Drawing.Point(3, 283);
            this.groupBoxCommon.Name = "groupBoxCommon";
            this.groupBoxCommon.Size = new System.Drawing.Size(994, 225);
            this.groupBoxCommon.TabIndex = 0;
            this.groupBoxCommon.TabStop = false;
            this.groupBoxCommon.Text = "Common Items (Slots 1-9)";
            // 
            // dataGridViewCommon
            // 
            this.dataGridViewCommon.AllowUserToAddRows = false;
            this.dataGridViewCommon.AllowUserToDeleteRows = false;
            this.dataGridViewCommon.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewCommon.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewCommon.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnCommonLevel,
            this.columnCommonItem1,
            this.columnCommonItem2,
            this.columnCommonItem3,
            this.columnCommonItem4,
            this.columnCommonItem5,
            this.columnCommonItem6,
            this.columnCommonItem7,
            this.columnCommonItem8,
            this.columnCommonItem9});
            this.dataGridViewCommon.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewCommon.Location = new System.Drawing.Point(3, 16);
            this.dataGridViewCommon.Name = "dataGridViewCommon";
            this.dataGridViewCommon.RowHeadersVisible = false;
            this.dataGridViewCommon.Size = new System.Drawing.Size(988, 206);
            this.dataGridViewCommon.TabIndex = 0;
            this.dataGridViewCommon.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewCommon_CellValueChanged);
            // 
            // columnCommonLevel
            // 
            this.columnCommonLevel.FillWeight = 60F;
            this.columnCommonLevel.HeaderText = "Level Range";
            this.columnCommonLevel.Name = "columnCommonLevel";
            this.columnCommonLevel.ReadOnly = true;
            // 
            // columnCommonItem1
            // 
            this.columnCommonItem1.HeaderText = "Slot 1";
            this.columnCommonItem1.Name = "columnCommonItem1";
            // 
            // columnCommonItem2
            // 
            this.columnCommonItem2.HeaderText = "Slot 2";
            this.columnCommonItem2.Name = "columnCommonItem2";
            // 
            // columnCommonItem3
            // 
            this.columnCommonItem3.HeaderText = "Slot 3";
            this.columnCommonItem3.Name = "columnCommonItem3";
            // 
            // columnCommonItem4
            // 
            this.columnCommonItem4.HeaderText = "Slot 4";
            this.columnCommonItem4.Name = "columnCommonItem4";
            // 
            // columnCommonItem5
            // 
            this.columnCommonItem5.HeaderText = "Slot 5";
            this.columnCommonItem5.Name = "columnCommonItem5";
            // 
            // columnCommonItem6
            // 
            this.columnCommonItem6.HeaderText = "Slot 6";
            this.columnCommonItem6.Name = "columnCommonItem6";
            // 
            // columnCommonItem7
            // 
            this.columnCommonItem7.HeaderText = "Slot 7";
            this.columnCommonItem7.Name = "columnCommonItem7";
            // 
            // columnCommonItem8
            // 
            this.columnCommonItem8.HeaderText = "Slot 8";
            this.columnCommonItem8.Name = "columnCommonItem8";
            // 
            // columnCommonItem9
            // 
            this.columnCommonItem9.HeaderText = "Slot 9";
            this.columnCommonItem9.Name = "columnCommonItem9";
            // 
            // groupBoxRare
            // 
            this.groupBoxRare.Controls.Add(this.dataGridViewRare);
            this.groupBoxRare.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxRare.Location = new System.Drawing.Point(3, 514);
            this.groupBoxRare.Name = "groupBoxRare";
            this.groupBoxRare.Size = new System.Drawing.Size(994, 183);
            this.groupBoxRare.TabIndex = 1;
            this.groupBoxRare.TabStop = false;
            this.groupBoxRare.Text = "Rare Items (Special Slots, 1% total)";
            // 
            // dataGridViewRare
            // 
            this.dataGridViewRare.AllowUserToAddRows = false;
            this.dataGridViewRare.AllowUserToDeleteRows = false;
            this.dataGridViewRare.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewRare.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewRare.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnRareLevel,
            this.columnRareItem1,
            this.columnRareItem2});
            this.dataGridViewRare.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewRare.Location = new System.Drawing.Point(3, 16);
            this.dataGridViewRare.Name = "dataGridViewRare";
            this.dataGridViewRare.RowHeadersVisible = false;
            this.dataGridViewRare.Size = new System.Drawing.Size(988, 164);
            this.dataGridViewRare.TabIndex = 0;
            this.dataGridViewRare.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewRare_CellValueChanged);
            // 
            // columnRareLevel
            // 
            this.columnRareLevel.FillWeight = 60F;
            this.columnRareLevel.HeaderText = "Level Range";
            this.columnRareLevel.Name = "columnRareLevel";
            this.columnRareLevel.ReadOnly = true;
            // 
            // columnRareItem1
            // 
            this.columnRareItem1.HeaderText = "Slot 1";
            this.columnRareItem1.Name = "columnRareItem1";
            // 
            // columnRareItem2
            // 
            this.columnRareItem2.HeaderText = "Slot 2";
            this.columnRareItem2.Name = "columnRareItem2";
            // 
            // panelTop
            // 
            this.panelTop.Controls.Add(this.buttonSave);
            this.panelTop.Controls.Add(this.labelInfo);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelTop.Location = new System.Drawing.Point(3, 3);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(994, 74);
            this.panelTop.TabIndex = 4;
            // 
            // buttonSave
            // 
            this.buttonSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSave.Location = new System.Drawing.Point(869, 20);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(120, 30);
            this.buttonSave.TabIndex = 3;
            this.buttonSave.Text = "Save All Changes";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // labelInfo
            // 
            this.labelInfo.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelInfo.Location = new System.Drawing.Point(0, 0);
            this.labelInfo.Name = "labelInfo";
            this.labelInfo.Padding = new System.Windows.Forms.Padding(5);
            this.labelInfo.Size = new System.Drawing.Size(863, 74);
            this.labelInfo.TabIndex = 2;
            this.labelInfo.Text = resources.GetString("labelInfo.Text");
            // 
            // groupBoxActivation
            // 
            this.groupBoxActivation.Controls.Add(this.dataGridViewActivation);
            this.groupBoxActivation.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxActivation.Location = new System.Drawing.Point(3, 83);
            this.groupBoxActivation.Name = "groupBoxActivation";
            this.groupBoxActivation.Size = new System.Drawing.Size(994, 194);
            this.groupBoxActivation.TabIndex = 3;
            this.groupBoxActivation.TabStop = false;
            this.groupBoxActivation.Text = "Pickup Activation Odds (How often each slot is chosen)";
            // 
            // dataGridViewActivation
            // 
            this.dataGridViewActivation.AllowUserToAddRows = false;
            this.dataGridViewActivation.AllowUserToDeleteRows = false;
            this.dataGridViewActivation.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewActivation.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewActivation.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnActivationSlot,
            this.columnActivationThreshold,
            this.columnActivationProbability,
            this.columnActivationRange});
            this.dataGridViewActivation.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewActivation.Location = new System.Drawing.Point(3, 16);
            this.dataGridViewActivation.Name = "dataGridViewActivation";
            this.dataGridViewActivation.RowHeadersVisible = false;
            this.dataGridViewActivation.Size = new System.Drawing.Size(988, 175);
            this.dataGridViewActivation.TabIndex = 0;
            this.toolTip1.SetToolTip(this.dataGridViewActivation, "Edit these values to change pickup activation odds. Must be ascending order.");
            this.dataGridViewActivation.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewActivation_CellValueChanged);
            // 
            // columnActivationSlot
            // 
            this.columnActivationSlot.FillWeight = 40F;
            this.columnActivationSlot.HeaderText = "Slot";
            this.columnActivationSlot.Name = "columnActivationSlot";
            this.columnActivationSlot.ReadOnly = true;
            // 
            // columnActivationThreshold
            // 
            this.columnActivationThreshold.FillWeight = 50F;
            this.columnActivationThreshold.HeaderText = "Threshold (0-100)";
            this.columnActivationThreshold.Name = "columnActivationThreshold";
            // 
            // columnActivationProbability
            // 
            this.columnActivationProbability.FillWeight = 50F;
            this.columnActivationProbability.HeaderText = "Real Probability";
            this.columnActivationProbability.Name = "columnActivationProbability";
            this.columnActivationProbability.ReadOnly = true;
            // 
            // columnActivationRange
            // 
            this.columnActivationRange.FillWeight = 60F;
            this.columnActivationRange.HeaderText = "Random Range";
            this.columnActivationRange.Name = "columnActivationRange";
            this.columnActivationRange.ReadOnly = true;
            // 
            // PickupTableEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "PickupTableEditor";
            this.Size = new System.Drawing.Size(1000, 700);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.groupBoxCommon.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewCommon)).EndInit();
            this.groupBoxRare.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewRare)).EndInit();
            this.panelTop.ResumeLayout(false);
            this.groupBoxActivation.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewActivation)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.GroupBox groupBoxCommon;
        private System.Windows.Forms.DataGridView dataGridViewCommon;
        private System.Windows.Forms.GroupBox groupBoxRare;
        private System.Windows.Forms.DataGridView dataGridViewRare;
        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.Label labelInfo;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnCommonLevel;
        private System.Windows.Forms.DataGridViewComboBoxColumn columnCommonItem1;
        private System.Windows.Forms.DataGridViewComboBoxColumn columnCommonItem2;
        private System.Windows.Forms.DataGridViewComboBoxColumn columnCommonItem3;
        private System.Windows.Forms.DataGridViewComboBoxColumn columnCommonItem4;
        private System.Windows.Forms.DataGridViewComboBoxColumn columnCommonItem5;
        private System.Windows.Forms.DataGridViewComboBoxColumn columnCommonItem6;
        private System.Windows.Forms.DataGridViewComboBoxColumn columnCommonItem7;
        private System.Windows.Forms.DataGridViewComboBoxColumn columnCommonItem8;
        private System.Windows.Forms.DataGridViewComboBoxColumn columnCommonItem9;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnRareLevel;
        private System.Windows.Forms.DataGridViewComboBoxColumn columnRareItem1;
        private System.Windows.Forms.DataGridViewComboBoxColumn columnRareItem2;
        private System.Windows.Forms.GroupBox groupBoxActivation;
        private System.Windows.Forms.DataGridView dataGridViewActivation;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnActivationSlot;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnActivationThreshold;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnActivationProbability;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnActivationRange;
    }
}
