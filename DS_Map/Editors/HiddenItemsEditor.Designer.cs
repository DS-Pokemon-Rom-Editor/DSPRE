namespace DSPRE.Editors
{
    partial class HiddenItemsEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HiddenItemsEditor));
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.labelInfo = new System.Windows.Forms.Label();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.groupBoxList = new System.Windows.Forms.GroupBox();
            this.listBoxHiddenItems = new System.Windows.Forms.ListBox();
            this.panelListButtons = new System.Windows.Forms.Panel();
            this.labelEntryCount = new System.Windows.Forms.Label();
            this.buttonRemove = new System.Windows.Forms.Button();
            this.buttonAdd = new System.Windows.Forms.Button();
            this.groupBoxDetails = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanelDetails = new System.Windows.Forms.TableLayoutPanel();
            this.labelItem = new System.Windows.Forms.Label();
            this.comboBoxItem = new System.Windows.Forms.ComboBox();
            this.labelAmount = new System.Windows.Forms.Label();
            this.numericAmount = new System.Windows.Forms.NumericUpDown();
            this.labelScriptIDLabel = new System.Windows.Forms.Label();
            this.numericScriptID = new System.Windows.Forms.NumericUpDown();
            this.labelScriptCall = new System.Windows.Forms.Label();
            this.labelHowTo = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.groupBoxList.SuspendLayout();
            this.panelListButtons.SuspendLayout();
            this.groupBoxDetails.SuspendLayout();
            this.tableLayoutPanelDetails.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericAmount)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericScriptID)).BeginInit();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.labelInfo, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.splitContainer1, 0, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1000, 600);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // labelInfo
            // 
            this.labelInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelInfo.Location = new System.Drawing.Point(3, 0);
            this.labelInfo.Name = "labelInfo";
            this.labelInfo.Padding = new System.Windows.Forms.Padding(5);
            this.labelInfo.Size = new System.Drawing.Size(994, 80);
            this.labelInfo.TabIndex = 0;
            this.labelInfo.Text = resources.GetString("labelInfo.Text");
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(3, 83);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.groupBoxList);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.groupBoxDetails);
            this.splitContainer1.Size = new System.Drawing.Size(994, 514);
            this.splitContainer1.SplitterDistance = 400;
            this.splitContainer1.TabIndex = 1;
            // 
            // groupBoxList
            // 
            this.groupBoxList.Controls.Add(this.listBoxHiddenItems);
            this.groupBoxList.Controls.Add(this.panelListButtons);
            this.groupBoxList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxList.Location = new System.Drawing.Point(0, 0);
            this.groupBoxList.Name = "groupBoxList";
            this.groupBoxList.Size = new System.Drawing.Size(400, 514);
            this.groupBoxList.TabIndex = 0;
            this.groupBoxList.TabStop = false;
            this.groupBoxList.Text = "Hidden Item Entries";
            // 
            // listBoxHiddenItems
            // 
            this.listBoxHiddenItems.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBoxHiddenItems.FormattingEnabled = true;
            this.listBoxHiddenItems.Location = new System.Drawing.Point(3, 16);
            this.listBoxHiddenItems.Name = "listBoxHiddenItems";
            this.listBoxHiddenItems.Size = new System.Drawing.Size(394, 465);
            this.listBoxHiddenItems.TabIndex = 0;
            this.listBoxHiddenItems.SelectedIndexChanged += new System.EventHandler(this.listBoxHiddenItems_SelectedIndexChanged);
            // 
            // panelListButtons
            // 
            this.panelListButtons.Controls.Add(this.labelEntryCount);
            this.panelListButtons.Controls.Add(this.buttonRemove);
            this.panelListButtons.Controls.Add(this.buttonAdd);
            this.panelListButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelListButtons.Location = new System.Drawing.Point(3, 481);
            this.panelListButtons.Name = "panelListButtons";
            this.panelListButtons.Size = new System.Drawing.Size(394, 30);
            this.panelListButtons.TabIndex = 1;
            // 
            // labelEntryCount
            // 
            this.labelEntryCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelEntryCount.Location = new System.Drawing.Point(200, 5);
            this.labelEntryCount.Name = "labelEntryCount";
            this.labelEntryCount.Size = new System.Drawing.Size(120, 20);
            this.labelEntryCount.TabIndex = 2;
            this.labelEntryCount.Text = "Entries: 0 / 256";
            this.labelEntryCount.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // buttonRemove
            // 
            this.buttonRemove.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonRemove.Location = new System.Drawing.Point(326, 3);
            this.buttonRemove.Name = "buttonRemove";
            this.buttonRemove.Size = new System.Drawing.Size(65, 23);
            this.buttonRemove.TabIndex = 1;
            this.buttonRemove.Text = "Remove";
            this.buttonRemove.UseVisualStyleBackColor = true;
            this.buttonRemove.Click += new System.EventHandler(this.buttonRemove_Click);
            // 
            // buttonAdd
            // 
            this.buttonAdd.Location = new System.Drawing.Point(3, 3);
            this.buttonAdd.Name = "buttonAdd";
            this.buttonAdd.Size = new System.Drawing.Size(65, 23);
            this.buttonAdd.TabIndex = 0;
            this.buttonAdd.Text = "Add";
            this.buttonAdd.UseVisualStyleBackColor = true;
            this.buttonAdd.Click += new System.EventHandler(this.buttonAdd_Click);
            // 
            // groupBoxDetails
            // 
            this.groupBoxDetails.Controls.Add(this.tableLayoutPanelDetails);
            this.groupBoxDetails.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxDetails.Location = new System.Drawing.Point(0, 0);
            this.groupBoxDetails.Name = "groupBoxDetails";
            this.groupBoxDetails.Size = new System.Drawing.Size(590, 514);
            this.groupBoxDetails.TabIndex = 0;
            this.groupBoxDetails.TabStop = false;
            this.groupBoxDetails.Text = "Entry Details";
            // 
            // tableLayoutPanelDetails
            // 
            this.tableLayoutPanelDetails.ColumnCount = 2;
            this.tableLayoutPanelDetails.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanelDetails.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelDetails.Controls.Add(this.labelItem, 0, 0);
            this.tableLayoutPanelDetails.Controls.Add(this.comboBoxItem, 1, 0);
            this.tableLayoutPanelDetails.Controls.Add(this.labelAmount, 0, 1);
            this.tableLayoutPanelDetails.Controls.Add(this.numericAmount, 1, 1);
            this.tableLayoutPanelDetails.Controls.Add(this.labelScriptIDLabel, 0, 2);
            this.tableLayoutPanelDetails.Controls.Add(this.numericScriptID, 1, 2);
            this.tableLayoutPanelDetails.Controls.Add(this.labelScriptCall, 0, 3);
            this.tableLayoutPanelDetails.Controls.Add(this.labelHowTo, 0, 4);
            this.tableLayoutPanelDetails.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanelDetails.Location = new System.Drawing.Point(3, 16);
            this.tableLayoutPanelDetails.Name = "tableLayoutPanelDetails";
            this.tableLayoutPanelDetails.RowCount = 5;
            this.tableLayoutPanelDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanelDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanelDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanelDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanelDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.tableLayoutPanelDetails.Size = new System.Drawing.Size(584, 200);
            this.tableLayoutPanelDetails.TabIndex = 0;
            // 
            // labelItem
            // 
            this.labelItem.AutoSize = true;
            this.labelItem.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelItem.Location = new System.Drawing.Point(3, 0);
            this.labelItem.Name = "labelItem";
            this.labelItem.Size = new System.Drawing.Size(94, 30);
            this.labelItem.TabIndex = 0;
            this.labelItem.Text = "Item:";
            this.labelItem.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboBoxItem
            // 
            this.comboBoxItem.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboBoxItem.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxItem.FormattingEnabled = true;
            this.comboBoxItem.Location = new System.Drawing.Point(103, 3);
            this.comboBoxItem.Name = "comboBoxItem";
            this.comboBoxItem.Size = new System.Drawing.Size(478, 21);
            this.comboBoxItem.TabIndex = 1;
            this.comboBoxItem.SelectedIndexChanged += new System.EventHandler(this.comboBoxItem_SelectedIndexChanged);
            // 
            // labelAmount
            // 
            this.labelAmount.AutoSize = true;
            this.labelAmount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelAmount.Location = new System.Drawing.Point(3, 30);
            this.labelAmount.Name = "labelAmount";
            this.labelAmount.Size = new System.Drawing.Size(94, 30);
            this.labelAmount.TabIndex = 2;
            this.labelAmount.Text = "Amount:";
            this.labelAmount.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // numericAmount
            // 
            this.numericAmount.Location = new System.Drawing.Point(103, 33);
            this.numericAmount.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.numericAmount.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericAmount.Name = "numericAmount";
            this.numericAmount.Size = new System.Drawing.Size(120, 20);
            this.numericAmount.TabIndex = 3;
            this.numericAmount.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericAmount.ValueChanged += new System.EventHandler(this.numericAmount_ValueChanged);
            // 
            // labelScriptIDLabel
            // 
            this.labelScriptIDLabel.AutoSize = true;
            this.labelScriptIDLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelScriptIDLabel.Location = new System.Drawing.Point(3, 60);
            this.labelScriptIDLabel.Name = "labelScriptIDLabel";
            this.labelScriptIDLabel.Size = new System.Drawing.Size(94, 30);
            this.labelScriptIDLabel.TabIndex = 4;
            this.labelScriptIDLabel.Text = "Script ID:";
            this.labelScriptIDLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // numericScriptID
            // 
            this.numericScriptID.Location = new System.Drawing.Point(103, 63);
            this.numericScriptID.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.numericScriptID.Name = "numericScriptID";
            this.numericScriptID.Size = new System.Drawing.Size(120, 20);
            this.numericScriptID.TabIndex = 5;
            this.numericScriptID.ValueChanged += new System.EventHandler(this.numericScriptID_ValueChanged);
            // 
            // labelScriptCall
            // 
            this.tableLayoutPanelDetails.SetColumnSpan(this.labelScriptCall, 2);
            this.labelScriptCall.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelScriptCall.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelScriptCall.ForeColor = System.Drawing.Color.DarkGreen;
            this.labelScriptCall.Location = new System.Drawing.Point(3, 90);
            this.labelScriptCall.Name = "labelScriptCall";
            this.labelScriptCall.Size = new System.Drawing.Size(578, 30);
            this.labelScriptCall.TabIndex = 6;
            this.labelScriptCall.Text = "Use in spawnable: 8095";
            this.labelScriptCall.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labelHowTo
            // 
            this.tableLayoutPanelDetails.SetColumnSpan(this.labelHowTo, 2);
            this.labelHowTo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelHowTo.Location = new System.Drawing.Point(3, 120);
            this.labelHowTo.Name = "labelHowTo";
            this.labelHowTo.Padding = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.labelHowTo.Size = new System.Drawing.Size(578, 80);
            this.labelHowTo.TabIndex = 7;
            this.labelHowTo.Text = "How to use:\r\n1. Set the Item, Amount, and Script ID for your hidden item.\r\n2. In " +
    "the Event Editor, add a Spawnable with type \"Hidden Item\".\r\n3. Set the Spawnable" +
    "\'s script number to 8XXX (shown above).";
            // 
            // HiddenItemsEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "HiddenItemsEditor";
            this.Size = new System.Drawing.Size(1000, 600);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.groupBoxList.ResumeLayout(false);
            this.panelListButtons.ResumeLayout(false);
            this.groupBoxDetails.ResumeLayout(false);
            this.tableLayoutPanelDetails.ResumeLayout(false);
            this.tableLayoutPanelDetails.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericAmount)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericScriptID)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label labelInfo;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.GroupBox groupBoxList;
        private System.Windows.Forms.ListBox listBoxHiddenItems;
        private System.Windows.Forms.Panel panelListButtons;
        private System.Windows.Forms.Button buttonRemove;
        private System.Windows.Forms.Button buttonAdd;
        private System.Windows.Forms.GroupBox groupBoxDetails;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelDetails;
        private System.Windows.Forms.Label labelItem;
        private System.Windows.Forms.ComboBox comboBoxItem;
        private System.Windows.Forms.Label labelAmount;
        private System.Windows.Forms.NumericUpDown numericAmount;
        private System.Windows.Forms.Label labelScriptIDLabel;
        private System.Windows.Forms.NumericUpDown numericScriptID;
        private System.Windows.Forms.Label labelScriptCall;
        private System.Windows.Forms.Label labelHowTo;
        private System.Windows.Forms.Label labelEntryCount;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
